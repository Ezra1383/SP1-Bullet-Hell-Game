using UnityEngine;
using UnityEngine.VFX;

public class VFXAutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private string triggerEvent = "hit";

    void Start()
    {
        VisualEffect vfx = GetComponent<VisualEffect>();
        if (vfx != null && !string.IsNullOrEmpty(triggerEvent))
            vfx.SendEvent(triggerEvent);

        Destroy(gameObject, lifetime);
    }
}