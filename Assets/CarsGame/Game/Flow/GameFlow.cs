using UnityEngine;
using UnityEngine.InputSystem;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;
using RacingGame.World;

namespace RacingGame
{
    public enum FlowState { Menu, Racing, Results }

    /// <summary>
    /// Top-level game loop of the single scene: menu → race (event + tier from the menu) →
    /// results → menu/garage. Applies event rules (laps, reverse, night, damage, pickups),
    /// upgrades to the player car, pause key, audio cues and payouts.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public sealed class GameFlow : MonoBehaviour
    {
        public RaceEventDefinition[] events;
        public DifficultyTier[] tiers;

        public FlowState State { get; private set; } = FlowState.Menu;
        public RaceEventDefinition CurrentEvent => RaceSetup.Event;
        public int LastReward { get; private set; }
        public bool LastRewardDoubled { get; private set; }
        public event System.Action<FlowState> StateChanged;

        private RaceManager race;
        private InputAction pauseAction;
        private CarController playerCar;
        private EngineAudio playerEngine;
        private SkyDirector sky;
        private TrackPath track;
        private PickupSpawner pickups;
        private HudScreen hud;
        private GhostRecorder recorder;
        private readonly System.Collections.Generic.List<GhostCar> ghosts = new System.Collections.Generic.List<GhostCar>();
        private int raceGeneration;               // invalidates async work (ghost loads, ads) from an earlier race
        private bool doubling;
        [System.Serializable] private class GhostSession { public string ghost; public string name; }
        private static readonly Color GhostTint = new Color(0.6f, 0.8f, 1f, 0.35f);

        private void Awake()
        {
            race = GetComponent<RaceManager>();
            race.autoStart = false;
            hud = GetComponent<HudScreen>();
            pickups = GetComponent<PickupSpawner>();
            recorder = GetComponent<GhostRecorder>() ?? gameObject.AddComponent<GhostRecorder>();
            if (GetComponent<DebugBus>() == null) gameObject.AddComponent<DebugBus>();   // QA channel; idle until the page sends a command
            track = TrackPath.Instance;
            var map = InputSystem.actions != null ? InputSystem.actions.FindActionMap("Driving") : null;
            pauseAction = map != null ? map.FindAction("Pause") : null;
            if (map != null && !map.enabled) map.Enable();
            sky = GetComponent<SkyDirector>() ?? FindFirstObjectByType<SkyDirector>();
            if (RaceSetup.Tier == null && tiers != null && tiers.Length > 0) RaceSetup.Tier = tiers[Mathf.Min(1, tiers.Length - 1)];
            if (RaceSetup.Event == null && events != null && events.Length > 0) RaceSetup.Event = events[0];
        }

        private void Start() { EnterMenu(); }

        private void OnEnable()
        {
            race.OnFieldSpawned += OnFieldSpawned;
            race.OnCountdownTick += OnCountdown;
            race.OnRaceStarted += OnStarted;
            race.OnRaceFinished += OnFinished;
            race.OnParticipantFinished += OnParticipantFinished;
        }

        private void OnDisable()
        {
            race.OnFieldSpawned -= OnFieldSpawned;
            race.OnCountdownTick -= OnCountdown;
            race.OnRaceStarted -= OnStarted;
            race.OnRaceFinished -= OnFinished;
            race.OnParticipantFinished -= OnParticipantFinished;
            Teardown();
        }

        /// <summary>Idempotent: undo every race-time rule and drop async work in flight.</summary>
        private void Teardown()
        {
            raceGeneration++;
            PauseController.ResetAll();
            if (recorder != null) recorder.Stop();
            ClearGhosts();
            if (race != null && race.State != RaceState.Idle) race.AbortRace();
            if (pickups != null) pickups.Clear();
            var director = RaceDirector.Current; if (director != null) director.ResetRace();   // no shell survives into the next race
            if (sky != null) sky.ApplyDefault();
            CarHealth.GlobalDamageMultiplier = 1f;
            if (track != null && track.reversed) { track.reversed = false; track.Build(); }
        }

        private void Update()
        {
            bool pausePressed = pauseAction != null ? pauseAction.WasPressedThisFrame()
                : (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
            if (pausePressed && State == FlowState.Racing && race.State >= RaceState.Countdown && race.State < RaceState.Finished) PauseController.Toggle(PauseReason.User);
        }

        // ---------- transitions ----------
        public void EnterMenu()
        {
            Teardown();
            if (hud != null) hud.SetVisible(false);
            SetState(FlowState.Menu);
        }

        public void StartRace(RaceEventDefinition ev, DifficultyTier tier)
        {
            if (ev != null) RaceSetup.Event = ev;
            if (tier != null) RaceSetup.Tier = tier;
            ev = RaceSetup.Event; tier = RaceSetup.Tier;
            if (!PlatformGlue.IsReady) { MenuScreens.Toast(Loc.Get("common.loading")); return; }
            if (ev != null && !IsUnlocked(ev)) { MenuScreens.Toast(Loc.Get("events.locked")); return; }
            Teardown();
            int generation = raceGeneration;

            // Event rules
            race.tier = tier;
            race.laps = ev != null ? ev.laps : 3;
            race.aiCountOverride = ev != null ? ev.aiCountOverride : -1;
            if (track != null) { track.reversed = ev != null && ev.reversed; track.Build(); }
            CarHealth.GlobalDamageMultiplier = ev != null ? ev.damageMultiplier : 1f;
            // Look before the field spawns so the first frame of the countdown is already correct.
            if (sky != null) sky.Apply(sky.PresetFor(ev != null ? ev.sky : null, ev != null && ev.night));

            LastReward = 0; LastRewardDoubled = false;
            if (MenuCameraDirector.Instance != null) MenuCameraDirector.Instance.Activate(false);   // camera hand-over before cars exist
            race.StartRace();
            if (race.State == RaceState.Idle) { EnterMenu(); return; }
            if (pickups != null) pickups.Spawn(ev != null ? ev.pickupCount : 9);
            if (hud != null) hud.SetVisible(true);
            SetState(FlowState.Racing);
            SpawnGhosts(ev, generation);
        }

        // ---------- ghosts (async multiplayer) ----------
        // Stable across builds/runtimes (string.GetHashCode is not): FNV-1a over event id, direction and layout version.
        private const int LayoutVersion = 1;
        private int TrackHash(RaceEventDefinition ev)
        {
            unchecked
            {
                uint h = 2166136261u;
                string s = (ev != null ? ev.eventId : "e1") + "|" + (track != null && track.reversed ? "R" : "F") + "|" + LayoutVersion + "|" + (track != null ? Mathf.RoundToInt(track.Length) : 0);
                foreach (char c in s) h = (h ^ c) * 16777619u;
                return (int)h;
            }
        }

        private void ClearGhosts()
        {
            foreach (var g in ghosts) if (g != null) Destroy(g.gameObject);
            ghosts.Clear();
        }

        private async void SpawnGhosts(RaceEventDefinition ev, int generation)
        {
            var prefab = race.spawner != null ? race.spawner.raceCarPrefab : null;
            if (prefab == null) return;
            int hash = TrackHash(ev);
            var save = PlatformGlue.Save;
            var own = save != null ? Core.Ghost.GhostRecord.FromBase64(save.Data.bestGhost) : null;
            if (own != null && own.TrackHash == hash) AddGhost(GhostCar.Spawn(prefab, own, GhostTint));

            var sessions = PlatformGlue.Platform?.Sessions;
            if (sessions == null) return;
            // Yandex sessions filter by numeric meta1..meta3: meta1 = event/layout hash, meta2 = finish time (ms).
            var r = await sessions.LoadOpponentsAsync(3, "{\"meta1\":" + EventMeta(ev) + "}");
            if (this == null || generation != raceGeneration || State != FlowState.Racing || !r.Ok) return;   // race changed meanwhile
            foreach (var payload in r.Value)
            {
                var session = ParseSession(payload);
                var rec = session != null ? Core.Ghost.GhostRecord.FromBase64(session.ghost) : null;
                if (rec == null || rec.TrackHash != hash || ghosts.Count >= 4) continue;
                if (!string.IsNullOrEmpty(session.name)) rec.PlayerName = UiFactory.Escape(session.name);
                AddGhost(GhostCar.Spawn(prefab, rec, GhostTint));
            }
        }

        /// <summary>Positive, stable numeric id for the event + direction (Yandex session meta fields are numbers).</summary>
        private int EventMeta(RaceEventDefinition ev) => (TrackHash(ev) & 0x7fffffff) % 1000000000;

        // A ghost that arrives after GO starts from the elapsed race time instead of frozen at the line.
        private void AddGhost(GhostCar g)
        {
            ghosts.Add(g);
            if (race.State == RaceState.Racing || race.State == RaceState.Finishing) g.Begin(race.RaceStartTime);
        }

        // The template JS normalises each opponent session to {"ghost": base64, "name": "..."}.
        private static GhostSession ParseSession(string payload)
        {
            try { return string.IsNullOrEmpty(payload) ? null : JsonUtility.FromJson<GhostSession>(payload); } catch { return null; }
        }

        public void RetryRace() { StartRace(RaceSetup.Event, RaceSetup.Tier); }

        private void SetState(FlowState s) { State = s; StateChanged?.Invoke(s); }

        // ---------- race events ----------
        private void OnFieldSpawned()
        {
            var player = race.Player;
            if (player == null) return;
            playerCar = player.Car;
            var save = PlatformGlue.Save;
            if (save != null) PlayerProgression.ApplyTo(playerCar, player.Health, playerCar.GetComponent<CarCannon>(), save.Data.progress.upgrades);

            if (playerCar.GetComponent<PlayerEffects>() == null) playerCar.gameObject.AddComponent<PlayerEffects>();
            // Engine, tyres and wind for the player; revs with the pedal during the countdown.
            playerEngine = playerCar.GetComponent<EngineAudio>() ?? playerCar.gameObject.AddComponent<EngineAudio>();
            playerEngine.SetRunning(true);
            foreach (var p in race.Participants)
            {
                var health = p.Health; var participant = p;
                if (health == null) continue;
                float lastHp = health.Health;
                int hitIndex = 0;
                health.OnHealthChanged += (cur, max) =>
                {
                    // Only real HP loss sounds like a hit (heals and max-HP changes also raise this event).
                    if (cur < lastHp - 0.01f)
                    {
                        hitIndex++;
                        var clip = participant.isPlayer ? SfxSynth.HitMetal : SfxSynth.HitVariant(hitIndex);
                        SfxPlayer.Instance.Play(clip, participant.transform.position, participant.isPlayer ? 0.85f : 0.7f, 0.92f + 0.08f * (hitIndex % 3));
                    }
                    lastHp = cur;
                };
                health.OnDied += attacker => { SfxPlayer.Instance.Play(SfxSynth.Wreck, participant.transform.position, 1f); OnWreck(participant, attacker); };
                p.Progress.LapCompleted += (lap, t) => { if (participant.isPlayer) SfxPlayer.Instance.PlayUi(SfxSynth.LapDing); };
            }
        }

        private static readonly Color KillColor = new Color(1f, 0.85f, 0.25f), DeathColor = new Color(1f, 0.35f, 0.25f);

        // Kill confirmation for the player's own kills, and who got the player.
        private void OnWreck(RaceParticipant victim, GameObject attacker)
        {
            if (hud == null || race.Player == null) return;
            var player = race.Player;
            if (!victim.isPlayer && attacker != null && attacker == player.gameObject)
            {
                hud.ShowEvent(Loc.Format("hud.kill", "name", victim.displayName), KillColor);
                SfxPlayer.Instance.PlayUi(SfxSynth.KillSting, 0.8f);
                var impulse = Camera.main != null ? Camera.main.GetComponent<CameraImpulse>() : null;
                if (impulse != null) impulse.AddImpulse(0.2f);
            }
            else if (victim.isPlayer)
            {
                var killer = attacker != null ? attacker.GetComponent<RaceParticipant>() : null;
                hud.ShowEvent(killer != null ? Loc.Format("hud.killed_by", "name", killer.displayName) : Loc.Get("hud.wrecked"), DeathColor);
            }
        }

        private void OnCountdown(int value) { SfxPlayer.Instance.PlayUi(value > 0 ? SfxSynth.Beep : SfxSynth.Go); }
        private void OnStarted()
        {
            if (playerEngine != null) playerEngine.SetRunning(true);
            foreach (var g in ghosts) if (g != null) g.Begin(race.RaceStartTime);
            if (race.Player != null) recorder.Begin(race.Player, TrackHash(RaceSetup.Event), UiFactory.Escape(PlatformGlue.Platform != null ? PlatformGlue.Platform.Player.Name : "You"));
        }
        private void OnParticipantFinished(RaceParticipant p)
        {
            if (!p.isPlayer) return;
            SfxPlayer.Instance.PlayUi(SfxSynth.Finish);
            var rec = recorder.Stop();
            if (rec == null || rec.Samples.Count < 10) return;
            var save = PlatformGlue.Save;
            if (save != null)
            {
                var prev = Core.Ghost.GhostRecord.FromBase64(save.Data.bestGhost);
                if (prev == null || prev.TrackHash != rec.TrackHash || rec.Duration < prev.Duration) { save.Data.bestGhost = rec.ToBase64(); save.MarkDirty(); }
            }
            var sessions = PlatformGlue.Platform?.Sessions;
            if (sessions != null)
            {
                sessions.Commit(rec.ToBase64());
                _ = sessions.PushAsync("{\"meta1\":" + EventMeta(RaceSetup.Event) + ",\"meta2\":" + (long)(rec.Duration * 1000) + "}");
            }
        }

        private void OnFinished(RaceResults results)
        {
            if (playerEngine != null) playerEngine.SetRunning(false);
            recorder.Stop();                                   // DNF path never reached OnParticipantFinished
            var me = results.Player;
            var save = PlatformGlue.Save;
            var ev = RaceSetup.Event;
            float mult = RaceSetup.Tier != null ? RaceSetup.Tier.rewardMultiplier : 1f;
            int[] killsPerVictim = me != null && me.Kills > 0 ? KillsPerVictim(race.Player) : null;
            LastReward = me != null ? Economy.RaceReward(me.Position, me.Finished, me.Laps, race.laps, killsPerVictim, mult) : 0;
            LastRewardFinished = me != null && me.Finished;

            if (save != null && me != null)
            {
                var row = save.Local;
                row.coinsEarned += LastReward;
                row.races++;
                row.kills += me.Kills; row.deaths += me.Deaths;
                if (me.Finished && me.Position == 1) row.wins++;
                if (ev != null && me.Finished)
                {
                    long ms = (long)(me.TotalTime * 1000.0);
                    var best = save.Data.progress.bests.Find(b => b.eventId == ev.eventId);
                    if (best == null) save.Data.progress.bests.Add(new Core.Save.EventBest { eventId = ev.eventId, bestTimeMs = ms });
                    else if (ms < best.bestTimeMs) best.bestTimeMs = ms;
                    if (me.Position <= 3) UnlockNext(ev, save);
                }
                save.MarkDirty();
            }
            GetComponent<PlatformGlue>()?.AfterRaceRecorded();   // flush → leaderboards → interstitial, in that order
            if (hud != null) hud.SetVisible(false);
            SetState(FlowState.Results);
        }

        public bool LastRewardFinished { get; private set; }

        private static int[] KillsPerVictim(RaceParticipant player) => player != null ? player.KillTally.ToPerVictimCounts() : null;

        private void UnlockNext(RaceEventDefinition finished, Core.Save.SaveService save)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (e == null || e.unlockAfterEventId != finished.eventId) continue;
                if (!save.Data.progress.unlockedEvents.Contains(e.eventId)) save.Data.progress.unlockedEvents.Add(e.eventId);
            }
        }

        public bool IsUnlocked(RaceEventDefinition e)
        {
            if (e == null) return false;
            if (string.IsNullOrEmpty(e.unlockAfterEventId)) return true;
            var save = PlatformGlue.Save;
            return save != null && save.Data.progress.unlockedEvents.Contains(e.eventId);
        }

        /// <summary>Rewarded ad on the results screen: doubles the payout once.</summary>
        public async void DoubleReward()
        {
            if (doubling || LastRewardDoubled || LastReward <= 0 || !LastRewardFinished) return;
            var ads = PlatformGlue.Platform?.Ads;
            if (ads == null || !ads.IsRewardedAvailable) { MenuScreens.Toast(Loc.Get("ads.unavailable")); return; }
            doubling = true;
            int reward = LastReward, generation = raceGeneration;   // snapshot: a later race must not be credited
            try
            {
                var r = await ads.ShowRewardedAsync();
                if (this == null) return;
                if (r.Ok && r.Value && generation == raceGeneration && !LastRewardDoubled)
                {
                    LastRewardDoubled = true;
                    PlatformGlue.Save.Local.coinsEarned += reward;
                    PlatformGlue.Save.MarkDirty();
                    PlatformGlue.Pacer.OnRewardedShown(Time.realtimeSinceStartup);
                    MenuScreens.Toast(Loc.Get("results.doubled"));
                    StateChanged?.Invoke(State);
                }
                else if (!(r.Ok && r.Value)) MenuScreens.Toast(Loc.Get(r.Ok ? "ads.no_reward" : "ads.unavailable"));
            }
            finally { doubling = false; }
        }
    }
}
