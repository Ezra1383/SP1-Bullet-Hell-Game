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

        [Header("Player Stats & Regen")]
        [SerializeField] private int maxHealth = 10;
        [SerializeField] private int currentHealth;
        [SerializeField] private float regenDelay = 10f; // Wait 10s after being hit
        [SerializeField] private float regenInterval = 1f; // Heal 1 HP every 1s after delay

        private float lastHitTime;
        private float nextRegenTick;

        [Header("Visual Framing")]
        [SerializeField] private Vector2 homeOffset = new Vector2(0, -1.5f);
        [SerializeField] private float followDistance = 5f;

        [Header("Movement Settings")]
        [SerializeField] private Vector2 movementLimit = new Vector2(8f, 5f);
        [SerializeField] private float movementSpeed = 12f;
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Banking Settings")]
        [SerializeField] private float maxRoll = 60f;
        [SerializeField] private float rotationSmoothness = 10f;

        [Header("Turret Settings")]
        [SerializeField] private Transform leftTurretPivot;
        [SerializeField] private Transform rightTurretPivot;
        [SerializeField] private float turretRotateSpeed = 15f;

        [Header("Aim Settings")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float aimDistance = 100f; // Default distance if raycast hits nothing
        [SerializeField] private LayerMask aimLayerMask = ~0; // What layers to raycast against

        private Vector3 velocity;
        private Vector3 targetOffset;
        private float currentRoll;

        // Public property to expose current speed magnitude
        public float CurrentSpeed => velocity.magnitude;

        private void Start()
        {
            currentHealth = maxHealth;

            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Update()
        {
            if (followTarget == null || input == null) return;

            HandleMovement();
            HandleRotation();
            UpdateAimTarget();
            HandleTurretAiming();
            HandleRegeneration();
        }

        public void TakeDamage(int damage)
        {
            currentHealth -= damage;
            lastHitTime = Time.time; // Reset the 10-second timer
            Debug.Log($"Player Hit! Health: {currentHealth}");

            if (currentHealth <= 0) Die();
        }

        private void HandleRegeneration()
        {
            // Only heal if we are damaged and enough time has passed since the last hit
            if (currentHealth < maxHealth && Time.time > lastHitTime + regenDelay)
            {
                if (Time.time >= nextRegenTick)
                {
                    currentHealth++;
                    nextRegenTick = Time.time + regenInterval;
                    Debug.Log($"Regenerating... Health: {currentHealth}");
                }
            }
        }

        private void HandleMovement()
        {
            if (input.useMediaPipeInput)
            {
                // MediaPipe: input.Move encodes the desired face position in normalized [0,1] space.
                // We convert that to a target offset, then move the plane toward it using
                // the same "move vector" style the plane script already uses.

                // 1. Convert normalized face position to desired offset in our movement area.
                Vector2 normPos = input.Move; // expected 0..1 from MediaPipeInputBridge
                float desiredX = Mathf.Lerp(-movementLimit.x, movementLimit.x, Mathf.Clamp01(normPos.x));
                float desiredY = Mathf.Lerp(-movementLimit.y, movementLimit.y, Mathf.Clamp01(normPos.y));
                Vector2 desiredOffset = new Vector2(desiredX, desiredY);

                // 2. Compute direction from current offset to target offset.
                Vector2 currentOffset = new Vector2(targetOffset.x, targetOffset.y);
                Vector2 toTarget = desiredOffset - currentOffset;

                // 3. Turn that into a "move vector" limited to length 1
                // so the actual movement speed is still controlled by movementSpeed.
                Vector2 moveVector = Vector2.zero;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    // Scale by distance but clamp to max length 1 for stable speed.
                    moveVector = Vector2.ClampMagnitude(toTarget, 1f);
                }

                // 4. Apply the move vector exactly like other inputs do.
                targetOffset.x += moveVector.x * movementSpeed * Time.deltaTime;
                targetOffset.y += moveVector.y * movementSpeed * Time.deltaTime;
                targetOffset.x = Mathf.Clamp(targetOffset.x, -movementLimit.x, movementLimit.x);
                targetOffset.y = Mathf.Clamp(targetOffset.y, -movementLimit.y, movementLimit.y);
            }
            else
            {
                // Traditional controls: Move is a direct input vector (stick/keys)
                // that we integrate over time.
                targetOffset.x += input.Move.x * movementSpeed * Time.deltaTime;
                targetOffset.y += input.Move.y * movementSpeed * Time.deltaTime;
                targetOffset.x = Mathf.Clamp(targetOffset.x, -movementLimit.x, movementLimit.x);
                targetOffset.y = Mathf.Clamp(targetOffset.y, -movementLimit.y, movementLimit.y);

                if (input.Move == Vector2.zero)
                    targetOffset = Vector3.Lerp(targetOffset, Vector3.zero, Time.deltaTime * 3f);
            }

            Vector3 baseTargetPos = followTarget.position - (followTarget.forward * followDistance);
            Vector3 finalTargetPos = baseTargetPos
                                   + (followTarget.right * (homeOffset.x + targetOffset.x))
                                   + (followTarget.up * (homeOffset.y + targetOffset.y));

            transform.position = Vector3.SmoothDamp(transform.position, finalTargetPos, ref velocity, smoothTime);
        }

        private void HandleRotation()
        {
            transform.rotation = Quaternion.LookRotation(followTarget.forward, Vector3.up);
            if (playerModel != null)
            {
                // Direct tilt from input
                float tiltInput;
                if (input.useMediaPipeInput)
                {
                    // MediaPipe: input.Move.x is 0-1, convert to -1 to 1 for tilt
                    tiltInput = (input.Move.x - 0.5f) * 2f;
                }
                else
                {
                    // Keyboard/gamepad: input.Move.x is already -1 to 1
                    tiltInput = input.Move.x;
                }

                float targetRoll = -tiltInput * maxRoll;
                currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * rotationSmoothness);
                playerModel.localRotation = Quaternion.Euler(0f, 0f, currentRoll);
            }
        }

        private void UpdateAimTarget()
        {
            if (aimTarget == null || mainCamera == null) return;

            // Get mouse position directly from Unity (not Input System)
            Vector3 mousePos = (Vector3)input.Aim;

            // Create a ray from the camera through the mouse position
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            // Raycast to find actual world geometry, ignoring the aimpoint itself and player
            RaycastHit[] hits = Physics.RaycastAll(ray, aimDistance, aimLayerMask);
            RaycastHit? closestHit = null;
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                // Ignore the aimpoint itself, player, and player bullets
                if (hit.transform == aimTarget ||
                    hit.transform.IsChildOf(aimTarget) ||
                    hit.transform == transform ||
                    hit.transform.IsChildOf(transform) ||
                    hit.collider.CompareTag("PlayerBullet"))
                    continue;

                // Track closest valid hit
                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                }
            }

            if (closestHit.HasValue)
            {
                // Use the closest valid hit
                aimTarget.position = closestHit.Value.point;
            }
            else
            {
                // Hit nothing valid - use far distance
                aimTarget.position = ray.GetPoint(aimDistance);
            }
        }

        private void HandleTurretAiming()
        {
            RotateTurret(leftTurretPivot);
            RotateTurret(rightTurretPivot);
        }

        private void RotateTurret(Transform turret)
        {
            if (turret == null) return;
            Vector3 direction = aimTarget.position - turret.position;
            if (direction.sqrMagnitude < 0.1f) return;
            turret.rotation = Quaternion.Slerp(turret.rotation, Quaternion.LookRotation(direction), turretRotateSpeed * Time.deltaTime);
        }

        private void Die()
        {
            Debug.Log("Game Over!");
            // Add explosion VFX or Scene Restart here
        }
    }
}