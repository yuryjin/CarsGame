// Long, uninterrupted WebGL session. Actual races, UI transitions and pause callbacks.
// Supply a previously earned mock profile with CG_SOAK_STATE; no progress/finish grants.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-endurance-20261024/soak';
await fs.mkdir(out, { recursive: true });
const seconds = Number(process.env.CG_SOAK_SECONDS || 3600);
assert(Number.isFinite(seconds) && seconds >= 60 && seconds <= 36000);
const events = (process.env.EVENTS || Array.from({ length: 20 }, (_, i) => `e${i + 1}`).join(',')).split(',');
const tiers = (process.env.TIERS || 'easy,normal,hard').split(',');
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const report = { startedAt: new Date().toISOString(), seconds, races: [], menus: [], errors: [], warnings: [], outcome: 'running' };
let page, context;
const write = () => fs.writeFile(`${out}/report.json`, JSON.stringify(report, null, 2));
try {
  const options = { viewport: { width: 1280, height: 720 } };
  if (process.env.CG_SOAK_STATE) options.storageState = process.env.CG_SOAK_STATE;
  context = await browser.newContext(options);
  await context.addInitScript(() => {
    window.__carsContextEvents = [];
    for (const type of ['webglcontextlost', 'webglcontextrestored'])
      document.addEventListener(type, e => window.__carsContextEvents.push({ type, id: e.target.id, at: performance.now() }), true);
  });
  page = await context.newPage();
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('console', m => {
    const s = m.text();
    if (/context lost/i.test(s)) report.warnings.push({ message: s, location: m.location() });
    if (/Exception|\[Loc\] missing key|character with Unicode.*not found|OUT_OF_MEMORY/i.test(s)) report.errors.push(s);
  });
  const open = async () => {
    await page.goto(process.env.URL || 'http://127.0.0.1:8805/?sdk=mock');
    await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
    await page.waitForTimeout(1200);
  };
  await open();
  report.renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(report.renderer, /NVIDIA/i);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(150);
    const result = await page.evaluate(() => {
      const v = window.CarsPlatform.debug.state;
      return { state: typeof v === 'string' ? JSON.parse(v) : v, events: window.__carsContextEvents,
        lost: document.querySelector('canvas').getContext('webgl2').isContextLost() };
    });
    // Unity also creates a detached capability-probe canvas. Its disposal warning
    // is not a loss of the game's rendering context; inspect the actual canvas.
    assert.deepEqual(result.events, [], 'The game canvas lost or restored its rendering context');
    assert.equal(result.lost, false, 'The game WebGL context must stay live');
    const s = result.state;
    assert.equal(s.error || '', ''); return s;
  };
  const click = async name => {
    const b = (await cmd('ui')).buttons.find(b => b.name === name && b.interactable);
    assert(b && b.x > 0 && b.x < 1 && b.y > 0 && b.y < 1, `Visible button ${name}`);
    const box = await page.locator('canvas').boundingBox();
    await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height);
    await page.waitForTimeout(300);
  };
  const visible = async hidden => {
    await page.evaluate(hidden => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => hidden }); document.dispatchEvent(new Event('visibilitychange')); }, hidden);
    await page.waitForTimeout(200);
  };
  const pause = async hidden => {
    if (hidden) await visible(true);
    else { await page.keyboard.press('Escape'); await page.waitForTimeout(200); }
    const a = await cmd('state'); await page.waitForTimeout(500); const b = await cmd('state');
    assert(Math.abs(a.raceSeconds - b.raceSeconds) < .001, `${hidden ? 'Visibility' : 'User'} pause advanced the clock`);
    if (hidden) await visible(false);
    else { await page.keyboard.press('Escape'); await page.waitForTimeout(200); }
  };
  await cmd('perf', 'on');
  report.initial = await cmd('state');
  const deadline = Date.now() + seconds * 1000;
  for (let round = 0; Date.now() < deadline; round++) {
    const event = events[round % events.length], tier = tiers[Math.floor(round / events.length) % tiers.length];
    const run = { round, event, tier, samples: [], startedAt: new Date().toISOString() };
    report.races.push(run);
    await cmd('goto', 'events');
    const gate = (await cmd('ui')).buttons.find(b => b.name === `Start_${event}`);
    assert(gate?.interactable, `${event} must already be unlocked in the earned profile`);
    await cmd('startRace', event, tier);
    for (let i = 0; i < 80 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
    let s = await cmd('state');
    assert.equal(s.race, 'Racing'); assert.equal(s.evt, event);
    await cmd('autopilot', 'on');
    const raceDeadline = Date.now() + 900000;
    let userPaused = false, hiddenPaused = false, continued = false, nextSample = 0;
    while (Date.now() < raceDeadline) {
      s = await cmd('state');
      if (s.flow === 'Results') break;
      if (s.race === 'Finishing') {
        const ui = await cmd('ui');
        if (ui.buttons.some(b => b.name === 'FinishResults' && b.interactable)) { await click('FinishResults'); continue; }
      }
      if (s.health <= 0 && !continued) {
        const ui = await cmd('ui');
        if (ui.buttons.some(b => b.name === 'Watch' && b.interactable)) {
          const u = new URL(page.url());
          assert(['localhost','127.0.0.1'].includes(u.hostname) && u.searchParams.get('sdk') === 'mock');
          await click('Watch'); continued = true; run.continued = true;
        }
      }
      if (s.race === 'Racing' && s.health > 0) {
        if (!userPaused && s.raceSeconds > 12) { await pause(false); userPaused = true; }
        if (!hiddenPaused && s.raceSeconds > 35) { await pause(true); hiddenPaused = true; }
      }
      if (Date.now() >= nextSample) {
        run.samples.push(s); nextSample = Date.now() + 30000;
        console.log('RACE', round, event, tier, s.race, `lap=${s.lap}/${s.laps}`, `t=${s.raceSeconds.toFixed(1)}`, `resets=${s.resets}`, `fps=${s.perf?.fps?.toFixed(1)}`);
        await write();
      }
      assert.deepEqual(report.errors, []);
      await page.waitForTimeout(1000);
    }
    assert.equal(s.flow, 'Results', `${event} did not resolve within 15 minutes`);
    run.result = await cmd('ui'); run.finishedAt = new Date().toISOString();
    run.completed = run.result.lap >= run.result.laps;
    const previousCoins = round === 0 ? report.initial.coins : report.races[round - 1].result.coins;
    assert.equal(run.result.coins, previousCoins + run.result.lastReward, 'Each normal race reward must be paid exactly once');
    assert.equal(run.result.races, report.initial.races + round + 1, 'Each completed result must count once');
    const names = run.result.labels.filter(l => l.name === 'Name' && /^Result\d+$/.test(l.parent)).map(l => l.text.toLocaleLowerCase());
    assert(names.length && names.length === new Set(names).size, 'Distinct participants in results');
    if (round % events.length === 0) await page.screenshot({ path: `${out}/results-${round}.png` });
    await cmd('goto', 'garage');
    for (let i = 0; i < 4; i++) await click('NextBody');
    await cmd('goto', 'settings');
    await cmd('goto', 'main'); await page.waitForTimeout(500);
    const menu = await cmd('resources');
    assert(menu.resources && menu.resources.gameObjects > 0, 'Read-only resource diagnostics must be available');
    assert.equal(menu.liveShells, 0, 'No projectile may survive returning to menu');
    report.menus.push({ round, at: new Date().toISOString(), resources: menu.resources, perf: menu.perf });
    console.log('MENU', round, JSON.stringify(menu.resources), `bytes=${menu.perf?.totalAllocated}`);
    await context.storageState({ path: `${out}/earned-profile.json`, indexedDB: true });
    await write();
  }
  // Compare identical main-menu states after two complete event cycles warmed the caches.
  const warmed = report.menus.filter(m => m.round >= events.length * 2);
  if (warmed.length >= events.length) {
    const base = warmed.slice(0, events.length), last = warmed.at(-1);
    for (const [key, allowance] of Object.entries({ materials: 8, meshes: 8, renderTextures: 0, gameObjects: 12, audioClips: 20 })) {
      const high = Math.max(...base.map(m => m.resources[key]));
      assert(last.resources[key] <= high + allowance, `${key} keep growing after warmed identical menu returns: ${high} -> ${last.resources[key]}`);
    }
  }
  report.beforeReload = await cmd('state');
  await page.reload();
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1500);
  report.afterReload = await cmd('state');
  assert.equal(report.afterReload.coins, report.beforeReload.coins, 'Earned coins survive reloading');
  assert.equal(report.afterReload.races, report.beforeReload.races, 'Completed races survive reloading');
  assert.deepEqual(report.errors, []);
  report.outcome = 'passed'; console.log('ENDURANCE PASS', report.races.length, 'races');
} catch (error) {
  report.outcome = 'failed'; report.failure = String(error);
  if (page && !page.isClosed()) await page.screenshot({ path: `${out}/failure.png` }).catch(() => {});
  throw error;
} finally {
  report.finishedAt = new Date().toISOString(); await write();
  await browser.close();
}
