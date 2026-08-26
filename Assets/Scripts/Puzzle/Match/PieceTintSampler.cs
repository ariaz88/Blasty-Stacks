using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads the body colour straight off a stack's own sprite, so the shatter shards match
/// the block exactly instead of tracking a hand-maintained colour table that drifts.
///
/// The rule this implements: sample ONE small square block per colour, then give that
/// same colour to every shape carrying that colour - 1X, 2X, 3X, L-shapes, all of them.
/// Shape is irrelevant; only the colour family matters.
///
/// It cannot be keyed on PieceSimple.ColorId or on sprite names, because both lie:
///   - colorId 8 is used by Orange, Red AND Yellow sprites; colorId 3 by Pink and lavender.
///   - "Mid Pink 1X.png" is actually purple (#A46ACD); "Purple 1X.png" is lavender (#C49CCF).
/// So the colours group themselves: a sprite whose centre is a flat, uniform face IS a
/// small square, and it registers a canonical palette entry. Every other shape samples
/// itself and then snaps to the nearest canonical entry.
///
/// Results are cached per Sprite: the read-back happens once per sprite for the whole session.
/// </summary>
public static class PieceTintSampler
{
    /// <summary>
    /// The three flat colours a block is actually painted with: the dark bevel, the body
    /// (top face) and the highlight. The shards are drawn using ONLY these, in flat bands,
    /// so a shard reads as a chip off the same 2D block rather than a lit 3D rock.
    /// </summary>
    public struct TintBands
    {
        public Color dark;
        public Color body;
        public Color light;

        /// <summary>
        /// Builds a plausible dark/light pair around a body colour, for the fallback paths
        /// where only one colour is known. The factors are fitted to the real art:
        /// yellow body #F2C14E gives #967830 / #F7DB98 against the painted #9D651C / #FFE19B.
        /// </summary>
        public static TintBands FromBody(Color body)
        {
            return new TintBands
            {
                dark = new Color(body.r * 0.62f, body.g * 0.62f, body.b * 0.62f, 1f),
                body = body,
                light = Color.Lerp(body, Color.white, 0.42f)
            };
        }
    }

    private static readonly Dictionary<Sprite, TintBands> _cache = new();

    /// <summary>
    /// Canonical colours discovered so far. <see cref="Entry.isReference"/> marks the ones
    /// that came from a flat small square rather than being inferred from a bevelled shape.
    /// </summary>
    private struct Entry
    {
        public TintBands bands;
        public bool isReference;
    }

    private static readonly List<Entry> _palette = new();

    // A histogram bucket must cover at least this share of the opaque pixels to count as a
    // real painted band rather than antialiasing between two bands. The real bands are
    // 62% / 14% / 8%, and the next contender is 4.7%, so 2% is a comfortable floor.
    private const float MinBandShare = 0.02f;

    // Fraction of the sprite rect sampled for the centre probe. 0.30 keeps the box well
    // inside the top face of a 1X block while still averaging over ~1000 pixels.
    private const float CentreBoxFraction = 0.30f;

    // Max RGB spread inside the centre box for it to count as "one flat face". Measured:
    // a 1X/2X centre is literally 0.000, an L-shaped 3X centre lands on a bevel at 0.8+.
    private const float UniformTolerance = 0.05f;

    // How close two colours must be to be treated as the same family. The closest genuine
    // pair in the art is Crimson #D14A3C vs Dark Pink #C82E64 at 0.198 apart, and the
    // shape-to-square drift is under 1/255, so anything in ~0.01..0.09 is safe.
    private const float SnapDistance = 0.09f;

    // Quantisation grid for the fallback histogram. 32 levels per channel is coarse enough
    // that a gradient still lands in one bucket, fine enough to separate the block's bands.
    private const int Levels = 32;

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Finds the largest sprite under <paramref name="root"/> and returns its body colour.
    /// Returns false if there is no sprite to sample.
    /// </summary>
    public static bool TryGetTint(GameObject root, out Color tint)
    {
        tint = Color.white;
        if (!TryGetBands(root, out TintBands bands)) return false;
        tint = bands.body;
        return true;
    }

    /// <summary>
    /// Finds the largest sprite under <paramref name="root"/> and returns the three flat
    /// colours it is painted with. Returns false if there is no sprite to sample.
    /// </summary>
    public static bool TryGetBands(GameObject root, out TintBands bands)
    {
        bands = TintBands.FromBody(Color.white);
        if (!root) return false;

        var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);

        SpriteRenderer best = null;
        float bestArea = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            var sr = renderers[i];
            if (!sr || !sr.sprite) continue;

            var r = sr.sprite.rect;
            float area = r.width * r.height;
            if (area > bestArea) { bestArea = area; best = sr; }
        }

        if (!best) return false;

        if (!TryGetSpriteBands(best.sprite, out bands)) return false;

        // Respect any per-instance tint the renderer applies on top of the texture.
        var c = best.color;
        if (c != Color.white)
        {
            bands.dark = Multiply(bands.dark, c);
            bands.body = Multiply(bands.body, c);
            bands.light = Multiply(bands.light, c);
        }
        return true;
    }

    public static bool TryGetSpriteTint(Sprite sprite, out Color tint)
    {
        tint = Color.white;
        if (!TryGetSpriteBands(sprite, out TintBands bands)) return false;
        tint = bands.body;
        return true;
    }

    public static bool TryGetSpriteBands(Sprite sprite, out TintBands bands)
    {
        bands = TintBands.FromBody(Color.white);
        if (!sprite) return false;

        if (_cache.TryGetValue(sprite, out bands)) return true;

        if (!TrySample(sprite, out TintBands raw, out bool isSquare)) return false;

        bands = Register(raw, isSquare);
        _cache[sprite] = bands;
        return true;
    }

    private static Color Multiply(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, 1f);
    }

    /// <summary>
    /// Registers the canonical colours from every stack sprite currently in the scene,
    /// sampling the flat small squares FIRST so they win as the reference for their family.
    /// Optional - the palette also builds itself lazily - but calling this once at board
    /// setup means the very first clear of the session is already snapped correctly.
    /// </summary>
    public static void WarmUp()
    {
        var pieces = Object.FindObjectsByType<PieceSimple>(FindObjectsInactive.Include,
                                                           FindObjectsSortMode.None);

        var sprites = new List<Sprite>();
        for (int i = 0; i < pieces.Length; i++)
        {
            if (!pieces[i]) continue;
            var renderers = pieces[i].GetComponentsInChildren<SpriteRenderer>(true);
            for (int j = 0; j < renderers.Length; j++)
                if (renderers[j] && renderers[j].sprite && !sprites.Contains(renderers[j].sprite))
                    sprites.Add(renderers[j].sprite);
        }

        // Pass 1: the flat squares, so every family has its reference before anything snaps.
        // Pass 2: everything else, which now snaps onto those references.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                if (_cache.ContainsKey(sprite)) continue;
                if (!TrySample(sprite, out TintBands raw, out bool isSquare)) continue;
                if (isSquare != (pass == 0)) continue;

                _cache[sprite] = Register(raw, isSquare);
            }
        }
    }

    /// <summary>Drops the cache and the palette. For editor tooling after art changes.</summary>
    public static void Clear()
    {
        _cache.Clear();
        _palette.Clear();
    }

    // ------------------------------------------------------------------
    // Palette
    // ------------------------------------------------------------------

    /// <summary>
    /// Folds <paramref name="raw"/> into the palette and returns the canonical colour for
    /// its family, so every shape of one colour ends up bit-identical.
    /// </summary>
    private static TintBands Register(TintBands raw, bool isReference)
    {
        int index = FindNear(raw.body);

        if (index < 0)
        {
            _palette.Add(new Entry { bands = raw, isReference = isReference });
            return raw;
        }

        var entry = _palette[index];

        // A flat square outranks a colour that was only ever guessed off a bevelled shape.
        // Promote it and drop the cache, so anything already snapped to the old value
        // re-samples against the better reference.
        if (isReference && !entry.isReference)
        {
            entry.bands = raw;
            entry.isReference = true;
            _palette[index] = entry;
            _cache.Clear();
        }

        return entry.bands;
    }

    private static int FindNear(Color c)
    {
        for (int i = 0; i < _palette.Count; i++)
        {
            var p = _palette[i].bands.body;
            float dr = p.r - c.r, dg = p.g - c.g, db = p.b - c.b;
            if (dr * dr + dg * dg + db * db <= SnapDistance * SnapDistance) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------
    // Sampling
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads the sprite's pixels once and derives its body colour from them.
    /// <paramref name="isSquare"/> is true when the centre probe found one flat face,
    /// which is what makes this sprite usable as a canonical reference.
    /// </summary>
    private static bool TrySample(Sprite sprite, out TintBands bands, out bool isSquare)
    {
        bands = TintBands.FromBody(Color.white);
        isSquare = false;

        var source = sprite.texture;
        if (!source) return false;

        var rect = sprite.textureRect;
        int w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int h = Mathf.Max(1, Mathf.RoundToInt(rect.height));

        // Blit through a RenderTexture rather than reading the source directly, so this
        // works whether or not the texture has Read/Write enabled in its importer.
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0,
                                            RenderTextureFormat.ARGB32,
                                            RenderTextureReadWrite.sRGB);

        var previous = RenderTexture.active;
        Texture2D readable = null;

        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
            readable.Apply(false);

            var pixels = readable.GetPixels();

            // The three painted bands, from one pass over the histogram.
            bands = ExtractBands(pixels);

            // The small square's flat centre face, exactly as marked by hand, is the most
            // trustworthy read of the BODY colour - and its existence is what marks this
            // sprite as a canonical reference for its colour family.
            if (TryCentreColor(pixels, w, h, out Color centre))
            {
                bands.body = centre;
                isSquare = true;
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PieceTintSampler] Could not sample '" + sprite.name + "': " + e.Message);
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            if (readable)
            {
                // Object.Destroy throws in edit mode, and this runs from editor tooling too.
                if (Application.isPlaying) Object.Destroy(readable);
                else Object.DestroyImmediate(readable);
            }
        }
    }

    /// <summary>
    /// Averages the opaque pixels in a small box at the centre of the sprite, and reports
    /// failure if that box is not one flat colour - which is how a bevelled 3X shape is
    /// told apart from a small square.
    /// </summary>
    private static bool TryCentreColor(Color[] pixels, int w, int h, out Color tint)
    {
        tint = Color.white;

        int bw = Mathf.Max(1, Mathf.RoundToInt(w * CentreBoxFraction));
        int bh = Mathf.Max(1, Mathf.RoundToInt(h * CentreBoxFraction));
        int x0 = (w - bw) / 2;
        int y0 = (h - bh) / 2;

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int y = y0; y < y0 + bh; y++)
        for (int x = x0; x < x0 + bw; x++)
        {
            var p = pixels[y * w + x];
            if (p.a < 0.9f) continue;
            sum += new Vector3(p.r, p.g, p.b);
            count++;
        }

        // A mostly-transparent centre means the box missed the block entirely.
        if (count < (bw * bh) / 2) return false;

        var mean = sum / count;

        for (int y = y0; y < y0 + bh; y++)
        for (int x = x0; x < x0 + bw; x++)
        {
            var p = pixels[y * w + x];
            if (p.a < 0.9f) continue;
            if ((new Vector3(p.r, p.g, p.b) - mean).sqrMagnitude > UniformTolerance * UniformTolerance)
                return false;
        }

        tint = new Color(mean.x, mean.y, mean.z, 1f);
        return true;
    }

    /// <summary>
    /// Pulls the block's three painted bands out of the sprite: the body is the most common
    /// opaque colour, the highlight is the brightest significant band above it and the bevel
    /// is the darkest significant band below it. The outline and the antialiased rim are
    /// excluded, and each band is averaged within its bucket so the result is exact rather
    /// than quantised.
    /// </summary>
    private static TintBands ExtractBands(Color[] pixels)
    {
        var counts = new Dictionary<int, int>(256);
        var sums = new Dictionary<int, Vector3>(256);
        int opaque = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];

            if (p.a < 0.9f) continue;                       // soft rim
            float luma = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
            if (luma < 0.14f) continue;                     // black outline

            opaque++;

            int key = (Mathf.FloorToInt(p.r * (Levels - 1)) << 10)
                    | (Mathf.FloorToInt(p.g * (Levels - 1)) << 5)
                    |  Mathf.FloorToInt(p.b * (Levels - 1));

            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;

            sums.TryGetValue(key, out Vector3 s);
            sums[key] = s + new Vector3(p.r, p.g, p.b);
        }

        if (counts.Count == 0) return TintBands.FromBody(Color.white);

        int bodyKey = 0, bodyCount = -1;
        foreach (var kv in counts)
            if (kv.Value > bodyCount) { bodyCount = kv.Value; bodyKey = kv.Key; }

        Color body = Average(sums[bodyKey], bodyCount);
        float bodyLuma = Luma(body);

        // Only buckets big enough to be a painted band, not antialiasing between two.
        int minCount = Mathf.Max(1, Mathf.RoundToInt(opaque * MinBandShare));

        Color dark = body, light = body;
        float darkLuma = bodyLuma, lightLuma = bodyLuma;

        foreach (var kv in counts)
        {
            if (kv.Key == bodyKey || kv.Value < minCount) continue;

            Color c = Average(sums[kv.Key], kv.Value);
            float l = Luma(c);

            if (l < darkLuma) { darkLuma = l; dark = c; }
            if (l > lightLuma) { lightLuma = l; light = c; }
        }

        // A block with no separate bevel or highlight still needs a usable pair.
        var fallback = TintBands.FromBody(body);
        return new TintBands
        {
            dark = darkLuma < bodyLuma ? dark : fallback.dark,
            body = body,
            light = lightLuma > bodyLuma ? light : fallback.light
        };
    }

    private static Color Average(Vector3 sum, int count)
    {
        return new Color(sum.x / count, sum.y / count, sum.z / count, 1f);
    }

    private static float Luma(Color c)
    {
        return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
