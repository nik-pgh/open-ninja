using System.Collections;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// SFX clip holder + event router. Cube.HandleSlice calls PlaySlice directly
    /// (it has the CubeRole and mass on hand). Combo, miss, and game-over stings
    /// come from GameManager events. All SFX go through AudioOneShot so timeScale=0
    /// (game over) doesn't pause them.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Slice SFX")]
        [SerializeField] private AudioClip sliceClip;
        [SerializeField] private AudioClip bonusSliceClip;
        [SerializeField] private AudioClip dangerClip;

        [Header("Penalty / state SFX")]
        [SerializeField] private AudioClip missClip;
        [SerializeField] private AudioClip comboTierClip;
        [SerializeField] private AudioClip gameOverClip;

        [Header("Mix")]
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;
        [Range(0.1f, 2f)] [SerializeField] private float baseSlicePitch = 1f;

        private int _lastLives = int.MaxValue;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            StartCoroutine(SubscribeWhenReady());
        }

        private void OnDisable()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.OnLivesChanged -= HandleLivesChanged;
            gm.OnComboChanged -= HandleComboChanged;
            gm.OnGameOver    -= HandleGameOver;
        }

        // GameManager may Awake on the same frame; wait one tick so we don't race it.
        private IEnumerator SubscribeWhenReady()
        {
            float deadline = Time.unscaledTime + 2f;
            while (GameManager.Instance == null && Time.unscaledTime < deadline) yield return null;

            var gm = GameManager.Instance;
            if (gm == null) yield break;

            _lastLives = gm.Lives;
            gm.OnLivesChanged += HandleLivesChanged;
            gm.OnComboChanged += HandleComboChanged;
            gm.OnGameOver    += HandleGameOver;
        }

        // ---- Public API (Cube calls this directly) ----

        public void PlaySlice(Vector3 pos, CubeRole role, float massForPitch = 1f)
        {
            AudioClip clip = role switch
            {
                CubeRole.Bonus  => bonusSliceClip != null ? bonusSliceClip : sliceClip,
                CubeRole.Danger => dangerClip,
                _               => sliceClip,
            };
            if (clip == null) return;

            int combo = GameManager.Instance != null ? GameManager.Instance.ComboMultiplier : 1;
            float comboBoost = 1f + Mathf.Clamp(combo - 1, 0, 7) * 0.05f;
            float massPitch  = Mathf.Lerp(1.2f, 0.7f, Mathf.InverseLerp(0.3f, 3f, massForPitch));
            AudioOneShot.Play(clip, pos, baseSlicePitch * massPitch * comboBoost, sfxVolume);
        }

        // ---- Event handlers ----

        private void HandleLivesChanged(int lives)
        {
            // Final life loss is covered by the game-over sting; skip the miss thunk then.
            if (lives < _lastLives && lives > 0 && missClip != null)
                AudioOneShot.Play(missClip, Vector3.zero, 1f, sfxVolume);
            _lastLives = lives;
        }

        private void HandleComboChanged(int multiplier)
        {
            if (multiplier <= 1 || comboTierClip == null) return;
            float pitch = 1f + Mathf.Clamp(multiplier - 2, 0, 6) * 0.08f;
            AudioOneShot.Play(comboTierClip, Vector3.zero, pitch, sfxVolume * 0.7f);
        }

        private void HandleGameOver(int _)
        {
            if (gameOverClip != null)
                AudioOneShot.Play(gameOverClip, Vector3.zero, 1f, sfxVolume);
        }
    }
}
