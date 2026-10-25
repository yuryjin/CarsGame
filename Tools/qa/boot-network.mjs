// Actual transport failures, accessible retry and a throttled cold start of the built WebGL game.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const url = new URL(process.env.URL);
assert(['localhost', '127.0.0.1'].includes(url.hostname) && url.searchParams.get('sdk') === 'mock');
const out = process.env.OUT; await fs.mkdir(out, { recursive: true });
const report = { failures: [], locales: [], coldStart: {}, outcome: 'running' };
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const ready = page => page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
async function state(page, ...args) {
  await page.evaluate(a => window.CarsPlatform.cmd(...a), args.length ? args : ['state']); await page.waitForTimeout(180);
  const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
  assert.equal(s.error || '', ''); return s;
}
async function checkRetry(page, expected) {
  const button = page.locator('#cars-retry-btn'); await button.waitFor({ state: 'visible', timeout: 60000 });
  assert.equal(await button.textContent(), expected);
  const box = await button.boundingBox(), viewport = page.viewportSize();
  assert(box.width >= 44 && box.height >= 44 && box.x >= 0 && box.y >= 0 && box.x + box.width <= viewport.width && box.y + box.height <= viewport.height, 'Retry fits the viewport and touch target');
  assert(await button.evaluate(el => document.activeElement === el), 'Keyboard focus reaches retry');
  assert.equal(await page.locator('#cars-loading').evaluate(el => getComputedStyle(el).display), 'none');
  assert.equal(await page.locator('#cars-retry').count(), 1);
  return { text: await page.locator('#cars-retry').innerText(), button: box };
}
try {
  for (const failure of ['adapter', 'loader', 'framework', 'data', 'wasm']) {
    const context = await browser.newContext({ viewport: { width: 640, height: 360 }, locale: 'en' });
    try {
      const page = await context.newPage(), events = []; let blocked = 0, phase = 'injected';
      page.on('pageerror', e => events.push({ phase, error: e.message }));
      page.on('dialog', async dialog => { events.push({ phase, dialog: dialog.message() }); await dialog.dismiss(); });
      await page.route('**/*', route => {
        const path = new URL(route.request().url()).pathname;
        const matches = failure === 'adapter' ? path.endsWith('/cars-platform.js') : path.includes('/Build/') && path.includes(`.${failure}.`);
        if (!matches) return route.continue();
        blocked++;
        if (failure === 'framework') return route.fulfill({ status: 200, contentType: 'application/javascript', body: '/* Deliberately corrupt framework: no Unity entry point. */' });
        return route.abort('failed');
      });
      const local = new URL(url); local.searchParams.set('lang', 'en');
      await page.goto(local.href);
      const overlay = await checkRetry(page, 'Try again'); assert(blocked > 0, 'The intended request was intercepted');
      if (failure === 'loader') await page.screenshot({ path: `${out}/retry-en.png` });
      await page.unroute('**/*'); phase = 'retry';
      await page.keyboard.press('Enter', { delay: 80 }); await ready(page);
      const result = await state(page);
      assert.equal(result.flow, 'Menu'); assert.equal(await page.locator('#cars-retry').count(), 0);
      assert(!events.some(e => e.phase === 'retry'), 'Retry starts without uncaught errors or dialogs');
      report.failures.push({ failure, blocked, overlay, events, recovered: true });
      console.log('RECOVERED', failure);
    } finally { await context.close(); }
  }
  const labels = { ru: 'Повторить', en: 'Try again', de: 'Erneut versuchen', es: 'Reintentar', fr: 'Réessayer', uk: 'Спробувати ще раз', pt: 'Tentar novamente', tr: 'Tekrar dene', zh: '重试' };
  for (const viewport of [{ width: 320, height: 568 }, { width: 640, height: 360 }]) {
    const context = await browser.newContext({ viewport, hasTouch: true, isMobile: true });
    try {
      const page = await context.newPage();
      await page.route('**/*.loader.js', route => route.fulfill({ status: 200, contentType: 'application/javascript', body: 'window.createUnityInstance=function(){return Promise.reject(new Error("Injected boot failure"));};' }));
      for (const [language, label] of Object.entries(labels)) {
        const local = new URL(url); local.searchParams.set('lang', language); await page.goto(local.href);
        const overlay = await checkRetry(page, label);
        const bounds = await page.locator('#cars-retry > div').boundingBox();
        assert(bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= viewport.width && bounds.y + bounds.height <= viewport.height, 'The complete message fits');
        report.locales.push({ language, viewport, ...overlay });
        if (['ru', 'zh'].includes(language) && viewport.width === 320) await page.screenshot({ path: `${out}/retry-${language}.png` });
      }
    } finally { await context.close(); }
  }
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, locale: 'en' });
  try {
    const page = await context.newPage(), errors = []; page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (/Exception|OUT_OF_MEMORY|\[Loc\] missing key/.test(m.text())) errors.push(m.text()); });
    const cdp = await context.newCDPSession(page);
    await cdp.send('Network.enable'); await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });
    await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 80, downloadThroughput: 1500000, uploadThroughput: 750000 });
    const started = Date.now(); await page.goto(url.href);
    await page.waitForTimeout(1500); await page.screenshot({ path: `${out}/cold-loading.png` });
    await ready(page); report.coldStart.readyMilliseconds = Date.now() - started;
    report.coldStart.renderer = await page.evaluate(() => { const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info'); return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER); });
    assert.match(report.coldStart.renderer, /NVIDIA/i);
    assert.equal(await page.locator('#cars-retry').count(), 0);
    await cdp.send('Network.emulateNetworkConditions', { offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1 });
    const initial = report.coldStart.initial = await state(page);
    await state(page, 'startRace', 'e1', 'easy');
    for (let i = 0; i < 100 && (await state(page)).race !== 'Racing'; i++) await page.waitForTimeout(100);
    assert.equal((await state(page)).race, 'Racing'); await state(page, 'autopilot', 'on');
    const deadline = Date.now() + 300000; let result;
    while (Date.now() < deadline) {
      result = await state(page); if (result.flow === 'Results') break;
      if (result.race === 'Finishing') {
        const ui = await state(page, 'ui'), b = ui.buttons.find(b => b.name === 'FinishResults' && b.interactable);
        if (b) { const box = await page.locator('canvas').boundingBox(); await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height); }
      }
      await page.waitForTimeout(1000);
    }
    assert.equal(result.flow, 'Results'); assert(result.lap >= result.laps);
    assert.equal(result.races, initial.races + 1); assert.equal(result.coins, initial.coins + result.lastReward);
    report.coldStart.result = result;
    await state(page, 'goto', 'main'); await page.waitForTimeout(1600); await page.reload(); await ready(page);
    const restored = report.coldStart.restored = await state(page);
    assert.equal(restored.coins, result.coins); assert.equal(restored.races, result.races); assert.deepEqual(errors, []);
    report.coldStart.errors = errors;
  } finally { await context.close(); }
  report.outcome = 'passed'; console.log('BOOT NETWORK PASS', report.failures.length, 'transport recoveries;', report.locales.length, 'layouts; cold race and reload');
} catch (error) { report.outcome = 'failed'; report.failure = String(error); throw error; }
finally { await fs.writeFile(`${out}/report.json`, JSON.stringify(report, null, 2)); await browser.close(); }
