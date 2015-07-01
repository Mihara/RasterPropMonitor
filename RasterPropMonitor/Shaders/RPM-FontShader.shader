
Shader "RPM/DisplayShader" 
{
	Properties 
	{ 
		_MainTex ("Texture", 2D) = "white" {}
	} 

	SubShader {

		Tags { "RenderType"="Overlay" "Queue" = "Transparent" }
		
		// Premultiplied Alpha shader for rendering text on displays.
		
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
			#pragma only_renderers d3d9 d3d11 opengl d3d11_9x

			#include "UnityCG.cginc"

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			sampler2D _MainTex;

			uniform float4 _MainTex_ST;
			
			v2f vert (appdata_t v)
			{
				v2f dataOut;
				// Unfortunately, the original font implementation used a
				// shader that required RGB to be halved, so we have to expand
				// it here.
				dataOut.color = float4(v.color.rgb * 2.0, v.color.a);
				dataOut.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				dataOut.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				return dataOut;
			}

			float4 frag (v2f dataIn) : COLOR
			{
				float4 diffuse = tex2D(_MainTex, dataIn.texcoord);
				diffuse.a *= dataIn.color.a;
				diffuse.rgb = (diffuse.rgb * dataIn.color.rgb) * diffuse.a;
				return diffuse;
			}
			ENDCG 
		}
	} 	
 
	Fallback off 
}
