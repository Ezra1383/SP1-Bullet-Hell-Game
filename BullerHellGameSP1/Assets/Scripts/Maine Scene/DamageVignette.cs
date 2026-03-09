using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace BulletHell
{
    /// <summary>
    /// Full-screen red vignette that flashes on damage and glows softly at low health.
    ///
    /// SETUP:
    /// 1. Add a full-screen Image to your HUD Canvas (anchorMin 0,0 / anchorMax 1,1 / all offsets 0).
    /// 2. Use a dark-red vignette sprite (transparent center, dark edges) — or a solid dark-red
    ///    Image works too; adjust flashAlpha to taste.
    /// 3. Set raycastTarget = false on the Image.
    /// 4. Assign the Image to vignetteImage in the Inspector.
    /// 5. Set the Image's starting Color alpha to 0.
    /// </summary>
    public class DamageVignette : MonoBehaviour
    {
        [SerializeField] private Image vignetteImage;

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

            // Start fully transparent
            if (vignetteImage != null)
            {
                Color c = vignetteImage.color;
                c.a = 0f;
                vignetteImage.color = c;
            }
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
