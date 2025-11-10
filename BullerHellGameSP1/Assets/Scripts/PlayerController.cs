using DG.Tweening;
using UnityEngine;

namespace BulletHell
{
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private Transform followTarget;
        [SerializeField] Transform aimTarget;

        [SerializeField] private Transform playerModel;

        [Header("Movement Settings")]
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private Vector2 movementLimit = new Vector2(2f, 2f);
        [SerializeField] private float movementSpeed = 10f;
        [SerializeField] private float smoothTime = 0.2f;

        [Header("Rotation Settings")]
        [SerializeField] private float maxRoll = 15f;
        [SerializeField] private float maxPitch = 10f;
        [SerializeField] private float rollSpeed = 5f;
        [SerializeField] private float pitchSpeed = 5f;

        [SerializeField] Transform modelParent;
        [SerializeField] float rotateSpeed = 5f;

        private Vector3 velocity;
        private float roll;
        private float pitch;
        private Vector3 targetOffset;

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

        void HandleRotation()
        {
            if (aimTarget == null || modelParent == null) return;

            // Determine direction of the target
            Vector3 direction = aimTarget.position - modelParent.position;

            // Calculate the target rotation required to look at the target
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            modelParent.rotation = Quaternion.Lerp(modelParent.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }

        private void Update()
        {
            if (followTarget == null || input == null) return;

            // Handle movement input - accumulate offset over time
            if (input.Move != Vector2.zero)
            {
                // Apply movement to the offset with proper speed
                targetOffset.x += input.Move.x * movementSpeed * Time.deltaTime;
                targetOffset.y += input.Move.y * movementSpeed * Time.deltaTime;

                // Clamp the accumulated offset
                targetOffset.x = Mathf.Clamp(targetOffset.x, -movementLimit.x, movementLimit.x);
                targetOffset.y = Mathf.Clamp(targetOffset.y, -movementLimit.y, movementLimit.y);
            }
            else
            {
                // Gradually return to center when no input
                targetOffset = Vector3.Lerp(targetOffset, Vector3.zero, Time.deltaTime * 5f);
            }

            // 1. Base target position behind the followTarget
            Vector3 baseTargetPos = followTarget.position - followTarget.forward * followDistance;

            // 2. Apply the offset in the followTarget's local space
            Vector3 finalTargetPos = baseTargetPos +
                                   followTarget.right * targetOffset.x +
                                   followTarget.up * targetOffset.y;

            // 3. Smooth movement toward final target position
            transform.position = Vector3.SmoothDamp(transform.position, finalTargetPos, ref velocity, smoothTime);

            // 4. Rotate to face the followTarget forward
            Vector3 forwardDir = followTarget.forward;
            forwardDir.y = 0f; // Keep player upright

            if (forwardDir.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                // 5. Apply roll based on horizontal input (lean left/right)
                roll = Mathf.Lerp(roll, -input.Move.x * maxRoll, Time.deltaTime * rollSpeed);

                // 6. Apply pitch based on vertical input (tilt forward/backward)
                pitch = Mathf.Lerp(pitch, -input.Move.y * maxPitch, Time.deltaTime * pitchSpeed);

                // Combine rotation
                targetRotation *= Quaternion.Euler(pitch, 0f, roll);
                transform.rotation = targetRotation;
            }

            // Call the rotation handler every frame
            HandleRotation();
        }
    }
}