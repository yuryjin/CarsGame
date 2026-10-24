// Actual keyboard input and browser visibility callbacks; no finish/recovery injection.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-endurance-20261024/pause-browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [], failures = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await context.addInitScript(() => localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify({ version: 5, updatedAtUnixMs: Date.now(), settings: { lang: 'en', hintsSeen: 65535 } }) })));
  page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8801/?sdk=mock');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1000);
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i); evidence.push({ renderer });
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(120);
    const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
    assert.equal(s.error || '', ''); return s;
  };
  const visible = async hidden => {
    await page.evaluate(hidden => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden }); document.dispatchEvent(new Event('visibilitychange')); }, hidden);
    await page.waitForTimeout(250);
  };
  const waitRacing = async () => {
    for (let i = 0; i < 80; i++) {
      if ((await cmd('state')).race === 'Racing') return;
      await page.waitForTimeout(100);
    }
    throw new Error('Countdown did not resume');
  };
  const check = async (name, action) => {
    try { await action(); console.log('PASS', name); }
    catch (error) { failures.push({ name, error: String(error) }); console.log('FAIL', name, String(error)); await page.screenshot({ path: `${out}/${name}.png` }); }
    finally { await visible(false); await cmd('goto', 'main'); }
  };
  await check('paused-reset', async () => {
    await cmd('startRace', 'e1', 'easy'); await waitRacing();
    await page.keyboard.press('Escape'); await page.waitForTimeout(200);
    const before = await cmd('ui');
    assert(before.buttons.some(b => b.name === 'Resume'), 'Escape must open the actual pause menu');
    await page.keyboard.press('r'); await page.waitForTimeout(300);
    await page.keyboard.press('Escape'); await page.waitForTimeout(300);
    const after = await cmd('state'); evidence.push({ name: 'paused-reset', before, after });
    assert.equal(after.resets, before.resets, 'A reset entered in a pause menu must not teleport the car on resume');
    await page.keyboard.press('r'); await page.waitForTimeout(300);
    const deliberate = await cmd('state'); evidence.push({ name: 'fresh-reset', deliberate });
    assert.equal(deliberate.resets, after.resets + 1, 'Fresh driving input after resume must still recover the car');
  });
  await check('hidden-transition', async () => {
    await cmd('startRace', 'e1', 'easy'); await waitRacing();
    await visible(true);
    await cmd('goto', 'main');
    const before = await cmd('startRace', 'e1', 'easy');
    await page.waitForTimeout(4500);
    const hidden = await cmd('state'); evidence.push({ name: 'hidden-transition', before, hidden });
    assert.equal(hidden.race, 'Countdown', 'A menu/race transition must preserve browser visibility pause');
    assert.equal(hidden.raceSeconds, before.raceSeconds);
    await visible(false); await waitRacing();
  });
  await check('overlapping-pause', async () => {
    await cmd('startRace', 'e1', 'easy'); await waitRacing();
    await page.keyboard.press('Escape'); await page.waitForTimeout(200);
    await visible(true);
    await page.keyboard.press('Escape'); await page.waitForTimeout(200);
    const before = await cmd('state'); await page.waitForTimeout(900);
    const stillHidden = await cmd('state');
    assert(Math.abs(stillHidden.raceSeconds - before.raceSeconds) < .001, 'User resume cannot dismiss a visibility pause');
    await visible(false); await page.waitForTimeout(500);
    const resumed = await cmd('state'); evidence.push({ name: 'overlapping-pause', before, stillHidden, resumed });
    assert(resumed.raceSeconds > before.raceSeconds + .2);
  });
  assert.deepEqual(errors, []);
  assert.deepEqual(failures, []);
  console.log('PAUSE BOUNDARIES PASS');
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors, failures }, null, 2));
  await browser.close();
}
