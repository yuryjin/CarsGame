using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Runs the boss mission «Левиафан»: builds the desert, puts the player and five allied bots in front of the
    /// crawling fortress, spawns its escorts (tanks, armoured cars), outposts (turrets, troopers), drones from its
    /// hangar, trooper drops from its ramps and bomber raids, and decides the outcome: the core destroyed = victory,
    /// the fortress reaching the settlement = defeat. Everything runs in the simulation tick; the spawner asks it
    /// for start and recovery poses (<see cref="IBattleSite"/>). Created by <see cref="GameFlow"/> for
    /// <see cref="EventMode.Mission"/> events and torn down with <see cref="Dispose"/>.
    /// </summary>
    public sealed partial class MissionDirector : MonoBehaviour, ISimulated, IBattleSite
    {
        public enum Outcome { None, Victory, Defeat }

        public static MissionDirector Current { get; private set; }
        /// <summary>Last finished mission, for the results screen.</summary>
        public static bool LastVictory { get; private set; }

        public DesertMap Map { get; private set; }
        public BossCrawler Boss { get; private set; }
        public BossPart SquadTarget { get; private set; }
        public void MarkSquadTarget(BossPart part)
        {
            if (part == null || (Result == Outcome.None && part.Boss == Boss && part.Targetable && part.Role != BossRole.Hull))
                SquadTarget = part;
        }

        public MissionStrikes Strikes { get; private set; }
        public (float CoreDamage, int Stations, int AlliesSaved) FinalContribution { get; private set; }
        private float playerCoreDamage;
        private int playerStations;
        private readonly HashSet<int> rescuedAllies = new HashSet<int>();
        public Outcome Result { get; private set; }
        public int SimulationOrder => int.MaxValue - 6000;   // after the cars, before the units and the shells
        /// <summary>(text, colour) for the HUD's centre toast.</summary>
        public event Action<string, Color> Announce;
        public event Action PlaneRaid;

        public const float PlayerStartFraction = 0.3f;
        private static readonly Color InfoColor = new Color(1f, 0.78f, 0.3f), AlertColor = new Color(1f, 0.32f, 0.24f), GoodColor = new Color(0.45f, 1f, 0.55f);
        private static readonly Color EnemyPaint = new Color(0.32f, 0.12f, 0.08f), AllyPaint = new Color(0.9f, 0.55f, 0.12f);

        private RaceManager race;
        private HudScreen hud;
        private MissionHud missionHud;
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<CarController> escorts = new List<CarController>();
        private readonly Dictionary<CarController, int> escortWreckTick = new Dictionary<CarController, int>();
        private System.Random rng;
        private int carLayer = -1;
        private int nextRaidTick, nextEscortTick, nextSquadTick, endTick = -1, lastBlastTick, respawnFlip, allyCount, serial;
        private float coreMax;

        public static MissionDirector Create(RaceManager race)
        {
            var go = new GameObject("MissionDirector");
            var m = go.AddComponent<MissionDirector>();
            Current = m;
            m.race = race;
            m.rng = new System.Random((RaceDirector.Current ?? RaceDirector.Instance).raceSeed ^ 0x6c657669);
            MissionArt.Init(AbyssTrack.SceneryMaterial());   // before the circuit's scenery is hidden
            AbyssTrack.HideScenery(m.hidden, AbyssTrack.Scenery);
            AbyssTrack.HideScenery(m.hidden, new[] { "Environment/Mountains" });
            m.Map = DesertMap.Create();
            m.Strikes = new MissionStrikes(m.Map, go.transform);
            if (race.spawner != null) race.spawner.Site = m;
            race.OnFieldSpawned += m.OnFieldSpawned;
            race.OnRaceStarted += m.OnStarted;
            m.hud = race.GetComponent<HudScreen>() ?? FindAnyObjectByType<HudScreen>();
            return m;
        }

        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable() { var d = SimulationDriver.Current; if (d != null) d.Unregister(this); }

        // ---------- set-up ----------

        private static float TierScale => RaceSetup.TierId == "easy" ? 0.75f : RaceSetup.TierId == "hard" ? 1.3f : 1f;

        private void OnFieldSpawned()
        {
            carLayer = LayerMask.NameToLayer("Car");
            if (carLayer >= 0) Physics.IgnoreLayerCollision(carLayer, MissionLayers.SoftUnit, true);
            for (int slot = 0; slot < race.Participants.Count; slot++)
            {
                var p = race.Participants[slot];
                if (p == null) continue;
                Teams.Set(p.gameObject, Team.Players);
                var cannon = p.Car != null ? p.Car.Cannon : null;
                if (cannon != null) { cannon.barrelPitchLimit = 32f; cannon.turretMaxRange = 70f; cannon.turretYawLimit = Mathf.Max(cannon.turretYawLimit, 80f); }   // drive-by fire at a 46 m target
                if (p.isPlayer || GameFlow.IsRoomDriver(slot)) continue;
                var gunner = p.GetComponent<AiGunner>();
                if (gunner != null) { gunner.maxRange = 70f; gunner.startGraceSeconds = 1f; }
                if (p.Weapons != null) { p.Weapons.secondaryType = SecondaryWeaponType.None; p.Weapons.secondaryAmmoMax = 0; }
                if (p.Health != null) p.Health.SetMaxHealth(p.Health.maxHealth * 1.5f, true);   // the squad, not rivals: tougher than race bots
                var paint = p.GetComponent<CarPaint>();
                if (paint != null) paint.SetColor(Color.Lerp(AllyPaint, paint.color, 0.35f));
            }

            coreMax = 2600f * TierScale;
            Boss = BossCrawler.Create(Map, 0f, coreMax);
            Boss.PhaseChanged += OnPhase;
            Boss.PartDestroyed += OnPartDestroyed;
            BossPart.RequestDrone = LaunchDrone;
            BossPart.RequestStrike = (part, point, tick) => Strikes.Add(point, 8f, 34f * TierScale, 2.6f, Boss.gameObject, tick);
            MissionUnit.Killed += OnUnitKilled;
            MissionUnit.Damaged += OnUnitDamaged;

            missionHud = MissionHud.Create(this, hud);
            if (GameFlow.RoomReplica) return;
            // Escort at the start: two tanks behind, three armoured cars on the flanks.
            for (int i = 0; i < 5; i++)
            {
                bool tank = i < 2;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 at = tank ? Boss.GatePoint(side) + Boss.transform.forward * -6f : Boss.transform.position + Boss.transform.right * side * (BossCrawler.Width * 0.5f + 12f + i * 3f);
                SpawnEscort(tank, at, Boss.transform.forward);
            }
            // Three outposts along the route: two turrets and a squad each.
            float[] along = { 0.42f, 0.6f, 0.8f };
            for (int i = 0; i < along.Length; i++) BuildOutpost(Map.RouteLength * along[i], i % 2 == 0 ? 1f : -1f);

        }

        private void OnStarted()
        {
            MissionUnit.Running = true;
            int tick = SimulationDriver.Instance.Tick;
            nextRaidTick = tick + SimulationDriver.SecondsToTicks(35f);
            nextEscortTick = tick + SimulationDriver.SecondsToTicks(45f);
            nextSquadTick = tick + SimulationDriver.SecondsToTicks(20f);
            Say(Loc.Get("mission.brief"), InfoColor);
        }

        private void BuildOutpost(float along, float side)
        {
            Vector3 centre = Map.RoutePoint(along);
            Vector3 f = Map.RouteForward(along), right = Vector3.Cross(Vector3.up, f);
            Vector3 at = centre + right * side * 42f;
            GroundTurretUnit.Spawn(Map, at + f * 9f, -f);
            GroundTurretUnit.Spawn(Map, at - f * 9f + right * side * 6f, -f);
            for (int i = 0; i < 4; i++)
                TrooperUnit.Spawn(Map, at + right * side * (8f + i * 2.5f) + f * (i - 1.5f) * 3f, i == 3, ++serial);
        }

        // ---------- IBattleSite ----------

        public void GetSpawnPose(int slot, int total, out Vector3 position, out Quaternion rotation)
        {
            float d = Map.RouteLength * PlayerStartFraction + (slot / 3) * 11f;
            Vector3 f = Map.RouteForward(d), right = Vector3.Cross(Vector3.up, f);
            Vector3 at = Map.RoutePoint(d) + right * ((slot % 3) - 1) * 9f;
            Map.GroundPose(at, -f, out position, out rotation);   // facing the fortress
        }

        public bool TryGetRecoveryPose(CarController car, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (car == null || Map == null) return false;
            bool enemy = Teams.Of(car.gameObject) == Team.Enemy;
            float bossDistance = Boss != null ? Boss.Distance : 0f;
            int mask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 at, facing;
                if (enemy && Boss != null)
                {
                    at = Boss.GatePoint(attempt % 2 == 0 ? 1f : -1f) - Boss.transform.forward * (attempt * 6f);
                    facing = Boss.transform.forward;
                }
                else
                {
                    // Ahead of the fortress, off to one side of its path, facing it: straight back into the fight.
                    float d = Mathf.Clamp(bossDistance + 115f + attempt * 10f, 60f, Map.RouteLength - 30f);
                    Vector3 f = Map.RouteForward(d);
                    float side = (respawnFlip++ % 2 == 0) ? 1f : -1f;
                    at = Map.RoutePoint(d) + Vector3.Cross(Vector3.up, f) * side * (38f + attempt * 5f);
                    facing = Boss != null ? Boss.transform.position - at : -f;
                }
                Map.GroundPose(at, facing, out position, out rotation);
                if (!Physics.CheckBox(position + Vector3.up * 0.6f, new Vector3(1.4f, 0.7f, 2.6f), rotation, mask, QueryTriggerInteraction.Ignore)) return true;
            }
            return true;
        }

        public ICarInputSource CreateBotDriver(GameObject car, AiDriverProfile profile)
        {
            var driver = car.GetComponent<MissionDriver>() ?? car.AddComponent<MissionDriver>();
            driver.Initialize(this, profile, MissionDriver.Side.Ally, allyCount++);
            return driver;
        }

        // ---------- spawning ----------

        private CarController SpawnEscort(bool tank, Vector3 at, Vector3 facing)
        {
            var prefab = race.spawner != null ? race.spawner.raceCarPrefab : null;
            if (prefab == null) return null;
            Map.GroundPose(at, facing, out var pos, out var rot);
            var go = Instantiate(prefab, pos, rot);
            go.name = (tank ? "Enemy_Tank_" : "Enemy_Armored_") + (++serial);
            CarBody.Apply(go, Core.Economy.CarBodies.Get(tank ? "grizzly" : "raider"));
            Teams.Set(go, Team.Enemy);
            var car = go.GetComponent<CarController>();
            var participant = go.GetComponent<RaceParticipant>() ?? go.AddComponent<RaceParticipant>();
            participant.displayName = Loc.Get(tank ? "mission.unit.tank" : "mission.unit.armored");
            participant.Site = this;
            if (participant.Progress != null) participant.Progress.enabled = false;
            var paint = go.GetComponent<CarPaint>() ?? go.AddComponent<CarPaint>();
            paint.SetColor(EnemyPaint);
            var health = go.GetComponent<CarHealth>();
            if (health != null)
            {
                health.respawnOnDeath = false;
                health.showScreenBar = false;
                health.showWorldBar = true;
                health.SetMaxHealth((tank ? 240f : 140f) * TierScale, true);
            }
            var weapons = participant.Weapons;
            weapons.SetPrimaryType(tank ? PrimaryWeaponType.Cannon : PrimaryWeaponType.MachineGun);
            weapons.secondaryType = SecondaryWeaponType.None;
            weapons.secondaryAmmoMax = 0;
            var gunner = go.GetComponent<AiGunner>() ?? go.AddComponent<AiGunner>();
            gunner.Initialize(null, tank ? 2.4f : 1.6f);
            gunner.telegraphSeconds = BotAim.BaseTelegraph(RaceSetup.TierId);
            gunner.startGraceSeconds = 2f;
            gunner.maxRange = 55f;
            if (car.Cannon != null) car.Cannon.turretYawLimit = Mathf.Max(car.Cannon.turretYawLimit, 60f);
            var driver = go.GetComponent<MissionDriver>() ?? go.AddComponent<MissionDriver>();
            driver.Initialize(this, AiDriverProfile.Default, MissionDriver.Side.Escort, serial);
            car.SetInputSource(driver);
            if (go.GetComponent<CarDamageSmoke>() == null) go.AddComponent<CarDamageSmoke>();
            escorts.Add(car);
            if (hud != null) hud.KillFeed.RegisterRacerName(car.CarId, participant.displayName);
            return car;
        }

        private bool LaunchDrone(BossPart hangar, int tick)
        {
            int cap = Boss.Phase == 1 ? 3 : Boss.Phase == 2 ? 4 : 5;
            if (Count(UnitKind.Drone) >= cap) return false;
            DroneUnit.Spawn(Map, hangar.transform.position + Vector3.up * 3f, ++serial);
            return true;
        }

        private void DropSquad(int tick)
        {
            float side = rng.Next(2) == 0 ? 1f : -1f;
            Boss.Hold(tick, 3f);
            Vector3 ramp = Boss.RampPoint(side);
            Vector3 out_ = Boss.transform.right * side;
            int n = Boss.Phase >= 3 ? 5 : 4;
            for (int i = 0; i < n; i++)
                TrooperUnit.Spawn(Map, ramp + out_ * (i % 2) * 2.5f + Boss.transform.forward * (i - n * 0.5f) * 2.4f, i == 0 || (Boss.Phase >= 3 && i == n - 1), ++serial);
            Say(Loc.Get("mission.troops"), InfoColor);
        }

        private void LaunchRaid(int tick)
        {
            var target = PickRaidTarget();
            if (target == null) return;
            int planes = Boss.Phase >= 3 ? 2 : 1;
            for (int k = 0; k < planes; k++)
            {
                Vector3 v = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
                Vector3 p = target.transform.position + Vector3.ClampMagnitude(v * 3.5f, 90f) + new Vector3(k * 30f, 0f, -k * 20f);
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                const float lead = 430f;
                var plane = PlaneUnit.Spawn(Map, p - dir * lead, p + dir * lead);
                bool bombs = (rng.Next(3) != 0) || Boss.Phase >= 2;
                if (bombs)
                    for (int i = 0; i < 4; i++)
                    {
                        float offset = -24f + i * 16f;
                        Strikes.Add(p + dir * offset, 7f, 30f * TierScale, (lead + offset) / PlaneUnit.Speed, plane.gameObject, tick, 2.6f);
                    }
                else
                    for (int i = 0; i < 9; i++)
                    {
                        float offset = -40f + i * 10f;
                        Strikes.Add(p + dir * offset, 3.4f, 11f * TierScale, (lead + offset) / PlaneUnit.Speed, plane.gameObject, tick, 2.2f);
                    }
            }
            Say(Loc.Get("mission.air_raid"), AlertColor);
            PlaneRaid?.Invoke();
        }

        private CarController PickRaidTarget()
        {
            var candidates = new List<CarController>();
            CarController player = race.Player != null ? race.Player.Car : null;
            foreach (var p in race.Participants)
            {
                if (p == null || p.Car == null || p.Health == null || p.Health.IsDead) continue;
                candidates.Add(p.Car);
            }
            if (candidates.Count == 0) return null;
            if (player != null && candidates.Contains(player) && rng.NextDouble() < 0.45) return player;
            return candidates[rng.Next(candidates.Count)];
        }

        // ---------- simulation ----------

        public void SimulationStep(int tick)
        {
            if (Boss == null || GameFlow.RoomReplica) return;
            bool live = race != null && (race.State == RaceState.Racing || race.State == RaceState.Finishing);
            Boss.Step(tick, live && Result == Outcome.None);
            if (!live) return;
            Strikes.Step(tick);
            if (Result == Outcome.None)
            {
                if (Boss.Defeated) Finish(Outcome.Victory, tick);
                else if (Boss.ReachedTown) Finish(Outcome.Defeat, tick);
                else StepPressure(tick);
            }
            else StepEnding(tick);
            if (tick % 25 == 0) StepHousekeeping(tick);
        }

        private void StepPressure(int tick)
        {
            float phaseScale = Boss.Phase == 1 ? 1f : Boss.Phase == 2 ? 0.75f : 0.55f;
            if (tick >= nextRaidTick)
            {
                LaunchRaid(tick);
                nextRaidTick = tick + SimulationDriver.SecondsToTicks((36f + (float)rng.NextDouble() * 10f) * phaseScale);
            }
            if (tick >= nextEscortTick)
            {
                int cap = Boss.Phase == 1 ? 3 : Boss.Phase == 2 ? 4 : 5;
                if (AliveEscorts() < cap)
                {
                    bool tank = Boss.Phase >= 2 && rng.Next(2) == 0;
                    SpawnEscort(tank, Boss.GatePoint(rng.Next(2) == 0 ? 1f : -1f), Boss.transform.forward);
                    Say(Loc.Get(tank ? "mission.tank_out" : "mission.escort_out"), InfoColor);
                }
                nextEscortTick = tick + SimulationDriver.SecondsToTicks(32f * phaseScale);
            }
            if (Boss.Phase >= 2 && tick >= nextSquadTick && Count(UnitKind.Infantry) + Count(UnitKind.RocketTrooper) < 14)
            {
                DropSquad(tick);
                nextSquadTick = tick + SimulationDriver.SecondsToTicks(Boss.Phase >= 3 ? 20f : 26f);
            }
        }

        private void Finish(Outcome outcome, int tick)
        {
            SquadTarget = null;
            Result = outcome;
            FinalContribution = (playerCoreDamage, playerStations, rescuedAllies.Count);
            LastVictory = outcome == Outcome.Victory;
            endTick = tick + SimulationDriver.SecondsToTicks(outcome == Outcome.Victory ? 5.5f : 4.5f);
            Strikes.Clear();
            if (outcome == Outcome.Victory)
            {
                Say(Loc.Get("mission.victory"), GoodColor);
                foreach (var u in MissionUnit.All.ToArray()) if (u.Alive && !(u is BossPart)) u.Remove();
            }
            else
            {
                Say(Loc.Get("mission.defeat"), AlertColor);
                Boss.MarkDefeated();   // it stops at the gates; nothing more to shoot
            }
        }

        // A chain of blasts across the fortress (win) or the settlement (loss), then the results.
        private void StepEnding(int tick)
        {
            if (tick - lastBlastTick >= 9 && tick < endTick - 25)
            {
                lastBlastTick = tick;
                Vector3 at = Result == Outcome.Victory ? Boss.RandomHullPoint(rng)
                    : Map.Town + new Vector3(((float)rng.NextDouble() - 0.5f) * 70f, 3f, ((float)rng.NextDouble() - 0.5f) * 70f);
                MissionUnit.Explode(at, 2.5f);
            }
            if (tick == endTick - 25 && Result == Outcome.Victory) MissionUnit.Explode(Boss.transform.position + Vector3.up * 14f, 6f);
            if (tick < endTick) return;
            endTick = int.MaxValue;
            MissionUnit.Running = false;
            race.EndMission(Result == Outcome.Victory);
        }

        private void StepHousekeeping(int tick)
        {
            var director = RaceDirector.Current;
            if (director != null)
            {
                // Anything that left the desert (over the rim, through the ground) comes back.
                foreach (var car in director.Cars)
                    if (car != null && car.isActiveAndEnabled && !Map.Inside(car.transform.position)) car.ResetCar();
            }
            int wreckTicks = SimulationDriver.SecondsToTicks(7f);
            for (int i = escorts.Count - 1; i >= 0; i--)
            {
                var car = escorts[i];
                if (car == null) { escorts.RemoveAt(i); continue; }
                var h = car.GetComponent<CarHealth>();
                if (h == null || !h.IsDead) continue;
                if (!escortWreckTick.TryGetValue(car, out int at)) { escortWreckTick[car] = tick; continue; }
                if (tick - at < wreckTicks) continue;
                escortWreckTick.Remove(car);
                escorts.RemoveAt(i);
                car.gameObject.SetActive(false);
                Destroy(car.gameObject);
            }
        }

        // ---------- events ----------

        private void OnPhase(int phase)
        {
            Say(Loc.Get(phase == 2 ? "mission.phase2" : "mission.phase3"), AlertColor);
            var impulse = Camera.main != null ? Camera.main.GetComponent<CameraImpulse>() : null;
            if (impulse != null) impulse.AddImpulse(0.25f);
        }

        private void OnPartDestroyed(BossPart part)
        {
            if (part == SquadTarget) SquadTarget = null;
            if (part.Role == BossRole.Core) { Boss.MarkDefeated(); return; }
            // Every station shot off opens the armour a little: the core takes +15 % per lost station.
            Boss.Core.DamageTaken = 1f + 0.15f * (Boss.Parts.Count - 1 - AliveStations());
            string key = part.Role == BossRole.Cannon ? (Boss.AliveCount(BossRole.Cannon) == 0 ? "mission.cannons_down" : "mission.cannon_down")
                : part.Role == BossRole.Flak ? "mission.flak_down"
                : part.Role == BossRole.Mortar ? "mission.mortar_down" : "mission.hangar_down";
            Say(Loc.Get(key), GoodColor);
        }

        private void OnUnitDamaged(MissionUnit unit, GameObject attacker, float damage)
        {
            if (Result == Outcome.None && unit == Boss.Core && race.Player != null && attacker == race.Player.gameObject)
                playerCoreDamage += damage;
        }

        private void RescueAllies(Vector3 point, float radius)
        {
            foreach (var teammate in race.Participants)
            {
                if (teammate == null || teammate.isPlayer || teammate.Health == null || teammate.Health.IsDead || teammate.Health.IsInvulnerable) continue;
                if ((teammate.transform.position - point).sqrMagnitude <= radius * radius)
                    rescuedAllies.Add(teammate.Car.CarId);
            }
        }

        private void OnUnitKilled(MissionUnit unit, GameObject attacker)
        {
            bool personal = Result == Outcome.None && race.Player != null && attacker == race.Player.gameObject;
            if (unit.Kind == UnitKind.Plane) Strikes.CancelFrom(unit.gameObject, SimulationDriver.Instance.Tick, personal ? RescueAllies : (Action<Vector3, float>)null);
            if (personal && unit.Kind == UnitKind.BossPart && unit is BossPart station && station.Boss == Boss) playerStations++;
            if (attacker == null || Teams.Of(attacker) != Team.Players) return;
            var p = attacker.GetComponentInParent<RaceParticipant>();
            if (p == null) return;
            if (unit.Kind != UnitKind.BossHull) p.KillTally.Record(unit.KillId);
            if (p.isPlayer && unit.Kind == UnitKind.Plane) Say(Loc.Get("mission.plane_down"), GoodColor);
        }

        private void Say(string text, Color color) => Announce?.Invoke(text, color);

        // ---------- queries (bots, HUD) ----------

        public int Count(UnitKind kind)
        {
            int n = 0;
            foreach (var u in MissionUnit.All) if (u.Alive && u.Kind == kind) n++;
            return n;
        }

        private int AliveStations()
        {
            int n = 0;
            foreach (var p in Boss.Parts) if (p != Boss.Core && p.Alive) n++;
            return n;
        }

        public int AliveEscorts()
        {
            int n = 0;
            foreach (var car in escorts)
            {
                if (car == null) continue;
                var h = car.GetComponent<CarHealth>();
                if (h != null && !h.IsDead) n++;
            }
            return n;
        }

        public int EnemiesAlive => AliveEscorts() + Count(UnitKind.Drone) + Count(UnitKind.Infantry) + Count(UnitKind.RocketTrooper) + Count(UnitKind.Turret) + Count(UnitKind.Plane);

        public CarController NearestCar(Vector3 from, Team team, float range)
        {
            var director = RaceDirector.Current;
            if (director == null) return null;
            CarController best = null;
            float bestD = range * range;
            foreach (var car in director.Cars)
            {
                if (car == null || !car.isActiveAndEnabled || Teams.Of(car.gameObject) != team) continue;
                var h = car.GetComponent<CarHealth>();
                if (h != null && h.IsDead) continue;
                float d = (car.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = car; }
            }
            return best;
        }

        public MissionUnit NearestGroundUnit(Vector3 from, float range)
        {
            MissionUnit best = null;
            float bestD = range * range;
            foreach (var u in MissionUnit.All)
            {
                if (!u.Targetable || (u.Kind != UnitKind.Infantry && u.Kind != UnitKind.RocketTrooper && u.Kind != UnitKind.Turret)) continue;
                if (Boss != null && !Boss.Defeated && NearHull(u.transform.position, 12f)) continue;   // troopers at the ramps: not worth the tracks
                float d = (u.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = u; }
            }
            return best;
        }

        /// <summary>True when a point is within <paramref name="margin"/> metres of the fortress's footprint.</summary>
        public bool NearHull(Vector3 point, float margin)
        {
            Vector3 local = Boss.transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) < BossCrawler.Width * 0.5f + margin && local.z > -BossCrawler.Length * 0.5f - margin && local.z < BossCrawler.Length * 0.5f + 20f + margin;
        }

        /// <summary>Coins for the mission: a win pays well above a race; a loss pays for the damage done.</summary>
        public int Reward(RaceResultEntry me, float tierMultiplier)
            => Payout(me, tierMultiplier).Total;

        public Core.Economy.RacePayout Payout(RaceResultEntry me, float tierMultiplier)
        {
            if (me == null) return new Core.Economy.RacePayout(0, 0, tierMultiplier);
            int kills = me.Kills;
            float damage = Boss != null ? 1f - Boss.CoreFraction : 0f;
            int baseCoins = Result == Outcome.Victory ? 320 : Mathf.RoundToInt(40f + 200f * damage);
            int combatCoins = Result == Outcome.Victory ? Mathf.Min(kills * 6, 240) : Mathf.Min(kills * 4, 120);
            return new Core.Economy.RacePayout(baseCoins, combatCoins, tierMultiplier);
        }

        private void Update() { if (Strikes != null && SimulationDriver.Current != null) Strikes.UpdateVisuals(SimulationDriver.Current.Tick, 0f); }

        // ---------- teardown ----------

        /// <summary>Remove everything the mission added and give the circuit back. Idempotent.</summary>
        public void Dispose()
        {
            MissionUnit.Running = false;
            MissionUnit.Killed -= OnUnitKilled;
            MissionUnit.Damaged -= OnUnitDamaged;
            BossPart.RequestDrone = null;
            BossPart.RequestStrike = null;
            if (race != null)
            {
                race.OnFieldSpawned -= OnFieldSpawned;
                race.OnRaceStarted -= OnStarted;
                if (race.spawner != null && ReferenceEquals(race.spawner.Site, this)) race.spawner.Site = null;
            }
            foreach (var u in MissionUnit.All.ToArray()) if (u != null) { u.gameObject.SetActive(false); Destroy(u.gameObject); }
            MissionUnit.All.Clear();
            foreach (var car in escorts) if (car != null) { car.gameObject.SetActive(false); Destroy(car.gameObject); }
            escorts.Clear();
            if (Strikes != null) Strikes.Clear();
            if (Boss != null) Destroy(Boss.gameObject);
            if (Map != null) Destroy(Map.gameObject);
            if (missionHud != null) missionHud.Dispose();
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
            if (carLayer >= 0) Physics.IgnoreLayerCollision(carLayer, MissionLayers.SoftUnit, false);
            MissionArt.Clear();
            if (Current == this) Current = null;
        }

        private void OnDestroy() { if (Current == this) Dispose(); }
    }
}
