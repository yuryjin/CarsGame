// Real menu/touch interactions; physics-based completion is covered by PracticePlayTests.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-practice';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
const languages = (process.env.CG_PRACTICE_LANGS || 'ru,en,zh').split(',');
try {
  for (const [width, height] of [[1280,720], [812,375], [640,360]]) for (const language of languages) {
    const mobile = width < 1000;
    const context = await browser.newContext({ viewport: { width, height }, hasTouch: mobile, isMobile: mobile });
    const page = await context.newPage();
    try {
      const input = await context.newCDPSession(page);
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      const url = new URL(process.env.URL || 'http://127.0.0.1:8830/?sdk=mock');
      url.searchParams.set('lang', language);
      if (mobile) url.searchParams.set('device', 'mobile');
      await page.goto(url.href);
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
        const state = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
        assert.equal(state.error || '', ''); return state;
      };
      const click = async (name, inMenu = true) => {
        for (let i = 0; i < 30; i++) {
          const button = (await cmd('ui')).buttons.find(b => b.name === name);
          assert(button?.interactable, `Missing button ${name}`);
          const top = button.y * height - button.height / 2, bottom = top + button.height;
          if (top > (inMenu ? 90 : 0) && bottom < height - 10) {
            const box = await page.locator('canvas').boundingBox();
            const x = box.x + button.x * box.width, y = box.y + button.y * box.height;
            if (mobile) {
              await input.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y, id: 1 }] });
              await page.waitForTimeout(80);
              await input.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
            } else await page.mouse.click(x, y);
            await page.waitForTimeout(350); return;
          }
          await page.mouse.move(width * .65, height * .6);
          await page.mouse.wheel(0, top < 90 ? -140 : 140); await page.waitForTimeout(180);
        }
        throw new Error(`Cannot reach ${name}`);
      };
      await click('Practice');
      assert.equal((await cmd('state')).screen, 'practice');
      const before = await cmd('state');
      for (const lesson of ['Drift','Shortcut','WallRide','Aim']) {
        const menu = await cmd('ui');
        const instruction = menu.labels.find(l => l.name === 'Instruction' && l.parent === 'Lesson' + lesson);
        assert(instruction && !instruction.truncated && instruction.fontPixels >= 13.9, `Readable instruction ${lesson}`);
        await click('Practice' + lesson);
        for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
        const during = await cmd('ui');
        assert.equal(during.evt, 'practice_' + lesson.toLowerCase());
        const button = during.buttons.find(b => b.name === 'ReturnToGarage');
        const label = during.labels.find(l => l.parent === 'ReturnToGarage');
        assert(button && label && !label.truncated && label.fontPixels >= 13.9 && label.lines === 2, `Readable objective ${lesson}`);
        assert(button.height >= 63.5);
        assert.match(label.text, /0\/1/);
        for (const field of during.labels.filter(l => ['HpText','ActiveWeapon','Ammo','Hints','Timer'].includes(l.name)))
          assert(!(Math.abs(button.x-field.x)*width < (button.width+field.width)/2-1 && Math.abs(button.y-field.y)*height < (button.height+field.height)/2-1), `Practice overlay overlaps ${field.name}`);
        if (lesson === 'Drift') {
          await page.screenshot({ path: `${out}/${language}-${width}-practice.png` });
          if (mobile) await click('PauseButton', false); else await page.keyboard.press('Escape', { delay: 80 });
          const paused = await cmd('ui');
          assert(!paused.buttons.some(b => b.name === 'ReturnToGarage'));
          await page.waitForTimeout(500);
          assert.equal((await cmd('state')).raceSeconds, paused.raceSeconds);
          await click('Resume', false);
        }
        await click('ReturnToGarage', false);
        const after = await cmd('state');
        assert.equal(after.screen, 'practice');
        assert.equal(after.flow, 'Menu');
        assert.equal(after.coins, before.coins); assert.equal(after.races, before.races);
        assert.deepEqual(after.upgrades, before.upgrades);
        evidence.push({ width, height, language, renderer, lesson, menu, during, after });
        console.log('PASS practice', language, width, lesson);
      }
    } catch (error) {
      await page.screenshot({ path: `${out}/failure-${language}-${width}.png` }).catch(() => {});
      evidence.push({ width, height, language, error: String(error), state: await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null) });
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
  await browser.close();
}
