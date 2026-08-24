// Additive unlit shader for the summon arrival VFX.
//
// WHY A CUSTOM SHADER: configuring "Universal Render Pipeline/Particles/Unlit"
// from script does not work. Its blend state is applied by URP's material
// editor, not by writing _SrcBlend/_DstBlend, so a material built at runtime
// renders every particle as an OPAQUE WHITE QUAD regardless of the texture's
// alpha. On top of that, a shader that is only ever reached through
// Shader.Find can be stripped from a mobile build. Twenty lines of ShaderLab
// here removes both problems and pins the look on every platform.
//
// NOTE: this shader must stay listed in Project Settings > Graphics >
// Always Included Shaders, because the material is created at runtime by
// SummonVfxAssets and nothing in a scene references it.
Shader "Blasty/SummonAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"       = "Transparent"
            "Queue"            = "Transparent"
            "RenderPipeline"   = "UniversalPipeline"
            "IgnoreProjector"  = "True"
            "PreviewType"      = "Plane"
        }

        // Classic additive: the texture's alpha ramp decides how much light each
        // texel adds, so overlapping particles build to a white-hot core exactly
        // the way the reference clip's pillar does.
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
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                // Vertex colour carries the particle's startColor combined with
                // its colorOverLifetime, which is how the tint and the fade
                // reach this shader at all.
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Returned straight: "Blend SrcAlpha One" above does the
                // premultiply. Doing it here as well would square the alpha and
                // crush the soft edges into a hard dot.
                return tex * IN.color * _TintColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
