Shader "Orbit/Editor Guide"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _WidthPixels ("Width (pixels)", Float) = 6
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

            fixed4 _Color;
            float _WidthPixels;

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
                float distancePixels = abs(input.uv.y - 0.5) * (width + 2.0);
                fixed4 color = _Color;
                color.a *= saturate(width * 0.5 + 0.5 - distancePixels);
                return color;
            }
            ENDCG
        }
    }
}
