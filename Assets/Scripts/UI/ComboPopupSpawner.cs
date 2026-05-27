using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Listens to GameManager.OnHit and spawns a ComboPopup at the corresponding
    /// screen position (under a UI RectTransform anchor).
    /// </summary>
    public class ComboPopupSpawner : MonoBehaviour
    {
        [SerializeField] private ComboPopup popupPrefab;
        [SerializeField] private RectTransform layer;
        [SerializeField] private Camera gameCamera;
        // InkAmber (0.78, 0.62, 0.23) — dark enough to read on the cream
        // backdrop. The original pale yellow blended in.
        [SerializeField] private Color positiveColor = new Color(0.784f, 0.616f, 0.227f);
        [SerializeField] private Color negativeColor = new Color(0.78f, 0.20f, 0.20f);

        private void Awake()
        {
            if (gameCamera == null) gameCamera = Camera.main;
        }

        private void OnEnable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnHit += HandleHit;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnHit -= HandleHit;
        }

        private void HandleHit(int points, int mult, Vector3 worldPos)
        {
            if (popupPrefab == null || layer == null || gameCamera == null) return;

            string text;
            Color color;
            if (points < 0)
            {
                text = $"{points}!";
                color = negativeColor;
            }
            else if (mult <= 1)
            {
                text = $"+{points}";
                color = positiveColor;
            }
            else
            {
                text = $"+{points} x{mult}!";
                color = positiveColor;
            }

            Vector2 screen = gameCamera.WorldToScreenPoint(worldPos);
            // Convert screen point → local point inside the layer's RectTransform.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                layer, screen, layer.GetComponentInParent<Canvas>().worldCamera, out var local);

            var popup = Instantiate(popupPrefab, layer);
            popup.GetComponent<RectTransform>().anchoredPosition = local;
            popup.Initialize(text, color);
        }
    }
}
