// ProgressBarGlow.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puts a glow on the leading edge of a Slider's fill.
///
/// Position is COMPUTED from the slider value against a fixed-width track rect.
/// It does not hang off the Fill rect, because the Slider rewrites that rect's
/// anchors every update and anything parented to it inherits the churn.
/// </summary>
[DisallowMultipleComponent]
public class ProgressBarGlow : MonoBehaviour
{
    [Header("Bar")]
    [SerializeField] private Slider slider;
    [SerializeField] private Graphic glowGraphic;

    [Tooltip("The fixed-width rect the glow travels along. Empty = the Fill Rect's " +
             "parent (Fill Area), which is the rect that stays a constant width.")]
    [SerializeField] private RectTransform trackRect;

    [Header("Placement")]
    [Tooltip("Nudge from the computed tip, in pixels. Negative x pulls the glow back " +
             "into the bar instead of straddling the end of the fill.")]
    [SerializeField] private Vector2 tipOffset = Vector2.zero;

    [Header("Visibility")]
    [Tooltip("ON = stay hidden until the battle phase has actually started.")]
    [SerializeField] private bool requireBattleStarted = true;

    [SerializeField] private bool hideWhenEmpty = true;
    [SerializeField] private bool hideWhenFull = true;

    private RectTransform self;

    private void Awake()
    {
        self = transform as RectTransform;

        if (!glowGraphic) glowGraphic = GetComponent<Graphic>();
        if (!slider) slider = GetComponentInParent<Slider>();

        if (!trackRect && slider && slider.fillRect)
            trackRect = slider.fillRect.parent as RectTransform;

        if (!slider || !glowGraphic || !self || !trackRect)
        {
            Debug.LogError("[ProgressBarGlow] Missing slider / graphic / track rect.", this);
            enabled = false;
            return;
        }

        // Travel along the track, not along the fill. Reparenting also puts the glow
        // last, so it draws over the fill rather than under it.
        if (self.parent != trackRect) self.SetParent(trackRect, false);

        self.anchorMin = new Vector2(0f, 0.5f);
        self.anchorMax = new Vector2(0f, 0.5f);
        self.pivot = new Vector2(0.5f, 0.5f);

        SetVisible(false);
    }

    private void OnEnable()
    {
        SetVisible(false);
    }

    private float Normalized
    {
        get
        {
            float range = slider.maxValue - slider.minValue;
            if (Mathf.Approximately(range, 0f)) return 0f;
            return Mathf.Clamp01((slider.value - slider.minValue) / range);
        }
    }

    // LateUpdate so the Slider has already applied this frame's value.
    private void LateUpdate()
    {
        float value = Normalized;

        bool visible = !(requireBattleStarted && !BattleStartController.BattleIsRunning)
                    && !(hideWhenEmpty && value <= 0.001f)
                    && !(hideWhenFull && value >= 0.999f);

        SetVisible(visible);
        if (!visible) return;

        float x = trackRect.rect.width * value;
        self.anchoredPosition = new Vector2(x + tipOffset.x, tipOffset.y);
    }

    private void SetVisible(bool visible)
    {
        if (glowGraphic && glowGraphic.enabled != visible) glowGraphic.enabled = visible;
    }
}
