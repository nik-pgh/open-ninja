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
        // Static Caveat-Bold.ttf from Google's fonts CDN (resolved once via the
        // Google Fonts CSS API — `https://fonts.googleapis.com/css2?family=Caveat:wght@700`
        // tells you the actual gstatic URL). The variable Caveat[wght].ttf produces
        // mangled glyphs when TMP bakes an SDF, so we deliberately use the static
        // bold weight. The file is committed to Assets/Fonts/; this URL is the
        // recovery path if it ever needs re-fetching.
        private const string CaveatUrl =
            "https://fonts.gstatic.com/s/caveat/v23/WnznHAc5bAfYB2QRah7pcpNvOx-pjRV6SII.ttf";

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

            // SDF bake params chosen for legibility of Caveat at 36–144pt display:
            //   samplingPointSize 128 — must be ≥ largest display size or the
            //     SDF gradient can't stretch far enough and glyphs render as
            //     solid silhouettes
            //   atlasPadding 20      — Caveat has long horizontal swash tails;
            //     padding also widens the alpha gradient on each glyph
            //   2048×2048 atlas      — needed to fit the full preload set at
            //     the higher sample size
            //   multi-atlas off      — single page avoids page-selection bugs
            //   dynamic population   — required so TryAddCharacters can fill
            //     the atlas
            const int samplingPointSize = 128;
            const int atlasPadding      = 20;
            const int atlasSize         = 2048;

            var sdf = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: samplingPointSize,
                atlasPadding: atlasPadding,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: atlasSize,
                atlasHeight: atlasSize,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: false);

            AssetDatabase.CreateAsset(sdf, LabNotebookTheme.FontAssetPath);

            // The atlas Texture2D that CreateFontAsset allocated lives only in
            // memory; we have to persist it as a sub-asset BEFORE rasterising
            // glyphs into it, otherwise SaveAssets discards the freshly-baked
            // pixel data along with the unsaved texture.
            var atlasTex = sdf.atlasTexture;
            if (atlasTex != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlasTex)))
            {
                atlasTex.name = $"{sdf.name} Atlas";
                AssetDatabase.AddObjectToAsset(atlasTex, sdf);
            }

            // TMP_FontAsset.CreateFontAsset does NOT create a default material —
            // without one, TMP can't sample the SDF atlas and renders garbage at
            // runtime. Build the material manually and store it as a sub-asset.
            var distanceFieldShader = Shader.Find("TextMeshPro/Distance Field");
            var mat = new Material(distanceFieldShader) { name = sdf.name + " Material" };
            mat.SetTexture(ShaderUtilities.ID_MainTex, sdf.atlasTexture);
            mat.SetFloat(ShaderUtilities.ID_TextureWidth, sdf.atlasWidth);
            mat.SetFloat(ShaderUtilities.ID_TextureHeight, sdf.atlasHeight);
            mat.SetFloat(ShaderUtilities.ID_GradientScale, sdf.atlasPadding + 1);
            mat.SetFloat(ShaderUtilities.ID_WeightNormal, sdf.normalStyle);
            mat.SetFloat(ShaderUtilities.ID_WeightBold,   sdf.boldStyle);
            sdf.material = mat;
            AssetDatabase.AddObjectToAsset(mat, sdf);

            // Pre-populate every character we'll display via TMP's high-level
            // API. TryAddCharacters allocates glyph rects AND rasterises the
            // SDF pixels into the atlas — provided the atlas texture is
            // already saved as a sub-asset (which we did above).
            const string preload =
                " !\"#$%&'()*+,-./0123456789:;<=>?@" +
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                "abcdefghijklmnopqrstuvwxyz" +
                "[\\]^_`{|}~" +
                "★↗↓—♥−×";
            sdf.TryAddCharacters(preload, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"Caveat SDF: missing glyphs for '{missing}'");

            // Force the atlas texture to commit its pixel data and persist with
            // the asset. Without Apply() the rasterised SDF may live only in
            // the GPU-side copy.
            if (sdf.atlasTexture != null) sdf.atlasTexture.Apply(false, false);

            EditorUtility.SetDirty(sdf);
            EditorUtility.SetDirty(mat);
            if (sdf.atlasTexture != null) EditorUtility.SetDirty(sdf.atlasTexture);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(LabNotebookTheme.FontAssetPath);
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
