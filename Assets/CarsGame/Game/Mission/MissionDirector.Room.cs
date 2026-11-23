using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public struct RoomActor
    {
        public const byte Armored = 5, Tank = 6;
        public int id;
        public byte kind;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
    }

    public sealed partial class MissionDirector
    {
        private readonly Dictionary<int, GameObject> remoteActors = new Dictionary<int, GameObject>();
        private readonly List<int> departed = new List<int>();
        private readonly HashSet<int> present = new HashSet<int>();

        public void CopyRoomActors(List<RoomActor> actors)
        {
            actors.Clear();
            foreach (var unit in MissionUnit.All)
                if (unit.Alive && !unit.Hidden && unit.Kind <= UnitKind.Plane)
                    Add(unit.transform,unit.KillId,(byte)unit.Kind,unit.Health);
            foreach (var car in escorts)
            {
                if (car == null || !car.TryGetComponent<CarHealth>(out var hp) || hp.IsDead) continue;
                bool tank = car.GetComponent<CarBody>().Class == Core.Economy.BodyClass.Tank;
                Add(car.transform,-1-car.CarId,tank ? RoomActor.Tank : RoomActor.Armored,hp.Health);
            }
            void Add(Transform t, int id, byte kind, float health) => actors.Add(new RoomActor {
                id = id, kind = kind, position = t.position, rotation = t.rotation, health = health });
        }

        public void ApplyRoomActors(List<RoomActor> actors)
        {
            if (!GameFlow.RoomReplica) return;
            present.Clear();
            foreach (var a in actors)
            {
                present.Add(a.id);
                if (!remoteActors.TryGetValue(a.id,out var go))
                {
                    Vector3 forward = a.rotation * Vector3.forward;
                    switch (a.kind)
                    {
                        case (byte)UnitKind.Drone: go = DroneUnit.Spawn(Map,a.position,a.id).gameObject; break;
                        case (byte)UnitKind.Infantry:
                        case (byte)UnitKind.RocketTrooper: go = TrooperUnit.Spawn(Map,a.position,a.kind == (byte)UnitKind.RocketTrooper,a.id).gameObject; break;
                        case (byte)UnitKind.Turret: go = GroundTurretUnit.Spawn(Map,a.position,forward).gameObject; break;
                        case (byte)UnitKind.Plane: go = PlaneUnit.Spawn(Map,a.position,a.position + forward * 300f).gameObject; break;
                        default: go = SpawnEscort(a.kind == RoomActor.Tank,a.position,forward)?.gameObject; break;
                    }
                    if (go == null) continue;
                    remoteActors.Add(a.id,go);
                }
                if (go.TryGetComponent<Rigidbody>(out var body))
                {
                    body.isKinematic = true;
                    body.position = a.position;
                    body.rotation = a.rotation;
                }
                go.transform.SetPositionAndRotation(a.position,a.rotation);
                if (go.TryGetComponent<MissionUnit>(out var unit)) unit.ApplyRoomHealth(a.health);
                else if (go.TryGetComponent<CarHealth>(out var hp)) hp.ApplyRoomHealth(a.health,false);
            }
            departed.Clear();
            foreach (var pair in remoteActors)
            {
                if (present.Contains(pair.Key)) continue;
                var go = pair.Value;
                if (go != null)
                {
                    if (go.TryGetComponent<CarController>(out var car)) escorts.Remove(car);
                    go.SetActive(false);
                    Destroy(go);
                }
                departed.Add(pair.Key);
            }
            foreach (int id in departed) remoteActors.Remove(id);
        }

        public void ApplyRoomOutcome(Outcome outcome)
        {
            if (GameFlow.RoomReplica && Result == Outcome.None && outcome != Outcome.None)
                Finish(outcome,SimulationDriver.Instance.Tick);
        }
    }
}
