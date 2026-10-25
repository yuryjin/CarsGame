using System;
using UnityEngine;
using RacingGame.Core.Economy;
using RacingGame.Platform;

namespace RacingGame
{
    /// <summary>
    /// QA and profiling channel between the web page and the game. The page sends
    /// {"cmd":"...","args":[...]} through window.CarsPlatform.cmd (only on local hosts or with ?debug=1);
    /// the game answers with a JSON snapshot posted to window.CarsPlatform.debug.state. Frame statistics
    /// are collected only after "perf on", so a normal session pays nothing. Editor eval scripts can call
    /// <see cref="Execute"/> directly and read <see cref="PlatformRuntime.LastDebug"/>.
    /// </summary>
    [RequireComponent(typeof(GameFlow))]
    public sealed class DebugBus : MonoBehaviour
    {
        [Serializable] private class Command { public string cmd; public string[] args; }

        [Serializable]
        public class Snapshot
        {
            public string flow, race, evt, tier, screen, lastCommand, error;
            public int coins, lastReward, position, lap, laps, races, liveShells;
            public int[] upgrades;
            public bool touch, autopilot;
            public int equippedPrimary, reservePrimary, activePrimary, driftBoosts, shortcuts, ceilingBoosts, bombPasses, resets;
            public bool fullArsenal;
            public double raceSeconds;
            public float speedKmh, health, trackDistance, lateral, throttle, steer;
            public int deaths, aiResets, raceSeed;
            public float aiStuckTime;
            public bool braking, aiReversing, frozen;
            public UiButton[] buttons;
            public string[] texts;
            public UiLabel[] labels;
            public UiReticle reticle;
            public Perf perf;
            public ResourceCounts resources;
        }

        [Serializable] public class ResourceCounts
        {
            public int materials, physicsMaterials, textures, meshes, renderTextures, gameObjects, audioClips;
        }

        [Serializable] public class UiReticle
        {
            public bool visible, locked;
            public float x, y;
        }

        [Serializable] public class UiButton
        {
            public string name, text;
            public float x, y, width, height;
            public bool interactable;
        }

        [Serializable] public class UiLabel
        {
            public string name, parent, text, renderedText;
            public int lines;
            public float fontPixels, x, y, width, height;
            public bool truncated;
        }

        [Serializable]
        public class Perf
        {
            public float p50, p95, p99, max, fps;
            public int frames, gc0, gcDelta, liveShells, cars;
            public long monoUsed, totalAllocated;
            public float sinceStartup;
        }

        private GameFlow flow;
        private RaceManager race;
        private MenuScreens menus;
        private TouchControlsPresenter touch;
        private bool perfOn, autopilot;
        private readonly float[] frameTimes = new float[600];
        private readonly float[] sorted = new float[600];
        private int frameCount, frameHead, gcAtWindowStart;
        private float nextReport;
        private Perf lastPerf;
        private ResourceCounts lastResources;
        private string lastCommand = "", lastError = "";

        /// <summary>
        /// Commands are accepted in the Editor, in mock/standalone builds (there is no store session to
        /// protect) and on a Yandex page that declared itself a debug host (local or ?debug=1).
        /// </summary>
        public static bool ChannelOpen =>
            Application.isEditor || PlatformRuntime.DebugChannelEnabled ||
            (PlatformRuntime.Current != null && PlatformRuntime.Current.Kind != Core.Platform.PlatformKind.Yandex);

        /// <summary>
        /// Commands that fake race results (autopilot, finish) would farm coins and post impossible
        /// leaderboard times on a store page opened with ?debug=1: Editor, development builds, the local
        /// mock-SDK page and non-Yandex runtimes only.
        /// </summary>
        public static bool CheatsAllowed =>
            Application.isEditor || Debug.isDebugBuild || PlatformRuntime.MockSdk ||
            (PlatformRuntime.Current != null && PlatformRuntime.Current.Kind != Core.Platform.PlatformKind.Yandex);

        private void Awake()
        {
            flow = GetComponent<GameFlow>();
            race = GetComponent<RaceManager>();
            menus = GetComponent<MenuScreens>();
            touch = GetComponent<TouchControlsPresenter>() ?? FindAnyObjectByType<TouchControlsPresenter>();
        }

        private void OnEnable() { var rt = PlatformRuntime.Instance; if (rt != null) rt.JsMessage += OnJsMessage; }
        private void OnDisable() { var rt = PlatformRuntime.Instance; if (rt != null) rt.JsMessage -= OnJsMessage; }

        private void Start()
        {
            // PlatformRuntime may be created after this component: subscribe once it exists.
            var rt = PlatformRuntime.Instance;
            if (rt != null) { rt.JsMessage -= OnJsMessage; rt.JsMessage += OnJsMessage; }
        }

        private void OnJsMessage(string channel, string json)
        {
            if (!ChannelOpen) return;   // production Yandex session: ignore commands entirely
            if (string.IsNullOrEmpty(json) || json.IndexOf("\"cmd\"", StringComparison.Ordinal) < 0) return;
            Command c;
            try { c = JsonUtility.FromJson<Command>(json); } catch (Exception) { return; }
            if (c == null || string.IsNullOrEmpty(c.cmd)) return;
            Execute(c.cmd, c.args ?? Array.Empty<string>());
        }

        /// <summary>Run one command and post the resulting snapshot. Unknown commands report an error.</summary>
        public string Execute(string cmd, params string[] args)
        {
            lastCommand = cmd; lastError = "";
            try
            {
                switch (cmd)
                {
                    case "state": break;
                    case "ui": break;
                    case "resources":
                        lastResources = new ResourceCounts {
                            materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                            physicsMaterials = Resources.FindObjectsOfTypeAll<PhysicsMaterial>().Length,
                            textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length,
                            meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                            renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length,
                            gameObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length,
                            audioClips = Resources.FindObjectsOfTypeAll<AudioClip>().Length
                        };
                        break;
                    case "startRace": StartRace(Arg(args, 0), Arg(args, 1)); break;
                    case "daily": autopilot = false; flow.StartRace(flow.DailyEvent, null); break;
                    case "wear": if (CheatsAllowed) Wear(Arg(args, 0), Arg(args, 1)); else lastError = "not in store builds"; break;
                    case "autopilot": if (CheatsAllowed) SetAutopilot(Arg(args, 0) != "off"); else lastError = "not in store builds"; break;
                    case "goto": if (menus != null) { if (flow.State == FlowState.Menu) menus.Show(Arg(args, 0) ?? "main"); else menus.GoTo(Arg(args, 0) ?? "main"); } break;
                    case "buy":
                        if (menus == null || !Enum.TryParse(Arg(args, 0) ?? "", true, out UpgradeKind kind)) lastError = "bad kind";
                        else if (!menus.Buy(kind)) lastError = "not bought";
                        break;
                    case "finish": if (!CheatsAllowed) lastError = "not in store builds"; else if (race != null) race.QaFinishNow(); break;
                    case "touch": if (touch != null) touch.forceShow = Arg(args, 0) != "off"; break;
                    case "perf": perfOn = Arg(args, 0) != "off"; ResetPerf(); break;
                    default: lastError = "unknown command"; break;
                }
            }
            catch (Exception e) { lastError = e.GetType().Name + ": " + e.Message; }
            return Post();
        }

        private static string Arg(string[] args, int i) => args != null && i < args.Length ? args[i] : null;

        // QA: own and wear a paint and a livery (screenshots of cosmetics without buying them).
        private void Wear(string paint, string livery)
        {
            var save = PlatformGlue.Save;
            if (save == null) { lastError = "no save"; return; }
            var p = save.Data.progress;
            p.cosmetics ??= new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(paint)) { if (!p.cosmetics.Contains("paint:" + paint)) p.cosmetics.Add("paint:" + paint); p.paint = paint; }
            if (!string.IsNullOrEmpty(livery)) { if (!p.cosmetics.Contains("livery:" + livery)) p.cosmetics.Add("livery:" + livery); p.livery = livery; }
            save.MarkDirty();
        }

        private void StartRace(string eventId, string tierId)
        {
            RaceEventDefinition ev = null; DifficultyTier tier = null;
            if (flow.events != null) foreach (var e in flow.events) if (e != null && e.eventId == eventId) ev = e;
            if (flow.tiers != null) foreach (var t in flow.tiers) if (t != null && t.tierId == tierId) tier = t;
            if (ev == null && !string.IsNullOrEmpty(eventId)) lastError = "unknown event";
            autopilot = false;
            flow.StartRace(ev, tier);
        }

        // An AI driver in the player's seat: finishes races unattended for smoke tests and profiling.
        private void SetAutopilot(bool on)
        {
            var p = race != null ? race.Player : null;
            if (p == null) { lastError = "no race"; return; }
            var input = p.GetComponent<PlayerDriverInput>();
            var ai = p.GetComponent<AiDriver>();
            if (on)
            {
                if (ai == null) { ai = p.gameObject.AddComponent<AiDriver>(); ai.Initialize(null, 0f); }
                ai.enabled = true;
                p.Car.SetInputSource(ai);
                if (input != null) input.enabled = false;
            }
            else
            {
                if (ai != null) ai.enabled = false;
                if (input != null) { input.enabled = true; p.Car.SetInputSource(input); }
            }
            autopilot = on;
        }

        private void ResetPerf() { frameCount = 0; frameHead = 0; gcAtWindowStart = GC.CollectionCount(0); nextReport = Time.unscaledTime + 1f; }

        private void Update()
        {
            if (!perfOn) return;
            frameTimes[frameHead] = Time.unscaledDeltaTime;
            frameHead = (frameHead + 1) % frameTimes.Length;
            if (frameCount < frameTimes.Length) frameCount++;
            if (Time.unscaledTime >= nextReport)
            {
                nextReport = Time.unscaledTime + 1f;
                lastPerf = ComputePerf();
                Post();
            }
        }

        private Perf ComputePerf()
        {
            int n = frameCount;
            Array.Copy(frameTimes, sorted, n);
            Array.Sort(sorted, 0, n);
            float P(float q) => n == 0 ? 0f : sorted[Mathf.Clamp((int)(q * (n - 1)), 0, n - 1)] * 1000f;
            float sum = 0f; for (int i = 0; i < n; i++) sum += sorted[i];
            int gc = GC.CollectionCount(0);
            var director = RaceDirector.Current;
            var perf = new Perf
            {
                p50 = P(0.5f), p95 = P(0.95f), p99 = P(0.99f), max = P(1f), fps = sum > 0f ? n / sum : 0f, frames = n,
                gc0 = gc, gcDelta = gc - gcAtWindowStart,
                liveShells = director != null ? director.LiveProjectiles : 0, cars = director != null ? director.Cars.Count : 0,
                monoUsed = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
                totalAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                sinceStartup = Time.realtimeSinceStartup
            };
            return perf;
        }

        /// <summary>Current snapshot (also posted to the page).</summary>
        public Snapshot Capture()
        {
            var save = PlatformGlue.Save;
            var p = race != null ? race.Player : null;
            var director = RaceDirector.Current;
            var car = p != null ? p.Car : null;
            var progress = p != null ? p.Progress : null;
            var ai = p != null ? p.GetComponent<AiDriver>() : null;
            return new Snapshot
            {
                flow = flow.State.ToString(), race = race != null ? race.State.ToString() : "", screen = menus != null ? menus.Current : "",
                evt = RaceSetup.Event != null ? RaceSetup.Event.eventId : "", tier = RaceSetup.Tier != null ? RaceSetup.Tier.tierId : "",
                coins = save != null ? (int)save.Data.Coins : 0, races = save != null ? save.Data.Races : 0,
                upgrades = save != null ? save.Data.progress.upgrades : null,
                lastReward = flow.LastReward, position = p != null ? race.PositionOf(p) : 0,
                lap = p != null && p.Progress != null ? p.Progress.LapsCompleted : 0, laps = race != null ? race.laps : 0,
                liveShells = director != null ? director.LiveProjectiles : 0,
                touch = TouchControlsPresenter.IsShown, autopilot = autopilot,
                equippedPrimary = save?.Data.progress.equippedPrimary ?? 0,
                reservePrimary = save?.Data.progress.reservePrimary ?? 2,
                fullArsenal = save?.Data.progress.fullArsenal ?? false,
                activePrimary = p != null ? (int)p.Weapons.PrimaryType : -1,
                driftBoosts = p?.DriftBoosts ?? 0, shortcuts = p?.Shortcuts ?? 0,
                ceilingBoosts = p?.CeilingBoosts ?? 0, bombPasses = p?.BombPasses ?? 0, resets = p?.Resets ?? 0,
                raceSeconds = race != null ? race.RaceTime : 0d,
                speedKmh = car != null ? car.SpeedKmh : 0f, health = p != null && p.Health != null ? p.Health.Health : 0f,
                trackDistance = progress != null ? progress.Distance : 0f,
                lateral = progress != null && progress.Path != null ? progress.Path.Spline.LateralOffset(p.transform.position, progress.Distance) : 0f,
                throttle = car != null ? car.LastCommand.Throttle : 0f, steer = car != null ? car.LastCommand.Steer : 0f,
                braking = car != null && car.LastCommand.Brake, deaths = p?.Deaths ?? 0,
                aiResets = ai != null ? ai.ResetCount : 0,
                raceSeed = director != null ? director.raceSeed : 0,
                aiStuckTime = ai != null ? ai.StuckTime : 0f,
                aiReversing = ai != null && ai.Reversing,
                frozen = p != null && p.Status != null && p.Status.IsFrozen,
                buttons = lastCommand == "ui" ? ReadButtons() : null,
                texts = lastCommand == "ui" ? ReadTexts() : null,
                labels = lastCommand == "ui" ? ReadLabels() : null,
                reticle = lastCommand == "ui" ? ReadReticle() : null,
                lastCommand = lastCommand, error = lastError, perf = perfOn ? lastPerf : null,
                resources = lastCommand == "resources" ? lastResources : null
            };
        }

        private static UiButton[] ReadButtons()
        {
            var list = new System.Collections.Generic.List<UiButton>();
            foreach (var button in FindObjectsByType<UnityEngine.UI.Button>())
            {
                if (!button.isActiveAndEnabled) continue;
                var rect = (RectTransform)button.transform;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                list.Add(new UiButton { name = button.name, text = button.GetComponentInChildren<TMPro.TMP_Text>()?.text ?? "",
                    x = center.x / Screen.width, y = 1f - center.y / Screen.height,
                    width = rect.rect.width * rect.lossyScale.x, height = rect.rect.height * rect.lossyScale.y, interactable = button.IsInteractable() });
            }
            return list.ToArray();
        }

        private UiReticle ReadReticle()
        {
            var hud = GetComponent<HudScreen>();
            if (hud == null) return null;
            Vector2 point = hud.ReticleScreenPosition;
            return new UiReticle { visible = hud.ReticleVisible, locked = hud.ReticleLocked,
                x = point.x / Screen.width, y = 1f - point.y / Screen.height };
        }

        private static string[] ReadTexts()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var label in FindObjectsByType<TMPro.TMP_Text>())
                if (label.isActiveAndEnabled) list.Add(label.name + ": " + label.text);
            return list.ToArray();
        }

        private static UiLabel[] ReadLabels()
        {
            var list = new System.Collections.Generic.List<UiLabel>();
            foreach (var label in FindObjectsByType<TMPro.TextMeshProUGUI>())
            {
                if (!label.isActiveAndEnabled) continue;
                var rect = label.rectTransform;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                list.Add(new UiLabel { name = label.name, parent = rect.parent.name, text = label.text, lines = label.textInfo.lineCount,
                    renderedText = label.GetParsedText(),
                    fontPixels = label.fontSize * rect.lossyScale.y, truncated = label.isTextTruncated,
                    x = center.x / Screen.width, y = 1f - center.y / Screen.height,
                    width = rect.rect.width * rect.lossyScale.x, height = rect.rect.height * rect.lossyScale.y });
            }
            return list.ToArray();
        }

        private string Post()
        {
            string json = JsonUtility.ToJson(Capture());
            PlatformRuntime.PostDebug(json);
            return json;
        }
    }
}
