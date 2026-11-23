using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class PracticePlayTests
    {
        private float capture;
        private string saved;
        private string restoreLanguage;
        private GameFlow flow;
        private RaceManager race;
        private MenuScreens menu;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (restoreLanguage != null && PlatformGlue.Save != null) PlatformGlue.SetLanguageOverride(restoreLanguage);
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private IEnumerator Boot()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            string body = System.Environment.GetEnvironmentVariable("CG_PRACTICE_BODY");
            if (!string.IsNullOrEmpty(body))
            {
                var definition = Core.Economy.CarBodies.Get(body);
                Assert.AreEqual(body, definition.Id, "Practice matrix must select a real body");
                var progress = PlatformGlue.Save.Data.progress;
                if (!progress.cosmetics.Contains(definition.OwnedKey)) progress.cosmetics.Add(definition.OwnedKey);
                progress.body = body;
            }
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            menu = Object.FindAnyObjectByType<MenuScreens>();
            RaceDirector.Instance.Reseed(12345);
        }

        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);

        [UnityTest, Timeout(180000)]
        public IEnumerator ChangingLanguageInPausedPracticeRefreshesThePersistentGoal()
        {
            yield return Boot();
            restoreLanguage = PlatformGlue.Save.Data.settings.lang ?? "";
            PlatformGlue.SetLanguageOverride("en");
            float languageUntil = Time.realtimeSinceStartup + 30f;
            while ((!Loc.Ready || Loc.Language != "en" || PlatformGlue.Save.Data.settings.lang != "en") && Time.realtimeSinceStartup < languageUntil) yield return null;
            Assert.AreEqual("en", Loc.Language);
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            while (race.State == RaceState.Countdown) yield return null;
            yield return null;
            string oldTitle = Button("ReturnToGarage").GetComponentInChildren<TMPro.TMP_Text>().text;
            Button("PauseButton").onClick.Invoke(); yield return null;
            Button("PauseSettings").onClick.Invoke(); yield return null;
            Button("LanguageNext").onClick.Invoke();
            languageUntil = Time.realtimeSinceStartup + 30f;
            while ((!Loc.Ready || Loc.Language != "tr") && Time.realtimeSinceStartup < languageUntil) yield return null;
            yield return null;
            Assert.AreEqual("tr", Loc.Language);
            Button("Back").onClick.Invoke(); yield return null;
            Button("Resume").onClick.Invoke(); yield return null; yield return null;
            string title = Button("ReturnToGarage").GetComponentInChildren<TMPro.TMP_Text>().text;
            Assert.AreNotEqual(oldTitle, title);
            StringAssert.StartsWith(Loc.Get("practice.drift"), title);
            StringAssert.Contains("0/1", title, "Language does not complete or reset the exercise");
            StringAssert.Contains(Loc.Get("practice.return"), title);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ShortcutLessonCompletesThroughARealBridgePassage()
        {
            yield return Boot();
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Shortcut));
            while (race.State == RaceState.Countdown) yield return null;
            var fork = TrackPath.Instance.Fork;
            Assert.NotNull(fork);
            var input = race.Player.gameObject.AddComponent<ScriptedDriverInput>();
            race.Player.Car.SetInputSource(input);
            // Prepared at the entrance, then driven with throttle/steering through the real exit.
            yield return TrackForkPlayTests.DriveBranch(race, fork, input, false);
            yield return null;
            Assert.IsTrue(flow.PracticeCompleted);
            StringAssert.Contains("1/1", Button("ReturnToGarage").GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
            flow.EnterMenu();
            Assert.IsFalse(flow.PracticeCompleted);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LessonMenuRetryPauseAndExitPreserveCampaignAndRestoreThePreviousRace()
        {
            yield return Boot();
            PlatformGlue.Save.Data.settings.hintsSeen = int.MaxValue;
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            var previousEvent = RaceSetup.Event;
            var previousTier = RaceSetup.Tier;
            Assert.NotNull(Button("Practice"));
            Button("Practice").onClick.Invoke(); yield return null;
            Assert.AreEqual("practice", menu.Current);
            foreach (var lesson in new[] { PracticeLesson.Drift, PracticeLesson.Shortcut, PracticeLesson.WallRide, PracticeLesson.Aim })
            {
                Assert.NotNull(Button("Practice" + lesson));
                Button("Practice" + lesson).onClick.Invoke();
                while (race.State == RaceState.Countdown) yield return null;
                yield return null;
                Assert.IsTrue(flow.IsPractice);
                Assert.AreEqual(lesson, flow.CurrentPractice);
                Assert.IsFalse(flow.PracticeCompleted);
                Assert.IsFalse(flow.StartPractice(PracticeLesson.Drift), "A lesson cannot nest another session");
                Assert.AreEqual(lesson == PracticeLesson.Aim ? 2 : 1, race.Participants.Count);
                Assert.AreEqual(lesson == PracticeLesson.WallRide, RaceSetup.Event.starRingTrack);
                Assert.IsFalse(Button("LiveGoals") != null, "Practice cannot award campaign stars");
                var back = Button("ReturnToGarage");
                Assert.NotNull(back);
                PauseController.Set(PauseReason.User, true); yield return null;
                Assert.IsFalse(back.gameObject.activeInHierarchy);
                Button("Hint").onClick.Invoke(); yield return null;
                string help = GameObject.Find("Lessons").GetComponent<TMPro.TMP_Text>().text;
                StringAssert.Contains(Loc.Get("practice.note"), help);
                StringAssert.DoesNotContain(StarGoalText.Description(RaceSetup.Event, 0), help, "Training help must not promise race stars");
                Button("CloseHelp").onClick.Invoke(); yield return null;
                PauseController.Set(PauseReason.User, false); yield return null;
                Assert.IsTrue(back.gameObject.activeInHierarchy);
                // Advance the clock through the old test-drive cutoff without waiting 45 real seconds.
                for (int i = 0; i < 200; i++) RaceClock.Advance(0.25f);
                yield return null;
                Assert.IsTrue(flow.IsPractice, "Training must not inherit the garage's 45-second limit");
                Assert.AreEqual(FlowState.Racing, flow.State);
                flow.RetryRace();
                Assert.AreEqual(lesson, flow.CurrentPractice);
                Assert.IsFalse(flow.PracticeCompleted);
                while (race.State == RaceState.Countdown) yield return null;
                Button("ReturnToGarage").onClick.Invoke(); yield return null;
                Assert.AreEqual("practice", menu.Current);
                Assert.IsFalse(flow.IsTestDrive);
                Assert.AreSame(previousEvent, RaceSetup.Event);
                Assert.AreSame(previousTier, RaceSetup.Tier);
                Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
                Assert.AreEqual(int.MaxValue, PlatformGlue.Save.Data.settings.hintsSeen, "Practice tips cannot consume campaign tips");
            }
            menu.Show("settings"); yield return null;
            Button("ReplayHints").onClick.Invoke();
            Assert.AreEqual(0, PlatformGlue.Save.Data.settings.hintsSeen);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RealDriftCompletesTheExerciseAndRetryRequiresANewDrift()
        {
            yield return Boot();
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            yield return DriveDriftLesson();
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
            flow.RetryRace();
            Assert.IsFalse(flow.PracticeCompleted);
            Assert.AreEqual(0, race.Player.DriftBoosts);
            flow.EnterMenu();
            Assert.AreEqual("practice", menu.Current);
        }

        private IEnumerator DriveDriftLesson()
        {
            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            while (race.State == RaceState.Countdown) yield return null;
            input.Set(1f, 0f);
            for (int i = 0; i < 150; i++) yield return null;
            Debug.Log($"[PracticeDrift] body={car.GetComponent<CarBody>().BodyId} speedAfter3s={car.SpeedKmh:F2}");
            for (int i = 0; i < 300 && car.SpeedKmh < 45f; i++) yield return null;
            Assert.GreaterOrEqual(car.SpeedKmh, 40f, "Wait for the entry speed described in the lesson, including slow heavy bodies");
            input.Set(1f, 1f, true);
            for (int i = 0; i < 100 && car.DriftStage < 1; i++) yield return null;
            Assert.Greater(car.DriftStage, 0, "Release when the first visible stage is ready, before over-holding a light body into a spin");
            Assert.IsFalse(flow.PracticeCompleted, "Holding a drift is not yet the released boost");
            input.Set(1f, 0f);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.Greater(race.Player.DriftBoosts, 0);
            Assert.IsTrue(flow.PracticeCompleted);
            StringAssert.Contains("1/1", Button("ReturnToGarage").GetComponentInChildren<TMPro.TMP_Text>().text);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MissedDriftOpensItsLessonAndRestoresTheAwardedRace()
        {
            yield return Boot();
            var progress = PlatformGlue.Save.Data.progress;
            if (!progress.unlockedEvents.Contains("e2")) progress.unlockedEvents.Add("e2");
            var ev = System.Array.Find(flow.events, e => e.eventId == "e2");
            var tier = System.Array.Find(flow.tiers, t => t.tierId == "hard");
            flow.StartRace(ev, tier);
            while (race.State == RaceState.Countdown) yield return null;
            race.QaFinishNow(); yield return null;
            Assert.AreEqual(0, race.Results.Player.DriftBoosts);
            Assert.AreEqual(0, flow.LastStarsEarned & 2);
            int stars = flow.LastStarsEarned, reward = flow.LastTotalReward;
            Assert.Greater(reward, 0);
            string credited = JsonUtility.ToJson(progress);
            string next = Button("ResultAction0").GetComponentInChildren<TMPro.TMP_Text>().text;
            foreach (bool complete in new[] { false, true })
            {
                var lesson = Button("PracticeMissedGoal");
                Assert.AreEqual(Loc.Format("results.practice_goal", "lesson", Loc.Get("practice.drift")),
                    lesson.GetComponentInChildren<TMPro.TMP_Text>().text);
                lesson.onClick.Invoke(); yield return null;
                Assert.AreEqual(PracticeLesson.Drift, flow.CurrentPractice);
                if (complete) yield return DriveDriftLesson();
                else Assert.IsFalse(flow.PracticeCompleted);
                var back = Button("ReturnToGarage");
                StringAssert.Contains(Loc.Get("practice.return_results"), back.GetComponentInChildren<TMPro.TMP_Text>().text);
                back.onClick.Invoke(); yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.AreEqual("results", menu.Current);
                Assert.AreSame(ev, RaceSetup.Event);
                Assert.AreSame(tier, RaceSetup.Tier);
                Assert.AreEqual(stars, flow.LastStarsEarned);
                Assert.AreEqual(reward, flow.LastTotalReward);
                Assert.AreEqual(credited, JsonUtility.ToJson(progress));
                Assert.AreEqual(next, Button("ResultAction0").GetComponentInChildren<TMPro.TMP_Text>().text);
            }
            Button("ResultAction0").onClick.Invoke();
            Assert.AreEqual(FlowState.Racing, flow.State);
            Assert.IsFalse(flow.IsPractice);
            Assert.AreSame(tier, RaceSetup.Tier);
            Assert.AreEqual(credited, JsonUtility.ToJson(progress));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AimExerciseRequiresARealProjectileHitAndNeverAwardsCampaignProgress()
        {
            yield return Boot();
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Aim));
            while (race.State == RaceState.Countdown) yield return null;
            RaceParticipant target = null;
            foreach (var p in race.Participants)
            {
                p.Car.Teleport(new Vector3(0f, 1000f, p.isPlayer ? 0f : 18f), Quaternion.identity);
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                p.Car.inputEnabled = false;
                p.Progress.enabled = false;
                // Fixed firing range: do not let the off-track recovery return these suspended cars to the road.
                p.Car.enabled = false;
                var driver = p.GetComponent<AiDriver>();
                if (driver != null) driver.enabled = false;
                if (!p.isPlayer) target = p;
            }
            Assert.NotNull(target);
            Assert.IsFalse(target.GetComponent<AiGunner>().enabled, "Practice targets do not shoot back");
            for (int i = 0; i < 15; i++) yield return null;
            Assert.IsFalse(flow.PracticeCompleted);
            var cannon = race.Player.GetComponent<CarCannon>();
            cannon.SetWantsFire(true);
            for (int i = 0; i < 160 && !flow.PracticeCompleted; i++)
            {
                cannon.CommitAim(target.GetComponent<Collider>().bounds.center);
                cannon.SimulationStep(SimulationDriver.Instance.Tick);
                yield return null;
            }
            Assert.Less(target.Health.Health, target.Health.maxHealth, "The real projectile must reach the target");
            Assert.IsTrue(flow.PracticeCompleted);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
            flow.EnterMenu();
            Assert.AreEqual("practice", menu.Current);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator PracticeKeepsItsRailsAndDoesNotFinishAtTheHiddenLapLimit()
        {
            yield return Boot();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.WallRide));
            while (race.State == RaceState.Countdown) yield return null;
            var map = Object.FindAnyObjectByType<StarRingTrack>();
            Assert.AreEqual(0, map.Turrets.Count, "A driving lesson has no hostile turrets");
            var path = TrackPath.Instance;
            var progress = race.Player.Progress;
            float distance = path.startLineDistance + 1f;
            progress.transform.position = path.Position(distance);
            progress.BeginRace();
            int steps = Mathf.CeilToInt(path.Length / 5f);
            // Exercise the lap event pipeline; these samples do not claim a physically driven lap.
            for (int lap = 0; lap < 21; lap++) for (int i = 0; i < steps; i++)
            {
                distance += path.Length / steps;
                progress.transform.position = path.Position(distance);
                progress.SimulationStep(lap * steps + i);
            }
            Assert.GreaterOrEqual(progress.LapsCompleted, 20);
            Assert.AreEqual(RaceState.Racing, race.State);
            Assert.AreEqual(1, map.Stage);
            Assert.IsTrue(map.transform.Find("Rails").gameObject.activeSelf);
            flow.EnterMenu();
            var campaign = System.Array.Find(flow.events, e => e != null && e.starRingTrack);
            // Bypass campaign locks only inside this isolated fixture.
            PlatformGlue.Save.Data.progress.bests.Add(new RacingGame.Core.Save.EventBest { eventId = campaign.unlockAfterEventId, stars = 1 });
            flow.StartRace(campaign, null);
            Assert.IsFalse(RaceSetup.Event.IsPractice);
            Assert.Greater(Object.FindAnyObjectByType<StarRingTrack>().Turrets.Count, 0, "Normal races still have turrets");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CeilingExerciseCanBeReachedByDrivingFromTheFloor()
        {
            yield return Boot();
            bool reached = false;
            string sweep = System.Environment.GetEnvironmentVariable("CG_CLIMB_SWEEP");
            // Distinct body classes need different driving approaches, not different game physics.
            (float start, float lane, float aim, float speed, float across, bool early)[] routes = sweep == "2"
                ? new[] { (20f,0f,1.75f,16f,2f,false), (20f,0f,1.75f,22f,3f,false), (20f,3.4f,1.75f,16f,2f,false),
                    (20f,3.4f,3f,20f,3f,false), (15f,3.4f,1.75f,16f,2f,false),
                    (20f,3.4f,1f,22f,2f,false), (20f,0f,1.5f,22f,1.25f,true),
                    (10f,0f,.75f,22f,1f,true), (20f,0f,1f,22f,1f,true), (0f,0f,1f,22f,1f,true),
                    (10f,0f,1f,20f,.75f,true), (0f,0f,.75f,22f,.75f,true) }
                : new[] { (20f,0f,1.75f,16f,2f,false) };
            foreach (var route in routes)
            {
                Assert.IsTrue(flow.StartPractice(PracticeLesson.WallRide));
                while (race.State == RaceState.Countdown) yield return null;
                var map = Object.FindAnyObjectByType<StarRingTrack>();
                var path = TrackPath.Instance;
                var car = race.Player.Car;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                race.Player.Health.SetInvulnerable(600f);
                float startOffset = route.start, across = route.across;
                var entry = new Vector3(route.lane, route.aim, route.speed);
                float start = map.Layout.TunnelStart + startOffset;
                var approach = (path.Tangent(start) + path.Right(start) * entry.y).normalized;
                car.Teleport(path.Position(start) + path.Right(start) * entry.x + Vector3.up * 0.55f, Quaternion.LookRotation(approach));
                car.Body.linearVelocity = approach * entry.z;
                Physics.SyncTransforms();
                float maxHeight = 0f, minUp = 1f, closestPad = float.PositiveInfinity;
                Vector3 closestPosition = Vector3.zero;
                var pad = map.transform.Find("CeilingBoostPad");
                string tracePath = System.Environment.GetEnvironmentVariable("CG_PRACTICE_TRACE");
                var trace = string.IsNullOrEmpty(tracePath) ? null : new System.Text.StringBuilder("tick,distance,lane,height,upY,gravityY,speed,wallRiding,grounded,angle,yaw,steer,padX,padY,padZ\n");
                bool onRoof = false;
                for (int tick = 0; tick < 350; tick++)
                {
                    float d = race.Player.Progress.Distance;
                    var along = path.Tangent(d);
                    var up = car.GravityUp;
                    var cross = Vector3.Cross(up, along).normalized;
                    // Climb the right side, then straighten as the car approaches the roof centre.
                    float lane = Vector3.Dot(car.transform.position - path.Position(d), path.Right(d));
                    onRoof |= car.transform.up.y < (route.early ? 0f : -0.92f);
                    float lateralSpeed = Vector3.Dot(car.Body.linearVelocity, path.Right(d));
                    float climb = onRoof ? Mathf.Clamp(lane * (route.early ? .15f : .65f) + lateralSpeed * (route.early ? .1f : .2f), -across, across) : across;
                    var heading = Vector3.ProjectOnPlane(along + cross * climb, up);
                    float angle = Vector3.SignedAngle(car.transform.forward, heading, up);
                    float yaw = Vector3.Dot(car.Body.angularVelocity, up);
                    input.Set(car.SpeedKmh < 90f ? 1f : 0f, Mathf.Clamp(angle / 30f - yaw * .5f, -1f, 1f));
                    yield return null;
                    if (trace != null)
                    {
                        Vector3 local = pad.InverseTransformPoint(car.Body.worldCenterOfMass);
                        trace.AppendLine(System.FormattableString.Invariant($"{tick},{race.Player.Progress.Distance:F3},{lane:F3},{car.transform.position.y:F3},{car.transform.up.y:F3},{car.GravityUp.y:F3},{car.SpeedKmh:F3},{car.WallRiding},{car.IsGrounded},{angle:F3},{yaw:F3},{input.steer:F3},{local.x:F3},{local.y:F3},{local.z:F3}"));
                    }
                    maxHeight = Mathf.Max(maxHeight, car.transform.position.y);
                    minUp = Mathf.Min(minUp, car.transform.up.y);
                    float padDistance = Vector3.Distance(car.Body.worldCenterOfMass, pad.position);
                    if (padDistance < closestPad) { closestPad = padDistance; closestPosition = new Vector3(race.Player.Progress.Distance, car.transform.position.y, lane); }
                    if (flow.PracticeCompleted) { reached = true; break; }
                    if (race.Player.Progress.Distance > map.Layout.TunnelEnd - 10f) break;
                }
                Debug.Log($"[PracticeClimb] start={startOffset} entry={entry} maxHeight={maxHeight:F2} minUp={minUp:F2} closestPad={closestPad:F2} at={closestPosition} distance={race.Player.Progress.Distance:F2} ceilingBoosts={race.Player.CeilingBoosts} completed={reached}");
                if (trace != null) System.IO.File.WriteAllText(tracePath, trace.ToString());
                if (reached) break;
                flow.EnterMenu();
                yield return null; yield return null;
            }
            Assert.IsTrue(reached, "A real approach from the floor must reach the ceiling pad; placing the car upside down is not enough");
        }
    }
}
