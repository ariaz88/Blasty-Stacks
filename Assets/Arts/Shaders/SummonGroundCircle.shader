// Ground telegraph for the summon arrival: a filled white disc that pops in
// just BEFORE the unit lands, then hollows out from the centre into a ring and
// fades. Matches the reference clip frames f16-f25 (30fps).
//
// WHY PROCEDURAL, NOT A TEXTURE: the whole effect IS the inner radius
// animating from 0 (filled disc) outward to ~0.8 (thin ring). A texture bakes
// one fixed inner radius, so reproducing this with textures would need a
// flipbook. Two lines of smoothstep here do it exactly, at any resolution.
//
// Drawn by SummonGroundCircle.cs, which animates _InnerRadius and _Alpha
// through a MaterialPropertyBlock.
//
// NOTE: reached only via Shader.Find on a runtime-created material, so it MUST
// stay in Project Settings > Graphics > Always Included Shaders or it strips
// out of a mobile build.
Shader "Blasty/SummonGroundCircle"
{
    Properties
    {
        [HDR] _Color      ("Colour", Color)          = (1, 1, 1, 1)
        _InnerRadius      ("Inner Radius", Range(0, 1)) = 0
        _OuterRadius      ("Outer Radius", Range(0, 1)) = 0.9
        _Edge             ("Edge Softness", Range(0.001, 0.5)) = 0.12
        _Alpha            ("Alpha", Range(0, 1))      = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        // Additive, same as the rest of the summon: the reference disc reads as
        // saturated white light on the green board, not as white paint.
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _InnerRadius;
                float _OuterRadius;
                float _Edge;
                float _Alpha;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // UV 0..1 -> -1..1, so d is the normalised distance from centre
                // and the quad's inscribed circle reaches d = 1 at the edge.
                float2 p = IN.uv * 2.0 - 1.0;
                float  d = length(p);

                // Outer edge: opaque inside _OuterRadius, easing out over _Edge.
                float outer = 1.0 - smoothstep(_OuterRadius - _Edge, _OuterRadius, d);

                // Inner hole. At _InnerRadius = 0 this evaluates to 1 across the
                // whole quad, which is what gives the FILLED disc for free -
                // there is no separate "disc mode" branch.
                float inner = smoothstep(_InnerRadius - _Edge, _InnerRadius, d);

                float a = saturate(outer * inner) * _Alpha;

                half4 c = _Color;
                c.a *= a;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
