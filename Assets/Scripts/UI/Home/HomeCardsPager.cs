using System;
using UnityEngine;
using UnityEngine.UI;




public class HomeCardsPager : MonoBehaviour
{
    public event Action<int> OnIndexChanged;
    public event Action<int, GameObject> OnCardRebuilt;

    [Header("Hierarchy")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private ScrollRect scrollRect;   // << add: assign your ScrollRect here

    [Header("Cards (prefabs + build)")]
    [SerializeField] private GameObject normalCardPrefab;
    [SerializeField] private GameObject selectedCardPrefab;
    [SerializeField, Min(1)] private int cardCount = 20;

    [Header("Layout")]
    [SerializeField, Min(0)] private float spacing = 20f;

    [Header("Motion")]
    [SerializeField, Range(0.05f, 0.40f)] private float smoothTime = 0.18f;
    [SerializeField, Range(0.1f, 2f)] private float pixelSnapEpsilon = 0.5f;

    [Header("Data Binding")]
    [SerializeField, Min(1)] private int levelId = 1;

    [Header("Shared UI on Pager")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button disabledButton;
    [SerializeField] private RectTransform toastAnchor;
    [SerializeField] private HomeManager homeManager;

    private int currentIndex;
    public int CurrentIndex => currentIndex;

    private HorizontalLayoutGroup hlg;
    private bool built;

    private enum SlotKind { Normal, Selected }
    private SlotKind[] slotKinds;

    // jitter control
    private float targetX;
    private float xVelocity;
    private Canvas rootCanvas;

    private bool isAnimating = false;
    [SerializeField, Range(0.25f, 0.60f)] private float slideDuration = 0.33f;

    private ContentSizeFitter contentSizeFitter; // optional: if you have one on Content


    private void Start() => BuildIfNeeded();

    public void BuildIfNeeded()
    {
        if (built) return;

        if (!viewport || !content || !normalCardPrefab || !selectedCardPrefab)
        {
            Debug.LogError("[HomeCardsPager] Assign viewport, content, normalCardPrefab, selectedCardPrefab.");
            return;
        }
        contentSizeFitter = content.GetComponent<ContentSizeFitter>(); // may be null, that’s fine


        // Disable/lock ScrollRect so users cannot drag
        if (scrollRect)
        {
            scrollRect.inertia = false;
            scrollRect.horizontal = false;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            scrollRect.enabled = false; // fully disables drag & built-in scrolling
            scrollRect.StopMovement();
        }

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (!hlg) hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = spacing;
        // Inspector on this HLG:
        //  - Child Control Size: Width OFF, Height OFF
        //  - Child Force Expand: Width OFF, Height OFF

        slotKinds = new SlotKind[cardCount];

        for (int i = 0; i < cardCount; i++)
        {
            bool isSel = (i == 0);
            var prefab = isSel ? selectedCardPrefab : normalCardPrefab;
            slotKinds[i] = isSel ? SlotKind.Selected : SlotKind.Normal;

            var go = Instantiate(prefab, content);
            BindCard(i, go);
            OnCardRebuilt?.Invoke(i, go);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();

        currentIndex = 0;
        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        var pos = content.anchoredPosition;
        pos.x = targetX;
        content.anchoredPosition = pos;

        UpdatePlayButtonsForIndex(currentIndex);
        HookButtons();
        UpdateButtons();
        built = true;
    }

    //private void Update()
    //{
    //    if (!built) return;

    //    // If some component (e.g., an enabled ScrollRect somewhere) tries to move the content,
    //    // hard-override it by driving position from our own animation only.
    //    float newX = Mathf.SmoothDamp(content.anchoredPosition.x, targetX, ref xVelocity, smoothTime);

    //    newX = SnapToPixelGrid(newX);

    //    if (Mathf.Abs(newX - targetX) <= pixelSnapEpsilon)
    //    {
    //        newX = targetX;
    //        xVelocity = 0f;
    //    }

    //    var pos = content.anchoredPosition;
    //    pos.x = newX;
    //    content.anchoredPosition = pos;

    //    // Kill any stray ScrollRect velocity (paranoia guard, even if disabled)
    //    if (scrollRect && (scrollRect.velocity.sqrMagnitude > 0f))
    //    {
    //        scrollRect.velocity = Vector2.zero;
    //    }
    //}
    private System.Collections.IEnumerator AnimateToTargetX(float finalX)
    {
        isAnimating = true;

        // Only disable ContentSizeFitter (if present) to avoid its re-sizing during the slide
        bool fitterWasEnabled = contentSizeFitter ? contentSizeFitter.enabled : false;
        if (contentSizeFitter) contentSizeFitter.enabled = false;

        // Make sure current layout is fully computed before we start
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
        yield return null; // let one frame render the settled layout

        float startX = content.anchoredPosition.x;
        float t = 0f;

        float Snap(float x)
        {
            if (!rootCanvas) return x;
            float upp = 1f / Mathf.Max(1e-3f, rootCanvas.scaleFactor);
            return Mathf.Round(x / upp) * upp;
        }

        while (t < slideDuration)
        {
            t += Time.unscaledDeltaTime;               // smooth independent of timescale
            float u = Mathf.Clamp01(t / slideDuration);
            float eased = 1f - Mathf.Pow(1f - u, 3f); // easeOutCubic

            float x = Mathf.LerpUnclamped(startX, finalX, eased);
            var pos = content.anchoredPosition;
            pos.x = Snap(x);
            content.anchoredPosition = pos;

            yield return null;
        }

        // Hit exact target
        var final = content.anchoredPosition;
        final.x = Snap(finalX);
        content.anchoredPosition = final;

        // Re-enable fitter and settle once
        if (contentSizeFitter) contentSizeFitter.enabled = fitterWasEnabled;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        isAnimating = false;
    }


    private float SnapToPixelGrid(float x)
    {
        if (!rootCanvas) return x;
        float unitsPerPixel = 1f / Mathf.Max(1e-3f, rootCanvas.scaleFactor);
        return Mathf.Round(x / unitsPerPixel) * unitsPerPixel;
    }

    private void HookButtons()
    {
        if (leftButton)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(Prev);
        }
        if (rightButton)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(Next);
        }
    }

    private void UpdateButtons1()
    {
        if (leftButton) leftButton.interactable = currentIndex > 0;
        if (rightButton) rightButton.interactable = currentIndex < cardCount - 1;
    }
    private void UpdateButtons()
    {
        bool canGoLeft = currentIndex > 0;
        bool canGoRight = currentIndex < cardCount - 1;

        if (leftButton)
        {
             leftButton.gameObject.SetActive(canGoLeft);
        }
        if (rightButton)
        {
            rightButton.gameObject.SetActive(canGoRight);
        }
    }


    public void Next1()
    {
        if (!built || currentIndex >= cardCount - 1) return;

        SwapSelected(currentIndex, currentIndex + 1);

        currentIndex++;
        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);
        OnIndexChanged?.Invoke(currentIndex);
    }

    public void Prev1()
    {
        if (!built || currentIndex <= 0) return;

        SwapSelected(currentIndex, currentIndex - 1);

        currentIndex--;
        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);
        OnIndexChanged?.Invoke(currentIndex);
    }
    public void Next()
    {
        if (!built || isAnimating || currentIndex >= cardCount - 1) return;

        SwapSelected(currentIndex, currentIndex + 1);
        currentIndex++;

        targetX = ComputeTargetContentX(currentIndex);

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);
        OnIndexChanged?.Invoke(currentIndex);

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        StopAllCoroutines();
        StartCoroutine(AnimateToTargetX(targetX));
    }

    public void Prev()
    {
        if (!built || isAnimating || currentIndex <= 0) return;

        SwapSelected(currentIndex, currentIndex - 1);
        currentIndex--;

        targetX = ComputeTargetContentX(currentIndex);

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);
        OnIndexChanged?.Invoke(currentIndex);

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        StopAllCoroutines();
        StartCoroutine(AnimateToTargetX(targetX));
    }


    // —— Safe swap order to avoid phantom cards ——
    private void SwapSelected(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= content.childCount || newIndex >= content.childCount)
            return;

        if (newIndex > oldIndex)
        {
            ReplaceAt(newIndex, selectedCardPrefab, SlotKind.Selected);
            ReplaceAt(oldIndex, normalCardPrefab, SlotKind.Normal);
        }
        else
        {
            ReplaceAt(oldIndex, normalCardPrefab, SlotKind.Normal);
            ReplaceAt(newIndex, selectedCardPrefab, SlotKind.Selected);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void ReplaceAt(int index, GameObject prefab, SlotKind kind)
    {
        index = Mathf.Clamp(index, 0, content.childCount - 1);

        var oldGO = content.GetChild(index).gameObject;
        int sib = oldGO.transform.GetSiblingIndex();
        Destroy(oldGO);

        var go = Instantiate(prefab, content);
        go.transform.SetSiblingIndex(sib);

        slotKinds[index] = kind;

        BindCard(index, go);
        OnCardRebuilt?.Invoke(index, go);
    }

    // —— Centering for variable widths ——
    private float ComputeTargetContentX(int index)
    {
        float viewportCenter = viewport.rect.width * 0.5f;

        float padL = hlg ? hlg.padding.left : 0f;
        float padR = hlg ? hlg.padding.right : 0f;
        float space = hlg ? hlg.spacing : spacing;

        float before = padL;
        for (int i = 0; i < index; i++)
            before += GetPreferredWidthAt(i) + space;

        float w = GetPreferredWidthAt(index);
        float cardCenter = before + w * 0.5f;

        float desired = viewportCenter - cardCenter;

        float total = padL + padR;
        for (int i = 0; i < cardCount; i++)
            total += GetPreferredWidthAt(i) + (i < cardCount - 1 ? space : 0f);

        float minX = Mathf.Min(0f, viewport.rect.width - total);
        float maxX = 0f;
        return Mathf.Clamp(desired, minX, maxX);
    }

    private float GetPreferredWidthAt(int i)
    {
        if (i < 0 || i >= content.childCount) return 0f;
        var rt = content.GetChild(i) as RectTransform;
        if (!rt) return 0f;

        var le = rt.GetComponent<LayoutElement>();
        if (le && le.preferredWidth > 0f) return le.preferredWidth;

        return rt.rect.width;
    }

    // —— Bind card title/stars/locked ——
    private void BindCard1(int index, GameObject go)
    {
        if (!go) return;
        var sc = go.GetComponent<StageCard>() ?? go.GetComponentInChildren<StageCard>(true);
        if (!sc) return;

        sc.SetTitle($"STAGE {levelId}-{index + 1}");

        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        int stars = 0; bool locked = false;
        if (lvl != null && lvl.stars != null && index < lvl.stars.Length)
        {
            stars = Mathf.Clamp(lvl.stars[index], 0, 3);
            locked = index > lvl.highestUnlocked;
        }

        sc.SetStars(stars);
        sc.SetLocked(locked);
    }

    private void BindCard(int index, GameObject go)
    {
        if (!go) return;
        var sc = go.GetComponent<StageCard>() ?? go.GetComponentInChildren<StageCard>(true);
        if (!sc) return;

        sc.SetTitle($"STAGE {levelId}-{index + 1}");

        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        int stars = 0; bool locked = false;
        if (lvl != null && lvl.stars != null && index < lvl.stars.Length)
        {
            stars = Mathf.Clamp(lvl.stars[index], 0, 3);
            locked = index > lvl.highestUnlocked;
        }

        sc.SetStars(stars);
        sc.SetLocked(locked);

        // New: reward preview using HomeManager's shared config
        if (homeManager != null)
        {
            WinPanel.RewardValues preview;
            if (homeManager.TryGetStageRewardPreview(index, out preview))
            {
                // StageCard must implement this method and have 3 TMP_Texts for coins/gems/XP
                sc.SetRewardPreview(preview.coins, preview.gems, preview.heroXP);
            }
        }
    }


    // —— Pager-level Play/Disabled buttons ——
    private void UpdatePlayButtonsForIndex(int index)
    {
        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        bool locked = (lvl == null) || (index > lvl.highestUnlocked);

        if (playButton) playButton.gameObject.SetActive(!locked);
        if (disabledButton) disabledButton.gameObject.SetActive(locked);

        if (disabledButton)
        {
            disabledButton.onClick.RemoveAllListeners();
            if (locked && homeManager)
            {
                disabledButton.onClick.AddListener(() =>
                {
                    homeManager.ShowLockedStageToast(toastAnchor ? toastAnchor : (RectTransform)transform);
                });
            }
        }
    }



    public void JumpToIndex(int newIndex, bool animate = true, bool invokeEvent = true)
    {
        BuildIfNeeded();

        newIndex = Mathf.Clamp(newIndex, 0, cardCount - 1);

        if (!built) return;

        if (newIndex == currentIndex)
        {
            UpdateButtons();
            UpdatePlayButtonsForIndex(currentIndex);

            if (!animate)
            {
                targetX = ComputeTargetContentX(currentIndex);
                var p = content.anchoredPosition;
                p.x = targetX;
                content.anchoredPosition = p;
            }

            if (invokeEvent)
                OnIndexChanged?.Invoke(currentIndex);

            return;
        }

        SwapSelected(currentIndex, newIndex);
        currentIndex = newIndex;

        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);

        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();

        StopAllCoroutines();

        if (animate)
        {
            StartCoroutine(AnimateToTargetX(targetX));
        }
        else
        {
            var pos = content.anchoredPosition;
            pos.x = targetX;
            content.anchoredPosition = pos;
        }

        if (invokeEvent)
            OnIndexChanged?.Invoke(currentIndex);
    }

}

public class HomeCardsPager3 : MonoBehaviour
{
    public event Action<int> OnIndexChanged;
    public event Action<int, GameObject> OnCardRebuilt;

    [Header("Hierarchy")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Cards (prefabs + build)")]
    [SerializeField] private GameObject normalCardPrefab;    // non-scaled
    [SerializeField] private GameObject selectedCardPrefab;  // pre-scaled look
    [SerializeField, Min(1)] private int cardCount = 20;

    [Header("Layout")]
    [SerializeField, Min(0)] private float spacing = 20f; // must match HLG spacing

    [Header("Motion")]
    [SerializeField, Range(0.05f, 0.40f)] private float smoothTime = 0.18f; // slide feel
    [SerializeField, Range(0.1f, 2f)] private float pixelSnapEpsilon = 0.5f;

    [Header("Data Binding")]
    [SerializeField, Min(1)] private int levelId = 1; // for STAGE <level>-<n>

    [Header("Shared UI on Pager (not in cards)")]
    [SerializeField] private Button playButton;             // visible if selected stage is unlocked
    [SerializeField] private Button disabledButton;         // visible if selected stage is locked
    [SerializeField] private RectTransform toastAnchor;     // where the toast should spawn
    [SerializeField] private HomeManager homeManager;       // for ShowLockedStageToast(anchor)

    private int currentIndex;
    public int CurrentIndex => currentIndex;

    private HorizontalLayoutGroup hlg;
    private bool built;

    private enum SlotKind { Normal, Selected }
    private SlotKind[] slotKinds;

    // smooth/jitter control
    private float targetX;      // stable target for currentIndex
    private float xVelocity;    // SmoothDamp velocity
    private Canvas rootCanvas;  // for pixel snapping

    private void Start() => BuildIfNeeded();

    public void BuildIfNeeded()
    {
        if (built) return;

        if (!viewport || !content || !normalCardPrefab || !selectedCardPrefab)
        {
            Debug.LogError("[HomeCardsPager] Assign viewport, content, normalCardPrefab, selectedCardPrefab.");
            return;
        }

        // Clean children
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // Ensure layout group
        hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (!hlg) hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = spacing;
        // Inspector on this HLG:
        // - Child Control Size: Width OFF, Height OFF
        // - Child Force Expand: Width OFF, Height OFF

        slotKinds = new SlotKind[cardCount];

        // Build row: index 0 selected, others normal
        for (int i = 0; i < cardCount; i++)
        {
            bool isSel = (i == 0);
            var prefab = isSel ? selectedCardPrefab : normalCardPrefab;
            slotKinds[i] = isSel ? SlotKind.Selected : SlotKind.Normal;

            var go = Instantiate(prefab, content);
            BindCard(i, go);
            OnCardRebuilt?.Invoke(i, go);
        }

        // Force layout once so widths are settled
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // Canvas for pixel snapping
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();

        // Center on index 0 (compute a stable target once)
        currentIndex = 0;
        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        var pos = content.anchoredPosition;
        pos.x = targetX;
        content.anchoredPosition = pos;

        UpdatePlayButtonsForIndex(currentIndex);
        HookButtons();
        UpdateButtons();
        built = true;
    }

    private void Update()
    {
        if (!built) return;

        // Smooth toward precomputed target
        float newX = Mathf.SmoothDamp(content.anchoredPosition.x, targetX, ref xVelocity, smoothTime);

        // Pixel snap to prevent shimmer
        newX = SnapToPixelGrid(newX);

        // If close enough, snap to target and stop
        if (Mathf.Abs(newX - targetX) <= pixelSnapEpsilon)
        {
            newX = targetX;
            xVelocity = 0f;
        }

        var pos = content.anchoredPosition;
        pos.x = newX;
        content.anchoredPosition = pos;
    }

    private float SnapToPixelGrid(float x)
    {
        if (!rootCanvas) return x;
        float unitsPerPixel = 1f / Mathf.Max(1e-3f, rootCanvas.scaleFactor);
        return Mathf.Round(x / unitsPerPixel) * unitsPerPixel;
    }

    private void HookButtons()
    {
        if (leftButton)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(Prev);
        }
        if (rightButton)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(Next);
        }
    }

    private void UpdateButtons()
    {
        if (leftButton) leftButton.interactable = currentIndex > 0;
        if (rightButton) rightButton.interactable = currentIndex < cardCount - 1;
    }

    public void Next()
    {
        if (!built || currentIndex >= cardCount - 1) return;

        // Swap with safe ordering (prevents index drift & phantom cards)
        SwapSelected(currentIndex, currentIndex + 1);

        currentIndex++;
        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);
        OnIndexChanged?.Invoke(currentIndex);
    }

    public void Prev()
    {
        if (!built || currentIndex <= 0) return;

        SwapSelected(currentIndex, currentIndex - 1);

        currentIndex--;
        targetX = ComputeTargetContentX(currentIndex);
        xVelocity = 0f;

        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);
        OnIndexChanged?.Invoke(currentIndex);
    }

    // ——— Swap logic with safe ordering + rebinding ———
    private void SwapSelected(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= content.childCount || newIndex >= content.childCount)
            return;

        // Moving RIGHT: replace new first, then old
        if (newIndex > oldIndex)
        {
            ReplaceAt(newIndex, selectedCardPrefab, SlotKind.Selected);
            ReplaceAt(oldIndex, normalCardPrefab, SlotKind.Normal);
        }
        else // Moving LEFT: replace old first, then new
        {
            ReplaceAt(oldIndex, normalCardPrefab, SlotKind.Normal);
            ReplaceAt(newIndex, selectedCardPrefab, SlotKind.Selected);
        }

        // One explicit rebuild after both swaps
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void ReplaceAt(int index, GameObject prefab, SlotKind kind)
    {
        index = Mathf.Clamp(index, 0, content.childCount - 1);

        var oldGO = content.GetChild(index).gameObject;
        int sib = oldGO.transform.GetSiblingIndex();

        Destroy(oldGO);

        var go = Instantiate(prefab, content);
        go.transform.SetSiblingIndex(sib);

        slotKinds[index] = kind;

        // Re-bind title/stars/lock for the replaced slot
        BindCard(index, go);
        OnCardRebuilt?.Invoke(index, go);
    }

    // ——— Centering with variable widths ———
    private float ComputeTargetContentX(int index)
    {
        float viewportCenter = viewport.rect.width * 0.5f;

        float padL = hlg ? hlg.padding.left : 0f;
        float padR = hlg ? hlg.padding.right : 0f;
        float space = hlg ? hlg.spacing : spacing;

        float before = padL;
        for (int i = 0; i < index; i++)
            before += GetPreferredWidthAt(i) + space;

        float w = GetPreferredWidthAt(index);
        float cardCenter = before + w * 0.5f;

        float desired = viewportCenter - cardCenter;

        // compute total width to clamp
        float total = padL + padR;
        for (int i = 0; i < cardCount; i++)
            total += GetPreferredWidthAt(i) + (i < cardCount - 1 ? space : 0f);

        float minX = Mathf.Min(0f, viewport.rect.width - total);
        float maxX = 0f;
        return Mathf.Clamp(desired, minX, maxX);
    }

    private float GetPreferredWidthAt(int i)
    {
        if (i < 0 || i >= content.childCount) return 0f;
        var rt = content.GetChild(i) as RectTransform;
        if (!rt) return 0f;

        var le = rt.GetComponent<LayoutElement>();
        if (le && le.preferredWidth > 0f) return le.preferredWidth;

        return rt.rect.width; // prefab width
    }

    // ——— Bind a card from SaveSystem + decorate locked/unlocked state ———
    private void BindCard(int index, GameObject go)
    {
        if (!go) return;

        var sc = go.GetComponent<StageCard>() ?? go.GetComponentInChildren<StageCard>(true);
        if (!sc) return;

        // Title
        sc.SetTitle($"STAGE {levelId}-{index + 1}");

        // Stars + lock logic
        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        int stars = 0;
        bool locked = false;
        if (lvl != null && lvl.stars != null && index < lvl.stars.Length)
        {
            stars = Mathf.Clamp(lvl.stars[index], 0, 3);
            locked = index > lvl.highestUnlocked;
        }

        sc.SetStars(stars);
        sc.SetLocked(locked); // this only affects visuals INSIDE the card, if any (you can keep it false/hidden)
        // Note: The actual Play/Disabled buttons are on the pager object; see UpdatePlayButtonsForIndex.
    }

    // ——— Shared Play/Disabled buttons on the pager object ———
    private void UpdatePlayButtonsForIndex(int index)
    {
        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        bool locked = (lvl == null) || (index > lvl.highestUnlocked);

        if (playButton) playButton.gameObject.SetActive(!locked);
        if (disabledButton) disabledButton.gameObject.SetActive(locked);

        if (disabledButton)
        {
            disabledButton.onClick.RemoveAllListeners();
            if (locked && homeManager)
            {
                disabledButton.onClick.AddListener(() =>
                {
                    homeManager.ShowLockedStageToast(toastAnchor ? toastAnchor : (RectTransform)transform);
                });
            }
        }
    }
}


public class HomeCardsPager2 : MonoBehaviour
{
    public event Action<int> OnIndexChanged;
    public event Action<int, GameObject> OnCardRebuilt; // still available if you want it

    [Header("Hierarchy")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Cards (prefabs + build)")]
    [SerializeField] private GameObject normalCardPrefab;    // non-scaled
    [SerializeField] private GameObject selectedCardPrefab;  // your pre-scaled visual
    [SerializeField, Min(1)] private int cardCount = 20;

    [Header("Layout")]
    [SerializeField, Min(0)] private float spacing = 20f; // must match HLG spacing

    [Header("Motion")]
    [SerializeField, Range(1f, 30f)] private float snapSpeed = 12f;

    [Header("Data Binding")]
    [SerializeField, Min(1)] private int levelId = 1;       // used to label STAGE <level>-<n>
    [SerializeField] private bool bindOnBuildAndSwap = true; // auto-apply stars/title/lock

    private int currentIndex;
    public int CurrentIndex => currentIndex;

    private HorizontalLayoutGroup hlg;
    private bool built;

    private enum SlotKind { Normal, Selected }
    private SlotKind[] slotKinds;


    // Buttons & toast that live on the pager object (not inside cards)
    [SerializeField] private Button playButton;           // your real Play/Start button in the Home panel
    [SerializeField] private Button disabledButton;       // greyed-out button shown when locked
    [SerializeField] private RectTransform toastAnchor;   // where the toast should spawn
    [SerializeField] private HomeManager homeManager;     // drag from scene (for ShowLockedStageToast)



    private void Start() => BuildIfNeeded();

    public void BuildIfNeeded()
    {
        if (built) return;

        if (!viewport || !content || !normalCardPrefab || !selectedCardPrefab)
        {
            Debug.LogError("[HomeCardsPager] Assign viewport, content, normalCardPrefab, selectedCardPrefab.");
            return;
        }

        // wipe
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // layout
        hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (!hlg) hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = spacing;
        // IMPORTANT in Inspector on this HLG:
        // - Child Control Size: Width OFF, Height OFF
        // - Child Force Expand: Width OFF, Height OFF

        slotKinds = new SlotKind[cardCount];

        // initial row (index 0 selected)
        for (int i = 0; i < cardCount; i++)
        {
            bool isSel = (i == 0);
            var prefab = isSel ? selectedCardPrefab : normalCardPrefab;
            slotKinds[i] = isSel ? SlotKind.Selected : SlotKind.Normal;

            var go = Instantiate(prefab, content);
            BindCard(i, go);                 // << rebind title/stars/lock here
            OnCardRebuilt?.Invoke(i, go);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        currentIndex = 0;
        var pos = content.anchoredPosition;
        pos.x = ComputeTargetContentX(currentIndex);
        content.anchoredPosition = pos;

        UpdatePlayButtonsForIndex(currentIndex);   // << add this line


        HookButtons();
        UpdateButtons();
        built = true;
    }

    private void Update()
    {
        if (!built) return;

        float targetX = ComputeTargetContentX(currentIndex);
        var pos = content.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * snapSpeed);
        content.anchoredPosition = pos;
    }

    private void HookButtons()
    {
        if (leftButton)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(Prev);
        }
        if (rightButton)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(Next);
        }
    }

    private void UpdateButtons()
    {
        if (leftButton) leftButton.interactable = currentIndex > 0;
        if (rightButton) rightButton.interactable = currentIndex < cardCount - 1;
    }

    public void Next()
    {
        if (!built || currentIndex >= cardCount - 1) return;
        SwapSelected(currentIndex, currentIndex + 1);
        currentIndex++;
        UpdateButtons();

        UpdatePlayButtonsForIndex(currentIndex);     // << add this

        OnIndexChanged?.Invoke(currentIndex);   // header text, etc.
    }

    public void Prev()
    {
        if (!built || currentIndex <= 0) return;
        SwapSelected(currentIndex, currentIndex - 1);
        currentIndex--;
        UpdateButtons();
        UpdatePlayButtonsForIndex(currentIndex);     // << add this


        OnIndexChanged?.Invoke(currentIndex);
    }

    private void SwapSelected1(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= content.childCount || newIndex >= content.childCount)
            return;

        // 1) old selected -> normal
        if (slotKinds[oldIndex] == SlotKind.Selected)
        {
            var oldGO = content.GetChild(oldIndex).gameObject;
            int sib = oldGO.transform.GetSiblingIndex();
            Destroy(oldGO);
            var normal = Instantiate(normalCardPrefab, content);
            normal.transform.SetSiblingIndex(sib);
            slotKinds[oldIndex] = SlotKind.Normal;
            BindCard(oldIndex, normal);       // << rebind
            OnCardRebuilt?.Invoke(oldIndex, normal);
        }

        // 2) new index -> selected
        if (slotKinds[newIndex] == SlotKind.Normal)
        {
            var oldGO = content.GetChild(newIndex).gameObject;
            int sib = oldGO.transform.GetSiblingIndex();
            Destroy(oldGO);
            var sel = Instantiate(selectedCardPrefab, content);
            sel.transform.SetSiblingIndex(sib);
            slotKinds[newIndex] = SlotKind.Selected;
            BindCard(newIndex, sel);          // << rebind
            OnCardRebuilt?.Invoke(newIndex, sel);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
    private void SwapSelected(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= content.childCount || newIndex >= content.childCount)
            return;

        // If nothing to do, bail
        if (slotKinds[oldIndex] == SlotKind.Normal && slotKinds[newIndex] == SlotKind.Selected)
            return;

        // Moving RIGHT: newIndex > oldIndex
        if (newIndex > oldIndex)
        {
            // 1) Turn the new slot into Selected first (indices still valid)
            ReplaceAt(newIndex, selectedCardPrefab, SlotKind.Selected);

            // 2) Then turn the old slot into Normal
            ReplaceAt(oldIndex, normalCardPrefab, SlotKind.Normal);
        }
        else // Moving LEFT: newIndex < oldIndex
        {
            // 1) Turn the old slot into Normal first
            ReplaceAt(oldIndex, normalCardPrefab, SlotKind.Normal);

            // 2) Then turn the new slot into Selected
            ReplaceAt(newIndex, selectedCardPrefab, SlotKind.Selected);
        }

        // Rebuild for immediate layout update
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    // Replace the child at 'index' with 'prefab' while preserving sibling order
    private void ReplaceAt(int index, GameObject prefab, SlotKind kind)
    {
        // Defensive clamp in case the list changed
        index = Mathf.Clamp(index, 0, content.childCount - 1);

        var oldGO = content.GetChild(index).gameObject;
        int sib = oldGO.transform.GetSiblingIndex();

        Destroy(oldGO);

        var go = Instantiate(prefab, content);
        go.transform.SetSiblingIndex(sib);

        slotKinds[index] = kind;

        // Re-bind card data (title/stars/lock) every time we replace
        BindCard(index, go);
        OnCardRebuilt?.Invoke(index, go);
    }


    // ---------- centering with variable widths ----------
    private float ComputeTargetContentX(int index)
    {
        float viewportCenter = viewport.rect.width * 0.5f;

        float padL = hlg ? hlg.padding.left : 0f;
        float padR = hlg ? hlg.padding.right : 0f;
        float space = hlg ? hlg.spacing : spacing;

        float before = padL;
        for (int i = 0; i < index; i++)
            before += GetPreferredWidthAt(i) + space;

        float w = GetPreferredWidthAt(index);
        float cardCenter = before + w * 0.5f;

        float desired = viewportCenter - cardCenter;

        float total = padL + padR;
        for (int i = 0; i < cardCount; i++)
            total += GetPreferredWidthAt(i) + (i < cardCount - 1 ? space : 0f);

        float minX = Mathf.Min(0f, viewport.rect.width - total);
        float maxX = 0f;
        return Mathf.Clamp(desired, minX, maxX);
    }

    private float GetPreferredWidthAt(int i)
    {
        if (i < 0 || i >= content.childCount) return 0f;
        var rt = content.GetChild(i) as RectTransform;
        if (!rt) return 0f;

        var le = rt.GetComponent<LayoutElement>();
        if (le && le.preferredWidth > 0f) return le.preferredWidth;

        return rt.rect.width; // prefab width
    }

    // ---------- binding to your SaveSystem + StageCard ----------
    private void BindCard(int index, GameObject go)
    {
        if (!bindOnBuildAndSwap || go == null) return;

        // Find StageCard on this GO (or its children)
        var sc = go.GetComponent<StageCard>();
        if (!sc) sc = go.GetComponentInChildren<StageCard>(true);
        if (!sc) return;

        // 1) label
        if (!string.IsNullOrEmpty($"STAGE {levelId}-{index + 1}") && sc.titleText)
            sc.titleText.text = $"STAGE {levelId}-{index + 1}";

        // 2) stars + lock from SaveSystem
        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        int stars = 0;
        bool locked = false;
        if (lvl != null && lvl.stars != null && index < lvl.stars.Length)
        {
            stars = Mathf.Clamp(lvl.stars[index], 0, 3);
            locked = index > lvl.highestUnlocked;
        }

        sc.SetStars(stars); // your StageCard exclusive sections fix will render correctly

        //if (sc.lockOverlay) sc.lockOverlay.SetActive(locked);

        //// after sc.SetStars(stars); sc.SetLocked(locked);
        //if (locked)
        //{
        //    sc.WireLockedToast(anchor =>
        //    {
        //        if (homeManager) homeManager.ShowLockedStageToast(anchor);
        //    });
        //}
        //else
        //{
        //    // remove any old wiring when card becomes unlocked
        //    sc.WireLockedToast(null);
        //}

    }

    private void UpdatePlayButtonsForIndex(int index)
    {
        // Decide locked/unlocked from SaveSystem for the CURRENT card index
        var lvl = SaveSystem.EnsureLevel(levelId, cardCount);
        bool locked = (lvl == null) || (index > lvl.highestUnlocked);

        // Toggle which CTA is visible
        if (playButton) playButton.gameObject.SetActive(!locked);
        if (disabledButton) disabledButton.gameObject.SetActive(locked);

        // Wire disabled button to show the toast
        if (disabledButton)
        {
            disabledButton.onClick.RemoveAllListeners();
            if (locked && homeManager)
            {
                disabledButton.onClick.AddListener(() =>
                {
                    // Spawn the “not reached stage” toast
                    homeManager.ShowLockedStageToast(toastAnchor ? toastAnchor : (RectTransform)transform);
                });
            }
        }
    }
    
}



public class HomeCardsPager1 : MonoBehaviour
{
    public event Action<int> OnIndexChanged;

    [Header("Hierarchy")]
    [SerializeField] private RectTransform viewport;  // <-- assign Viewport
    [SerializeField] private RectTransform content;   // Content holding the cards
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Cards (prefab + build)")]
    [SerializeField] private GameObject cardPrefab;
    [SerializeField, Min(1)] private int cardCount = 8;

    [Header("Layout")]
    [SerializeField, Min(1)] private float cardWidth = 300f; // your card width
    [SerializeField, Min(0)] private float spacing = 20f;    // same as HLayout spacing

    [Header("Motion & Scale")]
    [SerializeField, Range(1f, 30f)] private float snapSpeed = 12f;
    [SerializeField, Range(1f, 30f)] private float scaleSpeed = 12f;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float otherScale = 1.0f;

    private int currentIndex = 0;
    public int CurrentIndex => currentIndex;

    private float[] cardCenters; // X centers of each card in Content local space
    private HorizontalLayoutGroup hlg;
    private bool built;

    public void BuildIfNeeded()
    {
        if (built) return;

        if (!viewport || !content || !cardPrefab)
        {
            Debug.LogError("Assign viewport, content, and cardPrefab in Inspector.");
            return;
        }

        // Clean any old children (optional)
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // Instantiate cards in a line; HLayout will place them
        for (int i = 0; i < cardCount; i++)
        {
            var go = Instantiate(cardPrefab, content);
            var rt = (RectTransform)go.transform;
            // enforce width (optional if prefab already has it)
            rt.sizeDelta = new Vector2(cardWidth, rt.sizeDelta.y);
            rt.localScale = Vector3.one;
            // Example: label
            // var txt = go.GetComponentInChildren<TMPro.TMP_Text>();
            // if (txt) txt.text = $"Level {i + 1}";
        }

        hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (!hlg) hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        // Make sure these match your Inspector:
        hlg.spacing = spacing;
        // Padding should be symmetric if you want the same left/right margin:
        // hlg.padding.left = 125; hlg.padding.right = 125;

        // Force layout so sizes/positions are up to date this frame
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // Precompute the X center of each card in Content space:
        cardCenters = new float[cardCount];
        float x = hlg.padding.left + cardWidth * 0.5f; // center of card 0
        for (int i = 0; i < cardCount; i++)
        {
            cardCenters[i] = x;
            // advance by card width + spacing (no spacing after the last; harmless to add then not use)
            x += cardWidth + spacing;
        }

        // Start on the first card, fully centered
        currentIndex = 0;
        content.anchoredPosition = new Vector2(ComputeTargetContentX(currentIndex), content.anchoredPosition.y);

        HookButtons();
        UpdateButtons();
        built = true;
    }

    private void Start() => BuildIfNeeded();

    private void Update()
    {
        if (!built) return;

        // 1) Smooth pan to keep the selected card centered in the viewport
        float targetX = ComputeTargetContentX(currentIndex);
        var pos = content.anchoredPosition;
        pos.x = Mathf.Lerp(pos.x, targetX, Time.deltaTime * snapSpeed);
        content.anchoredPosition = pos;

        // 2) Scale cards (center vs others)
        for (int i = 0; i < content.childCount; i++)
        {
            Transform t = content.GetChild(i);
            float goal = (i == currentIndex) ? selectedScale : otherScale;
            t.localScale = Vector3.Lerp(t.localScale, new Vector3(goal, goal, 1f), Time.deltaTime * scaleSpeed);
        }
    }

    private float ComputeTargetContentX(int index)
    {
        // We want: (card center in content) to line up with (viewport center in screen)
        float viewportCenter = viewport.rect.width * 0.5f;
        float cardCenter = cardCenters[index];

        // Content anchoredPosition.x must move such that:
        // contentX + cardCenter == viewportCenter  =>  contentX = viewportCenter - cardCenter
        float desired = viewportCenter - cardCenter;

        // Clamp so we don't scroll beyond the content edges
        float totalWidth = hlg.padding.left + hlg.padding.right
                           + cardCount * cardWidth
                           + Mathf.Max(0, cardCount - 1) * spacing;

        float minX = Mathf.Min(0f, viewport.rect.width - totalWidth); // most left we can go
        float maxX = 0f;                                              // most right we can go
        return Mathf.Clamp(desired, minX, maxX);
    }

    private void HookButtons()
    {
        if (leftButton)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(Prev);
        }
        if (rightButton)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(Next);
        }
    }

    //public void Next()
    //{
    //    if (!built) return;
    //    if (currentIndex < cardCount - 1)
    //    {
    //        currentIndex++;
    //        UpdateButtons();
    //    }
    //}

    //public void Prev()
    //{
    //    if (!built) return;
    //    if (currentIndex > 0)
    //    {
    //        currentIndex--;
    //        UpdateButtons();
    //    }
    //}

    private void UpdateButtons()
    {
        if (leftButton) leftButton.interactable = currentIndex > 0;
        if (rightButton) rightButton.interactable = currentIndex < cardCount - 1;
    }

    public void Next()
    {
        if (!built) return;
        if (currentIndex < cardCount - 1)
        {
            currentIndex++;
            UpdateButtons();
            OnIndexChanged?.Invoke(currentIndex);   // <-- ADD
        }
    }

    public void Prev()
    {
        if (!built) return;
        if (currentIndex > 0)
        {
            currentIndex--;
            UpdateButtons();
            OnIndexChanged?.Invoke(currentIndex);   // <-- ADD
        }
    }

}


public class HomeCardsPage1 : MonoBehaviour
{
    [Header("Hierarchy")]
    [SerializeField] private RectTransform content;   // the strip that holds the cards
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Cards (prefab + build)")]
    [SerializeField] private GameObject cardPrefab;   // your card prefab (same width for all)
    [SerializeField, Min(1)] private int cardCount = 5;

    [Header("Layout (all cards same size)")]
    [SerializeField, Min(1)] private float cardWidth = 300f; // width of the prefab's RectTransform
    [SerializeField, Min(0)] private float spacing = 60f;    // horizontal gap between cards

    [Header("Motion & Scale")]
    [SerializeField, Range(1f, 30f)] private float snapSpeed = 10f; // how fast we pan to a card
    [SerializeField, Range(1f, 30f)] private float scaleSpeed = 10f;
    [SerializeField] private float selectedScale = 1.2f;  // centered
    [SerializeField] private float otherScale = 1.0f;     // non-centered

    private int currentIndex = 0;         // which card is centered
    private float pageDistance;           // = cardWidth + spacing
    private bool built = false;

    // ----------------------------------------------------------------------
    // Public: call this when the "Home" panel becomes visible (or just let Start run once)
    // ----------------------------------------------------------------------
    public void BuildIfNeeded()
    {
        if (built) return;
        BuildCards();
        HookButtons();
        built = true;
    }

    private void Start()
    {
        BuildIfNeeded();
    }

    private void Update()
    {
        if (!built) return;

        // 1) Pan the content so currentIndex sits at the center
        float targetX = -currentIndex * pageDistance;
        Vector2 targetPos = new Vector2(targetX, content.anchoredPosition.y);
        content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, targetPos, Time.deltaTime * snapSpeed);

        // 2) Scale cards (center bigger, others normal)
        int childCount = content.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform t = content.GetChild(i);
            float goal = (i == currentIndex) ? selectedScale : otherScale;
            Vector3 targetScale = new Vector3(goal, goal, 1f);
            t.localScale = Vector3.Lerp(t.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }
    }

    // ----------------------------------------------------------------------
    // Building & Layout
    // ----------------------------------------------------------------------
    private void BuildCards()
    {
        // Safety
        if (!content)
        {
            Debug.LogError("HomeCardsPager: 'content' is not set.");
            return;
        }
        if (!cardPrefab)
        {
            Debug.LogError("HomeCardsPager: 'cardPrefab' is not set.");
            return;
        }

        // Layout math
        pageDistance = cardWidth + spacing;

        // Clean existing children (optional)
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        // IMPORTANT: for simplest math, set Content pivot to (0, 0.5) and anchor to middle-left.
        // Then anchoredPosition.x = 0 means the first card's left edge at the viewport's left,
        // and we’ll slide Content left (negative) to center cards.

        for (int i = 0; i < cardCount; i++)
        {
            var go = Instantiate(cardPrefab, content);
            var rt = go.transform as RectTransform;

            // Ensure consistent size (optional if your prefab is already correct)
            rt.sizeDelta = new Vector2(cardWidth, rt.sizeDelta.y);

            // Place horizontally in a line (y = 0 keeps them centered vertically if the parent is centered)
            rt.anchoredPosition = new Vector2(i * pageDistance, 0f);

            // Initial uniform scale
            rt.localScale = Vector3.one;

            // Example: set a label if your prefab has a TMP Text somewhere (optional)
            // var label = go.GetComponentInChildren<TMPro.TMP_Text>();
            // if (label) label.text = $"Level {i + 1}";
        }

        // Start centered on index 0
        currentIndex = 0;
        UpdateButtons();
        // Jump instantly to first position
        content.anchoredPosition = new Vector2(0f, content.anchoredPosition.y);
    }

    private void HookButtons()
    {
        if (leftButton)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(Prev);
        }
        if (rightButton)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(Next);
        }
        UpdateButtons();
    }

    // ----------------------------------------------------------------------
    // Navigation
    // ----------------------------------------------------------------------
    public void Next()
    {
        if (!built) return;
        if (currentIndex >= cardCount - 1) return;
        currentIndex++;
        UpdateButtons();
    }

    public void Prev()
    {
        if (!built) return;
        if (currentIndex <= 0) return;
        currentIndex--;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (leftButton) leftButton.interactable = currentIndex > 0;
        if (rightButton) rightButton.interactable = currentIndex < cardCount - 1;
    }
}
