using UnityEngine;
using System.Collections.Generic;

namespace BulletHell
{
    /// <summary>
    /// Procedurally populates the world with asteroid prefabs as the player flies forward.
    /// Uses object pooling so recycled asteroids are moved ahead rather than destroyed/created.
    /// Works with LOD Groups and GPU Instancing — no extra setup needed.
    /// </summary>
    public class AsteroidFieldSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class AsteroidVariant
        {
            [Tooltip("One of your Asteroid_00 to Asteroid_03 prefabs")]
            public GameObject prefab;

            [Tooltip("Relative spawn weight. Higher = appears more often.")]
            [Range(0f, 1f)] public float spawnWeight = 1f;
        }

        // ── Prefabs ────────────────────────────────────────────────────────────
        [Header("Asteroid Prefabs")]
        [Tooltip("Assign Asteroid_00 through Asteroid_03 here with desired weights")]
        [SerializeField] private AsteroidVariant[] asteroidVariants;

        // ── Pool ───────────────────────────────────────────────────────────────
        [Header("Object Pool")]
        [Tooltip("Total number of asteroid instances kept alive at once. " +
                 "Increase if you want a denser field (poolSize ≈ density target).")]
        [SerializeField] private int poolSize = 60;

        // ── Spawn Volume ───────────────────────────────────────────────────────
        [Header("Spawn Volume")]
        [Tooltip("How far ahead of the player (along their forward axis) new asteroids are placed")]
        [SerializeField] private float spawnAheadDistance = 150f;

        [Tooltip("How far behind the player before an asteroid is recycled to the front")]
        [SerializeField] private float despawnBehindDistance = 20f;

        [Tooltip("Radius of the tube/cylinder that asteroids are placed in (X/Y spread)")]
        [SerializeField] private float fieldRadius = 40f;

        [Tooltip("Depth of the initial seeding volume so the field feels full from the start")]
        [SerializeField] private float fieldDepth = 120f;

        // ── Scale ──────────────────────────────────────────────────────────────
        [Header("Scale")]
        [SerializeField] private float minScale = 0.5f;
        [SerializeField] private float maxScale = 3.5f;

        // ── Rotation ───────────────────────────────────────────────────────────
        [Header("Rotation")]
        [SerializeField] private float minRotationSpeed = 4f;
        [SerializeField] private float maxRotationSpeed = 28f;

        // ── Drift ──────────────────────────────────────────────────────────────
        [Header("Drift (optional slow movement toward player)")]
        [Tooltip("Set both to 0 for completely static asteroids. " +
                 "Small values (0–2) give a nice parallax feeling.")]
        [SerializeField] private float minDriftSpeed = 0f;
        [SerializeField] private float maxDriftSpeed = 1.5f;

        // ── Grow-In ────────────────────────────────────────────────────────────
        [Header("Grow-In (prevents pop-in)")]
        [Tooltip("How long in seconds an asteroid takes to scale from 0 to full size when it spawns. " +
                 "0 disables the effect.")]
        [SerializeField] private float growInDuration = 0.6f;

        // ── Runtime state ──────────────────────────────────────────────────────
        private Transform playerTransform;

        private class AsteroidInstance
        {
            public GameObject go;
            public int variantIndex;
            public Vector3 rotationAxis;
            public float rotationSpeed;
            public float driftSpeed;
            public float targetScale;
            public float growTimer;   // counts up from 0 to growInDuration
        }

        private List<AsteroidInstance> active = new List<AsteroidInstance>();
        private Queue<GameObject>[] pools; // one queue per variant

        // ──────────────────────────────────────────────────────────────────────

        void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("[AsteroidFieldSpawner] No GameObject with tag 'Player' found.");

            BuildPools();

            if (playerTransform != null)
                SeedField();
        }

        void Update()
        {
            if (playerTransform == null) return;

            Vector3 playerPos = playerTransform.position;
            Vector3 playerFwd = playerTransform.forward;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var a = active[i];

                if (a.go == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                // Grow-in: scale from 0 → target over growInDuration
                if (growInDuration > 0f && a.growTimer < growInDuration)
                {
                    a.growTimer += Time.deltaTime;
                    float t = Mathf.Clamp01(a.growTimer / growInDuration);
                    float eased = t * t * (3f - 2f * t); // smooth-step ease
                    a.go.transform.localScale = Vector3.one * (a.targetScale * eased);
                }

                // Slow rotation
                a.go.transform.Rotate(a.rotationAxis, a.rotationSpeed * Time.deltaTime, Space.Self);

                // Optional drift toward player
                if (a.driftSpeed > 0f)
                    a.go.transform.position -= playerFwd * (a.driftSpeed * Time.deltaTime);

                // Recycle check: asteroid is behind the player
                float dotAhead = Vector3.Dot(a.go.transform.position - playerPos, playerFwd);
                if (dotAhead < -despawnBehindDistance)
                {
                    Recycle(a);
                    active.RemoveAt(i);
                    // Immediately spawn a replacement at the front of the field
                    SpawnOne(playerPos + playerFwd * spawnAheadDistance);
                }
            }
        }

        // ── Pool management ────────────────────────────────────────────────────

        void BuildPools()
        {
            if (asteroidVariants == null || asteroidVariants.Length == 0)
            {
                Debug.LogError("[AsteroidFieldSpawner] No asteroid variants assigned!");
                return;
            }

            pools = new Queue<GameObject>[asteroidVariants.Length];
            int perVariant = Mathf.Max(1, poolSize / asteroidVariants.Length);

            for (int i = 0; i < asteroidVariants.Length; i++)
            {
                pools[i] = new Queue<GameObject>();

                if (asteroidVariants[i].prefab == null)
                {
                    Debug.LogWarning($"[AsteroidFieldSpawner] Variant {i} has no prefab assigned.");
                    continue;
                }

                for (int j = 0; j < perVariant; j++)
                {
                    var go = Instantiate(asteroidVariants[i].prefab);
                    go.SetActive(false);
                    go.name = $"Asteroid_{i:D2}_pool_{j}";
                    pools[i].Enqueue(go);
                }
            }
        }

        GameObject GetFromPool(int variantIndex)
        {
            if (pools[variantIndex].Count > 0)
                return pools[variantIndex].Dequeue();

            // Pool exhausted — create a fresh instance (shouldn't happen with correct poolSize)
            Debug.LogWarning($"[AsteroidFieldSpawner] Pool for variant {variantIndex} exhausted. " +
                             "Consider increasing Pool Size.");
            return Instantiate(asteroidVariants[variantIndex].prefab);
        }

        void Recycle(AsteroidInstance a)
        {
            a.go.SetActive(false);
            pools[a.variantIndex].Enqueue(a.go);
        }

        // ── Seeding & spawning ─────────────────────────────────────────────────

        /// <summary>
        /// Fills the full depth of the field on startup so there are no empty frames.
        /// </summary>
        void SeedField()
        {
            Vector3 origin = playerTransform.position;
            Vector3 fwd   = playerTransform.forward;
            Vector3 right = playerTransform.right;
            Vector3 up    = playerTransform.up;

            for (int i = 0; i < poolSize; i++)
            {
                float t = (float)i / poolSize;
                Vector3 center = origin + fwd * (t * fieldDepth);
                SpawnOne(center, right, up);

                // Stagger the initial grow timers so they don't all pop in at once on Start
                if (growInDuration > 0f && active.Count > 0)
                    active[active.Count - 1].growTimer = Random.Range(0f, growInDuration);
            }
        }

        void SpawnOne(Vector3 forwardCenter)
        {
            if (playerTransform == null) return;
            SpawnOne(forwardCenter, playerTransform.right, playerTransform.up);
        }

        void SpawnOne(Vector3 center, Vector3 right, Vector3 up)
        {
            int vi = PickVariant();
            if (vi < 0) return;

            var go = GetFromPool(vi);
            if (go == null) return;

            // Place in a disc perpendicular to the player's forward direction
            Vector2 disk = Random.insideUnitCircle * fieldRadius;
            go.transform.position = center + right * disk.x + up * disk.y;
            go.transform.rotation = Random.rotation;

            float s = Random.Range(minScale, maxScale);
            // Start at zero scale; Update() will grow it in
            go.transform.localScale = growInDuration > 0f ? Vector3.zero : Vector3.one * s;

            go.SetActive(true);

            active.Add(new AsteroidInstance
            {
                go            = go,
                variantIndex  = vi,
                rotationAxis  = Random.onUnitSphere,
                rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed),
                driftSpeed    = Random.Range(minDriftSpeed, maxDriftSpeed),
                targetScale   = s,
                growTimer     = 0f
            });
        }

        /// <summary>
        /// Weighted random selection across asteroid variants.
        /// </summary>
        int PickVariant()
        {
            float total = 0f;
            foreach (var v in asteroidVariants)
                if (v.prefab != null) total += v.spawnWeight;

            if (total <= 0f) return -1;

            float r = Random.Range(0f, total);
            float cum = 0f;
            for (int i = 0; i < asteroidVariants.Length; i++)
            {
                if (asteroidVariants[i].prefab == null) continue;
                cum += asteroidVariants[i].spawnWeight;
                if (r <= cum) return i;
            }
            return 0;
        }

        // ── Editor Gizmos ──────────────────────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Vector3 fwd    = transform.forward;

            if (Application.isPlaying && playerTransform != null)
            {
                origin = playerTransform.position;
                fwd    = playerTransform.forward;
            }

            // Spawn front plane (orange)
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            DrawCircleGizmo(origin + fwd * spawnAheadDistance, fwd, fieldRadius);

            // Despawn back plane (red)
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            DrawCircleGizmo(origin - fwd * despawnBehindDistance, fwd, fieldRadius);

            // Connecting lines
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
            Vector3 perp = Vector3.Cross(fwd, Vector3.up).normalized;
            if (perp == Vector3.zero) perp = Vector3.right;
            for (int i = 0; i < 4; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 90f, fwd) * perp * fieldRadius;
                Gizmos.DrawLine(origin - fwd * despawnBehindDistance + dir,
                                origin + fwd * spawnAheadDistance    + dir);
            }
        }

        void DrawCircleGizmo(Vector3 center, Vector3 normal, float radius)
        {
            Vector3 perp = Vector3.Cross(normal, Vector3.up).normalized;
            if (perp == Vector3.zero) perp = Vector3.right;

            const int segments = 32;
            Vector3 prev = center + perp * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 360f / segments;
                Vector3 pt = center + Quaternion.AngleAxis(angle, normal) * perp * radius;
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
        }
    }
}
