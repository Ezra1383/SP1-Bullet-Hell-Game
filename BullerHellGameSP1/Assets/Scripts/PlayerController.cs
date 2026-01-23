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
        [SerializeField] private float maxRoll = 35f;
        [SerializeField] private float rotationSmoothness = 10f;

        [Header("Turret Settings")]
        [SerializeField] private Transform leftTurretPivot;
        [SerializeField] private Transform rightTurretPivot;
        [SerializeField] private float turretRotateSpeed = 15f;

        private Vector3 velocity;
        private Vector3 targetOffset;
        private float currentRoll;

        private void Start()
        {
            currentHealth = maxHealth;
        }

        private void Update()
        {
            if (followTarget == null || input == null) return;

            HandleMovement();
            HandleRotation();
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
            targetOffset.x += input.Move.x * movementSpeed * Time.deltaTime;
            targetOffset.y += input.Move.y * movementSpeed * Time.deltaTime;
            targetOffset.x = Mathf.Clamp(targetOffset.x, -movementLimit.x, movementLimit.x);
            targetOffset.y = Mathf.Clamp(targetOffset.y, -movementLimit.y, movementLimit.y);

            if (input.Move == Vector2.zero)
                targetOffset = Vector3.Lerp(targetOffset, Vector3.zero, Time.deltaTime * 3f);

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
                currentRoll = Mathf.Lerp(currentRoll, -input.Move.x * maxRoll, Time.deltaTime * rotationSmoothness);
                playerModel.localRotation = Quaternion.Euler(0f, 0f, currentRoll);
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