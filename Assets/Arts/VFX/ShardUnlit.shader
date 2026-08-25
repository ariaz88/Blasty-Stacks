Shader "Blasty/ShardUnlit"
{
    // Unlit shard shader for the match-clear burst.
    //
    // Two reasons this is a custom shader instead of URP/Particles/Unlit:
    //  1. The gameplay scenes contain no Light or Light2D, and the URP 2D Renderer
    //     never draws a UniversalForward pass, so a Lit material renders nothing.
    //     The facet contrast is therefore faked here against a fixed light vector.
    //  2. Per-burst colour arrives through the particle COLOR stream, so one shared
    //     material serves every block colour with no material instancing.
    //
    // The pass deliberately carries NO LightMode tag. Unity treats an untagged pass
    // as "SRPDefaultUnlit", which is one of the two tags Render2DLightingPass collects.
    Properties
    {
        _Ambient  ("Ambient Floor",   Range(0,1)) = 0.42
        _LightDir ("Fake Light Dir",  Vector)     = (-0.35, 0.85, -0.40, 0)
        _TopBoost ("Top Facet Boost", Range(0,1)) = 0.22
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
                float  _Ambient;
                float4 _LightDir;
                float  _TopBoost;
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
                float3 n     = normalize(IN.normalWS);
                float  ndl   = saturate(dot(n, normalize(_LightDir.xyz)));
                float  shade = lerp(_Ambient, 1.0, ndl);

                // A little extra on up-facing facets so the cloud catches a highlight.
                shade += _TopBoost * pow(saturate(n.y), 4.0);

                return half4(IN.color.rgb * shade, IN.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
