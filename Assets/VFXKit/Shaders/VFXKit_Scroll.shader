// VFX Kit particle shader: two scrolling noise layers inside a mask - energy, portals, auras, beams.
// Plain unlit ShaderLab/CG with no LightMode tag, so it renders in the Built-in
// pipeline AND in URP (as SRPDefaultUnlit). Not HDRP.
// Reads the particle's vertex colour: RGB tints, alpha fades.
Shader "VFXKit/Scroll"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity (HDR, >1 feeds bloom)", Float) = 1
        _NoiseTex ("Noise (tileable)", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 1
        _SpeedA ("Scroll A (uv/s)", Vector) = (0, -0.6, 0, 0)
        _SpeedB ("Scroll B (uv/s)", Vector) = (0.15, -1.1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Intensity;
            sampler2D _NoiseTex;
            half _NoiseScale;
            float4 _SpeedA;
            float4 _SpeedB;

            struct appdata { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                // Two layers at different scales and speeds multiply into a pattern
                // that never visibly repeats; the texture's alpha keeps it in shape.
                half n1 = tex2D(_NoiseTex, i.uv * _NoiseScale + _Time.y * _SpeedA.xy).r;
                half n2 = tex2D(_NoiseTex, i.uv * _NoiseScale * 1.7 + _Time.y * _SpeedB.xy).r;
                half v = saturate(n1 * n2 * 2.2) * t.a * i.color.a;
                return half4(t.rgb * i.color.rgb * _Intensity * v, 0);
            }
            ENDCG
        }
    }
}
