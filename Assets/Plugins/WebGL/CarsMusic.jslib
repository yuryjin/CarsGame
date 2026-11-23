// Music for the WebGL build (MusicOutputs.cs → WebMusicOutput). Tracks are streamed through <audio>
// elements instead of being decoded into memory like AudioClips; each element runs through a Web Audio
// gain (element.volume is read-only on iOS) so fades and ducking work everywhere.
//  - Two music channels crossfade; a third plays stings. Loops use element.loop.
//  - Pauses on its own for a hidden tab (Unity stops running there) and when C# says so (ads, host
//    overlay). Autoplay blocked until a gesture: the first pointer/key/touch unlocks and resumes.
//  - window.CarsMusic is the state, for DevTools and the QA harness (CarsMusic.debug).
var CarsMusicLib = {
  $CarsMusic: {
    s: null,
    get: function () {
      if (CarsMusic.s) return CarsMusic.s;
      var s = { ctx: null, bus: null, duck: null, chans: [], cur: -1, sting: null, cache: {},
                volume: 0, paused: false, hidden: !!document.hidden, locked: false,
                debug: { plays: 0, blocked: 0, errors: 0, track: '', sting: '' } };
      try {
        var AC = window.AudioContext || window.webkitAudioContext;
        if (AC) {
          s.ctx = new AC();
          s.bus = s.ctx.createGain(); s.bus.gain.value = 0;
          s.duck = s.ctx.createGain(); s.duck.gain.value = 1;
          s.duck.connect(s.bus); s.bus.connect(s.ctx.destination);
        }
      } catch (e) { s.ctx = null; }
      CarsMusic.s = s;
      s.chans = [CarsMusic.chan(), CarsMusic.chan()];
      s.sting = CarsMusic.chan();
      s.finalLap = CarsMusic.chan();
      s.sting.el.addEventListener('ended', function () { s.sting.active = false; });
      document.addEventListener('visibilitychange', function () { s.hidden = !!document.hidden; CarsMusic.sync(); });
      var unlock = function () {
        if (s.ctx && s.ctx.state !== 'running' && !s.paused && !s.hidden) { try { s.ctx.resume(); } catch (e) {} }
        if (s.locked) { s.locked = false; CarsMusic.sync(); }
      };
      ['pointerdown', 'keydown', 'touchend'].forEach(function (ev) { document.addEventListener(ev, unlock, true); });
      window.CarsMusic = s;
      return s;
    },
    chan: function () {
      var s = CarsMusic.s, el = new Audio();
      el.preload = 'auto';
      var c = { el: el, gain: null, level: 0, active: false, timer: null, url: '' };
      if (s.ctx) {
        try { var src = s.ctx.createMediaElementSource(el); c.gain = s.ctx.createGain(); c.gain.gain.value = 0; src.connect(c.gain); c.gain.connect(s.duck); }
        catch (e) { c.gain = null; }
      }
      el.addEventListener('error', function () { s.debug.errors++; });
      return c;
    },
    // Ramp one channel's own gain; without Web Audio fall back to element.volume (no fade).
    // from (optional) pins the start value, e.g. 0 for a channel that starts a new track.
    ramp: function (c, to, sec, from) {
      var s = CarsMusic.s;
      c.level = to;
      if (c.gain) {
        var t = s.ctx.currentTime, g = c.gain.gain;
        g.cancelScheduledValues(t); g.setValueAtTime(from === undefined ? g.value : from, t); g.linearRampToValueAtTime(to, t + Math.max(0.01, sec));
      } else {
        c.el.volume = Math.max(0, Math.min(1, to * s.volume));
      }
    },
    start: function (c) {
      if (c.timer) { clearTimeout(c.timer); c.timer = null; }
      c.active = true;
      CarsMusic.sync();
    },
    fadeOut: function (c, sec) {
      if (!c.active) return;
      CarsMusic.ramp(c, 0, sec);
      if (c.timer) clearTimeout(c.timer);
      c.timer = setTimeout(function () { c.timer = null; c.active = false; CarsMusic.sync(); }, Math.max(10, sec * 1000 + 50));
    },
    playEl: function (c) {
      var s = CarsMusic.s, p;
      try { p = c.el.play(); } catch (e) { p = null; }
      if (p && p.catch) p.catch(function (e) {
        if (e && e.name === 'NotAllowedError') { s.locked = true; s.debug.blocked++; }
        else if (e && e.name !== 'AbortError') s.debug.errors++;
      });
    },
    // Make every element match its channel state and the pause/hidden/locked flags.
    sync: function () {
      var s = CarsMusic.s, halt = s.paused || s.hidden;
      if (s.ctx) {
        try { if (halt && s.ctx.state === 'running') s.ctx.suspend(); else if (!halt && s.ctx.state !== 'running') s.ctx.resume(); } catch (e) {}
      }
      var all = s.chans.concat([s.sting, s.finalLap]);
      for (var i = 0; i < all.length; i++) {
        var c = all[i], run = c.active && !halt;
        if (run && c.el.paused && !s.locked) CarsMusic.playEl(c);
        else if (!run && !c.el.paused) c.el.pause();
      }
    },
    load: function (c, url, loop) {
      if (c.url !== url) { c.url = url; c.el.src = url; }
      else { try { c.el.currentTime = 0; } catch (e) {} }
      c.el.loop = !!loop;
    }
  },

  CarsMusic_Play__deps: ['$CarsMusic'],
  CarsMusic_Play: function (urlPtr, loop, fade) {
    var s = CarsMusic.get(), url = UTF8ToString(urlPtr);
    var cur = s.cur >= 0 ? s.chans[s.cur] : null;
    if (cur && cur.url === url && cur.active) { CarsMusic.ramp(cur, 1, fade); CarsMusic.start(cur); return; }
    var next = s.chans[s.cur === 0 ? 1 : 0];
    if (cur) CarsMusic.fadeOut(cur, fade);
    CarsMusic.load(next, url, loop);
    CarsMusic.ramp(next, 1, fade, 0);
    s.cur = s.chans.indexOf(next);
    s.debug.plays++; s.debug.track = url.split('/').pop();
    CarsMusic.start(next);
  },

  CarsMusic_Stop__deps: ['$CarsMusic'],
  CarsMusic_Stop: function (fade) {
    var s = CarsMusic.get();
    if (s.cur >= 0) CarsMusic.fadeOut(s.chans[s.cur], fade);
    s.cur = -1; s.debug.track = '';
  },

  CarsMusic_Sting__deps: ['$CarsMusic'],
  CarsMusic_Sting: function (urlPtr, volume) {
    var s = CarsMusic.get(), c = s.sting, url = UTF8ToString(urlPtr);
    CarsMusic.load(c, url, false);
    CarsMusic.ramp(c, volume, 0.01, volume);
    s.debug.sting = url.split('/').pop();
    CarsMusic.start(c);
  },

  CarsMusic_FinalLap__deps: ['$CarsMusic'],
  CarsMusic_FinalLap: function (urlPtr, active, tempo) {
    var s = CarsMusic.get(), c = s.finalLap;
    if (!active) { c.active = false; CarsMusic.sync(); return; }
    CarsMusic.load(c, UTF8ToString(urlPtr), true);
    c.el.playbackRate = tempo;
    var main = s.cur >= 0 ? s.chans[s.cur].el : null;
    function align() {
      if (main && isFinite(c.el.duration) && c.el.duration > 0)
        c.el.currentTime = (main.currentTime * tempo) % c.el.duration;
    }
    if (c.el.readyState >= 1) align();
    else c.el.addEventListener('loadedmetadata', align, { once: true });
    CarsMusic.ramp(c, 0.33, 0.16, 0);
    CarsMusic.start(c);
  },

  CarsMusic_Preload__deps: ['$CarsMusic'],
  CarsMusic_Preload: function (urlPtr) {
    var s = CarsMusic.get(), url = UTF8ToString(urlPtr);
    if (s.cache[url]) return;
    var a = new Audio(); a.preload = 'auto'; a.src = url;   // warms the HTTP cache for the sting element
    s.cache[url] = a;
  },

  CarsMusic_SetVolume__deps: ['$CarsMusic'],
  CarsMusic_SetVolume: function (v) {
    var s = CarsMusic.get();
    s.volume = v;
    if (s.bus) { var t = s.ctx.currentTime, g = s.bus.gain; g.cancelScheduledValues(t); g.setValueAtTime(g.value, t); g.linearRampToValueAtTime(v, t + 0.15); }
    else { var all = s.chans.concat([s.sting, s.finalLap]); for (var i = 0; i < all.length; i++) all[i].el.volume = Math.max(0, Math.min(1, all[i].level * v)); }
  },

  CarsMusic_SetPaused__deps: ['$CarsMusic'],
  CarsMusic_SetPaused: function (p) {
    var s = CarsMusic.get();
    s.paused = !!p;
    CarsMusic.sync();
  },

  CarsMusic_Duck__deps: ['$CarsMusic'],
  CarsMusic_Duck: function (depth, attack, hold, release) {
    var s = CarsMusic.get();
    if (!s.duck) return;
    var t = s.ctx.currentTime, g = s.duck.gain, to = Math.max(0, 1 - depth);
    if (g.value < to) return;   // already dipped deeper
    g.cancelScheduledValues(t); g.setValueAtTime(g.value, t);
    g.linearRampToValueAtTime(to, t + Math.max(0.01, attack));
    g.setValueAtTime(to, t + attack + hold);
    g.linearRampToValueAtTime(1, t + attack + hold + Math.max(0.01, release));
  }
};
mergeInto(LibraryManager.library, CarsMusicLib);
