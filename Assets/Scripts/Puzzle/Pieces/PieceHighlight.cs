using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The pick-up feedback on ONE stack: a glowing halo, a small scale-up, and a quick
/// left/right shake. Added at runtime by <see cref="PieceHighlightDirector"/>; there is
/// nothing to author on the prefabs.
///
/// The halo is built from the piece's OWN sprite, so it follows the real silhouette of
/// every stack shape without any per-shape authoring:
///   - four offset copies drawn BEHIND the piece  -> the rim (only the fringe shows,
///     because the piece itself is opaque)
///   - one copy drawn ON TOP                      -> the brighten
///
/// !! EVERYTHING HERE IS VISUAL-ONLY, AND IT MUST STAY THAT WAY. The scale and the shake
/// !! are applied to the piece's "Visual ..." SpriteRenderer transforms, never to the root
/// !! and never to the Cell_* collider children. Scaling or rotating a collider would move
/// !! the bodies that BoardGridXY legality, MatchResolver.ArePiecesTouching and the pointer
/// !! pick all read, so a cosmetic 3% would quietly change what the player can place where.
/// </summary>
[DisallowMultipleComponent]
public class PieceHighlight : MonoBehaviour
{
    // ---- look -------------------------------------------------------------------
    private const float RimPixels = 4f;      // rim thickness, in sprite pixels
    private const float RimAlpha = 1.00f;    // the rim is a solid white silhouette

    // The body lift is NOT white. White alpha-blended over the stack can only wash it
    // toward grey - at 0.28 the green stacks turned pale mint, which is the opposite of the
    // reference, where the piece gets BRIGHTER and keeps its hue. Instead the overlay is
    // tinted with the stack's OWN highlight band, sampled by PieceTintSampler, so blending
    // it in lifts the colour along its own ramp. Additive would also work but cannot be
    // used here - see the sorting note in MakeCopy.
    private const float GlowAlpha = 0.55f;
    private const float FadeTime = 0.12f;    // halo fade in / out

    // ---- emphasis ---------------------------------------------------------------
    private const float EmphasisScale = 1.03f;   // +3%
    private const float EmphasisTime = 0.10f;

    // ---- shake ------------------------------------------------------------------
    private const float ShakeDegrees = 5f;    // +/- 5 degrees
    private const float ShakeDuration = 0.60f; // whole wobble, start to rest
    private const float ShakeCycles = 2.5f;   // left-right round trips inside that time

    private struct Visual
    {
        public Transform transform;
        public Vector3 baseScale;
        public Quaternion baseRotation;
        public SpriteRenderer[] halo;   // 4 rim copies + 1 brighten copy
        public Color glowTint;          // the stack's own highlight band
    }

    private readonly List<Visual> _visuals = new();
    private bool _built;

    /// <summary>
    /// White-silhouette copies of each stack sprite, built once per sprite per session.
    ///
    /// !! A white SpriteRenderer.color does NOT give a white shape - the colour MULTIPLIES
    /// !! the texture, so a "white" copy of a green block is just the green block again.
    /// !! That is why the halo needs a real silhouette texture rather than a tint.
    /// </summary>
    private static readonly Dictionary<Sprite, Sprite> _whiteCache = new();

    private float _haloTarget;   // 0 or 1
    private float _halo;         // current, eased

    private float _emphasisTarget;
    private float _emphasis;

    private float _shakeT = -1f; // <0 = not shaking

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Fades the halo in or out. Safe to call every frame.</summary>
    public void SetHalo(bool on)
    {
        Build();
        _haloTarget = on ? 1f : 0f;
        enabled = true;
    }

    /// <summary>Holds the piece at +3% while it is the held piece, or an in-range match.</summary>
    public void SetEmphasis(bool on)
    {
        Build();
        _emphasisTarget = on ? 1f : 0f;
        enabled = true;
    }

    /// <summary>One-shot left/right wobble. Restarts if it is already running.</summary>
    public void PlayShake()
    {
        Build();
        _shakeT = 0f;
        enabled = true;
    }

    /// <summary>Drops everything immediately - used when a drag ends.</summary>
    public void ClearAll()
    {
        _haloTarget = 0f;
        _emphasisTarget = 0f;
        _shakeT = -1f;
        enabled = true;
    }

    // ------------------------------------------------------------------
    // Build
    // ------------------------------------------------------------------

    private void Build()
    {
        if (_built) return;
        _built = true;

        // Every SpriteRenderer that is part of the piece's own art. Collected BEFORE any
        // halo is created, so the halo can never be mistaken for a source sprite.
        var sources = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (var src in sources)
        {
            if (!src || !src.sprite) continue;

            // The stack's own highlight band drives the body lift, so the piece brightens
            // along its own colour ramp instead of fading toward white. Falls back to a
            // plain lighten if the sprite cannot be sampled.
            Color glowTint = Color.white;
            if (PieceTintSampler.TryGetSpriteBands(src.sprite, out var bands))
                glowTint = bands.light;

            var v = new Visual
            {
                transform = src.transform,
                baseScale = src.transform.localScale,
                baseRotation = src.transform.localRotation,
                halo = new SpriteRenderer[5],
                glowTint = glowTint
            };

            // Rim offset in WORLD units, derived from the sprite's own pixels-per-unit so a
            // 114px stack and a 228px stack get the same visual thickness.
            float unit = RimPixels / Mathf.Max(1f, src.sprite.pixelsPerUnit);

            Vector2[] offsets =
            {
                new Vector2(+unit, 0f), new Vector2(-unit, 0f),
                new Vector2(0f, +unit), new Vector2(0f, -unit),
            };

            for (int i = 0; i < 4; i++)
                v.halo[i] = MakeCopy(src, offsets[i], src.sortingOrder - 1, "Rim" + i);

            // Brighten sits on top of the piece.
            v.halo[4] = MakeCopy(src, Vector2.zero, src.sortingOrder + 1, "Glow");

            _visuals.Add(v);
        }

        ApplyHalo(0f);
    }

    private SpriteRenderer MakeCopy(SpriteRenderer src, Vector2 offset,
                                    int sortingOrder, string name)
    {
        var go = new GameObject("~Highlight" + name);
        // Parented to the visual, so it inherits the scale-up and the shake for free.
        go.transform.SetParent(src.transform, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteSilhouette(src.sprite);

        // !! SHARE THE PIECE'S OWN MATERIAL. Do NOT give these a custom shader.
        // The stacks render with Universal Render Pipeline/2D/Sprite-Lit-Default, and the
        // URP 2D renderer collects lit sprites and SRPDefaultUnlit sprites in SEPARATE
        // batches - so sortingOrder does not order a custom-shader copy against a lit
        // sprite at all. A first version used an additive unlit shader and every copy drew
        // on top of the piece regardless of order, saturating the whole stack to solid
        // white. Same material = same pass = sortingOrder is honoured.
        sr.sharedMaterial = src.sharedMaterial;

        sr.sortingLayerID = src.sortingLayerID;
        sr.sortingOrder = sortingOrder;
        sr.color = new Color(1f, 1f, 1f, 0f);
        return sr;
    }

    /// <summary>
    /// A pure-white copy of <paramref name="source"/> keeping its alpha, its pivot and its
    /// pixels-per-unit, so it lines up with the original exactly. Cached per sprite.
    ///
    /// Reads the pixels by blitting through a RenderTexture rather than calling GetPixels
    /// directly - the stack textures do NOT have Read/Write enabled in their importers, and
    /// this is the same trick PieceTintSampler uses for the same reason. Do not "simplify"
    /// it to source.texture.GetPixels(); it will throw.
    /// </summary>
    private static Sprite WhiteSilhouette(Sprite source)
    {
        if (!source) return null;
        if (_whiteCache.TryGetValue(source, out var cached) && cached) return cached;

        var tex = source.texture;
        if (!tex) return source;

        var rect = source.textureRect;
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));

        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0,
                                            RenderTextureFormat.ARGB32,
                                            RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        Sprite made = source;

        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);

            var px = readable.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                // Keep alpha, throw the colour away. The alpha is what carries the rounded
                // silhouette, so the rim follows the real shape and not a bounding box.
                px[i].r = 255; px[i].g = 255; px[i].b = 255;
            }
            readable.SetPixels32(px);
            readable.Apply(false);
            readable.filterMode = tex.filterMode;
            readable.wrapMode = TextureWrapMode.Clamp;

            // Pivot as a 0-1 fraction of the rect, matching how Sprite.Create expects it.
            var pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);

            made = Sprite.Create(readable, new Rect(0, 0, w, h), pivot,
                                 source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            made.name = source.name + " (white)";
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PieceHighlight] Could not build a silhouette for '"
                             + source.name + "': " + e.Message);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }

        _whiteCache[source] = made;
        return made;
    }

    // ------------------------------------------------------------------
    // Tick
    // ------------------------------------------------------------------

    private void Update()
    {
        if (!_built) { enabled = false; return; }

        float dt = Time.deltaTime;

        _halo = Step(_halo, _haloTarget, dt / FadeTime);
        _emphasis = Step(_emphasis, _emphasisTarget, dt / EmphasisTime);

        ApplyHalo(_halo);

        float shakeAngle = 0f;
        if (_shakeT >= 0f)
        {
            _shakeT += dt;
            if (_shakeT >= ShakeDuration)
            {
                _shakeT = -1f;
            }
            else
            {
                // Decaying wobble: full swing at the start, settled by ShakeDuration.
                float k = _shakeT / ShakeDuration;
                float decay = 1f - k;
                shakeAngle = ShakeDegrees * decay *
                             Mathf.Sin(k * ShakeCycles * 2f * Mathf.PI);
            }
        }

        ApplyTransform(shakeAngle);

        // Nothing left to animate - stop ticking until something asks again.
        if (_halo <= 0.001f && _haloTarget <= 0f &&
            _emphasis <= 0.001f && _emphasisTarget <= 0f &&
            _shakeT < 0f)
        {
            enabled = false;
        }
    }

    private static float Step(float current, float target, float rate)
    {
        return Mathf.MoveTowards(current, target, Mathf.Max(rate, 0f));
    }

    private void ApplyHalo(float t)
    {
        float rim = RimAlpha * t;
        float glow = GlowAlpha * t;

        for (int i = 0; i < _visuals.Count; i++)
        {
            var halo = _visuals[i].halo;
            for (int j = 0; j < 4; j++)
                if (halo[j]) halo[j].color = new Color(1f, 1f, 1f, rim);

            // The silhouette is white, so this tint IS the resulting colour.
            var g = _visuals[i].glowTint;
            if (halo[4]) halo[4].color = new Color(g.r, g.g, g.b, glow);
        }
    }

    private void ApplyTransform(float shakeAngle)
    {
        float scale = Mathf.Lerp(1f, EmphasisScale, _emphasis);

        for (int i = 0; i < _visuals.Count; i++)
        {
            var v = _visuals[i];
            if (!v.transform) continue;

            v.transform.localScale = v.baseScale * scale;
            v.transform.localRotation = Mathf.Abs(shakeAngle) > 0.0001f
                ? v.baseRotation * Quaternion.Euler(0f, 0f, shakeAngle)
                : v.baseRotation;
        }
    }
}
