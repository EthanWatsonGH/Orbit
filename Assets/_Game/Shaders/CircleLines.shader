Shader "Orbit/Circle Lines"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite geometry", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _LineSpacing ("Line Spacing (World Units)", Float) = 0.6
        _LineWidth ("Line Width (World Units)", Float) = 0.15
        _LineSpeed ("Line Speed (World Units / Second; Negative Is Inward)", Float) = 0.5
        _IdleOpacity ("Idle Line Opacity", Range(0, 1)) = 0.25
        _HeldOpacity ("Held Line Opacity", Range(0, 1)) = 0.5
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
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"

            fixed4 _LineColor;
            float _LineSpacing, _LineWidth, _LineSpeed;
            float _IdleOpacity, _HeldOpacity;
            // Shared by the player and all level-object effect shaders.
            float _LevelInteractionHeld;

            struct FragmentInput
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                float spriteOpacity : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FragmentInput Vertex(appdata_t input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                v2f sprite = SpriteVert(input);
                FragmentInput output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = sprite.vertex;
                output.color = sprite.color;
                output.uv = sprite.texcoord;
                output.worldPosition = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex, _Flip)).xyz;
                output.spriteOpacity = input.color.a * _RendererColor.a;
                return output;
            }

            fixed4 Fragment(FragmentInput input) : SV_Target
            {
                float2 centeredUv = input.uv - float2(0.5, 0.5);
                float distanceInUv = length(centeredUv);
                float edgeWidth = max(fwidth(distanceInUv), 0.00001);
                float coverage = saturate((0.5 - distanceInUv) / edgeWidth + 0.5);

                // Reconstruct the two UV axes in world units from interpolated
                // derivatives, including when Unity batches several sprites.
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float3 worldDx = ddx(input.worldPosition);
                float3 worldDy = ddy(input.worldPosition);
                float determinant = uvDx.x * uvDy.y - uvDy.x * uvDx.y;
                float safeDeterminant = determinant < 0.0 ? min(determinant, -1e-12) : max(determinant, 1e-12);
                float3 worldUvX = (worldDx * uvDy.y - worldDy * uvDx.y) / safeDeterminant;
                float3 worldUvY = (worldDy * uvDx.x - worldDx * uvDy.x) / safeDeterminant;
                float distanceFromCenter = length(centeredUv.x * worldUvX + centeredUv.y * worldUvY);

                float spacing = max(_LineSpacing, 0.0001);
                float halfWidth = max(_LineWidth * 0.5, 0.00001);
                float phase = (distanceFromCenter - _Time.y * _LineSpeed) / spacing;
                float distanceFromLine = abs(frac(phase + 0.5) - 0.5) * spacing;
                float profile = 1.0 - smoothstep(0.0, halfWidth, distanceFromLine);
                float lineAlpha = profile * lerp(_IdleOpacity, _HeldOpacity, saturate(_LevelInteractionHeld));
                lineAlpha *= _LineColor.a * input.spriteOpacity;

                // Composite the line over the existing translucent circle before
                // applying the antialiased edge to both layers together.
                float alpha = lineAlpha + input.color.a * (1.0 - lineAlpha);
                float3 rgb = (_LineColor.rgb * lineAlpha + input.color.rgb * input.color.a * (1.0 - lineAlpha)) / max(alpha, 0.00001);
                return fixed4(rgb, alpha * coverage);
            }
            ENDCG
        }
    }
}
