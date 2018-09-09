// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/VolumeRayCasting" {
	Properties {
		SEGIFrontS ("SEGIFrontS", 2D) = "white" {}
		SEGIBackS ("SEGIBackS", 2D) = "white" {}
		//VolumeS ("VolumeS", 3D) = "white" {}
	}
	SubShader {
		Tags{ "RenderType" = "RayCastVolume" "ForceNoShadowCasting" = "True" "IgnoreProjector" = "True" }
		Pass {
		//LOD 200
		//Cull Off ZWrite Off Fog { Mode Off }
		//Lighting Off
		//Blend One One
        Fog { Mode Off }
		CGPROGRAM
		
		
		#pragma exclude_renderers flash
		#pragma vertex vert
		#pragma fragment fragDepth
		#pragma target 5.0
		#include "UnityCG.cginc"
		#pragma glsl
		
		sampler2D SEGIFrontS;
		sampler2D SEGIBackS;
		//sampler3D VolumeS;
		sampler3D SEGIActiveClipmapVolume;
		//float4x4 SEGIShadowCamView;
		//float4 SEGIShadowVector;
		
		// vertex input: position, UV

		half4 SEGIActiveClipmapVolume_ST;

		struct appdata {
		    float4 vertex : POSITION;
		    float4 texcoord : TEXCOORD0;
		};
		 
			struct v2f {
			    float4 pos : SV_POSITION;
			    float4 uv : TEXCOORD0;
			};
       	
			v2f vert (appdata v) {

			    v2f o;
			    o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = float4(TRANSFORM_TEX(v.vertex, SEGIActiveClipmapVolume), 1.0, 1.0);
			    return o;
			    
			}
			
			float4 frag( v2f i ) : COLOR {
				
				float2 texC = i.uv.xy /= i.uv.w;
				texC.x = 0.5f*texC.x + 0.5f; 
				texC.y = 0.5f*texC.y + 0.5f;			
				texC.y = 1 - texC.y;
			    float3 front = tex2D(SEGIFrontS, texC).rgb;
			    float3 back = tex2D(SEGIBackS, texC).rgb;

			    float3 dir = normalize(back - front);
			    float4 pos = float4(front, 0);
			    
			    float4 dst = float4(0, 0, 0, 0);
			    float4 src = 0;
			    
			    float4 value = float4(0, 0, 0, 0);
				
				float3 Step = dir * 0.0078125f;
			    
			    //[unroll]
			    for(int i = 0; i < 180; i++)
			    {
					pos.w = 0;
					value = tex3Dlod(SEGIActiveClipmapVolume, pos); //tex3Dlod(VolumeS, pos);
							
					src = (float4)value;
					src.a *= 0.1f; //reduce the alpha to have a more transparent result
								  //this needs to be adjusted based on the step size
								  //i.e. the more steps we take, the faster the alpha will grow	
						
					//Front to back blending
					// dst.rgb = dst.rgb + (1 - dst.a) * src.a * src.rgb;
					// dst.a   = dst.a   + (1 - dst.a) * src.a;
					src.rgb *= src.a;
					dst = (1.0f - dst.a)*src + dst;		
					
					//break from the loop when alpha gets high enough
					if(dst.a >= .95f)
						break;	
					
					//advance the current position
					pos.xyz += Step;
					
					//break if the position is greater than <1, 1, 1>
					if(pos.x > 1.0f || pos.y > 1.0f || pos.z > 1.0f)
						break;
			    }
			    
			    return dst;
			    		    
			  	//return float4(front, 1);
			    //return float4(back - front, .9f);
			}			
			

			float DecodeAuint(uint value)
			{
				//const float div = 1.0f / 255.0f;
				//float alpha = (value >> 24) * div;
				//alpha *= 2.0;
				//return alpha;

				uint ai = value & 0x0000007F;
				float a = ai * 2.0;
				return a;
			}

			float4 SEGI_GRID_SIZE;
			Texture2DArray<uint> SEGIRG0;

			float4 fragDepth( v2f i ) : COLOR {
				
				float2 texC = i.uv.xy /= i.uv.w;
				texC.x = 0.5f*texC.x + 0.5f; 
				texC.y = 0.5f*texC.y + 0.5f;			
				texC.y = 1 - texC.y;
				float3 front = tex2D(SEGIFrontS, texC).rgb;
				float3 back = tex2D(SEGIBackS, texC).rgb;
			    		
			    float3 dir = normalize(back - front);
			    float4 pos = float4(front, 0);
			    			    
			    float value = 0;
				
				float3 Step = dir * 0.0078125f; // == 1.0f/128.0f // TODO LowRes -> 1.0f/64.0f

				const float4 gridSize = SEGI_GRID_SIZE;

			    //[unroll]
			    for(int i = 0; i < 180; i++)//TODO 180 ?
			    {
					pos.w = 0;
					uint3 coord = pos.xyz * 128;
					uint2 coord2D = uint2(coord.x + gridSize.x*(coord.z%gridSize.w), coord.y + gridSize.y*(coord.z / gridSize.w));

					value = DecodeAuint(SEGIRG0[uint3(coord2D, 0)]); //tex3Dlod(SEGIActiveClipmapVolume, pos); //tex3Dlod(VolumeS, pos);

					//50.0f should really be a settable uniform variable
					if ((value * 255.0f) >= 50.0f)
					{
						//tempPos = float4(UnityObjectToViewPos(pos.xyz - 0.5),1);
						float4 tempPos = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(pos.xyz - 0.5,1)));
						float z = tempPos.z * _ProjectionParams.w;
						return z;
					}

					//advance the current position
					pos.xyz += Step;
					
					//break if the position is greater than <1, 1, 1>
					if(pos.x > 1.0f || pos.y > 1.0f || pos.z > 1.0f)
						break;
			    }
			    
			    return 0;

			}
		
		ENDCG
		}
	} 
	FallBack "Diffuse"
}
