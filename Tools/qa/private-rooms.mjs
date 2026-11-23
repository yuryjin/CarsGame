// Launch with webtest and two fresh browser contexts. URL selects the relay, OUT keeps both logs,
// and CG_ROOM_CASE selects race, coop-win, coop-loss, disconnect-race or disconnect-coop.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const scenario = process.env.CG_ROOM_CASE || 'race';
assert(['race','coop-win','coop-loss','disconnect-race','disconnect-coop'].includes(scenario));
const url = process.env.URL || 'http://127.0.0.1:43431/?sdk=mock&roomServer=ws://127.0.0.1:43430/net';
const out = process.env.OUT || '/tmp/cars-private-room';
await fs.mkdir(out,{recursive:true});
const report = { scenario, url, renderers:[], transport:[[],[]], messages:[], errors:[], samples:[] };
const clients = [[],[]];
const save = () => fs.writeFile(path.join(out,'evidence.json'),JSON.stringify(report,null,2));
const browser = await chromium.launch({ channel:'chrome', args:[...process.env.WEBTEST_CHROME_ARGS.split(' '),
  '--no-proxy-server','--disable-background-timer-throttling','--disable-renderer-backgrounding','--disable-backgrounding-occluded-windows'] });

let httpServer;
async function serveBuild() {
  const script=await fs.readFile(fileURLToPath(import.meta.url));
  report.scenarioSha256=createHash('sha256').update(script).digest('hex');
  await fs.writeFile(path.join(out,'scenario.mjs'),script);
  const dir=process.env.CG_ROOM_WEB_DIR;
  if(!dir) return;
  const origin=new URL(url);
  assert.equal(origin.hostname,'127.0.0.1');
  if(process.env.CG_ROOM_BUILD_LOG) {
    let finished=false;
    for(let i=0;i<100;i++) {
      const log=await fs.readFile(process.env.CG_ROOM_BUILD_LOG,'utf8');
      const succeeded=log.lastIndexOf('[BuildCommands] Succeeded');
      if(succeeded>=0 && /\[WebZip\].* MB zip,.* MB uncompressed/.test(log.slice(succeeded))) {
        finished=true; break;
      }
      await new Promise(resolve=>setTimeout(resolve,500));
    }
    assert(finished,'the final packaged WebGL build finished successfully');
  }
  const archiveDir=path.dirname(dir);
  const archives=(await fs.readdir(archiveDir)).filter(name=>name.endsWith('.zip'));
  assert.equal(archives.length,1,'expected a completed, packaged WebGL build');
  const archive=path.join(archiveDir,archives[0]);
  const index=await fs.readFile(path.join(dir,'index.html'));
  if(process.env.CG_ROOM_BUILD_LOG && process.env.CG_ROOM_SOURCE) {
    const buildLog=await fs.readFile(process.env.CG_ROOM_BUILD_LOG,'utf8');
    assert(buildLog.includes('-projectPath\n'+process.env.CG_ROOM_SOURCE+'\n'));
    assert(buildLog.includes('-buildOutput\n'+dir+'\n'));
  }
  report.build={archive,sha256:createHash('sha256').update(await fs.readFile(archive)).digest('hex'),
    indexSha256:createHash('sha256').update(index).digest('hex'),source:process.env.CG_ROOM_SOURCE};
  const serve=fileURLToPath(new URL('./serve.py',import.meta.url));
  httpServer=spawn('python3',[serve,dir,origin.port],{stdio:['ignore','ignore','pipe']});
  let stderr='';
  httpServer.stderr.on('data',chunk=>stderr+=chunk.toString());
  let ready=false;
  for(let i=0;i<70 && httpServer.exitCode === null;i++) {
    try {
      const response=await fetch(origin.origin+'/',{signal:AbortSignal.timeout(900)});
      if(response.ok && index.equals(Buffer.from(await response.arrayBuffer()))) { ready=true; break; }
    } catch {}
    await new Promise(resolve=>setTimeout(resolve,100));
  }
  assert(ready && httpServer.exitCode === null,'local HTTP server failed: '+stderr);
  await save();
}

async function command(page,...args) {
  await page.evaluate(args => window.CarsPlatform.cmd(...args),args);
  await page.waitForFunction(name => {
    const data = window.CarsPlatform.debug.state;
    return data && (typeof data === 'string' ? JSON.parse(data) : data).lastCommand === name;
  },args[0],{timeout:15000});
  await page.waitForTimeout(100);
  const state = await page.evaluate(() => {
    const x = window.CarsPlatform.debug.state;
    return typeof x === 'string' ? JSON.parse(x) : x;
  });
  assert(!state.error,state.error);
  return state;
}

async function click(page,name,label = false,scrollable = true) {
  const box = await page.locator('canvas').boundingBox();
  for (let i=0; i<40; i++) {
    const ui = await command(page,'ui');
    const control = (label ? ui.labels : ui.buttons).find(item => item.name === name);
    if(!control) {
      if(i<14) { await page.waitForTimeout(180); continue; }
      assert.fail('Missing '+name+' on '+ui.screen);
    }
    if (scrollable ? control.y >= .18 && control.y <= .9 : control.y > 0 && control.y < 1) {
      assert(label || control.interactable,name+' disabled');
      await page.mouse.click(box.x + control.x*box.width,box.y + control.y*box.height);
      await page.waitForTimeout(240);
      return;
    }
    await page.mouse.move(box.x+box.width*.67,box.y+box.height*.66);
    await page.mouse.wheel(0,control.y>.9?280:-220);
    await page.waitForTimeout(180);
  }
  throw new Error('Could not reach '+name);
}

async function until(page,accept,limit = 30000) {
  const end = Date.now()+limit;
  let state;
  do {
    state = await command(page,'room');
    if (accept(state)) return state;
    await page.waitForTimeout(250);
  } while (Date.now()<end);
  throw new Error('Room wait expired: '+JSON.stringify({screen:state.screen,room:state.room}));
}

async function checkRelay() {
  if (!process.env.CG_ROOM_SERVER_LOG) return;
  const ending=scenario.startsWith('disconnect') ? 'close' : 'finish';
  const marker='[PrivateRoom] '+ending+' '+report.code+' ';
  let final;
  for (let i=0;i<15 && !final;i++) {
    const lines=(await fs.readFile(process.env.CG_ROOM_SERVER_LOG,'utf8')).split('\n');
    final=lines.find(line=>line.includes(marker));
    if(!final) await new Promise(resolve=>setTimeout(resolve,200));
  }
  assert(final,'relay recorded '+ending+' for room '+report.code);
  if(ending==='close') {
    assert(final.includes('departing='+report.interruption.departed));
    assert.match(final,/finished=False/i);
  } else {
    const outcome=Number(final.match(/outcome=(\d+)/)?.[1]);
    const order=final.match(/order=([\d,]+)/)?.[1]?.split(',').map(Number);
    assert.equal(outcome,report.results.rooms[0].outcome);
    assert.deepEqual(order,report.results.rooms[0].order);
    if(report.classification?.available) {
      assert.equal(Number(final.match(/finished=(\d+)/)?.[1]),report.classification.finished[0]);
    }
    for(const seat of [0,1]) {
      const client=report.messages.find(([index,line])=>index===seat && line.includes('[PrivateRoom] result '+report.code+' '));
      assert(client,'client '+seat+' recorded its result');
      assert(client[1].includes('order='+order.join(',')));
    }
  }
  report.relayEnding=final;
}

async function driver(index) {
  const ctx = await browser.newContext({viewport:{width:960,height:540}});
  if (scenario.startsWith('disconnect')) {
    const endpoint = new URL(url).searchParams.get('roomServer');
    await ctx.addInitScript(endpoint=>{
      const native=window.WebSocket;
      const peers=new Set();
      window.WebSocket=class extends native {
        constructor(...args) {
          super(...args);
          if(this.url===endpoint) {
            peers.add(this);
            this.addEventListener('close',()=>peers.delete(this),{once:true});
          }
        }
      };
      window.closePrivateRoomTransport=()=>{
        const open=[...peers].filter(ws=>ws.readyState===native.OPEN);
        for(const socket of open) socket.close(1000,'local test disconnect');
        return open.length;
      };
    },endpoint);
  }
  const page = await ctx.newPage();
  page.on('websocket',socket=>{
    const trace={url:socket.url(),openedAt:Date.now(),sent:0,received:0};
    report.transport[index].push(trace);
    socket.on('framesent',()=>trace.sent++);
    socket.on('framereceived',()=>trace.received++);
    socket.on('socketerror',error=>trace.error=error);
    socket.on('close',()=>trace.closedAt=Date.now());
  });
  page.on('pageerror',e=>{
    report.errors.push([index,e.message]);
    clients[index].push({at:Date.now(), type:'pageerror', text:e.message});
  });
  page.on('requestfailed',req=>clients[index].push({at:Date.now(),type:'requestfailed',text:req.url()+' '+req.failure()?.errorText}));
  page.on('console',m=>{
    clients[index].push({at:Date.now(),type:m.type(),text:m.text().slice(0,1800)});
    if (/\[PrivateRoom\]|WebSocket|Exception|missing key/.test(m.text()))
      report.messages.push([index,m.text().slice(0,480)]);
  });
  await page.goto(url);
  await page.waitForFunction(()=>window.CarsPlatform?.debug?.log?.some(s=>/LoadingAPI.ready|ready\(\)/.test(s)),null,{timeout:180000});
  report.renderers[index] = await page.evaluate(()=>{
    const gl=document.querySelector('canvas').getContext('webgl2'), ext=gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(report.renderers[index],/NVIDIA/i);
  await click(page,'OpenRooms');
  return {page,ctx};
}

try {
  await serveBuild();
  const [host,guest] = await Promise.all([driver(0),driver(1)]);
  const pages = [host.page,guest.page];
  report.before = await Promise.all(pages.map(p=>command(p,'room')));
  const fortress = scenario.includes('coop');
  await click(host.page,fortress?'CreateCoopRoom':'CreateRaceRoom');
  const lobby = await until(host.page,s=>s.room?.state==='Lobby');
  report.code = lobby.room.code;
  await save();
  assert.match(String(lobby.room.code),/^\d{8}$/);
  await click(guest.page,'Code',true);
  await guest.page.keyboard.type(String(lobby.room.code),{delay:80});
  await guest.page.keyboard.press('Enter');
  await click(guest.page,'JoinRoom');
  const roster = await Promise.all(pages.map(p=>until(p,s=>s.room?.state==='Lobby' && s.room.members===3)));
  report.roster=roster.map(s=>s.room);
  assert.deepEqual(roster.map(s=>s.room.seat),[0,1]);
  assert.equal(roster[1].room.code,lobby.room.code);
  await click(host.page,'RoomStart');
  const start = await Promise.all(pages.map(p=>until(p,s=>s.room?.state==='Playing' && s.race==='Racing',50000)));
  assert.equal(start[0].raceSeed,start[1].raceSeed);
  assert(start.every(s=>s.room.positions.length===4));
  report.start = start;
  report.gameplayStartedAt = Date.now();
  await save();
  if (scenario.startsWith('disconnect')) {
    const departure = process.env.CG_ROOM_DEPARTING || (fortress ? 'host' : 'guest');
    assert(['host','guest'].includes(departure));
    const seat = departure === 'host' ? 0 : 1;
    const leaving = [host,guest][seat];
    const other = [host,guest][1-seat];
    report.departing = {seat,before:await command(leaving.page,'room')};
    await save();
    const sockets=await leaving.page.evaluate(()=>window.closePrivateRoomTransport());
    assert.equal(sockets,1,'sever the actual playing transport of one client');
    const [left,after]=await Promise.all([leaving.page,other.page].map(p=>until(p,s=>s.room?.state==='Interrupted',60000)));
    assert.equal(left.flow,'Menu');
    assert.equal(left.screen,'rooms');
    assert.equal(left.races,report.before[seat].races);
    assert.equal(left.coins,report.before[seat].coins);
    assert.equal(after.flow,'Menu');
    assert.equal(after.screen,'rooms');
    const message = (await command(other.page,'ui')).labels.find(l=>l.parent==='RoomStatus');
    assert(message?.text.trim(),'interrupted peer sees a reason');
    assert.equal(after.coins,report.before[1-seat].coins);
    assert.equal(after.races,report.before[1-seat].races);
    report.interruption = {departed:seat,disconnected:left,remaining:after,text:message.text};
  } else {
    await command(host.page,'autopilot','on');
    await guest.page.keyboard.down('s');
    try {
      await guest.page.waitForTimeout(1200);
      report.differentInput = await Promise.all(pages.map(p=>command(p,'room')));
    } finally { await guest.page.keyboard.up('s'); }
    const startPositions=report.start[0].room.positions;
    const telemetry=report.differentInput[0].room;
    assert(Math.hypot(telemetry.positions[0].x-startPositions[0].x,telemetry.positions[0].z-startPositions[0].z)>.35 ||
      Math.abs(telemetry.throttles[0]-telemetry.throttles[1])>.1,'the two seats act independently');
    if(scenario==='coop-win') {
      report.squadOrders=await Promise.all(pages.map(async p=>{
        await click(p,'SquadCommand',false,false);
        await click(p,'Mark0',false,false);
        return (await command(p,'ui')).labels.find(l=>l.parent==='SquadCommand')?.text;
      }));
      assert(report.squadOrders.every(Boolean),'both players issued a squad order through the HUD');
    }
    if(scenario!=='coop-loss') await command(guest.page,'autopilot','on');
    if(scenario==='coop-loss') {
      await command(host.page,'autopilot','off');
      const directions=['a','d'];
      try {
        await Promise.all(pages.map(async (p,i)=>{ await p.keyboard.down('s'); await p.keyboard.down(directions[i]); }));
        await guest.page.waitForTimeout(3000);
        report.retreat=await Promise.all(pages.map(p=>command(p,'room')));
      } finally {
        await Promise.all(pages.map(async (p,i)=>{ await p.keyboard.up(directions[i]); await p.keyboard.up('s'); }));
      }
    }
    const limit=Date.now()+(fortress?610000:345000);
    let last;
    do {
      last=await Promise.all(pages.map(p=>command(p,'room')));
      report.samples.push(last.map(s=>({room:s.room,seconds:s.raceSeconds,lap:s.lap,laps:s.laps,speed:s.speedKmh,finishState:s.race})));
      if(report.samples.length%5===0) await save();
      if(last.every(s=>s.room?.state==='Results')) break;
      assert(last.every(s=>['Playing','Results'].includes(s.room?.state)),'room interrupted');
      await guest.page.waitForTimeout(1100);
    } while(Date.now()<limit);
    assert(last.every(s=>s.room?.state==='Results'),'both clients reached results');
    assert.deepEqual(last[0].room.order,last[1].room.order);
    assert.deepEqual([...last[0].room.order].sort(),[0,1,2,3]);
    const wanted=scenario==='race'?0:scenario==='coop-win'?1:2;
    assert.equal(last[0].room.outcome,last[1].room.outcome,'both clients report the same outcome');
    if (scenario==='race') {
      const masks=last.map(s=>s.room.finished);
      const available=masks.every(Number.isInteger);
      report.classification={available,finished:masks,members:last[0].room.members};
      if (available || process.env.CG_ROOM_REQUIRE_FINISH==='1') {
        assert(available,'this release must report the finished-seat mask');
        assert.equal(masks[0],masks[1],'both screens report the same classified finishers');
        for(const s of last) assert.equal(s.room.finished & s.room.members,s.room.members,'every human driver physically finished');
      }
    }
    if(fortress && last.every(s=>Number.isInteger(s.room.finished))) {
      const masks=last.map(s=>s.room.finished);
      report.classification={available:true,finished:masks,members:last[0].room.members};
      assert.equal(masks[0],masks[1]);
      if(scenario==='coop-loss') assert.equal(masks[0],0,'defeat leaves every seat unclassified');
      else assert.equal(masks[0] & last[0].room.members,last[0].room.members);
    }
    if(scenario==='coop-win') for(let client=0;client<2;client++) for(const phase of [1,2,3])
      assert(report.samples.some(pair=>pair[client].room.phase===phase),'client '+client+' observed phase '+phase);
    const results=await Promise.all(pages.map(p=>command(p,'ui')));
    const rows=results.map(s=>s.labels.filter(l=>l.parent.startsWith('RoomPlace')).sort((a,b)=>a.parent.localeCompare(b.parent)).map(l=>l.text));
    assert.equal(rows[0].length,4);
    assert.deepEqual(rows[0],rows[1]);
    assert(results.every((s,i)=>s.coins===report.before[i].coins && s.races===report.before[i].races),'private matches leave solo progress intact');
    const driverMovement=[0,1].map(seat=>Math.max(0,...report.samples.map(pair=>{
      const position=pair[0].room?.positions?.[seat];
      return position ? Math.hypot(position.x-startPositions[seat].x,position.z-startPositions[seat].z) : 0;
    })));
    if(scenario!=='coop-loss') assert(driverMovement.every(d=>d>10),'both drivers traveled from their start');
    report.results={rooms:last.map(s=>s.room),rows,driverMovement,
      playerLapsObserved:pages.map((_,i)=>Math.max(0,...report.samples.map(pair=>pair[i].lap)))};
    if(scenario==='coop-win') {
      const contribution=['shots','coreDamage'].map(key=>[0,1].map(seat=>Math.max(0,
        ...report.samples.map(pair=>pair[0].room?.[key]?.[seat]??0),last[0].room?.[key]?.[seat]??0)));
      report.results.shots=contribution[0];
      report.results.coreDamage=contribution[1];
      if(process.env.CG_ROOM_REQUIRE_SHOTS==='1' || last[0].room.shots?.length) {
        assert(contribution[0].every(n=>n>0),'both team members fired live weapons');
        assert(contribution[1].some(n=>n>0),'a human seat damaged the core during the winning fight');
      }
    }
    await host.page.screenshot({path:path.join(out,'host-result.png')});
    await guest.page.screenshot({path:path.join(out,'guest-result.png')});
    await save();
    assert(last.every(s=>s.room.outcome===wanted),'shared outcome '+wanted);
  }
  assert.deepEqual(report.errors,[]);
  report.engineExceptions=clients.map(log=>log.filter(msg=>/\b[A-Za-z.]+Exception:/.test(msg.text))
    .map(msg=>({at:msg.at,text:msg.text.slice(0,320)})));
  assert(report.engineExceptions.every(errors=>errors.length===0),'Unity reported a runtime exception');
  await checkRelay();
  report.passed=true;
} catch(e) {
  report.failure=e.stack;
  for(const [i,ctx] of browser.contexts().entries()) for(const p of ctx.pages()) {
    const state=await command(p,'room').catch(()=>null);
    const ui=await command(p,'ui').catch(()=>null);
    (report.failureStates??=[]).push({i,state,ui});
  }
  throw e;
} finally {
  try {
    report.kinematicWarnings=clients.map(log=>log.filter(msg=>/Setting (angular|linear) velocity of a kinematic body/.test(msg.text)).length);
    await Promise.all(clients.map((messages,i)=>fs.writeFile(path.join(out,'client-'+i+'.json'),JSON.stringify(messages,null,2))));
    if (process.env.CG_ROOM_SERVER_LOG && report.code) {
      const relay = await fs.readFile(process.env.CG_ROOM_SERVER_LOG,'utf8');
      const entries = relay.split('\n').filter(line=>line.includes('[PrivateRoom]') && line.includes(String(report.code)));
      report.relay = entries;
      await fs.writeFile(path.join(out,'relay.txt'),entries.join('\n')+'\n');
    }
    await save();
  } finally {
    try { await browser.close(); } finally {
      if (httpServer && httpServer.exitCode===null && httpServer.signalCode===null) {
        const stopped=new Promise(resolve=>httpServer.once('close',resolve));
        httpServer.kill();
        const terminate=setTimeout(()=>httpServer.kill('SIGKILL'),2000);
        try { await stopped; } finally { clearTimeout(terminate); }
      }
    }
  }
}
