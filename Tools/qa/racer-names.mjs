// Observe a naturally assigned/passed bomb in WebGL; no carrier or damage cheats.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-combat-names/browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [], samples = [];
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, storageState: process.env.CG_SOAK_STATE || '/tmp/cars-journey-state.json' });
  const page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8789/?sdk=mock');
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
  await cmd('startRace', 'e18', 'easy');
  for (let i = 0; i < 80 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(200);
  assert.equal((await cmd('state')).evt, 'e18');
  await cmd('autopilot', 'on');
  let owner;
  for (let i = 0; i < 90; i++) {
    const s = await cmd('ui');
    samples.push(s);
    owner = s.labels.find(l => /^Бомба у .+: \d+ с$/.test(l.text));
    if (owner) break;
    await page.waitForTimeout(800);
  }
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ renderer, owner, samples, errors }, null, 2));
  await page.screenshot({ path: `${out}/bomb-owner.png` });
  assert(owner, 'A Russian rival-carrier banner must be observed during ordinary bomb play');
  assert.match(owner.text, /^Бомба у (Рекс|Нова|Болт|Вега|Зед|Кира|Макс)( \d+)?:/);
  assert(!owner.truncated);
  assert.equal(errors.length, 0, errors.join('\n'));
  console.log('LOCALIZED BOMB OWNER PASS', owner.text);
} finally {
  await fs.writeFile(`${out}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
