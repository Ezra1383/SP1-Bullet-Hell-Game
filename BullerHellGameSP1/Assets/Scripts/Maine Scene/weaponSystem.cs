using UnityEngine;

namespace BulletHell
{
    public class WeaponSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform[] firePoints;

        [Header("Settings")]
        [SerializeField] private float fireRate = 0.1f;
        [SerializeField] private bool alternateFire = false;
        [SerializeField] private Vector3 bulletScale = new Vector3(5, 5, 5);
        [SerializeField] private float aimSpeed = 20f;
        [SerializeField] private bool showDebugRays = false;

        private float nextFireTime;
        private int currentFirePointIndex = 0;

        private void Update()
        {
            if (input == null || aimTarget == null) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // All guns aim parallel - in the direction from camera through aimpoint
            Vector3 aimDirection = (aimTarget.position - mainCam.transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(aimDirection);

            // Rotate all fire points to match the aim direction
            foreach (Transform firePoint in firePoints)
            {
                if (firePoint != null)
                {
                    firePoint.rotation = Quaternion.Slerp(firePoint.rotation, targetRotation, aimSpeed * Time.deltaTime);

                    if (showDebugRays)
                    {
                        Debug.DrawRay(firePoint.position, firePoint.forward * 100f, Color.green);
                        Debug.DrawLine(firePoint.position, aimTarget.position, Color.yellow);
                    }
                }
            }

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
                    SpawnBullet(pt);
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
                proj.Launch(point.forward);
        }
    }
}
