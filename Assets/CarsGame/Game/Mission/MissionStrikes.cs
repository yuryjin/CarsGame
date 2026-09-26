using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Telegraphed blasts from above (the boss's mortar, bomber runs): a red ring appears on the sand, a shell drops
    /// into it for the last second, then the blast hurts the other side's cars inside the radius. Every death from
    /// the sky is announced first (Docs/AI/ArenaMode.md §8). Timing is in ticks; markers are pooled.
    /// </summary>
    public sealed class MissionStrikes
    {
        private struct Strike
        {
            public Vector3 Point;
            public float Radius, Damage;
            public int StartTick, ImpactTick, RevealTick;
            public GameObject Source;
            public Transform Marker, Shell;
        }

        private readonly DesertMap map;
        private readonly Transform root;
        private readonly List<Strike> live = new List<Strike>();
        private readonly Stack<Transform> markers = new Stack<Transform>();
        private readonly Stack<Transform> shells = new Stack<Transform>();
        private const float ShellFallSeconds = 0.9f, ShellHeight = 70f;

        public int Count => live.Count;

        public MissionStrikes(DesertMap map, Transform parent)
        {
            this.map = map;
            root = new GameObject("Strikes").transform;
            root.SetParent(parent, false);
        }

        /// <summary>A blast <paramref name="delaySeconds"/> from now; the ring shows for the last <paramref name="revealSeconds"/> (0 = at once).</summary>
        public void Add(Vector3 point, float radius, float damage, float delaySeconds, GameObject source, int tick, float revealSeconds = 0f)
        {
            point.y = map.Height(point);
            var marker = markers.Count > 0 ? markers.Pop() : NewMarker();
            marker.position = point + Vector3.up * 0.25f;
            marker.localScale = new Vector3(radius, 1f, radius);
            var shell = shells.Count > 0 ? shells.Pop() : NewShell();
            shell.gameObject.SetActive(false);
            live.Add(new Strike
            {
                Point = point, Radius = radius, Damage = damage, Source = source, Marker = marker, Shell = shell,
                StartTick = tick, ImpactTick = tick + SimulationDriver.SecondsToTicks(delaySeconds),
                RevealTick = revealSeconds > 0f ? tick + SimulationDriver.SecondsToTicks(Mathf.Max(0f, delaySeconds - revealSeconds)) : tick
            });
        }

        public void Step(int tick)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var s = live[i];
                if (tick < s.ImpactTick) continue;
                MissionUnit.Blast(s.Source, s.Point, s.Radius, s.Damage, 8f);
                MissionUnit.Explode(s.Point + Vector3.up * 0.5f, s.Radius > 6f ? 2f : 1.2f);
                Release(s);
                live.RemoveAt(i);
            }
        }

        /// <summary>Per frame: the ring pulses faster as the impact nears, the shell falls in the last second.</summary>
        public void UpdateVisuals(int tick, float frameFraction)
        {
            float now = tick + frameFraction;
            foreach (var s in live)
            {
                bool shown = now >= s.RevealTick;
                if (s.Marker.gameObject.activeSelf != shown) s.Marker.gameObject.SetActive(shown);
                if (!shown) continue;
                float total = Mathf.Max(1f, s.ImpactTick - s.RevealTick);
                float t = Mathf.Clamp01((now - s.RevealTick) / total);
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 30f, t));
                s.Marker.localScale = new Vector3(s.Radius * pulse, 1f, s.Radius * pulse);
                float remaining = (s.ImpactTick - now) * SimulationDriver.FixedStep;
                bool falling = remaining < ShellFallSeconds;
                if (s.Shell.gameObject.activeSelf != falling) s.Shell.gameObject.SetActive(falling);
                if (falling) s.Shell.position = s.Point + Vector3.up * (ShellHeight * Mathf.Max(0f, remaining) / ShellFallSeconds + 0.5f);
            }
        }

        /// <summary>The bomber was shot down: bombs it has not dropped yet (more than half a second out) never fall.</summary>
        public void CancelFrom(GameObject source, int tick)
        {
            int margin = SimulationDriver.SecondsToTicks(0.5f);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                if (live[i].Source != source || live[i].ImpactTick - tick < margin) continue;
                Release(live[i]);
                live.RemoveAt(i);
            }
        }

        /// <summary>True when an announced blast will land within its radius plus <paramref name="margin"/> of the point.</summary>
        public bool Threatens(Vector3 point, float margin)
        {
            foreach (var s in live)
            {
                Vector3 d = s.Point - point; d.y = 0f;
                if (d.sqrMagnitude < (s.Radius + margin) * (s.Radius + margin)) return true;
            }
            return false;
        }

        public void Clear()
        {
            foreach (var s in live) Release(s);
            live.Clear();
        }

        private void Release(Strike s)
        {
            s.Marker.gameObject.SetActive(false);
            s.Shell.gameObject.SetActive(false);
            markers.Push(s.Marker);
            shells.Push(s.Shell);
        }

        private Transform NewMarker()
        {
            var t = new GameObject("StrikeMarker").transform;
            t.SetParent(root, false);
            MissionArt.Attach(t, MissionArt.Cached("StrikeRing", MissionArt.Ring), MissionLayers.SoftUnit, false);
            return t;
        }

        private Transform NewShell()
        {
            var t = new GameObject("StrikeShell").transform;
            t.SetParent(root, false);
            MissionArt.Attach(t, MissionArt.Cached("StrikeShell", (w, g) =>
            {
                World.Gen.LowPoly.Cylinder(w, Matrix4x4.identity, new Vector3(0f, -0.4f, 0f), 0.35f, 0.35f, 1.6f, 6, MissionArt.Iron);
                World.Gen.LowPoly.Cylinder(g, Matrix4x4.identity, new Vector3(0f, -1.1f, 0f), 0f, 0.35f, 0.7f, 6, MissionArt.HazardGlow);   // nose down
            }), MissionLayers.SoftUnit, false);
            return t;
        }
    }
}
