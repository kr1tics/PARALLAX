Shader "COMP30019/Future Environment Decay"
{
    Properties
    {
        [HideInInspector] _MainTex ("Base Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Original Tint", Color) = (1,1,1,1)
        _DecayColor ("Future Decay Color", Color) = (0.28, 0.26, 0.22, 1)
        _DecayAmount ("Decay Amount", Range(0, 1)) = 1
        _GrimeStrength ("Grime Strength", Range(0, 2)) = 0.8
        _DamageStrength ("Damage Strength", Range(0, 2)) = 0.6
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 100

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _DecayColor;
            float _WeatherSeed; // Set per room renderer; keeps repeated kit pieces from matching.
            float _DecayAmount;
            float _GrimeStrength;
            float _DamageStrength;
            float _FlickerStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 localPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                float3 worldPos : TEXCOORD4;
            };

            // True 3D noise avoids collapsing a wall onto a line in a 2D projection.
            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 p)
            {
                float3 c = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(lerp(hash31(c), hash31(c + float3(1,0,0)), f.x),
                         lerp(hash31(c + float3(0,1,0)), hash31(c + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash31(c + float3(0,0,1)), hash31(c + float3(1,0,1)), f.x),
                         lerp(hash31(c + float3(0,1,1)), hash31(c + float3(1,1,1)), f.x), f.y), f.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // Compensate import/non-uniform scale, keeping detail in approximately metre units.
                float3 scale = float3(length(unity_ObjectToWorld._m00_m10_m20),
                    length(unity_ObjectToWorld._m01_m11_m21), length(unity_ObjectToWorld._m02_m12_m22));
                o.localPos = v.vertex.xyz * scale;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                float decay = saturate(_DecayAmount);

                float floorWeight = smoothstep(0.45,0.85,normalize(i.worldNormal).y);
                float3 p = i.localPos + (1.0-floorWeight) * _WeatherSeed * float3(17.3,31.7,11.9);
                // Rotate and warp the noise domain so corrosion does not follow a cubic grid.
                p = float3(dot(p,float3(0.8,0.0,0.6)), p.y, dot(p,float3(-0.6,0.0,0.8)));
                float broad = noise3(p * 2.3 + 5.7);
                float3 warped = p + (broad - 0.5) * float3(0.21,0.13,0.17);
                float medium = noise3(warped * 16.0 + 11.2);
                float fine = noise3(warped * 83.0 + 4.6);
                float micro = noise3(warped * 190.0 + 23.5);
                float fineVisible = 1.0 - smoothstep(0.5, 1.8, length(fwidth(p * 83.0)));
                float microVisible = 1.0 - smoothstep(0.5, 1.8, length(fwidth(p * 190.0)));
                fine = lerp(0.5, fine, fineVisible);
                micro = lerp(0.5, micro, microVisible);
                // Surface direction separates floors from walls without separate materials.
                float wallWeight = 1.0 - smoothstep(0.25,0.75,abs(normalize(i.worldNormal).y));
                // Large weathering zones leave genuinely quieter areas between damaged clusters.
                float zone = noise3(p * 0.65 + 31.4);
                float clustered = smoothstep(0.30,0.73,zone);
                // A weak long stain beneath fragmented corrosion, not solid islands of rust.
                float streak = noise3(p * float3(12.0, 0.8, 12.0) + 17.1);
                float runoff = smoothstep(0.48,0.78,streak) * wallWeight * clustered;
                float irregular = broad * 0.24 + medium * 0.40 + fine * 0.26 + micro * 0.10;
                irregular += (1.0-floorWeight) * ((clustered - 0.5) * 0.24 + runoff * 0.09);
                float edgeAA = max(fwidth(irregular) * 0.6, 0.008);
                float coatingLoss = smoothstep(0.53-edgeAA, 0.60+edgeAA, irregular);
                float rustCore = smoothstep(0.57-edgeAA, 0.65+edgeAA, irregular);
                float rim = saturate(coatingLoss-rustCore);
                float damage = saturate(decay * _DamageStrength);
                float grimeMask = lerp(
                    saturate(0.08 + broad * clustered * 0.5 + runoff * 0.6),
                    saturate(0.2 + broad * 0.45 + streak * 0.25), floorWeight) * decay * _GrimeStrength;

                fixed3 cleanColor = tex.rgb * _Color.rgb;
                float luminance = dot(cleanColor, float3(0.2126,0.7152,0.0722));
                fixed3 agedColor = lerp(cleanColor, luminance.xxx, decay * 0.3);
                // Weathered grey paint: retain the atlas detail without its bright white finish.
                agedColor *= lerp(1.0, lerp(0.50 + medium * 0.08 - clustered * 0.06, 0.49 + medium * 0.08, floorWeight), decay);
                agedColor = lerp(agedColor, agedColor * lerp(float3(1,1,1),_DecayColor.rgb,0.6),
                    saturate(grimeMask) * 0.65);
                fixed3 rustColor = lerp(fixed3(0.075,0.037,0.022), fixed3(0.29,0.13,0.052),fine);
                rustColor *= 0.8 + micro * 0.4;
                agedColor = lerp(agedColor,rustColor,rustCore * damage);
                agedColor = lerp(agedColor,agedColor * 0.48,rim * damage);
                float pits = smoothstep(0.62,0.8,micro) * coatingLoss * damage;
                agedColor *= 1.0-pits * 0.5;

                // Screen-space surface gradient gives chipped paint a small lighting relief.
                // This changes shading only, never the mesh or collision shape.
                float height = damage * (fine * 0.0006 - coatingLoss * 0.0012 - pits * 0.0005);
                float3 normalDir = normalize(i.worldNormal);
                float3 dpdx = ddx(i.worldPos), dpdy = ddy(i.worldPos);
                float3 r1 = cross(dpdy,normalDir), r2 = cross(normalDir,dpdx);
                float determinant = dot(dpdx,r1);
                float3 gradient = (ddx(height)*r1 + ddy(height)*r2) / (abs(determinant)+0.000001);
                normalDir = normalize(normalDir - sign(determinant)*gradient);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                half3 diffuse = max(half3(0.12,0.12,0.12),ShadeSH9(half4(normalDir,1)))
                    + _LightColor0.rgb * saturate(dot(normalDir,lightDir));

                // Tiny unstable glow suggests failing future tech / exposed corrosion.
                float flickerNoise = hash31(floor(p * 9.0) + floor(_Time.y * 6.0));
                float flicker = smoothstep(0.84, 0.97, flickerNoise) * rim * damage * _FlickerStrength;
                fixed3 emission = fixed3(0.20, 0.65, 0.52) * flicker;

                fixed3 finalColor = agedColor * diffuse + emission;
                fixed4 result = fixed4(finalColor, 1);
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    FallBack Off
}
