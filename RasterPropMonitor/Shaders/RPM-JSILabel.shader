Shader "RPM/JSILabel"
{
	// Derived from KSP Alpha / Transparent
	Properties
	{
        [Header(Texture Maps)]
		_MainTex("MainTex (RGBA)", 2D) = "white" {}
        _Color("_Color", Color) = (1,1,1,1)
        [Header(Specularity)]
		_SpecColor ("_SpecColor", Color) = (0.5, 0.5, 0.5, 1)
		_Shininess ("_Shininess", Range (0.03, 1)) = 0.078125
        [Header(Transparency)]
		_Opacity("_Opacity", Range(0,1)) = 1
		_Fresnel("_Fresnel", Range(0,10)) = 0
        [Header(Effects)]
		_RimFalloff("Rim Falloff", Range(0.01,5) ) = 0.1
		_RimColor("Rim Color", Color) = (0,0,0,0)
		_TemperatureColor("_TemperatureColor", Color) = (0,0,0,0)
		_BurnColor ("Burn Color", Color) = (1,1,1,1)
		_UnderwaterFogFactor("Underwater Fog Factor", Range(0,1)) = 0
		[Header(RPM)]
		_EmissiveFactor ("_EmissiveFactor", Range(0,1)) = 1
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.35
	}

	SubShader
	{
		Tags {"Queue"="AlphaTest"}

		Pass
		{
			ZWrite On
			ColorMask 0
		}

		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha
		Cull Back

		CGPROGRAM

        #pragma surface surf Lambert alpha
		#pragma target 3.0

		half _Shininess;

		UNITY_DECLARE_TEX2D(_MainTex);

		float _Opacity;
		float _Fresnel;
		float _RimFalloff;
		float4 _RimColor;
		float4 _TemperatureColor;
		float4 _BurnColor;
		float _EmissiveFactor;


		struct Input
		{
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float2 uv_Emissive;
			float3 viewDir;
			float3 worldPos;

			fixed4 color : COLOR;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			float4 color = IN.color * _BurnColor;

			float alpha = UNITY_SAMPLE_TEX2D(_MainTex, (IN.uv_MainTex)).a * IN.color.a;

			float3 normal = float3(0.0,0.0,1.0);
			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));

            float3 fresnel = pow(1 - rim, _Fresnel);

			float3 emission = (_RimColor.rgb * pow(rim, _RimFalloff)) * _RimColor.a;
			emission += _TemperatureColor.rgb * _TemperatureColor.a;

			o.Albedo = color.rgb;
            o.Emission = emission * (1.0 - _EmissiveFactor) + (_EmissiveFactor * color.rgb) * alpha;
			o.Normal = normal;
			o.Emission *= _Opacity;// * fog.a;
			o.Alpha = alpha;
		}

		ENDCG
	}
	Fallback "Standard"
}
