// Cross UTC midnight through the mock SDK clock; keep real browser/Unity time intact.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out=process.env.OUT || '/tmp/cars-endurance-20261024/daily-browser';
await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[],errors=[];
try {
  for (const lang of (process.env.CG_DAILY_LANGS || 'ru,en,zh').split(',')) {
    const context=await browser.newContext({viewport:{width:640,height:360},hasTouch:true});
    try {
      // An explicit UI fixture: one race exposes the daily card. No earned-profile claim.
      await context.addInitScript(lang=>localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify({version:5,updatedAtUnixMs:Date.now(),settings:{lang},progress:{ledger:[{deviceId:'daily-ui-fixture',races:1}]}})})),lang);
      const page=await context.newPage();
      page.on('pageerror',e=>errors.push(e.message));
      page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text()))errors.push(m.text());});
      await page.goto(process.env.URL || 'http://127.0.0.1:8809/?sdk=mock');
      await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
      const cmd=async(...args)=>{
        await page.evaluate(a=>window.CarsPlatform.cmd(...a),args);await page.waitForTimeout(200);
        const s=await page.evaluate(()=>{const v=window.CarsPlatform.debug.state;return typeof v==='string'?JSON.parse(v):v;});
        assert.equal(s.error || '','');return s;
      };
      const midnight=Date.UTC(2026,9,25);
      await page.evaluate(async now=>{
        window.__dailyNow=now;
        (await window.CarsMockSdk.init()).serverTime=()=>window.__dailyNow;
      },midnight-90000);
      await cmd('goto','main');
      const renderer=await page.evaluate(()=>{const g=document.querySelector('canvas').getContext('webgl2'),e=g.getExtension('WEBGL_debug_renderer_info');return g.getParameter(e?e.UNMASKED_RENDERER_WEBGL:g.RENDERER);});
      assert.match(renderer,/NVIDIA/i);
      let state;
      for(let attempt=0;attempt<25;attempt++) {
        state=await cmd('ui');const b=state.buttons.find(b=>b.name==='PlayDaily');
        assert(b?.interactable,'Daily card must be exposed by fixture');
        if(b.y>.35 && b.y<.9)break;
        await page.mouse.move(448,216);await page.mouse.wheel(0,b.y>.5?160:-160);await page.waitForTimeout(200);
      }
      const foot=s=>s.labels.find(l=>l.name==='Foot'&&l.parent==='DailyCard');
      const rules=s=>s.labels.find(l=>l.name==='Twist'&&l.parent==='DailyCard');
      await page.waitForTimeout(500);
      const initial=await cmd('ui');
      assert(foot(initial) && rules(initial));
      await page.evaluate(now=>window.__dailyNow=now,midnight-30000);await page.waitForTimeout(1400);
      const minute=await cmd('ui');
      assert.notEqual(foot(minute).text,foot(initial).text,'Minute countdown updates in the open menu');
      assert.equal(rules(minute).text,rules(initial).text,'A minute tick preserves the rules');
      await page.evaluate(now=>window.__dailyNow=now,midnight+1000);await page.waitForTimeout(1400);
      const next=await cmd('ui'),button=next.buttons.find(b=>b.name==='PlayDaily');
      const sample={lang,renderer,initial,minute,next};evidence.push(sample);
      await page.screenshot({path:`${out}/${lang}-midnight.png`});
      assert.notEqual(rules(next).text,rules(initial).text,'Midnight refreshes the daily rules');
      assert.notEqual(foot(next).text,foot(minute).text);
      assert(Math.abs(button.y-minute.buttons.find(b=>b.name==='PlayDaily').y)<.01,'Midnight preserves menu scroll');
      assert.equal(next.coins,initial.coins);assert.equal(next.races,initial.races);
      const box=await page.locator('canvas').boundingBox();
      await page.mouse.click(box.x+button.x*box.width,box.y+button.y*box.height);
      await page.waitForTimeout(400);
      const started=await cmd('state'),day=Math.floor((midnight-Date.UTC(2026,0,1))/86400000);
      assert.equal(started.evt,`d${day}`,'Play starts the displayed new UTC day');
      await page.evaluate(now=>window.__dailyNow=now,midnight+86401000);await page.waitForTimeout(1400);
      assert.equal((await cmd('state')).evt,started.evt,'A running race retains its start date');
      sample.started=started;
      console.log('DAILY ROLLOVER PASS',lang);
    } finally { await context.close(); }
  }
  assert.deepEqual(errors,[]);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));
  await browser.close();
}
