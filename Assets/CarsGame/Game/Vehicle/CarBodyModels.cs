using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Economy;
using Slot = RacingGame.BodyMesh.Slot;

namespace RacingGame
{
    /// <summary>Meshes and mount points of one body, shared by every car that wears it.</summary>
    public sealed class BodyModel
    {
        /// <summary>Sport bodies keep the prefab's sleek car (re-proportioned by <see cref="VisualScale"/>) and add parts under it.</summary>
        public bool KeepStockVisuals;
        public Vector3 VisualScale = Vector3.one;
        public Mesh Paint, Trim;
        public Slot[] TrimSlots;
        /// <summary>Tank turret shell, parented to the cannon's yaw pivot so it turns with it.</summary>
        public Mesh TurretPaint, TurretTrim;
        public Slot[] TurretTrimSlots;
        /// <summary>Replacement barrel (twin barrels), parented to the barrel pivot so it tilts and recoils.</summary>
        public Mesh Barrel;
        public Slot[] BarrelSlots;
        public Vector3 TurretPosition;
        public Vector3 BarrelScale = Vector3.one;
        public bool HideWheels, HideStockTurret, HideStockBarrel;
        /// <summary>Visual-only axles (z) between the physical ones: the six-wheeler.</summary>
        public float[] ExtraAxles = new float[0];
    }

    /// <summary>
    /// Procedural models for the twenty bodies (<see cref="CarBodies"/>). Built once per body and cached; the car-space
    /// convention is the prefab's: x right, y up, z forward, ground at y = 0, cannon turret pivot 0.05 m above the roof.
    /// </summary>
    public static class CarBodyModels
    {
        private static readonly Dictionary<string, BodyModel> cache = new Dictionary<string, BodyModel>();

        public static BodyModel Get(in BodyDef def)
        {
            if (cache.TryGetValue(def.Id, out var model) && model.Paint != null) return model;
            model = Build(def);
            cache[def.Id] = model;
            return model;
        }

        private static BodyModel Build(in BodyDef def)
        {
            var m = new BodyMesh();
            var model = new BodyModel();
            switch (def.Class)
            {
                case BodyClass.Buggy: Buggy(def, m, model); break;
                case BodyClass.Armored: Armored(def, m, model); break;
                case BodyClass.Tank: Tank(def, m, model); break;
                default: Sport(def, m, model); break;
            }
            model.Paint = m.Build("Body_" + def.Id + "_Paint", BodyMesh.PaintSlots, out _);
            model.Trim = m.Build("Body_" + def.Id + "_Trim", BodyMesh.TrimSlots, out model.TrimSlots);
            return model;
        }

        private static bool Has(in BodyDef d, BodyKit k) => (d.Kit & k) != 0;

        // ------------------------------------------------------------------ buggy
        // An open tub on long suspension arms, a tube roll cage with the cannon on its roof plate, big bare wheels.
        private static void Buggy(in BodyDef d, BodyMesh m, BodyModel model)
        {
            var s = d.Shape;
            float zF = s.Length * 0.5f, zR = -s.Length * 0.5f, c = s.Clearance, r = s.WheelRadius, hb = s.HalfBase, ht = s.HalfTrack;
            float y0 = c + 0.02f, y1 = c + 0.40f;
            float tub = 0.58f;

            // Tub, nose and rising engine cover.
            m.Section(Slot.Paint, zR + 0.5f, zF - 0.8f, y0, y0, y1, y1, 0.50f, 0.50f, tub, tub, zR + 0.45f, zF - 0.6f);
            m.Section(Slot.Paint, zF - 0.8f, zF - 0.05f, y0 + 0.08f, c + 0.16f, y1, c + 0.30f, 0.45f, 0.30f, tub, 0.34f, zF - 0.6f, zF);
            m.Section(Slot.Paint, zR + 0.05f, zR + 0.55f, c + 0.14f, y0 + 0.06f, y1 + 0.05f, y1 + 0.12f, 0.40f, 0.45f, 0.46f, 0.56f, zR, zR + 0.55f);

            // Fenders over the wheels, on struts.
            foreach (float z in new[] { hb, -hb })
            {
                m.BoxPair(Slot.Paint, new Vector3(ht, 2f * r + 0.07f, z), new Vector3(0.36f, 0.05f, 2f * r * 1.05f));
                m.BoxPair(Slot.Paint, new Vector3(ht, 2f * r - 0.05f, z + Mathf.Sign(z) * r * 1.05f), new Vector3(0.36f, 0.25f, 0.05f), Quaternion.Euler(Mathf.Sign(z) * 25f, 0f, 0f));
                m.TubePair(Slot.Dark, new Vector3(tub - 0.02f, y1 - 0.05f, z), new Vector3(ht - 0.16f, 2f * r + 0.05f, z), 0.03f);
                // Suspension arms to the hub.
                m.TubePair(Slot.Dark, new Vector3(0.48f, y0 + 0.12f, z + 0.18f), new Vector3(ht - 0.12f, r, z), 0.035f);
                m.TubePair(Slot.Dark, new Vector3(0.48f, y0 + 0.12f, z - 0.18f), new Vector3(ht - 0.12f, r, z), 0.035f);
            }

            // Seats.
            m.BoxPair(Slot.Dark, new Vector3(0.26f, y1 + 0.06f, -0.05f), new Vector3(0.38f, 0.12f, 0.42f));
            m.BoxPair(Slot.Dark, new Vector3(0.26f, y1 + 0.36f, -0.30f), new Vector3(0.38f, 0.55f, 0.10f), Quaternion.Euler(-12f, 0f, 0f));

            // Roll cage.
            float yt = y1 + (d.Id == "flea" ? 0.85f : 1.0f), zRr = -0.45f, zRf = 0.45f, cw = 0.5f;
            m.TubePair(Slot.Dark, new Vector3(tub - 0.02f, y1, zRr), new Vector3(cw, yt, zRr), 0.035f);
            m.TubePair(Slot.Dark, new Vector3(tub - 0.02f, y1, zRf + 0.75f), new Vector3(cw, yt, zRf), 0.035f);
            m.TubePair(Slot.Dark, new Vector3(cw, yt, zRr), new Vector3(cw, yt, zRf), 0.035f);
            m.Tube(Slot.Dark, new Vector3(-cw, yt, zRr), new Vector3(cw, yt, zRr), 0.035f);
            m.Tube(Slot.Dark, new Vector3(-cw, yt, zRf), new Vector3(cw, yt, zRf), 0.035f);
            m.TubePair(Slot.Dark, new Vector3(cw, yt, zRr), new Vector3(0.40f, y1 + 0.15f, zR + 0.35f), 0.03f);
            m.TubePair(Slot.Dark, new Vector3(tub, y1 + 0.25f, zRf + 0.55f), new Vector3(tub, y1 + 0.25f, zRr), 0.03f);
            m.Box(Slot.Dark, new Vector3(0f, yt + 0.02f, 0f), new Vector3(0.98f, 0.03f, 0.88f));   // roof plate under the cannon
            model.TurretPosition = new Vector3(0f, yt + 0.09f, 0f);

            // Engine or trophy-truck bed.
            if (Has(d, BodyKit.Bed))
            {
                m.Box(Slot.Dark, new Vector3(0f, y1 + 0.16f, zR + 0.55f), new Vector3(1.25f, 0.28f, 0.85f));
                m.Box(Slot.Metal, new Vector3(0f, y1 + 0.34f, zR + 0.75f), new Vector3(0.55f, 0.16f, 0.35f));
                // Rocker bodywork between the wheels.
                m.BoxPair(Slot.Paint, new Vector3(ht - 0.06f, c + 0.34f, 0f), new Vector3(0.30f, 0.30f, 2f * hb - 2f * r - 0.25f));
            }
            else
            {
                m.Box(Slot.Metal, new Vector3(0f, y1 + 0.2f, zR + 0.48f), new Vector3(0.62f, 0.28f, 0.52f));
                m.CylinderPair(Slot.Dark, new Vector3(0.16f, y1 + 0.36f, zR + 0.48f), new Vector3(0.16f, y1 + 0.46f, zR + 0.48f), 0.09f);
            }

            // Lights.
            m.BoxPair(Slot.Head, new Vector3(0.22f, c + 0.24f, zF + 0.01f), new Vector3(0.14f, 0.08f, 0.04f));
            m.BoxPair(Slot.Tail, new Vector3(0.36f, y1 - 0.02f, zR + 0.01f), new Vector3(0.12f, 0.08f, 0.04f));

            if (Has(d, BodyKit.LightBar))
            {
                m.Box(Slot.Dark, new Vector3(0f, yt + 0.07f, zRf + 0.03f), new Vector3(1.0f, 0.07f, 0.08f));
                for (int i = 0; i < 4; i++) m.Box(Slot.Head, new Vector3(-0.36f + 0.24f * i, yt + 0.07f, zRf + 0.08f), new Vector3(0.16f, 0.07f, 0.03f));
            }
            if (Has(d, BodyKit.SpareWheel))
            {
                var at = new Vector3(0f, y1 + 0.45f, zR + (Has(d, BodyKit.Bed) ? 0.35f : 0.02f));
                m.Cylinder(Slot.Tire, at - Vector3.forward * 0.1f, at + Vector3.forward * 0.1f, r * 0.85f, 10);
                m.Cylinder(Slot.Metal, at - Vector3.forward * 0.11f, at + Vector3.forward * 0.11f, r * 0.45f, 8);
            }
            if (Has(d, BodyKit.Snorkel))
            {
                m.Tube(Slot.Dark, new Vector3(tub + 0.02f, y1, zRf + 0.35f), new Vector3(tub + 0.04f, yt - 0.05f, zRf + 0.3f), 0.05f);
                m.Box(Slot.Dark, new Vector3(tub + 0.04f, yt - 0.02f, zRf + 0.34f), new Vector3(0.12f, 0.1f, 0.16f));
            }
            if (Has(d, BodyKit.Stacks))
                m.TubePair(Slot.Metal, new Vector3(0.18f, y1 + 0.3f, zR + 0.35f), new Vector3(0.26f, yt - 0.2f, zR + 0.05f), 0.045f);
            if (Has(d, BodyKit.Wing))
            {
                m.TubePair(Slot.Dark, new Vector3(0.40f, y1 + 0.3f, zR + 0.32f), new Vector3(0.40f, yt - 0.08f, zR + 0.12f), 0.03f);
                m.Box(Slot.Dark, new Vector3(0f, yt - 0.05f, zR + 0.1f), new Vector3(s.Width * 0.9f, 0.04f, 0.38f), Quaternion.Euler(-8f, 0f, 0f));
                m.BoxPair(Slot.Dark, new Vector3(s.Width * 0.45f, yt - 0.05f, zR + 0.1f), new Vector3(0.03f, 0.22f, 0.44f));
            }
            if (Has(d, BodyKit.Fins))
                m.Hull(Slot.Paint,
                    new Vector3(-0.02f, y1, zR + 0.9f), new Vector3(0.02f, y1, zR + 0.9f), new Vector3(0.02f, y1, zR + 0.2f), new Vector3(-0.02f, y1, zR + 0.2f),
                    new Vector3(-0.02f, y1 + 0.05f, zR + 0.95f), new Vector3(0.02f, y1 + 0.05f, zR + 0.95f), new Vector3(0.02f, y1 + 0.5f, zR + 0.15f), new Vector3(-0.02f, y1 + 0.5f, zR + 0.15f));
        }

        // ------------------------------------------------------------------ sport
        // The prefab's sleek car scaled to the body's proportions, with parts in the prefab's own space
        // (body top 0.65 m, canopy roof 1.05 m, rear deck at z −1.75, stock wing at y 0.91).
        private static void Sport(in BodyDef d, BodyMesh m, BodyModel model)
        {
            var s = d.Shape; var std = BodyShape.Standard;
            model.KeepStockVisuals = true;
            model.VisualScale = new Vector3(s.Width / std.Width, s.Height / std.Height, s.Length / std.Length);
            model.TurretPosition = new Vector3(0f, 1.12f * model.VisualScale.y, -0.45f * model.VisualScale.z);

            if (Has(d, BodyKit.BigWing))
            {
                // Swan-neck time-attack wing above the stock one.
                m.TubePair(Slot.Dark, new Vector3(0.42f, 0.62f, -1.62f), new Vector3(0.42f, 1.22f, -1.92f), 0.03f);
                m.Box(Slot.Dark, new Vector3(0f, 1.25f, -1.98f), new Vector3(1.9f, 0.05f, 0.46f), Quaternion.Euler(-9f, 0f, 0f));
                m.BoxPair(Slot.Paint, new Vector3(0.96f, 1.22f, -1.98f), new Vector3(0.03f, 0.28f, 0.54f));
                // Canards on the nose.
                m.BoxPair(Slot.Dark, new Vector3(0.78f, 0.33f, 1.85f), new Vector3(0.22f, 0.02f, 0.2f), Quaternion.Euler(0f, 0f, 14f));
            }
            if (Has(d, BodyKit.Scoop))
            {
                // Roof scoop ahead of the turret and hood vents.
                m.Section(Slot.Paint, -0.05f, 0.35f, 1.04f, 1.0f, 1.18f, 1.05f, 0.18f, 0.18f, 0.16f, 0.16f);
                m.Box(Slot.Dark, new Vector3(0f, 1.1f, 0.36f), new Vector3(0.28f, 0.08f, 0.03f));
                m.BoxPair(Slot.Dark, new Vector3(0.28f, 0.62f, 1.15f), new Vector3(0.22f, 0.03f, 0.34f));
                m.BoxPair(Slot.Dark, new Vector3(0.97f, 0.42f, -0.9f), new Vector3(0.04f, 0.14f, 0.5f));
            }
            if (Has(d, BodyKit.Wing) && !Has(d, BodyKit.BigWing))
            {
                // Ducktail lip on the rear deck.
                m.Section(Slot.Paint, -2.02f, -1.7f, 0.56f, 0.62f, 0.72f, 0.66f, 0.72f, 0.82f, 0.7f, 0.8f);
            }
            if (Has(d, BodyKit.Fins))
            {
                // Twin tail fins and a roof shark fin.
                foreach (float x in new[] { -0.55f, 0.55f })
                    m.Hull(Slot.Paint,
                        new Vector3(x - 0.02f, 0.6f, -2.0f), new Vector3(x + 0.02f, 0.6f, -2.0f), new Vector3(x + 0.02f, 0.63f, -1.15f), new Vector3(x - 0.02f, 0.63f, -1.15f),
                        new Vector3(x - 0.02f, 1.0f, -2.08f), new Vector3(x + 0.02f, 1.0f, -2.08f), new Vector3(x + 0.02f, 0.66f, -1.2f), new Vector3(x - 0.02f, 0.66f, -1.2f));
                m.Hull(Slot.Paint,
                    new Vector3(-0.02f, 0.98f, -1.25f), new Vector3(0.02f, 0.98f, -1.25f), new Vector3(0.02f, 1.02f, -0.75f), new Vector3(-0.02f, 1.02f, -0.75f),
                    new Vector3(-0.02f, 1.12f, -1.3f), new Vector3(0.02f, 1.12f, -1.3f), new Vector3(0.02f, 1.04f, -0.8f), new Vector3(-0.02f, 1.04f, -0.8f));
            }
            if (Has(d, BodyKit.Stacks))
            {
                // Side pipes along the sills.
                m.TubePair(Slot.Metal, new Vector3(0.99f, 0.26f, 0.7f), new Vector3(0.99f, 0.26f, -0.95f), 0.05f);
                m.TubePair(Slot.Dark, new Vector3(0.99f, 0.26f, -0.95f), new Vector3(0.99f, 0.26f, -1.02f), 0.035f);
            }
        }

        // ------------------------------------------------------------------ armored
        // Narrow lower hull between the wheels, a full-width deck over them, a sloped cabin with slit windows.
        private static void Armored(in BodyDef d, BodyMesh m, BodyModel model)
        {
            var s = d.Shape;
            float zF = s.Length * 0.5f, zR = -s.Length * 0.5f, c = s.Clearance, r = s.WheelRadius, hb = s.HalfBase, ht = s.HalfTrack;
            float hw = s.Width * 0.5f, lower = ht - 0.2f;
            float yW = 2f * r + 0.03f, yU = yW + 0.22f, yC = c + s.Height;
            float cabBack = zR + 0.05f, cabFront = zF - 0.75f, roofBack = zR + 0.15f, roofFront = zF - 1.35f;
            float cabHalf = hw - 0.06f, roofHalf = hw - 0.2f;
            var inside = new Vector3(0f, (yU + yC) * 0.5f, (cabBack + cabFront) * 0.5f);

            m.Section(Slot.Paint, zR + 0.3f, zF - 0.55f, c, c, yW, yW, lower - 0.05f, lower - 0.05f, lower, lower, zR + 0.1f, zF - 0.1f);
            m.Section(Slot.Paint, zR, zF, yW, yW, yU, yU, hw, hw, hw - 0.04f, hw - 0.04f, zR + 0.02f, zF - 0.35f);
            m.Section(Slot.Paint, cabBack, cabFront, yU, yU, yC, yC, cabHalf, cabHalf, roofHalf, roofHalf, roofBack, roofFront);

            // Cabin faces: front (windscreen), right side, rear.
            Vector3 fBL = new Vector3(-cabHalf, yU, cabFront), fBR = new Vector3(cabHalf, yU, cabFront), fTR = new Vector3(roofHalf, yC, roofFront), fTL = new Vector3(-roofHalf, yC, roofFront);
            m.Panel(Slot.Glass, fBL, fBR, fTR, fTL, 0.10f, 0.46f, 0.42f, 0.72f, 0.015f, inside);
            m.Panel(Slot.Glass, fBL, fBR, fTR, fTL, 0.54f, 0.90f, 0.42f, 0.72f, 0.015f, inside);
            Vector3 sBR = new Vector3(-cabHalf, yU, cabBack), sBF = new Vector3(-cabHalf, yU, cabFront), sTF = new Vector3(-roofHalf, yC, roofFront), sTR = new Vector3(-roofHalf, yC, roofBack);
            m.PanelPair(Slot.Glass, sBR, sBF, sTF, sTR, 0.62f, 0.88f, 0.52f, 0.72f, 0.015f, inside);
            m.PanelPair(Slot.Glass, sBR, sBF, sTF, sTR, 0.30f, 0.52f, 0.52f, 0.72f, 0.015f, inside);
            Vector3 rBL = new Vector3(cabHalf, yU, cabBack), rBR = new Vector3(-cabHalf, yU, cabBack), rTR = new Vector3(-roofHalf, yC, roofBack), rTL = new Vector3(roofHalf, yC, roofBack);
            m.Panel(Slot.Dark, rBL, rBR, rTR, rTL, 0.3f, 0.7f, 0.08f, 0.9f, 0.02f, inside);

            // Deck front: grille and headlights; deck rear: tail lights.
            var deckInside = new Vector3(0f, (yW + yU) * 0.5f, 0f);
            Vector3 nBL = new Vector3(-hw, yW, zF), nBR = new Vector3(hw, yW, zF), nTR = new Vector3(hw - 0.04f, yU, zF - 0.35f), nTL = new Vector3(-hw + 0.04f, yU, zF - 0.35f);
            for (int i = 0; i < 4; i++) m.Panel(Slot.Dark, nBL, nBR, nTR, nTL, 0.3f, 0.7f, 0.12f + 0.2f * i, 0.24f + 0.2f * i, 0.02f, deckInside);
            m.Panel(Slot.Head, nBL, nBR, nTR, nTL, 0.07f, 0.21f, 0.3f, 0.75f, 0.02f, deckInside);
            m.Panel(Slot.Head, nBL, nBR, nTR, nTL, 0.79f, 0.93f, 0.3f, 0.75f, 0.02f, deckInside);
            Vector3 tBL = new Vector3(hw, yW, zR), tBR = new Vector3(-hw, yW, zR), tTR = new Vector3(-hw + 0.04f, yU, zR + 0.02f), tTL = new Vector3(hw - 0.04f, yU, zR + 0.02f);
            m.Panel(Slot.Tail, tBL, tBR, tTR, tTL, 0.05f, 0.17f, 0.25f, 0.75f, 0.02f, deckInside);
            m.Panel(Slot.Tail, tBL, tBR, tTR, tTL, 0.83f, 0.95f, 0.25f, 0.75f, 0.02f, deckInside);

            // Mud flaps behind the wheels.
            foreach (float z in Axles(hb, Has(d, BodyKit.SixWheels)))
                m.BoxPair(Slot.Dark, new Vector3(ht, yW - 0.17f, z - r - 0.1f), new Vector3(0.3f, 0.3f, 0.03f));

            float turretZ = (roofBack + roofFront) * 0.5f + (Has(d, BodyKit.RoofRack) ? 0.35f : 0f);
            model.TurretPosition = new Vector3(0f, yC + 0.05f, turretZ);

            if (Has(d, BodyKit.RamBar))
            {
                float zb = zF + 0.16f, yLo = c + 0.3f, yHi = yW - 0.02f;
                m.Tube(Slot.Dark, new Vector3(-hw + 0.12f, yLo, zb), new Vector3(hw - 0.12f, yLo, zb), 0.05f);
                m.Tube(Slot.Dark, new Vector3(-hw + 0.2f, yHi, zb), new Vector3(hw - 0.2f, yHi, zb), 0.045f);
                foreach (float x in new[] { -0.55f, 0f, 0.55f }) m.Tube(Slot.Dark, new Vector3(x, yLo, zb), new Vector3(x, yHi + 0.05f, zb), 0.04f);
                m.TubePair(Slot.Dark, new Vector3(0.55f, yLo + 0.1f, zb), new Vector3(0.55f, yLo + 0.1f, zF - 0.3f), 0.04f);
                if (Has(d, BodyKit.Spikes))
                {
                    foreach (float x in new[] { -0.75f, -0.28f, 0.28f, 0.75f }) m.Cone(Slot.Metal, new Vector3(x, yLo, zb + 0.03f), new Vector3(x, yLo, zb + 0.36f), 0.065f);
                    foreach (float x in new[] { -0.5f, 0.5f }) m.Cone(Slot.Metal, new Vector3(x, yHi, zb + 0.03f), new Vector3(x, yHi + 0.02f, zb + 0.3f), 0.055f);
                }
            }
            if (Has(d, BodyKit.SidePlates))
            {
                Vector3 dBR = new Vector3(-hw, yW, zR), dBF = new Vector3(-hw, yW, zF), dTF = new Vector3(-hw + 0.04f, yU, zF - 0.35f), dTR = new Vector3(-hw + 0.04f, yU, zR + 0.02f);
                for (int i = 0; i < 4; i++) m.PanelPair(Slot.Dark, dBR, dBF, dTF, dTR, 0.08f + 0.215f * i, 0.27f + 0.215f * i, 0.1f, 0.9f, 0.035f, deckInside);
                m.PanelPair(Slot.Dark, sBR, sBF, sTF, sTR, 0.04f, 0.9f, 0.06f, 0.4f, 0.03f, inside);
            }
            if (Has(d, BodyKit.RoofRack))
            {
                float y = yC + 0.14f, z0 = roofBack + 0.12f, z1 = roofBack + 1.25f, x = roofHalf - 0.1f;
                m.TubePair(Slot.Dark, new Vector3(x, y, z0), new Vector3(x, y, z1), 0.025f);
                m.Tube(Slot.Dark, new Vector3(-x, y, z0), new Vector3(x, y, z0), 0.025f);
                m.Tube(Slot.Dark, new Vector3(-x, y, z1), new Vector3(x, y, z1), 0.025f);
                foreach (float z in new[] { z0, z1 }) m.TubePair(Slot.Dark, new Vector3(x, yC, z), new Vector3(x, y, z), 0.025f);
                m.Box(Slot.Dark, new Vector3(-0.3f, yC + 0.12f, z0 + 0.35f), new Vector3(0.5f, 0.22f, 0.45f));
                m.Box(Slot.Metal, new Vector3(0.3f, yC + 0.14f, z0 + 0.28f), new Vector3(0.18f, 0.26f, 0.32f));
                m.Box(Slot.Metal, new Vector3(0.52f, yC + 0.14f, z0 + 0.28f), new Vector3(0.18f, 0.26f, 0.32f));
            }
            if (Has(d, BodyKit.SpareWheel))
            {
                var at = new Vector3(hw * 0.45f, yU + 0.25f, zR - 0.12f);
                m.Cylinder(Slot.Tire, at - Vector3.forward * 0.11f, at + Vector3.forward * 0.11f, r * 0.85f, 10);
                m.Cylinder(Slot.Metal, at - Vector3.forward * 0.12f, at + Vector3.forward * 0.12f, r * 0.45f, 8);
            }
            if (Has(d, BodyKit.Antenna))
                m.Cylinder(Slot.Dark, new Vector3(-roofHalf + 0.12f, yC, roofBack + 0.2f), new Vector3(-roofHalf + 0.1f, yC + 1.15f, roofBack - 0.05f), 0.012f, 4);
            if (Has(d, BodyKit.LightBar))
            {
                m.Box(Slot.Dark, new Vector3(0f, yC + 0.06f, roofFront + 0.06f), new Vector3(roofHalf * 1.7f, 0.08f, 0.09f));
                for (int i = 0; i < 4; i++) m.Box(Slot.Head, new Vector3(-roofHalf * 0.6f + roofHalf * 0.4f * i, yC + 0.06f, roofFront + 0.11f), new Vector3(0.16f, 0.07f, 0.03f));
            }
            if (Has(d, BodyKit.Snorkel))
            {
                m.Tube(Slot.Dark, new Vector3(hw - 0.02f, yW + 0.1f, zF - 0.95f), new Vector3(roofHalf + 0.06f, yC + 0.2f, roofFront - 0.05f), 0.06f);
                m.Box(Slot.Dark, new Vector3(roofHalf + 0.06f, yC + 0.24f, roofFront), new Vector3(0.14f, 0.12f, 0.2f));
            }
            if (Has(d, BodyKit.SixWheels)) model.ExtraAxles = new[] { 0f };
        }

        private static float[] Axles(float hb, bool six) => six ? new[] { hb, 0f, -hb } : new[] { hb, -hb };

        // ------------------------------------------------------------------ tank
        // Two tracks with road wheels, a hull with a sloped glacis, and a turret shell that rides on the cannon's yaw pivot.
        private static void Tank(in BodyDef d, BodyMesh m, BodyModel model)
        {
            var s = d.Shape;
            float zF = s.Length * 0.5f, zR = -s.Length * 0.5f, hb = s.HalfBase, ht = s.HalfTrack;
            float hw = s.Width * 0.5f, tw = 0.5f, xin = ht - tw * 0.5f, xout = ht + tw * 0.5f;
            float yT = 0.6f, yD = s.Clearance + s.Height;
            model.HideWheels = true;

            // Tracks: a side profile extruded across the track width, grousers on the top run.
            var profile = new[]
            {
                new Vector2(-hb - 0.05f, 0.02f), new Vector2(hb + 0.05f, 0.02f), new Vector2(hb + 0.45f, 0.3f),
                new Vector2(hb + 0.3f, yT), new Vector2(-hb - 0.3f, yT), new Vector2(-hb - 0.45f, 0.3f),
            };
            m.Extrude(Slot.Dark, profile, xin, xout);
            m.Extrude(Slot.Dark, profile, -xout, -xin);
            for (float z = -hb - 0.22f; z <= hb + 0.24f; z += 0.22f)
                m.BoxPair(Slot.Dark, new Vector3(ht, yT + 0.015f, z), new Vector3(tw + 0.02f, 0.03f, 0.07f));
            for (int i = 0; i < 5; i++)
            {
                float z = Mathf.Lerp(-hb + 0.1f, hb - 0.1f, i / 4f);
                m.CylinderPair(Slot.Metal, new Vector3(xout - 0.02f, 0.27f, z), new Vector3(xout + 0.05f, 0.27f, z), 0.22f, 10);
                m.CylinderPair(Slot.Dark, new Vector3(xout + 0.05f, 0.27f, z), new Vector3(xout + 0.08f, 0.27f, z), 0.08f, 6);
            }
            m.CylinderPair(Slot.Metal, new Vector3(xout - 0.02f, 0.36f, hb + 0.3f), new Vector3(xout + 0.05f, 0.36f, hb + 0.3f), 0.2f, 8);
            m.CylinderPair(Slot.Metal, new Vector3(xout - 0.02f, 0.36f, -hb - 0.3f), new Vector3(xout + 0.05f, 0.36f, -hb - 0.3f), 0.18f, 8);

            // Hull: lower tub between the tracks, upper hull over them with a sloped glacis.
            m.Section(Slot.Paint, -hb - 0.2f, hb + 0.1f, 0.18f, 0.18f, yT, yT, xin - 0.02f, xin - 0.02f, xin, xin, zR + 0.15f, zF - 0.25f);
            float deckFront = zF - 0.85f, deckBack = zR + 0.2f, deckHalf = hw - 0.06f;
            m.Section(Slot.Paint, zR + 0.05f, zF - 0.05f, yT, yT, yD, yD, hw, hw, deckHalf, deckHalf, deckBack, deckFront);
            m.BoxPair(Slot.Dark, new Vector3(hw + 0.01f, yT + 0.02f, (zR + zF) * 0.5f), new Vector3(0.05f, 0.04f, s.Length - 0.15f));

            var inside = new Vector3(0f, (yT + yD) * 0.5f, 0f);
            Vector3 gBL = new Vector3(-hw, yT, zF - 0.05f), gBR = new Vector3(hw, yT, zF - 0.05f), gTR = new Vector3(deckHalf, yD, deckFront), gTL = new Vector3(-deckHalf, yD, deckFront);
            m.Panel(Slot.Head, gBL, gBR, gTR, gTL, 0.07f, 0.17f, 0.55f, 0.78f, 0.03f, inside);
            m.Panel(Slot.Head, gBL, gBR, gTR, gTL, 0.83f, 0.93f, 0.55f, 0.78f, 0.03f, inside);
            Vector3 kBL = new Vector3(-deckHalf, yD, deckBack), kBR = new Vector3(deckHalf, yD, deckBack), kTR = new Vector3(deckHalf, yD, deckFront), kTL = new Vector3(-deckHalf, yD, deckFront);
            for (int i = 0; i < 4; i++) m.Panel(Slot.Dark, kBL, kBR, kTR, kTL, 0.22f, 0.78f, 0.03f + 0.065f * i, 0.075f + 0.065f * i, 0.02f, inside);
            Vector3 bBL = new Vector3(hw, yT, zR + 0.05f), bBR = new Vector3(-hw, yT, zR + 0.05f), bTR = new Vector3(-deckHalf, yD, deckBack), bTL = new Vector3(deckHalf, yD, deckBack);
            m.Panel(Slot.Tail, bBL, bBR, bTR, bTL, 0.05f, 0.15f, 0.55f, 0.8f, 0.02f, inside);
            m.Panel(Slot.Tail, bBL, bBR, bTR, bTL, 0.85f, 0.95f, 0.55f, 0.8f, 0.02f, inside);

            if (Has(d, BodyKit.Skirts))
            {
                m.Hull(Slot.Paint,
                    new Vector3(hw - 0.02f, 0.3f, -hb - 0.3f), new Vector3(hw + 0.03f, 0.3f, -hb - 0.3f), new Vector3(hw + 0.03f, 0.3f, hb + 0.2f), new Vector3(hw - 0.02f, 0.3f, hb + 0.2f),
                    new Vector3(hw - 0.02f, yT, -hb - 0.35f), new Vector3(hw + 0.03f, yT, -hb - 0.35f), new Vector3(hw + 0.03f, yT, hb + 0.4f), new Vector3(hw - 0.02f, yT, hb + 0.4f));
                m.Hull(Slot.Paint,
                    new Vector3(-hw - 0.03f, 0.3f, -hb - 0.3f), new Vector3(-hw + 0.02f, 0.3f, -hb - 0.3f), new Vector3(-hw + 0.02f, 0.3f, hb + 0.2f), new Vector3(-hw - 0.03f, 0.3f, hb + 0.2f),
                    new Vector3(-hw - 0.03f, yT, -hb - 0.35f), new Vector3(-hw + 0.02f, yT, -hb - 0.35f), new Vector3(-hw + 0.02f, yT, hb + 0.4f), new Vector3(-hw - 0.03f, yT, hb + 0.4f));
            }
            if (Has(d, BodyKit.ReactiveArmor))
            {
                for (int row = 0; row < 2; row++)
                    for (int i = 0; i < 5; i++)
                        m.Panel(Slot.Paint, gBL, gBR, gTR, gTL, 0.2f + 0.12f * i, 0.3f + 0.12f * i, 0.08f + 0.22f * row, 0.26f + 0.22f * row, 0.06f, inside);
                if (Has(d, BodyKit.Skirts))
                {
                    Vector3 kR = new Vector3(-hw - 0.03f, 0.3f, -hb - 0.3f), kF = new Vector3(-hw - 0.03f, 0.3f, hb + 0.2f), kFT = new Vector3(-hw - 0.03f, yT, hb + 0.4f), kRT = new Vector3(-hw - 0.03f, yT, -hb - 0.35f);
                    for (int i = 0; i < 6; i++) m.PanelPair(Slot.Paint, kR, kF, kFT, kRT, 0.04f + 0.16f * i, 0.18f + 0.16f * i, 0.12f, 0.88f, 0.05f, new Vector3(0f, 0.45f, 0f));
                }
            }
            if (Has(d, BodyKit.Stacks))
            {
                m.BoxPair(Slot.Dark, new Vector3(hw - 0.35f, yD + 0.09f, zR + 0.4f), new Vector3(0.26f, 0.18f, 0.32f));
                m.CylinderPair(Slot.Metal, new Vector3(hw - 0.35f, yD + 0.12f, zR + 0.3f), new Vector3(hw - 0.35f, yD + 0.12f, zR - 0.02f), 0.06f, 8);
            }

            // Turret shell in the yaw pivot's space. CarCannon's barrel pivot sits at (0, 0.1, 0.1) in it.
            float ts, barrel;
            switch (d.Id)
            {
                case "lynx": ts = 0.85f; barrel = 1.55f; break;
                case "mammoth": ts = 1.2f; barrel = 1.25f; break;
                case "titan": ts = 1.15f; barrel = 1.45f; break;
                case "grizzly": ts = 1.05f; barrel = 1.3f; break;
                default: ts = 1f; barrel = 1.25f; break;
            }
            ts *= 1.2f;   // a turret about two thirds of the hull width reads as a tank from the chase camera
            model.TurretPosition = new Vector3(0f, yD + 0.12f, -0.1f);
            model.HideStockTurret = true;
            model.BarrelScale = new Vector3(1.8f, 1.8f, barrel);
            var t = new BodyMesh();
            float top = 0.26f * Mathf.Sqrt(ts);
            t.Hull(Slot.Paint,
                new Vector3(-0.62f * ts, -0.12f, -0.75f * ts), new Vector3(0.62f * ts, -0.12f, -0.75f * ts), new Vector3(0.5f * ts, -0.12f, 0.4f * ts), new Vector3(-0.5f * ts, -0.12f, 0.4f * ts),
                new Vector3(-0.52f * ts, top, -0.66f * ts), new Vector3(0.52f * ts, top, -0.66f * ts), new Vector3(0.34f * ts, top, 0.22f * ts), new Vector3(-0.34f * ts, top, 0.22f * ts));
            t.Box(Slot.Paint, new Vector3(0f, 0.1f, 0.4f * ts + 0.04f), new Vector3(0.36f, 0.26f, 0.2f));   // mantlet around the barrel root
            if (d.Id != "tortoise" && d.Id != "lynx")
                t.Section(Slot.Paint, -1.0f * ts, -0.7f * ts, -0.06f, -0.08f, top - 0.04f, top, 0.4f * ts, 0.5f * ts, 0.36f * ts, 0.48f * ts);   // bustle
            t.Cylinder(Slot.Dark, new Vector3(0.24f * ts, top - 0.01f, -0.3f * ts), new Vector3(0.24f * ts, top + 0.1f, -0.3f * ts), 0.17f, 8);
            t.Cylinder(Slot.Paint, new Vector3(0.24f * ts, top + 0.1f, -0.3f * ts), new Vector3(0.24f * ts, top + 0.13f, -0.3f * ts), 0.15f, 8);
            t.Box(Slot.Glass, new Vector3(0.24f * ts, top + 0.06f, -0.13f * ts), new Vector3(0.1f, 0.04f, 0.03f));
            t.Box(Slot.Glass, new Vector3(-0.2f * ts, top + 0.03f, 0.05f * ts), new Vector3(0.1f, 0.05f, 0.08f));
            for (int i = 0; i < 3; i++)   // smoke dischargers
                t.CylinderPair(Slot.Dark, new Vector3(0.45f * ts, 0.02f + 0.07f * i, 0.05f * ts), new Vector3(0.52f * ts, 0.05f + 0.07f * i, 0.2f * ts), 0.03f, 6);
            if (Has(d, BodyKit.Antenna))
                t.Cylinder(Slot.Dark, new Vector3(-0.4f * ts, top, -0.62f * ts), new Vector3(-0.42f * ts, top + 1.15f, -0.85f * ts), 0.012f, 4);
            if (Has(d, BodyKit.ReactiveArmor))
                for (int i = 0; i < 3; i++) t.BoxPair(Slot.Paint, new Vector3(0.2f + 0.14f * i, top + 0.03f, 0.05f), new Vector3(0.12f, 0.06f, 0.2f));
            model.TurretPaint = t.Build("Body_" + d.Id + "_TurretPaint", BodyMesh.PaintSlots, out _);
            model.TurretTrim = t.Build("Body_" + d.Id + "_TurretTrim", BodyMesh.TrimSlots, out model.TurretTrimSlots);

            if (Has(d, BodyKit.TwinBarrel))
            {
                // Two tubes in the barrel pivot's space (before its scale), either side of the centred muzzle.
                var b = new BodyMesh();
                b.CylinderPair(Slot.Dark, new Vector3(0.075f, 0f, 0f), new Vector3(0.075f, 0f, 1.3f), 0.05f, 8);
                b.CylinderPair(Slot.Dark, new Vector3(0.075f, 0f, 1.18f), new Vector3(0.075f, 0f, 1.3f), 0.07f, 8);
                model.Barrel = b.Build("Body_" + d.Id + "_Barrel", BodyMesh.TrimSlots, out model.BarrelSlots);
                model.HideStockBarrel = true;
            }
        }
    }

    internal static class BodyMeshPairs
    {
        /// <summary>Cylinder mirrored to both sides.</summary>
        public static void CylinderPair(this BodyMesh m, BodyMesh.Slot s, Vector3 a, Vector3 b, float radius, int segments = 8)
        {
            m.Cylinder(s, a, b, radius, segments);
            m.Cylinder(s, new Vector3(-a.x, a.y, a.z), new Vector3(-b.x, b.y, b.z), radius, segments);
        }
    }
}
