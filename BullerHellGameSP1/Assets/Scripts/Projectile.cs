using UnityEngine;

namespace BulletHell
{
    public class Projectile : MonoBehaviour
    {
        public float speed = 800f;
        public float lifeTime = 5f;
        public string targetTag = "Player"; // For enemy bullets

        private bool isMoving = false;
        private Vector3 moveDir;

        public void Launch(Vector3 direction)
        {
            // 1. Move it to the root of the hierarchy immediately
            transform.SetParent(null);

            // 2. Ensure it isn't marked as static
            gameObject.isStatic = false;

            moveDir = direction.normalized;
            isMoving = true;

            Destroy(gameObject, lifeTime);
        }

        void Update()
        {
            if (!isMoving) return;

            // Manual translation ignores all physics and static constraints
            transform.Translate(moveDir * speed * Time.deltaTime, Space.World);

            // Debug line to see it in Scene View
            Debug.DrawRay(transform.position, moveDir * 10f, Color.red);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(targetTag))
            {
                // Damage logic here
                Destroy(gameObject);
            }
        }
    }
}