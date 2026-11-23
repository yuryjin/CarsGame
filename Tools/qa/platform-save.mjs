// Logic-only adapter tests: no browser, GPU, network, real account or real save.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import vm from 'node:vm';
const source = await fs.readFile(new URL('../../Assets/WebGLTemplates/Yandex/TemplateData/cars-platform.js', import.meta.url), 'utf8');

async function fixture({ realTimers = false } = {}) {
  const requests = new Map(), timers = new Map(), storage = new Map();
  const oldWrites = [], newWrites = [];
  let resolveOld, rejectOld, timerId = 0;
  const oldWrite = new Promise((resolve, reject) => { resolveOld = resolve; rejectOld = reject; });
  const account = (id, write) => ({ getUniqueID: () => id, getName: () => id, isAuthorized: () => id !== 'guest',
    getData: () => Promise.resolve({}), setData: write });
  const previous = account('guest', data => { oldWrites.push(data.save); return oldWrite; });
  const current = account('account', data => { newWrites.push(data.save); return Promise.resolve(); });
  let player = previous, nextAccount = current;
  const sdk = { environment: { i18n: { lang: 'en' } }, deviceInfo: { isMobile: () => false, isTablet: () => false, isTV: () => false },
    on() {}, getPlayer: () => Promise.resolve(player), auth: { openAuthDialog: () => { player = nextAccount; return Promise.resolve(); } } };
  const context = { console: { log() {} }, TextEncoder, performance,
    location: { hostname: 'localhost', protocol: 'http:', search: '' }, navigator: { userAgent: 'logic test' },
    document: { hidden: false, addEventListener() {} }, addEventListener() {},
    localStorage: { getItem: key => storage.get(key) ?? null, setItem: (key, value) => storage.set(key, value) },
    setTimeout: (fn, ms) => { timers.set(++timerId, { fn, ms }); return timerId; }, clearTimeout: id => timers.delete(id),
    YaGames: { init: () => Promise.resolve(sdk) } };
  if (realTimers) Object.assign(context, { setTimeout, clearTimeout });
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
  const call = (method, ...args) => new Promise(resolve => { const id = ++requestId; requests.set(id, resolve); api[method](id, ...args); });
  return { api, call, sdk, oldWrites, newWrites, resolveOld, rejectOld,
    fireTimer(ms) {
      const entry = [...timers].find(([, timer]) => timer.ms === ms);
      assert(entry, 'missing timer of ' + ms + ' ms');
      timers.delete(entry[0]);
      entry[1].fn();
    },
    chooseAccount(id) { nextAccount = account(id, () => Promise.resolve()); } };
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
for (const legacy of [false, true]) {
  const name = 'Yandex one write per second across boards and accounts / ' + (legacy ? 'legacy' : 'v2');
  const attempts = [], intervalsMs = [], accepted = [];
  try {
    const f = await fixture({ realTimers: true });
    let lastAttempt = -Infinity, failOnce = false;
    const setScore = (board, score) => {
      const now = performance.now();
      const elapsed = now - lastAttempt;
      if (Number.isFinite(elapsed)) intervalsMs.push(Number(elapsed.toFixed(3)));
      lastAttempt = now;
      const write = { account: f.api.env().playerId, board, score };
      attempts.push(write);
      if (!f.api.env().authorized) return Promise.reject(new Error('unauthorized'));
      if (elapsed < 1000) return Promise.reject(new Error('SDK rate limit'));
      if (failOnce) { failOnce = false; return Promise.reject(new Error('offline')); }
      accepted.push(write);
      return Promise.resolve();
    };
    if (legacy) f.sdk.getLeaderboards = async () => ({ setLeaderboardScore: setScore });
    else f.sdk.leaderboards = { setScore };
    f.sdk.isAvailableMethod = async () => f.api.env().authorized;
    await f.call('requestAuth');
    const submit = (board, score) => f.call('submitScore', board, score, '');

    assert.equal((await submit('best_e1', 65000)).ok, true);
    let secondReplied = false;
    const second = submit('best_e2', 61000).then(reply => { secondReplied = true; return reply; });
    f.api.queueSave('race coins and best time');
    assert.equal((await f.call('flushSave')).ok, true);
    assert.deepEqual(f.newWrites, ['race coins and best time']);
    const savedBeforeSecond = !secondReplied;
    assert.equal((await second).ok, true, 'the second board must reach the SDK after its limit resets');
    assert(savedBeforeSecond, 'the save must be confirmed while the second score waits');
    assert.deepEqual(accepted.map(x => x.board), ['best_e1', 'best_e2']);

    const count = attempts.length;
    assert.equal((await submit('best_e1', 65000)).ok, true);
    assert.equal(attempts.length, count, 'the account already confirmed that value');
    assert.equal((await submit('best_e1', 64000)).ok, true, 'an improved time must reach the board');
    failOnce = true;
    assert.equal((await submit('arena_points_v1', 210)).ok, false);
    assert.equal((await submit('arena_points_v1', 210)).ok, true, 'a rejected score must remain retryable');
    assert.equal((await submit('arena_points_v1', 275)).ok, true, 'an improved point score must reach the board');

    const stale = submit('old_profile_board', 42000);
    f.chooseAccount('second-account');
    assert.equal((await f.call('requestAuth')).ok, true);
    assert.equal((await stale).ok, false, 'a previous account must not submit after a switch');
    assert.equal((await submit('best_e1', 64000)).ok, true, 'the new account needs its own confirmation');
    assert.deepEqual(accepted.filter(x => x.account === 'second-account'),
      [{ account: 'second-account', board: 'best_e1', score: 64000 }]);
    assert(intervalsMs.every(ms => ms >= 1000));
    results.push({ name, passed: true, attempts, accepted, intervalsMs });
    console.log('PASS', name, intervalsMs.join(', '), 'ms');
  } catch (error) {
    results.push({ name, passed: false, error: String(error), attempts, accepted, intervalsMs });
    console.error('FAIL', name, String(error), intervalsMs.join(', '), 'ms');
  }
}
{
  const f = await fixture();
  const names = [];
  let entered, unblock;
  const stalled = new Promise(resolve => { entered = resolve; });
  f.sdk.leaderboards = { setScore: async board => { names.push(board); } };
  f.sdk.isAvailableMethod = () => { entered(); return new Promise(resolve => { unblock = resolve; }); };
  assert.equal((await f.call('submitScore', 'guest', 42, '')).ok, false);
  await f.call('requestAuth');
  const previous = f.call('submitScore', 'stalled', 42, '');
  await stalled;
  f.fireTimer(12000);
  assert.equal((await previous).ok, false);
  f.sdk.isAvailableMethod = async () => true;
  assert.equal((await f.call('submitScore', 'fresh', 17, '')).ok, true);
  unblock(true);
  await Promise.resolve();
  await Promise.resolve();
  assert.deepEqual(names, ['fresh'], 'a late reply to a timed-out request must not send a stale score');
  results.push({ name: 'guest access and a stuck SDK availability check', passed: true, written: names });
}
{
  const f = await fixture({ realTimers: true });
  await f.call('requestAuth');
  const writes = [];
  let entered, acknowledge;
  const inFlight = new Promise(resolve => { entered = resolve; });
  f.sdk.leaderboards = { setScore(board, score) {
    writes.push({ player: f.api.env().playerId, board, score });
    if (writes.length === 1) { entered(); return new Promise(resolve => { acknowledge = resolve; }); }
    return Promise.resolve();
  } };
  const submit = () => f.call('submitScore', 'best_e1', 61000, '');
  const old = submit();
  await inFlight;
  f.chooseAccount('second-account');
  await f.call('requestAuth');
  acknowledge();
  assert.equal((await old).ok, false);
  const same = await Promise.all([submit(), submit()]);
  assert(same.every(reply => reply.ok));
  assert.deepEqual(writes.map(x => x.player), ['account', 'second-account']);
  results.push({ name: 'late former-account success and simultaneous identical submissions', passed: true, writes });
}
if (process.env.OUT) {
  await fs.mkdir(process.env.OUT, { recursive: true });
  await fs.writeFile(`${process.env.OUT}/evidence.json`, JSON.stringify(results, null, 2));
}
assert(results.every(r => r.passed), 'Cloud adapter regression suite failed');
