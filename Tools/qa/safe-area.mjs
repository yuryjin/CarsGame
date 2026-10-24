// Chrome's safe-area emulation exercises actual env() insets and Unity input coordinates.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-endurance-20261024/safe-area';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  const context = await browser.newContext({ viewport: { width: 812, height: 375 }, hasTouch: true, isMobile: true, deviceScaleFactor: 3,
    userAgent: `Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile/15E148 Safari/604.1` });
  const page = await context.newPage(), cdp = await context.newCDPSession(page);
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: { left: 44, right: 44, top: 0, bottom: 21 } });
  await page.goto(process.env.URL || 'http://127.0.0.1:8801/?sdk=mock&device=mobile');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(200);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  const measure = async name => {
    const result = await page.evaluate(() => {
      const canvas = document.querySelector('canvas'), r = canvas.getBoundingClientRect();
      const probe = document.createElement('div');
      probe.style.cssText = 'padding:env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left)';
      document.body.appendChild(probe); const style = getComputedStyle(probe);
      const safe = { top: parseFloat(style.paddingTop), right: parseFloat(style.paddingRight), bottom: parseFloat(style.paddingBottom), left: parseFloat(style.paddingLeft) };
      probe.remove();
      const gl = canvas.getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
      return { safe, box: { x: r.x, y: r.y, width: r.width, height: r.height }, renderWidth: canvas.width,
        viewport: { width: innerWidth, height: innerHeight }, renderer: gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER) };
    });
    assert.match(result.renderer, /NVIDIA/i); evidence.push({ name, ...result }); return result;
  };
  const assertSafe = r => {
    assert(Math.abs(r.box.x - r.safe.left) < 1 && Math.abs(r.box.y - r.safe.top) < 1, 'Canvas begins inside safe edges');
    assert(Math.abs(r.box.width - (r.viewport.width - r.safe.left - r.safe.right)) < 1, 'Canvas width excludes the notch');
    assert(Math.abs(r.box.height - (r.viewport.height - r.safe.top - r.safe.bottom)) < 1, 'Canvas height excludes the home indicator');
    assert(Math.abs(r.renderWidth - r.box.width) < 2, 'Dense phone rendering remains capped at CSS resolution');
  };
  if (process.env.CG_SAFE_CSS) {
    const baseline = await measure('before-css');
    assert.equal(baseline.safe.left, 44, 'Control: browser really exposes safe-area env values');
    assert(baseline.box.x < baseline.safe.left, 'Control: original canvas ignored the safe area');
    await page.addStyleTag({ content: await fs.readFile(process.env.CG_SAFE_CSS, 'utf8') });
    await page.waitForTimeout(700);
  }
  assertSafe(await measure('landscape-notch'));
  await cmd('touch', 'on'); await cmd('startRace', 'e1', 'easy');
  for (let i = 0; i < 70 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
  const click = async name => {
    const state = await cmd('ui'), button = state.buttons.find(b => b.name === name && b.interactable);
    assert(button, `Missing ${name}`);
    const box = await page.locator('canvas').boundingBox();
    await page.mouse.click(box.x + button.x * box.width, box.y + button.y * box.height);
    await page.waitForTimeout(300);
  };
  await click('PauseButton');
  assert((await cmd('ui')).buttons.some(b => b.name === 'Resume'), 'Input coordinates must follow the inset canvas');
  const paused = await cmd('state'); await page.waitForTimeout(500);
  assert(Math.abs((await cmd('state')).raceSeconds - paused.raceSeconds) < .001);
  await page.screenshot({ path: `${out}/landscape-pause.png`, scale: 'css' });
  await click('Resume'); await page.waitForTimeout(400);
  assert((await cmd('state')).raceSeconds > paused.raceSeconds + .2);
  await page.setViewportSize({ width: 375, height: 812 });
  await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: { left: 0, right: 0, top: 47, bottom: 34 } });
  await page.waitForTimeout(700); assertSafe(await measure('portrait-notch'));
  const portrait = await cmd('state'); await page.waitForTimeout(500);
  assert(Math.abs((await cmd('state')).raceSeconds - portrait.raceSeconds) < .001, 'Portrait orientation remains paused');
  await page.setViewportSize({ width: 812, height: 375 });
  await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: { left: 0, right: 44, top: 0, bottom: 21 } });
  await page.waitForTimeout(700); assertSafe(await measure('opposite-notch'));
  assert((await cmd('state')).raceSeconds > portrait.raceSeconds + .2);
  await cdp.send('Emulation.setSafeAreaInsetsOverride', { insets: { left: 0, right: 0, top: 0, bottom: 0 } });
  await page.waitForTimeout(700); assertSafe(await measure('no-insets'));
  assert.deepEqual(errors, []); console.log('SAFE AREA PASS');
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ cssOverride: process.env.CG_SAFE_CSS || null, evidence, errors }, null, 2));
  await browser.close();
}
