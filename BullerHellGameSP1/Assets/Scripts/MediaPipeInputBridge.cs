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
/// - Aim: Right wrist from pose detection (fallback to left wrist if right not available).
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

    private void Awake()
    {
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
            // Default aim to screen center so AimPoint isn't stuck at (0,0) when no wrist data yet.
            inputReader.SetMediaPipeAim(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
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

        // Cache deltaTime from main thread for use in background callback
        _cachedDeltaTime = Time.deltaTime;

        // For now we don't control fire; can be added later via gesture.
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

        // Calculate input velocity (change in nose position) for tilting
        Vector2 currentNosePos = new Vector2(normX, normY);
        Vector2 inputVelocity = Vector2.zero;

        if (_hasInitializedNosePosition)
        {
            // Calculate delta position (change from last frame)
            // Use cached deltaTime since this callback runs on background thread
            inputVelocity = (currentNosePos - _previousNosePosition) / _cachedDeltaTime;
            // Normalize to roughly -1 to 1 range (assuming typical head movement speed)
            inputVelocity *= 2f; // Scale factor to match typical input range
        }
        else
        {
            _hasInitializedNosePosition = true;
        }

        _previousNosePosition = currentNosePos;

        inputReader.SetMediaPipeMove(new Vector2(normX, normY));
        inputReader.SetMediaPipeInputVelocity(inputVelocity);

        // Aiming using wrist position (prefer right wrist, fallback to left)
        if (landmarks.landmarks.Count > PoseRightWrist)
        {
            var rightWrist = landmarks.landmarks[PoseRightWrist];
            var leftWrist = landmarks.landmarks[PoseLeftWrist];

            // Check which wrist is more visible (higher visibility score means more confident detection)
            bool useRightWrist = rightWrist.visibility > leftWrist.visibility;
            var wrist = useRightWrist ? rightWrist : leftWrist;

            // Convert normalized wrist position to screen coordinates
            float wristNormX = Mathf.Clamp01(wrist.x);
            float wristNormY = Mathf.Clamp01(wrist.y);

            float screenX = wristNormX * Screen.width;
            float screenY = (1f - wristNormY) * Screen.height; // Flip Y to match Unity screen coords

            inputReader.SetMediaPipeAim(new Vector2(screenX, screenY));
            Debug.Log($"[MediaPipe] Nose: ({nose.x:F3}, {nose.y:F3}) → Move: ({normX:F3}, {normY:F3}), Aim: ({screenX:F1}, {screenY:F1}) [{(useRightWrist ? "R" : "L")} wrist]");
        }
        else
        {
            SetAimToScreenCenter();
            Debug.Log($"[MediaPipe] Nose: ({nose.x:F3}, {nose.y:F3}) → Move: ({normX:F3}, {normY:F3}), Aim: center (no wrist)");
        }
    }

    private void SetAimToScreenCenter()
    {
        if (inputReader != null)
            inputReader.SetMediaPipeAim(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }
}
