using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlanetParallax : MonoBehaviour
{
    public Transform player;

    [Range(0f, 1f)]
    public float parallaxFactor = 0.3f;

    [Tooltip("Max units this object can drift from its origin. Keep this smaller than the distance between the planet and the spline path.")]
    public float maxDisplacement = 5f;

    private Vector3 startPos;
    private Vector3 startPlayerPos;

    void Start()
    {
        startPos = transform.position;
        startPlayerPos = player.position;
    }

    void LateUpdate()
    {
        Vector3 totalMovement = player.position - startPlayerPos;

        Vector3 targetPos = new Vector3(
            startPos.x + totalMovement.x * parallaxFactor,
            startPos.y + totalMovement.y * parallaxFactor,
            startPos.z // leave Z untouched on a spline game
        );

        // Clamp displacement so the planet never wanders into gameplay space
        Vector3 displacement = targetPos - startPos;
        if (displacement.magnitude > maxDisplacement)
            displacement = displacement.normalized * maxDisplacement;

        transform.position = startPos + displacement;
    }

    // Visualize the safe zone in the editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Application.isPlaying ? startPos : transform.position, maxDisplacement);
    }
}
