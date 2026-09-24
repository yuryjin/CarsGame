using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.World;

namespace RacingGame.Tests
{
    public sealed class SpaceTrackPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator GatesBoostCarsAndRestoreTheWorld()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var mountains = GameObject.Find("Environment/Mountains");
            var ground = GameObject.Find("Ground_Terrain");
            var ev = Resources.Load<RaceEventDefinition>("Events/Event_e17");
            Assert.IsNotNull(ev);
            flow.StartRace(ev, null);
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.IsFalse(mountains.activeSelf);
            Assert.IsFalse(ground.activeSelf);
            Assert.AreEqual(ev.sky, SkyDirector.Instance.Current);
            var gates = Object.FindObjectsByType<BoostGate>(FindObjectsInactive.Exclude);
            Assert.AreEqual(6, gates.Length);
            while (race.State == RaceState.Countdown) yield return null;
            var gate = gates[0];
            var car = race.Player.Car;
            car.SetInputSource(car.gameObject.AddComponent<ScriptedDriverInput>());
            car.Body.position = gate.transform.position - gate.transform.forward * 7f - Vector3.up * 2.5f;
            car.Body.rotation = gate.transform.rotation;
            car.Body.linearVelocity = gate.transform.forward * 20f;
            race.Player.Progress.NotifyTeleport();
            Physics.SyncTransforms();
            for (int i = 0; i < 30 && !car.IsBoosting; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(car.IsBoosting, "driving through the aperture enables nitro");
            Assert.Greater(Vector3.Dot(car.Body.linearVelocity, gate.transform.forward), 22f, "gate gives an immediate speed impulse");
            var before = car.Body.linearVelocity;
            gate.SendMessage("OnTriggerEnter", car.GetComponentInChildren<Collider>());
            Assert.AreEqual(before, car.Body.linearVelocity, "compound colliders cannot multiply the impulse");

            CarController rival = null;
            foreach (var participant in race.Participants) if (!participant.isPlayer) { rival = participant.Car; break; }
            Assert.IsNotNull(rival);
            rival.SetInputSource(rival.gameObject.AddComponent<ScriptedDriverInput>());
            var collider = rival.GetComponentInChildren<Collider>();
            rival.Body.position = gate.transform.position;
            rival.Body.linearVelocity = -gate.transform.forward * 20f;
            gate.SendMessage("OnTriggerEnter", collider);
            Assert.AreEqual(-gate.transform.forward * 20f, rival.Body.linearVelocity, "reverse passage gives no impulse");
            rival.Body.position = gate.transform.position + gate.transform.right * 7f;
            rival.Body.linearVelocity = gate.transform.forward * 20f;
            gate.SendMessage("OnTriggerEnter", collider);
            Assert.AreEqual(gate.transform.forward * 20f, rival.Body.linearVelocity, "outside the round aperture gives no impulse");
            rival.Body.position = gate.transform.position;
            gate.SendMessage("OnTriggerEnter", collider);
            Assert.Greater(Vector3.Dot(rival.Body.linearVelocity, gate.transform.forward), 25f, "AI receives the same boost");

            var path = TrackPath.Instance;
            var progress = race.Player.Progress;
            float distance = path.startLineDistance + 1f;
            progress.transform.position = path.Position(distance);
            progress.BeginRace();
            int steps = Mathf.CeilToInt(path.Length / 5f);
            for (int i = 0; i < steps * 2; i++)
            {
                distance += path.Length / steps;
                progress.transform.position = path.Position(distance);
                progress.SimulationStep(i);
            }
            Assert.AreEqual(2, progress.Counter.LapsCompleted);
            Assert.AreEqual(AbyssTrack.WideHalfWidth, path.halfWidth, "space road never narrows");
            flow.RetryRace();
            Assert.AreEqual(6, Object.FindObjectsByType<BoostGate>(FindObjectsInactive.Exclude).Length, "retry has one set of gates");
            flow.EnterMenu();
            Assert.AreEqual(0, Object.FindObjectsByType<BoostGate>(FindObjectsInactive.Exclude).Length);
            Assert.IsTrue(mountains.activeSelf);
            Assert.IsTrue(ground.activeSelf);
            Assert.AreEqual(SkyDirector.Instance.defaultPreset, SkyDirector.Instance.Current);
        }
    }
}
