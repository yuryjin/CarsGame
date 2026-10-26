// Real Addressables bundle failures: preserve the old UI, retry the same language, recover boot.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const {chromium}=await import(process.env.PLAYWRIGHT_CORE||'/home/user/gstack/node_modules/playwright-core/index.mjs');
assert(process.env.WEBTEST_CHROME_ARGS,'Run through webtest');
const out=process.env.OUT||'/tmp/cars-locale-recovery';await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[];
try{
 for(const mode of ['switch','startup','initialization']){
  const width=640,height=360;
  const context=await browser.newContext({viewport:{width,height},locale:'en-US'});
  const startLang=mode==='initialization'?'en':'ru';
  await context.addInitScript(lang=>{if(!localStorage.getItem('cars.mock.playerdata'))localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify({version:6,updatedAtUnixMs:Date.now(),settings:{lang,hintsSeen:65535}})}));},startLang);
  const page=await context.newPage(),logs=[],blocked=[];let failing=true;
  page.on('console',m=>logs.push(m.text()));page.on('pageerror',e=>logs.push('PAGEERROR '+e.message));
  await page.route('**/*',route=>{
   const url=route.request().url(),table=mode==='switch'?'turkish':mode==='startup'?'russian':'english';
   if(failing&&url.includes(`localization-string-tables-${table}_`)){blocked.push(url);return route.abort('failed');}
   return route.continue();
  });
  const cmd=async(...args)=>{await page.evaluate(a=>CarsPlatform.cmd(...a),args);await page.waitForTimeout(150);return page.evaluate(()=>{let s=CarsPlatform.debug.state;return typeof s==='string'?JSON.parse(s):s});};
  const ready=()=>page.waitForFunction(()=>window.CarsPlatform?.debug?.log.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
  const click=async name=>{const s=await cmd('ui'),b=s.buttons.find(b=>b.name===name);assert(b,name);await page.mouse.click(b.x*width,b.y*height,{delay:80});await page.waitForTimeout(700);};
  try{
   await page.goto(`${process.env.URL||'http://127.0.0.1:8847/'}?sdk=mock&lang=${startLang}`);
   await page.waitForFunction(()=>window.unityInstance,null,{timeout:180000});
   if(mode==='switch'){
    await ready();await cmd('goto','settings');await click('LanguageNext');await click('LanguageNext');
    for(let i=0;i<150&&!logs.some(l=>l.includes('[Loc] table load failed'));i++)await page.waitForTimeout(100);
    assert(blocked.length>0,'The target bundle really failed');
    const failed=await cmd('ui');assert(failed.buttons.some(b=>b.name==='Back'));
    assert.equal(failed.labels.find(l=>l.name==='LanguageName')?.text,'English','The last working language remains selected');
    const notice=failed.labels.find(l=>l.name==='Message'&&l.text.includes('Language could not load'));
    assert(notice,'Explain how to retry');
    await page.screenshot({path:`${out}/switch-failed.png`});
    await click('Back');assert((await cmd('ui')).buttons.length>0,'Back cannot erase the main menu');
    failing=false;await cmd('goto','settings');await click('LanguageNext');
    for(let i=0;i<100;i++){const s=await cmd('ui');if(s.labels.some(l=>l.name==='LanguageName'&&l.text==='Türkçe'))break;await page.waitForTimeout(100);}
    const restored=await cmd('ui');assert.equal(restored.labels.find(l=>l.name==='LanguageName')?.text,'Türkçe');
    evidence.push({mode,failed,restored,blocked,logs});
   }else{
    await page.locator('#cars-retry').waitFor({state:'visible',timeout:90000});
    assert(blocked.length>0);
    const button=page.locator('#cars-retry-btn'),box=await button.boundingBox();assert(box.height>=44);
    assert.equal(await page.locator('#cars-retry').getAttribute('lang'),startLang);
    await page.screenshot({path:`${out}/${mode}-failed.png`});
    const failures=[...logs];failing=false;
    await button.click();await ready();
    const restored=await cmd('ui');assert(restored.buttons.length>0);
    assert.equal(await page.locator('#cars-retry').count(),0);
    evidence.push({mode,restored,blocked,failures,logs});
   }
   console.log('PASS locale recovery',mode);
  }catch(error){evidence.push({mode,error:String(error),blocked,logs,state:await cmd('ui').catch(()=>null)});throw error;}
  finally{await context.close();}
 }
}finally{await fs.writeFile(out+'/evidence.json',JSON.stringify(evidence,null,2));await browser.close();}
