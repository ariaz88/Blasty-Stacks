using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class HomeManager : MonoBehaviour
{
    // Static info about the last selected level/stage
    public static int CurrentLevelId { get; private set; } = 1;
    public static int CurrentStage1Based { get;  set; } = 1;
    public static int PendingSelectStage1Based { get; private set; } = -1;

    private static int _lastShownUnlockedCount = -1; // how many stages we last showed as completed


    [Header("Progress UI")]
    [SerializeField] private Image progressFillImage;     // Image.type = Filled
    [SerializeField] private TMPro.TMP_Text progressText; // optional "x/20" label

    // NEW: Glow follows the end of the *fill area*
    [SerializeField] private RectTransform progressGlow;    // child of the Fill
    [SerializeField] private float glowTrailingOffset = -36f; // negative = shift left from fill end
    [SerializeField, Min(0f)] private float glowLeftPadding = 0f;  // px inside fill
    [SerializeField, Min(0f)] private float glowRightPadding = 0f;  // px inside fill


    [SerializeField, Range(0.1f, 1.5f)]
    private float progressAnimDuration = 0.45f;

    [SerializeField]
    private Ease progressEase = Ease.OutCubic;




    public static int StagesPerLevelStatic { get; private set; }


    [Header("Level Config")]
    [SerializeField, Min(1)] private int levelId = 1;        // current chapter/level
    [SerializeField, Min(1)] private int stagesPerLevel = 20;

    [Header("Pager & Content")]
    [SerializeField] private HomeCardsPager pager;
    [SerializeField] private RectTransform contentRoot;       // same Content used by pager

    [Header("Start Button")]
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject startLockedOverlay;   // optional
    [SerializeField] private TMP_Text startLabel;             // optional

    [Header("Stage Label (center selection)")]
    [SerializeField] private TMP_Text stageTitle;             // e.g., "STAGE 1-18"

    [Header("Scene Loading")]
    [Tooltip("Format: first arg = levelId, second = stage 1-based. Example: Level_2_Stage_11")]
    [SerializeField] private string sceneNamePattern = "Level_{0}_Stage_{1}";

    // inside the class fields (serialize so you can tweak from Inspector)
    [Header("Locked Stage Toast")]
    [SerializeField] private GameObject lockedToastPrefab;     // your existing toast prefab
    [SerializeField] private RectTransform toastCanvasParent;  // optional; if null, uses this.transform
    [SerializeField] private string lockedStageText = "You haven't reached this stage yet.";
    [SerializeField] private int shakeRepeats = 2;
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private Vector2 shakeStrength = new Vector2(16f, 8f);
    [SerializeField] private float toastDuration = 1.2f;

    // ***********************************************************
    // NEW: toast motion/opacity tuning
    [SerializeField, Range(4f, 40f)] private float toastRisePixels = 15f;   // total upward travel
    [SerializeField, Range(0f, 40f)] private float toastFadeStartPixels = 7f;    // where fade-out begins (from start of travel)
    [SerializeField] private float toastStartYOffset = 0f;    // spawn offset above the button
    [SerializeField, Range(0.05f, .3f)] private float toastFadeInDuration = 0.08f; // quick pop-in
    [SerializeField, Range(0.15f, 1f)] private float toastRiseDuration = 0.55f; // time to travel toastRisePixels
    [SerializeField] private Ease toastRiseEase = Ease.OutCubic;
    [SerializeField] private Color toastStartColor = Color.white;                 // before/at pop-in
    [SerializeField] private Color toastEndColor = new Color(0.82f, 0.82f, 0.82f, 1f); // “grayish” at fade-out end
    [SerializeField, Range(0f, 1f)] private float toastGrayAt = 0.75f;   // 75% of travel
    [SerializeField] private Color toastGrayColor = Color.gray;
    [SerializeField, Range(0f, 1f)] private float toastFadeOutAt = 0.90f; // 90% of travel

    
    private List<int> pendingUnlockedUnitIds = new List<int>();




    [Header("Stage reward config (shared with WinPanel)")]
    [SerializeField] private StageRewardConfig stageRewardConfig;

    // Global access so gameplay scene can use the same config
    public static StageRewardConfig SharedRewardConfig { get; private set; }

    private void OnEnable1()
    {
        // Make this config globally available (persists even when Home scene is unloaded)
        SharedRewardConfig = stageRewardConfig;


        if (!pager || !contentRoot)
        {
            Debug.LogError("HomeManager: Assign pager and contentRoot in Inspector.");
            return;
        }

        // Ensure the level data exists (and star array sized)
        SaveSystem.EnsureLevel(levelId, stagesPerLevel);

        // Build pager if needed (creates cards)
        pager.BuildIfNeeded();

        // Initialize card components with titles & indexes
        InitializeStageCards();



        // *******************************************************

        int pending = PendingSelectStage1Based;

        if (pending > 0)
        {
            int idx0 = pending - 1;

            // don't select beyond what is currently unlocked
            int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);
            idx0 = Mathf.Clamp(idx0, 0, Mathf.Max(0, highestUnlocked));

            // jump without animation (cleaner on menu load)
            pager.JumpToIndex(idx0, animate: false);

            // update static current stage so WinPanel/header stays consistent
            CurrentLevelId = levelId;
            CurrentStage1Based = idx0 + 1;

            PendingSelectStage1Based = -1;
        }
        // *******************************************************





        // Hook UI
        pager.OnIndexChanged += HandleSelectionChanged;

        if (startButton)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(LoadSelectedStage);
        }

        // First pass visuals
        RefreshAllVisuals();
        HandleSelectionChanged(pager.CurrentIndex);
    }
    private void OnEnable()
    {
        // Make this config globally available
        SharedRewardConfig = stageRewardConfig;

        if (!pager || !contentRoot)
        {
            Debug.LogError("HomeManager: Assign pager and contentRoot in Inspector.");
            return;
        }

        // Ensure save record for this level exists and is sized
        SaveSystem.EnsureLevel(levelId, stagesPerLevel);

        // Build card strip if needed and initialize card labels/indexes
        pager.BuildIfNeeded();
        InitializeStageCards();

        // ---- Select target index (pending or current), clamped to highest unlocked ----
        int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);
        int targetIdx0;

        if (PendingSelectStage1Based > 0)
        {
            targetIdx0 = Mathf.Clamp(PendingSelectStage1Based - 1, 0, Mathf.Max(0, highestUnlocked));
            PendingSelectStage1Based = -1; // consume
        }
        else
        {
            // If nothing pending, keep current index but clamp to unlocked range
            targetIdx0 = Mathf.Clamp(pager.CurrentIndex, 0, Mathf.Max(0, highestUnlocked));
        }

        // Jump without animation on (re)entering Home
        pager.JumpToIndex(targetIdx0, animate: false);

        // Keep static selectors in sync (used by WinPanel/header, etc.)
        CurrentLevelId = levelId;
        CurrentStage1Based = targetIdx0 + 1;

        // ---- Hook UI (defensively unhook first) ----
        pager.OnIndexChanged -= HandleSelectionChanged;
        pager.OnIndexChanged += HandleSelectionChanged;

        if (startButton)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(LoadSelectedStage);
        }

        // First-pass visuals (cards, header, buttons)
        RefreshAllVisuals();
        HandleSelectionChanged(pager.CurrentIndex);

        // ---- Progress bar: animate from last shown -> current unlocked count ----
        int currentUnlockedCount = Mathf.Clamp(highestUnlocked + 1, 0, stagesPerLevel);
        bool firstTime = _lastShownUnlockedCount < 0;
        int fromCount = firstTime ? currentUnlockedCount : _lastShownUnlockedCount;

        UpdateProgressUI(fromCount, currentUnlockedCount, animate: !firstTime);
        _lastShownUnlockedCount = currentUnlockedCount;
    }


    private void OnDisable()
    {
        if (pager) pager.OnIndexChanged -= HandleSelectionChanged;
    }

    private void Awake()
    {
        StagesPerLevelStatic = stagesPerLevel;
    }
    private void Start()
    {
        CachePendingCharacterUnlocks();
        StartCoroutine(CheckNewCharacterUnlocksDelayed());

    }


    // *********************** New Character Free **************************************

    private IEnumerator CachePendingCharacterUnlocks1()
    {
        yield return new WaitForSeconds(0.01f);

        Debug.Log(" BEFORE WE GP");
        pendingUnlockedUnitIds.Clear();

        var gsm = GameStartManager.Instance;
        if (gsm == null)
        {
            Debug.LogWarning("WinPanel: GameStartManager not found.");
            yield break;
        }

        var unitsDb = gsm.unitsDatabase;
        var unitsModel = gsm.PlayerUnits;

        if (gsm.progressionConfig == null || unitsDb == null || unitsModel == null)
        {
            Debug.LogWarning(
                $"WinPanel: Missing refs | progressionConfig={gsm.progressionConfig != null}, " +
                $"unitsDb={unitsDb != null}, unitsModel={unitsModel != null}"
            );
            yield break;
        }

        Debug.Log($"[UnlockCheck] Units in database: {unitsDb.Units.Count}");

        foreach (var def in unitsDb.Units)
        {
            if (def == null)
            {
                Debug.LogWarning("[UnlockCheck] Null UnitDefinitionSO found in unitsDb.Units");
                continue;
            }

            int id = def.unitId;

            bool isUnlocked = unitsModel.IsUnlocked(id);

            Debug.Log(
                $"[UnlockCheck] UnitId={id}, Name={def.displayName}, " +
                $"Unlocked={isUnlocked}, Required={def.requiredLevelIndex}-{def.requiredStageIndexWithinLevel}"
            );

            // already unlocked → ignore
            if (isUnlocked)
            {
                Debug.Log($"[UnlockCheck] → Skipped (already unlocked)");
                continue;
            }

            bool reached = LevelManager.Instance.HasReached(
                def.requiredLevelIndex,
                def.requiredStageIndexWithinLevel
            );

            Debug.Log($"[UnlockCheck] → HasReached={reached}");

            if (reached)
            {
                pendingUnlockedUnitIds.Add(id);
                Debug.Log($"[UnlockCheck] → ADDED to pending unlocks");
            }
        }

        Debug.Log(
            $"[UnlockCheck] Pending unlock count = {pendingUnlockedUnitIds.Count} " +
            $"[{string.Join(", ", pendingUnlockedUnitIds)}]"
        );
        GameState.Set(pendingUnlockedUnitIds);

    }
    public void CachePendingCharacterUnlocks()
    {
        pendingUnlockedUnitIds.Clear();

        var service = GameStartManager.Instance.ProgressionService;
        if (service == null)
            return;

        var newlyReachable = service.GetReachableButLockedUnits();

        if (newlyReachable.Count > 0)
        {
            pendingUnlockedUnitIds.AddRange(newlyReachable);
            GameState.Set(newlyReachable);
        }
    }



    private IEnumerator CheckNewCharacterUnlocksDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        Debug.Log("Start showing");



        if (GameState.HasAny())
        {
            int unitId = GameState.Pop();
            if (unitId != -1)
            {
                ShowNewCharacterPanel(unitId);

            }
        }


    }


    private void ShowNewCharacterPanel(int unitId)
    {
        var panel = FindObjectOfType<NewCharacterStats>(true);
        if (panel == null)
            return;

        panel.gameObject.SetActive(true);


        panel.Show(unitId);
        Debug.Log("Show the new Character Panel");
    }

    // *********************** New Character Free **************************************




    public static void QueueSelectStage1Based(int stage1Based)
    {
        PendingSelectStage1Based = Mathf.Max(1, stage1Based);
    }



    public static void NotifyStageWon(float hp01)
    {
        int levelId = CurrentLevelId;
        int stage1 = CurrentStage1Based;

        int idx0 = Mathf.Max(0, stage1 - 1);

        int stars = Hp01ToStars(hp01);   // ✅ CORRECT conversion


        SaveSystem.RecordStageResult(levelId, idx0, stars);


        // queue the next stage to be selected on Home
        QueueSelectStage1Based(stage1 + 1);
    }


    private void InitializeStageCards()
    {
        int childCount = contentRoot.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var t = contentRoot.GetChild(i);
            var card = t.GetComponent<StageCard>();
            if (!card) card = t.gameObject.AddComponent<StageCard>();

            card.stageIndex = i;
            // Title "STAGE 1-<n>"
            card.SetTitle($"STAGE {levelId}-{i + 1}");
        }
    }

    private void HandleSelectionChanged(int selectedIndex)
    {
        if (stageTitle) stageTitle.text = $"STAGE {levelId}-{selectedIndex + 1}";

        int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);
        bool unlocked = (selectedIndex <= highestUnlocked);

        if (startButton) startButton.interactable = unlocked;
        if (startLockedOverlay) startLockedOverlay.SetActive(!unlocked);
        if (startLabel) startLabel.text = unlocked ? "START" : "LOCKED";
    }

    private void RefreshAllVisuals()
    {
        int childCount = contentRoot.childCount;
        int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);

        for (int i = 0; i < childCount; i++)
        {
            var card = contentRoot.GetChild(i).GetComponent<StageCard>();
            if (!card) continue;

            bool locked = i > highestUnlocked;
            card.SetLocked(locked);

            int stars = SaveSystem.GetStars(levelId, i); // 0..3, 0 = not played yet
            card.SetStars(stars);
        }
    }

    private void LoadSelectedStage1()
    {
        int idx0 = pager.CurrentIndex;                  // 0-based
        int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);
        if (idx0 > highestUnlocked)
        {
            Debug.Log("Selected stage is locked.");
            return;
        }

        // Save current selection for gameplay scene
        CurrentLevelId = levelId;                     // Added
        CurrentStage1Based = idx0 + 1;                // Added

        string sceneName = string.Format(sceneNamePattern, levelId, idx0 + 1);
        Debug.Log($"Loading scene: {sceneName}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void LoadSelectedStage()
    {
        int idx0 = pager.CurrentIndex;                  // 0-based index in this level
        int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);

        if (idx0 > highestUnlocked)
        {
            Debug.Log("Selected stage is locked.");
            return;
        }

        // Save current selection for gameplay scene
        CurrentLevelId = levelId;
        CurrentStage1Based = idx0 + 1;

        if (LevelManager.Instance != null)
        {
            int globalStage = (levelId - 1) * stagesPerLevel + CurrentStage1Based;
            LevelManager.Instance.SetStage(globalStage, loadScene: false);
        }

        // Load the gameplay scene for this level / stage
        string sceneName = string.Format(sceneNamePattern, levelId, idx0 + 1);
        Debug.Log($"Loading scene: {sceneName}");
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }


    // ========= Optional: call this from gameplay scene when stage ends =========
    // Example use: Home is open in additive, or call after returning to Home.
    public void ReportStageResult_1Based(int stage1Based, float castleHpPercent)
    {
        int idx0 = Mathf.Clamp(stage1Based - 1, 0, stagesPerLevel - 1);

        RefreshAllVisuals();
        HandleSelectionChanged(pager.CurrentIndex);

        // Also bump the progress bar now (animate from previous displayed value)
        {
            int highestUnlocked = SaveSystem.GetHighestUnlocked(levelId);
            int toUnlockedCount = Mathf.Clamp(highestUnlocked + 1, 0, stagesPerLevel);
            int fromUnlockedCount = (_lastShownUnlockedCount < 0) ? toUnlockedCount : _lastShownUnlockedCount;

            UpdateProgressUI(fromUnlockedCount, toUnlockedCount, animate: true);
            _lastShownUnlockedCount = toUnlockedCount;
        }

    }


    public void ShowLockedStageToast1(RectTransform anchorParent)
    {
        if (!lockedToastPrefab) return;

        RectTransform parent = toastCanvasParent ? toastCanvasParent : (RectTransform)transform;
        RectTransform baseAnchor = anchorParent ? anchorParent : parent;

        const int LAYERS = 3;
        float[] alphas = { 1.0f, 0.6f, 0.35f };
        Vector2[] layerOffsets = { Vector2.zero, new Vector2(1.5f, -1.5f), new Vector2(-1.5f, 1.5f) };

        for (int i = 0; i < LAYERS; i++)
        {
            var go = Instantiate(lockedToastPrefab, baseAnchor);
            var rt = go.GetComponent<RectTransform>();
            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = layerOffsets[i];
            rt.localScale = Vector3.one;

            var tmp = go.GetComponentInChildren<TMP_Text>();
            if (tmp) tmp.text = lockedStageText;

            cg.alpha = alphas[i];

            var seq = DOTween.Sequence();
            for (int s = 0; s < shakeRepeats; s++)
            {
                seq.Append(rt.DOShakeAnchorPos(
                    shakeDuration,
                    strength: shakeStrength,
                    vibrato: 10,
                    randomness: 90,
                    snapping: false,
                    fadeOut: true
                ).SetEase(Ease.OutQuad));
            }

            float used = shakeRepeats * shakeDuration;
            float hold = Mathf.Max(0f, toastDuration - used - 0.35f);

            seq.AppendInterval(hold);
            seq.Append(cg.DOFade(0f, 0.35f));
            seq.OnComplete(() => Destroy(go));
        }
    }

    public void ShowLockedStageToast2(RectTransform anchorParent)
    {
        if (!lockedToastPrefab) return;

        RectTransform parent = toastCanvasParent ? toastCanvasParent : (RectTransform)transform;
        RectTransform baseAnchor = anchorParent ? anchorParent : parent;

        var go = Instantiate(lockedToastPrefab, baseAnchor);
        var rt = go.GetComponent<RectTransform>();
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();

        var tmp = go.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp) tmp.text = lockedStageText;

        // Align at the top of the button/anchor; start just above it
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);  // top-center
        rt.pivot = new Vector2(0.5f, 0f);                 // bottom of toast
        rt.anchoredPosition = new Vector2(0f, toastStartYOffset);

        // Initial visual state
        cg.alpha = 0f;
        // Set base color immediately (preserve current alpha on the text)
        if (tmp)
        {
            var c = tmp.color; 
            tmp.color = new Color(toastStartColor.r, toastStartColor.g, toastStartColor.b, c.a);
        }
        else
        {
            // Fallback: tint the first Graphic found (if no TMP_Text in prefab)
            var g = go.GetComponentInChildren<Graphic>(true);
            if (g) { var c = g.color;
                g.color = new Color(toastStartColor.r, toastStartColor.g, toastStartColor.b, c.a); }
        }

        // Compute fade window
        float rise = Mathf.Max(0f, toastRisePixels);
        float fadeStartPx = Mathf.Clamp(toastFadeStartPixels, 0f, rise);
        float delay = (rise <= 0f) ? 0f : toastRiseDuration * (fadeStartPx / rise);
        float fadeTime = Mathf.Max(0.08f, toastRiseDuration - delay);

        var seq = DOTween.Sequence();

        // 1) Quick fade-in
        seq.Append(cg.DOFade(1f, toastFadeInDuration));

        // 2) Rise up
        seq.Join(rt.DOAnchorPosY(rt.anchoredPosition.y + rise, toastRiseDuration)
                 .SetEase(toastRiseEase));

        // 3) Fade-out from the “7px” mark to the end
        seq.Insert(delay, cg.DOFade(0f, fadeTime));

        // 4) Desaturate/gray during the SAME fade window (RGB only; keep alpha)
        if (tmp)
        {
            var start = tmp.color; var target = new Color(toastEndColor.r, toastEndColor.g, toastEndColor.b, start.a);
            seq.Insert(delay/2, tmp.DOColor(target, fadeTime));
        }
        else
        {
            var g = go.GetComponentInChildren<Graphic>(true);
            if (g)
            {
                var start = g.color; var target = new Color(toastEndColor.r, toastEndColor.g, toastEndColor.b, start.a);
                seq.Insert(delay, g.DOColor(target, fadeTime));
            }
        }

        seq.OnComplete(() => Destroy(go));
    }
    public void ShowLockedStageToast(RectTransform anchorParent)
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
        if (fadeTime < 0.06f) fadeTime = 0.06f;   // tiny guard so fade is visible

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


    // Returns a preview reward for a given stage index (0-based),
    // assuming best case (hpCase = 3, i.e., full HP / 3 stars).
    public bool TryGetStageRewardPreview(int stageIndex0Based, out WinPanel.RewardValues reward)
    {
        reward = null;

        if (SharedRewardConfig == null)
            return false;

        int stage1Based = stageIndex0Based + 1;
        int hpCase = 1; // preview: max stars / full HP

        reward = StageRewardCalculator.GetRewardForStageAndHpCase(
            stage1Based,
            hpCase,
            SharedRewardConfig
        );

        return true;
    }

    // Animate the Progression Bar


    private void UpdateProgressUI1(int fromUnlockedCount, int toUnlockedCount, bool animate)
    {
        if (!progressFillImage && !progressText) return;

        // ensure the Image is configured as Filled
        if (progressFillImage)
        {
            progressFillImage.type = Image.Type.Filled;
            if (progressFillImage.fillMethod == Image.FillMethod.Radial360)
            {
            }
        }

        int total = Mathf.Max(1, StagesPerLevelStatic > 0 ? StagesPerLevelStatic : stagesPerLevel);
        int fromClamped = Mathf.Clamp(fromUnlockedCount, 0, total);
        int toClamped = Mathf.Clamp(toUnlockedCount, 0, total);

        float from01 = (float)fromClamped / total;
        float to01 = (float)toClamped / total;

        void SetLabel(int unlocked)
        {
        }

        if (!progressFillImage)
        {
            // only label present
            SetLabel(toClamped);
            return;
        }

        // no animation path
        if (!animate)
        {
            progressFillImage.fillAmount = to01;
            SetLabel(toClamped);
            return;
        }

        // animated path with DOTween
        DOTween.Kill(progressFillImage); // avoid stacking tweens on the same target
        progressFillImage.fillAmount = from01;
        SetLabel(fromClamped);

        progressFillImage
            .DOFillAmount(to01, progressAnimDuration)
            .SetEase(progressEase)
            .OnUpdate(() =>
            {
            // interpolate numeric label alongside the fill
            float t = Mathf.InverseLerp(from01, to01, progressFillImage.fillAmount);
                int live = Mathf.RoundToInt(Mathf.Lerp(fromClamped, toClamped, t));
                SetLabel(live);
            })
            .OnComplete(() => SetLabel(toClamped));
    }
    private void UpdateProgressUI(int fromUnlockedCount, int toUnlockedCount, bool animate)
    {
        if (!progressFillImage && !progressText) return;

        // ensure Filled setup (only once matters)
        if (progressFillImage) progressFillImage.type = Image.Type.Filled;

        int total = Mathf.Max(1, StagesPerLevelStatic > 0 ? StagesPerLevelStatic : stagesPerLevel);
        int fromClamped = Mathf.Clamp(fromUnlockedCount, 0, total);
        int toClamped = Mathf.Clamp(toUnlockedCount, 0, total);

        float from01 = (float)fromClamped / total;
        float to01 = (float)toClamped / total;

        void SetLabel(int unlocked)
        {
            if (progressText) progressText.text = $"{unlocked}/{total}";
        }

        // No animation
        if (!animate || !progressFillImage)
        {
            if (progressFillImage) progressFillImage.fillAmount = to01;
            SetLabel(toClamped);
            PositionProgressGlow(to01);                  // <<< add
            return;
        }

        // Animated path
        DOTween.Kill(progressFillImage);
        progressFillImage.fillAmount = from01;
        SetLabel(fromClamped);
        PositionProgressGlow(from01);                    // <<< add

        progressFillImage
            .DOFillAmount(to01, progressAnimDuration)
            .SetEase(progressEase)
            .OnUpdate(() =>
            {
                float live01 = progressFillImage.fillAmount;
                float t = Mathf.InverseLerp(from01, to01, live01);
                int live = Mathf.RoundToInt(Mathf.Lerp(fromClamped, toClamped, t));
                SetLabel(live);
                PositionProgressGlow(live01);           // <<< add
        })
            .OnComplete(() =>
            {
                SetLabel(toClamped);
                PositionProgressGlow(to01);             // <<< add
        });
    }


    private void PositionProgressGlow1(float fill01)
    {
        if (!progressGlow || !progressFillImage) return;

        // clamp and flip if fill originates from the right
        fill01 = Mathf.Clamp01(fill01);
        if (progressFillImage.type == Image.Type.Filled &&
            progressFillImage.fillMethod == Image.FillMethod.Horizontal &&
            progressFillImage.fillOrigin == (int)Image.OriginHorizontal.Right)
        {
            fill01 = 1f - fill01;
        }

        // Work in the Fill's local space so padding/9-slice are naturally respected
        RectTransform fillRT = progressFillImage.rectTransform;

        float w = fillRT.rect.width;
        float leftX = -fillRT.pivot.x * w + glowLeftPadding;
        float rightX = (1f - fillRT.pivot.x) * w - glowRightPadding;

        // Lerp to the end of the *visible fill*
        float x = Mathf.Lerp(leftX, rightX, fill01);

        // Apply in Fill local space; glow MUST be a child of Fill
        Vector2 p = progressGlow.anchoredPosition;
        p.x = x;
        progressGlow.anchoredPosition = p;

        // Optional: hide when empty
        progressGlow.gameObject.SetActive(fill01 > 0.001f);
    }
    private void PositionProgressGlow(float fill01)
    {
        if (!progressGlow || !progressFillImage) return;

        fill01 = Mathf.Clamp01(fill01);

        // If fill grows from right, normalize to left→right logic
        if (progressFillImage.type == Image.Type.Filled &&
            progressFillImage.fillMethod == Image.FillMethod.Horizontal &&
            progressFillImage.fillOrigin == (int)Image.OriginHorizontal.Right)
        {
            fill01 = 1f - fill01;
        }

        RectTransform fillRT = progressFillImage.rectTransform;

        // Compute left/right bounds inside the Fill rect (respect optional paddings)
        float w = fillRT.rect.width;
        float leftBound = -fillRT.pivot.x * w + glowLeftPadding;
        float rightBound = (1f - fillRT.pivot.x) * w - glowRightPadding;

        // End of the *visible* fill (no offset yet)
        float endX = Mathf.Lerp(leftBound, rightBound, fill01);

        // Apply manual offset (e.g., -36 to sit a bit inside the fill), then clamp
        float x = Mathf.Clamp(endX + glowTrailingOffset, leftBound, endX);

        var p = progressGlow.anchoredPosition;
        p.x = x;
        progressGlow.anchoredPosition = p;

        // Hide when effectively empty
        progressGlow.gameObject.SetActive(fill01 > 0.001f);
    }


    // Helper
    private static int Hp01ToStars(float hp01)
    {
        if (hp01 >= 0.999f) return 3;   // full HP
        if (hp01 >= 0.5f) return 2;   // >= 50%
        if (hp01 > 0f) return 1;   // cleared but low HP
        return 0;
    }




}
