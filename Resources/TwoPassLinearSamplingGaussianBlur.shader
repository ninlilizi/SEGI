Shader "hidden/two_pass_linear_sampling_gaussian_blur" 
{
	CGINCLUDE
	#include "UnityCG.cginc"
	#pragma multi_compile LITTLE_KERNEL MEDIUM_KERNEL BIG_KERNEL
	#pragma multi_compile_instancing
	#include "GaussianBlur.cginc"
	
	//uniform sampler2D _MainTex;
	uniform float4 _MainTex_TexelSize;
	uniform sampler2D _GrabTexture;
	uniform float4 _GrabTexture_TexelSize;
	uniform float _Sigma;
	
	UNITY_INSTANCING_BUFFER_START(Props)
	UNITY_DEFINE_INSTANCED_PROP(sampler2D, _MainTex)
	UNITY_INSTANCING_BUFFER_END(Props)

	float4 frag_horizontal (v2f_img i) : COLOR
	{
		UNITY_SETUP_INSTANCE_ID(i)

		pixel_info pinfo;
		pinfo.tex = UNITY_ACCESS_INSTANCED_PROP(Props, _MainTex);
		pinfo.uv = UnityStereoTransformScreenSpaceTex(i.uv);
		pinfo.texelSize = _MainTex_TexelSize;
		return GaussianBlurLinearSampling(pinfo, _Sigma, float2(1,0));
	}
	
	float4 frag_vertical (v2f_img i) : COLOR
	{				
		UNITY_SETUP_INSTANCE_ID(i)

		pixel_info pinfo;
		pinfo.tex = _GrabTexture;
		pinfo.uv = UnityStereoTransformScreenSpaceTex(i.uv);
		pinfo.texelSize = _GrabTexture_TexelSize;
		return GaussianBlurLinearSampling(pinfo, _Sigma, float2(0,1));
	}
	ENDCG
	
	Properties 
	{ 
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	SubShader 
	{
		Tags { "Queue" = "Overlay" }
		Lighting Off 
		Cull Off 
		ZWrite Off 
		ZTest Always 

	    Pass
		{
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert_img
			#pragma fragment frag_horizontal
			ENDCG
		}
		
		GrabPass{}
		
		Pass
		{
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vert_img
			#pragma fragment frag_vertical
			ENDCG
		}
	}
}