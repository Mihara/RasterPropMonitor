// Greenscale shader for rentex
Shader "RPM/Greenscale"
{
	Properties
	{
		_MainTex ("Render Input", 2D) = "white" {}
		_Gain ("_Gain", float) = 1.0
		_Opacity ("_Opacity", float) = 1.0
	}
	SubShader
	{
		ZTest Always Cull Off ZWrite Off Fog { Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha
		Pass
		{
			CGPROGRAM
				#pragma vertex vert_img
				#pragma fragment frag
				#pragma target 3.0
				#include "UnityCG.cginc"

				UNITY_DECLARE_TEX2D(_MainTex);
				uniform float _Gain;
				uniform float _Opacity;

				fixed4 frag(v2f_img IN) : SV_TARGET
				{
					float4 c = UNITY_SAMPLE_TEX2D (_MainTex, IN.uv);

					// CIE 1931 conversion of linear color to luminance
					float Y = c.r * 0.2126 + c.g * 0.7152 + c.b * 0.0722;
					// Apply gain
					float gainBoost = max(0.0, _Gain - 1.0) * 0.15;
					Y = saturate(Y * _Gain + gainBoost);
					return fixed4(0.0, Y, 0.0, saturate(_Opacity));
				}
			ENDCG
		}
	}
}
