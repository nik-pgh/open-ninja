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
            Random.InitState("Stone".GetHashCode());
            Color baseColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            Color micaColor = new Color(0.18f, 0.18f, 0.22f, 1f);

            float xOffset = Random.value * 100f;
            float yOffset = Random.value * 100f;

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    float u = (x + xOffset);
                    float v = (y + yOffset);

                    // 4-octave Perlin noise.
                    float n = Mathf.PerlinNoise(u * 0.04f, v * 0.04f) * 0.5f
                            + Mathf.PerlinNoise(u * 0.08f, v * 0.08f) * 0.25f
                            + Mathf.PerlinNoise(u * 0.16f, v * 0.16f) * 0.125f
                            + Mathf.PerlinNoise(u * 0.32f, v * 0.32f) * 0.0625f;
                    float lum = Mathf.Lerp(0.4f, 0.7f, n);

                    Color tone = baseColor * lum;
                    tone.a = 1f;

                    // Mica flecks: rare dark spots based on independent noise.
                    float fleckNoise = Mathf.PerlinNoise(u * 0.9f + 50f, v * 0.9f + 50f);
                    if (fleckNoise > 0.93f)
                    {
                        tone = micaColor;
                        n -= 0.3f;
                    }

                    albedo[y * TexSize + x] = tone;
                    height[y * TexSize + x] = n;
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 6f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
        }

        private static void GenerateMetal(string name)
        {
            Random.InitState("Metal".GetHashCode());
            Color baseColor = new Color(0.3f, 0.3f, 0.32f, 1f);

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                // Per-row sinusoidal line.
                float line = (Mathf.Sin(y * 1.2f) * 0.5f + 0.5f) * 0.06f;

                for (int x = 0; x < TexSize; x++)
                {
                    // Tiny per-row scratch using horizontal-favoring Perlin.
                    float scratches = Mathf.PerlinNoise(x * 0.01f, y * 0.3f) * 0.04f;
                    float darkness = line + scratches;

                    Color tone = baseColor * (1f - darkness);
                    tone.a = 1f;
                    albedo[y * TexSize + x] = tone;
                    height[y * TexSize + x] = darkness;
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 2f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
        }

        private static void GenerateCrystal(string name)
        {
            Random.InitState("Crystal".GetHashCode());
            Color baseTint = new Color(0.55f, 0.85f, 1f, 1f);
            const int CellCount = 24;

            var (centers, cellMap, _) = VoronoiCells(CellCount, TexSize, seed: "Crystal".GetHashCode());

            // Per-cell color jitter so each facet feels slightly distinct.
            var cellTints = new Color[CellCount];
            var rng = new System.Random("Crystal".GetHashCode());
            for (int i = 0; i < CellCount; i++)
            {
                float dr = (float)(rng.NextDouble() * 0.1 - 0.05);
                float dg = (float)(rng.NextDouble() * 0.1 - 0.05);
                float db = (float)(rng.NextDouble() * 0.1 - 0.05);
                cellTints[i] = new Color(
                    Mathf.Clamp01(baseTint.r + dr),
                    Mathf.Clamp01(baseTint.g + dg),
                    Mathf.Clamp01(baseTint.b + db),
                    1f);
            }

            // For each pixel: find the cell, find distance to the nearest cell BOUNDARY (vs. center).
            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    int cellIdx = cellMap[y * TexSize + x];

                    // Distance to nearest center − distance to nearest non-self center.
                    float dSelf = Vector2.Distance(new Vector2(x, y), centers[cellIdx]);
                    float dOther = float.MaxValue;
                    for (int i = 0; i < CellCount; i++)
                    {
                        if (i == cellIdx) continue;
                        float d = Vector2.Distance(new Vector2(x, y), centers[i]);
                        if (d < dOther) dOther = d;
                    }
                    // Edge proximity: 0 at boundary, increases toward cell center.
                    float edgeDist = dOther - dSelf;
                    float edgeShade = Mathf.Clamp01(edgeDist / 12f);

                    // Center is slightly brighter, edges darker.
                    Color tone = cellTints[cellIdx] * Mathf.Lerp(0.85f, 1.1f, edgeShade);
                    tone.r = Mathf.Clamp01(tone.r);
                    tone.g = Mathf.Clamp01(tone.g);
                    tone.b = Mathf.Clamp01(tone.b);
                    tone.a = 1f;

                    albedo[y * TexSize + x] = tone;
                    // Height encodes edge sharpness — high in centers, drops sharply at edges.
                    height[y * TexSize + x] = Mathf.Pow(edgeShade, 0.5f);
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 10f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
        }

        private static void GenerateSpiked(string name)
        {
            Random.InitState("Spiked".GetHashCode());
            Color baseColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            Color peakColor = new Color(0.35f, 0.1f, 0.35f, 1f);
            const int CellCount = 36;

            var (centers, cellMap, _) = VoronoiCells(CellCount, TexSize, seed: "Spiked".GetHashCode());

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    int cellIdx = cellMap[y * TexSize + x];
                    float dSelf = Vector2.Distance(new Vector2(x, y), centers[cellIdx]);

                    // Estimated cell radius — distance to nearest other center / 2.
                    float dOther = float.MaxValue;
                    for (int i = 0; i < CellCount; i++)
                    {
                        if (i == cellIdx) continue;
                        float d = Vector2.Distance(centers[cellIdx], centers[i]);
                        if (d < dOther) dOther = d;
                    }
                    float radius = dOther * 0.5f;
                    float t = Mathf.Clamp01(1f - dSelf / radius); // 1 at center, 0 at edge

                    // Pyramid shading: center is lit, edges are dark.
                    Color tone = Color.Lerp(baseColor, peakColor, t * 0.7f);
                    tone.a = 1f;
                    albedo[y * TexSize + x] = tone;

                    // Height = pyramid peak; full strength at center, 0 at edge.
                    height[y * TexSize + x] = t;
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 8f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
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
