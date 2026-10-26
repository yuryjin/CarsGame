// Local record fixtures cover provenance text; race capture/merge is checked by native tests.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-record-readability';
await fs.mkdir(out, { recursive:true });
const browser = await chromium.launch({ channel:'chrome', args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server'] });
const evidence = [], errors = [];
try {
  for (const [width,height] of [[1280,720],[812,375],[640,360]]) for (const lang of (process.env.CG_RECORD_LANGS || 'ru,en,zh,de,es,fr,pt,tr,uk').split(',')) {
    const context = await browser.newContext({ viewport:{width,height}, hasTouch:width<1000, isMobile:width<1000 });
    await context.addInitScript(lang => {
      if (localStorage.getItem('record-fixture-seeded')) return;
      const setup = { captured:true, tier:'hard', body:'sidewinder', trackHash:123, laps:3, build:3,
        primary:0,reserve:1,secondary:1,upgrades:[4,4,4],weapons:[3,3,3,3],fullArsenal:true,continued:true };
      const save = { version:6,updatedAtUnixMs:Date.now(),settings:{lang},progress:{bests:[
        {eventId:'e1',bestTimeMs:100000,stars:1,setup},
        {eventId:'e2',bestTimeMs:110000,stars:1},
        {eventId:'e19',bestTimeMs:120000,stars:1,setup}
      ]}};
      localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify(save)}));
      localStorage.setItem('record-fixture-seeded','1');
    },lang);
    const page = await context.newPage();
    try {
      page.on('pageerror', e=>errors.push(e.message));
      page.on('console', m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text());});
      const url = new URL(process.env.URL || 'http://127.0.0.1:8833/?sdk=mock');
      url.searchParams.set('lang',lang); if(width<1000) url.searchParams.set('device','mobile');
      const ready = ()=>page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
      await page.goto(url.href); await ready();
      const renderer = await page.evaluate(()=>{
        const gl=document.querySelector('canvas').getContext('webgl2'),ext=gl.getExtension('WEBGL_debug_renderer_info');
        return gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER);
      });
      assert.match(renderer,/NVIDIA/i);
      const cmd = async (...args)=>{
        await page.evaluate(a=>window.CarsPlatform.cmd(...a),args); await page.waitForTimeout(180);
        const s=await page.evaluate(()=>{const v=window.CarsPlatform.debug.state;return typeof v==='string'?JSON.parse(v):v;});
        assert.equal(s.error||'',''); return s;
      };
      await cmd('goto','leaders');
      const check = async phase=>{
        const ui=await cmd('ui');
        const rows=ui.labels.filter(l=>['Local_e1','Local_e2','Local_e19'].includes(l.parent));
        assert.equal(rows.filter(l=>l.name==='RecordSetup').length,3);
        for(const label of rows) {
          assert(!label.truncated, `${lang}/${width} ${label.parent}/${label.name} truncated: ${label.text}`);
          assert(label.fontPixels>=13.9,`${label.name} too small: ${label.fontPixels}`);
        }
        const known=rows.find(l=>l.parent==='Local_e1'&&l.name==='RecordSetup');
        const old=rows.find(l=>l.parent==='Local_e2'&&l.name==='RecordSetup');
        assert.match(known.text,/4/); assert(!old.text.includes('4'),'Old record must not invent upgrades');
        assert.notEqual(known.text,old.text);
        evidence.push({width,height,lang,phase,renderer,rows});
      };
      await check('initial');
      if(['ru','zh'].includes(lang)) await page.screenshot({path:`${out}/${lang}-${width}.png`,scale:'css'});
      await page.reload(); await ready(); await cmd('goto','leaders'); await check('reload');
      console.log('PASS record readability/reload',lang,width);
    } catch(error) {
      await page.screenshot({path:`${out}/failure-${lang}-${width}.png`,scale:'css'}).catch(()=>{});
      evidence.push({width,height,lang,error:String(error),state:await page.evaluate(()=>window.CarsPlatform?.debug?.state).catch(()=>null)});
      throw error;
    } finally { await context.close(); }
  }
  assert.deepEqual(errors,[]);
} finally {
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({evidence,errors},null,2));
  await browser.close();
}
