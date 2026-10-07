Shader "Unlit/SimpleHologram"
{
    Properties
    {
        _Color ("Hologram Colour (RGB)", Color) = (0.1, 0.85, 1, 1)
        _ScanDensity ("Scanlines per UV Height", Range(1, 40)) = 12
        _ScanSpeed ("Scan Speed (cycles/second)", Range(-3, 3)) = 0.5
        _FlickerSpeed ("Flicker Speed (cycles/second)", Range(0, 10)) = 2
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.15
        _Alpha ("Opacity", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        // Keep depth testing, but do not write an opaque depth for this projection.
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _ScanDensity, _ScanSpeed;
            float _FlickerSpeed, _FlickerStrength;
            float _Alpha;

            struct vertIn
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct vertOut
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            vertOut vert(vertIn v)
            {
                vertOut o;
                // Apply model, view and projection transforms to the mesh position.
                o.vertex = UnityObjectToClipPos(v.vertex);
                // The rasterizer interpolates these UVs across each triangle.
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(vertOut i) : SV_Target
            {
                // One sine cycle is 2*pi radians. Equal UV y gives equal brightness.
                float phase = 6.2831853 * (i.uv.y * _ScanDensity - _Time.y * _ScanSpeed);
                float wave = 0.5 + 0.5 * sin(phase); // Remap [-1, 1] to [0, 1].
                float scanBrightness = 0.25 + 0.75 * wave;

                // All fragments share this time-only pulse: brightness stays in [1-strength, 1].
                float pulse = 0.5 + 0.5 * sin(6.2831853 * _Time.y * _FlickerSpeed);
                float flicker = 1.0 - _FlickerStrength * pulse;

                // Unlit colour; opacity is controlled separately by _Alpha.
                return fixed4(_Color.rgb * scanBrightness * flicker, _Alpha);
            }
            ENDCG
        }
    }
}
