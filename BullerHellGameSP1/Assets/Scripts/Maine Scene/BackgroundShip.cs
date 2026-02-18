using UnityEngine;

namespace BulletHell
{
    /// <summary>
    /// Controls a background ship that moves across the screen and shoots lasers for atmospheric effect.
    /// These ships are not enemies - they exist purely to make the world feel alive with ambient space combat.
    /// </summary>
    public class BackgroundShip : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Speed at which the ship moves in its forward direction")]
        [SerializeField] private float moveSpeed = 15f;

        [Header("Shooting")]
        [Tooltip("Prefab to instantiate when shooting (should be a simple projectile)")]
        [SerializeField] private GameObject projectilePrefab;

        [Tooltip("Point from which projectiles are spawned")]
        [SerializeField] private Transform firePoint;

        [Tooltip("Minimum time between shots in seconds")]
        [SerializeField] private float minFireInterval = 0.5f;

        [Tooltip("Maximum time between shots in seconds")]
        [SerializeField] private float maxFireInterval = 2f;

        [Tooltip("Speed at which projectiles travel")]
        [SerializeField] private float projectileSpeed = 25f;

        [Tooltip("How long projectiles exist before being destroyed")]
        [SerializeField] private float projectileLifetime = 3f;

        [Header("Lifetime")]
        [Tooltip("How long the ship exists before self-destructing")]
        [SerializeField] private float lifetime = 10f;

        private float nextFireTime;
        private float destroyTime;

        /// <summary>
        /// Initialize the background ship with its movement direction and speed.
        /// </summary>
        /// <param name="direction">Normalized direction vector for ship movement</param>
        /// <param name="speed">Override speed (optional, uses default if not provided)</param>
        public void Initialize(Vector3 direction, float speed = -1f)
        {
            if (speed > 0f)
                moveSpeed = speed;

            // Orient the ship to face its movement direction
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            ScheduleNextShot();
        }

        void Start()
        {
            // Set destruction time
            destroyTime = Time.time + lifetime;

            // Schedule first shot if not already initialized
            if (nextFireTime == 0f)
            {
                ScheduleNextShot();
            }
        }

        void Update()
        {
            // Move forward continuously
            transform.position += transform.forward * moveSpeed * Time.deltaTime;

            // Check if it's time to shoot
            if (Time.time >= nextFireTime && projectilePrefab != null)
            {
                Shoot();
                ScheduleNextShot();
            }

            // Self-destruct after lifetime expires
            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Schedules the next shot at a random interval.
        /// </summary>
        private void ScheduleNextShot()
        {
            float randomInterval = Random.Range(minFireInterval, maxFireInterval);
            nextFireTime = Time.time + randomInterval;
        }

        /// <summary>
        /// Fires a projectile in the ship's forward direction.
        /// </summary>
        private void Shoot()
        {
            if (projectilePrefab == null) return;

            // Determine spawn position (use firePoint if available, otherwise use ship position)
            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;

            // Instantiate projectile
            GameObject projectile = Instantiate(
                projectilePrefab,
                spawnPosition,
                transform.rotation
            );

            // Add velocity to projectile
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = transform.forward * projectileSpeed;
            }
            else
            {
                // If no rigidbody, try to add a simple mover component
                BackgroundProjectile projScript = projectile.GetComponent<BackgroundProjectile>();
                if (projScript == null)
                {
                    projScript = projectile.AddComponent<BackgroundProjectile>();
                }
                projScript.Initialize(transform.forward, projectileSpeed);
            }

            // Destroy projectile after lifetime
            Destroy(projectile, projectileLifetime);
        }

        /// <summary>
        /// Draws debug information in the Scene view.
        /// </summary>
        private void OnDrawGizmos()
        {
            // Draw movement direction
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 5f);
        }
    }

    /// <summary>
    /// Simple projectile mover for background ship projectiles that don't have a Rigidbody.
    /// </summary>
    public class BackgroundProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;

        /// <summary>
        /// Initialize the projectile with direction and speed.
        /// </summary>
        /// <param name="dir">Direction to move</param>
        /// <param name="spd">Speed to move at</param>
        public void Initialize(Vector3 dir, float spd)
        {
            direction = dir.normalized;
            speed = spd;
        }

        void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
        }
    }
}
