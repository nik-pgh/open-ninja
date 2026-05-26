using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// Lights the first N heart images based on current lives. Hearts are assigned
    /// in the Inspector in order (left-to-right).
    /// </summary>
    public class LivesView : MonoBehaviour
    {
        [SerializeField] private Image[] hearts;
        [SerializeField] private Color aliveColor = Color.white;
        [SerializeField] private Color lostColor = new Color(1f, 1f, 1f, 0.2f);

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnLivesChanged += UpdateLives;
            UpdateLives(GameManager.Instance.Lives);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnLivesChanged -= UpdateLives;
        }

        private void UpdateLives(int lives)
        {
            if (hearts == null) return;
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                hearts[i].color = i < lives ? aliveColor : lostColor;
            }
        }
    }
}
