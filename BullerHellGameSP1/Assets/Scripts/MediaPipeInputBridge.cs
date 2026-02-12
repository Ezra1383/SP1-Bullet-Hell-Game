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
/// - Pose (body) controls plane movement via InputReader.Move
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

    [Tooltip("Lerp factor for smoothing movement (0 = no smoothing, 1 = instant).")]
    [Range(0f, 1f)]
    [SerializeField] private float moveSmoothing = 0.2f;

    [Header("Aim Tuning")]
    [Tooltip("Lerp factor for smoothing aim (0 = no smoothing, 1 = instant).")]
    [Range(0f, 1f)]
    [SerializeField] private float aimSmoothing = 0.2f;

    private Vector2 _currentMove;
    private Vector2 _currentAim;

    // MediaPipe landmark indices (see MediaPipe Pose & Hands docs)
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

        // Push the latest smoothed values into InputReader each frame
        inputReader.SetMediaPipeMove(_currentMove);
        inputReader.SetMediaPipeAim(_currentAim);
        // For now we don't control fire; can be added later via gesture.
        inputReader.SetMediaPipeFire(false);
    }

    private void HandlePoseResult(PoseLandmarkerResult result)
    {
        // PoseLandmarkerResult is a struct; check the internal list instead
        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            _currentMove = Vector2.Lerp(_currentMove, Vector2.zero, moveSmoothing);
            return;
        }

        var landmarks = result.poseLandmarks[0];
        // NormalizedLandmarks is a struct that wraps a List<NormalizedLandmark>
        if (landmarks.landmarks == null || landmarks.landmarks.Count <= PoseRightHip)
        {
            _currentMove = Vector2.Lerp(_currentMove, Vector2.zero, moveSmoothing);
            return;
        }

        var leftShoulder = landmarks.landmarks[PoseLeftShoulder];
        var rightShoulder = landmarks.landmarks[PoseRightShoulder];
        var leftHip = landmarks.landmarks[PoseLeftHip];
        var rightHip = landmarks.landmarks[PoseRightHip];

        // Center of upper body in normalized image space [0,1]
        float centerX = (leftShoulder.x + rightShoulder.x) * 0.5f;
        float hipY = (leftHip.y + rightHip.y) * 0.5f;

        // Map normalized to -1..1 with tunable sensitivity
        float moveX = Mathf.Clamp((centerX - 0.5f) / Mathf.Max(0.0001f, horizontalSensitivity), -1f, 1f);
        float moveY = Mathf.Clamp((0.5f - hipY) / Mathf.Max(0.0001f, verticalSensitivity), -1f, 1f);

        var targetMove = new Vector2(moveX, moveY);
        _currentMove = Vector2.Lerp(_currentMove, targetMove, moveSmoothing > 0f ? moveSmoothing : 1f);
    }

    private void HandleHandResult(HandLandmarkerResult result)
    {
        // HandLandmarkerResult is a struct; check the internal list instead
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
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

        var targetAim = new Vector2(screenX, screenY);
        _currentAim = Vector2.Lerp(_currentAim, targetAim, aimSmoothing > 0f ? aimSmoothing : 1f);
    }
}

