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

        [UnityTest, Timeout(120000)]
        public IEnumerator AimingRearRivalShowsTwoArrowsBesideAnOrdinaryRival()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            flow.StartRace(flow.events.Single(e => e.eventId == "e1"), flow.tiers.Single(t => t.tierId == "normal"));
            while (race.State == RaceState.Countdown) yield return null;
            var player = race.Player.Car;
            var rivals = race.Participants.Where(p => !p.isPlayer).Select(p => p.Car).ToArray();
            var gunner = rivals[0].GetComponent<AiGunner>();
            gunner.suppressed = true;
            foreach (var p in race.Participants)
            {
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                if (!p.isPlayer) p.Car.Teleport(player.transform.position + player.transform.right * 110f, player.transform.rotation);
            }
            Vector3 origin = player.transform.position, forward = player.transform.forward, right = player.transform.right;
            rivals[0].Teleport(origin - forward * 36f, player.transform.rotation);
            rivals[1].Teleport(origin + right * 56f, Quaternion.LookRotation(right));
            var markers = GameObject.Find("ThreatMarkers").transform;
            ThreatShapeGraphic Shape(int metres) => markers.Cast<Transform>()
                .Single(t => t.gameObject.activeInHierarchy && t.Find("Dist").GetComponent<TMP_Text>().text == metres + "m")
                .Find("Arrow").GetComponent<ThreatShapeGraphic>();
            for (int frame = 0; frame < 12; frame++) yield return null;
            Assert.AreEqual(1, Shape(36).Level);
            Assert.AreEqual(0, Shape(56).Level);
            gunner.startGraceSeconds = 0;
            gunner.telegraphSeconds = 5f;
            gunner.coneFromAggression = false;
            gunner.coneDegrees = 30f;
            int tick = SimulationDriver.Instance.Tick;
            for (int frame = 0; frame < 450 && gunner.AimTarget != player; frame++)
            {
                gunner.suppressed = false;
                gunner.WantsFire(++tick);
                yield return null;
            }
            Assert.AreSame(player, gunner.AimTarget, "The opponent's own targeting must commit to the player");
            for (int frame = 0; frame < 65; frame++) yield return null;
            var rear = Shape(36);
            Assert.AreEqual(2, rear.Level, "Aiming raises the same contact above the proximity marker");
            Assert.AreEqual(0, Shape(56).Level);
            Assert.Less((rear.rectTransform.localRotation * Vector3.up).y, -0.8f);
            Assert.Less(((RectTransform)rear.transform.parent).anchoredPosition.y, 0);
            string shot = System.Environment.GetEnvironmentVariable("CG_THREAT_SHOT");
            if (!string.IsNullOrEmpty(shot)) CaptureGrayscale(markers.GetComponentInParent<Canvas>(), shot);
        }

        private static void CaptureGrayscale(Canvas canvas, string file)
        {
            var camera = Camera.main;
            int width = Screen.width, height = Screen.height;
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var before = camera.targetTexture;
            var active = RenderTexture.active;
            var mode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            float plane = canvas.planeDistance;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                var pixels = image.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    byte shade = (byte)((299 * c.r + 587 * c.g + 114 * c.b + 500) / 1000);
                    pixels[i] = new Color32(shade, shade, shade, 255);
                }
                image.SetPixels32(pixels); image.Apply();
                System.IO.File.WriteAllBytes(file, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = before; RenderTexture.active = active;
                canvas.renderMode = mode; canvas.worldCamera = previousCamera; canvas.planeDistance = plane;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

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
