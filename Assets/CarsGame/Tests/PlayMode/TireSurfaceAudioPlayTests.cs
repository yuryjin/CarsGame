using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class TireSurfaceAudioPlayTests
    {
        // The circuit and the desert are separate maps. Put one strip of desert surface across the
        // normal bridge entry to record both wheel crossings without cuts or teleporting the car.
        private static Mesh SandAcrossEntry(TrackFork fork)
        {
            float bridge = -1f;
            int layer = 1 << LayerMask.NameToLayer("Track");
            for (int i = 1; i < 75; i++)
            {
                Vector3 point = fork.Position(i / 100f);
                if (Physics.Raycast(point + Vector3.up, Vector3.down, out var hit, 3f, layer)
                    && hit.collider.TryGetComponent(out TireAudioSurface marker) && marker.surface == TireSurface.Bridge)
                { bridge = i / 100f; break; }
            }
            Assert.Greater(bridge, 0f, "A production bridge collider is reachable");
            float end = Mathf.Clamp(bridge + .10f, .30f, .75f), start = end - .18f;
            const int sections = 16;
            var vertices = new Vector3[(sections + 1) * 2];
            var triangles = new int[sections * 6];
            for (int j = 0; j <= sections; j++)
            {
                float t = Mathf.Lerp(start, end, j / (float)sections);
                Vector3 middle = fork.Position(t) + Vector3.up * .05f;
                Vector3 side = Vector3.Cross(Vector3.up, fork.Position(t + .004f) - fork.Position(t - .004f)).normalized * 2.65f;
                int k = j * 2;
                vertices[k] = middle - side; vertices[k + 1] = middle + side;
                if (j == sections) continue;
                int n = j * 6;
                triangles[n] = k; triangles[n + 1] = k + 2; triangles[n + 2] = k + 1;
                triangles[n + 3] = k + 1; triangles[n + 4] = k + 2; triangles[n + 5] = k + 3;
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles };
            var cover = new GameObject("QA_SandAcrossEntry");
            cover.transform.SetParent(fork.transform, false);
            cover.layer = LayerMask.NameToLayer("Track");
            cover.AddComponent<MeshCollider>().sharedMesh = mesh;
            cover.AddComponent<TireAudioSurface>().surface = TireSurface.Sand;
            Physics.SyncTransforms();
            return mesh;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator PhysicalWheelPairsCrossFromRoadOverSandOntoTheDeck()
        {
            float clock = Time.captureDeltaTime;
            GameFlow flow = null;
            Mesh strip = null;
            RaceEventDefinition ev = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                ev = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                ev.aiCountOverride = 0;
                flow.StartRace(ev, flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                var car = race.Player.Car;
                var fork = race.Player.Progress.Path.Fork;
                strip = SandAcrossEntry(fork);
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                var a = car.transform.Find("Audio_Asphalt").GetComponent<AudioSource>();
                var s = car.transform.Find("Audio_Sand").GetComponent<AudioSource>();
                var b = car.transform.Find("Audio_Bridge").GetComponent<AudioSource>();
                using (var capture = new AgentAudioCapture("tire-crossings"))
                {
                    if (capture.Active) { capture.Solo(a, s, b); Time.captureDeltaTime = 0f; }
                    int phase = 0;
                    bool blend1 = false, blend2 = false;
                    var drive = TrackForkPlayTests.DriveBranch(race, fork, input, false);
                    while (phase < 5 && drive.MoveNext())
                    {
                        if (capture.Active && Time.frameCount % 7 == 0) capture.Solo(a, s, b);
                        var wheels = car.TireContactMix();
                        bool reached = phase == 0 ? wheels.x >= .75f && a.volume > .004f
                            : phase == 1 ? wheels.x > 0 && wheels.y > 0
                            : phase == 2 ? wheels.y >= .75f && s.volume > .015f
                            : phase == 3 ? wheels.y > 0 && wheels.z > 0
                            : wheels.z >= .75f && b.volume > .025f;
                        if (phase > 0 && phase < 4 && s.volume > .003f && a.volume > .003f) blend1 = true;
                        if (phase > 2 && s.volume > .003f && b.volume > .003f) blend2 = true;
                        if (reached)
                        {
                            string mark = new[] { "asphalt", "asphalt-sand-boundary", "sand", "sand-deck-boundary", "bridge" }[phase++];
                            capture.Mark(mark);
                            Debug.Log($"[TireCrossing] {mark} wheels={wheels} volume=({a.volume:F3},{s.volume:F3},{b.volume:F3}) position={car.transform.position}");
                        }
                        int step = SimulationDriver.Instance.Tick;
                        do { yield return drive.Current; } while (step == SimulationDriver.Instance.Tick);
                    }
                    Assert.AreEqual(5, phase);
                    Assert.IsTrue(blend1 && blend2, "The outgoing surface must still sound as the incoming one fades up");
                    if (capture.Active)
                    {
                        float end = Time.realtimeSinceStartup + 1.4f;
                        while (Time.realtimeSinceStartup < end && drive.MoveNext()) yield return drive.Current;
                    }
                }
                flow.EnterMenu(); yield return null;
                flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m3"), flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                for (int i = 0; i < 25; i++) yield return null;
                Assert.Greater(race.Player.Car.TireContactMix().y, .7f, "Real desert terrain emits sand under the tanker escort car");
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                if (strip != null) Object.Destroy(strip);
                if (ev != null) Object.Destroy(ev);
                Time.captureDeltaTime = clock;
            }
        }
    }
}
