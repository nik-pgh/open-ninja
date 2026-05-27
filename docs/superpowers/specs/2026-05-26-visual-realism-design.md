# Visual Realism — Procedural Textures + Lighting Design Spec

**Date:** 2026-05-26
**Status:** Approved by user, ready for implementation planning
**Project:** open-ninja (Unity 6, Built-in Render Pipeline, Standard shader)
**Builds on:** `2026-05-26-material-cubes-design.md`

---

## 1. Summary

The current six cube materials read as flat colored blocks because the Standard PBR shader has nothing to work with beyond a solid albedo and a smoothness float, and the scene has no environment to reflect. This spec adds two complementary fixes: (1) procedurally generated albedo + normal-map textures for each material so the surfaces have grain / speckle / facets / dimples that catch light, and (2) a proper lighting environment (procedural skybox, tuned directional light, reflection probe) so the PBR pipeline has reflections and ambient to sample. Together, the cubes go from "colored cubes" to "objects with material identity" without any code-side changes to gameplay.

---

## 2. Goals & Non-Goals

**Goals**
- Each material reads as a distinct *substance*: wood grain, stone speckle, brushed metal, crystal facets, spiked pyramids, rubber dimples.
- Lighting setup is self-contained — no external HDRI imports, no marketplace assets.
- Procedural generation is *re-runnable*: change a parameter, regenerate, see the result.
- All runtime behavior (`Cube`, `CubeSpawner`, `GameManager`) untouched.

**Non-Goals**
- Photorealistic textures. Stylized PBR is the target; "looks like a real plank of wood" is not.
- Real HDRI skyboxes or external texture packs (Poly Haven, AmbientCG).
- Shader Graph or custom shaders. Unity Standard shader only.
- Runtime texture generation. Textures are baked at setup time.
- Per-cube texture variation (e.g., randomized wood grain per cube). All cubes of a given material share the same texture.

---

## 3. Architecture

Two new Editor scripts plus targeted modifications to two existing scripts. No runtime code changes.

```
Procedural pipeline at setup time:

┌──────────────────────────────────┐
│ ProceduralTextureGenerator (NEW) │
│ GenerateAll() →                  │
│   per material:                  │
│     albedo Texture2D → PNG       │
│     normal Texture2D → PNG       │
│   import settings:               │
│     normal flagged as NormalMap  │
└──────────────┬───────────────────┘
               │
               ▼
┌──────────────────────────────────┐
│ MaterialAssetsSetup (MOD)        │
│ - calls ProceduralTextureGen     │
│ - on each .mat, sets:            │
│   _MainTex   = albedo PNG        │
│   _BumpMap   = normal PNG        │
│   _Metallic, _Smoothness         │
└──────────────┬───────────────────┘
               │
               ▼
┌──────────────────────────────────┐
│ SceneSetup (MOD)                 │
│ - creates ProceduralSky.mat      │
│ - RenderSettings.skybox = sky    │
│ - ambient = Skybox mode          │
│ - tunes Directional Light        │
│ - adds ReflectionProbe (Baked)   │
│ - Lightmapping.BakeReflProbe()   │
└──────────────────────────────────┘
```

Runtime: `Cube.Initialize(CubeMaterial)` still assigns `material.renderMaterial` to the cube's MeshRenderer; that material now has `_MainTex` and `_BumpMap` populated, so the surface renders with detail. No behavior change.

---

## 4. Texture generation algorithms

All textures are 256×256, RGBA32 for albedo, RGB24 for normal. Generated via `Color[]` arrays + `Texture2D.SetPixels` + `EncodeToPNG()` + `File.WriteAllBytes`. Normal maps are derived from a height field via a Sobel filter and encoded in the standard `(x, y, z) ∈ [-1, 1] → [0, 1]` mapping.

### 4a. Wood

**Albedo:** vertical grain bands. Build a 1D Perlin noise field along x (low-frequency + high-frequency layer). For each pixel at `(x, y)`:
- `band = Perlin(x * 0.04, 0) * 0.7 + Perlin(x * 0.12, 0) * 0.3` → height in `[0, 1]`
- `darkness = Mathf.Pow(band, 2.0)` (sharpen dark bands)
- `color = baseWood * (1 - 0.55 * darkness)` (warm brown → darker brown)
- Add tiny per-pixel Perlin noise to break tiling artifacts.

Optional knot spots: 3-4 random Voronoi cells per texture with very dark centers and tight radius (~12 px). Probability `0.04` per spawn → most textures have 0-1 knots.

**Normal:** the same band noise treated as a height field. Sobel filter to get `(dx, dy)`. The `z` component fills to 1 minus the gradient magnitude. Strength `1.0`.

### 4b. Stone

**Albedo:** uniform 4-octave Perlin noise scaled `[0, 1]`. Map to a luminance between `0.4` and `0.7`. Slight saturation toward gray; tinted by `baseStone = (0.55, 0.55, 0.55)`. 3% chance per pixel of a darker mica fleck (rgb `0.2, 0.2, 0.25`).

**Normal:** the noise field directly as height. Sobel filter. Strength `0.8` (subtler than wood).

### 4c. Metal

**Albedo:** horizontal brushed lines. For each row `y`:
- `line = (sin(y * 1.2) * 0.5 + 0.5) * 0.06`
- Add `Perlin(x * 0.01, y * 0.3) * 0.04` (tiny per-row scratches)
- `color = baseMetal * (1 - line - scratches)`

`baseMetal = (0.3, 0.3, 0.32)`. The mid-gray + faint horizontal lines suggests brushed steel.

**Normal:** mostly flat. Add very subtle horizontal grooves matching the `sin(y)` pattern with small amplitude. Strength `0.3`.

### 4d. Crystal

**Albedo:** Voronoi diagram with 24 cell centers. Each cell:
- Picks a random tint near `(0.55, 0.85, 1.0)` (±0.05 per channel)
- Inside the cell, color shades from edge (slightly darker) to center (slightly lighter)
- Edges are sharp delta between adjacent cells

**Normal:** strong gradients at cell edges (each edge encodes a normal pointing perpendicular to the edge direction); inside cells, near-flat. This gives the appearance of polished crystal facets catching light at edges.

### 4e. Spiked

**Albedo:** Voronoi diagram with 36 cell centers (denser than crystal). Each cell behaves like a pyramid:
- Center is lighter (`baseSpiked + 0.15`)
- Edges are darker (`baseSpiked - 0.15`)
- Outside the cell radius → dark base

`baseSpiked = (0.1, 0.1, 0.1)`. The cells read as small pyramid bumps.

**Normal:** each cell has its center treated as a "peak" with normal pointing outward (+z). The edges encode normals tilted radially outward, mapping to the standard `(x, y)` tangent encoding. Strength `1.0`.

### 4f. Rubber

**Albedo:** base `(1.0, 0.9, 0.2)` (saturated yellow) with subtle Perlin noise `±0.05` to break flatness.

**Normal:** dimple grid — 8×8 array of dimple centers, each dimple is a small circular depression (radius `12 px`). Strength `0.4` (subtle, like a basketball texture).

---

## 5. `ProceduralTextureGenerator` script

**File:** `Assets/Editor/ProceduralTextureGenerator.cs`

```csharp
public static class ProceduralTextureGenerator
{
    private const int TexSize = 256;
    private const string OutputDir = "Assets/Textures";

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

    private static void GenerateWood(string name) { /* §4a algorithm */ }
    // ... per material

    private static void SaveTexture(Color[] pixels, string path, bool isNormalMap)
    {
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (isNormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = false; // we baked the normal directly
        }
        importer.maxTextureSize = TexSize;
        importer.SaveAndReimport();
    }

    private static Color[] SobelNormal(float[] height, int size, float strength) { /* ... */ }

    private static (Vector2[] centers, int[] cellMap) VoronoiCells(int count, int size, int seed) { /* ... */ }
}
```

Estimated ~250 lines total. Helper functions for Sobel filtering and Voronoi cell maps are shared across `GenerateX` methods.

**Seeding** — each `GenerateX` method seeds `UnityEngine.Random` with a deterministic constant (e.g., `Random.InitState(name.GetHashCode())`) so re-runs produce identical textures. Designers can change patterns by changing the seed or the parameters.

---

## 6. `MaterialAssetsSetup` changes

Add a call to `ProceduralTextureGenerator.GenerateAll()` at the top of `Execute()`, before creating the 6 materials. After each material is created, set its textures and adjust the Standard-shader properties:

```csharp
private static Material MakeMaterial(string name, Color color, float smoothness, float metallic)
{
    string matPath = $"{MaterialsDir}/{name}.mat";
    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (mat == null)
    {
        mat = new Material(Shader.Find("Standard"));
        AssetDatabase.CreateAsset(mat, matPath);
    }
    mat.color = color;
    mat.SetFloat("_Glossiness", smoothness);
    mat.SetFloat("_Metallic", metallic);

    // Wire procedural textures.
    var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
        $"Assets/Textures/{name}_Albedo.png");
    var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(
        $"Assets/Textures/{name}_Normal.png");
    if (albedoTex != null) mat.SetTexture("_MainTex", albedoTex);
    if (normalTex != null)
    {
        mat.SetTexture("_BumpMap", normalTex);
        mat.EnableKeyword("_NORMALMAP");
        // Per-material normal strength (Standard shader's _BumpScale property).
        // Caller passes this in; per-material table in §9 / §6 below.
    }

    EditorUtility.SetDirty(mat);
    return mat;
}
```

Note: `MakeMaterial` is extended to take a `float bumpScale` parameter; pass the per-material strength from the table below and assign via `mat.SetFloat("_BumpScale", bumpScale);` inside the normal-texture branch.

Other tweaks per material (set inside the `MakeCubeMaterial` data tuning, not the texture pipeline):

| Material | _Metallic | _Glossiness |
|---|---|---|
| Wood    | 0.0  | 0.35 |
| Stone   | 0.0  | 0.25 |
| Metal   | 1.0  | 0.85 |
| Crystal | 0.1  | 0.95 |
| Spiked  | 0.0  | 0.4  |
| Rubber  | 0.0  | 0.3  |

(These supersede the v1 values in the previous spec where smoothness was just an aesthetic placeholder. Metal is now properly metallic, crystal is glossy, etc.)

---

## 7. `SceneSetup` changes

### 7a. Procedural skybox material

In `SceneSetup.Execute()`, before the camera-positioning block, build a procedural-sky material:

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
    sky.SetFloat("_SunDisk", 2);                 // small disk
    sky.SetFloat("_AtmosphereThickness", 0.9f);  // slight haze
    sky.SetColor("_SkyTint", new Color(0.5f, 0.7f, 0.95f, 1f));
    sky.SetColor("_GroundColor", new Color(0.2f, 0.18f, 0.16f, 1f));
    sky.SetFloat("_Exposure", 1.0f);
    EditorUtility.SetDirty(sky);
    return sky;
}
```

Assign at scene level: `RenderSettings.skybox = sky;` and `RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;`. Then `DynamicGI.UpdateEnvironment()` to bake the ambient contribution.

### 7b. Directional light tuning

The directional light created by `NewScene` gets updated:

```csharp
var dirLight = Object.FindFirstObjectByType<Light>();
if (dirLight != null && dirLight.type == LightType.Directional)
{
    dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    dirLight.color = new Color(1.0f, 0.95f, 0.85f);
    dirLight.intensity = 1.0f;
    dirLight.shadows = LightShadows.Soft;
    dirLight.shadowStrength = 0.6f;
}
```

### 7c. Reflection probe

A `ReflectionProbe` GameObject under `Systems/`:

```csharp
var probeGO = new GameObject("ReflectionProbe");
probeGO.transform.SetParent(systems.transform, false);
probeGO.transform.position = Vector3.zero;
var probe = probeGO.AddComponent<ReflectionProbe>();
probe.size = new Vector3(25f, 20f, 10f);
probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
probe.resolution = 128;
probe.importance = 1;
probe.boxProjection = false;
```

After scene save, bake the probe:

```csharp
Lightmapping.BakeReflectionProbe(probe, $"Assets/Scenes/MainScene/ReflectionProbe-0.exr");
```

The path needs to be a valid scene-relative directory; Unity creates it on bake.

### 7d. Camera background

Camera clear flags switch from `SolidColor` to `Skybox`:

```csharp
cam.clearFlags = CameraClearFlags.Skybox;
```

(The existing `cam.backgroundColor` assignment becomes irrelevant but stays; harmless.)

---

## 8. Data flow (changes only)

There are no runtime data-flow changes. The PBR pipeline now has data to chew on:

```
Scene render frame:
 cube.MeshRenderer.material = material.renderMaterial   (set by Cube.Initialize)
   → Standard shader reads _MainTex (procedural albedo)
   → reads _BumpMap (procedural normal)
   → reads _Metallic, _Glossiness floats
   → samples skybox via ambient (Skybox mode)
   → samples ReflectionProbe (the one we baked)
   → outputs lit pixel with surface detail + real reflections
```

The visible change is entirely a rendering-pipeline effect. Gameplay is identical.

---

## 9. Tuning defaults

| Knob | Default | Effect |
|---|---|---|
| Wood grain band density | 8 bands/256 px | Higher → tighter grain |
| Stone speckle scale | 4 octaves, base 32 | Higher → finer dots |
| Metal scratch line spacing | sin freq 1.2 | Higher → more lines |
| Crystal cell count | 24 | Higher → smaller facets |
| Spiked pyramid count | 36 | Higher → smaller spikes |
| Rubber dimple grid | 8 × 8 | Subtle dimples |
| Normal map strength | 1.0 wood/spiked, 0.8 stone/crystal, 0.4 rubber, 0.3 metal | Higher → deeper relief |
| Skybox `_AtmosphereThickness` | 0.9 | 0 = clean; 5 = soup |
| Skybox `_SunDisk` | 2 (small disk) | 0=none, 1=tiny, 2=small, 3=large |
| Directional light intensity | 1.0 | Brighter → more contrast |
| Reflection probe resolution | 128 | Higher → sharper reflections |

---

## 10. Files

```
NEW   Assets/Editor/ProceduralTextureGenerator.cs      ~250 lines, generates 12 PNGs
NEW   Assets/Textures/Wood_Albedo.png                  256×256 RGBA32
NEW   Assets/Textures/Wood_Normal.png                  256×256 normal-mapped import
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
NEW   Assets/Materials/ProceduralSky.mat               procedural skybox material
NEW   Assets/Scenes/MainScene/ReflectionProbe-0.exr    baked reflection probe data
MOD   Assets/Editor/MaterialAssetsSetup.cs             calls GenerateAll(); wires _MainTex/_BumpMap;
                                                       updates _Metallic/_Glossiness per spec
MOD   Assets/Editor/SceneSetup.cs                      ProceduralSky; light tuning; ReflectionProbe;
                                                       Skybox clear flags; Lightmapping.BakeReflectionProbe
```

Total ~3 MB of generated PNGs in git. Acceptable for an arcade-scale game.

---

## 11. Execution sequence

To apply this design after implementation:

1. **`ProceduralTextureGenerator.GenerateAll()`** — creates the 12 PNGs in `Assets/Textures/`. Sets the import-type to NormalMap on the 6 `_Normal.png` files.
2. **`MaterialAssetsSetup.Execute()`** — same as before, but now also assigns `_MainTex` + `_BumpMap` on each material and applies the per-material `_Metallic`/`_Glossiness` table.
3. **`SceneSetup.Execute()`** — same as before, plus: creates `ProceduralSky.mat`, assigns it to `RenderSettings.skybox`, sets ambient mode, tunes the Directional Light, adds the `ReflectionProbe`, and calls `Lightmapping.BakeReflectionProbe` to populate it.
4. **Enter Play Mode** — verify materials look like materials.

All three scripts are idempotent. Re-running them is a no-op cost-wise except for the texture/probe bake (~5 seconds total).

---

## 12. Testing

No new automated tests. The design is purely visual; correctness is verified by playtest.

**Verification checklist** during the implementation playtest:

- Wood cubes show vertical grain stripes; one cube might have a knot spot.
- Stone cubes have visible random speckle, not flat gray.
- Metal cubes show horizontal brush lines + the metal cube's reflection includes other cubes / the sky.
- Crystal cubes show clear facet boundaries; light highlights at facet edges.
- Spiked cubes have small pyramid-bump pattern across each face.
- Rubber cubes are saturated yellow with subtle dimples; mostly flat-looking but the dimples catch light.
- Reflection probe contributes — the metal cube is not pitch black; it has visible reflection of its surroundings.
- Skybox visible above the play area (slight blue gradient with a small sun disk).

---

## 13. Edge cases

| Case | Resolution |
|---|---|
| `_MainTex` slot not present in shader (shader downgrade) | `Material.HasProperty("_MainTex")` guard; if missing, skip. Standard always has it. |
| Normal map import setting not stuck | `AssetImporter` saved with `SaveAndReimport()` to force the flag to persist. |
| Reflection probe bake fails | Catch + warning log; cubes still render, just without baked reflections (fallback to skybox-only). Re-running scripts re-tries. |
| Re-running `GenerateAll` on machines without write permission | Catch IOException, log, abort. Should never happen in practice. |
| Materials still showing flat color after running scripts | Probably caused by `_NORMALMAP` keyword not enabled. The script `EnableKeyword("_NORMALMAP")` is the fix; ensured per material. |
| Camera background is now sky → too distracting | Verified via playtest; the camera angles slightly downward, so sky is a thin strip at top of view. If unacceptable, swap clear flags back to `SolidColor` (one-line change). |
| `Cube.prefab` already has a Material assigned to its MeshRenderer that doesn't pick up the new textures | The `Cube.Initialize` runtime `sharedMaterial` assignment overrides the prefab's default. No change needed. |

---

## 14. Out of scope (explicitly deferred)

- Per-cube random seed (each cube of a material gets a slightly different texture).
- Wear-and-tear textures (chipped wood, dented metal).
- Subsurface scattering, anisotropic shading, parallax mapping.
- Lightmaps. Real-time lighting only.
- Post-processing stack (bloom, color grading) — separate spec if wanted.
- Audio polish.
- Mobile shader variants — Standard shader works on mobile but may need fallback later.

---

## 15. Open questions for implementation

- The exact warm/cool palette of the procedural sky is best decided after a first playtest — defaults in §7a are a starting point.
- Reflection probe size may need tightening if the cubes pick up reflections from off-screen ineffective areas.
- The Sobel filter strength multiplier per material is a hand-tuned starting set; expect adjustments after seeing the first generated normal maps.
