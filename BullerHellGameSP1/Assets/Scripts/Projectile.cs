using UnityEngine;

namespace BulletHell
{
    public class Projectile : MonoBehaviour
    {
        public float speed = 800f;
        public float lifeTime = 5f;
        public int damage = 1; // New: Added damage value
        public string targetTag = "Enemy"; // Set this to "Enemy" for player bullets

        private bool isMoving = false;
        private Vector3 moveDir;

        public void Launch(Vector3 direction)
        {
            transform.SetParent(null);
            gameObject.isStatic = false;
            moveDir = direction.normalized;
            isMoving = true;
            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            if (!isMoving) return;
            transform.Translate(moveDir * speed * Time.deltaTime, Space.World);
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
    }
}