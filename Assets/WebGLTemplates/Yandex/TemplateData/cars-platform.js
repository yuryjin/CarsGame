/*
 * CarsPlatform — all Yandex Games SDK logic for the Unity build (port of the tggames factory
 * yandex.js adapter). The jslib only forwards calls here; replies go back to C# through
 * unityInstance.SendMessage('PlatformRuntime', 'OnJs', JSON) with the request id.
 *
 * Invariants (moderation checks them):
 *  - sdk init + game_api_pause/resume subscription happen BEFORE Unity boots; host pause state
 *    is buffered and replayed at attach(), so a startup ad never leaves the game running.
 *  - GameplayAPI.start/stop are balanced (inGameplay guard); ads stop gameplay on onOpen and
 *    restore the previous state on close/error.
 *  - Every request resolves exactly once (settled flag + JS timeout); C# has an outer timeout too.
 *  - setData is coalesced (≥ 900 ms) with a localStorage mirror; hard limit 200 KB / 100 per 5 min.
 */
(function () {
  'use strict';
  var ysdk = null, player = null, lb = null, unity = null, sessions = null;
  var lang = '', device = 'desktop', authorized = false, playerId = 'guest', playerName = 'Guest', avatar = '';
  var inGameplay = false, hostPaused = false, adOpen = false, readyCalled = false;
  var saveCache = null, saveTimer = null, savePending = false, lastSaveAt = 0;
  var SAVE_KEY = 'cars.save.mirror';
  var debug = { gameplayStarts: 0, gameplayStops: 0, adOpens: 0, adCloses: 0, setDataCalls: 0, log: [], state: null, perf: null, bootMs: {} };
  // The mock is a developer tool only: never on a real host. In production a missing or failing SDK
  // keeps the loading screen up with a retry, because gameplay must not start without the SDK.
  var devHost = /^(localhost|127\.0\.0\.1|0\.0\.0\.0|\[::1\])$/.test(location.hostname) || location.protocol === 'file:';
  var mock = devHost && (!!window.__ysdkMissing || /[?&]sdk=mock/.test(location.search));
  var saveQueue = null, saveInFlight = null, saveTimes = [];    // rolling setData quota: 100 per 5 minutes; saveInFlight = drain promise
  var SAVE_QUOTA = 90, SAVE_WINDOW = 5 * 60 * 1000;

  function log(m) { try { console.log('[CarsPlatform] ' + m); debug.log.push(m); if (debug.log.length > 200) debug.log.shift(); } catch (e) {} }
  function send(method, payload) {
    if (!unity) { queued.push([method, payload]); return; }
    try { unity.SendMessage('PlatformRuntime', method, payload); } catch (e) { log('SendMessage failed: ' + e); }
  }
  var queued = [];
  function reply(id, obj) { obj.id = id; send('OnJs', JSON.stringify(obj)); }
  function once(id) { var done = false; return function (obj) { if (done) return; done = true; reply(id, obj); }; }
  function lsGet(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
  function lsSet(k, v) { try { localStorage.setItem(k, v); } catch (e) {} }

  function detectDevice() {
    try {
      var t = ysdk.deviceInfo;
      if (t.isTV && t.isTV()) return 'tv';
      if (t.isTablet && t.isTablet()) return 'tablet';
      if (t.isMobile && t.isMobile()) return 'mobile';
    } catch (e) {}
    return /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) ? 'mobile' : 'desktop';
  }

  function setHostPaused(p) {
    hostPaused = !!p;
    send('OnHostPause', hostPaused ? '1' : '0');
  }

  // ---- boot: SDK (or mock) before Unity ----
  function showRetry(reason) {
    log('boot blocked: ' + reason);
    var el = document.getElementById('cars-retry');
    if (!el) {
      el = document.createElement('div'); el.id = 'cars-retry';
      el.innerHTML = '<div>Не удалось загрузить игру.<br><button id="cars-retry-btn">Повторить</button></div>';
      document.body.appendChild(el);
      document.getElementById('cars-retry-btn').onclick = function () { location.reload(); };
    }
    el.style.display = 'flex';
  }

  function boot() {
    return new Promise(function (resolve, reject) {
      var finished = false;
      function finish() { if (!finished) { finished = true; resolve(); } }
      function fail(reason) { if (!finished) { finished = true; showRetry(reason); reject(new Error(reason)); } }
      var timer = setTimeout(function () { fail('init timeout'); }, 20000);
      function useMock() {
        if (window.CarsMockSdk) { window.YaGames = window.CarsMockSdk; realInit(); return; }
        var s = document.createElement('script');
        s.src = 'TemplateData/ysdk-mock.js';
        s.onload = function () { window.YaGames = window.CarsMockSdk; realInit(); };
        s.onerror = function () { clearTimeout(timer); fail('mock sdk missing'); };
        document.head.appendChild(s);
      }
      function realInit() {
        if (typeof window.YaGames === 'undefined') { if (mock) { useMock(); } else { clearTimeout(timer); fail('sdk.js missing'); } return; }
        window.YaGames.init().then(function (sdk) {
          ysdk = sdk;
          try { lang = (sdk.environment.i18n.lang || '').toLowerCase().slice(0, 2); } catch (e) {}
          device = detectDevice();
          sdk.on('game_api_pause', function () { log('game_api_pause'); setHostPaused(true); });
          sdk.on('game_api_resume', function () { log('game_api_resume'); setHostPaused(false); });
          // Background tab: pause the race and mute (moderation rule), and flush the save while we still can.
          document.addEventListener('visibilitychange', function () { send('OnHidden', document.hidden ? '1' : '0'); if (document.hidden) flushNow(); });
          window.addEventListener('pagehide', flushNow);
          return sdk.getPlayer({ scopes: false }).then(function (p) {
            player = p;
            try { authorized = typeof p.isAuthorized === 'function' ? p.isAuthorized() : (p.getMode && p.getMode() !== 'lite'); } catch (e) {}
            try { playerId = p.getUniqueID() || 'guest'; } catch (e) {}
            try { playerName = p.getName() || 'Guest'; avatar = p.getPhoto ? p.getPhoto('small') : ''; } catch (e) {}
          });
        }).then(function () { clearTimeout(timer); log('sdk ready lang=' + lang + ' device=' + device + ' auth=' + authorized + (mock ? ' (mock)' : '')); finish(); })
          .catch(function (e) { clearTimeout(timer); fail('init failed: ' + e); });
      }
      if (mock) useMock(); else realInit();
    });
  }

  function attach(instance) {
    unity = instance;
    debug.bootMs.attach = Math.round(performance.now());
    while (queued.length) { var q = queued.shift(); send(q[0], q[1]); }
    if (hostPaused) send('OnHostPause', '1');   // replay a startup pause that arrived before Unity was up
    if (document.hidden) send('OnHidden', '1');   // backgrounded during loading: start paused and muted
    log('attached');
    // Dev/QA only: ?perf=1 turns on the in-game frame statistics (see DebugBus.cs).
    if (debugEnabled && /[?&]perf=1/.test(location.search)) cmd('perf', 'on');
  }

  // ---- QA / debug channel (local hosts or ?debug=1 only) ----
  // cmd('startRace','e1','easy'), cmd('autopilot'), cmd('goto','garage'), cmd('buy','engine'), cmd('touch','on'),
  // cmd('perf','on'), cmd('state'). The game answers through debugState(); read window.CarsPlatform.debug.state.
  var debugEnabled = devHost || /[?&]debug=1/.test(location.search);
  function cmd(name) {
    if (!debugEnabled) return false;
    var args = Array.prototype.slice.call(arguments, 1).map(String);
    send('OnJs', JSON.stringify({ cmd: String(name), args: args }));
    return true;
  }
  // Size of the wasm heap, read from the instance attach() was given (the page has no other handle on it).
  // null means "unknown" so a memory check cannot silently pass on a zero.
  function heapBytes() {
    try {
      var m = unity && unity.Module;
      if (m && m.HEAPU8 && typeof m.HEAPU8.length === 'number') return m.HEAPU8.length;
      if (m && m.asm && m.asm.memory && m.asm.memory.buffer) return m.asm.memory.buffer.byteLength;
    } catch (e) {}
    return null;
  }

  function debugState(json) {
    try {
      var st = JSON.parse(json);
      if (st.perf) { st.perf.heapBytes = heapBytes(); debug.perf = st.perf; }
      debug.state = st;
    } catch (e) { log('bad debug state ' + e); }
  }

  function env() {
    return { lang: lang, device: device, authorized: authorized, playerId: playerId, playerName: playerName, avatar: avatar, adv: !!(ysdk && ysdk.adv), debug: debugEnabled, mock: mock };
  }

  function init(id) {
    var e = env(); e.ok = !!ysdk; if (!ysdk) e.error = 'no sdk';
    reply(id, e);
  }

  // ---- gameplay markers ----
  function gameplayStart() {
    if (inGameplay || adOpen || hostPaused) return;
    inGameplay = true; debug.gameplayStarts++;
    try { ysdk && ysdk.features && ysdk.features.GameplayAPI && ysdk.features.GameplayAPI.start(); } catch (e) {}
  }
  function gameplayStop() {
    if (!inGameplay) return;
    inGameplay = false; debug.gameplayStops++;
    try { ysdk && ysdk.features && ysdk.features.GameplayAPI && ysdk.features.GameplayAPI.stop(); } catch (e) {}
  }
  function ready() {
    if (readyCalled) return; readyCalled = true;
    debug.bootMs.ready = Math.round(performance.now());
    try { ysdk && ysdk.features && ysdk.features.LoadingAPI && ysdk.features.LoadingAPI.ready(); } catch (e) {}
    log('LoadingAPI.ready()');
  }

  // ---- ads ----
  function interstitial(id) {
    var done = once(id);
    if (!ysdk || !ysdk.adv) { done({ ok: false, error: 'unavailable' }); return; }
    var resume = inGameplay, opened = false, terminal = false, watchdog = null;
    // No pre-open timer: a late onOpen after a synthetic failure would pause the game with no owner.
    // The request stays pending until a terminal SDK callback (C# bounds it at 120 s); once the ad
    // has opened, a watchdog guarantees the pause is lifted even if the SDK never calls back.
    function close(ok, err) {
      if (terminal) return; terminal = true;
      clearTimeout(watchdog);
      if (opened) { adOpen = false; debug.adCloses++; send('OnAdClose', '1'); }
      if (resume) gameplayStart();
      done(ok ? { ok: true } : { ok: false, error: err || 'error' });
    }
    try {
      ysdk.adv.showFullscreenAdv({ callbacks: {
        onOpen: function () { if (terminal) return; opened = true; adOpen = true; debug.adOpens++; gameplayStop(); send('OnAdOpen', '1'); watchdog = setTimeout(function () { log('interstitial watchdog'); close(true); }, 90000); },
        onClose: function () { close(true); },
        onError: function (e) { log('interstitial error ' + e); close(false, 'error'); },
        onOffline: function () { close(false, 'unavailable'); }
      } });
    } catch (e) { close(false, 'error'); }
  }

  function rewarded(id) {
    var done = once(id);
    if (!ysdk || !ysdk.adv) { done({ ok: false, error: 'unavailable' }); return; }
    var resume = inGameplay, opened = false, got = false, terminal = false, watchdog = null;
    function close(ok, err) {
      if (terminal) return; terminal = true;
      clearTimeout(watchdog);
      if (opened) { adOpen = false; debug.adCloses++; send('OnAdClose', '1'); }
      if (resume) gameplayStart();
      done(ok ? { ok: true, rewarded: got } : { ok: false, error: err || 'error' });
    }
    try {
      ysdk.adv.showRewardedVideo({ callbacks: {
        onOpen: function () { if (terminal) return; opened = true; adOpen = true; debug.adOpens++; gameplayStop(); send('OnAdOpen', '1'); watchdog = setTimeout(function () { log('rewarded watchdog'); close(true); }, 150000); },
        onRewarded: function () { if (!terminal) got = true; },
        onClose: function () { close(true); },
        onError: function (e) { log('rewarded error ' + e); close(false, 'error'); }
      } });
    } catch (e) { close(false, 'error'); }
  }

  // ---- cloud save: honest load, one serialized writer, pending data kept until confirmed ----
  function loadSave(id) {
    var done = once(id);
    var mirror = lsGet(SAVE_KEY) || '';
    if (!player || !player.getData) { done({ ok: false, error: 'no player' }); return; }
    var t = setTimeout(function () { done({ ok: false, error: 'timeout' }); }, 8000);
    player.getData(['save']).then(function (d) {
      clearTimeout(t);
      var cloud = d && d.save ? d.save : '';
      // Cloud is the source of truth; the mirror only covers a brand-new player with no cloud row.
      done({ ok: true, data: cloud || (cloud === '' && !d ? mirror : cloud) });
    }).catch(function (e) { clearTimeout(t); log('getData failed ' + e); done({ ok: false, error: 'error' }); });
  }
  function byteLength(str) { try { return new TextEncoder().encode(str).length; } catch (e) { return str.length * 2; } }
  function quotaAvailable() {
    var now = Date.now();
    while (saveTimes.length && now - saveTimes[0] > SAVE_WINDOW) saveTimes.shift();
    return saveTimes.length < SAVE_QUOTA;
  }
  function queueSave(json) {
    saveQueue = json; lsSet(SAVE_KEY, json);
    scheduleFlush(Math.max(900, 3000 - (Date.now() - lastSaveAt)));
  }
  // One drain loop: callers during an in-flight write join it; after every write the loop
  // immediately drains anything newer that arrived meanwhile, so the latest payload is never stranded.
  function flushNow() {
    if (saveInFlight) return saveInFlight;
    if (saveQueue === null) return Promise.resolve(true);
    if (!player || !player.setData) return Promise.resolve(false);
    saveInFlight = drainOnce().then(function (ok) {
      saveInFlight = null;
      if (ok && saveQueue !== null) return flushNow();       // newer data arrived during the write
      return ok;
    });
    return saveInFlight;
  }
  function drainOnce() {
    var payload = saveQueue;
    var size = byteLength(JSON.stringify({ save: payload }));
    if (size > 190000) { log('save too large: ' + size + ' bytes'); return Promise.resolve(false); }
    if (!quotaAvailable()) { log('setData quota reached, deferring'); scheduleFlush(15000); return Promise.resolve(false); }
    lastSaveAt = Date.now(); saveTimes.push(lastSaveAt); debug.setDataCalls++;
    var p;
    try { p = player.setData({ save: payload }, true); } catch (e) { p = Promise.reject(e); }
    return Promise.resolve(p).then(function () {
      if (saveQueue === payload) saveQueue = null;          // newer data may have arrived meanwhile: keep it queued
      return true;
    }).catch(function (e) { log('setData failed ' + e); scheduleFlush(5000); return false; });
  }
  function scheduleFlush(ms) { if (saveTimer) return; saveTimer = setTimeout(function () { saveTimer = null; flushNow(); }, ms); }
  function flushSave(id) {
    var done = once(id);
    var t = setTimeout(function () { done({ ok: false, error: 'timeout' }); }, 8000);
    flushNow().then(function (ok) { clearTimeout(t); done(ok ? { ok: true } : { ok: false, error: 'error' }); });
  }

  // ---- auth ----
  function requestAuth(id) {
    var done = once(id);
    if (!ysdk || !ysdk.auth) { done({ ok: false, error: 'unavailable' }); return; }
    ysdk.auth.openAuthDialog().then(function () {
      return ysdk.getPlayer({ scopes: false }).then(function (p) {
        // New identity: the guest's queued blob must never land in the signed-in account.
        saveQueue = null; if (saveTimer) { clearTimeout(saveTimer); saveTimer = null; }
        player = p; authorized = true;
        try { playerId = p.getUniqueID(); playerName = p.getName() || playerName; } catch (e) {}
        var e = env(); e.ok = true; done(e);
      });
    }).catch(function (e) { done({ ok: false, error: 'declined' }); });
  }

  // ---- leaderboards ----
  // Current SDK: ysdk.leaderboards.setScore/getEntries. Older SDK builds only have the deprecated
  // ysdk.getLeaderboards() object with setLeaderboardScore/getLeaderboardEntries — kept as a fallback.
  function getLb() {
    if (lb) return Promise.resolve(lb);
    if (ysdk.leaderboards && typeof ysdk.leaderboards.setScore === 'function') {
      var v2 = ysdk.leaderboards;
      lb = { setLeaderboardScore: function (b, s, x) { return v2.setScore(b, s, x); }, getLeaderboardEntries: function (b, o) { return v2.getEntries(b, o); }, v2: true };
      return Promise.resolve(lb);
    }
    return ysdk.getLeaderboards().then(function (l) { lb = l; return l; });
  }
  function submitScore(id, board, score, extra) {
    var done = once(id);
    if (!ysdk) { done({ ok: false, error: 'unavailable' }); return; }
    getLb().then(function (l) { debug.lbApi = l.v2 ? 'v2' : 'legacy'; return l.setLeaderboardScore(board, Number(score), extra || undefined); })
      .then(function () { done({ ok: true }); }).catch(function (e) { done({ ok: false, error: String(e) }); });
  }
  function getLeaderboard(id, board, top, includeUser, around) {
    var done = once(id);
    if (!ysdk) { done({ ok: false, error: 'unavailable' }); return; }
    getLb().then(function (l) { debug.lbApi = l.v2 ? 'v2' : 'legacy'; return l.getLeaderboardEntries(board, { quantityTop: top, includeUser: !!includeUser, quantityAround: around }); })
      .then(function (res) {
        var items = (res.entries || []).map(function (e) { return { rank: e.rank, id: e.player && e.player.uniqueID, name: e.player && e.player.publicName, score: e.score, extra: e.extraData || '' }; });
        done({ ok: true, entries: JSON.stringify(items) });
      }).catch(function (e) { done({ ok: false, error: String(e) }); });
  }

  // ---- async multiplayer sessions (ghosts) ----
  // Yandex filters sessions by numeric meta1..meta3; opponents come back as {player, timeline:[{payload,...}]}.
  // Each opponent is normalised to {ghost, name} where ghost is the latest committed payload's "d".
  function sessionsLoad(id, count, meta) {
    var done = once(id);
    if (!ysdk || !ysdk.multiplayer || !ysdk.multiplayer.sessions) { done({ ok: false, error: 'unavailable' }); return; }
    var opts = { count: count, isEventBased: false };
    try {
      var m = meta ? JSON.parse(meta) : {};
      if (typeof m.meta1 === 'number') opts.meta = { meta1: { min: m.meta1, max: m.meta1 } };
    } catch (e) {}
    ysdk.multiplayer.sessions.init(opts).then(function (opps) {
      sessions = ysdk.multiplayer.sessions;
      var items = (opps || []).map(function (o) {
        try {
          var tl = o.timeline || o.events || [];
          var last = null;
          for (var i = tl.length - 1; i >= 0 && !last; i--) { var pl = tl[i] && (tl[i].payload || tl[i].data); if (pl && pl.d) last = pl.d; }
          var name = (o.player && (o.player.name || o.player.publicName)) || (o.meta && o.meta.name) || '';
          return JSON.stringify({ ghost: last || '', name: name });
        } catch (e) { return ''; }
      });
      done({ ok: true, sessions: JSON.stringify(items) });
    }).catch(function (e) { done({ ok: false, error: String(e) }); });
  }
  function sessionsCommit(payload) { try { sessions && sessions.commit({ d: payload }); } catch (e) {} }
  function sessionsPush(id, meta) {
    var done = once(id);
    if (!sessions) { done({ ok: false, error: 'unavailable' }); return; }
    var m = {}; try { if (meta) m = JSON.parse(meta); } catch (e) {}
    var numeric = {};
    ['meta1', 'meta2', 'meta3'].forEach(function (k) { if (typeof m[k] === 'number') numeric[k] = Math.round(m[k]); });
    if (Object.keys(numeric).length === 0) numeric.meta1 = 0;
    try { sessions.push(numeric); done({ ok: true }); } catch (e) { done({ ok: false, error: String(e) }); }
  }

  window.CarsPlatform = {
    boot: boot, attach: attach, env: env, init: init, debug: debug, cmd: cmd, debugState: debugState, heapBytes: heapBytes,
    gameplayStart: gameplayStart, gameplayStop: gameplayStop, ready: ready,
    interstitial: interstitial, rewarded: rewarded,
    loadSave: loadSave, queueSave: queueSave, flushSave: flushSave,
    requestAuth: requestAuth, submitScore: submitScore, getLeaderboard: getLeaderboard,
    sessionsLoad: sessionsLoad, sessionsCommit: sessionsCommit, sessionsPush: sessionsPush
  };
})();
