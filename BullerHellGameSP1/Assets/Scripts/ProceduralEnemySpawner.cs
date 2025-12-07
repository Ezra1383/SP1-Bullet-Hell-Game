using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using Unity.Mathematics;

namespace BulletHell
{
    public class ProceduralEnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineContainer spline;
        [SerializeField] private Transform splineFollower; // The object moving along spline (your followTarget)
        [SerializeField] private SplineAnimate splineAnimate; // NEW: Direct reference to SplineAnimate component

        [Header("Enemy Prefabs")]
        [SerializeField] private EnemyType[] enemyTypes;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnDistance = 60f; // Distance ahead of player
        [SerializeField] private float minSpawnInterval = 0.5f;
        [SerializeField] private float maxSpawnInterval = 2f;
        [SerializeField] private Vector2 spawnAreaWidth = new Vector2(-15f, 15f); // Left/Right bounds
        [SerializeField] private Vector2 spawnAreaHeight = new Vector2(-8f, 8f); // Up/Down bounds

        [Header("Difficulty Scaling")]
        [SerializeField] private float difficultyIncreaseRate = 0.1f; // Per minute
        [SerializeField] private float maxDifficulty = 10f;
        [SerializeField] private AnimationCurve difficultySpawnRateCurve = AnimationCurve.Linear(0, 0, 10, 1);
        [SerializeField] private AnimationCurve difficultyEnemyCountCurve = AnimationCurve.Linear(0, 1, 10, 5);

        [Header("Wave Patterns")]
        [SerializeField] private bool useWaveSystem = true;
        [SerializeField] private float waveDuration = 15f;
        [SerializeField] private float waveBreakDuration = 3f;

        private float currentDifficulty = 1f;
        private float gameTime = 0f;
        private float nextSpawnTime;
        private float playerSplineT; // Normalized position (0-1)
        private bool isInWaveBreak = false;
        private float waveTimer = 0f;

        // Object pooling
        private Dictionary<string, Queue<GameObject>> enemyPools = new Dictionary<string, Queue<GameObject>>();

        void Start()
        {
            InitializePools();
            nextSpawnTime = Time.time + UnityEngine.Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        void Update()
        {
            gameTime += Time.deltaTime;
            UpdateDifficulty();
            UpdatePlayerProgress();

            if (useWaveSystem)
            {
                HandleWaveSystem();
            }
            else
            {
                HandleContinuousSpawning();
            }
        }

        void UpdateDifficulty()
        {
            // Gradually increase difficulty over time
            currentDifficulty = Mathf.Min(1f + (gameTime / 60f) * difficultyIncreaseRate, maxDifficulty);
        }

        void UpdatePlayerProgress()
        {
            if (spline == null) return;

            // Option 1: If SplineAnimate is assigned, use its normalized time directly
            if (splineAnimate != null)
            {
                playerSplineT = splineAnimate.NormalizedTime;
                return;
            }

            // Option 2: Fallback to calculating from position
            if (splineFollower != null)
            {
                Spline splineData = spline.Spline;
                float3 followerPos = splineFollower.position;
                SplineUtility.GetNearestPoint(splineData, followerPos, out float3 nearestPoint, out float t);
                playerSplineT = t;
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
            }
            else
            {
                if (waveTimer >= waveDuration)
                {
                    isInWaveBreak = true;
                    waveTimer = 0f;
                }
                else if (Time.time >= nextSpawnTime)
                {
                    SpawnProceduralWave();
                    float spawnRate = difficultySpawnRateCurve.Evaluate(currentDifficulty);
                    nextSpawnTime = Time.time + Mathf.Lerp(maxSpawnInterval, minSpawnInterval, spawnRate);
                }
            }
        }

        void HandleContinuousSpawning()
        {
            if (Time.time >= nextSpawnTime)
            {
                SpawnProceduralWave();
                float spawnRate = difficultySpawnRateCurve.Evaluate(currentDifficulty);
                nextSpawnTime = Time.time + Mathf.Lerp(maxSpawnInterval, minSpawnInterval, spawnRate);
            }
        }

        void SpawnProceduralWave()
        {
            // Determine spawn pattern based on difficulty
            SpawnPattern pattern = ChooseRandomPattern();

            int enemyCount = Mathf.RoundToInt(difficultyEnemyCountCurve.Evaluate(currentDifficulty) * UnityEngine.Random.Range(1, 4));

            switch (pattern)
            {
                case SpawnPattern.Single:
                    SpawnSingleEnemy();
                    break;
                case SpawnPattern.HorizontalLine:
                    SpawnHorizontalLine(enemyCount);
                    break;
                case SpawnPattern.VerticalLine:
                    SpawnVerticalLine(enemyCount);
                    break;
                case SpawnPattern.VFormation:
                    SpawnVFormation(enemyCount);
                    break;
                case SpawnPattern.Circle:
                    SpawnCircle(enemyCount);
                    break;
                case SpawnPattern.Random:
                    SpawnRandomCluster(enemyCount);
                    break;
                case SpawnPattern.Zigzag:
                    SpawnZigzag(enemyCount);
                    break;
            }
        }

        SpawnPattern ChooseRandomPattern()
        {
            // Weight patterns based on difficulty
            float rand = UnityEngine.Random.value;

            if (currentDifficulty < 2f)
            {
                return rand < 0.6f ? SpawnPattern.Single : SpawnPattern.HorizontalLine;
            }
            else if (currentDifficulty < 4f)
            {
                if (rand < 0.3f) return SpawnPattern.HorizontalLine;
                if (rand < 0.6f) return SpawnPattern.VFormation;
                return SpawnPattern.Random;
            }
            else
            {
                // Higher difficulty - all patterns available
                return (SpawnPattern)UnityEngine.Random.Range(0, System.Enum.GetValues(typeof(SpawnPattern)).Length);
            }
        }

        void SpawnSingleEnemy()
        {
            Vector2 randomOffset = new Vector2(
                UnityEngine.Random.Range(spawnAreaWidth.x, spawnAreaWidth.y),
                UnityEngine.Random.Range(spawnAreaHeight.x, spawnAreaHeight.y)
            );

            SpawnEnemyAtOffset(randomOffset, GetRandomEnemyType());
        }

        void SpawnHorizontalLine(int count)
        {
            float spacing = (spawnAreaWidth.y - spawnAreaWidth.x) / (count + 1);
            float yPos = UnityEngine.Random.Range(spawnAreaHeight.x, spawnAreaHeight.y);

            for (int i = 0; i < count; i++)
            {
                float xPos = spawnAreaWidth.x + spacing * (i + 1);
                SpawnEnemyAtOffset(new Vector2(xPos, yPos), GetRandomEnemyType());
            }
        }

        void SpawnVerticalLine(int count)
        {
            float spacing = (spawnAreaHeight.y - spawnAreaHeight.x) / (count + 1);
            float xPos = UnityEngine.Random.Range(spawnAreaWidth.x, spawnAreaWidth.y);

            for (int i = 0; i < count; i++)
            {
                float yPos = spawnAreaHeight.x + spacing * (i + 1);
                SpawnEnemyAtOffset(new Vector2(xPos, yPos), GetRandomEnemyType());
            }
        }

        void SpawnVFormation(int count)
        {
            float angleSpread = 60f;
            float spacing = 5f;

            for (int i = 0; i < count; i++)
            {
                float angle = (i - count / 2f) * (angleSpread / count) * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Sin(angle) * spacing * i, Mathf.Cos(angle) * spacing * i);
                SpawnEnemyAtOffset(offset, GetRandomEnemyType());
            }
        }

        void SpawnCircle(int count)
        {
            float radius = UnityEngine.Random.Range(5f, 10f);

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * 360f * Mathf.Deg2Rad;
                Vector2 offset = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                SpawnEnemyAtOffset(offset, GetRandomEnemyType());
            }
        }

        void SpawnRandomCluster(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 randomOffset = new Vector2(
                    UnityEngine.Random.Range(spawnAreaWidth.x, spawnAreaWidth.y),
                    UnityEngine.Random.Range(spawnAreaHeight.x, spawnAreaHeight.y)
                );
                SpawnEnemyAtOffset(randomOffset, GetRandomEnemyType());
            }
        }

        void SpawnZigzag(int count)
        {
            float xSpacing = (spawnAreaWidth.y - spawnAreaWidth.x) / count;

            for (int i = 0; i < count; i++)
            {
                float xPos = spawnAreaWidth.x + xSpacing * i;
                float yPos = (i % 2 == 0) ? spawnAreaHeight.x : spawnAreaHeight.y;
                SpawnEnemyAtOffset(new Vector2(xPos, yPos), GetRandomEnemyType());
            }
        }

        void SpawnEnemyAtOffset(Vector2 lateralOffset, EnemyType enemyType)
        {
            if (spline == null) return;

            Spline splineData = spline.Spline;

            // Spawn ahead of player using percentage of spline (0-1 range)
            // spawnDistance is in world units, but we work in normalized 0-1 space
            float splineLength = splineData.GetLength();
            float normalizedDistance = (spawnDistance / splineLength);

            float spawnT = playerSplineT + normalizedDistance;

            // Handle wrapping for looping splines
            if (spawnT > 1f)
                spawnT = spawnT - Mathf.Floor(spawnT); // Keeps decimal part
            if (spawnT < 0f)
                spawnT = 1f + spawnT;

            // Evaluate spline at position
            float3 position = splineData.EvaluatePosition(spawnT);
            float3 tangent = splineData.EvaluateTangent(spawnT);
            float3 up = splineData.EvaluateUpVector(spawnT);

            // Convert from spline's local space to world space
            Vector3 localPos = position;
            Vector3 worldPos = spline.transform.TransformPoint(localPos);

            // Calculate right vector in world space
            Vector3 splineTangent = spline.transform.TransformDirection(tangent);
            Vector3 splineUp = spline.transform.TransformDirection(up);
            Vector3 splineRight = Vector3.Cross(splineUp, splineTangent).normalized;

            Vector3 finalPos = worldPos +
                              splineRight * lateralOffset.x +
                              splineUp * lateralOffset.y;

            GameObject enemy = GetPooledEnemy(enemyType.prefab.name);
            if (enemy == null)
            {
                enemy = Instantiate(enemyType.prefab);
            }

            enemy.transform.position = finalPos;
            enemy.SetActive(true);

            // Initialize enemy
            EnemyController controller = enemy.GetComponent<EnemyController>();
            if (controller != null)
            {
                controller.Initialize(spline, spawnT, enemyType.moveSpeed, enemyType.movementPattern);
            }
        }

        EnemyType GetRandomEnemyType()
        {
            // Weight enemy types based on difficulty
            float totalWeight = 0f;
            foreach (var type in enemyTypes)
            {
                if (currentDifficulty >= type.minDifficulty)
                    totalWeight += type.spawnWeight;
            }

            float randomValue = UnityEngine.Random.value * totalWeight;
            float cumulativeWeight = 0f;

            foreach (var type in enemyTypes)
            {
                if (currentDifficulty >= type.minDifficulty)
                {
                    cumulativeWeight += type.spawnWeight;
                    if (randomValue <= cumulativeWeight)
                        return type;
                }
            }

            return enemyTypes[0];
        }

        // Object Pooling
        void InitializePools()
        {
            foreach (var enemyType in enemyTypes)
            {
                enemyPools[enemyType.prefab.name] = new Queue<GameObject>();

                for (int i = 0; i < enemyType.poolSize; i++)
                {
                    GameObject obj = Instantiate(enemyType.prefab);
                    obj.SetActive(false);
                    enemyPools[enemyType.prefab.name].Enqueue(obj);
                }
            }
        }

        GameObject GetPooledEnemy(string enemyName)
        {
            if (enemyPools.ContainsKey(enemyName) && enemyPools[enemyName].Count > 0)
            {
                GameObject obj = enemyPools[enemyName].Dequeue();
                return obj;
            }
            return null;
        }

        public void ReturnToPool(GameObject enemy)
        {
            enemy.SetActive(false);
            string cleanName = enemy.name.Replace("(Clone)", "").Trim();
            if (enemyPools.ContainsKey(cleanName))
            {
                enemyPools[cleanName].Enqueue(enemy);
            }
        }

        // Debug - Visualize spawn area
        void OnDrawGizmosSelected()
        {
            if (spline == null)
            {
                Debug.LogWarning("EnemySpawner: Spline not assigned!");
                return;
            }

            if (splineFollower == null)
            {
                Debug.LogWarning("EnemySpawner: Spline Follower not assigned!");
                return;
            }

            Spline splineData = spline.Spline;
            float splineLength = splineData.GetLength();

            // Use current player position if playing, otherwise show at middle of spline
            float testT = Application.isPlaying ? playerSplineT : 0.5f;
            float spawnT = testT + (spawnDistance / splineLength);
            if (spawnT > 1f) spawnT = spawnT % 1f;

            float3 position = splineData.EvaluatePosition(spawnT);
            float3 tangent = splineData.EvaluateTangent(spawnT);
            float3 up = splineData.EvaluateUpVector(spawnT);

            // Convert from spline's local space to world space
            Vector3 localPos = position;
            Vector3 worldPos = spline.transform.TransformPoint(localPos);

            Vector3 splineTangent = spline.transform.TransformDirection(tangent);
            Vector3 splineUp = spline.transform.TransformDirection(up);
            Vector3 splineRight = Vector3.Cross(splineUp, splineTangent).normalized;
            Vector3 splinePos = worldPos;

            // Draw spawn area boundaries in yellow
            Gizmos.color = Color.yellow;

            Vector3 topLeft = splinePos + splineRight * spawnAreaWidth.x + splineUp * spawnAreaHeight.y;
            Vector3 topRight = splinePos + splineRight * spawnAreaWidth.y + splineUp * spawnAreaHeight.y;
            Vector3 bottomLeft = splinePos + splineRight * spawnAreaWidth.x + splineUp * spawnAreaHeight.x;
            Vector3 bottomRight = splinePos + splineRight * spawnAreaWidth.y + splineUp * spawnAreaHeight.x;

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);

            // Draw center sphere for easy visibility
            Gizmos.DrawWireSphere(splinePos, 2f);

            // Draw line from follower to spawn point
            Gizmos.color = Color.cyan;
            if (splineFollower != null)
            {
                Gizmos.DrawLine(splineFollower.position, splinePos);
            }
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
        public float moveSpeed = 5f;
        public MovementPattern movementPattern;
    }

    public enum SpawnPattern
    {
        Single,
        HorizontalLine,
        VerticalLine,
        VFormation,
        Circle,
        Random,
        Zigzag
    }

    public enum MovementPattern
    {
        Straight,
        SineWave,
        CircleStrafe,
        Zigzag,
        FollowPlayer
    }
}