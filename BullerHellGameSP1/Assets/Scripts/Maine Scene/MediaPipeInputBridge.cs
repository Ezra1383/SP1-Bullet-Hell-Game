using UnityEngine;
using BulletHell;

using Mediapipe;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

/// <summary>
/// Bridges MediaPipe Unity Plugin results into the existing Bullet Hell input system.
/// Control mapping:
/// - Movement: Direct 1:1 mapping - plane position matches nose position on screen (X and Y).
/// - Aim: Left wrist → primary crosshair. Right wrist → secondary crosshair.
///
/// Position mapping:
/// - Nose X position (0=left, 1=right) → Plane X position (0=left, 1=right)
/// - Nose Y position (0=top, 1=bottom) → Plane Y position (1=top, 0=bottom, inverted)
/// The plane's screen position exactly follows where your nose is on the camera view.
/// </summary>
public class MediaPipeInputBridge : MonoBehaviour
{
    [Header("Game Input")]
    [SerializeField] private InputReader inputReader;

    [Header("MediaPipe Runners (optional – auto-found if left empty)")]
    [SerializeField] private PoseLandmarkerRunner poseRunner;

    // MediaPipe landmark indices (see MediaPipe Pose docs)
    private const int PoseNose = 0;
    private const int PoseLeftWrist = 15;
    private const int PoseRightWrist = 16;

    private static bool _hasWarnedNoPoseRunner;

    // Track previous nose position to calculate input velocity for tilting
    private Vector2 _previousNosePosition = new Vector2(0.5f, 0.5f);
    private bool _hasInitializedNosePosition = false;
    private float _cachedDeltaTime = 0.016f; // Cache deltaTime from main thread (default ~60fps)
    private float _cachedScreenWidth = 1920f;  // Cache screen dimensions from main thread
    private float _cachedScreenHeight = 1080f;

    private const string MovementTrackingKey = "UseMovementTracking";

    private void Awake()
    {
        if (PlayerPrefs.GetInt(MovementTrackingKey, 1) == 0)
        {
            enabled = false;
            return;
        }

        if (inputReader == null)
            inputReader = FindObjectOfType<InputReader>();

        if (poseRunner == null)
            poseRunner = FindObjectOfType<PoseLandmarkerRunner>(true);
        if (poseRunner == null && !_hasWarnedNoPoseRunner)
        {
            Debug.LogWarning("MediaPipeInputBridge: No PoseLandmarkerRunner found in scene. Movement and aim won't work. Add a Solution with Pose Landmark Detection.");
            _hasWarnedNoPoseRunner = true;
        }

        if (inputReader != null)
        {
            inputReader.useMediaPipeInput = true;
            // Default both aim points to screen center so they aren't stuck at (0,0) before wrist data arrives.
            inputReader.SetMediaPipeAim(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            inputReader.SetMediaPipeAim2(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
    }

    private void OnEnable()
    {
        if (poseRunner != null)
        {
            poseRunner.OnPoseResult += HandlePoseResult;
        }
    }

    private void OnDisable()
    {
        if (poseRunner != null)
        {
            poseRunner.OnPoseResult -= HandlePoseResult;
        }
    }

    private void Update()
    {
        if (inputReader == null) return;

        // Cache main-thread-only values for use in background callback
        _cachedDeltaTime = Time.deltaTime;
        _cachedScreenWidth = Screen.width;
        _cachedScreenHeight = Screen.height;

        // Fire is driven by WeaponSystem when using "fire only when on target" (aim ray hits enemy)
        inputReader.SetMediaPipeFire(false);
    }

    private void HandlePoseResult(PoseLandmarkerResult result)
    {
        if (inputReader == null) return;

        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            inputReader.SetMediaPipeMove(Vector2.zero);
            return;
        }

        var landmarks = result.poseLandmarks[0];
        if (landmarks.landmarks == null || landmarks.landmarks.Count <= PoseRightWrist)
        {
            inputReader.SetMediaPipeMove(Vector2.zero);
            return;
        }

        var nose = landmarks.landmarks[PoseNose];

        // Direct 1:1 mapping: plane position matches nose position on screen
        // MediaPipe coordinates: X: 0=left, 1=right; Y: 0=top, 1=bottom
        // Unity expects: X: 0=left, 1=right; Y: 0=bottom, 1=top (inverted)
        float normX = Mathf.Clamp01(nose.x);
        float normY = Mathf.Clamp01(1f - nose.y); // Invert Y: nose at top (0) → plane at top (1)

        // Calculate input velocity (change in nose position) for banking
        Vector2 currentNosePos = new Vector2(normX, normY);
        Vector2 inputVelocity = Vector2.zero;

        if (_hasInitializedNosePosition)
        {
            float dt = Mathf.Max(_cachedDeltaTime, 0.001f);
            inputVelocity = (currentNosePos - _previousNosePosition) / dt;
            // Scale so typical head movement gives roughly -1..1 for roll feel
            inputVelocity *= 2f;
        }
        else
        {
            _hasInitializedNosePosition = true;
        }

        _previousNosePosition = currentNosePos;

        inputReader.SetMediaPipeMove(new Vector2(normX, normY));
        inputReader.SetMediaPipeInputVelocity(inputVelocity);

        // Left wrist → primary aim (Crosshair / aimTarget)
        if (landmarks.landmarks.Count > PoseLeftWrist)
        {
            var leftWrist = landmarks.landmarks[PoseLeftWrist];
            float screenX = Mathf.Clamp01(leftWrist.x) * _cachedScreenWidth;
            float screenY = (1f - Mathf.Clamp01(leftWrist.y)) * _cachedScreenHeight;
            inputReader.SetMediaPipeAim(new Vector2(screenX, screenY));
        }
        else
        {
            SetAimToScreenCenter();
        }

        // Right wrist → secondary aim (Crosshair2 / aimTarget2)
        if (landmarks.landmarks.Count > PoseRightWrist)
        {
            var rightWrist = landmarks.landmarks[PoseRightWrist];
            float screenX2 = Mathf.Clamp01(rightWrist.x) * _cachedScreenWidth;
            float screenY2 = (1f - Mathf.Clamp01(rightWrist.y)) * _cachedScreenHeight;
            inputReader.SetMediaPipeAim2(new Vector2(screenX2, screenY2));
        }
        else
        {
            SetAim2ToScreenCenter();
        }
    }

    private void SetAimToScreenCenter()
    {
        if (inputReader != null)
            inputReader.SetMediaPipeAim(new Vector2(_cachedScreenWidth * 0.5f, _cachedScreenHeight * 0.5f));
    }

    private void SetAim2ToScreenCenter()
    {
        if (inputReader != null)
            inputReader.SetMediaPipeAim2(new Vector2(_cachedScreenWidth * 0.5f, _cachedScreenHeight * 0.5f));
    }
}
