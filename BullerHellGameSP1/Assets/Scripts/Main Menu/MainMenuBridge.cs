using UnityEngine;
using TMPro;

namespace BulletHell
{
    /// <summary>
    /// Game-specific additions to the SlimUI main menu.
    /// Attach to the same root GameObject as UIMenuManager.
    ///
    /// SETUP:
    /// 1. In Settings > Game panel, add a row with a button + TMP_Text label for "Movement Tracking".
    /// 2. Wire the button OnClick -> MainMenuBridge.ToggleMovementTracking().
    /// 3. Assign that TMP_Text label to movementTrackingText below.
    /// </summary>
    public class MainMenuBridge : MonoBehaviour
    {
        private const string MovementTrackingKey = "UseMovementTracking";

        [Header("Movement Tracking (Settings > Game panel)")]
        [Tooltip("The TMP_Text label that displays 'on' or 'off' next to the Movement Tracking button.")]
        [SerializeField] private TMP_Text movementTrackingText;

        private void Start()
        {
            RefreshText();
        }

        /// <summary>Wire this to the Movement Tracking button's OnClick in the Settings > Game panel.</summary>
        public void ToggleMovementTracking()
        {
            int next = PlayerPrefs.GetInt(MovementTrackingKey, 1) == 1 ? 0 : 1;
            PlayerPrefs.SetInt(MovementTrackingKey, next);
            RefreshText();
        }

        private void RefreshText()
        {
            if (movementTrackingText == null) return;
            movementTrackingText.text = PlayerPrefs.GetInt(MovementTrackingKey, 1) == 1 ? "on" : "off";
        }
    }
}
