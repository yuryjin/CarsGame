using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class NameplatePlayTests
    {
        private float capture;
        private string saved;
        private GameFlow flow;
        private RaceEventDefinition practice;
        private readonly List<MonoBehaviour> cameraScripts = new List<MonoBehaviour>();
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            foreach (var script in cameraScripts) if (script != null) script.enabled = true;
            if (flow != null) flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null) { JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }
        private static List<TextMeshProUGUI> Names()
        {
            var list = new List<TextMeshProUGUI>();
            foreach (var t in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None))
                if (t.name == "RivalName" && t.gameObject.activeInHierarchy) list.Add(t);
            return list;
        }
        private static Rect Bounds(RectTransform rt)
        {
            var c = new Vector3[4]; rt.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
        private static void Check(List<string> failures, string phase, string carrier)
        {
            var names = Names();
            if (names.Count != 5) failures.Add(phase + ": all five nearby rivals must retain a name, got " + names.Count);
            if (!names.Exists(n => n.text == carrier)) failures.Add(phase + ": bomb carrier name missing");
            for (int i = 0; i < names.Count; i++)
            {
                var a = Bounds(names[i].rectTransform);
                if (a.xMin < -0.1f || a.yMin < -0.1f || a.xMax > Screen.width + 0.1f || a.yMax > Screen.height + 0.1f)
                    failures.Add(phase + ": off-screen " + names[i].text);
                if (names[i].fontSize * names[i].rectTransform.lossyScale.y < 11.95f) failures.Add(phase + ": unreadable " + names[i].text);
                for (int j = i + 1; j < names.Count; j++) if (a.Overlaps(Bounds(names[j].rectTransform)))
                    failures.Add(phase + ": overlapping " + names[i].text + "/" + names[j].text);
            }
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator CeilingRiderHealthBarUsesTheCarsGravityDirection()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_e19"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var map = Object.FindAnyObjectByType<StarRingTrack>();
            foreach (var turret in map.Turrets) turret.enabled = false;
            foreach (var participant in race.Participants)
            {
                participant.Car.SetInputSource(participant.gameObject.AddComponent<ScriptedDriverInput>());
                participant.Health.SetInvulnerable(120f);
            }
            var rival = race.Participants[1];
            Assert.IsFalse(rival.isPlayer);
            float distance = map.Layout.TunnelStart + 2f * Core.Track.StarRingLayout.TunnelFlare + 10f;
            var path = TrackPath.Instance;
            Vector3 forward = path.Tangent(distance);
            Vector3 position = path.Position(distance);
            position.y = map.Layout.Height(distance) + StarRingTrack.TubeHeight - .5f;
            rival.Car.Teleport(position, Quaternion.LookRotation(forward, Vector3.down));
            rival.Car.Body.linearVelocity = forward * 25f;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(rival.Car.WallRiding, "The test must actually attach to the ceiling surface");
            Assert.Less(rival.Car.GravityUp.y, -.9f);
            // Stop simulation without disabling the car: its OnDisable intentionally unsticks it.
            var simulation = SimulationDriver.Instance;
            cameraScripts.Add(simulation); simulation.enabled = false;
            rival.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            var cam = Camera.main;
            foreach (var script in cam.GetComponents<MonoBehaviour>())
                if (script.enabled) { cameraScripts.Add(script); script.enabled = false; }
            cam.transform.position = rival.transform.position - forward * 10f + Vector3.down * 3f;
            cam.transform.rotation = Quaternion.LookRotation(rival.transform.position + forward * 3f + Vector3.down - cam.transform.position, Vector3.down);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.Less(rival.Car.GravityUp.y, -.9f, "The projected fixture must remain attached to the roof");
            var bar = GameObject.Find("Bar_" + rival.displayName);
            Assert.NotNull(bar);
            var healthBars = Object.FindAnyObjectByType<WorldHealthBars>();
            Vector2 expected = cam.WorldToScreenPoint(rival.transform.position + rival.Car.GravityUp * healthBars.heightAboveCar);
            Vector2 actual = RectTransformUtility.WorldToScreenPoint(null, bar.transform.position);
            Assert.Less(Vector2.Distance(actual, expected), 1f,
                "The health bar belongs above the car in its gravity frame, inside the tunnel rather than beyond the ceiling");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DensePackKeepsEveryNameAndCarrierReadableAtTheScreenEdge()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(Resources.Load<RaceEventDefinition>("Events/Event_e18"));
            practice.aiCountOverride = 5;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var cam = Camera.main;
            foreach (var script in cam.GetComponents<MonoBehaviour>()) if (script.enabled) { cameraScripts.Add(script); script.enabled = false; }
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1000f, 0f), Quaternion.identity);
            cam.fieldOfView = 56f;
            var rivals = new List<RaceParticipant>();
            foreach (var p in race.Participants)
            {
                // Controlled UI fixture: physics/progress are stopped, not a driving claim.
                p.enabled = false; p.Progress.enabled = false; p.Car.inputEnabled = false; p.Car.enabled = false;
                p.Car.Body.isKinematic = true;
                p.transform.position = new Vector3(1000f, 1000f, 1000f);
                if (!p.isPlayer) rivals.Add(p);
            }
            for (int i = 0; i < 85; i++) yield return null;
            var carrier = rivals[rivals.Count - 1];
            Assert.IsTrue(race.Player.HotBomb.Touch(race.Player, carrier));
            var failures = new List<string>();
            foreach (float x in new[] { Screen.width * 0.5f, 6f, Screen.width - 9f })
            {
                for (int i = 0; i < rivals.Count; i++)
                    rivals[i].transform.position = cam.ScreenToWorldPoint(new Vector3(x + i * 0.6f, Screen.height * 0.48f, 35f + i * 0.6f)) - Vector3.up * 1.9f;
                for (int i = 0; i < 8; i++) yield return null;
                Check(failures, x < 10f ? "left edge" : x > Screen.width - 10f ? "right edge" : "centre", HudScreen.GetLocalizedRacerName(carrier.displayName));
            }
            PauseController.Set(PauseReason.Menu, true); yield return null; yield return null;
            Assert.AreEqual(0, Names().Count, "pause hides world names");
            PauseController.Set(PauseReason.Menu, false); yield return null; yield return null;
            Check(failures, "resume", HudScreen.GetLocalizedRacerName(carrier.displayName));
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
