using System.Collections;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TextCore.LowLevel;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// One-shot Editor utility that ensures the lab-notebook assets exist:
    ///   1. Caveat-Bold.ttf in Assets/Fonts/ (fetched from Google Fonts on first run)
    ///   2. Caveat-Bold SDF.asset (TMP Font Asset generated from the TTF)
    ///   3. GraphPaper.png in Assets/Textures/ (procedurally generated)
    /// Idempotent: re-running is a no-op once assets exist.
    /// </summary>
    public static class StartScreenAssetsSetup
    {
        // Stable mirror of Google Fonts OFL repo.
        private const string CaveatUrl =
            "https://github.com/google/fonts/raw/main/ofl/caveat/Caveat%5Bwght%5D.ttf";

        private const string FontsDir    = "Assets/Fonts";
        private const string TexturesDir = "Assets/Textures";
        private const int    GraphSize   = 512;
        private const int    GraphStep   = 16;

        public static string Execute()
        {
            EnsureFolder(FontsDir);
            EnsureFolder(TexturesDir);

            string fontStatus = EnsureCaveatTtf();
            string sdfStatus  = EnsureCaveatSdf();
            string pngStatus  = EnsureGraphPaperPng();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"{fontStatus} | {sdfStatus} | {pngStatus}";
        }

        // ---- Caveat TTF ----

        private static string EnsureCaveatTtf()
        {
            if (File.Exists(LabNotebookTheme.CaveatTtfPath))
                return "ttf: already present";

            using var req = UnityWebRequest.Get(CaveatUrl);
            var op = req.SendWebRequest();
            while (!op.isDone)
                System.Threading.Thread.Sleep(50);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Caveat font download failed: {req.error}. " +
                               "Lab-notebook theme will fall back to LiberationSans.");
                return "ttf: download FAILED (fallback to LiberationSans)";
            }

            File.WriteAllBytes(LabNotebookTheme.CaveatTtfPath, req.downloadHandler.data);
            AssetDatabase.ImportAsset(LabNotebookTheme.CaveatTtfPath);
            return "ttf: downloaded";
        }

        // ---- Caveat SDF TMP Font Asset ----

        private static string EnsureCaveatSdf()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
            if (existing != null) return "sdf: already present";

            var font = AssetDatabase.LoadAssetAtPath<Font>(LabNotebookTheme.CaveatTtfPath);
            if (font == null)
            {
                Debug.LogError("Caveat TTF missing; cannot build SDF asset.");
                return "sdf: skipped (no ttf)";
            }

            var sdf = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            AssetDatabase.CreateAsset(sdf, LabNotebookTheme.FontAssetPath);
            sdf.TryAddCharacters("★↗—♥−", out _);
            EditorUtility.SetDirty(sdf);
            return "sdf: generated";
        }

        // ---- GraphPaper.png ----

        private static string EnsureGraphPaperPng()
        {
            if (File.Exists(LabNotebookTheme.GraphPaperSpritePath))
                return "png: already present";

            var pixels = new Color[GraphSize * GraphSize];
            Color paper = LabNotebookTheme.PaperCream;
            Color grid  = LabNotebookTheme.GridBlue;
            for (int y = 0; y < GraphSize; y++)
            {
                for (int x = 0; x < GraphSize; x++)
                {
                    bool isGridLine = (x % GraphStep == 0) || (y % GraphStep == 0);
                    pixels[y * GraphSize + x] = isGridLine ? Blend(paper, grid) : paper;
                }
            }

            var tex = new Texture2D(GraphSize, GraphSize, TextureFormat.RGBA32, mipChain: false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(LabNotebookTheme.GraphPaperSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(LabNotebookTheme.GraphPaperSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(LabNotebookTheme.GraphPaperSpritePath);
            if (importer != null)
            {
                importer.textureType   = TextureImporterType.Sprite;
                importer.wrapMode      = TextureWrapMode.Repeat;
                importer.filterMode    = FilterMode.Point;
                importer.maxTextureSize = GraphSize;
                importer.mipmapEnabled = false;
                importer.npotScale     = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }
            return "png: generated";
        }

        private static Color Blend(Color baseColor, Color overlay)
        {
            float a = overlay.a;
            return new Color(
                baseColor.r * (1 - a) + overlay.r * a,
                baseColor.g * (1 - a) + overlay.g * a,
                baseColor.b * (1 - a) + overlay.b * a,
                1f);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
