using UnityEngine;

namespace BulletHell
{
    /// <summary>
    /// Briefly freezes Time.timeScale on hit for a classic "hit-stop" feel.
    /// Call HitStopManager.Instance.TriggerHitStop() from PlayerController.TakeDamage().
    /// Uses unscaledDeltaTime so the countdown runs correctly while time is frozen.
    /// </summary>
    public class HitStopManager : MonoBehaviour
    {
        public static HitStopManager Instance { get; private set; }

        [Header("Hit-Stop Settings")]
        [Tooltip("How long time stays frozen (in real seconds). 2-4 frames = ~0.033-0.066s")]
        [SerializeField] private float duration = 0.05f;

        [Tooltip("How slow time gets during hit-stop. 0 = full freeze, 0.05 = near-freeze.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float frozenTimeScale = 0.05f;

        private float timer;
        private bool  active;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>
        /// Trigger a hit-stop. Safe to call while one is already running — restarts it.
        /// </summary>
        public void TriggerHitStop()
        {
            timer            = duration;
            Time.timeScale   = frozenTimeScale;
            active           = true;
        }

        /// <summary>Trigger with a custom duration override.</summary>
        public void TriggerHitStop(float customDuration)
        {
            timer            = customDuration;
            Time.timeScale   = frozenTimeScale;
            active           = true;
        }

        void Update()
        {
            if (!active) return;

            // Must use unscaledDeltaTime — deltaTime is near-zero during hit-stop
            timer -= Time.unscaledDeltaTime;

            if (timer <= 0f)
            {
                Time.timeScale = 1f;
                active         = false;
            }
        }

        void OnDestroy()
        {
            // Safety: always restore timeScale if this object is destroyed mid-stop
            if (active) Time.timeScale = 1f;
        }
    }
}
