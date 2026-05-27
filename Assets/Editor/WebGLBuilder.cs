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

        private static void CopyOver(string src, string dst)
        {
            if (!File.Exists(src))
            {
                Debug.LogWarning($"WebGLBuilder: missing {src}, skipping overlay");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, overwrite: true);
        }

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

            // ---- Overlay custom landing page + API + Vercel config ----
            // Unity wipes Build/WebGL on each build, so anything we want to ship
            // alongside the WebGL output has to be copied back in from the
            // checked-in web/ source folder.
            string webSrc = "web";
            if (Directory.Exists(webSrc))
            {
                // 1) Replace Unity's default index.html with our custom one;
                //    bring along the OG image so social link previews work.
                CopyOver(Path.Combine(webSrc, "index.html"),    Path.Combine(OutputDir, "index.html"));
                CopyOver(Path.Combine(webSrc, "vercel.json"),   Path.Combine(OutputDir, "vercel.json"));
                CopyOver(Path.Combine(webSrc, ".vercelignore"), Path.Combine(OutputDir, ".vercelignore"));
                CopyOver(Path.Combine(webSrc, "og.png"),        Path.Combine(OutputDir, "og.png"));

                // 2) Drop Unity's TemplateData/ (logo, default favicon, etc).
                string templateData = Path.Combine(OutputDir, "TemplateData");
                if (Directory.Exists(templateData)) Directory.Delete(templateData, true);

                // 3) Copy serverless functions (api/) into the deploy root.
                string apiSrc = Path.Combine(webSrc, "api");
                string apiDst = Path.Combine(OutputDir, "api");
                if (Directory.Exists(apiSrc))
                {
                    if (Directory.Exists(apiDst)) Directory.Delete(apiDst, true);
                    Directory.CreateDirectory(apiDst);
                    foreach (var f in Directory.GetFiles(apiSrc))
                        File.Copy(f, Path.Combine(apiDst, Path.GetFileName(f)), overwrite: true);
                }

                Debug.Log("WebGLBuilder: overlaid web/ → Build/WebGL/");
            }
            else
            {
                Debug.LogWarning("WebGLBuilder: web/ source folder missing; default Unity output will deploy as-is.");
            }
        }
    }
}
#endif
