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
