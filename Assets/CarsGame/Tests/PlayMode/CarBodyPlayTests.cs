using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    /// <summary>
    /// Car bodies on the real RaceCar prefab and track: every body builds a sane car, and one body per class drives
    /// with its own character (launch, top speed on the start straight, turn rate, grass).
    /// </summary>
    public sealed class CarBodyPlayTests
    {
        private float savedCaptureDelta;
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float wait = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < wait) yield return null;
            yield return null; yield return null;   // GameFlow.Start (EnterMenu) must run before a race starts
        }

        private static RaceEventDefinition SoloEvent(GameFlow flow)
        {
            RaceEventDefinition ev = null;
            foreach (var e in flow.events) if (e != null && e.eventId == "e1") ev = e;
            ev = Object.Instantiate(ev); ev.aiCountOverride = 0; ev.laps = 1; ev.pickupCount = 0;
            return ev;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator EveryBodyBuildsASaneCar()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadGame();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var prefab = race.spawner.raceCarPrefab;
            var log = new StringBuilder();
            int i = 0, stockRenderers = 0;
            var stock = Object.Instantiate(prefab, new Vector3(400f, 50f, 400f), Quaternion.identity);   // the turret is built in Awake
            var stockRb = stock.GetComponent<Rigidbody>();
            Debug.Log($"[CarBodyPlayTests] stock inertia at spawn {stockRb.inertiaTensor} com {stockRb.centerOfMass}");
            yield return new WaitForFixedUpdate();
            Debug.Log($"[CarBodyPlayTests] stock inertia after a step {stockRb.inertiaTensor}");
            foreach (var r in stock.GetComponentsInChildren<Renderer>()) if (r.enabled) stockRenderers++;
            Object.Destroy(stock);
            foreach (var def in CarBodies.All)
            {
                var go = Object.Instantiate(prefab, new Vector3(500f + 8f * i++, 50f, 500f), Quaternion.identity);
                try
                {
                    var body = CarBody.Apply(go, def);
                    var livery = go.AddComponent<CarLivery>();
                    livery.Apply(LiveryPattern.CenterStripe, Color.white);
                    yield return null;
                    var rb = go.GetComponent<Rigidbody>();
                    var car = go.GetComponent<CarController>();
                    var health = go.GetComponent<CarHealth>();
                    var cannon = go.GetComponent<CarCannon>();
                    int renderers = 0; foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled) renderers++;
                    int tris = 0; foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null && mf.GetComponent<Renderer>().enabled) tris += mf.sharedMesh.triangles.Length / 3;
                    Debug.Log($"[CarBodyPlayTests] {def.Id,-10} {def.Class,-7} mass={rb.mass:0} hp={health.maxHealth:0} top={car.maxSpeed:0} renderers={renderers} tris={tris} livery={livery.TriangleCount} turretY={cannon.turret.localPosition.y:0.00} inertia={rb.inertiaTensor} steerScale={car.steerTorqueScale:0.00}");

                    Assert.AreEqual(def.Id, body.BodyId);
                    Assert.AreEqual(1250f * def.Stats.Mass, rb.mass, 0.5f, def.Id);
                    Assert.AreEqual(100f * def.Stats.Health, health.maxHealth, 0.5f, def.Id);
                    Assert.AreEqual(100f * def.Stats.Health, health.Health, 0.5f, def.Id + " starts at full health");
                    Assert.LessOrEqual(renderers, stockRenderers + 3, def.Id + ": renderer budget (stock car + livery + 2)");
                    Assert.Less(tris, 12000, def.Id + ": triangle budget");
                    Assert.Greater(livery.TriangleCount, 0, def.Id + ": the livery finds the painted hull");
                    var box = go.GetComponent<BoxCollider>();
                    Assert.AreEqual(def.Shape.Width, box.size.x, 0.01f, def.Id);
                    Assert.AreEqual(def.Shape.Length, box.size.z, 0.01f, def.Id);
                    var sphere = go.transform.Find("Wheel_FL_Collider").GetComponent<SphereCollider>();
                    Assert.AreEqual(def.Shape.WheelRadius, sphere.radius, 0.001f, def.Id);
                    Assert.AreEqual(def.Shape.WheelRadius, sphere.transform.localPosition.y, 0.001f, def.Id + ": wheels touch the ground at y = 0");
                    // The cannon sits on the roof, above the hull box, whatever the shape.
                    Assert.GreaterOrEqual(cannon.turret.localPosition.y + 0.1f, def.Shape.Clearance + def.Shape.Height, def.Id);
                    Assert.AreEqual(30f * def.Stats.TurretArc, cannon.turretYawLimit, 0.01f, def.Id);
                    if (def.Class == BodyClass.Tank)
                        foreach (var r in car.frontLeftWheel.GetComponentsInChildren<Renderer>(true)) Assert.IsFalse(r.enabled, def.Id + ": tracks replace the wheels");
                    // Paint reaches the new hull.
                    var paint = go.GetComponent<CarPaint>(); paint.SetColor(Color.green);
                    var hull = FindPaintRenderer(go);
                    Assert.NotNull(hull, def.Id);
                    var block = new MaterialPropertyBlock(); hull.GetPropertyBlock(block);
                    Assert.AreEqual(Color.green, block.GetColor("_BaseColor"), def.Id + ": the paint colours the hull");
                }
                finally { Object.Destroy(go); }
            }
            Debug.Log("[CarBodyPlayTests]\n" + log);
        }

        private static Renderer FindPaintRenderer(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
                if (r.enabled && r.sharedMaterial != null && r.sharedMaterial.name.Contains("CarPaint")) return r;
            return null;
        }

        private struct Run { public float speed1, speed3, yawRate, turnSpeed, grassSpeed; public int grounded; public bool upright; public string state; }

        private static IEnumerator Drive(GameFlow flow, RaceManager race, string bodyId, Run[] result)
        {
            race.spawner.playerBodyId = bodyId;
            flow.StartRace(SoloEvent(flow), null);
            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            while (race.State == RaceState.Countdown) yield return null;
            var run = new Run { upright = true };
            input.Set(1f, 0f);
            for (int i = 1; i <= 150; i++)
            {
                yield return null;
                if (i == 50) run.speed1 = car.SpeedKmh;
                run.upright &= car.transform.up.y > 0.85f;
            }
            run.speed3 = car.SpeedKmh;
            // Full lock at a fixed 40 km/h: the settled yaw rate is the body's agility.
            car.Teleport(car.transform.position + Vector3.up * 0.3f, car.transform.rotation);
            for (int i = 0; i < 60; i++) { input.Set(car.SpeedKmh < 40f ? 1f : 0f, 0f); yield return null; }
            float yaw = 0f;
            for (int i = 0; i < 40; i++)
            {
                input.Set(car.SpeedKmh < 40f ? 0.8f : 0f, 1f);
                yield return null;
                if (i >= 20) yaw += Mathf.Abs(car.Body.angularVelocity.y) * Mathf.Rad2Deg / 20f;
                run.upright &= car.transform.up.y > 0.85f;
            }
            run.yawRate = yaw;
            run.turnSpeed = car.SpeedKmh;
            run.state = $"input={car.inputEnabled} kinematic={car.Body.isKinematic} grounded={car.IsGrounded} surface={car.Surface} enabled={car.enabled} pos={car.transform.position}";
            // Grass: a flat Ground-layer field (the participant's lost-car reset is paused for it) far from the track, so trees and barriers cannot get in the way.
            var field = GameObject.CreatePrimitive(PrimitiveType.Cube);
            field.layer = LayerMask.NameToLayer("Ground");
            field.transform.position = new Vector3(3000f, -0.5f, 3000f);
            field.transform.localScale = new Vector3(400f, 1f, 400f);
            yield return null;
            var participant = car.GetComponent<RaceParticipant>(); participant.enabled = false;
            car.Teleport(new Vector3(3000f, 0.3f, 2850f), Quaternion.identity);
            input.Set(1f, 0f);
            for (int i = 0; i < 150; i++) { yield return null; if (car.Surface == GroundSurface.Ground) run.grounded++; }
            run.grassSpeed = car.SpeedKmh;
            Object.Destroy(field);
            participant.enabled = true;
            result[0] = run;
            Time.captureDeltaTime = 0f;
            flow.EnterMenu();
            yield return null;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator EachClassDrivesWithItsOwnCharacter()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return LoadGame();
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            string[] ids = { "classic", "dune", "bulwark", "tortoise" };
            var runs = new Run[ids.Length];
            var log = new StringBuilder();
            for (int k = 0; k < ids.Length; k++)
            {
                var r = new Run[1];
                yield return Drive(flow, race, ids[k], r);
                runs[k] = r[0];
                log.AppendLine($"{ids[k],-9} 1s={r[0].speed1:0.0} 3s={r[0].speed3:0.0} km/h  yaw@40={r[0].yawRate:0.0} °/s at {r[0].turnSpeed:0} km/h  grass3s={r[0].grassSpeed:0.0} km/h ({r[0].grounded} ticks on grass)  upright={r[0].upright}  {r[0].state}");
            }
            race.spawner.playerBodyId = "";
            Debug.Log("[CarBodyPlayTests] drive\n" + log);

            for (int k = 0; k < ids.Length; k++) Assert.IsTrue(runs[k].upright, ids[k] + " stays on its wheels");
            Assert.Greater(runs[1].speed1, runs[0].speed1, "the buggy launches harder than the sports car");
            Assert.Greater(runs[0].speed1, runs[2].speed1, "the sports car launches harder than the armored car");
            Assert.Greater(runs[2].speed1, runs[3].speed1, "the armored car launches harder than the tank");
            Assert.Greater(runs[3].speed3, 30f, "the tank still gets going");
            Assert.Greater(runs[1].yawRate, runs[0].yawRate, "the buggy turns quicker");
            Assert.Greater(runs[3].yawRate, 20f, "the tank can still turn");
            Assert.Greater(runs[1].grassSpeed, runs[0].grassSpeed, "the buggy is quicker on grass than the sports car");
            Assert.Greater(runs[3].grassSpeed, runs[2].grassSpeed, "tracks beat the armored car on grass");
        }
    }
}
