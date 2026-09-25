Shader "DineIn/Kitchen Cooking Marks"
{
    Properties
    {
        _Dirt ("Mess Amount (Editor Preview)", Range(0,1)) = 0
        _Seed ("Pattern Seed", Float) = 1
        _SpotScale ("Mark Size", Float) = 1
        _Opacity ("Opacity", Range(0,1)) = .5
        [Enum(Oil,0,Patty,1,Crumbs,2)] _MarkStyle ("Cooking Marks", Float) = 0
        _OilColor ("Warm Oil", Color) = (.83,.48,.12,1)
        _PattyColor ("Patty Contact", Color) = (.40,.22,.10,1)
        _CrumbColor ("Golden Crumbs", Color) = (.76,.42,.13,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Dirt, _Seed, _SpotScale, _Opacity, _MarkStyle;
                half4 _OilColor, _PattyColor, _CrumbColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.world=TransformObjectToWorld(v.positionOS.xyz); o.normal=TransformObjectToWorldNormal(v.normalOS); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))+_Seed)*43758.5453); }
            half4 frag(Varyings i):SV_Target
            {
                // Cooking residue rests on worktops; appliance doors stay clean.
                if (_Dirt<.001 || i.normal.y<.7) discard;
                float2 uv=i.world.xz*2/max(.1,_SpotScale);
                float2 cell=floor(uv), p=frac(uv)-(.3+.4*float2(hash(cell),hash(cell+7)));
                float visible=step(hash(cell+23),lerp(.04,.34,saturate(_Dirt)));
                float angle=hash(cell+11)*6.28318;
                p=mul(float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)),p);
                float r=length(p*float2(1,1.25));
                float edge=max(fwidth(r),.003);
                float body=1-smoothstep(.14-edge,.14+edge,r);
                float drops=(1-smoothstep(.026,.038,length(p-float2(.20,.03))))
                    +(1-smoothstep(.016,.027,length(p-float2(-.12,.18))));
                half3 color=_OilColor.rgb;
                float mark=saturate(body*.48+drops*.8);
                if (_MarkStyle>.5 && _MarkStyle<1.5)
                {
                    float contact=(1-smoothstep(.022,.038,abs(r-.17)))*.55;
                    float stripes=(1-smoothstep(.013,.025,abs(frac((p.y+.5)*14)-.5)/14))
                        *(1-smoothstep(.13,.17,r));
                    mark=saturate(contact+stripes*.45);color=_PattyColor.rgb;
                }
                else if (_MarkStyle>1.5)
                {
                    float crumbs=(1-smoothstep(.025,.038,length(p)))
                        +(1-smoothstep(.025,.035,length(p-float2(.09,.05))))
                        +(1-smoothstep(.016,.025,length(p-float2(-.09,.09))));
                    mark=saturate(crumbs);color=_CrumbColor.rgb;
                }
                return half4(color,mark*visible*_Opacity*lerp(.35,1,saturate(_Dirt)));
            }
            ENDHLSL
        }
    }
}
