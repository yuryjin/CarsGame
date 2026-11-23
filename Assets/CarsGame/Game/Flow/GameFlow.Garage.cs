using TMPro;
using UnityEngine;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;

namespace RacingGame
{
    public enum PracticeLesson { None, Drift, Shortcut, WallRide, Aim, Launch, CannonTarget, BeamShield, PursuitMine }

    public sealed partial class GameFlow
    {
        public const float TestDriveSeconds = 45f;
        public bool IsTestDrive => testDriveEvent != null;
        public bool ReturningFromTestDrive { get; private set; }
        public bool ReturningFromPractice { get; private set; }
        public PracticeLesson CurrentPractice { get; private set; }
        public bool IsGarageDrill => CurrentPractice >= PracticeLesson.CannonTarget && CurrentPractice <= PracticeLesson.PursuitMine;
        public bool IsPractice => IsTestDrive && CurrentPractice != PracticeLesson.None;
        public bool PracticeCompleted { get; private set; }
        public float TestDriveRemaining => Mathf.Max(0f, TestDriveSeconds - (float)race.RaceTime);
        internal float TestDriveOverlayBottomPixels => testDriveTimer != null && testDriveCanvas.gameObject.activeInHierarchy
            ? -((RectTransform)(practiceRetryLabel != null ? practiceRetryLabel : testDriveTimer).transform.parent).offsetMin.y * testDriveCanvas.scaleFactor : 0f;
        private RaceEventDefinition testDriveEvent, beforeTestEvent;
        private DifficultyTier beforeTestTier;
        private string beforeTestBody;
        private Canvas testDriveCanvas;
        private TextMeshProUGUI testDriveTimer;
        private TextMeshProUGUI practiceRetryLabel;
        private PerfectStart launchTraining;
        private Pose? maneuverApproach;
        private int shownTestSecond = -1;
        private string shownTestLanguage;
        private float testDrivePixelScale = -1f;
        private int testDriveScreenWidth, testDriveScreenHeight;
        private bool testDriveTouch;
        private RaceDirector practiceDirector;

        public bool StartTestDrive(string bodyId) => StartDrive(bodyId, PracticeLesson.None);

        public bool StartPractice(PracticeLesson lesson)
        {
            if (lesson < PracticeLesson.Drift || lesson > PracticeLesson.Launch || PlatformGlue.Save == null) return false;
            var progress = PlatformGlue.Save.Data.progress;
            return StartDrive(CarBodies.Selected(progress.cosmetics, progress.body), lesson);
        }

        public static string PracticeTitleKey(PracticeLesson lesson) => "practice." + lesson.ToString().ToLowerInvariant();

        private bool StartDrive(string bodyId, PracticeLesson lesson)
        {
            bool fromResults = State == FlowState.Results && lesson != PracticeLesson.None && lesson == MissedLesson && !IsDoublingReward;
            if ((State != FlowState.Menu && !fromResults) || IsTestDrive || !PlatformGlue.IsReady || race.spawner == null || race.spawner.raceCarPrefab == null) return false;
            if (PlatformGlue.Save.LocalVersionTooNew) return false;
            if (CarBodies.Get(bodyId).Id != bodyId) return false;
            if (fromResults) practiceReturnResults = ResultsForMenu;
            beforeTestEvent = RaceSetup.Event;
            beforeTestTier = RaceSetup.Tier;
            beforeTestBody = race.spawner.playerBodyId;
            CurrentPractice = lesson;
            PracticeCompleted = false;
            testDriveEvent = ScriptableObject.CreateInstance<RaceEventDefinition>();
            testDriveEvent.IsPractice = lesson != PracticeLesson.None;
            testDriveEvent.eventId = lesson == PracticeLesson.None ? "garage_test" : "practice_" + lesson.ToString().ToLowerInvariant();
            testDriveEvent.nameKey = lesson == PracticeLesson.None ? "garage.test_drive" : PracticeTitleKey(lesson);
            testDriveEvent.laps = lesson == PracticeLesson.None ? 1 : 20;
            testDriveEvent.aiCountOverride = lesson == PracticeLesson.None || lesson == PracticeLesson.Aim || IsGarageDrill ? 1 : 0;
            testDriveEvent.pickupCount = IsGarageDrill ? 0 : 9;
            testDriveEvent.mineFieldCount = 0;
            if (lesson == PracticeLesson.WallRide)
            {
                testDriveEvent.starRingTrack = true;
                var ring = System.Array.Find(events, e => e != null && e.starRingTrack);
                if (ring != null) testDriveEvent.sky = ring.sky;
            }
            race.spawner.playerBodyId = bodyId;
            ReturningFromTestDrive = false;
            ReturningFromPractice = false;
            var practiceTier = lesson != PracticeLesson.None ? System.Array.Find(tiers, t => t != null && t.tierId == "easy") : null;
            StartRace(testDriveEvent, practiceTier != null ? practiceTier : beforeTestTier);
            if (!IsTestDrive) return false;

            if (lesson == PracticeLesson.Drift || lesson == PracticeLesson.Shortcut || lesson == PracticeLesson.WallRide)
            {
                var car = race.Player.Car;
                if (lesson != PracticeLesson.Drift)
                {
                    float entry = lesson == PracticeLesson.Shortcut ? TrackFork.Entry : starRing.Layout.TunnelStart;
                    track.Pose(track.Wrap(entry - 30f), 0f, out var position, out var rotation);
                    car.Teleport(position + Vector3.up * race.spawner.grid.dropHeight, rotation);
                }
                maneuverApproach = new Pose(car.transform.position, car.transform.rotation);
            }

            // A moving target lets the player try their weapons without being shot during the comparison.
            foreach (var p in race.Participants)
            {
                if (p.isPlayer) continue;
                var gunner = p.GetComponent<AiGunner>();
                if (gunner != null)
                {
                    if (IsPractice) gunner.enabled = false;
                    else gunner.startGraceSeconds = TestDriveSeconds + 10f;
                }
                p.Weapons.secondaryType = SecondaryWeaponType.None;
            }
            if (lesson == PracticeLesson.Aim && race.Player != null)
            {
                race.Player.Weapons.ConfigureLoadout(true, PrimaryWeaponType.Cannon, PrimaryWeaponType.Cannon);
                practiceDirector = RaceDirector.Current;
                if (practiceDirector != null) practiceDirector.ShellHit += OnPracticeHit;
            }
            if (IsGarageDrill) ConfigureGarageDrill();
            testDriveCanvas = UiFactory.CreateOverlayCanvas("TestDrive", 35);
            testDriveCanvas.transform.SetParent(transform, false);
            var back = UiFactory.Button(testDriveCanvas.transform, "ReturnToGarage", Loc.Get("garage.test_return"), EnterMenu,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-300f, -178f), new Vector2(300f, -110f), 26f);
            testDriveTimer = back.GetComponentInChildren<TextMeshProUGUI>();
            if (lesson == PracticeLesson.Launch || maneuverApproach.HasValue)
            {
                if (lesson == PracticeLesson.Launch) launchTraining = GetComponent<PerfectStart>();
                var retry = UiFactory.Button(testDriveCanvas.transform, "PracticeRetry", Loc.Get("results.retry"), RetryRace,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, 26f, UiFactory.Raised);
                practiceRetryLabel = retry.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (IsGarageDrill)
                UiFactory.Button(back.transform, "RepeatDrill", Loc.Get("drill.repeat"), RetryRace,
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -76f), new Vector2(0f, -6f), 24f, UiFactory.Raised);
            shownTestSecond = -1;
            testDrivePixelScale = -1f;
            return true;
        }

        private void RetryManeuver(Pose approach)
        {
            PauseController.Set(PauseReason.User | PauseReason.Menu, false);
            race.AbortRace();
            RaceDirector.Current?.ResetRace();
            PracticeCompleted = false;
            shownTestSecond = -1;
            race.StartRace();
            if (race.Player == null) { EnterMenu(); return; }
            race.Player.Car.Teleport(approach.position, approach.rotation);
            if (pickups != null) pickups.Spawn(testDriveEvent.pickupCount);
            if (starRing != null) starRing.Follow(race);
            if (hud != null) hud.SetVisible(true);
            race.StartPracticeImmediately();
        }

        private void OnPracticeHit(GameObject shooter, GameObject victim)
        {
            if (IsPractice && CurrentPractice == PracticeLesson.Aim && race.Player != null
                && shooter == race.Player.gameObject && victim != shooter) CompletePractice();
        }

        private void CompletePractice()
        {
            if (PracticeCompleted) return;
            PracticeCompleted = true;
            shownTestSecond = -1;
            if (hud != null) hud.ShowHint(Loc.Get("practice.completed"), 8f);
        }

        private void TickTestDrive()
        {
            if (!IsTestDrive || State != FlowState.Racing) return;
            if (IsPractice && !PracticeCompleted && race.State == RaceState.Racing && !PauseController.IsPaused && race.Player != null)
            {
                var player = race.Player;
                if (CurrentPractice == PracticeLesson.Drift && player.DriftBoosts > 0
                    || CurrentPractice == PracticeLesson.Shortcut && player.Shortcuts > 0
                    || CurrentPractice == PracticeLesson.WallRide && player.CeilingBoosts > 0) CompletePractice();
            }
            if (testDriveCanvas != null)
            {
                bool visible = !PauseController.IsPaused;
                if (testDriveCanvas.gameObject.activeSelf != visible) testDriveCanvas.gameObject.SetActive(visible);
                float scale = Mathf.Max(0.01f, testDriveCanvas.transform.lossyScale.x);
                bool touch = TouchControlsPresenter.IsShown;
                if (visible && (Mathf.Abs(scale - testDrivePixelScale) > 0.001f || testDriveScreenWidth != Screen.width || testDriveScreenHeight != Screen.height || testDriveTouch != touch))
                {
                    testDrivePixelScale = scale; testDriveScreenWidth = Screen.width; testDriveScreenHeight = Screen.height;
                    testDriveTouch = touch;
                    var button = (RectTransform)testDriveTimer.transform.parent;
                    float widthPixels = Mathf.Min(380f, Screen.width * 0.44f);
                    if (hud != null) widthPixels = hud.ConstrainTopCenterWidth(widthPixels);
                    float width = widthPixels / scale;
                    float height = Mathf.Max(IsPractice ? 64f : 44f, (IsPractice ? 100f : 68f) * scale) / scale;
                    float top = (IsPractice ? 12f : Mathf.Max(44f, 110f * scale)) / scale;
                    button.offsetMin = new Vector2(-width * 0.5f, -top - height);
                    button.offsetMax = new Vector2(width * 0.5f, -top);
                    testDriveTimer.fontSizeMax = Mathf.Max(26f, 14f / scale);
                    testDriveTimer.fontSizeMin = 14f / scale;
                    testDriveTimer.enableWordWrapping = IsPractice;
                    if (practiceRetryLabel != null)
                    {
                        var retry = (RectTransform)practiceRetryLabel.transform.parent;
                        float retryTop = button.offsetMin.y - 4f / scale;
                        retry.offsetMin = new Vector2(-width * .5f, retryTop - Mathf.Max(44f / scale, 60f));
                        retry.offsetMax = new Vector2(width * .5f, retryTop);
                        practiceRetryLabel.fontSizeMin = 14f / scale;
                        practiceRetryLabel.fontSizeMax = Mathf.Max(26f, practiceRetryLabel.fontSizeMin);
                    }
                }
            }
            int? launchOffset = launchTraining != null ? launchTraining.TrainingOffsetMs : null;
            if (launchOffset.HasValue) PracticeCompleted = launchTraining.Awarded;
            int seconds = IsPractice ? (PracticeCompleted || launchOffset.HasValue ? 1 : 0) : Mathf.CeilToInt(TestDriveRemaining);
            if (Loc.Ready && testDriveTimer != null && (seconds != shownTestSecond || shownTestLanguage != Loc.Language))
            {
                shownTestSecond = seconds;
                shownTestLanguage = Loc.Language;
                if (IsPractice)
                {
                    string status = Loc.Get(PracticeTitleKey(CurrentPractice));
                    if (launchOffset.HasValue)
                        status = Loc.Format(PracticeCompleted ? "practice.launch.hit" : "practice.launch.miss", "ms",
                            launchOffset.Value.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture));
                    else if (launchTraining == null) status += "  " + seconds + "/1";
                    testDriveTimer.text = status + "\n" + Loc.Get(IsGarageDrill ? "garage.test_return" : practiceReturnResults != null ? "practice.return_results" : "practice.return");
                    if (practiceRetryLabel != null) practiceRetryLabel.text = Loc.Get("results.retry");
                    testDriveTimer.color = UiFactory.Ink;
                    testDriveTimer.transform.parent.GetComponent<UnityEngine.UI.Image>().color = PracticeCompleted ? UiFactory.Positive : UiFactory.Accent;
                }
                else testDriveTimer.text = Loc.Get("garage.test_return") + "  ·  " + seconds;
            }
            if (!IsPractice && (race.State == RaceState.Racing || race.State == RaceState.Finishing) && seconds <= 0) EnterMenu();
        }

        private void RestoreTestDrive()
        {
            if (!IsTestDrive) return;
            if (practiceDirector != null) practiceDirector.ShellHit -= OnPracticeHit;
            RestoreGarageDrill();
            practiceDirector = null;
            PlatformGlue.Platform?.SetGameplayActive(false);
            RaceSetup.Event = beforeTestEvent;
            RaceSetup.Tier = beforeTestTier;
            if (race != null && race.spawner != null) race.spawner.playerBodyId = beforeTestBody;
            if (testDriveCanvas != null) { testDriveCanvas.gameObject.SetActive(false); Destroy(testDriveCanvas.gameObject); }
            testDriveCanvas = null; testDriveTimer = null;
            practiceRetryLabel = null; launchTraining = null;
            maneuverApproach = null;
            Destroy(testDriveEvent);
            testDriveEvent = null; beforeTestEvent = null; beforeTestTier = null;
            CurrentPractice = PracticeLesson.None;
            PracticeCompleted = false;
        }
    }
}
