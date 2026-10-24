// Observe nameplates in ordinary close-pack driving, at desktop and phone sizes.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-nameplates/before';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], samples = [];
const mobile=process.env.CG_NAMEPLATE_MOBILE==='1';
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, storageState: process.env.CG_SOAK_STATE || '/tmp/cars-journey-state.json',
    ...(mobile?{hasTouch:true,isMobile:true,userAgent:`Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${browser.version()} Mobile Safari/537.36`}:{}) });
  const page = await context.newPage(),input=mobile?await context.newCDPSession(page):null;
  const tap=async name=>{
    const b=(await cmd('ui')).buttons.find(b=>b.name===name);assert(b?.interactable,name);
    const box=await page.locator('canvas').boundingBox(),x=box.x+b.x*box.width,y=box.y+b.y*box.height;
    if(!mobile)await page.mouse.click(x,y);
    else{await input.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y,id:1}]});await page.waitForTimeout(100);await input.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});}
    await page.waitForTimeout(250);
  };
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || `http://127.0.0.1:8789/?sdk=mock${mobile?'&device=mobile':''}`);
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(200);
    const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
    assert.equal(s.error || '', ''); return s;
  };
  const findings=[];
  await cmd('goto','settings');
  for(let i=0;i<Number(process.env.CG_NAMEPLATE_LANGUAGE || 0);i++)await tap('LanguageNext');
  const language=(await cmd('ui')).labels.find(l=>l.name==='LanguageName')?.text;
  for (const [width,height] of [[1280,720],[640,360]]) {
    await page.setViewportSize({width,height});await cmd('touch',width===640?'on':'off');
    await cmd('startRace','e18','easy');
    if(mobile){const b=await page.locator('canvas').boundingBox();await input.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:b.x+b.width/2,y:b.y+b.height/2,id:1}]});await page.waitForTimeout(100);await input.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});}
    for(let i=0;i<80 && (await cmd('state')).race!=='Racing';i++)await page.waitForTimeout(200);
    assert.equal((await cmd('state')).evt,'e18');await cmd('autopilot','on');
    let maxNames=0,carrierSeen=false;
    for(let i=0;i<14;i++) {
      const state=await cmd('ui'),names=state.labels.filter(l=>l.name==='RivalName');
      samples.push({width,height,state});
      maxNames=Math.max(maxNames,names.length);
      const banner=state.labels.find(l=>l.name==='CarrierTimer')?.text;
      const owner=banner?.match(/^Бомба у (.+): \d+ с$/)?.[1] || banner?.match(/^(.+)持有炸弹：\d+秒$/)?.[1];
      if(owner && names.some(n=>n.text===owner))carrierSeen=true;
      const blockers=[...state.buttons.filter(b=>['LiveGoals','PauseButton','Fire','Brake','SwapWeapon','Left','Right','Reset','Secondary'].includes(b.name)),
        ...state.labels.filter(l=>['RaceStatus','HotBomb','TimerPanel','Health','AmmoPanel','Speedometer','SecondaryPanel','StatusPanel'].includes(l.parent))];
      for(const n of names)for(const b of blockers)
        if(Math.abs(n.x-b.x)*width<(n.width+b.width)/2-1 && Math.abs(n.y-b.y)*height<(n.height+b.height)/2-1)
          findings.push(`${width}: ${n.text} overlaps HUD ${b.name}`);
      for(const l of names) {
        if(l.fontPixels<11.95 || l.truncated || l.lines>1)findings.push(`${width}: unreadable ${l.text} ${l.fontPixels}`);
        if(l.x*width-l.width/2<0 || l.x*width+l.width/2>width || l.y*height-l.height/2<0 || l.y*height+l.height/2>height)findings.push(`${width}: out-of-view ${l.text}`);
      }
      for(let a=0;a<names.length;a++)for(let b=a+1;b<names.length;b++) {
        const x=names[a],y=names[b];
        if(Math.abs(x.x-y.x)*width<(x.width+y.width)/2-1 && Math.abs(x.y-y.y)*height<(x.height+y.height)/2-1)
          findings.push(`${width}: overlapping ${x.text} / ${y.text}`);
      }
      if(i===2)await page.screenshot({path:`${out}/pack-${width}.png`});
      await page.waitForTimeout(400);
    }
    assert(maxNames>=4,`${width}: the pack must retain at least four names`);
    assert(carrierSeen,`${width}: a visible bomb-carrier name must be observed`);
    if(mobile)await tap('PauseButton');else await page.keyboard.press('Escape');await page.waitForTimeout(300);
    assert(!(await cmd('ui')).labels.some(l=>l.name==='RivalName'),'Pause must hide world names');
  }
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({renderer,mobile,language,samples,errors,findings:[...new Set(findings)]},null,2));
  assert.equal(errors.length,0,errors.join('\n'));
  assert.equal(findings.length,0,[...new Set(findings)].join('\n'));
  console.log('NAMEPLATE READABILITY PASS');

} finally {
  await fs.writeFile(`${out}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
