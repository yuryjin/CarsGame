using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class GaragePlayTests
    {
        private float captureDelta;
        private int trialBefore;
        [SetUp] public void SaveTime() { captureDelta = Time.captureDeltaTime; trialBefore = RewardedOffers.TrialUpgrade; }
        [TearDown] public void RestoreTime()
        {
            Time.captureDeltaTime = captureDelta; PauseController.ResetAll();
            RewardedOffers.GrantTrial((UpgradeKind)trialBefore);
        }

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CornerSwitchChangesPreviewAndLiveCarsWithoutRespawning()
        {
            yield return Boot();
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            bool original = PlatformGlue.ClassicCarModels;
            try
            {
                if (PlatformGlue.ClassicCarModels) PlatformGlue.ToggleCarModels();
                menu.PreviewBody("mammoth");
                yield return null;
                var preview = Object.FindAnyObjectByType<GaragePreview>();
                Assert.NotNull(preview.Model.transform.Find("Tripo_Tank"));
                var progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
                GameObject.Find("ModelStyle").GetComponent<Button>().onClick.Invoke();
                yield return null;
                preview = Object.FindAnyObjectByType<GaragePreview>();
                Assert.NotNull(preview.Model.transform.Find("ClassicHull"));
                Assert.IsNull(preview.Model.transform.Find("Tripo_Tank"));
                Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));

                Assert.IsTrue(flow.StartTestDrive("classic"));
                yield return null;
                var player = race.Player;
                var rb = player.GetComponent<Rigidbody>();
                var position = rb.position;
                var velocity = rb.linearVelocity;
                float health = player.Health.Health, mass = rb.mass;
                var livery = player.GetComponent<CarLivery>() ?? player.gameObject.AddComponent<CarLivery>();
                livery.Apply(LiveryPattern.CenterStripe, Color.white);
                var record = new RacingGame.Core.Ghost.GhostRecord { BodyId = "classic" };
                record.AddPose(Vector3.zero, Quaternion.identity, 0d, 0f, 0);
                var ghost = GhostCar.Spawn(race.spawner.raceCarPrefab, record, Color.cyan);
                try
                {
                    var ghostFilter = ghost.GetComponent<MeshFilter>();
                    var oldMesh = ghostFilter.sharedMesh;
                    var button = GameObject.Find("RaceModelStyle").GetComponent<Button>();
                    var styleLabel = button.GetComponentInChildren<TMPro.TMP_Text>();
                    var spinner = player.Car.frontLeftWheel.GetChild(0);
                    var stockWheel = spinner.GetChild(0).gameObject;
                    Assert.AreEqual(Loc.Get("models.classic"), styleLabel.text);
                    button.onClick.Invoke();
                    Assert.AreEqual(Loc.Get("models.new"), styleLabel.text);
                    Assert.IsTrue(spinner.Find("AlloyWheel").gameObject.activeInHierarchy);
                    Assert.IsFalse(stockWheel.activeSelf);
                    foreach (var participant in race.Participants)
                    {
                        var body = participant.GetComponent<CarBody>();
                        Assert.IsTrue(body.transform.Find("Tripo_" + body.Class).gameObject.activeSelf);
                    }
                    var newParts = player.transform.Find("Tripo_Sport").GetComponentsInChildren<MeshRenderer>();
                    Assert.IsTrue(System.Array.Exists(newParts, r => r.name == "Livery" && r.enabled));
                    Assert.Greater(livery.TriangleCount, 0);
                    Assert.AreNotSame(oldMesh, ghostFilter.sharedMesh);
                    button.onClick.Invoke();
                    Assert.AreSame(oldMesh, ghostFilter.sharedMesh);
                    Assert.IsTrue(stockWheel.activeInHierarchy);
                    Assert.IsFalse(spinner.Find("AlloyWheel").gameObject.activeSelf);
                    Assert.IsTrue(player.transform.Find("Car_Visuals").gameObject.activeSelf);
                    Assert.AreSame(player, race.Player);
                    Assert.AreEqual(position, rb.position);
                    Assert.AreEqual(velocity, rb.linearVelocity);
                    Assert.AreEqual(mass, rb.mass);
                    Assert.AreEqual(health, player.Health.Health);

                    button.onClick.Invoke();
                    var trim = System.Array.Find(newParts, r => r.name == "Trim");
                    Assert.NotNull(trim);
                    player.Health.Detonate(null);
                    Assert.IsTrue(trim.HasPropertyBlock());
                    button.onClick.Invoke();
                    player.Health.ReviveNow();
                    button.onClick.Invoke();
                    Assert.IsFalse(trim.HasPropertyBlock(), "Respawning must clear the wreck tint on both sets");
                }
                finally { Object.Destroy(ghost.gameObject); }
            }
            finally
            {
                if (PlatformGlue.ClassicCarModels != original) PlatformGlue.ToggleCarModels();
                flow.EnterMenu();
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PreviewEveryBodyWithoutSpawningCarsAndReleaseResources()
        {
            yield return Boot();
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            int cars = RaceDirector.Instance.Cars.Count;
            string saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            foreach (var body in CarBodies.All)
            {
                menu.PreviewBody(body.Id);
                yield return null;
                var preview = Object.FindAnyObjectByType<GaragePreview>();
                Assert.NotNull(preview, body.Id);
                Assert.NotNull(preview.Model, body.Id);
                Assert.IsEmpty(preview.Model.GetComponentsInChildren<Collider>(), body.Id);
                Assert.IsEmpty(preview.Model.GetComponentsInChildren<Rigidbody>(), body.Id);
                Assert.IsNull(preview.Model.GetComponent<CarController>(), body.Id);
                if (PrimaryWeaponCatalog.Get((PrimaryWeaponType)PlatformGlue.Save.Data.progress.equippedPrimary).Mode == WeaponMode.Projectile)
                    Assert.NotNull(preview.Model.transform.Find("Cannon_Turret"), "The garage must show the actual roof-mounted weapon: " + body.Id);
                Assert.Greater(preview.Model.GetComponentsInChildren<MeshRenderer>().Length, 0, body.Id);
                Assert.AreEqual(cars, RaceDirector.Instance.Cars.Count, body.Id);
                Assert.AreEqual(RaceState.Idle, race.State);
                Assert.AreEqual(1, System.Array.FindAll(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name == "GarageCamera").Length);
                var before = preview.Model.transform.rotation;
                preview.Rotate(35f);
                Assert.Greater(Quaternion.Angle(before, preview.Model.transform.rotation), 34f);
                string screens = System.Environment.GetEnvironmentVariable("CG_GARAGE_SCREENS");
                if (!string.IsNullOrEmpty(screens) && body.Id == "rhino")
                {
                    System.IO.Directory.CreateDirectory(screens);
                    Capture(menu, screens, "garage-desktop.png", 1280, 720);
                    Capture(menu, screens, "garage-mobile.png", 812, 375);
                }
            }
            Assert.AreEqual(saved, JsonUtility.ToJson(PlatformGlue.Save.Data), "Looking at locked bodies must not buy or equip them.");
            menu.Show("main");
            yield return null;
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<GaragePreview>());
            Assert.IsNull(GameObject.Find("GarageStage"));
        }

        private static void Capture(MenuScreens menu, string directory, string name, int width, int height)
        {
            var camera = Camera.main;
            var canvas = System.Array.Find(menu.GetComponentsInChildren<Canvas>(), c => c.name == "Menus");
            var mode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float plane = canvas.planeDistance;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases(); menu.Show("garage"); Canvas.ForceUpdateCanvases();
                var previewCamera = System.Array.Find(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None), c => c.name == "GarageCamera");
                previewCamera.Render();
                var transition = canvas.GetComponentInChildren<CanvasGroup>();
                if (transition != null) transition.alpha = 1f;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = plane;
                target.Release(); Object.Destroy(target); Object.Destroy(image);
                menu.Show("garage");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TestDrivePreservesSaveAndBonusAndRestoresGarageOnExitAndFinish()
        {
            yield return Boot();
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            var previousEvent = RaceSetup.Event;
            var previousTier = RaceSetup.Tier;
            string previousOverride = race.spawner.playerBodyId;
            RewardedOffers.GrantTrial(UpgradeKind.Engine);
            int bonusBefore = RewardedOffers.TrialUpgrade;
            string saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            Assert.IsFalse(flow.StartTestDrive("missing_body"));
            foreach (string id in new[] { "dune", "tortoise" })
            {
                menu.PreviewBody(id);
                Assert.IsTrue(flow.StartTestDrive(id));
                Assert.IsFalse(flow.StartTestDrive(id), "A running test drive cannot be nested.");
                Assert.AreEqual(id, race.Player.GetComponent<CarBody>().BodyId);
                Assert.AreEqual(2, race.Participants.Count);
                while (race.State == RaceState.Countdown) yield return null;
                for (int i = 0; i < 20; i++) yield return null;
                Assert.IsFalse(System.Array.Exists(Object.FindObjectsByType<Button>(), b => b.name == "LiveGoals"), "Test drives cannot award campaign stars");
                var returnButton = System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == "ReturnToGarage");
                Assert.NotNull(returnButton);
                var returnRect = (RectTransform)returnButton.transform;
                Assert.GreaterOrEqual(returnRect.rect.height * returnRect.lossyScale.y, 43.5f);
                float left = flow.TestDriveRemaining;
                PauseController.Set(PauseReason.User, true);
                yield return null; yield return null;
                Assert.AreEqual(left, flow.TestDriveRemaining, 0.001f);
                Assert.IsFalse(returnButton.gameObject.activeInHierarchy, "The return overlay yields to pause");
                PauseController.Set(PauseReason.User, false);
                yield return null; yield return null;
                Assert.IsTrue(returnButton.gameObject.activeInHierarchy);
                if (id == "dune")
                {
                    flow.RetryRace();
                    Assert.IsTrue(flow.IsTestDrive);
                    Assert.AreEqual(id, race.Player.GetComponent<CarBody>().BodyId);
                    Assert.AreEqual(bonusBefore, RewardedOffers.TrialUpgrade);
                    var button = System.Array.Find(Object.FindObjectsByType<Button>(FindObjectsSortMode.None), b => b.name == "ReturnToGarage");
                    Assert.NotNull(button);
                    button.onClick.Invoke();
                }
                else race.QaFinishNow();
                yield return null;
                Assert.IsFalse(flow.IsTestDrive);
                Assert.AreEqual(FlowState.Menu, flow.State);
                Assert.AreEqual("garage", menu.Current);
                Assert.AreEqual(previousEvent, RaceSetup.Event);
                Assert.AreEqual(previousTier, RaceSetup.Tier);
                Assert.AreEqual(previousOverride, race.spawner.playerBodyId);
                Assert.AreEqual(saved, JsonUtility.ToJson(PlatformGlue.Save.Data));
                Assert.AreEqual(bonusBefore, RewardedOffers.TrialUpgrade);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TestDriveTimesOutWithoutPayingOrRecording()
        {
            yield return Boot();
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            string saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            Assert.IsTrue(flow.StartTestDrive("classic"));
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.Car.inputEnabled = false;
            race.Player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            int ticks = 0;
            double startedAt = RaceClock.Now;
            while (flow.IsTestDrive && ticks++ < 2400)
            {
                // captureDeltaTime speeds physics up; the race clock deliberately follows unscaled wall time.
                RaceClock.Advance(SimulationDriver.FixedStep);
                yield return null;
            }
            Assert.IsFalse(flow.IsTestDrive, "45-second limit must return a stationary player too.");
            Assert.GreaterOrEqual(RaceClock.Now - startedAt, GameFlow.TestDriveSeconds);
            Assert.AreEqual(saved, JsonUtility.ToJson(PlatformGlue.Save.Data));
            Assert.AreEqual(0, flow.LastReward);
        }
    }
}
