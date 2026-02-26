using UnityEngine;

namespace BulletHell
{
    public class CameraController : MonoBehaviour
    {
        [Header("Targets")]
        // Drag the same "Follow Target" (the rail anchor) used in PlayerController here
        [SerializeField] private Transform railTarget;
        [Tooltip("Drag the Player GameObject here. The camera will look ahead from the player's position instead of the rail anchor, keeping the ship in view with more battlefield visible in front.")]
        [SerializeField] private Transform playerTransform;

        [Header("Position Settings")]
        [SerializeField] private float followDistance = 20f;
        [SerializeField] private float heightOffset = 3f; // Raise camera above the rail
        [SerializeField] private float smoothTime = 0.15f;

        [Header("Look Settings")]
        [SerializeField] private float lookAheadDistance = 10f; // Look at a point ahead of the rail
        [SerializeField] private Vector3 lookOffset; // Fine-tune where the camera looks

        private Vector3 velocity;

        private void LateUpdate()
        {
            if (railTarget == null) return;

            // 1. Calculate the ideal position behind the rail target
            Vector3 targetPos = railTarget.position
                                - (railTarget.forward * followDistance)
                                + (railTarget.up * heightOffset);

            // 2. Smoothly move the camera to that position
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

            // 3. Determine the point to look at ahead of the player (falls back to rail if player not assigned)
            Vector3 lookOrigin = playerTransform != null ? playerTransform.position : railTarget.position;
            Vector3 lookAtPoint = lookOrigin + (railTarget.forward * lookAheadDistance) + lookOffset;

            transform.LookAt(lookAtPoint);
        }
    }
}