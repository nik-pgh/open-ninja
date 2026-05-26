# Swipe-Cube Arcade — Design Spec

**Date:** 2026-05-26
**Status:** Approved by user, ready for implementation planning
**Project:** open-ninja (Unity, fresh project with Coplay MCP plugin)

---

## 1. Summary

A Fruit Ninja-style arcade game built in Unity 3D. Cubes are launched up from the bottom of the screen and arc back down under gravity. The player swipes the mouse (held LMB drag) through cubes to slice them; slow drags don't count. Scoring is combo-based: hits inside a 0.5 s window multiply each other. Missing a regular cube (it falls off the bottom) costs a life; missing a "danger" cube is safe; slicing a danger cube costs a life. The run ends when lives reach 0.

---

## 2. Goals & Non-Goals

**Goals**
- Provide a clean, well-structured Unity base that demonstrates: prefab-driven cubes, physics-based motion, swipe input via swept-sphere raycasts, a single source of truth for game state, event-driven UI, and a difficulty ramp.
- Be easy to tune from the Inspector (curves, prefabs, layer masks, thresholds).
- Single seam (`GameManager` events) where listeners attach — replaceable later with an event bus if needed.

**Non-Goals**
- Runtime mesh cutting (cube splits into halves or mesh-slice along the swipe plane). Slice is a particle burst.
- Touch / multi-touch input. Mouse-only for now; the input layer is isolated so touch can be added later.
- Persistent high scores, online leaderboards, audio mix, accessibility passes.
- Multiple scenes / menus. One scene with a game-over overlay.

---

## 3. Gameplay Rules

| Rule | Value |
|------|-------|
| Starting lives | 3 |
| Combo window | 0.5 s (resets each successful slice) |
| Max combo multiplier | ×8 |
| Green cube | +1 pt × multiplier |
| Red cube | +2 pt × multiplier |
| Black ("danger") cube — sliced | –1 life, combo resets to ×1 |
| Black cube — falls off bottom | Silent despawn, no penalty |
| Green/Red — falls off bottom | –1 life, combo resets to ×1 |
| Game over | `lives <= 0`; freezes scene, shows panel + Restart |

**Combo behavior**
- `ComboMultiplier` starts at 1. Each slice awards `basePoints × ComboMultiplier` **at the current value**, then increments the multiplier (capped at ×8) and refreshes the 0.5 s timer.
- So: 1st slice awards ×1, 2nd within window awards ×2, 3rd ×3, … cap at ×8.
- Popup label rule: if the multiplier used was 1, show `"+N"`; otherwise `"+N x{mult}!"`.
- Timer expires → multiplier returns to 1. Combo badge hides.
- Slicing a black cube resets multiplier to 1 even though it's a "hit."

**Difficulty ramp**
- `spawnIntervalCurve(elapsed)` shortens spawn interval over time (default tuning: 1.2 s → 0.4 s by 60 s).
- `dangerProbCurve(elapsed)` raises black-cube probability over time (default: 0.05 → 0.15 by 60 s).
- Color weights at any given moment:
  - Black: `dangerP`
  - Red: 0.25 × (1 − dangerP)  (approx. — red stays at ~25% of non-danger spawns)
  - Green: remainder
- Curves are `AnimationCurve` assets exposed in the Inspector for easy tuning.

---

## 4. Architecture

Singleton `GameManager` owns all mutable game state. All other components either *call into it* (cubes on slice / miss; spawner reads `IsGameOver`; restart button) or *subscribe to its events* (every UI view). State has exactly one writer.

```
                       ┌───────────────────────┐
                       │      GameManager      │   ◀── Restart button
                       │  (score, combo, lives)│
                       └──────────┬────────────┘
                                  │ C# events
            ┌─────────────────────┼──────────────────────┐
            ▼                     ▼                      ▼
       ScoreView           ComboBadgeView           LivesView
       (TMP_Text)          (TMP_Text)               (heart Images)
                                  │
                                  ▼
                         ComboPopupSpawner
                         (instantiates floaters on OnHit)
                                  │
                                  ▼
                          GameOverView
                          (panel + Restart)

      ┌──────────────┐         ┌──────────────┐
      │ CubeSpawner  │────────▶│  Cube (x N)  │
      │ (timer +     │ Instan- │  Rigidbody   │
      │  curves)     │ tiate   │  BoxCollider │
      └──────────────┘         └──────┬───────┘
                                      │ HandleSlice / HandleFellOff
                                      ▼
                              GameManager.RegisterHit /
                              RegisterDangerClick / RegisterMiss

      ┌──────────────────┐
      │ BladeController  │ SphereCastAll across swipe segment per frame
      │ (mouse → world)  │──────────────────────────────────────────────▶ Cube.HandleSlice
      │ + TrailRenderer  │
      └──────────────────┘

      ┌──────────────┐
      │  KillZone    │ OnTriggerEnter → cube.HandleFellOff()
      │ (bottom box) │
      └──────────────┘
```

---

## 5. Scene Layout

```
SampleScene
├── Main Camera                  (perspective, looking down +Z toward play plane at z=0)
├── Directional Light
├── --- Bounds ---
│   └── KillZone                 (BoxCollider, isTrigger; spans full screen width below the visible area)
├── --- Systems ---
│   ├── GameManager              (singleton MonoBehaviour)
│   ├── CubeSpawner              (references prefabs + curves)
│   └── BladeController          (input + sweep detection)
│       └── BladeTip             (empty Transform with TrailRenderer child)
└── --- Canvas (Screen Space - Overlay) ---
    ├── ScorePanel               (TMP_Text "Score: 0")
    ├── ComboBadge               (TMP_Text "Combo x{n}"; hidden when n==1)
    ├── LivesRow                 (3 heart Image children)
    ├── ComboPopupLayer          (anchor for floating "+N x{mult}!" texts; pure RectTransform)
    └── GameOverPanel            (final score, "New best!" if applicable, Restart button; hidden)
```

**Play space**
- Cubes spawn just below the camera frustum's bottom edge (`spawnLineLeft` / `spawnLineRight` transforms mark the X range, all on the z=0 plane).
- `KillZone` sits below the spawn line so a cube's full arc completes inside the frustum before missing.
- Camera is angled so the z=0 plane covers the full screen comfortably; cubes have small random rotation for visual variety but stay on the play plane.

**Prefabs**
```
Prefabs/
├── Cube_Green.prefab    (Cube script, type=Green, basePoints=1, green material)
├── Cube_Red.prefab      (Cube script, type=Red,   basePoints=2, red material)
├── Cube_Black.prefab    (Cube script, type=Black, basePoints=0, black material)
├── SliceBurst.prefab    (ParticleSystem; ~20 particles, gravity, self-destroys ~0.8s)
└── ComboPopup.prefab    (UI element with TMP_Text + an animation that floats up + fades)
```

---

## 6. Component Contracts

### `GameManager` (singleton MonoBehaviour)

**Serialized config**
- `comboWindowSeconds = 0.5f`
- `startingLives = 3`
- `maxComboMultiplier = 8`

**State**
- `int Score`
- `int ComboMultiplier`
- `int Lives`
- `bool IsGameOver`
- `float _comboTimer`

**Public API**
- `void RegisterHit(CubeType type, Vector3 worldPos)`
  - Computes `awardedPoints = basePoints(type) × ComboMultiplier` and `awardedMult = ComboMultiplier`. Adds to `Score`.
  - Fires `OnScoreChanged(Score)` and `OnHit(awardedPoints, awardedMult, worldPos)` (so the popup shows the multiplier *used* for this slice).
  - Then advances multiplier: `ComboMultiplier = min(ComboMultiplier+1, maxComboMultiplier)`; sets `_comboTimer = comboWindowSeconds`; fires `OnComboChanged(ComboMultiplier)` (so the badge shows the multiplier the *next* slice will use).
- `void RegisterDangerClick(Vector3 worldPos)`
  - `Lives--`, `ComboMultiplier = 1`, `_comboTimer = 0`. Fires `OnLivesChanged`, `OnComboChanged(1)`, `OnHit(-1, 1, worldPos)` (so the popup spawner can render a "−1!" floater). If `Lives <= 0`, calls `SetGameOver()`.
- `void RegisterMiss()`
  - Same penalty path as a danger click, but no `OnHit` (no popup at the bottom).
- `void ResetGame()`
  - Destroys all live cubes, resets state, fires every `*Changed` event to refresh UI, `Time.timeScale = 1`. Spawner resumes via its `IsGameOver` guard.

**Events (plain C# `Action`s)**
- `event Action<int> OnScoreChanged`
- `event Action<int> OnLivesChanged`
- `event Action<int> OnComboChanged`
- `event Action<int, int, Vector3> OnHit`   *(points, multiplier, worldPos)*
- `event Action<int> OnGameOver`            *(finalScore)*

**Internal**
- `Update()` ticks `_comboTimer`; when it crosses zero with `ComboMultiplier > 1`, resets multiplier to 1 and fires `OnComboChanged(1)`.
- `SetGameOver()` sets the flag, calls `Time.timeScale = 0`, fires `OnGameOver`.

### `Cube` (MonoBehaviour on every cube prefab)

**Serialized**
- `CubeType type` (enum: `Green`, `Red`, `Black`)
- `int basePoints`
- `Color burstTint`
- `SliceBurst burstPrefab`

**Behavior**
- `void HandleSlice(Vector3 slicePoint)`
  - Guards on `_consumed`; sets `_consumed = true`.
  - Spawns `burstPrefab` at `slicePoint` with `burstTint`.
  - Calls `GameManager.Instance.RegisterDangerClick(slicePoint)` if `Black`, else `RegisterHit(type, slicePoint)`.
  - `Destroy(gameObject)`.
- `void HandleFellOff()` *(called by `KillZone`)*
  - If `_consumed` → just destroy.
  - If `Black` → silent destroy (no penalty).
  - Else → `GameManager.Instance.RegisterMiss()` then destroy.

### `BladeController` (MonoBehaviour, replaces a click router)

**Serialized**
- `float bladeRadius = 0.25f`
- `float minSliceSpeed = 4f` (world units / sec)
- `LayerMask cubeMask`
- `Transform bladeTip`           *(TrailRenderer lives on a child of this)*
- `float playPlaneZ = 0f`
- `Camera gameCamera`            *(falls back to `Camera.main`)*

**Behavior**
- Per frame:
  1. Convert mouse screen position to a world point on the play plane (using a fixed depth derived from camera position and `playPlaneZ`).
  2. Move `bladeTip.position` there. (TrailRenderer follows automatically.)
  3. On `Input.GetMouseButtonDown(0)` → `_isSwiping = true`, `_lastTipWorld = worldNow`, clear the TrailRenderer.
  4. On `Input.GetMouseButtonUp(0)` → `_isSwiping = false`.
  5. While `_isSwiping`:
     - `delta = worldNow − _lastTipWorld`
     - `speed = delta.magnitude / Time.deltaTime`
     - If `speed >= minSliceSpeed && delta.magnitude > 0`:
       - `Physics.SphereCastAll(_lastTipWorld, bladeRadius, delta.normalized, delta.magnitude, cubeMask)`
       - For each unique `Cube` (dedupe via a `HashSet<int>` of instance IDs already sliced this swipe), call `cube.HandleSlice(hit.point)`.
     - Update `_lastTipWorld = worldNow`.
  6. On mouse-up, clear the slice dedupe set.

**Why swept sphere?** A line-only test can tunnel between cubes on a fast horizontal swipe. A swept sphere with a small radius catches anything the blade visibly passes through and feels forgiving without being mushy.

**Why project to a fixed plane?** Cubes live on z=0 (2.5D playfield in a 3D scene). Projecting the cursor onto that same plane lets us run clean 3D physics queries while keeping input as flat 2D screen movement. Swap for `Camera.ScreenPointToRay` if cubes ever move on the Z axis.

### `CubeSpawner` (MonoBehaviour)

**Serialized**
- `Cube greenPrefab`, `Cube redPrefab`, `Cube blackPrefab`
- `Transform spawnLineLeft`, `Transform spawnLineRight`
- `AnimationCurve spawnIntervalOverTime`
- `AnimationCurve dangerProbabilityOverTime`
- `Vector2 launchImpulseRange` *(min/max upward force)*
- `Vector2 sideImpulseRange`   *(min/max horizontal force; sign randomized)*

**Behavior**
- Tracks `_runStartTime` (set on `Start` and on `GameManager.ResetGame`).
- Coroutine loop:
  - `elapsed = Time.time − _runStartTime`
  - `interval = spawnIntervalOverTime.Evaluate(elapsed)`
  - `dangerP  = dangerProbabilityOverTime.Evaluate(elapsed)`
  - Wait `interval`. If `GameManager.IsGameOver`, wait briefly and re-check (don't spawn).
  - Roll: `r = Random.value`. Pick prefab using weights documented in §3.
  - `x = Random.Range(spawnLineLeft.x, spawnLineRight.x)`
  - Instantiate at `(x, spawnLineLeft.y, 0)` with `Random.rotation`.
  - Apply impulse `(Random.Range(-side, side), Random.Range(launchMin, launchMax), 0)` via `Rigidbody.AddForce(..., ForceMode.Impulse)`.
- Subscribes to `GameManager.OnGameOver` (no-op until restart) and to a hook in `ResetGame` to refresh `_runStartTime`.

### `KillZone` (MonoBehaviour on the bottom trigger)

- `OnTriggerEnter(Collider other)` → if it carries a `Cube`, call `cube.HandleFellOff()`.

### UI Scripts (one MonoBehaviour per UI element)

| Script | Subscribes to | Effect |
|---|---|---|
| `ScoreView` | `OnScoreChanged` | Update `TMP_Text` |
| `ComboBadgeView` | `OnComboChanged` | Show `"Combo x{n}"` for `n > 1`, hide otherwise |
| `LivesView` | `OnLivesChanged` | Toggle heart `Image.enabled` (or color) for the first `Lives` icons |
| `ComboPopupSpawner` | `OnHit` | Instantiate `ComboPopup` under `ComboPopupLayer` at `WorldToScreenPoint(worldPos)`; set text using the rule: `points < 0 → "-1!"`, `mult == 1 → "+{points}"`, `mult >= 2 → "+{points} x{mult}!"`. Popup self-animates: float ~60 px up + fade over 0.8 s, then self-destroys |
| `GameOverView` | `OnGameOver` | Show panel with final score; Restart button → `GameManager.ResetGame()` and hide panel |

**Subscription discipline:** every UI script subscribes in `OnEnable`, unsubscribes in `OnDisable`. UI scripts never write to `GameManager` state directly; only the Restart button calls a mutator (`ResetGame`).

---

## 7. Data Flow

### Slice → score → popup
```
LMB held:
  BladeController per frame
    ├─ project cursor to world (plane z=0)
    ├─ bladeTip.position = worldNow         (TrailRenderer follows)
    └─ if speed ≥ minSliceSpeed:
        SphereCastAll(lastTip → worldNow, radius)
          for each new Cube hit:
            cube.HandleSlice(hit.point)
              ├─ spawn SliceBurst at hit.point (cube-colored)
              ├─ GameManager.RegisterHit(type, hit.point)   // or RegisterDangerClick for black
              │   ├─ awarded = basePoints × ComboMultiplier   // uses CURRENT value
              │   ├─ Score += awarded
              │   ├─ fires OnScoreChanged, OnHit(awarded, ComboMultiplier, worldPos)
              │   ├─ ComboMultiplier = min(ComboMultiplier+1, max)   // bump for next slice
              │   ├─ _comboTimer = comboWindowSeconds
              │   └─ fires OnComboChanged(ComboMultiplier)
              └─ Destroy(cube)
        ComboPopupSpawner sees OnHit → spawns "+N x{mult}!" floater
```

### Combo timeout
```
GameManager.Update():
  if _comboTimer > 0:
    _comboTimer -= Time.deltaTime
    if _comboTimer ≤ 0 and ComboMultiplier > 1:
      ComboMultiplier = 1
      fires OnComboChanged(1)         // badge hides
```

### Missed cube (fell off bottom)
```
KillZone OnTriggerEnter:
  cube.HandleFellOff()
    ├─ if Black: silent destroy
    └─ else: GameManager.RegisterMiss()
            ├─ Lives -= 1
            ├─ ComboMultiplier = 1, _comboTimer = 0
            ├─ fires OnLivesChanged, OnComboChanged(1)
            └─ if Lives ≤ 0: SetGameOver()
        Destroy(cube)
```

### Game over / restart
```
SetGameOver():
  IsGameOver = true
  Time.timeScale = 0                 // freezes physics, popups, particles
  fires OnGameOver(finalScore)       // GameOverView shows panel
  (Spawner is already idle thanks to its IsGameOver guard.)

Restart button:
  GameOverView.OnRestartClick → GameManager.ResetGame()
    ├─ Destroy all cubes (track via a runtime list registered on cube Start, or FindObjectsByType)
    ├─ Score=0, ComboMultiplier=1, Lives=startingLives, _comboTimer=0, IsGameOver=false
    ├─ CubeSpawner refreshes _runStartTime
    ├─ Time.timeScale = 1
    └─ fires OnScoreChanged, OnLivesChanged, OnComboChanged → UI snaps back
```

---

## 8. Tuning Defaults (Inspector starting points)

| Knob | Default |
|---|---|
| `comboWindowSeconds` | 0.5 |
| `startingLives` | 3 |
| `maxComboMultiplier` | 8 |
| `bladeRadius` | 0.25 (world units) |
| `minSliceSpeed` | 4.0 (world units / sec) |
| `launchImpulseRange` | (7, 11) |
| `sideImpulseRange` | (0, 2) |
| `spawnIntervalOverTime` | linear curve from 1.2 s @ t=0 → 0.4 s @ t=60 (flat after) |
| `dangerProbabilityOverTime` | linear curve from 0.05 @ t=0 → 0.15 @ t=60 (flat after) |
| Red weight (of non-danger) | ~0.25 |
| TrailRenderer time | 0.15 s |

---

## 9. Edge Cases & Resolutions

| Case | Resolution |
|---|---|
| Two cubes intersected by one swipe segment | `SphereCastAll` returns both; dedupe by instance ID; both score; combo advances twice |
| Same cube hit multiple frames in one swipe | `Cube._consumed` flag + per-swipe dedupe `HashSet` |
| Cube falls off after being sliced (network of triggers) | `_consumed` guard in `HandleFellOff` ignores already-sliced cubes |
| Slow drag through a stationary cube | Below `minSliceSpeed` → no slice; cube continues to fall |
| Combo popup outlives the cube that spawned it | Popup is parented to `ComboPopupLayer` on the canvas, not the cube |
| Game-over with cubes mid-air | `Time.timeScale = 0` freezes them; `ResetGame` clears them before unfreezing |
| Player swipes during the game-over freeze | `BladeController` reads `GameManager.IsGameOver` and skips the sphere cast (still moves `bladeTip` so trail looks alive) |
| Camera reference | `gameCamera` serialized, falls back to `Camera.main` if null |
| Click on a black cube already partially scored from a green-cube combo | Black always resets combo to 1 — it's both a penalty and a combo breaker |

---

## 10. Out of Scope (Explicitly Deferred)

- Mesh-cut halves or runtime mesh slicing — particle burst only.
- Touch input — `BladeController` input layer is isolated, swap-friendly.
- Audio — `Cube.HandleSlice` and `GameManager.SetGameOver` have a clear hook point for `AudioSource.PlayClipAtPoint` but no SFX assets ship with the base.
- High score persistence — `GameOverView` shows the run's final score only; `PlayerPrefs` integration is a one-line addition later.
- Multiple scenes / start menu — single scene with overlay.
- Object pooling for cubes / popups / bursts — Destroy/Instantiate is fine at this spawn rate; pooling is a later optimization.

---

## 11. Open Questions for Implementation

- Exact camera angle and play-plane Z — to be set during scene authoring.
- Heart icon art — placeholder Unity built-in sprite for v0 (`Knob.psd` or a TMP heart glyph) until art is provided.
- Trail material — Unity's default Sprites/Default with a white-to-transparent gradient is fine for v0.
