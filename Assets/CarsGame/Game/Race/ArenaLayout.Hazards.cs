using System.Collections.Generic;
using RacingGame.World.Gen;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class ArenaLayout
    {
        private const float PulseRadius = 104f, StripZ = -84f, StripHalfWidth = 8f;
        private static readonly Vector3 Garage = new Vector3(94f, 0f, 77f);
        private readonly BoxCollider[] shelters = new BoxCollider[6];
        private readonly HashSet<int> pulseHits = new HashSet<int>(), raidHits = new HashSet<int>();
        private Transform pulseSigns, pulseFront, raidSigns, roof, garageWarning;
        private int startTick = -1, roofTriggered = -1;
        private bool strafing, collapsed;
        private Vector3 formationOffset;

        private void BuildHazards()
        {
            Physics.SyncTransforms();
            pulseSigns = MissionArt.Pivot(transform, "PulseSectors", Vector3.up * .12f);
            MissionArt.Attach(pulseSigns, MissionArt.Cached("PulseSectorArrows", (w, g) =>
            {
                for (int i = 0; i < 72; i++)
                {
                    var yaw = Quaternion.Euler(0f, i * 5f, 0f);
                    foreach (float r in new[] { 48f, 98f })
                    {
                        Vector3 p = yaw * Vector3.forward * r;
                        var m = Matrix4x4.TRS(p, yaw, Vector3.one);
                        if (InRockShadow(transform.TransformPoint(p + Vector3.up)))
                            LowPoly.Box(g, m, Vector3.zero, new Vector3(4f, .05f, 1.2f), MissionArt.CyanGlow);
                        else
                            g.AddTriangle(m.MultiplyPoint3x4(new Vector3(-1.3f,0,-2)), m.MultiplyPoint3x4(Vector3.forward * 2),
                                m.MultiplyPoint3x4(new Vector3(1.3f,0,-2)), MissionArt.HazardGlow);
                    }
                }
            }), gameObject.layer, false);
            pulseFront = MissionArt.Pivot(transform, "PulseFront", Vector3.up * .25f);
            MissionArt.Attach(pulseFront, MissionArt.Cached("CanyonPulseRing", (w, g) =>
            {
                for (int i = 0; i < 72; i++)
                {
                    Vector3 a = Quaternion.Euler(0f, i * 5f, 0f) * Vector3.forward;
                    Vector3 b = Quaternion.Euler(0f, (i + 1) * 5f, 0f) * Vector3.forward;
                    g.AddQuad(a * .98f, a, b, b * .98f, MissionArt.HazardGlow);
                }
            }), gameObject.layer, false);
            raidSigns = MissionArt.Pivot(transform, "AirRaidStrip", new Vector3(0f,.13f,StripZ));
            MissionArt.Attach(raidSigns, MissionArt.Cached("AirRaidLines", (w,g) =>
            {
                for (int side = -1; side <= 1; side += 2)
                    LowPoly.Box(g, Matrix4x4.identity, Vector3.forward * side * StripHalfWidth, new Vector3(160f,.06f,.55f), MissionArt.EnemyGlow);
                for (int x = -75; x <= 75; x += 10)
                    LowPoly.Box(g, Matrix4x4.TRS(Vector3.right * x, Quaternion.Euler(0,32,0), Vector3.one),
                        Vector3.zero, new Vector3(.45f,.06f,13f), MissionArt.HazardGlow);
            }), gameObject.layer, false);
            foreach (float x in new[] { -6.8f, 6.8f })
                AddBox("GarageWall", Garage + new Vector3(x,2.8f,0), new Vector3(.8f,5.6f,18f), MissionArt.Rust);
            roof = AddBox("GarageRoof", Garage + Vector3.up * 6f, new Vector3(15f,.7f,19f), MissionArt.Iron);
            garageWarning = MissionArt.Pivot(transform, "GarageExitBands", Garage + Vector3.up * .15f);
            MissionArt.Attach(garageWarning, MissionArt.Cached("GarageWarning", (w,g) =>
            {
                for (int z = -8; z <= 8; z += 4)
                    LowPoly.Box(g, Matrix4x4.identity, Vector3.forward * z, new Vector3(12f,.06f,.65f), MissionArt.HazardGlow);
            }), gameObject.layer, false);
            pulseSigns.gameObject.SetActive(false);
            pulseFront.gameObject.SetActive(false);
            raidSigns.gameObject.SetActive(false);
            garageWarning.gameObject.SetActive(false);
        }

        private bool InRockShadow(Vector3 world)
        {
            var origin = transform.TransformPoint(Vector3.up * 2f);
            var delta = world - origin;
            var ray = new Ray(origin, delta.normalized);
            foreach (var rock in shelters)
                if (rock != null && rock.Raycast(ray, out _, delta.magnitude)) return true;
            return false;
        }

        private static bool UnderRoof(Vector3 p) => Mathf.Abs(p.x - Garage.x) < 6.4f &&
            Mathf.Abs(p.z - Garage.z) <= 9f && p.y < 6f;
        private static bool InStrip(Vector3 p) => Mathf.Abs(p.x) <= 80f && Mathf.Abs(p.z - StripZ) <= StripHalfWidth;

        private bool HazardThreatens(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            if (strafing && InStrip(p) || roofTriggered >= 0 && !collapsed && UnderRoof(p)) return true;
            return pulseSigns != null && pulseSigns.gameObject.activeSelf &&
                p.x * p.x + p.z * p.z < PulseRadius * PulseRadius && !InRockShadow(world + Vector3.up);
        }

        internal Vector3 EscapeTarget(Vector3 local, Vector3 forward)
        {
            if (strafing && InStrip(local))
                return transform.TransformPoint(new Vector3(local.x,0f,StripZ + (local.z >= StripZ ? 15f : -15f)));
            if (roofTriggered >= 0 && !collapsed && UnderRoof(local))
                return transform.TransformPoint(Garage + Vector3.forward * (local.z >= Garage.z ? 16f : -16f));
            local.y = 0f;
            if (local.sqrMagnitude < 1f) local = transform.InverseTransformDirection(forward);
            return transform.TransformPoint(local.normalized * (pulseSigns.gameObject.activeSelf ? PulseRadius + 5f : 55f));
        }

        private void StepHazards(int tick)
        {
            if (startTick < 0) startTick = tick;
            int elapsed = tick - startTick;
            int pulse = elapsed % SimulationDriver.SecondsToTicks(60f) - SimulationDriver.SecondsToTicks(26f);
            int raid = elapsed - SimulationDriver.SecondsToTicks(60f);
            int warning = SimulationDriver.SecondsToTicks(4f);
            bool pulsing = pulse >= 0 && pulse <= warning * 2;
            bool flying = raid >= 0 && raid <= warning * 3;
            if (pulse == 0) { pulseHits.Clear(); Signal("arena.rock_cover"); }
            if (raid == 0) Signal("arena.clear_strip");
            if (pulseSigns.gameObject.activeSelf != pulsing) pulseSigns.gameObject.SetActive(pulsing);
            bool wave = pulsing && pulse >= warning;
            if (pulseFront.gameObject.activeSelf != wave) pulseFront.gameObject.SetActive(wave);
            float radius = wave ? PulseRadius * (pulse - warning) / warning : 0f;
            if (wave) pulseFront.localScale = new Vector3(radius,1f,radius);
            float frontX = (raid - warning) * 20f * SimulationDriver.FixedStep - 80f;
            if (flying != strafing)
            {
                strafing = flying;
                raidSigns.gameObject.SetActive(flying);
                if (!flying && bomberFormation != null) bomberFormation.localPosition = formationOffset;
            }
            if (flying && bomberFormation != null)
            {
                if (raid == 0) formationOffset = bomberFormation.localPosition;
                var lead = bomberFormation.GetChild(0);
                bomberFormation.localRotation = Quaternion.AngleAxis(90f, Vector3.up) *
                    Quaternion.Inverse(Quaternion.LookRotation(Vector3.ProjectOnPlane(lead.localRotation * Vector3.forward, Vector3.up)));
                bomberFormation.localPosition = new Vector3(frontX,33f,StripZ) - bomberFormation.localRotation * lead.localPosition;
            }
            bool fall = !collapsed && roofTriggered >= 0 && tick - roofTriggered >= SimulationDriver.SecondsToTicks(3.5f);
            foreach (var p in spawner.Participants)
            {
                if (p?.Health == null || p.Health.IsDead) continue;
                Vector3 local = transform.InverseTransformPoint(p.transform.position);
                if (wave && Mathf.Abs(new Vector2(local.x,local.z).magnitude - radius) < 1.7f &&
                    !InRockShadow(p.transform.position + Vector3.up) && pulseHits.Add(p.Car.CarId)) HazardHit(p,30f);
                if (flying && raid >= warning && InStrip(local) && Mathf.Abs(local.x - frontX) <= 2f && raidHits.Add(p.Car.CarId))
                    HazardHit(p,36f);
                if (collapsed || !UnderRoof(local)) continue;
                if (roofTriggered < 0)
                {
                    roofTriggered = tick;
                    garageWarning.gameObject.SetActive(true);
                    if (race.Player != null && (race.Player.transform.position - roof.position).sqrMagnitude < 35f * 35f)
                        Signal("arena.leave_garage");
                }
                if (fall) HazardHit(p,48f);
            }
            if (fall)
            {
                collapsed = true;
                roof.GetComponent<Collider>().enabled = false;
                roof.localPosition = Garage + Vector3.up * 1.2f;
                roof.localRotation = Quaternion.Euler(0f,0f,-10f);
                garageWarning.gameObject.SetActive(false);
            }
        }

        private static void HazardHit(RaceParticipant p, float damage) =>
            p.Health.ApplyHit(new Core.Combat.HitInfo(null, damage, Core.Combat.HitType.Explosion)
                { Point = p.transform.position, Normal = Vector3.up });

        private static void Signal(string key)
        {
            var hud = FindAnyObjectByType<HudScreen>();
            if (hud != null) hud.ShowEvent(Loc.Get(key), UiFactory.Accent);
            SfxPlayer.Instance.PlayUi(SfxSynth.Beep, .55f, .8f);
        }
    }
}
