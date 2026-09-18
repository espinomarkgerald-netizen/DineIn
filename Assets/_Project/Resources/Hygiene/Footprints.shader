Shader "DineIn/HygieneFootprints"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-9" "RenderType"="Transparent" }
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
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float2 kind:TEXCOORD1; float4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.kind=v.kind; o.color=v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv;
                float toe=1-smoothstep(.72,1,length(float2(p.x/.78,(p.y-.35)/.55)));
                float heel=1-smoothstep(.7,1,length(float2(p.x/.58,(p.y+.55)/.30)));
                float sole=max(toe,heel);
                float tread=lerp(.55,1,step(.30,frac((p.y+1)*9)));
                float spill=1-smoothstep(.5,1,length(p));
                float grain=.8+.2*frac(sin(dot(floor(p*30),float2(12.98,78.23)))*43758.5);
                float alpha=lerp(sole*tread,spill,i.kind.x)*grain*saturate(i.color.a)*.85;
                return half4(i.color.rgb,alpha);
            }
            ENDHLSL
        }
    }
}
