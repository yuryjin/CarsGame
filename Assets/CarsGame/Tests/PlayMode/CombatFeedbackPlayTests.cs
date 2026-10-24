using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Combat;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class CombatFeedbackPlayTests
    {
        private string saved;
        private float capture;
        private GameFlow flow;
        private RaceEventDefinition practice;
        private readonly List<MonoBehaviour> disabled = new List<MonoBehaviour>();

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            foreach (var script in disabled) if (script != null) script.enabled = true;
            disabled.Clear();
            if (flow != null) flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            Time.captureDeltaTime = capture;
            PauseController.ResetAll();
        }

        private void Stop(MonoBehaviour script)
        {
            if (script != null && script.enabled) { disabled.Add(script); script.enabled = false; }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DamageShieldRepairAndFirstHitHaveConsistentFeedback()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.settings.cameraShake = true;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 1; practice.pickupCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            RaceParticipant rival = null;
            foreach (var p in race.Participants)
            {
                // Controlled feedback fixture. These are deterministic hits, not a driving claim.
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                p.Car.Teleport(new Vector3(p.isPlayer ? 0f : 2f, 1000f, p.isPlayer ? 0f : 18f), Quaternion.identity);
                Stop(p.Car); Stop(p.Progress); Stop(p.GetComponent<AiGunner>());
                if (!p.isPlayer) rival = p;
            }
            Assert.NotNull(rival);
            var cam = Camera.main;
            Stop(cam.GetComponent<CarCameraFollow>()); Stop(cam.GetComponent<MenuCameraDirector>());
            var impulse = cam.GetComponent<CameraImpulse>();
            impulse.ResetEnergy();
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1004f, -10f), Quaternion.LookRotation(new Vector3(0f, -2f, 28f)));
            for (int i = 0; i < 5; i++) yield return null;
            var fill = System.Array.Find(Object.FindObjectsByType<Image>(), i => i.name == "Fill" && i.transform.parent.name == "Bar_" + rival.displayName);
            Assert.NotNull(fill);
            var trail = fill.transform.parent.Find("DamageTrail").GetComponent<Image>();
            Assert.GreaterOrEqual(fill.rectTransform.rect.height * fill.rectTransform.lossyScale.y, 4.95f, "Health colour must be at least five pixels high");
            Assert.AreEqual(1f, fill.fillAmount);

            var hit = rival.Health.ApplyHit(new HitInfo(race.Player.gameObject, 25f, HitType.Projectile));
            Assert.AreEqual(25f, hit.DamageDealt);
            float damagedRatio = (rival.Health.maxHealth - hit.DamageDealt) / rival.Health.maxHealth;
            yield return null; yield return null;
            var text = System.Array.Find(Object.FindObjectsByType<TMP_Text>(), t => t.name == "HitOutcome");
            Assert.NotNull(text);
            StringAssert.Contains("-25", text.text);
            Assert.GreaterOrEqual(text.fontSize * text.transform.lossyScale.y, 13.9f);
            Assert.AreEqual(damagedRatio, fill.fillAmount, 0.001f);
            Assert.AreEqual(1f, trail.fillAmount, 0.001f, "Lost health remains visible briefly");
            Assert.AreEqual(Color.white, fill.color, "Hit flashes the bar");

            PauseController.Set(PauseReason.User, true);
            yield return new WaitForSecondsRealtime(0.45f);
            Assert.AreEqual(1f, trail.fillAmount, "Pause cannot consume the damage trail");
            PauseController.Set(PauseReason.User, false);
            for (int i = 0; i < 50; i++) yield return null;
            Assert.AreEqual(fill.fillAmount, trail.fillAmount, 0.001f, "Trail catches up after the hit");

            rival.Status.ApplyStatus(StatusKind.Shield, 2f);
            hit = rival.Health.ApplyHit(new HitInfo(race.Player.gameObject, 25f, HitType.Projectile));
            Assert.IsTrue(hit.ShieldAbsorbed);
            Assert.AreEqual(Loc.Get("combat.shield"), text.text);
            yield return null; yield return null;
            Assert.AreEqual(damagedRatio, fill.fillAmount, 0.001f);
            Assert.AreEqual(fill.fillAmount, trail.fillAmount);
            rival.Health.Heal(100f);
            yield return null; yield return null;
            Assert.AreEqual(1f, fill.fillAmount);
            Assert.AreEqual(1f, trail.fillAmount, "Repair does not leave a phantom damage segment");

            impulse.ResetEnergy();
            race.Player.Health.ApplyHit(new HitInfo(rival.gameObject, 10f));
            Assert.Greater(impulse.Energy, 0f, "The first hit of the race must produce feedback");
            PlatformGlue.Save.Data.settings.cameraShake = false;
            yield return null; yield return null;
            Assert.AreEqual(0f, impulse.Energy, "Turning shake off clears an active effect");
            race.Player.Health.ApplyHit(new HitInfo(rival.gameObject, 10f));
            Assert.AreEqual(0f, impulse.Energy, "The accessibility setting suppresses subsequent hits");
        }
    }
}
