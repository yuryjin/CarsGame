using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class RooftopPlayTests
    {
        private float capture;
        private GameFlow flow;
        private string saved;
        [SetUp]
        public void Setup()
        {
            capture = Time.captureDeltaTime;
        }
        [TearDown]
        public void Cleanup()
        {
            if (flow != null)
                flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(360000)]
        public IEnumerator RooftopJumpLowerRoadAndShortLandingReturnAllLeadToALegalFinish()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var normal = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var source = System.Array.Find(flow.events, e => e.eventId == "roofs");
            Assert.NotNull(source);
            var ev = Object.Instantiate(source);
            ev.aiCountOverride = 0;
            ev.pickupCount = 0;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            var levels = PlatformGlue.Save.Data.progress.upgrades;
            System.Array.Clear(levels, 0, levels.Length);
            foreach (string mode in new[] { "lower", "jump", "short" })
            {
                RaceDirector.Instance.Reseed(12345);
                race.spawner.playerBodyId = "classic";
                flow.StartRace(ev, normal);
                var player = race.Player;
                var car = player.Car;
                var path = player.Progress.Path;
                var fork = path.Fork;
                var ai = player.gameObject.AddComponent<AiDriver>();
                ai.speedMultiplier = mode == "lower" ? 1f : .55f;
                ai.Initialize(null, 0f);
                var input = player.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(ai);
                Assert.AreEqual("classic", player.GetComponent<CarBody>().BodyId);
                Assert.AreEqual(MountainFork.Rooftops, fork.Course);
                Assert.IsNotNull(fork.transform.Find("RoofLandingMarks_0"));
                while (race.State == RaceState.Countdown)
                    yield return null;
                bool usingFork = false, crossed = false, landingSeen = false, landed = false, safeReturn = false;
                bool capturedApproach = false;
                int flight = 0, maxFlight = 0, steps = 0, gate = player.Progress.GatesPassedThisLap, gates = 0;
                int startTick = SimulationDriver.Instance.Tick;
                float lipSpeed = 0f;
                int lastBin = -10;
                bool trace = System.Environment.GetEnvironmentVariable("CG_ROOF_TRACE") == "1";
                var landing = fork.Position(fork.LandingFraction + .015f);
                var previousPosition = car.transform.position;
                for (; steps < 9000 && player.Progress.LapsCompleted == 0; steps++)
                {
                    float along = path.SignedDelta(fork.ApproachDistance, player.Progress.Distance);
                    float t = along / fork.MainSpan;
                    int bin = Mathf.FloorToInt(t * 20f);
                    if (trace && mode != "lower" && !crossed && bin >= -1 && bin <= 19 && bin != lastBin)
                    {
                        lastBin = bin;
                        float deckDistance = fork.Project(car.transform.position, out var error);
                        float deckFraction = path.SignedDelta(fork.ApproachDistance, deckDistance) / fork.MainSpan;
                        Debug.Log($"[RooftopTrace] {mode} d={t:F3} deck={deckFraction:F3} offset={Mathf.Sqrt(error):F2}m speed={car.SpeedKmh:F1} grounded={car.IsGrounded} reset={player.AutoResets} position={car.transform.position}");
                    }
                    if (player.AutoResets > 0 && !safeReturn)
                    {
                        safeReturn = true;
                        if (mode == "short")
                        {
                            float lastDistance = fork.Project(previousPosition, out _);
                            float lastFraction = path.SignedDelta(fork.ApproachDistance, lastDistance) / fork.MainSpan;
                            float lowerRoof = Mathf.Min(fork.Position(fork.LaunchFraction).y, fork.Position(fork.LandingFraction).y);
                            Assert.That(lastFraction, Is.InRange(fork.LaunchFraction - .045f, fork.LandingFraction + .045f));
                            Assert.Less(previousPosition.y, lowerRoof - .65f);
                            Debug.Log($"[RoofRecovery] drop={lowerRoof - previousPosition.y:F2}m fraction={lastFraction:F3} return={car.transform.position}");
                        }
                        Assert.IsFalse(fork.UnderRoofGap(car.transform.position));
                        Assert.Less(Mathf.Abs(path.Spline.LateralOffset(car.transform.position, player.Progress.Distance)), path.halfWidth + 1f);
                        crossed = true;
                    }
                    if (mode != "lower" && !crossed && along >= -27f && along < fork.MainSpan + 2f)
                    {
                        if (!usingFork)
                        {
                            usingFork = true;
                            car.SetInputSource(input);
                        }
                        fork.FollowShortcut(car, player.Progress.Distance, out var steer, out var throttle);
                        if (mode == "short" && t >= .32f && t < fork.LandingFraction + .02f)
                            throttle = Mathf.Clamp((5f - car.SpeedKmh) / 2f, -1f, 1f);
                        input.Set(throttle, steer);
                        if (!landingSeen && mode == "jump" && t >= fork.LaunchFraction - .085f && t <= fork.LaunchFraction)
                        {
                            var cam = Camera.main;
                            var viewport = cam.WorldToViewportPoint(landing + Vector3.up * .1f);
                            bool clearView = !Physics.Linecast(cam.transform.position, landing + Vector3.up * .12f,
                                out var occluder, LayerMask.GetMask("Track", "Ground"), QueryTriggerInteraction.Ignore);
                            if (trace && !capturedApproach && t > fork.LaunchFraction - .03f)
                            {
                                capturedApproach = true;
                                Debug.Log($"[RoofView] camera={cam.transform.position} landing={landing} viewport={viewport} surface={occluder.collider?.name}");
                                CaptureLanding(cam, "-approach");
                            }
                            if (clearView && viewport.z > 0f && viewport.x > .03f && viewport.x < .97f && viewport.y > .03f && viewport.y < .97f)
                            {
                                landingSeen = true;
                                CaptureLanding(cam);
                            }
                        }
                        if (t >= fork.LaunchFraction - .035f && t < fork.LandingFraction + .1f)
                        {
                            if (car.IsGrounded && t <= fork.LaunchFraction) lipSpeed = car.SpeedKmh;
                            if (!car.IsGrounded)
                            {
                                flight++;
                                maxFlight = Mathf.Max(maxFlight, flight);
                                if (mode == "jump" && flight == 6)
                                    CaptureLanding(Camera.main, "-air");
                            }
                            else if (flight != 0)
                            {
                                if (t >= fork.LandingFraction && car.Surface == GroundSurface.Track &&
                                    Physics.Raycast(car.transform.position + Vector3.up * .6f, Vector3.down, out var contact, 1.5f,
                                        LayerMask.GetMask("Track"), QueryTriggerInteraction.Ignore) && contact.collider.transform.IsChildOf(fork.transform))
                                    landed = true;
                                flight = 0;
                            }
                        }
                        if (t >= 1f)
                            crossed = true;
                    }
                    else if (usingFork)
                    {
                        usingFork = false;
                        car.SetInputSource(ai);
                    }
                    if (player.Progress.GatesPassedThisLap != gate)
                    {
                        gate = player.Progress.GatesPassedThisLap;
                        gates++;
                    }
                    previousPosition = car.transform.position;
                    yield return null;
                }
                Debug.Log($"[Rooftops] mode={mode} flightTicks={maxFlight} lipSpeed={lipSpeed:F1}km/h physics={(SimulationDriver.Instance.Tick - startTick) * SimulationDriver.FixedStep:F2}s landingVisible={landingSeen} landed={landed} safeReturn={safeReturn} resets={player.AutoResets} gates={gates} lap={player.Progress.LastLapTime:F2}s d={player.Progress.Distance:F1}");
                Assert.AreEqual(1, player.Progress.LapsCompleted, mode);
                Assert.GreaterOrEqual(gates, player.Progress.gateCount - 1);
                for (int frame = 0; frame < 8 && flow.State == FlowState.Racing; frame++)
                    yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.IsTrue(race.Results.Player.Finished && race.Results.Player.Laps == 1, mode);
                if (mode == "short")
                {
                    Assert.IsTrue(safeReturn);
                    Assert.AreEqual(1, player.AutoResets);
                }
                else
                    Assert.AreEqual(0, player.Resets);
                if (mode == "jump")
                {
                    Assert.Greater(maxFlight, 4);
                    Assert.IsTrue(landed && landingSeen);
                    Assert.AreEqual(1, player.Shortcuts);
                }
                if (mode == "lower")
                    Assert.AreEqual(0, player.Shortcuts);
                flow.EnterMenu();
                Assert.IsNull(path.RespawnGuard);
                yield return null;
            }
            Object.Destroy(ev);
        }

        private static void CaptureLanding(Camera camera, string suffix = "")
        {
            string file = System.Environment.GetEnvironmentVariable("CG_ROOF_LANDING_SHOT");
            if (string.IsNullOrEmpty(file))
                return;
            if (suffix.Length > 0)
                file = System.IO.Path.ChangeExtension(file, null) + suffix + ".png";
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            var target = new RenderTexture(960, 540, 24);
            var pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(file, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }
    }
}
