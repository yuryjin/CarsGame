using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// After a WebGL build: validate the Yandex constraints (index.html at root, no spaces or
    /// Cyrillic in paths, ≤100 MB uncompressed), write a size report and zip the folder
    /// contents so index.html sits at the archive root.
    /// </summary>
    public sealed class WebZipPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;
        public const long MaxUncompressedBytes = 100L * 1024 * 1024;
        public const long WarnBytes = 40L * 1024 * 1024;

        public void OnPostprocessBuild(BuildReport report) => Process(report);

        /// <summary>Also called explicitly by BuildCommands so the zip exists even if the callback is skipped.</summary>
        public static void Process(BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.WebGL || report.summary.result != BuildResult.Succeeded) return;
            string dir = report.summary.outputPath;
            var problems = new StringBuilder();
            string version = Application.version;
            string zipPath = Path.Combine(Path.GetDirectoryName(dir) ?? ".", $"{Sanitize(Application.productName)}-web-{version}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);   // never leave a stale artifact behind a failed validation

            MoveOutDoNotShip(dir);
            if (!File.Exists(Path.Combine(dir, "index.html"))) problems.AppendLine("index.html missing at build root");
            var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
            long total = 0;
            var bad = new Regex(@"[\sЀ-ӿ]");
            foreach (var f in files)
            {
                total += new FileInfo(f).Length;
                string rel = f.Substring(dir.Length).Replace('\\', '/');
                if (bad.IsMatch(rel)) problems.AppendLine("bad path (space/Cyrillic): " + rel);
            }
            if (total > MaxUncompressedBytes) problems.AppendLine($"uncompressed size {total / 1048576} MB exceeds 100 MB");
            else if (total > WarnBytes) Debug.LogWarning($"[WebZip] uncompressed size {total / 1048576} MB — over the 40 MB comfort line");

            WriteSizeReport(report, dir, total);

            if (problems.Length > 0) throw new BuildFailedException("[WebZip] Yandex validation failed:\n" + problems);

            ZipFile.CreateFromDirectory(dir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                if (zip.GetEntry("index.html") == null) throw new BuildFailedException("[WebZip] index.html is not at the zip root");
            }
            Debug.Log($"[WebZip] {zipPath} ({new FileInfo(zipPath).Length / 1048576} MB zip, {total / 1048576} MB uncompressed)");
        }

        /// <summary>
        /// Unity writes debug-only folders such as "&lt;Product&gt;_BurstDebugInformation_DoNotShip" next to the
        /// player; they must not reach the store archive. They move beside the build folder instead.
        /// </summary>
        private static void MoveOutDoNotShip(string dir)
        {
            foreach (var sub in Directory.GetDirectories(dir, "*_DoNotShip", SearchOption.TopDirectoryOnly))
            {
                string target = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dir).TrimEnd('/', '\\')) ?? ".", Path.GetFileName(sub));
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.Move(sub, target);
                Debug.Log("[WebZip] moved out of the shipped folder: " + Path.GetFileName(sub));
            }
        }

        private static void WriteSizeReport(BuildReport report, string dir, long total)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Build {report.summary.result} — uncompressed {total / 1048576.0:F1} MB, {report.summary.totalTime}");
            foreach (var step in report.steps) if (step.duration.TotalSeconds > 5) sb.AppendLine($"step {step.name}: {step.duration.TotalSeconds:F0}s");
            var packed = report.packedAssets.SelectMany(p => p.contents).OrderByDescending(c => c.packedSize).Take(40);
            sb.AppendLine("Top assets:");
            foreach (var c in packed) sb.AppendLine($"  {c.packedSize / 1024,8} KB  {c.sourceAssetPath}");
            File.WriteAllText(Path.Combine(dir, "..", "build-size-report.txt"), sb.ToString());
        }

        private static string Sanitize(string s) => Regex.Replace(s ?? "game", @"[^A-Za-z0-9_-]", "");
    }
}
