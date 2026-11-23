// Logic-only adapter tests: no browser, GPU, network, real account or real save.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';
const source = await fs.readFile(new URL('../../Assets/WebGLTemplates/Yandex/TemplateData/cars-platform.js', import.meta.url), 'utf8');

async function fixture() {
  const requests = new Map(), timers = new Map(), storage = new Map();
  const oldWrites = [], newWrites = [];
  let resolveOld, rejectOld, timerId = 0;
  const oldWrite = new Promise((resolve, reject) => { resolveOld = resolve; rejectOld = reject; });
  const account = (id, write) => ({ getUniqueID: () => id, getName: () => id, isAuthorized: () => id !== 'guest',
    getData: () => Promise.resolve({}), setData: write });
  const previous = account('guest', data => { oldWrites.push(data.save); return oldWrite; });
  const current = account('account', data => { newWrites.push(data.save); return Promise.resolve(); });
  let player = previous;
  const sdk = { environment: { i18n: { lang: 'en' } }, deviceInfo: { isMobile: () => false, isTablet: () => false, isTV: () => false },
    on() {}, getPlayer: () => Promise.resolve(player), auth: { openAuthDialog: () => { player = current; return Promise.resolve(); } } };
  const context = { console: { log() {} }, TextEncoder, performance,
    location: { hostname: 'localhost', protocol: 'http:', search: '' }, navigator: { userAgent: 'logic test' },
    document: { hidden: false, addEventListener() {} }, addEventListener() {},
    localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value) },
    setTimeout: (fn, ms) => { timers.set(++timerId, { fn, ms }); return timerId; }, clearTimeout: id => timers.delete(id),
    YaGames: { init: () => Promise.resolve(sdk) } };
  context.window = context;
  vm.runInNewContext(source, context, { filename: 'cars-platform.js' });
  const api = context.CarsPlatform;
  await api.boot();
  api.attach({ SendMessage(object, method, value) {
    if (method !== 'OnJs') return;
    const reply = JSON.parse(value), resolve = requests.get(reply.id);
    if (resolve) { requests.delete(reply.id); resolve(reply); }
  } });
  let requestId = 0;
  const call = method => new Promise(resolve => { const id = ++requestId; requests.set(id, resolve); api[method](id); });
  return { api, call, oldWrites, newWrites, resolveOld, rejectOld };
}

const results = [];
for (const identical of [true, false]) for (const oldSuccess of [true, false]) {
  const name = `account switch / ${identical ? 'identical' : 'newer'} payload / old write ${oldSuccess ? 'success' : 'failure'}`;
  try {
    const f = await fixture();
    f.api.queueSave('local-progress');
    const first = f.call('flushSave');
    assert.deepEqual(f.oldWrites, ['local-progress']);
    assert.equal((await f.call('requestAuth')).ok, true);
    const merged = identical ? 'local-progress' : 'merged-progress';
    f.api.queueSave(merged);
    if (oldSuccess) f.resolveOld(); else f.rejectOld(new Error('old account offline'));
    assert.equal((await first).ok, oldSuccess);
    assert.equal((await f.call('flushSave')).ok, true);
    assert.deepEqual(f.newWrites, [merged], 'The new account must receive its own save even when its bytes match the previous account');
    assert.deepEqual(f.oldWrites, ['local-progress'], 'Merged account progress must never be sent to the previous account');
    results.push({ name, passed: true });
    console.log('PASS', name);
  } catch (error) {
    results.push({ name, passed: false, error: String(error) });
    console.error('FAIL', name, String(error));
  }
}
const mockSdk = await fs.readFile(new URL('../../Assets/WebGLTemplates/Yandex/TemplateData/ysdk-mock.js', import.meta.url), 'utf8');
for (const legacy of [false, true]) {
  const data = new Map([['cars.mock.lb', JSON.stringify({ best_a1: 180000 })]]);
  const context = { URLSearchParams, location: { search: legacy ? '?lb=legacy' : '' },
    localStorage: { getItem: key => data.get(key), setItem: (key, value) => data.set(key, value) } };
  context.window = context;
  vm.runInNewContext(mockSdk, context);
  const sdk = await context.CarsMockSdk.init();
  const boards = legacy ? await sdk.getLeaderboards() : sdk.leaderboards;
  const set = legacy ? boards.setLeaderboardScore : boards.setScore;
  const get = legacy ? boards.getLeaderboardEntries : boards.getEntries;
  assert.equal((await get('arena_points_v1')).entries.length, 0);
  for (const score of [210, 275, 200]) await set('arena_points_v1', score);
  assert.equal((await get('arena_points_v1')).entries[0].score, 275);
  assert.equal((await get('best_a1')).entries[0].score, 180000);
  for (const time of [65000, 61000, 63000]) await set('best_e1', time);
  assert.equal((await get('best_e1')).entries[0].score, 61000);
  results.push({ name: 'arena score and race time boards / ' + (legacy ? 'legacy' : 'v2'), passed: true });
}
if (process.env.OUT) {
  await fs.mkdir(process.env.OUT, { recursive: true });
  await fs.writeFile(`${process.env.OUT}/evidence.json`, JSON.stringify(results, null, 2));
}
assert(results.every(r => r.passed), 'Cloud adapter regression suite failed');
