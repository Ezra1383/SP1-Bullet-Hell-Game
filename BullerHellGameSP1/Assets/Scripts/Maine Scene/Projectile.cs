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

        // Spline-based culling (set by enemy on fire — skipped for player bullets)
        private Transform _cullPlayer;
        private Vector3 _splineForward;
        private bool _useCullCheck;

        public void SetSplineCull(Transform player, Vector3 splineForward)
        {
            _cullPlayer   = player;
            _splineForward = splineForward;
            _useCullCheck  = true;
        }

        public void Launch(Vector3 dir)
        {
            transform.SetParent(null);
            gameObject.isStatic = false;
            direction = dir.normalized;
            isMoving = true;
            Destroy(gameObject, lifeTime); // fallback safety timer
        }

        void Update()
        {
            if (!isMoving) return;
            transform.position += direction * speed * Time.deltaTime;

            // Cull enemy bullets the moment they pass behind the player on the spline axis
            if (_useCullCheck && _cullPlayer != null)
            {
                float signedDist = Vector3.Dot(transform.position - _cullPlayer.position, _splineForward);
                if (signedDist < 0f)
                    Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Support child-collider setups: accept the tag on the hit object OR on its root
            if (!other.CompareTag(targetTag) && !other.transform.root.CompareTag(targetTag))
                return;

            EnemyController  enemy  = other.GetComponent<EnemyController>()
                                   ?? other.GetComponentInParent<EnemyController>();
            StationaryTurret turret = other.GetComponent<StationaryTurret>()
                                   ?? other.GetComponentInParent<StationaryTurret>();
            PlayerController player = other.GetComponent<PlayerController>()
                                   ?? other.GetComponentInParent<PlayerController>();

            if      (enemy  != null) enemy.TakeDamage(damage);
            else if (turret != null) turret.TakeDamage(damage);
            else if (player != null) player.TakeDamage(damage);

            Destroy(gameObject);
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