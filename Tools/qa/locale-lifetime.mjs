// Rebuild every locale/body/track repeatedly in one real WebGL session, without forced GC or reloads.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
if (!process.env.WEBTEST_CHROME_ARGS) throw new Error('Run through webtest');
const url = new URL(process.env.URL);
assert(['localhost', '127.0.0.1'].includes(url.hostname) && url.searchParams.get('sdk') === 'mock');
const out = process.env.OUT; await fs.mkdir(out, { recursive: true });
const rounds = Math.max(5, Math.min(10, Number(process.env.CG_LOCALE_ROUNDS || 8)));
const report = { outcome: 'running', rounds: [], errors: [], languages: [], previews: 0, trackVisits: 0 };
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
const saveReport = () => fs.writeFile(`${out}/report.json`, JSON.stringify(report, null, 2));
try {
  const page = await browser.newPage({ viewport: { width: 1280, height: 720 } });
  page.on('pageerror', e => report.errors.push(e.message));
  page.on('console', m => { if (/Exception|OUT_OF_MEMORY|\[Loc\] missing key|character with Unicode.*not found/.test(m.text())) report.errors.push(m.text()); });
  await page.goto(url.href);
  await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(l => /LoadingAPI.ready|ready\(\)/.test(l)), null, { timeout: 180000 });
  report.renderer = await page.evaluate(() => { const gl = document.querySelector('canvas').getContext('webgl2'), ext = gl.getExtension('WEBGL_debug_renderer_info'); return gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER); });
  assert.match(report.renderer, /NVIDIA/i);
  const state = async (...args) => {
    await page.evaluate(a => window.CarsPlatform.cmd(...a), args.length ? args : ['state']);
    await page.waitForTimeout(120);
    const s = await page.evaluate(() => { const v = window.CarsPlatform.debug.state; return typeof v === 'string' ? JSON.parse(v) : v; });
    assert.equal(s.error || '', ''); return s;
  };
  async function click(name) {
    for (let attempt = 0; attempt < 20; attempt++) {
      const s = await state('ui'), b = s.buttons.find(b => b.name === name && b.interactable);
      assert(b, name);
      if (b.y > .15 && b.y < .92) {
        const canvas = await page.locator('canvas').boundingBox();
        await page.mouse.click(canvas.x + b.x * canvas.width, canvas.y + b.y * canvas.height);
        await page.waitForTimeout(160); return;
      }
      await page.mouse.move(900, 450); await page.mouse.wheel(0, b.y > .5 ? 250 : -250); await page.waitForTimeout(100);
    }
    throw new Error(`Cannot reach ${name}`);
  }
  const initial = report.initial = await state('perf', 'on');
  await state('goto', 'settings');
  const originalName = (await state('ui')).labels.find(l => l.name === 'LanguageName')?.text;
  assert(originalName);
  for (let round = 0; round < rounds; round++) {
    const languages = [];
    // Includes System plus all nine explicit locales, returning to the original override every round.
    for (let locale = 0; locale < 10; locale++) {
      await state('goto', 'settings');
      const before = (await state('ui')).labels.find(l => l.name === 'LanguageName')?.text;
      await click('LanguageNext');
      let language;
      for (let wait = 0; wait < 50; wait++) {
        language = (await state('ui')).labels.find(l => l.name === 'LanguageName')?.text;
        if (language && language !== before) break;
      }
      assert(language && language !== before, 'Locale change must rebuild the visible settings'); languages.push(language);
      await state('goto', 'garage');
      for (let body = 0; body < 20; body++) { await click('NextBody'); report.previews++; }
      for (const event of ['e1', 'e16', 'e17', 'e19', 'e20']) {
        const countdown = await state('startRace', event, 'easy'); assert.equal(countdown.race, 'Countdown');
        await page.waitForTimeout(150);
        const menu = await state('goto', 'main'); assert.equal(menu.flow, 'Menu'); report.trackVisits++;
      }
    }
    await state('goto', 'settings');
    assert.equal((await state('ui')).labels.find(l => l.name === 'LanguageName')?.text, originalName);
    await state('goto', 'main'); await page.waitForTimeout(500);
    const snapshot = await state('resources');
    assert.equal(snapshot.coins, initial.coins); assert.equal(snapshot.races, initial.races); assert.deepEqual(snapshot.upgrades, initial.upgrades);
    assert.equal(new Set(languages).size, 10); report.languages = languages;
    report.rounds.push({ round, at: new Date().toISOString(), resources: snapshot.resources, perf: snapshot.perf });
    await saveReport(); console.log('LOCALE LIFETIME', round, JSON.stringify(snapshot.resources), snapshot.perf?.totalAllocated);
  }
  const baseline = report.rounds[2].resources, last = report.rounds.at(-1).resources;
  for (const [key, allowance] of Object.entries({ materials: 3, physicsMaterials: 0, textures: 0, meshes: 3, renderTextures: 0, gameObjects: 3, audioClips: 0 })) {
    assert(Number.isFinite(last[key]), `Resource counter ${key} is available`);
    assert(last[key] <= baseline[key] + allowance, `${key} accumulates after warmed identical transitions: ${baseline[key]} -> ${last[key]}`);
  }
  assert.deepEqual(report.errors, []); report.outcome = 'passed';
  console.log('LOCALE LIFETIME PASS', report.previews, 'previews', report.trackVisits, 'track visits');
} catch (error) { report.outcome = 'failed'; report.failure = String(error); throw error; }
finally { await saveReport(); await browser.close(); }
