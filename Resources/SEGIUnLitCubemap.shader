// Upgrade NOTE: upgraded instancing buffer 'Props' to new syntax.
Shader "Hidden/SEGIUnLitCubemap"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_MainTex("Texture", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_BumpMap("Normalmap", 2D) = "bump" {}
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile_instancing


			#include "UnityCG.cginc" // for UnityObjectToWorldNormal
			#include "UnityLightingCommon.cginc" // for _LightColor0

			struct appdata
			{
				float4 vertex		: POSITION;
				float3 normal		: NORMAL;
				float4 tangent	    : TANGENT;
				float2 uv			: TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex		 : SV_POSITION;
				//fixed4 diff			 : COLOR0; // diffuse lighting color
				float2 uv			 : TEXCOORD0;
				//half3 worldNormal	 : TEXCOORD1;
				half3 tspace0 : TEXCOORD2; // tangent.x, bitangent.x, normal.x
				half3 tspace1 : TEXCOORD3; // tangent.y, bitangent.y, normal.y
				half3 tspace2 : TEXCOORD4; // tangent.z, bitangent.z, normal.z
				half3 viewDir : TEXCOORD5;
				half3 normalDir : TEXCOORD6;
			};

			fixed4 _Color;
			sampler2D _MainTex;
			sampler2D _BumpMap;
			float4 _MainTex_ST;
			float4 _BumpMap_ST;
			float4 _LightDir;
			int _Glossiness;

			UNITY_INSTANCING_BUFFER_START(Props)
			//UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
			//UNITY_DEFINE_INSTANCED_PROP(int, _Glossiness)
			#define _Color_arr Props
			UNITY_INSTANCING_BUFFER_END(Props)

			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				//o.worldNormal = UnityObjectToWorldNormal(v.normal);

				// get vertex normal in world space
				//half3 worldNormal = UnityObjectToWorldNormal(v.normal);

				// Ambient light color
				//o.diff = _LightColor0;
				//o.diff.rgb += ShadeSH9(half4(worldNormal, 1));
				// Light Direction
				//half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
				//o.diff = nl * _LightColor0;

				o.viewDir = normalize(ObjSpaceViewDir(v.vertex));
				o.normalDir = v.normal;

				half3 wNormal = UnityObjectToWorldNormal(v.normal);
				half3 wTangent = UnityObjectToWorldDir(v.tangent.xyz);
				// compute bitangent from cross product of normal and tangent
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 wBitangent = cross(wNormal, wTangent) * tangentSign;
				// output the tangent space matrix
				o.tspace0 = half3(wTangent.x, wBitangent.x, wNormal.x);
				o.tspace1 = half3(wTangent.y, wBitangent.y, wNormal.y);
				o.tspace2 = half3(wTangent.z, wBitangent.z, wNormal.z);

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				half2 uv_BumpMap = TRANSFORM_TEX(i.uv, _BumpMap);

				half3 tnormal = UnpackNormal(tex2D(_BumpMap, uv_BumpMap));
				// transform normal from tangent to world space
				half3 worldNormal;
				worldNormal.x = dot(i.tspace0, tnormal);
				worldNormal.y = dot(i.tspace1, tnormal);
				worldNormal.z = dot(i.tspace2, tnormal);

				//half diffuse = saturate(dot(_LightDir.xyz, worldNormal) * 1.2);
				half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
				//half nl = max(0, dot(i.worldNormal, _WorldSpaceLightPos0.xyz));

				// This gives Albedo on RGB and Shineyness on Alpha
				//fixed4 col = float4(tex2D(_MainTex, i.uv).rgb, 1 - _Glossiness);
				fixed4 col = tex2D(_MainTex, i.uv);// *diffuse *nl;
				col.rgb *= _Color;
				col.a *= 1 - _Glossiness;
				col *= 1 - (nl * 0.5);
				//col.a *= 1 - nl;

				//fixed4 finalcol = col;

				// multiply by lighting
				//col *= i.diff * pow(1 - saturate(dot(i.viewDir, i.normalDir)), 1);
				//col *= pow(1 - saturate(dot(i.viewDir, i.normalDir)), 1);
				//float alpha = col.a;
				//col *= pow(1 - saturate(dot(i.viewDir, i.normalDir)), 1);
				
				//col.a = alpha;


				//Calculate final specular alpha value		
				//col.a *= _Glossiness * LinearRgbToLuminance(col.rgb);

			return col;
		}
		ENDCG
	}
	}
}