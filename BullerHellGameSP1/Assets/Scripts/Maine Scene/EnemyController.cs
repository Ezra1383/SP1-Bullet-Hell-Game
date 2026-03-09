using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Random = UnityEngine.Random;

namespace BulletHell
{
    public class EnemyController : MonoBehaviour
    {
        // Fired whenever any enemy is killed — used by UI systems (combo, stats, etc.)
        public static event System.Action OnEnemyKilled;

        [Header("Damage Numbers")]
        [SerializeField] private GameObject damageNumberPrefab;

        [Header("Shooting")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float fireRate = 1f;
        [SerializeField] private bool canShoot = true;
        [SerializeField] private AudioClip shootSound;
        [Range(0f, 3f)][SerializeField] private float shootVolume = 1f;
        [SerializeField] private GameObject explosionPrefab;

        // --- Retreat tuning (tweak in Inspector via constants or expose if needed) ---
        private const float RetreatTransitionSpeed = 2.5f; // how fast blend 0→1 moves
        private const float RetreatTriggerDistance = 30f;  // world-space proximity that triggers retreat
        private const float LaneLerpSpeed          = 3f;   // how fast enemy slides to new lane
        private const float ShootBlendCutoff       = 0.55f;// stop shooting once retreatBlend exceeds this
        private const float BehindCullThreshold    = -0.01f; // cull if this far behind player (normalized T)
        private const float AheadCullThreshold     = 0.30f;  // cull if this far ahead after retreating (normalized T)

        // --- Runtime state ---
        private SplineContainer splineContainer;
        private Spline spline;
        private ProceduralEnemySpawner spawner;

        private float currentT;
        private float cachedSplineLength;
        private float moveSpeed;
        private float retreatSpeed;
        private MovementPattern movementPattern;
        private bool canRetreat;
        private bool oscillates;
        private float weaveAmplitude;
        private float weaveFrequency;
        private int health;
        private int scoreValue;

        private Transform playerTarget;
        private float nextFireTime;

        // --- Lane / lateral state ---
        private float[] laneOffsets;
        private int currentLaneIndex;
        private float currentLateralX; // actual smoothed X (lerped each frame)
        private float targetLateralX;  // X we're moving toward
        private float lateralY;        // fixed Y set at spawn

        // --- Movement pattern ---
        private float wavePhase; // per-enemy phase offset so they don't all sync

        // --- Retreat blend ---
        // 0 = fully approaching, 1 = fully retreating
        // Speed and rotation both lerp with this value, giving a smooth decelerate-flip-accelerate feel
        private bool isRetreating;
        private float retreatBlend;

        // --- Kamikaze ---
        private float detonationRadius;
        private float suicideSpeedMultiplier;

        void Start()
        {
            playerTarget = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        public void Initialize(
            SplineContainer container,
            float startT,
            EnemyType type,
            int laneIndex,
            float[] lanes,
            float spawnLateralY,
            ProceduralEnemySpawner spawnerRef)
        {
            splineContainer   = container;
            spline            = container.Spline;
            currentT          = startT;
            cachedSplineLength = ProceduralEnemySpawner.CachedSplineLength;
            spawner           = spawnerRef;

            moveSpeed         = type.moveSpeed;
            retreatSpeed      = type.retreatSpeed;
            movementPattern   = type.movementPattern;
            canRetreat        = type.canRetreat;
            oscillates        = type.oscillates;
            weaveAmplitude    = type.weaveAmplitude;
            weaveFrequency    = type.weaveFrequency;
            health            = type.health;
            scoreValue        = type.scoreValue;

            laneOffsets       = lanes;
            currentLaneIndex  = laneIndex;
            currentLateralX   = lanes[laneIndex];
            targetLateralX    = currentLateralX;
            lateralY          = spawnLateralY;

            wavePhase    = Random.Range(0f, Mathf.PI * 2f);
            isRetreating = false;
            retreatBlend = 0f;

            detonationRadius     = type.detonationRadius;
            suicideSpeedMultiplier = type.suicideSpeedMultiplier;
        }

        void Update()
        {
            if (spline == null) return;

            float playerT = ProceduralEnemySpawner.PlayerSplineT;

            // --- Retreat intent ---
            bool wantsToRetreat = canRetreat
                && playerTarget != null
                && Vector3.Distance(transform.position, playerTarget.position) < RetreatTriggerDistance;

            if (wantsToRetreat && !isRetreating)
            {
                isRetreating = true;
                PickRetreatLane();
            }
            else if (!wantsToRetreat && isRetreating && oscillates)
            {
                isRetreating = false;
                PickApproachLane();
            }

            // Blend 0→1 (approaching→retreating) drives speed, rotation, and shoot cutoff
            float targetBlend = isRetreating ? 1f : 0f;
            retreatBlend = Mathf.MoveTowards(retreatBlend, targetBlend, RetreatTransitionSpeed * Time.deltaTime);

            // --- Move along spline ---
            if (cachedSplineLength <= 0f) { ReturnToPool(); return; }

            // Kamikaze: ramp speed up as it closes in on the player
            float activeMoveSpeed = moveSpeed;
            if (movementPattern == MovementPattern.Kamikaze && playerTarget != null)
            {
                float dist    = Vector3.Distance(transform.position, playerTarget.position);
                float ramp    = 1f - Mathf.Clamp01(dist / (RetreatTriggerDistance * 0.75f));
                activeMoveSpeed = Mathf.Lerp(moveSpeed, moveSpeed * suicideSpeedMultiplier, ramp);
            }

            float approachDelta = -(activeMoveSpeed / cachedSplineLength) * Time.deltaTime;
            float retreatDelta  = +(retreatSpeed    / cachedSplineLength) * Time.deltaTime;
            currentT += Mathf.Lerp(approachDelta, retreatDelta, retreatBlend);

            // Wrap T for looping spline
            if (currentT < 0f) currentT += 1f;
            if (currentT > 1f) currentT -= 1f;

            // --- Cull checks (wrap-aware) ---
            float tDiff = currentT - playerT;
            if (tDiff >  0.5f) tDiff -= 1f; // normalize to [-0.5, 0.5]
            if (tDiff < -0.5f) tDiff += 1f;

            if (tDiff < BehindCullThreshold)  { ReturnToPool(); return; } // behind player
            if (tDiff > AheadCullThreshold)   { ReturnToPool(); return; } // retreated too far ahead

            // --- Lateral X: smooth slide toward target lane ---
            currentLateralX = Mathf.Lerp(currentLateralX, targetLateralX, LaneLerpSpeed * Time.deltaTime);

            // --- Movement patterns (oscillation layered on top of lane) ---
            float patternOffsetX = 0f;
            float t = Time.time + wavePhase;

            switch (movementPattern)
            {
                case MovementPattern.SineWave:
                    patternOffsetX = Mathf.Sin(t * weaveFrequency) * weaveAmplitude;
                    break;

                case MovementPattern.Zigzag:
                    patternOffsetX = Mathf.PingPong(t * weaveFrequency, weaveAmplitude * 2f) - weaveAmplitude;
                    break;

                case MovementPattern.CircleStrafe:
                    patternOffsetX = Mathf.Cos(t * weaveFrequency) * weaveAmplitude;
                    lateralY = Mathf.Sin(t * weaveFrequency) * weaveAmplitude;
                    break;

                case MovementPattern.FollowPlayer:
                    // Track the player's lateral position on the spline
                    if (playerTarget != null)
                        targetLateralX = EstimatePlayerLateralX();
                    break;

                case MovementPattern.Kamikaze:
                    // Track both X and Y so the enemy homes directly onto the player
                    if (playerTarget != null)
                    {
                        targetLateralX = EstimatePlayerLateralX();
                        lateralY = Mathf.Lerp(lateralY, EstimatePlayerLateralY(), 4f * Time.deltaTime);
                    }
                    break;

                case MovementPattern.SinusoidalWeave:
                    // X and Y oscillate at different frequencies, creating a 2D Lissajous-style weave
                    patternOffsetX = Mathf.Sin(t * weaveFrequency) * weaveAmplitude;
                    lateralY = Mathf.Sin(t * weaveFrequency * 0.6f + wavePhase * 0.5f) * weaveAmplitude * 0.75f;
                    break;
            }

            // --- Apply position and rotation ---
            UpdatePositionAndRotation(currentLateralX + patternOffsetX, lateralY);

            // --- Kamikaze detonation check ---
            if (movementPattern == MovementPattern.Kamikaze && playerTarget != null)
            {
                if (Vector3.Distance(transform.position, playerTarget.position) <= detonationRadius)
                {
                    SuicideDetonate();
                    return;
                }
            }

            // --- Shoot only while not significantly retreating and still ahead of the player ---
            if (canShoot && retreatBlend < ShootBlendCutoff && tDiff > 0f && Time.time >= nextFireTime)
            {
                Shoot();
                nextFireTime = Time.time + fireRate;
            }
        }

        void UpdatePositionAndRotation(float lateralX, float lateralYVal)
        {
            float clampedT = Mathf.Clamp01(currentT);

            float3 position = spline.EvaluatePosition(clampedT);
            float3 tangent  = spline.EvaluateTangent(clampedT);
            float3 up       = spline.EvaluateUpVector(clampedT);

            Vector3 worldPos      = splineContainer.transform.TransformPoint(position);
            Vector3 splineTangent = splineContainer.transform.TransformDirection(tangent);
            Vector3 splineUp      = splineContainer.transform.TransformDirection(up);
            Vector3 cross         = Vector3.Cross(splineUp, splineTangent);
            if (cross.sqrMagnitude < 0.0001f) return; // degenerate tangent at this spline point — skip frame
            Vector3 splineRight   = cross.normalized;

            transform.position = worldPos + splineRight * lateralX + splineUp * lateralYVal;

            transform.rotation = Quaternion.LookRotation(-splineTangent, splineUp);
        }

        // Pick a different lane to slide into during retreat
        void PickRetreatLane()
        {
            if (laneOffsets == null || laneOffsets.Length <= 1) return;
            int newLane;
            do { newLane = Random.Range(0, laneOffsets.Length); }
            while (newLane == currentLaneIndex);
            currentLaneIndex = newLane;
            targetLateralX   = laneOffsets[newLane];
        }

        // Pick a lane to re-enter when oscillating back toward player
        void PickApproachLane()
        {
            if (laneOffsets == null || laneOffsets.Length == 0) return;
            currentLaneIndex = Random.Range(0, laneOffsets.Length);
            targetLateralX   = laneOffsets[currentLaneIndex];
        }

        // Estimate how far left/right the player sits relative to the spline center
        float EstimatePlayerLateralX()
        {
            float playerT = Mathf.Clamp01(ProceduralEnemySpawner.PlayerSplineT);
            float3 splinePos3 = spline.EvaluatePosition(playerT);
            float3 splineTan3 = spline.EvaluateTangent(playerT);
            float3 splineUp3  = spline.EvaluateUpVector(playerT);

            Vector3 worldSplinePos = splineContainer.transform.TransformPoint(splinePos3);
            Vector3 worldTangent   = splineContainer.transform.TransformDirection(splineTan3);
            Vector3 worldUp        = splineContainer.transform.TransformDirection(splineUp3);
            Vector3 worldRight     = Vector3.Cross(worldUp, worldTangent).normalized;

            Vector3 toPlayer = playerTarget.position - worldSplinePos;
            float lateral    = Vector3.Dot(toPlayer, worldRight);

            // Clamp to lane bounds
            return Mathf.Clamp(lateral, laneOffsets[0], laneOffsets[laneOffsets.Length - 1]);
        }

        // Estimate the player's vertical (Y) offset from the spline center
        float EstimatePlayerLateralY()
        {
            if (playerTarget == null) return lateralY;

            float pT = Mathf.Clamp01(ProceduralEnemySpawner.PlayerSplineT);
            float3 splinePos3 = spline.EvaluatePosition(pT);
            float3 splineUp3  = spline.EvaluateUpVector(pT);

            Vector3 worldSplinePos = splineContainer.transform.TransformPoint(splinePos3);
            Vector3 worldUp        = splineContainer.transform.TransformDirection(splineUp3);

            Vector3 toPlayer = playerTarget.position - worldSplinePos;
            return Vector3.Dot(toPlayer, worldUp);
        }

        // Kamikaze detonation: deal damage, spawn explosion, return to pool
        void SuicideDetonate()
        {
            // Damage + hit-stop are handled by ExplosionDamage.cs on the explosion prefab.
            // No direct TakeDamage call here — avoids double-hitting the player.
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, transform.rotation);

            ReturnToPool();
        }

        void Shoot()
        {
            if (bulletPrefab == null || firePoint == null || playerTarget == null) return;

            Vector3 direction = (playerTarget.position - firePoint.position).normalized;
            GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));

            // Enemy_Bullet prefab uses Projectile.cs — must call Launch() to enable movement
            if (shootSound != null)
            {
                var src = new GameObject("SFX").AddComponent<AudioSource>();
                src.transform.position = firePoint.position;
                src.volume = shootVolume;
                src.PlayOneShot(shootSound);
                Destroy(src.gameObject, shootSound.length);
            }

            Projectile projectile = bullet.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Launch(direction);

                // Cull the bullet once it passes behind the player on the spline axis
                float3 tan = spline.EvaluateTangent(Mathf.Clamp01(currentT));
                Vector3 splineForward = splineContainer.transform.TransformDirection(tan).normalized;
                projectile.SetSplineCull(playerTarget, splineForward);
            }
        }

        public void TakeDamage(int damage)
        {
            health -= damage;

            if (damageNumberPrefab != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up;
                var dn = Instantiate(damageNumberPrefab, spawnPos, Quaternion.identity);
                dn.GetComponent<DamageNumber>()?.Play(damage, spawnPos, Camera.main);
            }

            if (health <= 0) Die();
        }

        void Die()
        {
            OnEnemyKilled?.Invoke();
            ScoreManager.Instance?.AddScore(scoreValue);

            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position, transform.rotation);

            ReturnToPool();
        }

        void ReturnToPool()
        {
            if (spawner != null)
                spawner.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
        }

    }

    public class EnemyBullet : MonoBehaviour
    {
        [SerializeField] private float speed = 20f;

        void Update()
        {
            // transform.forward is set at Instantiate time to aim at the player
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>()
                                   ?? other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(1);
                Destroy(gameObject);
            }
        }
    }
}
