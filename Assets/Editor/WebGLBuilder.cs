#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenNinja.EditorBuild
{
    /// <summary>
    /// One-call WebGL builder. Invoked via coplay-mcp execute_script — also
    /// runnable from the menu Tools → Build WebGL for manual triggers.
    /// Output goes to Build/WebGL/ at the repo root (sibling of Assets/).
    /// </summary>
    public static class WebGLBuilder
    {
        public const string OutputDir = "Build/WebGL";

        [MenuItem("Tools/Build WebGL")]
        public static void Execute()
        {
            // ---- Configure WebGL player settings for a small static deploy ----
            // Brotli > Gzip for first-load size, but requires the host to send the
            // right Content-Encoding header. Vercel handles this for .br files
            // when the request includes Accept-Encoding: br (default in browsers).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.SetIl2CppCompilerConfiguration(
                NamedBuildTarget.WebGL, Il2CppCompilerConfiguration.Master);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);

            // Branding / window
            PlayerSettings.productName = "Material Ninja";
            PlayerSettings.companyName = "open-ninja";

            // Ensure the two scenes are in build settings in the right order.
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/StartScene.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainScene.unity", true),
            };
            EditorBuildSettings.scenes = scenes;

            // Clean output dir so stale .br files don't linger.
            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);
            Directory.CreateDirectory(OutputDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/Scenes/StartScene.unity",
                    "Assets/Scenes/MainScene.unity",
                },
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            Debug.Log($"WebGLBuilder: starting build → {OutputDir}");
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log(
                $"WebGLBuilder: result={summary.result}, " +
                $"size={summary.totalSize / (1024 * 1024)} MB, " +
                $"duration={summary.totalTime.TotalSeconds:F1}s, " +
                $"errors={summary.totalErrors}, warnings={summary.totalWarnings}");

            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"WebGL build failed: {summary.result}");
        }
    }
}
#endif
