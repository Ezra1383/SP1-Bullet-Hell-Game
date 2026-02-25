using UnityEngine;

namespace BulletHell
{
    /// <summary>
    /// Attach to each Asteroid prefab. Deals 1 damage to the player on contact.
    /// Requires the asteroid to have a Collider with "Is Trigger" enabled.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AsteroidDamage : MonoBehaviour
    {
        [Tooltip("Seconds before this asteroid can damage the player again. " +
                 "Prevents rapid damage while the player overlaps with the asteroid.")]
        [SerializeField] private float damageCooldown = 1f;

        private float nextDamageTime;

        private void OnTriggerEnter(Collider other)
        {
            if (Time.time < nextDamageTime) return;

            PlayerController player = other.GetComponent<PlayerController>()
                                   ?? other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            player.TakeDamage(1);
            nextDamageTime = Time.time + damageCooldown;
        }
    }
}
