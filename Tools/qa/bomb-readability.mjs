// Verify live bomb instructions in all languages, including touch pause and viewport changes.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-bomb-hud/browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], samples = [];
const mobile=process.env.CG_BOMB_MOBILE==='1';
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
  const findings=[], languages=[];
  const check=(state,width,height,language)=>{
    const fields=state.labels.filter(l=>['CarrierTimer','PassHint'].includes(l.name));
    if(fields.length!==2)findings.push(`${language}: bomb fields missing`);
    const blockers=[...state.buttons.filter(b=>['LiveGoals','PauseButton','Fire','Brake','SwapWeapon','Left','Right','Reset','Secondary'].includes(b.name)),
      ...state.labels.filter(l=>['RaceStatus','TimerPanel','Health','AmmoPanel','Speedometer','SecondaryPanel','StatusPanel','HintsPanel','EventPanel'].includes(l.parent))];
    for(const l of fields){
      const min=l.name==='CarrierTimer'?14:12;
      if(l.fontPixels<min-.05 || l.truncated || l.height+.1<l.fontPixels*l.lines)findings.push(`${language} ${width}: unreadable ${l.name}`);
      if(l.x*width-l.width/2<0 || l.x*width+l.width/2>width || l.y*height-l.height/2<0 || l.y*height+l.height/2>height)findings.push(`${language} ${width}: out of view ${l.name}`);
      for(const b of [...blockers,...fields.filter(x=>x!==l)])
        if(Math.abs(l.x-b.x)*width<(l.width+b.width)/2-1 && Math.abs(l.y-b.y)*height<(l.height+b.height)/2-1)findings.push(`${language} ${width}: ${l.name} overlaps ${b.name}`);
    }
  };
  for(let choice=0;choice<Number(process.env.CG_BOMB_LOCALES || 1);choice++){
    await cmd('goto','settings');if(choice>0)await tap('LanguageNext');
    const language=(await cmd('ui')).labels.find(l=>l.name==='LanguageName')?.text;languages.push(language);
    for(const [width,height] of (choice===0?[[1280,720],[812,375],[640,360]]:[[640,360]])){
      await page.setViewportSize({width,height});await cmd('touch',width<1000?'on':'off');
      await cmd('startRace','e18','easy');
      if(mobile){const b=await page.locator('canvas').boundingBox();await input.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:b.width/2,y:b.height/2,id:1}]});await input.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});}
      for(let i=0;i<80 && (await cmd('state')).race!=='Racing';i++)await page.waitForTimeout(200);
      assert.equal((await cmd('state')).race,'Racing');
      for(let i=0;i<5;i++){
        const state=await cmd('ui');samples.push({language,width,height,phase:'driving',state});check(state,width,height,language);
        if(i===0)await page.screenshot({path:`${out}/bomb-${choice}-${width}.png`});
        if(i===1)await cmd('autopilot','on');await page.waitForTimeout(650);
      }
      await tap('LiveGoals');
      const expanded=await cmd('ui');samples.push({language,width,height,phase:'goals',state:expanded});check(expanded,width,height,language);
      await tap('LiveGoals');await tap('PauseButton');
      const paused=await cmd('ui');assert(paused.buttons.some(b=>b.name==='Resume'));
      assert(!paused.labels.some(l=>['CarrierTimer','PassHint'].includes(l.name)),'Pause hides bomb overlay');
      await page.waitForTimeout(500);const later=await cmd('state');assert(Math.abs(later.raceSeconds-paused.raceSeconds)<.03);
    }
  }
  // Switching to an ordinary race must restore the usual teaching-tip row.
  await cmd('startRace','e1','easy');
  if(mobile){const b=await page.locator('canvas').boundingBox();await input.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:b.width/2,y:b.height/2,id:1}]});await input.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});}
  for(let i=0;i<80 && (await cmd('state')).race!=='Racing';i++)await page.waitForTimeout(200);
  const ordinary=await cmd('ui'),hint=ordinary.labels.find(l=>l.name==='Hints');
  samples.push({phase:'ordinary-race',width:640,height:360,state:ordinary});
  assert(!ordinary.labels.some(l=>['CarrierTimer','PassHint'].includes(l.name)),'Ordinary race has no bomb card');
  assert(hint && hint.y*360-hint.height/2<85,'Ordinary race restores the original hint row');
  await tap('PauseButton');
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({renderer,mobile,languages,samples,errors,findings:[...new Set(findings)]},null,2));
  if(Number(process.env.CG_BOMB_LOCALES || 1)===10)assert.equal(new Set(languages).size,10);
  assert.equal(errors.length,0,errors.join('\n'));assert.equal(findings.length,0,[...new Set(findings)].join('\n'));
  console.log('BOMB HUD READABILITY PASS');
} finally {
  await fs.writeFile(`${out}/errors.json`,JSON.stringify(errors,null,2));await browser.close();
}
