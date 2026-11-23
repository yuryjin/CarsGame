// Actual menu clicks/touches and Escape; one paused race survives nine language changes.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-pause-settings';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel:'chrome', args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server'] });
const evidence = [], errors = [];
const cameraSettings=process.env.CG_CAMERA_SETTINGS==='1';
const launchHelp={ru:'Идеальный старт',en:'Perfect start',tr:'Mükemmel başlangıç',zh:'完美起步',de:'Perfekter Start',es:'Salida perfecta',fr:'Départ parfait',pt:'Largada perfeita',uk:'Ідеальний старт'};
try {
  for (const [width,height] of [[640,360],[812,375],[1280,720]]) {
    const mobile = width < 1000;
    const context = await browser.newContext({viewport:{width,height},hasTouch:mobile,isMobile:mobile});
    await context.addInitScript(() => { if(!localStorage.getItem('cars.mock.playerdata')) localStorage.setItem('cars.mock.playerdata', JSON.stringify({save:JSON.stringify({version:6,updatedAtUnixMs:Date.now(),settings:{lang:'ru',hintsSeen:65535}})})); });
    const page = await context.newPage();
    try {
      const cdp = await context.newCDPSession(page);
      page.on('pageerror', e => errors.push(e.message));
      page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
      const url = new URL(process.env.URL || 'http://127.0.0.1:8837/?sdk=mock');
      url.searchParams.set('lang','ru'); if(mobile) url.searchParams.set('device','mobile');
      await page.goto(url.href);
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
      const renderer = await page.evaluate(() => {
        const gl=document.querySelector('canvas').getContext('webgl2'), ext=gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
      });
      assert.match(renderer,/NVIDIA/i);
      const cmd = async (...args) => {
        await page.evaluate(a=>window.CarsPlatform.cmd(...a),args); await page.waitForTimeout(150);
        const s=await page.evaluate(()=>{const s=window.CarsPlatform.debug.state;return typeof s==='string'?JSON.parse(s):s;});
        assert.equal(s.error||'',''); return s;
      };
      const tap = async control => {
        const box=await page.locator('canvas').boundingBox(), x=box.x+control.x*box.width, y=box.y+control.y*box.height;
        if(mobile) {
          await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y,id:1}]});
          await page.waitForTimeout(80);
          await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
        } else await page.mouse.click(x,y,{delay:80});
        await page.waitForTimeout(250);
      };
      const reveal = async (name, collection='buttons',scroll=false) => {
        let wheelGain=1, previousScroll=null;
        for(let i=0;i<40;i++) {
          const control=(await cmd('ui'))[collection].find(c=>c.name===name);
          assert(control?.interactable,name);
          const top=scroll?(mobile?72:110):0;
          // The list is clipped above the shell/footer, not at the canvas edge. A dump can
          // include an interactable button whose center is already outside its viewport.
          const bottom=scroll?height-(mobile?45:80):height-5;
          if(control.y*height-control.height/2>=top && control.y*height+control.height/2<=bottom) return control;
          assert(scroll,`${name} outside viewport`);
          const y=control.y*height, delta=y-(top+bottom)/2;
          if(mobile) {
            // A real swipe in the list's left margin avoids grabbing a volume slider.
            const x=30, from=delta>0?height-45:top+45;
            const travel=Math.sign(delta)*Math.min(150,Math.max(35,Math.abs(delta)));
            await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y:from,id:1}]});
            for(let step=1;step<=8;step++) {
              await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x,y:from-travel*step/8,id:1}]});
              await page.waitForTimeout(35);
            }
            await page.waitForTimeout(200);
            await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
            evidence.push({width,phase:'scroll',name,attempt:i,beforeY:y,swipe:travel});
          } else {
            if(previousScroll) {
              const measured=(previousScroll.y-y)/previousScroll.wheel;
              if(measured>.01) wheelGain=measured;
            }
            const wheel=Math.sign(delta)*Math.max(1,Math.min(80,Math.abs(delta)/wheelGain));
            await page.mouse.move(width*.7,height*.65);await page.mouse.wheel(0,wheel);
            evidence.push({width,phase:'scroll',name,attempt:i,beforeY:y,wheel});
            previousScroll={y,wheel};
          }
          await page.waitForTimeout(180);
        }
        throw new Error(`Cannot reach ${name}`);
      };
      const click=async(name,scroll=false)=>tap(await reveal(name,'buttons',scroll));
      // startRace bypasses the menu's normal Start tap. Establish the real touch device
      // before reading button positions; the first touch switches the HUD layout.
      if(mobile) await tap({x:.01,y:.01});
      await cmd('startRace','e1','easy');
      for(let i=0;i<80 && (await cmd('state')).race!=='Racing';i++) await page.waitForTimeout(100);
      if (process.env.CG_COMPACT_HUD === '1' && width === 640) {
        const original = await cmd('ui');
        const model = await reveal('RaceModelStyle'), reset = await reveal('Reset');
        await page.screenshot({path:`${out}/road-standard.png`,scale:'css'});
        await click('RaceModelStyle');
        const switched = (await cmd('ui')).labels.find(l=>l.parent==='RaceModelStyle').text;
        assert.notEqual(switched, original.labels.find(l=>l.parent==='RaceModelStyle').text);
        await click('PauseButton'); await click('PauseSettings'); await click('CompactHudOn',true); await click('Back');
        assert.equal((await cmd('ui')).labels.find(l=>l.parent==='RaceModelStyle').text,switched);
        await click('RaceModelStyle');
        assert.equal((await cmd('ui')).labels.find(l=>l.parent==='RaceModelStyle').text, original.labels.find(l=>l.parent==='RaceModelStyle').text);
        await click('Resume');
        const compact = await cmd('ui'), newReset = await reveal('Reset');
        assert(!compact.buttons.some(b=>b.name==='RaceModelStyle'));
        assert((reset.y-newReset.y)*height >= model.height, 'The width of the old reset button now shows the road for at least one button height');
        await page.screenshot({path:`${out}/road-compact.png`,scale:'css'});
        evidence.push({width,phase:'compact-road',model,reset,newReset,standard:original,compact});
        await click('PauseButton'); await click('PauseSettings'); await click('CompactHudOff',true); await click('Back'); await click('Resume');
        assert((await cmd('ui')).buttons.some(b=>b.name==='RaceModelStyle'&&b.interactable));
      }
      await click('PauseButton');
      const before=await cmd('ui');
      assert(before.buttons.some(b=>b.name==='Resume'&&b.interactable),'Pause must be open before measuring frozen state');
      const sameRace=s=>{
        assert.equal(s.flow,'Racing'); assert.equal(s.race,'Racing');
        for(const key of ['raceSeconds','raceSeed','trackDistance','coins','races','resets']) assert.equal(s[key],before[key],key);
      };
      for(const language of ['ru','en','tr','zh','de','es','fr','pt','uk']) {
        const paused=await cmd('ui'); sameRace(paused);
        for(const name of ['Resume','Restart','PauseSettings','MainMenu']) {
          const b=paused.buttons.find(b=>b.name===name), l=paused.labels.find(l=>l.parent===name);
          assert(b?.interactable && b.height>=43.5 && b.width>=80,`Usable ${name}`);
          assert(l && !l.truncated && l.fontPixels>=13.9,`Readable ${language} ${name}`);
          assert(b.x*width-b.width/2>=0 && b.x*width+b.width/2<=width && b.y*height-b.height/2>=0 && b.y*height+b.height/2<=height,`${name} fits`);
        }
        if(['ru','zh','uk'].includes(language)) await page.screenshot({path:`${out}/${width}-${language}-pause.png`,scale:'css'});
        await click('PauseSettings');
        assert.equal((await cmd('state')).screen,'settings');
        if(language==='ru') {
          await tap(await reveal('MusicVolume','sliders',true));
          assert.equal((await cmd('ui')).sliders.find(s=>s.name==='MusicVolume').value,50);
          await click('TouchLargeOn',true); await click('TouchLeftOn',true);
        }
        if(cameraSettings) {
          await click('DynamicCameraOff',true);
          await reveal('DynamicCameraOff','buttons',true);
          const controls=await cmd('ui');
          if ('dynamicCamera' in controls) assert.equal(controls.dynamicCamera,false,'The actual setting changed');
          const caption=controls.labels.find(l=>l.parent==='DynamicCamera');
          assert(caption && !caption.truncated && caption.fontPixels>=14,`${language}: readable camera setting`);
          for(const name of ['DynamicCameraOff','DynamicCameraOn']) {
            const b=controls.buttons.find(b=>b.name===name);assert(b?.interactable && b.height>=43.5 && b.width>=43.5);
          }
          if(width===640 && ['ru','zh'].includes(language)) await page.screenshot({path:`${out}/${width}-${language}-camera-setting.png`,scale:'css'});
          evidence.push({width,height,language,phase:'camera-setting',controls});
        }
        sameRace(await cmd('state'));
        await click('Back');
        const back=await cmd('ui'); sameRace(back); assert.equal(back.screen,'');
        assert(back.buttons.some(b=>b.name==='Resume'));
        await click('PauseSettings');
        await page.keyboard.press('Escape',{delay:80}); await page.waitForTimeout(250);
        const escaped=await cmd('ui'); sameRace(escaped); assert.equal(escaped.screen,'');
        assert(escaped.buttons.some(b=>b.name==='Resume'),'Escape closes only settings');
        if(cameraSettings) {
          await click('Hint');
          const help=await cmd('ui');sameRace(help);
          const text=help.labels.find(l=>l.name==='Lessons');
          assert(text && text.fontPixels>=16 && !text.truncated);
          assert(text.text.includes(launchHelp[language]),`${language}: launch technique is explained`);
          await click('CloseHelp');
          const closedHelp=await cmd('ui');sameRace(closedHelp);
          assert(!closedHelp.buttons.some(b=>b.name==='CloseHelp') && closedHelp.buttons.some(b=>b.name==='Resume'&&b.interactable),'Back closes help before another menu action');
          evidence.push({width,height,language,phase:'launch-help',help});
        }
        evidence.push({width,height,language,renderer,paused,back,escaped});
        console.log('PASS paused settings',width,language);
        if(language!=='uk') {
          await click('PauseSettings'); await click('LanguageNext',true); await click('Back');
        }
      }
      await click('Resume'); await page.waitForTimeout(600);
      const after=await cmd('state'); assert(after.raceSeconds>before.raceSeconds+.2);
      assert.equal(after.raceSeed,before.raceSeed); assert.equal(after.resets,before.resets);
      if(mobile) {
        // Escape selected keyboard mode; a fresh real touch restores the updated layout.
        await tap({x:.5,y:.5});
        const ui=await cmd('ui'), left=ui.buttons.find(b=>b.name==='Left'), fire=ui.buttons.find(b=>b.name==='Fire');
        assert(fire.x<left.x,'Updated layout applies to the same race');
        evidence.push({width,phase:'resumed',ui});
      }
      if(cameraSettings) {
        await page.waitForFunction(()=>{
          const data=JSON.parse(JSON.parse(localStorage.getItem('cars.mock.playerdata')||'{}').save||'{}');
          return data.settings?.dynamicCamera===false;
        },null,{timeout:15000});
        await page.reload();
        await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
        const restored=await page.evaluate(()=>JSON.parse(JSON.parse(localStorage.getItem('cars.mock.playerdata')).save).settings);
        assert.equal(restored.dynamicCamera,false,'The view choice survives reload');
        evidence.push({width,phase:'camera-reload',restored});
      }
    } catch(error) {
      await page.screenshot({path:`${out}/failure-${width}.png`,scale:'css'}).catch(()=>{});
      evidence.push({width,error:String(error),state:await page.evaluate(()=>window.CarsPlatform?.debug?.state).catch(()=>null),storage:await page.evaluate(()=>({mock:localStorage.getItem('cars.mock.playerdata'),local:localStorage.getItem('cars.local.cars.save.v1')})).catch(()=>null)});
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors,[]);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));
  await browser.close();
}
