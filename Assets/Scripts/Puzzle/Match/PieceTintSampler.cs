using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads the body colour straight off a stack's own sprite, so the shatter shards match
/// the block exactly instead of tracking a hand-maintained colour table that drifts.
///
/// The stack sprites are tinted white (m_Color 1,1,1,1) — all colour lives in the texture —
/// and each is a light top face, a mid bevel band, a dark base and a black outline. The top
/// face is the largest flat region, so the most common opaque colour is the body colour.
///
/// Results are cached per Sprite: the read-back happens once per sprite for the whole session.
/// </summary>
public static class PieceTintSampler
{
    private static readonly Dictionary<Sprite, Color> _cache = new();

    // Quantisation grid for the histogram. 32 levels per channel is coarse enough that
    // a gradient still lands in one bucket, fine enough to separate the block's bands.
    private const int Levels = 32;

    /// <summary>
    /// Finds the largest sprite under <paramref name="root"/> and returns its body colour.
    /// Returns false if there is no sprite to sample.
    /// </summary>
    public static bool TryGetTint(GameObject root, out Color tint)
    {
        tint = Color.white;
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

        if (!TryGetSpriteTint(best.sprite, out tint)) return false;

        // Respect any per-instance tint the renderer applies on top of the texture.
        var c = best.color;
        tint = new Color(tint.r * c.r, tint.g * c.g, tint.b * c.b, 1f);
        return true;
    }

    public static bool TryGetSpriteTint(Sprite sprite, out Color tint)
    {
        tint = Color.white;
        if (!sprite) return false;

        if (_cache.TryGetValue(sprite, out tint)) return true;

        if (!TrySample(sprite, out tint)) return false;

        _cache[sprite] = tint;
        return true;
    }

    private static bool TrySample(Sprite sprite, out Color tint)
    {
        tint = Color.white;

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

            tint = DominantColor(readable.GetPixels());
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
    /// Most common opaque colour, ignoring the outline and the antialiased rim, then averaged
    /// within the winning bucket so the result is exact rather than quantised.
    /// </summary>
    private static Color DominantColor(Color[] pixels)
    {
        var counts = new Dictionary<int, int>(256);
        var sums = new Dictionary<int, Vector3>(256);

        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];

            if (p.a < 0.9f) continue;                       // soft rim
            float luma = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
            if (luma < 0.14f) continue;                     // black outline

            int key = (Mathf.FloorToInt(p.r * (Levels - 1)) << 10)
                    | (Mathf.FloorToInt(p.g * (Levels - 1)) << 5)
                    |  Mathf.FloorToInt(p.b * (Levels - 1));

            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;

            sums.TryGetValue(key, out Vector3 s);
            sums[key] = s + new Vector3(p.r, p.g, p.b);
        }

        if (counts.Count == 0) return Color.white;

        int bestKey = 0, bestCount = -1;
        foreach (var kv in counts)
            if (kv.Value > bestCount) { bestCount = kv.Value; bestKey = kv.Key; }

        var sum = sums[bestKey];
        return new Color(sum.x / bestCount, sum.y / bestCount, sum.z / bestCount, 1f);
    }
}
