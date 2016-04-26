
Shader "RPM/CroppedDisplayShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Opacity("_Opacity", Range(0,1) ) = 1
		_Color ("_Color", Color) = (1,1,1,1)
		_CropBound ("_CropBound", Vector) = (-1,-1,1,1)
	}

	SubShader {

		Tags { "RenderType"="Overlay" "Queue" = "Transparent" }

		// Premultiplied Alpha shader for rendering/coloring textures.
		// Includes cropping values in normalized device coordinates

		Lighting Off
		Blend One OneMinusSrcAlpha
		Cull Back
		Fog { Mode Off }
		ZWrite Off
		ZTest Always

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma glsl

			#include "UnityCG.cginc"

			struct appdata_t {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float2 pixelPos : TEXCOORD1;
			};

			sampler2D _MainTex;

			uniform float4 _MainTex_ST;
			// Color modulation
			uniform float4 _Color;
			// Cropping window in device normalized coordinates
			uniform float4 _CropBound;
			// Overall opacity scalar
			uniform float _Opacity;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				o.pixelPos = float2(o.vertex.xy / o.vertex.w);
				return o;
			}

			float4 frag (v2f i) : COLOR
			{
				if(i.pixelPos.x < _CropBound.x || i.pixelPos.y < _CropBound.y || i.pixelPos.x > _CropBound.z || i.pixelPos.y > _CropBound.w)
				{
					discard;
					return (0,0,0,0);
				}
				else
				{
					float4 diffuse = tex2D(_MainTex, i.texcoord);
					diffuse.a *= _Color.a * _Opacity;
					diffuse.rgb = (diffuse.rgb * _Color.rgb) * diffuse.a;
					return diffuse;
				}
			}
			ENDCG
		}
	}

	Fallback off
}
