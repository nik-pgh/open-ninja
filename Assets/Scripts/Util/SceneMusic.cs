using System.Collections;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Per-scene BGM. Owns an AudioSource that loops the configured track and
    /// fades it out on game over (unscaled time, since timeScale becomes 0).
    /// StartScene typically sets stopOnGameOver=false since there's no GameManager.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip track;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool stopOnGameOver = true;
        [SerializeField, Range(0.1f, 5f)] private float fadeOnGameOverSeconds = 1.5f;

        private AudioSource _source;
        private Coroutine _fadeCo;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.clip = track;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = volume;
            _source.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            if (playOnEnable && _source.clip != null) _source.Play();
            if (stopOnGameOver) StartCoroutine(HookGameOverWhenReady());
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= HandleGameOver;
            if (_fadeCo != null)
            {
                StopCoroutine(_fadeCo);
                _fadeCo = null;
            }
        }

        private IEnumerator HookGameOverWhenReady()
        {
            float deadline = Time.unscaledTime + 2f;
            while (GameManager.Instance == null && Time.unscaledTime < deadline) yield return null;
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += HandleGameOver;
        }

        private void HandleGameOver(int _)
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeOut(fadeOnGameOverSeconds));
        }

        private IEnumerator FadeOut(float seconds)
        {
            float start = _source.volume;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _source.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            _source.Stop();
            _source.volume = start; // restore for the next run (after ResetGame)
        }
    }
}
