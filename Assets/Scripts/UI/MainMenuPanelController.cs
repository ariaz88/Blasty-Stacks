using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


public class MainMenuPanelController6 : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject unitsPanel;
    [SerializeField] private GameObject testPanel;
    [Header("Main Buttons (small / inactive)")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button unitsButton;
    [SerializeField] private Button testButton;
    [Header("Selected Visual Buttons (big)")]
    [SerializeField] private Button homeSelectedButton;
    [SerializeField] private Button unitsSelectedButton;
    [SerializeField] private Button testSelectedButton;
    [Header("Home inactive position offset when Test is selected")]
    [SerializeField] private float homeButtonOffsetX = -20f;
    [Header("Slide Settings")]
    [SerializeField, Range(0.15f, 1.0f)] private float slideDuration = 0.35f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private bool fadeDuringSlide = true;
    [SerializeField, Range(0.0f, 1.0f)] private float fadeDuration = 0.25f;
    [SerializeField] private float extraSlidePadding = 60f;
    [Header("Coming Soon Toast Settings")]
    [SerializeField] private GameObject lockedToastPrefab;
    [SerializeField] private RectTransform toastCanvasParent;
    [SerializeField] private Color toastStartColor = Color.white;
    [SerializeField] private Color toastGrayColor = Color.gray;
    [SerializeField] private string lockedStageText = "Coming Soon";
    [SerializeField] private float toastStartYOffset = 0f;
    [SerializeField] private float toastRisePixels = 100f;
    [SerializeField] private float toastRiseDuration = 2f;
    [SerializeField] private Ease toastRiseEase = Ease.OutQuad;
    [SerializeField] private float toastFadeInDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float toastGrayAt = 0.5f;
    [SerializeField, Range(0f, 1f)] private float toastFadeOutAt = 0.9f;
    private Vector2 homeButtonDefaultAnchoredPos;
    private enum MenuTab { Home = 0, Units = 1, Test = 2 }
    private MenuTab currentTab = MenuTab.Home;
    private bool isAnimating;
    private void Start()
    {
        if (homeButton && homeButton.transform is RectTransform rt)
            homeButtonDefaultAnchoredPos = rt.anchoredPosition;
        PreparePanelsForAnimation(); // place all panels according to currentTab
        SelectMenu(MenuTab.Home, animate: false); // show default without anim
    }
    // Button hooks
    public void ShowHomePanel() => SelectMenu(MenuTab.Home, animate: true);
    public void ShowUnitsPanel() => SelectMenu(MenuTab.Units, animate: true);
    public void ShowTestPanel1()
    {
        ShowLockedStageToast(testButton ? testButton.GetComponent<RectTransform>() : null);
    }
    public void ShowTestPanel()
    {
        UpdateButtonVisuals(MenuTab.Test);
        NudgeInactiveHomeWhenTest(MenuTab.Test);
        ShowLockedStageToast(testSelectedButton ? testSelectedButton.GetComponent<RectTransform>() : null);
    }
    // ---------------- CORE ----------------
    private void SelectMenu(MenuTab target, bool animate)
    {
        if (isAnimating) return;
        UpdateButtonVisuals(target);
        NudgeInactiveHomeWhenTest(target);
        if (target == currentTab)
        {
            // Just make sure only the current panel is active/interactive
            PositionPanelsForCurrent(currentTab, instant: true, setActiveOnlyForCurrent: true);
            return;
        }
        RectTransform fromRT = GetPanelRT(currentTab);
        RectTransform toRT = GetPanelRT(target);
        if (!fromRT || !toRT) return;
        CanvasGroup fromCG = RequireCanvasGroup(fromRT);
        CanvasGroup toCG = RequireCanvasGroup(toRT);
        int fromSpatial = GetSpatialIndex(currentTab);
        int toSpatial = GetSpatialIndex(target);
        int delta = toSpatial - fromSpatial;
        float panelWidth = ((RectTransform)transform).rect.width + Mathf.Max(0f, extraSlidePadding);
        float centerX = 0f;
        // current panel always starts in the center
        Vector2 fromPos = fromRT.anchoredPosition;
        fromPos.x = centerX;
        fromRT.anchoredPosition = fromPos;
        // target starts off-screen on the correct side (based on delta)
        Vector2 toPos = toRT.anchoredPosition;
        float startToX = delta * panelWidth;
        toPos.x = startToX;
        toRT.anchoredPosition = toPos;
        fromRT.gameObject.SetActive(true);
        toRT.gameObject.SetActive(true);
        // disable interaction while animating
        SetCGInteractable(fromCG, false);
        SetCGInteractable(toCG, false);
        if (fadeDuringSlide)
        {
            fromCG.alpha = 1f;
            toCG.alpha = 0f;
        }
        else
        {
            fromCG.alpha = 1f;
            toCG.alpha = 1f;
        }
        if (!animate)
        {
            // snap, no tween
            float fromEndXx = -delta * panelWidth;
            fromPos.x = fromEndXx;
            fromRT.anchoredPosition = fromPos;
            toPos.x = centerX;
            toRT.anchoredPosition = toPos;
            if (fadeDuringSlide)
            {
                fromCG.alpha = 0f;
                toCG.alpha = 1f;
            }
            fromRT.gameObject.SetActive(false);
            SetCGInteractable(toCG, true);
            currentTab = target;
            PositionPanelsForCurrent(target, instant: true, setActiveOnlyForCurrent: true);
            return;
        }
        isAnimating = true;
        // tween out / in
        float fromEndX = -delta * panelWidth;
        float toEndX = centerX;
        Tween outTween = fromRT.DOAnchorPosX(fromEndX, slideDuration).SetEase(slideEase);
        Tween inTween = toRT.DOAnchorPosX(toEndX, slideDuration).SetEase(slideEase);
        Tween fadeOut = null, fadeIn = null;
        if (fadeDuringSlide)
        {
            fadeOut = fromCG.DOFade(0f, Mathf.Min(fadeDuration, slideDuration)).SetEase(Ease.OutQuad);
            fadeIn = toCG.DOFade(1f, Mathf.Min(fadeDuration, slideDuration)).SetEase(Ease.OutQuad);
        }
        DOTween.Sequence()
               .Join(outTween)
               .Join(inTween)
               .Join(fadeOut ?? DOTween.Sequence())
               .Join(fadeIn ?? DOTween.Sequence())
               .OnComplete(() =>
               {
                   fromRT.gameObject.SetActive(false);
                   SetCGInteractable(toCG, true);
                   currentTab = target;
                   PositionPanelsForCurrent(target, instant: true, setActiveOnlyForCurrent: true);
                   isAnimating = false;
               });
    }
    // Maintain Instagram-like invariant: current at 0, left tabs at -width, right tabs at +width
    private void PositionPanelsForCurrent(MenuTab center, bool instant, bool setActiveOnlyForCurrent)
    {
        RectTransform homeRT = GetPanelRT(MenuTab.Home);
        RectTransform unitsRT = GetPanelRT(MenuTab.Units);
        RectTransform testRT = GetPanelRT(MenuTab.Test);
        float panelWidth = ((RectTransform)transform).rect.width + Mathf.Max(0f, extraSlidePadding);
        SetPanel(MenuTab.Home, homeRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        SetPanel(MenuTab.Units, unitsRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        SetPanel(MenuTab.Test, testRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        void SetPanel(MenuTab tab, RectTransform rt, MenuTab centerTab, float width, bool instantPos, bool activeOnlyCurrent)
        {
            if (!rt) return;
            bool isCurrent = (tab == centerTab);
            float targetX;
            if (isCurrent)
            {
                targetX = 0f;
            }
            else
            {
                int tabSpatial = tab == MenuTab.Units ? 0 : tab == MenuTab.Home ? 1 : 2;
                int centerSpatial = centerTab == MenuTab.Units ? 0 : centerTab == MenuTab.Home ? 1 : 2;
                targetX = (tabSpatial - centerSpatial) * width;
            }
            if (instantPos)
            {
                var pos = rt.anchoredPosition;
                pos.x = targetX;
                rt.anchoredPosition = pos;
            }
            else
            {
                rt.DOAnchorPosX(targetX, 0.01f);
            }
            CanvasGroup cg = RequireCanvasGroup(rt);
            cg.alpha = isCurrent ? 1f : (fadeDuringSlide ? 0f : 1f);
            SetCGInteractable(cg, isCurrent);
            rt.gameObject.SetActive(activeOnlyCurrent ? isCurrent : true);
        }
    }
    private int GetSpatialIndex(MenuTab tab) => tab == MenuTab.Units ? 0 : tab == MenuTab.Home ? 1 : 2;
    private int IndexOf(MenuTab tab) => (int)tab;
    private void UpdateButtonVisuals(MenuTab tab)
    {
        bool homeSelected = (tab == MenuTab.Home);
        bool unitsSelected = (tab == MenuTab.Units);
        bool testSelected = (tab == MenuTab.Test);
        if (homeButton) homeButton.gameObject.SetActive(!homeSelected);
        if (homeSelectedButton) homeSelectedButton.gameObject.SetActive(homeSelected);
        if (unitsButton) unitsButton.gameObject.SetActive(!unitsSelected);
        if (unitsSelectedButton) unitsSelectedButton.gameObject.SetActive(unitsSelected);
        if (testButton) testButton.gameObject.SetActive(!testSelected);
        if (testSelectedButton) testSelectedButton.gameObject.SetActive(testSelected);
    }
    private void NudgeInactiveHomeWhenTest(MenuTab tab)
    {
        if (!homeButton) return;
        if (!(homeButton.transform is RectTransform rt)) return;
        Vector2 target = (tab == MenuTab.Test)
            ? homeButtonDefaultAnchoredPos + new Vector2(homeButtonOffsetX, 0f)
            : homeButtonDefaultAnchoredPos;
        rt.DOAnchorPos(target, 0.2f).SetEase(Ease.OutQuad);
    }
    private void ShowLockedStageToast(RectTransform anchorParent)
    {
        if (!lockedToastPrefab) return;
        // Parent & anchor (top of the button area)
        RectTransform parent = toastCanvasParent ? toastCanvasParent : (RectTransform)transform;
        RectTransform baseAnchor = anchorParent ? anchorParent : parent;
        // Spawn
        var go = Instantiate(lockedToastPrefab, baseAnchor);
        var rt = go.GetComponent<RectTransform>();
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        // Text reference(s); set starting color
        var tmps = go.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var t in tmps) t.color = toastStartColor;
        if (tmps.Length > 0) tmps[0].text = lockedStageText; // keep your existing text
        // Place at top-center of anchor, pivot at bottom so it rises “up”
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, toastStartYOffset);
        // Motion parameters (kept from your previous version)
        float rise = Mathf.Max(0f, toastRisePixels);
        float duration = Mathf.Max(0.01f, toastRiseDuration);
        // Clamp the visual events
        float grayPct = Mathf.Clamp01(toastGrayAt);
        float fadePct = Mathf.Clamp01(toastFadeOutAt);
        if (grayPct > fadePct) grayPct = fadePct; // ensure gray happens before/at fade start
        float grayTime = duration * grayPct;
        float fadeTime = duration * (1f - fadePct);
        if (fadeTime < 0.06f) fadeTime = 0.06f; // tiny guard so fade is visible
        // Build sequence:
        cg.alpha = 0f;
        var seq = DOTween.Sequence();
        // 1) quick pop-in
        seq.Append(cg.DOFade(1f, toastFadeInDuration));
        // 2) rise upward over full duration (ease unchanged from your last logic)
        seq.Join(rt.DOAnchorPosY(rt.anchoredPosition.y + rise, duration)
                 .SetEase(toastRiseEase));
        // 3) at 75% of travel, flip text color to gray
        seq.InsertCallback(grayTime, () =>
        {
            foreach (var t in tmps) t.color = toastGrayColor;
        });
        // 4) from 90% of travel to the end, fade out to 0
        seq.Insert(duration * fadePct, cg.DOFade(0f, fadeTime));
        // 5) cleanup
        seq.OnComplete(() => Destroy(go));
    }
    // ---------------- UTILITIES ----------------
    private void PreparePanelsForAnimation()
    {
        // Ensure all panels exist, activate so we can position them
        if (homePanel) homePanel.SetActive(true);
        if (unitsPanel) unitsPanel.SetActive(true);
        if (testPanel) testPanel.SetActive(true);
        // Place according to currentTab and disable non-current
        PositionPanelsForCurrent(currentTab, instant: true, setActiveOnlyForCurrent: true);
    }
    private RectTransform GetPanelRT(MenuTab tab)
    {
        GameObject go = null;
        switch (tab)
        {
            case MenuTab.Home: go = homePanel; break;
            case MenuTab.Units: go = unitsPanel; break;
            case MenuTab.Test: go = testPanel; break;
        }
        return go ? go.GetComponent<RectTransform>() : null;
    }
    private CanvasGroup RequireCanvasGroup(RectTransform rt)
    {
        if (!rt) return null;
        var cg = rt.GetComponent<CanvasGroup>();
        if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }
    private void SetCGInteractable(CanvasGroup cg, bool on)
    {
        if (!cg) return;
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }
}
public class MainMenuPanelController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject unitsPanel;
    [SerializeField] private GameObject testPanel;
    [Header("Main Buttons (small / inactive)")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button unitsButton;
    [SerializeField] private Button testButton;
    [Header("Selected Visual Buttons (big)")]
    [SerializeField] private Button homeSelectedButton;
    [SerializeField] private Button unitsSelectedButton;
    [SerializeField] private Button testSelectedButton;
    [Header("Home inactive position offset when Test is selected")]
    [SerializeField] private float homeButtonOffsetX = -20f;
    [Header("Slide Settings")]
    [SerializeField, Range(0.15f, 1.0f)] private float slideDuration = 0.35f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private bool fadeDuringSlide = true;
    [SerializeField, Range(0.0f, 1.0f)] private float fadeDuration = 0.25f;
    [SerializeField] private float extraSlidePadding = 60f;
    [Header("Coming Soon Toast Settings")]
    [SerializeField] private GameObject lockedToastPrefab;
    [SerializeField] private RectTransform toastCanvasParent;
    [SerializeField] private Color toastStartColor = Color.white;
    [SerializeField] private Color toastGrayColor = Color.gray;
    [SerializeField] private string lockedStageText = "Coming Soon";
    [SerializeField] private float toastStartYOffset = 0f;
    [SerializeField] private float toastRisePixels = 100f;
    [SerializeField] private float toastRiseDuration = 2f;
    [SerializeField] private Ease toastRiseEase = Ease.OutQuad;
    [SerializeField] private float toastFadeInDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float toastGrayAt = 0.5f;
    [SerializeField, Range(0f, 1f)] private float toastFadeOutAt = 0.9f;
    private Vector2 homeButtonDefaultAnchoredPos;
    private enum MenuTab { Home = 0, Units = 1, Test = 2 }
    private MenuTab currentTab = MenuTab.Home;
    private bool isAnimating;
    private void Start()
    {
        if (homeButton && homeButton.transform is RectTransform rt)
            homeButtonDefaultAnchoredPos = rt.anchoredPosition;
        PreparePanelsForAnimation(); // place all panels according to currentTab
        SelectMenu(MenuTab.Home, animate: false); // show default without anim
    }
    // Button hooks
    public void ShowHomePanel() => SelectMenu(MenuTab.Home, animate: true);
    public void ShowUnitsPanel() => SelectMenu(MenuTab.Units, animate: true);
    public void ShowTestPanel()
    {
        ShowLockedStageToast(testButton ? testButton.GetComponent<RectTransform>() : null);
    }
    // ---------------- CORE ----------------
    private void SelectMenu(MenuTab target, bool animate)
    {
        if (isAnimating) return;
        UpdateButtonVisuals(target);
        NudgeInactiveHomeWhenTest(target);
        if (target == currentTab)
        {
            // Just make sure only the current panel is active/interactive
            PositionPanelsForCurrent(currentTab, instant: true, setActiveOnlyForCurrent: true);
            return;
        }
        RectTransform fromRT = GetPanelRT(currentTab);
        RectTransform toRT = GetPanelRT(target);
        if (!fromRT || !toRT) return;
        CanvasGroup fromCG = RequireCanvasGroup(fromRT);
        CanvasGroup toCG = RequireCanvasGroup(toRT);
        int fromSpatial = GetSpatialIndex(currentTab);
        int toSpatial = GetSpatialIndex(target);
        int delta = toSpatial - fromSpatial;
        float panelWidth = ((RectTransform)transform).rect.width + Mathf.Max(0f, extraSlidePadding);
        float centerX = 0f;
        // current panel always starts in the center
        Vector2 fromPos = fromRT.anchoredPosition;
        fromPos.x = centerX;
        fromRT.anchoredPosition = fromPos;
        // target starts off-screen on the correct side (based on delta)
        Vector2 toPos = toRT.anchoredPosition;
        float startToX = delta * panelWidth;
        toPos.x = startToX;
        toRT.anchoredPosition = toPos;
        fromRT.gameObject.SetActive(true);
        toRT.gameObject.SetActive(true);
        // disable interaction while animating
        SetCGInteractable(fromCG, false);
        SetCGInteractable(toCG, false);
        if (fadeDuringSlide)
        {
            fromCG.alpha = 1f;
            toCG.alpha = 0f;
        }
        else
        {
            fromCG.alpha = 1f;
            toCG.alpha = 1f;
        }
        if (!animate)
        {
            // snap, no tween
            float fromEndXx = -delta * panelWidth;
            fromPos.x = fromEndXx;
            fromRT.anchoredPosition = fromPos;
            toPos.x = centerX;
            toRT.anchoredPosition = toPos;
            if (fadeDuringSlide)
            {
                fromCG.alpha = 0f;
                toCG.alpha = 1f;
            }
            fromRT.gameObject.SetActive(false);
            SetCGInteractable(toCG, true);
            currentTab = target;
            PositionPanelsForCurrent(target, instant: true, setActiveOnlyForCurrent: true);
            return;
        }
        isAnimating = true;
        // tween out / in
        float fromEndX = -delta * panelWidth;
        float toEndX = centerX;
        Tween outTween = fromRT.DOAnchorPosX(fromEndX, slideDuration).SetEase(slideEase);
        Tween inTween = toRT.DOAnchorPosX(toEndX, slideDuration).SetEase(slideEase);
        Tween fadeOut = null, fadeIn = null;
        if (fadeDuringSlide)
        {
            fadeOut = fromCG.DOFade(0f, Mathf.Min(fadeDuration, slideDuration)).SetEase(Ease.OutQuad);
            fadeIn = toCG.DOFade(1f, Mathf.Min(fadeDuration, slideDuration)).SetEase(Ease.OutQuad);
        }
        DOTween.Sequence()
               .Join(outTween)
               .Join(inTween)
               .Join(fadeOut ?? DOTween.Sequence())
               .Join(fadeIn ?? DOTween.Sequence())
               .OnComplete(() =>
               {
                   fromRT.gameObject.SetActive(false);
                   SetCGInteractable(toCG, true);
                   currentTab = target;
                   PositionPanelsForCurrent(target, instant: true, setActiveOnlyForCurrent: true);
                   isAnimating = false;
               });
    }
    // Maintain Instagram-like invariant: current at 0, left tabs at -width, right tabs at +width
    private void PositionPanelsForCurrent(MenuTab center, bool instant, bool setActiveOnlyForCurrent)
    {
        RectTransform homeRT = GetPanelRT(MenuTab.Home);
        RectTransform unitsRT = GetPanelRT(MenuTab.Units);
        RectTransform testRT = GetPanelRT(MenuTab.Test);
        float panelWidth = ((RectTransform)transform).rect.width + Mathf.Max(0f, extraSlidePadding);
        SetPanel(MenuTab.Home, homeRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        SetPanel(MenuTab.Units, unitsRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        SetPanel(MenuTab.Test, testRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        void SetPanel(MenuTab tab, RectTransform rt, MenuTab centerTab, float width, bool instantPos, bool activeOnlyCurrent)
        {
            if (!rt) return;
            bool isCurrent = (tab == centerTab);
            float targetX;
            if (isCurrent)
            {
                targetX = 0f;
            }
            else
            {
                int tabSpatial = tab == MenuTab.Units ? 0 : tab == MenuTab.Home ? 1 : 2;
                int centerSpatial = centerTab == MenuTab.Units ? 0 : centerTab == MenuTab.Home ? 1 : 2;
                targetX = (tabSpatial - centerSpatial) * width;
            }
            if (instantPos)
            {
                var pos = rt.anchoredPosition;
                pos.x = targetX;
                rt.anchoredPosition = pos;
            }
            else
            {
                rt.DOAnchorPosX(targetX, 0.01f);
            }
            CanvasGroup cg = RequireCanvasGroup(rt);
            cg.alpha = isCurrent ? 1f : (fadeDuringSlide ? 0f : 1f);
            SetCGInteractable(cg, isCurrent);
            rt.gameObject.SetActive(activeOnlyCurrent ? isCurrent : true);
        }
    }
    private int GetSpatialIndex(MenuTab tab) => tab == MenuTab.Units ? 0 : tab == MenuTab.Home ? 1 : 2;
    private int IndexOf(MenuTab tab) => (int)tab;
    private void UpdateButtonVisuals(MenuTab tab)
    {
        bool homeSelected = (tab == MenuTab.Home);
        bool unitsSelected = (tab == MenuTab.Units);
        bool testSelected = (tab == MenuTab.Test);
        if (homeButton) homeButton.gameObject.SetActive(!homeSelected);
        if (homeSelectedButton) homeSelectedButton.gameObject.SetActive(homeSelected);
        if (unitsButton) unitsButton.gameObject.SetActive(!unitsSelected);
        if (unitsSelectedButton) unitsSelectedButton.gameObject.SetActive(unitsSelected);
        if (testButton) testButton.gameObject.SetActive(!testSelected);
        if (testSelectedButton) testSelectedButton.gameObject.SetActive(testSelected);
    }
    private void NudgeInactiveHomeWhenTest(MenuTab tab)
    {
        if (!homeButton) return;
        if (!(homeButton.transform is RectTransform rt)) return;
        Vector2 target = (tab == MenuTab.Test)
            ? homeButtonDefaultAnchoredPos + new Vector2(homeButtonOffsetX, 0f)
            : homeButtonDefaultAnchoredPos;
        rt.DOAnchorPos(target, 0.2f).SetEase(Ease.OutQuad);
    }
    private void ShowLockedStageToast(RectTransform anchorParent)
    {
        if (!lockedToastPrefab) return;
        // Parent & anchor (top of the button area)
        RectTransform parent = toastCanvasParent ? toastCanvasParent : (RectTransform)transform;
        RectTransform baseAnchor = anchorParent ? anchorParent : parent;
        // Spawn
        var go = Instantiate(lockedToastPrefab, baseAnchor);
        var rt = go.GetComponent<RectTransform>();
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        // Text reference(s); set starting color
        var tmps = go.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var t in tmps) t.color = toastStartColor;
        if (tmps.Length > 0) tmps[0].text = lockedStageText; // keep your existing text
        // Place at top-center of anchor, pivot at bottom so it rises “up”
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, toastStartYOffset);
        // Motion parameters (kept from your previous version)
        float rise = Mathf.Max(0f, toastRisePixels);
        float duration = Mathf.Max(0.01f, toastRiseDuration);
        // Clamp the visual events
        float grayPct = Mathf.Clamp01(toastGrayAt);
        float fadePct = Mathf.Clamp01(toastFadeOutAt);
        if (grayPct > fadePct) grayPct = fadePct; // ensure gray happens before/at fade start
        float grayTime = duration * grayPct;
        float fadeTime = duration * (1f - fadePct);
        if (fadeTime < 0.06f) fadeTime = 0.06f; // tiny guard so fade is visible
        // Build sequence:
        cg.alpha = 0f;
        var seq = DOTween.Sequence();
        // 1) quick pop-in
        seq.Append(cg.DOFade(1f, toastFadeInDuration));
        // 2) rise upward over full duration (ease unchanged from your last logic)
        seq.Join(rt.DOAnchorPosY(rt.anchoredPosition.y + rise, duration)
                 .SetEase(toastRiseEase));
        // 3) at 75% of travel, flip text color to gray
        seq.InsertCallback(grayTime, () =>
        {
            foreach (var t in tmps) t.color = toastGrayColor;
        });
        // 4) from 90% of travel to the end, fade out to 0
        seq.Insert(duration * fadePct, cg.DOFade(0f, fadeTime));
        // 5) cleanup
        seq.OnComplete(() => Destroy(go));
    }
    // ---------------- UTILITIES ----------------
    private void PreparePanelsForAnimation()
    {
        // Ensure all panels exist, activate so we can position them
        if (homePanel) homePanel.SetActive(true);
        if (unitsPanel) unitsPanel.SetActive(true);
        if (testPanel) testPanel.SetActive(true);
        // Place according to currentTab and disable non-current
        PositionPanelsForCurrent(currentTab, instant: true, setActiveOnlyForCurrent: true);
    }
    private RectTransform GetPanelRT(MenuTab tab)
    {
        GameObject go = null;
        switch (tab)
        {
            case MenuTab.Home: go = homePanel; break;
            case MenuTab.Units: go = unitsPanel; break;
            case MenuTab.Test: go = testPanel; break;
        }
        return go ? go.GetComponent<RectTransform>() : null;
    }
    private CanvasGroup RequireCanvasGroup(RectTransform rt)
    {
        if (!rt) return null;
        var cg = rt.GetComponent<CanvasGroup>();
        if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }
    private void SetCGInteractable(CanvasGroup cg, bool on)
    {
        if (!cg) return;
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }
}
public class MainMenuPanelController4 : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject unitsPanel;
    [SerializeField] private GameObject testPanel;
    [Header("Main Buttons (small / inactive)")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button unitsButton;
    [SerializeField] private Button testButton;
    [Header("Selected Visual Buttons (big)")]
    [SerializeField] private Button homeSelectedButton;
    [SerializeField] private Button unitsSelectedButton;
    [SerializeField] private Button testSelectedButton;
    [Header("Home inactive position offset when Test is selected")]
    [SerializeField] private float homeButtonOffsetX = -20f;
    [Header("Slide Settings")]
    [SerializeField, Range(0.15f, 1.0f)] private float slideDuration = 0.35f;
    [SerializeField] private Ease slideEase = Ease.OutCubic;
    [SerializeField] private bool fadeDuringSlide = true;
    [SerializeField, Range(0.0f, 1.0f)] private float fadeDuration = 0.25f;
    [SerializeField] private float extraSlidePadding = 60f;
    private Vector2 homeButtonDefaultAnchoredPos;
    private enum MenuTab { Home = 0, Units = 1, Test = 2 }
    private MenuTab currentTab = MenuTab.Home;
    private bool isAnimating;
    private void Start()
    {
        if (homeButton && homeButton.transform is RectTransform rt)
            homeButtonDefaultAnchoredPos = rt.anchoredPosition;
        PreparePanelsForAnimation(); // place all panels according to currentTab
        SelectMenu(MenuTab.Home, animate: false); // show default without anim
    }
    // Button hooks
    public void ShowHomePanel() => SelectMenu(MenuTab.Home, animate: true);
    public void ShowUnitsPanel() => SelectMenu(MenuTab.Units, animate: true);
    public void ShowTestPanel() => SelectMenu(MenuTab.Test, animate: true);
    // ---------------- CORE ----------------
    private void SelectMenu(MenuTab target, bool animate)
    {
        if (isAnimating) return;
        UpdateButtonVisuals(target);
        NudgeInactiveHomeWhenTest(target);
        if (target == currentTab)
        {
            // Just make sure only the current panel is active/interactive
            PositionPanelsForCurrent(currentTab, instant: true, setActiveOnlyForCurrent: true);
            return;
        }
        RectTransform fromRT = GetPanelRT(currentTab);
        RectTransform toRT = GetPanelRT(target);
        if (!fromRT || !toRT) return;
        CanvasGroup fromCG = RequireCanvasGroup(fromRT);
        CanvasGroup toCG = RequireCanvasGroup(toRT);
        int fromSpatial = GetSpatialIndex(currentTab);
        int toSpatial = GetSpatialIndex(target);
        int delta = toSpatial - fromSpatial;
        float panelWidth = ((RectTransform)transform).rect.width + Mathf.Max(0f, extraSlidePadding);
        float centerX = 0f;
        // current panel always starts in the center
        Vector2 fromPos = fromRT.anchoredPosition;
        fromPos.x = centerX;
        fromRT.anchoredPosition = fromPos;
        // target starts off-screen on the correct side (based on delta)
        Vector2 toPos = toRT.anchoredPosition;
        float startToX = delta * panelWidth;
        toPos.x = startToX;
        toRT.anchoredPosition = toPos;
        fromRT.gameObject.SetActive(true);
        toRT.gameObject.SetActive(true);
        // disable interaction while animating
        SetCGInteractable(fromCG, false);
        SetCGInteractable(toCG, false);
        if (fadeDuringSlide)
        {
            fromCG.alpha = 1f;
            toCG.alpha = 0f;
        }
        else
        {
            fromCG.alpha = 1f;
            toCG.alpha = 1f;
        }
        if (!animate)
        {
            // snap, no tween
            float fromEndXx = -delta * panelWidth;
            fromPos.x = fromEndXx;
            fromRT.anchoredPosition = fromPos;
            toPos.x = centerX;
            toRT.anchoredPosition = toPos;
            if (fadeDuringSlide)
            {
                fromCG.alpha = 0f;
                toCG.alpha = 1f;
            }
            fromRT.gameObject.SetActive(false);
            SetCGInteractable(toCG, true);
            currentTab = target;
            PositionPanelsForCurrent(target, instant: true, setActiveOnlyForCurrent: true);
            return;
        }
        isAnimating = true;
        // tween out / in
        float fromEndX = -delta * panelWidth;
        float toEndX = centerX;
        Tween outTween = fromRT.DOAnchorPosX(fromEndX, slideDuration).SetEase(slideEase);
        Tween inTween = toRT.DOAnchorPosX(toEndX, slideDuration).SetEase(slideEase);
        Tween fadeOut = null, fadeIn = null;
        if (fadeDuringSlide)
        {
            fadeOut = fromCG.DOFade(0f, Mathf.Min(fadeDuration, slideDuration)).SetEase(Ease.OutQuad);
            fadeIn = toCG.DOFade(1f, Mathf.Min(fadeDuration, slideDuration)).SetEase(Ease.OutQuad);
        }
        DOTween.Sequence()
               .Join(outTween)
               .Join(inTween)
               .Join(fadeOut ?? DOTween.Sequence())
               .Join(fadeIn ?? DOTween.Sequence())
               .OnComplete(() =>
               {
                   fromRT.gameObject.SetActive(false);
                   SetCGInteractable(toCG, true);
                   currentTab = target;
                   PositionPanelsForCurrent(target, instant: true, setActiveOnlyForCurrent: true);
                   isAnimating = false;
               });
    }
    // Maintain Instagram-like invariant: current at 0, left tabs at -width, right tabs at +width
    private void PositionPanelsForCurrent(MenuTab center, bool instant, bool setActiveOnlyForCurrent)
    {
        RectTransform homeRT = GetPanelRT(MenuTab.Home);
        RectTransform unitsRT = GetPanelRT(MenuTab.Units);
        RectTransform testRT = GetPanelRT(MenuTab.Test);
        float panelWidth = ((RectTransform)transform).rect.width + Mathf.Max(0f, extraSlidePadding);
        SetPanel(MenuTab.Home, homeRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        SetPanel(MenuTab.Units, unitsRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        SetPanel(MenuTab.Test, testRT, center, panelWidth, instant, setActiveOnlyForCurrent);
        void SetPanel(MenuTab tab, RectTransform rt, MenuTab centerTab, float width, bool instantPos, bool activeOnlyCurrent)
        {
            if (!rt) return;
            bool isCurrent = (tab == centerTab);
            float targetX;
            if (isCurrent)
            {
                targetX = 0f;
            }
            else
            {
                int tabSpatial = tab == MenuTab.Units ? 0 : tab == MenuTab.Home ? 1 : 2;
                int centerSpatial = centerTab == MenuTab.Units ? 0 : centerTab == MenuTab.Home ? 1 : 2;
                targetX = (tabSpatial - centerSpatial) * width;
            }
            if (instantPos)
            {
                var pos = rt.anchoredPosition;
                pos.x = targetX;
                rt.anchoredPosition = pos;
            }
            else
            {
                rt.DOAnchorPosX(targetX, 0.01f);
            }
            CanvasGroup cg = RequireCanvasGroup(rt);
            cg.alpha = isCurrent ? 1f : (fadeDuringSlide ? 0f : 1f);
            SetCGInteractable(cg, isCurrent);
            rt.gameObject.SetActive(activeOnlyCurrent ? isCurrent : true);
        }
    }
    private int GetSpatialIndex(MenuTab tab) => tab == MenuTab.Units ? 0 : tab == MenuTab.Home ? 1 : 2;
    private int IndexOf(MenuTab tab) => (int)tab;
    private void UpdateButtonVisuals(MenuTab tab)
    {
        bool homeSelected = (tab == MenuTab.Home);
        bool unitsSelected = (tab == MenuTab.Units);
        bool testSelected = (tab == MenuTab.Test);
        if (homeButton) homeButton.gameObject.SetActive(!homeSelected);
        if (homeSelectedButton) homeSelectedButton.gameObject.SetActive(homeSelected);
        if (unitsButton) unitsButton.gameObject.SetActive(!unitsSelected);
        if (unitsSelectedButton) unitsSelectedButton.gameObject.SetActive(unitsSelected);
        if (testButton) testButton.gameObject.SetActive(!testSelected);
        if (testSelectedButton) testSelectedButton.gameObject.SetActive(testSelected);
    }
    private void NudgeInactiveHomeWhenTest(MenuTab tab)
    {
        if (!homeButton) return;
        if (!(homeButton.transform is RectTransform rt)) return;
        Vector2 target = (tab == MenuTab.Test)
            ? homeButtonDefaultAnchoredPos + new Vector2(homeButtonOffsetX, 0f)
            : homeButtonDefaultAnchoredPos;
        rt.DOAnchorPos(target, 0.2f).SetEase(Ease.OutQuad);
    }
    // ---------------- UTILITIES ----------------
    private void PreparePanelsForAnimation()
    {
        // Ensure all panels exist, activate so we can position them
        if (homePanel) homePanel.SetActive(true);
        if (unitsPanel) unitsPanel.SetActive(true);
        if (testPanel) testPanel.SetActive(true);
        // Place according to currentTab and disable non-current
        PositionPanelsForCurrent(currentTab, instant: true, setActiveOnlyForCurrent: true);
    }
    private RectTransform GetPanelRT(MenuTab tab)
    {
        GameObject go = null;
        switch (tab)
        {
            case MenuTab.Home: go = homePanel; break;
            case MenuTab.Units: go = unitsPanel; break;
            case MenuTab.Test: go = testPanel; break;
        }
        return go ? go.GetComponent<RectTransform>() : null;
    }
    private CanvasGroup RequireCanvasGroup(RectTransform rt)
    {
        if (!rt) return null;
        var cg = rt.GetComponent<CanvasGroup>();
        if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }
    private void SetCGInteractable(CanvasGroup cg, bool on)
    {
        if (!cg) return;
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }
}

public class MainMenuPanelController3 : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject unitsPanel;
    [SerializeField] private GameObject testPanel;

    [Header("Main Buttons (small / inactive)")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button unitsButton;
    [SerializeField] private Button testButton;

    [Header("Selected Visual Buttons (big)")]
    [SerializeField] private Button homeSelectedButton;
    [SerializeField] private Button unitsSelectedButton;
    [SerializeField] private Button testSelectedButton;

    [Header("Home inactive position offset when Test is selected")]
    [SerializeField] private float homeButtonOffsetX = -20f;

    // store default anchored position of the inactive Home button
    private Vector2 homeButtonDefaultAnchoredPos;

    private enum MenuTab { Home, Units, Test }

    private void Start()
    {
        if (homeButton != null)
        {
            RectTransform rt = homeButton.transform as RectTransform;
            if (rt != null)
                homeButtonDefaultAnchoredPos = rt.anchoredPosition;
        }

        // Home/Battle is default
        SelectMenu(MenuTab.Home);
    }

    // These are called from the buttons' OnClick
    public void ShowHomePanel() => SelectMenu(MenuTab.Home);
    public void ShowUnitsPanel() => SelectMenu(MenuTab.Units);
    public void ShowTestPanel() => SelectMenu(MenuTab.Test);

    private void SelectMenu(MenuTab tab)
    {
        // 1) Panels
        if (homePanel) homePanel.SetActive(tab == MenuTab.Home);
        if (unitsPanel) unitsPanel.SetActive(tab == MenuTab.Units);
        if (testPanel) testPanel.SetActive(tab == MenuTab.Test);

        // 2) Buttons: for each tab, ONLY one of the two states is visible
        bool homeSelected = (tab == MenuTab.Home);
        bool unitsSelected = (tab == MenuTab.Units);
        bool testSelected = (tab == MenuTab.Test);

        if (homeButton) homeButton.gameObject.SetActive(!homeSelected);
        if (homeSelectedButton) homeSelectedButton.gameObject.SetActive(homeSelected);

        if (unitsButton) unitsButton.gameObject.SetActive(!unitsSelected);
        if (unitsSelectedButton) unitsSelectedButton.gameObject.SetActive(unitsSelected);

        if (testButton) testButton.gameObject.SetActive(!testSelected);
        if (testSelectedButton) testSelectedButton.gameObject.SetActive(testSelected);

        // 3) Move the inactive Home button when Test tab is selected
        if (homeButton != null)
        {
            RectTransform rt = homeButton.transform as RectTransform;
            if (rt != null)
            {
                if (tab == MenuTab.Test)
                    rt.anchoredPosition = homeButtonDefaultAnchoredPos + new Vector2(homeButtonOffsetX, 0f);
                else
                    rt.anchoredPosition = homeButtonDefaultAnchoredPos;
            }
        }
    }
}

public class MainMenuPanelController2 : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject unitsPanel;
    [SerializeField] private GameObject testPanel;

    [Header("Main Buttons (small)")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button unitsButton;
    [SerializeField] private Button testButton;

    [Header("Selected Visual Buttons (big / scaled clones)")]
    [SerializeField] private Button homeSelectedButton;
    [SerializeField] private Button unitsSelectedButton;
    [SerializeField] private Button testSelectedButton;

    private void Start()
    {
        // When we enter the Main Menu scene, always show Home/Battle by default
        ShowHomePanel();
    }

    // Called from the Home/Battle button OnClick
    public void ShowHomePanel()
    {
        ShowPanel(homePanel);
        SetSelectedVisual(homeSelectedButton);
    }

    // Called from the Units button OnClick
    public void ShowUnitsPanel()
    {
        ShowPanel(unitsPanel);
        SetSelectedVisual(unitsSelectedButton);
    }

    // Called from the Test button OnClick
    public void ShowTestPanel()
    {
        ShowPanel(testPanel);
        SetSelectedVisual(testSelectedButton);
    }

    // Enable only the requested panel, disable all others
    private void ShowPanel(GameObject panelToShow)
    {
        if (homePanel != null)
            homePanel.SetActive(panelToShow == homePanel);

        if (unitsPanel != null)
            unitsPanel.SetActive(panelToShow == unitsPanel);

        if (testPanel != null)
            testPanel.SetActive(panelToShow == testPanel);
    }

    // Turn on only the selected "big" button, turn off the others
    private void SetSelectedVisual(Button selectedVisualButton)
    {
        if (homeSelectedButton != null)
            homeSelectedButton.gameObject.SetActive(selectedVisualButton == homeSelectedButton);

        if (unitsSelectedButton != null)
            unitsSelectedButton.gameObject.SetActive(selectedVisualButton == unitsSelectedButton);

        if (testSelectedButton != null)
            testSelectedButton.gameObject.SetActive(selectedVisualButton == testSelectedButton);
    }
}

public class MainMenuPanelController1 : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject unitsPanel;
    [SerializeField] private GameObject testPanel;

    [Header("Tab Buttons")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button unitsButton;
    [SerializeField] private Button testButton;

    [Header("Button Scale Settings")]
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.3f;

    private void Start()
    {
        // When we enter the Main Menu scene, always show Home/Battle by default
        ShowHomePanel();
    }

    // Called from the Home/Battle button OnClick
    public void ShowHomePanel()
    {
        ShowPanel(homePanel);
        SetSelectedButton(homeButton);
    }

    // Called from the Units button OnClick
    public void ShowUnitsPanel()
    {
        ShowPanel(unitsPanel);
        SetSelectedButton(unitsButton);
    }

    // Called from the Test button OnClick
    public void ShowTestPanel()
    {
        ShowPanel(testPanel);
        SetSelectedButton(testButton);
    }

    // Enable only the requested panel, disable all others
    private void ShowPanel(GameObject panelToShow)
    {
        if (homePanel != null)
            homePanel.SetActive(panelToShow == homePanel);

        if (unitsPanel != null)
            unitsPanel.SetActive(panelToShow == unitsPanel);

        if (testPanel != null)
            testPanel.SetActive(panelToShow == testPanel);
    }

    // Scale up only the selected button, reset others
    private void SetSelectedButton(Button selectedButton)
    {
        SetButtonScale(homeButton, selectedButton == homeButton);
        SetButtonScale(unitsButton, selectedButton == unitsButton);
        SetButtonScale(testButton, selectedButton == testButton);
    }

    // Helper for scaling a single button
    private void SetButtonScale(Button button, bool isSelected)
    {
        if (button == null) return;

        float targetScale = isSelected ? selectedScale : normalScale;
        button.transform.localScale = new Vector3(targetScale, targetScale, 1f);
    }
}
