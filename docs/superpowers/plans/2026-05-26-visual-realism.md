# Visual Realism Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the six material cubes from solid colored blocks to surfaces with real texture (procedural wood grain, stone speckle, brushed metal, crystal facets, spiked pyramids, rubber dimples), and give the scene a proper lighting environment (procedural skybox + tuned directional light + baked reflection probe) so PBR has reflections to sample.

**Architecture:** A new Editor script `ProceduralTextureGenerator` generates 12 PNGs (6 albedo + 6 normal) into `Assets/Textures/`. `MaterialAssetsSetup` wires those textures into the existing 6 materials and tunes per-material smoothness/metallic/normal-strength. `SceneSetup` adds a procedural skybox, tunes the Directional Light, adds a ReflectionProbe, and bakes it. Pure visual upgrade — no runtime code changes.

**Tech Stack:** Unity 6, C#, Built-in Render Pipeline (Standard shader), `coplay-mcp` for executing Editor scripts and visually verifying outputs.

**Spec:** `docs/superpowers/specs/2026-05-26-visual-realism-design.md`

---

## File Structure

```
NEW   Assets/Editor/ProceduralTextureGenerator.cs   ~300 lines: helpers + 6 per-material generators
NEW   Assets/Textures/Wood_Albedo.png               256×256 RGBA32
NEW   Assets/Textures/Wood_Normal.png               256×256 NormalMap import
NEW   Assets/Textures/Stone_Albedo.png
NEW   Assets/Textures/Stone_Normal.png
NEW   Assets/Textures/Metal_Albedo.png
NEW   Assets/Textures/Metal_Normal.png
NEW   Assets/Textures/Crystal_Albedo.png
NEW   Assets/Textures/Crystal_Normal.png
NEW   Assets/Textures/Spiked_Albedo.png
NEW   Assets/Textures/Spiked_Normal.png
NEW   Assets/Textures/Rubber_Albedo.png
NEW   Assets/Textures/Rubber_Normal.png
NEW   Assets/Materials/ProceduralSky.mat            procedural skybox material
NEW   Assets/Scenes/MainScene/ReflectionProbe-0.exr baked reflection probe data
MOD   Assets/Editor/MaterialAssetsSetup.cs          wire _MainTex/_BumpMap + _BumpScale, per-material PBR values
MOD   Assets/Editor/SceneSetup.cs                   create skybox, tune light, add ReflectionProbe, bake
```

Each per-material generator is a self-contained method on `ProceduralTextureGenerator`. The class also holds three shared helpers (`SaveTexture`, `HeightToNormal`, `VoronoiCells`).

---

## Conventions

- Working directory: `/Users/nikkim/dev/open-ninja/.claude/worktrees/material-cubes`. Use the existing worktree.
- C# namespace `OpenNinja.EditorSetup` for Editor scripts.
- Unity Editor must be open with the Coplay MCP plugin connected throughout this plan. Verify via `mcp__coplay-mcp__get_unity_editor_state` before each Editor-driven task.
- "Run generator" = `mcp__coplay-mcp__execute_script` with `filePath` = `Assets/Editor/ProceduralTextureGenerator.cs` and `methodName` = `GenerateAll`.
- "View texture" = use the `Read` tool on the PNG file path; Unity-generated PNGs are valid images and the tool renders them inline so the implementer (and reviewer) can see whether the algorithm produced what was expected.
- No automated tests for this work — verification is visual. Each per-material task ends with a `Read` of the generated PNG and a one-line subjective check ("does this look like wood grain?").
- After every code change, call `mcp__coplay-mcp__check_compile_errors` if Unity is open; expect `No compile errors`.
- Standard shader property names (Unity 6): `_MainTex` (albedo), `_BumpMap` (normal), `_BumpScale` (normal strength), `_Metallic`, `_Glossiness`.

---

## Task 1: `ProceduralTextureGenerator` skeleton + shared helpers

**Goal:** Stand up the new class with `GenerateAll()` that produces stub solid-color textures for all 6 materials. Validates the file/import pipeline before any real algorithms run.

**Files:**
- Create: `Assets/Editor/ProceduralTextureGenerator.cs`

- [ ] **Step 1: Write the skeleton**

Write `Assets/Editor/ProceduralTextureGenerator.cs`:

```csharp
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
            SaveStubPair(name, new Color(0.55f, 0.35f, 0.2f, 1f));
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
```

- [ ] **Step 2: Verify compile clean**

Call `mcp__coplay-mcp__check_compile_errors`. Expected: `No compile errors`.

- [ ] **Step 3: Run the generator**

Call `mcp__coplay-mcp__execute_script` with:
- `filePath`: `Assets/Editor/ProceduralTextureGenerator.cs`
- `methodName`: `GenerateAll`

Expected result: `Generated 12 textures (6 albedo + 6 normal) at Assets/Textures`.

- [ ] **Step 4: Verify outputs**

```bash
ls Assets/Textures/
```

Expected: 12 PNG files (`Wood_Albedo.png`, `Wood_Normal.png`, ..., `Rubber_Normal.png`).

- [ ] **Step 5: Spot-check one normal map import setting**

Read the `.meta` file for one normal PNG:
```bash
grep -A2 "textureType:" Assets/Textures/Wood_Normal.png.meta
```
Expected: `textureType: 1` (Unity's enum value for `TextureImporterType.NormalMap`).

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures
git commit -m "feat(textures): ProceduralTextureGenerator skeleton + 12 stub PNGs"
```

---

## Task 2: Wood — vertical grain bands + Sobel normal

**Goal:** Replace the `GenerateWood` stub with the real algorithm: vertical grain stripes from Perlin noise, optional darker knot spots from rare Voronoi cells.

**Files:**
- Modify: `Assets/Editor/ProceduralTextureGenerator.cs` (the `GenerateWood` method only)

- [ ] **Step 1: Replace `GenerateWood`**

In `Assets/Editor/ProceduralTextureGenerator.cs`, find:

```csharp
private static void GenerateWood(string name)
{
    SaveStubPair(name, new Color(0.55f, 0.35f, 0.2f, 1f));
}
```

Replace with:

```csharp
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → expected `No compile errors`.

- [ ] **Step 3: Run the generator**

`mcp__coplay-mcp__execute_script` → `ProceduralTextureGenerator.GenerateAll`. Expected: same success message.

- [ ] **Step 4: View the wood textures**

Use the `Read` tool on:
- `Assets/Textures/Wood_Albedo.png`
- `Assets/Textures/Wood_Normal.png`

Verify subjectively: the albedo shows brown vertical grain stripes (varying intensity left-to-right), possibly one or two darker knots; the normal looks predominantly neutral (purple-blue) with subtle vertical ridge patterns where the grain bands are.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures/Wood_Albedo.png Assets/Textures/Wood_Normal.png
git commit -m "feat(textures): wood — vertical grain bands + knot spots"
```

---

## Task 3: Stone — uniform speckle + mica flecks

**Goal:** Stone albedo is 4-octave Perlin noise mapped to a gray luminance, plus rare dark mica flecks. Normal is derived from the same noise field.

**Files:**
- Modify: `Assets/Editor/ProceduralTextureGenerator.cs` (the `GenerateStone` method only)

- [ ] **Step 1: Replace `GenerateStone`**

```csharp
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Run the generator**

`mcp__coplay-mcp__execute_script` → `GenerateAll`.

- [ ] **Step 4: View the stone textures**

`Read` `Assets/Textures/Stone_Albedo.png` and `Assets/Textures/Stone_Normal.png`. Verify: gray field with visible non-uniform speckle pattern; small darker fleck dots scattered; normal map shows fine-grained noise.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures/Stone_Albedo.png Assets/Textures/Stone_Normal.png
git commit -m "feat(textures): stone — 4-octave noise speckle + mica flecks"
```

---

## Task 4: Metal — brushed horizontal lines

**Goal:** Metal albedo shows subtle horizontal brush lines (sinusoid + Perlin noise scratches). Normal is very subtle horizontal grooves.

**Files:**
- Modify: `Assets/Editor/ProceduralTextureGenerator.cs` (the `GenerateMetal` method only)

- [ ] **Step 1: Replace `GenerateMetal`**

```csharp
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Run the generator**

`mcp__coplay-mcp__execute_script` → `GenerateAll`.

- [ ] **Step 4: View the metal textures**

`Read` `Assets/Textures/Metal_Albedo.png` and `Assets/Textures/Metal_Normal.png`. Verify: dark gray field with very faint horizontal lines visible if you squint; normal map almost neutral with horizontal-line bias.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures/Metal_Albedo.png Assets/Textures/Metal_Normal.png
git commit -m "feat(textures): metal — brushed horizontal lines"
```

---

## Task 5: Crystal — Voronoi facets

**Goal:** Crystal albedo is a 24-cell Voronoi diagram with each cell tinted slightly differently and shaded center-to-edge. Normal shows sharp gradients at cell edges.

**Files:**
- Modify: `Assets/Editor/ProceduralTextureGenerator.cs` (the `GenerateCrystal` method only)

- [ ] **Step 1: Replace `GenerateCrystal`**

```csharp
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Run the generator**

`mcp__coplay-mcp__execute_script` → `GenerateAll`.

- [ ] **Step 4: View the crystal textures**

`Read` `Assets/Textures/Crystal_Albedo.png` and `Assets/Textures/Crystal_Normal.png`. Verify: cyan/light-blue field divided into ~24 polygonal facets with distinct boundaries; each facet has a slight color shift; normal map shows colored regions matching the facet boundaries (edge normals are not flat purple).

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures/Crystal_Albedo.png Assets/Textures/Crystal_Normal.png
git commit -m "feat(textures): crystal — Voronoi facet pattern with per-cell tint jitter"
```

---

## Task 6: Spiked — Voronoi pyramid bumps

**Goal:** Spiked albedo is a 36-cell Voronoi where each cell is shaded as a small pyramid (bright center, dark edges). Normal map encodes outward-pointing facet normals.

**Files:**
- Modify: `Assets/Editor/ProceduralTextureGenerator.cs` (the `GenerateSpiked` method only)

- [ ] **Step 1: Replace `GenerateSpiked`**

```csharp
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Run the generator**

`mcp__coplay-mcp__execute_script` → `GenerateAll`.

- [ ] **Step 4: View the spiked textures**

`Read` `Assets/Textures/Spiked_Albedo.png` and `Assets/Textures/Spiked_Normal.png`. Verify: dark field broken into small bright purple-tinted pyramidal cells; normal map shows strong gradients with each cell pointing outward.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures/Spiked_Albedo.png Assets/Textures/Spiked_Normal.png
git commit -m "feat(textures): spiked — Voronoi pyramid bumps"
```

---

## Task 7: Rubber — subtle dimple grid

**Goal:** Rubber albedo is saturated yellow with subtle Perlin variation. Normal is a regular 8×8 grid of shallow dimples.

**Files:**
- Modify: `Assets/Editor/ProceduralTextureGenerator.cs` (the `GenerateRubber` method only)

- [ ] **Step 1: Replace `GenerateRubber`**

```csharp
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Run the generator**

`mcp__coplay-mcp__execute_script` → `GenerateAll`.

- [ ] **Step 4: View the rubber textures**

`Read` `Assets/Textures/Rubber_Albedo.png` and `Assets/Textures/Rubber_Normal.png`. Verify: saturated yellow field that's mostly flat with very subtle color variation; normal map shows a regular 8×8 grid of small darker (indented) bowls.

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/ProceduralTextureGenerator.cs Assets/Textures/Rubber_Albedo.png Assets/Textures/Rubber_Normal.png
git commit -m "feat(textures): rubber — subtle yellow + dimple grid"
```

---

## Task 8: `MaterialAssetsSetup` — wire textures + per-material PBR

**Goal:** When `MaterialAssetsSetup.Execute()` runs, after creating each material it now also assigns `_MainTex` (albedo) + `_BumpMap` (normal) + per-material `_Metallic`, `_Glossiness`, `_BumpScale`.

**Files:**
- Modify: `Assets/Editor/MaterialAssetsSetup.cs`

- [ ] **Step 1: Update `MakeMaterial` to wire textures + take a `_BumpScale`**

Replace the existing `MakeMaterial` method in `Assets/Editor/MaterialAssetsSetup.cs` with:

```csharp
private static Material MakeMaterial(string name, Color color, float smoothness, float metallic, float bumpScale)
{
    string path = $"{MaterialsDir}/{name}.mat";
    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (mat == null)
    {
        mat = new Material(Shader.Find("Standard"));
        AssetDatabase.CreateAsset(mat, path);
    }
    mat.color = color;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

    // Wire procedural textures generated by ProceduralTextureGenerator.
    var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
        $"Assets/Textures/{name}_Albedo.png");
    var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
        $"Assets/Textures/{name}_Normal.png");
    if (albedoTex != null && mat.HasProperty("_MainTex"))
        mat.SetTexture("_MainTex", albedoTex);
    if (normalTex != null && mat.HasProperty("_BumpMap"))
    {
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");
        if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", bumpScale);
    }

    EditorUtility.SetDirty(mat);
    return mat;
}
```

- [ ] **Step 2: Update the six `MakeMaterial` calls in `Execute()` with new PBR values**

In `Execute()`, find the six material-creation calls:

```csharp
var woodMat    = MakeMaterial("Wood",    new Color(0.55f, 0.35f, 0.2f, 1f), 0.7f, 0f);
var stoneMat   = MakeMaterial("Stone",   new Color(0.55f, 0.55f, 0.55f, 1f), 0.5f, 0f);
var metalMat   = MakeMaterial("Metal",   new Color(0.25f, 0.25f, 0.27f, 1f), 0.85f, 1f);
var crystalMat = MakeMaterial("Crystal", new Color(0.55f, 0.85f, 1f, 1f), 0.95f, 0.3f);
var spikedMat  = MakeMaterial("Spiked",  new Color(0.1f, 0.1f, 0.1f, 1f), 0.3f, 0f);
var rubberMat  = MakeMaterial("Rubber",  new Color(1f, 0.9f, 0.2f, 1f), 0.15f, 0f);
```

Replace with the tuned PBR table from the spec (§6 / §9):

```csharp
var woodMat    = MakeMaterial("Wood",    new Color(0.55f, 0.35f, 0.2f, 1f), smoothness: 0.35f, metallic: 0.0f, bumpScale: 1.0f);
var stoneMat   = MakeMaterial("Stone",   new Color(0.55f, 0.55f, 0.55f, 1f), smoothness: 0.25f, metallic: 0.0f, bumpScale: 0.8f);
var metalMat   = MakeMaterial("Metal",   new Color(0.25f, 0.25f, 0.27f, 1f), smoothness: 0.85f, metallic: 1.0f, bumpScale: 0.3f);
var crystalMat = MakeMaterial("Crystal", new Color(0.55f, 0.85f, 1f, 1f), smoothness: 0.95f, metallic: 0.1f, bumpScale: 0.8f);
var spikedMat  = MakeMaterial("Spiked",  new Color(0.1f, 0.1f, 0.1f, 1f), smoothness: 0.4f, metallic: 0.0f, bumpScale: 1.0f);
var rubberMat  = MakeMaterial("Rubber",  new Color(1f, 0.9f, 0.2f, 1f), smoothness: 0.3f, metallic: 0.0f, bumpScale: 0.4f);
```

- [ ] **Step 3: Add `ProceduralTextureGenerator.GenerateAll()` call at the top of `Execute()`**

At the start of `MaterialAssetsSetup.Execute()`, right after the folder ensures and before the legacy-file deletion section, add:

```csharp
// Make sure procedural textures exist before we wire them onto materials.
ProceduralTextureGenerator.GenerateAll();
```

- [ ] **Step 4: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 5: Run `MaterialAssetsSetup.Execute`**

`mcp__coplay-mcp__execute_script` with:
- `filePath`: `Assets/Editor/MaterialAssetsSetup.cs`
- `methodName`: `Execute`

Expected result: same success message as before (textures get regenerated as part of the call now).

- [ ] **Step 6: Spot-check one material**

```bash
grep -E "_MainTex|_BumpMap|_BumpScale|_Glossiness|_Metallic" Assets/Materials/Wood.mat | head -20
```
Expected: lines showing `_MainTex` pointing at a Texture2D GUID, `_BumpMap` at another, `_BumpScale: 1`, `_Glossiness: 0.35`, `_Metallic: 0`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Editor/MaterialAssetsSetup.cs Assets/Materials
git commit -m "feat(materials): wire procedural textures + per-material PBR tuning"
```

---

## Task 9: `SceneSetup` — skybox + light + reflection probe

**Goal:** Modify `SceneSetup.Execute()` to create the procedural skybox material, set ambient mode to Skybox, tune the Directional Light, add and bake a ReflectionProbe, and switch the camera clear flags to Skybox.

**Files:**
- Modify: `Assets/Editor/SceneSetup.cs`

- [ ] **Step 1: Add `CreateProceduralSky` helper**

Add this static method to the `SceneSetup` class (anywhere convenient, e.g. near the other helpers like `WireEntry`):

```csharp
private static Material CreateProceduralSky()
{
    const string path = "Assets/Materials/ProceduralSky.mat";
    var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (sky == null)
    {
        sky = new Material(Shader.Find("Skybox/Procedural"));
        AssetDatabase.CreateAsset(sky, path);
    }
    sky.SetFloat("_SunDisk", 2);                                       // small sun disk
    sky.SetFloat("_AtmosphereThickness", 0.9f);                        // slight haze
    sky.SetColor("_SkyTint", new Color(0.5f, 0.7f, 0.95f, 1f));
    sky.SetColor("_GroundColor", new Color(0.2f, 0.18f, 0.16f, 1f));
    sky.SetFloat("_Exposure", 1.0f);
    EditorUtility.SetDirty(sky);
    return sky;
}
```

- [ ] **Step 2: Apply skybox + ambient at scene level**

In `SceneSetup.Execute()`, immediately after the new-scene setup (`var scene = EditorSceneManager.NewScene(...)`) and *before* the camera-positioning block, add:

```csharp
// ---- Skybox & ambient ----
var skyMat = CreateProceduralSky();
RenderSettings.skybox = skyMat;
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
DynamicGI.UpdateEnvironment();
log.Add("skybox + ambient set");
```

- [ ] **Step 3: Tune the directional light**

After the skybox block (same `Execute()` method), add:

```csharp
// ---- Tune the directional light ----
var dirLight = Object.FindFirstObjectByType<Light>();
if (dirLight != null && dirLight.type == LightType.Directional)
{
    dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    dirLight.color = new Color(1.0f, 0.95f, 0.85f);
    dirLight.intensity = 1.0f;
    dirLight.shadows = LightShadows.Soft;
    dirLight.shadowStrength = 0.6f;
    log.Add("directional light tuned");
}
```

- [ ] **Step 4: Switch the camera clear flags to Skybox**

Find the existing camera setup block in `Execute()`. The line that says:

```csharp
cam.clearFlags = CameraClearFlags.SolidColor;
```

Change to:

```csharp
cam.clearFlags = CameraClearFlags.Skybox;
```

(Leave the `cam.backgroundColor` assignment in place — harmless with Skybox clear flags.)

- [ ] **Step 5: Add the ReflectionProbe**

After the walls block in `Execute()` (where you see `log.Add("walls built");`), add:

```csharp
// ---- Reflection probe ----
var probeGO = new GameObject("ReflectionProbe");
probeGO.transform.SetParent(systems.transform, false);
probeGO.transform.position = Vector3.zero;
var probe = probeGO.AddComponent<ReflectionProbe>();
probe.size = new Vector3(25f, 20f, 10f);
probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
probe.resolution = 128;
probe.importance = 1;
probe.boxProjection = false;
log.Add("reflection probe added");
```

- [ ] **Step 6: Bake the probe after scene save**

At the bottom of `Execute()`, after the `EditorSceneManager.SaveScene(scene, scenePath)` call and the build-settings update block, add:

```csharp
// ---- Bake reflection probe ----
// Re-fetch the probe from the saved scene; the local reference may have been
// invalidated by the scene save.
var bakedProbe = Object.FindFirstObjectByType<ReflectionProbe>();
if (bakedProbe != null)
{
    string probePath = "Assets/Scenes/MainScene/ReflectionProbe-0.exr";
    var probeDir = System.IO.Path.GetDirectoryName(probePath);
    if (!AssetDatabase.IsValidFolder(probeDir))
    {
        AssetDatabase.CreateFolder(
            System.IO.Path.GetDirectoryName(probeDir),
            System.IO.Path.GetFileName(probeDir));
    }
    Lightmapping.BakeReflectionProbe(bakedProbe, probePath);
    log.Add("reflection probe baked");
}
```

- [ ] **Step 7: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 8: Run `SceneSetup.Execute`**

`mcp__coplay-mcp__execute_script` with:
- `filePath`: `Assets/Editor/SceneSetup.cs`
- `methodName`: `Execute`

Expected: log message ending with `... reflection probe baked | scene saved to Assets/Scenes/MainScene.unity`.

- [ ] **Step 9: Verify outputs**

```bash
ls Assets/Materials/ProceduralSky.mat
ls Assets/Scenes/MainScene/      # contains ReflectionProbe-0.exr
```

- [ ] **Step 10: Commit**

```bash
git add Assets/Editor/SceneSetup.cs Assets/Scenes/MainScene.unity Assets/Materials/ProceduralSky.mat Assets/Scenes/MainScene
git commit -m "feat(scene): procedural skybox, tuned light, baked reflection probe"
```

---

## Task 10: Playtest verification

**Goal:** Press Play and verify the visual upgrade landed.

This task does no code work — a manual verification gate.

- [ ] **Step 1: Verify the scene compiles and renders correctly**

Call `mcp__coplay-mcp__check_compile_errors`. Expected: `No compile errors`.

Call `mcp__coplay-mcp__get_unity_editor_state`. Confirm `hasCompilationErrors: false`.

- [ ] **Step 2: Enter Play Mode and capture a screenshot**

`mcp__coplay-mcp__play_game`, wait 2 seconds, `mcp__coplay-mcp__capture_scene_object` (or use the in-Editor Game view).

- [ ] **Step 3: Visually verify per the spec's verification checklist (§12)**

- [ ] Wood cubes show vertical grain stripes; rare cube has a knot spot.
- [ ] Stone cubes have visible random speckle, not flat gray.
- [ ] Metal cubes show subtle horizontal brushed lines + reflect their surroundings.
- [ ] Crystal cubes show clear facet boundaries; light catches the facet edges.
- [ ] Spiked cubes have small pyramid-bump pattern across each face.
- [ ] Rubber cubes are saturated yellow with subtle dimples.
- [ ] Reflection probe contributes — metal cube is not pitch black; shows reflection of its surroundings.
- [ ] Skybox visible above the play area (slight blue gradient with a small sun disk).

If any material reads as flat color despite the texture being assigned, the most likely cause is `_NORMALMAP` keyword not enabled — re-run `MaterialAssetsSetup.Execute` and check the spot-check from Task 8 Step 6.

- [ ] **Step 4: Stop play mode + commit any tuning changes**

If the visuals look right, no commit needed. If any per-material parameter needs tweaking (`bumpScale`, smoothness, color), edit `ProceduralTextureGenerator.cs` or `MaterialAssetsSetup.cs`, re-run the relevant Editor script, and commit:

```bash
git status
git add Assets
git commit -m "tune: post-playtest visual refinements"
```

---

## Done criteria

- 12 PNGs in `Assets/Textures/` (6 albedo + 6 normal, all 256×256).
- All 6 materials in `Assets/Materials/` have `_MainTex`, `_BumpMap`, and `_BumpScale` assigned.
- `Assets/Materials/ProceduralSky.mat` exists and is referenced by `RenderSettings.skybox` in the scene.
- `Assets/Scenes/MainScene/ReflectionProbe-0.exr` exists.
- A play session shows cubes with material identity (grain, speckle, brushed metal, facets, spiked bumps, yellow dimples) and metal cubes show real reflections.
- No console errors during play.
