using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BulletHell
{
    /// <summary>
    /// Symmetric wing-style health bar (HUD_Sharp), anchored to the top-center of the screen.
    /// Left and right fill wings shrink from their tips inward as health decreases.
    /// The health number sits inside the center circle medallion.
    ///
    /// BEFORE USE: select all PNGs in HUD_Sharp/Individual/ → Inspector → Texture Type: Sprite (2D and UI) → Apply
    ///
    /// Assign sprites in the Inspector, then call HealthBarUI.Instance.SetHealth(current, max)
    /// whenever the player's health changes (already wired in PlayerController).
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        public static HealthBarUI Instance { get; private set; }

        // ── Canvas ─────────────────────────────────────────────────────────────
        [Header("Canvas (auto-created if empty)")]
        [SerializeField] private Canvas canvas;

        // ── HUD_Sharp Sprites ──────────────────────────────────────────────────
        [Header("HUD_Sharp Sprites  (Individual folder)")]
        [Tooltip("Ind_Health_left.png — red fill, tip on left, curves into center on right")]
        [SerializeField] private Sprite leftFillSprite;

        [Tooltip("Ind_Health_right.png — red fill, tip on right, curves into center on left")]
        [SerializeField] private Sprite rightFillSprite;

        [Tooltip("Ind_Health_backplate.png — dark angular background behind the fill")]
        [SerializeField] private Sprite backplateSprite;

        [Tooltip("Ind_Center_backplate.png — dark circle for the center medallion")]
        [SerializeField] private Sprite centerSprite;

        // ── Colors ─────────────────────────────────────────────────────────────
        [Header("Colors")]
        [Tooltip("Tint applied to the fill sprites. Set to white to show sprites unchanged.")]
        [SerializeField] private Color fillTint       = Color.white;
        [SerializeField] private Color backplateTint  = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color centerTint     = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color textColor      = Color.white;

        [Tooltip("When enabled, fill tint shifts from green (full) → red (empty) over the sprite color.")]
        [SerializeField] private bool tintWithHealth  = false;
        [SerializeField] private Color highHealthTint = new Color(0.9f, 0.9f, 0.9f);
        [SerializeField] private Color lowHealthTint  = new Color(1.0f, 0.3f, 0.3f);

        // ── Layout ─────────────────────────────────────────────────────────────
        [Header("Layout")]
        [Tooltip("Offset from the top-center of the screen")]
        [SerializeField] private Vector2 screenOffset  = new Vector2(0f, -10f);
        [Tooltip("Width and height of each wing bar")]
        [SerializeField] private Vector2 wingSize      = new Vector2(350f, 40f);
        [Tooltip("Diameter of the center circle")]
        [SerializeField] private float   centerSize    = 80f;
        [Tooltip("Gap between each wing tip and the center circle edge")]
        [SerializeField] private float   wingGap       = 2f;

        // ── Flash ──────────────────────────────────────────────────────────────
        [Header("Damage Flash")]
        [SerializeField] private float flashDuration   = 0.20f;
        [SerializeField] private Color flashColor      = Color.white;

        // ── Runtime ────────────────────────────────────────────────────────────
        private Image              leftFill;
        private Image              rightFill;
        private TextMeshProUGUI    healthText;
        private float              flashTimer;
        private int                cachedCurrent;
        private int                cachedMax;
        private Color              baseFillColor;

        // ──────────────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Called by PlayerController whenever health changes.</summary>
        public void SetHealth(int current, int max)
        {
            bool tookDamage = current < cachedCurrent;
            cachedCurrent   = current;
            cachedMax       = max;

            if (leftFill == null) return;

            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

            leftFill.fillAmount  = ratio;
            rightFill.fillAmount = ratio;

            baseFillColor = tintWithHealth
                ? Color.Lerp(lowHealthTint, highHealthTint, ratio)
                : fillTint;

            if (flashTimer <= 0f)
            {
                leftFill.color  = baseFillColor;
                rightFill.color = baseFillColor;
            }

            if (healthText != null)
                healthText.text = current.ToString();

            if (tookDamage)
                flashTimer = flashDuration;
        }

        void Update()
        {
            if (flashTimer <= 0f) return;

            flashTimer -= Time.deltaTime;
            float t    = Mathf.Clamp01(flashTimer / flashDuration);
            Color c    = Color.Lerp(baseFillColor, flashColor, t);
            leftFill.color  = c;
            rightFill.color = c;
        }

        // ── UI Construction ────────────────────────────────────────────────────

        void BuildUI()
        {
            // Canvas
            if (canvas == null)
            {
                var cGO = new GameObject("HealthBarCanvas");
                canvas  = cGO.AddComponent<Canvas>();
                canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                cGO.AddComponent<CanvasScaler>();
                cGO.AddComponent<GraphicRaycaster>();
            }

            float halfCenter = centerSize * 0.5f;
            float rootWidth  = (wingSize.x + halfCenter + wingGap) * 2f;
            float rootHeight = Mathf.Max(wingSize.y, centerSize);

            // ── Root — top-center ─────────────────────────────────────────────
            var root     = new GameObject("HUDRoot");
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin        = new Vector2(0.5f, 1f);
            rootRect.anchorMax        = new Vector2(0.5f, 1f);
            rootRect.pivot            = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = screenOffset;
            rootRect.sizeDelta        = new Vector2(rootWidth, rootHeight);

            // ── Left backplate ────────────────────────────────────────────────
            MakeWingImage("BackplateLeft", root.transform,
                backplateSprite, backplateTint,
                pivot: new Vector2(1f, 0.5f),
                anchoredPos: new Vector2(-(halfCenter + wingGap), 0f));

            // ── Right backplate ───────────────────────────────────────────────
            MakeWingImage("BackplateRight", root.transform,
                backplateSprite, backplateTint,
                pivot: new Vector2(0f, 0.5f),
                anchoredPos: new Vector2(halfCenter + wingGap, 0f),
                flipX: true);

            // ── Left fill ─────────────────────────────────────────────────────
            leftFill = MakeWingImage("FillLeft", root.transform,
                leftFillSprite, baseFillColor,
                pivot: new Vector2(1f, 0.5f),
                anchoredPos: new Vector2(-(halfCenter + wingGap), 0f));
            leftFill.type        = Image.Type.Filled;
            leftFill.fillMethod  = Image.FillMethod.Horizontal;
            leftFill.fillOrigin  = (int)Image.OriginHorizontal.Right; // fills from center-side out to tip
            leftFill.fillAmount  = 1f;

            // ── Right fill ────────────────────────────────────────────────────
            rightFill = MakeWingImage("FillRight", root.transform,
                rightFillSprite, baseFillColor,
                pivot: new Vector2(0f, 0.5f),
                anchoredPos: new Vector2(halfCenter + wingGap, 0f));
            rightFill.type        = Image.Type.Filled;
            rightFill.fillMethod  = Image.FillMethod.Horizontal;
            rightFill.fillOrigin  = (int)Image.OriginHorizontal.Left; // fills from center-side out to tip
            rightFill.fillAmount  = 1f;

            // ── Center circle ─────────────────────────────────────────────────
            var centerGO   = new GameObject("CenterCircle");
            centerGO.transform.SetParent(root.transform, false);
            var centerImg  = centerGO.AddComponent<Image>();
            centerImg.sprite = centerSprite;
            centerImg.color  = centerSprite != null ? centerTint : new Color(0.12f, 0.12f, 0.12f, 1f);
            var centerRect   = centerGO.GetComponent<RectTransform>();
            centerRect.anchorMin        = new Vector2(0.5f, 0.5f);
            centerRect.anchorMax        = new Vector2(0.5f, 0.5f);
            centerRect.pivot            = new Vector2(0.5f, 0.5f);
            centerRect.anchoredPosition = Vector2.zero;
            centerRect.sizeDelta        = new Vector2(centerSize, centerSize);

            // ── Health number ─────────────────────────────────────────────────
            var textGO  = new GameObject("HealthText");
            textGO.transform.SetParent(centerGO.transform, false);
            healthText  = textGO.AddComponent<TextMeshProUGUI>();
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            healthText.alignment  = TextAlignmentOptions.Center;
            healthText.fontSize   = Mathf.Round(centerSize * 0.38f);
            healthText.fontStyle  = FontStyles.Bold;
            healthText.color      = textColor;
            healthText.text       = "0";

            baseFillColor = tintWithHealth ? highHealthTint : fillTint;
        }

        /// <summary>Creates a wing-sized Image at the given pivot and position.</summary>
        Image MakeWingImage(string name, Transform parent, Sprite sprite, Color color,
                            Vector2 pivot, Vector2 anchoredPos, bool flipX = false)
        {
            var go   = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img  = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color  = color;
            var rect   = go.GetComponent<RectTransform>();
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.pivot            = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta        = wingSize;
            if (flipX) rect.localScale = new Vector3(-1f, 1f, 1f);
            return img;
        }
    }
}
