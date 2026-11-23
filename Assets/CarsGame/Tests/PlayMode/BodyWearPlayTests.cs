using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class BodyWearPlayTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator TwoRealImpactsDeformEitherHullWhileItsGunAndCollidersRemainIndependent()
        {
            float clock = Time.captureDeltaTime;
            string saved = null;
            GameFlow flow = null;
            RaceEventDefinition ev = null;
            GameObject obstacle = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
                ev = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                ev.aiCountOverride = 0; ev.pickupCount = 0; ev.damageMultiplier = 1f;
                foreach (bool classic in new[] { true, false })
                {
                    if (PlatformGlue.ClassicCarModels != classic) PlatformGlue.ToggleCarModels();
                    flow.StartRace(ev, flow.tiers[0]);
                    while (race.State == RaceState.Countdown) yield return null;
                    var player = race.Player;
                    var car = player.Car;
                    var health = player.Health; health.respawnOnDeath = true;
                    var body = car.GetComponent<CarBody>();
                    var hull = car.transform.Find(classic ? "Car_Visuals" : "Tripo_" + body.Class);
                    Assert.NotNull(hull);
                    Vector3 originalScale = hull.localScale;
                    Quaternion originalRot = hull.localRotation;
                    var box = car.GetComponent<BoxCollider>();
                    Vector3 size = box.size, center = box.center;
                    var phys = car.GetComponentsInChildren<Collider>(true);
                    var gun = car.GetComponent<CarCannon>();
                    var barrelParent = gun.barrel.parent;
                    var path = player.Progress.Path;
                    path.Pose(path.startLineDistance + 14f, 0f, out var start, out var rot);
                    var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                    car.SetInputSource(input); input.Set(0f, 0f);
                    obstacle = new GameObject("ImpactWall", typeof(BoxCollider));
                    obstacle.transform.SetPositionAndRotation(start + rot * Vector3.forward * 11f + Vector3.up * 2f, rot);
                    obstacle.GetComponent<BoxCollider>().size = new Vector3(10f, 4f, 1f);
                    string directory = System.Environment.GetEnvironmentVariable("CG_WEAR_SHOTS");
                    void Capture(string state)
                    {
                        if (string.IsNullOrEmpty(directory)) return;
                        Directory.CreateDirectory(directory);
                        var camera = Camera.main;
                        Vector3 pivot = car.transform.position;
                        camera.transform.position = pivot + car.transform.rotation * new Vector3(3.5f, 2.1f, 4.8f);
                        camera.transform.LookAt(pivot + Vector3.up * .6f);
                        TrackForkPlayTests.Capture(camera, Path.Combine(directory, (classic ? "classic-" : "new-") + state + ".png"));
                    }
                    car.Teleport(start + Vector3.up * .45f, rot); yield return null;
                    Capture("intact");
                    for (int stage = 1; stage <= 2; stage++)
                    {
                        car.Teleport(start + Vector3.up * .45f, rot);
                        car.Body.linearVelocity = rot * Vector3.forward * 17f;
                        for (int i = 0; i < 90 && body.DamageStage < stage; i++) yield return null;
                        Assert.AreEqual(stage, body.DamageStage);
                        Assert.Less(hull.localScale.z, originalScale.z * (1f - stage * .05f));
                        Assert.AreEqual(size, box.size); Assert.AreEqual(center, box.center);
                        CollectionAssert.AreEquivalent(phys, car.GetComponentsInChildren<Collider>(true));
                        Assert.AreSame(barrelParent, gun.barrel.parent);
                        car.Teleport(start + Vector3.up * .45f, rot);
                        for (int i = 0; i < 14; i++) yield return null;
                        Capture("stage" + stage);
                        int shots = RaceDirector.Current.ShotsFired;
                        Assert.IsTrue(gun.TryFire()); Assert.Greater(RaceDirector.Current.ShotsFired, shots);
                        for (int i = 0; i < 75; i++) yield return null;
                    }
                    Object.Destroy(obstacle); obstacle = null;
                    bool respawned = false; health.OnRespawned += () => respawned = true;
                    health.Detonate(null);
                    for (int i = 0; i < 380 && !respawned; i++) yield return null;
                    Assert.IsTrue(respawned); Assert.AreEqual(0, body.DamageStage);
                    Assert.AreEqual(originalScale, hull.localScale); Assert.AreEqual(originalRot, hull.localRotation);
                    car.Teleport(start + Vector3.up * .45f, rot);
                    for (int i = 0; i < 14; i++) yield return null;
                    Capture("restored");
                    Debug.Log($"[BodyWear] classic={classic}, two impacts, two shells, {phys.Length} unchanged colliders, repaired on respawn");
                    flow.EnterMenu(); yield return null;
                }
            }
            finally
            {
                if (obstacle != null) Object.Destroy(obstacle);
                if (flow != null) flow.EnterMenu();
                if (ev != null) Object.Destroy(ev);
                if (saved != null) { JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
                Time.captureDeltaTime = clock;
            }
        }
    }
}
