// Charge and release a real drift through keyboard controls; inspect the transient HUD.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-endurance-20261024/drift-browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
const languages = (process.env.CG_DRIFT_LANGS || 'ru,en,tr,zh,de,es,fr,pt,uk').split(',');
try {
  for (const lang of languages) {
    const context = await browser.newContext({ viewport: { width: 640, height: 360 } });
    let page;
    try {
      await context.addInitScript(lang => localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify({ version: 5, updatedAtUnixMs: Date.now(), settings: { lang, hintsSeen: 65535 } }) })), lang);
      page = await context.newPage();
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
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(80);
        const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
        assert.equal(s.error || '', ''); return s;
      };
      await cmd('startRace', 'e1', 'easy');
      for (let i = 0; i < 80 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
      assert.equal((await cmd('state')).race, 'Racing');
      await page.locator('canvas').focus();
      await page.keyboard.down('w'); await page.waitForTimeout(3000);
      const before = await cmd('state');
      assert(before.speedKmh > 30, 'Control: actual acceleration reached drift speed');
      await page.keyboard.down('d'); await page.keyboard.down('Space');
      const end = Date.now() + 1000, charge = [];
      while (Date.now() < end) {
        const state = await cmd('ui');
        const label = state.labels.find(t => t.name === 'Action' && t.parent === 'DriftMeter');
        if (label?.text) charge.push({ state, label });
      }
      await page.keyboard.up('Space'); await page.keyboard.up('d');
      await page.waitForTimeout(150);
      const released = await cmd('ui');
      await page.screenshot({ path: `${out}/${lang}-boost.png` });
      await page.keyboard.up('w');
      const boost = released.labels.find(t => t.name === 'Action' && t.parent === 'DriftMeter');
      evidence.push({ lang, renderer, before, charge, released, boost });
      assert(charge.length > 0, 'Actual brake+steer must show a drift charge meter');
      assert(released.driftBoosts > before.driftBoosts, 'Releasing the actual brake must award the drift boost');
      assert(boost?.text && boost.text !== charge.at(-1).label.text, 'Release confirmation must replace the charging instruction');
      for (const label of [...charge.map(c => c.label), boost]) {
        assert(label.fontPixels >= 13.9, `${lang}: drift label is only ${label.fontPixels}px`);
        assert(!label.truncated && label.lines <= 2, `${lang}: drift instruction must fit in two readable lines`);
        assert(label.x > .1 && label.x < .9 && label.y > .2 && label.y < .85);
      }
      console.log('PASS drift', lang, 'charge samples', charge.length, 'boosts', released.driftBoosts);
    } catch (error) {
      if (page && !page.isClosed()) await page.screenshot({ path: `${out}/failure-${lang}.png` }).catch(() => {});
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
  console.log('DRIFT FEEDBACK PASS');
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
  await browser.close();
}
