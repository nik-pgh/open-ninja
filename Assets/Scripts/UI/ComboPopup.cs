using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Self-animating UI floater. Initialize() sets the text + color, then it
    /// moves up and fades out over `lifetime` seconds and destroys itself.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ComboPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float lifetime = 0.8f;
        [SerializeField] private float floatPixels = 60f;

        private RectTransform _rt;
        private Vector2 _startAnchored;
        private float _elapsed;
        private Color _baseColor;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            if (label != null) _baseColor = label.color;
        }

        public void Initialize(string text, Color color)
        {
            if (label != null)
            {
                label.text = text;
                label.color = color;
                _baseColor = color;
            }
            _startAnchored = _rt.anchoredPosition;
            _elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime; // outlive scene Time.timeScale freezes
            float t = Mathf.Clamp01(_elapsed / lifetime);
            _rt.anchoredPosition = _startAnchored + new Vector2(0f, floatPixels * t);
            if (label != null)
            {
                var c = _baseColor;
                c.a = 1f - t;
                label.color = c;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
