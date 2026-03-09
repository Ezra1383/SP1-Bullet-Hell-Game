using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BulletHell
{
    /// <summary>
    /// Pause menu toggled with ESC. Will not open during game over.
    ///
    /// SETUP:
    /// 1. Create a new Canvas (sortingOrder 100, Screen Space Overlay).
    /// 2. Add a child GameObject "PausePanel":
    ///    - Add a CanvasGroup component to it.
    ///    - Add a full-screen semi-transparent black Image child (alpha ~0.85, raycastTarget true).
    ///    - Add a vertical layout group with 4 Buttons: Resume, New Game, Main Menu, Quit.
    /// 3. Add this script to the Canvas (or any always-active GameObject).
    /// 4. Assign pausePanel, panelCanvasGroup, and the three buttons in the Inspector.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsPaused { get; private set; }

        [Header("Panel")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Scene")]
        [SerializeField] private string mainMenuScene = "Main Menu";
        [SerializeField] private string gameScene = "Main Scene";

        [Header("Animation")]
        [SerializeField] private float fadeInDuration  = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.15f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (pausePanel != null) pausePanel.SetActive(false);

            if (resumeButton   != null) resumeButton.onClick.AddListener(Resume);
            if (newGameButton  != null) newGameButton.onClick.AddListener(NewGame);
            if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
            if (quitButton     != null) quitButton.onClick.AddListener(QuitGame);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Don't allow pause while game over screen is showing
                bool gameOverVisible = GameOverScreen.Instance != null && GameOverScreen.Instance.IsVisible;
                if (!gameOverVisible)
                    TogglePause();
            }
        }

        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (pausePanel == null) return;

            IsPaused = true;
            pausePanel.SetActive(true);
            Time.timeScale = 0f;

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
            }
        }

        public void Resume()
        {
            if (pausePanel == null) { FinishResume(); return; }

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.DOFade(0f, fadeOutDuration)
                    .SetUpdate(true)
                    .OnComplete(FinishResume);
            }
            else
            {
                FinishResume();
            }
        }

        private void FinishResume()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        public void NewGame()
        {
            Time.timeScale = 1f;
            IsPaused = false;
            SceneManager.LoadScene(gameScene);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            IsPaused = false;
            SceneManager.LoadScene(mainMenuScene);
        }

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
