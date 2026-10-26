// Real touchscreen launch input plus countdown layout checks. Run only through webtest.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const {chromium}=await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if(!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out=process.env.OUT || '/tmp/cars-touch-launch';
await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[],errors=[];
try {
  for(const lang of ['ru','en','tr','zh','de','es','fr','pt','uk']) {
    const context=await browser.newContext({viewport:{width:640,height:360},hasTouch:true,isMobile:true});
    let page;
    try {
      await context.addInitScript(lang=>localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify({version:6,updatedAtUnixMs:Date.now(),settings:{lang,hintsSeen:65535,touchLargeButtons:true,touchLeftHanded:true}})})),lang);
      page=await context.newPage();
      page.on('pageerror',e=>errors.push(e.message));
      page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text());});
      const url=new URL(process.env.URL || 'http://127.0.0.1:8844/?sdk=mock&device=mobile');url.searchParams.set('lang',lang);
      await page.goto(url.href);
      await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
      const renderer=await page.evaluate(()=>{const gl=document.querySelector('canvas').getContext('webgl2'),ext=gl.getExtension('WEBGL_debug_renderer_info');return gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER);});
      assert.match(renderer,/NVIDIA/i);
      const cdp=await context.newCDPSession(page);
      const send=(type,touchPoints)=>cdp.send('Input.dispatchTouchEvent',{type,touchPoints});
      await send('touchStart',[{x:320,y:350,id:1}]);await page.waitForTimeout(80);await send('touchEnd',[]);
      const cmd=async(...args)=>{await page.evaluate(a=>window.CarsPlatform.cmd(...a),args);await page.waitForTimeout(100);const s=await page.evaluate(()=>{const s=window.CarsPlatform.debug.state;return typeof s==='string'?JSON.parse(s):s;});assert.equal(s.error||'','');return s;};
      for(const [width,height] of [[640,360],[812,375],[1280,720]]) {
        await page.setViewportSize({width,height});await page.waitForTimeout(200);
        for(const mode of lang==='ru' && width===640 ? ['timed','early','none'] : ['timed']) {
          await cmd('startRace','e1','easy');
          const ui=await cmd('ui');
          assert.equal(ui.race,'Countdown');assert(ui.touch);
          const button=ui.buttons.find(b=>b.name==='Launch');assert(button?.interactable);
          const label=ui.labels.find(t=>t.parent==='Launch');assert(label && !label.truncated && label.fontPixels>=13.9);
          assert(button.height>=63.9 && button.width>=43.9);
          const overlap=(a,b)=>Math.abs(a.x-b.x)*width<(a.width+b.width)/2-1 && Math.abs(a.y-b.y)*height<(a.height+b.height)/2-1;
          for(const other of ui.buttons.filter(b=>b.name!=='Launch')) assert(!overlap(button,other),`Launch overlaps ${other.name}`);
          assert(button.x*width-button.width/2>=0 && button.x*width+button.width/2<=width && button.y*height+button.height/2<=height);
          const box=await page.locator('canvas').boundingBox();
          const point={x:box.x+button.x*box.width,y:box.y+button.y*box.height,id:1};
          if(lang==='ru' && mode==='timed') await page.screenshot({path:`${out}/${width}-countdown.png`,scale:'css'});
          if(mode==='timed') await page.waitForFunction(()=>{
            window.CarsPlatform.cmd('state');const v=window.CarsPlatform.debug.state,s=typeof v==='string'?JSON.parse(v):v;
            return s.race==='Countdown' && s.raceClock>=s.countdownSeconds-.18;
          },null,{timeout:10000,polling:'raf'});
          if(mode!=='none') {await send('touchStart',[point]);await page.waitForTimeout(mode==='early'?3500:120);await send('touchEnd',[]);}
          await page.waitForFunction(()=>{
            window.CarsPlatform.cmd('state');const v=window.CarsPlatform.debug.state,s=typeof v==='string'?JSON.parse(v):v;
            return s.race==='Racing' && s.raceSeconds>.7;
          },null,{timeout:10000,polling:'raf'});
          const after=await cmd('ui');
          assert.equal(after.perfectStart,mode==='timed',`${mode} launch at ${width}/${lang}`);
          assert(!after.buttons.some(b=>b.name==='Launch'),'Launch leaves the driving view');
          assert(after.speedKmh>0,'Auto-throttle continues after the launch action disappears');
          evidence.push({lang,width,height,mode,renderer,ui,after});
          console.log('PASS touch launch',lang,width,mode);
        }
      }
    } catch(error) {
      if(page) {await page.screenshot({path:`${out}/failure-${lang}.png`,scale:'css'}).catch(()=>{});evidence.push({lang,error:String(error),state:await page.evaluate(()=>window.CarsPlatform?.debug?.state).catch(()=>null)});}
      throw error;
    } finally {await context.close();}
  }
  assert.deepEqual(errors,[]);
} finally {await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));await browser.close();}
