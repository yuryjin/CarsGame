import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-audio-settings';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const [width, height] of [[1280,720], [640,360]]) {
    const context = await browser.newContext({ viewport: { width, height }, hasTouch: width < 1000, isMobile: width < 1000 });
    const page = await context.newPage();
    try {
      const cdp = await context.newCDPSession(page);
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      const url = new URL(process.env.URL || 'http://127.0.0.1:8831/?sdk=mock');
      url.searchParams.set('lang', 'ru');
      const ready = () => page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      await page.goto(url.href); await ready();
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
      const reveal = async name => {
        for (let i = 0; i < 30; i++) {
          const control = (await cmd('ui')).sliders.find(s => s.name === name);
          assert(control?.interactable, name);
          const top = width < 1000 ? 72 : 110;
          if (control.y * height - control.height/2 > top && control.y * height + control.height/2 < height - 15) return control;
          await page.mouse.move(width * .7, height * .65);
          const delta = control.y * height - (top + height - 15) / 2;
          await page.mouse.wheel(0, Math.sign(delta) * Math.min(80, Math.max(25, Math.abs(delta)))); await page.waitForTimeout(150);
        }
        throw new Error(`Cannot reveal ${name}`);
      };
      const set = async (name, percent) => {
        const slider = await reveal(name), box = await page.locator('canvas').boundingBox();
        assert(slider.height >= 42.5, 'Usable volume target');
        const x = box.x + slider.x * box.width, y = box.y + slider.y * box.height;
        if (width < 1000) {
          await cdp.send('Input.dispatchTouchEvent', { type:'touchStart', touchPoints:[{x,y,id:1}] });
          await page.waitForTimeout(80);
          await cdp.send('Input.dispatchTouchEvent', { type:'touchEnd', touchPoints:[] });
        } else await page.mouse.click(x, y);
        await page.waitForTimeout(150);
        const current = (await cmd('ui')).sliders.find(s => s.name === name).value;
        assert.equal(current, 50, 'Tap the middle of the slider');
        for (let i = 0; i < Math.abs(percent - current); i++) {
          await page.keyboard.press(percent > current ? 'ArrowRight' : 'ArrowLeft', { delay: 35 });
          await page.waitForTimeout(30);
        }
        const state = await cmd('ui');
        assert.equal(state.sliders.find(s => s.name === name).value, percent, 'Keyboard fine adjustment');
        return state;
      };
      await cmd('goto', 'settings');
      await set('MusicVolume', 80);
      await set('SoundVolume', 35);
      const before = await cmd('ui');
      assert.equal(before.sliders.find(s => s.name === 'MusicVolume').value, 80);
      await page.waitForTimeout(500);
      const bus = await page.evaluate(() => window.CarsMusic?.bus?.gain.value);
      assert(Math.abs(bus - .44) < .025, `Actual music bus should be .44, got ${bus}`);
      await page.screenshot({path:`${out}/${width}-settings.png`});
      await page.reload(); await ready();
      await cmd('goto', 'settings');
      const restored = await cmd('ui');
      assert.equal(restored.sliders.find(s => s.name === 'MusicVolume').value, 80);
      assert.equal(restored.sliders.find(s => s.name === 'SoundVolume').value, 35);
      await set('MusicVolume', 0);
      await page.waitForTimeout(500);
      assert.equal(await page.evaluate(() => window.CarsMusic?.bus?.gain.value), 0);
      assert.equal((await cmd('ui')).sliders.find(s => s.name === 'SoundVolume').value, 35);
      evidence.push({ width, height, renderer, before, restored, bus });
      console.log('PASS independent audio, actual bus and reload', width);
    } catch (error) {
      await page.screenshot({path:`${out}/failure-${width}.png`}).catch(() => {});
      evidence.push({ width, error:String(error), state:await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null) });
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({evidence,errors},null,2));
  await browser.close();
}
