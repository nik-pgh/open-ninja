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
