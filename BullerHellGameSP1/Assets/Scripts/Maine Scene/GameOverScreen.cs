using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace BulletHell
{
    public class GameOverScreen : MonoBehaviour
    {
        public static GameOverScreen Instance { get; private set; }

        [Header("Panel")]
        [Tooltip("Root panel GameObject to show/hide.")]
        [SerializeField] private GameObject panel;

        [Header("Text")]
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private string   gameOverMessage = "You Suck";

        [Header("Retry Button")]
        [SerializeField] private Button   retryButton;
        [SerializeField] private TMP_Text retryButtonText;
        [SerializeField] private string   retryButtonLabel = "Retry";

        [Header("Scene")]
        [Tooltip("Exact name of the scene to load on retry (must be in Build Settings).")]
        [SerializeField] private string sceneToLoad = "MainScene";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (panel != null) panel.SetActive(false);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        }

        public void Show()
        {
            Time.timeScale = 0f;

            if (panel        != null) panel.SetActive(true);
            if (gameOverText != null) gameOverText.text   = gameOverMessage;
            if (retryButtonText != null) retryButtonText.text = retryButtonLabel;
        }

        public void OnRetry()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneToLoad);
        }
    }
}
