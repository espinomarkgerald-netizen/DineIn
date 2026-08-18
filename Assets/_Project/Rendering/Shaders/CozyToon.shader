Shader "Dine In/Cozy Toon"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0,2)) = 1
        _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite("Z Write", Float) = 1
        [HideInInspector] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _EmissionColor;
            float _BumpScale;
            float _Cutoff;
            float _AlphaClip;
            float _Surface;
            float _SrcBlend;
            float _DstBlend;
            float _ZWrite;
            float _Cull;
        CBUFFER_END

        float4 _CozyToonShadowColor;
        float _CozyToonShadowTintStrength;
        float _CozyToonShadowBrightness;
        float _CozyToonDeepShadowBrightness;
        float _CozyToonDeepShadowThreshold;
        float4 _CozyToonLightTint;
        float _CozyToonLightTintStrength;
        float _CozyToonShadowThreshold;
        float _CozyToonHighlightThreshold;
        float _CozyToonBandSoftness;
        float4 _CozyToonAmbientColor;
        float _CozyToonAmbientStrength;
        float _CozyToonSceneLightColorInfluence;
        float _CozyToonUseSceneFog;
        float _CozyToonPaletteGradeEnabled;
        float _CozyToonSaturation;
        float _CozyToonWarmth;
        float _CozyToonRimEnabled;
        float4 _CozyToonRimColor;
        float _CozyToonRimIntensity;
        float _CozyToonRimPower;
        float _CozyToonSpecularEnabled;
        float4 _CozyToonSpecularColor;
        float _CozyToonSpecularIntensity;
        float _CozyToonSpecularSize;
        float _CozyToonOutlineEnabled;
        float4 _CozyToonOutlineColor;
        float _CozyToonOutlineWidth;

        half4 SampleBase(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
        }

        void ApplyAlphaClip(half alpha)
        {
            #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "CozyToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            struct ToonAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ToonVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ToonVaryings ToonVertex(ToonAttributes input)
            {
                ToonVaryings output = (ToonVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                real tangentSign = input.tangentOS.w * GetOddNegativeScale();

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, tangentSign);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 GetToonNormal(ToonVaryings input)
            {
                half3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP)
                    half3 tangentWS = normalize(input.tangentWS.xyz);
                    half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(tangentWS, bitangentWS, normalWS)));
                #endif
                return normalWS;
            }

            half3 ApplyCozyGrade(half3 color)
            {
                if (_CozyToonPaletteGradeEnabled < 0.5h)
                    return color;

                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color = lerp(luminance.xxx, color, _CozyToonSaturation);

                half warmAmount = saturate(_CozyToonWarmth);
                half coolAmount = saturate(-_CozyToonWarmth);
                color *= lerp(half3(1.0h, 1.0h, 1.0h), half3(1.28h, 1.08h, 0.72h), warmAmount);
                color *= lerp(half3(1.0h, 1.0h, 1.0h), half3(0.72h, 1.04h, 1.28h), coolAmount);
                return color;
            }

            half3 EvaluateMainToonLight(half3 albedo, half3 normalWS, half3 viewDirectionWS, Light light)
            {
                half attenuation = saturate(light.distanceAttenuation * light.shadowAttenuation);
                half halfLambert = saturate(dot(normalWS, light.direction) * 0.5h + 0.5h);
                half shapedLight = halfLambert * attenuation;
                half softness = max(_CozyToonBandSoftness, 0.001h);

                half baseBand = smoothstep(_CozyToonShadowThreshold - softness, _CozyToonShadowThreshold + softness, shapedLight);
                half firstShadeBand = smoothstep(_CozyToonDeepShadowThreshold - softness, _CozyToonDeepShadowThreshold + softness, shapedLight);
                half highlightBand = smoothstep(_CozyToonHighlightThreshold - softness, _CozyToonHighlightThreshold + softness, shapedLight);
                half shadowAmount = 1.0h - baseBand;
                half3 shadowTint = lerp(
                    half3(1.0h, 1.0h, 1.0h),
                    _CozyToonShadowColor.rgb,
                    shadowAmount * _CozyToonShadowTintStrength);
                half brightness = lerp(_CozyToonDeepShadowBrightness, _CozyToonShadowBrightness, firstShadeBand);
                brightness = lerp(brightness, 1.0h, baseBand);
                half lightIntensity = max(dot(light.color, half3(0.2126h, 0.7152h, 0.0722h)), 0.05h);
                half3 sceneLightColor = lerp(
                    lightIntensity.xxx,
                    light.color,
                    _CozyToonSceneLightColorInfluence);
                half3 color = albedo * shadowTint * brightness * sceneLightColor;
                color = lerp(
                    color,
                    color * _CozyToonLightTint.rgb,
                    highlightBand * _CozyToonLightTintStrength);

                if (_CozyToonSpecularEnabled > 0.5h)
                {
                    half3 halfDirection = SafeNormalize(light.direction + viewDirectionWS);
                    half specular = saturate(dot(normalWS, halfDirection));
                    half specularEdge = saturate(1.0h - _CozyToonSpecularSize);
                    half specularBand = smoothstep(specularEdge, specularEdge + softness, specular);
                    color += _CozyToonSpecularColor.rgb * specularBand * baseBand * attenuation * _CozyToonSpecularIntensity;
                }

                return color;
            }

            half4 ToonFragment(ToonVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 surface = SampleBase(input.uv);
                ApplyAlphaClip(surface.a);

                half3 albedo = surface.rgb;
                half3 normalWS = GetToonNormal(input);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(input.shadowCoord);

                half3 color = EvaluateMainToonLight(albedo, normalWS, viewDirectionWS, mainLight);
                half3 ambientSH = max(SampleSH(normalWS), 0.0h);
                half ambientLuminance = dot(ambientSH, half3(0.2126h, 0.7152h, 0.0722h));
                color += albedo * _CozyToonAmbientColor.rgb * ambientLuminance * (_CozyToonAmbientStrength * 0.3h);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint additionalLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < additionalLightCount; ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                        half attenuation = saturate(additionalLight.distanceAttenuation * additionalLight.shadowAttenuation);
                        half halfLambert = saturate(dot(normalWS, additionalLight.direction) * 0.5h + 0.5h);
                        half litBand = smoothstep(
                            _CozyToonShadowThreshold - _CozyToonBandSoftness,
                            _CozyToonShadowThreshold + _CozyToonBandSoftness,
                            halfLambert * attenuation);
                        half additionalLightIntensity = max(dot(additionalLight.color, half3(0.2126h, 0.7152h, 0.0722h)), 0.0h);
                        half3 additionalLightColor = lerp(
                            additionalLightIntensity.xxx,
                            additionalLight.color,
                            _CozyToonSceneLightColorInfluence);
                        color += albedo * additionalLightColor * litBand * attenuation * 0.3h;
                    }
                #endif

                if (_CozyToonRimEnabled > 0.5h)
                {
                    half rim = pow(saturate(1.0h - dot(normalWS, viewDirectionWS)), _CozyToonRimPower);
                    color += _CozyToonRimColor.rgb * rim * _CozyToonRimIntensity;
                }

                #if defined(_EMISSION)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                if (_CozyToonUseSceneFog > 0.5h)
                    color = MixFog(color, input.fogFactor);
                color = ApplyCozyGrade(color);
                return half4(color, surface.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "CozyToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            struct OutlineAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            OutlineVaryings OutlineVertex(OutlineAttributes input)
            {
                OutlineVaryings output = (OutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 normalVS = TransformWorldToViewDir(normalWS, true);
                float2 outlineDirection = normalVS.xy;
                float directionLength = max(length(outlineDirection), 0.0001);
                outlineDirection /= directionLength;
                positionCS.xy += outlineDirection * (_CozyToonOutlineWidth * 2.0 / _ScreenParams.xy) * positionCS.w;

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 OutlineFragment(OutlineVaryings input) : SV_Target
            {
                clip(_CozyToonOutlineEnabled - 0.5h);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return _CozyToonOutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetCozyShadowPosition(float3 positionOS, float3 normalOS)
            {
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float3 lightDirectionWS = _LightDirection;
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    lightDirectionWS = normalize(_LightPosition - positionWS);
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE * positionCS.w);
                #endif
                return positionCS;
            }

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = GetCozyShadowPosition(input.positionOS.xyz, input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHATEST_ON

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            DepthNormalsVaryings DepthNormalsVertex(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyAlphaClip(SampleBase(input.uv).a);
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
