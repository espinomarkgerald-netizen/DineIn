Shader "Dine In/World Outline"
{
    Properties { _Color("Outline color", Color) = (0,0,0,1) _Width("Width at 1080p", Range(0,3)) = .9 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        float _Width;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
        Varyings MaskVertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }
        Varyings EdgeVertex(Attributes input)
        {
            Varyings output = MaskVertex(input);
            float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
            float2 direction = mul((float3x3)UNITY_MATRIX_P, normalVS).xy;
            direction /= max(length(direction), .0001);
            float pixels = _Width * _ScaledScreenParams.y / 1080.0;
            output.positionCS.xy += direction * (2.0 * pixels / _ScaledScreenParams.xy) * output.positionCS.w;
            return output;
        }
        half4 Fragment(Varyings input) : SV_Target { return _Color; }
        ENDHLSL
        Pass
        {
            Name "Opaque edge"
            Cull Front ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex EdgeVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "Transparent silhouette mask"
            Cull Off ZWrite Off ZTest LEqual ColorMask 0
            Stencil { Ref 8 ReadMask 8 WriteMask 8 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "Transparent edge"
            Cull Front ZWrite Off ZTest LEqual
            Stencil { Ref 8 ReadMask 8 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma vertex EdgeVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "World screen edges"
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex ScreenVertex
            #pragma fragment ScreenFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            struct ScreenVaryings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            ScreenVaryings ScreenVertex(uint id : SV_VertexID)
            {
                ScreenVaryings o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = GetFullScreenTriangleVertexPosition(id);
                return o;
            }
            float EyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (unity_OrthoParams.w > .5)
                {
                    #if UNITY_REVERSED_Z
                    raw = 1 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }
            half4 ScreenFragment(ScreenVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float depth = EyeDepth(uv);
                float3 normal = SampleSceneNormals(uv);
                float2 stepUV = max(.65, _Width * _ScaledScreenParams.y / 1080.0) / _ScaledScreenParams.xy;
                float edge = 0;
                const float2 directions[4] = { float2(1,0), float2(-1,0), float2(0,1), float2(0,-1) };
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 adjacent = saturate(uv + directions[i] * stepUV);
                    float otherDepth = EyeDepth(adjacent);
                    float threshold = max(.025, min(depth, otherDepth) * .012);
                    edge = max(edge, smoothstep(threshold, threshold * 2, abs(depth - otherDepth)));
                    float3 otherNormal = SampleSceneNormals(adjacent);
                    if (dot(normal, normal) > .5 && dot(otherNormal, otherNormal) > .5)
                        edge = max(edge, smoothstep(.42, .7, 1 - dot(normal, otherNormal)));
                }
                return half4(_Color.rgb, _Color.a * edge * .85);
            }
            ENDHLSL
        }
    }
}
