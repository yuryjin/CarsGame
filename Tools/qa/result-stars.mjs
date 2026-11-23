// Controlled UI fixtures: seed a mock profile and use QA finish. Not a driving/balance test.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-polish-20261024/result-stars';
const eventId = process.env.CG_STAR_EVENT || 'e1';
const languages = process.env.CG_STAR_LANGS?.split(',') || ['ru', 'en', 'tr', 'zh', 'de', 'es', 'fr', 'pt', 'uk'];
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const [lang, previous] of [...languages.map(lang => [lang, 7]), ...(languages.includes('ru') ? [['ru', 0]] : [])]) {
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
    let page;
    try {
      await context.addInitScript(({ lang, previous, eventId }) => {
        const save = { version: 5, updatedAtUnixMs: Date.now(), settings: { lang, hintsSeen: 65535 },
          progress: { bests: [{ eventId, bestTimeMs: 60000, stars: previous }] } };
        localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify(save) }));
      }, { lang, previous, eventId });
      page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      const url = new URL(process.env.URL || 'http://127.0.0.1:8795/?sdk=mock');
      url.searchParams.set('lang', lang);
      await page.goto(url.href);
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      await page.waitForTimeout(1000);
      const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(250);
        const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
        assert.equal(s.error || '', ''); return s;
      };
      const start = async daily => {
        await cmd(...(daily ? ['daily'] : ['startRace', eventId, 'easy']));
        for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(200);
        assert.equal((await cmd('state')).race, 'Racing');
      };
      await start(false);
      await cmd('finish'); await page.waitForTimeout(1500);
      for (const [width, height] of [[1280, 720], [812, 375], [640, 360]]) {
        await page.setViewportSize({ width, height }); await page.waitForTimeout(600);
        let state = await cmd('ui');
        assert.equal(state.flow, 'Results');
        assert.equal(state.labels.filter(t => t.name === 'StarStatus').length, 3);
        assert(state.labels.find(t => t.name === 'StarsTitle').text.includes('2/3'), 'Only this race counts toward the header');
        const icon = star => state.labels.find(t => t.name === 'StarIcon' && t.parent === `Star${star}`).text;
        assert.equal(icon(0), '★'); assert.equal(icon(1), '☆'); assert.equal(icon(2), '★');
        const current = state.labels.find(t => t.name === 'StarStatus' && t.parent === 'Star0').text;
        const missed = state.labels.find(t => t.name === 'StarStatus' && t.parent === 'Star1').text;
        assert.notEqual(current, missed);
        const goal = state.labels.find(t => t.name === 'StarDesc' && t.parent === 'Star1');
        if (eventId === 'e1') assert.match(goal.renderedText, /0\D+2/, lang + ' current run shows zero of two wrecks');
        if (lang === 'ru') assert.match(missed, previous ? /раньше.*сохранена/ : /Не получена/);
        // Calibrate UGUI's scroll sensitivity instead of treating wheel units as screen
        // pixels: that oscillates between the ends and captures a clipped card.
        const target = height < 500 ? .25 : .29;
        let pixelsPerWheelUnit = null;
        for (let i = 0; i < 12; i++) {
          const title = state.labels.find(t => t.name === 'StarsTitle');
          const error = (title.y - target) * height;
          if (Math.abs(error) < 3) break;
          const step = pixelsPerWheelUnit ? Math.max(-8, Math.min(8, error / pixelsPerWheelUnit)) : Math.sign(error) * 2;
          await page.mouse.move(width * .28, height * .55);
          await page.mouse.wheel(0, step);
          await page.waitForTimeout(220); state = await cmd('ui');
          const moved = (title.y - state.labels.find(t => t.name === 'StarsTitle').y) * height;
          if (Math.abs(moved) > .5) pixelsPerWheelUnit = Math.abs(moved / step);
        }
        const rows = state.labels.filter(t => ['StarStatus', 'StarDesc', 'StarsTitle'].includes(t.name));
        for (const t of rows) {
          assert(!t.truncated, `${lang}/${width}: truncated ${t.name}`);
          assert(t.fontPixels >= 14, `${lang}/${width}: small ${t.name}`);
          assert(t.lines <= (t.name === 'StarDesc' ? 2 : 1), `${lang}/${width}: wrapped ${t.name} exceeds its row`);
        }
        const footer = state.buttons.filter(b => /^ResultAction[0-2]$/.test(b.name));
        assert.equal(footer.length, 3);
        assert(footer.every(b => b.y > 0 && b.y < 1 && b.height >= 42));
        const footerTop = Math.min(...footer.map(b => b.y * height - b.height / 2));
        const title = rows.find(t => t.name === 'StarsTitle');
        assert(Math.abs(title.y - target) * height < 5, `${lang}/${width}: card could not be brought into view`);
        assert(rows.every(t => t.y * height + t.height / 2 < footerTop - 10), `${lang}/${width}: star card is clipped by the result footer`);
        evidence.push({ lang, previous, eventId, width, height, renderer, state });
        if (lang === 'ru' || width === 640)
          await page.screenshot({ path: `${output}/${lang}-${previous}-${width}.png` });
      }
      await start(true);
      assert(!(await cmd('ui')).buttons.some(b => b.name === 'LiveGoals'), 'Daily HUD must not promise campaign stars');
      await cmd('finish'); await page.waitForTimeout(1000);
      const daily = await cmd('ui');
      assert(!daily.labels.some(t => t.name === 'StarsTitle'), 'Daily results must not claim 0/3 stars');
      evidence.push({ lang, phase: 'daily', state: daily });
      console.log('PASS', lang, previous, 'stars, narrow layouts, daily');
    } catch (error) {
      if (page && !page.isClosed()) {
        await page.screenshot({ path: `${output}/failure-${lang}-${previous}.png` }).catch(() => {});
        evidence.push({ lang, previous, phase: 'failure', error: String(error),
          state: await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null) });
      }
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
  console.log('RESULT STARS PASS');
} finally {
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
  await browser.close();
}
