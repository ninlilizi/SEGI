// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/VolumeRayCasting" {
	Properties {
		FrontS ("FrontS", 2D) = "white" {}
		BackS ("BackS", 2D) = "white" {}
		//VolumeS ("VolumeS", 3D) = "white" {}
	}
	SubShader {
    Pass {
		//Tags { "RenderType"="Transparent" }
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
		
		sampler2D FrontS;
		sampler2D BackS;
		//sampler3D VolumeS;
		sampler3D SEGIActiveClipmapVolume;
		//float4x4 SEGIShadowCamView;
		//float4 SEGIShadowVector;
		
		// vertex input: position, UV
		struct appdata {
		    float4 vertex : POSITION;
		    float4 texcoord : TEXCOORD0;
		};
		 
			struct v2f {
			    float4 pos : SV_POSITION;
			    float4 uv : TEXCOORD0;
			};

        	float4 FrontS_ST;
        	float4 BackS_ST;	
        	
			v2f vert (appdata v) {

			    v2f o;
			    o.pos = UnityObjectToClipPos(v.vertex);
			    o.uv = UnityObjectToClipPos(v.vertex);
			    return o;
			    
			}
			
			float4 frag( v2f i ) : COLOR {
				
				float2 texC = i.uv.xy /= i.uv.w;
				texC.x = 0.5f*texC.x + 0.5f; 
				texC.y = 0.5f*texC.y + 0.5f;			
				texC.y = 1 - texC.y;
			    float3 front = tex2D(FrontS, texC).rgb;
			    float3 back = tex2D(BackS, texC).rgb;		

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
			
			float3 hsv2rgb(float3 c)
			{
				float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
				float3 p = abs(frac(c.xxx + k.xyz) * 6.0 - k.www);
				return c.z * lerp(k.xxx, saturate(p - k.xxx), c.y);
			}

			float4 DecodeRGBAuint(uint value)
			{
				uint ai = value & 0x0000007F;
				uint vi = (value / 0x00000080) & 0x000007FF;
				uint si = (value / 0x00040000) & 0x0000007F;
				uint hi = value / 0x02000000;

				float h = float(hi) / 127.0;
				float s = float(si) / 127.0;
				float v = (float(vi) / 2047.0) * 10.0;
				float a = ai * 2.0;

				v = pow(v, 3.0);

				float3 color = hsv2rgb(float3(h, s, v));

				return float4(color.rgb, a);
			}

			float4 SEGI_GRID_SIZE;
			Texture2D<uint> RG0;

			float4 fragDepth( v2f i ) : COLOR {
				
				float2 texC = i.uv.xy /= i.uv.w;
				texC.x = 0.5f*texC.x + 0.5f; 
				texC.y = 0.5f*texC.y + 0.5f;			
				texC.y = 1 - texC.y;
			    float3 front = tex2D(FrontS, texC).rgb;
			    float3 back = tex2D(BackS, texC).rgb;		
			    		
			    float3 dir = normalize(back - front);
			    float4 pos = float4(front, 0);
			    			    
			    float4 value = float4(0, 0, 0, 0);
				
				float3 Step = dir * 0.0078125f; // == 1.0f/128.0f // TODO LowRes -> 1.0f/64.0f

				const float4 gridSize = SEGI_GRID_SIZE;

			    //[unroll]
			    for(int i = 0; i < 180; i++)//TODO 180 ?
			    {
					pos.w = 0;
					uint3 coord = pos.xyz * 128;
					uint2 coord2D = uint2(coord.x + gridSize.x*(coord.z%gridSize.w), coord.y + gridSize.y*(coord.z / gridSize.w));

					value = DecodeRGBAuint(RG0[coord2D]); //tex3Dlod(SEGIActiveClipmapVolume, pos); //tex3Dlod(VolumeS, pos);

					//50.0f should really be a settable uniform variable
					if ((value.a * 255.0f) >= 50.0f)
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
