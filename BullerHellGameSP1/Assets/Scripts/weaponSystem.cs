using UnityEngine;

namespace BulletHell
{
    public class WeaponSystem : MonoBehaviour // Fixed capitalization (convention)
    {
        [SerializeField] InputReader input;
        [SerializeField] Transform targetPoint;
        [SerializeField] float targetDistance = 50f;
        [SerializeField] float smoothTime = 0.2f;
        [SerializeField] Vector2 aimLimit = new Vector2(50f, 20f);
        [SerializeField] float aimSpeed = 10f;
        [SerializeField] float aimReturnSpeed = 0.2f;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField] Transform firePoint;

        Vector3 velocity;
        Vector2 aimOffset;

        void Awake()
        {
            // Subscribe to the public fire event instead of accessing private field
            input.OnFire += OnFire;
        }

        void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            // Set the targetPosition ahead of the player's local position by the target distance
            Vector3 targetPosition = transform.position + transform.forward * targetDistance;
            Vector3 localPos = transform.InverseTransformPoint(targetPosition); // Changed to TransformPoint for position

            // Fixed the comparison operator
            if (input.Aim != Vector2.zero)
            {
                aimOffset += input.Aim * aimSpeed * Time.deltaTime;

                // Clamp the AimOffset 
                aimOffset.x = Mathf.Clamp(aimOffset.x, -aimLimit.x, aimLimit.x);
                aimOffset.y = Mathf.Clamp(aimOffset.y, -aimLimit.y, aimLimit.y);
            }
            else
            {
                // otherwise return AimOffset to zero
                aimOffset = Vector2.Lerp(aimOffset, Vector2.zero, Time.deltaTime * aimReturnSpeed);
            }

            // Apply the aimOffset to the local position
            localPos.x += aimOffset.x;
            localPos.y += aimOffset.y;

            Vector3 desiredPosition = transform.TransformPoint(localPos); // Fixed C# syntax

            // Smoothly damp to the desired position
            targetPoint.position = Vector3.SmoothDamp(targetPoint.position, desiredPosition, ref velocity, smoothTime);
        }

        void OnFire()
        {
            // Fixed C# syntax (no colon type declarations)
            Vector3 direction = targetPoint.position - firePoint.position;
            Quaternion rotation = Quaternion.LookRotation(direction);
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, rotation);
            Destroy(projectile, 5f);
        }

        void OnDestroy()
        {
            // Unsubscribe from the event
            input.OnFire -= OnFire;
        }
    }
}