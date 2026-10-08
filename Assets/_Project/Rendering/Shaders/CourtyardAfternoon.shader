Shader "Dine In/Courtyard Afternoon"
{
    Properties
    {
        _Tint("Tint", Color) = (1,.89,.69,.09)
        _HourFade("Hour fade", Range(0,1)) = 1
        _Shadow("Ground shadow", Range(0,1)) = 0
        _SunDirection("Sun direction", Vector) = (-.9,-.438,0,0)
        _RoomA("Dining footprint", Vector) = (-11,-101,71,-51)
        _RoomB("Service footprint", Vector) = (-11,-52,47,-7)
        _RoomC("Reception footprint", Vector) = (-41,-93,-10,-61)
    }
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
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _HourFade, _Shadow;
            float4 _RoomA, _RoomB, _RoomC, _SunDirection;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 origin=TransformObjectToWorld(v.positionOS.xyz);
                float3 direction=normalize(_SunDirection.xyz);
                float distanceToGround=min(65,(origin.y+.4)/max(.1,-direction.y));
                float3 side=normalize(cross(direction,float3(0,1,0)));
                o.world=origin+direction*distanceToGround*v.uv.y+side*(v.uv.x*2-1)*lerp(1.5,6.5,v.uv.y);
                o.positionCS=TransformWorldToHClip(o.world); o.uv=v.uv; return o;
            }
            float InRoom(float2 p,float4 room) { return step(room.x,p.x)*step(room.y,p.y)*step(p.x,room.z)*step(p.y,room.w); }
            half4 Frag(Varyings i):SV_Target
            {
                // Sample actual sun shadows below: walls and cutaway roof proxies occlude the shafts.
                float2 p=i.uv*2-1;
                float shadow=1-smoothstep(.5,1,length(p));
                float edge=saturate(1-abs(p.x));
                edge*=edge;
                float ends=smoothstep(0,.15,i.uv.y)*(1-smoothstep(.65,1,i.uv.y));
                float ribbons=.9+.1*sin(i.uv.x*18+i.uv.y*2);
                if (_Shadow > .5) return half4(_Tint.rgb, saturate(_Tint.a*_HourFade*shadow));
                float visibility=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
                return half4(_Tint.rgb,saturate(_Tint.a*_HourFade*edge*ends*ribbons*visibility));
            }
            ENDHLSL
        }
    }
}
