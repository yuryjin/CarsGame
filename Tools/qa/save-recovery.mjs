// Browser storage faults at the real WebGL .jslib boundary; mock cloud remains independent.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const {chromium}=await import(process.env.PLAYWRIGHT_CORE||'/home/user/gstack/node_modules/playwright-core/index.mjs');
assert(process.env.WEBTEST_CHROME_ARGS,'Run through webtest');
const out=process.env.OUT||'/tmp/cars-save-recovery';await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[],errors=[];
try{
 for(const [width,height,mode] of [[640,360,'write'],[812,375,'write'],[1280,720,'write'],[640,360,'read']]){
  const context=await browser.newContext({viewport:{width,height}}),page=await context.newPage();
  await context.addInitScript(mode=>{
   const key='cars.local.cars.save.v1',get=Storage.prototype.getItem,set=Storage.prototype.setItem;
   const saved={version:6,updatedAtUnixMs:Date.now(),settings:{lang:'ru',hintsSeen:65535,sfx:mode==='read'?0:1},progress:{ledger:[{deviceId:'qa-browser-storage',coinsEarned:123}]}};
   if(mode==='read')set.call(localStorage,key,JSON.stringify(saved));
   else if(!get.call(localStorage,key)&&!get.call(localStorage,'cars.mock.playerdata'))set.call(localStorage,'cars.mock.playerdata',JSON.stringify({save:JSON.stringify(saved)}));
   window.__denyLocalRead=mode==='read';window.__denyLocalWrite=false;window.__storageFaults=0;
   Storage.prototype.getItem=function(k){if(this===localStorage&&k===key&&window.__denyLocalRead){window.__storageFaults++;throw new DOMException('QA denied read','SecurityError');}return get.call(this,k);};
   Storage.prototype.setItem=function(k,v){if(this===localStorage&&k===key&&window.__denyLocalWrite){window.__storageFaults++;throw new DOMException('QA full storage','QuotaExceededError');}return set.call(this,k,v);};
  },mode);
  page.on('pageerror',e=>errors.push(e.message));
  page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text()))errors.push(m.text());});
  const cmd=async(...args)=>{await page.evaluate(a=>CarsPlatform.cmd(...a),args);await page.waitForTimeout(150);const s=await page.evaluate(()=>{let s=CarsPlatform.debug.state;return typeof s==='string'?JSON.parse(s):s});assert.equal(s.error||'','');return s;};
  const ready=()=>page.waitForFunction(()=>window.CarsPlatform?.debug?.log.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
  const click=async name=>{const s=await cmd('ui'),b=s.buttons.find(b=>b.name===name&&b.interactable);assert(b,name);await page.mouse.click(b.x*width,b.y*height,{delay:80});await page.waitForTimeout(500);};
  try{
   await page.goto(process.env.URL||'http://127.0.0.1:8848/?sdk=mock&lang=ru');await ready();
   const renderer=await page.evaluate(()=>{const g=document.querySelector('canvas').getContext('webgl2'),e=g.getExtension('WEBGL_debug_renderer_info');return g.getParameter(e?e.UNMASKED_RENDERER_WEBGL:g.RENDERER);});assert.match(renderer,/NVIDIA/i);
   if(mode==='write'){
    assert.equal((await cmd('state')).coins,123);
    await cmd('goto','settings');await page.evaluate(()=>window.__denyLocalWrite=true);
    // Sound is visible at the top on every viewport; Music is below the clip at 640x360.
    const slider=(await cmd('ui')).sliders.find(s=>s.name==='SoundVolume');assert(slider);
    assert(slider.y*height-slider.height/2>=72&&slider.y*height+slider.height/2<=height-40,'Trigger is visible');
    await page.mouse.click(slider.x*width,slider.y*height,{delay:80});await page.waitForTimeout(400);
    for(const language of ['ru','en','tr','zh','de','es','fr','pt','uk']){
     const s=await cmd('ui');assert.equal(s.localWriteFailed,true);assert.equal(s.localReadFailed,false);assert.equal(s.coins,123);
     for(const name of ['SaveRetry','SaveContinue']){
      const b=s.buttons.find(b=>b.name===name);assert(b?.interactable,name);assert(b.width>=44&&b.height>=44);
      assert(b.x*width-b.width/2>=0&&b.x*width+b.width/2<=width&&b.y*height-b.height/2>=0&&b.y*height+b.height/2<=height,name+' fits');
     }
     const text=s.labels.find(l=>l.name==='SaveRecoveryMessage');assert(text&&text.text.length>30&&!text.text.includes('…')&&text.fontPixels>=16&&!text.truncated);
     const title=s.labels.find(l=>l.name==='SaveRecoveryTitle');assert(title&&title.fontPixels>=20&&!title.truncated);
     await click('SaveRetry');assert.equal((await cmd('state')).localWriteFailed,true,'An unsuccessful retry cannot dismiss the failure');
     if(width===640&&['ru','zh'].includes(language))await page.screenshot({path:`${out}/${width}-${language}.png`});
     evidence.push({width,height,mode,language,state:s});console.log('PASS save notice',width,language);
     if(language!=='uk'){
      await click('SaveContinue');await cmd('goto','settings');await click('LanguageNext');await click('RetryLocalSave');
     }
    }
    assert(await page.evaluate(()=>window.__storageFaults)>0,'The real storage write was denied');
    await page.evaluate(()=>window.__denyLocalWrite=false);await click('SaveRetry');
    const recovered=await cmd('ui');assert.equal(recovered.localWriteFailed,false);assert(!recovered.buttons.some(b=>b.name==='SaveRetry'));
    const stored=await page.evaluate(()=>JSON.parse(localStorage.getItem('cars.local.cars.save.v1')));assert.equal(stored.settings.lang,'uk');
    // Prove the local copy restores the profile even when the mock cloud starts empty.
    await page.evaluate(()=>{localStorage.removeItem('cars.mock.playerdata');localStorage.removeItem('cars.save.mirror');});
    await page.reload();await ready();const reloaded=await cmd('ui');assert.equal(reloaded.coins,123);assert.equal(reloaded.localWriteFailed,false);
    evidence.push({width,mode,phase:'local-only-reload',reloaded,stored});
   }else{
    const failed=await cmd('ui');assert.equal(failed.localReadFailed,true);assert.equal(failed.localWriteFailed,true);
    assert(failed.labels.some(l=>l.name==='SaveRecoveryMessage'&&l.text.includes('не заменён')));
    await page.screenshot({path:`${out}/read-denied.png`});
    await page.evaluate(()=>window.__denyLocalRead=false);await click('SaveRetry');
    const restored=await cmd('ui');assert.equal(restored.localReadFailed,false);assert.equal(restored.localWriteFailed,false);assert.equal(restored.coins,123);
    assert(restored.labels.some(l=>l.name==='Coins'&&l.text.includes('123')),'Recovered wallet is visible immediately');
    evidence.push({mode,failed,restored});console.log('PASS denied read preserved and merged');
   }
   if(mode==='read')assert(await page.evaluate(()=>window.__storageFaults)>0,'The real storage read was denied');
  }catch(error){evidence.push({width,mode,error:String(error),state:await cmd('ui').catch(()=>null)});await page.screenshot({path:`${out}/failure-${width}-${mode}.png`}).catch(()=>{});throw error;}
  finally{await context.close();}
 }
 assert.deepEqual(errors,[]);
}finally{await fs.writeFile(out+'/evidence.json',JSON.stringify({evidence,errors},null,2));await browser.close();}
