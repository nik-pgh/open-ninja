using System.IO;
using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// Generates the 12 procedural textures (6 albedo + 6 normal) for the
    /// material cubes. Re-runnable: changes to parameters take effect on
    /// the next GenerateAll() call. The 6 _Normal.png files are imported
    /// with TextureType.NormalMap.
    /// </summary>
    public static class ProceduralTextureGenerator
    {
        public const int TexSize = 256;
        public const string OutputDir = "Assets/Textures";

        public static string GenerateAll()
        {
            EnsureFolder(OutputDir);
            GenerateWood("Wood");
            GenerateStone("Stone");
            GenerateMetal("Metal");
            GenerateCrystal("Crystal");
            GenerateSpiked("Spiked");
            GenerateRubber("Rubber");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"Generated 12 textures (6 albedo + 6 normal) at {OutputDir}";
        }

        // ---- Per-material generators (stubs for Task 1; real algorithms in later tasks) ----

        private static void GenerateWood(string name)
        {
            Random.InitState("Wood".GetHashCode());
            Color baseColor = new Color(0.55f, 0.35f, 0.2f, 1f);
            Color darkAccent = new Color(0.3f, 0.18f, 0.08f, 1f);

            // Knot spots — rare; ~3 cells per 256 px texture.
            var (knots, _, knotDist) = VoronoiCells(3, TexSize, seed: 7341);
            const float knotRadius = 18f;

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            // Random per-texture offset so grain doesn't always start at the same x.
            float xOffset = Random.value * 100f;

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    // Vertical grain bands: two-octave Perlin along x with a small y-variation
                    // so the bands aren't perfectly straight.
                    float u = (x + xOffset) * 0.04f;
                    float v = y * 0.005f;
                    float band = Mathf.PerlinNoise(u, v) * 0.7f
                               + Mathf.PerlinNoise(u * 3f, v) * 0.3f;
                    float darkness = Mathf.Pow(band, 2f);

                    // Tiny per-pixel noise to break tiling.
                    float dither = (Mathf.PerlinNoise(x * 0.6f, y * 0.6f) - 0.5f) * 0.04f;

                    // Knot influence: if any knot's distance is within radius, push toward darkAccent.
                    float knotMix = 0f;
                    float d = knotDist[y * TexSize + x];
                    if (d < knotRadius) knotMix = Mathf.SmoothStep(1f, 0f, d / knotRadius);

                    Color baseTone = Color.Lerp(baseColor, darkAccent, darkness * 0.55f + dither);
                    albedo[y * TexSize + x] = Color.Lerp(baseTone, darkAccent, knotMix * 0.85f);
                    height[y * TexSize + x] = darkness + knotMix * 0.4f;
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 8f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
        }

        private static void GenerateStone(string name)
        {
            SaveStubPair(name, new Color(0.55f, 0.55f, 0.55f, 1f));
        }

        private static void GenerateMetal(string name)
        {
            SaveStubPair(name, new Color(0.3f, 0.3f, 0.32f, 1f));
        }

        private static void GenerateCrystal(string name)
        {
            SaveStubPair(name, new Color(0.55f, 0.85f, 1f, 1f));
        }

        private static void GenerateSpiked(string name)
        {
            SaveStubPair(name, new Color(0.1f, 0.1f, 0.1f, 1f));
        }

        private static void GenerateRubber(string name)
        {
            SaveStubPair(name, new Color(1f, 0.9f, 0.2f, 1f));
        }

        // ---- Stub helper: solid albedo + flat normal ----

        private static void SaveStubPair(string name, Color albedo)
        {
            var albedoPixels = new Color[TexSize * TexSize];
            for (int i = 0; i < albedoPixels.Length; i++) albedoPixels[i] = albedo;
            SaveTexture(albedoPixels, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);

            // Flat normal = (0, 0, 1) → (0.5, 0.5, 1.0) in [0,1] space.
            var normalPixels = new Color[TexSize * TexSize];
            var flat = new Color(0.5f, 0.5f, 1f, 1f);
            for (int i = 0; i < normalPixels.Length; i++) normalPixels[i] = flat;
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
        }

        // ---- Shared helpers ----

        public static void SaveTexture(Color[] pixels, string path, bool isNormalMap)
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, mipChain: false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (isNormalMap)
            {
                // We baked the normal directly; don't have Unity re-derive from height.
                importer.convertToNormalmap = false;
            }
            importer.maxTextureSize = TexSize;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Converts a height field (size×size, values in [0,1]) into a tangent-space
        /// normal map via a Sobel filter. Returns Color[] suitable for SetPixels.
        /// Strength scales the gradient before encoding.
        /// </summary>
        public static Color[] HeightToNormal(float[] height, int size, float strength)
        {
            var result = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Wrap-sample neighbors so the texture tiles cleanly.
                    int xm = (x - 1 + size) % size;
                    int xp = (x + 1) % size;
                    int ym = (y - 1 + size) % size;
                    int yp = (y + 1) % size;

                    float gx = (height[y * size + xp] - height[y * size + xm]) * strength;
                    float gy = (height[yp * size + x] - height[ym * size + x]) * strength;

                    // Tangent-space normal: (-gx, -gy, 1) normalized, mapped to [0,1].
                    Vector3 n = new Vector3(-gx, -gy, 1f).normalized;
                    result[y * size + x] = new Color(
                        n.x * 0.5f + 0.5f,
                        n.y * 0.5f + 0.5f,
                        n.z * 0.5f + 0.5f,
                        1f);
                }
            }
            return result;
        }

        /// <summary>
        /// Generates `cellCount` Voronoi cell centers seeded by `seed`. Returns the
        /// centers (positions in [0,size)) and a per-pixel `int[]` mapping each pixel
        /// to the index of its nearest center. Pixel index = y*size + x.
        /// </summary>
        public static (Vector2[] centers, int[] cellMap, float[] distanceMap) VoronoiCells(
            int cellCount, int size, int seed)
        {
            var rng = new System.Random(seed);
            var centers = new Vector2[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                centers[i] = new Vector2(
                    (float)(rng.NextDouble() * size),
                    (float)(rng.NextDouble() * size));
            }

            var cellMap = new int[size * size];
            var distanceMap = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float bestDistSq = float.MaxValue;
                    int bestIdx = 0;
                    for (int i = 0; i < cellCount; i++)
                    {
                        float dx = x - centers[i].x;
                        float dy = y - centers[i].y;
                        float d = dx * dx + dy * dy;
                        if (d < bestDistSq) { bestDistSq = d; bestIdx = i; }
                    }
                    cellMap[y * size + x] = bestIdx;
                    distanceMap[y * size + x] = Mathf.Sqrt(bestDistSq);
                }
            }
            return (centers, cellMap, distanceMap);
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
