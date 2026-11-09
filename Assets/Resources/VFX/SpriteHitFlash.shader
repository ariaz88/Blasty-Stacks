Shader "BlastyStacks/SpriteHitFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Hit Color", Color) = (1,1,1,1)
        _FlashAmount ("Hit Amount", Range(0,1)) = 0
        _HitPivot ("World Pivot", Vector) = (0,0,0,0)
        _HitScale ("Visual Scale", Vector) = (1,1,0,0)
        _HitOffset ("Visual Offset", Vector) = (0,0,0,0)
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex HitVert
            #pragma fragment HitFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _FlashColor;
            float _FlashAmount;
            float4 _HitPivot, _HitScale, _HitOffset;

            v2f HitVert(appdata_t input)
            {
                v2f output = SpriteVert(input);
                // Deform the image in world XY, keeping physics and all transforms intact.
                float4 local = UnityFlipSprite(input.vertex, _Flip);
                float4 world = mul(unity_ObjectToWorld, local);
                world.xy = _HitPivot.xy + (world.xy - _HitPivot.xy) * _HitScale.xy + _HitOffset.xy;
                output.vertex = mul(UNITY_MATRIX_VP, world);
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            fixed4 HitFrag(v2f input) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(input.texcoord) * input.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, saturate(_FlashAmount));
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
