using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public sealed class TrackForkPlayTests
    {
        private float savedCapture;
        [SetUp] public void Setup() { savedCapture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup() { Time.captureDeltaTime = savedCapture; }

        private static void CaptureOverview()
        {
            string output = System.Environment.GetEnvironmentVariable("CG_FORK_SHOT");
            if (string.IsNullOrEmpty(output)) return;
            var cameraObject = new GameObject("ForkOverview", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(65f, 120f, 5f);
            camera.transform.LookAt(new Vector3(65f, 0f, 85f));
            camera.fieldOfView = 60f;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(output, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
                Object.Destroy(cameraObject);
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator BothDirectionsHaveDriveableShortcutValidLapsAndRepair()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var path = TrackPath.Instance;
            RaceEventDefinition firstEvent = null;
            foreach (var candidate in flow.events) if (candidate != null && candidate.eventId == "e1") firstEvent = candidate;
            Assert.IsNotNull(firstEvent);
            var ev = Object.Instantiate(firstEvent);
            ev.aiCountOverride = 0;
            ev.laps = 3;
            DifficultyTier normal = null;
            foreach (var tier in flow.tiers) if (tier != null && tier.tierId == "normal") normal = tier;
            int trackMask = 1 << LayerMask.NameToLayer("Track");
            foreach (bool reverse in new[] { false, true })
            {
                ev.reversed = reverse;
                flow.StartRace(ev, normal);
                var fork = path.Fork;
                Assert.IsNotNull(fork);
                Assert.Less(fork.Length, (TrackFork.Exit - TrackFork.Entry) * 0.85f, "a meaningful shortcut");
                Debug.Log($"[TrackFork] bridge={fork.Length:F1}m main={TrackFork.Exit - TrackFork.Entry:F1}m reverse={reverse}");
                if (!reverse) CaptureOverview();
                Physics.SyncTransforms();
                for (int i = 1; i < 100; i++)
                {
                    Vector3 p = fork.Position(i / 100f);
                    Assert.IsTrue(Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 0.8f, trackMask), $"road at {i}%");
                    Assert.AreEqual(fork.transform, hit.transform.parent, $"baked scenery blocks bridge at {i}%: {hit.collider.name}");
                    Assert.Greater(hit.normal.y, 0.9f, "road faces up and has a driveable grade");
                    foreach (var obstacle in Physics.OverlapBox(p + Vector3.up * 0.8f, new Vector3(0.8f, 0.5f, 0.8f), Quaternion.identity,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        Assert.IsTrue(obstacle.transform.IsChildOf(fork.transform), $"obstacle at {i}%: {obstacle.name}");
                }

                // Drive the whole lap through the branch using small physical-position samples.
                // No gate skipping or teleport exemptions: the standard counter must accept both directions.
                var counter = new LapCounter(path.Length, path.startLineDistance, 12, 6f);
                float start = path.startLineDistance + 1f;
                counter.Begin(start);
                int hint = -1;
                int raw = -1;
                float entry = path.Spline.Project(fork.Position(reverse ? 1f : 0f), ref raw, 0);
                raw = -1;
                float exit = path.Spline.Project(fork.Position(reverse ? 0f : 1f), ref raw, 0);
                for (float d = start + 0.5f; d < entry; d += 0.5f) counter.Step(path.Project(path.Position(d), ref hint));
                float previous = entry;
                for (int i = 0; i <= 1000; i++)
                {
                    float t = i / 1000f;
                    float d = path.Project(fork.Position(reverse ? 1f - t : t) + Vector3.up * 0.4f, ref hint);
                    float delta = path.SignedDelta(previous, d);
                    Assert.That(delta, Is.InRange(-0.1f, 6f), $"continuous forward progress at {i}: {delta}");
                    counter.Step(d);
                    previous = d;
                }
                for (float d = exit + 0.5f; d <= path.Length + start + 1f; d += 0.5f)
                    counter.Step(path.Project(path.Position(d), ref hint));
                Assert.AreEqual(1, counter.LapsCompleted, "shortcut completes a legal lap");
                counter.Begin(start);
                hint = -1;
                for (float d = start + 0.5f; d <= path.Length + start + 1f; d += 0.5f)
                    counter.Step(path.Project(path.Position(d) + Vector3.up * 0.4f, ref hint));
                Assert.AreEqual(1, counter.LapsCompleted, "the long route still completes a legal lap");

                var car = race.Player.Car;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                while (race.State == RaceState.Countdown) yield return null;
                float first = reverse ? 1f : 0f;
                Vector3 facing = fork.Position(reverse ? 0.98f : 0.02f) - fork.Position(first);
                facing.y = 0f;
                car.Teleport(fork.Position(first) + Vector3.up * 0.5f, Quaternion.LookRotation(facing));
                input.Set(0f, 0f);
                for (int i = 0; i < 25; i++) yield return null;
                int farthest = 0;
                int ticks = 0;
                for (; ticks < 1500 && farthest < 198; ticks++)
                {
                    int best = farthest;
                    float error = float.MaxValue;
                    for (int i = Mathf.Max(0, farthest - 3); i <= Mathf.Min(200, farthest + 20); i++)
                    {
                        float t = i / 200f;
                        float e = (car.transform.position - fork.Position(reverse ? 1f - t : t)).sqrMagnitude;
                        if (e < error) { error = e; best = i; }
                    }
                    farthest = Mathf.Max(farthest, best);
                    float aim = Mathf.Min(1f, (best + 10) / 200f);
                    Vector3 target = fork.Position(reverse ? 1f - aim : aim) - car.transform.position;
                    target.y = 0f;
                    float angle = Vector3.SignedAngle(car.transform.forward, target, Vector3.up);
                    Vector3 Route(float t) => fork.Position(reverse ? 1f - Mathf.Clamp01(t) : Mathf.Clamp01(t));
                    float fraction = best / 200f;
                    Vector3 p0 = Route(fraction - 0.015f), p1 = Route(fraction), p2 = Route(fraction + 0.015f);
                    Vector3 tangent = (p2 - p0).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                    float curvature = best > 3 && best < 197
                        ? Vector3.SignedAngle(p1 - p0, p2 - p1, Vector3.up) * Mathf.Deg2Rad / (Vector3.Distance(p0, p2) * 0.5f) : 0f;
                    float speed = car.Body.linearVelocity.magnitude;
                    float feed = car.SteerForCurvature(curvature, speed);
                    float lateral = Vector3.Dot(p1 - car.transform.position, right) * 0.6f - Vector3.Dot(car.Body.linearVelocity, right) * 0.15f;
                    float correction = angle - target.magnitude * curvature * 0.5f * Mathf.Rad2Deg;
                    float steer = Mathf.Clamp(feed + lateral + correction / 35f - (car.Body.angularVelocity.y - speed * curvature) * 0.7f, -1f, 1f);
                    float desired = Mathf.Min(45f, car.MaxSpeedForCurvature(curvature) * 3.6f * 0.85f);
                    input.Set(car.SpeedKmh < desired ? 1f : -0.4f, steer);
                    yield return null;
                    Assert.AreEqual(0, race.Player.AutoResets, $"bridge recovery at sample={best} tick={ticks} error={Mathf.Sqrt(error):F2}");
                }
                Debug.Log($"[TrackFork] drive reverse={reverse} ticks={ticks} reached={farthest}/200 pos={car.transform.position}");
                Assert.GreaterOrEqual(farthest, 198, "car physically reaches the other end");
                Assert.AreEqual(0, race.Player.Deaths);

                // Missing the bridge costs time but does not strand the car or jump its progress to another bend.
                Vector3 middle = fork.Position(0.5f);
                hint = -1;
                float bridgeProgress = path.Project(middle, ref hint);
                hint = -1;
                float belowProgress = path.Project(new Vector3(middle.x, 0f, middle.z), ref hint);
                Assert.Less(Mathf.Abs(path.SignedDelta(bridgeProgress, belowProgress)), 2f);
                Assert.IsFalse(path.OnFork(new Vector3(middle.x, 0f, middle.z)), "grass below is not the bridge");
                Vector3 offBridge = Vector3.Cross(Vector3.up, fork.Position(0.52f) - fork.Position(0.48f)).normalized;
                car.Teleport(middle + offBridge * 7f + Vector3.up, Quaternion.identity);
                input.Set(0f, 0f);
                for (int i = 0; i < 425; i++) yield return null;
                Assert.Greater(race.Player.AutoResets, 0, "fall recovery returns to the main road");
                Assert.AreEqual(GroundSurface.Track, car.Surface);

                Pickup repair = null;
                foreach (var pickup in Object.FindObjectsByType<Pickup>())
                    if (pickup.kind == PickupKind.Health && Vector3.Distance(pickup.transform.position, path.Position(fork.RepairDistance)) < 3f) repair = pickup;
                Assert.IsNotNull(repair, "safe route has repair");
                race.Player.Health.TakeDamage(50f);
                float before = race.Player.Health.Health;
                repair.OnTriggerEnter(car.GetComponent<Collider>());
                Assert.Greater(race.Player.Health.Health, before);
                flow.EnterMenu();
                Assert.IsNull(path.Fork);
                Assert.IsFalse(fork.gameObject.activeSelf);
                yield return null;
            }
            Object.Destroy(ev);
        }
    }
}
