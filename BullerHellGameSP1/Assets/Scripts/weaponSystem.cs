using UnityEngine;

namespace BulletHell
{
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform[] firePoints; // Assign both turret fire points here
        [SerializeField] private float fireRate = 0.1f;
        [SerializeField] private bool alternateFire = false; // Fire guns one at a time

        [Header("Visual Effects (Optional)")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private float muzzleFlashDuration = 0.1f;

        private float nextFireTime;
        private int currentFirePointIndex = 0;

        public void TryFire()
        {
            if (Time.time >= nextFireTime)
            {
                if (alternateFire)
                {
                    // Fire one gun at a time
                    if (firePoints.Length > 0)
                    {
                        SpawnBullet(firePoints[currentFirePointIndex]);
                        currentFirePointIndex = (currentFirePointIndex + 1) % firePoints.Length;
                    }
                }
                else
                {
                    // Fire all guns at once
                    foreach (Transform pt in firePoints)
                    {
                        SpawnBullet(pt);
                    }
                }

                nextFireTime = Time.time + fireRate;
            }
        }

        private void SpawnBullet(Transform point)
        {
            if (point == null || bulletPrefab == null) return;

            // Create the bullet at the fire point
            GameObject bullet = Instantiate(bulletPrefab, point.position, point.rotation);

            // Set bullet scale
            bullet.transform.localScale = new Vector3(5, 5, 5);

            // Launch the bullet in the direction the turret is facing
            Projectile proj = bullet.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Launch(point.forward);
            }
            else
            {
                Debug.LogError("No Projectile script on bullet prefab!");
            }

            // Optional: Spawn muzzle flash
            if (muzzleFlashPrefab != null)
            {
                GameObject flash = Instantiate(muzzleFlashPrefab, point.position, point.rotation);
                Destroy(flash, muzzleFlashDuration);
            }
        }
    }
}