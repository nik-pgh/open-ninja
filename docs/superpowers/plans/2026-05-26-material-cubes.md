# Material Cubes & Bouncy Walls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the three color cube types with six material-driven cubes (Wood/Stone/Metal/Crystal/Spiked/Rubber), wrap the playfield with bumpy walls so cubes ricochet irregularly, and convey "sense of mass" through visual scale, audio, hit-stop, and blade-trail lag.

**Architecture:** A new `CubeMaterial` ScriptableObject holds all per-material data (mass, points, role, scale, render material, bounciness multiplier, audio). `Cube` becomes a single reusable prefab that reads its `CubeMaterial` at spawn time. `CubeSpawner` picks materials via a weighted list. Walls are static GameObjects with segmented colliders (±8° angle jitter) so reflection direction varies bounce-to-bounce. Sense of mass is conveyed by `HitStopController` (brief `Time.timeScale` dip) + `BladeController.ApplySliceDrag` + `AudioOneShot` with mass-pitched bounce SFX.

**Tech Stack:** Unity 6, C#, Unity Physics, TextMeshPro, Unity Test Framework (EditMode + PlayMode), `coplay-mcp` for Editor-driven asset and scene setup.

**Spec:** `docs/superpowers/specs/2026-05-26-material-cubes-design.md`

---

## File Structure

```
NEW   Assets/Scripts/Core/CubeMaterial.cs           ScriptableObject + CubeRole enum
NEW   Assets/Scripts/Gameplay/HitStopController.cs  Static helper + hidden MonoBehaviour runner
NEW   Assets/Scripts/Util/AudioOneShot.cs           Pitched one-shot helper
MOD   Assets/Scripts/Gameplay/Cube.cs               Reads CubeMaterial; OnCollisionEnter for bounce SFX + bounciness mult
MOD   Assets/Scripts/Gameplay/CubeSpawner.cs        MaterialEntry list + weighted picker + per-material impulse override
MOD   Assets/Scripts/Gameplay/BladeController.cs    ApplySliceDrag method + per-frame trail lag
MOD   Assets/Scripts/Core/GameManager.cs            RegisterHit takes (int basePoints, Vector3)
DEL   Assets/Scripts/Core/CubeType.cs               replaced by CubeMaterial
DEL   Assets/Prefabs/Cube_Green/Red/Black.prefab    replaced by single Cube.prefab
NEW   Assets/Prefabs/Cube.prefab                    base prefab (no material assigned)
NEW   Assets/Materials/Wood|Stone|Metal|Crystal|Spiked|Rubber.mat
NEW   Assets/Data/CubeMaterials/Wood|Stone|Metal|Crystal|Spiked|Rubber.asset
NEW   Assets/Data/BouncyWall.physicMaterial
MOD   Assets/Editor/SceneSetup.cs                   build walls; build all assets via helpers; assign SO refs on spawner
MOD   Assets/Tests/EditMode/GameManagerTests.cs     new RegisterHit signature
MOD   Assets/Tests/EditMode/CubeTests.cs            uses in-memory CubeMaterial
NEW   Assets/Tests/EditMode/CubeMaterialTests.cs    smoke tests for SO field round-trip
NEW   Assets/Tests/EditMode/SpawnerWeightingTests.cs Probability distribution over many rolls (statistical, ±5%)
```

The unified `Cube.prefab` replaces the three colored ones because the cube's appearance (mass, scale, render material) is now driven entirely by the runtime `CubeMaterial` assignment.

---

## Conventions

- Working directory: `/Users/nikkim/dev/open-ninja`. Use a fresh worktree via the `using-git-worktrees` skill before starting Task 1.
- C# namespace `OpenNinja` for runtime, `OpenNinja.Tests` for tests, `OpenNinja.EditorSetup` for Editor-only scripts.
- "Run tests" = Unity Editor → `Window → General → Test Runner` → EditMode → Run All. CLI alternative: `Unity -batchmode -projectPath <repo> -runTests -testPlatform editmode -nographics -logFile -`.
- "Open Unity Editor" — most tasks edit C# only and don't require the Editor to be running. Tasks 8–10 (asset & scene creation) require Unity Editor open with the Coplay MCP plugin connected, so the implementer should verify `mcp__coplay-mcp__get_unity_editor_state` works before those tasks.
- After every code change, run `mcp__coplay-mcp__check_compile_errors` if Unity Editor is open; otherwise assume clean compile when the diff matches the plan.

---

## Task 1: `CubeMaterial` ScriptableObject + `CubeRole` enum

**Goal:** Introduce the new data-driven type. Pure addition — nothing else changes yet, project still compiles.

**Files:**
- Create: `Assets/Scripts/Core/CubeMaterial.cs`

- [ ] **Step 1: Write `CubeMaterial.cs`**

```csharp
using UnityEngine;

namespace OpenNinja
{
    public enum CubeRole
    {
        Normal = 0,
        Bonus = 1,
        Danger = 2,
    }

    /// <summary>
    /// Per-material data driving a Cube at spawn time. Each in-game material
    /// (Wood, Stone, Metal, Crystal, Spiked, Rubber) is an instance of this
    /// asset. Mass and impulse drive physics; basePoints and role drive
    /// scoring; renderMaterial / displayScale drive presentation; bounce and
    /// audio fields drive sense-of-mass feedback.
    /// </summary>
    [CreateAssetMenu(menuName = "OpenNinja/Cube Material", fileName = "CubeMaterial")]
    public class CubeMaterial : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        public CubeRole role = CubeRole.Normal;
        public int basePoints = 1;

        [Header("Physics")]
        [Min(0.05f)] public float mass = 1f;
        [Range(0.2f, 2f)] public float displayScale = 1f;
        [Range(0.1f, 2f)] public float bouncinessMultiplier = 1f;
        /// <summary>If both components are 0, the spawner falls back to its default range.</summary>
        public Vector2 launchImpulseOverride;

        [Header("Presentation")]
        public Material renderMaterial;
        public Color burstTint = Color.white;

        [Header("Audio")]
        public AudioClip impactClip;
        [Range(0.1f, 4f)] public float audioPitchAtMassOne = 1f;
        [Range(0f, 1f)] public float audioVolume = 0.7f;

        public bool HasLaunchOverride =>
            launchImpulseOverride.x > 0f && launchImpulseOverride.y > 0f;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Core/CubeMaterial.cs
git commit -m "feat: add CubeMaterial ScriptableObject + CubeRole enum"
```

---

## Task 2: `GameManager` — `RegisterHit(int, Vector3)` (TDD)

**Goal:** Strip `RegisterHit` of its `CubeType` parameter so it no longer cares what the cube is made of — just how many points it's worth. Behavior is unchanged; only the signature.

**Files:**
- Modify: `Assets/Scripts/Core/GameManager.cs`
- Modify: `Assets/Tests/EditMode/GameManagerTests.cs`

### Step 1: Update tests (they will fail)

- [ ] **Step 1.1: Replace `CubeType.X` calls in tests**

Edit `Assets/Tests/EditMode/GameManagerTests.cs`. Find every `_gm.RegisterHit(CubeType.Green, Vector3.zero)` and replace with `_gm.RegisterHit(1, Vector3.zero)`. Find every `_gm.RegisterHit(CubeType.Red, Vector3.zero)` and replace with `_gm.RegisterHit(2, Vector3.zero)`. The complete updated file:

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
            Time.timeScale = 1f;
        }

        [Test]
        public void FirstHit_AwardsBasePointsAtMultiplierOne()
        {
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
        }

        [Test]
        public void SecondHitWithinWindow_AwardsBasePointsTimesTwo()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(3, _gm.Score);
        }

        [Test]
        public void ThirdHitWithinWindow_AwardsBasePointsTimesThree()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(1, Vector3.zero);
            _gm.Tick(0.1f);
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(6, _gm.Score);
        }

        [Test]
        public void RedCube_AwardsTwoBasePoints()
        {
            _gm.RegisterHit(2, Vector3.zero);
            Assert.AreEqual(2, _gm.Score);
        }

        [Test]
        public void ComboTimer_ExpiresAfterWindow_ResetsMultiplier()
        {
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(2, _gm.ComboMultiplier);
            _gm.Tick(0.6f);
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void ComboMultiplier_CapsAtMaxConfigured()
        {
            for (int i = 0; i < 20; i++)
            {
                _gm.RegisterHit(1, Vector3.zero);
                _gm.Tick(0.1f);
            }
            Assert.AreEqual(8, _gm.ComboMultiplier);
        }

        [Test]
        public void RegisterDangerClick_DeductsLifeAndResetsComboAndFiresEvents()
        {
            int livesEventValue = -1;
            _gm.OnLivesChanged += v => livesEventValue = v;
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterDangerClick(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
            Assert.AreEqual(2, livesEventValue);
        }

        [Test]
        public void RegisterMiss_DeductsLifeAndResetsCombo()
        {
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterMiss();
            Assert.AreEqual(2, _gm.Lives);
            Assert.AreEqual(1, _gm.ComboMultiplier);
        }

        [Test]
        public void GameOver_FiresAfterLivesReachZero()
        {
            int finalScoreReported = -1;
            _gm.OnGameOver += s => finalScoreReported = s;
            _gm.RegisterHit(1, Vector3.zero);
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            Assert.IsTrue(_gm.IsGameOver);
            Assert.AreEqual(1, finalScoreReported);
        }

        [Test]
        public void ResetGame_RestoresInitialState()
        {
            _gm.RegisterHit(1, Vector3.zero);
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
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(1, reportedPoints);
            Assert.AreEqual(1, reportedMult);

            _gm.RegisterHit(2, Vector3.zero);
            Assert.AreEqual(4, reportedPoints);
            Assert.AreEqual(2, reportedMult);
        }

        [Test]
        public void RegisterHit_AfterGameOver_IsNoOp()
        {
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            _gm.RegisterMiss();
            Assert.IsTrue(_gm.IsGameOver);

            int scoreBefore = _gm.Score;
            _gm.RegisterHit(1, Vector3.zero);
            Assert.AreEqual(scoreBefore, _gm.Score);
        }
    }
}
```

- [ ] **Step 1.2: Run tests — expect compile failure**

Run EditMode tests. Expected: compile error referencing `GameManager.RegisterHit(int, Vector3)` not found — current signature is `RegisterHit(CubeType, Vector3)`.

### Step 2: Update GameManager

- [ ] **Step 2.1: Change `RegisterHit` signature**

Edit `Assets/Scripts/Core/GameManager.cs`. Find:

```csharp
public void RegisterHit(CubeType type, Vector3 worldPos)
{
    if (IsGameOver) return;

    int basePoints = BasePointsFor(type);
    int awardedMult = ComboMultiplier;
    int awardedPoints = basePoints * awardedMult;
    ...
}
```

Replace with:

```csharp
public void RegisterHit(int basePoints, Vector3 worldPos)
{
    if (IsGameOver) return;

    int awardedMult = ComboMultiplier;
    int awardedPoints = basePoints * awardedMult;

    Score += awardedPoints;
    OnScoreChanged?.Invoke(Score);
    OnHit?.Invoke(awardedPoints, awardedMult, worldPos);

    ComboMultiplier = Mathf.Min(ComboMultiplier + 1, maxComboMultiplier);
    _comboTimer = comboWindowSeconds;
    OnComboChanged?.Invoke(ComboMultiplier);
}
```

- [ ] **Step 2.2: Delete the `BasePointsFor` helper**

At the bottom of `GameManager.cs`, delete the entire method:

```csharp
private static int BasePointsFor(CubeType type) => type switch
{
    CubeType.Green => 1,
    CubeType.Red => 2,
    CubeType.Black => 0,
    _ => 0
};
```

(Leave every other line in `GameManager.cs` untouched. The `using System; using UnityEngine;` stays — nothing in the file still references `CubeType`.)

- [ ] **Step 2.3: Patch the call site in `Cube.cs` so the project still compiles**

The existing `Cube.cs` still references the old signature. Until Task 3 fully rewrites it, we need a one-line patch. In `Assets/Scripts/Gameplay/Cube.cs`, find:

```csharp
if (type == CubeType.Black) gm.RegisterDangerClick(slicePoint);
else                       gm.RegisterHit(type, slicePoint);
```

Replace with:

```csharp
if (type == CubeType.Black) gm.RegisterDangerClick(slicePoint);
else                       gm.RegisterHit(type == CubeType.Red ? 2 : 1, slicePoint);
```

This is a temporary shim — Task 3 deletes this entire branch. It only exists so the codebase compiles between Task 2 and Task 3.

- [ ] **Step 2.4: Run tests — expect pass**

Expected: all 12 GameManager EditMode tests pass. Project compiles cleanly.

- [ ] **Step 2.5: Commit**

```bash
git add Assets/Scripts/Core/GameManager.cs Assets/Tests/EditMode/GameManagerTests.cs Assets/Scripts/Gameplay/Cube.cs
git commit -m "refactor(GameManager): RegisterHit takes basePoints (int)"
```

---

## Task 3: `Cube` script — reads `CubeMaterial`, OnCollisionEnter for bounce + audio (TDD)

**Goal:** `Cube` no longer holds `type` / `burstTint` / `burstPrefab` as Inspector fields. Instead it holds one `[SerializeField] CubeMaterial material`. On `Initialize(material)` it applies mass, scale, render material, and layer. On collision with a `Wall`-tagged object, it plays the impact audio and applies the bounciness multiplier.

**Files:**
- Modify: `Assets/Scripts/Gameplay/Cube.cs`
- Modify: `Assets/Tests/EditMode/CubeTests.cs`

### Step 1: Update tests (they will fail)

- [ ] **Step 1.1: Rewrite `CubeTests.cs`**

```csharp
using NUnit.Framework;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class CubeTests
    {
        private GameObject _gmGO;
        private GameManager _gm;

        private static CubeMaterial MakeMaterial(int basePoints, CubeRole role, float mass = 1f)
        {
            var mat = ScriptableObject.CreateInstance<CubeMaterial>();
            mat.basePoints = basePoints;
            mat.role = role;
            mat.mass = mass;
            mat.displayScale = 1f;
            mat.bouncinessMultiplier = 1f;
            mat.burstTint = Color.white;
            return mat;
        }

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
            Time.timeScale = 1f;
        }

        [Test]
        public void NormalCube_HandleSlice_RegistersHit()
        {
            var go = new GameObject("Green", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void BonusCube_HandleSlice_AwardsBasePoints()
        {
            var go = new GameObject("Bonus", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(5, CubeRole.Bonus));
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(5, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DangerCube_HandleSlice_DeductsLife()
        {
            var go = new GameObject("Spiked", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(0, CubeRole.Danger));
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_DoubleSlice_OnlyAwardsOnce()
        {
            var go = new GameObject("Wood", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleSlice(Vector3.zero);
            cube.HandleSlice(Vector3.zero);
            Assert.AreEqual(1, _gm.Score);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void NormalCube_HandleFellOff_RegistersMiss()
        {
            var go = new GameObject("Wood", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleFellOff();
            Assert.AreEqual(2, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DangerCube_HandleFellOff_NoPenalty()
        {
            var go = new GameObject("Spiked", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(0, CubeRole.Danger));
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Cube_FellOffAfterSlice_NoExtraPenalty()
        {
            var go = new GameObject("Wood", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            cube.Initialize(MakeMaterial(1, CubeRole.Normal));
            cube.HandleSlice(Vector3.zero);
            cube.HandleFellOff();
            Assert.AreEqual(3, _gm.Lives);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Initialize_AppliesMassAndScale()
        {
            var go = new GameObject("Heavy", typeof(Rigidbody), typeof(BoxCollider));
            var cube = go.AddComponent<Cube>();
            var mat = MakeMaterial(2, CubeRole.Bonus, mass: 3f);
            mat.displayScale = 1.4f;
            cube.Initialize(mat);
            Assert.AreEqual(3f, go.GetComponent<Rigidbody>().mass);
            Assert.That(go.transform.localScale.x, Is.EqualTo(1.4f).Within(0.001f));
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 1.2: Run tests — expect compile failure**

Expected: compile errors referencing `Cube.Initialize(CubeMaterial)` and missing `CubeRole` members.

### Step 2: Rewrite `Cube.cs`

- [ ] **Step 2.1: Replace the entire file**

```csharp
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Per-cube router. Holds a CubeMaterial reference at runtime; applies its
    /// physics/visual settings on Initialize. Routes slice and fall-off events
    /// into GameManager. On wall collision plays bounce SFX and applies the
    /// material's bounciness multiplier to the rigidbody velocity.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Cube : MonoBehaviour
    {
        [SerializeField] private CubeMaterial material;
        [SerializeField] private ParticleSystem burstPrefab;

        private const string WallTag = "Wall";
        private const float MinBounceAudioInterval = 0.05f;

        private Rigidbody _rb;
        private MeshRenderer _renderer;
        private bool _consumed;
        private float _lastAudioTime;

        public CubeMaterial Material => material;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<MeshRenderer>();
        }

        /// <summary>Configure the cube before adding force. Idempotent.</summary>
        public void Initialize(CubeMaterial mat)
        {
            material = mat;
            if (material == null) return;

            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (_renderer == null) _renderer = GetComponentInChildren<MeshRenderer>();

            _rb.mass = material.mass;
            _rb.drag = 0.05f;
            _rb.angularDrag = 0.1f;
            transform.localScale = Vector3.one * material.displayScale;

            if (_renderer != null && material.renderMaterial != null)
                _renderer.sharedMaterial = material.renderMaterial;

            gameObject.layer = LayerMask.NameToLayer("Cube");
            _consumed = false;
            _lastAudioTime = -1f;
        }

        public void HandleSlice(Vector3 slicePoint)
        {
            if (_consumed) return;
            _consumed = true;

            SpawnBurst(slicePoint);

            var gm = GameManager.Instance;
            if (gm != null && material != null)
            {
                if (material.role == CubeRole.Danger) gm.RegisterDangerClick(slicePoint);
                else                                  gm.RegisterHit(material.basePoints, slicePoint);
            }

            // Sense-of-mass feedback. Both helpers no-op when the runtime
            // pieces (singletons, scene) aren't present (e.g. in unit tests).
            if (material != null)
            {
                HitStopController.Apply(material.mass);
                BladeController.Instance?.ApplySliceDrag(material.mass);
            }

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
            if (gm != null && material != null && material.role != CubeRole.Danger)
                gm.RegisterMiss();

            if (Application.isPlaying) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_consumed) return;
            if (material == null) return;
            if (!collision.gameObject.CompareTag(WallTag)) return;

            // Rate-limit so two segment hits in a single physics step don't double-play.
            float now = Time.time;
            if (now - _lastAudioTime >= MinBounceAudioInterval)
            {
                _lastAudioTime = now;
                if (material.impactClip != null)
                {
                    Vector3 contactPoint = collision.GetContact(0).point;
                    float pitch = material.audioPitchAtMassOne *
                                  Mathf.Lerp(1.6f, 0.5f,
                                      Mathf.InverseLerp(0.3f, 3f, material.mass));
                    AudioOneShot.Play(material.impactClip, contactPoint, pitch, material.audioVolume);
                }
            }

            if (!Mathf.Approximately(material.bouncinessMultiplier, 1f) && _rb != null)
            {
                _rb.linearVelocity *= material.bouncinessMultiplier;
                _rb.angularVelocity *= material.bouncinessMultiplier;
            }
        }

        private void SpawnBurst(Vector3 worldPos)
        {
            if (burstPrefab == null) return;
            var burst = Instantiate(burstPrefab, worldPos, Quaternion.identity);
            var main = burst.main;
            if (material != null) main.startColor = material.burstTint;
            burst.Play();
            Destroy(burst.gameObject, main.duration + main.startLifetime.constantMax);
        }
    }
}
```

- [ ] **Step 2.2: Run tests — expect compile failure (HitStopController / AudioOneShot / BladeController.Instance not defined yet)**

The Cube script now references types from Task 4, 5, 6. Tests can't compile until those exist. **This is expected** — keep going. The plan creates those next.

- [ ] **Step 2.3: Commit anyway as a snapshot, OR wait until later tasks compile**

To keep history clean, hold this commit until Task 6 lands and tests compile. Mark this task as DONE_WITH_CONCERNS in the report; downstream tasks will let it compile.

---

## Task 4: `AudioOneShot` helper

**Goal:** Play an `AudioClip` once at a world position with a custom pitch. Unity's `AudioSource.PlayClipAtPoint` doesn't expose pitch, so we build a one-frame `AudioSource` and destroy it after the clip finishes.

**Files:**
- Create: `Assets/Scripts/Util/AudioOneShot.cs`

- [ ] **Step 1: Write the file**

```csharp
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Plays a clip once at a world position with custom pitch and volume.
    /// AudioSource.PlayClipAtPoint can't change pitch, so this builds a
    /// throwaway AudioSource, configures it, and destroys it when done.
    /// No-op if the clip is null.
    /// </summary>
    public static class AudioOneShot
    {
        public static void Play(AudioClip clip, Vector3 position, float pitch = 1f, float volume = 1f)
        {
            if (clip == null) return;

            var go = new GameObject($"OneShot_{clip.name}");
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch, 0.1f, 4f);
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = 0f; // 2D — the playfield is flat
            source.Play();

            // Account for pitch — at pitch != 1, real playback length scales.
            float lifetime = clip.length / Mathf.Max(0.01f, source.pitch);
            Object.Destroy(go, lifetime + 0.1f);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Util/AudioOneShot.cs
git commit -m "feat: add AudioOneShot helper for pitched world-space SFX"
```

---

## Task 5: `HitStopController`

**Goal:** Briefly dip `Time.timeScale` after a slice; depth and duration scale with cube mass.

**Files:**
- Create: `Assets/Scripts/Gameplay/HitStopController.cs`

- [ ] **Step 1: Write the file**

```csharp
using System.Collections;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Briefly dips Time.timeScale after a slice, then restores it. Depth and
    /// duration scale with cube mass: heavier cube → deeper, longer hit-stop.
    /// Re-entrant: a second slice during an active hit-stop is ignored so the
    /// scales don't stack. Skips if the game is already over (so we don't undo
    /// the game-over freeze).
    /// </summary>
    public static class HitStopController
    {
        private const float MassMin = 0.3f;
        private const float MassMax = 3.0f;
        private const float ScaleAtMassMin = 0.8f;
        private const float ScaleAtMassMax = 0.15f;
        private const float DurationAtMassMin = 0.02f;
        private const float DurationAtMassMax = 0.08f;

        private static Runner _runner;
        private static bool _active;

        public static void Apply(float mass)
        {
            if (_active) return;
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
            if (!Application.isPlaying) return;

            EnsureRunner();
            float t = Mathf.InverseLerp(MassMin, MassMax, mass);
            float scale = Mathf.Lerp(ScaleAtMassMin, ScaleAtMassMax, t);
            float duration = Mathf.Lerp(DurationAtMassMin, DurationAtMassMax, t);
            _runner.StartCoroutine(Run(scale, duration));
        }

        private static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("HitStopRunner") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        private static IEnumerator Run(float scale, float duration)
        {
            _active = true;
            float prev = Time.timeScale;
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(duration);
            // Don't trample a game-over freeze that happened mid hit-stop.
            if (GameManager.Instance == null || !GameManager.Instance.IsGameOver)
                Time.timeScale = prev;
            _active = false;
        }

        private class Runner : MonoBehaviour { }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Gameplay/HitStopController.cs
git commit -m "feat: add HitStopController for mass-scaled slice feedback"
```

---

## Task 6: `BladeController` — `ApplySliceDrag` + Instance singleton

**Goal:** After a slice, lag the blade tip for a brief window proportional to the sliced cube's mass. Cube calls `BladeController.Instance.ApplySliceDrag(mass)`.

**Files:**
- Modify: `Assets/Scripts/Gameplay/BladeController.cs`

- [ ] **Step 1: Add Instance field + ApplySliceDrag + update Update**

Edit `Assets/Scripts/Gameplay/BladeController.cs`. Apply the following changes:

1. Above the class declaration, no changes needed.
2. After `public class BladeController : MonoBehaviour` and the existing `[Header]` blocks, add a static `Instance` field. After `private bool _isSwiping;` and the other state fields, add `_dragUntil` and `_dragFactor`. After `Awake`, add an `OnDestroy` clear and the new `ApplySliceDrag` method. Modify the part of `Update` that sets `bladeTip.position`.

Here is the complete updated file:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Translates mouse input into a swipe in world space (on a fixed z plane),
    /// then slices any cube the swept sphere passes through. Drives the BladeTip
    /// transform that the TrailRenderer follows. Exposes ApplySliceDrag so cubes
    /// can "drag" the blade tip after a heavy slice.
    /// </summary>
    public class BladeController : MonoBehaviour
    {
        public static BladeController Instance { get; private set; }

        [Header("Slice physics")]
        [SerializeField] private float bladeRadius = 0.25f;
        [SerializeField] private float minSliceSpeed = 4f;
        [SerializeField] private LayerMask cubeMask = ~0;
        [SerializeField] private float playPlaneZ = 0f;

        [Header("Refs")]
        [SerializeField] private Transform bladeTip;
        [SerializeField] private TrailRenderer bladeTrail;
        [SerializeField] private Camera gameCamera;

        private const float SliceDragMassMin = 0.3f;
        private const float SliceDragMassMax = 3.0f;
        private const float DragFactorMin = 0.1f;
        private const float DragFactorMax = 0.6f;
        private const float DragDurationMin = 0.04f;
        private const float DragDurationMax = 0.12f;

        private bool _isSwiping;
        private Vector3 _lastTipWorld;
        private float _dragUntil;
        private float _dragFactor;
        private readonly HashSet<Cube> _slicedThisSwipe = new();

        private void Awake()
        {
            Instance = this;
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ApplySliceDrag(float mass)
        {
            float t = Mathf.InverseLerp(SliceDragMassMin, SliceDragMassMax, mass);
            _dragFactor = Mathf.Lerp(DragFactorMin, DragFactorMax, t);
            _dragUntil = Time.unscaledTime + Mathf.Lerp(DragDurationMin, DragDurationMax, t);
        }

        private void Update()
        {
            if (gameCamera == null) return;

            Vector3 worldNow = MouseToWorld();
            if (bladeTip != null)
            {
                if (_dragUntil > Time.unscaledTime)
                    bladeTip.position = Vector3.Lerp(bladeTip.position, worldNow, 1f - _dragFactor);
                else
                    bladeTip.position = worldNow;
            }

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
            Ray ray = gameCamera.ScreenPointToRay(Input.mousePosition);
            if (Mathf.Approximately(ray.direction.z, 0f))
                return new Vector3(ray.origin.x, ray.origin.y, playPlaneZ);
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
                if (!_slicedThisSwipe.Add(cube)) continue;
                cube.HandleSlice(hit.point);
            }
        }
    }
}
```

- [ ] **Step 2: Run all EditMode tests — expect pass**

The `Cube` script written in Task 3 referenced `BladeController.Instance` and `HitStopController.Apply`, both of which now exist. Tests can compile. Expect all 19 EditMode tests pass (12 GameManager + 7 Cube + 0 new for this task).

- [ ] **Step 3: Commit (this lands Tasks 3, 4, 5, and 6 together as a compilable unit)**

```bash
git add Assets/Scripts/Gameplay/Cube.cs \
        Assets/Tests/EditMode/CubeTests.cs \
        Assets/Scripts/Util/AudioOneShot.cs \
        Assets/Scripts/Gameplay/HitStopController.cs \
        Assets/Scripts/Gameplay/BladeController.cs
git commit -m "feat: material-driven Cube with collision SFX, hit-stop, blade drag"
```

**Note:** Task 3's commit was deferred to here so the codebase compiles between commits.

---

## Task 7: `CubeSpawner` — weighted picker over `MaterialEntry`

**Goal:** Spawner becomes a weighted picker over a `List<MaterialEntry>`. The legacy `Cube greenPrefab/redPrefab/blackPrefab` references are removed in favor of one `Cube cubePrefab`. Each entry's spawn weight is an `AnimationCurve` over elapsed run time.

**Files:**
- Modify: `Assets/Scripts/Gameplay/CubeSpawner.cs`
- Create: `Assets/Tests/EditMode/SpawnerWeightingTests.cs`

### Step 1: Replace `CubeSpawner.cs`

- [ ] **Step 1.1: Rewrite the file**

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Spawns cubes from a horizontal line at the bottom of the play area.
    /// The cube material is chosen by a weighted roll over a configured list,
    /// where each entry's weight is an AnimationCurve evaluated at elapsed
    /// run time. Each material can also override the launch impulse range.
    /// </summary>
    public class CubeSpawner : MonoBehaviour
    {
        [Serializable]
        public struct MaterialEntry
        {
            public CubeMaterial material;
            public AnimationCurve weightOverTime;
        }

        [Header("Prefab")]
        [SerializeField] private Cube cubePrefab;

        [Header("Spawn line")]
        [SerializeField] private Transform spawnLineLeft;
        [SerializeField] private Transform spawnLineRight;

        [Header("Difficulty")]
        [SerializeField] private AnimationCurve spawnIntervalOverTime =
            AnimationCurve.Linear(0f, 0.7f, 60f, 0.3f);
        [SerializeField] private List<MaterialEntry> entries = new();

        [Header("Launch impulse (defaults; per-material override wins if set)")]
        [SerializeField] private Vector2 launchImpulseRange = new(5f, 14f);
        [SerializeField] private Vector2 sideImpulseRange = new(0f, 4f);
        [SerializeField] private float maxUpwardVelocity = 25f;

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

        /// <summary>Picks a material by weight at the current elapsed time. Public for tests.</summary>
        public CubeMaterial PickMaterial(float elapsed)
        {
            if (entries == null || entries.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].material == null) continue;
                float w = Mathf.Max(0f, entries[i].weightOverTime?.Evaluate(elapsed) ?? 0f);
                total += w;
            }
            if (total <= 0f) return entries[0].material;

            float roll = UnityEngine.Random.value * total;
            float running = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].material == null) continue;
                float w = Mathf.Max(0f, entries[i].weightOverTime?.Evaluate(elapsed) ?? 0f);
                running += w;
                if (roll <= running) return entries[i].material;
            }
            return entries[entries.Count - 1].material;
        }

        private void SpawnOne(float elapsed)
        {
            if (cubePrefab == null || spawnLineLeft == null || spawnLineRight == null) return;
            CubeMaterial material = PickMaterial(elapsed);
            if (material == null) return;

            float x = UnityEngine.Random.Range(spawnLineLeft.position.x, spawnLineRight.position.x);
            Vector3 pos = new Vector3(x, spawnLineLeft.position.y, spawnLineLeft.position.z);
            Quaternion rot = UnityEngine.Random.rotation;

            Cube cube = Instantiate(cubePrefab, pos, rot);
            cube.Initialize(material);

            Rigidbody rb = cube.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector2 upRange = material.HasLaunchOverride ? material.launchImpulseOverride : launchImpulseRange;
            float up = UnityEngine.Random.Range(upRange.x, upRange.y);
            float side = UnityEngine.Random.Range(sideImpulseRange.x, sideImpulseRange.y);
            side *= UnityEngine.Random.value < 0.5f ? -1f : 1f;
            rb.AddForce(new Vector3(side, up, 0f), ForceMode.Impulse);

            // Cap upward velocity so lightest cubes don't escape the play area instantly.
            if (rb.linearVelocity.y > maxUpwardVelocity)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxUpwardVelocity, rb.linearVelocity.z);
        }
    }
}
```

### Step 2: Add the spawner weighting test

- [ ] **Step 2.1: Write `SpawnerWeightingTests.cs`**

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace OpenNinja.Tests
{
    public class SpawnerWeightingTests
    {
        private static CubeMaterial Mat(string name, int basePoints)
        {
            var m = ScriptableObject.CreateInstance<CubeMaterial>();
            m.displayName = name;
            m.basePoints = basePoints;
            m.mass = 1f;
            m.displayScale = 1f;
            m.bouncinessMultiplier = 1f;
            return m;
        }

        [Test]
        public void PickMaterial_TwoEntriesEqualWeight_FiftyFifty()
        {
            var go = new GameObject("Spawner");
            var spawner = go.AddComponent<CubeSpawner>();
            spawner.SetEntriesForTest(new List<CubeSpawner.MaterialEntry>
            {
                new() { material = Mat("A", 1), weightOverTime = AnimationCurve.Constant(0, 60, 1f) },
                new() { material = Mat("B", 2), weightOverTime = AnimationCurve.Constant(0, 60, 1f) },
            });

            Random.InitState(12345);
            int aCount = 0, bCount = 0;
            for (int i = 0; i < 2000; i++)
            {
                var picked = spawner.PickMaterial(0f);
                if (picked.displayName == "A") aCount++;
                else if (picked.displayName == "B") bCount++;
            }
            float ratio = (float)aCount / (aCount + bCount);
            Assert.That(ratio, Is.EqualTo(0.5f).Within(0.05f),
                "Equal weights should yield ~50/50 with 5% tolerance");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PickMaterial_ZeroWeight_NeverPicked()
        {
            var go = new GameObject("Spawner");
            var spawner = go.AddComponent<CubeSpawner>();
            spawner.SetEntriesForTest(new List<CubeSpawner.MaterialEntry>
            {
                new() { material = Mat("Active", 1), weightOverTime = AnimationCurve.Constant(0, 60, 1f) },
                new() { material = Mat("Zero",   2), weightOverTime = AnimationCurve.Constant(0, 60, 0f) },
            });

            Random.InitState(42);
            for (int i = 0; i < 500; i++)
            {
                Assert.AreEqual("Active", spawner.PickMaterial(0f).displayName);
            }
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PickMaterial_TimeChangesDistribution()
        {
            var go = new GameObject("Spawner");
            var spawner = go.AddComponent<CubeSpawner>();
            spawner.SetEntriesForTest(new List<CubeSpawner.MaterialEntry>
            {
                new() { material = Mat("Early", 1), weightOverTime = AnimationCurve.Linear(0, 1, 60, 0) },
                new() { material = Mat("Late",  2), weightOverTime = AnimationCurve.Linear(0, 0, 60, 1) },
            });

            Random.InitState(7);
            int earlyAt0 = 0;
            for (int i = 0; i < 500; i++)
                if (spawner.PickMaterial(0f).displayName == "Early") earlyAt0++;
            int lateAt60 = 0;
            for (int i = 0; i < 500; i++)
                if (spawner.PickMaterial(60f).displayName == "Late") lateAt60++;

            Assert.That(earlyAt0, Is.GreaterThan(450), "At t=0, Early should dominate");
            Assert.That(lateAt60, Is.GreaterThan(450), "At t=60, Late should dominate");
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2.2: Add `SetEntriesForTest` to `CubeSpawner`**

The tests need a way to set `entries` without going through serialization. Add this method to `CubeSpawner.cs` just before the closing `}` of the class:

```csharp
        /// <summary>EditMode tests use this to inject a synthetic entries list.</summary>
        public void SetEntriesForTest(List<MaterialEntry> testEntries)
        {
            entries = testEntries;
        }
```

- [ ] **Step 2.3: Run all EditMode tests — expect pass**

Expected: 22 tests pass (12 GameManager + 7 Cube + 3 SpawnerWeighting).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Gameplay/CubeSpawner.cs Assets/Tests/EditMode/SpawnerWeightingTests.cs
git commit -m "feat(spawner): weighted material picker + per-material impulse override"
```

---

## Task 8: Delete `CubeType.cs`

**Goal:** Remove the now-unused enum.

**Files:**
- Delete: `Assets/Scripts/Core/CubeType.cs`

- [ ] **Step 1: Verify no references remain**

```bash
grep -r "CubeType" Assets --include="*.cs"
```
Expected: zero output (apart from possibly comments — if any, leave the enum in place and audit).

- [ ] **Step 2: Delete the file and its meta**

```bash
git rm Assets/Scripts/Core/CubeType.cs
# meta file should follow:
rm -f Assets/Scripts/Core/CubeType.cs.meta
```

- [ ] **Step 3: Run tests — expect all pass**

22 tests should still pass.

- [ ] **Step 4: Commit**

```bash
git add -A Assets/Scripts/Core
git commit -m "chore: remove CubeType enum (replaced by CubeMaterial)"
```

---

## Task 9: Build Unity assets via Editor script

**Goal:** One Editor-only script creates all the new assets atomically: 6 render materials, 6 `CubeMaterial` SOs, one `BouncyWall.physicMaterial`, one base `Cube.prefab`. Also creates the `Wall` tag and `Cube` layer if missing. The previous three colored cube prefabs and three Cube_*.mat materials are deleted.

**Prerequisite:** Unity Editor must be open with the project loaded and the Coplay MCP plugin connected. Verify via `mcp__coplay-mcp__get_unity_editor_state`.

**Files:**
- Create: `Assets/Editor/MaterialAssetsSetup.cs`

- [ ] **Step 1: Write the setup script**

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace OpenNinja.EditorSetup
{
    public static class MaterialAssetsSetup
    {
        private const string MaterialsDir = "Assets/Materials";
        private const string CubeMaterialsDir = "Assets/Data/CubeMaterials";
        private const string DataDir = "Assets/Data";
        private const string PrefabsDir = "Assets/Prefabs";
        private const string PhysicMaterialPath = "Assets/Data/BouncyWall.physicMaterial";

        public static string Execute()
        {
            EnsureFolder(MaterialsDir);
            EnsureFolder(DataDir);
            EnsureFolder(CubeMaterialsDir);
            EnsureFolder(PrefabsDir);
            EnsureTag("Wall");
            EnsureLayer("Cube");

            // 1) Delete legacy cube color prefabs + materials.
            DeleteIfExists("Assets/Prefabs/Cube_Green.prefab");
            DeleteIfExists("Assets/Prefabs/Cube_Red.prefab");
            DeleteIfExists("Assets/Prefabs/Cube_Black.prefab");
            DeleteIfExists("Assets/Materials/Cube_Green.mat");
            DeleteIfExists("Assets/Materials/Cube_Red.mat");
            DeleteIfExists("Assets/Materials/Cube_Black.mat");

            // 2) Render materials.
            var woodMat    = MakeMaterial("Wood",    new Color(0.55f, 0.35f, 0.2f, 1f), 0.7f, 0f);
            var stoneMat   = MakeMaterial("Stone",   new Color(0.55f, 0.55f, 0.55f, 1f), 0.5f, 0f);
            var metalMat   = MakeMaterial("Metal",   new Color(0.25f, 0.25f, 0.27f, 1f), 0.85f, 1f);
            var crystalMat = MakeMaterial("Crystal", new Color(0.55f, 0.85f, 1f, 1f), 0.95f, 0.3f);
            var spikedMat  = MakeMaterial("Spiked",  new Color(0.1f, 0.1f, 0.1f, 1f), 0.3f, 0f);
            var rubberMat  = MakeMaterial("Rubber",  new Color(1f, 0.9f, 0.2f, 1f), 0.15f, 0f);

            // 3) BouncyWall physic material.
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicMaterialPath);
            if (pm == null) pm = new PhysicsMaterial("BouncyWall");
            pm.bounciness = 0.75f;
            pm.dynamicFriction = 0.1f;
            pm.staticFriction = 0.1f;
            pm.bounceCombine = PhysicsMaterialCombine.Maximum;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicMaterialPath) == null)
                AssetDatabase.CreateAsset(pm, PhysicMaterialPath);
            else
                EditorUtility.SetDirty(pm);

            // 4) CubeMaterial SO assets.
            MakeCubeMaterial("Wood",    role: CubeRole.Normal, basePts: 1, mass: 0.4f, scale: 0.7f,
                renderMat: woodMat,    burst: new Color(0.65f, 0.45f, 0.25f, 1f),
                bounceMult: 1.0f, launchOverride: new Vector2(6f, 12f));
            MakeCubeMaterial("Stone",   role: CubeRole.Normal, basePts: 2, mass: 1.0f, scale: 1.0f,
                renderMat: stoneMat,   burst: new Color(0.7f, 0.7f, 0.7f, 1f),
                bounceMult: 1.0f, launchOverride: Vector2.zero);
            MakeCubeMaterial("Metal",   role: CubeRole.Bonus,  basePts: 3, mass: 3.0f, scale: 1.4f,
                renderMat: metalMat,   burst: new Color(0.5f, 0.55f, 0.6f, 1f),
                bounceMult: 0.9f, launchOverride: new Vector2(10f, 14f));
            MakeCubeMaterial("Crystal", role: CubeRole.Bonus,  basePts: 5, mass: 0.3f, scale: 0.9f,
                renderMat: crystalMat, burst: new Color(0.7f, 0.95f, 1f, 1f),
                bounceMult: 1.0f, launchOverride: Vector2.zero);
            MakeCubeMaterial("Spiked",  role: CubeRole.Danger, basePts: 0, mass: 1.2f, scale: 1.0f,
                renderMat: spikedMat,  burst: new Color(0.4f, 0.0f, 0.4f, 1f),
                bounceMult: 1.0f, launchOverride: Vector2.zero);
            MakeCubeMaterial("Rubber",  role: CubeRole.Normal, basePts: 2, mass: 0.5f, scale: 0.9f,
                renderMat: rubberMat,  burst: new Color(1f, 0.95f, 0.3f, 1f),
                bounceMult: 1.4f, launchOverride: Vector2.zero);

            // 5) Base Cube.prefab.
            string cubePrefabPath = "Assets/Prefabs/Cube.prefab";
            DeleteIfExists(cubePrefabPath);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Cube";
            go.layer = LayerMask.NameToLayer("Cube");
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.drag = 0.05f;
            rb.angularDrag = 0.1f;
            var cube = go.AddComponent<OpenNinja.Cube>();
            // burstPrefab assignment wired by SceneSetup; left null here.
            PrefabUtility.SaveAsPrefabAsset(go, cubePrefabPath);
            Object.DestroyImmediate(go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "Created materials, CubeMaterial SOs, BouncyWall.physicMaterial, Cube.prefab; deleted legacy color cubes.";
        }

        private static Material MakeMaterial(string name, Color color, float smoothness, float metallic)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void MakeCubeMaterial(string name, CubeRole role, int basePts, float mass,
            float scale, Material renderMat, Color burst, float bounceMult, Vector2 launchOverride)
        {
            string path = $"{CubeMaterialsDir}/{name}.asset";
            var so = AssetDatabase.LoadAssetAtPath<OpenNinja.CubeMaterial>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<OpenNinja.CubeMaterial>();
                AssetDatabase.CreateAsset(so, path);
            }
            so.displayName = name;
            so.role = role;
            so.basePoints = basePts;
            so.mass = mass;
            so.displayScale = scale;
            so.renderMaterial = renderMat;
            so.burstTint = burst;
            so.bouncinessMultiplier = bounceMult;
            so.launchImpulseOverride = launchOverride;
            so.audioPitchAtMassOne = 1f;
            so.audioVolume = 0.7f;
            EditorUtility.SetDirty(so);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void EnsureTag(string tag)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var tagsProp = so.FindProperty("tags");
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureLayer(string layer)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
            var so = new SerializedObject(asset);
            var layersProp = so.FindProperty("layers");
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var prop = layersProp.GetArrayElementAtIndex(i);
                if (prop.stringValue == layer) return;
                if (string.IsNullOrEmpty(prop.stringValue))
                {
                    prop.stringValue = layer;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }
}
```

- [ ] **Step 2: Verify no compile errors**

Call `mcp__coplay-mcp__check_compile_errors`. Expected: `No compile errors`.

- [ ] **Step 3: Run the setup**

Call `mcp__coplay-mcp__execute_script` with `filePath = "Assets/Editor/MaterialAssetsSetup.cs"`, `methodName = "Execute"`.
Expected result: `"Created materials, CubeMaterial SOs, BouncyWall.physicMaterial, Cube.prefab; deleted legacy color cubes."`

- [ ] **Step 4: Verify outputs**

```bash
ls Assets/Materials/                    # Wood/Stone/Metal/Crystal/Spiked/Rubber .mat files
ls Assets/Data/CubeMaterials/           # Wood/Stone/Metal/Crystal/Spiked/Rubber .asset files
ls Assets/Data/BouncyWall.physicMaterial
ls Assets/Prefabs/Cube.prefab
ls Assets/Prefabs/                      # Cube_Green/Red/Black should be GONE
```

- [ ] **Step 5: Commit**

```bash
git add Assets/Editor/MaterialAssetsSetup.cs Assets/Materials Assets/Data Assets/Prefabs ProjectSettings/TagManager.asset
git commit -m "feat(assets): 6 render materials, 6 CubeMaterial SOs, BouncyWall physic, unified Cube.prefab"
```

---

## Task 10: Update `SceneSetup.cs` — walls + new spawner wiring

**Goal:** Rebuild `MainScene.unity` from scratch with:
- Walls (3 of them: Left, Right, Top) made of segmented colliders + a single visible mesh per wall
- `CubeSpawner` wired to the new `Cube.prefab` and a `List<MaterialEntry>` referencing the 6 SO assets
- KillZone, GameManager, BladeController, Canvas/UI all preserved as before
- Old per-color prefab references on the spawner are gone

**Files:**
- Modify: `Assets/Editor/SceneSetup.cs`

- [ ] **Step 1: Update the CubeSpawner wiring section**

Find the section in `SceneSetup.cs` that creates the `CubeSpawner` and assigns prefabs. The old code looks roughly like:

```csharp
var spawner = spawnerGO.AddComponent<CubeSpawner>();
// ...
SetRef(spawnerSO, "greenPrefab", greenPrefab);
SetRef(spawnerSO, "redPrefab", redPrefab);
SetRef(spawnerSO, "blackPrefab", blackPrefab);
SetRef(spawnerSO, "spawnLineLeft", spawnLeft.transform);
SetRef(spawnerSO, "spawnLineRight", spawnRight.transform);
```

Replace with:

```csharp
var spawner = spawnerGO.AddComponent<CubeSpawner>();

var cubePrefab = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube.prefab");

var spawnLeftT = spawnLeft.transform;
var spawnRightT = spawnRight.transform;

var spawnerSO = new SerializedObject(spawner);
SetRef(spawnerSO, "cubePrefab", cubePrefab);
SetRef(spawnerSO, "spawnLineLeft", spawnLeftT);
SetRef(spawnerSO, "spawnLineRight", spawnRightT);

// Build the MaterialEntry list. Curves shift the mix over time.
// Early game: lots of Wood/Stone; later game: more Metal/Crystal/Spiked/Rubber.
var entriesProp = spawnerSO.FindProperty("entries");
entriesProp.arraySize = 6;
WireEntry(entriesProp, 0, "Wood",    AnimationCurve.Linear(0, 4f, 60, 2f));
WireEntry(entriesProp, 1, "Stone",   AnimationCurve.Linear(0, 3f, 60, 3f));
WireEntry(entriesProp, 2, "Metal",   AnimationCurve.Linear(0, 0.5f, 60, 1.5f));
WireEntry(entriesProp, 3, "Crystal", AnimationCurve.Linear(0, 0.25f, 60, 1f));
WireEntry(entriesProp, 4, "Spiked",  AnimationCurve.Linear(0, 0.5f, 60, 1.5f));
WireEntry(entriesProp, 5, "Rubber",  AnimationCurve.Linear(0, 0.5f, 60, 1f));
spawnerSO.ApplyModifiedPropertiesWithoutUndo();
```

Add this helper method anywhere in the `SceneSetup` class:

```csharp
private static void WireEntry(SerializedProperty arr, int index, string materialName, AnimationCurve curve)
{
    var entry = arr.GetArrayElementAtIndex(index);
    string path = $"Assets/Data/CubeMaterials/{materialName}.asset";
    var so = AssetDatabase.LoadAssetAtPath<CubeMaterial>(path);
    entry.FindPropertyRelative("material").objectReferenceValue = so;
    entry.FindPropertyRelative("weightOverTime").animationCurveValue = curve;
}
```

- [ ] **Step 2: Add walls construction**

After the KillZone block in `SceneSetup.Execute`, add this block that builds the three walls under a `Walls` parent:

```csharp
// ---- Walls (bumpy) ----
var walls = new GameObject("Walls");
walls.transform.SetParent(systems.transform, false);

var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Data/BouncyWall.physicMaterial");
BuildWall(walls.transform, "Wall_Left",  position: new Vector3(-10f, 0f, 0f),
    rotation: Quaternion.identity,
    fullSize: new Vector3(1f, 20f, 4f),  segmentCount: 12, pm: pm);
BuildWall(walls.transform, "Wall_Right", position: new Vector3(10f, 0f, 0f),
    rotation: Quaternion.identity,
    fullSize: new Vector3(1f, 20f, 4f),  segmentCount: 12, pm: pm);
BuildWall(walls.transform, "Wall_Top",   position: new Vector3(0f, 8f, 0f),
    rotation: Quaternion.identity,
    fullSize: new Vector3(20f, 1f, 4f),  segmentCount: 12, pm: pm);

log.Add("walls built");
```

And add this helper method to `SceneSetup`:

```csharp
/// <summary>
/// Builds a wall composed of `segmentCount` BoxCollider children, each tilted by
/// a random angle in [-8, +8] degrees around the wall's local Z axis so that
/// reflections vary segment to segment.
/// </summary>
private static void BuildWall(Transform parent, string name, Vector3 position,
    Quaternion rotation, Vector3 fullSize, int segmentCount, PhysicsMaterial pm)
{
    var wall = new GameObject(name);
    wall.transform.SetParent(parent, false);
    wall.transform.SetPositionAndRotation(position, rotation);
    wall.tag = "Wall";

    // Visible mesh: one stretched cube. Cosmetic only.
    var visualGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
    visualGO.name = "Visual";
    Object.DestroyImmediate(visualGO.GetComponent<Collider>()); // collider is on segments
    visualGO.transform.SetParent(wall.transform, false);
    visualGO.transform.localScale = fullSize;
    var mr = visualGO.GetComponent<MeshRenderer>();
    if (mr != null) mr.enabled = true;

    // Segment colliders.
    bool isHorizontal = fullSize.x > fullSize.y;
    float length = isHorizontal ? fullSize.x : fullSize.y;
    float segmentLength = length / segmentCount;
    var rng = new System.Random(name.GetHashCode());

    for (int i = 0; i < segmentCount; i++)
    {
        var seg = new GameObject($"Seg_{i}", typeof(BoxCollider));
        seg.transform.SetParent(wall.transform, false);
        seg.tag = "Wall";

        float t = (i + 0.5f) / segmentCount; // [0,1) centers
        float localOffset = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
        Vector3 localPos = isHorizontal
            ? new Vector3(localOffset, 0f, 0f)
            : new Vector3(0f, localOffset, 0f);

        float angleDeg = (float)(rng.NextDouble() * 16.0 - 8.0); // [-8, +8]
        seg.transform.localPosition = localPos;
        seg.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);

        var col = seg.GetComponent<BoxCollider>();
        col.size = isHorizontal
            ? new Vector3(segmentLength * 1.05f, fullSize.y, fullSize.z)
            : new Vector3(fullSize.x, segmentLength * 1.05f, fullSize.z);
        if (pm != null) col.material = pm;
    }
}
```

- [ ] **Step 3: Update the SliceBurst reference on Cube.prefab**

After all the walls and the canvas are built, and before saving the scene, also assign the `SliceBurst` particle prefab onto `Assets/Prefabs/Cube.prefab` (so cubes emit burst on slice). Add this near the bottom of `SceneSetup.Execute`, before the scene save:

```csharp
// Wire the slice burst prefab into the unified Cube prefab.
var cubePrefabAsset = AssetDatabase.LoadAssetAtPath<Cube>("Assets/Prefabs/Cube.prefab");
var burstPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>("Assets/Prefabs/SliceBurst.prefab");
if (cubePrefabAsset != null && burstPrefab != null)
{
    var prefabSO = new SerializedObject(cubePrefabAsset);
    var burstProp = prefabSO.FindProperty("burstPrefab");
    if (burstProp != null)
    {
        burstProp.objectReferenceValue = burstPrefab;
        prefabSO.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cubePrefabAsset);
        AssetDatabase.SaveAssets();
    }
}
```

- [ ] **Step 4: Verify no compile errors**

Call `mcp__coplay-mcp__check_compile_errors`.

- [ ] **Step 5: Run the scene setup**

```
mcp__coplay-mcp__execute_script
  filePath: Assets/Editor/SceneSetup.cs
  methodName: Execute
```

Expected: log message ending with `... walls built ... scene saved to Assets/Scenes/MainScene.unity`.

- [ ] **Step 6: Commit**

```bash
git add Assets/Editor/SceneSetup.cs Assets/Scenes/MainScene.unity ProjectSettings
git commit -m "feat(scene): walls + MaterialEntry-wired spawner in MainScene"
```

---

## Task 11: Playtest verification

**Goal:** Press Play in the Editor and confirm the new system feels right.

This task does no code work. It is a manual verification gate.

- [ ] **Step 1: Press Play**

If Coplay MCP is connected: `mcp__coplay-mcp__play_game`. Watch the Game view.

- [ ] **Step 2: Verify happy path**

- Multiple cube *materials* visible per spawn (wood, stone, metal, crystal, spiked, rubber). Use the color/scale to identify at a glance.
- Heavier cubes (metal) visibly larger and arc lower.
- Lighter cubes (wood, crystal) shoot higher and bounce off walls more.
- Walls reflect cubes back into play; bounces look irregular (not perfectly symmetric).
- Slicing a metal cube produces a perceptible micro-pause (hit-stop) and brief trail lag.

- [ ] **Step 3: Verify danger and energy decay**

- Spiked cubes display the danger material; slicing one deducts a heart.
- After roughly 8 seconds, no cube remains bouncing forever — energy decay forces them into the KillZone eventually.

- [ ] **Step 4: Verify combo + UI**

- Slicing 2 cubes in a fast swipe → "Combo x2" appears, timer bar drains.
- Slicing 3 cubes in a single swipe → "Combo x3" or higher.
- Lose 3 lives → game-over panel; Restart resets cubes and curves (`spawner.NotifyRunRestarted` is still wired in the old `GameOverView.OnRestartClicked`).

- [ ] **Step 5: Stop, tune if needed, commit**

If anything feels off, tune the curves on `Systems/CubeSpawner` in the Inspector, then re-save the scene (or re-run `MaterialAssetsSetup` / `SceneSetup` if the change is structural). Commit any tuning.

```bash
git status
git add Assets ProjectSettings
git commit -m "tune: post-playtest material weights and impulse ranges"
```

If no changes needed, skip this step.

---

## Done criteria

- All 22 EditMode tests pass (12 GameManager + 7 Cube + 3 SpawnerWeighting).
- A full play session shows a varied cube mix (light/medium/heavy/danger/bouncy), wall bounces visibly differ in direction, hit-stop is perceptible on metal, and game-over → Restart works end-to-end.
- No console errors during play.
- `Assets/Prefabs/Cube.prefab` (singular) replaces the three color prefabs. `CubeType.cs` is deleted.
