// Use the garage's real buttons to enter, pause and leave a test drive without buying a car.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-polish-20261024/test-drive';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const [width, height] of [[1280, 720], [812, 375], [640, 360]]) {
    const mobile = width < 1000;
    const context = await browser.newContext({ viewport: { width, height }, hasTouch: mobile, isMobile: mobile });
    let page;
    try {
      page = await context.newPage();
      const input = await context.newCDPSession(page);
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      const url = new URL(process.env.URL || 'http://127.0.0.1:8795/?sdk=mock');
      if (mobile) url.searchParams.set('device', 'mobile');
      await page.goto(url.href);
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      await page.waitForTimeout(1000);
      const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(180);
        const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
        assert.equal(s.error || '', ''); return s;
      };
      const click = async name => {
        for (let i = 0; i < 20; i++) {
          const b = (await cmd('ui')).buttons.find(b => b.name === name); assert(b?.interactable, name);
          const box = await page.locator('canvas').boundingBox();
          if (b.y > (name === 'TestDrive' ? .16 : 0) && b.y < .94) {
            const x = box.x + b.x * box.width, y = box.y + b.y * box.height;
            if (mobile) {
              await input.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y, id: 1 }] });
              await page.waitForTimeout(100);
              await input.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
            } else await page.mouse.click(x, y);
            await page.waitForTimeout(350); return;
          }
          await page.mouse.move(width * .7, height * .6);
          await page.mouse.wheel(0, b.y > .5 ? 200 : -200); await page.waitForTimeout(180);
        }
        throw new Error(`Cannot reach ${name}`);
      };
      await cmd('goto', 'garage');
      const before = await cmd('state');
      await click('TestDrive');
      for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
      const during = await cmd('ui'); assert.equal(during.evt, 'garage_test');
      const button = during.buttons.find(b => b.name === 'ReturnToGarage'); assert(button);
      const label = during.labels.find(l => l.parent === 'ReturnToGarage'); assert(label);
      evidence.push({ width, height, renderer, phase: 'drive', before, during });
      await page.screenshot({ path: `${output}/${width}-drive.png` });
      if (process.env.CG_BASELINE !== '1') {
        assert(button.height >= 43.5, 'Return to garage needs a usable touch target');
        assert(label.fontPixels >= 13.9 && !label.truncated && label.lines === 1, 'Return text must be legible');
        assert(!during.buttons.some(b => b.name === 'LiveGoals'), 'Practice must not promise permanent stars');
        if (mobile) await click('PauseButton'); else await page.keyboard.press('Escape');
        const paused = await cmd('ui');
        assert(!paused.buttons.some(b => b.name === 'ReturnToGarage'), 'The test-drive overlay must yield to pause');
        await page.waitForTimeout(500);
        assert.equal((await cmd('state')).raceSeconds, paused.raceSeconds, 'Pause freezes the test-drive clock');
        await click('Resume');
      }
      await click('ReturnToGarage');
      const after = await cmd('state');
      assert.equal(after.screen, 'garage'); assert.equal(after.flow, 'Menu');
      assert.equal(after.coins, before.coins); assert.equal(after.races, before.races);
      assert.deepEqual(after.upgrades, before.upgrades);
      evidence.push({ width, height, phase: 'returned', after });
      console.log('PASS test drive', width, 'button height', button.height, 'font', label.fontPixels);
    } catch (error) {
      if (page && !page.isClosed()) {
        await page.screenshot({ path: `${output}/failure-${width}.png` }).catch(() => {});
        evidence.push({ width, height, phase: 'failure', error: String(error),
          state: await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null) });
      }
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
} finally {
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ evidence, errors }, null, 2));
  await browser.close();
}
