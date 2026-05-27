# Material Cubes & Bouncy Walls — Design Spec

**Date:** 2026-05-26
**Status:** Approved by user, ready for implementation planning
**Project:** open-ninja
**Builds on:** `2026-05-26-swipe-cube-arcade-design.md`

---

## 1. Summary

Replace the three color-coded cube types (Green/Red/Black) with six material-driven cubes (Wood / Stone / Metal / Crystal / Spiked / Rubber), each with its own mass, bounciness, point value, and role. Add bumpy walls on all four sides of the play area (open KillZone at the bottom) so cubes ricochet irregularly. The combination of variable mass, wider launch impulse range, and bouncy-wall geometry produces emergent variety: lighter cubes rocket high and bounce wildly; heavier cubes barely arc but thud their way across. Audio, visual scale, and brief slice-time hit-stop give the player a felt "sense of mass."

---

## 2. Goals & Non-Goals

**Goals**
- Replace `CubeType` enum with a ScriptableObject-driven `CubeMaterial` system so future materials can be added without code changes.
- Six v1 materials covering a clear light-to-heavy spectrum, including one danger and one bouncy outlier.
- Bumpy walls on left/right/top with energy-decay bounces so cubes eventually settle into the KillZone.
- Three sense-of-mass channels: visual scale, pitched impact audio, and slice hit-stop + blade-trail lag.

**Non-Goals**
- Mobile port. Stays a separate effort. The legacy `Input` calls already function on mobile if needed.
- Audio asset authoring. The system wires hooks (clip refs + pitch); v1 ships with empty/silent clips, designer drops in real SFX later.
- New shaders or PBR rendering for materials beyond what Unity's standard Lit shader supports.
- Object pooling. Bounces increase cube lifespan; if frame rate dips we revisit.

---

## 3. Architecture

`CubeMaterial` ScriptableObject holds all per-material data. `Cube` MonoBehaviour holds a single `[SerializeField] CubeMaterial material` reference and reads everything from it at spawn time. `GameManager` no longer switches on a type enum — `Cube` passes its `material` reference (or a derived role + points value) into `RegisterHit`. Spawner is a weighted picker over a `List<MaterialEntry>` where each entry has a `CubeMaterial` and a weight curve over elapsed run time.

```
                  ┌───────────────────────────┐
                  │ CubeMaterial (SO asset)   │◀──refs──┐
                  │  mass, points, role,      │         │
                  │  scale, bounciness,       │         │
                  │  meshMaterial, audioClip, │         │
                  │  launchImpulseOverride    │         │
                  └────────────┬──────────────┘         │
                               │                        │
            ┌──────────────────┼────────────────────────┤
            ▼                  ▼                        │
       ┌─────────┐       ┌─────────────────┐            │
       │  Cube   │       │  CubeSpawner    │            │
       │  reads  │       │  weighted pick  │            │
       │  mat    │       │  from List<>    │────spawns──┘
       └────┬────┘       └─────────────────┘
            │
            │ HandleSlice → RegisterHit(material, pos)
            ▼
      ┌─────────────────────────┐
      │       GameManager       │
      │  base points = mat.pts  │
      │  danger check = mat.role│
      └─────────────────────────┘

      ┌─────────────────────┐
      │  HitStopController  │ static helper; Cube.HandleSlice → ApplyHitStop(mass)
      └─────────────────────┘

      ┌─────────────────────┐
      │  BladeController    │ public ApplySliceDrag(mass)
      └─────────────────────┘

      ┌─────────────────────────────┐
      │ Walls (3 static colliders)  │
      │ Left, Right, Top — bumpy    │
      │ segmented BoxColliders +    │
      │ shared BouncyWall.physMat   │
      └─────────────────────────────┘
```

---

## 4. `CubeMaterial` ScriptableObject

```csharp
[CreateAssetMenu(menuName = "OpenNinja/Cube Material")]
public class CubeMaterial : ScriptableObject
{
    public string displayName;
    public CubeRole role;                      // Normal / Bonus / Danger
    public int basePoints;
    public float mass;                         // Rigidbody.mass
    public float displayScale;                 // visible weight
    public Material renderMaterial;            // PBR look
    public Color burstTint;                    // particle color on slice
    public float bouncinessMultiplier = 1f;    // post-collision velocity scalar
    public Vector2 launchImpulseOverride;      // (min,max); zero magnitude → use spawner default
    public AudioClip impactClip;               // bounce SFX (may be null in v1)
    public float audioPitchAtMassOne = 1f;     // pitch reference; runtime scales by mass
}

public enum CubeRole { Normal, Bonus, Danger }
```

### v1 catalog

| Asset           | Role   | Pts | Mass | Scale | Bounce× | Impulse override |
|-----------------|--------|-----|------|-------|---------|------------------|
| `Cube_Wood`     | Normal | 1   | 0.4  | 0.7   | 1.0     | (6, 12)          |
| `Cube_Stone`    | Normal | 2   | 1.0  | 1.0   | 1.0     | (default)        |
| `Cube_Metal`    | Bonus  | 3   | 3.0  | 1.4   | 0.9     | (10, 14)         |
| `Cube_Crystal`  | Bonus  | 5   | 0.3  | 0.9   | 1.0     | (default)        |
| `Cube_Spiked`   | Danger | 0   | 1.2  | 1.0   | 1.0     | (default)        |
| `Cube_Rubber`   | Normal | 2   | 0.5  | 0.9   | 1.4     | (default)        |

Render materials are stock Unity Lit with characteristic surfaces: Wood = warm brown, Stone = gray with mild roughness, Metal = dark gray + high smoothness + metallic 1.0, Crystal = light blue + smoothness 0.95 + slight transparency, Spiked = matte black, Rubber = saturated yellow with low smoothness.

---

## 5. Cube prefab

One reusable `Cube` prefab in `Assets/Prefabs/Cube.prefab` (instead of six color-specific prefabs). At spawn time, `CubeSpawner` instantiates the base prefab and assigns a chosen `CubeMaterial`. The `Cube.Awake` (or first frame after `OnEnable`) reads the material and applies:

- `Rigidbody.mass = material.mass`
- `transform.localScale = Vector3.one * material.displayScale`
- `MeshRenderer.sharedMaterial = material.renderMaterial`
- Layer `Cube`

If a material's render assets aren't loaded, the cube falls back to a default flat material with `burstTint` color.

The `Cube` script gains:

```csharp
[SerializeField] private CubeMaterial material;
public CubeMaterial Material => material;

public void Initialize(CubeMaterial mat) { material = mat; ApplyMaterial(); }
private void ApplyMaterial() { /* assign mass/scale/render/layer */ }

private void OnCollisionEnter(Collision c)
{
    if (_consumed) return;
    if (!c.gameObject.CompareTag("Wall")) return;
    PlayImpactAudio(c);
    if (material.bouncinessMultiplier != 1f && _rb != null)
        _rb.linearVelocity *= material.bouncinessMultiplier;
}
```

(Note: Unity 6 renamed `Rigidbody.velocity` → `linearVelocity`.)

---

## 6. `CubeSpawner` changes

Replace `[SerializeField] Cube greenPrefab/redPrefab/blackPrefab` with:

```csharp
[Serializable]
private struct MaterialEntry
{
    public CubeMaterial material;
    public AnimationCurve weightOverTime; // weight at run-elapsed t
}

[SerializeField] private Cube cubePrefab;            // single base prefab
[SerializeField] private List<MaterialEntry> entries;
```

`SpawnOne` becomes:

1. Sum each entry's `weightOverTime.Evaluate(elapsed)`; roll uniform `[0, totalWeight)`; pick entry.
2. Compute spawn position (unchanged) and rotation.
3. Pick `(low, high)` from `material.launchImpulseOverride` if set, else from the spawner-level `launchImpulseRange`.
4. Instantiate `cubePrefab`, call `cube.Initialize(material)`, then apply impulse.

Spawner-level fallback impulse range widens from `(7, 11)` → `(5, 14)` to give the per-material overrides room.

Default `Rigidbody.drag = 0.05` and `angularDrag = 0.1` set in `Initialize` so wall bounces decay.

`dangerProbabilityOverTime` is removed — the danger curve is now expressed through the `Spiked` entry's `weightOverTime` curve directly.

---

## 7. Walls

Three static GameObjects under `Systems/Bounds/Walls/`:

```
Walls
├── Wall_Left   (x ≈ -10, BoxCollider, tag = Wall)
├── Wall_Right  (x ≈ +10, BoxCollider, tag = Wall)
└── Wall_Top    (y ≈ +8,  BoxCollider, tag = Wall)
```

**Bumpiness** — each wall is built from 12 segment colliders rotated ±8° around the wall's perpendicular axis, attached as children of the wall root. The renderer (a single stretched cube mesh) stays flat for visual coherence; the bumpiness is purely in the collider. Generation lives in the scene-setup script — for each wall, instantiate 12 child empty GameObjects with a `BoxCollider` and a small rotation jitter.

**Shared `BouncyWall.physicMaterial`** asset assigned to every wall segment:

```
bounciness = 0.75
dynamicFriction = 0.1
staticFriction = 0.1
bounceCombine = Maximum
frictionCombine = Minimum
```

**Tag** — all wall objects use a new tag `Wall` so `Cube.OnCollisionEnter` can filter for wall collisions (vs. cube-cube which can happen mid-air).

**Top wall corner cut** — instead of meeting Left/Right at 90°, the Top wall ends ~2 units before each side wall and has a 45° angled segment bridging the corner. Prevents cubes from wedging into the corner.

---

## 8. Sense of mass

### 8a. Visual scale
`Cube.Initialize` sets `transform.localScale = Vector3.one * material.displayScale`. Wood = 0.7 cube, Metal = 1.4 cube. Bigger cubes both *look* heavier and have a larger collision profile (which is fine — they're slower and lower-arcing anyway).

### 8b. Audio
`Cube.OnCollisionEnter` plays `material.impactClip` at the contact point with:

```csharp
float pitch = material.audioPitchAtMassOne *
              Mathf.Lerp(1.6f, 0.5f, Mathf.InverseLerp(0.3f, 3.0f, material.mass));
AudioSource.PlayClipAtPoint(clip, contact, volume) // + pitch via temporary AudioSource
```

Implementation note: `PlayClipAtPoint` doesn't expose pitch directly, so we create a transient `GameObject` with an `AudioSource`, configure it, play one-shot, destroy after `clip.length`. Helper `AudioOneShot.Play(clip, pos, pitch, volume)`.

V1 ships with `material.impactClip = null` allowed; the helper no-ops in that case so the system runs silently until SFX assets are imported.

### 8c. Slice resistance — hit-stop
New static class `HitStopController`:

```csharp
public static class HitStopController
{
    public static void Apply(float mass)
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;
        // Map mass [0.3, 3.0] → (timeScale, duration)
        float t = Mathf.InverseLerp(0.3f, 3.0f, mass);
        float scale = Mathf.Lerp(0.8f, 0.15f, t);
        float duration = Mathf.Lerp(0.02f, 0.08f, t);
        _runner.StartCoroutine(Run(scale, duration));
    }
    private static IEnumerator Run(float scale, float duration)
    {
        float prev = Time.timeScale;
        Time.timeScale = scale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = prev;
    }
}
```

Requires a `MonoBehaviour` runner — a hidden singleton spawned on first call. Re-entrant calls during an active hit-stop early-return so we don't stack scales (a second slice during an existing hit-stop is fine — the next slice fires its own hit-stop after the first resolves).

`Cube.HandleSlice` calls `HitStopController.Apply(material.mass)` after registering the hit.

### 8d. Slice resistance — blade-trail lag
`BladeController` gains a public method and per-frame state:

```csharp
private float _dragUntil;
private float _dragFactor;

public void ApplySliceDrag(float mass)
{
    float t = Mathf.InverseLerp(0.3f, 3.0f, mass);
    _dragFactor = Mathf.Lerp(0.1f, 0.6f, t);
    _dragUntil = Time.unscaledTime + Mathf.Lerp(0.04f, 0.12f, t);
}
```

In `Update`, after computing `worldNow`:

```csharp
if (_dragUntil > Time.unscaledTime)
    bladeTip.position = Vector3.Lerp(bladeTip.position, worldNow, 1f - _dragFactor);
else
    bladeTip.position = worldNow;
```

`Cube.HandleSlice` calls `BladeController.Instance.ApplySliceDrag(material.mass)`. The trail visually "drags" through the slice plane and re-catches up over the next 50–120ms.

---

## 9. `GameManager` changes

Smallest possible surface. The combo math, lives, events stay identical. Only changes:

- `RegisterHit` takes `CubeMaterial` (or its derived `(int points, bool isBonus)` data) instead of `CubeType`. Black-cube-specific check disappears; danger is routed through `RegisterDangerClick` based on `material.role == Danger`.
- `BasePointsFor` is deleted — `Cube.HandleSlice` passes `material.basePoints` directly.

```csharp
public void RegisterHit(int basePoints, Vector3 worldPos) { /* unchanged math, takes points directly */ }
```

Tests update to call `RegisterHit(1, Vector3.zero)` instead of `RegisterHit(CubeType.Green, Vector3.zero)`.

---

## 10. Data flow (changes only)

### Slice
```
Blade SphereCastAll → Cube hit
 └─ Cube.HandleSlice(slicePoint)
     ├─ spawn burst (cube material's burstTint)
     ├─ BladeController.Instance.ApplySliceDrag(material.mass)
     ├─ HitStopController.Apply(material.mass)
     ├─ if material.role == Danger → GM.RegisterDangerClick(slicePoint)
     │  else                         GM.RegisterHit(material.basePoints, slicePoint)
     └─ Destroy(cube)
```

### Wall bounce
```
Rigidbody.OnCollisionEnter(c)
 └─ if c.gameObject.tag == "Wall":
     ├─ AudioOneShot.Play(material.impactClip, c.contacts[0].point, pitchFromMass, volume)
     └─ if material.bouncinessMultiplier != 1f:
         linearVelocity *= material.bouncinessMultiplier
```

### Spawn
```
SpawnLoop tick
 ├─ pick CubeMaterial via weighted roll over entries
 ├─ instantiate cubePrefab at spawn line
 ├─ cube.Initialize(material)         // mass, scale, render, layer
 ├─ (low, high) = override or default
 └─ Rigidbody.AddImpulse(...)
```

---

## 11. Tuning defaults (Inspector starting points)

| Knob | Value |
|------|-------|
| Spawner `launchImpulseRange` (up) | (5, 14) |
| Spawner `sideImpulseRange` | (0, 4) |
| Cube default `Rigidbody.drag` | 0.05 |
| Cube default `Rigidbody.angularDrag` | 0.1 |
| Wall `bounciness` | 0.75 |
| Wall segment angle jitter | ±8° |
| Wall segments per side | 12 |
| `HitStopController` mass range | [0.3, 3.0] |
| Hit-stop timeScale range | [0.8, 0.15] |
| Hit-stop duration range | [0.02, 0.08] |
| Slice drag factor range | [0.1, 0.6] |
| Slice drag duration range | [0.04, 0.12] |

---

## 12. Tests

Existing 17 EditMode tests continue to apply with light edits:

- `GameManagerTests` — `RegisterHit(CubeType.Green, ...)` → `RegisterHit(1, ...)` (pass `basePoints` directly). The combo math doesn't change so the assertions stand.
- `CubeTests` — construct an in-memory `CubeMaterial`:
  ```csharp
  var mat = ScriptableObject.CreateInstance<CubeMaterial>();
  mat.basePoints = 1;
  mat.role = CubeRole.Normal;
  mat.mass = 1f;
  cube.Initialize(mat);
  cube.HandleSlice(Vector3.zero);
  ```
  Re-verify slice, fall-off, danger, and double-slice cases.

New tests:
- `CubeMaterialTests` — sanity that the SO asset fields serialize round-trip (a smoke test).
- `CubeSpawnerWeightedPickTests` — drive `entries` with synthetic curves, assert the picker's probability distribution over many rolls (statistical, but tolerant — within ±5%).
- `HitStopControllerTests` — call `Apply`, advance unscaled time via a test fixture, assert `Time.timeScale` restores. (Runs only in PlayMode because timeScale + coroutines.)

---

## 13. Files

```
NEW   Assets/Scripts/Core/CubeMaterial.cs                ScriptableObject + CubeRole enum
NEW   Assets/Scripts/Gameplay/HitStopController.cs       Static helper + hidden MonoBehaviour runner
NEW   Assets/Scripts/Util/AudioOneShot.cs                Pitched one-shot helper
MOD   Assets/Scripts/Gameplay/Cube.cs                    Reads CubeMaterial, OnCollisionEnter audio+bounce
MOD   Assets/Scripts/Gameplay/CubeSpawner.cs             MaterialEntry list, weighted picker, impulse override
MOD   Assets/Scripts/Gameplay/BladeController.cs         ApplySliceDrag + per-frame drag application
MOD   Assets/Scripts/Core/GameManager.cs                 RegisterHit takes basePoints (int)
DEL   Assets/Scripts/Core/CubeType.cs                    replaced by CubeMaterial
DEL   Assets/Prefabs/Cube_Green/Red/Black.prefab         replaced by single Cube.prefab
NEW   Assets/Prefabs/Cube.prefab                         base prefab (no material assigned)
NEW   Assets/Materials/                                   Wood/Stone/Metal/Crystal/Spiked/Rubber.mat
NEW   Assets/Data/CubeMaterials/                          Wood/Stone/Metal/Crystal/Spiked/Rubber.asset
NEW   Assets/Data/BouncyWall.physicMaterial               wall bounce material
MOD   Assets/Editor/SceneSetup.cs                        build walls, build 6 materials, build SOs, spawner entries
```

---

## 14. Edge cases

| Case | Resolution |
|------|------------|
| Cube-cube collision mid-air | Cube.OnCollisionEnter filters on `Wall` tag; cube-cube collisions don't trigger the audio/bounce-mult path. Physics still applies — they bump naturally. |
| Hit-stop during another hit-stop | Re-entrant `Apply` early-returns. Each slice within the active window doesn't stack. Slight feel cost (chained heavy slices feel less heavy) but avoids `Time.timeScale` getting clobbered. |
| Game-over during hit-stop | The active coroutine restores `Time.timeScale` to its pre-stop value, but `SetGameOver` ran in between and set it to 0. Solution: `HitStopController` reads `IsGameOver` before restoring; if game-over, leave timeScale at 0. |
| Spawner with all entry weights at 0 at time t | Picker returns the first entry. Curves should be authored to always have at least one positive weight; v1 enforces via an `Assert.IsTrue(totalWeight > 0)` in dev builds. |
| Crystal mass 0.3 + impulse override default | Crystal uses spawner default range (5, 14). Lightest mass + same impulse = highest velocity. Cap upward velocity at `25 m/s` in `Cube.Initialize` to prevent off-screen escapes faster than camera can track. |
| Audio on every wall bounce → spam | `AudioOneShot.Play` ignores `null` clips (v1 default). Per-cube `_lastAudioTime` ratelimit set to `0.05s` to avoid stereo-burst on multi-segment hits. |
| Bouncy walls preventing the cube from ever reaching KillZone | Energy decay (`drag=0.05` + `bounciness=0.75` + `multiplier ≤ 1.0` for non-rubber) is empirically enough. Hard safety net: per-cube `_spawnTime`; if `Time.time - _spawnTime > 8s`, the cube self-destroys silently. |

---

## 15. Out of scope (explicitly deferred)

- Mobile build & touch input
- Real audio clips for `impactClip` (v1 ships silent stubs)
- Object pooling
- High-score persistence
- Custom particle effects per material (e.g., wood shards vs metal sparks). v1 uses one `SliceBurst` colored by `burstTint`.
- Bouncing cube-vs-cube combo bonuses
- Multi-touch for parallel blade input

---

## 16. Open questions for implementation

- Exact wall positions/sizes are best discovered during scene authoring. Spec gives starting values; tune after first playtest.
- Whether the `Cube.prefab` should use one shared MeshRenderer + swap materials at runtime, or use a child mesh-per-material. v1: shared renderer + `sharedMaterial` swap (cheaper).
- Audio routing — global `AudioMixer` setup is out of scope; one-shots play through the default sink.
