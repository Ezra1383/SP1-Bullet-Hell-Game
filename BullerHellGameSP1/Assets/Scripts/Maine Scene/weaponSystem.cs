using UnityEngine;

namespace BulletHell
{
    public class WeaponSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader input;
        [SerializeField] private Transform aimTarget;
        [SerializeField] private Transform aimTarget2;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform[] firePoints;
        [SerializeField] private Transform[] firePoints2;

        [Header("Audio")]
        [SerializeField] private AudioClip shootSound;
        [Range(0f, 3f)][SerializeField] private float shootVolume = 1f;

        [Header("Settings")]
        [SerializeField] private float fireRate = 0.1f;
        [SerializeField] private bool alternateFire = false;
        [SerializeField] private Vector3 bulletScale = new Vector3(5, 5, 5);
        [SerializeField] private float aimSpeed = 20f;
        [SerializeField] private bool showDebugRays = false;
        [Tooltip("When using MediaPipe: only fire when aim ray hits an enemy. Requires enemies to use tag \"Enemy\".")]
        [SerializeField] private string enemyTag = "Enemy";
        [SerializeField] private float aimRayDistance = 1000f;
        [SerializeField] private LayerMask aimRayLayerMask = ~0;
        [Tooltip("Radius of the sphere used for aim detection. Increase to match the visual size of your aim overlay so the player fires whenever the overlay covers an enemy. Set to 0 to use a thin raycast instead.")]
        [SerializeField] private float aimRayRadius = 2f;

        private float nextFireTime;
        private int currentFirePointIndex = 0;
        private Camera mainCam;

        private void Awake()
        {
            mainCam = Camera.main;
        }

        private void Start()
        {
            // Diagnostic: catch common Inspector setup mistakes for the dual-aim system.
            // NOTE: PlayerController and WeaponSystem BOTH have an aimTarget2 field.
            // Assigning Crosshair2 in PlayerController does NOT automatically assign it here.
            if (aimTarget2 == null)
                Debug.LogWarning("[WeaponSystem] Aim Target 2 is not assigned on the WeaponSystem component. " +
                    "The second crosshair won't trigger firing and FirePoint2 won't rotate. " +
                    "Drag Crosshair2 into the Aim Target 2 slot on the WeaponSystem Inspector " +
                    "(separate from the PlayerController slot).");

            if (aimTarget2 != null && (firePoints2 == null || firePoints2.Length == 0))
                Debug.LogWarning("[WeaponSystem] aimTarget2 is assigned but Fire Points 2 is empty. " +
                    "Drag FirePoint2 into the Fire Points 2 array on the WeaponSystem Inspector.");

            if (aimTarget2 != null && firePoints2 != null && firePoints2.Length > 0)
                Debug.Log("[WeaponSystem] Dual-aim ready: aimTarget2 + firePoints2 both assigned.");
        }

        private void Update()
        {
            if (input == null || aimTarget == null) return;

            if (mainCam == null) mainCam = Camera.main;
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

            if (aimTarget2 != null && firePoints2 != null && firePoints2.Length > 0)
            {
                Vector3 aimDir2 = (aimTarget2.position - mainCam.transform.position).normalized;
                Quaternion targetRot2 = Quaternion.LookRotation(aimDir2);
                foreach (Transform fp in firePoints2)
                {
                    if (fp != null)
                    {
                        fp.rotation = Quaternion.Slerp(fp.rotation, targetRot2, aimSpeed * Time.deltaTime);

                        if (showDebugRays)
                        {
                            Debug.DrawRay(fp.position, fp.forward * 100f, Color.cyan);
                            Debug.DrawLine(fp.position, aimTarget2.position, Color.magenta);
                        }
                    }
                }
            }

            bool shouldFire = input.IsFiring ||
                              (input.useMediaPipeInput && (IsAimOnEnemy(mainCam, aimTarget) || IsAimOnEnemy(mainCam, aimTarget2)));

            if (shouldFire && Time.time >= nextFireTime)
            {
                Fire();
            }
        }

        /// <summary>
        /// When using MediaPipe: only fire when the ray from camera through aim target hits an enemy.
        /// </summary>
        private bool IsAimOnEnemy(Camera cam, Transform target)
        {
            if (cam == null || target == null) return false;
            Vector3 origin = cam.transform.position;
            Vector3 dir = (target.position - origin).normalized;
            Ray ray = new Ray(origin, dir);
            RaycastHit[] hits = aimRayRadius > 0f
                ? Physics.SphereCastAll(ray, aimRayRadius, aimRayDistance, aimRayLayerMask)
                : Physics.RaycastAll(ray, aimRayDistance, aimRayLayerMask);

            if (showDebugRays)
                Debug.DrawRay(origin, dir * 200f, target == aimTarget ? Color.yellow : Color.red);

            // Sort by distance and return true if the first valid hit is an enemy
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                if (showDebugRays)
                    Debug.Log($"[IsAimOnEnemy:{target.name}] hit '{hit.collider.name}' " +
                        $"tag='{hit.collider.tag}' dist={hit.distance:F1} " +
                        $"isSelf={hit.transform == target || hit.transform.IsChildOf(target)}");

                if (hit.transform == target || hit.transform.IsChildOf(target))
                    continue;
                if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("PlayerBullet"))
                    continue;
                return hit.collider.CompareTag(enemyTag);
            }
            return false;
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
            if (firePoints2 != null)
            {
                foreach (Transform pt in firePoints2)
                    SpawnBullet(pt);
            }

            nextFireTime = Time.time + fireRate;
            if (shootSound != null)
            {
                var src = new GameObject("SFX").AddComponent<AudioSource>();
                src.transform.position = transform.position;
                src.volume = shootVolume;
                src.PlayOneShot(shootSound);
                Destroy(src.gameObject, shootSound.length);
            }
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
