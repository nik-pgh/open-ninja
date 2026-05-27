using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// Debug helper: dumps the Caveat SDF atlas texture + glyph table to disk so
    /// we can eyeball whether the bake is the source of garbled text.
    /// </summary>
    public static class CaveatAtlasInspector
    {
        public static string Execute()
        {
            var sdf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
            if (sdf == null) return "no SDF asset found";

            var sb = new StringBuilder();
            sb.AppendLine($"atlas count: {sdf.atlasTextures?.Length ?? 0}");
            sb.AppendLine($"glyph count: {sdf.glyphTable?.Count ?? 0}");
            sb.AppendLine($"char count: {sdf.characterTable?.Count ?? 0}");
            sb.AppendLine($"face point size: {sdf.faceInfo.pointSize}");
            sb.AppendLine($"face scale: {sdf.faceInfo.scale}");
            sb.AppendLine($"face ascent: {sdf.faceInfo.ascentLine}");
            sb.AppendLine($"atlas width/height: {sdf.atlasWidth}/{sdf.atlasHeight}");
            sb.AppendLine($"atlas render mode: {sdf.atlasRenderMode}");

            if (sdf.atlasTextures != null && sdf.atlasTextures.Length > 0)
            {
                var atlas = sdf.atlasTextures[0];
                if (atlas != null)
                {
                    sb.AppendLine($"atlas[0] format: {atlas.format}, size: {atlas.width}x{atlas.height}");
                    sb.AppendLine($"atlas[0] readable: {atlas.isReadable}");

                    // Sum some pixels to check if there's any non-zero data
                    if (atlas.isReadable)
                    {
                        var pixels = atlas.GetPixels32();
                        long sumR = 0, sumA = 0, nonZero = 0;
                        int total = pixels.Length;
                        for (int i = 0; i < total; i++)
                        {
                            sumR += pixels[i].r;
                            sumA += pixels[i].a;
                            if (pixels[i].r > 0 || pixels[i].a > 0) nonZero++;
                        }
                        sb.AppendLine($"FULL SCAN: sumR={sumR} sumA={sumA} nonZeroPixels={nonZero}/{total}");
                    }

                    var rt = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32);
                    var prev = RenderTexture.active;
                    Graphics.Blit(atlas, rt);
                    RenderTexture.active = rt;
                    var readable = new Texture2D(atlas.width, atlas.height, TextureFormat.RGBA32, false);
                    readable.ReadPixels(new Rect(0, 0, atlas.width, atlas.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);

                    // Also probe pixel data after blit/read
                    var blitPixels = readable.GetPixels32();
                    long blitNonZero = 0;
                    for (int i = 0; i < blitPixels.Length; i += 1024)
                    {
                        if (blitPixels[i].r > 0 || blitPixels[i].g > 0 || blitPixels[i].b > 0 || blitPixels[i].a > 0)
                            blitNonZero++;
                    }
                    sb.AppendLine($"blit nonZeroSamples={blitNonZero}");

                    string outPath = "Assets/Fonts/_CaveatAtlasDump.png";
                    var png = readable.EncodeToPNG();
                    File.WriteAllBytes(outPath, png);
                    Object.DestroyImmediate(readable);
                    AssetDatabase.ImportAsset(outPath);
                    sb.AppendLine($"atlas[0] dumped to {outPath} ({png?.Length ?? 0} bytes)");
                }
                else
                {
                    sb.AppendLine("atlas[0] is null");
                }
            }

            // Sample a few characters
            if (sdf.characterTable != null)
            {
                int sampled = 0;
                foreach (var c in sdf.characterTable)
                {
                    if (sampled >= 8) break;
                    sb.AppendLine($"  '{(char)c.unicode}' (U+{c.unicode:X4}) glyphIdx={c.glyphIndex} scale={c.scale}");
                    sampled++;
                }
            }

            Debug.Log(sb.ToString());
            return sb.ToString();
        }
    }
}
