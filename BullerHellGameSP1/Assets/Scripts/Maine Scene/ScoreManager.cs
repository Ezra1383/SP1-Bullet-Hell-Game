using UnityEngine;
using TMPro;

namespace BulletHell
{
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Score Settings")]
        [SerializeField] private int currentScore = 0;
        [SerializeField] private string scorePrefix = "Score: ";

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private Canvas canvas;

        [Header("Position & Style")]
        [SerializeField] private Vector2 anchorMin = new Vector2(1f, 1f);
        [SerializeField] private Vector2 anchorMax = new Vector2(1f, 1f);
        [SerializeField] private Vector2 anchoredPosition = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 sizeDelta = new Vector2(300f, 100f);
        [SerializeField] private float fontSize = 36f;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.TopRight;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (scoreText == null)
            {
                CreateScoreUI();
            }

            UpdateScoreDisplay();
        }

        void Start()
        {
            ApplyUISettings();
        }

        public void AddScore(int amount)
        {
            currentScore += amount;
            UpdateScoreDisplay();
        }

        public void SubtractScore(int amount)
        {
            currentScore -= amount;
            if (currentScore < 0) currentScore = 0;
            UpdateScoreDisplay();
        }

        public void SetScore(int newScore)
        {
            currentScore = newScore;
            UpdateScoreDisplay();
        }

        public void ResetScore()
        {
            currentScore = 0;
            UpdateScoreDisplay();
        }

        public int GetScore()
        {
            return currentScore;
        }

        private void UpdateScoreDisplay()
        {
            if (scoreText != null)
            {
                scoreText.text = scorePrefix + currentScore.ToString();
            }
        }

        private void CreateScoreUI()
        {
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("ScoreCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            GameObject textObj = new GameObject("ScoreText");
            textObj.transform.SetParent(canvas.transform, false);
            scoreText = textObj.AddComponent<TextMeshProUGUI>();
        }

        private void ApplyUISettings()
        {
            if (scoreText == null) return;

            RectTransform rectTransform = scoreText.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;

            scoreText.fontSize = fontSize;
            scoreText.color = textColor;
            scoreText.alignment = alignment;
        }

        public void UpdateUISettings()
        {
            ApplyUISettings();
            UpdateScoreDisplay();
        }
    }
}
