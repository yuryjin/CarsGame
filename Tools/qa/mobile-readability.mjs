// Small-screen interactions and measured UI bounds. Run only through webtest.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-readable/browser';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 812, height: 375 }, hasTouch: true });
  page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8789/?sdk=mock&device=mobile');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args);
    await page.waitForTimeout(300);
    const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
    assert.equal(s.error || '', ''); return s;
  };
  const ui = () => cmd('ui');
  const click = async name => {
    for (let i = 0; i < 18; i++) {
      const s = await ui(), b = s.buttons.find(b => b.name === name);
      assert(b?.interactable, `Unavailable ${name}`);
      const box = await page.locator('canvas').first().boundingBox();
      const top = ['Back', 'PauseButton'].includes(name) ? 0 : .25;
      if (b.x > 0 && b.x < 1 && b.y > top && b.y < .91) {
        await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height);
        await page.waitForTimeout(400); return;
      }
      await page.mouse.move(box.x + box.width * .72, box.y + box.height * .67);
      await page.mouse.wheel(0, b.y > .5 ? 160 : -160);
      await page.waitForTimeout(200);
    }
    throw new Error(`Cannot reach ${name}`);
  };
  const record = async name => {
    evidence.push({ name, state: await ui() });
    await page.screenshot({ path: `${output}/${name}.png` });
    console.log('PASS', name);
  };
  const label = (s, name, parent) => {
    const t = s.labels.find(t => t.name === name && (!parent || t.parent === parent));
    assert(t, `Missing label ${name}/${parent}`); return t;
  };
  const target = (s, name, min = 42) => {
    const b = s.buttons.find(b => b.name === name);
    assert(b, `Missing ${name}`); assert(b.width >= min && b.height >= min, `${name} target ${b.width} × ${b.height}`);
    return b;
  };
  const checkMain = (s, width, height) => {
    for (const name of ['Play','garage','Practice','leaders','settings']) {
      const b = target(s, name, 44);
      assert(b.interactable && b.x * width >= b.width / 2 && b.x * width + b.width / 2 <= width &&
        b.y * height >= b.height / 2 && b.y * height + b.height / 2 <= height, `${name} must be fully visible on entry`);
      const text = label(s, 'Label', name);
      assert(text.fontPixels >= 15 && !text.truncated, `Unreadable destination: ${text.text}`);
    }
  };
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i);
  console.log('GPU', renderer);
  for (const [width, height] of [[812,375], [640,360]]) {
    await page.setViewportSize({ width, height }); await page.waitForTimeout(500);
    await cmd('goto', 'main');
    let s = await ui();
    checkMain(s, width, height);
    assert(label(s, 'Details').fontPixels >= 13, 'Main details must be readable');
    await record(`${width}-main`);
    await cmd('goto', 'garage');
    s = await ui(); target(s, 'PreviousBody'); target(s, 'NextBody'); target(s, 'RotateLeft');
    assert(label(s, 'PreviewName').fontPixels >= 17);
    await record(`${width}-garage`);
    await click('ChooseSlot1');
    s = await ui(); target(s, 'Back'); target(s, 'Filter_Control');
    await click('Filter_Control');
    s = await ui();
    assert(label(s, 'Stats', 'Arsenal_6').fontPixels >= 13, 'Weapon stats must be readable');
    assert(!label(s, 'Stats', 'Arsenal_6').truncated);
    await record(`${width}-arsenal`);
    await page.keyboard.press('Escape'); await page.waitForTimeout(500);
    assert.equal((await ui()).screen, 'garage', 'Escape must follow the arsenal Back route');
    await cmd('goto', 'settings');
    s = await ui(); assert(label(s, 'LanguageName').fontPixels >= 13);
    for (const name of ['SoundVolume', 'MusicVolume']) {
      const slider = s.sliders.find(slider => slider.name === name);
      assert(slider?.interactable && slider.y * height + slider.height / 2 <= height && slider.height >= 44, `${name} fits on entry`);
    }
    await record(`${width}-settings`);
    await cmd('touch', 'on'); await cmd('startRace', 'e1', 'easy');
    for (let i = 0; i < 40 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(250);
    assert.equal((await cmd('state')).race, 'Racing');
    s = await ui(); assert(label(s, 'ActiveWeapon').fontPixels >= 11.9);
    if (width === 812) {
      await page.setViewportSize({ width: 375, height: 812 }); await page.waitForTimeout(700);
      const before = await ui();
      await page.waitForTimeout(1500);
      const after = await ui();
      evidence.push({ name: 'portrait-race-clock-observation', before, state: after });
      assert(Number.isFinite(before.raceSeconds), 'The build must expose the authoritative race clock');
      assert(Math.abs(after.raceSeconds - before.raceSeconds) < .03, 'Rotation prompt must pause the race');
      console.log('PORTRAIT CLOCK', before.raceSeconds, after.raceSeconds);
      await page.setViewportSize({ width, height }); await page.waitForTimeout(500);
      assert((await cmd('state')).raceSeconds > after.raceSeconds + .2, 'Returning to landscape must resume an unpaused race');
    }
    await click('PauseButton'); target(await ui(), 'Hint'); await click('Hint');
    s = await ui(); target(s, 'CloseHelp');
    const lessons = label(s, 'Lessons');
    assert(lessons.fontPixels >= 16, `Help font ${lessons.fontPixels}`);
    assert(!lessons.truncated, 'Lessons must scroll instead of truncating');
    await record(`${width}-help`);
    const box = await page.locator('canvas').first().boundingBox();
    await page.mouse.move(box.x + box.width * .6, box.y + box.height * .5);
    await page.mouse.wheel(0, 700); await page.waitForTimeout(500);
    const afterScroll = label(await ui(), 'Lessons');
    const back = s.buttons.find(b => b.name === 'CloseHelp');
    const bottom = lessons.y * height + lessons.height / 2;
    if (bottom > back.y * height - back.height / 2)
      assert(afterScroll.y < lessons.y, 'Help extending below the visible text area must scroll upward');
    await record(`${width}-help-bottom`);
    if (width === 812) {
      const clock = (await cmd('state')).raceSeconds;
      await page.setViewportSize({ width: 375, height: 812 }); await page.waitForTimeout(400);
      await page.setViewportSize({ width, height }); await page.waitForTimeout(500);
      assert(Math.abs((await cmd('state')).raceSeconds - clock) < .03, 'Rotation must preserve a user pause');
    }
    const pausedClock = (await cmd('state')).raceSeconds;
    await page.keyboard.press('Escape'); await page.waitForTimeout(500);
    const afterBack = await ui();
    assert(!afterBack.buttons.some(b => b.name === 'CloseHelp'), 'Escape must close the help layer');
    assert(afterBack.buttons.some(b => b.name === 'Resume'), 'Escape from help must return to pause');
    assert(Math.abs(afterBack.raceSeconds - pausedClock) < .03, 'Closing help must not resume the race');
    await click('Hint');
    assert(Math.abs(label(await ui(), 'Lessons').y - lessons.y) < .02, 'Help must reopen at the beginning');
    await click('CloseHelp'); await click('Resume');
    await cmd('goto', 'main');
  }
  // Exercise all nine existing languages on the smallest layout using the public settings controls.
  await cmd('goto', 'settings');
  for (const language of ['ru','en','tr','zh','de','es','fr','pt','uk']) {
    await click('LanguageNext');
    const s = await ui();
    assert(label(s, 'LanguageName').fontPixels >= 13);
    assert(!label(s, 'LanguageName').truncated);
    for (const text of s.labels.filter(t => t.name === 'Name')) {
      assert(text.fontPixels >= 13 && !text.truncated, `${language}: unreadable setting ${text.text}`);
    }
    await cmd('goto', 'main');
    checkMain(await ui(), 640, 360);
    await cmd('goto', 'settings');
    if (['ru','zh','de'].includes(language)) await record(`640-settings-${language}`);
    if (['zh','de'].includes(language)) {
      await cmd('goto', 'garage'); await click('ChooseSlot1'); await click('Filter_Control');
      const stats = label(await ui(), 'Stats', 'Arsenal_6');
      assert(stats.fontPixels >= 13 && !stats.truncated, `${language}: unreadable weapon statistics`);
      await record(`640-arsenal-${language}`);
      await cmd('goto', 'settings');
    }
  }
  await page.setViewportSize({ width: 375, height: 812 }); await page.waitForTimeout(500);
  assert((await ui()).texts.some(t => t.startsWith('RotatePhone:')), 'Portrait mode must show its rotation prompt');
  await record('portrait-rotation-prompt');
  await page.setViewportSize({ width: 1280, height: 720 }); await page.waitForTimeout(500);
  await cmd('goto', 'garage'); await record('desktop-garage-after-resize');
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, evidence, errors }, null, 2));
  console.log('MOBILE READABILITY PASS');
} catch (error) {
  if (page && !page.isClosed()) {
    await page.screenshot({ path: `${output}/failure.png` }).catch(() => {});
    await fs.writeFile(`${output}/failure-state.json`, JSON.stringify(await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null), null, 2));
  }
  throw error;
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
