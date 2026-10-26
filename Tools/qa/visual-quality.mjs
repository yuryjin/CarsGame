// Sequential browser comparison on the same GPU. Physical phones require a separate device check.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const {chromium}=await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if(!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out=process.env.OUT || '/tmp/cars-visual-quality';
await fs.mkdir(out,{recursive:true});
const report={samples:[],errors:[]};
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
try {
  for(const [tag,address] of [['before',process.env.BEFORE_URL],['after',process.env.URL]]) {
    if(!address) continue;
    for(const [width,height] of [[1280,720],[640,360]]) {
      const mobile=width<1000;
      const context=await browser.newContext({viewport:{width,height},hasTouch:mobile,isMobile:mobile});
      try {
        const page=await context.newPage();
        page.on('pageerror',e=>report.errors.push(e.message));
        page.on('console',m=>{if(/Exception|OUT_OF_MEMORY|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) report.errors.push(m.text());});
        const url=new URL(address);url.searchParams.set('lang','ru');if(mobile)url.searchParams.set('device','mobile');
        await page.goto(url.href);
        await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
        const renderer=await page.evaluate(()=>{
          const gl=document.querySelector('canvas').getContext('webgl2'),ext=gl.getExtension('WEBGL_debug_renderer_info');
          return gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER);
        });
        assert.match(renderer,/NVIDIA/i);
        const cmd=async(...args)=>{
          await page.evaluate(a=>window.CarsPlatform.cmd(...a),args);await page.waitForTimeout(150);
          const s=await page.evaluate(()=>{const v=window.CarsPlatform.debug.state;return typeof v==='string'?JSON.parse(v):v;});
          assert.equal(s.error||'','');return s;
        };
        for(const event of ['e1','e19']) {
          await cmd('startRace',event,'easy');await cmd('autopilot','on');
          for(let i=0;i<80&&(await cmd('state')).race!=='Racing';i++)await page.waitForTimeout(100);
          await page.waitForTimeout(3000);await cmd('perf','on');
          await page.waitForTimeout(16000);
          const state=await cmd('ui');
          assert(state.perf.frames>300,'A useful window of real rendered frames');
          assert(state.perf.p95<50,`Long frame times: ${tag}/${width}/${event} p95=${state.perf.p95} ms`);
          const screenshot=`${tag}-${width}-${event}.png`;
          await page.screenshot({path:`${out}/${screenshot}`,scale:'css'});
          report.samples.push({tag,width,height,event,renderer,screenshot,state});
          await fs.writeFile(`${out}/evidence.json`,JSON.stringify(report,null,2));
          console.log('QUALITY',tag,width,event,JSON.stringify(state.perf));
          await cmd('goto','main');
        }
      } finally {await context.close();}
    }
  }
  assert.deepEqual(report.errors,[]);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify(report,null,2));
  await browser.close();
}
