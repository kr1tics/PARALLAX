Shader "COMP30019/DistortableObject"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _Amplitude ("Distortion Amplitude", Range(0, 0.5)) = 0.15
        _EffectAmount ("Effect Amount", Range(0,1)) = 0
        _Frequency ("Distortion Frequency", Range(1, 90)) = 8
        _Speed ("Distortion Speed", Range(0, 10)) = 3
        _AberrationStrength (
            "Chromatic Aberration",
            Range(0, 0.05)
        ) = 0.01
        _ChaosStrength ("Chaos Strength", Range(0, 0.05)) = 0.01
        _ChaosSpeed ("Chaos Speed", Range(0, 10)) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _EffectAmount;
            float _Amplitude;
            float _Frequency;
            float _Speed;
            float _AberrationStrength;
            float _ChaosStrength;
            float _ChaosSpeed;
            
            v2f vert(appdata v)
            {
                v2f o;

                float4 displacedVertex = v.vertex;

                float wave1 = sin(
                    (v.vertex.x + v.vertex.y + v.vertex.z)
                    * _Frequency
                    + _Time.y * _Speed
                );

                float wave2 = sin(
                    (v.vertex.x - v.vertex.y + v.vertex.z)
                    * (_Frequency * 0.7)
                    - _Time.y * (_Speed * 1.3)
                );

                float wave =
                    wave1 * 0.65
                  + wave2 * 0.35;

                float displacement =
                    wave
                    * _Amplitude
                    * _EffectAmount;
                float3 distortionDirection =
                    normalize(v.vertex.xyz);

                displacedVertex.xyz +=
                    distortionDirection * displacement;

                o.vertex =
                    UnityObjectToClipPos(displacedVertex);

                o.uv =
                    TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float offset =
                    _AberrationStrength
                    * _EffectAmount;

                float baseOffset =
                    _AberrationStrength
                    * _EffectAmount;

                float chaosAmount =
                    _ChaosStrength
                    * _EffectAmount;

                float chaos1 = sin(
                    i.uv.x * 25
                    + i.uv.y * 17
                    + _Time.y * _ChaosSpeed
                );

                float chaos2 = cos(
                    i.uv.x * 19
                    - i.uv.y * 23
                    - _Time.y * (_ChaosSpeed * 1.3)
                );

                float2 chaosDir = normalize(
                    float2(chaos1, chaos2)
                );

                float2 chaosOffset =
                    chaosDir * chaosAmount;

                float2 redUV =
                    i.uv
                    + float2(baseOffset, 0)
                    + chaosOffset * 1.2;

                float2 greenUV =
                    i.uv
                    + chaosOffset * 0.3;

                float2 blueUV =
                    i.uv
                    - float2(baseOffset, 0)
                    - chaosOffset;

                float red =
                    tex2D(_MainTex, redUV).r;

                float green =
                    tex2D(_MainTex, greenUV).g;

                float blue =
                    tex2D(_MainTex, blueUV).b;

                fixed4 col =
                    fixed4(red, green, blue, 1);

                col *= _Color;

                return col;
            }
            ENDCG
        }
    }
}
