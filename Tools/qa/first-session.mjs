// Four first-session milestones and three cold navigations on a local WebGL build.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';

const { chromium } = await import(process.env.PLAYWRIGHT_CORE || '/home/user/gstack/node_modules/playwright-core/index.mjs');
assert(process.env.WEBTEST_CHROME_ARGS, 'Run through webtest');
const url = new URL(process.env.URL);
assert(['127.0.0.1', 'localhost'].includes(url.hostname) && url.searchParams.get('sdk') === 'mock');
const out = process.env.OUT || '/tmp/cars-first-session';
await fs.mkdir(out, { recursive: true });
const network = { offline: false, latency: 80, downloadThroughput: 1_500_000, uploadThroughput: 750_000 };
const baseline = process.env.CG_FIRST_SESSION_BASELINE === '1';
const report = { environment: 'Desktop Chrome, local mock SDK, laboratory network shaping', baseline, network, sessions: [], errors: [] };
const browser = await chromium.launch({ channel: 'chrome', args: [...process.env.WEBTEST_CHROME_ARGS.split(' '), '--no-proxy-server'] });
report.chrome = browser.version();

function observe(trace, state, ms) {
  let phase;
  if (!trace.length && state.flow === 'Menu' && state.screen === 'main') phase = 'menu ready';
  if (trace.length === 1 && state.flow === 'Racing' && state.race === 'Racing') phase = 'first start';
  if (trace.length === 2 && state.flow === 'Results') phase = 'first results';
  if (trace.length === 3 && state.flow === 'Racing' && state.race === 'Racing') phase = 'next start';
  if (phase) {
    if (!baseline) assert.equal(state.firstSessionStage, trace.length + 1, 'Gameplay milestone at ' + phase);
    trace.push({ phase, ms: Math.round(ms), event: state.evt });
  }
}

try {
  for (let run = 0; run < 3; run++) {
    const session = { driver: run === 0 ? 'keyboard then autopilot' : 'keyboard', trace: [] };
    report.sessions.push(session);
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, locale: 'ru' });
    try {
      const page = await context.newPage();
      page.on('pageerror', error => report.errors.push(error.message));
      page.on('console', msg => { if (/Exception|OUT_OF_MEMORY|\[Loc\] missing key/.test(msg.text())) report.errors.push(msg.text()); });
      const cdp = await context.newCDPSession(page);
      await cdp.send('Network.enable');
      await cdp.send('Network.setCacheDisabled', { cacheDisabled: true });
      await cdp.send('Network.emulateNetworkConditions', network);
      await page.goto(url.href);
      await page.waitForFunction(() => window.CarsPlatform?.debug?.log?.some(s => s.includes('LoadingAPI.ready')), null, { timeout: 180000 });
      const box = await page.locator('#unity-canvas').boundingBox();
      async function snapshot(command = 'state', ...args) {
        const data = await page.evaluate(async ({ command, args }) => {
          const previous = window.CarsPlatform.debug.state;
          window.CarsPlatform.cmd(command, ...args);
          for (let frame = 0; frame < 90; frame++) {
            const raw = window.CarsPlatform.debug.state;
            if (raw && raw !== previous) {
              const state = typeof raw === 'string' ? JSON.parse(raw) : raw;
              if (state.lastCommand === command) return { state, ms: performance.now() };
            }
            await new Promise(resolve => requestAnimationFrame(resolve));
          }
          throw Error('Timed out reading ' + command);
        }, { command, args });
        assert.equal(data.state.error || '', '');
        observe(session.trace, data.state, data.ms);
        return data;
      }
      async function until(predicate, timeoutMs = 15000) {
        const deadline = Date.now() + timeoutMs;
        do {
          const data = await snapshot();
          if (predicate(data.state)) return data;
          await page.waitForTimeout(60);
        } while (Date.now() < deadline);
        assert.fail('Game state timeout: ' + JSON.stringify((await snapshot()).state));
      }
      async function click(name) {
        const { state } = await snapshot('ui');
        const b = state.buttons.find(item => item.name === name && item.interactable);
        assert(b && b.x > 0 && b.x < 1 && b.y > 0 && b.y < 1, 'Reachable ' + name);
        await page.mouse.click(box.x + b.x * box.width, box.y + b.y * box.height, { delay: 60 });
      }

      await until(s => s.screen === 'main' && s.flow === 'Menu');
      session.menuReadyMs = session.trace[0].ms;
      const platform = await page.evaluate(() => {
        const gl = document.querySelector('canvas').getContext('webgl2');
        const ext = gl.getExtension('WEBGL_debug_renderer_info');
        const downloads = performance.getEntriesByType('resource').filter(e => /\/Build\/.*\.(data|wasm)\.unityweb(?:\?|$)/.test(e.name));
        return { renderer: gl.getParameter(ext ? ext.UNMASKED_RENDERER_WEBGL : gl.RENDERER), ua: navigator.userAgent,
          downloads: downloads.map(e => ({ file: new URL(e.name).pathname.split('/').pop(), endMs: Math.round(e.responseEnd), transferred: e.transferSize })) };
      });
      assert.match(platform.renderer, /NVIDIA/i);
      assert.equal(platform.downloads.length, 2);
      assert(platform.downloads.every(row => row.transferred > 0));
      Object.assign(session, platform);
      session.downloadCompleteMs = Math.max(...platform.downloads.map(row => row.endMs));
      if (run === 0) await page.screenshot({ path: out + '/menu.png' });
      await click('Play');
      await until(s => s.race === 'Racing' && s.evt === 'e1');
      await page.locator('#unity-canvas').focus();
      await page.keyboard.down('w');
      const start = await until(s => s.speedKmh >= 8);
      await page.keyboard.down('d');
      const turn = await until(s => s.steer > 0.25 && s.speedKmh >= 8 && Math.abs(s.lateral - start.state.lateral) > 0.10);
      session.firstSteeredTurnMs = Math.round(turn.ms);
      session.turn = { speedKmh: turn.state.speedKmh, steer: turn.state.steer, lateralChange: turn.state.lateral - start.state.lateral };
      await page.keyboard.up('d');
      if (run === 0) {
        await page.keyboard.down('Control');
        await page.waitForTimeout(350);
        await page.screenshot({ path: out + '/first-race.png' });
        await page.keyboard.up('Control');
      }
      await page.keyboard.up('w');
      assert(session.downloadCompleteMs <= session.menuReadyMs && session.menuReadyMs < session.firstSteeredTurnMs);
      if (run === 0) {
        await click('PauseButton');
        await page.waitForTimeout(250);
        const paused = (await snapshot('ui')).state;
        assert(paused.buttons.some(b => b.name === 'Resume' && b.interactable));
        await page.waitForTimeout(300);
        const stillPaused = (await snapshot()).state;
        assert.equal(stillPaused.raceSeconds, paused.raceSeconds);
        if (!baseline) assert.equal(stillPaused.firstSessionStage, 2);
        await click('Resume'); await until(s => s.raceSeconds > paused.raceSeconds);
        await snapshot('autopilot', 'on');
        const deadline = Date.now() + 300000;
        while (session.trace.length < 3 && Date.now() < deadline) {
          const { state } = await snapshot();
          if (state.race === 'Finishing' && state.flow === 'Racing') {
            const ui = (await snapshot('ui')).state;
            if (ui.buttons.some(b => b.name === 'FinishResults' && b.interactable)) await click('FinishResults');
          }
          await page.waitForTimeout(700);
        }
        assert.equal(session.trace.at(-1)?.phase, 'first results');
        await page.screenshot({ path: out + '/results.png' });
        await snapshot('ui');
        if (!baseline) assert.equal((await snapshot('ui')).state.firstSessionStage, 3);
        await click('ResultAction0');
        await until(s => s.race === 'Racing');
        assert.deepEqual(session.trace.map(s => s.phase), ['menu ready','first start','first results','next start']);
        await snapshot('goto','main');
        await until(s => s.flow === 'Menu' && s.screen === 'main');
        await click('garage'); await until(s => s.screen === 'garage');
        await page.screenshot({ path: out + '/garage.png' });
      } else {
        assert.deepEqual(session.trace.map(s => s.phase), ['menu ready','first start']);
      }
      session.lastStage = session.trace.at(-1).phase;
      console.log('FIRST SESSION', run + 1, session.lastStage, session.downloadCompleteMs, session.menuReadyMs, session.firstSteeredTurnMs);
    } finally { await context.close(); }
  }
  assert.deepEqual(report.errors, []);
} finally {
  await fs.writeFile(out + '/report.json', JSON.stringify(report, null, 2));
  await browser.close();
}
