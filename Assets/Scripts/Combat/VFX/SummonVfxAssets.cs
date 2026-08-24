using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The textures and material the ParticleSystem backend draws with, generated in
/// code and cached for the whole session.
///
/// WHY PROCEDURAL: the whole point of the ParticleSystem backend is that the
/// summon works the moment the scripts compile - no prefab to author, no art to
/// import, no VFX Graph to build. Requiring three hand-painted PNGs would put
/// the effect right back behind a pile of editor work. Assign real art on
/// <see cref="SummonEmitterParticles"/> later and these are ignored.
/// </summary>
public static class SummonVfxAssets
{
    private static Texture2D _glow;
    private static Texture2D _ring;
    private static Material _glowMat;
    private static Material _ringMat;
    private static Material _circleMat;
    private static Mesh _quad;

    /// <summary>Soft round falloff. The pillar, the trail and the flash all use it.</summary>
    public static Texture2D Glow
    {
        get
        {
            if (_glow == null) _glow = BuildRadial(128, 0f, 1f);
            return _glow;
        }
    }

    /// <summary>Thin bright annulus - the ring that expands out of the landing point.</summary>
    public static Texture2D Ring
    {
        get
        {
            if (_ring == null) _ring = BuildRadial(128, 0.78f, 0.14f);
            return _ring;
        }
    }

    /// <summary>
    /// Additive material for the pillar, trail and flash. Additive is what sells
    /// a light pillar - alpha blending over the bright green board reads as dirty
    /// yellow paint rather than light.
    /// </summary>
    public static Material GlowMaterial
    {
        get
        {
            if (_glowMat == null) _glowMat = BuildAdditive(Glow, "SummonGlowAdditive(gen)");
            return _glowMat;
        }
    }

    /// <summary>Same blend, ring texture - the shockwave that expands on landing.</summary>
    public static Material RingMaterial
    {
        get
        {
            if (_ringMat == null) _ringMat = BuildAdditive(Ring, "SummonRingAdditive(gen)");
            return _ringMat;
        }
    }

    /// <summary>
    /// Radial falloff baked into the alpha channel. <paramref name="innerRadius"/>
    /// 0 gives a filled glow; a non-zero value gives a ring whose thickness is
    /// <paramref name="falloff"/> (both normalised, 1 = texture edge).
    /// </summary>
    private static Texture2D BuildRadial(int size, float innerRadius, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = innerRadius > 0f ? "SummonRing(gen)" : "SummonGlow(gen)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        float half = size * 0.5f;
        var px = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Normalised distance from centre, 1 at the texture edge.
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (innerRadius > 0f)
                {
                    // Ring: peak at innerRadius, fading either side over falloff.
                    a = 1f - Mathf.Abs(d - innerRadius) / Mathf.Max(0.0001f, falloff);
                }
                else
                {
                    // Glow: solid centre easing to nothing at the edge. Squared so
                    // the core stays hot instead of washing out into a flat disc.
                    a = 1f - d / Mathf.Max(0.0001f, falloff);
                    if (a > 0f) a *= a;
                }

                a = Mathf.Clamp01(a);

                // Kill anything past the edge so the quad's corners never show.
                if (d > 1f) a = 0f;

                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }

    /// <summary>
    /// Builds the additive material from Blasty/SummonAdditive.
    ///
    /// DO NOT swap this back to "Universal Render Pipeline/Particles/Unlit".
    /// That shader's blend state is applied by URP's material EDITOR, not by
    /// writing _SrcBlend/_DstBlend, so a material built at runtime renders every
    /// particle as an opaque white square no matter what the texture's alpha
    /// says - verified in this project, not theory.
    /// </summary>
    private static Material BuildAdditive(Texture2D tex, string matName)
    {
        const string shaderName = "Blasty/SummonAdditive";

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            // Only reachable if the shader was stripped from the build. Sprites/Default
            // respects texture alpha, so the effect still reads - just alpha-blended
            // rather than additive, which looks flatter.
            shader = Shader.Find("Sprites/Default");
            Debug.LogWarning($"[SummonVfx] '{shaderName}' not found - falling back to " +
                             "Sprites/Default, which is not additive and will look flat. " +
                             "Add it to Project Settings > Graphics > Always Included Shaders.");
        }

        var mat = new Material(shader) { name = matName, hideFlags = HideFlags.HideAndDontSave };
        mat.SetTexture("_MainTex", tex);
        mat.renderQueue = (int)RenderQueue.Transparent;
        return mat;
    }

    /// <summary>
    /// Material for the pre-landing ground telegraph. Unlike the two above this
    /// one is TEXTURELESS - Blasty/SummonGroundCircle computes the annulus from
    /// UV, because the whole effect is its inner radius animating open and a
    /// texture can only bake one fixed inner radius.
    /// </summary>
    public static Material GroundCircleMaterial
    {
        get
        {
            if (_circleMat == null) _circleMat = BuildGroundCircle();
            return _circleMat;
        }
    }

    /// <summary>
    /// A 1x1 quad centred on the origin with UV 0..1, shared by every ground
    /// circle. Built by hand rather than via CreatePrimitive(Quad) so it carries
    /// no collider and no hidden asset dependency.
    /// </summary>
    public static Mesh UnitQuad
    {
        get
        {
            if (_quad != null) return _quad;

            _quad = new Mesh { name = "SummonUnitQuad(gen)", hideFlags = HideFlags.HideAndDontSave };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
            };
            _quad.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
            return _quad;
        }
    }

    private static Material BuildGroundCircle()
    {
        const string shaderName = "Blasty/SummonGroundCircle";

        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[SummonVfx] '{shaderName}' not found - the landing telegraph " +
                             "will not draw. Add it to Project Settings > Graphics > " +
                             "Always Included Shaders.");
            return null;
        }

        return new Material(shader)
        {
            name = "SummonGroundCircle(gen)",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent,
        };
    }
}
