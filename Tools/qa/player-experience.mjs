// Run through webtest. URL must point to a local WebGL build with ?sdk=mock.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run this browser test through webtest.');
const output = process.env.OUT || '/tmp/cars-experience/browser';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, hasTouch: true });
  page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  const ready = async () => {
    await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
    await page.waitForTimeout(1800);
  };
  const state = () => page.evaluate(() => {
    const s = window.CarsPlatform.debug.state;
    return typeof s === 'string' ? JSON.parse(s) : s;
  });
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args);
    await page.waitForTimeout(250);
    const s = await state();
    assert.equal(s?.error || '', '', JSON.stringify(s));
    return s;
  };
  const ui = () => cmd('ui');
  const click = async name => {
    for (let i = 0; i < 10; i++) {
      const s = await ui(), b = s.buttons?.find(b => b.name === name);
      assert(b, `Missing button ${name}: ${JSON.stringify(s.buttons)}`);
      assert(b.interactable, `Disabled button ${name}`);
      const box = await page.locator('canvas').first().boundingBox();
      if (b.x > 0 && b.x < 1 && b.y > 0.15 && b.y < 0.90) {
        await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height);
        await page.waitForTimeout(500);
        return;
      }
      await page.mouse.move(box.x + box.width * 0.7, box.y + box.height * 0.7);
      await page.mouse.wheel(0, b.y > .5 ? 180 : -180);
      await page.waitForTimeout(250);
    }
    throw new Error(`Cannot scroll to ${name}`);
  };
  const racing = async expectedEvent => {
    for (let i = 0; i < 60; i++) {
      const s = await cmd('state');
      if (s.race === 'Racing') {
        if (expectedEvent) assert.equal(s.evt, expectedEvent, 'Requested event must be unlocked and started');
        return s;
      }
      await page.waitForTimeout(250);
    }
    throw new Error('Race did not start');
  };
  const record = async name => {
    evidence.push({ name, state: await ui() });
    await page.screenshot({ path: `${output}/${name}.png` });
    console.log('PASS', name);
  };
  await page.goto(process.env.URL || 'http://127.0.0.1:8789/?sdk=mock');
  await ready();
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2');
    const ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i, renderer);
  console.log('GPU', renderer);
  await cmd('goto', 'garage');
  await click('ChooseSlot1');
  await click('Filter_Traps');
  let filtered = await ui();
  assert(filtered.buttons.some(b => b.name === 'EquipSlot_40'));
  assert(!filtered.buttons.some(b => b.name === 'EquipSlot_0'));
  await click('Filter_Control');
  filtered = await ui();
  assert(filtered.buttons.some(b => b.name === 'EquipSlot_6'));
  assert(filtered.buttons.some(b => b.name === 'EquipSlot_22'));
  assert(!filtered.buttons.some(b => b.name === 'EquipSlot_0'));
  await record('desktop-control-weapons');
  await click('Filter_All');
  assert((await ui()).texts.some(t => t.startsWith('Stats:') && t.includes('7 × 7')));
  await click('EquipSlot_0');
  let s = await cmd('state');
  assert.equal(s.equippedPrimary, 2); assert.equal(s.reservePrimary, 0);
  await record('desktop-arsenal');
  await page.reload(); await ready();
  s = await cmd('state');
  assert.equal(s.equippedPrimary, 2); assert.equal(s.reservePrimary, 0);
  await cmd('goto', 'garage');
  await click('ToggleArsenal');
  assert.equal((await cmd('state')).fullArsenal, true);
  await click('ToggleArsenal');
  assert.equal((await cmd('state')).fullArsenal, false);
  await cmd('goto', 'events');
  s = await ui();
  assert(s.buttons.some(b => b.name === 'CupStart_rookie'));
  await record('desktop-cups');
  await cmd('startRace', 'e1', 'easy'); await racing();
  const rejected = await cmd('startRace', 'e6', 'easy');
  assert.equal(rejected.evt, 'e1', 'A locked event must not replace active race rules');
  assert.equal(rejected.race, 'Racing');
  assert.equal((await cmd('state')).activePrimary, 2);
  const previousWeapon = (await ui()).texts.find(t => t.startsWith('ActiveWeapon:'));
  assert(previousWeapon && previousWeapon.length > 'ActiveWeapon: '.length);
  await page.keyboard.press('q'); await page.waitForTimeout(200);
  assert.equal((await cmd('state')).activePrimary, 0);
  assert.notEqual((await ui()).texts.find(t => t.startsWith('ActiveWeapon:')), previousWeapon);
  await page.keyboard.press('1'); await page.waitForTimeout(200);
  assert.equal((await cmd('state')).activePrimary, 2);
  await page.keyboard.press('2'); await page.waitForTimeout(200);
  assert.equal((await cmd('state')).activePrimary, 0);
  await click('LiveGoals');
  s = await ui(); assert(s.texts.some(t => t.startsWith('Goals:') && t.includes('\n')));
  await click('LiveGoals');
  await record('desktop-race');
  await cmd('finish'); await page.waitForTimeout(2000);
  s = await ui(); assert.equal(s.flow, 'Results');
  assert(s.texts.some(t => /Следующая цель|Your next goal/.test(t)));
  await record('desktop-results');
  // Unlock the mines event through the same completion path as normal progression.
  // QA finish skips driving here; the physical race scenarios are tested in PlayMode.
  for (const id of ['e2', 'e3', 'e4', 'e5']) {
    await cmd('startRace', id, 'easy'); await racing(id);
    await cmd('finish'); await page.waitForTimeout(2000);
    assert.equal((await cmd('state')).flow, 'Results');
  }
  await page.setViewportSize({ width: 812, height: 375 });
  await cmd('touch', 'on');
  await cmd('startRace', 'e18', 'easy'); await racing();
  s = await ui();
  assert(s.touch);
  assert(!s.buttons.some(b => ['Fire', 'Secondary', 'SwapWeapon'].includes(b.name)));
  await record('touch-bomb');
  await cmd('startRace', 'e6', 'easy'); await racing('e6');
  s = await ui();
  assert(s.buttons.some(b => b.name === 'Secondary'));
  assert(!s.buttons.some(b => ['Fire', 'SwapWeapon'].includes(b.name)));
  await record('touch-mines');
  await cmd('startRace', 'e1', 'easy'); await racing();
  await click('SwapWeapon');
  assert.equal((await cmd('state')).activePrimary, 0);
  await record('touch-race');
  // Pause is near the top edge, outside the scroll helper's safe region.
  s = await ui();
  const pause = s.buttons.find(b => b.name === 'PauseButton');
  const canvas = await page.locator('canvas').first().boundingBox();
  await page.mouse.click(canvas.x + pause.x * canvas.width, canvas.y + pause.y * canvas.height);
  await page.waitForTimeout(300);
  await click('Hint');
  s = await ui(); assert(s.buttons.some(b => b.name === 'CloseHelp'));
  await record('touch-help');
  await click('CloseHelp'); await click('Resume');
  await cmd('goto', 'main');
  await record('touch-menu');
  await cmd('goto', 'garage');
  await record('touch-garage');
  await click('ChooseSlot1');
  await record('touch-arsenal');
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, evidence, errors }, null, 2));
  console.log('PLAYER EXPERIENCE PASS');
} catch (error) {
  if (page && !page.isClosed()) {
    await page.screenshot({ path: `${output}/failure.png` }).catch(() => {});
    const snapshot = await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null);
    await fs.writeFile(`${output}/failure-state.json`, JSON.stringify(snapshot, null, 2));
  }
  throw error;
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
