// Hero-arrival capsule glow: while the pillar rises, its LOWER parts fade out first, so it
// never reads as one solid block being shifted up (Arash, 2026-10-05).
//
// ALPHA ONLY. Output colour = the sprite's colour x the SpriteRenderer colour, untouched;
// alpha is the sprite's own alpha x a 0..1 mask - never above it. Arash's rule: never
// tint, whiten or recolour his layers.
//
// Driven per frame by GateArrivalCapsule (MaterialPropertyBlock):
//   _Cut        height (sprite uv.y, 0 = bottom, 1 = top) the fade has eaten up to.
//               Below it the glow is gone; above it fades back in over _Soft. Starts
//               below 0 (nothing faded) and climbs past 1 as the pillar rises.
//   _FlameTime  seconds + a per-capsule seed: the cut's edge wobbles with noise that
//               streams up, so the lower part breaks up like a flame's base instead of a
//               straight line.
// Plain unlit CG with no LightMode tag: renders in URP (SRPDefaultUnlit), like VFXKit's.
Shader "Blasty/CapsuleRiseFade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Cut ("Fade line from the bottom (uv.y)", Float) = -1
        _FlameTime ("Flame time (s)", Float) = 0
        _Soft ("Fade softness (uv.y)", Range(0.02, 1)) = 0.45
        _Wobble ("Edge wobble (uv.y)", Range(0, 0.4)) = 0.12
        _WobbleScale ("Wobble noise scale (x, y)", Vector) = (3, 2.5, 0, 0)
        _WobbleSpeed ("Wobble climb speed (uv/s)", Float) = 2.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Cut, _FlameTime, _Soft, _Wobble, _WobbleSpeed;
            float4 _WobbleScale;

            struct appdata { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float hash (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv) * i.color;
                float v = i.uv.y;
                float n = vnoise(float2(i.uv.x * _WobbleScale.x, v * _WobbleScale.y - _FlameTime * _WobbleSpeed)) - 0.5;
                float edge = _Cut + n * _Wobble * 2;
                float mask = smoothstep(edge, edge + _Soft, v);   // 0 below the line, 1 well above it
                return half4(tex.rgb, tex.a * mask);
            }
            ENDCG
        }
    }
}
