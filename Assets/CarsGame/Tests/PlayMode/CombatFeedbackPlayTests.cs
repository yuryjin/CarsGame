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
        public IEnumerator ReducedFlashDimsTheSameMuzzleButKeepsHitAndMissDistinct()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.settings.cameraShake = false;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 1; practice.pickupCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var position = new Vector3(0f, 1000f, 0f);
            RaceParticipant rival = null;
            foreach (var p in race.Participants)
            {
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                p.Car.Teleport(position + Vector3.forward * (p.isPlayer ? 0f : 18f), Quaternion.identity);
                Stop(p.Progress); Stop(p.GetComponent<AiGunner>()); Stop(p.GetComponent<PlayerDriverInput>());
                if (!p.isPlayer) { rival = p; Stop(p.GetComponent<CarCannon>()); }
            }
            Assert.NotNull(rival);
            var camera = Camera.main;
            Stop(camera.GetComponent<CarCameraFollow>()); Stop(camera.GetComponent<MenuCameraDirector>());
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
            var cannon = race.Player.GetComponent<CarCannon>();
            race.Player.Car.Weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            var hitText = System.Array.Find(race.GetComponentsInChildren<TMP_Text>(true), t => t.name == "HitOutcome");
            Vector3 target = position + Vector3.forward * 15f + Vector3.up * 0.65f;
            camera.transform.position = position + new Vector3(3f, 2.5f, -3f);
            camera.transform.LookAt(cannon.muzzle.position + Vector3.forward * 0.6f);
            string directory = System.Environment.GetEnvironmentVariable("CG_FLASH_SHOTS");
            float full = 0f;
            foreach (var mode in new[] { "standard-hit", "reduced-hit", "reduced-miss" })
            {
                bool hit = mode != "reduced-miss";
                PlatformGlue.Save.Data.settings.reducedCombatFlashes = mode != "standard-hit";
                rival.Car.Teleport(target - Vector3.up * 0.65f + (hit ? Vector3.zero : Vector3.right * 12f), Quaternion.identity);
                for (int frame = 0; frame < 100; frame++) { cannon.CommitAim(target); yield return null; }
                // Hit feedback uses unscaled time; in batch, a simulated second can
                // finish before the previous confirmation's real display interval.
                float clearBy = Time.realtimeSinceStartup + 5f;
                while (!string.IsNullOrEmpty(hitText.text) && Time.realtimeSinceStartup < clearBy) yield return null;
                float health = rival.Health.Health;
                Assert.IsEmpty(hitText.text);
                Vector3 muzzlePoint = cannon.muzzle.position;
                float before = MuzzleLuminance(camera, muzzlePoint, directory, mode + "-before");
                Assert.IsTrue(cannon.TryFire(), mode);
                float after = MuzzleLuminance(camera, muzzlePoint, directory, mode);
                float gain = after - before;
                Debug.Log($"[CombatFlash] {mode} muzzle={muzzlePoint:F3} baseline={before:F2} shot={after:F2} difference={gain:F2}");
                Assert.Greater(gain, 1f, "The shot must visibly brighten the muzzle region");
                if (mode == "standard-hit") full = gain;
                else Assert.Less(gain, full * 0.85f, "The same shot is visibly dimmer");
                bool confirmed = false;
                for (int frame = 0; frame < 40; frame++)
                {
                    yield return null;
                    if (hitText.gameObject.activeInHierarchy && hitText.text.Contains(Loc.Get("combat.hit"))) confirmed = true;
                }
                Assert.AreEqual(hit, rival.Health.Health < health, mode);
                Assert.AreEqual(hit, confirmed, mode + " HUD confirmation");
            }
        }

        private static float MuzzleLuminance(Camera camera, Vector3 muzzle, string directory, string name)
        {
            const int width = 640, height = 360;
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                var point = camera.WorldToViewportPoint(muzzle);
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                if (!string.IsNullOrEmpty(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                }
                var pixels = image.GetPixels32();
                int x = Mathf.Clamp(Mathf.RoundToInt(point.x * width), 3, width - 4);
                int y = Mathf.Clamp(Mathf.RoundToInt(point.y * height), 3, height - 4);
                float sum = 0f;
                for (int row = y - 3; row <= y + 3; row++)
                    for (int col = x - 3; col <= x + 3; col++)
                    {
                        var c = pixels[row * width + col]; sum += .2126f * c.r + .7152f * c.g + .0722f * c.b;
                    }
                return sum / 49f;
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
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

            // Radial weapons and rear traps have no forward crosshair, but their
            // actual damage still needs a visible confirmation.
            for (int i = 0; i < 110; i++) yield return null;
            foreach (var type in new[] { PrimaryWeaponType.TeslaCoil, PrimaryWeaponType.MineDispenser })
            {
                race.Player.Car.Weapons.SetPrimaryType(type);
                yield return null; yield return null;
                Assert.IsFalse(race.GetComponent<HudScreen>().ReticleVisible);
                var result = rival.Health.ApplyHit(new HitInfo(race.Player.gameObject, 7f));
                Assert.AreEqual(7f, result.DamageDealt);
                yield return null; yield return null;
                Assert.IsTrue(text.gameObject.activeInHierarchy, type + " damage confirmation must survive hiding its aiming marker");
                StringAssert.Contains("-7", text.text);
            }
        }
    }
}
