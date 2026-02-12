using UnityEngine;
using BulletHell;

using Mediapipe;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

/// <summary>
/// Bridges MediaPipe Unity Plugin results into the existing Bullet Hell input system.
/// - Pose (body/face) drives plane movement toward the face position
/// - Right hand index fingertip controls turret aim via InputReader.Aim (screen position)
/// </summary>
public class MediaPipeInputBridge : MonoBehaviour
{
    [Header("Game Input")]
    [SerializeField] private InputReader inputReader;

    [Header("MediaPipe Runners (optional – auto-found if left empty)")]
    [SerializeField] private PoseLandmarkerRunner poseRunner;
    [SerializeField] private HandLandmarkerRunner handRunner;

    [Header("Movement Tuning")]
    [Tooltip("How strongly body lean affects horizontal movement. Smaller = more sensitive.")]
    [SerializeField] private float horizontalSensitivity = 0.2f;

    [Tooltip("How strongly body up/down affects vertical movement. Smaller = more sensitive.")]
    [SerializeField] private float verticalSensitivity = 0.2f;

    // MediaPipe landmark indices (see MediaPipe Pose & Hands docs)
    private const int PoseNose = 0;
    private const int PoseLeftShoulder = 11;
    private const int PoseRightShoulder = 12;
    private const int PoseLeftHip = 23;
    private const int PoseRightHip = 24;

    private const int HandIndexTip = 8;

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = FindObjectOfType<InputReader>();
        }

        if (poseRunner == null)
        {
            poseRunner = FindObjectOfType<PoseLandmarkerRunner>(true);
        }

        if (handRunner == null)
        {
            handRunner = FindObjectOfType<HandLandmarkerRunner>(true);
        }

        if (inputReader != null)
        {
            // Tell the rest of the game to use MediaPipe-sourced input vectors.
            inputReader.useMediaPipeInput = true;
        }
    }

    private void OnEnable()
    {
        if (poseRunner != null)
        {
            poseRunner.OnPoseResult += HandlePoseResult;
        }

        if (handRunner != null)
        {
            handRunner.OnHandResult += HandleHandResult;
        }
    }

    private void OnDisable()
    {
        if (poseRunner != null)
        {
            poseRunner.OnPoseResult -= HandlePoseResult;
        }

        if (handRunner != null)
        {
            handRunner.OnHandResult -= HandleHandResult;
        }
    }

    private void Update()
    {
        if (inputReader == null) return;

        // For now we don't control fire; can be added later via gesture.
        inputReader.SetMediaPipeFire(false);
    }

    private void HandlePoseResult(PoseLandmarkerResult result)
    {
        if (inputReader == null) return;

        // PoseLandmarkerResult is a struct; check the internal list instead
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            // No body detected – treat as no movement.
            inputReader.SetMediaPipeMove(Vector2.zero);
            return;
        }

        var landmarks = result.poseLandmarks[0];
        // NormalizedLandmarks is a struct that wraps a List<NormalizedLandmark>
        if (landmarks.landmarks == null || landmarks.landmarks.Count <= PoseRightHip)
        {
            inputReader.SetMediaPipeMove(Vector2.zero);
            return;
        }

        var leftShoulder = landmarks.landmarks[PoseLeftShoulder];
        var rightShoulder = landmarks.landmarks[PoseRightShoulder];
        var nose = landmarks.landmarks[PoseNose];

        // Center of upper body in normalized image space [0,1]
        float centerX = (leftShoulder.x + rightShoulder.x) * 0.5f;
        // Use face/nose Y so nodding or moving your head up/down maps to ship vertical movement.
        float faceY = nose.y;

        // Convert to normalized "screen-space" position (0..1 on each axis).
        // X is left (0) to right (1) of the image.
        float normX = Mathf.Clamp01(centerX);
        // MediaPipe Y grows downward; flip so 0 = bottom, 1 = top in game space.
        float normY = Mathf.Clamp01(1f - faceY);

        // Store face "target position" (0..1) in Move; the player controller
        // will convert this into a movement vector toward that position.
        var faceScreenPos = new Vector2(normX, normY);
        inputReader.SetMediaPipeMove(faceScreenPos);
    }

    private void HandleHandResult(HandLandmarkerResult result)
    {
        if (inputReader == null) return;

        // HandLandmarkerResult is a struct; check the internal list instead
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            // No hand – keep last aim; you can set zero here if you prefer.
            return;
        }

        // Use the first detected hand for aiming
        var handLandmarks = result.handLandmarks[0];
        if (handLandmarks.landmarks == null || handLandmarks.landmarks.Count <= HandIndexTip)
        {
            return;
        }

        var indexTip = handLandmarks.landmarks[HandIndexTip];

        // indexTip.X/Y are normalized [0,1] in image coordinates.
        float normX = indexTip.x;
        float normY = indexTip.y;

        float screenX = normX * Screen.width;
        float screenY = (1f - normY) * Screen.height; // flip Y to match Unity screen coords

        // Write raw aim (no smoothing) straight into the game input.
        var targetAim = new Vector2(screenX, screenY);
        inputReader.SetMediaPipeAim(targetAim);
    }
}
