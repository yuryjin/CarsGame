// Real keyboard fire on the opening straight. No damage/finish/autopilot injection.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-polish-20261024/combat-browser';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], runs = [];
try {
  for (const [width, height] of [[1280, 720], [640, 360]]) {
    const context = await browser.newContext({ viewport: { width, height } });
    try {
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      await page.goto(process.env.URL || 'http://127.0.0.1:8795/?sdk=mock');
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      await page.waitForTimeout(1000);
      const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(100);
        const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
        assert.equal(s.error || '', ''); return s;
      };
      const run = { width, height, renderer, samples: [], hits: [], shotsSeen: false };
      runs.push(run);
      await cmd('startRace', 'e1', 'easy');
      for (let i = 0; i < 80 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
      assert.equal((await cmd('state')).race, 'Racing');
      await page.locator('canvas').click({ position: { x: width / 2, y: height / 2 } });
      try {
        await page.keyboard.down('w'); await page.keyboard.down('Control');
        const end = Date.now() + 14000;
        let captured = false;
        while (Date.now() < end) {
          const state = await cmd('ui'); run.samples.push(state);
          run.shotsSeen ||= state.liveShells > 0;
          const label = state.labels.find(l => l.name === 'HitOutcome' && l.text);
          if (label) {
            run.hits.push({ raceSeconds: state.raceSeconds, label });
            if (process.env.CG_BASELINE !== '1') {
              assert(label.fontPixels >= 13.9, 'Hit outcome must remain readable');
              assert(!label.truncated && label.lines === 1, 'Hit outcome must fit on one line');
            }
            if (!captured) {
              await page.screenshot({ path: `${output}/hit-${width}.png` }); captured = true;
            }
          }
        }
      } finally { await page.keyboard.up('Control'); await page.keyboard.up('w'); }
      assert(run.shotsSeen, 'Actual Ctrl input must launch projectiles');
      await page.screenshot({ path: `${output}/after-${width}.png` });
      assert(run.hits.length > 0, 'At least one actual opening-straight hit must be visible');
      if (process.env.CG_BASELINE !== '1') assert(run.hits.some(h => /-\d+/.test(h.label.text)), 'A real hit must report damage');
      console.log('PASS combat', width, 'hit samples', run.hits.length);
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
} finally {
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ runs, errors }, null, 2));
  await browser.close();
}
