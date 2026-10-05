using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "You cleared every stage in this build - more are coming" screen.
///
/// Shown after the last stage that ships in the build (StageBuildAvailability),
/// and when the player presses BATTLE on a stage card that is not in the build.
/// The whole screen is built from code out of the game's own art (assigned by
/// ComingSoonSceneBuilder), so the scene holds one GameObject and nothing can
/// drift out of sync with a hand-made layout. All motion is DOTween on
/// unscaled time.
///
/// Layout follows the Level Complete reference (blue ribbon title, gold stars,
/// reward cells, yellow button) so it reads as part of the same game.
/// </summary>
public class ComingSoonScreen : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string title = "CHAPTER 1 CLEARED!";
    [SerializeField] private string subtitle = "STAGES 1 - 10 COMPLETE";
    [SerializeField] private string headline = "MORE STAGES COMING SOON!";
    [SerializeField, TextArea(2, 4)] private string body =
        "New stages are on their way in the next update.\nStay tuned, commander!";
    [SerializeField] private string buttonText = "BACK TO HOME";
    [SerializeField] private string homeScene = "MenuScene";

    [Header("Art (assigned by ComingSoonSceneBuilder)")]
    [SerializeField] private Sprite skyBackground;
    [SerializeField] private Sprite patternOverlay;
    [SerializeField] private Sprite darkOverlay;
    [SerializeField] private Sprite ribbon;
    [SerializeField] private Sprite[] stars = new Sprite[3];
    [SerializeField] private Sprite heroCell;
    [SerializeField] private Sprite button;
    [SerializeField] private Sprite sparkle;
    [SerializeField] private Sprite sparkleTail;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material fontOutlineMaterial;

    [Header("Fallback heroes when no roster is loaded (e.g. playing the scene directly)")]
    [SerializeField] private Sprite[] fallbackPortraits;

    private static readonly Color Gold = new Color32(0xff, 0xd2, 0x01, 0xff);

    private RectTransform _root;
    private bool _leaving;

    private void Awake()
    {
        Time.timeScale = 1f;
        BuildCanvas();
        Build();
    }

    // ------------------------------------------------------------------
    //  Building
    // ------------------------------------------------------------------

    private void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1125, 2436);   // same as the game's HUD
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();
        _root = (RectTransform)transform;
    }

    private void Build()
    {
        // Background: the Home sky, the skull pattern, then a dark wash for contrast.
        Stretch(Img("Sky", _root, skyBackground, new Color(0.55f, 0.62f, 0.85f)));
        if (patternOverlay) Stretch(Img("Pattern", _root, patternOverlay, new Color(1, 1, 1, 0.18f)));
        var wash = Img("Wash", _root, darkOverlay, new Color(0.06f, 0.09f, 0.2f, 0.72f));
        Stretch(wash);

        BuildSparkles();

        // Ribbon title.
        var ribbonImg = Img("Ribbon", _root, ribbon, Color.white);
        Place(ribbonImg, new Vector2(0, 760), new Vector2(980, 260), preserve: true);
        var titleTxt = Txt("Title", ribbonImg.rectTransform, title, 66, Color.white, outline: true);
        Stretch(titleTxt);
        titleTxt.rectTransform.offsetMin = new Vector2(80, 20);
        titleTxt.rectTransform.offsetMax = new Vector2(-80, -10);

        // Sits in the dark frame the ribbon art carries under its title.
        var sub = Txt("Subtitle", _root, subtitle, 36, Gold, outline: true);
        Place(sub, new Vector2(0, 676), new Vector2(760, 60));

        // Three single stars, the middle one bigger. (The art set's 02/03 sprites
        // are 2- and 3-star RATING icons, so only the single star is used.)
        var starRow = new List<Image>();
        float[] sx = { -230, 0, 230 };
        float[] sy = { 440, 480, 440 };
        float[] ss = { 170, 220, 170 };
        Sprite single = stars != null && stars.Length > 0 ? stars[0] : null;
        for (int i = 0; i < 3; i++)
        {
            var st = Img("Star" + i, _root, single, Color.white);
            Place(st, new Vector2(sx[i], sy[i]), new Vector2(ss[i], ss[i]), preserve: true);
            starRow.Add(st);
        }

        // Heroes in reward cells.
        var heroes = BuildHeroRow();

        // Headline + body.
        var head = Txt("Headline", _root, headline, 62, Gold, outline: true);
        Place(head, new Vector2(0, -260), new Vector2(1050, 90));
        var bodyTxt = Txt("Body", _root, body, 40, new Color(0.92f, 0.95f, 1f), outline: false);
        Place(bodyTxt, new Vector2(0, -400), new Vector2(980, 160));

        // Button.
        var btnImg = Img("HomeButton", _root, button, Color.white);
        Place(btnImg, new Vector2(0, -720), new Vector2(560, 190), preserve: true);
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(GoHome);
        var btnTxt = Txt("Label", btnImg.rectTransform, buttonText, 50, Color.white, outline: true);
        Stretch(btnTxt);
        btnTxt.rectTransform.offsetMin = new Vector2(30, 18);

        Animate(ribbonImg.rectTransform, sub, starRow, heroes, head, bodyTxt, btnImg.rectTransform);
    }

    private List<RectTransform> BuildHeroRow()
    {
        var portraits = new List<Sprite>();
        var gsm = GameStartManager.Instance;
        if (gsm && gsm.unitsDatabase && gsm.PlayerUnits != null)
            foreach (var def in gsm.unitsDatabase.Units)
                if (def && def.portrait && gsm.PlayerUnits.IsDeployed(def.unitId) && portraits.Count < 4)
                    portraits.Add(def.portrait);
        if (portraits.Count == 0 && fallbackPortraits != null)
            foreach (var p in fallbackPortraits) if (p && portraits.Count < 4) portraits.Add(p);

        var cells = new List<RectTransform>();
        int n = portraits.Count;
        const float cell = 210f, gap = 30f;
        float start = -(n - 1) * (cell + gap) * 0.5f;
        for (int i = 0; i < n; i++)
        {
            var c = Img("HeroCell" + i, _root, heroCell, Color.white);
            Place(c, new Vector2(start + i * (cell + gap), 130), new Vector2(cell, cell));
            var p = Img("Portrait", c.rectTransform, portraits[i], Color.white);
            p.preserveAspect = true;
            Stretch(p);
            p.rectTransform.offsetMin = new Vector2(18, 18);
            p.rectTransform.offsetMax = new Vector2(-18, -18);
            cells.Add(c.rectTransform);
        }
        return cells;
    }

    private void BuildSparkles()
    {
        if (!sparkle) return;
        var rng = new System.Random(10);
        for (int i = 0; i < 26; i++)
        {
            bool tail = sparkleTail && i % 3 == 0;
            var s = Img(tail ? "Streak" : "Sparkle", _root, tail ? sparkleTail : sparkle, new Color(1, 1, 1, 0));
            float size = tail ? 26 + (float)rng.NextDouble() * 22 : 18 + (float)rng.NextDouble() * 30;
            var start = new Vector2(-540 + (float)rng.NextDouble() * 1080, -1200 + (float)rng.NextDouble() * 2400);
            Place(s, start, tail ? new Vector2(size * 0.45f, size) : new Vector2(size, size), preserve: true);

            float dur = 2.5f + (float)rng.NextDouble() * 3f;
            float delay = (float)rng.NextDouble() * 3f;
            s.rectTransform.DOAnchorPosY(start.y + 260 + (float)rng.NextDouble() * 200, dur)
                .SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear).SetDelay(delay).SetUpdate(true);
            s.DOFade(0.95f, dur * 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetDelay(delay).SetUpdate(true);
            if (!tail)
                s.rectTransform.DORotate(new Vector3(0, 0, 360), 4f + (float)rng.NextDouble() * 4f, RotateMode.FastBeyond360)
                    .SetLoops(-1).SetEase(Ease.Linear).SetUpdate(true);
        }
    }

    // ------------------------------------------------------------------
    //  Entrance choreography
    // ------------------------------------------------------------------

    private void Animate(RectTransform ribbonRt, TMP_Text sub, List<Image> starRow, List<RectTransform> heroes,
                         TMP_Text head, TMP_Text bodyTxt, RectTransform btn)
    {
        var seq = DOTween.Sequence().SetUpdate(true);

        // Ribbon drops in from above with a little overshoot.
        float ribbonY = ribbonRt.anchoredPosition.y;
        ribbonRt.anchoredPosition += new Vector2(0, 500);
        seq.Append(ribbonRt.DOAnchorPosY(ribbonY, 0.55f).SetEase(Ease.OutBack));

        sub.alpha = 0;
        seq.Append(sub.DOFade(1f, 0.25f));

        // Stars pop one after another.
        foreach (var st in starRow)
        {
            st.rectTransform.localScale = Vector3.zero;
            seq.Append(st.rectTransform.DOScale(1f, 0.28f).SetEase(Ease.OutBack, 2.2f));
            seq.Join(st.rectTransform.DOPunchRotation(new Vector3(0, 0, 18), 0.35f, 8, 0.6f));
        }

        // Heroes bounce up into their cells, then bob forever.
        for (int i = 0; i < heroes.Count; i++)
        {
            var h = heroes[i];
            h.localScale = Vector3.zero;
            seq.Insert(1.4f + i * 0.12f, h.DOScale(1f, 0.4f).SetEase(Ease.OutBack, 1.8f));
            float y = h.anchoredPosition.y;
            h.DOAnchorPosY(y + 14f, 0.9f + i * 0.07f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                .SetDelay(2.2f + i * 0.1f).SetUpdate(true);
        }

        head.alpha = 0;
        head.rectTransform.localScale = Vector3.one * 0.6f;
        seq.Insert(2.0f, head.DOFade(1f, 0.3f));
        seq.Insert(2.0f, head.rectTransform.DOScale(1f, 0.45f).SetEase(Ease.OutBack));
        bodyTxt.alpha = 0;
        seq.Insert(2.35f, bodyTxt.DOFade(1f, 0.4f));

        btn.localScale = Vector3.zero;
        seq.Insert(2.6f, btn.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            if (head) head.rectTransform.DOScale(1.06f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            if (btn) btn.DOScale(1.05f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
            foreach (var st in starRow)
                if (st) st.rectTransform.DORotate(new Vector3(0, 0, 6), 1.2f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetUpdate(true);
        });
    }

    private void GoHome()
    {
        if (_leaving) return;
        _leaving = true;
        DOTween.KillAll();
        SceneManager.LoadScene(homeScene);
    }

    private void OnDestroy() => DOTween.Kill(this);

    // ------------------------------------------------------------------
    //  Small UI helpers
    // ------------------------------------------------------------------

    private static Image Img(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text Txt(string name, RectTransform parent, string text, float size, Color color, bool outline)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font) t.font = font;
        if (outline && fontOutlineMaterial) t.fontSharedMaterial = fontOutlineMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private static void Place(Graphic g, Vector2 pos, Vector2 size, bool preserve = false)
    {
        var rt = g.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (preserve && g is Image img) img.preserveAspect = true;
    }

    private static void Stretch(Graphic g)
    {
        var rt = g.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
