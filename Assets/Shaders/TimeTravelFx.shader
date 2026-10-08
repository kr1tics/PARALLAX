// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Based on the Multi-pass shader in Workshop 6.
// https://github.com/feit-comp30019/workshop-6/blob/main/Assets/MultiPassShader.shader
// Also uses some tricks inspired by https://www.ronja-tutorials.com/post/039-screenspace-texture/#screenspace-coordinates-in-unlit-shaders
Shader "Unlit/TimeTravelFx"{
	Properties{
		_Intensity ("Intensity", Float) = 25.0
		_StartTime ("StartTime", Float) = -999.0 
		_Duration ("Duration", Float) = 5.0 
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("_Color", Color) = (0,0,0,1)
	}

	SubShader{
		// You need to set up a blending mode or we can't do alpha: https://discussions.unity.com/t/alpha-value-in-fragment-shader/604737/2
		Blend SrcAlpha OneMinusSrcAlpha
		// Set to the right layer https://docs.unity3d.com/2019.3/Documentation/Manual/SL-SubShaderTags.html
		Tags{ "RenderType"="Overlay" "Queue"="Overlay"}

		Pass{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			
			// Unity Material Details
			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			
			// Custom Properties
			float _Intensity;
			float _StartTime;
			float _Duration;

			
			struct vertIn
			{
				float4 vertex : POSITION;
			};
			struct vertOutFragIn
			{
				float4 vertex : SV_POSITION;
				float4 screenPos : TEXCOORD0;
			};

			// Vertex Shader -- Basically just exists to get screen-space coordinates.
			vertOutFragIn vert(vertIn v){
				vertOutFragIn o;
				// Move the vertices from world to clip-space. 
				// This function has some shortcuts in edge-cases over just multiplying by the MVP Matrix.
				// https://discussions.unity.com/t/unityobjecttoclippos/625278/2
				o.vertex = UnityObjectToClipPos(v.vertex);
				
				// Get the Rasterized Coordinate.
				o.screenPos = ComputeScreenPos(o.vertex);
				
				return o;
			}

			int between(float _in, float2 bounds)
			{
				return ((_in > bounds.x) && (_in < bounds.y));
			}
			
			
			// Fragment Shader -- The actual "meat" of the shader.
			fixed4 frag(vertOutFragIn i) : SV_TARGET{
				/// CONSTANTS:
				// X-Axis Unit Vector
				float2 x = { 1, 0 };
				
				
				// The Aspect Ratio of the screen.
                float aspect = _ScreenParams.x / _ScreenParams.y;
				
				// Stretch _MainTex across the screen.
                float2 textureCoordinates = { i.screenPos.x * aspect, i.screenPos.y };
				textureCoordinates = textureCoordinates / i.screenPos.w;
                textureCoordinates = TRANSFORM_TEX(textureCoordinates, _MainTex);
				
				// Get the texture colors and tint them.
				fixed4 colour = tex2D(_MainTex, textureCoordinates);
				colour *= _Color;
				
				// Midpoint of the screen.
                float2 midpoint = { _ScreenParams.x / 2 , _ScreenParams.y / 2 };
				float furthest = length(midpoint);
				// Actual pixel coordinates of the current pixel.
				float2 pixels = i.screenPos.xy * _ScreenParams.xy;
				// Position relative to center rather than top-left.
				float2 fromCenter = { (pixels.x - midpoint.x) , (pixels.y - midpoint.y) };
				
				// Show nothing by default.
				colour.a = 0.0f;
				
				// Our period is $\frac{2\pi}{duration}$, but we divide by this, so this is flipped.
				float scaleFactor = _Duration / (radians(180)); 
				
				// Actually draw the effect.
				if (_Time.y - _StartTime < _Duration && _Time.y >= _StartTime)
				{
					if (length(fromCenter) > abs(cos((_Time.y - _StartTime) / scaleFactor) * furthest))
					{
						colour.a = 1.0f;
					}
				}
				
				return colour;
			}

			ENDCG
		}
	}
}