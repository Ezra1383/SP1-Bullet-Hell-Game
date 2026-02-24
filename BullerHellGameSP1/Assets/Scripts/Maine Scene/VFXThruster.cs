using UnityEngine;
using UnityEngine.VFX;

public class VFXThruster : MonoBehaviour
{
    private VisualEffect vfx;

    void Start()
    {
        vfx = GetComponent<VisualEffect>();
        if (vfx == null) return;

        vfx.SendEvent("create");
        vfx.SendEvent("loop");
    }

    void OnDisable()
    {
        vfx?.SendEvent("stop");
    }
}
