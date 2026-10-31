Shader "Orbit/Circle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite geometry", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _Flip ("Flip", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex SpriteVert
            #pragma fragment CircleFragment
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            fixed4 CircleFragment(v2f input) : SV_Target
            {
                // Full-rect sprites provide UVs from 0 to 1. The shape itself is
                // calculated here, without sampling the sprite's image.
                float distanceFromCenter = length(input.texcoord - float2(0.5, 0.5));
                float edgeWidth = max(fwidth(distanceFromCenter), 0.00001);
                float coverage = saturate((0.5 - distanceFromCenter) / edgeWidth + 0.5);
                fixed4 color = input.color;
                color.a *= coverage;
                return color;
            }
            ENDCG
        }
    }
}
