using TMPro;
using UnityEngine;

namespace BulletHell
{
    /// <summary>
    /// MM:SS survival timer displayed in the HUD. Stops automatically on game over.
    ///
    /// SETUP:
    /// 1. Add a TextMeshProUGUI to your HUD Canvas at the top-left:
    ///    anchorMin (0,1), anchorMax (0,1), pivot (0,1), anchoredPosition (10, -10).
    /// 2. Assign the TMP reference to timerText in the Inspector.
    /// 3. Attach this script to any active GameObject in the scene.
    /// </summary>
    public class SurvivalTimer : MonoBehaviour
    {
        public static SurvivalTimer Instance { get; private set; }

        [SerializeField] private TextMeshProUGUI timerText;

        private float elapsedTime;
        private bool  isRunning = true;

        public float ElapsedTime => elapsedTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            GameOverScreen.OnGameOver += Stop;
        }

        private void OnDestroy()
        {
            GameOverScreen.OnGameOver -= Stop;
        }

        private void Update()
        {
            if (!isRunning) return;

            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (timerText == null) return;
            int minutes = (int)(elapsedTime / 60f);
            int seconds = (int)(elapsedTime % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        public void Stop()
        {
            isRunning = false;
        }

        public void ResetTimer()
        {
            elapsedTime = 0f;
            isRunning   = true;
            UpdateDisplay();
        }

        /// <summary>Returns formatted "MM:SS" string for use on game over screen.</summary>
        public string GetFormattedTime()
        {
            int minutes = (int)(elapsedTime / 60f);
            int seconds = (int)(elapsedTime % 60f);
            return $"{minutes:00}:{seconds:00}";
        }
    }
}
