using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Reads PlayerSession.BestScore into a label on enable and refreshes if it
    /// gets bumped during the run (happens on game over via TrySetBestScore).
    /// </summary>
    public class BestScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        // Just the number — the panel's "BEST" label already provides context.
        [SerializeField] private string format = "{0}";

        private void OnEnable()
        {
            Refresh(PlayerSession.BestScore);
            PlayerSession.OnBestScoreChanged += Refresh;
        }

        private void OnDisable()
        {
            PlayerSession.OnBestScoreChanged -= Refresh;
        }

        private void Refresh(int best)
        {
            if (label != null) label.text = string.Format(format, best);
        }
    }
}
