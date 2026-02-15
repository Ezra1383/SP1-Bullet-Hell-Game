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
/// Control mapping:
/// - Depth (pose world Z): far = up, near = down (vertical movement).
/// - Position (body, not hand): horizontal pose center drives roll (left = roll left, right = roll right).
/// - Aim: right hand only, using wrist (root node) as aim point.
///
/// Depth in MediaPipe Unity: there is no separate "depth detection" API. Depth comes from landmark Z:
/// - Pose: poseWorldLandmarks give 3D world coords in meters (origin at hip); smaller Z = closer to camera.
/// - Hand: normalized hand landmarks have Z relative to wrist (not metric depth).
/// For vertical we use pose world Z (e.g. nose) so lean forward = down, lean back = up.
///
/// Pose vs face for aiming: use hand (wrist/root) for aim; pose/face are better for body position and gaze.
/// </summary>
public class MediaPipeInputBridge : MonoBehaviour
{
    [Header("Game Input")]
    [SerializeField] private InputReader inputReader;

    [Header("MediaPipe Runners (optional – auto-found if left empty)")]
    [SerializeField] private PoseLandmarkerRunner poseRunner;
    [SerializeField] private HandLandmarkerRunner handRunner;

    [Header("Movement Tuning")]
    [Tooltip("Horizontal: body center X (0=left, 1=right). Also drives roll (position left = roll left).")]
    [SerializeField] private float horizontalSensitivity = 0.2f;

    [Tooltip("Depth → vertical: pose world Z. Far (larger Z) = up, near (smaller Z) = down. Neutral Z for center.")]
    [SerializeField] private float depthNeutralZ = 0f;
    [Tooltip("Scale for depth→vertical. Pose Z is in meters (~-0.2 to 0.2); higher = more response.")]
    [SerializeField] private float depthToVerticalScale = 8f;

    // MediaPipe landmark indices (see MediaPipe Pose & Hands docs)
    private const int PoseNose = 0;
    private const int PoseLeftShoulder = 11;
    private const int PoseRightShoulder = 12;
    private const int PoseLeftHip = 23;
    private const int PoseRightHip = 24;

    /// <summary>Hand root node (wrist) for aiming; index 0 in hand landmarks.</summary>
    private const int HandWrist = 0;

    private static bool _hasWarnedNoHandRunner;
    private static bool _hasWarnedNoPoseRunner;

    private void Awake()
    {
        if (inputReader == null)
            inputReader = FindObjectOfType<InputReader>();

        if (poseRunner == null)
            poseRunner = FindObjectOfType<PoseLandmarkerRunner>(true);
        if (poseRunner == null && !_hasWarnedNoPoseRunner)
        {
            Debug.LogWarning("MediaPipeInputBridge: No PoseLandmarkerRunner found in scene. Movement/depth won't work. Add a Solution with Pose Landmark Detection.");
            _hasWarnedNoPoseRunner = true;
        }

        if (handRunner == null)
            handRunner = FindObjectOfType<HandLandmarkerRunner>(true);
        if (handRunner == null && !_hasWarnedNoHandRunner)
        {
            Debug.LogWarning("MediaPipeInputBridge: No HandLandmarkerRunner found in scene. Aim won't follow hand. Add a Solution with Hand Landmark Detection (or a scene that runs both Pose + Hand).");
            _hasWarnedNoHandRunner = true;
        }

        if (inputReader != null)
        {
            inputReader.useMediaPipeInput = true;
            // Default aim to screen center so AimPoint isn't stuck at (0,0) when no hand data yet.
            inputReader.SetMediaPipeAim(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
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

        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            inputReader.SetMediaPipeMove(Vector2.zero);
            return;
        }

        var landmarks = result.poseLandmarks[0];
        if (landmarks.landmarks == null || landmarks.landmarks.Count <= PoseRightHip)
        {
            inputReader.SetMediaPipeMove(Vector2.zero);
            return;
        }

        var leftShoulder = landmarks.landmarks[PoseLeftShoulder];
        var rightShoulder = landmarks.landmarks[PoseRightShoulder];
        var nose = landmarks.landmarks[PoseNose];

        // Position (body, not hand): center X for horizontal and roll (left = roll left, right = roll right).
        float centerX = (leftShoulder.x + rightShoulder.x) * 0.5f;
        float normX = Mathf.Clamp01(centerX);

        // Depth → vertical: far = up, near = down. Use pose world Z when available.
        float normY = 0.5f;
        bool usedWorldZ = false;
        if (result.poseWorldLandmarks != null && result.poseWorldLandmarks.Count > 0)
        {
            var worldList = result.poseWorldLandmarks[0];
            if (worldList.landmarks != null && worldList.landmarks.Count > PoseNose)
            {
                // Pose world: smaller Z = closer (near), larger Z = farther (far). Z is in meters.
                float noseZ = worldList.landmarks[PoseNose].z;
                normY = 0.5f + depthToVerticalScale * (noseZ - depthNeutralZ);
                normY = Mathf.Clamp01(normY);
                usedWorldZ = true;
            }
        }
        if (!usedWorldZ)
        {
            // Fallback: nose Y in image — head higher in frame = move up.
            normY = Mathf.Clamp01(1f - nose.y);
        }

        inputReader.SetMediaPipeMove(new Vector2(normX, normY));
    }

    private void HandleHandResult(HandLandmarkerResult result)
    {
        if (inputReader == null) return;

        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            SetAimToScreenCenter();
            return;
        }

        // Prefer right hand for aim; fallback to left so either hand works.
        int aimHandIndex = -1;
        if (result.handedness != null && result.handedness.Count == result.handLandmarks.Count)
        {
            for (int i = 0; i < result.handedness.Count; i++)
            {
                if (result.handedness[i].categories == null || result.handedness[i].categories.Count == 0) continue;
                string label = result.handedness[i].categories[0].categoryName;
                if (string.Equals(label, "Right", System.StringComparison.OrdinalIgnoreCase))
                {
                    aimHandIndex = i;
                    break;
                }
            }
            if (aimHandIndex < 0)
            {
                for (int i = 0; i < result.handedness.Count; i++)
                {
                    if (result.handedness[i].categories == null || result.handedness[i].categories.Count == 0) continue;
                    string label = result.handedness[i].categories[0].categoryName;
                    if (string.Equals(label, "Left", System.StringComparison.OrdinalIgnoreCase))
                    {
                        aimHandIndex = i;
                        break;
                    }
                }
            }
        }
        if (aimHandIndex < 0)
            aimHandIndex = 0;

        var handLandmarks = result.handLandmarks[aimHandIndex];
        if (handLandmarks.landmarks == null || handLandmarks.landmarks.Count <= HandWrist)
        {
            SetAimToScreenCenter();
            return;
        }

        var wrist = handLandmarks.landmarks[HandWrist];
        float normX = Mathf.Clamp01(wrist.x);
        float normY = Mathf.Clamp01(wrist.y);

        float screenX = normX * Screen.width;
        float screenY = (1f - normY) * Screen.height; // flip Y to match Unity screen coords

        inputReader.SetMediaPipeAim(new Vector2(screenX, screenY));
    }

    private void SetAimToScreenCenter()
    {
        if (inputReader != null)
            inputReader.SetMediaPipeAim(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }
}
