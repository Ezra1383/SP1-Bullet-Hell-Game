using DG.Tweening;
using UnityEngine;

namespace BulletHell
{
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform playerModel;

        [Header("Movement Settings")]
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private Vector2 movementLimit = new Vector2(2f, 2f);
        [SerializeField] private float movementSpeed = 10f;
        [SerializeField] private float smoothTime = 0.2f;
        [SerializeField] private float movementRange = 5f;

        [Header("Rotation Settings")]
        [SerializeField] private float maxRoll = 15f;
        [SerializeField] private float maxPitch = 10f;
        [SerializeField] private float rollSpeed = 5f;
        [SerializeField] private float pitchSpeed = 5f;

        private Vector3 velocity;
        private float roll;
        private float pitch;

        private void Awake()
        {
            if (input != null)
            {
                input.leftTap += OnLeftTap;
                input.rightTap += OnRightTap;
            }
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.leftTap -= OnLeftTap;
                input.rightTap -= OnRightTap;
            }
        }

        private void OnLeftTap()
        {
            BarrelRoll(-1);
        }

        private void OnRightTap()
        {
            BarrelRoll(1);
        }

        private void BarrelRoll(int direction)
        {
            if (playerModel == null)
            {
                Debug.LogWarning("Player model not assigned!");
                return;
            }

            if (!DOTween.IsTweening(playerModel))
            {
                playerModel.DOLocalRotate(
                    new Vector3(
                        playerModel.localEulerAngles.x,
                        playerModel.localEulerAngles.y,
                        360f * direction),
                    0.5f,
                    RotateMode.LocalAxisAdd
                ).SetEase(Ease.OutCubic);
            }
        }

        private void Update()
        {
            if (followTarget == null || input == null) return;

            // 1. Target position behind the followTarget
            Vector3 targetPos = followTarget.position - followTarget.forward * followDistance;

            // 2. Smooth movement toward target
            Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

            // 3. Convert to local space of the followTarget
            Vector3 localPos = followTarget.InverseTransformPoint(smoothedPos);

            // 4. Apply player input offset
            localPos.x += input.Move.x * movementSpeed * Time.deltaTime * movementRange;
            localPos.y += input.Move.y * movementSpeed * Time.deltaTime * movementRange;

            // 5. Clamp within movement limits
            localPos.x = Mathf.Clamp(localPos.x, -movementLimit.x, movementLimit.x);
            localPos.y = Mathf.Clamp(localPos.y, -movementLimit.y, movementLimit.y);

            // 6. Convert back to world space
            transform.position = followTarget.TransformPoint(localPos);

            // 7. Rotate to face the followTarget forward
            Vector3 forwardDir = followTarget.forward;
            forwardDir.y = 0f; // Keep player upright

            if (forwardDir.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                // 8. Apply roll based on horizontal input (lean left/right)
                roll = Mathf.Lerp(roll, -input.Move.x * maxRoll, Time.deltaTime * rollSpeed);

                // 9. Apply pitch based on vertical input (tilt forward/backward)
                pitch = Mathf.Lerp(pitch, -input.Move.y * maxPitch, Time.deltaTime * pitchSpeed);

                // Combine rotation
                targetRotation *= Quaternion.Euler(pitch, 0f, roll);
                transform.rotation = targetRotation;
            }
        }
    }
}