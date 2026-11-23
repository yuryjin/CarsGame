using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public sealed class DeathReportPlayTests
    {
        private GameFlow flow;
        private float capture;
        private string saved;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ActualMineBreaksShieldBeforeTheFatalShellAndNewLifeStartsClean()
        {
            IEnumerator Frames(int count) { while (count-- > 0) yield return null; }
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return Frames(3);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var hud = flow.GetComponent<HudScreen>();
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow.StartRace(System.Array.Find(flow.events, ev => ev.eventId == "e1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var player = race.Player;
            var field = new System.Collections.Generic.List<RaceParticipant>(race.Participants);
            field.Remove(player); Assert.GreaterOrEqual(field.Count, 2);
            var miner = field[0]; var gunner = field[1];
            miner.displayName = "Remy"; gunner.displayName = "Petra";
            var origin = new Vector3(0f, 1000f, 0f);
            int slot = 0;
            foreach (var p in race.Participants)
            {
                p.offTrackResetSeconds = 1000f; p.Progress.enabled = false;
                p.Car.inputEnabled = false; p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                p.Car.Teleport(origin + (p.isPlayer ? Vector3.zero : Vector3.right * (22f + slot++ * 6f)), Quaternion.identity);
                p.GetComponent<CarCannon>().enabled = false;
            }
            Physics.SyncTransforms(); yield return Frames(3);
            player.Health.respawnInvulnerability = 0f;
            var status = player.GetComponent<CarStatus>();
            Assert.IsTrue(status.ApplyStatus(StatusKind.Shield, 8f));
            float hp = player.Health.Health;
            var director = RaceDirector.Instance;
            var mine = director.RequestMine(new MineSpawn {
                Owner = miner.gameObject, OwnerId = miner.Car.CarId, Position = origin + Vector3.forward,
                Rotation = Quaternion.identity, Tick = SimulationDriver.Instance.Tick, Damage = 30f
            });
            mine.Detonate(SimulationDriver.Instance.Tick);
            Assert.IsFalse(status.HasShield); Assert.AreEqual(hp, player.Health.Health);
            Assert.IsEmpty(hud.LastDeathReport, "The recap waits for a confirmed fatal hit");
            director.RequestProjectile(new ProjectileSpawn {
                Owner = gunner.gameObject, OwnerId = gunner.Car.CarId, Position = origin + Vector3.back * 6f + Vector3.up * .55f,
                Velocity = Vector3.forward * 75f, Damage = 10000f, Tick = SimulationDriver.Instance.Tick
            });
            for (int i = 0; i < 90 && !player.Health.IsDead; i++) yield return null;
            Assert.IsTrue(player.Health.IsDead); yield return Frames(1);
            string[] lines = hud.LastDeathReport.Split('\n');
            Assert.AreEqual(2, lines.Length, hud.LastDeathReport);
            StringAssert.Contains("Remy", lines[0]); StringAssert.Contains(Loc.Get("touch.secondary"), lines[0]);
            StringAssert.Contains(Loc.Get("combat.shield"), lines[0]);
            StringAssert.Contains("Petra", lines[1]); StringAssert.Contains(Loc.Get("combat.final_hit"), lines[1]);
            var visible = GameObject.Find("DeathReportLines").GetComponent<TMP_Text>();
            Assert.AreEqual(hud.LastDeathReport, visible.text);
            player.Health.ReviveNow(); yield return Frames(2);
            Assert.IsEmpty(hud.LastDeathReport); Assert.IsNull(GameObject.Find("DeathReportLines"));
            Assert.IsTrue(player.Health.TakeDamage(10000f, gunner.gameObject)); yield return Frames(2);
            lines = hud.LastDeathReport.Split('\n'); Assert.AreEqual(1, lines.Length);
            StringAssert.Contains("Petra", lines[0]); StringAssert.DoesNotContain("Remy", lines[0]);
            race.EndForPlayerOut(); yield return Frames(5);
            Assert.AreEqual(FlowState.Results, flow.State);
            Assert.AreEqual(lines[0], GameObject.Find("FinalHits").transform.Find("DeathReportLines").GetComponent<TMP_Text>().text);
        }
    }
}
