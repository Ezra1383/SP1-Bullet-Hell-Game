using UnityEngine;

namespace BulletHell
{
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform[] firePoints;
        [SerializeField] private float fireRate = 0.1f;

        private float nextFireTime;

        public void TryFire()
        {
            if (Time.time >= nextFireTime)
            {
                foreach (Transform pt in firePoints)
                {
                    SpawnBullet(pt);
                }
                nextFireTime = Time.time + fireRate;
            }
        }

        private void SpawnBullet(Transform point)
        {
            // Create the bullet at the fire point
            GameObject bullet = Instantiate(bulletPrefab, point.position, point.rotation);

            // FORCE SCALE: If your sun is 1000, your turret might be 0.1
            // This makes sure the bullet is actually big enough to see
            bullet.transform.localScale = new Vector3(5, 5, 5);

            Projectile proj = bullet.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Launch(point.forward);
            }
            else
            {
                Debug.LogError("No Projectile script on bullet prefab!");
            }
        }
    }
}