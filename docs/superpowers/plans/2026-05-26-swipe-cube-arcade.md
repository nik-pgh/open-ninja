# Swipe-Cube Arcade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Fruit Ninja–style cube slicer in Unity 3D with combos, lives, color-coded cube types, and a difficulty ramp — all driven by a single `GameManager` singleton.

**Architecture:** Singleton `GameManager` owns all mutable game state and emits C# events. Cubes are physics-driven prefabs that the `BladeController` slices via swept-sphere casts against a 2D mouse path projected onto a z=0 play plane. UI is event-driven and read-only.

**Tech Stack:** Unity 6 (or 2022.3 LTS+), C#, Unity Physics, TextMeshPro, Unity Test Framework (EditMode). Optional `coplay-mcp` tools for scene/prefab authoring; manual Editor instructions are provided as fallback.

**Spec:** `docs/superpowers/specs/2026-05-26-swipe-cube-arcade-design.md`

---

## File Structure

```
Assets/
├── Scripts/
│   ├── Core/
│   │   ├── CubeType.cs              # enum Green / Red / Black
│   │   └── GameManager.cs           # Singleton state, combo math, events
│   ├── Gameplay/
│   │   ├── Cube.cs                  # Per-cube slice + fall-off
│   │   ├── CubeSpawner.cs           # Timed spawn with curves
│   │   ├── BladeController.cs       # Mouse → world swipe, SphereCastAll
│   │   └── KillZone.cs              # Bottom trigger forwarder
│   └── UI/
│       ├── ScoreView.cs
│       ├── ComboBadgeView.cs
│       ├── LivesView.cs
│       ├── ComboPopupSpawner.cs     # Listens to OnHit, spawns floaters
│       ├── ComboPopup.cs            # Self-animating floater
│       └── GameOverView.cs
├── Tests/
│   ├── OpenNinja.Tests.asmdef       # EditMode test assembly
│   └── EditMode/
│       └── GameManagerTests.cs
├── Materials/
│   ├── Cube_Green.mat
│   ├── Cube_Red.mat
│   └── Cube_Black.mat
├── Prefabs/
│   ├── Cube_Green.prefab
│   ├── Cube_Red.prefab
│   ├── Cube_Black.prefab
│   ├── SliceBurst.prefab
│   └── ComboPopup.prefab
└── Scenes/
    └── SampleScene.unity            # Single main scene
```

**Assembly definitions**
- `Assets/Scripts/OpenNinja.asmdef` — runtime asmdef. References: `Unity.TextMeshPro`.
- `Assets/Tests/OpenNinja.Tests.asmdef` — EditMode test asmdef. References: `OpenNinja`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`. Optional refs: `nunit.framework.dll`. Include platforms: Editor only.

---

## Conventions used in this plan

- All paths are relative to the repo root (`/Users/nikkim/dev/open-ninja`).
- C# uses `namespace OpenNinja` for runtime code and `namespace OpenNinja.Tests` for tests.
- Tests use NUnit attributes (Unity Test Framework). EditMode tests run without Play Mode.
- "Run tests" means: in Unity Editor open `Window → General → Test Runner`, select EditMode, click Run All. Or via CLI:
  `"/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath <repo> -runTests -testPlatform editmode -testResults TestResults.xml -nographics -logFile -`
  Expected: NUnit XML report showing all tests pass.
- "coplay-mcp" steps use the `mcp__coplay-mcp__*` tools when Unity Editor is open. If the editor isn't running, do the equivalent manually in the Editor — each task lists both.

---

## Task 1: Project Bootstrap

**Goal:** Folder layout, asmdefs, and `.gitignore` hygiene so the rest of the work compiles.

**Files:**
- Create: `Assets/Scripts/OpenNinja.asmdef`
- Create: `Assets/Tests/OpenNinja.Tests.asmdef`
- Create: `Assets/Scripts/Core/.gitkeep`
- Create: `Assets/Scripts/Gameplay/.gitkeep`
- Create: `Assets/Scripts/UI/.gitkeep`
- Create: `Assets/Tests/EditMode/.gitkeep`
- Create: `Assets/Materials/.gitkeep`
- Create: `Assets/Prefabs/.gitkeep`
- Create: `Assets/Scenes/.gitkeep`

- [ ] **Step 1: Create folder structure**

```bash
mkdir -p Assets/Scripts/Core Assets/Scripts/Gameplay Assets/Scripts/UI \
         Assets/Tests/EditMode Assets/Materials Assets/Prefabs Assets/Scenes
touch Assets/Scripts/Core/.gitkeep Assets/Scripts/Gameplay/.gitkeep \
      Assets/Scripts/UI/.gitkeep Assets/Tests/EditMode/.gitkeep \
      Assets/Materials/.gitkeep Assets/Prefabs/.gitkeep Assets/Scenes/.gitkeep
```

- [ ] **Step 2: Write runtime asmdef**

Write `Assets/Scripts/OpenNinja.asmdef`:

```json
{
  "name": "OpenNinja",
  "rootNamespace": "OpenNinja",
  "references": [
    "Unity.TextMeshPro"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 3: Write test asmdef**

Write `Assets/Tests/OpenNinja.Tests.asmdef`:

```json
{
  "name": "OpenNinja.Tests",
  "rootNamespace": "OpenNinja.Tests",
  "references": [
    "OpenNinja",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 4: Update `.gitignore` to exclude noise**

Verify `.gitignore` already ignores `Library/`, `Temp/`, `Logs/`, `obj/`, `.DS_Store`, `*.csproj`, `*.sln`. If `.DS_Store` is missing, add it.

- [ ] **Step 5: Commit**

```bash
git add Assets .gitignore
git commit -m "chore: scaffold script + test folder structure and asmdefs"
```

---

## Task 2: `CubeType` enum

**Goal:** Shared enum used by `Cube`, `GameManager`, and the spawner.

**Files:**
- Create: `Assets/Scripts/Core/CubeType.cs`

- [ ] **Step 1: Write the enum**

Write `Assets/Scripts/Core/CubeType.cs`:

```csharp
namespace OpenNinja
{
    public enum CubeType
    {
        Green = 0,
        Red = 1,
        Black = 2
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Core/CubeType.cs
git commit -m "feat: add CubeType enum"
```

---

## Task 3: `GameManager` core (TDD)

**Goal:** All score / combo / lives state lives in `GameManager`, with deterministic time-stepping that's exercised by EditMode tests.

**Files:**
- Create: `Assets/Tests/EditMode/GameManagerTests.cs`
- Create: `Assets/Scripts/Core/GameManager.cs`

### Step 1: Write failing tests

- [ ] **Step 1.1: Create the test file**

Write `Assets/Tests/EditMode/GameManagerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class GameManagerTests
    {
        private GameObject _go;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GameManager");
            _gm = _go.AddComponent<GameManager>();
            _gm.ConfigureForTests(comboWindowSeconds: 0.5f, startingLives: 3, maxComboMultiplier: 8);
            _gm.ResetGame();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void FirstHit_AwardsBasePointsAtMultiplierOne()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
        }

        [Test]
        public void SecondHitWithinWindow_AwardsBasePointsTimesTwo()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            // First slice: +1 (x1), second slice: +2 (x2) → total 3
            Assert.AreEqual(3, _gm.Score);
        }

        [Test]
        public void ThirdHitWithinWindow_AwardsBasePointsTimesThree()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            // 1 + 2 + 3 = 6
            Assert.AreEqual(6, _gm.Score);
        }

        [Test]
        public void RedCube_AwardsTwoBasePoints()
        {
            _gm.RegisterHit(CubeType.Red, Vector3.zero);
            Assert.AreEqual(2, _gm.Score);
        }

        [Test]
        public void ComboTimer_ExpiresAfterWindow_ResetsMultiplier()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            // After first hit, multiplier should be 2 (for the next slice)
            Assert.AreEqual(2, _gm.ComboMultiplier);
            _gm.Tick(0.6f); // beyond comboWindowSeconds=0.5
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void ComboMultiplier_CapsAtMaxConfigured()
        {
            for (int i = 0; i < 20; i++)
            {
                _gm.RegisterHit(CubeType.Green, Vector3.zero);
                _gm.Tick(0.1f);
            }
            Assert.AreEqual(8, _gm.ComboMultiplier);
        }

        [Test]
        public void RegisterDangerClick_DeductsLifeAndResetsCombo()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);   // combo now 2
            _gm.RegisterDangerClick(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void RegisterMiss_DeductsLifeAndResetsCombo()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            _gm.RegisterMiss();
            Assert.AreEqual(2, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void GameOver_FiresAfterLivesReachZero()
        {
            int finalScoreReported = -1;
            _gm.OnGameOver += s => finalScoreReported = s;
            _gm.RegisterHit(CubeType.Green, Vector3.zero); // score = 1
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            Assert.IsTrue(_gm.IsGameOver);
            Assert.AreEqual(1, finalScoreReported);
        }

        [Test]
        public void ResetGame_RestoresInitialState()
        {
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            _gm.RegisterMiss();
            _gm.ResetGame();
            Assert.AreEqual(0, _gm.Score);
            Assert.AreEqual(3, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
            Assert.IsFalse(_gm.IsGameOver);
        }

        [Test]
        public void OnHit_ReportsAwardedPointsAndMultiplierUsed()
        {
            int reportedPoints = 0;
            int reportedMult = 0;
            _gm.OnHit += (pts, mult, _) => { reportedPoints = pts; reportedMult = mult; };
            _gm.RegisterHit(CubeType.Green, Vector3.zero);
            Assert.AreEqual(1, reportedPoints);
            Assert.AreEqual(1, reportedMult);   // popup shows the mult USED

            _gm.RegisterHit(CubeType.Red, Vector3.zero);
            Assert.AreEqual(4, reportedPoints); // 2 base × 2 mult
            Assert.AreEqual(2, reportedMult);
        }
    }
}
```

- [ ] **Step 1.2: Run tests — expect compile failure**

Open Unity Editor → `Window → General → Test Runner` → EditMode tab → Run All.
Expected: **compile error** (`GameManager` not defined). Do not proceed until you see the compile error specifically about `GameManager` — that confirms the asmdefs wire up correctly.

### Step 2: Implement `GameManager` to pass

- [ ] **Step 2.1: Write `GameManager.cs`**

Write `Assets/Scripts/Core/GameManager.cs`:

```csharp
using System;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Single source of truth for run-time game state. Other components either call
    /// the Register* mutators or subscribe to the events; nobody else writes state.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private float comboWindowSeconds = 0.5f;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int maxComboMultiplier = 8;

        public int Score { get; private set; }
        public int ComboMultiplier { get; private set; } = 1;
        public int Lives { get; private set; }
        public bool IsGameOver { get; private set; }

        private float _comboTimer;

        public event Action<int> OnScoreChanged;
        public event Action<int> OnLivesChanged;
        public event Action<int> OnComboChanged;
        public event Action<int, int, Vector3> OnHit;
        public event Action<int> OnGameOver;

        // ---- Lifecycle ----

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Lives = startingLives;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // ---- Public mutators ----

        public void RegisterHit(CubeType type, Vector3 worldPos)
        {
            if (IsGameOver) return;

            int basePoints = BasePointsFor(type);
            int awardedMult = ComboMultiplier;
            int awardedPoints = basePoints * awardedMult;

            Score += awardedPoints;
            OnScoreChanged?.Invoke(Score);
            OnHit?.Invoke(awardedPoints, awardedMult, worldPos);

            ComboMultiplier = Mathf.Min(ComboMultiplier + 1, maxComboMultiplier);
            _comboTimer = comboWindowSeconds;
            OnComboChanged?.Invoke(ComboMultiplier);
        }

        public void RegisterDangerClick(Vector3 worldPos)
        {
            if (IsGameOver) return;
            OnHit?.Invoke(-1, 1, worldPos);
            ApplyPenalty();
        }

        public void RegisterMiss()
        {
            if (IsGameOver) return;
            ApplyPenalty();
        }

        public void ResetGame()
        {
            Score = 0;
            ComboMultiplier = 1;
            Lives = startingLives;
            _comboTimer = 0f;
            IsGameOver = false;
            Time.timeScale = 1f;

            OnScoreChanged?.Invoke(Score);
            OnLivesChanged?.Invoke(Lives);
            OnComboChanged?.Invoke(ComboMultiplier);
        }

        // ---- Testing seam ----

        /// <summary>EditMode tests call this in place of Time.deltaTime ticks.</summary>
        public void Tick(float deltaTime)
        {
            if (_comboTimer <= 0f) return;
            _comboTimer -= deltaTime;
            if (_comboTimer <= 0f && ComboMultiplier > 1)
            {
                ComboMultiplier = 1;
                OnComboChanged?.Invoke(ComboMultiplier);
            }
        }

        /// <summary>EditMode tests use this so they don't depend on serialized defaults.</summary>
        public void ConfigureForTests(float comboWindowSeconds, int startingLives, int maxComboMultiplier)
        {
            this.comboWindowSeconds = comboWindowSeconds;
            this.startingLives = startingLives;
            this.maxComboMultiplier = maxComboMultiplier;
        }

        // ---- Internals ----

        private void ApplyPenalty()
        {
            Lives = Mathf.Max(0, Lives - 1);
            ComboMultiplier = 1;
            _comboTimer = 0f;
            OnLivesChanged?.Invoke(Lives);
            OnComboChanged?.Invoke(ComboMultiplier);

            if (Lives <= 0) SetGameOver();
        }

        private void SetGameOver()
        {
            IsGameOver = true;
            Time.timeScale = 0f;
            OnGameOver?.Invoke(Score);
        }

        private static int BasePointsFor(CubeType type) => type switch
        {
            CubeType.Green => 1,
            CubeType.Red => 2,
            CubeType.Black => 0,
            _ => 0
        };
    }
}
```

- [ ] **Step 2.2: Run tests — expect all pass**

Test Runner → EditMode → Run All.
Expected: all 11 tests pass.

- [ ] **Step 2.3: Commit**

```bash
git add Assets/Tests/EditMode/GameManagerTests.cs Assets/Scripts/Core/GameManager.cs
git commit -m "feat: add GameManager with combo/score/lives state and EditMode tests"
```

---

## Task 4: `Cube` script

**Goal:** Per-cube behavior — slice handling, fall-off, particle burst spawn.

**Files:**
- Create: `Assets/Scripts/Gameplay/Cube.cs`
- Create: `Assets/Tests/EditMode/CubeTests.cs`

### Step 1: Write failing tests

- [ ] **Step 1.1: Create test file**

Write `Assets/Tests/EditMode/CubeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class CubeTests
    {
        private GameObject _gmGO;
        private GameManager _gm;

        [SetUp]
        public void SetUp()
        {
            _gmGO = new GameObject("GameManager");
            _gm = _gmGO.AddComponent<GameManager>();
            _gm.ConfigureForTests(0.5f, 3, 8);
            _gm.ResetGame();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gmGO);
        }

        [Test]
        public void GreenCube_HandleSlice_RegistersHit()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BlackCube_HandleSlice_DeductsLife()
        {
            var go = new GameObject("Black");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Black);
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_DoubleSlice_OnlyAwardsOnce()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleSlice(Vector3.zero);
            cube.HandleSlice(Vector3.zero); // second call: already consumed
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void GreenCube_HandleFellOff_RegistersMiss()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleFellOff();
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BlackCube_HandleFellOff_NoPenalty()
        {
            var go = new GameObject("Black");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Black);
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_FellOffAfterSlice_NoExtraPenalty()
        {
            var go = new GameObject("Green");
            var cube = go.AddComponent<Cube>();
            cube.ConfigureForTests(CubeType.Green);
            cube.HandleSlice(Vector3.zero);
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives); // no double-count
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 1.2: Run tests — expect compile failure for `Cube`**

Test Runner → EditMode → Run All.
Expected: compile error on missing `Cube` class.

### Step 2: Implement `Cube.cs`

- [ ] **Step 2.1: Write `Cube.cs`**

Write `Assets/Scripts/Gameplay/Cube.cs`:

```csharp
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Attached to every cube prefab. Holds type/visual data and routes slice / fall-off
    /// events to the GameManager. Does no scoring math itself.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Cube : MonoBehaviour
    {
        [SerializeField] private CubeType type;
        [SerializeField] private Color burstTint = Color.white;
        [SerializeField] private ParticleSystem burstPrefab;

        private bool _consumed;

        public CubeType Type => type;

        // Test seam — set type without an Inspector field assignment.
        public void ConfigureForTests(CubeType cubeType)
        {
            type = cubeType;
        }

        public void HandleSlice(Vector3 slicePoint)
        {
            if (_consumed) return;
            _consumed = true;

            SpawnBurst(slicePoint);

            var gm = GameManager.Instance;
            if (gm == null) return; // not in a real scene (e.g. unit test) — fine

            if (type == CubeType.Black) gm.RegisterDangerClick(slicePoint);
            else                       gm.RegisterHit(type, slicePoint);

            if (Application.isPlaying) Destroy(gameObject);
        }

        public void HandleFellOff()
        {
            if (_consumed)
            {
                if (Application.isPlaying) Destroy(gameObject);
                return;
            }
            _consumed = true;

            var gm = GameManager.Instance;
            if (gm != null && type != CubeType.Black) gm.RegisterMiss();

            if (Application.isPlaying) Destroy(gameObject);
        }

        private void SpawnBurst(Vector3 worldPos)
        {
            if (burstPrefab == null) return;
            var burst = Instantiate(burstPrefab, worldPos, Quaternion.identity);
            var main = burst.main;
            main.startColor = burstTint;
            burst.Play();
            Destroy(burst.gameObject, main.duration + main.startLifetime.constantMax);
        }
    }
}
```

Note on `Cube` requiring `Rigidbody` + `Collider`: in EditMode tests we add `Cube` to a bare GameObject, which auto-adds those components. That's fine; tests don't touch physics.

- [ ] **Step 2.2: Run tests — expect all pass**

Expected: 6 new Cube tests + 11 existing GameManager tests = 17 tests passing.

- [ ] **Step 2.3: Commit**

```bash
git add Assets/Scripts/Gameplay/Cube.cs Assets/Tests/EditMode/CubeTests.cs
git commit -m "feat: add Cube script with slice/fall-off and EditMode tests"
```

---

## Task 5: `KillZone` script

**Goal:** Bottom-of-screen trigger that tells cubes they fell off.

**Files:**
- Create: `Assets/Scripts/Gameplay/KillZone.cs`

- [ ] **Step 1: Write `KillZone.cs`**

Write `Assets/Scripts/Gameplay/KillZone.cs`:

```csharp
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Attached to a wide trigger box below the play area. Notifies any Cube that
    /// crosses into it. Cubes are responsible for deciding whether that's a miss.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        private void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var cube = other.GetComponent<Cube>() ?? other.GetComponentInParent<Cube>();
            if (cube != null) cube.HandleFellOff();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Gameplay/KillZone.cs
git commit -m "feat: add KillZone trigger that forwards fall-off to cubes"
```

---

## Task 6: `BladeController` script

**Goal:** Mouse-drag → swept-sphere cast against cubes; drives the blade trail.

**Files:**
- Create: `Assets/Scripts/Gameplay/BladeController.cs`

- [ ] **Step 1: Write `BladeController.cs`**

Write `Assets/Scripts/Gameplay/BladeController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Translates mouse input into a swipe in world space (on a fixed z plane),
    /// then slices any cube the swept sphere passes through. Drives the BladeTip
    /// transform that the TrailRenderer follows.
    /// </summary>
    public class BladeController : MonoBehaviour
    {
        [Header("Slice physics")]
        [SerializeField] private float bladeRadius = 0.25f;
        [SerializeField] private float minSliceSpeed = 4f;
        [SerializeField] private LayerMask cubeMask = ~0;
        [SerializeField] private float playPlaneZ = 0f;

        [Header("Refs")]
        [SerializeField] private Transform bladeTip;
        [SerializeField] private TrailRenderer bladeTrail;
        [SerializeField] private Camera gameCamera;

        private bool _isSwiping;
        private Vector3 _lastTipWorld;
        private readonly HashSet<int> _slicedThisSwipe = new();

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void Update()
        {
            if (gameCamera == null) return;

            Vector3 worldNow = MouseToWorld();
            if (bladeTip != null) bladeTip.position = worldNow;

            if (Input.GetMouseButtonDown(0))
            {
                _isSwiping = true;
                _lastTipWorld = worldNow;
                _slicedThisSwipe.Clear();
                if (bladeTrail != null) bladeTrail.Clear();
            }

            if (Input.GetMouseButtonUp(0))
            {
                _isSwiping = false;
                _slicedThisSwipe.Clear();
            }

            if (_isSwiping)
            {
                if (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
                {
                    TrySlice(_lastTipWorld, worldNow);
                }
                _lastTipWorld = worldNow;
            }
        }

        private Vector3 MouseToWorld()
        {
            // Cast a ray from the camera through the cursor and intersect the world
            // play plane (z = playPlaneZ). This handles angled cameras correctly,
            // unlike ScreenToWorldPoint which projects along the camera's local +Z.
            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Approximately(ray.direction.z, 0f))
            {
                // Ray is parallel to the play plane; fall back to the cursor at z=plane.
                return new Vector3(ray.origin.x, ray.origin.y, playPlaneZ);
            }
            float t = (playPlaneZ - ray.origin.z) / ray.direction.z;
            Vector3 hit = ray.origin + ray.direction * t;
            hit.z = playPlaneZ;
            return hit;
        }

        private void TrySlice(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= Mathf.Epsilon) return;

            float speed = distance / Time.deltaTime;
            if (speed < minSliceSpeed) return;

            var hits = Physics.SphereCastAll(
                from, bladeRadius, delta.normalized, distance, cubeMask, QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
            {
                var cube = hit.collider.GetComponent<Cube>() ?? hit.collider.GetComponentInParent<Cube>();
                if (cube == null) continue;
                int id = cube.GetInstanceID();
                if (!_slicedThisSwipe.Add(id)) continue;
                cube.HandleSlice(hit.point);
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Gameplay/BladeController.cs
git commit -m "feat: add BladeController with swept-sphere slice detection"
```

---

## Task 7: `CubeSpawner` script

**Goal:** Timed spawner with difficulty curves and weighted color rolls.

**Files:**
- Create: `Assets/Scripts/Gameplay/CubeSpawner.cs`

- [ ] **Step 1: Write `CubeSpawner.cs`**

Write `Assets/Scripts/Gameplay/CubeSpawner.cs`:

```csharp
using System.Collections;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Spawns cubes from a horizontal line at the bottom of the play area, with
    /// difficulty curves driving the spawn interval and danger-cube probability.
    /// </summary>
    public class CubeSpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private Cube greenPrefab;
        [SerializeField] private Cube redPrefab;
        [SerializeField] private Cube blackPrefab;

        [Header("Spawn line")]
        [SerializeField] private Transform spawnLineLeft;
        [SerializeField] private Transform spawnLineRight;

        [Header("Difficulty")]
        [SerializeField] private AnimationCurve spawnIntervalOverTime =
            AnimationCurve.Linear(0f, 1.2f, 60f, 0.4f);
        [SerializeField] private AnimationCurve dangerProbabilityOverTime =
            AnimationCurve.Linear(0f, 0.05f, 60f, 0.15f);
        [SerializeField, Range(0f, 1f)] private float redWeightOfNonDanger = 0.25f;

        [Header("Launch impulse")]
        [SerializeField] private Vector2 launchImpulseRange = new(7f, 11f);
        [SerializeField] private Vector2 sideImpulseRange = new(0f, 2f);

        private float _runStartTime;

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
        }

        private void Start()
        {
            _runStartTime = Time.time;
            StartCoroutine(SpawnLoop());
        }

        private void HandleGameOver(int _) { /* loop self-pauses via IsGameOver guard */ }

        public void NotifyRunRestarted()
        {
            _runStartTime = Time.time;
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                float elapsed = Time.time - _runStartTime;
                float interval = Mathf.Max(0.05f, spawnIntervalOverTime.Evaluate(elapsed));
                yield return new WaitForSeconds(interval);

                if (GameManager.Instance != null && GameManager.Instance.IsGameOver) continue;
                SpawnOne(elapsed);
            }
        }

        private void SpawnOne(float elapsed)
        {
            if (spawnLineLeft == null || spawnLineRight == null) return;

            float dangerP = Mathf.Clamp01(dangerProbabilityOverTime.Evaluate(elapsed));
            Cube prefab = ChoosePrefab(dangerP);
            if (prefab == null) return;

            float x = Random.Range(spawnLineLeft.position.x, spawnLineRight.position.x);
            Vector3 pos = new Vector3(x, spawnLineLeft.position.y, spawnLineLeft.position.z);
            Quaternion rot = Random.rotation;

            Cube cube = Instantiate(prefab, pos, rot);
            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                float side = Random.Range(sideImpulseRange.x, sideImpulseRange.y);
                side *= Random.value < 0.5f ? -1f : 1f;
                float up = Random.Range(launchImpulseRange.x, launchImpulseRange.y);
                rb.AddForce(new Vector3(side, up, 0f), ForceMode.Impulse);
            }
        }

        private Cube ChoosePrefab(float dangerP)
        {
            float roll = Random.value;
            if (roll < dangerP) return blackPrefab;
            float redP = redWeightOfNonDanger * (1f - dangerP);
            if (roll < dangerP + redP) return redPrefab;
            return greenPrefab;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Gameplay/CubeSpawner.cs
git commit -m "feat: add CubeSpawner with difficulty curves and weighted rolls"
```

---

## Task 8: UI views — Score, Lives, Combo Badge

**Goal:** Three read-only UI listeners.

**Files:**
- Create: `Assets/Scripts/UI/ScoreView.cs`
- Create: `Assets/Scripts/UI/LivesView.cs`
- Create: `Assets/Scripts/UI/ComboBadgeView.cs`

- [ ] **Step 1: Write `ScoreView.cs`**

```csharp
using TMPro;
using UnityEngine;

namespace OpenNinja
{
    public class ScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "Score: {0}";

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnScoreChanged += UpdateScore;
            UpdateScore(GameManager.Instance.Score);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnScoreChanged -= UpdateScore;
        }

        private void UpdateScore(int score)
        {
            if (label != null) label.text = string.Format(format, score);
        }
    }
}
```

- [ ] **Step 2: Write `LivesView.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// Lights the first N heart images based on current lives. Hearts are assigned
    /// in the Inspector in order (left-to-right).
    /// </summary>
    public class LivesView : MonoBehaviour
    {
        [SerializeField] private Image[] hearts;
        [SerializeField] private Color aliveColor = Color.white;
        [SerializeField] private Color lostColor = new Color(1f, 1f, 1f, 0.2f);

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnLivesChanged += UpdateLives;
            UpdateLives(GameManager.Instance.Lives);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnLivesChanged -= UpdateLives;
        }

        private void UpdateLives(int lives)
        {
            if (hearts == null) return;
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                hearts[i].color = i < lives ? aliveColor : lostColor;
            }
        }
    }
}
```

- [ ] **Step 3: Write `ComboBadgeView.cs`**

```csharp
using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Shows "Combo x{n}" while the multiplier is >= 2; hides itself otherwise.
    /// </summary>
    public class ComboBadgeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject root;

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnComboChanged += UpdateBadge;
            UpdateBadge(GameManager.Instance.ComboMultiplier);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnComboChanged -= UpdateBadge;
        }

        private void UpdateBadge(int mult)
        {
            bool show = mult > 1;
            if (root != null) root.SetActive(show);
            if (show && label != null) label.text = $"Combo x{mult}";
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/ScoreView.cs Assets/Scripts/UI/LivesView.cs Assets/Scripts/UI/ComboBadgeView.cs
git commit -m "feat: add Score, Lives, and ComboBadge view scripts"
```

---

## Task 9: ComboPopup + spawner

**Goal:** Floating `+N x{mult}!` text that appears at the slice point and animates up + fades.

**Files:**
- Create: `Assets/Scripts/UI/ComboPopup.cs`
- Create: `Assets/Scripts/UI/ComboPopupSpawner.cs`

- [ ] **Step 1: Write `ComboPopup.cs`**

```csharp
using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Self-animating UI floater. Initialize() sets the text + color, then it
    /// moves up and fades out over `lifetime` seconds and destroys itself.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ComboPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float floatPixels = 60f;

        private RectTransform _rt;
        private Vector2 _startAnchored;
        private float _elapsed;
        private Color _baseColor;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            if (label != null) _baseColor = label.color;
        }

        public void Initialize(string text, Color color)
        {
            if (label != null)
            {
                label.text = text;
                label.color = color;
                _baseColor = color;
            }
            _startAnchored = _rt.anchoredPosition;
            _elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime; // outlive scene Time.timeScale freezes
            float t = Mathf.Clamp01(_elapsed / lifetime);
            _rt.anchoredPosition = _startAnchored + new Vector2(0f, floatPixels * t);
            if (label != null)
            {
                var c = _baseColor;
                c.a = 1f - t;
                label.color = c;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 2: Write `ComboPopupSpawner.cs`**

```csharp
using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Listens to GameManager.OnHit and spawns a ComboPopup at the corresponding
    /// screen position (under a UI RectTransform anchor).
    /// </summary>
    public class ComboPopupSpawner : MonoBehaviour
    {
        [SerializeField] private ComboPopup popupPrefab;
        [SerializeField] private RectTransform layer;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private Color positiveColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] private Color negativeColor = new Color(1f, 0.3f, 0.3f);

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHit -= HandleHit;
        }

        private void HandleHit(int points, int mult, Vector3 worldPos)
        {
            if (popupPrefab == null || layer == null || gameCamera == null) return;

            string text;
            Color color;
            if (points < 0)
            {
                text = $"{points}!";
                color = negativeColor;
            }
            else if (mult <= 1)
            {
                text = $"+{points}";
                color = positiveColor;
            }
            else
            {
                text = $"+{points} x{mult}!";
                color = positiveColor;
            }

            Vector2 screen = gameCamera.WorldToScreenPoint(worldPos);
            // Convert screen point → local point inside the layer's RectTransform.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                layer, screen, layer.GetComponentInParent<Canvas>().worldCamera, out var local);

            var popup = Instantiate(popupPrefab, layer);
            popup.GetComponent<RectTransform>().anchoredPosition = local;
            popup.Initialize(text, color);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/ComboPopup.cs Assets/Scripts/UI/ComboPopupSpawner.cs
git commit -m "feat: add ComboPopup floater and spawner"
```

---

## Task 10: `GameOverView` script

**Goal:** Show the panel on game over and wire up Restart.

**Files:**
- Create: `Assets/Scripts/UI/GameOverView.cs`

- [ ] **Step 1: Write `GameOverView.cs`**

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
        [SerializeField] private CubeSpawner spawner;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver += Show;
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= Show;
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        private void Show(int finalScore)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (finalScoreLabel != null) finalScoreLabel.text = $"Score: {finalScore}";
        }

        private void OnRestartClicked()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            GameManager.Instance?.ResetGame();
            spawner?.NotifyRunRestarted();
            // Also destroy any cubes left in the scene (e.g. frozen mid-flight).
            foreach (var cube in FindObjectsByType<Cube>(FindObjectsSortMode.None))
            {
                Destroy(cube.gameObject);
            }
        }
    }
}
```

- [ ] **Step 2: Run all tests one more time to confirm nothing regressed**

Expected: 17 tests pass (no new ones in this task).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/GameOverView.cs
git commit -m "feat: add GameOverView with restart wiring"
```

---

## Task 11: Materials and Cube prefabs

**Goal:** Three colored materials and three cube prefabs that reference the right material + `Cube` component.

**Files:**
- Create (via Unity Editor): `Assets/Materials/Cube_Green.mat`, `Cube_Red.mat`, `Cube_Black.mat`
- Create (via Unity Editor): `Assets/Prefabs/Cube_Green.prefab`, `Cube_Red.prefab`, `Cube_Black.prefab`

**Two paths — pick whichever works:**

### Path A: Use `coplay-mcp` tools (Unity Editor must be open)

- [ ] **Step 1: Create materials**

For each color, call `mcp__coplay-mcp__create_material` with:
- Green: name `Cube_Green`, baseColor `#4CAF50` (or `0.298, 0.686, 0.314, 1`), saved to `Assets/Materials/`.
- Red: name `Cube_Red`, baseColor `#E53935` (`0.898, 0.224, 0.208, 1`).
- Black: name `Cube_Black`, baseColor `#212121` (`0.129, 0.129, 0.129, 1`), with `smoothness` ~ 0.3 so the danger cube still reads.

- [ ] **Step 2: Create the green cube prefab**

1. `mcp__coplay-mcp__create_game_object`: name `Cube_Green`, primitive `Cube`.
2. `mcp__coplay-mcp__assign_material` to the Cube's `MeshRenderer` referencing `Assets/Materials/Cube_Green.mat`.
3. `mcp__coplay-mcp__add_component`: `Rigidbody` (mass 1, useGravity true, drag 0, angularDrag 0.05).
4. The primitive Cube already has a `BoxCollider`.
5. `mcp__coplay-mcp__add_component`: `OpenNinja.Cube`. Then `mcp__coplay-mcp__set_property` for fields:
   - `type` = `0` (Green)
   - `burstTint` = `0.298, 0.686, 0.314, 1`
   - `burstPrefab` = (leave null for now — populated in Task 12)
6. Set `layer` to a new layer called `Cube` (create via `mcp__coplay-mcp__set_layer`; layer index 8 is typical). The `BladeController`'s `cubeMask` will reference this layer.
7. `mcp__coplay-mcp__create_prefab`: save at `Assets/Prefabs/Cube_Green.prefab`. Delete the scene instance afterward.

- [ ] **Step 3: Duplicate for red and black**

Use `mcp__coplay-mcp__duplicate_asset` from `Cube_Green.prefab` to `Cube_Red.prefab` and `Cube_Black.prefab`. For each, open the prefab and change:
- `Cube_Red`: `type` = `1`, `burstTint` = `0.898, 0.224, 0.208, 1`, material = `Cube_Red.mat`.
- `Cube_Black`: `type` = `2`, `burstTint` = `0.6, 0.6, 0.6, 1`, material = `Cube_Black.mat`.

### Path B: Manual Unity Editor steps

For each material:
1. `Project window → right-click Assets/Materials → Create → Material`. Name `Cube_Green` (etc.). Set Albedo / Base Color to the values above. For URP/HDRP, use the project's Lit shader.

For each prefab:
1. `Hierarchy → +` → `3D Object → Cube`. Rename `Cube_Green`.
2. Drag the matching material onto it.
3. `Add Component → Rigidbody`. Leave defaults.
4. `Add Component → Cube` (the script you wrote). Set `Type`, `Burst Tint`, leave `Burst Prefab` empty.
5. `Layer` → create a layer named `Cube` (or pick the first free user layer, e.g. 8) and assign it.
6. Drag `Cube_Green` from Hierarchy into `Assets/Prefabs/` to make a prefab. Delete the Hierarchy instance.
7. Right-click `Cube_Green.prefab` → Duplicate → rename to `Cube_Red`. Open it, swap material, set `Type = Red`, `Burst Tint = red`. Save.
8. Repeat for `Cube_Black`.

- [ ] **Step 4: Commit**

```bash
git add Assets/Materials Assets/Prefabs
git commit -m "feat: add Cube_Green/Red/Black materials and prefabs"
```

---

## Task 12: SliceBurst and ComboPopup prefabs

**Goal:** Particle prefab for the slice burst and the UI prefab for the floating combo popup.

### Step 1: SliceBurst prefab

- [ ] **Step 1.1: Create the particle system**

Path A (coplay-mcp): `mcp__coplay-mcp__create_game_object` → primitive `ParticleSystem` named `SliceBurst`. Then use `mcp__coplay-mcp__set_property` to configure the ParticleSystem (`main.duration = 0.5`, `main.startLifetime = 0.8`, `main.startSpeed = 4`, `main.gravityModifier = 1.5`, `emission.rateOverTime = 0`, `emission.burstCount = 1` with 20 particles at t=0, `shape.shapeType = Sphere`, `shape.radius = 0.1`).

Path B (manual): `Hierarchy → + → Effects → Particle System`. Set:
- Duration `0.5`, Start Lifetime `0.6`, Start Speed `4`, Gravity Modifier `1.5`, Start Size `0.1`, Looping `off`.
- Emission: Rate over Time `0`, Bursts → add one at time `0`, count `20`.
- Shape: Sphere, Radius `0.1`.
- Renderer: Material `Default-Particle` (built-in) or any unlit particle material.

- [ ] **Step 1.2: Make it a prefab**

Save as `Assets/Prefabs/SliceBurst.prefab`. Delete the scene instance.

- [ ] **Step 1.3: Wire it into the cube prefabs**

For each of `Cube_Green`, `Cube_Red`, `Cube_Black`, open the prefab and drag `SliceBurst.prefab` into the `Burst Prefab` field on the `Cube` component.

### Step 2: ComboPopup prefab

- [ ] **Step 2.1: Build the popup**

Path B (manual is clearest here):

1. In the Hierarchy, create a temporary Canvas (`UI → Canvas`) — Render Mode: Screen Space - Overlay.
2. Inside, `UI → Text - TextMeshPro`. (If prompted, import TMP Essentials.)
3. Name the root `ComboPopup`. Set the TMP_Text font size to ~48, color white, alignment center, default text `+1`.
4. `Add Component → ComboPopup` on the root. Assign the TMP_Text to the `label` field.
5. Drag the `ComboPopup` GameObject (not the canvas) into `Assets/Prefabs/`.
6. Delete the temporary Canvas in the scene.

- [ ] **Step 3: Commit**

```bash
git add Assets/Prefabs
git commit -m "feat: add SliceBurst and ComboPopup prefabs"
```

---

## Task 13: Scene assembly

**Goal:** Wire the prefabs, scripts, camera, killzone, and UI together into a playable scene.

**Files:**
- Create/Modify (via Unity Editor): `Assets/Scenes/SampleScene.unity`

This task is editor-heavy. Where `coplay-mcp` shortens a step, it's noted; otherwise do it manually in the Editor.

- [ ] **Step 1: Open / create the main scene**

If `Assets/Scenes/SampleScene.unity` doesn't exist, `File → New Scene → Basic (URP/Built-in)` and `File → Save As → Assets/Scenes/SampleScene.unity`. Set this scene as the only scene in `File → Build Settings → Scenes In Build`.

- [ ] **Step 2: Configure the camera**

- Select `Main Camera`.
- Position `(0, 4, -12)`, Rotation `(20, 0, 0)`. (Looks slightly down at z=0.)
- Projection: Perspective, FOV `60`.
- Clear Flags: Solid Color, dark color (e.g. `#0F0F1F`).

- [ ] **Step 3: Add Systems hierarchy**

Create empty GameObjects:
- `--- Bounds ---` (empty, organization only)
  - `KillZone` — `Add Component → BoxCollider` (size ~ `(40, 2, 4)`, set `Is Trigger = true`). `Add Component → KillZone` script. Position `(0, -8, 0)`.
- `--- Systems ---` (empty)
  - `GameManager` — `Add Component → GameManager`. Defaults are fine.
  - `CubeSpawner` — `Add Component → CubeSpawner`. Assign the three cube prefabs from `Assets/Prefabs/`. Create two empty child GameObjects `SpawnLeft` and `SpawnRight` and position them at `(-7, -6, 0)` and `(7, -6, 0)`; assign them to `spawnLineLeft` / `spawnLineRight`.
  - `BladeController` — `Add Component → BladeController`. Set `cubeMask` to the `Cube` layer only. Create an empty child `BladeTip` and assign it to `bladeTip`. Add `TrailRenderer` component on `BladeTip`:
    - Time `0.15`
    - Min Vertex Distance `0.02`
    - Width: animation curve from `0.15` at t=0 to `0` at t=1
    - Color: white → transparent gradient
    - Material: `Default-Line` (or any additive trail material)
    - Assign the TrailRenderer to `bladeTrail` on `BladeController`.

- [ ] **Step 4: Add UI Canvas**

- Create `Canvas` (Render Mode: Screen Space - Overlay; UI Scale Mode: Scale With Screen Size, ref `1920x1080`). EventSystem auto-creates.
- Inside, build:
  - `ScorePanel` — `UI → Text - TextMeshPro`. Anchor top-left, offset `(40, -40)`. Font size `48`. Add `ScoreView` to this GameObject; assign `label`.
  - `ComboBadge` — empty GameObject anchored top-center. Child `Label` is a TMP_Text reading `Combo x2`. Add `ComboBadgeView` to `ComboBadge`; assign `label` and `root` (root = the ComboBadge GameObject itself).
  - `LivesRow` — Horizontal Layout Group, anchor top-right, offset `(-40, -40)`. Add three child `Image` GameObjects called `Heart_1/2/3`. Use Unity's built-in `UISprite` or a heart sprite of your choice; tint white. Add `LivesView` to `LivesRow`; populate `hearts` array with the three Images.
  - `ComboPopupLayer` — empty RectTransform stretching full-screen (anchors min `(0,0)`, max `(1,1)`). Add `ComboPopupSpawner` to it; assign `popupPrefab = Assets/Prefabs/ComboPopup`, `layer = ComboPopupLayer`'s RectTransform, `gameCamera = Main Camera`.
  - `GameOverPanel` — full-screen panel, dimmed background `(0,0,0,0.7)`. Inside add a TMP_Text `FinalScore` and a Button `RestartButton` with a TMP_Text child reading `Restart`. Add `GameOverView` to this panel; assign `panelRoot = GameOverPanel`, `finalScoreLabel`, `restartButton`, `spawner = Systems/CubeSpawner`.

- [ ] **Step 5: Make sure each cube prefab is on the `Cube` layer**

Open each of the three cube prefabs and verify the Layer dropdown is set to `Cube` (the one referenced by `BladeController.cubeMask`).

- [ ] **Step 6: Save the scene and commit**

```bash
git add Assets/Scenes/SampleScene.unity ProjectSettings
git commit -m "feat: assemble main scene with camera, systems, killzone, UI"
```

---

## Task 14: Playtest verification

**Goal:** Press Play and confirm the game behaves per the spec.

This task does no code work — it's the manual verification gate before declaring the base done.

- [ ] **Step 1: Press Play in the Editor**

If `coplay-mcp` is available: `mcp__coplay-mcp__play_game`. Then capture screenshots with `mcp__coplay-mcp__capture_scene_object` at each checkpoint below. Otherwise just play visually.

- [ ] **Step 2: Verify the happy path**

- Cubes spawn from the bottom and arc back down ✅
- Drag the mouse across a cube fast → cube bursts and score goes up ✅
- Move the cursor slowly over a cube → nothing happens (below speed threshold) ✅
- Slice two cubes within 0.5 s → "Combo x2" badge appears; score goes up by `2 × basePoints` on the second ✅
- After 0.5 s with no slices, the badge disappears ✅

- [ ] **Step 3: Verify penalties**

- Let a green cube fall to the bottom → one heart dims; combo resets ✅
- Slice a black cube → one heart dims; combo resets ✅
- Let a black cube fall off → no penalty ✅

- [ ] **Step 4: Verify game over**

- Lose all 3 hearts → game freezes mid-air, GameOverPanel shows with final score ✅
- Click Restart → cubes clear, score resets to 0, hearts refill, spawning resumes ✅

- [ ] **Step 5: Verify difficulty ramp (informal)**

After ~60 s of play, confirm spawn rate has noticeably increased and black cubes appear more frequently. Tune `spawnIntervalOverTime` and `dangerProbabilityOverTime` curves on the `CubeSpawner` if pacing feels off.

- [ ] **Step 6: Final commit (if any tuning changes were made)**

```bash
git status
git add Assets ProjectSettings
git commit -m "tune: adjust spawn curves after playtest"
```

If no changes are needed, skip this step.

---

## Done criteria

- All 17 EditMode tests pass.
- A full play session: spawn → slice → combo → miss → game over → restart works end-to-end.
- No console errors during play.
- Scene + prefab assets exist under `Assets/Scenes/`, `Assets/Prefabs/`, `Assets/Materials/`.
