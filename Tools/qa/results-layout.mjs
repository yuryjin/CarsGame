// Results are seeded with QA finish; this checks presentation/actions, not driving.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const output = process.env.OUT || '/tmp/cars-results/browser';
await fs.mkdir(output, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [];
let page;
try {
  page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', e => errors.push(e.message));
  page.on('console', m => { if (/Exception|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) errors.push(m.text()); });
  await page.goto(process.env.URL || 'http://127.0.0.1:8788/?sdk=mock');
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  await page.waitForTimeout(1800);
  const cmd = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args);
    await page.waitForTimeout(250);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  const renderer = await page.evaluate(() => {
    const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info');
    return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
  });
  assert.match(renderer, /NVIDIA/i); console.log('GPU', renderer);
  const click = async name => {
    const b = (await cmd('ui')).buttons.find(b => b.name === name);
    assert(b?.interactable, `Unavailable ${name}`);
    assert(b.y > 0 && b.y < 1, `Offscreen ${name}`);
    const box = await page.locator('canvas').boundingBox();
    await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height);
    await page.waitForTimeout(450);
  };
  const finish = async () => {
    await cmd('startRace', 'e1', 'easy');
    for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(200);
    assert.equal((await cmd('state')).race, 'Racing');
    await cmd('finish'); await page.waitForTimeout(4000);
    assert.equal((await cmd('state')).flow, 'Results');
  };
  for (const [width, height] of [[1280,720], [812,375], [640,360]]) {
    await page.setViewportSize({ width, height });
    await finish();
    const initial = await cmd('ui');
    const reward = initial.labels.find(t => t.name === 'Reward');
    assert(reward && !reward.truncated && reward.fontPixels >= 16, 'Readable reward');
    assert(reward.y * height - reward.height / 2 > (height < 500 ? 45 : 120), 'Initial focus must not scroll reward above the viewport');
    const actions = initial.buttons.filter(b => /^ResultAction[0-2]$/.test(b.name));
    assert.equal(actions.length, 3);
    for (const b of actions) {
      assert(b.height >= 42 && b.width >= 90, `Target too small: ${b.name}`);
      assert(b.y * height + b.height / 2 < height && b.y * height - b.height / 2 > 0, `Offscreen ${b.name}`);
    }
    await page.screenshot({ path: `${output}/${width}-arrival.png` });
    await page.mouse.move(width * .3, height * .5);
    await page.mouse.wheel(0, 1800); await page.waitForTimeout(700);
    const scrolled = await cmd('ui');
    for (const b of actions) {
      const now = scrolled.buttons.find(x => x.name === b.name);
      assert(Math.abs(now.y - b.y) < .005, 'Actions must stay available during scrolling');
    }
    await page.screenshot({ path: `${output}/${width}-scrolled.png` });
    // Open the garage through the footer after scrolling, then verify its route.
    await click('ResultAction2'); assert.equal((await cmd('state')).screen, 'garage');
    evidence.push({ width, height, initial, scrolled });
    console.log('PASS', width, 'reward and fixed actions');
  }
  // Rewarded doubling updates both the persistent balance and the displayed amount exactly once.
  await finish();
  let state = await cmd('ui');
  for (let i = 0; i < 15; i++) {
    const ad = state.buttons.find(b => b.name === 'Double');
    if (ad && ad.y > .25 && ad.y < .72) break;
    await page.mouse.move(640 * .3, 360 * .5); await page.mouse.wheel(0, 160);
    await page.waitForTimeout(200); state = await cmd('ui');
  }
  const before = state.coins, reward = state.lastReward;
  await click('Double'); await page.waitForTimeout(4000);
  const doubled = await cmd('ui');
  assert.equal(doubled.coins, before + reward);
  assert(!doubled.buttons.some(b => b.name === 'Double'), 'Double reward cannot be claimed twice');
  await click('ResultAction1');
  const retry = await cmd('ui');
  assert.equal(retry.evt, 'e1', 'Retry starts the same event');
  assert.equal(retry.race, 'Countdown');
  assert.equal(retry.labels.find(t => t.name === 'Timer').renderedText, '0:00.000', 'Retry resets the displayed clock immediately');
  assert.equal(errors.length, 0, errors.join('\n'));
  await fs.writeFile(`${output}/evidence.json`, JSON.stringify({ renderer, evidence, doubled, errors }, null, 2));
  console.log('RESULTS LAYOUT PASS');
} catch (error) {
  if (page && !page.isClosed()) {
    await page.screenshot({ path: `${output}/failure.png` }).catch(() => {});
    await fs.writeFile(`${output}/failure-state.json`, JSON.stringify(await page.evaluate(() => window.CarsPlatform?.debug?.state).catch(() => null), null, 2));
  }
  throw error;
} finally {
  await fs.writeFile(`${output}/errors.json`, JSON.stringify(errors, null, 2));
  await browser.close();
}
