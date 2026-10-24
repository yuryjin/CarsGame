// Full real-time races. No finish/unlock cheats; autopilot is explicitly recorded.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-readable/races';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], races = [];
let page;
try {
  const statePath = process.env.CG_SOAK_STATE;
  const options = { viewport: { width: 1280, height: 720 } };
  if (statePath && await fs.access(statePath).then(() => true, () => false)) options.storageState = statePath;
  const context = await browser.newContext(options);
  page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8789/?sdk=mock');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args);
    await page.waitForTimeout(150);
    const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
    assert.equal(s.error || '', ''); return s;
  };
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i); console.log('GPU', renderer);
  await cmd('perf', 'on');
  const events = (process.env.EVENTS || 'e1,e2,e3').split(',');
  const tier = process.env.TIER || 'easy';
  let blocked = false;
  const click = async name => {
    for (let i = 0; i < 30; i++) {
      const b = (await cmd('ui')).buttons.find(b => b.name === name);
      assert(b?.interactable, `Unavailable ${name}`);
      const box = await page.locator('canvas').first().boundingBox();
      if (b.y > .23 && b.y < .88) {
        await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height);
        await page.waitForTimeout(450); return;
      }
      await page.mouse.move(box.x + box.width * .73, box.y + box.height * .65);
      await page.mouse.wheel(0, b.y > .5 ? 280 : -280); await page.waitForTimeout(200);
    }
    throw new Error(`Cannot reach ${name}`);
  };
  for (const event of events) {
    await cmd('goto', 'events');
    const gate = (await cmd('ui')).buttons.find(b => b.name === `Start_${event}`);
    if (!gate?.interactable) {
      blocked = true;
      races.push({ event, tier, skipped: 'Locked: a top-three finish in the prerequisite is required.' });
      break;
    }
    const run = { event, tier, startedAt: new Date().toISOString(), samples: [], recoverySamples: [], maxAiStuckTime: 0 };
    races.push(run);
    await cmd('startRace', event, tier);
    let s;
    for (let i = 0; i < 80; i++) {
      s = await cmd('state');
      if (s.race === 'Racing') break;
      await page.waitForTimeout(200);
    }
    assert.equal(s.race, 'Racing'); assert.equal(s.evt, event, 'Natural progression must unlock the next race');
    if (event === 'e1') {
      // Actual keyboard acceleration, firing, steering/brake, weapon swap and manual recovery.
      await page.locator('canvas').first().click({ position: { x: 640, y: 360 } });
      const beforeStop = await cmd('state');
      await page.keyboard.down('Space'); await page.waitForTimeout(7500); await page.keyboard.up('Space');
      const stopped = await cmd('state');
      run.samples.push({ phase: 'deliberate-brake-stop', before: beforeStop, state: stopped });
      run.findings = stopped.resets > beforeStop.resets ? ['Holding the brake at a deliberate stop triggered automatic recovery.'] : [];
      console.log('BRAKE STOP', beforeStop.resets, stopped.resets);
      assert.equal(stopped.resets, beforeStop.resets, 'A deliberate brake stop must not trigger recovery');
      const initial = await cmd('ui');
      await page.keyboard.down('w'); await page.keyboard.down('Control');
      await page.waitForTimeout(3500);
      run.samples.push({ phase: 'keyboard-fire', state: await cmd('ui') });
      assert.notEqual(run.samples.at(-1).state.texts.find(t => t.startsWith('Ammo:')), initial.texts.find(t => t.startsWith('Ammo:')), 'Firing must change ammunition/reload state');
      await page.screenshot({ path: `${output}/keyboard-fire.png` });
      await page.keyboard.up('Control'); await page.keyboard.down('d'); await page.keyboard.down('Space');
      await page.waitForTimeout(650);
      await page.keyboard.up('d'); await page.keyboard.up('Space');
      await page.keyboard.press('q'); await page.waitForTimeout(600);
      run.samples.push({ phase: 'keyboard-drift-and-swap', state: await cmd('state') });
      assert.notEqual(run.samples.at(-1).state.activePrimary, initial.activePrimary, 'Q must swap weapons during actual driving');
      await page.keyboard.up('w'); await page.keyboard.press('r'); await page.waitForTimeout(1000);
      run.samples.push({ phase: 'manual-reset', state: await cmd('state') });
      assert(run.samples.at(-1).state.resets > initial.resets, 'R must recover the car');
    }
    await cmd('autopilot', 'on');
    const raceSeconds = Number(process.env.CG_RACE_SECONDS || 300);
    const deadline = Date.now() + raceSeconds * 1000;
    let nextSample = 0;
    let capturedDriving = false;
    let previousAiResets = 0;
    let continueRequested = false;
    while (Date.now() < deadline) {
      s = await cmd('state');
      if (process.env.CG_WATCH_CONTINUE === '1' && !continueRequested && s.health <= 0 && s.flow === 'Racing') {
        const prompt = await cmd('ui');
        if (prompt.buttons.some(b => b.name === 'Watch' && b.interactable)) {
          const url = new URL(page.url());
          assert(['127.0.0.1', 'localhost'].includes(url.hostname) && url.searchParams.get('sdk') === 'mock', 'Continue-ad tests require the local mock SDK');
          run.samples.push({ phase: 'continue-offer', state: prompt });
          await page.screenshot({ path: `${output}/${event}-continue.png` });
          await click('Watch'); continueRequested = true;
        }
      }
      if (Number.isFinite(s.aiStuckTime)) {
        run.maxAiStuckTime = Math.max(run.maxAiStuckTime, s.aiStuckTime);
        if (s.aiStuckTime >= 2 || s.aiResets !== previousAiResets)
          run.recoverySamples.push({ raceSeconds: s.raceSeconds, distance: s.trackDistance, stuck: s.aiStuckTime,
            reversing: s.aiReversing, frozen: s.frozen, aiResets: s.aiResets, resets: s.resets });
        previousAiResets = s.aiResets;
      }
      if (Date.now() >= nextSample) {
        run.samples.push({ phase: 'autopilot', state: s });
        console.log(event, s.race, `lap ${s.lap}/${s.laps}`, `position ${s.position}`, `resets ${s.resets}`, `deaths ${s.deaths}`, `speed ${s.speedKmh}`, `distance ${s.trackDistance}`, `lateral ${s.lateral}`, `AI stuck ${s.aiStuckTime} reverse ${s.aiReversing} frozen ${s.frozen} seed ${s.raceSeed}`, `fps ${s.perf?.fps}`);
        nextSample = Date.now() + 15000;
      }
      if (!capturedDriving && s.race === 'Racing' && s.raceSeconds > 20) {
        await page.screenshot({ path: `${output}/${event}-driving.png` });
        capturedDriving = true;
      }
      if (s.flow === 'Results') break;
      await page.waitForTimeout(1000);
    }
    assert.equal(s.flow, 'Results', `${event} did not complete within ${raceSeconds} seconds`);
    await page.waitForTimeout(1500);
    run.result = await cmd('ui'); run.finishedAt = new Date().toISOString();
    run.completed = run.result.lap >= run.result.laps;
    run.continueRequested = continueRequested;
    if (run.completed) assert(run.result.lastReward > 0, 'A completed race must grant its normal reward');
    else {
      assert.equal(process.env.CG_ALLOW_DNF, '1', 'The player must finish the full distance unless this probe explicitly records DNF outcomes');
      assert(run.result.lastReward >= 0, 'DNF rewards cannot be negative');
      console.log('DNF RECORDED', event, run.result.lap, '/', run.result.laps);
    }
    await page.screenshot({ path: `${output}/${event}-results.png` });
    if (event === 'e1' && run.result.coins >= 150 && run.result.upgrades[1] === 0) {
      await cmd('goto', 'garage'); await click('Buy_armor');
      run.purchase = await cmd('state');
      assert.equal(run.purchase.upgrades[1], 1);
      assert.equal(run.purchase.coins, run.result.coins - 150);
      await page.screenshot({ path: `${output}/first-upgrade.png` });
    }
    if (statePath) await context.storageState({ path: statePath, indexedDB: true });
    await fs.writeFile(`${output}/races.json`, JSON.stringify({ renderer, races, errors }, null, 2));
  }
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/races.json`, JSON.stringify({ renderer, races, errors, blocked }, null, 2));
  console.log(blocked ? 'SOAK RECORDED: NEXT EVENT REQUIRES A PODIUM'
    : races.some(r => r.completed === false) ? 'RACE OUTCOMES RECORDED: INCLUDES DNF' : 'FULL RACE SOAK PASS');
} catch (error) {
  if (page && !page.isClosed()) await page.screenshot({ path: `${output}/failure.png` }).catch(() => {});
  await fs.writeFile(`${output}/failure.json`, JSON.stringify({ error: String(error), races, errors }, null, 2));
  throw error;
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
