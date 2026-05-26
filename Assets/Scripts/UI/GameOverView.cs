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
            foreach (var cube in FindObjectsByType<Cube>(FindObjectsInactive.Exclude))
            {
                Destroy(cube.gameObject);
            }
        }
    }
}
