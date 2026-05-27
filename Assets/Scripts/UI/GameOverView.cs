using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private GameObject dimOverlay;
        [SerializeField] private TMP_Text finalScoreLabel;
        [SerializeField] private TMP_Text bestScoreLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject newBestFlag;
        [SerializeField] private CubeSpawner spawner;

        private int _bestAtSceneStart;

        private void Awake()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (dimOverlay != null) dimOverlay.SetActive(false);
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
            if (dimOverlay != null) dimOverlay.SetActive(true);
            if (finalScoreLabel != null) finalScoreLabel.text = $"Score: {finalScore}";
            // PlayerSession.BestScore is already up-to-date here since
            // GameManager.SetGameOver wrote it before firing OnGameOver.
            if (bestScoreLabel != null) bestScoreLabel.text = $"Best: {PlayerSession.BestScore}";
            if (newBestFlag != null) newBestFlag.SetActive(finalScore > _bestAtSceneStart);
        }

        private void OnRestartClicked()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            if (dimOverlay != null) dimOverlay.SetActive(false);
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
