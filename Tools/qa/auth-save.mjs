// End-to-end identity race through the actual settings/sign-in UI and a controlled local mock SDK.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const url = new URL(process.env.URL || 'http://127.0.0.1:8795/?sdk=mock');
assert(['localhost', '127.0.0.1'].includes(url.hostname) && url.searchParams.get('sdk') === 'mock');
const output = process.env.OUT || '/tmp/cars-polish-20261024/auth-save';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await context.addInitScript(() => {
    const probe = window.__saveProbe = { oldLoads: 0, newLoads: 0, writes: [], oldStarted: 0, releasedAt: 0 };
    const save = (deviceId, coinsEarned) => JSON.stringify({ version: 5, progress: { ledger: [{ deviceId, coinsEarned }] } });
    let wrapped;
    Object.defineProperty(window, 'CarsMockSdk', { configurable: true, get: () => wrapped, set(value) {
      wrapped = { ...value, init: async () => {
        const sdk = await value.init(), previous = await sdk.getPlayer();
        let signedIn = false;
        previous.getData = () => {
          probe.oldLoads++;
          if (probe.oldLoads === 1) return Promise.reject(new Error('controlled initial offline'));
          probe.oldStarted = Date.now();
          return new Promise(resolve => { probe.release = () => { probe.releasedAt = Date.now(); resolve({ save: save('old-account', 900) }); }; });
        };
        const current = { ...previous, getUniqueID: () => 'new-account', getName: () => 'New account', isAuthorized: () => true,
          getData: () => { probe.newLoads++; return Promise.resolve({ save: save('new-account', 200) }); },
          setData: data => { probe.writes.push(JSON.parse(data.save)); return Promise.resolve(); } };
        sdk.getPlayer = () => Promise.resolve(signedIn ? current : previous);
        sdk.auth.openAuthDialog = () => { signedIn = true; return Promise.resolve(); };
        return sdk;
      } };
    } });
  });
  page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(url.href);
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(200);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  const click = async name => {
    for (let i = 0; i < 20; i++) {
      const b = (await cmd('ui')).buttons.find(b => b.name === name); assert(b?.interactable, name);
      const box = await page.locator('canvas').boundingBox();
      if (b.y > .2 && b.y < .9) {
        await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height);
        await page.waitForTimeout(250); return;
      }
      await page.mouse.move(900, 500); await page.mouse.wheel(0, b.y > .5 ? 250 : -250); await page.waitForTimeout(150);
    }
    throw new Error(`Cannot reach ${name}`);
  };
  await cmd('goto', 'settings');
  await click('SoundOff'); // A real local change asks the save service to retry the failed cloud read.
  await page.waitForFunction(() => window.__saveProbe.oldLoads >= 2, null, { timeout: 20000 });
  await cmd('goto', 'leaders');
  await click('SignIn');
  await page.waitForFunction(() => window.CarsPlatform.env().playerId === 'new-account');
  await page.waitForTimeout(350); // Deliver the auth reply and C# identity event before releasing the old response.
  const elapsed = await page.evaluate(() => Date.now() - window.__saveProbe.oldStarted);
  assert(elapsed < 7000, 'Release the stale response before the adapter timeout; otherwise this would not exercise the C# guard');
  await page.evaluate(() => window.__saveProbe.release());
  await page.waitForFunction(() => window.__saveProbe.writes.length > 0, null, { timeout: 15000 });
  const state = await cmd('state'), probe = await page.evaluate(() => {
    const { release, ...data } = window.__saveProbe; return data;
  });
  assert(probe.newLoads > 0, 'The signed-in account must be read before a write');
  assert.equal(state.coins, 200, 'The delayed 900-coin response must not contaminate the new account');
  for (const write of probe.writes) {
    assert(!write.progress.ledger.some(row => row.deviceId === 'old-account'));
    assert.equal(write.progress.ledger.reduce((sum, row) => sum + row.coinsEarned, 0), 200);
  }
  assert.deepEqual(errors, []);
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, state, probe, errors }, null, 2));
  console.log('AUTH SAVE PASS', probe.oldLoads, 'old reads;', probe.newLoads, 'new reads;', probe.writes.length, 'safe writes');
} catch (error) {
  if (page && !page.isClosed()) {
    await page.screenshot({ path: `${output}/failure.png` }).catch(() => {});
    await fs.writeFile(`${output}/failure.json`, JSON.stringify({ error: String(error),
      probe: await page.evaluate(() => { const { release, ...data } = window.__saveProbe; return data; }).catch(() => null),
      state: await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null), errors }, null, 2));
  }
  throw error;
} finally { await browser.close(); }
