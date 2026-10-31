Shader "Orbit/Booster Lines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Main Color", Color) = (1, 1, 1, 1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _LineSpacing ("Line Spacing (World Units)", Float) = 0.6
        _LineWidth ("Line Width (World Units)", Float) = 0.15
        _LineSpeed ("Line Speed (World Units / Second)", Float) = 0.5
        _IdleOpacity ("Idle Line Opacity", Range(0, 1)) = 0.25
        _HeldOpacity ("Held Line Opacity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "CanUseSpriteAtlas" = "False" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _LineColor;
            float _LineSpacing, _LineWidth, _LineSpeed;
            float _IdleOpacity, _HeldOpacity;
            // This is shared by all level objects and set by Player, not a material property.
            float _LevelInteractionHeld;

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct FragmentInput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            FragmentInput Vertex(VertexInput input)
            {
                FragmentInput output;
                output.position = UnityObjectToClipPos(input.position);
                output.worldPosition = mul(unity_ObjectToWorld, input.position).xyz;
                output.uv = input.uv;
                return output;
            }

            fixed4 Fragment(FragmentInput input) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                fixed4 baseColor = sprite * _Color;

                // Recover the world distance represented by one UV unit along the
                // sprite's up axis. Unlike an object matrix, these interpolated
                // derivatives also work when Unity batches several sprites together.
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float determinant = uvDx.y * uvDy.x - uvDy.y * uvDx.x;
                float3 worldY = ddx(input.worldPosition) * uvDy.x - ddy(input.worldPosition) * uvDx.x;
                float worldUnitsPerUvY = length(worldY) / max(abs(determinant), 1e-12);
                float spacing = max(_LineSpacing, 0.0001);
                float halfWidth = max(_LineWidth * 0.5, 0.00001);
                float distanceAlongUp = input.uv.y * worldUnitsPerUvY;
                float phase = (distanceAlongUp - _Time.y * _LineSpeed) / spacing;
                float distanceFromLine = abs(frac(phase + 0.5) - 0.5) * spacing;
                float profile = 1.0 - smoothstep(0.0, halfWidth, distanceFromLine);
                float lineAlpha = profile * lerp(_IdleOpacity, _HeldOpacity, saturate(_LevelInteractionHeld));
                lineAlpha *= _LineColor.a * sprite.a;

                // Composite the line over the existing translucent sprite, then
                // return straight alpha for the normal sprite blending mode.
                float alpha = lineAlpha + baseColor.a * (1.0 - lineAlpha);
                float3 rgb = (_LineColor.rgb * lineAlpha + baseColor.rgb * baseColor.a * (1.0 - lineAlpha)) / max(alpha, 0.00001);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
