using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public sealed class TrainRoute : MonoBehaviour
    {
        public Transform Locomotive { get; private set; }
        public Transform Tail { get; private set; }
        public Transform RampExit { get; private set; }
        public Transform Trophy { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Pose StartPose { get; private set; }
        public Vector3 SwitchPoint { get; private set; }
        public Vector3 StationPoint { get; private set; }
        public bool PassedSwitch { get; private set; }
        public bool PassedWhistlePoint { get; private set; }
        public bool AtBorder { get; private set; }
        public bool AtStation { get; private set; }

        private const float TailOffset = 33f, RoadOffset = 11f, EscapeSeconds = 90f;
        private TrainLine approach, border, siding;
        private TrainLine[] roads;
        private TrainRouteArt art;
        private Transform map, lever;
        private readonly Rigidbody[] cars = new Rigidbody[3];
        private float distance, whistleDistance, speed;
        private bool diverted;

        public void Build(Material scenery)
        {
            map = MissionArt.Pivot(transform, "DryGulch", new Vector3(4000f, 0f, -400f));
            approach = new TrainLine(P(0, -80), P(0, 70), P(0, 155), P(-60, 265),
                P(-205, 402), P(-215, 546), P(-145, 657), P(-25, 773), P(0, 825), P(0, 875));
            border = new TrainLine(P(0, 875), P(0, 923), P(-42, 1068), P(-98, 1235), P(-85, 1510));
            siding = new TrainLine(P(0, 875), P(0, 895), P(38, 972), P(87, 1040), P(92, 1100));
            var wash = new TrainLine(P(11, 128), P(23, 163), P(53, 227), P(48, 310), P(28, 394),
                P(41, 484), P(65, 574), P(51, 678), P(29, 782), P(24, 865), P(12, 894));
            roads = new[] { approach.Offset(RoadOffset), wash, border.Offset(RoadOffset), siding.Offset(RoadOffset) };
            distance = approach.DistanceNearest(P(0, 73f));
            speed = (approach.Length + border.Length - distance) / EscapeSeconds;
            whistleDistance = approach.DistanceNearest(P(-150, 353));
            StartPose = GroundPose(roads[0].At(roads[0].DistanceNearest(P(11, 0))));
            SwitchPoint = map.TransformPoint(P(24, 865));
            Pose pickup = siding.At(siding.Length - TailOffset);
            StationPoint = map.TransformPoint(pickup.position + pickup.rotation * Vector3.right * RoadOffset);

            art = new TrainRouteArt(scenery);
            Pose borderGate = border.At(border.Length - TailOffset + TrainRouteArt.TrophyDeckZ);
            lever = art.BuildCanyon(map, approach, border, siding, roads, map.InverseTransformPoint(SwitchPoint),
                map.InverseTransformPoint(StationPoint), borderGate);
            for (int i = 0; i < cars.Length; i++) cars[i] = art.BuildWagon(map, i);
            Locomotive = cars[0].transform;
            Tail = cars[2].transform;
            Trophy = art.BuildTrophy(Tail);
            RampExit = MissionArt.Pivot(Tail, "RampExit", new Vector3(8.3f, 0.12f, -2f));
            RampExit.localRotation = Quaternion.Euler(0f, 90f, 0f);
            PlaceTrain(true);
        }

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            if (AtBorder || AtStation) { Velocity = Vector3.zero; return; }
            var branch = diverted ? siding : border;
            float end = approach.Length + branch.Length;
            float pace = diverted ? Mathf.Min(speed, Mathf.Sqrt(6f * (end - distance))) : speed;
            distance = Mathf.Min(end, distance + Mathf.Max(0.5f, pace) * dt);
            PassedWhistlePoint = distance >= whistleDistance;
            PassedSwitch = distance + TrainRouteArt.HalfWagonLength >= approach.Length;
            AtStation = diverted && distance >= end;
            AtBorder = !diverted && distance >= end;
            PlaceTrain(false);
            Velocity = AtStation ? Vector3.zero : map.rotation * Rail(distance - TailOffset).rotation * Vector3.forward * pace;
        }

        public void Divert()
        {
            if (PassedSwitch || diverted) return;
            diverted = true;
            lever.localRotation = Quaternion.Euler(0f, 0f, -48f);
        }

        public bool InSwitchZone(Vector3 position) => InYard(position, SwitchPoint, 8f);
        public bool InStationZone(Vector3 position) => InYard(position, StationPoint, 10f);
        private static bool InYard(Vector3 position, Vector3 target, float radius)
        {
            Vector3 delta = position - target;
            return Mathf.Abs(delta.y) <= 4f && delta.x * delta.x + delta.z * delta.z <= radius * radius;
        }

        public bool InCaptureZone(Vector3 position)
        {
            Vector3 local = Tail.InverseTransformPoint(position);
            return local.x >= 4f && local.x <= 17f && Mathf.Abs(local.z) <= 8f && local.y >= -0.6f && local.y <= 3.2f;
        }

        public void CollectTrophy(Transform car)
        {
            if (car == null || Trophy == null || Trophy.parent != Tail) return;
            var hull = car.GetComponent<BoxCollider>();
            var size = hull != null ? hull.size : new Vector3(1.85f, 0.7f, 4.2f);
            var center = hull != null ? hull.center : new Vector3(0f, 0.45f, 0.05f);
            Trophy.SetParent(car, false);
            Trophy.localPosition = center + new Vector3(0f, size.y * 0.5f, -size.z * 0.34f);
            Trophy.localRotation = Quaternion.identity;
            Trophy.localScale = Vector3.one * Mathf.Clamp(size.x * 0.24f, 0.35f, 0.6f);
        }

        /// <summary>World pose on the road beside the active rails, ahead in metres along that road.</summary>
        public Pose AlongsideRoad(Vector3 world, float ahead)
        {
            Vector3 local = map.InverseTransformPoint(world);
            TrainLine mainRoad = roads[0], branchRoad = roads[diverted ? 3 : 2];
            float mainAt = mainRoad.DistanceNearest(local), branchAt = branchRoad.DistanceNearest(local);
            float mainError = (mainRoad.At(mainAt).position - local).sqrMagnitude;
            float branchError = (branchRoad.At(branchAt).position - local).sqrMagnitude;
            float at = (mainError <= branchError ? mainAt : mainRoad.Length + branchAt) + ahead;
            return GroundPose(at <= mainRoad.Length ? mainRoad.At(at) : branchRoad.At(at - mainRoad.Length));
        }

        public Pose RecoveryPose(Vector3 position)
        {
            Vector3 at = map.InverseTransformPoint(position);
            Pose nearest = default;
            float best = float.PositiveInfinity;
            foreach (var road in roads)
            {
                Pose candidate = road.At(road.DistanceNearest(at));
                float error = (at - candidate.position).sqrMagnitude;
                if (error >= best) continue;
                best = error;
                nearest = candidate;
            }
            return GroundPose(nearest);
        }

        private static Vector3 P(float x, float z) => new Vector3(x, 0f, z);
        private Pose GroundPose(Pose p) => new Pose(map.TransformPoint(p.position + Vector3.up * 0.65f), map.rotation * p.rotation);
        private Pose Rail(float at) => at <= approach.Length ? approach.At(at) : (diverted ? siding : border).At(at - approach.Length);

        private void PlaceTrain(bool initial)
        {
            for (int i = 0; i < cars.Length; i++)
            {
                Pose pose = Rail(distance - i * (TailOffset * 0.5f));
                var position = map.TransformPoint(pose.position);
                var rotation = map.rotation * pose.rotation;
                if (initial)
                {
                    // Spawned cargo and the HUD read these transforms before the first physics step.
                    cars[i].transform.SetPositionAndRotation(position, rotation);
                    cars[i].position = position;
                    cars[i].rotation = rotation;
                }
                else { cars[i].MovePosition(position); cars[i].MoveRotation(rotation); }
            }
        }

        private void OnDestroy()
        {
            if (Trophy != null && Trophy.parent != Tail)
            {
                if (Application.isPlaying) Destroy(Trophy.gameObject); else DestroyImmediate(Trophy.gameObject);
            }
            art?.Dispose();
        }
    }

    // An open, arc-length sampled line: every wagon uses the same distance scale across the switch.
    internal sealed class TrainLine
    {
        public readonly Vector3[] Points;
        private readonly float[] metres;
        public float Length => metres[metres.Length - 1];

        public TrainLine(params Vector3[] controls) : this(Sample(controls)) { }
        private TrainLine(List<Vector3> points)
        {
            Points = points.ToArray();
            metres = new float[Points.Length];
            for (int i = 1; i < Points.Length; i++) metres[i] = metres[i - 1] + Vector3.Distance(Points[i - 1], Points[i]);
        }

        private static List<Vector3> Sample(Vector3[] c)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < c.Length - 1; i++)
            {
                Vector3 p0 = c[Mathf.Max(0, i - 1)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(c.Length - 1, i + 2)];
                int count = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 6f));
                for (int j = 0; j < count; j++)
                {
                    float t = (float)j / count;
                    points.Add(0.5f * (2f*p1 + (p2-p0)*t + (2f*p0-5f*p1+4f*p2-p3)*t*t + (3f*p1-p0-3f*p2+p3)*t*t*t));
                }
            }
            points.Add(c[c.Length - 1]);
            return points;
        }

        public Vector3 Right(int i) => Vector3.Cross(Vector3.up, Points[Mathf.Min(i + 1, Points.Length - 1)] - Points[Mathf.Max(0, i - 1)]).normalized;
        public TrainLine Offset(float lateral)
        {
            var points = new List<Vector3>(Points.Length);
            for (int i = 0; i < Points.Length; i++) points.Add(Points[i] + Right(i) * lateral);
            return new TrainLine(points);
        }

        public Pose At(float at)
        {
            at = Mathf.Clamp(at, 0f, Length);
            int end = System.Array.BinarySearch(metres, at);
            end = Mathf.Clamp(end < 0 ? ~end : end, 1, Points.Length - 1);
            float t = Mathf.InverseLerp(metres[end - 1], metres[end], at);
            Vector3 right = Vector3.Lerp(Right(end - 1), Right(end), t);
            return new Pose(Vector3.Lerp(Points[end - 1], Points[end], t), Quaternion.LookRotation(Vector3.Cross(right, Vector3.up)));
        }

        public float DistanceNearest(Vector3 point)
        {
            float best = float.PositiveInfinity, at = 0f;
            for (int i = 1; i < Points.Length; i++)
            {
                Vector3 edge = Points[i] - Points[i - 1];
                float t = Mathf.Clamp01(Vector3.Dot(point - Points[i - 1], edge) / edge.sqrMagnitude);
                float error = (point - (Points[i - 1] + edge * t)).sqrMagnitude;
                if (error >= best) continue;
                best = error;
                at = Mathf.Lerp(metres[i - 1], metres[i], t);
            }
            return at;
        }
    }
}
