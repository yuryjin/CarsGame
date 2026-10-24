// Headless-Chrome check of the music (CarsMusic.jslib) in the WebGL build with the mock SDK.
// Env: URL (served by Tools/qa/serve.py, add ?sdk=mock), AUTOPLAY=0 to test the gesture unlock,
// RACE=0 to skip the race part, SHOTS=<dir> for a settings screenshot. Prints one line per check and "MUSIC PASS/FAIL".
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
const autoplay = process.env.AUTOPLAY !== '0';
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const args = [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'];
args.push(autoplay ? '--autoplay-policy=no-user-gesture-required' : '--autoplay-policy=user-gesture-required');
const browser = await chromium.launch({ channel: 'chrome', args });
let ok = true;
try {
  const page = await (await browser.newContext({ viewport: { width: 1280, height: 720 } })).newPage();
  // Playwright page.evaluate marks its CDP calls as user gestures. Passive reads must not
  // accidentally unlock audio before the gesture-unlock scenario has checked the initial state.
  const cdp = await page.context().newCDPSession(page);
  const passive = async fn => (await cdp.send('Runtime.evaluate', {
    expression: '(' + fn.toString() + ')()', returnByValue: true, awaitPromise: true, userGesture: false
  })).result.value;
  const logs = [];
  page.on('console', m => { const t = m.text(); if (/error|exception|music/i.test(m.type() + t)) logs.push(m.type() + ': ' + t.slice(0, 300)); });
  page.on('response', r => { if (r.status() >= 400) logs.push('HTTP ' + r.status() + ' ' + r.url()); });
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const cmd = (...a) => page.evaluate(a => window.CarsPlatform.cmd(...a), a);
  const music = () => passive(() => {
    const s = window.CarsMusic; if (!s) return null;
    const ch = c => ({ src: (c.el.src || '').split('/').pop(), paused: c.el.paused, t: +c.el.currentTime.toFixed(2), level: c.level, active: c.active });
    return { track: s.debug.track, sting: s.debug.sting, plays: s.debug.plays, blocked: s.debug.blocked, errors: s.debug.errors,
      ctx: s.ctx ? s.ctx.state : 'none', bus: s.bus ? +s.bus.gain.value.toFixed(3) : null, locked: s.locked, chans: s.chans.map(ch), stingCh: ch(s.sting) };
  });
  const flow = async () => { await cmd('state'); await sleep(400); const st = await page.evaluate(() => window.CarsPlatform.debug.state); return st && (typeof st === 'string' ? JSON.parse(st) : st).flow; };
  const check = (name, cond, info) => { console.log((cond ? 'ok   ' : 'FAIL ') + name + (info ? '  ' + JSON.stringify(info) : '')); if (!cond) ok = false; };
  const playing = m => m && m.chans.some(c => c.active && !c.paused && c.src === m.track);

  await page.goto(process.env.URL || 'http://127.0.0.1:8788/?sdk=mock');
  for (let i = 0; i < 120; i++) { await sleep(1000); if (await passive(() => !!(window.CarsPlatform && window.CarsPlatform.debug.log.some(l => /LoadingAPI.ready/.test(l))))) break; }
  await sleep(4000);
  const renderer = await passive(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  if (!/NVIDIA/i.test(renderer)) throw new Error('Expected NVIDIA renderer: ' + renderer);
  console.log('GPU', renderer);
  let m = await music();
  if (!autoplay) {
    check('before a gesture the menu loop waits (blocked or suspended)', m && m.track === 'menu.mp3' && (m.blocked > 0 || m.ctx !== 'running'), m);
    const box = await page.locator('canvas').first().boundingBox();
    await page.mouse.click(box.x + 5, box.y + 5);
    await sleep(2500);
    m = await music();
  }
  const t0 = m && m.chans.find(c => c.src === 'menu.mp3');
  await sleep(2000);
  m = await music();
  const t1 = m && m.chans.find(c => c.src === 'menu.mp3');
  check('menu loop plays and advances', playing(m) && m.track === 'menu.mp3' && t1.t > t0.t, m);
  check('audio context running, bus at the music level', m.ctx === 'running' && m.bus > 0.3 && m.bus < 0.7, { ctx: m.ctx, bus: m.bus });
  check('no media errors', m.errors === 0, { errors: m.errors });

  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => true }); document.dispatchEvent(new Event('visibilitychange')); });
  await sleep(800);
  m = await music();
  check('hidden tab: every element paused, context suspended', m.chans.every(c => c.paused) && m.ctx !== 'running', m);
  await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => false }); document.dispatchEvent(new Event('visibilitychange')); });
  await sleep(1500);
  m = await music();
  check('visible again: menu loop resumes', playing(m) && m.ctx === 'running', m);

  // Mock interstitial: onOpen at 100 ms, onClose at 1600 ms. The pause travels JS → C# (PauseReason.Ad) → jslib.
  await page.evaluate(() => window.CarsPlatform.interstitial(990001));
  await sleep(700);
  m = await music();
  check('interstitial open: music paused', m.chans.every(c => c.paused), m);
  await sleep(2000);
  m = await music();
  check('interstitial closed: music resumes', playing(m), m);
  if (process.env.SHOTS) {
    await cmd('goto', 'settings'); await sleep(1500);
    await page.screenshot({ path: process.env.SHOTS + '/music_settings.png' });
    await cmd('goto', 'main'); await sleep(1000);
  }

  if (process.env.RACE !== '0') {
    await cmd('startRace', 'e1', 'easy');
    await sleep(1500);
    m = await music();
    check('countdown: music fades out', m.track === '', m);
    let raceOk = false;
    for (let i = 0; i < 20 && !raceOk; i++) { await sleep(1000); m = await music(); raceOk = playing(m) && /^race_/.test(m.track); }
    check('GO: a race loop plays', raceOk, m);
    await sleep(3000);
    await cmd('finish');                      // QaFinishNow: the field finishes at once
    let f = '';
    for (let i = 0; i < 60 && f !== 'Results'; i++) { await sleep(1000); f = await flow(); }
    m = await music();
    check('results reached', f === 'Results', { flow: f });
    check('finish sting played', /^(win|lose)\.mp3$/.test(m.sting), m);
    await sleep(5000);
    m = await music();
    check('menu loop back on results', playing(m) && m.track === 'menu.mp3', m);
  }
  console.log('LOGS', logs.length); for (const l of logs.slice(0, 30)) console.log('  ', l);
  console.log(ok ? 'MUSIC PASS' : 'MUSIC FAIL');
} finally {
  await browser.close();
}
process.exitCode = ok ? 0 : 1;
