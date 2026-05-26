using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// Lights the first N heart graphics based on current lives. Hearts are assigned
    /// in the Inspector in order (left-to-right). Accepts any Graphic (Image or
    /// TMP_Text) so callers can use sprite hearts or unicode hearts interchangeably.
    /// </summary>
    public class LivesView : MonoBehaviour
    {
        [SerializeField] private Graphic[] hearts;
        [SerializeField] private Color aliveColor = new Color(1f, 0.25f, 0.3f, 1f);
        [SerializeField] private Color lostColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

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
