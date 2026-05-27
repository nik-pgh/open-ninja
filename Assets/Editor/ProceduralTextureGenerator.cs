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
            // Warm sandstone gray — sits opposite Metal's cool steel-blue so the
            // two materials are clearly different at a glance.
            Color baseColor = new Color(0.68f, 0.62f, 0.52f, 1f);
            Color micaColor = new Color(0.30f, 0.22f, 0.18f, 1f);

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
            // Steel-blue brushed look — distinctly cooler and brighter than Stone's
            // warm gray, so the two materials read as different alloys at a glance.
            Color baseColor   = new Color(0.62f, 0.68f, 0.78f, 1f);
            Color highlight   = new Color(0.92f, 0.94f, 1.00f, 1f);
            Color shadowTint  = new Color(0.30f, 0.36f, 0.45f, 1f);

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                // Brushed-steel band: bright horizontal ridge that varies row-to-row.
                float band = Mathf.Sin(y * 0.35f) * 0.5f + 0.5f;
                float ridge = Mathf.SmoothStep(0.45f, 0.55f, band); // sharper ridge

                for (int x = 0; x < TexSize; x++)
                {
                    // Long horizontal scratches: high-frequency noise stretched in x.
                    float scratch = Mathf.PerlinNoise(x * 0.015f, y * 0.6f) - 0.5f;
                    // Tiny per-pixel jitter so it doesn't band visibly.
                    float jitter = (Mathf.PerlinNoise(x * 0.9f, y * 0.9f) - 0.5f) * 0.08f;

                    float lightness = ridge * 0.45f + scratch * 0.25f + jitter;

                    Color tone;
                    if (lightness >= 0f)
                        tone = Color.Lerp(baseColor, highlight, Mathf.Clamp01(lightness));
                    else
                        tone = Color.Lerp(baseColor, shadowTint, Mathf.Clamp01(-lightness));
                    tone.a = 1f;
                    albedo[y * TexSize + x] = tone;
                    height[y * TexSize + x] = lightness * 0.5f + 0.5f;
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 4f);
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
            // Dark backplate, steel-bright pyramid tips, with a danger-red wash
            // around each pyramid base so the cube reads as menacing on sight.
            Color plateColor  = new Color(0.20f, 0.18f, 0.22f, 1f);
            Color baseColor   = new Color(0.45f, 0.10f, 0.10f, 1f); // ring around each spike
            Color midColor    = new Color(0.65f, 0.62f, 0.65f, 1f);
            Color peakColor   = new Color(1.00f, 0.97f, 0.92f, 1f);

            // 4 large iconic pyramids per face — a grid layout reads as
            // intentional studs rather than a noisy random-dot pattern.
            const int Cols = 4;
            const int Rows = 4;
            float cellW = TexSize / (float)Cols;
            float cellH = TexSize / (float)Rows;
            // Pyramid base ~ 90% of cell size; tip is sharp.
            float baseRadius = Mathf.Min(cellW, cellH) * 0.45f;

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    // Nearest pyramid center for this pixel.
                    float gx = (Mathf.Floor(x / cellW) + 0.5f) * cellW;
                    float gy = (Mathf.Floor(y / cellH) + 0.5f) * cellH;
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(gx, gy));

                    // 0 outside pyramid base, 1 at tip.
                    float t = Mathf.Clamp01(1f - d / baseRadius);
                    // Sharp cone — pushes most of the gradient into the central spike.
                    float coneT = Mathf.Pow(t, 3f);

                    Color tone;
                    if (t <= 0f)
                    {
                        tone = plateColor;
                    }
                    else if (t < 0.25f)
                    {
                        // Red ring near the base (close to plate edge).
                        tone = Color.Lerp(plateColor, baseColor, t / 0.25f);
                    }
                    else if (coneT < 0.55f)
                    {
                        tone = Color.Lerp(baseColor, midColor, (coneT - 0f) / 0.55f);
                    }
                    else
                    {
                        tone = Color.Lerp(midColor, peakColor, (coneT - 0.55f) / 0.45f);
                    }
                    tone.a = 1f;
                    albedo[y * TexSize + x] = tone;
                    height[y * TexSize + x] = coneT;
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            // Strong normal so the pyramid tips catch lighting and read as 3D.
            var normalPixels = HeightToNormal(height, TexSize, strength: 22f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
        }

        private static void GenerateRubber(string name)
        {
            Random.InitState("Rubber".GetHashCode());
            Color baseColor = new Color(1f, 0.9f, 0.2f, 1f);
            const int DimpleGrid = 8;
            float gridStep = TexSize / (float)DimpleGrid;
            float dimpleRadius = gridStep * 0.45f;

            var height = new float[TexSize * TexSize];
            var albedo = new Color[TexSize * TexSize];

            for (int y = 0; y < TexSize; y++)
            {
                for (int x = 0; x < TexSize; x++)
                {
                    // Slight per-pixel color variation.
                    float n = Mathf.PerlinNoise(x * 0.03f, y * 0.03f);
                    Color tone = baseColor * Mathf.Lerp(0.95f, 1.05f, n);
                    tone.r = Mathf.Clamp01(tone.r);
                    tone.g = Mathf.Clamp01(tone.g);
                    tone.b = Mathf.Clamp01(tone.b);
                    tone.a = 1f;
                    albedo[y * TexSize + x] = tone;

                    // Find nearest grid center.
                    float gx = Mathf.Round(x / gridStep) * gridStep;
                    float gy = Mathf.Round(y / gridStep) * gridStep;
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(gx, gy));

                    // Smooth depression: 0 at radius, depth at center.
                    float depth = Mathf.Clamp01(1f - d / dimpleRadius);
                    // Quadratic falloff for a bowl shape.
                    height[y * TexSize + x] = -depth * depth * 0.4f; // negative → indented
                }
            }

            SaveTexture(albedo, $"{OutputDir}/{name}_Albedo.png", isNormalMap: false);
            var normalPixels = HeightToNormal(height, TexSize, strength: 4f);
            SaveTexture(normalPixels, $"{OutputDir}/{name}_Normal.png", isNormalMap: true);
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
