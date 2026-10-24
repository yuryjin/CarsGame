// Actual secondary/weapon-selection inputs and contextual pause-help coverage.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Use webtest');
const out = process.env.OUT || '/tmp/cars-combat-help/browser';
await fs.mkdir(out, { recursive: true });
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const evidence = [], errors = [], findings = [];
let page;
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, storageState: process.env.CG_SOAK_STATE || '/tmp/cars-journey-state.json' });
  page = await context.newPage();
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
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args); await page.waitForTimeout(220);
    const s = await page.evaluate(() => { const s = window.CarsPlatform.debug.state; return typeof s === 'string' ? JSON.parse(s) : s; });
    assert.equal(s.error || '', ''); return s;
  };
  const click = async name => {
    const b = (await cmd('ui')).buttons.find(b => b.name === name); assert(b?.interactable, name);
    const box = await page.locator('canvas').boundingBox();
    await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height); await page.waitForTimeout(350);
  };
  const start = async event => {
    await cmd('startRace', event, 'easy');
    for (let i = 0; i < 60 && (await cmd('state')).race !== 'Racing'; i++) await page.waitForTimeout(200);
    assert.equal((await cmd('state')).evt, event); assert.equal((await cmd('state')).race, 'Racing');
  };
  const help = async event => {
    await page.keyboard.press('Escape'); await page.waitForTimeout(300); await click('Hint');
    const s = await cmd('ui'), text = s.labels.find(l => l.name === 'Lessons')?.text;
    assert(text); evidence.push({ phase: `${event}-help`, state: s });
    await page.screenshot({ path: `${out}/${event}-help.png` });
    await page.keyboard.press('Escape'); await page.waitForTimeout(200);
    assert((await cmd('ui')).buttons.some(b => b.name === 'Resume'));
    await page.keyboard.press('Escape'); await page.waitForTimeout(300);
    return text;
  };
  await start('e12');
  const minesHelp = await help('e12');
  if (!minesHelp.includes('Shift') || !minesHelp.includes('E')) findings.push('Mines-only help does not explain the secondary weapon key.');
  if (!minesHelp.includes('W')) findings.push('Special-event help omits basic driving controls.');
  const ammo = s => Number(s.labels.find(l => l.name === 'Secondary' && l.parent === 'SecondaryPanel')?.text.match(/(\d+)\s*\//)?.[1]);
  const beforeMine = await cmd('ui'); assert(Number.isFinite(ammo(beforeMine)));
  await page.keyboard.down('e'); await page.waitForTimeout(140); await page.keyboard.up('e');
  const afterMine = await cmd('ui'); assert.equal(ammo(afterMine), ammo(beforeMine) - 1, 'E must lay exactly one mine');
  await page.keyboard.down('Control'); await page.waitForTimeout(300); await page.keyboard.up('Control');
  const afterPrimary = await cmd('ui'); assert(!afterPrimary.labels.some(l => l.name === 'ActiveWeapon'), 'Mines-only must not offer an active cannon');
  evidence.push({ phase: 'actual-mine-key', beforeMine, afterMine, afterPrimary });
  await page.screenshot({ path: `${out}/mine-after-E.png` });
  await start('e11');
  const arsenalHelp = await help('e11');
  if (!arsenalHelp.includes('Q')) findings.push('Special-event help omits the equipped loadout controls.');
  const compact = await cmd('state');
  await page.keyboard.press('q'); await page.waitForTimeout(300);
  assert.notEqual((await cmd('state')).activePrimary, compact.activePrimary, 'Q must swap the two equipped weapons');
  await cmd('goto', 'garage');
  // Reach the existing full-arsenal setting through the actual scrolling garage.
  for (let i = 0; i < 30; i++) {
    const b = (await cmd('ui')).buttons.find(b => b.name === 'ToggleArsenal'); assert(b);
    if (b.y > .25 && b.y < .85) { await click('ToggleArsenal'); break; }
    await page.mouse.move(940, 500); await page.mouse.wheel(0, b.y > .5 ? 260 : -260); await page.waitForTimeout(180);
  }
  assert((await cmd('state')).fullArsenal, 'Full arsenal must be selected through the garage');
  await start('e11');
  const fullHelp = await help('e11-full');
  if (!fullHelp.includes('Q') || !fullHelp.includes('Tab')) findings.push('Full-arsenal help omits weapon-wheel/page controls.');
  await page.keyboard.down('q'); await page.waitForTimeout(200);
  const wheel = await cmd('ui'); assert(wheel.labels.some(l => l.name === 'Key' && l.parent === 'WeaponWheel'), 'Holding Q must open the arsenal wheel');
  await page.keyboard.press('Tab'); await page.waitForTimeout(200);
  const nextPage = await cmd('ui');
  assert.notEqual(nextPage.labels.find(l => l.name === 'Key' && l.parent === 'WeaponWheel')?.text, wheel.labels.find(l => l.name === 'Key' && l.parent === 'WeaponWheel')?.text, 'Tab must change pages');
  await page.keyboard.press('0'); await page.keyboard.up('q'); await page.waitForTimeout(350);
  const selected = await cmd('ui'); assert.equal(selected.activePrimary, 19, 'Second page, slot 0 selects the twentieth weapon');
  evidence.push({ phase: 'actual-arsenal-keys', wheel, nextPage, selected });
  await fs.writeFile(`${out}/evidence.json`, JSON.stringify({ renderer, evidence, errors, findings }, null, 2));
  assert.equal(errors.length, 0, errors.join('\n'));
  assert.equal(findings.length, 0, findings.join('\n'));
  console.log('COMBAT INPUT AND HELP PASS');
} catch (e) {
  await fs.writeFile(`${out}/failure.json`, JSON.stringify({ error: String(e), evidence, errors, findings }, null, 2));
  throw e;
} finally { await browser.close(); }
