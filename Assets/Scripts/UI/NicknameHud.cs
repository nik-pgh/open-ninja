using TMPro;
using UnityEngine;

namespace OpenNinja
{
    /// <summary>
    /// Small label in MainScene that shows the current player's nickname.
    /// Hides itself if no nickname is set (so a player who launches MainScene
    /// directly without going through StartScene sees a clean HUD).
    /// </summary>
    public class NicknameHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private string format = "Player: {0}";

        private void OnEnable()
        {
            if (label == null) return;
            string nick = PlayerSession.Nickname;
            if (string.IsNullOrEmpty(nick))
            {
                gameObject.SetActive(false);
                return;
            }
            label.text = string.Format(format, nick);
        }
    }
}
