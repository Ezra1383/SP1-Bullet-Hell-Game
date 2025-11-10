using UnityEngine;

namespace BulletHell
{

    public class CameraController : MonoBehaviour
    {


        [SerializeField] Transform player;
        [SerializeField] float followDistance = 22f;
        [SerializeField] float smoothTime = 0.3f;

        Vector3 velocity;

        private void Update()
        {
            // Simple follow behind the player
            Vector3 targetPos = player.position + -player.forward * followDistance;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);

            // Make camera look at player
            transform.LookAt(player);
        }
    }
}