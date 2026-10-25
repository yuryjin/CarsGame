using TMPro;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    public sealed partial class GameFlow
    {
        public const float TestDriveSeconds = 45f;
        public bool IsTestDrive => testDriveEvent != null;
        public bool ReturningFromTestDrive { get; private set; }
        public float TestDriveRemaining => Mathf.Max(0f, TestDriveSeconds - (float)race.RaceTime);
        internal float TestDriveOverlayBottomPixels => testDriveTimer != null && testDriveCanvas.gameObject.activeInHierarchy
            ? -((RectTransform)testDriveTimer.transform.parent).offsetMin.y * testDriveCanvas.scaleFactor : 0f;
        private RaceEventDefinition testDriveEvent, beforeTestEvent;
        private DifficultyTier beforeTestTier;
        private string beforeTestBody;
        private Canvas testDriveCanvas;
        private TextMeshProUGUI testDriveTimer;
        private int shownTestSecond = -1;
        private float testDrivePixelScale = -1f;
        private int testDriveScreenWidth, testDriveScreenHeight;
        private bool testDriveTouch;

        public bool StartTestDrive(string bodyId)
        {
            if (State != FlowState.Menu || IsTestDrive || !PlatformGlue.IsReady || race.spawner == null || race.spawner.raceCarPrefab == null) return false;
            if (CarBodies.Get(bodyId).Id != bodyId) return false;
            beforeTestEvent = RaceSetup.Event;
            beforeTestTier = RaceSetup.Tier;
            beforeTestBody = race.spawner.playerBodyId;
            testDriveEvent = ScriptableObject.CreateInstance<RaceEventDefinition>();
            testDriveEvent.eventId = "garage_test";
            testDriveEvent.nameKey = "garage.test_drive";
            testDriveEvent.laps = 1;
            testDriveEvent.aiCountOverride = 1;
            testDriveEvent.pickupCount = 9;
            testDriveEvent.mineFieldCount = 0;
            race.spawner.playerBodyId = bodyId;
            ReturningFromTestDrive = false;
            StartRace(testDriveEvent, beforeTestTier);
            if (!IsTestDrive) return false;

            // A moving target lets the player try their weapons without being shot during the comparison.
            foreach (var p in race.Participants)
            {
                if (p.isPlayer) continue;
                var gunner = p.GetComponent<AiGunner>();
                if (gunner != null) gunner.startGraceSeconds = TestDriveSeconds + 10f;
                p.Weapons.secondaryType = SecondaryWeaponType.None;
            }
            testDriveCanvas = UiFactory.CreateOverlayCanvas("TestDrive", 35);
            testDriveCanvas.transform.SetParent(transform, false);
            var back = UiFactory.Button(testDriveCanvas.transform, "ReturnToGarage", Loc.Get("garage.test_return"), EnterMenu,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-300f, -178f), new Vector2(300f, -110f), 26f);
            testDriveTimer = back.GetComponentInChildren<TextMeshProUGUI>();
            shownTestSecond = -1;
            testDrivePixelScale = -1f;
            return true;
        }

        private void TickTestDrive()
        {
            if (!IsTestDrive || State != FlowState.Racing) return;
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
                    float height = Mathf.Max(44f, 68f * scale) / scale;
                    float top = Mathf.Max(44f, 110f * scale) / scale;
                    button.offsetMin = new Vector2(-width * 0.5f, -top - height);
                    button.offsetMax = new Vector2(width * 0.5f, -top);
                    testDriveTimer.fontSizeMax = Mathf.Max(26f, 14f / scale);
                    testDriveTimer.fontSizeMin = 14f / scale;
                    testDriveTimer.enableWordWrapping = false;
                }
            }
            int seconds = Mathf.CeilToInt(TestDriveRemaining);
            if (seconds != shownTestSecond && testDriveTimer != null)
            {
                shownTestSecond = seconds;
                testDriveTimer.text = Loc.Get("garage.test_return") + "  ·  " + seconds;
            }
            if ((race.State == RaceState.Racing || race.State == RaceState.Finishing) && seconds <= 0) EnterMenu();
        }

        private void RestoreTestDrive()
        {
            if (!IsTestDrive) return;
            PlatformGlue.Platform?.SetGameplayActive(false);
            RaceSetup.Event = beforeTestEvent;
            RaceSetup.Tier = beforeTestTier;
            if (race != null && race.spawner != null) race.spawner.playerBodyId = beforeTestBody;
            if (testDriveCanvas != null) { testDriveCanvas.gameObject.SetActive(false); Destroy(testDriveCanvas.gameObject); }
            testDriveCanvas = null; testDriveTimer = null;
            Destroy(testDriveEvent);
            testDriveEvent = null; beforeTestEvent = null; beforeTestTier = null;
        }
    }
}
