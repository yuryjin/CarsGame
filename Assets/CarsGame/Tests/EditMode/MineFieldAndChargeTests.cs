using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    /// <summary>Remote charges, minefields (arena and track), the dropped-mine cap and the garage/save wiring.</summary>
    public class MineFieldAndChargeTests
    {
        private RaceDirector director;
        private GameObject ownerGo, victimGo, trackGo;
        private CarHealth ownerHealth, victimHealth;
        private CarWeapons ownerWeapons;

        private sealed class FixedSite : IMineFieldSite
        {
            public readonly List<Pose> Poses = new List<Pose>();
            public void GetMinePoses(int count, int seed, List<Pose> into)
            {
                for (int i = 0; i < count && i < Poses.Count; i++) into.Add(Poses[i]);
            }
        }

        [SetUp]
        public void SetUp()
        {
            director = SimulationDriver.Instance.GetComponent<RaceDirector>();
            director.ResetRace();

            ownerGo = new GameObject("OwnerCar");
            var ownerCar = ownerGo.AddComponent<CarController>();
            ownerHealth = ownerGo.AddComponent<CarHealth>();
            ownerGo.AddComponent<CarStatus>();
            ownerWeapons = ownerGo.AddComponent<CarWeapons>();
            ownerHealth.SetMaxHealth(100f, true);
            director.RegisterCar(ownerCar);

            victimGo = new GameObject("VictimCar");
            var victimCar = victimGo.AddComponent<CarController>();
            victimHealth = victimGo.AddComponent<CarHealth>();
            victimGo.AddComponent<CarStatus>();
            victimHealth.SetMaxHealth(100f, true);
            director.RegisterCar(victimCar);
        }

        [TearDown]
        public void TearDown()
        {
            if (director != null) director.ResetRace();
            if (ownerGo != null) { director.UnregisterCar(ownerGo.GetComponent<CarController>()); Object.DestroyImmediate(ownerGo); }
            if (victimGo != null) { director.UnregisterCar(victimGo.GetComponent<CarController>()); Object.DestroyImmediate(victimGo); }
            if (trackGo != null) Object.DestroyImmediate(trackGo);
            if (SimulationDriver.Current != null) Object.DestroyImmediate(SimulationDriver.Current.gameObject);
        }

        private MineHazard Drop(MineKind kind, Vector3 at, int tick = 0, bool field = false, GameObject owner = null)
        {
            return director.RequestMine(new MineSpawn
            {
                Owner = field ? null : owner != null ? owner : ownerGo,
                Position = at,
                Rotation = Quaternion.identity,
                Tick = tick,
                Kind = kind,
                Field = field
            });
        }

        // ---------- charge ----------

        [Test]
        public void Charge_IgnoresProximity_AndFuseSetsItOff()
        {
            ownerGo.transform.position = new Vector3(50f, 0f, 0f);
            var charge = Drop(MineKind.Charge, Vector3.zero);
            victimGo.transform.position = new Vector3(0.5f, 0f, 0f);   // parked right on top of it

            charge.SimulationStep(charge.ArmTick + 10);
            Assert.IsTrue(charge.IsLive, "a charge waits for its owner, a car on top does not set it off");
            Assert.AreEqual(100f, victimHealth.Health);

            charge.SimulationStep(charge.DeathTick);
            Assert.IsFalse(charge.IsLive, "the 8 s fuse fires an untouched charge");
            Assert.Less(victimHealth.Health, 100f);
            Assert.AreEqual(SimulationDriver.SecondsToTicks(8f), charge.DeathTick - charge.SpawnTick);
        }

        [Test]
        public void Charge_FirstPressPlants_SecondPressDetonatesOnceArmed()
        {
            ownerWeapons.secondaryType = SecondaryWeaponType.RemoteCharge;
            ownerWeapons.secondaryAmmoMax = 2;
            ownerWeapons.RefillSecondaryAmmo();
            ownerWeapons.secondaryCooldown = 4f;

            Assert.IsTrue(ownerWeapons.TryFireSecondary(100), "first press plants");
            var charge = ownerWeapons.PlantedCharge;
            Assert.IsNotNull(charge);
            Assert.AreEqual(MineKind.Charge, charge.Kind);
            Assert.AreEqual(1, ownerWeapons.SecondaryAmmo);

            Assert.IsFalse(ownerWeapons.TryFireSecondary(101), "still arming: the press does nothing");
            Assert.IsTrue(charge.IsLive);

            // Move the owner clear and put the rival next to the charge.
            ownerGo.transform.position = new Vector3(0f, 0f, 40f);
            victimGo.transform.position = charge.transform.position + new Vector3(2f, 0f, 0f);
            Assert.IsTrue(ownerWeapons.TryFireSecondary(charge.ArmTick), "second press blows it, the planting cooldown does not apply");
            Assert.IsFalse(charge.IsLive);
            Assert.IsNull(ownerWeapons.PlantedCharge);
            Assert.Less(victimHealth.Health, 100f);
            Assert.AreEqual(100f, ownerHealth.Health, "owner was out of the blast");
            Assert.AreEqual(1, ownerWeapons.SecondaryAmmo, "detonating costs no ammo");
        }

        [Test]
        public void Charge_HeldButtonPlantsOnlyOnce()
        {
            ownerWeapons.secondaryType = SecondaryWeaponType.RemoteCharge;
            ownerWeapons.secondaryAmmoMax = 3;
            ownerWeapons.RefillSecondaryAmmo();
            ownerWeapons.secondaryCooldown = 0.1f;
            ownerGo.transform.position = new Vector3(0f, 0f, 60f);
            ownerWeapons.SetWantsSecondary(true);
            for (int t = 1; t <= 200; t++) ownerWeapons.SimulationStep(t);
            Assert.AreEqual(2, ownerWeapons.SecondaryAmmo, "a held button is one press");
            Assert.IsNotNull(ownerWeapons.PlantedCharge, "and it neither blew the charge nor planted a second one");
        }

        [Test]
        public void ChargeWorthFiring_NeedsRivalInsideAndOwnerClear()
        {
            var charge = Drop(MineKind.Charge, Vector3.zero);
            var owner = ownerGo.GetComponent<CarController>();
            int armed = charge.ArmTick;
            ownerGo.transform.position = new Vector3(0f, 0f, 30f);
            victimGo.transform.position = new Vector3(3f, 0f, 0f);
            Assert.IsFalse(TargetFinder.ChargeWorthFiring(owner, charge, armed - 1), "not armed yet");
            Assert.IsTrue(TargetFinder.ChargeWorthFiring(owner, charge, armed));
            ownerGo.transform.position = new Vector3(0f, 0f, 4f);
            Assert.IsFalse(TargetFinder.ChargeWorthFiring(owner, charge, armed), "owner would blow up too");
            ownerGo.transform.position = new Vector3(0f, 0f, 30f);
            victimGo.transform.position = new Vector3(20f, 0f, 0f);
            Assert.IsFalse(TargetFinder.ChargeWorthFiring(owner, charge, armed), "nobody in range");
        }

        // ---------- field mines ----------

        [Test]
        public void FieldMine_NeverExpires_TriggersOnlyWhenNearlyOnTop_HasNoOwner()
        {
            ownerGo.transform.position = new Vector3(80f, 0f, 0f);
            var mine = Drop(MineKind.Contact, Vector3.zero, field: true);
            Assert.IsTrue(mine.IsField);
            Assert.IsNull(mine.Owner);
            Assert.AreEqual(-1, mine.DeathTick);

            victimGo.transform.position = new Vector3(2.2f, 0f, 0f);   // would set off a dropped mine (2.5 m)
            mine.SimulationStep(100000);
            Assert.IsTrue(mine.IsLive, "field mines leave room to pass");

            victimGo.transform.position = new Vector3(1.2f, 0f, 0f);
            mine.SimulationStep(100001);
            Assert.IsFalse(mine.IsLive);
            Assert.Less(victimHealth.Health, 100f);
            Assert.IsNull(victimHealth.LastAttacker, "the map gets no kill credit");
        }

        [Test]
        public void DroppedMineCap_RecyclesOldestDropped_NeverFieldMines()
        {
            director.maxLiveMines = 3;
            try
            {
                var far = new Vector3(500f, 0f, 500f);
                var fields = new List<MineHazard>();
                for (int i = 0; i < 3; i++) fields.Add(Drop(MineKind.Contact, far + Vector3.right * i * 10f, field: true));
                var first = Drop(MineKind.Contact, far + Vector3.forward * 10f);
                int firstGeneration = first.Generation;
                Drop(MineKind.Contact, far + Vector3.forward * 20f);
                Drop(MineKind.Contact, far + Vector3.forward * 30f);
                Drop(MineKind.Contact, far + Vector3.forward * 40f);   // over the cap
                // The pool hands the recycled object straight back out, so compare generations, not IsLive.
                Assert.AreNotEqual(firstGeneration, first.Generation, "oldest dropped mine recycled");
                foreach (var m in director.LiveMineHazards) Assert.AreNotEqual(far + Vector3.forward * 10f, m.transform.position);
                foreach (var f in fields) Assert.IsTrue(f.IsLive, "field mines are outside the cap");
                Assert.AreEqual(6, director.LiveMines);
                Assert.AreEqual(4, director.MinesDropped, "field mines are not counted as dropped");
            }
            finally { director.maxLiveMines = 24; }
        }

        [Test]
        public void MineField_RelaysMineAtSameSpot_AfterDelay_AndWaitsForCarsToLeave()
        {
            var site = new FixedSite();
            site.Poses.Add(new Pose(new Vector3(0f, 0f, 0f), Quaternion.identity));
            site.Poses.Add(new Pose(new Vector3(30f, 0f, 0f), Quaternion.identity));
            ownerGo.transform.position = new Vector3(0f, 0f, 60f);
            victimGo.transform.position = new Vector3(0f, 0f, 80f);
            int start = director.Tick;
            director.Field.Lay(director, site, 5, 1, 10f);
            Assert.AreEqual(2, director.Field.Count, "a site may offer fewer spots than asked");
            Assert.AreEqual(2, director.Field.LiveCount);

            var first = director.LiveMineHazards[0];
            first.Detonate(start + 100);
            Assert.AreEqual(1, director.Field.LiveCount);

            int relay = SimulationDriver.SecondsToTicks(10f);
            director.Field.SimulationStep(director, start + 101);            // schedules the relay
            victimGo.transform.position = new Vector3(1f, 0f, 0f);            // a car sits on the spot
            director.Field.SimulationStep(director, start + 101 + relay);
            Assert.AreEqual(1, director.Field.LiveCount, "no mine appears under a car");
            victimGo.transform.position = new Vector3(0f, 0f, 80f);
            director.Field.SimulationStep(director, start + 102 + relay);
            Assert.AreEqual(2, director.Field.LiveCount);
            Assert.AreEqual(Vector3.zero, director.Field.SlotPosition(0));

            director.ResetRace();
            Assert.AreEqual(0, director.Field.Count, "a new race starts without the old field");
            Assert.AreEqual(0, director.LiveMines);
        }

        [Test]
        public void ArenaSite_ScattersSpacedMinesInsideTheFightZone_Deterministically()
        {
            var arena = ArenaLayout.Create(null);
            try
            {
                var a = new List<Pose>();
                var b = new List<Pose>();
                arena.GetMinePoses(RaceEventDefinition.ArenaMineFieldDefault, 42, a);
                arena.GetMinePoses(RaceEventDefinition.ArenaMineFieldDefault, 42, b);
                Assert.AreEqual(RaceEventDefinition.ArenaMineFieldDefault, a.Count);
                for (int i = 0; i < a.Count; i++)
                {
                    Assert.AreEqual(a[i].position, b[i].position, "same seed, same field");
                    float r = Vector3.Distance(arena.transform.position, a[i].position);
                    Assert.That(r, Is.InRange(ArenaLayout.MineInnerRadius - 0.01f, ArenaLayout.MineOuterRadius + 0.01f));
                    for (int j = 0; j < i; j++)
                        Assert.GreaterOrEqual(Vector3.Distance(a[i].position, a[j].position), ArenaLayout.MineSpacing - 0.01f);
                }
            }
            finally { Object.DestroyImmediate(arena.gameObject); }
        }

        [Test]
        public void TrackSite_BuildsSlalomRowsThatLeaveALaneFree()
        {
            trackGo = new GameObject("TrackPath_Test");
            var tp = trackGo.AddComponent<TrackPath>();
            // A 1.2 km rounded rectangle with long straights.
            var points = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(200f, 0f, 0f), new Vector3(400f, 0f, 0f), new Vector3(450f, 0f, 50f),
                new Vector3(450f, 0f, 150f), new Vector3(400f, 0f, 200f), new Vector3(200f, 0f, 200f), new Vector3(0f, 0f, 200f),
                new Vector3(-50f, 0f, 150f), new Vector3(-50f, 0f, 50f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                var pt = new GameObject("pt" + i);
                pt.transform.SetParent(trackGo.transform);
                pt.transform.position = points[i];
            }
            tp.Build();

            var poses = new List<Pose>();
            new TrackMineSite(tp).GetMinePoses(12, 7, poses);
            Assert.AreEqual(12, poses.Count);

            int hint = -1;
            var rows = new Dictionary<int, List<float>>();
            foreach (var p in poses)
            {
                float d = tp.Spline.Project(p.position, ref hint, -1);
                float lateral = tp.Spline.LateralOffset(p.position, d);
                Assert.LessOrEqual(Mathf.Abs(lateral), tp.halfWidth, "on the road");
                Assert.GreaterOrEqual(tp.Wrap(d - tp.startLineDistance), 100f, "clear of the start");
                int key = Mathf.RoundToInt(d);
                if (!rows.TryGetValue(key, out var lanes)) rows[key] = lanes = new List<float>();
                lanes.Add(lateral);
            }
            Assert.AreEqual(6, rows.Count, "two gates of three rows");
            foreach (var row in rows.Values)
            {
                Assert.AreEqual(2, row.Count, "two of three lanes mined");
                // The free lane is at least a car width plus both trigger radii wide.
                float lane = Mathf.Min(3f, tp.halfWidth - 1.4f);
                float free = FreeWidth(row, tp.halfWidth);
                Assert.GreaterOrEqual(free, lane * 2f - 2f * 1.6f - 0.01f);
            }
        }

        private static float FreeWidth(List<float> mines, float halfWidth)
        {
            var edges = new List<float>(mines) { -halfWidth - 1.6f, halfWidth + 1.6f };
            edges.Sort();
            float widest = 0f;
            for (int i = 1; i < edges.Count; i++) widest = Mathf.Max(widest, edges[i] - edges[i - 1] - 2f * 1.6f);
            return widest;
        }

        [Test]
        public void AiDriver_SteersIntoTheFreeLaneOfAMinefieldRow()
        {
            trackGo = new GameObject("TrackPath_Test");
            var tp = trackGo.AddComponent<TrackPath>();
            var points = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(200f, 0f, 0f), new Vector3(200f, 0f, 100f),
                new Vector3(200f, 0f, 200f), new Vector3(100f, 0f, 200f), new Vector3(0f, 0f, 200f), new Vector3(0f, 0f, 100f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                var pt = new GameObject("pt" + i);
                pt.transform.SetParent(trackGo.transform);
                pt.transform.position = points[i];
            }
            tp.Build();

            var botGo = new GameObject("Bot");
            try
            {
                botGo.AddComponent<Rigidbody>();
                var bot = botGo.AddComponent<CarController>();
                botGo.AddComponent<RaceProgress>().Resync();
                director.RegisterCar(bot);
                var driver = botGo.AddComponent<AiDriver>();
                driver.baseLaneOffset = 0f;
                driver.Initialize(null, 0f);

                // A row 20 m ahead mining the middle and one side (lanes 0 and -3, "right" is +).
                int hint = -1;
                float d = tp.Spline.Project(new Vector3(20f, 0f, 0f), ref hint, -1);
                foreach (float lane in new[] { 0f, -3f })
                {
                    tp.Pose(d, lane, out var pos, out _);
                    Drop(MineKind.Contact, pos, field: true);
                }
                driver.Sample(100);
                Assert.GreaterOrEqual(driver.TargetLane, 1.6f + 0.2f, "clear of the middle mine's trigger");
                Assert.LessOrEqual(driver.TargetLane, tp.halfWidth - 1.1f, "still on the road");
            }
            finally
            {
                director.UnregisterCar(botGo.GetComponent<CarController>());
                Object.DestroyImmediate(botGo);
            }
        }

        [Test]
        public void MineFieldCount_DefaultsByMode()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            try
            {
                Assert.AreEqual(0, ev.MineFieldCount, "races have no field unless the event asks");
                ev.mode = EventMode.Arena;
                Assert.AreEqual(RaceEventDefinition.ArenaMineFieldDefault, ev.MineFieldCount);
                ev.mineFieldCount = 0;
                Assert.AreEqual(0, ev.MineFieldCount, "an arena event can switch it off");
                var minefield = Resources.Load<RaceEventDefinition>("Events/Event_e6");
                Assert.IsNotNull(minefield);
                Assert.AreEqual(12, minefield.MineFieldCount, "the Minefield event lays a real minefield");
            }
            finally { Object.DestroyImmediate(ev); }
        }

        // ---------- garage and save ----------

        [Test]
        public void RemoteCharge_IsItsOwnGarageLine()
        {
            Assert.AreEqual(WeaponKind.Mines, PlayerProgression.KindOf(SecondaryWeaponType.Mines));
            Assert.AreEqual(WeaponKind.FreezeLauncher, PlayerProgression.KindOf(SecondaryWeaponType.FreezeLauncher));
            Assert.AreEqual(WeaponKind.RemoteCharge, PlayerProgression.KindOf(SecondaryWeaponType.RemoteCharge));

            var levels = new int[4];
            PlayerProgression.ApplyWeapons(ownerWeapons, levels, (int)SecondaryWeaponType.RemoteCharge);
            Assert.AreEqual(SecondaryWeaponType.None, ownerWeapons.SecondaryType, "not bought");

            levels[(int)WeaponKind.MachineGun] = 1;   // owning the machine gun must not unlock the charge
            PlayerProgression.ApplyWeapons(ownerWeapons, levels, (int)SecondaryWeaponType.RemoteCharge);
            Assert.AreEqual(SecondaryWeaponType.None, ownerWeapons.SecondaryType);

            levels[(int)WeaponKind.RemoteCharge] = 1;
            PlayerProgression.ApplyWeapons(ownerWeapons, levels, (int)SecondaryWeaponType.RemoteCharge);
            Assert.AreEqual(SecondaryWeaponType.RemoteCharge, ownerWeapons.SecondaryType);
            Assert.AreEqual(2, ownerWeapons.SecondaryAmmoMax);
            Assert.AreEqual(45f, ownerWeapons.chargeDamage, 0.01f);

            levels[(int)WeaponKind.RemoteCharge] = 3;
            PlayerProgression.ApplyWeapons(ownerWeapons, levels, (int)SecondaryWeaponType.RemoteCharge);
            Assert.AreEqual(3, ownerWeapons.SecondaryAmmoMax);
            Assert.Greater(ownerWeapons.chargeDamage, 45f);
        }

        [Test]
        public void OldSave_GetsAFourthWeaponSlot_KeepingLevels()
        {
            var data = new SaveData();
            data.progress.weapons = new[] { 1, 2, 3 };
            var parsed = SaveMigrator.Parse(SaveMigrator.ToJson(data), out var status);
            Assert.AreEqual(SaveParseStatus.Ok, status);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, parsed.progress.weapons);
        }
    }
}
