Shader "Orbit/Redirector Triangles"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite geometry", 2D) = "white" {}
        _Color ("Background Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [PerRendererData] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        _TriangleColor ("Triangle Color", Color) = (1, 1, 1, 1)
        _CellSize ("Cell Size (World Units)", Float) = 0.6
        _IdleOpacity ("Idle Triangle Opacity", Range(0, 1)) = 0.1
        _HeldOpacity ("Held Triangle Opacity", Range(0, 1)) = 0.3
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

            fixed4 _TriangleColor;
            float _CellSize, _IdleOpacity, _HeldOpacity;
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
                // Recover the dimensions of a UV unit in world space, even when
                // Unity batches sprites with different transforms together.
                float2 uvDx = ddx(input.uv);
                float2 uvDy = ddy(input.uv);
                float3 worldDx = ddx(input.worldPosition);
                float3 worldDy = ddy(input.worldPosition);
                float determinant = uvDx.x * uvDy.y - uvDy.x * uvDx.y;
                float safeDeterminant = determinant < 0.0 ? min(determinant, -1e-12) : max(determinant, 1e-12);
                float3 worldUvX = (worldDx * uvDy.y - worldDy * uvDx.y) / safeDeterminant;
                float3 worldUvY = (worldDy * uvDx.x - worldDx * uvDy.x) / safeDeterminant;
                float2 worldDimensions = float2(length(worldUvX), length(worldUvY));
                float2 grid = (input.uv - 0.5) * worldDimensions / max(_CellSize, 0.0001);
                float2 cell = floor(grid);
                float2 insideCell = frac(grid);
                float oddCell = frac((cell.x + cell.y) * 0.5) * 2.0;

                // One filled, upward-pointing triangle in each odd checkerboard cell.
                float distanceToTriangle = max(0.15 - insideCell.y,
                    max(insideCell.y - 0.85, abs(insideCell.x - 0.5) - (0.85 - insideCell.y) * 0.6));
                float edgeWidth = max(fwidth(distanceToTriangle), 0.0001);
                float triangleCoverage = (1.0 - smoothstep(-edgeWidth, edgeWidth, distanceToTriangle)) * oddCell;
                float triangleAlpha = triangleCoverage * lerp(_IdleOpacity, _HeldOpacity, saturate(_LevelInteractionHeld));
                triangleAlpha *= _TriangleColor.a * input.spriteOpacity;

                float alpha = triangleAlpha + input.color.a * (1.0 - triangleAlpha);
                float3 rgb = (_TriangleColor.rgb * triangleAlpha + input.color.rgb * input.color.a * (1.0 - triangleAlpha)) / max(alpha, 0.00001);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
