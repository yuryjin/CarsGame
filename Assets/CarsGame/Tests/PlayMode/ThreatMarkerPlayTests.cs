using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class ThreatMarkerPlayTests
    {
        private GameFlow flow;
        private float capture;

        [SetUp]
        public void SetUp()
        {
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
        }

        [TearDown]
        public void TearDown()
        {
            if (flow != null) flow.EnterMenu();
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator MissionAllyHasNoThreatArrowOrReticleLock() => CheckCar("m1", Team.Players);

        [UnityTest, Timeout(120000)]
        public IEnumerator RaceRivalShowsAThreatArrowAndReticleLock() => CheckCar("e1", Team.None);

        private IEnumerator CheckCar(string eventId, Team team)
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;

            flow = Object.FindAnyObjectByType<GameFlow>();
            flow.StartRace(flow.events.Single(e => e.eventId == eventId), flow.tiers.First(t => t.tierId == "normal"));
            var race = flow.GetComponent<RaceManager>();
            var hud = race.GetComponent<HudScreen>();
            while (race.State == RaceState.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State);
            var player = race.Player.Car;
            var other = race.Participants.First(p => !p.isPlayer).Car;
            player.Weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            Assert.AreEqual(team, Teams.Of(player.gameObject));
            Assert.AreEqual(team, Teams.Of(other.gameObject));

            // Hold the comparison poses after GO; the live camera, aiming and threat displays continue updating.
            Vector3 origin = player.transform.position;
            int slot = 0;
            foreach (var p in race.Participants)
            {
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                if (!p.isPlayer) p.Car.Teleport(origin + player.transform.right * (110f + 8f * slot++), player.transform.rotation);
            }
            var markers = GameObject.Find("ThreatMarkers").transform;
            yield return null;
            yield return null;
            Assert.AreEqual(0, markers.Cast<Transform>().Count(t => t.gameObject.activeInHierarchy));

            other.Teleport(origin + player.transform.right * 18f, player.transform.rotation);
            for (int frame = 0; frame < 8; frame++) yield return null;
            var atSide = markers.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy).ToArray();
            Assert.AreEqual(18f, Vector3.Distance(player.transform.position, other.transform.position), 0.05f);
            Debug.Log($"[ThreatMarker] event={eventId} right=18.0m arrows={atSide.Length} reticleLocked={hud.ReticleLocked}");
            Assert.IsFalse(hud.ReticleLocked, "The car beside the player is outside the reticle's forward cone");

            other.Teleport(origin + player.transform.forward * 18f, player.transform.rotation);
            for (int frame = 0; frame < 8; frame++) yield return null;
            var visible = markers.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy).ToArray();
            float distance = Vector3.Distance(player.transform.position, other.transform.position);
            Debug.Log($"[ThreatMarker] event={eventId} ahead={distance:F1}m arrows={visible.Length} reticleLocked={hud.ReticleLocked}");
            Assert.AreEqual(18f, distance, 0.05f);
            Assert.IsTrue(hud.ReticleVisible);
            bool hostile = team == Team.None;
            Assert.AreEqual(hostile, hud.ReticleLocked, "Reticle lock at 18 m");
            Assert.AreEqual(hostile ? 1 : 0, atSide.Length, "Threat arrows beside the player at 18 m");
            Assert.AreEqual(hostile ? 1 : 0, visible.Length, "Threat arrows ahead of the player at 18 m");
            if (hostile)
                Assert.AreEqual("18m", visible[0].Find("Dist").GetComponent<TMP_Text>().text);
        }
    }
}
