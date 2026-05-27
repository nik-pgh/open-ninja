# Start Screen — Design Spec

**Date:** 2026-05-27
**Status:** Approved by user, ready for implementation planning
**Project:** open-ninja
**Builds on:** all previous specs (swipe-cube-arcade, material-cubes, visual-realism, mobile aspect)

---

## 1. Summary

Add a dedicated start screen scene that runs before gameplay. Players type a nickname, see a table of all six cube materials with their roles and point values, see their best score, and tap **Start** to enter `MainScene`. The nickname persists between launches via `PlayerPrefs` and is displayed in the game HUD. Best score is updated automatically on game over. The game-over panel gains an **Exit** button alongside Restart.

---

## 2. Goals & Non-Goals

**Goals**
- A separate `StartScene` (Unity build index 0) loaded at launch.
- Per-cube reference table on the start screen so players can read the rules at a glance.
- Persistent nickname + best score across launches via `PlayerPrefs`.
- Player nickname visible during gameplay in the HUD.
- Exit button on game-over panel.

**Non-Goals**
- Leaderboards / online score sync. Best score is local-only.
- Tutorial / interactive how-to-play.
- Settings panel (audio sliders, etc.) — that's the audio spec.
- Animated scene transitions. Cuts only.
- "Back to start screen" button mid-game — Restart keeps the player in `MainScene`.

---

## 3. Architecture

A static `PlayerSession` class wraps two PlayerPrefs-backed properties (`Nickname`, `BestScore`). Both scenes read it directly; no DontDestroyOnLoad, no ScriptableObject asset. The start screen is a single `StartCanvas` containing a vertical stack of UI elements built by an Editor-only setup script (`StartSceneSetup.cs`), matching the pattern established by `SceneSetup.cs` for `MainScene`.

```
                            App launch
                                 ↓
                       SceneManager loads scene 0
                                 ↓
                         StartScene
        ┌────────────────────────────────────────────────┐
        │  Main Camera (orthographic, dark navy clear)   │
        │  EventSystem                                   │
        │  Canvas (Screen Space Overlay, 1080x1920 ref)  │
        │   ├─ Title       "MATERIAL NINJA"              │
        │   ├─ BestScore   "Best: 42"                    │
        │   ├─ CubeInfoTable (RectTransform + VLG)       │
        │   │    └─ 6× CubeInfoRow (prefab)              │
        │   ├─ NicknameLabel + NicknameInput             │
        │   └─ StartButton                               │
        └────────────────────────────────────────────────┘
                                 ↓
                       Click Start → SceneManager.LoadScene("MainScene")
                                 ↓
                          MainScene (today)
        ┌────────────────────────────────────────────────┐
        │  + NicknameHud reads PlayerSession.Nickname    │
        │  + GameManager writes BestScore on game over   │
        │  + GameOverView gains Exit button              │
        └────────────────────────────────────────────────┘
```

---

## 4. `PlayerSession` static class

`Assets/Scripts/Core/PlayerSession.cs` — the only data exchange between the two scenes.

```csharp
using System;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// PlayerPrefs-backed app-wide session state. Static because there are no
    /// per-instance lifetimes; both scenes read/write the same backing store.
    /// </summary>
    public static class PlayerSession
    {
        private const string NicknameKey  = "OpenNinja.Nickname";
        private const string BestScoreKey = "OpenNinja.BestScore";
        private const int    NicknameMaxChars = 16;

        public static event Action<int> OnBestScoreChanged;

        public static string Nickname
        {
            get => PlayerPrefs.GetString(NicknameKey, "");
            set
            {
                string sanitized = (value ?? string.Empty).Trim();
                if (sanitized.Length > NicknameMaxChars)
                    sanitized = sanitized.Substring(0, NicknameMaxChars);
                PlayerPrefs.SetString(NicknameKey, sanitized);
                PlayerPrefs.Save();
            }
        }

        public static int BestScore
        {
            get => PlayerPrefs.GetInt(BestScoreKey, 0);
            set
            {
                int clamped = Mathf.Max(0, value);
                PlayerPrefs.SetInt(BestScoreKey, clamped);
                PlayerPrefs.Save();
                OnBestScoreChanged?.Invoke(clamped);
            }
        }

        /// <summary>Convenience: only writes if the candidate beats the current best.</summary>
        public static bool TrySetBestScore(int candidate)
        {
            if (candidate <= BestScore) return false;
            BestScore = candidate;
            return true;
        }
    }
}
```

---

## 5. Scripts

### New files

| File | Lines | Purpose |
|---|---|---|
| `Assets/Scripts/Core/PlayerSession.cs` | ~50 | session state (above) |
| `Assets/Scripts/UI/StartScreen.cs` | ~80 | StartScene root MonoBehaviour |
| `Assets/Scripts/UI/CubeInfoTable.cs` | ~60 | spawns rows from a CubeMaterial[] |
| `Assets/Scripts/UI/CubeInfoRow.cs` | ~50 | one-row prefab Bind(CubeMaterial) |
| `Assets/Scripts/UI/NicknameHud.cs` | ~30 | shows PlayerSession.Nickname in MainScene |
| `Assets/Editor/StartSceneSetup.cs` | ~250 | builds StartScene from scratch (re-runnable) |

### `StartScreen.cs`

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenNinja
{
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bestScoreLabel;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button startButton;
        [SerializeField] private CubeInfoTable infoTable;
        [SerializeField] private string gameTitle = "MATERIAL NINJA";
        [SerializeField] private string mainSceneName = "MainScene";

        private void Awake()
        {
            if (titleLabel != null) titleLabel.text = gameTitle;
            if (bestScoreLabel != null) bestScoreLabel.text = $"Best: {PlayerSession.BestScore}";
            if (nicknameInput != null)
            {
                nicknameInput.text = PlayerSession.Nickname;
                nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
            }
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
                UpdateStartButtonInteractable();
            }
        }

        private void Start()
        {
            if (infoTable != null) infoTable.Populate();
        }

        private void OnNicknameChanged(string _) => UpdateStartButtonInteractable();

        private void UpdateStartButtonInteractable()
        {
            startButton.interactable =
                nicknameInput != null && !string.IsNullOrWhiteSpace(nicknameInput.text);
        }

        private void OnStartClicked()
        {
            if (nicknameInput != null)
                PlayerSession.Nickname = nicknameInput.text.Trim();
            SceneManager.LoadScene(mainSceneName);
        }
    }
}
```

### `CubeInfoTable.cs`

```csharp
using UnityEngine;

namespace OpenNinja
{
    public class CubeInfoTable : MonoBehaviour
    {
        [SerializeField] private CubeInfoRow rowPrefab;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private CubeMaterial[] materials;

        public void Populate()
        {
            if (rowPrefab == null || rowContainer == null || materials == null) return;

            // Clear any existing rows (idempotent — supports re-populate).
            for (int i = rowContainer.childCount - 1; i >= 0; i--)
                Destroy(rowContainer.GetChild(i).gameObject);

            foreach (var mat in materials)
            {
                if (mat == null) continue;
                var row = Instantiate(rowPrefab, rowContainer);
                row.Bind(mat);
            }
        }
    }
}
```

### `CubeInfoRow.cs`

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    public class CubeInfoRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text pointsLabel;
        [SerializeField] private TMP_Text roleBadge;
        [SerializeField] private Image roleBadgeBackground;

        private static readonly Color BadgeNormal = new Color(0.26f, 0.63f, 0.28f, 1f); // green
        private static readonly Color BadgeBonus  = new Color(0.98f, 0.75f, 0.18f, 1f); // gold
        private static readonly Color BadgeDanger = new Color(0.90f, 0.22f, 0.21f, 1f); // red

        public void Bind(CubeMaterial mat)
        {
            if (mat == null) return;

            if (nameLabel  != null) nameLabel.text  = mat.displayName;
            if (pointsLabel != null) pointsLabel.text = FormatPoints(mat);
            if (roleBadge  != null) roleBadge.text  = mat.role.ToString().ToUpperInvariant();
            if (roleBadgeBackground != null) roleBadgeBackground.color = ColorForRole(mat.role);
            if (icon != null) icon.sprite = SpriteFromTexture(mat.renderMaterial);
        }

        private static string FormatPoints(CubeMaterial mat) =>
            mat.role == CubeRole.Danger ? "-1 life" : $"+{mat.basePoints}";

        private static Color ColorForRole(CubeRole role) => role switch
        {
            CubeRole.Bonus  => BadgeBonus,
            CubeRole.Danger => BadgeDanger,
            _               => BadgeNormal,
        };

        private static Sprite SpriteFromTexture(Material renderMat)
        {
            if (renderMat == null) return null;
            var tex = renderMat.mainTexture as Texture2D;
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f));
        }
    }
}
```

### `NicknameHud.cs`

```csharp
using TMPro;
using UnityEngine;

namespace OpenNinja
{
    public class NicknameHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "Player: {0}";

        private void OnEnable()
        {
            if (label == null) return;
            string nick = PlayerSession.Nickname;
            label.text = string.IsNullOrEmpty(nick) ? "" : string.Format(format, nick);
            gameObject.SetActive(!string.IsNullOrEmpty(nick));
        }
    }
}
```

---

## 6. Modifications to existing scripts

### `GameManager.cs` — write BestScore on game over

In `SetGameOver()`, before firing `OnGameOver`:

```csharp
private void SetGameOver()
{
    IsGameOver = true;
    Time.timeScale = 0f;
    PlayerSession.TrySetBestScore(Score);
    OnGameOver?.Invoke(Score);
}
```

### `GameOverView.cs` — Exit button + "NEW BEST!" flag

Add:

```csharp
[SerializeField] private Button quitButton;
[SerializeField] private GameObject newBestFlag;

private void OnEnable()
{
    // (existing subscriptions...)
    if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
}

private void OnDisable()
{
    // (existing unsubscriptions...)
    if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
}

private void Show(int finalScore)
{
    if (panelRoot != null) panelRoot.SetActive(true);
    if (finalScoreLabel != null) finalScoreLabel.text = $"Score: {finalScore}";

    // The session-best was updated by GameManager BEFORE this fires, so we
    // compare against a snapshot the panel takes when the scene starts.
    if (newBestFlag != null)
        newBestFlag.SetActive(finalScore > _bestAtSceneStart);
}

private int _bestAtSceneStart;
private void Awake()
{
    if (panelRoot != null) panelRoot.SetActive(false);
    _bestAtSceneStart = PlayerSession.BestScore;
}

private void OnQuitClicked()
{
#if UNITY_EDITOR
    UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
}
```

---

## 7. StartScene visual layout (portrait 1080×1920)

```
y=1920 ┌──────────────────────────────────────┐  TOP
       │                                      │
       │           MATERIAL NINJA             │  Title @ y=-120, ~120pt, bold, yellow tint
       │                                      │
       │              Best: 42                │  BestScoreLabel @ y=-300, ~48pt, dim white
       │                                      │
       │ ┌──────────────────────────────────┐ │
       │ │  [🟫] Wood     +1     NORMAL     │ │  Row 120px tall
       │ │  [⬜] Stone    +2     NORMAL     │ │
       │ │  [⬛] Metal    +3     BONUS      │ │
       │ │  [🔷] Crystal  +5     BONUS      │ │
       │ │  [⬛] Spiked   -1 life  DANGER   │ │
       │ │  [🟨] Rubber   +2     NORMAL     │ │
       │ └──────────────────────────────────┘ │  Table @ center, 920x768
       │                                      │
       │     NICKNAME                         │  Label @ y=-1340
       │     ┌──────────────────────────────┐ │
       │     │ Alice                        │ │  InputField @ y=-1430, 600x100
       │     └──────────────────────────────┘ │
       │                                      │
       │           ┌──────────────┐           │
       │           │    START     │           │  Button @ y=-1700, 400x120
       │           └──────────────┘           │
       │                                      │
y=0    └──────────────────────────────────────┘  BOTTOM
```

### Canvas
- `Screen Space - Overlay`, `Scale With Screen Size`, reference resolution 1080×1920, `matchWidthOrHeight = 0.5`.

### CubeInfoRow prefab
- RectTransform 920×120, `HorizontalLayoutGroup` left-to-right with 16px spacing, padding 24px each side.
- Icon `Image` 80×80 (sprite assigned at Bind time from material's MainTex).
- Name TMP_Text, ~40pt, white, flex-grow 1.
- Points TMP_Text, ~40pt, gold for "+N", red for "-1 life", fixed width 200.
- Role badge container: 160×56 Image background + centered TMP_Text inside, ~28pt, white bold.

### Camera
- `Main Camera`, orthographic, depth 0, clear flags = Solid Color, background `#0F0F1F` (matches the night portion of MainScene's procedural sky for visual continuity).

---

## 8. `StartSceneSetup.cs` (Editor utility)

Re-runnable Editor script that builds `StartScene.unity` from scratch. Mirrors the pattern of `SceneSetup.cs` and `MaterialAssetsSetup.cs`. Responsibilities:

1. New empty scene named `StartScene`.
2. Reposition `Main Camera`, set orthographic + dark background.
3. Add EventSystem if missing.
4. Build the Canvas with all child elements (title, best score, info table container, nickname input, start button).
5. Build the `CubeInfoRow` prefab in `Assets/Prefabs/` (one row pre-fabricated so the table can spawn copies).
6. Attach scripts: `StartScreen`, `CubeInfoTable`.
7. Wire serialized references (label refs, button, info table → row prefab and 6 `CubeMaterial` SOs from `Assets/Data/CubeMaterials/`).
8. Save scene to `Assets/Scenes/StartScene.unity`.
9. Update `EditorBuildSettings.scenes` so `StartScene` is index 0 and `MainScene` is index 1.

Bake completion log similar to existing setup scripts (`"start scene built | row prefab created | build settings updated | scene saved"`).

---

## 9. Build Settings

```
0: Assets/Scenes/StartScene.unity   (default launch scene)
1: Assets/Scenes/MainScene.unity
```

`StartSceneSetup.Execute()` writes this.

---

## 10. Data flow

```
[App launch]
 └─ Unity loads scene 0 (StartScene)
     └─ StartScreen.Awake:
          titleLabel.text   = "MATERIAL NINJA"
          bestScoreLabel.text = $"Best: {PlayerSession.BestScore}"
          nicknameInput.text  = PlayerSession.Nickname
          startButton.interactable = !empty(nicknameInput.text)
     └─ StartScreen.Start:
          infoTable.Populate()  → 6× CubeInfoRow.Bind(CubeMaterial)

[User types nickname]
 └─ onValueChanged → startButton.interactable updated

[User clicks Start]
 └─ PlayerSession.Nickname = input.text.Trim()
 └─ SceneManager.LoadScene("MainScene")

[MainScene loads]
 └─ NicknameHud.OnEnable → label.text = "Player: Alice"
 └─ rest of gameplay unchanged

[GameManager.SetGameOver]
 └─ PlayerSession.TrySetBestScore(Score)
 └─ fires OnGameOver(finalScore)
     └─ GameOverView.Show(finalScore)
         ├─ panel.SetActive(true)
         ├─ finalScoreLabel.text = ...
         └─ newBestFlag.SetActive(finalScore > _bestAtSceneStart)

[User clicks Restart] (unchanged flow)
 └─ GameManager.ResetGame + spawner.NotifyRunRestarted

[User clicks Exit]
 └─ EditorApplication.isPlaying = false  (in Editor)
    Application.Quit()                    (in build)
```

---

## 11. Tests

```csharp
// Assets/Tests/EditMode/PlayerSessionTests.cs

[SetUp]    public void Setup()    { PlayerPrefs.DeleteKey("OpenNinja.Nickname"); PlayerPrefs.DeleteKey("OpenNinja.BestScore"); }
[TearDown] public void Teardown() { PlayerPrefs.DeleteKey("OpenNinja.Nickname"); PlayerPrefs.DeleteKey("OpenNinja.BestScore"); }

[Test] public void Nickname_DefaultsToEmptyString()
[Test] public void Nickname_RoundtripsThroughPlayerPrefs()
[Test] public void Nickname_TrimsWhitespace()
[Test] public void Nickname_TruncatesToMaxChars()
[Test] public void BestScore_DefaultsToZero()
[Test] public void BestScore_RoundtripsThroughPlayerPrefs()
[Test] public void TrySetBestScore_OnlyWritesWhenCandidateIsHigher()
[Test] public void TrySetBestScore_FiresEventOnUpdate()
```

8 tests total. All EditMode; no scene required.

Playtest checklist:
- First launch → StartScene visible. Nickname empty, Best: 0, Start button disabled.
- Type nickname → Start enables → click → MainScene loads, "Player: <nick>" visible in HUD.
- Score N → game over → "Score: N", if N > 0 also "NEW BEST!" → Exit button visible.
- Restart → MainScene state resets, no return to start.
- Exit → Editor: play mode stops. Build: application quits.
- Relaunch → StartScene prefills nickname, Best score reflects the previous run.

---

## 12. Files

```
NEW   Assets/Scripts/Core/PlayerSession.cs
NEW   Assets/Scripts/UI/StartScreen.cs
NEW   Assets/Scripts/UI/CubeInfoTable.cs
NEW   Assets/Scripts/UI/CubeInfoRow.cs
NEW   Assets/Scripts/UI/NicknameHud.cs
NEW   Assets/Editor/StartSceneSetup.cs
NEW   Assets/Prefabs/CubeInfoRow.prefab
NEW   Assets/Scenes/StartScene.unity
NEW   Assets/Tests/EditMode/PlayerSessionTests.cs
MOD   Assets/Scripts/Core/GameManager.cs                 +2 lines (TrySetBestScore call)
MOD   Assets/Scripts/UI/GameOverView.cs                  +30 lines (Exit button, newBestFlag, _bestAtSceneStart)
MOD   Assets/Editor/SceneSetup.cs                        +1 NicknameHud GameObject under Canvas
MOD   ProjectSettings/EditorBuildSettings.asset          StartScene added at index 0
```

---

## 13. Edge cases

| Case | Resolution |
|---|---|
| User taps Start with whitespace-only input | `interactable` is gated on `!IsNullOrWhiteSpace`. Click is disabled. |
| User types a 50-char nickname | `TMP_InputField.characterLimit = 16` blocks beyond limit. `PlayerSession.Nickname` setter truncates as a safety net. |
| First launch, no PlayerPrefs key | `GetString` returns `""`. Input is blank. Start button disabled until user types. |
| Game over without ever scoring (Score=0) | `TrySetBestScore(0)` no-ops (returns false). BestScore stays unchanged. |
| New best score === previous best | `TrySetBestScore` uses strict `>`, so equal score doesn't trigger NEW BEST. |
| GameOverView shown twice without scene reload | `_bestAtSceneStart` is captured in Awake; if BestScore was updated mid-game (e.g. by Restart sequence), the "NEW BEST!" flag may show stale info. Restart clears the panel; this is fine. |
| Building for mobile, Exit button on iOS | `Application.Quit()` doesn't actually quit on iOS per Apple HIG. The button is shown anyway for consistency; iOS users use the home gesture. |
| Special characters in nickname | TMP_InputField content type = Standard (allow Unicode). Trim only handles whitespace. No further sanitization needed for a local-only game. |

---

## 14. Out of scope (deferred)

- Audio (separate spec).
- Online leaderboard.
- Tutorial screen.
- Settings panel.
- Animated scene transitions.
- "Back to Start" mid-game button.
- Localization of the nickname / title / labels.

---

## 15. Open questions for implementation

- Final color/glow on the title text. Defaults to plain yellow bold; can be polished later if it feels flat.
- Whether "NEW BEST!" is just a TMP flag or a small animation. v1: static TMP shown above final score.
