// Fresh-profile event discovery through the real menu; no unlock/progress grants.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-event-discovery';
await fs.mkdir(out, { recursive: true });
const tiers = await Promise.all(['easy', 'normal', 'hard'].map(async id => {
  const file = new URL(`../../Assets/Data/Difficulty/Difficulty_${id}.asset`, import.meta.url);
  const asset = await fs.readFile(file, 'utf8');
  const number = key => Number(asset.match(new RegExp(`^  ${key}: ([\\d.]+)$`, 'm'))[1]);
  return { id, count: number('aiCount'), interval: number('aiFireCooldown'), reward: number('rewardMultiplier') };
}));
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
      const home = await cmd('ui');
      const box = await page.locator('canvas').boundingBox();
      for (const name of ['Play', 'ChooseRace']) {
        const action = home.buttons.find(b => b.name === name);
        const label = home.labels.find(l => l.parent === name && l.name === 'Label');
        assert(action?.interactable && action.height >= 44 && action.width >= 44, lang + ' ' + name);
        assert(label && !label.truncated && label.fontPixels >= 15, lang + ' ' + name + ' label');
      }
      const chooser = home.buttons.find(b => b.name === 'ChooseRace');
      await page.mouse.click(box.x + chooser.x * box.width, box.y + chooser.y * box.height);
      await page.waitForTimeout(500);
      const state = await cmd('ui');
      assert.equal(state.screen, 'events');
      evidence.push({ lang, renderer, initial, home, menu: state });
      await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
      if (['ru', 'zh'].includes(lang)) await page.screenshot({ path: `${out}/${lang}-events.png` });
      const cards = state.buttons.filter(b => b.name.startsWith('Start_'));
      assert.equal(cards.length, 24, 'Every race, mission and arena remains available in the list');
      const available = cards.filter(b => b.interactable), locked = cards.filter(b => !b.interactable);
      assert.equal(available.length, 10, 'Fresh players can choose all ten unlocked modes');
      assert(available.some(b => b.name === 'Start_tt1'), 'A new driver can start a solo time trial');
      assert(available.some(b => b.name === 'Start_a1'), 'The arena is available from the first launch');
      assert(available.some(b => b.name === 'Start_m2'), 'The train chase is available from the first launch');
      for (const field of ['Name', 'Stats', 'Stars']) {
        const label = state.labels.find(l => l.parent === 'm2' && l.name === field);
        const oldBoss = state.labels.find(l => l.parent === 'm1' && l.name === field);
        assert(label?.renderedText && !label.truncated && label.fontPixels >= 11.9 && label.text !== oldBoss?.text,
          lang + ' train ' + field + ' is readable and specific to the chase');
      }
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
      for (const id of ['e1', 'e2', 'e3', 'e4', 'e19', 'e20']) {
        const hint = state.labels.find(l => l.parent === id && l.name === 'RouteHint');
        const estimate = state.labels.find(l => l.parent === id && l.name === 'ReferenceTime');
        const button = cards.find(b => b.name === 'Start_' + id);
        assert(hint?.renderedText && !hint.truncated && hint.fontPixels >= 11.9, lang + ' ' + id + ' driving cue');
        assert(estimate?.renderedText.includes('~') && !estimate.truncated && estimate.fontPixels >= 11.9, lang + ' ' + id + ' reference time');
        assert(hint.x * box.width + hint.width / 2 <= button.x * box.width - button.width / 2);
        assert(estimate.y * box.height - estimate.height / 2 > button.y * box.height + button.height / 2);
      }
      assert.equal(state.coins, initial.coins); assert.equal(state.races, initial.races);
      const numbers = new Intl.NumberFormat(lang);
      const difficultyChecks = [];
      let selected = state;
      for (const index of [1, 2, 0]) {
        const tier = tiers[index];
        const tab = selected.buttons.find(b => b.name === 'Tier' + index);
        assert(tab.interactable && tab.width >= 44 && tab.height >= 44, lang + ' tier touch target');
        await page.mouse.click(box.x + tab.x * box.width, box.y + tab.y * box.height);
        await page.waitForTimeout(300);
        selected = await cmd('ui');
        const tierRows = {};
        for (const [id, count] of Object.entries({ e1: tier.count, a1: 5, m1: 5, m2: 1, e18: 5, e6: tier.count })) {
          const button = selected.buttons.find(b => b.name === 'Start_' + id);
          const label = name => selected.labels.find(l => l.parent === id && l.name === name);
          const stats = label('Stats'), difficulty = label('Difficulty');
          const prefix = `${lang} ${tier.id} ${id}`;
          assert(stats.renderedText.includes(String(count)), prefix + ' field or escort count');
          assert(difficulty.renderedText && !difficulty.truncated && difficulty.fontPixels >= 12 && difficulty.lines === 2, prefix + ' both lines fit');
          const [fire, reward] = difficulty.text.split('\n');
          assert(reward.includes('×' + numbers.format(tier.reward)), prefix + ' reward');
          if (id === 'e18' || id === 'e6') assert(!/\d/.test(fire), prefix + ' guns are disabled');
          else assert(fire.includes(numbers.format(id === 'm2' ? 1.6 : tier.interval)), prefix + ' base shot interval');
          assert(button.width >= 44 && button.height >= 44, prefix + ' start touch target');
          assert(difficulty.x * box.width + difficulty.width / 2 <= button.x * box.width - button.width / 2, prefix + ' start button clears the text');
          tierRows[id] = { stats: stats.text, difficulty: difficulty.text, fontPixels: difficulty.fontPixels };
        }
        difficultyChecks.push({ tier: tier.id, rows: tierRows });
      }
      evidence.at(-1).difficulty = difficultyChecks;
      let target;
      for (let attempt = 0; attempt < 35; attempt++) {
        target = (await cmd('ui')).buttons.find(b => b.name === 'Start_e17');
        if (target.y > .35 && target.y < .9) break;
        await page.mouse.move(420, 230); await page.mouse.wheel(0, target.y > .5 ? 180 : -180); await page.waitForTimeout(220);
      }
      assert(target.y > .35 && target.y < .9, 'Space race is reachable by ordinary scrolling');
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
