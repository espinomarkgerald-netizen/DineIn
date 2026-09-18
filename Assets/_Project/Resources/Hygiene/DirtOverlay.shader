Shader "DineIn/HygieneDirtOverlay"
{
    Properties
    {
        _Dirt ("Dirt", Range(0, 1)) = 0
        _Seed ("Seed", Float) = 1
        _SpotScale ("Spot Scale", Float) = 1
        _Opacity ("Opacity", Range(0,1)) = .75
        _CleanShine ("Clean Shine", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Dirt, _Seed, _SpotScale, _Opacity, _CleanShine;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalOS : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                // World units keep stains visible on imported meshes regardless of
                // whether their authoring units were centimetres or metres.
                o.positionOS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalOS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            float hash(float3 p) { return frac(sin(dot(p, float3(12.9898,78.233,37.719)) + _Seed) * 43758.5453); }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = abs(i.normalOS);
                float dirt = _Dirt;
                if ((dirt < .001 && _CleanShine <= 0) || _Opacity <= 0) discard;
                float2 uv = n.y >= max(n.x,n.z) ? i.positionOS.xz : n.x >= n.z ? i.positionOS.zy : i.positionOS.xy;
                uv *= 4.0 / max(.1, _SpotScale);
                float2 cell = floor(uv);
                if (_CleanShine > 0)
                {
                    float2 sparkle = frac(uv * .65) - .5;
                    float pulse = pow(saturate(sin(_Time.y * 2 + hash(float3(floor(uv * .65),9)) * 6.28318)), 8);
                    float star = max((1-smoothstep(.01,.035,abs(sparkle.x))) * (1-smoothstep(.02,.20,abs(sparkle.y))),
                                     (1-smoothstep(.01,.035,abs(sparkle.y))) * (1-smoothstep(.02,.20,abs(sparkle.x))));
                    return half4(1,1,1,star * pulse * .8);
                }
                float2 center = float2(hash(float3(cell,1)), hash(float3(cell,2))) * .4 + .3;
                float angle = hash(float3(cell,3)) * 6.28318;
                float2 p = frac(uv) - center;
                p = float2(cos(angle)*p.x-sin(angle)*p.y, sin(angle)*p.x+cos(angle)*p.y);
                p.y *= lerp(1, 2, hash(float3(cell,4)));
                float radius = lerp(.1,.34,hash(float3(cell,5)));
                float spot = 1-smoothstep(radius*.5,radius,length(p));
                float visible = step(hash(float3(cell,6)), saturate(.25 + dirt));
                return half4(.10,.075,.055,spot * visible * sqrt(saturate(dirt)) * _Opacity);
            }
            ENDHLSL
        }
    }
}
