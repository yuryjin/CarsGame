using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class ResourceLifetimePlayTests
    {
        private GameFlow flow;
        private MenuScreens menu;
        private string originalLanguage;
        private readonly List<Sample> samples = new List<Sample>();

        [Serializable] private sealed class Sample
        {
            public int round, meshes, materials, renderTextures, gameObjects;
            public long nativeBytes, managedBytes;
            public string[] meshNames, materialNames;
        }
        [Serializable] private sealed class Report { public List<Sample> samples; }

        [TearDown]
        public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (originalLanguage != null && PlatformGlue.Save != null)
                PlatformGlue.SetLanguageOverride(originalLanguage);
            PauseController.ResetAll();
            string output = Environment.GetEnvironmentVariable("CG_LIFETIME_REPORT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(new Report { samples = samples }, true));
            }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator RepeatedGarageAndGeneratedTracksReleaseTheirRuntimeResources()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            menu = Object.FindAnyObjectByType<MenuScreens>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            // Background persistence may update its timestamp without changing any progression.
            string saved = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            originalLanguage = PlatformGlue.Save.Data.settings.lang ?? "";
            string languageList = Environment.GetEnvironmentVariable("CG_LIFETIME_LANGS");
            var languages = string.IsNullOrEmpty(languageList) ? new[] { originalLanguage } : languageList.Split(',');
            int rounds = int.TryParse(Environment.GetEnvironmentVariable("CG_LIFETIME_ROUNDS"), out int value) ? Mathf.Clamp(value, 5, 100) : 8;
            Sample baseline = null;
            for (int round = 0; round < rounds; round++)
            {
                foreach (string language in languages)
                {
                    if (PlatformGlue.Save.Data.settings.lang != language)
                    {
                        PlatformGlue.SetLanguageOverride(language);
                        until = Time.realtimeSinceStartup + 30f;
                        while (!Loc.Ready && Time.realtimeSinceStartup < until) yield return null;
                        Assert.IsTrue(Loc.Ready, "Locale table must finish loading before opening the garage: " + language);
                        yield return null; yield return null;
                    }
                    foreach (string body in new[] { CarBodies.DefaultId, "dune", "rhino", "tortoise" })
                    {
                        menu.PreviewBody(body);
                        yield return null; yield return null;
                        Assert.NotNull(Object.FindAnyObjectByType<GaragePreview>());
                    }
                    menu.Show("main");
                    foreach (string id in new[] { "e1", "e16", "e17", "e19", "e20" })
                    {
                        var ev = Array.Find(flow.events, e => e.eventId == id);
                        Assert.NotNull(ev);
                        flow.StartRace(ev, flow.tiers[0]);
                        Assert.AreEqual(RaceState.Countdown, race.State);
                        for (int i = 0; i < 4; i++) yield return null;
                        flow.EnterMenu();
                        yield return null; yield return null;
                        Assert.AreEqual(0, RaceDirector.Instance.Cars.Count, "No car may survive returning to the menu.");
                        Assert.IsFalse(TrackPath.Instance.HasLayout, "Generated layouts must release the shared path.");
                    }
                }
                for (int i = 0; i < 4; i++) yield return null;
                Assert.IsNull(Object.FindAnyObjectByType<GaragePreview>());
                Assert.IsNull(GameObject.Find("GarageStage"));
                Assert.IsFalse(Array.Exists(Resources.FindObjectsOfTypeAll<RenderTexture>(), t => t.name == "GaragePreview"));
                var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
                var materials = Resources.FindObjectsOfTypeAll<Material>();
                var sample = new Sample {
                    round = round, meshes = meshes.Length, materials = materials.Length,
                    renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length,
                    gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length,
                    nativeBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                    managedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(),
                    meshNames = Array.ConvertAll(meshes, m => m.name), materialNames = Array.ConvertAll(materials, m => m.name)
                };
                Array.Sort(sample.meshNames); Array.Sort(sample.materialNames);
                samples.Add(sample);
                Debug.Log($"[Lifetime] round={round} mesh={sample.meshes} material={sample.materials} RT={sample.renderTextures} GO={sample.gameObjects} bytes={sample.nativeBytes}");
                // Warm all body/weapon/shader caches before comparing identical subsequent rounds.
                if (round == 2) baseline = sample;
            }
            var last = samples[samples.Count - 1];
            Assert.LessOrEqual(last.meshes, baseline.meshes + 3, "Meshes grow after identical warmed-up scene transitions; inspect the lifetime report.");
            Assert.LessOrEqual(last.materials, baseline.materials + 3, "Materials grow after identical warmed-up scene transitions; inspect the lifetime report.");
            Assert.LessOrEqual(last.renderTextures, baseline.renderTextures + 1, "Render textures grow after leaving the garage.");
            Assert.LessOrEqual(last.gameObjects, baseline.gameObjects + 5, "Scene objects accumulate after returning to the same menu.");
            Assert.AreEqual(saved, JsonUtility.ToJson(PlatformGlue.Save.Data.progress), "Aborted countdowns and viewing car bodies must not modify progression.");
        }
    }
}
