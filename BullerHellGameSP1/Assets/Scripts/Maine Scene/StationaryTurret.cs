using UnityEngine;
using System.Collections.Generic;

namespace BulletHell
{
    [System.Serializable]
    public class CannonBattery
    {
        public string cannonName;   // For your organization
        public Transform pivot;      // The part that rotates
        public Transform firePoint;  // Where the bullet spawns
        public float fireRate = 2f;  // How fast THIS specific gun fires
        [HideInInspector] public float nextFireTime;
    }

    public class StationaryTurret : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField] private int health = 15;
        [Tooltip("Points awarded when this turret is destroyed")]
        [SerializeField] private int scoreValue = 250;
        [SerializeField] private float detectionRadius = 400f;
        [SerializeField] private float rotationSpeed = 5f;

        [Header("Weapon Systems")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private List<CannonBattery> cannons = new List<CannonBattery>();

        [Header("Damage Numbers")]
        [SerializeField] private GameObject damageNumberPrefab;

        [Header("Death")]
        [SerializeField] private GameObject explosion;

        private Transform playerTarget;

        void Start()
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTarget = playerObj.transform;
        }

        void Update()
        {
            if (playerTarget == null || cannons.Count == 0) return;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);

            // If the player is in range of the BASE, all cannons start tracking
            if (distanceToPlayer <= detectionRadius)
            {
                foreach (var cannon in cannons)
                {
                    HandleCannon(cannon);
                }
            }
        }

        private void HandleCannon(CannonBattery cannon)
        {
            if (cannon.pivot == null) return;

            // 1. Individual Aiming
            // Each cannon calculates its own direction to the player
            Vector3 targetDirection = playerTarget.position - cannon.pivot.position;
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

            cannon.pivot.rotation = Quaternion.Slerp(
                cannon.pivot.rotation,
                targetRotation,
                Time.deltaTime * rotationSpeed
            );

            // 2. Individual Shooting
            // Each cannon tracks its own fire rate
            if (Time.time >= cannon.nextFireTime)
            {
                Shoot(cannon);
                cannon.nextFireTime = Time.time + cannon.fireRate;
            }
        }

        private void Shoot(CannonBattery cannon)
        {
            if (bulletPrefab != null && cannon.firePoint != null)
            {
                GameObject bullet = Instantiate(bulletPrefab, cannon.firePoint.position, cannon.firePoint.rotation);

                Projectile bulletScript = bullet.GetComponent<Projectile>();
                if (bulletScript != null)
                {
                    bulletScript.Launch(cannon.firePoint.forward);
                }
            }
        }

        public void TakeDamage(int damage)
        {
            health -= damage;

            if (damageNumberPrefab != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 2f;
                var dn = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
                dn.GetComponent<DamageNumber>()?.Play(damage, spawnPos, Camera.main);
            }

            if (health <= 0) Die();
        }

        private void Die()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddScore(scoreValue);
            }

            if (explosion != null)
                Instantiate(explosion, transform.position, transform.rotation);

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}