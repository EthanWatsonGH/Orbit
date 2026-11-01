Shader "Orbit/Scale Snap Highlights"
{
    Properties
    {
        _CurrentColor ("Current Point Color", Color) = (1, 1, 1, 1)
        _CandidateColor ("Valid Point Color", Color) = (0.1, 0.7, 1, 0.8)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 0.8)
        _CurrentRadiusPixels ("Current Point Radius (pixels)", Float) = 6
        _CandidateRadiusPixels ("Valid Point Radius (pixels)", Float) = 3
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            fixed4 _CurrentColor, _CandidateColor, _OutlineColor;
            float _CurrentRadiusPixels, _CandidateRadiusPixels, _Spacing;
            float4 _PathOrigin, _PathDirection, _CurrentPoint;
            float _PathMinimum, _PathMaximum;

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
                float2 origin = _PathOrigin.xy;
                float2 direction = _PathDirection.xy;
                float spacing = max(_Spacing, 0.000001);
                float worldPerPixel = max(max(length(ddx(input.world)), length(ddy(input.world))), 0.000001);
                float alongPath = dot(input.world - origin, direction);
                float2 projected = origin + direction * alongPath;
                float2 safeDirection = float2(
                    abs(direction.x) < 0.000001 ? 1.0 : direction.x,
                    abs(direction.y) < 0.000001 ? 1.0 : direction.y);
                float2 firstIndex = (origin + direction * _PathMinimum) / spacing;
                float2 lastIndex = (origin + direction * _PathMaximum) / spacing;
                float2 lowest = ceil(min(firstIndex, lastIndex) - 0.000001);
                float2 highest = floor(max(firstIndex, lastIndex) + 0.000001);
                float2 nearest = floor(projected / spacing + 0.5);
                float2 gridCoordinates = clamp(nearest, lowest, highest) * spacing;
                float2 crossing = (gridCoordinates - origin) / safeDirection;
                float2 valid = step(lowest, highest) *
                               step(float2(_PathMinimum, _PathMinimum), crossing) *
                               step(crossing, float2(_PathMaximum, _PathMaximum)) *
                               step(float2(0.000001, 0.000001), abs(direction));

                float xDistance = length(input.world - (origin + direction * crossing.x)) / worldPerPixel;
                float yDistance = length(input.world - (origin + direction * crossing.y)) / worldPerPixel;
                float candidateDistance = min(valid.x > 0.5 ? xDistance : 1000000.0,
                                              valid.y > 0.5 ? yDistance : 1000000.0);
                float currentDistance = length(input.world - _CurrentPoint.xy) / worldPerPixel;

                // Keep the current point visible even when dense candidate dots fade out.
                float lineSpacingPixels = spacing / max(max(abs(direction.x), abs(direction.y)) * worldPerPixel, 0.000001);
                float candidateFade = smoothstep(6.0, 14.0, lineSpacingPixels);
                float candidateCoverage = saturate(_CandidateRadiusPixels + 0.5 - candidateDistance) * candidateFade;
                float currentCoverage = saturate(_CurrentRadiusPixels + 0.5 - currentDistance);
                float candidateCore = saturate(_CandidateRadiusPixels - 1.0 - candidateDistance);
                float currentCore = saturate(_CurrentRadiusPixels - 1.0 - currentDistance);

                fixed4 candidate = lerp(_OutlineColor, _CandidateColor, candidateCore);
                candidate.a *= candidateCoverage;
                fixed4 current = lerp(_OutlineColor, _CurrentColor, currentCore);
                current.a *= currentCoverage;
                float alpha = current.a + candidate.a * (1.0 - current.a);
                float3 rgb = (current.rgb * current.a + candidate.rgb * candidate.a * (1.0 - current.a)) / max(alpha, 0.00001);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
