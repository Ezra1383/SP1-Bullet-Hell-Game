using UnityEngine;

namespace BulletHell
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] float speed = 10f;

         
        private void Update()
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }
    }
}