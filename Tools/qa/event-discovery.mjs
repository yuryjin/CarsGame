// Fresh-profile event discovery through the real menu; no unlock/progress grants.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-event-discovery';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const lang of (process.env.CG_EVENT_LANGS || 'ru,en,tr,zh,de,es,fr,pt,uk').split(',')) {
    const context = await browser.newContext({ viewport: { width: 640, height: 360 } });
    try {
      await context.addInitScript(lang => localStorage.setItem('cars.mock.playerdata', JSON.stringify({ save: JSON.stringify({ version: 5, updatedAtUnixMs: Date.now(), settings: { lang } }) })), lang);
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      await page.goto(process.env.URL || 'http://127.0.0.1:8812/?sdk=mock');
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
        const state = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
        assert.equal(state.error || '', ''); return state;
      };
      const renderer = await page.evaluate(() => { const gl = document.querySelector('canvas').getContext('webgl2'), e = gl.getExtension('WEBGL_debug_renderer_info'); return gl.getParameter(e ? e.UNMASKED_RENDERER_WEBGL : gl.RENDERER); });
      assert.match(renderer, /NVIDIA/i);
      const initial = await cmd('state');
      await cmd('goto', 'events'); await page.waitForTimeout(500);
      const state = await cmd('ui');
      evidence.push({ lang, renderer, initial, menu: state });
      await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
      if (['ru', 'zh'].includes(lang)) await page.screenshot({ path: `${out}/${lang}-events.png` });
      const cards = state.buttons.filter(b => b.name.startsWith('Start_'));
      assert.equal(cards.length, 21, 'Every race and mission remains available in the list');
      const available = cards.filter(b => b.interactable), locked = cards.filter(b => !b.interactable);
      assert.equal(available.length, 7, 'Fresh players retain the original seven available modes');
      assert(Math.max(...available.map(b => b.y)) < Math.min(...locked.map(b => b.y)), 'Playable modes precede locked career races');
      for (const parent of ['AvailableEventsTitle', 'LockedEventsTitle']) {
        const label = state.labels.find(l => l.parent === parent && l.name === 'Title');
        assert(label && label.renderedText && !label.truncated && label.fontPixels >= 12.9, `${lang}: readable localized group heading ${parent}`);
      }
      for (const button of locked) {
        const id = button.name.slice('Start_'.length);
        const status = state.labels.find(l => l.parent === id && l.name === 'Status');
        assert(status && status.renderedText && !status.truncated && status.fontPixels >= 11.9 && status.lines <= 2,
          `${lang}: prerequisite fits the locked card ${id}`);
      }
      const first = cards.find(b => b.name === 'Start_e1');
      assert(first.y > .2 && first.y < .95, 'First playable race is visible without passing cup cards');
      assert(!cards.find(b => b.name === 'Start_e2').interactable, 'Career gate is preserved');
      assert.equal(state.coins, initial.coins); assert.equal(state.races, initial.races);
      let target;
      for (let attempt = 0; attempt < 35; attempt++) {
        target = (await cmd('ui')).buttons.find(b => b.name === 'Start_e17');
        if (target.y > .35 && target.y < .9) break;
        await page.mouse.move(420, 230); await page.mouse.wheel(0, target.y > .5 ? 180 : -180); await page.waitForTimeout(220);
      }
      assert(target.y > .35 && target.y < .9, 'Space race is reachable by ordinary scrolling');
      const box = await page.locator('canvas').boundingBox();
      await page.mouse.click(box.x + target.x * box.width, box.y + target.y * box.height);
      await page.waitForTimeout(450);
      const started = await cmd('state');
      assert.equal(started.evt, 'e17');
      assert(['Countdown', 'Racing'].includes(started.race));
      assert.equal(started.races, initial.races, 'Starting an available event awards no race completion');
      evidence.at(-1).started = started;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
  console.log('EVENT DISCOVERY PASS', evidence.length, 'locales');
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
  await browser.close();
}
