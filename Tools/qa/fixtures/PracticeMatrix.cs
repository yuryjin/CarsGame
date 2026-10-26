// Copy into an isolated PlayMode assembly. Runs physical skills without restarting the Editor per body.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public sealed class PracticeMatrix
    {
        [UnityTest, Timeout(7200000)]
        public IEnumerator PhysicalSkillsAcrossBodyClasses()
        {
            string output = Environment.GetEnvironmentVariable("CG_PRACTICE_MATRIX_OUT");
            Assert.IsFalse(string.IsNullOrEmpty(output));
            Directory.CreateDirectory(output);
            string selected = Environment.GetEnvironmentVariable("CG_PRACTICE_BODIES");
            string[] bodies = string.IsNullOrWhiteSpace(selected)
                ? Array.ConvertAll(CarBodies.All, body => body.Id) : selected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (string body in bodies)
                Assert.IsTrue(Array.Exists(CarBodies.All, candidate => candidate.Id == body), "Unknown body: " + body);
            string requestedSkills = Environment.GetEnvironmentVariable("CG_PRACTICE_SKILLS");
            var allSkills = new[] { "drift", "aim", "shortcut", "ceiling" };
            string[] skills = string.IsNullOrWhiteSpace(requestedSkills) ? allSkills
                : requestedSkills.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (string skill in skills)
                CollectionAssert.Contains(allSkills, skill, "Unknown skill: " + skill);
            string previousBody = Environment.GetEnvironmentVariable("CG_PRACTICE_BODY");
            string previousSweep = Environment.GetEnvironmentVariable("CG_CLIMB_SWEEP");
            var failures = new List<string>();
            try
            {
                Environment.SetEnvironmentVariable("CG_CLIMB_SWEEP", "2");
                foreach (string body in bodies)
                {
                    Environment.SetEnvironmentVariable("CG_PRACTICE_BODY", body);
                    foreach (string skill in skills)
                    {
                        string tag = body + "/" + skill;
                        Debug.Log("[PracticeMatrix] START " + tag);
                        var test = new PracticePlayTests();
                        test.Setup();
                        DateTime started = DateTime.UtcNow;
                        bool passed = false;
                        Exception failure = null;
                        var stack = new Stack<IEnumerator>();
                        try
                        {
                            IEnumerator run = skill == "drift" ? test.RealDriftCompletesTheExerciseAndRetryRequiresANewDrift()
                                : skill == "aim" ? test.AimExerciseRequiresARealProjectileHitAndNeverAwardsCampaignProgress()
                                : skill == "shortcut" ? test.ShortcutLessonCompletesThroughARealBridgePassage()
                                : test.CeilingExerciseCanBeReachedByDrivingFromTheFloor();
                            stack.Push(run);
                            // Flatten nested routines so an assertion executes in this try/finally;
                            // a failed child yielded directly to Unity can bypass parent cleanup.
                            while (stack.Count > 0)
                            {
                                var routine = stack.Peek();
                                bool moved;
                                try { moved = routine.MoveNext(); }
                                catch (Exception exception) { failure = exception; break; }
                                if (!moved)
                                {
                                    stack.Pop(); (routine as IDisposable)?.Dispose();
                                    continue;
                                }
                                if (routine.Current is IEnumerator nested) stack.Push(nested);
                                else yield return routine.Current;
                            }
                            passed = failure == null;
                        }
                        finally
                        {
                            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                            test.Cleanup();
                            string status = tag + " " + (passed ? "passed" : "failed") + " "
                                + (DateTime.UtcNow - started).TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                            File.AppendAllText(Path.Combine(output, "status.txt"), status + "\n");
                            Debug.Log("[PracticeMatrix] END " + status);
                        }
                        if (failure != null)
                        {
                            failures.Add(tag);
                            File.AppendAllText(Path.Combine(output, "failures.txt"), tag + "\n" + failure + "\n");
                        }
                        yield return null;
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("CG_PRACTICE_BODY", previousBody);
                Environment.SetEnvironmentVariable("CG_CLIMB_SWEEP", previousSweep);
            }
            Assert.IsEmpty(failures, string.Join(", ", failures));
        }
    }
}
