using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class AbyssTrackPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator SceneLapsFallsRetryAndMenuRestore()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var path = TrackPath.Instance;
            var ground = GameObject.Find("Ground_Terrain");
            float originalWidth = path.halfWidth;
            var ev = Resources.Load<RaceEventDefinition>("Events/Event_e16");
            Assert.IsNotNull(ev);
            flow.StartRace(ev, null);
            var hud = Object.FindAnyObjectByType<HudScreen>();
            Assert.AreEqual(Loc.Get("lesson.abyss"), HudScreen.ControlsHint(false));
            var map = Object.FindAnyObjectByType<AbyssTrack>();
            Assert.IsNotNull(map);
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.IsFalse(ground.activeSelf);
            while (race.State == RaceState.Countdown) yield return null;

            var progress = race.Player.Progress;
            float distance = path.startLineDistance + 1f;
            progress.transform.position = path.Position(distance);
            progress.BeginRace();
            for (int lap = 2; lap <= 3; lap++)
            {
                int steps = Mathf.CeilToInt(path.Length / 5f);
                for (int i = 0; i < steps; i++)
                {
                    distance += path.Length / steps;
                    progress.transform.position = path.Position(distance);
                    progress.SimulationStep(i);
                }
                Assert.AreEqual(lap, map.Lap, "player lap event changes the map");
                Assert.AreEqual(Loc.Get(lap == 2 ? "abyss.open" : "abyss.narrow"), hud.LastHint,
                    "The changing road must explain the newly removed protection.");
            }
            Physics.SyncTransforms();
            int mask = 1 << LayerMask.NameToLayer("Track") | 1 << LayerMask.NameToLayer("Ground");
            Assert.IsFalse(Physics.Raycast(path.Position(120f) + path.Right(120f) * 4f + Vector3.up * 3f,
                Vector3.down, 15f, mask), "no baked collider remains under the removed shoulder");
            race.Player.transform.position = path.Position(120f) + Vector3.down * 12f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Greater(race.Player.transform.position.y, -1f, "fall recovery returns the player");
            Assert.LessOrEqual(Mathf.Abs(path.Spline.LateralOffset(race.Player.transform.position, race.Player.Progress.Distance)),
                AbyssTrack.NarrowHalfWidth - 1f, "respawn fits the narrow road");
            flow.RetryRace();
            Assert.AreEqual(1, Object.FindAnyObjectByType<AbyssTrack>().Lap);
            Assert.AreEqual(AbyssTrack.WideHalfWidth, path.halfWidth);
            flow.EnterMenu();
            Assert.IsTrue(ground.activeSelf);
            Assert.AreEqual(originalWidth, path.halfWidth);
            Assert.IsNull(Object.FindAnyObjectByType<AbyssTrack>());
        }
    }
}
