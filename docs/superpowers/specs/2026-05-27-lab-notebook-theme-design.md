# Lab Notebook Theme — Design Spec

**Date:** 2026-05-27
**Status:** Approved by user, ready for implementation planning
**Project:** open-ninja
**Builds on:** all previous specs, especially `2026-05-27-start-screen-design.md`

---

## 1. Summary

Replace the flat dark-navy + Helvetica look across the start screen, gameplay HUD, and game-over panel with a unified **lab notebook** theme: cream graph paper, hand-lettered title and labels (Caveat font), red ink margin line, hard-offset shadows, and circled scribble accents. The gameplay backdrop (procedural sky + walls + cubes) stays as-is — the notebook style lives in the UI Canvas layer that floats over it.

---

## 2. Goals & Non-Goals

**Goals**
- Consistent lab-notebook aesthetic across StartScene + MainScene HUD + game-over panel.
- Single source of truth for palette, fonts, sprite refs (a small `LabNotebookTheme` static class).
- Asset pipeline that fetches the Caveat font + generates the graph-paper background reproducibly.
- No runtime script changes: `StartScreen`, `CubeInfoTable`, `CubeInfoRow`, `NicknameHud`, `GameOverView`, `ScoreView`, `LivesView`, `ComboBadgeView`, `ComboPopup`, `ComboPopupSpawner`, `GameManager` all unchanged.

**Non-Goals**
- Theming the gameplay scene's sky / walls / cubes — those are visually decoupled.
- Customizable theme at runtime (no settings screen).
- Animations beyond Unity defaults (hover, button press flash). The popup "scribble" effect uses a static sprite, not animated ink.
- Localization of any new label text.
- Audio.

---

## 3. Architecture

A static `LabNotebookTheme` class holds the palette, font references, and sprite asset paths. Two existing Editor setup scripts (`StartSceneSetup.cs`, `SceneSetup.cs`) read from it. A new one-shot `StartScreenAssetsSetup.cs` fetches the Caveat font and generates the procedural background texture. The `CubeInfoRow.prefab` is rebuilt by `StartSceneSetup` (no change to its C# script). All MainScene UI elements are restyled by `SceneSetup`.

```
                ┌────────────────────────────────────────┐
                │  LabNotebookTheme (static class)        │
                │   Color PaperCream, InkDark, InkRed... │
                │   const float TitleSize, BadgeSize...  │
                │   string FontAssetPath                  │
                │   string GraphPaperSpritePath           │
                └─────────────────┬──────────────────────┘
                                  │
        ┌─────────────────────────┴──────────────────────────┐
        │                                                     │
┌───────▼────────────────────┐              ┌─────────────────▼─────────────┐
│ StartSceneSetup.cs (MOD)   │              │ SceneSetup.cs (MOD)           │
│ rebuilds StartScene + row  │              │ restyles MainScene HUD +      │
│ prefab in notebook style   │              │ game-over panel               │
└────────────────────────────┘              └───────────────────────────────┘

       ┌──────────────────────────────────────────────┐
       │ StartScreenAssetsSetup.cs (NEW, one-shot)     │
       │ - downloads Assets/Fonts/Caveat-Bold.ttf      │
       │ - builds Caveat-Bold SDF TMP_FontAsset        │
       │ - generates Assets/Textures/GraphPaper.png    │
       └──────────────────────────────────────────────┘
```

Runtime: no code paths change. Only the Inspector-set fields (fonts, colors, sprite refs) and the scene hierarchies (panel structure of the game-over view, sticky-note wrappers around HUD elements) shift.

---

## 4. `LabNotebookTheme` static class

`Assets/Scripts/Util/LabNotebookTheme.cs`

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

        // Game-over overlay (above gameplay)
        public static readonly Color GameOverDim = new Color(0f, 0f, 0f, 0.55f);

        // ---- Typography (font sizes in canvas units; portrait ref 1080x1920) ----
        public const float TitleSize        = 144f; // start-screen Material Ninja
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
        public const float HudLabelSize     = 22f;  // "SCORE", "LIVES" — small uppercase
        public const float HudValueSize     = 60f;  // the actual score number
        public const float HudNicknameSize  = 36f;
        public const float HudHeartSize     = 56f;
        public const float ComboBadgeSize   = 48f;
        public const float ComboPopupSize   = 64f;

        // Game-over panel
        public const float GameOverTitleSize = 96f;
        public const float GameOverScoreSize = 72f;
        public const float NewBestStampSize  = 40f;
        public const float GameOverButtonSize = 56f;

        // ---- Geometry ----
        public const float MarginLineX      = 110f;  // x in canvas units (1080 wide)
        public const float TitleRotation    = -2f;
        public const float ButtonRotation   = -1.5f;
        public const float BadgeRotation    = -2f;
        public const float HudCardRotationLeft  = -3f;
        public const float HudCardRotationRight = 2f;
        public const float StampRotation    = -8f;
        public const float ShadowOffsetSmall = 4f;  // swatches, HUD cards
        public const float ShadowOffsetBig   = 8f;  // start-button, game-over card

        // ---- Asset paths ----
        public const string FontAssetPath        = "Assets/Fonts/Caveat-Bold SDF.asset";
        public const string GraphPaperSpritePath = "Assets/Textures/GraphPaper.png";
    }
}
```

This is data, not behavior. It compiles into any assembly that needs it (runtime or Editor).

---

## 5. Asset pipeline — `StartScreenAssetsSetup.cs`

`Assets/Editor/StartScreenAssetsSetup.cs` — one-shot, idempotent, called at the top of both `StartSceneSetup.Execute()` and `SceneSetup.Execute()`.

### 5a. Caveat font

If `Assets/Fonts/Caveat-Bold.ttf` is missing:
- Fetch from Google Fonts via `UnityWebRequest` to:
  `https://github.com/google/fonts/raw/main/ofl/caveat/Caveat%5Bwght%5D.ttf`
  (Google's official GitHub mirror of the OFL fonts. Stable URL, no API key needed.)
- Save as `Assets/Fonts/Caveat-Bold.ttf`.
- Wait for `AssetDatabase.ImportAsset` to register it as a `Font`.

Then if `Assets/Fonts/Caveat-Bold SDF.asset` is missing:
- Load the Font.
- Call `TMP_FontAsset.CreateFontAsset(font, samplingPointSize: 90, atlasPadding: 9, renderMode: GlyphRenderMode.SDFAA, atlasWidth: 1024, atlasHeight: 1024)`.
- Add extra Unicode characters: `★` (U+2605), `↗` (U+2197), `—` (U+2014), `♥` (U+2665) via `TMP_FontAsset.TryAddCharacters("★↗—♥")`.
- Save the asset to `Assets/Fonts/Caveat-Bold SDF.asset`.

### 5b. Graph paper PNG

If `Assets/Textures/GraphPaper.png` is missing:
- Generate a 512×512 RGBA32 texture in memory:
  - Fill with `LabNotebookTheme.PaperCream`.
  - For every 16th row and column, draw a 1px line in `LabNotebookTheme.GridBlue` (alpha 0.10).
  - Optional: skip the margin-line bake from the PNG — the margin is per-screen and varies in position. It's a separate Image element.
- Save with `texture.EncodeToPNG()` + `File.WriteAllBytes`.
- `AssetDatabase.ImportAsset(path)` and set the import settings:
  - `textureType = Sprite`
  - `wrapMode = Repeat`
  - `filterMode = Point` (the grid lines should not blur)
  - `maxTextureSize = 512`
  - `mipmapEnabled = false`
  - `npotScale = None`
  - `spritePixelsPerUnit = 100`

Output: a tileable sprite that any UI Image can use.

---

## 6. Start Screen restyle

`StartSceneSetup.cs` rebuilds `StartScene.unity` with the notebook theme. Inspector references (already wired by Task 5 of the previous start-screen plan) remain unchanged — only the visual presentation changes.

### 6a. Scene structure (changes from current)

```
StartScene
├── Main Camera                (orthographic, clear color = PaperCream)
├── EventSystem
└── StartCanvas
    ├── Background (full-screen Image, GraphPaper.png tiled)
    ├── MarginLine (thin Image, MarginLineX at full height, MarginRed)
    ├── Title         "Material Ninja" (Caveat Bold, 144pt, InkDark,
    │                                   rotated -2°, two lines)
    ├── Subtitle      "— field notes vol. 1 —" (Caveat, 36pt, SubduedInk, rotated -1°)
    ├── BestScoreLabel "★ Best: 0"            (Caveat, 48pt, InkRed, rotated -1°)
    ├── SpecimensDivider "specimens"            (Caveat, 28pt, uppercase, SubduedInk
    │                                            + thin Image underline)
    ├── CubeInfoTable
    │   └── RowContainer
    │       └── 6× CubeInfoRow (rebuilt prefab, see §6b)
    ├── SubjectDivider "subject"                (Caveat, 28pt, uppercase, with underline)
    ├── NicknameLabel  "Nickname:"              (Caveat, 36pt, InkDark)
    ├── NicknameInput  (TMP_InputField, no Image bg, underline as a separate Image)
    │   ├── TextArea / Text (Caveat, 64pt, InkDark)
    │   └── Placeholder "your name here..."     (Caveat italic, 64pt, GridBlue→0.4)
    ├── StartButton    "Start!" (Caveat, 88pt, outlined rect, ButtonRotation -1.5°)
    │   └── ShadowPanel (Image, behind, InkDark, offset 8,−8)
    └── TapHereArrow   "↗ tap here" (Caveat, 36pt, InkRed, rotated -8°,
                                     anchored next to button)
```

### 6b. `CubeInfoRow.prefab` rebuild

Same script (`CubeInfoRow.cs`) — unchanged. The prefab's visual structure changes:

```
CubeInfoRow (HorizontalLayoutGroup)
├── Icon (Image — albedo sprite)              80×80, with offset shadow
│   └── IconShadow (sibling, InkDark @ 0.4, offset 4,−4)
├── Name  (TMP_Text Caveat, 56pt, InkDark)
├── Points (TMP_Text Caveat, 48pt — color tied to role: InkAmber/InkRed/InkDark)
└── RoleBadgeWrap (RectTransform, rotated -2°)
    └── RoleBadge (TMP_Text Caveat, 24pt, ink-colored border via Image,
                   text uppercase: NORMAL/BONUS/DANGER)
```

The badge "border" is achieved via an Image with a thin outline sprite (or simply an Image with 1px alpha mask + the text on top). Color matches the role's ink (`InkGreen` / `InkAmber` / `InkRed`).

### 6c. "↗ tap here" annotation

A child of StartCanvas anchored to the right of the StartButton. TMP_Text "↗ tap here" in Caveat 36pt, `InkRed`, rotated −8°. Active at all times once the Start button is interactable. (If Start is not interactable, fade the arrow to 40% alpha so it's a hint without overpromising.)

---

## 7. MainScene HUD restyle

The gameplay sky / walls / cubes are unchanged. The Canvas children get themed.

### 7a. ScorePanel → "Score" sticky-note

Existing `ScorePanel` GameObject (with `ScoreView` script attached) becomes a small notebook card:

```
ScorePanel (RectTransform, rotated -3°, anchored top-left)
├── PaperBackground (Image, GraphPaper.png, shadow Image behind)
├── Label "SCORE" (Caveat 22pt, SubduedInk, uppercase)
└── ValueLabel  (TMP_Text, Caveat Bold, 60pt, InkDark)  ← ScoreView.label refers here
```

`ScoreView.cs` already references a `TMP_Text label` field — `SceneSetup` wires it to `ValueLabel`. No script change.

### 7b. NicknameHud → "★ Player: Alice" red note

Existing NicknameHud GameObject (currently a plain TMP under ScorePanel):
- Backdrop: small Image with GraphPaper.png, rotated -3°, anchored under ScorePanel.
- Text "★ Player: Alice" in Caveat 36pt, `InkRed` for the star + `InkDark` for the name (or just all `InkDark` with a separate `★` Image — simpler is one TMP with a colored rich-text span).
- `NicknameHud.cs` already produces the formatted string via `string.Format(format, nick)`. We can change the format to `"<color=#c83232>★</color> Player: {0}"` since TMP supports rich text by default. No script change.

### 7c. LivesRow → notebook-card with handwritten hearts

```
LivesRow (RectTransform, rotated +2°, anchored top-right)
├── PaperBackground (Image, GraphPaper.png, shadow Image behind)
├── Label "LIVES" (Caveat 22pt, SubduedInk, uppercase)
└── HeartsRow (HorizontalLayoutGroup with 3 children)
    └── 3× Heart (TMP_Text "♥", Caveat 56pt, InkRed when alive / SubduedInk-faded when lost)
```

`LivesView.cs` already accepts `Graphic[]` and recolors them. The Graphic is a `TMP_Text` (which inherits from Graphic). Wiring stays the same.

### 7d. ComboBadge → scribbled note

```
ComboBadge (RectTransform top-center, anchored y=-200)
└── Visual (the existing child that gets toggled on/off by ComboBadgeView)
    ├── PaperBackground (Image, GraphPaper.png, rotated -2°, shadow Image behind)
    ├── Label (TMP_Text "Combo ×N!", Caveat 48pt, InkRed, bold)
    └── TimerFillBackground + TimerFill (Image, filled bar in InkRed)
```

`ComboBadgeView.cs` already drives `root.SetActive(show)` and writes the label text. We pass the new structure into the same fields. No script change.

### 7e. ComboPopup → red Caveat with scribble outline

The `ComboPopup.prefab` is rebuilt with:
- TMP_Text in Caveat 64pt, `InkRed`, slightly bold.
- A child Image behind the text: a **scribble ellipse sprite** (procedurally generated PNG, ~3 alternating ink strokes around the text). Optional in v1 — even just the rotated red Caveat text reads as "ink." If easy, include a static sprite.

`ComboPopup.cs` already handles fade + float-up animation; only the visuals change.

### 7f. Game-over panel restyle

The current GameOverPanel becomes a centered notebook card overlay:

```
GameOver (RectTransform, fullscreen, GameOverView script — unchanged)
├── DimOverlay (Image, fullscreen, GameOverDim color 0,0,0,0.55)
└── Panel (RectTransform centered, rotated -1°, scale 0.78 of screen)
    ├── ShadowPanel (Image, InkDark, offset 8,-8 behind the panel)
    ├── PaperBackground (Image, GraphPaper.png, scale 1)
    ├── MarginLine (Image, MarginRed, vertical, 18px from left edge of panel)
    ├── TitleLabel "Run Complete" (Caveat Bold 96pt, InkDark, centered)
    ├── FinalScoreLabel "Score: 42" (Caveat 72pt, InkRed, centered)  ← GameOverView.finalScoreLabel
    ├── NewBestStamp "NEW BEST!" (Caveat 40pt, InkRed, rotated -8°,
    │                              with InkRed border Image)         ← GameOverView.newBestFlag
    ├── Divider "next?" (Caveat 28pt, SubduedInk, uppercase, underline)
    ├── RestartButton "Try Again" (Caveat 56pt, outlined InkDark, shadow)  ← restartButton
    └── ExitButton    "Pack Up" (Caveat 56pt, outlined InkRed, shadow)     ← quitButton
```

`GameOverView.cs` is unchanged — it just toggles `panelRoot.SetActive()` and writes the score / NEW BEST flag.

---

## 8. Files

```
NEW   Assets/Scripts/Util/LabNotebookTheme.cs
NEW   Assets/Editor/StartScreenAssetsSetup.cs       Caveat downloader + GraphPaper generator
NEW   Assets/Fonts/Caveat-Bold.ttf                  fetched once, ~80 KB
NEW   Assets/Fonts/Caveat-Bold SDF.asset            generated TMP Font Asset
NEW   Assets/Textures/GraphPaper.png                generated background
NEW   Assets/Prefabs/SliceScribble.prefab           (only if §7e includes the scribble sprite)
MOD   Assets/Editor/StartSceneSetup.cs              full rewrite of visual styling, reads LabNotebookTheme
MOD   Assets/Editor/SceneSetup.cs                   restyle Canvas children (HUD + game-over panel)
MOD   Assets/Prefabs/CubeInfoRow.prefab             rebuilt by StartSceneSetup, notebook-styled
MOD   Assets/Prefabs/ComboPopup.prefab              rebuilt by SceneSetup, notebook-styled
```

No changes to any runtime script.

---

## 9. Tests

No new automated tests. Existing 35 EditMode tests continue to pass (no signature changes).

Visual verification:
1. Run `StartScreenAssetsSetup.Execute` — verify `Assets/Fonts/Caveat-Bold.ttf`, `Caveat-Bold SDF.asset`, `Assets/Textures/GraphPaper.png` all exist.
2. Run `StartSceneSetup.Execute` — open `StartScene`, press Play, verify the full notebook look matches the mockup.
3. Run `SceneSetup.Execute` — open `MainScene`, press Play, slice cubes, verify:
   - Sticky-note cards in corners (Score top-left, Lives top-right, NicknameHud under Score).
   - ComboBadge appears as a scribbled note with red Caveat text + timer bar.
   - Combo popups float up in red Caveat.
   - Game over → notebook page overlay centered with NEW BEST stamp, Try Again + Pack Up buttons.

---

## 10. Edge cases

| Case | Resolution |
|---|---|
| Caveat font download fails (network) | `StartScreenAssetsSetup` catches the exception, logs an error, falls back to LiberationSans. The setup still completes; the look is just less personality. Designer can re-run later. |
| `★` / `↗` glyphs missing from baked font atlas | `TMP_FontAsset.TryAddCharacters` runs after creation; if it still fails (broken atlas), the glyph renders as a `?` square. Acceptable — the labels still read. |
| Rotation breaks layout groups | Rotated elements are wrapped in a plain `RectTransform` parent that the layout group sees as a normal sibling. Rotation is applied to the child. Works around Unity's behavior of layouts measuring rotated children incorrectly. |
| Game-over panel covers the HUD | Intentional. The dim overlay sits at the same Canvas depth as the HUD; child order makes the panel render on top. Hearts/Score etc. dim through the overlay (alpha 0.55 over them is ~45% opacity remaining). |
| First launch — graph paper sprite import not finished before scene rebuild | `StartScreenAssetsSetup.Execute` ends with `AssetDatabase.SaveAssets() + AssetDatabase.Refresh()`. The Editor will block on these synchronously. After return, the sprite is loadable. |
| Player has no nickname (`PlayerSession.Nickname == ""`) | `NicknameHud` already hides itself in that case (existing behavior). The sticky-note paper backdrop hides with it. |

---

## 11. Out of scope

- Touch / mouse hover animations (Unity's default Button transition is fine).
- Animated "ink stroke" effect on the START button (would require Shader Graph or a separate animator).
- Per-row hover preview of 3D cube model (we use the static albedo sprite).
- Localization.
- Re-themable runtime (no settings panel to switch palettes).
- Audio.

---

## 12. Open questions for implementation

- Whether the "↗ tap here" arrow stays after first interaction or hides once the nickname is filled. Default: stays. Easy to switch behavior later.
- Whether the ComboPopup gets a scribble outline image or just relies on the red Caveat text being eye-catching enough. Default: text-only for v1; add scribble sprite as a follow-up if needed.
- Exact text on the game-over title ("Run Complete" vs "Game Over" vs "Study Complete"). Defaults to "Run Complete" per the mockup; trivial to change.
- Exact text on the buttons ("Try Again" / "Pack Up" vs "Restart" / "Exit"). Defaults to playful copy from the mockup.
