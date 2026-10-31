Shader "Orbit/Selection Outline"
{
    Properties
    {
        _InnerColor ("Edge Color", Color) = (1, 1, 1, 1)
        _OuterColor ("Outer Color", Color) = (0, 0.4, 1, 0)
        _WidthPixels ("Width (pixels)", Float) = 8
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            float4 _InnerColor;
            float4 _OuterColor;
            float _WidthPixels;
            float2 _HalfSizePixels;
            float _IsCircle;

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct FragmentInput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            FragmentInput Vertex(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            fixed4 Fragment(FragmentInput input) : SV_Target
            {
                float width = max(_WidthPixels, 0.1);
                float2 halfSize = max(_HalfSizePixels, float2(0.001, 0.001));
                float2 positionPixels = (input.uv - 0.5) * (2.0 * (halfSize + width + 2.0));
                float distancePixels;
                if (_IsCircle > 0.5)
                {
                    // Convert the ellipse's normalized distance into screen pixels.
                    float distanceToEllipse = length(positionPixels / halfSize) - 1.0;
                    float pixelGradient = max(length(float2(ddx(distanceToEllipse), ddy(distanceToEllipse))), 0.00001);
                    distancePixels = distanceToEllipse / pixelGradient;
                }
                else
                {
                    float2 outside = abs(positionPixels) - halfSize;
                    distancePixels = length(max(outside, 0.0)) + min(max(outside.x, outside.y), 0.0);
                }

                float fade = saturate(max(distancePixels, 0.0) / width);
                fixed4 color = lerp(_InnerColor, _OuterColor, fade);
                color.a *= saturate(distancePixels + 1.0);
                return color;
            }
            ENDCG
        }
    }
}
