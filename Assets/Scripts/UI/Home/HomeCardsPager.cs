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
