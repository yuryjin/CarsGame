// Compare actual glyph metrics and tap rectangles on the same 640 × 360 WebGL canvas.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-text-size';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const lang of ['de', 'fr', 'uk']) {
    const measured = [];
    for (const [largeText, touchLargeButtons] of [[false, false], [true, false], [false, true]]) {
      const context = await browser.newContext({ viewport: { width: 640, height: 360 }, hasTouch: true, isMobile: true });
      try {
        await context.addInitScript(settings => localStorage.setItem('cars.mock.playerdata', JSON.stringify({
          save: JSON.stringify({ version: 6, updatedAtUnixMs: Date.now(), settings })
        })), { lang, hintsSeen: 65535, largeText, touchLargeButtons });
        const page = await context.newPage();
        page.on('pageerror', e => errors.push(e.message));
        page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
        await page.goto(process.env.URL || 'http://127.0.0.1:8837/?sdk=mock&device=mobile');
        await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)), null, {timeout: 180000});
        const renderer = await page.evaluate(() => {
          const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
          return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
        });
        assert.match(renderer, /NVIDIA/i);
        const cmd = async (...args) => {
          await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
          const state = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
          assert.equal(state.error || '', ''); return state;
        };
        await cmd('touch', 'on'); await cmd('startRace', 'e1', 'normal');
        for (let i = 0; i < 70 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
        const goal = (await cmd('ui')).buttons.find(b => b.name === 'LiveGoals');
        assert(goal?.interactable); await page.touchscreen.tap(goal.x * 640, goal.y * 360); await page.waitForTimeout(500);
        const hud = await cmd('ui');
        const labels = Object.fromEntries(['Goals', 'Ammo', 'ActiveWeapon'].map(k => [k, hud.labels.find(l => l.name === k)]));
        for (const label of Object.values(labels)) {
          assert(label && !label.truncated && label.text, JSON.stringify(label));
          assert(label.x * 640 - label.width / 2 >= -1 && label.x * 640 + label.width / 2 <= 641);
          assert(label.y * 360 - label.height / 2 >= -1 && label.y * 360 + label.height / 2 <= 361);
        }
        assert(labels.Goals.lines >= 3, 'Expanded objectives');
        const controls = Object.fromEntries(['Left', 'Right', 'Fire', 'PauseButton'].map(k => [k, hud.buttons.find(b => b.name === k)]));
        await cmd('finish'); await page.waitForTimeout(3000);
        const results = await cmd('ui');
        assert.equal(results.screen, 'results');
        const rows = results.labels.filter(l => l.name === 'StarDesc' || l.name === 'StarStatus');
        assert.equal(rows.length, 6);
        assert(rows.every(l => !l.truncated && l.text));
        const actions = results.buttons.filter(b => /^ResultAction[0-2]$/.test(b.name));
        assert.equal(actions.length, 3);
        for (const b of actions) {
          assert(b.interactable && b.height >= 43.5 && b.y * 360 + b.height / 2 <= 361);
        }
        measured.push({labels, controls});
        evidence.push({lang, largeText, touchLargeButtons, renderer, labels, controls, rows, actions});
      } finally { await context.close(); }
    }
    const [base, type, touch] = measured;
    for (const key of Object.keys(base.controls)) {
      for (const dimension of ['width', 'height']) assert(Math.abs(base.controls[key][dimension] - type.controls[key][dimension]) < 1);
    }
    assert(touch.controls.Left.width > base.controls.Left.width * 1.2);
    for (const key of ['Goals','Ammo']) {
      assert(type.labels[key].fontPixels > base.labels[key].fontPixels * 1.1, key);
      assert(Math.abs(touch.labels[key].fontPixels - base.labels[key].fontPixels) < 1, key);
    }
    console.log('PASS independent text and buttons', lang);
  }
  assert.deepEqual(errors, []);
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({evidence, errors}, null, 2));
  await browser.close();
}
