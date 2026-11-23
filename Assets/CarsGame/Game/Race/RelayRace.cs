using System.Collections.Generic;
using RacingGame.Core.Economy;
using RacingGame.World.Gen;
using UnityEngine;

namespace RacingGame
{
    public sealed class RelayRace : MonoBehaviour, ISimulated
    {
        public const string Loaner = "dune";
        private RaceManager race;
        private TrackPath path;
        private HudScreen hud;
        private string second;
        private List<Mesh> meshes;
        public int SimulationOrder => int.MaxValue - 5100;
        public float GateDistance { get; private set; }
        public Vector3 GatePosition { get; private set; }
        public bool Exchanged { get; private set; }

        public static List<BodyDef> Available()
        {
            var owned = PlatformGlue.Save?.Data.progress.cosmetics;
            var result = new List<BodyDef>();
            foreach (var body in CarBodies.All)
                if (body.Id == Loaner || CarBodies.Owns(owned, body.Id)) result.Add(body);
            return result;
        }

        public static string First(RaceEventDefinition ev)
        {
            var available = Available();
            var selected = PlatformGlue.Save?.Data.progress.body;
            if (available.Exists(b => b.Id == ev.RelayFirst)) return ev.RelayFirst;
            return available.Exists(b => b.Id == selected) ? selected : CarBodies.DefaultId;
        }

        public static string Second(RaceEventDefinition ev, string first)
        {
            var available = Available();
            if (first != ev.RelaySecond && available.Exists(b => b.Id == ev.RelaySecond)) return ev.RelaySecond;
            return available.Find(b => b.Id != first).Id;
        }

        internal void Build(RaceManager owner, TrackPath road, HudScreen display, Material material, RaceEventDefinition ev)
        {
            race = owner; owner.Relay = this; path = road; hud = display; second = Second(ev, First(ev));
            GateDistance = path.Wrap(path.startLineDistance + path.Length * .5f);
            path.Pose(GateDistance, 0f, out var p, out var r);
            GatePosition = p;
            var writer = new MeshChunkWriter("RelayExchangeGate");
            var frame = Matrix4x4.TRS(p, r, Vector3.one);
            float side = path.halfWidth + .8f;
            var stripe = new Color32(85, 242, 193, 120);
            var metal = new Color32(29, 50, 61, 0);
            foreach (float x in new[] { -side, side })
            {
                LowPoly.Box(writer, frame, new Vector3(x, 2.7f, 0), new Vector3(.38f, 5.4f, .48f), metal);
                LowPoly.Box(writer, frame, new Vector3(x, 3.5f, 0), new Vector3(.43f, 1.7f, .52f), stripe);
            }
            LowPoly.Box(writer, frame, Vector3.up * 5.4f, new Vector3(side * 2f, .32f, .42f), stripe);
            meshes = writer.Finish();
            foreach (var mesh in meshes)
            {
                var model = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                model.transform.SetParent(transform, false);
                model.GetComponent<MeshFilter>().sharedMesh = mesh; model.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        public void SimulationStep(int tick)
        {
            if (Exchanged || race == null || race.State != RaceState.Racing || PauseController.IsPaused) return;
            var player = race.Player;
            var progress = player.Progress;
            if (progress.SpeedAlongTrack <= 0f || progress.GatesPassedThisLap < progress.gateCount / 2 ||
                Mathf.Abs(path.SignedDelta(progress.Distance, GateDistance)) > 4f ||
                Vector3.Distance(player.transform.position, GatePosition) > path.halfWidth + 3f) return;
            Exchanged = true;
            var next = CarBodies.Get(second);
            CarBody.Apply(player.gameObject, next);
            var audio = player.GetComponent<EngineAudio>();
            if (audio != null) audio.SetRunning(true);
            if (hud != null) hud.ShowEvent(Loc.Get("format.relay.second") + " · " + Loc.Get(next.NameKey), UiFactory.Accent);
        }

        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable() { if (SimulationDriver.Current != null) SimulationDriver.Current.Unregister(this); }
        private void OnDestroy()
        {
            if (race != null && race.Relay == this) race.Relay = null;
            if (meshes != null) foreach (var m in meshes) Destroy(m);
        }
    }
}
