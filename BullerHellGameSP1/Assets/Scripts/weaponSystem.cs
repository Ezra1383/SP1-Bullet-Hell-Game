using UnityEngine;

namespace BulletHell
{
    public class WeaponSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private Transform aimTarget; // Drag your AimTarget (reticle) here
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform[] firePoints;

        [Header("Settings")]
        [SerializeField] private float fireRate = 0.1f;
        [SerializeField] private bool alternateFire = false;
        [SerializeField] private Vector3 bulletScale = new Vector3(5, 5, 5);

        private float nextFireTime;
        private int currentFirePointIndex = 0;

        private void Update()
        {
            if (input == null || aimTarget == null) return;

            if (input.IsFiring && Time.time >= nextFireTime)
            {
                Fire();
            }
        }

        private void Fire()
        {
            if (alternateFire)
            {
                if (firePoints.Length > 0)
                {
                    SpawnBullet(firePoints[currentFirePointIndex]);
                    currentFirePointIndex = (currentFirePointIndex + 1) % firePoints.Length;
                }
            }
            else
            {
                foreach (Transform pt in firePoints)
                {
                    SpawnBullet(pt);
                }
            }
            nextFireTime = Time.time + fireRate;
        }

        private void SpawnBullet(Transform point)
        {
            if (point == null || bulletPrefab == null) return;

            GameObject bullet = Instantiate(bulletPrefab, point.position, point.rotation);
            bullet.transform.localScale = bulletScale;

            Projectile proj = bullet.GetComponent<Projectile>();
            if (proj != null)
            {
                // FIX: Calculate direction towards the ACTUAL aim target world position
                Vector3 targetDir = (aimTarget.position - point.position).normalized;
                proj.Launch(targetDir);
            }
        }
    }
}