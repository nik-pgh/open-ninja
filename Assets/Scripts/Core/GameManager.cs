using System;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Single source of truth for run-time game state. Other components either call
    /// the Register* mutators or subscribe to the events; nobody else writes state.
    /// Runs early so UI listeners see Instance != null in their OnEnable.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Tuning")]
        [SerializeField] private float comboWindowSeconds = 1.0f;
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int maxComboMultiplier = 8;

        public int Score { get; private set; }
        public int ComboMultiplier { get; private set; } = 1;
        public int Lives { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>Seconds left on the active combo window. Hits 0 when the window expires.</summary>
        public float ComboTimeRemaining => Mathf.Max(0f, _comboTimer);

        /// <summary>Full combo window length. UIs use this to compute a normalized [0,1] fill.</summary>
        public float ComboWindowSeconds => comboWindowSeconds;

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

        public void RegisterDangerClick(Vector3 worldPos)
        {
            if (IsGameOver) return;
            ApplyPenalty();
            OnHit?.Invoke(-1, 1, worldPos);
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


    }
}
