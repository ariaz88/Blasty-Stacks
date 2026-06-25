using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// Put this on the SAME GameObject as your Unity UI Button.
/// Optionally assign a child "target" if you only want to scale the graphic, not the whole button.
[RequireComponent(typeof(Button))]
public class UIButtonPressScaler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IEndDragHandler, ICancelHandler
{
    [Header("Target")]
    [SerializeField] private Transform target;                 // if null, uses this.transform

    [Header("Scales")]
    [SerializeField] private Vector3 normalScale  = Vector3.one;
    [SerializeField] private Vector3 pressedScale = new Vector3(0.9f, 0.9f, 1f);

    [Header("Timings & Easing")]
    [SerializeField, Range(0.01f, 0.50f)] private float pressDuration   = 0.08f;
    [SerializeField, Range(0.01f, 0.50f)] private float releaseDuration = 0.12f;
    [SerializeField] private Ease pressEase   = Ease.OutQuad;
    [SerializeField] private Ease releaseEase = Ease.OutQuad;

    [Header("Behavior")]
    [SerializeField] private bool onlyWhenInteractable = true; // ignore presses if Button.interactable=false

    private Button button;
    private bool pressed;
    private bool inside;
    private Tween tween;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (!target) target = transform;
    }

    private void OnEnable()
    {
        KillTween();
        if (target) target.localScale = normalScale;
        pressed = false;
        inside  = false;
    }

    private void OnDisable()
    {
        KillTween();
        if (target) target.localScale = normalScale; // never leave it squashed
        pressed = false;
        inside  = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanInteract()) return;
        pressed = true;
        inside  = true;
        AnimateTo(pressedScale, pressDuration, pressEase);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        AnimateTo(normalScale, releaseDuration, releaseEase);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //if (!pressed || !CanInteract()) return;
        //inside = true;
        //AnimateTo(pressedScale, pressDuration, pressEase);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pressed) return;
        inside = false;
        //AnimateTo(normalScale, releaseDuration, releaseEase);
        AnimateTo(pressedScale, pressDuration, pressEase);

    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Treat drag as leaving the button area
        if (!pressed) return;
        inside = false;
        AnimateTo(normalScale, releaseDuration, releaseEase);
    }

    public void OnEndDrag(PointerEventData eventData) { /* scale resets on PointerUp */ }

    public void OnCancel(BaseEventData eventData)
    {
        // System canceled (e.g., modal opened). Reset cleanly.
        pressed = false;
        inside  = false;
        AnimateTo(normalScale, releaseDuration, releaseEase);
    }

    private bool CanInteract()
    {
        if (!onlyWhenInteractable) return true;
        return button == null || button.interactable;
    }

    private void AnimateTo(Vector3 scale, float duration, Ease ease)
    {
        if (!target) return;
        KillTween();
        tween = target.DOScale(scale, duration).SetEase(ease);
    }

    private void KillTween()
    {
        if (tween != null && tween.IsActive()) tween.Kill();
        tween = null;
    }
}
