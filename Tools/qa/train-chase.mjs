// Drive both routes through the ordinary menu, then observe a muted border loss and Retry.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
assert(process.env.WEBTEST_CHROME_ARGS, 'Launch with webtest');
assert.equal(process.env.PULSE_SINK, 'AgentSink', 'Route train audio to the agent sink');
const out = process.env.OUT || '/tmp/cars-train-browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', ignoreDefaultArgs: ['--mute-audio'],
  args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const report = { evidence: [], errors: [] };
async function note(entry) {
  report.evidence.push(entry);
  await fs.writeFile(out + '/evidence.json', JSON.stringify(report, null, 2));
  console.log('Checked', entry.phase, entry.seconds ?? '');
}
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  await context.addInitScript(() => {
    const save = { version: 6, updatedAtUnixMs: Date.now(), settings: { lang: 'ru', sfx: 1, music: 0, musicChosen: true, autoThrottle: false } };
    localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify(save) }));
  });
  const page = await context.newPage();
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) report.errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8840/?sdk=mock');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  let box = await page.locator('canvas').boundingBox();
  report.renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(report.renderer, /NVIDIA/i);
  async function ui() {
    await page.evaluate(() => window.CarsPlatform.cmd('ui'));
    await page.waitForTimeout(140);
    const s = await page.evaluate(() => window.CarsPlatform.debug.state);
    const state = typeof s === 'string' ? JSON.parse(s) : s;
    assert.equal(state.error || '', ''); return state;
  }
  async function clickButton(button) {
    assert(button?.interactable && button.x > 0 && button.x < 1 && button.y > 0 && button.y < 1, 'Action is on screen: ' + JSON.stringify(button));
    await page.mouse.click(box.x + button.x * box.width, box.y + button.y * box.height, { delay: 80 });
    await page.waitForTimeout(300);
  }
  const button = (state, name) => state.buttons.find(b => b.name === name);
  async function waitForUi(matches, description) {
    const deadline = Date.now() + 7000;
    let state;
    do {
      state = await ui();
      if (state.lastCommand === 'ui' && matches(state)) return state;
    } while (Date.now() < deadline);
    assert.fail(description + ': ' + JSON.stringify({ flow: state.flow, race: state.race, buttons: state.buttons.map(b => b.name) }));
  }
  async function pauseRace() {
    const hud = await waitForUi(s => s.race === 'Racing' && button(s, 'PauseButton')?.interactable, 'Ready to pause');
    await clickButton(button(hud, 'PauseButton'));
    return waitForUi(s => button(s, 'Resume')?.interactable, 'Pause opened');
  }
  async function startFromMenu(id) {
    await clickButton(button(await ui(), 'ChooseRace'));
    if (id === 'm2') {
      const normal = (await ui()).buttons.find(b => b.name.startsWith('Tier') && b.text === 'Нормально');
      await clickButton(normal);
    }
    for (let attempt = 0; attempt < 50; attempt++) {
      const state = await ui(), start = button(state, 'Start_' + id);
      assert.equal(state.screen, 'events'); assert(start?.interactable, id + ' is unlocked');
      if (start.y > .36 && start.y < .9) { await clickButton(start); return; }
      await page.mouse.move(box.x + box.width * .65, box.y + box.height * .65);
      await page.mouse.wheel(0, start.y > .6 ? 150 : -150); await page.waitForTimeout(160);
    }
    assert.fail(id + ' could not be reached by scrolling');
  }
  function readable(label) {
    assert(label?.renderedText && !label.truncated && label.fontPixels >= 11.9, JSON.stringify(label));
    assert(label.x * box.width - label.width / 2 >= -1 && label.x * box.width + label.width / 2 <= box.width + 1);
    assert(label.y * box.height - label.height / 2 >= -1 && label.y * box.height + label.height / 2 <= box.height + 1);
  }

  // The QA channel supplies positions. Only browser keys steer, accelerate and brake.
  async function telemetry() {
    return page.evaluate(async () => {
      window.CarsPlatform.cmd('train');
      for (let i = 0; i < 8; i++) {
        const raw = window.CarsPlatform.debug.state;
        const state = typeof raw === 'string' ? JSON.parse(raw) : raw;
        if (state?.lastCommand === 'train') return state;
        await new Promise(resolve => requestAnimationFrame(resolve));
      }
      throw new Error('Train telemetry did not arrive');
    });
  }
  const held = new Set();
  async function key(name, down) {
    if (held.has(name) === down) return;
    if (down) { await page.keyboard.down(name); held.add(name); }
    else { await page.keyboard.up(name); held.delete(name); }
  }
  async function release() { for (const name of [...held]) await key(name, false); }
  const dot = (a, b) => a.x * b.x + a.z * b.z;
  const minus = (a, b) => ({ x: a.x - b.x, z: a.z - b.z });
  const add = (p, d, n) => ({ x: p.x + d.x * n, z: p.z + d.z * n });
  const side = f => ({ x: f.z, z: -f.x });
  const clamp = (n, min, max) => Math.max(min, Math.min(max, n));

  async function drive(path) {
    const viaWash = path === 'station';
    const deadline = Date.now() + 205000;
    const pauseTarget = !viaWash && button(await ui(), 'PauseButton');
    let segment = 1, wash, lastLog = -10, pausedCapture = false, holdStart, previousHold = 0, switchTime, stopTime, bend, last;
    let screenshotTaken = false;
    const lane = [];
    while (Date.now() < deadline) {
      const state = await telemetry();
      if (!state || state.lastCommand !== 'train') { await page.waitForTimeout(40); continue; }
      if (state.flow === 'Results') break;
      if (!state.train || state.race !== 'Racing') { await page.waitForTimeout(55); continue; }
      const t = state.train, pos = t.player;
      if (viaWash && !wash) {
        // Surveyed centrelines from the start of Dry Gulch to the siding platform.
        const local = (x, z) => ({ x: t.start.x + x, z: t.start.z + z });
        wash = [local(0,0),local(0,125),local(12,163),local(42,227),local(37,310),local(17,394),
          local(30,484),local(54,574),local(40,678),local(18,782),t.switchPoint,local(1,893),
          local(9,936),local(48,986),local(83,1035),t.station];
      }
      let target, speed;
      if (viaWash) {
        let leg = minus(wash[segment], wash[segment - 1]), length = Math.hypot(leg.x, leg.z);
        let along = dot(minus(pos, wash[segment - 1]), leg) / length;
        if (segment + 1 < wash.length && along >= length - 10) {
          segment++; leg = minus(wash[segment], wash[segment - 1]); length = Math.hypot(leg.x, leg.z);
          along = dot(minus(pos, wash[segment - 1]), leg) / length;
        }
        target = add(wash[segment - 1], leg, clamp(along + 17, 0, length) / length);
        const remaining = Math.hypot(pos.x - t.station.x, pos.z - t.station.z);
        // Brake on the approach, wait clear of the platform, then drive up to the stopped train.
        speed = !t.atStation ? clamp((remaining - 45) * .26, 0, 26)
          : segment === wash.length - 1 ? clamp((remaining - 2) * .55, 0, 26) : 26;
      } else {
        if (!lane.length) lane.push(t.start);
        const gap = dot(minus(t.tail, pos), t.tailForward);
        const alongside = add(t.tail, side(t.tailForward), gap < 19 && gap > -10 ? 15 : 12.6);
        const previous = lane[lane.length - 1];
        if (Math.hypot(previous.x - alongside.x, previous.z - alongside.z) > 3) lane.push(alongside);
        // Follow the carriage's path through the bend. A straight line to a distant tail cuts across the wash.
        while (segment < lane.length - 1 && dot(minus(pos, lane[segment]), minus(lane[segment], lane[segment - 1])) > 0) segment++;
        const ahead = lane[Math.min(segment + 5, lane.length - 1)];
        target = gap > 26 && Math.hypot(ahead.x - pos.x, ahead.z - pos.z) > 13 ? ahead : add(alongside, t.tailForward, 18);
        // Pass the buggy on its outside and run level with the front of the rear platform.
        speed = clamp(t.trainSpeed + (gap + 5.5) * .64, 4, 32);
      }
      if (!bend && pos.z - t.start.z > 265) bend = { x: pos.x - t.start.x, z: pos.z - t.start.z, seconds: state.raceSeconds };
      const desired = minus(target, pos);
      const angle = Math.atan2(dot(desired, side(t.forward)), dot(desired, t.forward)) * 180 / Math.PI;
      const steer = angle / 23 - t.yawRate * .18;
      const error = speed - state.speedKmh / 3.6;
      await key('a', steer < -.10); await key('d', steer > .10);
      await key('w', error > .15); await key('Space', error < -1.35 || speed < .4);
      last = { seconds: state.raceSeconds, pos, speedKmh: state.speedKmh, segment, capture: t.capture, diverted: t.diverted };
      if (state.raceSeconds - lastLog >= 10) {
        console.log(path, state.raceSeconds.toFixed(1), state.speedKmh.toFixed(1), segment, 'position',
          Math.round(pos.x - t.start.x), Math.round(pos.z - t.start.z), 'car relative to tail',
          dot(minus(pos, t.tail), side(t.tailForward)).toFixed(1), dot(minus(pos, t.tail), t.tailForward).toFixed(1),
          'clear', t.approachClear, 'capture', t.capture);
        lastLog = state.raceSeconds;
      }
      if (t.diverted && switchTime == null) {
        switchTime = state.raceSeconds;
        const goal = (await ui()).labels.find(l => l.name === 'TrainGoal');
        readable(goal); assert.equal(goal.text, 'Стрелка переведена. Езжай за поездом к станции.');
        await note({ phase: 'switch thrown', seconds: switchTime, goal });
      }
      if (t.atStation && stopTime == null) {
        stopTime = state.raceSeconds;
        const goal = (await ui()).labels.find(l => l.name === 'TrainGoal');
        readable(goal); assert.equal(goal.text, 'Подъедь к хвосту за кубком.');
        await note({ phase: 'stopped train', seconds: stopTime, goal, distance: Math.hypot(t.station.x - pos.x, t.station.z - pos.z) });
      }
      if (t.capture > 0 && previousHold === 0) holdStart = state.raceSeconds - 3 * t.capture;
      previousHold = t.capture;
      if (!viaWash && !pausedCapture && t.capture > .10 && t.capture < .70) {
        await clickButton(pauseTarget);
        const paused = await waitForUi(s => button(s, 'Resume')?.interactable, 'Pause during capture');
        const frozen = (await telemetry()).train;
        if (frozen.capture > 0) {
          await page.waitForTimeout(500);
          const after = (await telemetry()).train;
          assert.equal(after.capture, frozen.capture);
          assert.deepEqual(after.tail, frozen.tail);
          const goal = paused.labels.find(l => l.name === 'TrainGoal');
          assert.match(goal.text, /Держись у вагона.*%/); readable(goal);
          await note({ phase: 'pause during capture', capture: frozen.capture, seconds: paused.raceSeconds, goal });
          pausedCapture = true;
        }
        await clickButton(button(paused, 'Resume'));
      } else if (!viaWash && pausedCapture && !screenshotTaken && t.capture > .16) {
        await page.screenshot({ path: out + '/chase.png' }); screenshotTaken = true;
      }
      await page.waitForTimeout(30);
    }
    await release();
    const state = await ui(), detail = state.labels.find(l => l.parent === 'Summary' && l.name === 'Detail');
    await note({ phase: path, event: state.evt, seconds: state.raceSeconds, holdStart, switchTime, stopTime, bend, last, detail, reward: state.lastReward });
    assert.equal(state.flow, 'Results', JSON.stringify(last)); readable(detail);
    assert.equal(detail.text, viaWash ? 'Кубок получен на станции' : 'Кубок снят с поезда на ходу', JSON.stringify(last));
    assert(state.lastReward > 0);
    assert(bend && (viaWash ? bend.x > 15 : bend.x < -30), 'Actual chassis must take the chosen side of the large bend: ' + JSON.stringify(bend));
    if (viaWash) assert(switchTime > 0 && stopTime > switchTime && state.raceSeconds > stopTime);
    else assert(pausedCapture && screenshotTaken);
    return state;
  }
  const retry = async state => clickButton(state.buttons.find(b => b.name.startsWith('ResultAction') && b.text.includes('Ещё раз')));
  async function muteEffects() {
    const paused = await pauseRace();
    await clickButton(button(paused, 'PauseSettings'));
    await waitForUi(s => s.sliders.some(v => v.name === 'SoundVolume'), 'Sound settings opened');
    for (let attempt = 0; attempt < 30; attempt++) {
      const slider = (await ui()).sliders.find(s => s.name === 'SoundVolume');
      assert(slider?.interactable);
      if (slider.y > .30 && slider.y < .88) {
        const center = box.x + box.width * slider.x, y = box.y + box.height * slider.y;
        await page.mouse.move(center, y); await page.mouse.down(); await page.waitForTimeout(100);
        await page.mouse.move(center - slider.width / 2 + 2, y, { steps: 8 });
        await page.waitForTimeout(80); await page.mouse.up();
        const remaining = (await ui()).sliders.find(s => s.name === 'SoundVolume').value;
        assert(remaining < 100, 'Dragging the volume control must change its value');
        for (let i = 0; i < remaining; i++) await page.keyboard.press('ArrowLeft', { delay: 40 });
        assert.equal((await ui()).sliders.find(s => s.name === 'SoundVolume').value, 0, 'SFX muted via the slider');
        await note({ phase: 'sound slider muted', percent: 0 });
        await clickButton(button(await ui(), 'Back'));
        const settingsClosed = await waitForUi(s => button(s, 'Resume')?.interactable, 'Settings returned to pause');
        await clickButton(button(settingsClosed, 'Resume')); return;
      }
      await page.mouse.move(box.x + box.width * .7, box.y + box.height * .65);
      await page.mouse.wheel(0, slider.y > .6 ? 90 : -90); await page.waitForTimeout(120);
    }
    assert.fail('SFX volume could not be reached by scrolling');
  }

  const initial = await ui();
  await startFromMenu('m2');
  const started = await ui(), goal = started.labels.find(l => l.name === 'TrainGoal');
  readable(goal); assert.match(goal.text, /Слева.*справа.*стрелке/);
  assert(!started.labels.some(l => l.parent === 'RaceStatus'));
  await note({ phase: 'start', event: started.evt, goal });
  const moving = await drive('moving'); assert.equal(moving.races, initial.races + 1);
  await page.setViewportSize({ width: 640, height: 360 }); await page.waitForTimeout(500);
  box = await page.locator('canvas').boundingBox();
  await retry(await ui());
  const station = await drive('station'); assert.equal(station.races, initial.races + 2);
  await retry(station);
  await muteEffects();
  const deadline = Date.now() + 230000;
  let result, sawWhistle = false, sawSwitchClose = false;
  while (Date.now() < deadline) {
    const s = await ui();
    if (s.flow === 'Results') { result = s; break; }
    const signal = s.labels.find(l => l.text === 'Гудок поезда за скалой');
    if (signal && !sawWhistle) {
      readable(signal); assert(s.labels.some(l => l.name === 'TrainBearing'), 'The whistle also has a visible bearing');
      sawWhistle = true;
      await note({ phase: 'whistle', seconds: s.raceSeconds, caption: signal });
    }
    const goal = s.labels.find(l => l.name === 'TrainGoal');
    if (goal?.text.startsWith('Догони хвост') && !sawSwitchClose) {
      sawSwitchClose = true; readable(goal);
      await note({ phase: 'switch closed', seconds: s.raceSeconds, goal });
    }
    await page.waitForTimeout(150);
  }
  assert(sawWhistle && sawSwitchClose && result, 'The unattended train passes the canyon, switch and border');
  assert.equal(result.races, initial.races + 3);
  const detail = result.labels.find(l => l.parent === 'Summary' && l.name === 'Detail');
  readable(detail); assert.equal(detail.text, 'Поезд увёз кубок за границу');
  await note({ phase: 'border', seconds: result.raceSeconds, detail, completed: result.races });
  await retry(result);
  const again = await ui();
  assert.equal(again.evt, 'm2'); assert.equal(again.races, result.races);
  assert.equal(again.labels.filter(l => l.name === 'TrainGoal').length, 1);
  assert.equal(again.labels.find(l => l.name === 'TrainGoal').text, goal.text);
  const paused = await pauseRace();
  await clickButton(button(paused, 'MainMenu'));
  await startFromMenu('e1');
  const circuit = await ui();
  assert.equal(circuit.evt, 'e1'); assert(!circuit.labels.some(l => l.name === 'TrainGoal'));
  assert(circuit.labels.some(l => l.parent === 'RaceStatus'));
  await note({ phase: 'retry, menu, circuit', event: circuit.evt, completed: circuit.races });
  assert.deepEqual(report.errors, []);
  console.log('TRAIN CHASE PASS', report.renderer, report.evidence.map(e => e.phase).join(', '));
} finally {
  try { await fs.writeFile(out + '/evidence.json', JSON.stringify(report, null, 2)); }
  finally { await browser.close(); }
}
