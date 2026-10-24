// Real touch activation/secondary use, readable scrolling help, all nine locales.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-combat-help/mobile';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  const context = await browser.newContext({ viewport: { width: 640, height: 360 }, hasTouch: true, isMobile: true,
    userAgent: `Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${browser.version()} Mobile Safari/537.36`,
    storageState: process.env.CG_SOAK_STATE || '/tmp/cars-journey-state.json' });
  const page = await context.newPage(), input = await context.newCDPSession(page);
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8789/?sdk=mock&device=mobile');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(220);
    const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
    assert.equal(s.error || '', ''); return s;
  };
  const tap = async name => {
    const b = (await cmd('ui')).buttons.find(b => b.name === name); assert(b?.interactable, name);
    const box = await page.locator('canvas').boundingBox();
    await input.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: box.x + b.x * box.width, y: box.y + b.y * box.height, id: 1 }] });
    await page.waitForTimeout(100); await input.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await page.waitForTimeout(250);
  };
  const languages = [];
  let scrollingChecks = 0;
  const choices = Number(process.env.CG_HELP_COUNT || 10); // platform default plus nine explicit locales
  await cmd('goto', 'settings');
  for (let i = 0; i < Number(process.env.CG_HELP_SKIP || 0); i++) await tap('LanguageNext');
  for (let i = 0; i < choices; i++) {
    await cmd('goto', 'settings');
    const language = (await cmd('ui')).labels.find(l => l.name === 'LanguageName')?.text;
    languages.push(language);
    await cmd('startRace', 'e12', 'easy');
    await input.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: 320, y: 180, id: 1 }] });
    await page.waitForTimeout(120);
    await input.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    for (let n = 0; n < 60 && (await cmd('state')).race !== 'Racing'; n++) await page.waitForTimeout(200);
    const initial = await cmd('ui'); assert.equal(initial.race, 'Racing'); assert(initial.touch);
    assert(!initial.buttons.some(b => b.name === 'Fire' || b.name === 'SwapWeapon'));
    const secondary = initial.buttons.find(b => b.name === 'Secondary'); assert(secondary?.interactable);
    if (i === 0) {
      const ammo = s => Number(s.labels.find(l => l.name === 'Secondary' && l.parent === 'SecondaryPanel')?.text.match(/(\d+)\s*\//)?.[1]);
      const before = await cmd('ui'); await tap('Secondary'); const after = await cmd('ui');
      assert.equal(ammo(after), ammo(before) - 1, 'An actual screen tap must lay one mine');
      evidence.push({ phase: 'real-touch-mine', before, after });
    }
    await tap('PauseButton'); await tap('Hint');
    const top = await cmd('ui'), text = top.labels.find(l => l.name === 'Lessons');
    assert(text && text.fontPixels >= 16 && !text.truncated, `${language}: help must remain readable`);
    assert(!text.text.includes('Ctrl') && !text.text.includes('Shift'), `${language}: touch help must not advertise keyboard commands`);
    assert(text.text.includes(secondary.text), `${language}: help must name the actual mine button`);
    const clock = top.raceSeconds;
    // A real finger swipe inside the help viewport must reach the lower text while paused.
    await input.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: 420, y: 220, id: 1 }] });
    for (let y = 205; y >= 75; y -= 13) {
      await input.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: 420, y, id: 1 }] });
      await page.waitForTimeout(25);
    }
    await input.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    await page.waitForTimeout(500);
    const bottom = await cmd('ui');
    const fixedBack = top.buttons.find(b => b.name === 'CloseHelp');
    if (text.y * 360 + text.height / 2 > fixedBack.y * 360 - fixedBack.height / 2) {
      assert(bottom.labels.find(l => l.name === 'Lessons').y < text.y, `${language}: overflowing help must scroll`);
      scrollingChecks++;
    }
    assert(Math.abs(bottom.raceSeconds - clock) < .03, 'Reading help must keep the race paused');
    const back = bottom.buttons.find(b => b.name === 'CloseHelp');
    assert(back && back.y > 0 && back.y < 1 && back.height >= 42, 'Back must remain visible while scrolling');
    evidence.push({ phase: 'localized-mobile-help', language, top, bottom });
    if ([0,3,4].includes(i)) await page.screenshot({ path: `${out}/help-${i}.png`, scale: 'css' });
    await tap('CloseHelp'); await tap('Hint');
    const reopened = await cmd('ui');
    assert(Math.abs(reopened.labels.find(l => l.name === 'Lessons').y - text.y) < .02, 'Reopening help must return to the beginning');
    await cmd('goto', 'settings'); await tap('LanguageNext');
    console.log('HELP LANGUAGE PASS', language);
  }
  assert.equal(new Set(languages).size, choices, 'Every requested language-selector choice must be visited');
  assert(scrollingChecks > 0, 'At least one translated help page must exercise scrolling');
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ renderer, languages, evidence, errors }, null, 2));
  console.log('MOBILE HELP PASS');
} catch (e) {
  await fs.writeFile(`${out}/failure.json`, JSON.stringify({ error: String(e), evidence, errors }, null, 2));
  throw e;
} finally { await browser.close(); }
