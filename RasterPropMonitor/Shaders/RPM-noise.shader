// Textured noise shader for rentex
Shader "RPM/Noise"
{
	Properties
	{
		_MainTex ("Render Input", 2D) = "white" {}
		_Noise ("_Noise", 2D) = "white" {}
		_Gain ("_Gain", float) = 1.0
		_Blend ("_Blend", float) = 1.0
		_NoiseOffset ("_NoiseOffset", float) = 0.0
		//_ImageDims ("_ImageDims", Vector) = (512,512,0.001953125,0.001953125)
	}
	SubShader 
	{
		ZTest Always Cull Off ZWrite Off Fog { Mode Off }
		Pass
		{
			CGPROGRAM
				#pragma vertex vert_img
				#pragma fragment frag
				#pragma target 3.0
				#include "UnityCG.cginc"

				sampler2D _MainTex;
				sampler2D _Noise;
				uniform float _Gain;
				uniform float _Blend;
				uniform float _NoiseOffset;

				fixed4 frag(v2f_img IN) : SV_TARGET
				{
					// Fetch color
					float2 uv = IN.uv;
					float4 color = tex2D(_MainTex, uv);

					// Apply gain
					float gainBoost = max(0.0, _Gain - 1.0) * 0.15;
					color.r = saturate((color.r * _Gain) + gainBoost);
					color.g = saturate((color.g * _Gain) + gainBoost);
					color.b = saturate((color.b * _Gain) + gainBoost);
					
					// Fetch noise, including offset
					uv.y = frac(uv.y + _NoiseOffset);
					float4 noise = tex2D(_Noise, uv);
					
					// Blend RGB
					color.r = lerp(noise.r, color.r, _Blend);
					color.g = lerp(noise.g, color.g, _Blend);
					color.b = lerp(noise.b, color.b, _Blend);
					
					return color;
				}
			ENDCG
		}
	}
}
