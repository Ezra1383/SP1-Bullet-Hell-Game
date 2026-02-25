using UnityEngine;
using UnityEngine.VFX;

namespace BulletHell
{
    public class Projectile : MonoBehaviour
    {
        public float speed = 800f;
        public float lifeTime = 5f;
        public int damage = 1;
        public string targetTag = "Enemy";

        public GameObject explosionPrefab;

        private bool isMoving = false;
        private Vector3 direction;

        public void Launch(Vector3 dir)
        {
            transform.SetParent(null);
            gameObject.isStatic = false;
            direction = dir.normalized;
            isMoving = true;
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            if (!isMoving) return;
            transform.position += direction * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 1. Check if we hit the intended target tag
            if (other.CompareTag(targetTag))
            {
                // 2. Try to find a component that can take damage
                // We use TryGetComponent for efficiency
                if (other.TryGetComponent(out EnemyController enemy))
                {
                    enemy.TakeDamage(damage);
                }
                else if (other.TryGetComponent(out StationaryTurret turret))
                {
                    turret.TakeDamage(damage);
                }

                // 3. Destroy the bullet after dealing damage
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (!gameObject.scene.isLoaded) return;

            // Detach any child trail VFX so it fades out instead of cutting off
            VisualEffect trail = GetComponentInChildren<VisualEffect>();
            if (trail != null)
            {
                trail.transform.SetParent(null);
                trail.Stop();
                Destroy(trail.gameObject, 2f);
            }

            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, transform.rotation);
        }
    }
}