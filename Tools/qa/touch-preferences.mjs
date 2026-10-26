// Actual touch input for every control layout; no injected driving commands.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-touch-preferences';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const [width, height] of [[640,360], [812,375], [1280,720]]) {
    const context = await browser.newContext({ viewport: { width, height }, hasTouch: true, isMobile: true });
    const page = await context.newPage();
    await page.addInitScript(() => {
      window.__touchTrace = [];
      for (const type of ['touchstart','touchend','touchcancel','pointerdown','pointerup','pointercancel','mousedown','mouseup'])
        document.addEventListener(type, event => {
          window.__touchTrace.push({ type, at:performance.now(), pointerId:event.pointerId, pointerType:event.pointerType,
            x:event.clientX,y:event.clientY, target:event.target?.id,
            touches:event.touches ? [...event.touches].map(t=>({id:t.identifier,x:t.clientX,y:t.clientY})) : undefined,
            changed:event.changedTouches ? [...event.changedTouches].map(t=>({id:t.identifier,x:t.clientX,y:t.clientY})) : undefined });
          if(window.__touchTrace.length>120) window.__touchTrace.shift();
        },true);
    });
    try {
      const cdp = await context.newCDPSession(page);
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      const url = new URL(process.env.URL || 'http://127.0.0.1:8833/?sdk=mock&device=mobile&lang=ru');
      const ready = () => page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
      await page.goto(url.href); await ready();
      const renderer = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer, /NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(150);
        const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
        assert.equal(s.error || '', ''); return s;
      };
      const point = async (name, id = 1) => {
        const b = (await cmd('ui')).buttons.find(b => b.name === name);
        assert(b?.interactable, name);
        const box = await page.locator('canvas').boundingBox();
        return { x: box.x + b.x * box.width, y: box.y + b.y * box.height, id };
      };
      const send = (type, touchPoints) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints });
      const tap = async name => {
        await send('touchStart', [await point(name)]); await page.waitForTimeout(80);
        await send('touchEnd', []); await page.waitForTimeout(300);
      };
      const toggle = async name => {
        for (let n = 0; n < 40; n++) {
          const b = (await cmd('ui')).buttons.find(b => b.name === name);
          assert(b, name);
          const top = width < 1000 ? 72 : 110;
          if (b.y * height - b.height / 2 > top && b.y * height + b.height / 2 < height - 10) return tap(name);
          const delta = b.y * height - (top + height - 10) / 2;
          await page.mouse.move(width * .7, height * .65);
          await page.mouse.wheel(0, Math.sign(delta) * Math.min(100, Math.max(25, Math.abs(delta))));
          await page.waitForTimeout(120);
        }
        throw new Error('Cannot reach ' + name);
      };
      let previous = { large: false, left: false }, normalWidth;
      for (const prefs of [{ large:false,left:false }, { large:true,left:false }, { large:true,left:true }, { large:false,left:true }]) {
        await cmd('goto', 'settings');
        if (prefs.large !== previous.large) await toggle('TouchLarge' + (prefs.large ? 'On' : 'Off'));
        if (prefs.left !== previous.left) await toggle('TouchLeft' + (prefs.left ? 'On' : 'Off'));
        previous = prefs;
        await cmd('startRace', 'e1', 'easy');
        await send('touchStart', [{ x:width/2, y:height/2, id:1 }]); await send('touchEnd', []);
        for (let i = 0; i < 80 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(100);
        const ui = await cmd('ui');
        assert(ui.touch);
        const get = name => { const b = ui.buttons.find(b => b.name === name); assert(b, name); return b; };
        const left = get('Left'), right = get('Right'), fire = get('Fire');
        assert(left.x < right.x, 'Arrow direction must not reverse');
        assert.equal(fire.x < left.x, prefs.left, 'Action and steering groups must swap sides');
        if (!prefs.large) normalWidth = left.width;
        else assert(left.width >= normalWidth * 1.24, 'Large buttons must be visibly larger');
        for (const name of ['Left','Right','Fire','Brake','Reset','SwapWeapon','PauseButton']) {
          const b = get(name);
          assert(b.width >= 43.5 && b.height >= 43.5, `${name} touch size ${b.width}x${b.height}`);
          assert(b.x*width-b.width/2 >= 0 && b.x*width+b.width/2 <= width && b.y*height-b.height/2 >= 0 && b.y*height+b.height/2 <= height, `${name} stays on screen`);
        }
        const overlap = (a,b) => Math.abs(a.x-b.x)*width < (a.width+b.width)/2-1 && Math.abs(a.y-b.y)*height < (a.height+b.height)/2-1;
        const controls = ui.buttons.filter(b => ['Left','Right','Fire','Brake','Secondary','SwapWeapon','Reset','PauseButton'].includes(b.name));
        for (let a=0;a<controls.length;a++) for (let b=a+1;b<controls.length;b++) assert(!overlap(controls[a],controls[b]), `${controls[a].name} overlaps ${controls[b].name}`);
        const speed = ui.labels.find(l => l.name === 'Speed');
        assert(speed, 'Speed display');
        assert.equal(speed.x > .5, prefs.left, 'Speed follows steering side');
        for (const b of controls) assert(!overlap(speed,b), `Speed overlaps ${b.name}`);
        await page.screenshot({ path:`${out}/${width}-${prefs.large?'large':'normal'}-${prefs.left?'left':'right'}.png`, scale:'css' });
        for (const [name, sign] of [['Left',-1], ['Right',1]]) {
          await send('touchStart', [await point(name,1), await point('Fire',2)]);
          await page.waitForTimeout(600);
          const held = await cmd('state');
          evidence.push({ width,height,prefs,phase:'held',name,held });
          assert(held.steer * sign > .5, `${name} steers in correct direction while firing`);
          await send('touchEnd', []); await page.waitForTimeout(250);
          const afterRelease = await cmd('state');
          evidence.push({ width,height,prefs,phase:'release',name,afterRelease });
          assert(Math.abs(afterRelease.steer)<=.351, `${name} must release before the next touch`);
        }
        await tap('PauseButton');
        const paused = await cmd('ui'); assert(paused.buttons.some(b=>b.name==='Resume'));
        await tap('Resume'); await page.waitForTimeout(500);
        // Released touch keeps the road-following assist (at most 0.35), not a held full turn.
        const released = await cmd('state'); assert(Math.abs(released.steer)<=.351, 'No held steering after pause');
        evidence.push({ width,height,prefs,renderer,ui,released });
        console.log('PASS touch layout', width, prefs);
      }
      await cmd('goto','settings');
      await page.waitForFunction(() => {
        const save = JSON.parse(JSON.parse(localStorage.getItem('cars.mock.playerdata') || '{}').save || '{}');
        return save.settings?.touchLeftHanded === true && save.settings?.touchLargeButtons === false;
      }, null, { timeout:15000 });
      await page.reload(); await ready(); await cmd('goto','settings');
      const restored = await cmd('ui');
      await cmd('startRace','e1','easy');
      await send('touchStart',[{x:width/2,y:height/2,id:1}]); await send('touchEnd',[]);
      await page.waitForTimeout(1000);
      const reloaded = await cmd('ui');
      assert(reloaded.buttons.find(b=>b.name==='Fire').x < reloaded.buttons.find(b=>b.name==='Left').x, 'Left handed layout survives reload');
      assert(Math.abs(reloaded.buttons.find(b=>b.name==='Left').width-normalWidth)<1, 'Normal size survives reload');
      evidence.push({ width, phase:'reload', restored,reloaded });
    } catch(error) {
      await page.screenshot({path:`${out}/failure-${width}.png`,scale:'css'}).catch(()=>{});
      evidence.push({width,error:String(error),state:await page.evaluate(()=>window.CarsPlatform?.debug?.state).catch(()=>null),
        events:await page.evaluate(()=>window.__touchTrace).catch(()=>null)});
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors, []);
} finally {
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ evidence,errors },null,2));
  await browser.close();
}
