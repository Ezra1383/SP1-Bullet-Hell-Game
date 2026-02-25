using UnityEngine;
using UnityEngine.VFX;

public class VFXThruster : MonoBehaviour
{
    private VisualEffect vfx;

    void Awake()
    {
        vfx = GetComponent<VisualEffect>();
    }

    void OnEnable()
    {
        if (vfx == null) return;
        vfx.Stop();
        vfx.Play();          // resets the simulation so pooled enemies don't get stale particles
        vfx.SendEvent("loop");
    }

    void OnDisable()
    {
        vfx?.SendEvent("stop");
    }
}
