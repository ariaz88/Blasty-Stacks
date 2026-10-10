Shader "Blasty/ShardUnlit"
{
    // Unlit shard shader for the match-clear burst.
    //
    // The goal is that a shard reads as a CHIP OFF THE 2D BLOCK, not as a lit 3D rock.
    // The block sprites are flat-shaded with exactly three painted colours - a dark bevel,
    // a body top face and a highlight - so this shader paints the shard facets with those
    // same three colours, in flat bands, and does no lighting maths on the colour at all.
    //
    // The previous version faked a light with shade = lerp(_Ambient, 1.0, ndl). That could
    // never exceed 1.0, so every shard was darker than the block: measured mean shade 0.646,
    // and 25% of the visible facet area sat pinned at the 0.42 ambient floor. Yellow #F2C14E
    // came out as #C89E3E dust. Do not reintroduce a multiply-by-shade term here.
    //
    // The three colours arrive as material properties rather than through the particle COLOR
    // stream, because the stream carries only one colour per particle and we need three.
    // ShardBurst gives each pooled system its own material instance for this - which is also
    // why MaterialPropertyBlock is NOT used: these properties live in UnityPerMaterial, and
    // the SRP Batcher ignores per-renderer overrides of those.
    //
    // The COLOR stream is still read, for ALPHA only: that is the burst's fade-out ramp.
    //
    // The pass deliberately carries NO LightMode tag. Unity treats an untagged pass
    // as "SRPDefaultUnlit", which is one of the two tags Render2DLightingPass collects.
    Properties
    {
        _ColDark  ("Bevel Colour",     Color)     = (0.06, 0.28, 0.49, 1)
        _ColBody  ("Body Colour",      Color)     = (0.18, 0.49, 0.78, 1)
        _ColLight ("Highlight Colour", Color)     = (0.58, 0.79, 1.00, 1)

        // Facet-angle cuts between the three bands, fitted to the actual shard meshes by
        // measuring ndl over their screen-projected facet area. These give an 18% bevel /
        // 64% body / 18% highlight split, close to the block's own 14 / 62 / 8.
        _DarkCut  ("Bevel Cut",        Range(0,1)) = 0.10
        _LightCut ("Highlight Cut",    Range(0,1)) = 0.86

        // Points mostly AT THE VIEWER (-Z), not up-and-away like a real key light would.
        // That is deliberate: with backface culling the visible facets are the ones facing
        // the camera, so a steep light leaves most of them at ndl = 0. The old
        // (-0.35, 0.85, -0.40) put 31% of the visible area at ndl = 0 - that flat mass of
        // bevel-colour is what made the cloud read as dull mud. This direction cuts it to
        // 13% while keeping enough +Y that the highlight still favours upward faces.
        _LightDir ("Facet Reference Dir", Vector) = (-0.30, 0.60, -0.74, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
        }

        Pass
        {
            Name "ShardUnlit"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float3 normalWS   : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColDark;
                float4 _ColBody;
                float4 _ColLight;
                float  _DarkCut;
                float  _LightCut;
                float4 _LightDir;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Mesh particles bake each particle's rotation into the vertex stream,
                // so this normal is already per-shard.
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 n   = normalize(IN.normalWS);
                float  ndl = saturate(dot(n, normalize(_LightDir.xyz)));

                // Three FLAT bands. No gradient between them on purpose: the blocks are
                // flat-shaded, and any smooth ramp immediately reads as a different,
                // shinier material sitting in a 2D scene.
                half3 c = _ColBody.rgb;
                c = (ndl < _DarkCut)  ? _ColDark.rgb  : c;
                c = (ndl > _LightCut) ? _ColLight.rgb : c;

                // Alpha only from the particle stream - the burst's fade-out.
                return half4(c, IN.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
