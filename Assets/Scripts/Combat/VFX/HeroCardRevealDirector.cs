using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the "you earned these heroes" CARD animation that replaced hero units
/// spawning onto the gates.
///
/// WHY THIS EXISTS
/// ---------------
/// Before PHASE 2, every puzzle match put physical heroes on the four gate
/// platforms and made them leap into the battlefield. Three or four matches filled
/// the field, which made the scene unreadable and the stage far too easy. The
/// heroes are still EARNED at exactly the same rate (LevelBattleRules.Deployments -
/// stage 4 is still 1,1,2,4); they are now held in data by PlayerWaveManager and
/// announced by this card deal instead of being instantiated.
///
/// This is presentation only. It owns no roster state and never decides how many
/// heroes anything gets - it renders whatever PlayerWaveManager.HeroesEarned hands
/// it. Deployment of those heroes into the battle is a separate, later step.
///
/// THE BEATS  (authored in seconds; Ref1 frame numbers in brackets)
/// ---------------------------------------------------------------
///   0.00-0.18  cards fly in from above and gather, FACE DOWN, at the left  [fr.1-3]
///   0.18-0.34  each card flips to its real hero portrait (staggered)
///   0.34-0.62  they fan out along a shallow arc to centre, face up         [fr.3-9]
///   0.62-0.86  the fan holds - this is the beat the player actually reads
///   0.86-1.00  cards collapse back into one stack at centre                [fr.9-11]
///   1.00-1.06  white flash                                                 [fr.12]
///   1.06-1.20  resolves to ONE teal-glowing card, back to "?"              [fr.13]
///   1.20-1.55  the glow holds                                              [fr.15-22]
///   1.55-1.80  fade out
///
/// Ref1 itself is 1.11s and ends holding the glow; the flip and the read-hold are
/// additions this game needs, since unlike the reference these cards have to tell
/// the player WHICH heroes were earned.
///
/// SELF-BOOTSTRAPPING: like ShardBurst, there is nothing to wire in a scene. It
/// installs itself after every scene load and subscribes to the static event.
/// </summary>
[DisallowMultipleComponent]
public class HeroCardRevealDirector : MonoBehaviour
{
    // ---------------------------------------------------------------- timeline
    private const float TFlyIn = 0.18f;
    private const float TFlipStart = 0.18f;
    private const float TFlipDur = 0.16f;
    private const float TFlipStagger = 0.035f;
    private const float TFanStart = 0.34f;
    private const float TFanDur = 0.28f;
    private const float THoldEnd = 0.86f;
    private const float TCollapseDur = 0.14f;
    private const float TFlashDur = 0.06f;
    private const float TGlowIn = 0.14f;
    private const float TGlowHold = 0.35f;
    private const float TFadeOut = 0.25f;

    // ---------------------------------------------------------------- layout
    /// <summary>Card width as a fraction of screen width - measured off Ref1 (~20%).</summary>
    private const float CardWidthFraction = 0.20f;

    /// <summary>Playing-card ratio, w/h. Matches the baked sprites (256x358).</summary>
    private const float CardAspect = 256f / 358f;

    /// <summary>
    /// How far above the player base the deal sits, in card heights. The brief was
    /// "two of these cards stacked" of clearance - the animation must never land on
    /// the base itself.
    /// </summary>
    private const float ClearanceInCardHeights = 2.0f;

    /// <summary>Horizontal step between fanned cards, in card widths. Under 1 so they overlap.</summary>
    private const float FanStepInCardWidths = 0.62f;

    /// <summary>Tilt of the outermost cards in the fan, degrees. Middle cards sit near 0.</summary>
    private const float FanTiltDegrees = 13f;

    /// <summary>How high the centre of the fan arcs above its ends, in card heights.</summary>
    private const float FanArcInCardHeights = 0.10f;

    private static readonly Color TealGlow = new Color32(0x38, 0xE1, 0xF0, 0xFF);
    private static readonly Color TealCard = new Color32(0x7C, 0xEC, 0xF7, 0xFF);

    private static HeroCardRevealDirector instance;

    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasGroup group;
    private RectTransform stage;

    private readonly List<CardView> pool = new();
    private Image flash;
    private Coroutine playing;

    /// <summary>Cached clone source for the avatar frame - see ResolveAvatarFrame.</summary>
    private static GameObject avatarFrameTemplate;
    private static bool avatarFrameSearched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance) return;

        var go = new GameObject("[HeroCardRevealDirector]");

        // DontDestroyOnLoad throws outright in edit mode, and EditorPreviewFan
        // bootstraps from there - the preview object is temporary anyway.
        if (Application.isPlaying) DontDestroyOnLoad(go);
        else go.hideFlags = HideFlags.DontSave;

        instance = go.AddComponent<HeroCardRevealDirector>();
    }

    private void OnEnable()
    {
        PlayerWaveManager.HeroesEarned += HandleHeroesEarned;
    }

    private void OnDisable()
    {
        // The event is static and PlayerWaveManager never clears it, so an
        // unsubscribe here is mandatory - otherwise a destroyed director keeps
        // receiving deals across scene loads.
        PlayerWaveManager.HeroesEarned -= HandleHeroesEarned;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        // A scene change invalidates whatever card template was found in the old
        // scene; the next deal re-resolves against the new one.
        avatarFrameTemplate = null;
        avatarFrameSearched = false;
    }

    private void HandleHeroesEarned(IReadOnlyList<UnitDefinitionSO> heroes, Vector3 anchorWorld)
    {
        if (heroes == null || heroes.Count == 0) return;

        // A second match landing mid-deal restarts the presentation rather than
        // overlapping two fans. Queued matches already arrive one at a time from
        // PlayerWaveManager, so this is a safety net, not the normal path.
        if (playing != null) StopCoroutine(playing);

        playing = StartCoroutine(PlayDeal(heroes, anchorWorld));
    }

    // ======================================================================
    //  The deal
    // ======================================================================

    private IEnumerator PlayDeal(IReadOnlyList<UnitDefinitionSO> heroes, Vector3 anchorWorld)
    {
        EnsureCanvas();

        int n = heroes.Count;
        MeasureLayout(anchorWorld, out float cardW, out float cardH, out Vector2 anchor);

        var cards = new List<CardView>(n);
        for (int i = 0; i < n; i++)
        {
            var card = Rent(i);
            card.Configure(heroes[i], new Vector2(cardW, cardH));
            cards.Add(card);
        }

        // Cards later in the list must draw ON TOP while stacked, so the fan reads
        // left-to-right rather than as an arbitrary jumble.
        for (int i = 0; i < n; i++) cards[i].Root.SetSiblingIndex(i);

        group.alpha = 1f;
        stage.gameObject.SetActive(true);
        SetFlash(0f, anchor, new Vector2(cardW, cardH));

        Vector2 gather = anchor + new Vector2(-cardW * 0.95f, 0f);

        // --- fly in, face down ------------------------------------------------
        Vector2 offscreen = gather + new Vector2(cardW * 0.55f, canvasRect.rect.height * 0.75f);
        for (float t = 0f; t < TFlyIn; t += Time.unscaledDeltaTime)
        {
            float k = EaseOutCubic(t / TFlyIn);
            for (int i = 0; i < n; i++)
            {
                // A small per-card offset keeps the incoming stack from looking
                // like one thick card.
                Vector2 to = gather + new Vector2(i * cardW * 0.035f, -i * cardH * 0.012f);
                cards[i].SetPose(Vector2.Lerp(offscreen, to, k),
                                 Mathf.Lerp(-26f, -8f + i * 1.5f, k),
                                 Mathf.Lerp(0.82f, 1f, k));
                cards[i].ShowFace(false);
            }
            yield return null;
        }

        // --- flip to the real portraits --------------------------------------
        float flipEnd = TFlipDur + TFlipStagger * (n - 1);
        for (float t = 0f; t < flipEnd; t += Time.unscaledDeltaTime)
        {
            for (int i = 0; i < n; i++)
            {
                float local = Mathf.Clamp01((t - i * TFlipStagger) / TFlipDur);
                // scaleX 1 -> 0 -> 1, face swapped at the midpoint. The card is
                // edge-on at exactly 0.5, which is what hides the swap.
                cards[i].SetFlip(local);
            }
            yield return null;
        }
        for (int i = 0; i < n; i++) cards[i].SetFlip(1f);

        // --- fan out to centre -------------------------------------------------
        ComputeFan(n, anchor, cardW, cardH, out var fanPos, out var fanRot);

        var fromPos = new Vector2[n];
        var fromRot = new float[n];
        for (int i = 0; i < n; i++) { fromPos[i] = cards[i].Position; fromRot[i] = cards[i].Rotation; }

        for (float t = 0f; t < TFanDur; t += Time.unscaledDeltaTime)
        {
            float k = EaseOutBack(t / TFanDur);
            for (int i = 0; i < n; i++)
                cards[i].SetPose(Vector2.Lerp(fromPos[i], fanPos[i], k),
                                 Mathf.Lerp(fromRot[i], fanRot[i], k),
                                 1f);
            yield return null;
        }
        for (int i = 0; i < n; i++) cards[i].SetPose(fanPos[i], fanRot[i], 1f);

        // --- hold the fan ------------------------------------------------------
        yield return new WaitForSecondsRealtime(THoldEnd - (TFanStart + TFanDur));

        // --- collapse back into one stack -------------------------------------
        for (float t = 0f; t < TCollapseDur; t += Time.unscaledDeltaTime)
        {
            float k = EaseInCubic(t / TCollapseDur);
            for (int i = 0; i < n; i++)
                cards[i].SetPose(Vector2.Lerp(fanPos[i], anchor, k),
                                 Mathf.Lerp(fanRot[i], 0f, k),
                                 Mathf.Lerp(1f, 0.96f, k));
            yield return null;
        }

        // --- white flash -------------------------------------------------------
        for (float t = 0f; t < TFlashDur; t += Time.unscaledDeltaTime)
        {
            SetFlash(Mathf.Clamp01(t / (TFlashDur * 0.45f)), anchor, new Vector2(cardW, cardH));
            yield return null;
        }

        // Under the flash: everything collapses to ONE card, face DOWN again -
        // the "?" is the point of this beat, per the brief.
        for (int i = 1; i < n; i++) cards[i].Hide();
        var hero = cards[0];
        hero.ShowFace(false);
        hero.SetPose(anchor, 0f, 1f);

        // --- resolve into the teal glow ---------------------------------------
        for (float t = 0f; t < TGlowIn; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / TGlowIn);
            SetFlash(1f - k, anchor, new Vector2(cardW, cardH));
            hero.SetGlow(k, TealGlow, TealCard);
            hero.SetPose(anchor, 0f, Mathf.Lerp(1.12f, 1f, EaseOutCubic(k)));
            yield return null;
        }
        SetFlash(0f, anchor, new Vector2(cardW, cardH));
        hero.SetGlow(1f, TealGlow, TealCard);

        yield return new WaitForSecondsRealtime(TGlowHold);

        // --- fade out ----------------------------------------------------------
        for (float t = 0f; t < TFadeOut; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - Mathf.Clamp01(t / TFadeOut);
            yield return null;
        }

        group.alpha = 0f;
        foreach (var c in cards) { c.SetGlow(0f, TealGlow, Color.white); c.Hide(); }
        stage.gameObject.SetActive(false);
        playing = null;
    }

    /// <summary>
    /// Where each card sits at the fan's rest pose, and how far it is tilted.
    ///
    /// u runs -1 (leftmost) .. +1 (rightmost), so a single card sits dead centre
    /// with no tilt. The arc term (1 - u*u) lifts the MIDDLE of the fan, which is
    /// what makes it read as a held hand rather than a straight row.
    /// </summary>
    private static void ComputeFan(int n, Vector2 anchor, float cardW, float cardH,
                                   out Vector2[] pos, out float[] rot)
    {
        pos = new Vector2[n];
        rot = new float[n];

        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0f : (i / (float)(n - 1)) * 2f - 1f;
            pos[i] = anchor + new Vector2(u * (n - 1) * 0.5f * cardW * FanStepInCardWidths,
                                          (1f - u * u) * cardH * FanArcInCardHeights);
            rot[i] = -u * FanTiltDegrees;
        }
    }

#if UNITY_EDITOR
    /// <summary>EDITOR ONLY. The live preview canvas, for screenshot tooling.</summary>
    public static Canvas EditorPreviewCanvas => instance ? instance.canvas : null;

    /// <summary>
    /// EDITOR ONLY. Destroys every edit-mode preview object.
    ///
    /// !! Sweeps by TYPE, never through the static `instance`. A domain reload -
    /// which every script compile triggers - resets that static to null while the
    /// GameObject lives on, orphaning a preview that nothing can then clean up.
    /// HideFlags.DontSave keeps such an orphan out of the scene file, so it never
    /// shows in a diff or a build, but it DOES keep rendering in the Game view in
    /// every scene until the editor is restarted. That happened on 2026-09-14.
    ///
    /// Registered below to run on every assembly reload and on every play-mode
    /// transition, so an orphan cannot outlive the compile that created it.
    /// </summary>
    public static void EditorPreviewCleanup()
    {
        foreach (var d in Resources.FindObjectsOfTypeAll<HeroCardRevealDirector>())
        {
            if (!d) continue;

            var go = d.gameObject;

            // Only ever touch preview objects. A real runtime director created by
            // Bootstrap during Play mode does not carry DontSave.
            if (go.hideFlags != HideFlags.DontSave) continue;
            if (Application.isPlaying) continue;

            DestroyImmediate(go);
        }

        instance = null;
        avatarFrameTemplate = null;
        avatarFrameSearched = false;
    }

    [UnityEditor.InitializeOnLoadMethod]
    private static void EditorInstallPreviewGuard()
    {
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= EditorPreviewCleanup;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += EditorPreviewCleanup;

        UnityEditor.EditorApplication.playModeStateChanged -= EditorOnPlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += EditorOnPlayModeChanged;

        // Also sweep right now, which clears anything a previous reload orphaned.
        EditorPreviewCleanup();
    }

    private static void EditorOnPlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
            state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            EditorPreviewCleanup();
    }

    /// <summary>
    /// EDITOR ONLY. Builds the canvas and poses <paramref name="count"/> cards at
    /// the fan's rest pose so the layout can be screenshotted and tuned without
    /// entering Play mode (the coroutine needs a real deltaTime, which edit mode
    /// does not provide).
    ///
    /// Returns the measured card size and fan extents so the numbers can be checked
    /// against the authored targets rather than eyeballed.
    /// </summary>
    public static string EditorPreviewFan(int count, bool faceUp, Vector3 anchorWorld)
    {
        Bootstrap();
        var d = instance;
        d.EnsureCanvas();

        d.MeasureLayout(anchorWorld, out float cardW, out float cardH, out Vector2 anchor);
        ComputeFan(count, anchor, cardW, cardH, out var pos, out var rot);

        d.group.alpha = 1f;
        d.stage.gameObject.SetActive(true);

        var db = FindObjectOfType<GameStartManager>();
        var units = db ? db.unitsDatabase : null;

        float minX = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            var card = d.Rent(i);
            UnitDefinitionSO def = null;
            if (units != null && units.Units != null && units.Units.Count > 0)
                def = units.Units[i % units.Units.Count];

            card.Configure(def, new Vector2(cardW, cardH));
            card.SetPose(pos[i], rot[i], 1f);
            card.ShowFace(faceUp);
            card.Root.SetSiblingIndex(i);

            minX = Mathf.Min(minX, pos[i].x - cardW * 0.5f);
            maxX = Mathf.Max(maxX, pos[i].x + cardW * 0.5f);
            maxY = Mathf.Max(maxY, pos[i].y + cardH * 0.5f);
        }
        for (int i = count; i < d.pool.Count; i++) d.pool[i].Hide();

        Canvas.ForceUpdateCanvases();

        return $"canvas={d.canvasRect.rect.width:0}x{d.canvasRect.rect.height:0} " +
               $"card={cardW:0.0}x{cardH:0.0} anchor=({anchor.x:0.0},{anchor.y:0.0}) " +
               $"fanWidth={(maxX - minX):0.0} fanTop={maxY:0.0}";
    }
#endif

    // ======================================================================
    //  Placement
    // ======================================================================

    /// <summary>
    /// Screen-anchor for the deal: above the player base by
    /// <see cref="ClearanceInCardHeights"/>, clamped so a tall fan can never run
    /// off the top of the screen on a short phone.
    /// </summary>
    private Vector2 ResolveAnchor(Vector3 anchorWorld, float cardH)
    {
        var cam = Camera.main;
        Vector2 screen = cam
            ? (Vector2)cam.WorldToScreenPoint(anchorWorld)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.35f);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screen, null, out Vector2 local);

        local.y += cardH * ClearanceInCardHeights;

        float halfH = canvasRect.rect.height * 0.5f;
        float margin = cardH * 0.75f;
        local.y = Mathf.Clamp(local.y, -halfH + margin, halfH - margin);
        local.x = Mathf.Clamp(local.x,
                              -canvasRect.rect.width * 0.5f + margin,
                              canvasRect.rect.width * 0.5f - margin);
        return local;
    }

    // ======================================================================
    //  Canvas + pooling
    // ======================================================================

    private void EnsureCanvas()
    {
        if (canvas) return;

        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the battle HUD. The deal is a full-attention moment.
        canvas.sortingOrder = 900;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;   // never eat a board touch
        group.interactable = false;

        canvasRect = go.GetComponent<RectTransform>();

        var stageGo = new GameObject("Stage", typeof(RectTransform));
        stage = stageGo.GetComponent<RectTransform>();
        stage.SetParent(canvasRect, false);
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = Vector2.zero;

        var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
        flash = flashGo.GetComponent<Image>();
        flash.sprite = Load(CardSprites.Glow);
        flash.raycastTarget = false;
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.rectTransform.SetParent(stage, false);
        flash.rectTransform.anchorMin = flash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

        // MANDATORY. A canvas created this frame has not had a layout pass, so
        // canvasRect.rect is stale - it reported 1125 wide on the first deal and
        // 979 afterwards, which sized the cards differently every time and put the
        // second deal completely off screen. Every measurement below depends on
        // this rect being real.
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Card size and screen anchor for one deal, measured AFTER a layout pass.
    ///
    /// Re-measured per deal rather than cached: the canvas rect changes with
    /// device rotation and with any resolution change, and a cached size would
    /// silently deal off-screen cards afterwards.
    /// </summary>
    private void MeasureLayout(Vector3 anchorWorld,
                               out float cardW, out float cardH, out Vector2 anchor)
    {
        Canvas.ForceUpdateCanvases();

        cardW = canvasRect.rect.width * CardWidthFraction;
        cardH = cardW / CardAspect;
        anchor = ResolveAnchor(anchorWorld, cardH);
    }

    private void SetFlash(float a, Vector2 anchor, Vector2 cardSize)
    {
        if (!flash) return;

        flash.rectTransform.anchoredPosition = anchor;
        flash.rectTransform.sizeDelta = cardSize * 1.9f;
        flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        flash.transform.SetAsLastSibling();
    }

    private CardView Rent(int index)
    {
        while (pool.Count <= index) pool.Add(CardView.Build(stage));

        var card = pool[index];
        card.Show();
        return card;
    }

    // ======================================================================
    //  One card
    // ======================================================================

    /// <summary>
    /// Destroy that also works from the editor preview. Object.Destroy is deferred
    /// to end-of-frame, which edit mode never reaches - it logs an error and the
    /// object survives.
    /// </summary>
    private static void SafeDestroy(Object o)
    {
        if (!o) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    private static class CardSprites
    {
        public const string Back = "Assets/Arts/VFX/HeroCardBack.png";
        public const string Face = "Assets/Arts/VFX/HeroCardFace.png";
        public const string Glow = "Assets/Arts/VFX/HeroCardGlow.png";
    }

    private static readonly Dictionary<string, Sprite> spriteCache = new();

    /// <summary>
    /// Loads a baked card sprite. Resources.Load is not usable here (the sprites
    /// deliberately live under Arts/, not Resources/), so the sprites are resolved
    /// through a lookup that works in the Editor and falls back to a runtime-built
    /// 1x1 in a player build if the asset was never packed - which would show as a
    /// plain rectangle rather than a NullReferenceException.
    /// </summary>
    private static Sprite Load(string path)
    {
        if (spriteCache.TryGetValue(path, out var cached) && cached) return cached;

        Sprite sp = null;
#if UNITY_EDITOR
        sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#endif
        if (!sp)
        {
            // Runtime fallback: a plain white sprite, so the animation still plays.
            var tex = Texture2D.whiteTexture;
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        spriteCache[path] = sp;
        return sp;
    }

    /// <summary>
    /// Finds the authored "Cell Active" avatar frame (mask + avatar) to clone onto
    /// the card face, so the card carries the same portrait treatment as the
    /// Heroes Stats panel rather than a bare sprite.
    ///
    /// Resolved from any HeroStatCell in the scene INCLUDING INACTIVE ONES: the
    /// panel's template is switched off by HeroStatsPanel on Start, which is
    /// exactly the object wanted here.
    /// </summary>
    private static GameObject ResolveAvatarFrame()
    {
        if (avatarFrameSearched) return avatarFrameTemplate;
        avatarFrameSearched = true;

        var cell = FindObjectOfType<HeroStatCell>(true);
        if (!cell) return null;

        foreach (var t in cell.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.Replace(" ", string.Empty).ToLowerInvariant();
            if (n == "cellactive") { avatarFrameTemplate = t.gameObject; break; }
        }

        return avatarFrameTemplate;
    }

    private class CardView
    {
        public RectTransform Root;
        private RectTransform face;
        private RectTransform back;
        private Image glow;
        private Image faceImg;
        private Image backImg;
        private Image avatar;

        public Vector2 Position => Root.anchoredPosition;
        public float Rotation => Root.localEulerAngles.z > 180f
            ? Root.localEulerAngles.z - 360f
            : Root.localEulerAngles.z;

        public static CardView Build(RectTransform parent)
        {
            var v = new CardView();

            var root = new GameObject("Card", typeof(RectTransform));
            v.Root = root.GetComponent<RectTransform>();
            v.Root.SetParent(parent, false);
            v.Root.anchorMin = v.Root.anchorMax = new Vector2(0.5f, 0.5f);

            v.glow = NewImage("Glow", v.Root, Load(CardSprites.Glow));
            v.glow.color = new Color(1f, 1f, 1f, 0f);

            v.faceImg = NewImage("Face", v.Root, Load(CardSprites.Face));
            v.face = v.faceImg.rectTransform;

            // The authored avatar frame, cloned onto the face. Null-safe: without
            // the Heroes Stats panel in the scene the card still deals, it just
            // shows a blank face.
            var frame = ResolveAvatarFrame();
            if (frame)
            {
                var clone = Instantiate(frame, v.face);
                clone.name = "Avatar Frame";
                clone.SetActive(true);

                // Strip anything interactive the template carried in - the brief
                // is explicit that these cards have no buttons and no text.
                foreach (var b in clone.GetComponentsInChildren<Button>(true)) SafeDestroy(b);
                foreach (var g in clone.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                foreach (var txt in clone.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    txt.gameObject.SetActive(false);

                var rt = clone.GetComponent<RectTransform>();
                if (rt)
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                }

                foreach (var img in clone.GetComponentsInChildren<Image>(true))
                {
                    string n = img.name.Replace(" ", string.Empty).ToLowerInvariant();
                    if (n == "avatar") { v.avatar = img; break; }
                }
            }

            v.backImg = NewImage("Back", v.Root, Load(CardSprites.Back));
            v.back = v.backImg.rectTransform;

            return v;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = false;

            var rt = img.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            return img;
        }

        public void Configure(UnitDefinitionSO def, Vector2 size)
        {
            Root.sizeDelta = size;
            face.sizeDelta = size;
            back.sizeDelta = size;

            // The glow sprite is baked with padding around the card footprint, so
            // it has to be scaled by the SAME ratio the baker used or the halo
            // would sit at the wrong distance from the edge.
            glow.rectTransform.sizeDelta = new Vector2(size.x * (400f / 256f), size.y * (502f / 358f));

            if (avatar)
            {
                avatar.sprite = def ? def.portrait : null;
                avatar.enabled = def && def.portrait;

                var rt = avatar.rectTransform;
                if (rt && rt.parent is RectTransform holder)
                    holder.sizeDelta = size * 0.74f;
            }

            glow.color = new Color(1f, 1f, 1f, 0f);
            faceImg.color = Color.white;
            backImg.color = Color.white;
        }

        public void SetPose(Vector2 pos, float rotZ, float scale)
        {
            Root.anchoredPosition = pos;
            Root.localEulerAngles = new Vector3(0f, 0f, rotZ);
            Root.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Card flip driven by a single 0..1 value: the card squashes to edge-on at
        /// 0.5 and the visible side is swapped exactly there.
        /// </summary>
        public void SetFlip(float k)
        {
            // cos(k*pi) runs 1 -> 0 -> -1; the absolute value is the foreshortening
            // of a card turning about its vertical axis. Floored just above zero so
            // the card never collapses to a zero-scale transform.
            float sx = Mathf.Max(Mathf.Abs(Mathf.Cos(k * Mathf.PI)), 0.02f);

            var s = Root.localScale;
            Root.localScale = new Vector3(sx * Mathf.Abs(s.y), s.y, 1f);
            ShowFace(k >= 0.5f);
        }

        public void ShowFace(bool faceUp)
        {
            if (face) face.gameObject.SetActive(faceUp);
            if (back) back.gameObject.SetActive(!faceUp);
        }

        public void SetGlow(float k, Color glowColour, Color cardTint)
        {
            if (glow)
            {
                var c = glowColour;
                c.a = k;
                glow.color = c;
            }

            if (backImg) backImg.color = Color.Lerp(Color.white, cardTint, k);
        }

        public void Show() => Root.gameObject.SetActive(true);
        public void Hide() => Root.gameObject.SetActive(false);
    }

    // ======================================================================
    //  Easing
    // ======================================================================

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float f = 1f - t;
        return 1f - f * f * f;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    /// <summary>Slight overshoot - it is what gives the fan its "snap" in Ref1.</summary>
    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.30f;
        const float c3 = c1 + 1f;
        float f = t - 1f;
        return 1f + c3 * f * f * f + c1 * f * f;
    }
}
