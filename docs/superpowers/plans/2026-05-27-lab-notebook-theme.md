# Lab Notebook Theme Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply a unified lab-notebook visual theme (Caveat font, cream graph paper, red/black ink, hard-offset shadows, slight rotations) to the start screen, gameplay HUD, and game-over panel — without changing any runtime gameplay scripts.

**Architecture:** A static `LabNotebookTheme` class holds the palette + sizes + asset paths. A new one-shot `StartScreenAssetsSetup` Editor utility downloads the Caveat font, generates a TMP Font Asset, and bakes a procedural `GraphPaper.png`. Two existing Editor setup scripts (`StartSceneSetup`, `SceneSetup`) are extended to read from the theme and rebuild their respective scenes/prefabs in the notebook style. Pure presentation overhaul — every runtime MonoBehaviour stays untouched.

**Tech Stack:** Unity 6, C#, TextMeshPro, Unity UI, `UnityWebRequest` (font download), `TMP_FontAsset.CreateFontAsset` (font asset generation), `Texture2D.EncodeToPNG` (graph paper). `coplay-mcp` for executing Editor scripts and visual verification.

**Spec:** `docs/superpowers/specs/2026-05-27-lab-notebook-theme-design.md`

---

## File Structure

```
NEW   Assets/Scripts/Util/LabNotebookTheme.cs       ~80 lines, static class of constants
NEW   Assets/Editor/StartScreenAssetsSetup.cs       ~200 lines, font downloader + texture gen
NEW   Assets/Fonts/Caveat-Bold.ttf                  ~80 KB, fetched once
NEW   Assets/Fonts/Caveat-Bold SDF.asset            generated TMP Font Asset
NEW   Assets/Textures/GraphPaper.png                512×512, generated
MOD   Assets/Editor/StartSceneSetup.cs              major rewrite of visual layer (Caveat font,
                                                    graph paper backdrop, rotations, shadows)
MOD   Assets/Editor/SceneSetup.cs                   HUD elements wrapped in sticky-note cards,
                                                    game-over panel becomes notebook page
MOD   Assets/Prefabs/CubeInfoRow.prefab             rebuilt by StartSceneSetup
MOD   Assets/Prefabs/ComboPopup.prefab              rebuilt by SceneSetup
```

No new runtime scripts. No changes to any runtime script logic. Tests stay green at 35/35 (no signatures changed).

---

## Conventions

- Working directory: `/Users/nikkim/dev/open-ninja/.claude/worktrees/lab-notebook` (create via `using-git-worktrees` skill at start).
- C# namespace `OpenNinja` for runtime code, `OpenNinja.EditorSetup` for Editor scripts.
- After every code change, call `mcp__coplay-mcp__check_compile_errors` if Unity Editor is open; expect `No compile errors`.
- Editor scripts are idempotent — re-runnable any number of times.
- All UI lives on portrait Canvases referencing 1080×1920.

---

## Task 1: `LabNotebookTheme` static class

**Goal:** Single source of truth for the notebook palette, typography sizes, geometry, and asset paths. No behavior, just constants.

**Files:**
- Create: `Assets/Scripts/Util/LabNotebookTheme.cs`

- [ ] **Step 1: Write the file**

```csharp
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Single source of truth for the lab-notebook visual identity. Read by
    /// Editor setup scripts at scene build time. Tuning the look = editing
    /// constants here and re-running the setup scripts.
    /// </summary>
    public static class LabNotebookTheme
    {
        // ---- Palette ----
        public static readonly Color PaperCream  = new Color(0.992f, 0.988f, 0.969f, 1f); // #fdfcf7
        public static readonly Color InkDark     = new Color(0.102f, 0.102f, 0.102f, 1f); // #1a1a1a
        public static readonly Color InkRed      = new Color(0.784f, 0.196f, 0.196f, 1f); // #c83232
        public static readonly Color InkGreen    = new Color(0.290f, 0.541f, 0.227f, 1f); // #4a8a3a
        public static readonly Color InkAmber    = new Color(0.784f, 0.616f, 0.227f, 1f); // #c89d3a
        public static readonly Color GridBlue    = new Color(0.157f, 0.353f, 0.549f, 0.10f);
        public static readonly Color MarginRed   = new Color(0.784f, 0.196f, 0.196f, 0.50f);
        public static readonly Color SubduedInk  = new Color(0.333f, 0.333f, 0.333f, 1f); // #555
        public static readonly Color GameOverDim = new Color(0f, 0f, 0f, 0.55f);

        // ---- Typography (font sizes in canvas units; portrait ref 1080x1920) ----
        public const float TitleSize        = 144f;
        public const float SubtitleSize     = 36f;
        public const float BestSize         = 48f;
        public const float DividerSize      = 28f;
        public const float RowNameSize      = 56f;
        public const float RowPointsSize    = 48f;
        public const float BadgeSize        = 24f;
        public const float InputSize        = 64f;
        public const float ButtonSize       = 88f;
        public const float ArrowSize        = 36f;

        // HUD (gameplay sticky-note cards)
        public const float HudLabelSize     = 22f;
        public const float HudValueSize     = 60f;
        public const float HudNicknameSize  = 36f;
        public const float HudHeartSize     = 56f;
        public const float ComboBadgeSize   = 48f;
        public const float ComboPopupSize   = 64f;

        // Game-over panel
        public const float GameOverTitleSize  = 96f;
        public const float GameOverScoreSize  = 72f;
        public const float NewBestStampSize   = 40f;
        public const float GameOverButtonSize = 56f;

        // ---- Geometry ----
        public const float MarginLineX          = 110f;
        public const float TitleRotation        = -2f;
        public const float ButtonRotation       = -1.5f;
        public const float BadgeRotation        = -2f;
        public const float HudCardRotationLeft  = -3f;
        public const float HudCardRotationRight = 2f;
        public const float StampRotation        = -8f;
        public const float ShadowOffsetSmall    = 4f;
        public const float ShadowOffsetBig      = 8f;

        // ---- Asset paths ----
        public const string FontAssetPath        = "Assets/Fonts/Caveat-Bold SDF.asset";
        public const string GraphPaperSpritePath = "Assets/Textures/GraphPaper.png";
        public const string CaveatTtfPath        = "Assets/Fonts/Caveat-Bold.ttf";
    }
}
```

- [ ] **Step 2: Verify compile clean**

If Unity is running, call `mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Util/LabNotebookTheme.cs
git commit -m "feat(theme): add LabNotebookTheme static class with palette, sizes, paths"
```

---

## Task 2: `StartScreenAssetsSetup` — font download + graph paper generator

**Goal:** One-shot, idempotent Editor utility that ensures Caveat-Bold.ttf, Caveat-Bold SDF.asset, and GraphPaper.png all exist. Both other Editor setup scripts call into this at the top of `Execute()`.

**Files:**
- Create: `Assets/Editor/StartScreenAssetsSetup.cs`

- [ ] **Step 1: Write the file**

```csharp
using System.Collections;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

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
            sdf.TryAddCharacters("★↗—♥−", out _, includeMissingCharactersInError: false);
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
```

- [ ] **Step 2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3: Run the asset setup**

If Unity Editor is open and connected:

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/StartScreenAssetsSetup.cs
  methodName: Execute
```

Expected result on first run:
`ttf: downloaded | sdf: generated | png: generated`

On any later run:
`ttf: already present | sdf: already present | png: already present`

- [ ] **Step 4: Verify outputs**

```bash
ls -la Assets/Fonts/Caveat-Bold.ttf      # ~70-90 KB
ls -la "Assets/Fonts/Caveat-Bold SDF.asset"
ls -la Assets/Textures/GraphPaper.png    # ~5-15 KB
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/StartScreenAssetsSetup.cs \
        Assets/Fonts \
        Assets/Textures/GraphPaper.png \
        Assets/Textures/GraphPaper.png.meta
git commit -m "feat(assets): Caveat font + GraphPaper texture for notebook theme"
```

---

## Task 3: Restyle `StartSceneSetup` — full lab-notebook layout

**Goal:** Rebuild `StartScene.unity` and `CubeInfoRow.prefab` with the notebook theme (cream graph paper background, red margin line, Caveat title rotated −2°, sticky-note styling, outlined buttons with hard shadows, "↗ tap here" arrow).

This task is a major rewrite of `StartSceneSetup.cs`. Read the existing file first to understand the current structure; then replace the visual-layer code (camera bg, title, info table, input, button) while preserving the script-wiring at the end.

**Files:**
- Modify: `Assets/Editor/StartSceneSetup.cs`

### Step 1: Add `StartScreenAssetsSetup.Execute()` call at the top of `StartSceneSetup.Execute()`

- [ ] **Step 1.1: Insert one line**

Find `public static string Execute()` in `StartSceneSetup.cs`. At the very top of the method body (before `var log = new List<string>();`), add:

```csharp
StartScreenAssetsSetup.Execute();
```

This guarantees the font and graph paper exist before the scene is built.

### Step 2: Replace `BuildRowPrefab` — notebook-styled row

- [ ] **Step 2.1: Replace the existing `BuildRowPrefab` method entirely**

Locate `private static void BuildRowPrefab()` and replace its body with:

```csharp
private static void BuildRowPrefab()
{
    EnsureFolder("Assets/Prefabs");
    DeleteIfExists(RowPrefabPath);

    var caveat = LoadCaveat();
    var paper  = AssetDatabase.LoadAssetAtPath<Sprite>(LabNotebookTheme.GraphPaperSpritePath);

    var root = new GameObject("CubeInfoRow",
        typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
    var rt = (RectTransform)root.transform;
    rt.sizeDelta = new Vector2(920, 120);

    var hlg = root.GetComponent<HorizontalLayoutGroup>();
    hlg.spacing = 18;
    hlg.padding = new RectOffset(20, 20, 12, 12);
    hlg.childAlignment = TextAnchor.MiddleLeft;
    hlg.childForceExpandWidth = false;
    hlg.childForceExpandHeight = false;
    hlg.childControlWidth = false;
    hlg.childControlHeight = false;

    // Faint paper backdrop on the row (lets the page show through).
    // No image; the page background already provides graph paper.

    // Icon with offset hard shadow.
    var iconWrap = new GameObject("IconWrap", typeof(RectTransform), typeof(LayoutElement));
    iconWrap.transform.SetParent(root.transform, false);
    var iconWrapLE = iconWrap.GetComponent<LayoutElement>();
    iconWrapLE.preferredWidth = 88; iconWrapLE.preferredHeight = 88;

    var iconShadow = new GameObject("IconShadow", typeof(RectTransform), typeof(Image));
    iconShadow.transform.SetParent(iconWrap.transform, false);
    var iconShadowRT = (RectTransform)iconShadow.transform;
    iconShadowRT.anchorMin = Vector2.zero;
    iconShadowRT.anchorMax = Vector2.one;
    iconShadowRT.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                         -LabNotebookTheme.ShadowOffsetSmall);
    iconShadowRT.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                         -LabNotebookTheme.ShadowOffsetSmall);
    var iconShadowImg = iconShadow.GetComponent<Image>();
    iconShadowImg.color = new Color(LabNotebookTheme.InkDark.r,
                                    LabNotebookTheme.InkDark.g,
                                    LabNotebookTheme.InkDark.b, 0.35f);

    var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
    iconGO.transform.SetParent(iconWrap.transform, false);
    var iconRT = (RectTransform)iconGO.transform;
    iconRT.anchorMin = Vector2.zero;
    iconRT.anchorMax = Vector2.one;
    iconRT.offsetMin = Vector2.zero;
    iconRT.offsetMax = Vector2.zero;
    var iconImg = iconGO.GetComponent<Image>();
    iconImg.preserveAspect = true;
    iconImg.color = Color.white;

    // Name (Caveat, dark ink, large).
    var nameGO = NewLabel(root.transform, "Name", "Material",
        LabNotebookTheme.RowNameSize, LabNotebookTheme.InkDark,
        TextAlignmentOptions.Left, caveat, FontStyles.Bold,
        preferredWidth: 380);

    // Points (Caveat, color set by Bind, ink-dark default).
    var pointsGO = NewLabel(root.transform, "Points", "+1",
        LabNotebookTheme.RowPointsSize, LabNotebookTheme.InkDark,
        TextAlignmentOptions.Right, caveat, FontStyles.Bold,
        preferredWidth: 200);

    // Role badge — rotated wrapper + Image border + TMP text inside.
    var badgeWrap = new GameObject("RoleBadgeWrap", typeof(RectTransform), typeof(LayoutElement));
    badgeWrap.transform.SetParent(root.transform, false);
    var badgeWrapRT = (RectTransform)badgeWrap.transform;
    badgeWrapRT.sizeDelta = new Vector2(180, 60);
    var badgeWrapLE = badgeWrap.GetComponent<LayoutElement>();
    badgeWrapLE.minWidth = 180; badgeWrapLE.minHeight = 60;
    badgeWrapRT.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.BadgeRotation);

    var badgeBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
    badgeBg.transform.SetParent(badgeWrap.transform, false);
    var badgeBgRT = (RectTransform)badgeBg.transform;
    badgeBgRT.anchorMin = Vector2.zero;
    badgeBgRT.anchorMax = Vector2.one;
    badgeBgRT.offsetMin = Vector2.zero;
    badgeBgRT.offsetMax = Vector2.zero;
    var badgeBgImg = badgeBg.GetComponent<Image>();
    badgeBgImg.color = LabNotebookTheme.PaperCream;
    // A simple bordered look: rely on the badge text color matching the border.
    // For a real outline we'd need a 9-slice sprite; the text + colored bg reads.

    var badgeText = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
    badgeText.transform.SetParent(badgeBg.transform, false);
    var badgeTextRT = (RectTransform)badgeText.transform;
    badgeTextRT.anchorMin = Vector2.zero;
    badgeTextRT.anchorMax = Vector2.one;
    badgeTextRT.offsetMin = Vector2.zero;
    badgeTextRT.offsetMax = Vector2.zero;
    var badgeTmp = badgeText.GetComponent<TextMeshProUGUI>();
    badgeTmp.text = "NORMAL";
    badgeTmp.fontSize = LabNotebookTheme.BadgeSize;
    badgeTmp.color = LabNotebookTheme.InkGreen;
    badgeTmp.alignment = TextAlignmentOptions.Center;
    badgeTmp.fontStyle = FontStyles.Bold;
    badgeTmp.outlineColor = LabNotebookTheme.InkGreen;
    badgeTmp.outlineWidth = 0.2f;

    // Attach CubeInfoRow script and wire serialized references.
    var rowScript = root.AddComponent<CubeInfoRow>();
    var so = new SerializedObject(rowScript);
    so.FindProperty("icon").objectReferenceValue = iconImg;
    so.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
    so.FindProperty("pointsLabel").objectReferenceValue = pointsGO.GetComponent<TMP_Text>();
    so.FindProperty("roleBadge").objectReferenceValue = badgeTmp;
    so.FindProperty("roleBadgeBackground").objectReferenceValue = badgeBgImg;
    so.ApplyModifiedPropertiesWithoutUndo();

    PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
    Object.DestroyImmediate(root);
}
```

### Step 3: Replace `ConfigureCamera`

- [ ] **Step 3.1: Replace**

```csharp
private static void ConfigureCamera()
{
    var cam = Camera.main;
    if (cam == null) return;
    cam.orthographic = true;
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = LabNotebookTheme.PaperCream;
    cam.transform.position = new Vector3(0f, 0f, -10f);
    cam.transform.rotation = Quaternion.identity;
}
```

### Step 4: Add graph-paper background + red margin line to the Canvas

- [ ] **Step 4.1: Add a `BuildBackground` helper**

Anywhere in the `StartSceneSetup` class:

```csharp
private static void BuildBackground(GameObject canvas)
{
    var paper = AssetDatabase.LoadAssetAtPath<Sprite>(LabNotebookTheme.GraphPaperSpritePath);

    var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
    bg.transform.SetParent(canvas.transform, false);
    var rt = (RectTransform)bg.transform;
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    var img = bg.GetComponent<Image>();
    img.sprite = paper;
    img.color = Color.white;
    img.type = Image.Type.Tiled;
    // Render behind everything else.
    bg.transform.SetAsFirstSibling();

    var margin = new GameObject("MarginLine", typeof(RectTransform), typeof(Image));
    margin.transform.SetParent(canvas.transform, false);
    var mrt = (RectTransform)margin.transform;
    mrt.anchorMin = new Vector2(0, 0);
    mrt.anchorMax = new Vector2(0, 1);
    mrt.pivot = new Vector2(0, 0.5f);
    mrt.anchoredPosition = new Vector2(LabNotebookTheme.MarginLineX, 0);
    mrt.sizeDelta = new Vector2(2.5f, 0);
    margin.GetComponent<Image>().color = LabNotebookTheme.MarginRed;
    margin.transform.SetSiblingIndex(1); // just above background
}
```

- [ ] **Step 4.2: Call it in `Execute`**

After `var canvas = BuildCanvas();` in `Execute`, add:

```csharp
BuildBackground(canvas);
```

### Step 5: Replace `BuildTitleLabel` and add `BuildSubtitle` + `BuildBestScoreLabel` updates

- [ ] **Step 5.1: Replace `BuildTitleLabel`**

```csharp
private static GameObject BuildTitleLabel(GameObject canvas)
{
    var caveat = LoadCaveat();
    var go = NewLabel(canvas.transform, "Title", "Material\nNinja",
        LabNotebookTheme.TitleSize, LabNotebookTheme.InkDark,
        TextAlignmentOptions.Center, caveat, FontStyles.Bold,
        preferredWidth: 0);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0.5f, 1f);
    rt.anchoredPosition = new Vector2(20, -120);
    rt.sizeDelta = new Vector2(960, 320);
    rt.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.TitleRotation);
    return go;
}
```

- [ ] **Step 5.2: Add `BuildSubtitle` method**

```csharp
private static GameObject BuildSubtitle(GameObject canvas)
{
    var caveat = LoadCaveat();
    var go = NewLabel(canvas.transform, "Subtitle", "— field notes vol. 1 —",
        LabNotebookTheme.SubtitleSize, LabNotebookTheme.SubduedInk,
        TextAlignmentOptions.Center, caveat, FontStyles.Italic,
        preferredWidth: 0);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0.5f, 1f);
    rt.anchoredPosition = new Vector2(0, -460);
    rt.sizeDelta = new Vector2(800, 60);
    rt.localRotation = Quaternion.Euler(0, 0, -1f);
    return go;
}
```

- [ ] **Step 5.3: Replace `BuildBestScoreLabel`**

```csharp
private static GameObject BuildBestScoreLabel(GameObject canvas)
{
    var caveat = LoadCaveat();
    var go = NewLabel(canvas.transform, "BestScore", "★ Best: 0",
        LabNotebookTheme.BestSize, LabNotebookTheme.InkRed,
        TextAlignmentOptions.Center, caveat, FontStyles.Bold,
        preferredWidth: 0);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0.5f, 1f);
    rt.anchoredPosition = new Vector2(0, -540);
    rt.sizeDelta = new Vector2(800, 80);
    rt.localRotation = Quaternion.Euler(0, 0, -1f);
    return go;
}
```

### Step 6: Add section dividers (specimens / subject)

- [ ] **Step 6.1: Add `BuildDivider` helper**

```csharp
private static GameObject BuildDivider(GameObject canvas, string label, Vector2 anchored)
{
    var caveat = LoadCaveat();
    var wrap = new GameObject(label + "Divider", typeof(RectTransform));
    wrap.transform.SetParent(canvas.transform, false);
    var rt = (RectTransform)wrap.transform;
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0.5f, 1f);
    rt.anchoredPosition = anchored;
    rt.sizeDelta = new Vector2(920, 50);

    var text = NewLabel(wrap.transform, "Text", label.ToUpperInvariant(),
        LabNotebookTheme.DividerSize, LabNotebookTheme.SubduedInk,
        TextAlignmentOptions.Center, caveat, FontStyles.Bold,
        preferredWidth: 0);
    var trt = (RectTransform)text.transform;
    trt.anchorMin = Vector2.zero;
    trt.anchorMax = Vector2.one;
    trt.offsetMin = Vector2.zero;
    trt.offsetMax = new Vector2(0, -8);

    var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
    underline.transform.SetParent(wrap.transform, false);
    var urt = (RectTransform)underline.transform;
    urt.anchorMin = new Vector2(0, 0);
    urt.anchorMax = new Vector2(1, 0);
    urt.pivot = new Vector2(0.5f, 0);
    urt.anchoredPosition = Vector2.zero;
    urt.sizeDelta = new Vector2(0, 1.5f);
    underline.GetComponent<Image>().color = new Color(
        LabNotebookTheme.GridBlue.r, LabNotebookTheme.GridBlue.g,
        LabNotebookTheme.GridBlue.b, 0.4f);

    return wrap;
}
```

- [ ] **Step 6.2: Use the divider helper in `Execute`**

After `BuildBestScoreLabel(canvas)` and before `BuildInfoTable(canvas)` is called, add:

```csharp
BuildDivider(canvas, "specimens", new Vector2(0, -640));
```

And after the info table block + before `BuildNicknameInput(canvas)`, add:

```csharp
BuildDivider(canvas, "subject", new Vector2(0, -1280));
```

### Step 7: Restyle nickname input

- [ ] **Step 7.1: Replace `BuildNicknameInput`**

```csharp
private static GameObject BuildNicknameInput(GameObject canvas)
{
    var caveat = LoadCaveat();

    var wrap = new GameObject("NicknameWrap", typeof(RectTransform));
    wrap.transform.SetParent(canvas.transform, false);
    var wrt = (RectTransform)wrap.transform;
    wrt.anchorMin = new Vector2(0.5f, 1f);
    wrt.anchorMax = new Vector2(0.5f, 1f);
    wrt.pivot = new Vector2(0.5f, 1f);
    wrt.anchoredPosition = new Vector2(0, -1340);
    wrt.sizeDelta = new Vector2(700, 200);

    // "Nickname:" label.
    var label = NewLabel(wrap.transform, "Label", "Nickname:",
        LabNotebookTheme.SubtitleSize, LabNotebookTheme.InkDark,
        TextAlignmentOptions.Left, caveat, FontStyles.Bold,
        preferredWidth: 0);
    var lrt = (RectTransform)label.transform;
    lrt.anchorMin = new Vector2(0, 1);
    lrt.anchorMax = new Vector2(0, 1);
    lrt.pivot = new Vector2(0, 1);
    lrt.anchoredPosition = new Vector2(40, 0);
    lrt.sizeDelta = new Vector2(400, 50);
    lrt.localRotation = Quaternion.Euler(0, 0, -1f);

    // The TMP_InputField — no background image, just an underline.
    var inputGO = new GameObject("NicknameInput",
        typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
    inputGO.transform.SetParent(wrap.transform, false);
    var irt = (RectTransform)inputGO.transform;
    irt.anchorMin = new Vector2(0.5f, 1f);
    irt.anchorMax = new Vector2(0.5f, 1f);
    irt.pivot = new Vector2(0.5f, 1f);
    irt.anchoredPosition = new Vector2(0, -70);
    irt.sizeDelta = new Vector2(620, 100);
    var inputBg = inputGO.GetComponent<Image>();
    inputBg.color = new Color(0, 0, 0, 0); // transparent — no card behind text

    // Underline.
    var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
    underline.transform.SetParent(inputGO.transform, false);
    var urt = (RectTransform)underline.transform;
    urt.anchorMin = new Vector2(0, 0);
    urt.anchorMax = new Vector2(1, 0);
    urt.pivot = new Vector2(0.5f, 0);
    urt.anchoredPosition = new Vector2(0, 4);
    urt.sizeDelta = new Vector2(-20, 3f);
    underline.GetComponent<Image>().color = LabNotebookTheme.InkDark;

    // Text + placeholder.
    var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
    textArea.transform.SetParent(inputGO.transform, false);
    var tart = (RectTransform)textArea.transform;
    tart.anchorMin = Vector2.zero;
    tart.anchorMax = Vector2.one;
    tart.offsetMin = new Vector2(16, 0);
    tart.offsetMax = new Vector2(-16, 0);

    var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
    textGO.transform.SetParent(textArea.transform, false);
    var trt = (RectTransform)textGO.transform;
    trt.anchorMin = Vector2.zero;
    trt.anchorMax = Vector2.one;
    trt.offsetMin = Vector2.zero;
    trt.offsetMax = Vector2.zero;
    var text = textGO.GetComponent<TextMeshProUGUI>();
    text.font = caveat;
    text.fontSize = LabNotebookTheme.InputSize;
    text.color = LabNotebookTheme.InkDark;
    text.alignment = TextAlignmentOptions.MidlineLeft;

    var placeholderGO = new GameObject("Placeholder",
        typeof(RectTransform), typeof(TextMeshProUGUI));
    placeholderGO.transform.SetParent(textArea.transform, false);
    var prt = (RectTransform)placeholderGO.transform;
    prt.anchorMin = Vector2.zero;
    prt.anchorMax = Vector2.one;
    prt.offsetMin = Vector2.zero;
    prt.offsetMax = Vector2.zero;
    var placeholder = placeholderGO.GetComponent<TextMeshProUGUI>();
    placeholder.font = caveat;
    placeholder.text = "your name here...";
    placeholder.fontSize = LabNotebookTheme.InputSize;
    placeholder.color = new Color(LabNotebookTheme.SubduedInk.r,
                                  LabNotebookTheme.SubduedInk.g,
                                  LabNotebookTheme.SubduedInk.b, 0.55f);
    placeholder.alignment = TextAlignmentOptions.MidlineLeft;
    placeholder.fontStyle = FontStyles.Italic;

    var inputField = inputGO.GetComponent<TMP_InputField>();
    inputField.textViewport = tart;
    inputField.textComponent = text;
    inputField.placeholder = placeholder;
    inputField.characterLimit = 16;
    inputField.contentType = TMP_InputField.ContentType.Standard;

    return inputGO;
}
```

### Step 8: Restyle the Start button

- [ ] **Step 8.1: Replace `BuildStartButton`**

```csharp
private static GameObject BuildStartButton(GameObject canvas)
{
    var caveat = LoadCaveat();

    var wrap = new GameObject("StartButtonWrap", typeof(RectTransform));
    wrap.transform.SetParent(canvas.transform, false);
    var wrt = (RectTransform)wrap.transform;
    wrt.anchorMin = new Vector2(0.5f, 1f);
    wrt.anchorMax = new Vector2(0.5f, 1f);
    wrt.pivot = new Vector2(0.5f, 1f);
    wrt.anchoredPosition = new Vector2(0, -1700);
    wrt.sizeDelta = new Vector2(450, 140);
    wrt.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.ButtonRotation);

    // Shadow Image behind the button (offset for the hard-shadow look).
    var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
    shadow.transform.SetParent(wrap.transform, false);
    var srt = (RectTransform)shadow.transform;
    srt.anchorMin = Vector2.zero;
    srt.anchorMax = Vector2.one;
    srt.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetBig,
                                -LabNotebookTheme.ShadowOffsetBig);
    srt.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetBig,
                                -LabNotebookTheme.ShadowOffsetBig);
    shadow.GetComponent<Image>().color = LabNotebookTheme.InkDark;

    // The button itself.
    var btn = new GameObject("StartButton",
        typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
    btn.transform.SetParent(wrap.transform, false);
    var brt = (RectTransform)btn.transform;
    brt.anchorMin = Vector2.zero;
    brt.anchorMax = Vector2.one;
    brt.offsetMin = Vector2.zero;
    brt.offsetMax = Vector2.zero;
    var img = btn.GetComponent<Image>();
    img.color = LabNotebookTheme.PaperCream;
    var outline = btn.GetComponent<Outline>();
    outline.effectColor = LabNotebookTheme.InkDark;
    outline.effectDistance = new Vector2(3, -3);

    var label = NewLabel(btn.transform, "Label", "Start!",
        LabNotebookTheme.ButtonSize, LabNotebookTheme.InkDark,
        TextAlignmentOptions.Center, caveat, FontStyles.Bold,
        preferredWidth: 0);
    var lrt = (RectTransform)label.transform;
    lrt.anchorMin = Vector2.zero;
    lrt.anchorMax = Vector2.one;
    lrt.offsetMin = Vector2.zero;
    lrt.offsetMax = Vector2.zero;

    return btn;
}
```

### Step 9: Add "↗ tap here" arrow next to the Start button

- [ ] **Step 9.1: Add `BuildTapHereArrow` helper**

```csharp
private static GameObject BuildTapHereArrow(GameObject canvas)
{
    var caveat = LoadCaveat();
    var go = NewLabel(canvas.transform, "TapHereArrow", "↗ tap here",
        LabNotebookTheme.ArrowSize, LabNotebookTheme.InkRed,
        TextAlignmentOptions.Left, caveat, FontStyles.Italic,
        preferredWidth: 0);
    var rt = (RectTransform)go.transform;
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0, 1f);
    rt.anchoredPosition = new Vector2(180, -1690);
    rt.sizeDelta = new Vector2(300, 60);
    rt.localRotation = Quaternion.Euler(0, 0, -8f);
    return go;
}
```

- [ ] **Step 9.2: Call it in `Execute`**

After `BuildStartButton(canvas)`, add:

```csharp
BuildTapHereArrow(canvas);
```

### Step 10: Add a shared `NewLabel` + `LoadCaveat` helper

- [ ] **Step 10.1: Add helpers**

If they're not already present (the existing file's `NewTmpChild` might be similar — replace it with this version that takes a font):

```csharp
private static TMP_FontAsset _cachedCaveat;
private static TMP_FontAsset LoadCaveat()
{
    if (_cachedCaveat == null)
        _cachedCaveat = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
    return _cachedCaveat;
}

private static GameObject NewLabel(Transform parent, string name, string text,
    float fontSize, Color color, TextAlignmentOptions alignment,
    TMP_FontAsset font, FontStyles style, float preferredWidth)
{
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var tmp = go.AddComponent<TextMeshProUGUI>();
    tmp.text = text;
    tmp.fontSize = fontSize;
    tmp.color = color;
    tmp.alignment = alignment;
    tmp.fontStyle = style;
    if (font != null) tmp.font = font;
    tmp.enableWordWrapping = true;
    if (preferredWidth > 0)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = preferredWidth;
        le.preferredHeight = fontSize * 1.4f;
    }
    return go;
}
```

Then update all old `NewTmpChild` call sites to use `NewLabel` (passing `LoadCaveat()` and an explicit `FontStyles` value). The simplest path: search/replace `NewTmpChild` → `NewLabel` and add the two extra args, OR keep `NewTmpChild` as a thin wrapper that calls `NewLabel(parent, name, text, fontSize, white, alignment, null, FontStyles.Bold, preferredWidth)` for backward compat. Either works.

### Step 11: Verify compile + run + commit

- [ ] **Step 11.1: Verify compile**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 11.2: Run StartSceneSetup**

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/StartSceneSetup.cs
  methodName: Execute
```

Expected: `row prefab built | camera configured | canvas built | start screen wired | scene saved + build settings updated → Assets/Scenes/StartScene.unity`

- [ ] **Step 11.3: Open StartScene and capture a screenshot**

```
mcp__coplay-mcp__open_scene
  scene_path: Scenes/StartScene
```

Then `mcp__coplay-mcp__play_game` and `mcp__coplay-mcp__capture_scene_object`. The screenshot should show:
- Cream graph paper background
- Red margin line on the left
- "Material Ninja" in Caveat, rotated −2°, two lines, dark ink
- "— field notes vol. 1 —" subtitle
- "★ Best: 0" in red
- "SPECIMENS" divider with underline
- 6 cube info rows with offset-shadow icons + role badges
- "SUBJECT" divider with underline
- Nickname underlined input
- "Start!" outlined button with offset shadow, rotated
- "↗ tap here" arrow in red

- [ ] **Step 11.4: Commit**

```bash
git add Assets/Editor/StartSceneSetup.cs Assets/Scenes/StartScene.unity Assets/Prefabs/CubeInfoRow.prefab
git commit -m "feat(ui): notebook-style start screen (Caveat font, graph paper, ink)"
```

---

## Task 4: Restyle MainScene HUD via `SceneSetup`

**Goal:** Wrap each HUD element (Score, Nickname, Lives, ComboBadge) in a "sticky-note" paper card with offset shadow and slight rotation. The gameplay backdrop (sky, cubes, walls) is untouched.

**Files:**
- Modify: `Assets/Editor/SceneSetup.cs`
- Modify: `Assets/Prefabs/ComboPopup.prefab` (rebuilt in-place)

### Step 1: Add `StartScreenAssetsSetup.Execute()` call at the top of `SceneSetup.Execute()`

- [ ] **Step 1.1: Insert one line**

At the top of `Execute()`:

```csharp
StartScreenAssetsSetup.Execute();
```

### Step 2: Add a `BuildStickyNote` helper

- [ ] **Step 2.1: Add the helper anywhere in the class**

```csharp
/// <summary>
/// Creates a small "sticky note" card: a paper-graph background with an
/// offset hard-shadow Image behind, slightly rotated. Returns the inner
/// content RectTransform — callers parent their labels into that.
/// </summary>
private static RectTransform BuildStickyNote(Transform parent, string name,
    Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
    Vector2 anchored, Vector2 size, float rotationDeg)
{
    var paper = AssetDatabase.LoadAssetAtPath<Sprite>(LabNotebookTheme.GraphPaperSpritePath);

    var wrap = new GameObject(name, typeof(RectTransform));
    wrap.transform.SetParent(parent, false);
    var wrt = (RectTransform)wrap.transform;
    wrt.anchorMin = anchorMin;
    wrt.anchorMax = anchorMax;
    wrt.pivot = pivot;
    wrt.anchoredPosition = anchored;
    wrt.sizeDelta = size;
    wrt.localRotation = Quaternion.Euler(0, 0, rotationDeg);

    var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
    shadow.transform.SetParent(wrap.transform, false);
    var srt = (RectTransform)shadow.transform;
    srt.anchorMin = Vector2.zero;
    srt.anchorMax = Vector2.one;
    srt.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                -LabNotebookTheme.ShadowOffsetSmall);
    srt.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                -LabNotebookTheme.ShadowOffsetSmall);
    shadow.GetComponent<Image>().color = new Color(0, 0, 0, 0.35f);

    var bg = new GameObject("Paper", typeof(RectTransform), typeof(Image));
    bg.transform.SetParent(wrap.transform, false);
    var brt = (RectTransform)bg.transform;
    brt.anchorMin = Vector2.zero;
    brt.anchorMax = Vector2.one;
    brt.offsetMin = Vector2.zero;
    brt.offsetMax = Vector2.zero;
    var bgImg = bg.GetComponent<Image>();
    bgImg.sprite = paper;
    bgImg.color = Color.white;
    bgImg.type = Image.Type.Tiled;

    // Content child — callers add labels into this.
    var content = new GameObject("Content", typeof(RectTransform));
    content.transform.SetParent(wrap.transform, false);
    var crt = (RectTransform)content.transform;
    crt.anchorMin = Vector2.zero;
    crt.anchorMax = Vector2.one;
    crt.offsetMin = new Vector2(12, 8);
    crt.offsetMax = new Vector2(-12, -8);
    return crt;
}
```

### Step 3: Restyle ScorePanel as a sticky note

- [ ] **Step 3.1: Replace the existing ScorePanel block**

Find the existing block in `Execute()` that creates `ScorePanel` (with a `ScoreView` component). Replace it with:

```csharp
// ScorePanel — sticky note in top-left.
var scoreCard = BuildStickyNote(canvasGO.transform, "ScorePanel",
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
    anchored: new Vector2(60, -60), size: new Vector2(280, 160),
    rotationDeg: LabNotebookTheme.HudCardRotationLeft);

var scoreLabelGO = NewTMP(scoreCard, "Label", "SCORE",
    LabNotebookTheme.HudLabelSize,
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
    anchored: new Vector2(0, -4), size: new Vector2(0, 30),
    color: LabNotebookTheme.SubduedInk, alignment: TextAlignmentOptions.Center);
ApplyCaveat(scoreLabelGO);

var scoreValueGO = NewTMP(scoreCard, "Value", "0",
    LabNotebookTheme.HudValueSize,
    anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 0.5f),
    anchored: new Vector2(0, -16), size: new Vector2(0, 0),
    color: LabNotebookTheme.InkDark, alignment: TextAlignmentOptions.Center);
ApplyCaveat(scoreValueGO);

var scoreView = scoreCard.gameObject.AddComponent<ScoreView>();
var scoreSO = new SerializedObject(scoreView);
SetRef(scoreSO, "label", scoreValueGO.GetComponent<TMP_Text>());
// Format already defaults to "Score: {0}" — override to just "{0}" since
// the SCORE label sits above.
scoreSO.FindProperty("format").stringValue = "{0}";
scoreSO.ApplyModifiedPropertiesWithoutUndo();
```

Note: this requires that `ScoreView` is added to the *content* RectTransform's GameObject (`scoreCard.gameObject`), not the wrap. That keeps the script's parent active during rotation.

### Step 4: Restyle NicknameHud as a sticky note (or merge into ScorePanel card)

- [ ] **Step 4.1: Replace the existing NicknameHud block**

The existing NicknameHud block (from the previous start-screen plan) sits under ScorePanel at (40, -120). Replace it with:

```csharp
// NicknameHud — small note under the score card.
var nickCard = BuildStickyNote(canvasGO.transform, "NicknameHud",
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
    anchored: new Vector2(60, -240), size: new Vector2(320, 70),
    rotationDeg: LabNotebookTheme.HudCardRotationLeft);

var nickLabel = NewTMP(nickCard, "Label", "<color=#c83232>★</color> Player:",
    LabNotebookTheme.HudNicknameSize,
    anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
    anchored: Vector2.zero, size: Vector2.zero,
    color: LabNotebookTheme.InkDark, alignment: TextAlignmentOptions.MidlineLeft);
ApplyCaveat(nickLabel);
nickLabel.GetComponent<TMP_Text>().richText = true;

var nickHud = nickCard.gameObject.AddComponent<NicknameHud>();
var nickSO = new SerializedObject(nickHud);
SetRef(nickSO, "label", nickLabel.GetComponent<TMP_Text>());
nickSO.FindProperty("format").stringValue = "<color=#c83232>★</color> Player: {0}";
nickSO.ApplyModifiedPropertiesWithoutUndo();
```

### Step 5: Restyle LivesRow as a sticky note with TMP hearts

- [ ] **Step 5.1: Replace the existing LivesRow block**

The current LivesRow uses `Graphic[]` of TMP hearts already (from the previous spec). Wrap it in a sticky note:

```csharp
// LivesRow — sticky note in top-right with three TMP hearts.
var livesCard = BuildStickyNote(canvasGO.transform, "LivesRow",
    anchorMin: new Vector2(1, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(1, 1),
    anchored: new Vector2(-60, -60), size: new Vector2(320, 160),
    rotationDeg: LabNotebookTheme.HudCardRotationRight);

var livesLabelGO = NewTMP(livesCard, "Label", "LIVES",
    LabNotebookTheme.HudLabelSize,
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
    anchored: new Vector2(0, -4), size: new Vector2(0, 30),
    color: LabNotebookTheme.SubduedInk, alignment: TextAlignmentOptions.Center);
ApplyCaveat(livesLabelGO);

var heartsRow = new GameObject("HeartsRow",
    typeof(RectTransform), typeof(HorizontalLayoutGroup));
heartsRow.transform.SetParent(livesCard, false);
var hrt = (RectTransform)heartsRow.transform;
hrt.anchorMin = new Vector2(0, 0);
hrt.anchorMax = new Vector2(1, 1);
hrt.offsetMin = new Vector2(0, 0);
hrt.offsetMax = new Vector2(0, -32);
var hhlg = heartsRow.GetComponent<HorizontalLayoutGroup>();
hhlg.spacing = 4;
hhlg.childAlignment = TextAnchor.MiddleCenter;
hhlg.childForceExpandWidth = false;
hhlg.childForceExpandHeight = false;
hhlg.childControlWidth = false;
hhlg.childControlHeight = false;

var hearts = new Graphic[3];
for (int i = 0; i < 3; i++)
{
    var heartGO = new GameObject($"Heart_{i + 1}",
        typeof(RectTransform));
    heartGO.transform.SetParent(heartsRow.transform, false);
    var heartRT = (RectTransform)heartGO.transform;
    heartRT.sizeDelta = new Vector2(72, 72);
    var tmp = heartGO.AddComponent<TextMeshProUGUI>();
    tmp.text = "♥";
    tmp.fontSize = LabNotebookTheme.HudHeartSize;
    tmp.alignment = TextAlignmentOptions.Center;
    tmp.color = LabNotebookTheme.InkRed;
    tmp.textWrappingMode = TextWrappingModes.NoWrap;
    ApplyCaveat(heartGO);
    hearts[i] = tmp;
}

var livesView = livesCard.gameObject.AddComponent<LivesView>();
var livesSO = new SerializedObject(livesView);
var heartsArr = livesSO.FindProperty("hearts");
heartsArr.arraySize = 3;
for (int i = 0; i < 3; i++)
    heartsArr.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];
livesSO.FindProperty("aliveColor").colorValue = LabNotebookTheme.InkRed;
livesSO.FindProperty("lostColor").colorValue =
    new Color(LabNotebookTheme.SubduedInk.r,
              LabNotebookTheme.SubduedInk.g,
              LabNotebookTheme.SubduedInk.b, 0.35f);
livesSO.ApplyModifiedPropertiesWithoutUndo();
```

### Step 6: Restyle the ComboBadge as a sticky note + timer bar

- [ ] **Step 6.1: Replace the existing ComboBadge block**

Find the ComboBadge construction (the wrapper + Visual + Label + timer fill). Replace it with the notebook version:

```csharp
// ComboBadge wrapper — always active, holds the script.
var comboWrap = new GameObject("ComboBadge", typeof(RectTransform));
comboWrap.transform.SetParent(canvasGO.transform, false);
var cwrt = (RectTransform)comboWrap.transform;
cwrt.anchorMin = new Vector2(0.5f, 1f);
cwrt.anchorMax = new Vector2(0.5f, 1f);
cwrt.pivot = new Vector2(0.5f, 1f);
cwrt.anchoredPosition = new Vector2(0, -260);
cwrt.sizeDelta = new Vector2(320, 130);

var comboVisual = BuildStickyNote(comboWrap.transform, "Visual",
    anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
    anchored: Vector2.zero, size: Vector2.zero,
    rotationDeg: LabNotebookTheme.HudCardRotationLeft);

var comboLabelGO = NewTMP(comboVisual, "Label", "Combo ×2!",
    LabNotebookTheme.ComboBadgeSize,
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
    anchored: new Vector2(0, -4), size: new Vector2(0, 70),
    color: LabNotebookTheme.InkRed, alignment: TextAlignmentOptions.Center);
ApplyCaveat(comboLabelGO);
comboLabelGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

// Timer bar background.
var timerBg = new GameObject("TimerBg",
    typeof(RectTransform), typeof(Image));
timerBg.transform.SetParent(comboVisual, false);
var tbgRT = (RectTransform)timerBg.transform;
tbgRT.anchorMin = new Vector2(0, 0);
tbgRT.anchorMax = new Vector2(1, 0);
tbgRT.pivot = new Vector2(0.5f, 0);
tbgRT.anchoredPosition = new Vector2(0, 8);
tbgRT.sizeDelta = new Vector2(-20, 8);
timerBg.GetComponent<Image>().color = new Color(0, 0, 0, 0.15f);

var timerFillGO = new GameObject("TimerFill",
    typeof(RectTransform), typeof(Image));
timerFillGO.transform.SetParent(timerBg.transform, false);
var tfRT = (RectTransform)timerFillGO.transform;
tfRT.anchorMin = Vector2.zero;
tfRT.anchorMax = Vector2.one;
tfRT.offsetMin = Vector2.zero;
tfRT.offsetMax = Vector2.zero;
var timerFillImg = timerFillGO.GetComponent<Image>();
timerFillImg.color = LabNotebookTheme.InkRed;
timerFillImg.type = Image.Type.Filled;
timerFillImg.fillMethod = Image.FillMethod.Horizontal;
timerFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
timerFillImg.fillAmount = 1f;

var badgeView = comboWrap.AddComponent<ComboBadgeView>();
var badgeSO = new SerializedObject(badgeView);
SetRef(badgeSO, "label", comboLabelGO.GetComponent<TMP_Text>());
SetRef(badgeSO, "root", comboVisual.gameObject);
SetRef(badgeSO, "timerFill", timerFillImg);
badgeSO.ApplyModifiedPropertiesWithoutUndo();
comboVisual.gameObject.SetActive(false);
```

### Step 7: Rebuild `ComboPopup.prefab` with notebook styling

- [ ] **Step 7.1: Add a helper `RebuildComboPopupPrefab` in `SceneSetup`**

```csharp
private static void RebuildComboPopupPrefab()
{
    const string path = "Assets/Prefabs/ComboPopup.prefab";
    var caveat = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
    if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        AssetDatabase.DeleteAsset(path);

    var root = new GameObject("ComboPopup", typeof(RectTransform));
    var rt = (RectTransform)root.transform;
    rt.sizeDelta = new Vector2(300, 100);

    var tmp = root.AddComponent<TextMeshProUGUI>();
    tmp.text = "+1";
    tmp.font = caveat;
    tmp.fontSize = LabNotebookTheme.ComboPopupSize;
    tmp.alignment = TextAlignmentOptions.Center;
    tmp.color = LabNotebookTheme.InkRed;
    tmp.fontStyle = FontStyles.Bold;

    var popup = root.AddComponent<ComboPopup>();
    var so = new SerializedObject(popup);
    so.FindProperty("label").objectReferenceValue = tmp;
    so.ApplyModifiedPropertiesWithoutUndo();

    PrefabUtility.SaveAsPrefabAsset(root, path);
    Object.DestroyImmediate(root);
}
```

- [ ] **Step 7.2: Call it in `Execute`**

Near the start of `Execute()` (after `StartScreenAssetsSetup.Execute()`), add:

```csharp
RebuildComboPopupPrefab();
```

### Step 8: Add an `ApplyCaveat` helper

- [ ] **Step 8.1: Add helper**

Anywhere in the class:

```csharp
private static TMP_FontAsset _cachedCaveat;
private static TMP_FontAsset LoadCaveat()
{
    if (_cachedCaveat == null)
        _cachedCaveat = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabNotebookTheme.FontAssetPath);
    return _cachedCaveat;
}
private static void ApplyCaveat(GameObject tmpGO)
{
    var tmp = tmpGO.GetComponent<TMP_Text>();
    if (tmp != null && LoadCaveat() != null) tmp.font = LoadCaveat();
}
```

### Step 9: Verify compile + run + commit

- [ ] **Step 9.1: Verify compile**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 9.2: Run SceneSetup**

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/SceneSetup.cs
  methodName: Execute
```

Expected: existing success message (`... scene saved to Assets/Scenes/MainScene.unity`).

- [ ] **Step 9.3: Visually verify in Play Mode**

```
mcp__coplay-mcp__open_scene
  scene_path: Scenes/MainScene
mcp__coplay-mcp__play_game
```

Wait 1-2 seconds, then `mcp__coplay-mcp__capture_scene_object`. Verify:
- Top-left: cream sticky-note with "SCORE" label + value, tilted left.
- Below score: smaller card "★ Player: <name>" (red star + dark text).
- Top-right: cream card with "LIVES" + three red ♥ glyphs, tilted right.
- Mid-top (when slicing): red Caveat "Combo ×N!" sticky note + timer fill bar.
- Slice popups: red Caveat text floating up.
- Game-over: existing panel (will be restyled in Task 5).

- [ ] **Step 9.4: Commit**

```bash
git add Assets/Editor/SceneSetup.cs Assets/Scenes/MainScene.unity Assets/Prefabs/ComboPopup.prefab
git commit -m "feat(ui): notebook-style HUD — sticky-note cards over the sky"
```

---

## Task 5: Restyle the game-over panel

**Goal:** Replace the existing flat dim-panel game-over view with a tilted notebook page card (graph paper, margin line, Caveat title, NEW BEST! stamp, outlined buttons).

**Files:**
- Modify: `Assets/Editor/SceneSetup.cs` (the GameOver / Panel block)

### Step 1: Replace the GameOver block in `Execute()`

- [ ] **Step 1.1: Find and replace the existing GameOver / Panel construction**

The current SceneSetup has a `GameOver` wrapper and a `Panel` child with FinalScore + RestartButton. Replace the entire GameOver block (down through the GameOverView wiring) with:

```csharp
// GameOver wrapper — always active, holds the script.
var gameOverHost = new GameObject("GameOver", typeof(RectTransform));
gameOverHost.transform.SetParent(canvasGO.transform, false);
var gohRT = (RectTransform)gameOverHost.transform;
gohRT.anchorMin = Vector2.zero;
gohRT.anchorMax = Vector2.one;
gohRT.offsetMin = Vector2.zero;
gohRT.offsetMax = Vector2.zero;

// Dim overlay covering the whole canvas behind the panel.
var dim = new GameObject("DimOverlay", typeof(RectTransform), typeof(Image));
dim.transform.SetParent(gameOverHost.transform, false);
var dimRT = (RectTransform)dim.transform;
dimRT.anchorMin = Vector2.zero;
dimRT.anchorMax = Vector2.one;
dimRT.offsetMin = Vector2.zero;
dimRT.offsetMax = Vector2.zero;
dim.GetComponent<Image>().color = LabNotebookTheme.GameOverDim;

// The notebook page card (centered, slightly rotated).
var panel = BuildStickyNote(gameOverHost.transform, "Panel",
    anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
    pivot: new Vector2(0.5f, 0.5f),
    anchored: Vector2.zero, size: new Vector2(800, 1100),
    rotationDeg: -1f);

// Margin line on the panel.
var pageMargin = new GameObject("MarginLine",
    typeof(RectTransform), typeof(Image));
pageMargin.transform.SetParent(panel, false);
var pmRT = (RectTransform)pageMargin.transform;
pmRT.anchorMin = new Vector2(0, 0);
pmRT.anchorMax = new Vector2(0, 1);
pmRT.pivot = new Vector2(0, 0.5f);
pmRT.anchoredPosition = new Vector2(60, 0);
pmRT.sizeDelta = new Vector2(2.5f, 0);
pageMargin.GetComponent<Image>().color = LabNotebookTheme.MarginRed;

// Title.
var goTitle = NewTMP(panel, "Title", "Run Complete",
    LabNotebookTheme.GameOverTitleSize,
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
    anchored: new Vector2(0, -80), size: new Vector2(0, 160),
    color: LabNotebookTheme.InkDark, alignment: TextAlignmentOptions.Center);
ApplyCaveat(goTitle);
goTitle.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

// Final score.
var finalScoreGO = NewTMP(panel, "FinalScore", "Score: 0",
    LabNotebookTheme.GameOverScoreSize,
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), pivot: new Vector2(0.5f, 1),
    anchored: new Vector2(0, -280), size: new Vector2(0, 100),
    color: LabNotebookTheme.InkRed, alignment: TextAlignmentOptions.Center);
ApplyCaveat(finalScoreGO);
finalScoreGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

// NEW BEST! stamp (rotated red text with red border Image).
var newBestWrap = new GameObject("NewBestFlag", typeof(RectTransform), typeof(Image));
newBestWrap.transform.SetParent(panel, false);
var nbRT = (RectTransform)newBestWrap.transform;
nbRT.anchorMin = new Vector2(0.5f, 1);
nbRT.anchorMax = new Vector2(0.5f, 1);
nbRT.pivot = new Vector2(0.5f, 1);
nbRT.anchoredPosition = new Vector2(0, -400);
nbRT.sizeDelta = new Vector2(280, 70);
nbRT.localRotation = Quaternion.Euler(0, 0, LabNotebookTheme.StampRotation);
newBestWrap.GetComponent<Image>().color = new Color(0, 0, 0, 0);
var newBestOutline = newBestWrap.AddComponent<Outline>();
newBestOutline.effectColor = LabNotebookTheme.InkRed;
newBestOutline.effectDistance = new Vector2(2, -2);
var newBestText = NewTMP(newBestWrap.transform, "Text", "NEW BEST!",
    LabNotebookTheme.NewBestStampSize,
    anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
    anchored: Vector2.zero, size: Vector2.zero,
    color: LabNotebookTheme.InkRed, alignment: TextAlignmentOptions.Center);
ApplyCaveat(newBestText);
newBestText.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

// "next?" divider.
var nextDiv = BuildDividerOnPanel(panel, "next?", new Vector2(0, -540));

// Restart button.
var restartBtn = BuildPanelButton(panel, "RestartButton", "Try Again",
    new Vector2(0, -640), LabNotebookTheme.InkDark);

// Exit button.
var exitBtn = BuildPanelButton(panel, "QuitButton", "Pack Up",
    new Vector2(0, -780), LabNotebookTheme.InkRed);

// Attach GameOverView and wire references.
var goView = gameOverHost.AddComponent<GameOverView>();
var goSO = new SerializedObject(goView);
SetRef(goSO, "panelRoot", panel.gameObject);
SetRef(goSO, "finalScoreLabel", finalScoreGO.GetComponent<TMP_Text>());
SetRef(goSO, "restartButton", restartBtn);
SetRef(goSO, "quitButton", exitBtn);
SetRef(goSO, "newBestFlag", newBestWrap);
SetRef(goSO, "spawner", spawner);
goSO.ApplyModifiedPropertiesWithoutUndo();
panel.gameObject.SetActive(false);
```

### Step 2: Add `BuildDividerOnPanel` + `BuildPanelButton` helpers

- [ ] **Step 2.1: Add helpers**

```csharp
private static RectTransform BuildDividerOnPanel(RectTransform parent, string label, Vector2 anchored)
{
    var wrap = new GameObject($"Divider_{label}", typeof(RectTransform));
    wrap.transform.SetParent(parent, false);
    var rt = (RectTransform)wrap.transform;
    rt.anchorMin = new Vector2(0, 1);
    rt.anchorMax = new Vector2(1, 1);
    rt.pivot = new Vector2(0.5f, 1);
    rt.anchoredPosition = anchored;
    rt.sizeDelta = new Vector2(-40, 50);

    var text = NewTMP(wrap.transform, "Text", label.ToUpperInvariant(),
        LabNotebookTheme.DividerSize,
        anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
        anchored: new Vector2(0, 4), size: Vector2.zero,
        color: LabNotebookTheme.SubduedInk, alignment: TextAlignmentOptions.Center);
    ApplyCaveat(text);

    var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
    underline.transform.SetParent(wrap.transform, false);
    var urt = (RectTransform)underline.transform;
    urt.anchorMin = new Vector2(0, 0);
    urt.anchorMax = new Vector2(1, 0);
    urt.pivot = new Vector2(0.5f, 0);
    urt.anchoredPosition = Vector2.zero;
    urt.sizeDelta = new Vector2(0, 1.5f);
    underline.GetComponent<Image>().color = new Color(
        LabNotebookTheme.GridBlue.r, LabNotebookTheme.GridBlue.g,
        LabNotebookTheme.GridBlue.b, 0.4f);

    return rt;
}

private static Button BuildPanelButton(RectTransform parent, string name, string label,
    Vector2 anchored, Color outlineColor)
{
    var wrap = new GameObject($"{name}Wrap", typeof(RectTransform));
    wrap.transform.SetParent(parent, false);
    var wrt = (RectTransform)wrap.transform;
    wrt.anchorMin = new Vector2(0.5f, 1);
    wrt.anchorMax = new Vector2(0.5f, 1);
    wrt.pivot = new Vector2(0.5f, 1);
    wrt.anchoredPosition = anchored;
    wrt.sizeDelta = new Vector2(600, 110);
    wrt.localRotation = Quaternion.Euler(0, 0, -0.5f);

    var shadow = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
    shadow.transform.SetParent(wrap.transform, false);
    var srt = (RectTransform)shadow.transform;
    srt.anchorMin = Vector2.zero;
    srt.anchorMax = Vector2.one;
    srt.offsetMin = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                -LabNotebookTheme.ShadowOffsetSmall);
    srt.offsetMax = new Vector2(LabNotebookTheme.ShadowOffsetSmall,
                                -LabNotebookTheme.ShadowOffsetSmall);
    shadow.GetComponent<Image>().color = outlineColor;

    var btnGO = new GameObject(name,
        typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
    btnGO.transform.SetParent(wrap.transform, false);
    var brt = (RectTransform)btnGO.transform;
    brt.anchorMin = Vector2.zero;
    brt.anchorMax = Vector2.one;
    brt.offsetMin = Vector2.zero;
    brt.offsetMax = Vector2.zero;
    btnGO.GetComponent<Image>().color = LabNotebookTheme.PaperCream;
    var outline = btnGO.GetComponent<Outline>();
    outline.effectColor = outlineColor;
    outline.effectDistance = new Vector2(2.5f, -2.5f);

    var labelGO = NewTMP(btnGO.transform, "Label", label,
        LabNotebookTheme.GameOverButtonSize,
        anchorMin: Vector2.zero, anchorMax: Vector2.one, pivot: new Vector2(0.5f, 0.5f),
        anchored: Vector2.zero, size: Vector2.zero,
        color: outlineColor, alignment: TextAlignmentOptions.Center);
    ApplyCaveat(labelGO);
    labelGO.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;

    return btnGO.GetComponent<Button>();
}
```

### Step 3: Verify + run + commit

- [ ] **Step 3.1: Verify compile**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 3.2: Run SceneSetup**

`mcp__coplay-mcp__execute_script` → `Assets/Editor/SceneSetup.cs` → `Execute`.

- [ ] **Step 3.3: Verify in Play**

Play MainScene, let three cubes fall to game over. Verify:
- Dim overlay over the sky.
- Centered notebook card (rotated −1°) with cream + graph paper.
- Red margin line at left of the card.
- "Run Complete" title in dark ink Caveat.
- "Score: N" in red Caveat below.
- "NEW BEST!" red stamp rotated −8° (visible if score > previous best).
- Outlined "Try Again" (dark) and "Pack Up" (red) buttons with offset shadows.

- [ ] **Step 3.4: Commit**

```bash
git add Assets/Editor/SceneSetup.cs Assets/Scenes/MainScene.unity
git commit -m "feat(ui): notebook-style game-over panel with NEW BEST! stamp"
```

---

## Task 6: Final playtest + cleanup

**Goal:** Run the whole flow end-to-end and confirm all three screens share the notebook identity.

This task does no code work — manual verification.

- [ ] **Step 1: Run all setup scripts in order**

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/StartScreenAssetsSetup.cs
  methodName: Execute

mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/StartSceneSetup.cs
  methodName: Execute

mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/SceneSetup.cs
  methodName: Execute
```

Expected: idempotent success messages.

- [ ] **Step 2: Open StartScene, press Play**

Verify the start-screen visual matches the spec mockup:
- Graph paper + margin line.
- Caveat title rotated.
- Subtitle, best score, dividers.
- 6 cube rows with offset-shadow icons + role badges.
- Underlined nickname input.
- Outlined start button + tap-here arrow.

- [ ] **Step 3: Type a nickname → click Start**

Verify MainScene loads. Check HUD:
- ScorePanel sticky-note top-left.
- "★ Player: <nickname>" below.
- LivesRow sticky-note top-right with 3 red hearts.
- Sky + cubes look unchanged.

- [ ] **Step 4: Slice cubes, watch combos, lose**

Verify:
- "+1", "+2 ×2!" etc. floaters in red Caveat.
- Combo badge appears as sticky-note when multi-hit.
- Game-over notebook page on death.

- [ ] **Step 5: Click Try Again, then Pack Up (or close Play)**

Verify Restart resumes gameplay; Exit stops Play Mode in Editor.

- [ ] **Step 6: Tune + commit if needed**

If anything needs visual adjustment (font sizes, anchor positions, colors), edit `LabNotebookTheme.cs` constants, re-run the setup scripts, commit the result:

```bash
git status
git add Assets ProjectSettings
git commit -m "tune: post-playtest notebook-theme adjustments"
```

If nothing needed tuning, skip this step.

---

## Done criteria

- All 35 EditMode tests still pass (no signatures changed).
- `Assets/Fonts/Caveat-Bold.ttf`, `Caveat-Bold SDF.asset`, `Assets/Textures/GraphPaper.png` exist.
- StartScene shows the lab-notebook layout per the mockup.
- MainScene HUD elements are sticky-note cards in the corners (Score, Nickname, Lives).
- Combo popups and combo badge render in Caveat red ink.
- Game over panel is a centered notebook page with NEW BEST! stamp and outlined buttons.
- No Console errors during play.
