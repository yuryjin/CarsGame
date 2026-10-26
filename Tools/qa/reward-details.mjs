// Controlled finish fixtures verify coin presentation, never driving or real ad delivery.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out=process.env.OUT || '/tmp/cars-reward-details';
await fs.mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[],errors=[];
try {
  for(const lang of ['ru','en','tr','zh','de','es','fr','pt','uk']) {
    const context=await browser.newContext({viewport:{width:1280,height:720}});
    let page;
    try {
      await context.addInitScript(lang=>localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify({version:6,updatedAtUnixMs:Date.now(),settings:{lang,hintsSeen:65535}})})),lang);
      page=await context.newPage();
      page.on('pageerror',e=>errors.push(e.message));
      page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text());});
      const url=new URL(process.env.URL || 'http://127.0.0.1:8843/?sdk=mock');url.searchParams.set('lang',lang);
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
      const reveal=async(name,collection='buttons')=>{
        let previous=null,gain=1;
        for(let i=0;i<45;i++) {
          const state=await cmd('ui'),t=state[collection].find(t=>t.name===name);
          assert(t,`Missing ${name}`);
          const {width,height}=page.viewportSize();
          const footer=state.buttons.filter(b=>/^ResultAction[0-2]$/.test(b.name));
          const bottom=Math.min(...footer.map(b=>b.y*height-b.height/2))-8;
          const top=height<500?72:130,y=t.y*height;
          if(y-t.height/2>=top && y+t.height/2<=bottom) return t;
          if(previous) {const measured=(previous.y-y)/previous.step;if(measured>.01) gain=measured;}
          const delta=y-(top+bottom)/2;
          const step=Math.sign(delta)*Math.max(1,Math.min(60,Math.abs(delta)/gain));
          await page.mouse.move(width*.25,(top+bottom)/2);await page.mouse.wheel(0,step);await page.waitForTimeout(180);
          previous={y,step};
        }
        throw new Error(`Cannot reveal ${name}`);
      };
      const click=async name=>{
        const t=await reveal(name),box=await page.locator('canvas').boundingBox();
        assert(t.interactable,name);await page.mouse.click(box.x+t.x*box.width,box.y+t.y*box.height);await page.waitForTimeout(350);
      };
      const daily=async()=>{
        await cmd('daily');
        for(let i=0;i<80&&(await cmd('state')).race!=='Racing';i++) await page.waitForTimeout(100);
        assert.equal((await cmd('state')).race,'Racing');await cmd('finish');await page.waitForTimeout(1300);
        assert.equal((await cmd('state')).flow,'Results');
      };
      const before=(await cmd('state')).coins;
      await daily();
      let state=await cmd('ui');
      assert(state.lastDailyBonus>0);
      const received=state.coins-before;
      assert.equal(received,state.lastReward+state.lastDailyBonus);
      const check=(state,expected)=>{
        assert.equal(state.lastTotalReward,expected);
        const reward=state.labels.find(t=>t.name==='Reward');assert(reward && !reward.truncated && reward.fontPixels>=16);
        assert.equal(Number(reward.text.match(/\d+/)[0]),expected);
        const rows=state.labels.filter(t=>t.parent==='RewardBreakdown');
        assert(rows.length>3,'Expanded reward breakdown');
        assert(rows.every(t=>!t.truncated && t.fontPixels>=14),`${lang}: reward details remain readable`);
        const amounts=rows.filter(t=>/Coins$/.test(t.name) && t.name!=='RewardTotalCoins');
        assert.equal(amounts.reduce((sum,t)=>sum+Number(t.text),0),expected);
        assert.equal(Number(rows.find(t=>t.name==='RewardTotalCoins').text),expected);
      };
      await click('RewardDetails');
      for(const [width,height] of [[1280,720],[812,375],[640,360]]) {
        await page.setViewportSize({width,height});await page.waitForTimeout(500);
        state=await cmd('ui');check(state,received);
        await reveal('RewardTotalCoins','labels');
        state=await cmd('ui');check(state,received);
        evidence.push({lang,width,height,phase:'daily',renderer,state});
        if(width===640 || lang==='ru') await page.screenshot({path:`${out}/${lang}-${width}-details.png`,scale:'css'});
      }
      if(lang==='ru') {
        await click('Double');await page.waitForTimeout(4000);
        state=await cmd('ui');
        assert.equal(state.coins-before,received+state.lastReward);
        check(state,received+state.lastReward);
        assert(!state.buttons.some(b=>b.name==='Double'),'The ad reward is available once');
        assert.equal(Number(state.labels.find(t=>t.name==='RewardAdCoins').text),state.lastReward);
        evidence.push({lang,phase:'ad',state});
        const repeatBefore=state.coins;
        await daily();state=await cmd('ui');
        assert.equal(state.lastDailyBonus,0);
        assert.equal(state.coins-repeatBefore,state.lastReward);
        assert(!state.labels.some(t=>t.parent==='RewardBreakdown'),'A new result starts with details collapsed');
        await click('RewardDetails');state=await cmd('ui');check(state,state.lastReward);
        assert(!state.labels.some(t=>t.name==='RewardAdCoins'||t.name==='RewardDailyCoins'));
        evidence.push({lang,phase:'repeat',state});
      }
      console.log('PASS reward details',lang);
    } catch(error) {
      if(page && !page.isClosed()) {
        await page.screenshot({path:`${out}/failure-${lang}.png`,scale:'css'}).catch(()=>{});
        evidence.push({lang,error:String(error),state:await page.evaluate(()=>window.CarsPlatform?.debug?.state).catch(()=>null)});
      }
      throw error;
    } finally {await context.close();}
  }
  assert.deepEqual(errors,[]);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));
  await browser.close();
}
