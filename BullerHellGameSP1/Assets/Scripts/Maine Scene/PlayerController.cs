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
        [Tooltip("MediaPipe only: bank from movement velocity (direction nose is moving) instead of position. Smoother and more natural.")]
        [SerializeField] private bool bankFromVelocity = true;
        [Tooltip("MediaPipe only: how much horizontal velocity translates to roll. Tune so moving your head feels right.")]
        [SerializeField] private float velocityToRollGain = 0.5f;
        [Tooltip("MediaPipe only: velocity below this is treated as zero so the ship levels out when still.")]
        [SerializeField] private float velocityDeadzone = 0.3f;
        [Tooltip("MediaPipe only: smoothing of velocity before converting to roll. Higher = smoother, less jitter.")]
        [SerializeField] private float velocitySmoothTime = 0.08f;

        [Header("Turret Settings")]
        [SerializeField] private Transform leftTurretPivot;
        [SerializeField] private Transform rightTurretPivot;
        [SerializeField] private float turretRotateSpeed = 15f;

        [Header("Aim Settings")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private float aimDistance = 500f;
        [SerializeField] private float aimTweenDuration = 0.12f;
        [SerializeField] private Ease aimEase = Ease.OutSine;

        private Vector3 velocity;
        private Vector3 targetOffset;
        private float currentRoll;
        private float smoothedVelocityX; // For MediaPipe velocity-based banking

        // Public property to expose current speed magnitude
        public float CurrentSpeed => velocity.magnitude;

        private void Start()
        {
            currentHealth = maxHealth;

            if (mainCamera == null)
                mainCamera = Camera.main;

            HealthBarUI.Instance?.SetHealth(currentHealth, maxHealth);
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

            HealthBarUI.Instance?.SetHealth(currentHealth, maxHealth);

            if (currentHealth <= 0)
                Die();
            else
                HitStopManager.Instance?.TriggerHitStop();
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
                    HealthBarUI.Instance?.SetHealth(currentHealth, maxHealth);
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
                float tiltInput;
                if (input.useMediaPipeInput)
                {
                    if (bankFromVelocity)
                    {
                        // Bank from movement direction: ship tilts in the direction your nose is moving,
                        // levels out when still. Much more consistent than position-based banking.
                        float rawVelocityX = input.InputVelocity.x * velocityToRollGain;
                        float dt = Time.deltaTime;
                        float smoothFactor = velocitySmoothTime > 0f ? Mathf.Clamp01(dt / velocitySmoothTime) : 1f;
                        smoothedVelocityX = Mathf.Lerp(smoothedVelocityX, rawVelocityX, smoothFactor);

                        // Deadzone: treat small velocity as zero so ship levels out when head is still
                        if (Mathf.Abs(smoothedVelocityX) < velocityDeadzone)
                            smoothedVelocityX = Mathf.MoveTowards(smoothedVelocityX, 0f, (1f / velocitySmoothTime) * dt);

                        tiltInput = Mathf.Clamp(smoothedVelocityX, -1f, 1f);
                    }
                    else
                    {
                        // Legacy: position-based (nose X 0–1 → -1 to 1)
                        tiltInput = (input.Move.x - 0.5f) * 2f;
                    }
                }
                else
                {
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

            Vector3 mousePos = (Vector3)input.Aim;
            Ray ray = mainCamera.ScreenPointToRay(mousePos);

            // Intersect the ray with a plane sitting aimDistance units in front of the ship.
            Vector3 planeCenter = transform.position + transform.forward * aimDistance;
            Plane aimPlane = new Plane(-mainCamera.transform.forward, planeCenter);

            Vector3 worldHit;
            if (aimPlane.Raycast(ray, out float enter))
                worldHit = ray.GetPoint(enter);
            else
                worldHit = planeCenter;

            // Convert to local space and only move XY — Z stays fixed (set in Inspector).
            Vector3 localHit = transform.InverseTransformPoint(worldHit);
            Vector3 desiredLocal = new Vector3(localHit.x, localHit.y, aimTarget.localPosition.z);

            aimTarget.DOKill();
            aimTarget.DOLocalMove(desiredLocal, aimTweenDuration).SetEase(aimEase);
        }

        private void HandleTurretAiming()
        {
            RotateTurret(leftTurretPivot);
            RotateTurret(rightTurretPivot);
        }

        private void RotateTurret(Transform turret)
        {
            if (turret == null || aimTarget == null) return;
            Vector3 direction = aimTarget.position - turret.position;
            if (direction.sqrMagnitude < 0.1f) return;
            turret.rotation = Quaternion.Slerp(turret.rotation, Quaternion.LookRotation(direction), turretRotateSpeed * Time.deltaTime);
        }

        private void Die()
        {
            GameOverScreen.Instance?.Show();
        }
    }
}