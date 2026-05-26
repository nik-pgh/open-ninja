using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// Shows "Combo x{n}" while the multiplier is >= 2; hides itself otherwise.
    /// If a timerFill is assigned, also drives its horizontal scale to visualize
    /// the remaining combo window each frame.
    /// </summary>
    public class ComboBadgeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject root;
        [SerializeField] private Image timerFill; // optional; type "Filled" with horizontal fill

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnComboChanged += UpdateBadge;
            UpdateBadge(GameManager.Instance.ComboMultiplier);
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnComboChanged -= UpdateBadge;
        }

        private void Update()
        {
            if (timerFill == null) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.ComboMultiplier <= 1 || gm.ComboWindowSeconds <= 0f)
            {
                timerFill.fillAmount = 0f;
                return;
            }
            timerFill.fillAmount = Mathf.Clamp01(gm.ComboTimeRemaining / gm.ComboWindowSeconds);
        }

        private void UpdateBadge(int mult)
        {
            bool show = mult > 1;
            if (root != null) root.SetActive(show);
            if (show && label != null) label.text = $"Combo x{mult}";
        }
    }
}
