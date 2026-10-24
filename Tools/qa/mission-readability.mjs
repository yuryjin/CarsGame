// Mission HUD readability and pause safety, with real pointer activation.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-mission-review/browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], samples = [];
const mobile = process.env.CG_MISSION_MOBILE === '1';
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, storageState: process.env.CG_SOAK_STATE || '/tmp/cars-journey-state.json',
    ...(mobile ? {hasTouch:true,isMobile:true,userAgent:`Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${browser.version()} Mobile Safari/537.36`} : {}) });
  const page = await context.newPage(), input = mobile ? await context.newCDPSession(page) : null;
  const clickAt = async (x,y,hold=100) => {
    if (!mobile) return page.mouse.click(x,y);
    await input.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x,y,id:1}]});
    await page.waitForTimeout(hold);await input.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
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
  const findings = [], languages = [];
  const checkMarker=(state,width,height,label)=>{
    const marker=state.labels.find(l=>l.name==='BossDistance');if(!marker)return;
    const overlaps=b=>Math.abs(marker.x-b.x)*width<(marker.width+b.width)/2-1 && Math.abs(marker.y-b.y)*height<(marker.height+b.height)/2-1;
    const blockers=[...state.buttons.filter(b=>['LiveGoals','PauseButton','Fire','Brake','SwapWeapon','Left','Right','Reset','Secondary'].includes(b.name)),
      ...state.labels.filter(l=>['Speedometer','Health','AmmoPanel','SecondaryPanel','StatusPanel','BossPanel'].includes(l.parent))];
    for(const b of blockers)if(overlaps(b))findings.push(`${label}: boss distance overlaps ${b.name}`);
    if(marker.x*width-marker.width/2<0 || marker.x*width+marker.width/2>width || marker.y*height-marker.height/2<0 || marker.y*height+marker.height/2>height)
      findings.push(`${label}: boss distance is outside the viewport`);
  };
  for (let choice=0;choice<Number(process.env.CG_MISSION_LOCALES || 1);choice++) {
  await cmd('goto','settings');
  if(choice>0) {
    const b=(await cmd('ui')).buttons.find(b=>b.name==='LanguageNext');assert(b?.interactable);
    const box=await page.locator('canvas').boundingBox();
    await clickAt(box.x+b.x*box.width,box.y+b.y*box.height);await page.waitForTimeout(400);
  }
  const language=(await cmd('ui')).labels.find(l=>l.name==='LanguageName')?.text;
  languages.push(language);
  await cmd('startRace', 'm1', 'easy');
  if(mobile){const box=await page.locator('canvas').boundingBox();await clickAt(box.x+box.width/2,box.y+box.height/2);} 
  for (let i = 0; i < 80 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(200);
  assert.equal((await cmd('state')).evt, 'm1');
  if(mobile) {
    const before=await cmd('ui'),fire=before.buttons.find(b=>b.name==='Fire');assert(fire?.interactable);
    const ammo=s=>Number(s.labels.find(l=>l.name==='Ammo'&&l.parent==='AmmoPanel')?.text.match(/(\d+)\s*\//)?.[1]);
    const box=await page.locator('canvas').boundingBox();
    await clickAt(box.x+fire.x*box.width,box.y+fire.y*box.height,400);
    const after=await cmd('ui');assert(Number.isFinite(ammo(after)) && ammo(after)<ammo(before),'Actual touch must consume ammunition');
    samples.push({phase:'touch-fire',language,before,after});
    await page.waitForTimeout(8000);
  } else {
    await page.keyboard.down('w');await page.keyboard.down('Control');await page.waitForTimeout(9000);
    await page.keyboard.up('Control');await page.keyboard.up('w');
  }
  for (const [width, height] of (choice===0 ? [[1280,720],[960,540],[640,360]] : [[640,360]])) {
    await page.setViewportSize({width,height});
    await cmd('touch', width < 1000 ? 'on' : 'off');
    await page.waitForTimeout(600);
    const state = await cmd('ui'); samples.push({language,width,height,state});
    checkMarker(state,width,height,language);
    await page.screenshot({path:`${out}/mission-${choice}-${width}.png`});
    for (const [name, min] of [['BossName',14],['CorePct',14],['RouteText',12],['EnemyCount',12],['BossDistance',12]]) {
      const l=state.labels.find(l=>l.name===name);
      if (!l) findings.push(`${language} ${width}: ${name} absent`);
      else if (l.fontPixels < min - .05 || l.truncated || l.lines > (name==='BossName'?2:1) || l.height+.1<l.fontPixels*l.lines) findings.push(`${language} ${width}: ${name} font=${l.fontPixels.toFixed(1)}, truncated=${l.truncated}, lines=${l.lines}, height=${l.height}`);
    }
  }
  {
    const state=await cmd('ui'),b=state.buttons.find(b=>b.name==='LiveGoals');assert(b?.interactable);
    const box=await page.locator('canvas').boundingBox();
    await clickAt(box.x+b.x*box.width,box.y+b.y*box.height);await page.waitForTimeout(400);
    const expanded=await cmd('ui');samples.push({phase:'expanded-goals',language,state:expanded});
    checkMarker(expanded,640,360,language+' expanded');
    const goals=expanded.labels.find(l=>l.name==='Goals');assert(goals && goals.fontPixels>=11.95 && !goals.truncated && goals.lines>=3,'The actual goal tap must reveal readable mission objectives');
    if(choice===0) await page.screenshot({path:`${out}/mission-expanded-goals.png`});
    const g=expanded.buttons.find(b=>b.name==='LiveGoals');
    const overlap=(a,b)=>Math.abs(a.x-b.x)*640<(a.width+b.width)/2-1 && Math.abs(a.y-b.y)*360<(a.height+b.height)/2-1;
    for(const l of expanded.labels.filter(l=>['BossName','CorePct','RouteText','EnemyCount'].includes(l.name)))
      if(overlap(g,l))findings.push(`${language}: Expanded goals cover ${l.name}`);
    await clickAt(box.x+g.x*box.width,box.y+g.y*box.height);await page.waitForTimeout(300);
  }
  const before=await cmd('ui');
  const pause=before.buttons.find(b=>b.name==='PauseButton');assert(pause?.interactable);
  const box=await page.locator('canvas').boundingBox();
  await clickAt(box.x+pause.x*box.width,box.y+pause.y*box.height);
  await page.waitForTimeout(300);const paused=await cmd('ui');
  assert(paused.buttons.some(b=>b.name==='Resume'));
  await page.waitForTimeout(1200);const later=await cmd('ui');
  assert(Math.abs(paused.raceSeconds-later.raceSeconds)<.03);
  assert.equal(paused.labels.find(l=>l.name==='RouteText')?.text,later.labels.find(l=>l.name==='RouteText')?.text);
  samples.push({phase:'paused',language,state:later});
  }
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({renderer,mobile,languages,samples,errors,findings},null,2));
  if(Number(process.env.CG_MISSION_LOCALES || 1)===10) assert.equal(new Set(languages).size,10,'Each language selector option must be visited');
  assert.equal(errors.length,0,errors.join('\n'));
  assert.equal(findings.length,0,findings.join('\n'));
  console.log('MISSION READABILITY AND PAUSE PASS');
} finally {
  await fs.writeFile(`${out}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
