Shader "RPM/FontShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	SubShader 
	{

		Tags { "RenderType"="Overlay" "Queue" = "Transparent" }

		//Lighting Off
		Blend One OneMinusSrcAlpha
		//Cull Off
		//Fog { Mode Off }
		ZWrite Off
		ZTest Always

		// Premultiplied Alpha shader for rendering text on displays.
		Pass 
		{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			struct appdata_t
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f_fontshader
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			UNITY_DECLARE_TEX2D(_MainTex);

			v2f_fontshader vert (appdata_t v)
			{
				v2f_fontshader dataOut;

				// Unfortunately, the original font implementation used a
				// Unity shader that used 0.5 as full brightness, which skews
				// everything.  Doubling alpha appears to fix the problem for
				// both DX and OGL paths.
				dataOut.color = fixed4(v.color.rgb, v.color.a * 2.0);

				dataOut.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				dataOut.texcoord = v.texcoord;
				
				return dataOut;
			}

			fixed4 frag (v2f_fontshader dataIn) : SV_Target
			{
				fixed4 diffuse = UNITY_SAMPLE_TEX2D(_MainTex, dataIn.texcoord);
				diffuse.a *= dataIn.color.a;
				diffuse.rgb = (diffuse.rgb * dataIn.color.rgb) * diffuse.a;
				return diffuse;
			}
			
		ENDCG
		}
	}
}
