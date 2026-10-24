// Measured vital readouts in the actual WebGL HUD, with keyboard and touch layouts.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-endurance-20261024/vitals';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [], failures = [];
const languages = (process.env.CG_HUD_LANGS || 'ru,en,tr,zh,de,es,fr,pt,uk').split(',');
const minima = { Lap: 12, Position: 16, Timer: 16, HpText: 12, Ammo: 12, ActiveWeapon: 14, Speed: 18, Unit: 9, Goals: 12 };
try {
  for (const lang of languages) {
    const context = await browser.newContext({ viewport: { width: 640, height: 360 }, hasTouch: true });
    try {
      await context.addInitScript(lang => localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify({ version: 5, updatedAtUnixMs: Date.now(), settings: { lang, hintsSeen: 65535 } }) })), lang);
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      await page.goto(process.env.URL || 'http://127.0.0.1:8801/?sdk=mock');
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
        const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
        assert.equal(s.error || '', ''); return s;
      };
      for (const [width, height] of [[640,360], [812,375], [1280,720]]) {
        await page.setViewportSize({ width, height }); await page.waitForTimeout(400);
        for (const touch of [false, true]) {
          await cmd('touch', touch ? 'on' : 'off');
          await cmd('startRace', 'e1', 'easy');
          for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
          await page.waitForTimeout(150);
          const state = await cmd('ui'), name = `${lang}-${width}-${touch ? 'touch' : 'keys'}`;
          evidence.push({ name, renderer, state });
          try {
            assert.equal(state.race, 'Racing'); assert.equal(state.touch, touch);
            for (const [name, min] of Object.entries(minima)) {
              const label = state.labels.find(l => l.name === name);
              assert(label, `Missing ${name}`);
              assert(label.fontPixels >= min - .1, `${name}: ${label.fontPixels.toFixed(1)}px < ${min}px`);
              assert(!label.truncated, `${name}: truncated ${label.text}`);
              assert(label.lines <= 1, `${name}: vital readout wraps`);
              assert(label.x * width - label.width / 2 >= -1 && label.x * width + label.width / 2 <= width + 1, `${name}: outside screen horizontally`);
              assert(label.y * height - label.height / 2 >= -1 && label.y * height + label.height / 2 <= height + 1, `${name}: outside screen vertically`);
            }
            const labels = Object.keys(minima).map(n => state.labels.find(l => l.name === n));
            for (let i = 0; i < labels.length; i++) for (let j = i + 1; j < labels.length; j++) {
              const a = labels[i], b = labels[j];
              const dx = Math.abs(a.x - b.x) * width, dy = Math.abs(a.y - b.y) * height;
              assert(dx >= (a.width + b.width) / 2 - 1 || dy >= (a.height + b.height) / 2 - 1, `${a.name}/${b.name}: readout rectangles overlap`);
            }
            console.log('PASS', name);
          } catch (error) {
            failures.push({ name, error: String(error) }); console.log('FAIL', name, String(error));
          }
          if (width === 640 && ['ru','en','zh'].includes(lang)) await page.screenshot({ path: `${out}/${name}.png` });
          await cmd('goto', 'main');
        }
      }
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []); assert.deepEqual(failures, []);
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors, failures }, null, 2));
  await browser.close();
}
