using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Shows "Combo x{n}" while the multiplier is >= 2; hides itself otherwise.
    /// </summary>
    public class ComboBadgeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private GameObject root;

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

        private void UpdateBadge(int mult)
        {
            bool show = mult > 1;
            if (root != null) root.SetActive(show);
            if (show && label != null) label.text = $"Combo x{mult}";
        }
    }
}
