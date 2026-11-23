using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    public sealed partial class TrackFork
    {
        private readonly Dictionary<RaceParticipant, ForkRoute> admitted = new Dictionary<RaceParticipant, ForkRoute>();
        private RaceManager volcanoRace;
        private HudScreen volcanoHud;
        private RaceProgress volcanoPlayer;
        private BoxCollider mainGate, shortcutGate;
        private bool warnedChange, announcedApproach;
        public ForkRoute OpenVolcanoRoute { get; private set; } = ForkRoute.MainRoad;

        private void BuildVolcano(Material material)
        {
            var lava = new MeshChunkWriter("VolcanoCrater");
            Vector3 caldera = points[Steps / 2] + Right(Steps / 2) * 18f;
            caldera.y = -.1f;
            var rock = new Color32(75, 55, 58, 0);
            var ember = new Color32(255, 102, 37, 0);
            LowPoly.Cylinder(lava, Matrix4x4.identity, caldera, 13f, 5f, 12f, 13, rock);
            LowPoly.Cylinder(lava, Matrix4x4.identity, caldera + Vector3.up * 12.01f, 4.3f, 4.1f, .18f, 12, ember);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 1.27f + .25f;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                Vector3 a = caldera + dir * 4.9f + Vector3.up * 12.1f, b = caldera + dir * 13.5f + Vector3.up * .16f;
                Vector3 side = Vector3.Cross(Vector3.up, dir) * .42f;
                lava.AddQuad(a - side, a + side, b + side, b - side, ember);
            }
            AddMeshes(lava, material, false);
            const float fraction = .36f;
            float d = ApproachDistance + MainSpan * fraction;
            mainGate = BuildGate("VolcanoMainGate", path.Position(d), path.Tangent(d), path.halfWidth * 2f, material);
            shortcutGate = BuildGate("VolcanoShortcutGate", Position(fraction), Position(fraction + .015f) - Position(fraction - .015f), RoadHalfWidth * 2f, material);
            ApplyVolcanoGates();
        }

        private BoxCollider BuildGate(string name, Vector3 p, Vector3 forward, float width, Material material)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(p, Quaternion.LookRotation(forward, Vector3.up));
            root.layer = LayerMask.NameToLayer("Track");
            var writer = new MeshChunkWriter(name + "Bars");
            var steel = new Color32(68, 60, 62, 0);
            var red = new Color32(215, 60, 43, 0);
            var amber = new Color32(255, 202, 73, 0);
            for (int side = -1; side <= 1; side += 2)
                LowPoly.Box(writer, Matrix4x4.identity, new Vector3(side * (width * .5f - .12f), 1.1f, 0f), new Vector3(.24f, 2.2f, .35f), steel);
            for (int k = 0; k < 8; k++)
                LowPoly.Box(writer, Matrix4x4.identity, new Vector3(width * ((k + .5f) / 8f - .5f), 1.2f, 0f), new Vector3(width / 8f, .43f, .32f), k % 2 == 0 ? red : amber);
            var mesh = writer.Finish()[0];
            meshes.Add(mesh);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material;
            var box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * .85f;
            box.size = new Vector3(width, 1.7f, .35f);
            return box;
        }

        public void FollowVolcano(RaceManager race, HudScreen hud)
        {
            if (Course != MountainFork.Volcano)
                return;
            volcanoRace = race;
            volcanoHud = hud;
            volcanoPlayer = race != null && race.Player != null ? race.Player.Progress : null;
            if (volcanoPlayer != null)
                volcanoPlayer.LapCompleted += OnVolcanoLap;
        }

        private void OnVolcanoLap(int completed, double time)
        {
            if (completed != 1)
                return;
            // Fence positions are sampled before switching. A car still between the sign and
            // the old barrier has already committed to that route and may clear it.
            var player = volcanoRace.Player;
            foreach (var racer in volcanoRace.Participants)
                if (racer != player) RecordVolcanoApproach(racer);
            if (player != null && admitted.TryGetValue(player, out var oldRoute))
            {
                GrantPass(player, oldRoute, false);
                admitted.Remove(player);
            }
            OpenVolcanoRoute = ForkRoute.Shortcut;
            ApplyVolcanoGates();
            foreach (var entry in admitted)
                GrantPass(entry.Key, entry.Value, true);
            volcanoHud?.ShowHint(Loc.Get("volcano.shortcut_open"), 5f);
            announcedApproach = false;
        }

        private void ApplyVolcanoGates()
        {
            mainGate.gameObject.SetActive(OpenVolcanoRoute != ForkRoute.MainRoad);
            shortcutGate.gameObject.SetActive(OpenVolcanoRoute != ForkRoute.Shortcut);
        }

        private void FixedUpdate()
        {
            if (volcanoRace == null || volcanoRace.State < RaceState.Racing || volcanoRace.State > RaceState.Finishing)
                return;
            foreach (var racer in volcanoRace.Participants)
                RecordVolcanoApproach(racer);
            var player = volcanoRace.Player;
            if (player == null)
                return;
            float toFinish = path.Wrap(path.startLineDistance - player.Progress.Distance);
            if (!warnedChange && player.Progress.LapsCompleted == 0 && player.Progress.Counter.LineArmed && player.Progress.GatesPassedThisLap == 0 && toFinish < 75f)
            {
                warnedChange = true;
                volcanoHud?.ShowHint(Loc.Get("volcano.changing"), 5f);
            }
            float along = path.SignedDelta(ApproachDistance, player.Progress.Distance);
            if (along >= -52f && along < -20f && !announcedApproach)
            {
                announcedApproach = true;
                volcanoHud?.ShowHint(Loc.Get(OpenVolcanoRoute == ForkRoute.MainRoad ? "volcano.main_open" : "volcano.shortcut_open"), 5f);
            }
        }

        private void RecordVolcanoApproach(RaceParticipant racer)
        {
            if (racer == null || racer.Progress == null)
                return;
            float along = path.SignedDelta(ApproachDistance, racer.Progress.Distance);
            if (along >= -DecisionLead && along <= MainSpan + 4f)
            {
                if (admitted.ContainsKey(racer))
                    return;
                admitted.Add(racer, OpenVolcanoRoute);
                GrantPass(racer, OpenVolcanoRoute, true);
            }
            else if (admitted.TryGetValue(racer, out var prior))
            {
                GrantPass(racer, prior, false);
                admitted.Remove(racer);
            }
        }

        private void GrantPass(RaceParticipant racer, ForkRoute route, bool allow)
        {
            if (racer == null)
                return;
            var gate = route == ForkRoute.Shortcut ? shortcutGate : mainGate;
            if (gate == null || !gate.gameObject.activeInHierarchy)
                return;
            foreach (var part in racer.GetComponentsInChildren<Collider>())
                if (part.enabled && !part.isTrigger && part.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(gate, part, allow);
        }

        public ForkRoute VolcanoRouteFor(RaceParticipant racer)
        {
            RecordVolcanoApproach(racer);
            return racer != null && admitted.TryGetValue(racer, out var previous) ? previous : OpenVolcanoRoute;
        }

        private void ReleaseVolcano()
        {
            if (volcanoPlayer != null)
                volcanoPlayer.LapCompleted -= OnVolcanoLap;
            volcanoPlayer = null;
            foreach (var entry in admitted)
                GrantPass(entry.Key, entry.Value, false);
            admitted.Clear();
            volcanoRace = null;
            volcanoHud = null;
        }
    }
}
