using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public sealed partial class CarBody
    {
        private RelayGeometry relayGeometry;

        // Capture only the unmodified prefab used by the relay runner. Other cars retain their light spawn path.
        internal static void PrepareRelay(GameObject go)
        {
            var body = go.GetComponent<CarBody>() ?? go.AddComponent<CarBody>();
            body.relayGeometry = new RelayGeometry(go);
        }

        private sealed class RelayGeometry
        {
            private readonly Transform root;
            private readonly List<StockPart> stock = new List<StockPart>();
            private readonly List<Transform> generated = new List<Transform>();
            private HashSet<Transform> beforeBuild;
            private readonly struct StockPart
            {
                private readonly Transform part;
                private readonly Vector3 position, scale;
                private readonly Quaternion rotation;
                private readonly Renderer renderer;
                private readonly bool active, rendering;
                public StockPart(Transform t)
                {
                    part = t; position = t.localPosition; rotation = t.localRotation; scale = t.localScale;
                    active = t.gameObject.activeSelf; renderer = t.GetComponent<Renderer>(); rendering = renderer != null && renderer.enabled;
                }
                public void Restore()
                {
                    part.SetLocalPositionAndRotation(position, rotation); part.localScale = scale;
                    part.gameObject.SetActive(active);
                    if (renderer != null) renderer.enabled = rendering;
                }
            }

            public RelayGeometry(GameObject go)
            {
                root = go.transform;
                var car = go.GetComponent<CarController>(); var cannon = go.GetComponent<CarCannon>();
                var parts = new[] { root.Find("Car_Visuals"), cannon?.turret, cannon?.barrel, car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel };
                var found = new HashSet<Transform>();
                foreach (var part in parts)
                    if (part != null) foreach (var t in part.GetComponentsInChildren<Transform>(true))
                        if (found.Add(t)) stock.Add(new StockPart(t));
            }

            public void BeginBuild() => beforeBuild = new HashSet<Transform>(root.GetComponentsInChildren<Transform>(true));
            public void EndBuild()
            {
                if (beforeBuild == null) return;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (!beforeBuild.Contains(t) && beforeBuild.Contains(t.parent)) generated.Add(t);
                beforeBuild = null;
            }

            public void Restore(CarBody body)
            {
                body.ClearWear();
                // The garage's visual-style toggle can add a detailed hull and wheels after the initial spawn.
                if (body.newHull != null && !generated.Contains(body.newHull)) generated.Add(body.newHull);
                foreach (var wheel in body.alloyWheels) if (wheel != null && !generated.Contains(wheel)) generated.Add(wheel);
                foreach (var obj in generated)
                    if (obj != null) { obj.gameObject.SetActive(false); obj.SetParent(null, true); Destroy(obj.gameObject); }
                foreach (var part in stock) part.Restore();
                generated.Clear(); body.alloyWheels.Clear(); body.extraSpinners.Clear(); body.enabled = false;
                body.classicHull = body.newHull = null;
            }
        }
    }
}
