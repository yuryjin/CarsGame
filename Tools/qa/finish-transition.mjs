// Natural full-distance finish, followed by actual pause and keyboard UI interactions.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-finish/browser';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], evidence = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, hasTouch: true,
    storageState: process.env.CG_SOAK_STATE || '/tmp/cars-journey-state.json' });
  page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8789/?sdk=mock&device=mobile');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(150);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  const click = async name => {
    const b = (await cmd('ui')).buttons.find(b => b.name === name);
    assert(b?.interactable, `Unavailable ${name}`);
    const box = await page.locator('canvas').boundingBox();
    await page.touchscreen.tap(box.x + b.x * box.width, box.y + b.y * box.height);
    await page.waitForTimeout(250);
  };
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i); console.log('GPU', renderer);
  const initial = await cmd('state');
  await cmd('startRace', 'e7', 'easy');
  await page.touchscreen.tap(640, 360);
  await cmd('autopilot', 'on');
  let state = await cmd('ui');
  assert(!state.buttons.some(b => b.name === 'FinishResults'), 'The finish action must not exist before finishing');
  const deadline = Date.now() + 300000;
  let nextSample = 0;
  while (Date.now() < deadline) {
    state = await cmd('state');
    if (Date.now() >= nextSample) {
      console.log('e7', state.race, `lap ${state.lap}/${state.laps}`, `place ${state.position}`);
      nextSample = Date.now() + 15000;
    }
    if (state.race === 'Finishing' || state.flow === 'Results') break;
    await page.waitForTimeout(250);
  }
  assert.equal(state.race, 'Finishing', 'The driver must finish naturally with rivals still on track');
  const desktop = await cmd('ui');
  assert(desktop.lap >= desktop.laps);
  assert(desktop.buttons.some(b => b.name === 'FinishResults'));
  assert(!desktop.buttons.some(b => ['Fire','Left','Right','Brake','SwapWeapon'].includes(b.name)), 'Driving controls must disappear after crossing the line');
  await page.screenshot({ path: `${output}/1280-finish.png` });
  const timer = desktop.labels.find(t => t.name === 'Timer').renderedText;
  await page.waitForTimeout(1500);
  const waiting = await cmd('ui');
  assert.equal(waiting.labels.find(t => t.name === 'Timer').renderedText, timer, 'The displayed clock must stay fixed during the spectator wait');
  assert(waiting.raceSeconds > desktop.raceSeconds + 1, 'Rivals must keep racing while the personal timer is fixed');
  await page.setViewportSize({ width: 640, height: 360 }); await page.waitForTimeout(400);
  const mobile = await cmd('ui'), button = mobile.buttons.find(b => b.name === 'FinishResults');
  assert(button && button.width >= 44 && button.height >= 44);
  assert(button.x > 0 && button.x < 1 && button.y > 0 && button.y < 1);
  for (const name of ['FinishTitle','FinishSummary']) {
    const label = mobile.labels.find(t => t.name === name);
    assert(label.fontPixels >= 18 && !label.truncated);
  }
  await page.screenshot({ path: `${output}/640-finish.png` });
  await page.setViewportSize({ width: 360, height: 640 }); await page.waitForTimeout(300);
  const portrait = await cmd('ui');
  assert(!portrait.buttons.some(b => b.name === 'FinishResults'));
  await page.waitForTimeout(500);
  assert(Math.abs((await cmd('state')).raceSeconds - portrait.raceSeconds) < .03, 'Rotation must suspend the remaining finish wait');
  await page.setViewportSize({ width: 640, height: 360 }); await page.waitForTimeout(300);
  await click('PauseButton');
  const paused = await cmd('ui');
  assert(!paused.buttons.some(b => b.name === 'FinishResults'));
  assert(paused.buttons.some(b => b.name === 'Resume'));
  await click('Resume');
  assert((await cmd('ui')).buttons.some(b => b.name === 'FinishResults'));
  // Resume must restore keyboard focus to the finish action.
  await page.keyboard.press('Enter'); await page.waitForTimeout(500);
  const result = await cmd('ui');
  assert.equal(result.flow, 'Results');
  assert.equal(result.evt, 'e7');
  assert(result.lastReward > 0);
  assert.equal(result.coins, initial.coins + result.lastReward);
  assert.equal(result.races, initial.races + 1);
  assert(result.raceSeconds - desktop.raceSeconds < 15, 'Results should arrive before the automatic 20-second timeout');
  await page.screenshot({ path: `${output}/640-results.png` });
  evidence.push({ initial, desktop, waiting, mobile, portrait, paused, result });
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, evidence, errors }, null, 2));
  console.log('NATURAL FINISH TRANSITION PASS');
} catch (error) {
  if (page && !page.isClosed()) await page.screenshot({ path: `${output}/failure.png` }).catch(() => {});
  throw error;
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
