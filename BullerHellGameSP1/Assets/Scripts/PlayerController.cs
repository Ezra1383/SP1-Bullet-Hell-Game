using UnityEngine;

namespace BulletHell
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputReader input; // Reference to your InputReader

        [SerializeField] private Transform followTarget; // The sphere that follows the spiral path
        [SerializeField] private float followDistance = 2f;
        [SerializeField] private Vector2 movementLimit = new Vector2(2f, 2f);
        [SerializeField] private float movementSpeed = 10f;
        [SerializeField] private float smoothTime = 0.2f;
        [SerializeField] private float movementRange = 5f;

        [Header("Rotation Settings")]
        [SerializeField] private float maxRoll = 15f;   // Lean left/right
        [SerializeField] private float maxPitch = 10f;  // Tilt forward/backward
        [SerializeField] private float rollSpeed = 5f;  // Lerp speed for roll
        [SerializeField] private float pitchSpeed = 5f; // Lerp speed for pitch

        private Vector3 velocity;
        private float roll;
        private float pitch;

        void Update()
        {
            if (followTarget == null || input == null) return;

            // 1. Target position behind the followTarget
            Vector3 targetPos = followTarget.position - followTarget.forward * followDistance;

            // 2. Smooth movement toward target
            Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

            // 3. Convert to local space of the followTarget
            Vector3 localPos = followTarget.InverseTransformPoint(smoothedPos);

            // 4. Apply player input offset
            localPos.x += input.Move.x * movementSpeed * Time.deltaTime * movementRange;
            localPos.y += input.Move.y * movementSpeed * Time.deltaTime * movementRange;

            // 5. Clamp within movement limits
            localPos.x = Mathf.Clamp(localPos.x, -movementLimit.x, movementLimit.x);
            localPos.y = Mathf.Clamp(localPos.y, -movementLimit.y, movementLimit.y);

            // 6. Convert back to world space
            transform.position = followTarget.TransformPoint(localPos);

            // 7. Rotate to face the followTarget forward
            Vector3 forwardDir = followTarget.forward;
            forwardDir.y = 0f; // Keep player upright
            if (forwardDir.sqrMagnitude > 0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                // 8. Apply roll based on horizontal input (lean left/right)
                roll = Mathf.Lerp(roll, -input.Move.x * maxRoll, Time.deltaTime * rollSpeed);

                // 9. Apply pitch based on vertical input (tilt forward/backward)
                pitch = Mathf.Lerp(pitch, -input.Move.y * maxPitch, Time.deltaTime * pitchSpeed);

                // Combine rotation
                targetRotation *= Quaternion.Euler(pitch, 0f, roll);

                transform.rotation = targetRotation;
            }
        }
    }
}
