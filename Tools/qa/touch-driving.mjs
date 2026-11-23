// Real multi-touch input through Chrome's touch device, with no forced touch/debug driver.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-touch/browser';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], evidence = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 640, height: 360 }, hasTouch: true, isMobile: true,
    deviceScaleFactor: 3,
    userAgent: `Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${browser.version()} Mobile Safari/537.36` });
  page = await context.newPage();
  const input = await context.newCDPSession(page);
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8788/?sdk=mock&device=mobile');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i); console.log('GPU', renderer);
  const density = await page.evaluate(() => ({ device: window.devicePixelRatio,
    render: document.querySelector('canvas').width, css: document.querySelector('canvas').getBoundingClientRect().width }));
  assert.equal(density.device, 3);
  assert(Math.abs(density.render - density.css) < 2, 'Mobile rendering must stay capped at CSS resolution on dense screens');
  const point = async (name, id) => {
    const s = await cmd('ui'), b = s.buttons.find(b => b.name === name);
    assert(b?.interactable, `Unavailable ${name}`);
    const box = await page.locator('canvas').boundingBox();
    return { x: box.x + b.x * box.width, y: box.y + b.y * box.height, id };
  };
  const send = (type, touchPoints) => input.send('Input.dispatchTouchEvent', { type, touchPoints });
  const tap = async name => {
    await send('touchStart', [await point(name, 1)]); await page.waitForTimeout(120); await send('touchEnd', []);
    await page.waitForTimeout(300);
  };
  await cmd('startRace', 'e1', 'easy');
  await send('touchStart', [{ x: 320, y: 180, id: 1 }]); await page.waitForTimeout(100); await send('touchEnd', []);
  for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(200);
  const initial = await cmd('ui');
  assert(initial.touch, 'A real screen touch must enable the mobile controls');
  await fs.writeFile(`${output}/initial.json`, JSON.stringify(initial, null, 2));
  await page.screenshot({ path: `${output}/640-driving.png`, scale: 'css' });
  for (const name of ['Left','Right','Fire','Brake','Reset','SwapWeapon','PauseButton','LiveGoals','ToggleMiniMap']) {
    const b = initial.buttons.find(b => b.name === name);
    assert(b && b.width >= 42 && b.height >= 42, `${name}: target ${b?.width} × ${b?.height}; minimum 42 px`);
  }
  const swapLabel = initial.labels.find(t => t.parent === 'SwapWeapon');
  assert(swapLabel.lines === 1 && swapLabel.fontPixels >= 11.9 && !swapLabel.truncated, 'Weapon swap label must fit on one readable line');
  assert(initial.labels.find(t => t.name === 'Hints').fontPixels >= 11.9, 'Touch hints must be readable');
  await tap('LiveGoals');
  const goals = await cmd('ui');
  const goalText = goals.labels.find(t => t.name === 'Goals');
  assert(goalText.fontPixels >= 11.9 && !goalText.truncated, 'Expanded race goals must remain readable');
  await page.screenshot({ path: `${output}/640-goals.png`, scale: 'css' });
  await tap('LiveGoals');
  await page.waitForTimeout(2200);
  const openMap = initial.buttons.find(b => b.name === 'ToggleMiniMap');
  await tap('ToggleMiniMap');
  const before = await cmd('ui');
  const foldedMap = before.buttons.find(b => b.name === 'ToggleMiniMap');
  assert(foldedMap.interactable && foldedMap.width < openMap.width && foldedMap.height >= 43.9);
  assert(before.labels.some(l => l.parent === 'ToggleMiniMap' && l.renderedText === 'Map'));
  evidence.push({ phase: 'folded-map', openMap, foldedMap });
  const fire = await point('Fire', 1), right = await point('Right', 2);
  await send('touchStart', [fire, right]); await page.waitForTimeout(800);
  const held = await cmd('ui');
  assert(held.steer > .3, 'The steering finger must remain active while firing');
  assert.notEqual(held.texts.find(t => t.startsWith('Ammo:')), before.texts.find(t => t.startsWith('Ammo:')), 'Second finger must fire');
  await send('touchEnd', []); await page.waitForTimeout(400);
  const released = await cmd('state');
  assert(Math.abs(released.steer) < Math.abs(held.steer), 'Releasing the steering finger must release full turn input');
  evidence.push({ phase: 'two-finger-steer-and-fire', before, held, released });
  await tap('SwapWeapon'); assert.notEqual((await cmd('state')).activePrimary, initial.activePrimary);
  const resets = (await cmd('state')).resets;
  await tap('Reset'); assert.equal((await cmd('state')).resets, resets + 1);
  // Open pause while another finger holds FIRE, then release everything behind the overlay.
  await send('touchStart', [await point('Fire', 1)]); await page.waitForTimeout(200);
  await send('touchStart', [await point('Fire', 1), await point('PauseButton', 2)]);
  await page.waitForTimeout(150); await send('touchEnd', []); await page.waitForTimeout(400);
  const paused = await cmd('ui');
  assert(paused.buttons.some(b => b.name === 'Resume'));
  await page.waitForTimeout(800); assert(Math.abs((await cmd('state')).raceSeconds - paused.raceSeconds) < .03);
  await tap('Resume');
  await page.waitForTimeout(1800); // Allow any reload started before pause to finish.
  const resumed = await cmd('ui'); await page.waitForTimeout(800); const after = await cmd('ui');
  assert.equal(after.texts.find(t => t.startsWith('Ammo:')), resumed.texts.find(t => t.startsWith('Ammo:')), 'Fire must not remain held after pausing and releasing');
  evidence.push({ phase: 'pause-releases-touch', paused, resumed, after });
  if (process.env.CG_TOUCH_LOCALES === '1') {
    // Rebuild the actual on-screen controls through the public language selector.
    // This catches long translations that fit in menus but wrap on a driving button.
    for (let i = 0; i < 9; i++) {
      await cmd('goto', 'settings'); await tap('LanguageNext');
      const language = (await cmd('ui')).labels.find(t => t.name === 'LanguageName')?.text;
      await cmd('startRace', 'e1', 'easy');
      for (let n = 0; n < 60 && (await cmd('state')).race !== 'Racing'; n++) await page.waitForTimeout(200);
      const localized = await cmd('ui');
      const labels = localized.labels.filter(t => ['SwapWeapon', 'Fire', 'Brake', 'Reset', 'Secondary'].includes(t.parent));
      evidence.push({ phase: 'localized-touch', language, labels });
      await fs.writeFile(`${output}/locales.json`, JSON.stringify(evidence.filter(e => e.phase === 'localized-touch'), null, 2));
      for (const label of labels) {
        assert.equal(label.lines, 1, `${language}: ${label.parent} must fit on one line`);
        assert(!label.truncated, `${language}: ${label.parent} must not truncate`);
        assert(label.fontPixels >= 11, `${language}: ${label.parent} text is too small`);
      }
      console.log('TOUCH LANGUAGE PASS', language);
    }
  }
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, density, evidence, errors }, null, 2));
  console.log('TOUCH DRIVING PASS');
} catch (error) {
  if (page && !page.isClosed()) await page.screenshot({ path: `${output}/failure.png`, scale: 'css' }).catch(() => {});
  throw error;
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
