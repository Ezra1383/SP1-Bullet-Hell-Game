using UnityEngine;

namespace BulletHell
{
    public class CameraSync : MonoBehaviour
    {
        [Header("Settings")]
        // Drag your "Main Camera" (the Overlay one) here
        [SerializeField] private Transform targetCamera;

        void LateUpdate()
        {
            if (targetCamera != null)
            {
                // This makes the Space Camera mirror the Main Camera perfectly
                transform.position = targetCamera.position;
                transform.rotation = targetCamera.rotation;
            }
        }
    }
}