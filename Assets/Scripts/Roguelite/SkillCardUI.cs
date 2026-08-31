using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// One card on the level-up screen, modelled on the reference clip.
///
/// STATES, driven purely by how many stars this hero already holds of this buff:
///
///   0 stars   "UNLOCK"  - the hero has never taken this buff. No value, no stars.
///   1-3       value + that many GOLD stars, centred.
///   4-6       value + (stars-3) PURPLE stars - the stronger tier.
///
/// The star row shows what the hero ALREADY has; the green value is what taking
/// the card grants now.
///
/// On selection the icon does a quick pop (big -> small -> normal) and small stars
/// burst out of the star row, matching the reference's feedback.
/// </summary>
public class SkillCardUI : MonoBehaviour
{
    [Header("Refs (all optional - every use is null-guarded)")]
    public GameObject firstUI;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Transform starsContainer;
    [SerializeField] private Image[] starImages;
    [SerializeField] private GameObject newRibbon;

    [Header("Star tiers")]
    [SerializeField] private Color goldStar = new Color(1f, 0.80f, 0.18f);
    [SerializeField] private Color purpleStar = new Color(0.69f, 0.35f, 0.95f);
    [Tooltip("Stars per tier before the colour changes. 3 = gold 1-3, purple 4-6.")]
    [SerializeField, Min(1)] private int starsPerTier = 3;

    [Header("Text style")]
    [SerializeField] private Color valueColor = new Color(0.16f, 0.70f, 0.29f);
    [SerializeField] private Color unlockColor = new Color(0.20f, 0.22f, 0.26f);

    [Header("Animated hero preview")]
    [Tooltip("RawImage that displays the live hero render. Without it the card falls " +
             "back to the static portrait sprite.")]
    [SerializeField] private RawImage previewImage;
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField, Min(64)] private int previewResolution = 256;
    [Tooltip("How much empty space around the character. 1 = tight crop.")]
    [SerializeField, Min(1f)] private float previewPadding = 1.15f;
    [Tooltip("Far from any level geometry, so the preview camera films only the hero.")]
    [SerializeField] private Vector2 previewStageOrigin = new Vector2(10000f, 10000f);

    [Tooltip("Gap between two cards' hero stages. Must comfortably exceed the size of a " +
             "rig: these are authored at PIXEL scale, so a character can be hundreds of " +
             "units across, not one or two.")]
    [SerializeField] private float previewStageStride = 5000f;

    [Header("Selection beat")]
    [Tooltip("How far the card pops out on click, before dipping back.")]
    [SerializeField] private float selectPopScale = 1.12f;
    [SerializeField] private float selectDipScale = 0.92f;

    private BuffOffer offer;
    private Action<BuffOffer> onSelected;
    private GameObject spawnedVisual;
    private Tween idlePulse;
    private Sequence clickSequence;

    private GameObject previewStage;
    private Camera previewCamera;
    private RenderTexture previewTexture;

    // Shared across cards so three simultaneous previews never overlap.
    private static int stageCounter;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        idlePulse?.Kill();
        idlePulse = null;
    }

    private void OnDestroy()
    {
        idlePulse?.Kill();
        idlePulse = null;
        clickSequence?.Kill();
        clickSequence = null;

        // The off-screen hero stage, its camera and its RenderTexture are created at
        // runtime and are NOT children of this card, so they leak unless released here.
        ClearPreview();
    }

    /// <summary>
    /// Fills the card in. <paramref name="unit"/> supplies both the portrait and the
    /// animated visual; it is null on a global (army-wide) card.
    /// </summary>
    public void Init(BuffOffer data, UnitDefinitionSO unit, Action<BuffOffer> callback)
    {
        offer = data;
        onSelected = callback;
        if (!data.IsValid) return;

        var skill = data.skill;

        // The star row belongs to the CHARACTER, not to this particular stat. Once a
        // hero has been upgraded even once, every later card for that hero shows a
        // star - whether the first pick was Attack, Defense or Speed.
        int stars = data.isGlobal ? data.currentStars : data.heroStars;

        BuildHero(unit);

        // There is NO "unlock" state. Every hero is already in the battle, so a card
        // always names its feature and its value. The only thing the star count
        // changes is the star row: none on the first pick, one more on each pick after.
        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(true);
            if (skill.normalIcon != null)
            {
                bool topTier = stars >= starsPerTier;
                iconImage.sprite = topTier && skill.evolvedIcon != null
                    ? skill.evolvedIcon : skill.normalIcon;
            }
        }

        if (nameText != null)
        {
            nameText.gameObject.SetActive(true);
            nameText.text = skill.skillName;
        }

        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.text = skill.description;
        }

        if (valueText != null)
        {
            valueText.gameObject.SetActive(true);
            valueText.text = "+" + Mathf.RoundToInt(data.increment * 100f) + "%";
            valueText.color = valueColor;
            valueText.fontStyle = FontStyles.Bold;
        }

        // These are leftovers from the old design and have no place on either state.
        if (firstUI != null) firstUI.SetActive(false);
        if (newRibbon != null) newRibbon.SetActive(false);

        DrawStars(stars);
    }

    /// <summary>
    /// Gold for the first tier, purple for the second. The row is centred by the
    /// container's own layout; only the count and colour change here.
    /// </summary>
    private void DrawStars(int stars)
    {
        bool show = stars > 0;
        if (starsContainer != null) starsContainer.gameObject.SetActive(show);

        idlePulse?.Kill();
        idlePulse = null;

        // The star row holds MORE children than starImages wires up (spare
        // "star-Off"/"Star-on" pairs from the old design). The layout group lays out
        // every ACTIVE child, so they must all be switched off first or the card
        // shows a full row of stars no matter what the count is.
        if (starsContainer != null)
            for (int i = 0; i < starsContainer.childCount; i++)
                starsContainer.GetChild(i).gameObject.SetActive(false);

        if (starImages == null) return;

        int tier = show ? (stars - 1) / starsPerTier : 0;
        int inTier = show ? ((stars - 1) % starsPerTier) + 1 : 0;
        var color = tier == 0 ? goldStar : purpleStar;

        for (int i = 0; i < starImages.Length; i++)
        {
            var s = starImages[i];
            if (s == null) continue;

            bool lit = show && i < inTier;
            s.gameObject.SetActive(lit);
            if (lit)
            {
                s.color = color;
                s.transform.localScale = Vector3.one;
            }
        }

        // The newest star breathes off->on so the eye lands on it.
        if (show && inTier > 0 && inTier <= starImages.Length)
        {
            var newest = starImages[inTier - 1];
            if (newest != null)
                idlePulse = newest.DOFade(0.45f, 0.55f)
                                  .SetLoops(-1, LoopType.Yoyo)
                                  .SetEase(Ease.InOutSine)
                                  .SetUpdate(true);       // unscaled: the game is paused
        }
    }

    /// <summary>
    /// Puts the hero on the card, idling.
    ///
    /// The rig CANNOT simply be parented into the card. Two hard reasons:
    ///   1. It is built from SpriteRenderers, and a SpriteRenderer is never drawn by
    ///      a ScreenSpaceOverlay canvas - which is the mode the card panel needs in
    ///      order to sit above the battlefield at all.
    ///   2. The card screen pauses the game with Time.timeScale = 0, which freezes
    ///      every Animator on the default update mode.
    ///
    /// So the hero is instantiated far off-screen, filmed by its own small
    /// orthographic camera into a RenderTexture, and that texture is shown in the
    /// card. The Animator is switched to UnscaledTime so it keeps playing while the
    /// game is paused.
    /// </summary>
    private void BuildHero(UnitDefinitionSO unit)
    {
        ClearPreview();

        if (unit == null)
        {
            if (portraitImage != null) portraitImage.gameObject.SetActive(false);
            return;
        }

        var source = unit.visualPrefab != null ? unit.visualPrefab : unit.runtimePrefab;
        var animatorSource = source != null ? source.GetComponentInChildren<Animator>(true) : null;

        // Only a MISSING RIG falls back to the portrait. A rig with no animator
        // controller (unit id 1, Valkir3) is still rendered - it just stands still,
        // which beats showing an empty card.
        if (previewImage == null || source == null)
        {
            ShowStaticPortrait(unit);
            return;
        }

        // Somewhere no level geometry will ever be, and unique per card so three
        // cards do not film each other.
        var stagePos = new Vector3(previewStageOrigin.x + (stageCounter++ * previewStageStride),
                                   previewStageOrigin.y, 0f);

        previewStage = new GameObject("Card Hero Preview");
        previewStage.transform.position = stagePos;

        var rig = Instantiate(source, previewStage.transform);
        rig.transform.localPosition = Vector3.zero;
        rig.transform.localRotation = Quaternion.identity;
        // NOT uiVisualScale. That value (108-165) exists to blow the rig up to pixel
        // size inside a canvas; here a camera frames whatever it is given, so scaling
        // only turns a 7-unit character into an 800-unit one and pushes its parts
        // outside the sane-bounds guard. Native scale, and let the camera do the work.
        rig.transform.localScale = Vector3.one;

        StripGameplayBehaviours(rig);

        var anim = rig.GetComponentInChildren<Animator>(true);
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.enabled = true;
            // Without this the idle sits on frame 0, because the card screen paused
            // the game before this card was ever built.
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Animator.Play takes a STATE name, not a clip name. Checking the clip
            // list was wrong and produced "toState: State could not be found", which
            // aborted the rig before it ever started. If the state is not there we
            // simply let the controller's own default state run.
            int hash = Animator.StringToHash(idleStateName);
            if (!string.IsNullOrEmpty(idleStateName) && anim.HasState(0, hash))
                anim.Play(hash, 0, UnityEngine.Random.value);
        }

        previewTexture = new RenderTexture(previewResolution, previewResolution, 16,
                                           RenderTextureFormat.ARGB32) { name = "HeroPreview" };
        previewTexture.Create();

        var camGo = new GameObject("Preview Camera");
        camGo.transform.SetParent(previewStage.transform, true);

        previewCamera = camGo.AddComponent<Camera>();
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.targetTexture = previewTexture;
        previewCamera.depth = -100;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = false;
        previewCamera.useOcclusionCulling = false;

        FrameHero(rig, stagePos);

        // The rig's parts are positioned by its own component in LateUpdate, so right
        // after Instantiate some renderers still report stale bounds. Re-frame once
        // they have settled, or the first frame is measured wrong.
        StartCoroutine(ReframeNextFrame(rig, stagePos));

        previewImage.texture = previewTexture;
        previewImage.color = Color.white;
        previewImage.gameObject.SetActive(true);

        if (portraitImage != null) portraitImage.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator ReframeNextFrame(GameObject rig, Vector3 stagePos)
    {
        yield return null;                       // advances even at timeScale 0
        if (rig != null && previewCamera != null) FrameHero(rig, stagePos);
    }

    /// <summary>
    /// Points the camera at this hero and nothing else.
    ///
    /// Renderers further from the stage than half the stride are IGNORED, and the
    /// resulting size is clamped. Without both guards a single stale renderer sitting
    /// near world origin stretches the bounding box across ten thousand units, the
    /// orthographic size explodes, and the camera films every other card's hero too -
    /// which is exactly how three cards ended up showing four characters between them.
    /// </summary>
    private void FrameHero(GameObject rig, Vector3 stagePos)
    {
        float maxReach = Mathf.Max(1f, previewStageStride * 0.4f);

        var bounds = new Bounds(stagePos, Vector3.zero);
        bool any = false;

        foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled) continue;

            // Anything this far out is not really part of the character yet.
            if (Vector3.Distance(r.bounds.center, stagePos) > maxReach) continue;

            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!any) bounds = new Bounds(stagePos, Vector3.one);

        float size = Mathf.Max(bounds.extents.x, bounds.extents.y) * previewPadding;
        size = Mathf.Clamp(size, 0.1f, maxReach);

        previewCamera.orthographicSize = size;
        previewCamera.transform.position =
            new Vector3(bounds.center.x, bounds.center.y, stagePos.z - 10f);
    }

    private void ShowStaticPortrait(UnitDefinitionSO unit)
    {
        if (previewImage != null) previewImage.gameObject.SetActive(false);
        if (portraitImage == null) return;

        portraitImage.gameObject.SetActive(unit.portrait != null);
        if (unit.portrait != null)
        {
            portraitImage.sprite = unit.portrait;
            portraitImage.color = Color.white;
        }
    }

    /// <summary>Tears down the off-screen stage, camera and texture for this card.</summary>
    private void ClearPreview()
    {
        if (previewStage != null) Destroy(previewStage);
        previewStage = null;
        previewCamera = null;

        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
            previewTexture = null;
        }

        if (previewImage != null) previewImage.texture = null;

        if (spawnedVisual != null) { Destroy(spawnedVisual); spawnedVisual = null; }
    }

    /// <summary>
    /// Only PHYSICS is removed: a card is not a battlefield, so colliders and bodies
    /// would fight the UI and warn about velocity on a static body.
    ///
    /// Scripts are deliberately LEFT ALONE. Spriter2UnityDX rigs are driven by their
    /// own component (EntityRenderer and friends) - deleting "everything that is not
    /// an Animator" freezes the character solid, which is exactly what happened when
    /// this method was more aggressive.
    /// </summary>
    private static void StripGameplayBehaviours(GameObject go)
    {
        foreach (var col in go.GetComponentsInChildren<Collider2D>(true)) Destroy(col);
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>(true)) Destroy(rb);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
    }

    /// <summary>
    /// Wired from the Button in Awake. Plays the pick animation and only reports the
    /// choice once it has FINISHED - the manager hides the panel the moment it hears
    /// back, so reporting immediately meant the animation was never seen.
    /// </summary>
    public void OnClick()
    {
        if (!offer.IsValid) { onSelected?.Invoke(offer); return; }

        // A second tap while the beat is playing would pick twice.
        if (clickSequence != null && clickSequence.IsActive()) return;

        PlaySelectFeedback();
    }

    /// <summary>
    /// The reference's selection beat, on the WHOLE CARD: it snaps big, dips small,
    /// settles back, while small stars burst out of the star row and fade. The pick is
    /// reported on completion.
    /// </summary>
    private void PlaySelectFeedback()
    {
        var card = transform;
        card.DOKill();
        card.localScale = Vector3.one;

        clickSequence = DOTween.Sequence().SetUpdate(true)   // unscaled: the game is paused
               .Append(card.DOScale(selectPopScale, 0.09f).SetEase(Ease.OutQuad))
               .Append(card.DOScale(selectDipScale, 0.07f).SetEase(Ease.InQuad))
               .Append(card.DOScale(1.00f, 0.09f).SetEase(Ease.OutBack))
               .OnComplete(() =>
               {
                   card.localScale = Vector3.one;
                   onSelected?.Invoke(offer);
               });

        SpawnStarBurst();
    }

    private void SpawnStarBurst()
    {
        if (starImages == null || starImages.Length == 0) return;

        Image source = null;
        for (int i = starImages.Length - 1; i >= 0; i--)
            if (starImages[i] != null && starImages[i].gameObject.activeSelf) { source = starImages[i]; break; }

        if (source == null) source = starImages[0];
        if (source == null) return;

        for (int i = 0; i < 6; i++)
        {
            var mote = Instantiate(source.gameObject, source.transform.parent);
            mote.SetActive(true);

            var rt = mote.transform as RectTransform;
            var img = mote.GetComponent<Image>();
            if (rt == null || img == null) { Destroy(mote); continue; }

            rt.anchoredPosition = (source.transform as RectTransform).anchoredPosition;
            rt.localScale = Vector3.one * 0.45f;

            var dir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(35f, 70f);

            DOTween.Sequence().SetUpdate(true)
                   .Join(rt.DOAnchorPos(rt.anchoredPosition + dir, 0.45f).SetEase(Ease.OutQuad))
                   .Join(rt.DOScale(0f, 0.45f).SetEase(Ease.InQuad))
                   .Join(img.DOFade(0f, 0.45f))
                   .OnComplete(() => { if (mote != null) Destroy(mote); });
        }
    }
}
