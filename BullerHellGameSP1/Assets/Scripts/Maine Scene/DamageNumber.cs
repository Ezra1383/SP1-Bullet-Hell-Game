using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BulletHell
{
    /// <summary>
    /// Attach to a prefab that has a TextMeshPro (world-space) child.
    /// Call Play() immediately after Instantiating to animate the number.
    /// The GameObject self-destructs after the animation completes.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshPro damageText;
        [SerializeField] private float floatHeight = 2f;
        [SerializeField] private float duration = 0.9f;

        public void Play(int damage, Vector3 worldPos, Camera cam)
        {
            transform.position = worldPos + Vector3.up * 0.5f;
            transform.localScale = Vector3.zero;

            if (damageText != null)
            {
                damageText.text = damage.ToString();
                // Ensure alpha starts at 1
                Color c = damageText.color;
                c.a = 1f;
                damageText.color = c;
            }

            var seq = DOTween.Sequence();
            seq.Append(transform.DOScale(1.3f, 0.1f).SetEase(Ease.OutBack));
            seq.Append(transform.DOScale(1f, 0.05f));
            seq.Join(transform.DOMoveY(worldPos.y + floatHeight, duration).SetEase(Ease.OutCubic));
            if (damageText != null)
                seq.Join(damageText.DOFade(0f, duration * 0.55f).SetDelay(duration * 0.45f));
            seq.OnComplete(() => Destroy(gameObject));
        }

        private void LateUpdate()
        {
            // Billboard: always face the main camera
            if (Camera.main != null)
                transform.forward = Camera.main.transform.forward;
        }
    }
}
