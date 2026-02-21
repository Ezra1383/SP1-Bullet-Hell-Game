using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

namespace BulletHell
{
    /// <summary>
    /// Spawns background ships at random intervals and positions to create an atmospheric
    /// "space war" happening around the player. Ships are purely visual/atmospheric.
    /// </summary>
    public class BackgroundSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Prefab of the background ship to spawn")]
        [SerializeField] private GameObject backgroundShipPrefab;

        [Tooltip("Minimum time between spawns in seconds")]
        [SerializeField] private float minSpawnInterval = 2f;

        [Tooltip("Maximum time between spawns in seconds")]
        [SerializeField] private float maxSpawnInterval = 4f;

        [Tooltip("Maximum number of background ships that can exist at once")]
        [SerializeField] private int maxActiveShips = 10;

        [Tooltip("How many ships to spawn each interval")]
        [SerializeField] private int spawnCountPerInterval = 1;

        [Header("Spawn Area")]
        [Tooltip("Minimum bounds for spawn positions (relative to this spawner)")]
        [SerializeField] private Vector3 spawnAreaMin = new Vector3(-50f, -30f, 0f);

        [Tooltip("Maximum bounds for spawn positions (relative to this spawner)")]
        [SerializeField] private Vector3 spawnAreaMax = new Vector3(50f, 30f, 100f);

        [Header("Ship Behavior")]
        [Tooltip("Minimum speed for spawned ships")]
        [SerializeField] private float minShipSpeed = 10f;

        [Tooltip("Maximum speed for spawned ships")]
        [SerializeField] private float maxShipSpeed = 20f;

        [Tooltip("Possible movement directions for ships")]
        [SerializeField] private List<MovementDirection> allowedDirections = new List<MovementDirection>
        {
            MovementDirection.LeftToRight,
            MovementDirection.RightToLeft,
            MovementDirection.TopToBottom,
            MovementDirection.BottomToTop,
            MovementDirection.DiagonalDownRight,
            MovementDirection.DiagonalDownLeft
        };

        [Header("Follow Player")]
        [Tooltip("If true, spawner follows the player's position")]
        [SerializeField] private bool followPlayer = true;

        [Tooltip("Offset from player position when following")]
        [SerializeField] private Vector3 playerOffset = new Vector3(0f, 0f, 50f);

        [Tooltip("Assign the Main Camera here so the spawn area rotates with the camera view")]
        [SerializeField] private Transform orientTarget;

        [Header("Spline Distance")]
        [Tooltip("Assign the same SplineContainer used by the enemy spawner")]
        [SerializeField] private SplineContainer splineContainer;

        [Tooltip("Assign the SplineAnimate component on the rail follower")]
        [SerializeField] private SplineAnimate splineAnimate;

        [Tooltip("Distance ahead of the player along the spline (in world units) where the spawn zone is placed. Set 0 to use the flat playerOffset instead.")]
        [SerializeField] private float spawnDistance = 0f;

        private float nextSpawnTime;
        private Transform playerTarget;
        private List<GameObject> activeShips = new List<GameObject>();

        /// <summary>
        /// Movement direction options for background ships.
        /// </summary>
        public enum MovementDirection
        {
            LeftToRight,
            RightToLeft,
            TopToBottom,
            BottomToTop,
            DiagonalDownRight,
            DiagonalDownLeft,
            DiagonalUpRight,
            DiagonalUpLeft
        }

        void Start()
        {
            if (followPlayer)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerTarget = player.transform;
                }
            }

            ScheduleNextSpawn();
        }

        void Update()
        {
            // Follow player if enabled
            if (followPlayer && playerTarget != null)
            {
                if (orientTarget != null)
                    transform.rotation = orientTarget.rotation;

                if (spawnDistance > 0f && splineContainer != null && splineAnimate != null)
                {
                    // Place the spawn zone at a fixed distance ahead along the spline
                    Spline spline = splineContainer.Spline;
                    float splineLength = spline.GetLength();
                    float playerT = splineAnimate.NormalizedTime;
                    float targetT = playerT + (spawnDistance / splineLength);
                    if (targetT > 1f) targetT -= Mathf.Floor(targetT);

                    var splinePos = spline.EvaluatePosition(targetT);
                    transform.position = splineContainer.transform.TransformPoint((Vector3)splinePos);
                }
                else
                {
                    // Fallback: flat offset in local space
                    transform.position = playerTarget.position + transform.TransformDirection(playerOffset);
                }
            }

            // Clean up destroyed ships from active list
            activeShips.RemoveAll(ship => ship == null);

            // Spawn new ships if it's time and we haven't hit the limit
            if (Time.time >= nextSpawnTime && activeShips.Count < maxActiveShips)
            {
                for (int i = 0; i < spawnCountPerInterval; i++)
                {
                    if (activeShips.Count >= maxActiveShips) break;
                    SpawnBackgroundShip();
                }
                ScheduleNextSpawn();
            }
        }

        /// <summary>
        /// Schedules the next spawn at a random interval.
        /// </summary>
        private void ScheduleNextSpawn()
        {
            float randomInterval = Random.Range(minSpawnInterval, maxSpawnInterval);
            nextSpawnTime = Time.time + randomInterval;
        }

        /// <summary>
        /// Spawns a background ship with random position, direction, and speed.
        /// </summary>
        private void SpawnBackgroundShip()
        {
            if (backgroundShipPrefab == null) return;
            if (allowedDirections.Count == 0) return;

            // Choose random direction
            MovementDirection direction = allowedDirections[Random.Range(0, allowedDirections.Count)];

            // Get spawn position and movement vector based on direction
            Vector3 spawnPosition = GetSpawnPosition(direction);
            Vector3 movementVector = GetMovementVector(direction);

            // Instantiate ship
            GameObject ship = Instantiate(
                backgroundShipPrefab,
                transform.TransformPoint(spawnPosition),
                Quaternion.identity
            );

            // Initialize ship - rotate movement vector to match camera orientation
            BackgroundShip shipScript = ship.GetComponent<BackgroundShip>();
            if (shipScript != null)
            {
                float randomSpeed = Random.Range(minShipSpeed, maxShipSpeed);
                Vector3 worldMovement = transform.TransformDirection(movementVector);
                shipScript.Initialize(worldMovement, randomSpeed);
            }

            // Add to active ships list
            activeShips.Add(ship);
        }

        /// <summary>
        /// Gets a spawn position appropriate for the given movement direction.
        /// Ships spawn at the edges of the spawn area based on their direction.
        /// </summary>
        /// <param name="direction">The movement direction</param>
        /// <returns>Local spawn position</returns>
        private Vector3 GetSpawnPosition(MovementDirection direction)
        {
            Vector3 position = Vector3.zero;

            switch (direction)
            {
                case MovementDirection.LeftToRight:
                    // Spawn on left edge
                    position.x = spawnAreaMin.x;
                    position.y = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.RightToLeft:
                    // Spawn on right edge
                    position.x = spawnAreaMax.x;
                    position.y = Random.Range(spawnAreaMin.y, spawnAreaMax.y);
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.TopToBottom:
                    // Spawn on top edge
                    position.x = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
                    position.y = spawnAreaMax.y;
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.BottomToTop:
                    // Spawn on bottom edge
                    position.x = Random.Range(spawnAreaMin.x, spawnAreaMax.x);
                    position.y = spawnAreaMin.y;
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.DiagonalDownRight:
                    // Spawn on top-left
                    position.x = spawnAreaMin.x;
                    position.y = spawnAreaMax.y;
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.DiagonalDownLeft:
                    // Spawn on top-right
                    position.x = spawnAreaMax.x;
                    position.y = spawnAreaMax.y;
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.DiagonalUpRight:
                    // Spawn on bottom-left
                    position.x = spawnAreaMin.x;
                    position.y = spawnAreaMin.y;
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;

                case MovementDirection.DiagonalUpLeft:
                    // Spawn on bottom-right
                    position.x = spawnAreaMax.x;
                    position.y = spawnAreaMin.y;
                    position.z = Random.Range(spawnAreaMin.z, spawnAreaMax.z);
                    break;
            }

            return position;
        }

        /// <summary>
        /// Gets the movement vector for a given direction.
        /// </summary>
        /// <param name="direction">The movement direction</param>
        /// <returns>Normalized movement vector</returns>
        private Vector3 GetMovementVector(MovementDirection direction)
        {
            switch (direction)
            {
                case MovementDirection.LeftToRight:
                    return Vector3.right;

                case MovementDirection.RightToLeft:
                    return Vector3.left;

                case MovementDirection.TopToBottom:
                    return Vector3.down;

                case MovementDirection.BottomToTop:
                    return Vector3.up;

                case MovementDirection.DiagonalDownRight:
                    return new Vector3(1f, -1f, 0f).normalized;

                case MovementDirection.DiagonalDownLeft:
                    return new Vector3(-1f, -1f, 0f).normalized;

                case MovementDirection.DiagonalUpRight:
                    return new Vector3(1f, 1f, 0f).normalized;

                case MovementDirection.DiagonalUpLeft:
                    return new Vector3(-1f, 1f, 0f).normalized;

                default:
                    return Vector3.right;
            }
        }

        /// <summary>
        /// Draws the spawn area bounds in the Scene view.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;

            // Draw spawn area box
            Vector3 center = transform.position + (spawnAreaMin + spawnAreaMax) / 2f;
            Vector3 size = spawnAreaMax - spawnAreaMin;

            Gizmos.DrawWireCube(center, size);

            // Draw spawn edge indicators
            Gizmos.color = Color.green;

            // Left edge
            Vector3 leftEdge = transform.position + new Vector3(spawnAreaMin.x, (spawnAreaMin.y + spawnAreaMax.y) / 2f, (spawnAreaMin.z + spawnAreaMax.z) / 2f);
            Gizmos.DrawWireSphere(leftEdge, 2f);

            // Right edge
            Vector3 rightEdge = transform.position + new Vector3(spawnAreaMax.x, (spawnAreaMin.y + spawnAreaMax.y) / 2f, (spawnAreaMin.z + spawnAreaMax.z) / 2f);
            Gizmos.DrawWireSphere(rightEdge, 2f);
        }
    }
}
