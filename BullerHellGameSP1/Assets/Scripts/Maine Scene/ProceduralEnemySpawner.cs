using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;
using Random = UnityEngine.Random;

namespace BulletHell
{
    public class ProceduralEnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineContainer spline;
        [SerializeField] private SplineAnimate splineAnimate;
        [SerializeField] private Transform splineFollower;

        [Header("Enemy Prefabs")]
        [SerializeField] private EnemyType[] enemyTypes;

        [Header("Lane Settings")]
        [SerializeField] private float[] laneOffsets = { -15f, -7.5f, 0f, 7.5f, 15f };
        [SerializeField] private Vector2 spawnAreaHeight = new Vector2(-8f, 8f);
        [SerializeField] private float spawnDistance = 60f;

        [Header("Spawn Timing")]
        [SerializeField] private float minSpawnInterval = 0.5f;
        [SerializeField] private float maxSpawnInterval = 2f;

        [Header("Difficulty Scaling")]
        [SerializeField] private float difficultyIncreaseRate = 1.5f; // per minute - reaches diff 2 in ~40s, diff 10 in ~6 min
        [SerializeField] private float maxDifficulty = 10f;
        [SerializeField] private AnimationCurve difficultySpawnRateCurve = AnimationCurve.Linear(0, 0, 10, 1);
        [SerializeField] private AnimationCurve difficultyEnemyCountCurve = AnimationCurve.Linear(0, 1, 10, 5);

        [Header("Wave Settings")]
        [SerializeField] private bool useWaveSystem = true;
        [SerializeField] private float waveDuration = 15f;
        [SerializeField] private float waveBreakDuration = 3f;

        // Shared state read by all EnemyControllers - avoids per-enemy spline queries
        public static float PlayerSplineT { get; private set; }
        public static float CachedSplineLength { get; private set; }

        private float currentDifficulty = 1f;
        private float gameTime = 0f;
        private float nextSpawnTime;
        private bool isInWaveBreak = false;
        private float waveTimer = 0f;

        private Dictionary<string, Queue<GameObject>> enemyPools = new Dictionary<string, Queue<GameObject>>();

        void Start()
        {
            if (enemyTypes == null || enemyTypes.Length == 0)
            {
                Debug.LogError("ProceduralEnemySpawner: no enemy types configured!", this);
                enabled = false;
                return;
            }

            if (spline != null)
                CachedSplineLength = spline.Spline.GetLength();

            InitializePools();
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        void Update()
        {
            gameTime += Time.deltaTime;
            UpdateDifficulty();
            UpdatePlayerProgress();

            if (useWaveSystem)
                HandleWaveSystem();
            else
                HandleContinuousSpawning();
        }

        void UpdateDifficulty()
        {
            currentDifficulty = Mathf.Min(1f + (gameTime / 60f) * difficultyIncreaseRate, maxDifficulty);
        }

        void UpdatePlayerProgress()
        {
            if (spline == null) return;

            if (splineAnimate != null)
            {
                PlayerSplineT = splineAnimate.NormalizedTime;
                return;
            }

            if (splineFollower != null)
            {
                float3 followerPos = splineFollower.position;
                SplineUtility.GetNearestPoint(spline.Spline, followerPos, out _, out float t);
                PlayerSplineT = t;
            }
        }

        void HandleWaveSystem()
        {
            waveTimer += Time.deltaTime;

            if (isInWaveBreak)
            {
                if (waveTimer >= waveBreakDuration)
                {
                    isInWaveBreak = false;
                    waveTimer = 0f;
                }
                return;
            }

            if (waveTimer >= waveDuration)
            {
                isInWaveBreak = true;
                waveTimer = 0f;
                return;
            }

            if (Time.time >= nextSpawnTime)
                SpawnWave();
        }

        void HandleContinuousSpawning()
        {
            if (Time.time >= nextSpawnTime)
                SpawnWave();
        }

        void SpawnWave()
        {
            SpawnPattern pattern = ChoosePattern();
            int count = Mathf.Max(1, Mathf.RoundToInt(difficultyEnemyCountCurve.Evaluate(currentDifficulty) * Random.Range(1, 4)));

            switch (pattern)
            {
                case SpawnPattern.Single:         SpawnSingle();              break;
                case SpawnPattern.HorizontalLine: SpawnHorizontalLine(count); break;
                case SpawnPattern.VerticalLine:   SpawnVerticalLine(count);   break;
                case SpawnPattern.VFormation:     SpawnVFormation(count);     break;
                case SpawnPattern.Circle:         SpawnCircle(count);         break;
                case SpawnPattern.Random:         SpawnRandomCluster(count);  break;
                case SpawnPattern.Zigzag:         SpawnZigzag(count);         break;
            }

            float spawnRate = difficultySpawnRateCurve.Evaluate(currentDifficulty);
            nextSpawnTime = Time.time + Mathf.Lerp(maxSpawnInterval, minSpawnInterval, spawnRate);
        }

        SpawnPattern ChoosePattern()
        {
            float rand = Random.value;

            if (currentDifficulty < 2f)
                return rand < 0.6f ? SpawnPattern.Single : SpawnPattern.HorizontalLine;

            if (currentDifficulty < 4f)
            {
                if (rand < 0.3f) return SpawnPattern.HorizontalLine;
                if (rand < 0.6f) return SpawnPattern.VFormation;
                return SpawnPattern.Random;
            }

            // High difficulty: weighted toward complex patterns
            if (rand < 0.15f) return SpawnPattern.Single;
            if (rand < 0.30f) return SpawnPattern.HorizontalLine;
            if (rand < 0.50f) return SpawnPattern.VFormation;
            if (rand < 0.65f) return SpawnPattern.Circle;
            if (rand < 0.80f) return SpawnPattern.Zigzag;
            return SpawnPattern.Random;
        }

        // --- Spawn Patterns ---

        void SpawnSingle()
        {
            int lane = Random.Range(0, laneOffsets.Length);
            float y = Random.Range(spawnAreaHeight.x, spawnAreaHeight.y);
            SpawnEnemy(lane, y, GetRandomEnemyType());
        }

        void SpawnHorizontalLine(int count)
        {
            // One enemy per lane, capped to lane count
            int[] lanes = PickRandomLanes(Mathf.Min(count, laneOffsets.Length));
            float y = Random.Range(spawnAreaHeight.x, spawnAreaHeight.y);
            foreach (int lane in lanes)
                SpawnEnemy(lane, y, GetRandomEnemyType());
        }

        void SpawnVerticalLine(int count)
        {
            int lane = Random.Range(0, laneOffsets.Length);
            float spacing = (spawnAreaHeight.y - spawnAreaHeight.x) / (count + 1);
            for (int i = 0; i < count; i++)
            {
                float y = spawnAreaHeight.x + spacing * (i + 1);
                SpawnEnemy(lane, y, GetRandomEnemyType());
            }
        }

        void SpawnVFormation(int count)
        {
            // Expands outward from center: center, left1, right1, left2, right2...
            int center = laneOffsets.Length / 2;
            for (int i = 0; i < count && i < laneOffsets.Length; i++)
            {
                int offset = i / 2 + (i % 2 == 0 ? 0 : 1);
                int laneIndex = (i % 2 == 0) ? center - i / 2 : center + offset;
                laneIndex = Mathf.Clamp(laneIndex, 0, laneOffsets.Length - 1);
                float y = i * 1.5f; // slight depth stagger gives V shape
                SpawnEnemy(laneIndex, y, GetRandomEnemyType());
            }
        }

        void SpawnCircle(int count)
        {
            float radius = Random.Range(5f, 8f);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                int lane = NearestLane(x);
                SpawnEnemy(lane, y, GetRandomEnemyType());
            }
        }

        void SpawnRandomCluster(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int lane = Random.Range(0, laneOffsets.Length);
                float y = Random.Range(spawnAreaHeight.x, spawnAreaHeight.y);
                SpawnEnemy(lane, y, GetRandomEnemyType());
            }
        }

        void SpawnZigzag(int count)
        {
            // Alternates between leftmost and rightmost lanes
            for (int i = 0; i < count; i++)
            {
                int lane = (i % 2 == 0) ? 0 : laneOffsets.Length - 1;
                float y = Random.Range(spawnAreaHeight.x, spawnAreaHeight.y);
                SpawnEnemy(lane, y, GetRandomEnemyType());
            }
        }

        // --- Core Spawn ---

        void SpawnEnemy(int laneIndex, float lateralY, EnemyType enemyType)
        {
            if (spline == null || enemyType.prefab == null) return;

            float normalizedDistance = spawnDistance / CachedSplineLength;
            float spawnT = (PlayerSplineT + normalizedDistance) % 1f;

            Spline splineData = spline.Spline;
            float3 position = splineData.EvaluatePosition(spawnT);
            float3 tangent  = splineData.EvaluateTangent(spawnT);
            float3 up       = splineData.EvaluateUpVector(spawnT);

            Vector3 worldPos     = spline.transform.TransformPoint(position);
            Vector3 splineTangent = spline.transform.TransformDirection(tangent);
            Vector3 splineUp     = spline.transform.TransformDirection(up);
            Vector3 splineRight  = Vector3.Cross(splineUp, splineTangent).normalized;

            float lateralX = laneOffsets[laneIndex];
            Vector3 finalPos = worldPos + splineRight * lateralX + splineUp * lateralY;

            GameObject enemy = GetPooledEnemy(enemyType.prefab.name) ?? Instantiate(enemyType.prefab);
            enemy.transform.position = finalPos;
            enemy.SetActive(true);

            EnemyController controller = enemy.GetComponent<EnemyController>();
            controller?.Initialize(spline, spawnT, enemyType, laneIndex, laneOffsets, lateralY, this);
        }

        // --- Enemy Type Selection ---

        EnemyType GetRandomEnemyType()
        {
            float totalWeight = 0f;
            foreach (var type in enemyTypes)
                if (currentDifficulty >= type.minDifficulty)
                    totalWeight += type.spawnWeight;

            float rand = Random.value * totalWeight;
            float cumulative = 0f;
            foreach (var type in enemyTypes)
            {
                if (currentDifficulty >= type.minDifficulty)
                {
                    cumulative += type.spawnWeight;
                    if (rand <= cumulative) return type;
                }
            }
            return enemyTypes[0];
        }

        // --- Helpers ---

        int[] PickRandomLanes(int count)
        {
            List<int> all = new List<int>();
            for (int i = 0; i < laneOffsets.Length; i++) all.Add(i);

            int[] result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int idx = Random.Range(0, all.Count);
                result[i] = all[idx];
                all.RemoveAt(idx);
            }
            return result;
        }

        int NearestLane(float xPos)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < laneOffsets.Length; i++)
            {
                float d = Mathf.Abs(laneOffsets[i] - xPos);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        // --- Object Pooling ---

        void InitializePools()
        {
            foreach (var type in enemyTypes)
            {
                if (type.prefab == null) continue;
                enemyPools[type.prefab.name] = new Queue<GameObject>();
                for (int i = 0; i < type.poolSize; i++)
                {
                    GameObject obj = Instantiate(type.prefab);
                    obj.SetActive(false);
                    enemyPools[type.prefab.name].Enqueue(obj);
                }
            }
        }

        GameObject GetPooledEnemy(string prefabName)
        {
            return enemyPools.TryGetValue(prefabName, out var pool) && pool.Count > 0
                ? pool.Dequeue()
                : null;
        }

        public void ReturnToPool(GameObject enemy)
        {
            enemy.SetActive(false);
            string key = enemy.name.Replace("(Clone)", "").Trim();
            if (enemyPools.TryGetValue(key, out var pool))
                pool.Enqueue(enemy);
        }

        // --- Gizmos ---

        void OnDrawGizmosSelected()
        {
            if (spline == null) return;

            Spline splineData = spline.Spline;
            float splineLength = Application.isPlaying ? CachedSplineLength : splineData.GetLength();
            if (splineLength <= 0f) return;

            float testT = Application.isPlaying ? PlayerSplineT : 0.5f;
            float spawnT = (testT + spawnDistance / splineLength) % 1f;

            float3 pos = splineData.EvaluatePosition(spawnT);
            float3 tan = splineData.EvaluateTangent(spawnT);
            float3 up  = splineData.EvaluateUpVector(spawnT);

            Vector3 worldPos    = spline.transform.TransformPoint(pos);
            Vector3 splineUp    = spline.transform.TransformDirection(up);
            Vector3 splineRight = Vector3.Cross(splineUp, spline.transform.TransformDirection(tan)).normalized;

            Gizmos.color = Color.yellow;
            foreach (float lane in laneOffsets)
            {
                Vector3 center = worldPos + splineRight * lane;
                Gizmos.DrawWireSphere(center, 1f);
                Gizmos.DrawLine(center + splineUp * spawnAreaHeight.y, center + splineUp * spawnAreaHeight.x);
            }

            Gizmos.color = Color.cyan;
            if (splineFollower != null)
                Gizmos.DrawLine(splineFollower.position, worldPos);
        }
    }

    [System.Serializable]
    public class EnemyType
    {
        public string name;
        public GameObject prefab;
        public float spawnWeight = 1f;
        public float minDifficulty = 1f;
        public int poolSize = 10;
        public int health = 3;
        public int scoreValue = 100;
        public float moveSpeed = 5f;
        public float retreatSpeed = 12f;
        public MovementPattern movementPattern;
        public bool canRetreat = true;
        public bool oscillates = false;
        public float weaveAmplitude = 6f;  // how wide/tall the weave travels
        public float weaveFrequency = 3f;  // how fast it oscillates

        [Header("Kamikaze (only used when MovementPattern = Kamikaze)")]
        [Tooltip("World-space radius at which the enemy detonates and deals 1 damage")]
        public float detonationRadius = 3f;
        [Tooltip("Extra speed multiplier applied at point-blank range (ramps from 1x → this value as it closes in)")]
        public float suicideSpeedMultiplier = 2.5f;
    }

    public enum SpawnPattern
    {
        Single, HorizontalLine, VerticalLine, VFormation, Circle, Random, Zigzag
    }

    public enum MovementPattern
    {
        Straight, SineWave, CircleStrafe, Zigzag, FollowPlayer, SinusoidalWeave, Kamikaze
    }
}
