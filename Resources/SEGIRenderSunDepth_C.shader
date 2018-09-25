Shader "Hidden/SEGIRenderSunDepth_C" {
	Properties{
		_Color("Main Color", Color) = (1,1,1,1)
		_MainTex("Base (RGB)", 2D) = "white" {}
		_Cutoff("Alpha Cutoff", Range(0,1)) = 0.333
	}
		SubShader
		{
			Pass
			{

				HLSLPROGRAM

					#pragma vertex vert
					#pragma fragment Frag
					#pragma target 5.0

					#include "PostProcessing/Shaders/StdLib.hlsl"
					#include "SEGI_HLSL_Helpers.cginc"

					TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
					float4 _MainTex_ST;
					float4 _Color;
					float _Cutoff;

					struct AttributesSEGISunDepth
					{
						float4 vertex : POSITION;
						half2 texcoord : TEXCOORD0;
						float2 uvstereo : TEXCOORD1;
						float3 normal : TEXCOORD2;
						half4 color : COLOR;
					};

					struct VaryingsSEGISunDepth
					{
						float4 pos : SV_POSITION;
						float4 uv : TEXCOORD0;
						float2 uvstereo : TEXCOORD1;
						float3 normal : TEXCOORD2;
						half4 color : COLOR;
					};


					VaryingsSEGISunDepth vert(AttributesSEGISunDepth v)
					{
						VaryingsSEGISunDepth o;

						o.pos = UnityObjectToClipPos(v.vertex);

						float3 pos = o.pos.xyz;

						o.pos.xy = (o.pos.xy);


						o.uv = float4(TRANSFORM_TEX(v.texcoord.xy, _MainTex), 1.0, 1.0);
						o.normal = UnityObjectToWorldNormal(v.normal);

						o.uvstereo = TransformStereoScreenSpaceTex(o.uv, 1.0).xy;

						o.color = v.color;

						return o;
					}


					TEXTURE2D_SAMPLER2D(GILightCookie, samplerGILightCookie);
					float4x4 GIProjection;

					float4 Frag(VaryingsSEGISunDepth input) : SV_Target
					{
						float depth = input.pos.z;

						return depth;
					}

				ENDHLSL
			}

		}
}

