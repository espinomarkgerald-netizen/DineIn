Shader "Dine In/Cozy Food"
{
    Properties
    {
        _BaseColor ("Cooked / Base Colour", Color) = (1,.65,.25,1)
        _RawColor ("Raw Food Colour", Color) = (1,.75,.65,1)
        [Toggle] _CookingSurface ("Changes Colour While Cooking", Float) = 0
        _ShadowStrength ("Toon Shade Strength", Range(0,1)) = .25
        _LightThreshold ("Light Band Threshold", Range(0,1)) = .55
        _Feather ("Light Band Softness", Range(.001,.3)) = .04
        _HighlightStrength ("Soft Highlight", Range(0,.5)) = .08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "Cozy Food"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RawColor;
                half _CookingSurface, _ShadowStrength, _LightThreshold, _Feather, _HighlightStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                Light light=GetMainLight();
                half3 n=normalize(i.normalWS);
                half lighting=dot(n,light.direction)*.5+.5;
                half band=smoothstep(_LightThreshold-_Feather,_LightThreshold+_Feather,lighting);
                half3 color=_BaseColor.rgb*lerp(1-_ShadowStrength,1,band);
                half3 h=SafeNormalize(light.direction+GetWorldSpaceNormalizeViewDir(i.positionWS));
                color+=pow(saturate(dot(n,h)),24)*_HighlightStrength;
                return half4(color,_BaseColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct Input { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Output { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; };
            Output DepthVertex(Input i)
            { Output o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.normalWS=TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 DepthFragment(Output i):SV_Target
            {
                float3 normal=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(normal);
                return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                return half4(normal,0);
                #endif
            }
            ENDHLSL
        }
    }
}
