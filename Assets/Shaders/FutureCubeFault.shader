Shader "COMP30019/Future Cube Fault"
{
    Properties
    {
        _Color ("Red Casing", Color) = (1,0,0,1)
        _DecayAmount ("Failure Amount", Range(0,1)) = 1
        _DamageStrength ("Casing Wear", Range(0,1)) = 0.5
        _FlickerStrength ("Pulse Strength", Range(0,1)) = 0.3
        _EmissionColor ("Fault Light", Color) = (1,0.28,0.035,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "UnityLightingCommon.cginc"
            fixed4 _Color, _EmissionColor;
            float _DecayAmount, _DamageStrength, _FlickerStrength;
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; UNITY_FOG_COORDS(2) };
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 c=floor(p), f=frac(p);
                f=f*f*(3.0-2.0*f);
                return lerp(lerp(hash(c),hash(c+float2(1,0)),f.x),
                    lerp(hash(c+float2(0,1)),hash(c+float2(1,1)),f.x),f.y);
            }
            v2f vert(appdata v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                o.normal=UnityObjectToWorldNormal(v.normal); UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float failure=saturate(_DecayAmount);
                float2 uv=i.uv;
                float aa=max(fwidth(uv.x),fwidth(uv.y));
                // Inset outline reads as a manufactured casing panel, unlike wall corrosion.
                float2 edge=min(uv,1.0-uv);
                float borderDistance=abs(min(edge.x,edge.y)-0.09);
                float border=1.0-smoothstep(0.004,0.009+aa,borderDistance);
                float track=1.0-smoothstep(0.003,0.007+aa,abs(uv.y-0.32));
                track*=smoothstep(0.1,0.12,uv.x)*(1.0-smoothstep(0.65,0.67,uv.x));
                float branch=1.0-smoothstep(0.003,0.007+aa,abs(uv.x-0.65));
                branch*=smoothstep(0.32,0.34,uv.y)*(1.0-smoothstep(0.7,0.72,uv.y));
                float circuit=saturate(border+track+branch);
                // Large irregular clusters modulate finer chips, leaving intact red paint.
                float zone=noise(uv*3.7+float2(8.3,2.1));
                float detail=noise(uv*23.0+zone*0.8);
                float grain=noise(uv*95.0+7.1);
                float resolved=1.0-smoothstep(0.5,1.5,aa*95.0);
                grain=lerp(0.5,grain,resolved);
                float edgeWear=1.0-smoothstep(0.02,0.24,min(edge.x,edge.y));
                float damage=zone*0.48+detail*0.32+grain*0.20+edgeWear*0.13;
                float antialias=max(fwidth(damage),0.008);
                float chips=smoothstep(0.59-antialias,0.68+antialias,damage);
                float rust=smoothstep(0.54,0.72,damage)*smoothstep(0.32,0.70,zone);
                float wear=failure*saturate(_DamageStrength*1.5);
                fixed3 paint=_Color.rgb*lerp(1.0,0.65,failure);
                fixed3 rustColor=lerp(fixed3(0.095,0.047,0.026),fixed3(0.37,0.16,0.065),detail);
                paint=lerp(paint,rustColor,rust*wear);
                paint=lerp(paint,fixed3(0.085,0.08,0.07),chips*wear);
                // Keep the red casing plain; no inset panel or circuit markings.
                float pulse=0.35+0.65*pow(0.5+0.5*sin(_Time.y*2.3),3.0);
                float interruptions=lerp(0.35,1.0,step(0.15,hash(float2(floor(_Time.y*5.0),2))));
                fixed3 glow=_EmissionColor.rgb*circuit*pulse*interruptions*_FlickerStrength*failure;
                float3 n=normalize(i.normal);
                half3 light=max(half3(0.12,0.12,0.12),ShadeSH9(half4(n,1)))
                    +_LightColor0.rgb*saturate(dot(n,normalize(_WorldSpaceLightPos0.xyz)));
                fixed4 result=fixed4(paint*light+glow,1);
                UNITY_APPLY_FOG(i.fogCoord,result); return result;
            }
            ENDCG
        }
    }
    FallBack Off
}
