using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OpenNinja
{
    /// <summary>
    /// Root MonoBehaviour on the StartScene canvas. Reads PlayerSession state
    /// into the UI, gates the Start button on a non-empty nickname, and
    /// transitions to MainScene on click.
    /// </summary>
    public class StartScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bestScoreLabel;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button startButton;
        [SerializeField] private CubeInfoTable infoTable;
        [SerializeField] private string gameTitle = "MATERIAL NINJA";
        [SerializeField] private string mainSceneName = "MainScene";

        private void Awake()
        {
            if (titleLabel != null) titleLabel.text = gameTitle;
            if (bestScoreLabel != null) bestScoreLabel.text = $"Best: {PlayerSession.BestScore}";
            if (nicknameInput != null)
            {
                nicknameInput.text = PlayerSession.Nickname;
                nicknameInput.onValueChanged.AddListener(OnNicknameChanged);
            }
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
                UpdateStartButtonInteractable();
            }
        }

        private void Start()
        {
            if (infoTable != null) infoTable.Populate();
        }

        private void OnNicknameChanged(string _) => UpdateStartButtonInteractable();

        private void UpdateStartButtonInteractable()
        {
            if (startButton == null) return;
            startButton.interactable =
                nicknameInput != null && !string.IsNullOrWhiteSpace(nicknameInput.text);
        }

        private void OnStartClicked()
        {
            if (nicknameInput != null)
                PlayerSession.Nickname = nicknameInput.text.Trim();
            SceneManager.LoadScene(mainSceneName);
        }
    }
}
