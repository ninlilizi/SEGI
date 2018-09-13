Shader "Hidden/SEGIRenderSunDepth_C" {
Properties {
	_Color ("Main Color", Color) = (1,1,1,1)
	_MainTex ("Base (RGB)", 2D) = "white" {}
	_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.333
}
SubShader 
{
	Pass
	{
	
		CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 5.0
			#pragma multi_compile_instancing
			
			#include "UnityCG.cginc"
				
			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0;
				float3 normal : TEXCOORD1;
				//half4 color : COLOR;

				UNITY_VERTEX_OUTPUT_STEREO
			};

			UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(sampler2D, _MainTex)
			UNITY_DEFINE_INSTANCED_PROP(fixed4, _color)
			UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
			UNITY_INSTANCING_BUFFER_END(Props)

			float4 _MainTex_ST;
			
			
			v2f vert (appdata_full v)
			{
				v2f o;

				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				o.pos = UnityObjectToClipPos(v.vertex);
				
				//float3 pos = o.pos;
				//
				//o.pos.xy = (o.pos.xy);
				//
				//
				o.uv = float4(TRANSFORM_TEX(v.texcoord.xy, _MainTex), 1.0, 1.0);
				o.normal = UnityObjectToWorldNormal(v.normal);
				//
				//o.color = v.color;
				//
				return o;
			}
			
			UNITY_DECLARE_SCREENSPACE_TEXTURE(GILightCookie);
			//sampler2D GILightCookie;
			float4x4 GIProjection;
			
			float4 frag (v2f input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				float depth = UNITY_SAMPLE_SCREENSPACE_TEXTURE(GILightCookie, UnityStereoTransformScreenSpaceTex(input).uv);

				depth = input.pos.z;
				
				return depth;
			}
			
		ENDCG
	}
}

Fallback "Legacy Shaders/VertexLit"
}
