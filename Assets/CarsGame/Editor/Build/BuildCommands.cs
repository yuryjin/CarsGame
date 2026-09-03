using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Store builds from the CLI: `unity build --target WebGL --execute-method RacingGame.EditorTools.BuildCommands.BuildYandex -- -buildOutput Builds/Yandex/web`.
    /// Player settings that differ per store are applied here (defines, template, compression),
    /// so one project serves every storefront.
    /// </summary>
    public static class BuildCommands
    {
        [MenuItem("Racing/Build/Yandex (WebGL)")]
        public static void BuildYandexMenu() => BuildYandex();

        public static BuildReport BuildYandex()
        {
            string output = Arg("-buildOutput") ?? "Builds/Yandex/web";
            return BuildWeb(output, "PLATFORM_YANDEX", "PROJECT:Yandex");
        }

        [MenuItem("Racing/Build/Web mock (WebGL)")]
        public static void BuildWebMockMenu() => BuildWeb(Arg("-buildOutput") ?? "Builds/WebMock/web", "", "PROJECT:Yandex");

        public static BuildReport BuildWeb(string output, string storeDefine, string template)
        {
            var target = BuildTarget.WebGL;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, target))
                throw new InvalidOperationException("WebGL module is not installed: unity install-modules -e 6000.5.9f1 -m webgl");

            PlayerSettings.WebGL.template = template;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.stripEngineCode = true;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
                extraScriptingDefines = string.IsNullOrEmpty(storeDefine) ? Array.Empty<string>() : new[] { storeDefine }
            };
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[BuildCommands] {report.summary.result} size={report.summary.totalSize / (1024 * 1024)} MB time={report.summary.totalTime}");
            WebZipPostprocessor.Process(report);
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
            return report;
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
