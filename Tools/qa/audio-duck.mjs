// Observe the actual WebAudio ducking automation at a controlled race finish.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const {chromium}=await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if(!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out=process.env.OUT || '/tmp/cars-audio-duck';await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[],errors=[];
try {
  for(const effects of [0,1]) {
    const context=await browser.newContext({viewport:{width:640,height:360}});
    let page;
    try {
      await context.addInitScript(sfx=>localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify({version:6,updatedAtUnixMs:Date.now(),settings:{sfx,music:1,musicChosen:true,hintsSeen:65535}})})),effects);
      page=await context.newPage();
      page.on('pageerror',e=>errors.push(e.message));
      page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text());});
      await page.goto(process.env.URL || 'http://127.0.0.1:8845/?sdk=mock');
      await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
      const renderer=await page.evaluate(()=>{const gl=document.querySelector('canvas').getContext('webgl2'),ext=gl.getExtension('WEBGL_debug_renderer_info');return gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER);});assert.match(renderer,/NVIDIA/i);
      await page.mouse.click(320,350);await page.waitForTimeout(200);
      await page.waitForFunction(()=>window.CarsMusic?.duck?.gain);
      await page.evaluate(()=>{
        const gain=window.CarsMusic.duck.gain,original=gain.linearRampToValueAtTime.bind(gain);
        window.__duckAutomation=[];
        gain.linearRampToValueAtTime=function(value,time){window.__duckAutomation.push({value,time});return original(value,time);};
      });
      const cmd=async(...args)=>{await page.evaluate(a=>window.CarsPlatform.cmd(...a),args);await page.waitForTimeout(120);const s=await page.evaluate(()=>{const v=window.CarsPlatform.debug.state;return typeof v==='string'?JSON.parse(v):v;});assert.equal(s.error||'','');return s;};
      await cmd('startRace','e1','easy');
      for(let i=0;i<70 && (await cmd('state')).race!=='Racing';i++) await page.waitForTimeout(100);
      assert.equal((await cmd('state')).race,'Racing');
      await page.waitForTimeout(1600); // Separate countdown duck from the finish cue.
      await page.evaluate(()=>{window.__duckAutomation=[];});
      await cmd('finish');await page.waitForTimeout(100);
      const state=await cmd('state');assert.equal(state.flow,'Results');
      const mix=await page.evaluate(()=>({bus:window.CarsMusic.bus.gain.value,duck:window.CarsMusic.duck.gain.value,calls:window.__duckAutomation,context:window.CarsMusic.ctx.state}));
      evidence.push({effects,renderer,state,mix});
      assert.equal(mix.context,'running');
      assert(Math.abs(mix.bus-.55)<.01,'Music remains independently enabled');
      const dips=mix.calls.filter(c=>c.value<.99);
      if(effects===0) {assert.equal(dips.length,0,'Muted finish SFX must not duck music');assert(Math.abs(mix.duck-1)<.001);}
      else assert(dips.length>0,'A loud enabled finish effect still ducks the music');
      console.log('PASS actual WebAudio ducking',effects,mix.duck);
    } catch(error) {
      evidence.push({effects,error:String(error)});
      if(page) await page.screenshot({path:`${out}/failure-${effects}.png`,scale:'css'}).catch(()=>{});
      throw error;
    } finally {await context.close();}
  }
  assert.deepEqual(errors,[]);
} finally {await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));await browser.close();}
