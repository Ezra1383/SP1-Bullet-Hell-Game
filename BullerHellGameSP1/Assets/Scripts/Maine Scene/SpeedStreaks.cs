using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpeedStreaks : MonoBehaviour
{
    public ParticleSystem streakParticles;
    public float minSpeed = 2f;
    public float maxSpeed = 20f;

    [Header("References")]
    [Tooltip("Reference to the PlayerController to read speed from")]
    public BulletHell.PlayerController playerController;

    private float currentSpeed;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.MainModule main;

    void Start()
    {
        emission = streakParticles.emission;
        main = streakParticles.main;

        // Auto-find player controller if not assigned
        if (playerController == null)
        {
            playerController = FindObjectOfType<BulletHell.PlayerController>();
        }
    }

    void Update()
    {
        // Get current speed from player controller
        if (playerController != null)
        {
            currentSpeed = playerController.CurrentSpeed;
        }

        float t = Mathf.InverseLerp(0f, maxSpeed, currentSpeed);

        // More particles + longer streaks as speed increases
        emission.rateOverTime = Mathf.Lerp(5f, 30f, t);   // was 5-50, way too many at max
        main.startLifetime = Mathf.Lerp(0.1f, 0.4f, t);   // was 0.2-0.8, particles living too long
        main.startSpeed = Mathf.Lerp(8f, 25f, t);          // was 5-40, fine but slightly tamed
    }
}