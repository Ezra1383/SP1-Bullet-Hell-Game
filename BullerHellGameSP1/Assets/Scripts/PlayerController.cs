using DG.Tweening;
using UnityEngine;

namespace BulletHell
{
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private Transform playerModel;

        [Header("Turret References")]
        [SerializeField] private Transform leftTurretPivot;
        [SerializeField] private Transform rightTurretPivot;
        [SerializeField] private float turretRotateSpeed = 8f;
        [SerializeField] private bool limitTurretRotation = true;
        [SerializeField] private float maxTurretYaw = 60f; // How far turrets can rotate left/right
        [SerializeField] private float maxTurretPitch = 45f; // How far turrets can rotate up/down

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

        private void Update()
        {
            if (followTarget == null || input == null) return;

            HandleMovement();
            HandleBodyRotation();
            HandleTurretAiming();
        }

        private void HandleMovement()
        {
            if (input.Move != Vector2.zero)
            {
                targetOffset.x += input.Move.x * movementSpeed * Time.deltaTime;
                targetOffset.y += input.Move.y * movementSpeed * Time.deltaTime;

                targetOffset.x = Mathf.Clamp(targetOffset.x, -movementLimit.x, movementLimit.x);
                targetOffset.y = Mathf.Clamp(targetOffset.y, -movementLimit.y, movementLimit.y);
            }
            else
            {
                targetOffset = Vector3.Lerp(targetOffset, Vector3.zero, Time.deltaTime * 5f);
            }

            Vector3 baseTargetPos = followTarget.position - followTarget.forward * followDistance;
            Vector3 finalTargetPos = baseTargetPos +
                                   followTarget.right * targetOffset.x +
                                   followTarget.up * targetOffset.y;

            transform.position = Vector3.SmoothDamp(transform.position, finalTargetPos, ref velocity, smoothTime);
        }

        private void HandleBodyRotation()
        {
            // Player body follows the spline direction
            Vector3 forwardDir = followTarget.forward;

            if (forwardDir.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                roll = Mathf.Lerp(roll, -input.Move.x * maxRoll, Time.deltaTime * rollSpeed);
                pitch = Mathf.Lerp(pitch, -input.Move.y * maxPitch, Time.deltaTime * pitchSpeed);

                targetRotation *= Quaternion.Euler(pitch, 0f, roll);
                transform.rotation = targetRotation;
            }
        }

        private void HandleTurretAiming()
        {
            if (aimTarget == null) return;

            // Rotate both turrets to aim at the target
            RotateTurret(leftTurretPivot);
            RotateTurret(rightTurretPivot);
        }

        private void RotateTurret(Transform turret)
        {
            if (turret == null) return;

            Vector3 direction = aimTarget.position - turret.position;

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);

                if (limitTurretRotation)
                {
                    // Calculate the rotation relative to the player's forward
                    Quaternion localRotation = Quaternion.Inverse(transform.rotation) * targetRotation;
                    Vector3 localEuler = localRotation.eulerAngles;

                    // Normalize angles to -180 to 180
                    if (localEuler.x > 180) localEuler.x -= 360;
                    if (localEuler.y > 180) localEuler.y -= 360;

                    // Clamp the rotation
                    localEuler.y = Mathf.Clamp(localEuler.y, -maxTurretYaw, maxTurretYaw);
                    localEuler.x = Mathf.Clamp(localEuler.x, -maxTurretPitch, maxTurretPitch);
                    localEuler.z = 0; // Keep turret upright

                    // Convert back to world rotation
                    targetRotation = transform.rotation * Quaternion.Euler(localEuler);
                }

                turret.rotation = Quaternion.Lerp(
                    turret.rotation,
                    targetRotation,
                    turretRotateSpeed * Time.deltaTime
                );
            }
        }
    }
}