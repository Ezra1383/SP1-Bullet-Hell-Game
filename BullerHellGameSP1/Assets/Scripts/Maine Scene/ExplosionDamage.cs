using UnityEngine;

namespace BulletHell
{
    /// <summary>
    /// Attach to any explosion prefab to deal AoE damage on spawn.
    /// Uses Physics.OverlapSphere — no collider needed on the prefab.
    /// The radius should match the visual size of your explosion VFX.
    /// </summary>
    public class ExplosionDamage : MonoBehaviour
    {
        [Tooltip("Blast radius in world units. Match this to the visible size of your explosion VFX.")]
        [SerializeField] private float blastRadius = 6f;

        [Tooltip("Damage dealt to the player if they are inside the blast radius.")]
        [SerializeField] private int damage = 1;

        [Tooltip("Layer mask to limit the overlap check. Set to the Player layer for performance.")]
        [SerializeField] private LayerMask targetLayers = Physics.AllLayers;

        void Awake()
        {
            // OverlapSphere fires once instantly when the explosion spawns.
            // No need for a Rigidbody or Collider on this GameObject.
            Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius, targetLayers);

            foreach (Collider hit in hits)
            {
                PlayerController player = hit.GetComponent<PlayerController>()
                                       ?? hit.GetComponentInParent<PlayerController>();
                if (player == null) continue;

                player.TakeDamage(damage);
                HitStopManager.Instance?.TriggerHitStop();
                break; // only damage the player once even if multiple colliders match
            }
        }

        // Scene-view gizmo so you can see the blast radius without entering Play mode
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.35f);
            Gizmos.DrawSphere(transform.position, blastRadius);
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, blastRadius);
        }
    }
}
