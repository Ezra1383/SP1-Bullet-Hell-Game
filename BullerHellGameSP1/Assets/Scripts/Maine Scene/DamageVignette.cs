using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BulletHell
{
    /// <summary>
    /// Full-screen red vignette that flashes on damage and glows softly at low health.
    ///
    /// SETUP:
    /// 1. Add a full-screen RawImage to your HUD Canvas (anchorMin 0,0 / anchorMax 1,1 / all offsets 0).
    /// 2. Set raycastTarget = false on the RawImage.
    /// 3. Assign the RawImage to vignetteImage in the Inspector.
    ///    The vignette texture (transparent center, red edges) is generated automatically at runtime.
    /// </summary>
    public class DamageVignette : MonoBehaviour
    {
        [SerializeField] private RawImage vignetteImage;

        [Header("Vignette Shape")]
        [SerializeField] private int textureResolution = 256;
        [SerializeField] [Range(0f, 1f)] private float innerRadius = 0.35f;
        [SerializeField] [Range(0f, 1f)] private float outerRadius = 0.75f;
        [SerializeField] private float aspectRatio = 1.78f; // 16:9 — widen the ellipse horizontally

        [Header("Flash Settings")]
        [SerializeField] private float flashAlpha = 0.65f;
        [SerializeField] private float flashInDuration = 0.05f;
        [SerializeField] private float flashOutDuration = 0.35f;

        [Header("Low Health Ambient")]
        [SerializeField] private float lowHealthAmbient = 0.25f;
        [SerializeField] [Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField] private float ambientFadeDuration = 0.5f;

        private bool isLowHealth;

        private void Awake()
        {
            PlayerController.OnDamaged += OnPlayerDamaged;
            PlayerController.OnHealthChanged += OnHealthChanged;

            if (vignetteImage != null)
            {
                vignetteImage.texture = GenerateVignetteTexture();
                Color c = vignetteImage.color;
                c.a = 0f;
                vignetteImage.color = c;
            }
        }

        private Texture2D GenerateVignetteTexture()
        {
            int res = textureResolution;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[res * res];
            Vector2 center = new Vector2(0.5f, 0.5f);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)y / (res - 1);
                    float dx = (u - 0.5f) * 2f;
                    float dy = (v - 0.5f) * 2f * aspectRatio; // scale Y by aspect ratio to form an ellipse
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    // Map distance to alpha: 0 inside innerRadius, 1 outside outerRadius
                    float alpha = Mathf.InverseLerp(innerRadius, outerRadius, dist);
                    alpha = Mathf.Clamp01(alpha);

                    pixels[y * res + x] = new Color(1f, 0f, 0f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            PlayerController.OnDamaged -= OnPlayerDamaged;
            PlayerController.OnHealthChanged -= OnHealthChanged;
        }

        private void OnPlayerDamaged()
        {
            if (vignetteImage == null) return;

            vignetteImage.DOKill();
            float targetAlpha = isLowHealth ? lowHealthAmbient : 0f;
            DOTween.Sequence()
                .Append(vignetteImage.DOFade(flashAlpha, flashInDuration))
                .Append(vignetteImage.DOFade(targetAlpha, flashOutDuration));
        }

        private void OnHealthChanged(int current, int max)
        {
            if (vignetteImage == null) return;

            bool wasLowHealth = isLowHealth;
            isLowHealth = max > 0 && (float)current / max < lowHealthThreshold;

            if (isLowHealth && !wasLowHealth)
            {
                // Transitioned into low health: fade up to ambient glow
                vignetteImage.DOKill();
                vignetteImage.DOFade(lowHealthAmbient, ambientFadeDuration);
            }
            else if (!isLowHealth && wasLowHealth)
            {
                // Recovered from low health: fade back out
                vignetteImage.DOKill();
                vignetteImage.DOFade(0f, ambientFadeDuration);
            }
        }
    }
}
