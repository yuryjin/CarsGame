// Copy into an isolated project's PlayMode test assembly only. Never a shipped asset.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class RaceMatrix
    {
        [UnityTest, Timeout(14400000)]
        public IEnumerator MeasureBodiesInOneEditorSession()
        {
            string output = Environment.GetEnvironmentVariable("CG_MATRIX_OUT");
            Assert.IsFalse(string.IsNullOrEmpty(output), "Use an explicit output folder.");
            Directory.CreateDirectory(output);
            string events = Environment.GetEnvironmentVariable("CG_MATRIX_EVENTS") ?? "e1,e16,e17,e19,e20";
            string bodies = Environment.GetEnvironmentVariable("CG_MATRIX_BODIES");
            Assert.IsFalse(string.IsNullOrEmpty(bodies));
            string tiers = Environment.GetEnvironmentVariable("CG_MATRIX_TIERS");
            string seeds = Environment.GetEnvironmentVariable("CG_MATRIX_SEEDS");
            string reverses = Environment.GetEnvironmentVariable("CG_MATRIX_REVERSES");
            string rivals = Environment.GetEnvironmentVariable("CG_MATRIX_AI") ?? "0";
            string laps = Environment.GetEnvironmentVariable("CG_MATRIX_LAPS");
            string parameters = Environment.GetEnvironmentVariable("CG_MATRIX_PARAMS") ?? "";
            string[] keys = { "CG_EVENT", "CG_BODY", "CG_AI", "CG_LAPS", "CG_TIER", "CG_SEED", "CG_PARAMS", "CG_OUT", "CG_REVERSE" };
            var previous = new Dictionary<string, string>();
            foreach (string key in keys) previous[key] = Environment.GetEnvironmentVariable(key);
            try
            {
                foreach (string seed in (seeds ?? "12345").Split(','))
                foreach (string reverse in (reverses ?? "default").Split(','))
                foreach (string tier in (tiers ?? "normal").Split(','))
                foreach (string evt in events.Split(','))
                foreach (string body in bodies.Split(','))
                {
                    string tag = (seeds != null ? "seed" + seed + "_" : "") + (reverses != null ? "rev" + reverse + "_" : "") + (tiers != null ? tier + "_" : "") + evt + "_" + body;
                    string path = Path.Combine(output, "race_" + tag + ".json");
                    Environment.SetEnvironmentVariable("CG_EVENT", evt);
                    if (reverses != null) Environment.SetEnvironmentVariable("CG_REVERSE", reverse);
                    Environment.SetEnvironmentVariable("CG_BODY", body == "default" ? "" : body);
                    Environment.SetEnvironmentVariable("CG_AI", rivals);
                    Environment.SetEnvironmentVariable("CG_LAPS", laps ?? (evt == "e1" ? "1" : "3"));
                    Environment.SetEnvironmentVariable("CG_TIER", tier);
                    Environment.SetEnvironmentVariable("CG_SEED", seed);
                    Environment.SetEnvironmentVariable("CG_PARAMS", parameters);
                    Environment.SetEnvironmentVariable("CG_OUT", path);
                    var harness = new RaceHarness();
                    harness.SaveTimestep();
                    DateTime start = DateTime.UtcNow;
                    Debug.Log("[RaceMatrix] START " + tag);
                    try { yield return harness.AutopilotRace(); }
                    finally { harness.RestoreTimestep(); }
                    Assert.IsTrue(File.Exists(path), "Missing measurement: " + tag);
                    var report = JsonUtility.FromJson<RaceHarness.Report>(File.ReadAllText(path));
                    Assert.AreEqual(evt, report.evt);
                    Assert.AreEqual(tier, report.tier);
                    if (rivals == "0") Assert.AreEqual(1, report.cars.Length);
                    var player = Array.Find(report.cars, c => c.player);
                    Assert.NotNull(player);
                    if (body != "default") Assert.AreEqual(body, player.body);
                    string status = tag + " " + (player.finished ? "finished" : "unfinished") + " " + (DateTime.UtcNow - start).TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                    File.AppendAllText(Path.Combine(output, "status.txt"), status + "\n");
                    Debug.Log("[RaceMatrix] END " + status);
                    yield return null;
                }
            }
            finally { foreach (string key in keys) Environment.SetEnvironmentVariable(key, previous[key]); }
        }
    }
}
