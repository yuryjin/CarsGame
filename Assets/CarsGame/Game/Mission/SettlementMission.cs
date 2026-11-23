using System.Collections.Generic;
using RacingGame.Core.Sim;
using RacingGame.World.Gen;
using UnityEngine;

namespace RacingGame
{
    public sealed class SettlementMission : MonoBehaviour, ISimulated, IBattleSite
    {
        public const string TankerEvent = "m3";
        public static bool Supports(string id) => id == TankerEvent;
        public static SettlementMission Current { get; private set; }
        public RaceManager Race { get; private set; }
        public DesertMap Map { get; private set; }
        public SettlementObjective Objective { get; private set; }
        public float Remaining => Mathf.Max(0f, Map.RouteLength - distance);
        public Vector3 Destination => Map.Town;
        public IReadOnlyList<TrooperUnit> Attackers => attackers;
        public bool Threatened => ClosestThreat(57f) != null;
        public int SimulationOrder => int.MaxValue - 6000;

        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<TrooperUnit> attackers = new List<TrooperUnit>();
        private float distance;
        private bool finished;
        private const float TankerSpeed = 9f;

        public static SettlementMission Create(RaceManager race)
        {
            var m = new GameObject("SettlementMission").AddComponent<SettlementMission>();
            Current = m;
            m.Race = race;
            MissionArt.Init(AbyssTrack.SceneryMaterial());
            AbyssTrack.HideScenery(m.hidden, AbyssTrack.Scenery);
            AbyssTrack.HideScenery(m.hidden, new[] { "Environment/Mountains" });
            m.Map = DesertMap.Create();
            m.Map.transform.SetParent(m.transform, true);
            m.distance = m.Map.RouteLength - 245f;
            m.Objective = SettlementObjective.Tanker(m.transform, m.RoutePose(m.distance));
            m.BuildDestination();
            m.SpawnRaider(m.distance + 68f, -9f, false);
            m.SpawnRaider(m.distance + 90f, 11f, true);
            m.SpawnRaider(m.distance + 151f, -8f, false);
            MissionUnit.Killed += m.OnKilled;
            race.spawner.Site = m;
            race.OnFieldSpawned += m.OnFieldSpawned;
            race.OnRaceStarted += m.OnStarted;
            return m;
        }

        private Pose RoutePose(float along)
        {
            Vector3 p = Map.RoutePoint(along);
            return new Pose(p, Quaternion.LookRotation(Map.RouteForward(along)));
        }

        private void BuildDestination()
        {
            var finish = MissionArt.Pivot(transform, "TankerDestination", Vector3.zero);
            finish.SetPositionAndRotation(Destination, Quaternion.LookRotation(Map.RouteForward(Map.RouteLength)));
            MissionArt.Attach(finish, MissionArt.Cached("DeliveryArch", (w, g) =>
            {
                foreach (float x in new[] { -6f, 6f })
                {
                    LowPoly.Box(w, Matrix4x4.identity, new Vector3(x, 3f, 0), new Vector3(.4f, 6f, .4f), MissionArt.Iron);
                    LowPoly.Box(g, Matrix4x4.identity, new Vector3(x, 5f, 0), new Vector3(.5f, 1.3f, .5f), MissionArt.CyanGlow);
                }
                LowPoly.Box(g, Matrix4x4.identity, Vector3.up * 6f, new Vector3(12.5f, .65f, .6f), MissionArt.CyanGlow);
            }), 0);
        }

        private void SpawnRaider(float along, float side, bool rocket)
        {
            var pose = RoutePose(along);
            Vector3 p = pose.position + pose.rotation * Vector3.right * side;
            var enemy = TrooperUnit.Spawn(Map, p, rocket, attackers.Count + 1);
            enemy.transform.SetParent(transform, true);
            enemy.AssaultTarget = Objective;
            attackers.Add(enemy);
        }

        public TrooperUnit ClosestThreat(float maxDistance)
        {
            TrooperUnit nearest = null;
            float closest = maxDistance * maxDistance;
            foreach (var enemy in attackers)
            {
                if (enemy == null || !enemy.Targetable) continue;
                float d = (enemy.transform.position - Objective.transform.position).sqrMagnitude;
                if (d > closest) continue;
                closest = d; nearest = enemy;
            }
            return nearest;
        }

        private void OnFieldSpawned()
        {
            if (Race.Player != null) Teams.Set(Race.Player.gameObject, Team.Players);
        }

        private void OnStarted() => MissionUnit.Running = true;

        private void OnKilled(MissionUnit unit, GameObject attacker)
        {
            if (!finished && unit.Kind != UnitKind.ProtectedObjective && Race.Player != null && attacker == Race.Player.gameObject)
                Race.Player.KillTally.Record(unit.KillId);
        }

        public void SimulationStep(int tick)
        {
            if (finished || Race == null || Race.State != RaceState.Racing || PauseController.IsPaused) return;
            if (Objective.Alive && Remaining > 0f)
            {
                distance = Mathf.Min(Map.RouteLength, distance + TankerSpeed * SimulationDriver.FixedStep);
                var pose = RoutePose(distance);
                Objective.Travel(pose, pose.rotation * Vector3.forward * TankerSpeed);
            }
            if (Objective.Alive && Remaining > 0f) return;
            finished = true;
            MissionUnit.Running = false;
            Race.EndMission(Objective.Alive);
        }

        public void GetSpawnPose(int slot, int total, out Vector3 position, out Quaternion rotation)
        {
            var pose = RoutePose(distance);
            Map.GroundPose(pose.position + pose.rotation * new Vector3(5f, 0f, 32f), pose.rotation * Vector3.forward, out position, out rotation);
        }

        public bool TryGetRecoveryPose(CarController car, out Vector3 position, out Quaternion rotation)
        {
            GetSpawnPose(0, 1, out position, out rotation);
            return true;
        }

        public ICarInputSource CreateBotDriver(GameObject car, AiDriverProfile profile) => null;
        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable() { if (SimulationDriver.Current != null) SimulationDriver.Current.Unregister(this); }
        private void OnDestroy() { if (Current == this) Dispose(); }
        public void Dispose()
        {
            if (Current != this) return;
            MissionUnit.Running = false;
            MissionUnit.Killed -= OnKilled;
            Race.OnFieldSpawned -= OnFieldSpawned;
            Race.OnRaceStarted -= OnStarted;
            if (ReferenceEquals(Race.spawner.Site, this)) Race.spawner.Site = null;
            gameObject.SetActive(false);
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            MissionArt.Clear();
            Current = null;
        }
    }
}
