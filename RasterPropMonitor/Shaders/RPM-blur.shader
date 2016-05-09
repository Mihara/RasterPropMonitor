// 5x5 gaussian blur shader for rentex
Shader "RPM/Blur"
{
	Properties
	{
		_MainTex ("Render Input", 2D) = "white" {}
		_Gain ("_Gain", float) = 1.0
		_ImageDims ("_ImageDims", Vector) = (512,512,0.001953125,0.001953125)
	}
	SubShader {
		ZTest Always Cull Off ZWrite Off Fog { Mode Off }
		Pass
		{
			CGPROGRAM
				#pragma vertex vert_img
				#pragma fragment frag
				#pragma target 3.0
				#include "UnityCG.cginc"

				sampler2D _MainTex;
				uniform float _Gain;
				uniform float4 _ImageDims;

				float4 frag(v2f_img IN) : COLOR
				{
					// 5x5 gaussian filter written longhand....
					// and assuming a 512x512 ish texture size
					float4 color;

					// (-2, -2) wt 1/273
					float2 uv = saturate(IN.uv + float2(-2.0 * _ImageDims.z, -2.0 * _ImageDims.w));
					color = tex2D(_MainTex, uv) * 0.003663003;
					// (-1, -2) wt 4/273
					uv = saturate(IN.uv + float2(-1.0 * _ImageDims.z, -2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;
					// (+0, -2) wt 7/273
					uv = saturate(IN.uv + float2(+0.0 * _ImageDims.z, -2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.025641025;
					// (+1, -2) wt 4/273
					uv = saturate(IN.uv + float2(+1.0 * _ImageDims.z, -2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;
					// (+2, -2) wt 1/273
					uv = saturate(IN.uv + float2(+2.0 * _ImageDims.z, -2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.003663003;

					// (-2, -1) wt 4/273
					uv = saturate(IN.uv + float2(-2.0 * _ImageDims.z, -1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;
					// (-1, -1) wt 16/273
					uv = saturate(IN.uv + float2(-1.0 * _ImageDims.z, -1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.058608058;
					// (+0, -1) wt 24/273
					uv = saturate(IN.uv + float2(+0.0 * _ImageDims.z, -1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.087912087;
					// (+1, -1) wt 16/273
					uv = saturate(IN.uv + float2(+1.0 * _ImageDims.z, -1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.058608058;
					// (+2, -1) wt 4/273
					uv = saturate(IN.uv + float2(+2.0 * _ImageDims.z, -1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;

					// (-2, 0) wt 6/273
					uv = saturate(IN.uv + float2(-2.0 * _ImageDims.z, +0.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.021978021;
					// (-1, -1) wt 24/273
					uv = saturate(IN.uv + float2(-1.0 * _ImageDims.z, +0.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.087912087;
					// (+0, 0) wt 41/273
					uv = saturate(IN.uv + float2(+0.0 * _ImageDims.z, +0.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.15018315;
					// (+1, 0) wt 2473
					uv = saturate(IN.uv + float2(+1.0 * _ImageDims.z, +0.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.087912087;
					// (+2, 0) wt 6/273
					uv = saturate(IN.uv + float2(+2.0 * _ImageDims.z, +0.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.021978021;

					// (-2, +1) wt 4/273
					uv = saturate(IN.uv + float2(-2.0 * _ImageDims.z, +1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;
					// (-1, +1) wt 16/273
					uv = saturate(IN.uv + float2(-1.0 * _ImageDims.z, +1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.058608058;
					// (+0, +1) wt 24/273
					uv = saturate(IN.uv + float2(+0.0 * _ImageDims.z, +1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.087912087;
					// (+1, +1) wt 16/273
					uv = saturate(IN.uv + float2(+1.0 * _ImageDims.z, +1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.058608058;
					// (+2, +1) wt 4/273
					uv = saturate(IN.uv + float2(+2.0 * _ImageDims.z, +1.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;

					// (-2, +2) wt 1/273
					uv = saturate(IN.uv + float2(-2.0 * _ImageDims.z, +2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.003663003;
					// (-1, +2) wt 4/273
					uv = saturate(IN.uv + float2(-1.0 * _ImageDims.z, +2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;
					// (+0, +2) wt 7/273
					uv = saturate(IN.uv + float2(+0.0 * _ImageDims.z, +2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.025641025;
					// (+1, +2) wt 4/273
					uv = saturate(IN.uv + float2(+1.0 * _ImageDims.z, +2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.014652014;
					// (+2, +2) wt 1/273
					uv = saturate(IN.uv + float2(+2.0 * _ImageDims.z, +2.0 * _ImageDims.w));
					color += tex2D(_MainTex, uv) * 0.003663003;

					color.r = color.r * _Gain;
					color.g = color.g * _Gain;
					color.b = color.b * _Gain;
					
					return color;
				}
			ENDCG
		}
	}
}
