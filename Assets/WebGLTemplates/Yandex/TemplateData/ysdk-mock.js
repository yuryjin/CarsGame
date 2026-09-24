/* Local stand-in for /sdk.js: same surface the game uses, backed by localStorage. Never ship as sdk.js. */
(function () {
  'use strict';
  var params = new URLSearchParams(location.search);
  var handlers = {};
  var data = {};
  try { data = JSON.parse(localStorage.getItem('cars.mock.playerdata') || '{}'); } catch (e) {}
  function persist() { try { localStorage.setItem('cars.mock.playerdata', JSON.stringify(data)); } catch (e) {} }
  var player = {
    getUniqueID: function () { return 'mock-user'; },
    getName: function () { return 'Mock Player'; },
    getPhoto: function () { return ''; },
    isAuthorized: function () { return params.get('auth') === '1'; },
    getData: function (keys) { var out = {}; (keys || Object.keys(data)).forEach(function (k) { if (k in data) out[k] = data[k]; }); return Promise.resolve(out); },
    setData: function (obj) { Object.keys(obj).forEach(function (k) { data[k] = obj[k]; }); persist(); return Promise.resolve(); }
  };
  // Leaderboards: v2 (ysdk.leaderboards) unless ?lb=legacy, so both code paths can be exercised.
  var boards = {}; try { boards = JSON.parse(localStorage.getItem('cars.mock.lb') || '{}'); } catch (e) {}
  // Boards are ascending (times) except 'daily', which is created DESCENDING in the console (day-encoded score).
  function mockSetScore(b, s) { var desc = b === 'daily'; if (!(b in boards) || (desc ? s > boards[b] : s < boards[b])) boards[b] = s; try { localStorage.setItem('cars.mock.lb', JSON.stringify(boards)); } catch (e) {} return Promise.resolve(); }
  function mockGetEntries(b) { return Promise.resolve({ entries: b in boards ? [{ rank: 1, score: boards[b], player: { uniqueID: 'mock-user', publicName: 'Mock Player' } }] : [] }); }
  var sdk = {
    environment: { i18n: { lang: params.get('lang') || 'ru' }, app: { id: 'mock' } },
    deviceInfo: { isMobile: function () { return params.get('device') === 'mobile'; }, isTablet: function () { return false; }, isTV: function () { return params.get('device') === 'tv'; } },
    on: function (ev, fn) { handlers[ev] = fn; },
    off: function (ev) { delete handlers[ev]; },
    features: { LoadingAPI: { ready: function () { console.log('[mock] LoadingAPI.ready'); } }, GameplayAPI: { start: function () { console.log('[mock] gameplay start'); }, stop: function () { console.log('[mock] gameplay stop'); } } },
    adv: {
      showFullscreenAdv: function (o) { var c = o.callbacks || {}; setTimeout(function () { c.onOpen && c.onOpen(); }, 100); setTimeout(function () { c.onClose && c.onClose(); }, 1600); },
      showRewardedVideo: function (o) { var c = o.callbacks || {}; setTimeout(function () { c.onOpen && c.onOpen(); }, 100); setTimeout(function () { c.onRewarded && c.onRewarded(); }, 1200); setTimeout(function () { c.onClose && c.onClose(); }, 1800); }
    },
    auth: { openAuthDialog: function () { params.set('auth', '1'); return Promise.resolve(); } },
    getPlayer: function () { return Promise.resolve(player); },
    serverTime: function () { return Date.now() + (Number(params.get('dayshift') || 0) * 86400000); },
    getLeaderboards: function () { return Promise.resolve({ setLeaderboardScore: mockSetScore, getLeaderboardEntries: mockGetEntries }); },
    leaderboards: params.get('lb') === 'legacy' ? undefined : { setScore: mockSetScore, getEntries: mockGetEntries },
    multiplayer: { sessions: { init: function () { return Promise.resolve([]); }, commit: function () {}, push: function () {} } }
  };
  // Simulate the host pause from the DevTools console: CarsMockSdk.fire('game_api_pause')
  sdk.fire = function (ev) { handlers[ev] && handlers[ev](); };
  window.CarsMockSdk = { init: function () { return Promise.resolve(sdk); }, fire: sdk.fire };
})();
