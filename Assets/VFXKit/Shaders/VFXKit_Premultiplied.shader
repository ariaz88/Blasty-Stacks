// VFX Kit particle shader: occludes by alpha AND can glow past it - hot smoke, magic with a dark core.
// Plain unlit ShaderLab/CG with no LightMode tag, so it renders in the Built-in
// pipeline AND in URP (as SRPDefaultUnlit). Not HDRP.
// Reads the particle's vertex colour: RGB tints, alpha fades.
Shader "VFXKit/Premultiplied"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity (HDR, >1 feeds bloom)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One OneMinusSrcAlpha
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
                half a = t.a * i.color.a;
                return half4(t.rgb * i.color.rgb * _Intensity * a, a);
            }
            ENDCG
        }
    }
}
