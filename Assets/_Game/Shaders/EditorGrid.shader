Shader "Orbit/Editor Grid"
{
    Properties
    {
        _Color ("Grid Color", Color) = (1, 1, 1, 0.25)
        _WidthPixels ("Line Width (pixels)", Float) = 1
    }
    SubShader
    {
        // Draw before the scene's transparent sprites, regardless of their sort order.
        Tags { "Queue" = "Background+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            fixed4 _Color;
            float _WidthPixels;
            float _Spacing;
            float4 _GridOrigin;
            float4 _GridRight;
            float4 _GridUp;

            struct VertexInput { float4 position : POSITION; };
            struct FragmentInput { float4 position : SV_POSITION; float2 world : TEXCOORD0; };

            FragmentInput Vertex(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.position);
                output.world = mul(unity_ObjectToWorld, input.position).xy;
                return output;
            }

            fixed4 Fragment(FragmentInput input) : SV_Target
            {
                float2 delta = input.world - _GridOrigin.xy;
                float2 grid = float2(dot(delta, _GridRight.xy), dot(delta, _GridUp.xy)) / max(_Spacing, 0.000001);
                float2 changePerPixel = max(fwidth(grid), float2(0.000001, 0.000001));
                float2 distancePixels = abs(frac(grid + 0.5) - 0.5) / changePerPixel;
                float distanceToLine = min(distancePixels.x, distancePixels.y);
                float coverage = 1.0 - smoothstep(_WidthPixels * 0.5, _WidthPixels * 0.5 + 1.0, distanceToLine);
                float pixelsPerCell = 1.0 / max(changePerPixel.x, changePerPixel.y);
                fixed4 color = _Color;
                color.a *= coverage * smoothstep(4.0, 10.0, pixelsPerCell);
                return color;
            }
            ENDCG
        }
    }
}
