Shader "Hidden/SEGITraceScene_C" {
	Properties
	{
		//_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
		//_EmissionColor("Color", Color) = (0,0,0)
		//_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.333
	}
	SubShader 
	{
		Cull Off
		ZTest Always
		
		Pass
		{
			CGPROGRAM
			
				#pragma target 5.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma geometry geom
				#pragma multi_compile_instancing
				#include "UnityCG.cginc"
				#include "SEGI_C.cginc"
				
				RWTexture2D<uint> RG0;
								
				float4x4 SEGIVoxelViewFront;
				float4x4 SEGIVoxelViewLeft;
				float4x4 SEGIVoxelViewTop;
				
				float4 _MainTex_ST;
				//half4 _EmissionColor;
				//float _Cutoff;
				
				struct v2g
				{
					float4 pos : SV_POSITION;
					half4 uv : TEXCOORD0;
					float3 normal : TEXCOORD1;
					float angle : TEXCOORD2;

					UNITY_VERTEX_OUTPUT_STEREO
				};
				
				struct g2f
				{
					float4 pos : SV_POSITION;
					half4 uv : TEXCOORD0;
					float3 normal : TEXCOORD1;
					float angle : TEXCOORD2;

					UNITY_VERTEX_OUTPUT_STEREO
				};
				
				//half4 _Color;
				
				v2g vert(appdata_full v)
				{
					v2g o;

					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_OUTPUT(v2g, o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					
					float4 vertex = v.vertex;
					
					o.normal = UnityObjectToWorldNormal(v.normal);
					//float3 absNormal = abs(o.normal);
					
					o.pos = vertex;
					
					o.uv = float4(TRANSFORM_TEX(v.texcoord.xy, _MainTex), 1.0, 1.0);
					
					return o;
				}
				
				int SEGIVoxelResolution;
				
				[maxvertexcount(3)]
				void geom(triangle v2g input[3], inout TriangleStream<g2f> triStream)
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
					UNITY_SETUP_INSTANCE_ID(input);
					v2g p[3];
					int i = 0;
					
					for (i = 0; i < 3; i++)
					{
						p[i] = input[i];
						p[i].pos = mul(unity_ObjectToWorld, p[i].pos);						
					}
					
					float3 realNormal = float3(0.0, 0.0, 0.0);
					
					float3 V = p[1].pos.xyz - p[0].pos.xyz;
					float3 W = p[2].pos.xyz - p[0].pos.xyz;
					
					realNormal.x = (V.y * W.z) - (V.z * W.y);
					realNormal.y = (V.z * W.x) - (V.x * W.z);
					realNormal.z = (V.x * W.y) - (V.y * W.x);
					
					float3 absNormal = abs(realNormal);
					

					
					int angle = 0;
					if (absNormal.z > absNormal.y && absNormal.z > absNormal.x)
					{
						angle = 0;
					}
					else if (absNormal.x > absNormal.y && absNormal.x > absNormal.z)
					{
						angle = 1;
					}
					else if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
					{
						angle = 2;
					}
					else
					{
						angle = 0;
					}
					
					for (i = 0; i < 3; i ++)
					{
						if (angle == 0)
						{
							p[i].pos = mul(SEGIVoxelViewFront, p[i].pos);					
						}
						else if (angle == 1)
						{
							p[i].pos = mul(SEGIVoxelViewLeft, p[i].pos);					
						}
						else
						{
							p[i].pos = mul(SEGIVoxelViewTop, p[i].pos);		
						}
						
						p[i].pos = mul(UNITY_MATRIX_P, p[i].pos);
						

						#if defined(UNITY_REVERSED_Z)
						p[i].pos.z = 1.0 - p[i].pos.z;
						#else
						p[i].pos.z *= -1.0;
						#endif
						
						p[i].angle = (float)angle;
					}
					
					triStream.Append(p[0]);
					triStream.Append(p[1]);
					triStream.Append(p[2]);
				}
				
				#define VoxelResolution (SEGIVoxelResolution)

				float4 frag (g2f input) : SV_TARGET
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
					UNITY_SETUP_INSTANCE_ID(input);

					int3 coord = int3((int)(input.pos.x), (int)(input.pos.y), (int)(input.pos.z * VoxelResolution));
					
					int angle = 0;
					
					angle = (int)input.angle;
					
					if (angle == 1)
					{
						coord.xyz = coord.zyx;
						coord.z = VoxelResolution - coord.z - 1;
					}
					else if (angle == 2)
					{
						coord.xyz = coord.xzy;
						coord.y = VoxelResolution - coord.y - 1;
					}
					
					float3 fcoord = (float3)coord.xyz / VoxelResolution;

					float3 minCoord = (SEGIClipmapOverlap.xyz * 1.0 + 0.5) - SEGIClipmapOverlap.w * 0.5;
					minCoord += 16.0 / VoxelResolution;
					float3 maxCoord = (SEGIClipmapOverlap.xyz * 1.0 + 0.5) + SEGIClipmapOverlap.w * 0.5;
					maxCoord -= 16.0 / VoxelResolution;

					if (fcoord.x > minCoord.x && fcoord.x < maxCoord.x &&
						fcoord.y > minCoord.y && fcoord.y < maxCoord.y &&
						fcoord.z > minCoord.z && fcoord.z < maxCoord.z)
					{
						discard;
					}

					float3 gi = (0.0).xxx;
					
					float3 worldNormal = input.normal;
					
					float3 voxelOrigin = (fcoord + worldNormal.xyz * 0.006 * 1.0);
					
					float4 traceResult = float4(0,0,0,0);
					
					float2 dither = rand(fcoord);
					
					const float phi = 1.618033988;
					const float gAngle = phi * PI * 2.0;
					
					
					const int numSamples = SEGISecondaryCones;
					for (int i = 0; i < numSamples; i++)
					{
						float fi = (float)i; 
						float fiN = fi / numSamples;
						float longitude = gAngle * fi;
						float latitude = asin(fiN * 2.0 - 1.0);
						
						float3 kernel;
						kernel.x = cos(latitude) * cos(longitude);
						kernel.z = cos(latitude) * sin(longitude);
						kernel.y = sin(latitude);
						
						kernel = normalize(kernel + worldNormal.xyz);

						if (i == 0)
						{
							kernel = float3(0.0, 1.0, 0.0);
						}

							traceResult += ConeTrace(voxelOrigin.xyz, kernel.xyz, worldNormal.xyz);
					}
					
					traceResult /= numSamples;
					
					
					gi.rgb = traceResult.rgb;
					
					gi.rgb *= 4.3;
					
					gi.rgb += traceResult.a * 1.0 * SEGISkyColor;

					
					float4 result = float4(gi.rgb, 2.0);

					const float4 gridSize = SEGI_GRID_SIZE;
					uint2 coord2D = uint2(coord.x + gridSize.x*(coord.z%gridSize.w), coord.y + gridSize.y*(coord.z / gridSize.w));

					interlockedAddFloat4(RG0, coord2D, result);
					
					return float4(0.0, 0.0, 0.0, 0.0);
				}
			
			ENDCG
		}
	} 
	FallBack Off
}
