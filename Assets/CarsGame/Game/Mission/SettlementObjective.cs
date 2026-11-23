using RacingGame.World.Gen;
using UnityEngine;

namespace RacingGame
{
    public sealed class SettlementObjective : MissionUnit
    {
        private Rigidbody body;
        private Vector3 velocity;
        public override Vector3 AimPoint => transform.position + transform.up * 2.1f;
        public override Vector3 Velocity => velocity;

        public static SettlementObjective Tanker(Transform parent, Pose pose)
        {
            var go = new GameObject("WaterTanker");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var objective = go.AddComponent<SettlementObjective>();
            objective.Kind = UnitKind.ProtectedObjective;
            objective.Radius = 3f;
            objective.SetHealth(85f);
            Teams.Set(go, Team.Players);
            MissionArt.Attach(go.transform, MissionArt.Cached("WaterTanker", TankerShape), 0);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.8f, 0f); box.size = new Vector3(3.2f, 2.9f, 8.4f);
            objective.body = go.AddComponent<Rigidbody>();
            objective.body.isKinematic = true;
            objective.CollectRenderers();
            return objective;
        }

        private static void TankerShape(MeshChunkWriter solid, MeshChunkWriter glow)
        {
            var frame = Matrix4x4.identity;
            LowPoly.Box(solid, frame, Vector3.up * 1.1f, new Vector3(3f, .6f, 8.5f), MissionArt.Iron);
            LowPoly.Box(solid, frame, new Vector3(0, 2.3f, 2.7f), new Vector3(3f, 2.2f, 2.8f), MissionArt.Hazard);
            LowPoly.Box(solid, frame, new Vector3(0, 2.7f, 4.12f), new Vector3(2.45f, .7f, .08f), MissionArt.Tar);
            var tank = Matrix4x4.TRS(new Vector3(0, 2.45f, -4f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            LowPoly.Cylinder(solid, tank, Vector3.zero, 1.38f, 1.38f, 5.2f, 12, MissionArt.Bone);
            foreach (float y in new[] { .55f, 4.4f }) LowPoly.Cylinder(glow, tank, Vector3.up * y, 1.41f, 1.41f, .25f, 12, MissionArt.CyanGlow);
            foreach (float x in new[] { -1.68f, 1.25f })
                foreach (float z in new[] { -3f, -1.1f, 2.9f })
                    LowPoly.Cylinder(solid, Matrix4x4.TRS(new Vector3(x, .75f, z), Quaternion.Euler(0, 0, -90), Vector3.one),
                        Vector3.zero, .75f, .75f, .43f, 10, MissionArt.Tar);
        }

        public void Travel(Pose pose, Vector3 speed)
        {
            velocity = speed;
            body.MovePosition(pose.position);
            body.MoveRotation(pose.rotation);
        }

        protected override void Step(int tick) { }
    }
}
