// Reload the saved real-race journey in a fresh browser context; never grant progress.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-playtest-final/persistence';
const journey = JSON.parse(await fs.readFile(process.env.CG_JOURNEY_RESULTS || '/tmp/cars-playtest-final/campaign/races.json', 'utf8'));
const last = journey.races.filter(r => r.result).at(-1);
assert(last, 'The journey must contain a recorded race result');
const expected = last.result;
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const errors = [];
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
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(300);
    return page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
  };
  const restored = await cmd('state');
  for (const key of ['coins','races','equippedPrimary','reservePrimary','fullArsenal','upgrades'])
    assert.deepEqual(restored[key], expected[key], `Reload must preserve ${key}`);
  await cmd('goto', 'events');
  const events = await cmd('ui');
  const next = `Start_e${Number(last.event.slice(1)) + 1}`;
  if (last.completed !== false && expected.position <= 3 && Number(last.event.slice(1)) < 17)
    assert(events.buttons.some(b => b.name === next && b.interactable), 'The next campaign event must remain unlocked after reload');
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, event: last.event, expected, restored, events, errors }, null, 2));
  console.log('PROFILE RELOAD PASS', restored.coins, 'coins;', restored.races, 'races');
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
