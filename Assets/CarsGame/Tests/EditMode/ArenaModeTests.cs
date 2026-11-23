using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Sim;
using UnityEngine;

namespace RacingGame.Tests
{
    public class ArenaModeTests
    {
        [Test]
        public void FiveSharesUseDrivingDamageAndLongestLifeAndCapAtOneThousand()
        {
            var score = new Core.Sim.ArenaScore(50, 9000);
            for (int i = 0; i < 100; i++) score.Step(80, false, true, false);
            var p = score.Points;
            Assert.AreEqual(4, p.Speed); Assert.AreEqual(4, p.Centre); Assert.AreEqual(2, p.Survival);
            Assert.AreEqual(0, p.Hits); Assert.AreEqual(0, p.Knockouts);
            score.Hit(false, 80);
            score.Knockout(true);
            Assert.AreEqual(35, score.Points.Total);
            score.Died();
            for (int i = 0; i < 50; i++) score.Step(0, false, true, false);
            Assert.AreEqual(2, score.Points.Survival);
            Assert.AreEqual(4, score.Points.Centre);
            for (int i = 0; i < 9000; i++) score.Step(90, true, true, false);
            for (int i = 0; i < 100; i++) { score.Hit(false, 90); score.Knockout(false); }
            Assert.AreEqual(1000, score.Points.Total);
        }

        [Test]
        public void ADriftHitAndFastRamRaiseHeatOnceAndOnlyCombatUsesIt()
        {
            var score = new ArenaScore(50, 9000);
            score.Hit(true, 100);
            score.DriftBoosted();
            score.Hit(false, 80);
            score.Hit(true, 69);
            Assert.AreEqual(1, score.Multiplier);
            score.Hit(true, 70);
            score.Hit(true, 80);
            Assert.AreEqual(2, score.Multiplier);
            Assert.AreEqual(35, score.Points.Hits);
            score.Knockout(true);
            Assert.AreEqual(40, score.Points.Knockouts);
            for (int i = 0; i < 50; i++) score.Step(90, true, true, false);
            Assert.AreEqual(3, score.Points.Speed);
            Assert.AreEqual(2, score.Points.Centre);
            for (int i = 0; i < 150; i++) score.Step(0, false, true, true);
            Assert.AreEqual(2, score.Multiplier);
            for (int i = 0; i < 150; i++) score.Step(0, false, true, false);
            Assert.AreEqual(1, score.Multiplier);
            Assert.AreEqual(ArenaScore.HeatChange.Slowed, score.Change);
        }

        [Test]
        public void AssistsExpireAfterFiveSecondsAndAtRespawn()
        {
            var victim = new ArenaScore(50, 9000);
            victim.DamagedBy(5, 50);
            victim.DamagedBy(7, 299);
            Assert.AreEqual(5, victim.RecentAttacker(300, 7));
            Assert.AreEqual(-1, victim.RecentAttacker(301, 7));
            Assert.AreEqual(7, victim.RecentAttacker(301));
            victim.Died();
            Assert.AreEqual(-1, victim.RecentAttacker(301));
        }

        [Test]
        public void SharedPlacesAndPrizesIgnoreCarIdButTheMissionKeepsItsOrder()
        {
            var entries = new List<RankEntry> {
                new RankEntry {CarId = 9, Points = 250, Kills = 1, Deaths = 2},
                new RankEntry {CarId = 3, Points = 250, Kills = 1, Deaths = 2},
                new RankEntry {CarId = 5, Points = 249, Kills = 8}
            };
            var places = RaceRanking.RankArena(entries);
            Assert.AreEqual(new[] {1, 1, 3}, places);
            var a = entries[0]; a.CarId = 2; entries[0] = a;
            Assert.AreEqual(places, RaceRanking.RankArena(entries));
            Assert.AreEqual(Core.Economy.Economy.RaceReward(places[0], true, new[]{1}, 1),
                Core.Economy.Economy.RaceReward(places[1], true, new[]{1}, 1));
            for (int i = 0; i < entries.Count; i++) { var e = entries[i]; e.Points = 0; entries[i] = e; }
            Assert.AreEqual(new[] {2, 3, 1}, RaceRanking.RankArena(entries, sharedPlaces:false));
        }

        [Test]
        public void AnOldMatchDurationNeverBecomesAnArenaScoreAfterLoadingOrMerging()
        {
            var old = Core.Save.SaveMigrator.Parse("{\"version\":7,\"progress\":{\"bests\":[{\"eventId\":\"a1\",\"bestTimeMs\":180000,\"stars\":3}]}}", out _);
            Assert.IsFalse(old.progress.bests[0].hasArenaScore);
            var fresh = new Core.Save.SaveData();
            fresh.progress.bests.Add(new Core.Save.EventBest {eventId="a1",hasArenaScore=true,arenaPoints=314});
            var m = Core.Save.SaveMerge.Merge(old, fresh).progress.bests[0];
            Assert.AreEqual(314, m.arenaPoints); Assert.IsTrue(m.hasArenaScore); Assert.AreEqual(3, m.stars);
            fresh.progress.bests[0].arenaPoints = 290;
            var weaker = Core.Save.SaveMerge.Merge(fresh, old);
            weaker.progress.bests[0] = m;
            Assert.AreEqual(314, Core.Save.SaveMerge.Merge(weaker, fresh).progress.bests[0].arenaPoints);
        }
        [Test]
        public void ArenaUsesKillsThenDeathsWhenScoreIsEqual()
        {
            var entries = new List<RankEntry>
            {
                new RankEntry { CarId = 10, Kills = 3, Deaths = 2 },
                new RankEntry { CarId = 20, Kills = 5, Deaths = 1 },
                new RankEntry { CarId = 30, Kills = 3, Deaths = 0 },
                new RankEntry { CarId = 40, Kills = 5, Deaths = 1 },
                new RankEntry { CarId = 50, Kills = 0, Deaths = 4 }
            };

            var positions = RaceRanking.RankArena(entries);
            Assert.AreEqual(4, positions[0]); // Car 10 is 4th
            Assert.AreEqual(1, positions[1]); // Car 20 is 1st
            Assert.AreEqual(3, positions[2]); // Car 30 is 3rd
            Assert.AreEqual(1, positions[3]); // Car 40 shares the lead with car 20.
            Assert.AreEqual(5, positions[4]); // Car 50 is 5th
        }

        [Test]
        public void ArenaModeConfiguresFastRespawnAndInvulnerability()
        {
            var root = new GameObject("ArenaTestRoot");
            try
            {
                var prefab = new GameObject("CarPrefab");
                prefab.AddComponent<BoxCollider>();
                prefab.AddComponent<CarController>();
                prefab.AddComponent<CarHealth>();
                prefab.AddComponent<RaceParticipant>();
                prefab.AddComponent<RaceProgress>();

                var spawner = root.AddComponent<RaceSpawner>();
                spawner.raceCarPrefab = prefab;
                spawner.aiCount = 2;
                spawner.spawnOnStart = false;

                bool spawned = spawner.Spawn(arena: true);
                Assert.IsTrue(spawned);
                Assert.IsNotNull(spawner.Arena);
                Assert.AreEqual(3, spawner.Participants.Count);

                foreach (var p in spawner.Participants)
                {
                    Assert.IsNotNull(p.Health);
                    Assert.IsTrue(p.Health.respawnOnDeath, "Arena participants must respawn on death.");
                    Assert.AreEqual(2f, p.Health.respawnDelay, 0.01f, "Arena respawn delay must be 2.0s.");
                    Assert.AreEqual(2f, p.Health.respawnInvulnerability, 0.01f, "Arena respawn invulnerability must be 2.0s.");
                    Assert.IsFalse(p.Progress.enabled, "Lap progress must be disabled in arena mode.");
                }

                spawner.Clear();
                Object.DestroyImmediate(prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArenaEventDefinitionContainsMatchDuration()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.mode = EventMode.Arena;
            ev.matchDuration = 75f;
            Assert.AreEqual(75f, ev.matchDuration);
            Object.DestroyImmediate(ev);
        }

        [Test]
        public void ArenaResultsRecordFinishedAndSurvived()
        {
            var results = new RaceResults
            {
                IsArena = true,
                LapsRequired = 0,
                RaceDuration = 75.0
            };

            var winner = new RaceResultEntry
            {
                Name = "Player",
                IsPlayer = true,
                Position = 1,
                Finished = true,
                Survived = true,
                Kills = 4,
                Deaths = 0
            };

            var runnerUp = new RaceResultEntry
            {
                Name = "AI_1",
                IsPlayer = false,
                Position = 2,
                Finished = true,
                Survived = false,
                Kills = 2,
                Deaths = 2
            };

            results.Entries.Add(winner);
            results.Entries.Add(runnerUp);

            Assert.IsTrue(results.IsArena);
            Assert.AreEqual(0, results.LapsRequired);
            Assert.AreEqual(winner, results.Player);
            Assert.IsTrue(winner.Finished);
            Assert.IsTrue(winner.Survived);
            Assert.IsTrue(runnerUp.Finished);
            Assert.IsFalse(runnerUp.Survived);
        }

        [Test]
        public void ArenaEventStarMaskCalculatesCorrectly()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.mode = EventMode.Arena;
            ev.star1TargetPosition = 3;
            ev.star2TargetKills = 3;
            ev.star3ZeroDeaths = true;

            // Scenario 1: Finished 1st, 4 kills, 0 deaths -> 3 stars (mask 7)
            int mask1 = ev.StarMask(finished: true, position: 1, kills: 4, deaths: 0);
            Assert.AreEqual(7, mask1);
            Assert.AreEqual(3, RaceEventDefinition.CountStars(mask1));

            // Scenario 2: Finished 2nd, 1 kill, 0 deaths -> 2 stars (star 1 & 3, mask 5)
            int mask2 = ev.StarMask(finished: true, position: 2, kills: 1, deaths: 0);
            Assert.AreEqual(5, mask2);
            Assert.AreEqual(2, RaceEventDefinition.CountStars(mask2));

            // Scenario 3: Finished 4th, 5 kills, 1 death -> 1 star (star 2, mask 2)
            int mask3 = ev.StarMask(finished: true, position: 4, kills: 5, deaths: 1);
            Assert.AreEqual(2, mask3);
            Assert.AreEqual(1, RaceEventDefinition.CountStars(mask3));

            Object.DestroyImmediate(ev);
        }
    }
}
