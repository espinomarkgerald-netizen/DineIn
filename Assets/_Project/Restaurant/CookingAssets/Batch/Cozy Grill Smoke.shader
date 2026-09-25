Shader "Dine In/Cozy Grill Smoke"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;o.uv=v.uv;return o; }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;
                float d=min(length(p-float2(-.28,-.14))-.5,min(length(p-float2(.27,-.1))-.48,length(p-float2(0,.28))-.52));
                return half4(i.color.rgb*(.88+.12*smoothstep(-.4,.05,p.y)),i.color.a*(1-smoothstep(-.05,.08,d)));
            }
            ENDHLSL
        }
    }
}
