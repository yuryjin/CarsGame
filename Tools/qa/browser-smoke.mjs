// Headless-Chrome smoke of the WebGL build with the mock SDK (served by Tools/qa/serve.py).
// Env: URL, SP (output dir), W, H, TOUCH=1, TAG, STEPS="click:fx:fy,wait:s,shot:name,cmd:a|b,state,hide:1|0,waitresults:n,wheel:dy".
// Needs playwright-core somewhere: PLAYWRIGHT_CORE=/path/to/playwright-core/index.mjs.
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
const SP = process.env.SP, url = process.env.URL, W = +(process.env.W||1280), H = +(process.env.H||720), tag = process.env.TAG||'d';
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const browser = await chromium.launch({ channel: 'chrome',
  args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--autoplay-policy=no-user-gesture-required', '--no-proxy-server'] });
try {
  const ctx = await browser.newContext({ viewport: { width: W, height: H }, hasTouch: process.env.TOUCH==='1', isMobile: process.env.TOUCH==='1' });
  const page = await ctx.newPage();
  const logs = [];
  page.on('console', m => { const t = m.text(); if (/error|exception|warn/i.test(m.type()+t)) logs.push(m.type()+': '+t.slice(0,1500)); });
  page.on('requestfailed', r => logs.push('reqfail: '+r.url())); page.on('response', r => { if (r.status() >= 400) logs.push('HTTP '+r.status()+' '+r.url()); }); page.on('pageerror', e => logs.push('pageerror: '+e.message));
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const shot = async n => page.screenshot({ path: `${SP}/shots/${tag}_${n}.png` });
  const st = async () => page.evaluate(() => window.CarsPlatform && window.CarsPlatform.debug ? { state: window.CarsPlatform.debug.state, starts: window.CarsPlatform.debug.gameplayStarts, stops: window.CarsPlatform.debug.gameplayStops } : null);
  const cmd = async (...a) => page.evaluate(a => window.CarsPlatform.cmd(...a), a);
  async function clickAt(fx, fy) {
    const box = await page.locator('canvas').first().boundingBox();
    await page.mouse.click(box.x + box.width*fx, box.y + box.height*fy);
  }
  await page.goto(url);
  let ready = false;
  for (let i = 0; i < 120 && !ready; i++) { await sleep(1000); ready = await page.evaluate(() => !!(window.CarsPlatform && window.CarsPlatform.debug && window.CarsPlatform.debug.log.some(l => /ready\(\)|LoadingAPI.ready/.test(l)))); }
  await sleep(3000);
  console.log('ready', ready);
  if (!ready) throw new Error('Game readiness timed out');
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  if (!/NVIDIA/i.test(renderer)) throw new Error('Expected NVIDIA renderer: ' + renderer);
  console.log('GPU', renderer);
  await cmd('state'); await sleep(500); console.log('menu', JSON.stringify(await st()));
  await shot('01_menu');
  const steps = (process.env.STEPS||'').split(',').filter(Boolean);
  for (const s of steps) {
    const [op, a, b] = s.split(':');
    if (op === 'click') { await clickAt(+a, +b); await sleep(2500); }
    else if (op === 'cmd') { await cmd(...a.split('|')); await sleep(1500); }
    else if (op === 'wait') await sleep(+a*1000);
    else if (op === 'shot') await shot(a);
    else if (op === 'wheel') { const box = await page.locator('canvas').first().boundingBox(); await page.mouse.move(box.x + box.width/2, box.y + box.height/2); await page.mouse.wheel(0, +a); await sleep(800); }
    else if (op === 'state') { await cmd('state'); await sleep(300); console.log('state', JSON.stringify(await st())); }
    else if (op === 'hide') { await page.evaluate(v => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => v === '1' }); document.dispatchEvent(new Event('visibilitychange')); }, a); await sleep(1000); }
    else if (op === 'waitresults') { for (let i = 0; i < +a; i++) { await cmd('state'); await sleep(1000); const x = await st(); if (x && x.state && (typeof x.state === 'string' ? JSON.parse(x.state) : x.state).flow === 'Results') break; } }
  }
  console.log('LOGS', logs.length); for (const l of logs.slice(0, 40)) console.log('  ', l);
} finally {
  await browser.close();
}
