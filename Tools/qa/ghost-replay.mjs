// Browser bridge + IL2CPP replay validation. The opponents below are synthetic mock-SDK fixtures.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import { gzipSync, gunzipSync } from 'node:zlib';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const out = process.env.OUT || '/tmp/cars-ghost-replay';
await fs.mkdir(out, {recursive:true});
function fnv(bytes) { let h=2166136261; for(const b of bytes) h=Math.imul(h^b,16777619)>>>0; return h; }
function fixture(hash, name, body='', legacy=false) {
  const label=Buffer.from(name), id=Buffer.from(body), count=legacy?121:4;
  const header=Buffer.alloc(legacy?13+label.length:18+label.length+id.length);
  header[0]=legacy?1:2; header.writeInt32LE(hash,1); header.writeInt16LE(50,5); header[7]=5;
  header[8]=label.length; label.copy(header,9); let p=9+label.length;
  if(!legacy) { header[p++]=id.length; id.copy(header,p); p+=id.length; }
  header.writeInt32LE(count,p); if(!legacy) header.writeInt32LE(12000,p+4);
  const samples=Buffer.alloc(count*(legacy?16:26)); let checksum=2166136261;
  for(let i=0;i<count;i++) {
    let k=i*(legacy?16:26); const x=legacy?1400:(i<2?0:5000), y=550, z=2200;
    if(!legacy) { samples.writeInt32LE([0,1000,2000,12000][i],k); k+=4; }
    samples.writeInt32LE(x,k); samples.writeInt32LE(y,k+4); samples.writeInt32LE(z,k+8); k+=12;
    if(legacy) {
      samples.writeUInt16LE(9000,k);
      for(const v of [x,z,9000]) checksum=Math.imul(checksum^v,16777619)>>>0;
    } else {
      // A rigid 150-degree roll makes discarded orientation unmistakable.
      samples.writeInt16LE(0,k); samples.writeInt16LE(0,k+2);
      samples.writeInt16LE(Math.round(Math.sin(75*Math.PI/180)*32767),k+4);
      samples.writeInt16LE(Math.round(Math.cos(75*Math.PI/180)*32767),k+6);
      samples[k+9]=i===2?2:0;
    }
  }
  const raw=Buffer.concat([header,samples]), footer=Buffer.alloc(4);
  footer.writeUInt32LE(legacy?checksum:fnv(raw));
  return gzipSync(Buffer.concat([raw,footer])).toString('base64');
}
function decodeHeader(encoded) {
  const raw=gunzipSync(Buffer.from(encoded,'base64')); assert.equal(raw[0],2);
  assert.equal(raw.readUInt32LE(raw.length-4),fnv(raw.subarray(0,-4)));
  let p=9+raw[8]; const length=raw[p++], body=raw.subarray(p,p+length).toString(); p+=length;
  const count=raw.readInt32LE(p), duration=raw.readInt32LE(p+4), start=p+8;
  assert.equal(raw.length,start+count*26+4); assert.equal(raw.readInt32LE(start),0);
  let previous=-1; for(let i=0;i<count;i++) { const time=raw.readInt32LE(start+i*26); assert(time>previous&&time<=duration); previous=time; }
  return {body,count,duration,bytes:Buffer.from(encoded,'base64').length};
}
const browser=await chromium.launch({channel:'chrome',args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),'--no-proxy-server']});
const evidence=[], errors=[];
let page;
try {
  const context=await browser.newContext({viewport:{width:1280,height:720}});
  await context.addInitScript(()=>{
    window.__ghostTest={opponents:[],delay:0,commits:[],pushes:[]};
    let mock;
    Object.defineProperty(window,'CarsMockSdk',{configurable:true,get:()=>mock,set(value){
      mock=value; const init=value.init;
      value.init=async()=>{
        const sdk=await init();
        sdk.multiplayer.sessions={
          init:()=>{ const list=structuredClone(window.__ghostTest.opponents), delay=window.__ghostTest.delay; return new Promise(resolve=>setTimeout(()=>resolve(list),delay)); },
          commit:payload=>window.__ghostTest.commits.push(payload),
          push:meta=>window.__ghostTest.pushes.push(meta)
        }; return sdk;
      };
    }});
    localStorage.setItem('cars.mock.playerdata',JSON.stringify({save:JSON.stringify({version:6,updatedAtUnixMs:Date.now(),settings:{lang:'en',hintsSeen:65535}})}));
  });
  page=await context.newPage();
  page.on('pageerror',e=>errors.push(e.message));
  page.on('console',m=>{if(/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text());});
  await page.goto(process.env.URL || 'http://127.0.0.1:8840/?sdk=mock&lang=en');
  await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(l=>/LoadingAPI.ready|ready\(\)/.test(l)),null,{timeout:180000});
  const renderer=await page.evaluate(()=>{const gl=document.querySelector('canvas').getContext('webgl2'), ext=gl.getExtension('WEBGL_debug_renderer_info');return gl.getParameter(ext?ext.UNMASKED_RENDERER_WEBGL:gl.RENDERER);});
  assert.match(renderer,/NVIDIA/i);
  const cmd=async(...args)=>{ await page.evaluate(a=>CarsPlatform.cmd(...a),args); await page.waitForTimeout(90); const state=await page.evaluate(()=>{const s=CarsPlatform.debug.state;return typeof s==='string'?JSON.parse(s):s;}); assert.equal(state.error||'','');return state; };
  const until=async(predicate,timeout=20000)=>{ const deadline=Date.now()+timeout; let state; do {state=await cmd('ghosts');if(predicate(state))return state;await page.waitForTimeout(100);}while(Date.now()<deadline);throw new Error(`Timed out: ${JSON.stringify(state)}`); };
  await cmd('startRace','e1','easy');
  const probe=await until(s=>s.race==='Racing'&&s.replay.recording);
  const hash=probe.replay.trackHash; assert.notEqual(hash,0);
  await cmd('goto','main');
  const opponents=[
    {player:{name:'Wrong track'},timeline:[{payload:{d:fixture(hash^1,'Wrong track','tortoise')}}]},
    {player:{name:'Broken'},timeline:[{payload:{d:'not base64'}}]},
    ...[['Tank replay','tortoise',false],['Buggy replay','dune',false],['Legacy replay','',true]].map(([name,body,legacy])=>({player:{name},timeline:[{payload:{d:fixture(hash,name,body,legacy)}}]}))
  ];
  await page.evaluate(opponents=>{window.__ghostTest.opponents=opponents;window.__ghostTest.delay=4500;},opponents);
  await cmd('startRace','e1','easy');
  const joined=await until(s=>s.replay.ghosts.length===3);
  assert.equal(joined.race,'Racing','Delayed opponents join after GO');
  for(const g of joined.replay.ghosts) {
    assert.equal(g.colliders,0);assert.equal(g.rigidbodies,0);assert(g.active);
    if(g.bodyId) {
      assert(g.meshes.some(name=>name===`Body_${g.bodyId}_Paint`||name===`GhostVisual_${g.bodyId}`));
      assert(Math.abs(g.rotation.z)>.96&&Math.abs(g.rotation.w)>.25&&Math.abs(g.rotation.w)<.27);
      if(Math.abs(joined.raceSeconds-2)>.05) assert.equal(g.position.x,joined.raceSeconds<2?0:50,'Late arrival uses elapsed race time');
    }
  }
  evidence.push({step:'late-join',state:joined});
  const pausedUi=await cmd('ui'), pause=pausedUi.buttons.find(b=>b.name==='PauseButton');assert(pause?.interactable);
  await page.mouse.click(pause.x*1280,pause.y*720);await page.waitForTimeout(100);
  const paused=await cmd('ghosts');await page.waitForTimeout(750);const still=await cmd('ghosts');
  assert.equal(still.raceSeconds,paused.raceSeconds);assert.deepEqual(still.replay.ghosts,paused.replay.ghosts);
  await page.keyboard.press('Escape');
  const cut=await until(s=>s.raceSeconds>2.15&&s.raceSeconds<10);
  for(const g of cut.replay.ghosts.filter(g=>g.bodyId)) assert.equal(g.position.x,50,'Recovery is a cut, not a slide');
  evidence.push({step:'recovery-cut',state:cut});
  await page.screenshot({path:`${out}/replay-race.png`,scale:'css'});
  await cmd('autopilot','on');
  const finished=await until(s=>s.flow==='Results'||s.race==='Finishing'||s.race==='Finished',240000);
  const sent=await page.evaluate(()=>window.__ghostTest);
  assert(sent.commits.length>0&&sent.pushes.length>0,'A real autopilot finish commits the replay');
  const recorded=decodeHeader(sent.commits.at(-1).d);
  assert(recorded.count>=10);assert.equal(recorded.body,'classic');assert(recorded.bytes<96*1024);
  assert.equal(sent.pushes.at(-1).meta2,recorded.duration,'Session time uses exact integer milliseconds');
  evidence.push({step:'natural-finish',recorded,state:finished});
  await fs.writeFile(`${out}/recorded-ghost.txt`,sent.commits.at(-1).d);
  await cmd('goto','main');
  assert.equal((await cmd('ghosts')).replay.ghosts.length,0);
  await page.evaluate(()=>{window.__ghostTest.delay=4500;});
  await cmd('startRace','e1','easy');await cmd('goto','main');await page.waitForTimeout(5000);
  const left=await cmd('ghosts');assert.equal(left.flow,'Menu');assert.equal(left.replay.ghosts.length,0,'Late response cannot recreate a finished race');
  evidence.push({step:'late-response-after-exit',state:left});
  assert.deepEqual(errors,[]);
  await fs.writeFile(`${out}/evidence.json`,JSON.stringify({renderer,evidence,errors},null,2));
  console.log('Ghost replay passed: v1/v2, body/orientation, pause, late join, recovery cut, actual finish, stale response.');
} catch(error) {
  await fs.writeFile(`${out}/failure.json`,JSON.stringify({error:String(error),evidence,errors},null,2));
  if(page) await page.screenshot({path:`${out}/failure.png`,scale:'css'}).catch(()=>{});
  throw error;
} finally { await browser.close(); }
