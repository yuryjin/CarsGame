// Real keyboard and touch events in one browser context, without forcing touch mode.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out=process.env.OUT || '/tmp/cars-hybrid-input';
await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[],errors=[];
try {
  for(const [width,height] of [[1280,720],[640,360]]) {
    const context=await browser.newContext({viewport:{width,height},hasTouch:true,isMobile:width<1000});
    const page=await context.newPage();
    try {
      const cdp=await context.newCDPSession(page);
      page.on('pageerror',e=>errors.push(e.message));
      page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text());});
      const url=new URL(process.env.URL || 'http://127.0.0.1:8834/?sdk=mock');
      if(width<1000) url.searchParams.set('device','mobile');
      await page.goto(url.href);
      await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
      const renderer=await page.evaluate(()=>{
        const gl=document.querySelector('canvas').getContext('webgl2'),ext=gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER);
      });
      assert.match(renderer,/NVIDIA/i);
      const cmd=async(...args)=>{
        await page.evaluate(a=>window.CarsPlatform.cmd(...a),args);await page.waitForTimeout(180);
        const s=await page.evaluate(()=>{const v=window.CarsPlatform.debug.state;return typeof v==='string'?JSON.parse(v):v;});
        assert.equal(s.error||'','');return s;
      };
      const send=(type,touchPoints)=>cdp.send('Input.dispatchTouchEvent',{type,touchPoints});
      await cmd('startRace','e1','easy');
      for(let i=0;i<80&&(await cmd('state')).race!=='Racing';i++) await page.waitForTimeout(100);
      for(let cycle=0;cycle<3;cycle++) {
        await page.keyboard.down('w');await page.waitForTimeout(500);
        const keyboard=await cmd('ui');
        assert(!keyboard.touch,'Keyboard driving hides the virtual buttons');
        assert(keyboard.speedKmh>0,'Keyboard actually accelerates the car');
        await page.keyboard.up('w');
        const box=await page.locator('canvas').boundingBox();
        const pause=keyboard.buttons.find(b=>b.name==='PauseButton');
        assert(pause?.interactable);
        assert(pause.width>=43.9 && pause.height>=43.9,'Pause remains a usable touch target in keyboard mode');
        await send('touchStart',[{x:box.x+pause.x*box.width,y:box.y+pause.y*box.height,id:1}]);
        await page.waitForTimeout(100);await send('touchEnd',[]);await page.waitForTimeout(200);
        const paused=await cmd('ui');
        assert(paused.buttons.some(b=>b.name==='Resume'&&b.interactable),'The first touch on the visible pause button must open pause');
        await page.waitForTimeout(350);
        assert.equal((await cmd('state')).raceSeconds,paused.raceSeconds,'First-touch pause freezes the race');
        await page.keyboard.press('Escape',{delay:80});await page.waitForTimeout(200);
        await send('touchStart',[{x:width/2,y:height/2,id:1}]);await page.waitForTimeout(100);
        await send('touchEnd',[]);await page.waitForTimeout(250);
        const touch=await cmd('ui');assert(touch.touch,'Fresh touch restores the virtual buttons without reload');
        const right=touch.buttons.find(b=>b.name==='Right');assert(right?.interactable);
        await send('touchStart',[{x:box.x+right.x*box.width,y:box.y+right.y*box.height,id:1}]);
        await page.waitForTimeout(400);const held=await cmd('state');
        assert(held.steer>.3,'Restored virtual buttons still steer');
        await send('touchEnd',[]);await page.waitForTimeout(200);
        evidence.push({width,height,cycle,renderer,keyboard,paused,touch,held});
      }
      await page.screenshot({path:`${out}/${width}-restored-touch.png`,scale:'css'});
      console.log('PASS keyboard/touch switching',width);
    } catch(error) {
      await page.screenshot({path:`${out}/failure-${width}.png`,scale:'css'}).catch(()=>{});
      evidence.push({width,height,error:String(error),state:await page.evaluate(()=>window.CarsPlatform?.debug?.state).catch(()=>null)});
      throw error;
    } finally {await context.close();}
  }
  assert.deepEqual(errors,[]);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));
  await browser.close();
}
