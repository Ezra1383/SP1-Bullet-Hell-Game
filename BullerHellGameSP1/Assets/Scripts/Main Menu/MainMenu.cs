using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Toggle movementTrackingToggle;

    private const string MovementTrackingKey = "UseMovementTracking";

    private void Start()
    {
        if (movementTrackingToggle != null)
            movementTrackingToggle.isOn = PlayerPrefs.GetInt(MovementTrackingKey, 1) == 1;
    }

    public void PlayGame()
    {
        if (movementTrackingToggle != null)
            PlayerPrefs.SetInt(MovementTrackingKey, movementTrackingToggle.isOn ? 1 : 0);

        SceneManager.LoadSceneAsync("Main Scene");
    }
}
