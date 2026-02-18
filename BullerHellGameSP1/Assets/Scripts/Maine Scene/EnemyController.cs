using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace BulletHell
{
    public class EnemyController : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private int health = 3;
#pragma warning disable 0414 // Field assigned but never used - reserved for future scoring system
        [SerializeField] private int scoreValue = 100;
#pragma warning restore 0414
        [SerializeField] private float stopDistance = 30f; // Stop this distance from player

        [Header("Shooting")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 1f;
        [SerializeField] private bool canShoot = true;

        private SplineContainer splineContainer;
        private Spline spline;
        private float currentT;
        private float moveSpeed;
        private MovementPattern movementPattern;
        private float spawnTime;
        private Transform playerTarget;
        private float nextFireTime;
        private float playerSplineT; // Track player's position on spline

        // Movement pattern variables
        private Vector2 lateralOffset;
        private float waveFrequency = 2f;
        private float waveAmplitude = 3f;
        private float circleRadius = 5f;
        private float circleSpeed = 2f;

        void Start()
        {
            playerTarget = GameObject.FindGameObjectWithTag("Player")?.transform;
            spawnTime = Time.time;
        }

        public void Initialize(SplineContainer container, float startT, float speed, MovementPattern pattern)
        {
            splineContainer = container;
            spline = container.Spline;
            currentT = startT;
            moveSpeed = speed;
            movementPattern = pattern;
            spawnTime = Time.time;
            health = 3; // Reset health
        }

        void Update()
        {
            if (spline == null) return;

            // Get player's current position on spline
            if (playerTarget != null)
            {
                float3 playerPos = playerTarget.position;
                SplineUtility.GetNearestPoint(spline, playerPos, out float3 nearestPoint, out playerSplineT);
            }

            // Check distance to player - stop if too close
            bool shouldMove = true;
            if (playerTarget != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
                if (distanceToPlayer < stopDistance)
                {
                    shouldMove = false; // Stop moving, just shoot
                }
            }

            // Move backward along spline (toward player) only if far enough
            if (shouldMove)
            {
                float splineLength = spline.GetLength();
                currentT -= (moveSpeed / splineLength) * Time.deltaTime;

                // Wrap around if needed for looping spline
                if (currentT < 0f)
                    currentT += 1f;
            }

            // Destroy if passed player significantly
            float tDifference = currentT - playerSplineT;
            if (tDifference < -0.15f) // Enemy is significantly behind player
            {
                ReturnToPool();
                return;
            }

            UpdateMovementPattern();
            UpdatePosition();

            if (canShoot && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }

        void UpdateMovementPattern()
        {
            float timeSinceSpawn = Time.time - spawnTime;

            switch (movementPattern)
            {
                case MovementPattern.Straight:
                    // No lateral movement
                    lateralOffset = Vector2.zero;
                    break;

                case MovementPattern.SineWave:
                    lateralOffset.x = Mathf.Sin(timeSinceSpawn * waveFrequency) * waveAmplitude;
                    break;

                case MovementPattern.CircleStrafe:
                    float angle = timeSinceSpawn * circleSpeed;
                    lateralOffset.x = Mathf.Cos(angle) * circleRadius;
                    lateralOffset.y = Mathf.Sin(angle) * circleRadius;
                    break;

                case MovementPattern.Zigzag:
                    lateralOffset.x = Mathf.PingPong(timeSinceSpawn * 3f, waveAmplitude * 2) - waveAmplitude;
                    break;

                case MovementPattern.FollowPlayer:
                    if (playerTarget != null)
                    {
                        Vector3 directionToPlayer = playerTarget.position - transform.position;
                        lateralOffset += new Vector2(directionToPlayer.x, directionToPlayer.y).normalized * Time.deltaTime * 2f;
                        lateralOffset.x = Mathf.Clamp(lateralOffset.x, -10f, 10f);
                        lateralOffset.y = Mathf.Clamp(lateralOffset.y, -10f, 10f);
                    }
                    break;
            }
        }

        void UpdatePosition()
        {
            // Clamp currentT to valid range
            float clampedT = Mathf.Clamp01(currentT);

            // Evaluate spline
            float3 position = spline.EvaluatePosition(clampedT);
            float3 tangent = spline.EvaluateTangent(clampedT);
            float3 up = spline.EvaluateUpVector(clampedT);

            // Convert from spline's local space to world space
            Vector3 localPos = position;
            Vector3 worldPos = splineContainer.transform.TransformPoint(localPos);

            Vector3 splineTangent = splineContainer.transform.TransformDirection(tangent);
            Vector3 splineUp = splineContainer.transform.TransformDirection(up);
            Vector3 splineRight = Vector3.Cross(splineUp, splineTangent).normalized;

            // Apply lateral offset
            Vector3 finalPos = worldPos +
                              splineRight * lateralOffset.x +
                              splineUp * lateralOffset.y;

            transform.position = finalPos;

            // Face movement direction
            Vector3 lookDirection = -splineTangent;
            if (lookDirection.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, splineUp);
            }
        }

        void Shoot()
        {
            if (bulletPrefab == null || firePoint == null || playerTarget == null) return;

            Vector3 direction = (playerTarget.position - firePoint.position).normalized;
            Quaternion rotation = Quaternion.LookRotation(direction);

            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, rotation);

            // Add bullet pattern variations based on difficulty
            EnemyBullet bulletScript = bullet.GetComponent<EnemyBullet>();
            if (bulletScript != null)
            {
                bulletScript.Initialize(direction);
            }

            Destroy(bullet, 5f);
        }

        public void TakeDamage(int damage)
        {
            health -= damage;

            if (health <= 0)
            {
                Die();
            }
        }

        void Die()
        {
            if (BulletHell.ScoreManager.Instance != null)
            {
                BulletHell.ScoreManager.Instance.AddScore(scoreValue);
            }
            ReturnToPool();
        }

        void ReturnToPool()
        {
            ProceduralEnemySpawner spawner = FindObjectOfType<ProceduralEnemySpawner>();
            if (spawner != null)
            {
                spawner.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("PlayerBullet"))
            {
                TakeDamage(1);
                Destroy(other.gameObject);
            }
        }
    }

    // Simple enemy bullet script
    public class EnemyBullet : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;
        private Vector3 direction;

        public void Initialize(Vector3 dir)
        {
            direction = dir;
        }

        void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            // Check if the hit object is the Player
            if (other.CompareTag("Player"))
            {
                // Access the PlayerController to apply damage
                if (other.TryGetComponent(out PlayerController player))
                {
                    player.TakeDamage(1);
                }

                Destroy(gameObject);
            }
        }
    }
}