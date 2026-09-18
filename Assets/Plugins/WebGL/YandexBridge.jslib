// Thin marshalling layer only. All Yandex SDK logic lives in TemplateData/cars-platform.js
// (window.CarsPlatform) so it can be debugged in DevTools and changed without an IL2CPP rebuild.
mergeInto(LibraryManager.library, {
  CarsYa_Free: function (ptr) { _free(ptr); },
  CarsYa_Available: function () { return (typeof window !== 'undefined' && window.CarsPlatform) ? 1 : 0; },
  CarsYa_Init: function (reqId) { window.CarsPlatform.init(reqId); },
  CarsYa_GetEnv: function () {
    var s = JSON.stringify(window.CarsPlatform.env());
    var size = lengthBytesUTF8(s) + 1; var buf = _malloc(size); stringToUTF8(s, buf, size); return buf;
  },
  CarsYa_LoadSave: function (reqId) { window.CarsPlatform.loadSave(reqId); },
  CarsYa_QueueSave: function (jsonPtr) { window.CarsPlatform.queueSave(UTF8ToString(jsonPtr)); },
  CarsYa_FlushSave: function (reqId) { window.CarsPlatform.flushSave(reqId); },
  CarsYa_ShowInterstitial: function (reqId) { window.CarsPlatform.interstitial(reqId); },
  CarsYa_ShowRewarded: function (reqId) { window.CarsPlatform.rewarded(reqId); },
  CarsYa_GameplayStart: function () { window.CarsPlatform.gameplayStart(); },
  CarsYa_GameplayStop: function () { window.CarsPlatform.gameplayStop(); },
  CarsYa_Ready: function () { window.CarsPlatform.ready(); },
  CarsYa_RequestAuth: function (reqId) { window.CarsPlatform.requestAuth(reqId); },
  CarsYa_SubmitScore: function (reqId, boardPtr, score, extraPtr) { window.CarsPlatform.submitScore(reqId, UTF8ToString(boardPtr), Number(score), UTF8ToString(extraPtr)); },
  CarsYa_GetLeaderboard: function (reqId, boardPtr, top, includeUser, around) { window.CarsPlatform.getLeaderboard(reqId, UTF8ToString(boardPtr), top, includeUser, around); },
  CarsYa_SessionsLoad: function (reqId, count, metaPtr) { window.CarsPlatform.sessionsLoad(reqId, count, UTF8ToString(metaPtr)); },
  CarsYa_SessionsCommit: function (payloadPtr) { window.CarsPlatform.sessionsCommit(UTF8ToString(payloadPtr)); },
  CarsYa_SessionsPush: function (reqId, metaPtr) { window.CarsPlatform.sessionsPush(reqId, UTF8ToString(metaPtr)); },
  CarsYa_Debug: function (jsonPtr) { if (window.CarsPlatform && window.CarsPlatform.debugState) window.CarsPlatform.debugState(UTF8ToString(jsonPtr)); }
});
