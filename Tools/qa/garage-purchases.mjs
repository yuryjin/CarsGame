// Real garage purchases from an earned local mock profile, followed by save/reload.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const url = new URL(process.env.URL);
assert(['localhost', '127.0.0.1'].includes(url.hostname) && url.searchParams.get('sdk') === 'mock');
assert(process.env.CG_SOAK_STATE, 'Use a previously earned profile');
const out = process.env.OUT;
await fs.mkdir(out, { recursive: true });
const evidence = { purchases: [], errors: [] };
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, storageState: process.env.CG_SOAK_STATE });
  const page = await context.newPage();
  page.on('pageerror', e => evidence.errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key/.test(m.text())) evidence.errors.push(m.text()); });
  const ready = async () => {
    await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
    await page.waitForTimeout(700);
  };
  await page.goto(url.href); await ready();
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  const profile = () => page.evaluate(() => JSON.parse(JSON.parse(localStorage.getItem('cars.mock.playerdata')).save));
  evidence.renderer = await page.evaluate(() => { const g = document.querySelector('canvas').getContext('webgl2'), e = g.getExtension('WEBGL_debug_renderer_info'); return g.getParameter(e ? e.UNMASKED_RENDERER_WEBGL : g.RENDERER); });
  assert.match(evidence.renderer, /NVIDIA/i);
  const initial = evidence.initial = await cmd('state');
  assert(initial.races > 0 && initial.coins >= 2700);
  const click = async name => {
    let b;
    for (let i = 0; i < 60; i++) {
      b = (await cmd('ui')).buttons.find(b => b.name === name);
      assert(b, `Control ${name} exists`);
      if (b.y > .25 && b.y < .88) break;
      await page.mouse.move(1080, 460); await page.mouse.wheel(0, b.y > .5 ? 260 : -260); await page.waitForTimeout(200);
    }
    assert(b.interactable && b.y > .25 && b.y < .88, `Reachable ${name}`);
    const box = await page.locator('canvas').boundingBox();
    await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height); await page.waitForTimeout(400);
  };
  await cmd('goto', 'garage');
  let coins = initial.coins;
  for (const [body, cost] of [['arrow', 800], ['comet', 1900]]) {
    await click(`Buy_${body}`);
    const s = await cmd('ui');
    coins -= cost;
    assert.equal(s.coins, coins, 'Purchase deducts exactly the catalog price');
    assert.equal(s.races, initial.races, 'Purchase awards no race completion');
    assert(s.buttons.some(b => b.name === `Use_${body}` && !b.interactable), 'Purchased body is equipped');
    evidence.purchases.push({ body, cost, state: s });
  }
  await click('Tab_Buggy');
  await click('Ad_sidewinder');
  let selected;
  for (let i = 0; i < 100; i++) {
    selected = await cmd('ui');
    if (selected.buttons.some(b => b.name === 'Use_sidewinder' && !b.interactable)) break;
    await page.waitForTimeout(200);
  }
  assert(selected.buttons.some(b => b.name === 'Use_sidewinder' && !b.interactable), 'Completed mock rewarded ad equips the body');
  assert.equal(selected.coins, coins, 'Rewarded body costs no coins');
  assert.equal(selected.races, initial.races);
  evidence.rewarded = selected;
  // The cloud row is deliberately coalesced for five seconds; first verify the
  // immediate local save survives reloading before requiring cloud convergence.
  await page.waitForTimeout(400);
  evidence.beforeReload = await profile();
  await page.reload(); await ready();
  const saved = await cmd('goto', 'garage');
  assert.equal(saved.coins, coins); assert.equal(saved.races, initial.races);
  assert((await cmd('ui')).buttons.some(b => b.name === 'Use_sidewinder' && !b.interactable), 'Local save retains the rewarded selection immediately');
  await click('Tab_Sport');
  const owned = await cmd('ui');
  for (const body of ['arrow', 'comet']) assert(owned.buttons.some(b => b.name === `Use_${body}` && b.interactable), 'Purchased ownership survives reload');
  await cmd('startRace', 'e1', 'normal');
  for (let i = 0; i < 100 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
  const car = evidence.car = await cmd('state');
  assert.equal(car.race, 'Racing');
  assert(Math.abs(car.health - 68 * (1 + .15 * initial.upgrades[1])) < .01, 'Sidewinder spawns with its own body stats and the earned armor upgrade');
  await page.screenshot({ path: `${out}/sidewinder.png` });
  await cmd('goto', 'main'); await page.waitForTimeout(1600);
  const end = await cmd('state');
  assert.equal(end.coins, coins); assert.equal(end.races, initial.races, 'Abandoning the test start grants nothing');
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => true }); document.dispatchEvent(new Event('visibilitychange')); });
  await page.waitForFunction(() => JSON.parse(JSON.parse(localStorage.getItem('cars.mock.playerdata')).save).progress.body === 'sidewinder', null, { timeout: 15000 });
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => false }); document.dispatchEvent(new Event('visibilitychange')); });
  evidence.afterReload = await profile();
  await context.storageState({ path: `${out}/earned-profile.json`, indexedDB: true });
  assert.deepEqual(evidence.errors, []);
  console.log('GARAGE PURCHASES PASS: two exact-price purchases, rewarded body, reload, correct spawned stats');
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify(evidence, null, 2));
  await browser.close();
}
