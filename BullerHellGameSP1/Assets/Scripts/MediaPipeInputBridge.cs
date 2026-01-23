using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.HandLandmarker;
// Add your Sample namespaces if needed, e.g.:
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

namespace BulletHell
{
    public class MediaPipeInputBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private PoseLandmarkerRunner poseRunner;
        [SerializeField] private HandLandmarkerRunner handRunner;

        [Header("Movement Calibration (Pose)")]
        [SerializeField] private float movementSensitivity = 2.0f;
        [SerializeField] private float deadzone = 0.05f; // How much you can wiggle before the jet moves

        [Header("Aiming Calibration (Hand)")]
        [SerializeField] private float aimSensitivity = 1.5f;
        [SerializeField] private float pinchThreshold = 0.05f; // Distance for "Click"

        // Local state
        private Vector2 _neutralChestPos = new Vector2(0.5f, 0.5f); // Start assuming center is 0.5
        private bool _isCalibrated = false;

        private void Start()
        {
            // Auto-enable the MediaPipe override
            if (inputReader != null)
                inputReader.useMediaPipeInput = true;
        }

        private void OnEnable()
        {
            if (poseRunner != null) poseRunner.OnPoseResult += HandlePose;
            if (handRunner != null) handRunner.OnHandResult += HandleHand;
        }

        private void OnDisable()
        {
            if (poseRunner != null) poseRunner.OnPoseResult -= HandlePose;
            if (handRunner != null) handRunner.OnHandResult -= HandleHand;
        }

        // --- HANDLER: POSE (Movement) ---
        private void HandlePose(PoseLandmarkerResult result)
        {
            if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
            {
                inputReader.SetMediaPipeMove(Vector2.zero);
                return;
            }

            // Get landmarks (11 = Left Shoulder, 12 = Right Shoulder)
            // Note: Lists contain NormalizedLandmarks usually
            var landmarks = result.poseLandmarks[0];
            var leftShoulder = landmarks[11];
            var rightShoulder = landmarks[12];

            // 1. Calculate Chest Center
            float chestX = (leftShoulder.x + rightShoulder.x) / 2f;
            float chestY = (leftShoulder.y + rightShoulder.y) / 2f;

            // Optional: Auto-calibrate on first frame or via button
            if (!_isCalibrated)
            {
                _neutralChestPos = new Vector2(chestX, chestY);
                _isCalibrated = true;
            }

            // 2. Calculate Deviation from Neutral
            // Note: MediaPipe X is 0(left)->1(right). Y is 0(top)->1(bottom).
            // Unity Input is -1(left)->1(right). Y is -1(down)->1(up).

            float diffX = (chestX - _neutralChestPos.x);
            float diffY = (_neutralChestPos.y - chestY); // Invert Y for game feel (Lean forward = Up)

            // 3. Apply Deadzone & Sensitivity
            if (Mathf.Abs(diffX) < deadzone) diffX = 0;
            if (Mathf.Abs(diffY) < deadzone) diffY = 0;

            Vector2 moveSignal = new Vector2(diffX, diffY) * movementSensitivity;

            // Clamp to -1 to 1 for standard input behavior
            moveSignal.x = Mathf.Clamp(moveSignal.x, -1f, 1f);
            moveSignal.y = Mathf.Clamp(moveSignal.y, -1f, 1f);

            // Send to InputReader
            inputReader.SetMediaPipeMove(moveSignal);
        }

        // --- HANDLER: HAND (Aiming & Fire) ---
        private void HandleHand(HandLandmarkerResult result)
        {
            if (result.handLandmarks == null || result.handLandmarks.Count == 0)
            {
                inputReader.SetMediaPipeFire(false);
                return;
            }

            // Assume Player uses the first detected hand for aiming
            var landmarks = result.handLandmarks[0];
            var indexTip = landmarks[8];
            var thumbTip = landmarks[4];

            // 1. Aiming (Map 0-1 coords to Screen/Game Aim)
            // In Unity Input System, "Aim" usually expects Screen Coordinates or Delta.
            // Since your PlayerController does: Ray ray = mainCamera.ScreenPointToRay(mousePos);
            // We need to provide "Virtual Mouse Coordinates".

            float screenX = indexTip.x * Screen.width;
            float screenY = (1f - indexTip.y) * Screen.height; // Invert Y for Screen Space

            // NOTE: MediaPipe camera is mirrored? You might need (1f - indexTip.x) depending on setup.

            inputReader.SetMediaPipeAim(new Vector2(screenX, screenY));

            // 2. Firing (Pinch Detection)
            float pinchDist = Vector2.Distance(new Vector2(indexTip.x, indexTip.y), new Vector2(thumbTip.x, thumbTip.y));
            bool isPinching = pinchDist < pinchThreshold;

            inputReader.SetMediaPipeFire(isPinching);
        }
    }
}