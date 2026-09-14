using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the face-DOWN hero card ("?" back) into a real sprite asset at
/// Assets/Arts/VFX/HeroCardBack.png.
///
/// WHY A BAKED PNG AND NOT A RUNTIME TEXTURE:
/// the card back is art, not an effect. Baking it to an asset means it shows up in
/// the Project window, can be swapped for hand-drawn art later without touching a
/// line of code, and is reviewable. A Texture2D built at runtime would be invisible
/// to the artist and would have to be rebuilt on every load.
///
/// WHY PROCEDURAL AND NOT AN IMPORTED IMAGE:
/// the shape is a handful of primitives (rounded rect, inset frame, diamond,
/// question mark) and every one of them is authored here in pixels, so the whole
/// look is a diff. Re-run the menu item after changing any constant below.
///
/// Everything is drawn with signed distance fields and resolved with a half-pixel
/// smoothstep, which is what keeps the curves clean at 256px without supersampling.
///
/// Menu: Tools > Blasty > Bake Hero Card Back Sprite
/// </summary>
public static class CardBackSpriteBaker
{
    public const string OutputPath = "Assets/Arts/VFX/HeroCardBack.png";

    /// <summary>The same frame with NO diamond and NO "?" - the face a portrait sits on.</summary>
    public const string OutputPathFace = "Assets/Arts/VFX/HeroCardFace.png";

    /// <summary>
    /// Soft card-shaped glow, baked WHITE so the director can tint it (the teal
    /// halo of Ref1 fr.13). Larger than the card on every side - the bleed IS the
    /// glow, so it cannot live inside the card's own bounds.
    /// </summary>
    public const string OutputPathGlow = "Assets/Arts/VFX/HeroCardGlow.png";

    // 2.5:3.5 - the playing-card ratio Ref1 uses (measured ~0.70 w/h off the clip).
    private const int W = 256;
    private const int H = 358;

    private const int GlowPad = 72;
    private const int GW = W + GlowPad * 2;
    private const int GH = H + GlowPad * 2;

    // --- palette -----------------------------------------------------------
    // Sampled to match the silver/grey back in the reference clip.
    private static readonly Color RimLight = new Color32(0xF4, 0xF6, 0xFA, 0xFF);
    private static readonly Color BodyTop = new Color32(0xCB, 0xD1, 0xDB, 0xFF);
    private static readonly Color BodyBottom = new Color32(0xA3, 0xAC, 0xBA, 0xFF);
    private static readonly Color InsetLine = new Color32(0xE2, 0xE6, 0xEC, 0xFF);
    private static readonly Color DiamondLine = new Color32(0xDB, 0xDF, 0xE7, 0xFF);
    private static readonly Color GlyphInk = new Color32(0x77, 0x80, 0x8E, 0xFF);

    // --- geometry (pixels) -------------------------------------------------
    private const float CornerRadius = 30f;
    private const float RimThickness = 9f;
    private const float InsetMargin = 18f;
    private const float InsetRadius = 20f;
    private const float InsetStroke = 5f;

    private const float DiamondHalfW = 88f;
    private const float DiamondHalfH = 126f;
    private const float DiamondStroke = 6f;

    // Question mark. The hook is a ring segment; the tail is two capsules
    // (diagonal then vertical) so it reads as a real "?" rather than a hook
    // with a stick under it.
    private const float HookRadius = 42f;
    private const float Stroke = 15f;          // full stroke width
    private const float GlyphCenterX = W * 0.5f;
    private const float HookCenterY = 154f;    // tuned so the whole glyph is optically centred
    private const float HookStartDeg = 200f;   // sweeps CLOCKWISE from here...
    private const float HookEndDeg = -10f;     // ...to here, over the top of the circle

    [MenuItem("Tools/Blasty/Bake Hero Card Sprites")]
    public static void Bake()
    {
        BakeOne(OutputPath, W, H, ShadeCardBack);
        BakeOne(OutputPathFace, W, H, ShadeCardFace);
        BakeOne(OutputPathGlow, GW, GH, ShadeCardGlow);

        Debug.Log($"[CardBackSpriteBaker] Baked card back + face ({W}x{H}) and glow ({GW}x{GH}).");
    }

    private static void BakeOne(string path, int w, int h, System.Func<float, float, Color> shade)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        var pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Pixel centre, y measured DOWNWARD so the constants above read
                // the same way they look on screen.
                float px = x + 0.5f;
                float py = (h - 1 - y) + 0.5f;

                pixels[y * w + x] = shade(px, py);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllBytes(full, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(path);
    }

    /// <summary>
    /// Sprite, single, centre pivot, no compression. Point-free (bilinear) because
    /// the card is scaled and rotated all the way through the deal animation.
    /// </summary>
    private static void ConfigureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;

        importer.SaveAndReimport();
    }

    // ======================================================================
    //  Shading
    // ======================================================================

    private static Color ShadeCardBack(float px, float py) => ShadeCard(px, py, true);

    /// <summary>
    /// The face-UP card: identical frame, but the middle is left clean because a
    /// hero portrait is composited over it at runtime.
    /// </summary>
    private static Color ShadeCardFace(float px, float py) => ShadeCard(px, py, false);

    /// <summary>
    /// White card-shaped glow. Alpha only - the director tints it teal. The falloff
    /// is squared so the halo is dense against the card edge and thins out fast,
    /// which is what reads as "glow" rather than "grey border".
    /// </summary>
    private static Color ShadeCardGlow(float px, float py)
    {
        float d = SdRoundRect(px - GW * 0.5f, py - GH * 0.5f,
                              W * 0.5f, H * 0.5f, CornerRadius);

        // Inside the card footprint the glow is solid; outside it falls to zero
        // across the padding.
        float t = Mathf.Clamp01(1f - d / GlowPad);
        float a = t * t;

        return new Color(1f, 1f, 1f, a);
    }

    private static Color ShadeCard(float px, float py, bool withBackArt)
    {
        float cx = W * 0.5f;
        float cy = H * 0.5f;

        // Card silhouette. Everything else is masked by this, so a pixel outside
        // it is transparent no matter what the other shapes say.
        float card = SdRoundRect(px - cx, py - cy, W * 0.5f - 1f, H * 0.5f - 1f, CornerRadius);
        float cardA = Coverage(card);
        if (cardA <= 0f) return Color.clear;

        // Vertical body gradient, lighter at the top.
        Color c = Color.Lerp(BodyTop, BodyBottom, Mathf.Clamp01(py / H));

        // Bright rim just inside the silhouette edge.
        float rim = Mathf.Abs(card + RimThickness * 0.5f) - RimThickness * 0.5f;
        c = Blend(c, RimLight, Coverage(rim));

        // Inset frame line.
        float inset = SdRoundRect(px - cx, py - cy,
                                  W * 0.5f - InsetMargin, H * 0.5f - InsetMargin, InsetRadius);
        float insetRing = Mathf.Abs(inset) - InsetStroke * 0.5f;
        c = Blend(c, InsetLine, Coverage(insetRing));

        if (withBackArt)
        {
            // Centre diamond.
            float diamond = SdDiamond(px - cx, py - cy, DiamondHalfW, DiamondHalfH);
            float diamondRing = Mathf.Abs(diamond) - DiamondStroke * 0.5f;
            c = Blend(c, DiamondLine, Coverage(diamondRing));

            // The question mark, on top of everything.
            c = Blend(c, GlyphInk, Coverage(SdQuestionMark(px, py)));
        }

        c.a = cardA;
        return c;
    }

    /// <summary>
    /// "?" as the union of three primitives: the hook (an arc), the diagonal tail
    /// running from the arc's end back to the centre line, and the dot.
    /// </summary>
    private static float SdQuestionMark(float px, float py)
    {
        float half = Stroke * 0.5f;
        float x = px - GlyphCenterX;
        float y = py - HookCenterY;

        // Hook: ring segment. Angles are measured the usual way (0 = +X, CCW
        // positive), which is why y is negated - py grows downward.
        float ring = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - HookRadius) - half;
        float ang = Mathf.Atan2(-y, x) * Mathf.Rad2Deg;
        float hook = InSweepClockwise(ang, HookStartDeg, HookEndDeg)
            ? ring
            : float.MaxValue;

        // Where the arc stops, so the tail starts exactly on it with no seam.
        float endRad = HookEndDeg * Mathf.Deg2Rad;
        float ex = GlyphCenterX + Mathf.Cos(endRad) * HookRadius;
        float ey = HookCenterY - Mathf.Sin(endRad) * HookRadius;

        float midX = GlyphCenterX;
        float midY = HookCenterY + HookRadius * 1.22f;

        float tail = SdSegment(px, py, ex, ey, midX, midY) - half;
        float stem = SdSegment(px, py, midX, midY, midX, HookCenterY + HookRadius * 1.55f) - half;

        // Dot.
        float dotY = HookCenterY + HookRadius * 2.15f;
        float dot = Mathf.Sqrt((px - GlyphCenterX) * (px - GlyphCenterX) +
                               (py - dotY) * (py - dotY)) - half * 1.12f;

        return Mathf.Min(Mathf.Min(hook, tail), Mathf.Min(stem, dot));
    }

    /// <summary>
    /// True when <paramref name="ang"/> lies on the clockwise sweep from
    /// <paramref name="startDeg"/> down to <paramref name="endDeg"/>.
    ///
    /// Everything is folded into [0,360) first: the raw angles disagree about sign
    /// either side of the -180/180 seam, and comparing them directly puts a wedge
    /// of missing pixels on the left of the hook - exactly where the arc starts.
    /// </summary>
    private static bool InSweepClockwise(float ang, float startDeg, float endDeg)
    {
        float sweep = Mod360(startDeg - endDeg);
        float fromStart = Mod360(startDeg - ang);
        return fromStart <= sweep;
    }

    private static float Mod360(float v)
    {
        v %= 360f;
        return v < 0f ? v + 360f : v;
    }

    // ======================================================================
    //  Signed distance helpers - all return <0 inside, >0 outside, in pixels.
    // ======================================================================

    private static float SdRoundRect(float x, float y, float halfW, float halfH, float r)
    {
        float qx = Mathf.Abs(x) - (halfW - r);
        float qy = Mathf.Abs(y) - (halfH - r);
        float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                   Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    /// <summary>
    /// Diamond |x|/a + |y|/b = 1, normalised by the gradient so the result is in
    /// pixels rather than in units of the implicit function - without that the
    /// stroke width would vary along the edge.
    /// </summary>
    private static float SdDiamond(float x, float y, float a, float b)
    {
        float f = Mathf.Abs(x) * b + Mathf.Abs(y) * a - a * b;
        return f / Mathf.Sqrt(a * a + b * b);
    }

    private static float SdSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float pax = px - ax, pay = py - ay;
        float bax = bx - ax, bay = by - ay;
        float denom = bax * bax + bay * bay;
        float h = denom <= Mathf.Epsilon ? 0f : Mathf.Clamp01((pax * bax + pay * bay) / denom);
        float dx = pax - bax * h, dy = pay - bay * h;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Half-pixel antialiased coverage from a signed distance.</summary>
    private static float Coverage(float d) => Mathf.Clamp01(0.5f - d);

    private static Color Blend(Color under, Color over, float a)
    {
        if (a <= 0f) return under;
        return Color.Lerp(under, over, a);
    }
}
