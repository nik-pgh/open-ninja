# Start Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `StartScene` (loaded first at launch) with the game title, best score, a 6-row cube info table, a persistent nickname input, and a Start button — plus a `NicknameHud` in `MainScene` and an Exit button on the game-over panel.

**Architecture:** A static `PlayerSession` class wraps two PlayerPrefs-backed properties (`Nickname`, `BestScore`). A new `StartScene` is built from scratch by an Editor utility (`StartSceneSetup`) following the same re-runnable pattern as `SceneSetup` and `MaterialAssetsSetup`. Five small new MonoBehaviours (StartScreen, CubeInfoTable, CubeInfoRow, NicknameHud + minor GameOverView additions) wire the UI to PlayerSession. No new runtime singletons; PlayerPrefs is the persistence layer.

**Tech Stack:** Unity 6, C#, TextMeshPro, Unity UI (Image, Button, TMP_InputField, HorizontalLayoutGroup, VerticalLayoutGroup), Unity Test Framework EditMode, `coplay-mcp` for Editor-driven scene + prefab setup.

**Spec:** `docs/superpowers/specs/2026-05-27-start-screen-design.md`

---

## File Structure

```
NEW   Assets/Scripts/Core/PlayerSession.cs         ~50 lines, PlayerPrefs-backed static
NEW   Assets/Scripts/UI/StartScreen.cs             ~80 lines, root MB on StartCanvas
NEW   Assets/Scripts/UI/CubeInfoTable.cs           ~40 lines, spawns rows
NEW   Assets/Scripts/UI/CubeInfoRow.cs             ~55 lines, one-row MB on prefab
NEW   Assets/Scripts/UI/NicknameHud.cs             ~30 lines, MainScene HUD
NEW   Assets/Editor/StartSceneSetup.cs             ~280 lines, builds StartScene + row prefab + build settings
NEW   Assets/Prefabs/CubeInfoRow.prefab            built by StartSceneSetup
NEW   Assets/Scenes/StartScene.unity               built by StartSceneSetup
NEW   Assets/Tests/EditMode/PlayerSessionTests.cs  8 EditMode tests
MOD   Assets/Scripts/Core/GameManager.cs           +1 line: PlayerSession.TrySetBestScore(Score)
MOD   Assets/Scripts/UI/GameOverView.cs            +Exit button + NEW BEST flag + _bestAtSceneStart
MOD   Assets/Editor/SceneSetup.cs                  +NicknameHud GameObject under Canvas
MOD   ProjectSettings/EditorBuildSettings.asset    StartScene at index 0
```

The five new scripts each have one responsibility:
- `PlayerSession` — persistence
- `StartScreen` — orchestrate the start scene
- `CubeInfoTable` — spawn rows
- `CubeInfoRow` — render one row
- `NicknameHud` — show the nickname in MainScene

---

## Conventions

- Working directory: `/Users/nikkim/dev/open-ninja/.claude/worktrees/start-screen` (create via the `using-git-worktrees` skill at execution time).
- C# namespace `OpenNinja` for runtime, `OpenNinja.Tests` for tests, `OpenNinja.EditorSetup` for Editor scripts.
- "Run tests" = Unity Editor → `Window → General → Test Runner` → EditMode → Run All. Tasks that introduce tests will note when this is required.
- After every code change, call `mcp__coplay-mcp__check_compile_errors` if Unity Editor is open; expect `No compile errors`.
- Editor scripts (`StartSceneSetup`, modifications to `SceneSetup`) are re-runnable. Running them again is a no-op except for scene rebuild.
- All UI uses `Screen Space - Overlay` and the existing canvas convention (`Scale With Screen Size`, reference 1080×1920, `matchWidthOrHeight = 0.5`).

---

## Task 1: `PlayerSession` static class (TDD)

**Goal:** PlayerPrefs-backed session state. All session data flowing between StartScene and MainScene goes through this class.

**Files:**
- Create: `Assets/Tests/EditMode/PlayerSessionTests.cs`
- Create: `Assets/Scripts/Core/PlayerSession.cs`

### Step 1: Write the failing test file

- [ ] **Step 1.1: Write `PlayerSessionTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class PlayerSessionTests
    {
        private const string NicknameKey  = "OpenNinja.Nickname";
        private const string BestScoreKey = "OpenNinja.BestScore";

        [SetUp]
        public void Setup()
        {
            PlayerPrefs.DeleteKey(NicknameKey);
            PlayerPrefs.DeleteKey(BestScoreKey);
        }

        [TearDown]
        public void Teardown()
        {
            PlayerPrefs.DeleteKey(NicknameKey);
            PlayerPrefs.DeleteKey(BestScoreKey);
        }

        [Test]
        public void Nickname_DefaultsToEmptyString()
        {
            Assert.AreEqual("", PlayerSession.Nickname);
        }

        [Test]
        public void Nickname_RoundtripsThroughPlayerPrefs()
        {
            PlayerSession.Nickname = "Alice";
            Assert.AreEqual("Alice", PlayerSession.Nickname);
        }

        [Test]
        public void Nickname_TrimsWhitespace()
        {
            PlayerSession.Nickname = "   Bob   ";
            Assert.AreEqual("Bob", PlayerSession.Nickname);
        }

        [Test]
        public void Nickname_TruncatesToMaxChars()
        {
            // 16 char limit
            PlayerSession.Nickname = "abcdefghijklmnopqrstuvwxyz";
            Assert.AreEqual("abcdefghijklmnop", PlayerSession.Nickname);
            Assert.AreEqual(16, PlayerSession.Nickname.Length);
        }

        [Test]
        public void Nickname_NullBecomesEmpty()
        {
            PlayerSession.Nickname = null;
            Assert.AreEqual("", PlayerSession.Nickname);
        }

        [Test]
        public void BestScore_DefaultsToZero()
        {
            Assert.AreEqual(0, PlayerSession.BestScore);
        }

        [Test]
        public void BestScore_RoundtripsThroughPlayerPrefs()
        {
            PlayerSession.BestScore = 42;
            Assert.AreEqual(42, PlayerSession.BestScore);
        }

        [Test]
        public void BestScore_NegativeClampsToZero()
        {
            PlayerSession.BestScore = -5;
            Assert.AreEqual(0, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_HigherValue_UpdatesAndReturnsTrue()
        {
            PlayerSession.BestScore = 10;
            bool wasNewBest = PlayerSession.TrySetBestScore(20);
            Assert.IsTrue(wasNewBest);
            Assert.AreEqual(20, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_EqualValue_DoesNotUpdateAndReturnsFalse()
        {
            PlayerSession.BestScore = 10;
            bool wasNewBest = PlayerSession.TrySetBestScore(10);
            Assert.IsFalse(wasNewBest);
            Assert.AreEqual(10, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_LowerValue_DoesNotUpdateAndReturnsFalse()
        {
            PlayerSession.BestScore = 10;
            bool wasNewBest = PlayerSession.TrySetBestScore(5);
            Assert.IsFalse(wasNewBest);
            Assert.AreEqual(10, PlayerSession.BestScore);
        }

        [Test]
        public void TrySetBestScore_FiresEventOnUpdate()
        {
            int eventValue = -1;
            System.Action<int> handler = v => eventValue = v;
            PlayerSession.OnBestScoreChanged += handler;
            try
            {
                PlayerSession.TrySetBestScore(99);
                Assert.AreEqual(99, eventValue);
            }
            finally
            {
                PlayerSession.OnBestScoreChanged -= handler;
            }
        }

        [Test]
        public void TrySetBestScore_DoesNotFireEventWhenNoUpdate()
        {
            PlayerSession.BestScore = 50;
            int eventCallCount = 0;
            System.Action<int> handler = _ => eventCallCount++;
            PlayerSession.OnBestScoreChanged += handler;
            try
            {
                PlayerSession.TrySetBestScore(30);
                Assert.AreEqual(0, eventCallCount);
            }
            finally
            {
                PlayerSession.OnBestScoreChanged -= handler;
            }
        }
    }
}
```

- [ ] **Step 1.2: Run tests — expect compile failure**

Expected: compile error referencing missing `PlayerSession` type.

### Step 2: Write the implementation

- [ ] **Step 2.1: Write `PlayerSession.cs`**

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
        private const string NicknameKey     = "OpenNinja.Nickname";
        private const string BestScoreKey    = "OpenNinja.BestScore";
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

        /// <summary>Only writes if the candidate strictly beats the current best.</summary>
        public static bool TrySetBestScore(int candidate)
        {
            if (candidate <= BestScore) return false;
            BestScore = candidate;
            return true;
        }
    }
}
```

- [ ] **Step 2.2: Run tests — expect all pass**

Expected: all 13 PlayerSession EditMode tests pass.

- [ ] **Step 2.3: Commit**

```bash
git add Assets/Scripts/Core/PlayerSession.cs Assets/Tests/EditMode/PlayerSessionTests.cs
git commit -m "feat: add PlayerSession with PlayerPrefs-backed Nickname + BestScore"
```

---

## Task 2: `CubeInfoRow` + `CubeInfoTable`

**Goal:** A row MonoBehaviour that knows how to render one `CubeMaterial`, and a table MonoBehaviour that instantiates 6 rows from a list. No tests — these are pure UI scripts verified by playtest.

**Files:**
- Create: `Assets/Scripts/UI/CubeInfoRow.cs`
- Create: `Assets/Scripts/UI/CubeInfoTable.cs`

- [ ] **Step 1: Write `CubeInfoRow.cs`**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// One row of the start-screen cube info table. Built as a prefab whose
    /// fields are populated by Bind(CubeMaterial). The icon sprite is created
    /// at runtime from the material's albedo texture — no separate icon assets.
    /// </summary>
    public class CubeInfoRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text pointsLabel;
        [SerializeField] private TMP_Text roleBadge;
        [SerializeField] private Image roleBadgeBackground;

        private static readonly Color BadgeNormal = new Color(0.26f, 0.63f, 0.28f, 1f);
        private static readonly Color BadgeBonus  = new Color(0.98f, 0.75f, 0.18f, 1f);
        private static readonly Color BadgeDanger = new Color(0.90f, 0.22f, 0.21f, 1f);
        private static readonly Color PointsPositive = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color PointsNegative = new Color(1f, 0.4f, 0.4f, 1f);

        public void Bind(CubeMaterial mat)
        {
            if (mat == null) return;

            if (nameLabel != null) nameLabel.text = mat.displayName;
            if (pointsLabel != null)
            {
                pointsLabel.text = mat.role == CubeRole.Danger ? "-1 life" : $"+{mat.basePoints}";
                pointsLabel.color = mat.role == CubeRole.Danger ? PointsNegative : PointsPositive;
            }
            if (roleBadge != null) roleBadge.text = mat.role.ToString().ToUpperInvariant();
            if (roleBadgeBackground != null) roleBadgeBackground.color = ColorForRole(mat.role);
            if (icon != null) icon.sprite = SpriteFromMaterial(mat.renderMaterial);
        }

        private static Color ColorForRole(CubeRole role) => role switch
        {
            CubeRole.Bonus  => BadgeBonus,
            CubeRole.Danger => BadgeDanger,
            _               => BadgeNormal,
        };

        private static Sprite SpriteFromMaterial(Material renderMat)
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

- [ ] **Step 2: Write `CubeInfoTable.cs`**

```csharp
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Spawns one CubeInfoRow per CubeMaterial under rowContainer. Idempotent —
    /// re-calling Populate clears existing rows first.
    /// </summary>
    public class CubeInfoTable : MonoBehaviour
    {
        [SerializeField] private CubeInfoRow rowPrefab;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private CubeMaterial[] materials;

        public void Populate()
        {
            if (rowPrefab == null || rowContainer == null || materials == null) return;

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

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/CubeInfoRow.cs Assets/Scripts/UI/CubeInfoTable.cs
git commit -m "feat(ui): CubeInfoRow + CubeInfoTable for the start screen"
```

---

## Task 3: `StartScreen` + `NicknameHud`

**Goal:** The orchestrator script for `StartScene` and the in-game HUD label showing the player's nickname. No tests — manual verification.

**Files:**
- Create: `Assets/Scripts/UI/StartScreen.cs`
- Create: `Assets/Scripts/UI/NicknameHud.cs`

- [ ] **Step 1: Write `StartScreen.cs`**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// Root MonoBehaviour on the StartScene canvas. Reads PlayerSession state
    /// into the UI, gates the Start button on a non-empty nickname, and
    /// transitions to MainScene on click.
    /// </summary>
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
            if (startButton == null) return;
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

- [ ] **Step 2: Write `NicknameHud.cs`**

```csharp
using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Small label in MainScene that shows the current player's nickname.
    /// Hides itself if no nickname is set (so a player who launches MainScene
    /// directly without going through StartScene sees a clean HUD).
    /// </summary>
    public class NicknameHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "Player: {0}";

        private void OnEnable()
        {
            if (label == null) return;
            string nick = PlayerSession.Nickname;
            if (string.IsNullOrEmpty(nick))
            {
                gameObject.SetActive(false);
                return;
            }
            label.text = string.Format(format, nick);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/StartScreen.cs Assets/Scripts/UI/NicknameHud.cs
git commit -m "feat(ui): StartScreen orchestrator + in-game NicknameHud"
```

---

## Task 4: `GameManager` + `GameOverView` modifications

**Goal:** GameManager writes the new best score on game over. GameOverView gains an Exit button and a "NEW BEST!" flag that compares against the BestScore captured at scene start.

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Scripts/UI/GameOverView.cs`

### Step 1: GameManager — write best score

- [ ] **Step 1.1: Modify `GameManager.cs`**

Find the `SetGameOver` method:

```csharp
private void SetGameOver()
{
    IsGameOver = true;
    Time.timeScale = 0f;
    OnGameOver?.Invoke(Score);
}
```

Replace with:

```csharp
private void SetGameOver()
{
    IsGameOver = true;
    Time.timeScale = 0f;
    PlayerSession.TrySetBestScore(Score);
    OnGameOver?.Invoke(Score);
}
```

(One added line. Leave everything else unchanged.)

### Step 2: GameOverView — Exit button + NEW BEST flag

- [ ] **Step 2.1: Replace `GameOverView.cs` entirely**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text finalScoreLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject newBestFlag;
        [SerializeField] private CubeSpawner spawner;

        private int _bestAtSceneStart;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (newBestFlag != null) newBestFlag.SetActive(false);
            _bestAtSceneStart = PlayerSession.BestScore;
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver += Show;
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= Show;
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        private void Show(int finalScore)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (finalScoreLabel != null) finalScoreLabel.text = $"Score: {finalScore}";
            if (newBestFlag != null) newBestFlag.SetActive(finalScore > _bestAtSceneStart);
        }

        private void OnRestartClicked()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (newBestFlag != null) newBestFlag.SetActive(false);
            GameManager.Instance?.ResetGame();
            spawner?.NotifyRunRestarted();
            foreach (var cube in FindObjectsByType<Cube>(FindObjectsInactive.Exclude))
            {
                Destroy(cube.gameObject);
            }
            // Refresh the best-at-scene-start snapshot so the NEW BEST! flag
            // works correctly for subsequent runs in the same play session.
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
    }
}
```

### Step 3: Run tests

- [ ] **Step 3.1: Run tests — expect all to still pass**

Expected: 13 PlayerSession tests + 22 prior EditMode tests = 35 total pass.

### Step 4: Commit

- [ ] **Step 4.1: Commit**

```bash
git add Assets/Scripts/Core/GameManager.cs Assets/Scripts/UI/GameOverView.cs
git commit -m "feat: write BestScore on game-over; add Exit button + NEW BEST flag"
```

---

## Task 5: `StartSceneSetup` Editor script — build StartScene + row prefab

**Goal:** One re-runnable Editor utility that builds `StartScene.unity` from scratch, creates the `CubeInfoRow.prefab`, wires every script reference, and registers the scene in Build Settings as index 0.

**Prerequisite:** Unity Editor must be open with the Coplay MCP plugin connected. Verify with `mcp__coplay-mcp__get_unity_editor_state` before running.

**Files:**
- Create: `Assets/Editor/StartSceneSetup.cs`

### Step 1: Write the setup script

- [ ] **Step 1.1: Write `StartSceneSetup.cs`**

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// Re-runnable Editor utility. Builds Assets/Scenes/StartScene.unity from
    /// scratch with title / best score / cube info table / nickname input /
    /// start button. Also builds Assets/Prefabs/CubeInfoRow.prefab and adds
    /// StartScene to Build Settings as index 0.
    /// </summary>
    public static class StartSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/StartScene.unity";
        private const string RowPrefabPath = "Assets/Prefabs/CubeInfoRow.prefab";
        private static readonly string[] MaterialNames =
            { "Wood", "Stone", "Metal", "Crystal", "Spiked", "Rubber" };

        public static string Execute()
        {
            var log = new List<string>();

            BuildRowPrefab();
            log.Add("row prefab built");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "StartScene";

            ConfigureCamera();
            log.Add("camera configured");

            var canvas = BuildCanvas();
            var title = BuildTitleLabel(canvas);
            var best  = BuildBestScoreLabel(canvas);
            var (table, rowContainer) = BuildInfoTable(canvas);
            var input = BuildNicknameInput(canvas);
            var button = BuildStartButton(canvas);
            EnsureEventSystem();
            log.Add("canvas built");

            WireStartScreen(canvas, title, best, table, rowContainer, input, button);
            log.Add("start screen wired");

            EditorSceneManager.SaveScene(scene, ScenePath);
            UpdateBuildSettings();
            log.Add($"scene saved + build settings updated → {ScenePath}");

            return string.Join(" | ", log);
        }

        // ---- Row prefab ----

        private static void BuildRowPrefab()
        {
            EnsureFolder("Assets/Prefabs");
            DeleteIfExists(RowPrefabPath);

            // RectTransform root with HorizontalLayoutGroup.
            var root = new GameObject("CubeInfoRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(920, 120);
            var hlg = root.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.padding = new RectOffset(24, 24, 16, 16);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Optional faint background.
            var bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);

            // Icon.
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(root.transform, false);
            var iconRT = (RectTransform)iconGO.transform;
            iconRT.sizeDelta = new Vector2(80, 80);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;

            // Name.
            var nameGO = NewTmpChild(root.transform, "Name", "Material", 40,
                preferredWidth: 320, alignment: TextAlignmentOptions.Left);

            // Points.
            var pointsGO = NewTmpChild(root.transform, "Points", "+1", 40,
                preferredWidth: 200, alignment: TextAlignmentOptions.Right);

            // Role badge: small Image with a child TMP.
            var badge = new GameObject("RoleBadge",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            badge.transform.SetParent(root.transform, false);
            var badgeRT = (RectTransform)badge.transform;
            badgeRT.sizeDelta = new Vector2(160, 56);
            var badgeLE = badge.GetComponent<LayoutElement>();
            badgeLE.minWidth = 160; badgeLE.minHeight = 56;
            var badgeBg = badge.GetComponent<Image>();
            badgeBg.color = new Color(0.26f, 0.63f, 0.28f, 1f);

            var badgeText = NewTmpChild(badge.transform, "Text", "NORMAL", 28,
                preferredWidth: 0, alignment: TextAlignmentOptions.Center);
            var badgeTextRT = (RectTransform)badgeText.transform;
            badgeTextRT.anchorMin = Vector2.zero;
            badgeTextRT.anchorMax = Vector2.one;
            badgeTextRT.offsetMin = Vector2.zero;
            badgeTextRT.offsetMax = Vector2.zero;
            var badgeTmp = badgeText.GetComponent<TMP_Text>();
            badgeTmp.color = Color.white;
            badgeTmp.fontStyle = FontStyles.Bold;

            // Attach the row script and wire serialized references.
            var rowScript = root.AddComponent<CubeInfoRow>();
            var so = new SerializedObject(rowScript);
            so.FindProperty("icon").objectReferenceValue = iconImg;
            so.FindProperty("nameLabel").objectReferenceValue = nameGO.GetComponent<TMP_Text>();
            so.FindProperty("pointsLabel").objectReferenceValue = pointsGO.GetComponent<TMP_Text>();
            so.FindProperty("roleBadge").objectReferenceValue = badgeText.GetComponent<TMP_Text>();
            so.FindProperty("roleBadgeBackground").objectReferenceValue = badgeBg;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ---- Scene pieces ----

        private static void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.12f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
        }

        private static GameObject BuildCanvas()
        {
            var canvasGO = new GameObject("StartCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGO;
        }

        private static GameObject BuildTitleLabel(GameObject canvas)
        {
            var go = NewTmpChild(canvas.transform, "Title", "MATERIAL NINJA", 120,
                preferredWidth: 1000, alignment: TextAlignmentOptions.Center);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -120);
            rt.sizeDelta = new Vector2(1000, 160);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.color = new Color(1f, 0.9f, 0.2f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            return go;
        }

        private static GameObject BuildBestScoreLabel(GameObject canvas)
        {
            var go = NewTmpChild(canvas.transform, "BestScore", "Best: 0", 48,
                preferredWidth: 800, alignment: TextAlignmentOptions.Center);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -300);
            rt.sizeDelta = new Vector2(800, 70);
            var tmp = go.GetComponent<TMP_Text>();
            tmp.color = new Color(1f, 1f, 1f, 0.75f);
            return go;
        }

        private static (GameObject table, RectTransform rowContainer) BuildInfoTable(GameObject canvas)
        {
            var table = new GameObject("CubeInfoTable", typeof(RectTransform));
            table.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)table.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -400);
            rt.sizeDelta = new Vector2(920, 800);

            var container = new GameObject("RowContainer",
                typeof(RectTransform), typeof(VerticalLayoutGroup));
            container.transform.SetParent(table.transform, false);
            var crt = (RectTransform)container.transform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var vlg = container.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;

            return (table, crt);
        }

        private static GameObject BuildNicknameInput(GameObject canvas)
        {
            // Container holding the label and the input.
            var wrap = new GameObject("NicknameWrap", typeof(RectTransform));
            wrap.transform.SetParent(canvas.transform, false);
            var wrt = (RectTransform)wrap.transform;
            wrt.anchorMin = new Vector2(0.5f, 1f);
            wrt.anchorMax = new Vector2(0.5f, 1f);
            wrt.pivot = new Vector2(0.5f, 1f);
            wrt.anchoredPosition = new Vector2(0, -1340);
            wrt.sizeDelta = new Vector2(700, 200);

            // Label.
            var label = NewTmpChild(wrap.transform, "Label", "NICKNAME", 36,
                preferredWidth: 700, alignment: TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0.5f, 1f);
            lrt.anchorMax = new Vector2(0.5f, 1f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0, 0);
            lrt.sizeDelta = new Vector2(700, 50);

            // Input.
            var inputGO = new GameObject("NicknameInput",
                typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGO.transform.SetParent(wrap.transform, false);
            var irt = (RectTransform)inputGO.transform;
            irt.anchorMin = new Vector2(0.5f, 1f);
            irt.anchorMax = new Vector2(0.5f, 1f);
            irt.pivot = new Vector2(0.5f, 1f);
            irt.anchoredPosition = new Vector2(0, -70);
            irt.sizeDelta = new Vector2(600, 100);
            var inputBg = inputGO.GetComponent<Image>();
            inputBg.color = new Color(1f, 1f, 1f, 0.1f);

            // Text Area + child Text required by TMP_InputField.
            var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGO.transform, false);
            var tart = (RectTransform)textArea.transform;
            tart.anchorMin = Vector2.zero;
            tart.anchorMax = Vector2.one;
            tart.offsetMin = new Vector2(20, 0);
            tart.offsetMax = new Vector2(-20, 0);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(textArea.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<TextMeshProUGUI>();
            text.fontSize = 48;
            text.color = Color.white;
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
            placeholder.text = "Type a nickname...";
            placeholder.fontSize = 48;
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.fontStyle = FontStyles.Italic;

            var inputField = inputGO.GetComponent<TMP_InputField>();
            inputField.textViewport = (RectTransform)textArea.transform;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.characterLimit = 16;
            inputField.contentType = TMP_InputField.ContentType.Standard;

            return inputGO;
        }

        private static GameObject BuildStartButton(GameObject canvas)
        {
            var btn = new GameObject("StartButton",
                typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0, -1700);
            rt.sizeDelta = new Vector2(400, 120);
            var img = btn.GetComponent<Image>();
            img.color = new Color(0.13f, 0.59f, 0.95f, 1f);

            var label = NewTmpChild(btn.transform, "Label", "START", 56,
                preferredWidth: 400, alignment: TextAlignmentOptions.Center);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var ltmp = label.GetComponent<TMP_Text>();
            ltmp.color = Color.white;
            ltmp.fontStyle = FontStyles.Bold;

            return btn;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            _ = es;
        }

        // ---- Wiring ----

        private static void WireStartScreen(GameObject canvas, GameObject titleGO, GameObject bestGO,
            GameObject tableGO, RectTransform rowContainer, GameObject inputGO, GameObject buttonGO)
        {
            var screen = canvas.AddComponent<StartScreen>();
            var rowPrefab = AssetDatabase.LoadAssetAtPath<CubeInfoRow>(RowPrefabPath);

            var materials = new CubeMaterial[MaterialNames.Length];
            for (int i = 0; i < MaterialNames.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<CubeMaterial>(
                    $"Assets/Data/CubeMaterials/{MaterialNames[i]}.asset");
            }

            var tableComp = tableGO.AddComponent<CubeInfoTable>();
            var tso = new SerializedObject(tableComp);
            tso.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
            tso.FindProperty("rowContainer").objectReferenceValue = rowContainer;
            var matsProp = tso.FindProperty("materials");
            matsProp.arraySize = materials.Length;
            for (int i = 0; i < materials.Length; i++)
                matsProp.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
            tso.ApplyModifiedPropertiesWithoutUndo();

            var sso = new SerializedObject(screen);
            sso.FindProperty("titleLabel").objectReferenceValue = titleGO.GetComponent<TMP_Text>();
            sso.FindProperty("bestScoreLabel").objectReferenceValue = bestGO.GetComponent<TMP_Text>();
            sso.FindProperty("nicknameInput").objectReferenceValue = inputGO.GetComponent<TMP_InputField>();
            sso.FindProperty("startButton").objectReferenceValue = buttonGO.GetComponent<Button>();
            sso.FindProperty("infoTable").objectReferenceValue = tableComp;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateBuildSettings()
        {
            const string mainScenePath = "Assets/Scenes/MainScene.unity";
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            // Ensure MainScene is present (at any non-zero index).
            if (!scenes.Exists(s => s.path == mainScenePath))
                scenes.Add(new EditorBuildSettingsScene(mainScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---- Helpers ----

        private static GameObject NewTmpChild(Transform parent, string name, string text,
            float fontSize, float preferredWidth, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            if (preferredWidth > 0)
            {
                var le = go.AddComponent<LayoutElement>();
                le.preferredWidth = preferredWidth;
                le.preferredHeight = fontSize * 1.4f;
            }
            return go;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}
```

- [ ] **Step 1.2: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

### Step 2: Run the setup

- [ ] **Step 2.1: Execute via MCP**

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/StartSceneSetup.cs
  methodName: Execute
```

Expected: log message ending with `... scene saved + build settings updated → Assets/Scenes/StartScene.unity`.

- [ ] **Step 2.2: Verify outputs**

```bash
ls Assets/Scenes/StartScene.unity      # exists
ls Assets/Prefabs/CubeInfoRow.prefab   # exists
```

### Step 3: Commit

- [ ] **Step 3.1: Commit**

```bash
git add Assets/Editor/StartSceneSetup.cs Assets/Scenes/StartScene.unity Assets/Prefabs/CubeInfoRow.prefab ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(scene): build StartScene + CubeInfoRow prefab + build settings"
```

---

## Task 6: Add `NicknameHud` to `MainScene` via `SceneSetup`

**Goal:** `MainScene` should show the player's nickname in the HUD. The user's existing `SceneSetup.cs` builds the MainScene Canvas; we add one new label child for the NicknameHud.

**Files:**
- Modify: `Assets/Editor/SceneSetup.cs`

### Step 1: Add the NicknameHud GameObject

- [ ] **Step 1.1: Find the score panel block in `SceneSetup.Execute()`**

Look for the section that creates `ScorePanel`. It looks roughly like:

```csharp
// ScorePanel (TMP_Text in top-left).
var scoreGO = NewTMP(canvasGO.transform, "ScorePanel", "Score: 0", 48, ...);
```

- [ ] **Step 1.2: After the ScorePanel block, add a NicknameHud block**

Insert this code immediately after the ScorePanel construction:

```csharp
// NicknameHud (label under the score; reads PlayerSession.Nickname).
var nickGO = NewTMP(canvasGO.transform, "NicknameHud", "Player:", 36,
    anchorMin: new Vector2(0, 1), anchorMax: new Vector2(0, 1), pivot: new Vector2(0, 1),
    anchored: new Vector2(40, -120), size: new Vector2(700, 60),
    color: new Color(1f, 1f, 1f, 0.7f), alignment: TextAlignmentOptions.TopLeft);
var nickHud = nickGO.AddComponent<NicknameHud>();
var nickSO = new SerializedObject(nickHud);
SetRef(nickSO, "label", nickGO.GetComponent<TMP_Text>());
nickSO.ApplyModifiedPropertiesWithoutUndo();
```

(The `NewTMP` helper and `SetRef` already exist in `SceneSetup`. The y-offset `-120` places the HUD just below the score line.)

### Step 2: Run the updated setup

- [ ] **Step 2.1: Verify compile clean**

`mcp__coplay-mcp__check_compile_errors` → `No compile errors`.

- [ ] **Step 2.2: Execute via MCP**

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/SceneSetup.cs
  methodName: Execute
```

Expected: the existing log message ending with `... scene saved to Assets/Scenes/MainScene.unity`.

- [ ] **Step 2.3: Verify scene contents**

```
mcp__coplay-mcp__list_game_objects_in_hierarchy
  nameFilter: NicknameHud
  onlyPaths: true
```

Expected: returns `Canvas/NicknameHud` (or similar path under the Canvas).

### Step 3: Commit

- [ ] **Step 3.1: Commit**

```bash
git add Assets/Editor/SceneSetup.cs Assets/Scenes/MainScene.unity
git commit -m "feat(scene): add NicknameHud label under the score panel in MainScene"
```

---

## Task 7: Playtest verification

**Goal:** End-to-end verification of the start-screen flow on the running Unity Editor.

This task does no code work. It's a manual verification gate.

### Step 1: Confirm Build Settings

- [ ] **Step 1.1: Open `File → Build Settings`**

Verify:
- `Scenes/StartScene.unity` at index 0, checked.
- `Scenes/MainScene.unity` at index 1, checked.

(If not, re-run `StartSceneSetup.Execute` via MCP — it idempotently fixes Build Settings.)

### Step 2: Run the start screen

- [ ] **Step 2.1: Press Play (or `mcp__coplay-mcp__play_game`)**

If currently in MainScene, Unity will load it in Play Mode. Stop play mode. Open `Assets/Scenes/StartScene.unity` manually, OR re-press Play after setting StartScene as the active scene (double-click `StartScene.unity` in the Project window first).

- [ ] **Step 2.2: Capture a screenshot**

`mcp__coplay-mcp__capture_scene_object` — verify the screenshot shows:
- Title `MATERIAL NINJA` near the top.
- `Best: 0` (or whatever PlayerPrefs has).
- 6-row cube info table (each row shows the icon, name, points, role badge).
- Nickname input field (empty or prefilled).
- START button (disabled if input empty).

### Step 3: Test the flow

- [ ] **Step 3.1: Type a nickname**

Click into the nickname input, type "Tester". The START button should become interactable.

- [ ] **Step 3.2: Click START**

MainScene loads. The score area shows `Score: 0` and below it `Player: Tester` (NicknameHud).

- [ ] **Step 3.3: Lose the game**

Let three cubes fall off (or wait). Game-over panel appears with:
- `Score: <final>`
- `NEW BEST!` flag (if the final score beats the previous best).
- Restart button.
- Exit button.

- [ ] **Step 3.4: Click Restart**

Cubes clear, score resets, Player label still says `Tester`, game resumes.

- [ ] **Step 3.5: Lose again, click Exit**

In the Editor: Play Mode stops. In a build: the application quits.

### Step 4: Relaunch verification

- [ ] **Step 4.1: Restart Play Mode (StartScene)**

The nickname input is pre-filled with `Tester`. Best Score shows the highest score from the previous Play session.

### Step 5: Final commit (if any tuning changes were made)

If anything needed adjustment (anchor positions, button colors, etc.), tune the relevant Editor script, re-run it, and commit:

```bash
git status
git add Assets ProjectSettings
git commit -m "tune: post-playtest start-screen tweaks"
```

If nothing needed adjustment, skip this step.

---

## Done criteria

- All EditMode tests pass (35 total: 13 PlayerSession + 22 prior).
- `StartScene.unity` exists and is build index 0.
- `CubeInfoRow.prefab` exists in `Assets/Prefabs/`.
- Launching Play loads StartScene first. Typing a nickname + clicking START loads MainScene with the nickname visible in the HUD.
- Beating the previous best triggers `NEW BEST!` on the game-over panel.
- Restart resumes gameplay; Exit closes Play Mode (Editor) or the application (build).
- Relaunching the Editor preserves nickname and best score across runs.
