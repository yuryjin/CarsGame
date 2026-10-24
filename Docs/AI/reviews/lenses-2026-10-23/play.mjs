// Драйвер WebGL-сборки для ревью: открывает страницу, ждёт загрузки, выполняет шаги из STEPS, пишет скриншоты и состояние.
// Запуск ТОЛЬКО через webtest:  webtest -t 800 -- node play.mjs
// Env: URL (обязателен), OUT (папка для скриншотов/логов), STEPS, PROFILE (папка профиля Chrome — сохраняет сейв между запусками),
//      W/H (viewport, по умолчанию 1280x720), TOUCH=1 (эмуляция телефона 812x375 с тачем).
// STEPS — шаги через запятую:
//   click:fx:fy      клик по канвасу в долях ширины/высоты (0..1), напр. click:0.5:0.46
//   press:Code       нажать и отпустить клавишу (Playwright-код: KeyW, Space, Escape, Digit2, ControlLeft, ShiftLeft, KeyR, KeyQ, Tab)
//   hold:Code+Code:ms удерживать клавиши ms миллисекунд, напр. hold:KeyW+ControlLeft:4000 или hold:ArrowUp:3000
//   wait:s           подождать s секунд
//   shot:name        скриншот OUT/name.png
//   state            напечатать window.CarsPlatform.debug.state (JSON состояния игры) и OUT/state-<n>.json
//   cmd:a|b|c        QA-команда CarsPlatform.cmd(a,b,c), напр. cmd:goto|garage, cmd:startRace|e1|easy, cmd:touch|on, cmd:finish, cmd:autopilot|on
//   wheel:dy         колесо мыши по центру канваса
//   move:fx:fy       переместить курсор мыши (прицел) в долю канваса
//   mdown / mup      нажать/отпустить левую кнопку мыши (стрельба мышью) в текущей позиции
//   logs             напечатать накопленные ошибки консоли
// Пример: URL=... OUT=/tmp/x STEPS="shot:menu,state,click:0.5:0.46,wait:3,shot:events,state" webtest -- node play.mjs
import fs from 'node:fs';
const { chromium } = await import('/home/user/gstack/node_modules/playwright-core/index.mjs');
const url = process.env.URL, OUT = process.env.OUT || '.', W = +(process.env.W||1280), H = +(process.env.H||720), touch = process.env.TOUCH==='1';
fs.mkdirSync(OUT, { recursive: true });
const args = (process.env.WEBTEST_CHROME_ARGS || '--use-angle=vulkan --enable-features=Vulkan').split(' ').concat(['--autoplay-policy=no-user-gesture-required','--no-proxy-server']);
const vp = touch ? { width: 812, height: 375 } : { width: W, height: H };
let browser, ctx;
if (process.env.PROFILE) { ctx = await chromium.launchPersistentContext(process.env.PROFILE, { channel: 'chrome', headless: true, args, viewport: vp, hasTouch: touch, isMobile: touch }); browser = ctx; }
else { browser = await chromium.launch({ channel: 'chrome', headless: true, args }); ctx = await browser.newContext({ viewport: vp, hasTouch: touch, isMobile: touch }); }
const page = await ctx.newPage();
const logs = [];
page.on('console', m => { const t = m.text(); if (/error|exception/i.test(m.type()+t)) logs.push(m.type()+': '+t.slice(0,600)); });
page.on('pageerror', e => logs.push('pageerror: '+e.message));
const sleep = ms => new Promise(r => setTimeout(r, ms));
let n = 0;
const st = async () => { await page.evaluate(() => window.CarsPlatform.cmd('state')); await sleep(400); let s = await page.evaluate(() => window.CarsPlatform.debug.state); s = typeof s === 'string' ? s : JSON.stringify(s); fs.writeFileSync(`${OUT}/state-${++n}.json`, s || 'null'); console.log('STATE', s); };
const box = async () => page.locator('canvas').first().boundingBox();
try {
  await page.goto(url);
  let ready = false;
  for (let i = 0; i < 150 && !ready; i++) { await sleep(1000); ready = await page.evaluate(() => !!(window.CarsPlatform && window.CarsPlatform.debug && window.CarsPlatform.debug.log.some(l => /ready\(\)|LoadingAPI.ready/.test(l)))); }
  await sleep(3000);
  console.log('ready', ready);
  await page.evaluate(() => { const c = document.querySelector('canvas'); if (c) { c.setAttribute('tabindex','1'); c.focus(); } });
  for (const s of (process.env.STEPS||'').split(',').map(x => x.trim()).filter(Boolean)) {
    const [op, a, b] = s.split(':');
    console.log('>', s);
    if (op === 'click') { const bx = await box(); await page.mouse.click(bx.x + bx.width*+a, bx.y + bx.height*+b); await sleep(1500); }
    else if (op === 'press') { await page.keyboard.press(a); await sleep(300); }
    else if (op === 'hold') { const ks = a.split('+'); for (const k of ks) await page.keyboard.down(k); await sleep(+b); for (const k of ks) await page.keyboard.up(k); }
    else if (op === 'wait') await sleep(+a*1000);
    else if (op === 'shot') await page.screenshot({ path: `${OUT}/${a}.png` });
    else if (op === 'state') await st();
    else if (op === 'cmd') { await page.evaluate(x => window.CarsPlatform.cmd(...x), a.split('|')); await sleep(1500); }
    else if (op === 'wheel') { const bx = await box(); await page.mouse.move(bx.x + bx.width/2, bx.y + bx.height/2); await page.mouse.wheel(0, +a); await sleep(500); }
    else if (op === 'move') { const bx = await box(); await page.mouse.move(bx.x + bx.width*+a, bx.y + bx.height*+b); await sleep(100); }
    else if (op === 'mdown') await page.mouse.down();
    else if (op === 'mup') await page.mouse.up();
    else if (op === 'logs') { console.log('LOGS', logs.length); for (const l of logs.slice(-30)) console.log('  ', l); }
    else console.log('unknown step', s);
  }
  console.log('LOGS', logs.length); for (const l of logs.slice(-20)) console.log('  ', l);
} finally { await browser.close(); }
