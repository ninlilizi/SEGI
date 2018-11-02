Shader "Hidden/SEGI_C" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	HLSLINCLUDE
	#include "HLSLSupport.cginc"
	#include "PostProcessing/Shaders/StdLib.hlsl"
	#include "SEGI_HLSL_Helpers.cginc"
	#include "SEGI_C.cginc"
	#pragma target 5.0

	struct AttributesSEGI
	{
		float4 vertex	 : POSITION;
		float3 normal    : NORMAL;
		half2 texcoord   : TEXCOORD0;
		float4 screenPos : TEXCOORD1;
	};

	struct VaryingsSEGI
	{
		float4 vertex	 : SV_POSITION;
		float2 texcoord  : TEXCOORD0;
		float4 screenPos : TEXCOORD1;
		half3 normal     : TEXCOORD2;
	};

	VaryingsSEGI VertSEGI(AttributesSEGI v)
	{
		if (StereoEnabled)
		{
			VaryingsSEGI o;
			o.vertex = float4(v.texcoord.x - 0.5, v.texcoord.y + 0.5, 0, 0.5);
			//o.texcoord = float4(v.texcoord.x, v.texcoord.y, 0, 1);
			o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
			//o.texcoordstereo = o.texcoord;// UnityStereoScreenSpaceUVAdjust(o.texcoord, float4(1, 1, 0, 0));
			//o.texcoordstereo = TransformStereoScreenSpaceTex(o.texcoord, 1.0);

			#if UNITY_UV_STARTS_AT_TOP
					o.vertex.y = 1 - o.vertex.y;
			#endif

			o.normal = v.normal;
			o.screenPos = ComputeScreenPos(mul(UNITY_MATRIX_MVP, v.vertex));

			return o;
		}
		else
		{
			VaryingsSEGI o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.texcoord = float4(v.texcoord.xy, 1, 1);
			//o.texcoordstereo = TransformStereoScreenSpaceTex(o.texcoord, 1.0);

			#if UNITY_UV_STARTS_AT_TOP
				//o.uv2 = float4(v.texcoord.xy, 1, 1);
				if (_MainTex_TexelSize.y < 0.0)
				o.texcoord.y = 1.0 - o.texcoord.y;
			#endif

			o.normal = v.normal;
			o.screenPos = ComputeScreenPos(mul(UNITY_MATRIX_MVP, v.vertex));

			return o;
		}
	}
	ENDHLSL


		SubShader
	{
		ZTest Off
		Cull Off
		ZWrite Off
		Fog { Mode off }

		Pass //0
		{
			HLSLPROGRAM
			#pragma vertex VertSEGI
			#pragma fragment Frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_instancing
			#if defined (VRWORKS)
				#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
			#endif

			int FrameSwitch;


			sampler2D NoiseTexture;

			float4 Frag(VaryingsSEGI input) : SV_Target
			{
				float2 coord = input.texcoord.xy;
				float2 uv = input.texcoord;

				//Get view space position and view vector
				float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
				float3 viewVector = normalize(viewSpacePosition.xyz);
				float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

				//Get voxel space position
				float4 voxelSpacePosition = mul(CameraToWorld, viewSpacePosition);
				voxelSpacePosition = mul(SEGIWorldToVoxel0, voxelSpacePosition);
				voxelSpacePosition = mul(SEGIVoxelProjection0, voxelSpacePosition);
				voxelSpacePosition.xyz = voxelSpacePosition.xyz * 0.5 + 0.5;

				//Prepare for cone trace

				float3 worldNormal;
				if (ForwardPath) worldNormal = GetWorldNormal(coord).rgb;
				else worldNormal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, coord).rgb * 2.0 - 1.0);

				float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.003 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;

				float3 gi = float3(0.0, 0.0, 0.0);
				float3 traceResult = float3(0, 0, 0);

				const float phi = 1.618033988;
				const float gAngle = phi * PI * 1.0;

				//Get blue noise
				float2 noiseCoord = (input.texcoord.xy * _MainTex_TexelSize.zw) / (64.0).xx;
				float4 blueNoise = tex2Dlod(NoiseTexture, float4(noiseCoord, 0.0, 0.0));

				float depth = GetDepthTextureTraceCache(uv);
				blueNoise *= (1 - GetDepthTexture(uv)) * 50;
				int tracedCount = tracedTextureA0[uint3(input.screenPos.x, input.screenPos.y, 1)];;

				//Trace GI cones
				int numSamples = TraceDirections;
				uint3 voxelCoord = float3(0, 0, 0);
				uint voxelDepth;
				float latitude;
				float longitude;

				for (int i = 0; i < numSamples; i++)
				{
					float fi;
					if (i > 1) fi = (float)tracedCount + i + blueNoise.x * StochasticSampling;
					else fi = (float)i * StochasticSampling;
					float fiN = fi / numSamples;
					longitude = gAngle * fi;
					latitude = asin(fiN * 2.0 - 1.0);

					float3 kernel;
					kernel.x = cos(latitude) * cos(longitude);
					kernel.z = cos(latitude) * sin(longitude);
					kernel.y = sin(latitude);

					kernel = normalize(kernel + worldNormal.xyz * 1.0);

					traceResult += ConeTrace(voxelOrigin.xyz, kernel.xyz, worldNormal.xyz, coord, 0, TraceSteps, ConeSize, 1.0, 1.0, depth, voxelDepth);

					voxelCoord = float3(voxelOrigin.x + kernel.x  * SEGITraceCacheScaleFactor + (blueNoise.x * 32), voxelOrigin.y + kernel.y * SEGITraceCacheScaleFactor + (blueNoise.y * 32), depth * voxelDepth + (blueNoise.z * 32));
				}

				traceResult /= numSamples;

				if (tracedCount < 128)
				{
					tracedTexture1[uint3(voxelCoord)] += float4(traceResult.rgb, 0);// *0.004;
					tracedTextureA0[uint3(input.screenPos.x, input.screenPos.y, 1)]++;
				}
				
				traceResult.rgb = lerp(tracedTexture0[uint3(voxelCoord)].rgb, traceResult.rgb, 0.5);
				//traceResult.rgb = tracedTexture0[uint3(voxelCoord)].rgb;
				//traceResult.rgb = tracedTexture1[uint3(voxelCoord)].rgb / 64;

				gi = traceResult.rgb * 1.18;

				return float4(gi, 1.0);
			}
			ENDHLSL
		}

			Pass //1 Bilateral Blur
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					float2 Kernel;

					float DepthTolerance;

					TEXTURE2D_SAMPLER2D(DepthNormalsLow, samplerDepthNormalsLow);
					TEXTURE2D_SAMPLER2D(DepthLow, samplerDepthLow);
					int SourceScale;

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						/*#if UNITY_UV_STARTS_AT_TOP
							float2 coord = input.uv2.xy;
							float2 uv = input.uv2;
						#else*/
							float2 coord = input.texcoord.xy;
							float2 uv = input.texcoord;
						//#endif

						float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
						float validWeights = 0.0;
						#if defined (VRWORKS)				
							float depth = LinearEyeDepth(tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(iv).xy).x);
						#else
							float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, coord).x);
						#endif
						half3 normal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, coord).rgb * 2.0 - 1.0);
						float thresh = 0.26;

						float3 viewPosition = GetViewSpacePosition(coord, uv).xyz;
						float3 viewVector = normalize(viewPosition);

						float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
						thresh *= 1.0 + NdotV * 2.0;

						for (int i = -4; i <= 4; i++)
						{
							float2 offs = Kernel.xy * (i)* _MainTex_TexelSize.xy * 1.0;
							#if defined (VRWORKS)
								float sampleDepth = LinearEyeDepth(tex2Dlod(VRWorksGetDepthSampler(), VRWorksRemapUV(iv).xy + offs.xy * 1, 0).x);
							#else
								float sampleDepth = LinearEyeDepth(tex2Dlod(_CameraDepthTexture, float4(input.texcoord.xy + offs.xy * 1, 0, 0)).x);
							#endif
							half3 sampleNormal = normalize(SAMPLE_TEXTURE2D_LOD(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, coord + offs.xy * 1, 0).rgb * 2.0 - 1.0);

							float weight = saturate(1.0 - abs(depth - sampleDepth) / thresh);
							weight *= pow(saturate(dot(sampleNormal, normal)), 24.0);

							float4 blurSample = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(coord + offs.xy), 0).rgba;
							blurred += blurSample * weight;
							validWeights += weight;
						}

						blurred /= validWeights + 0.001;

						return blurred;
					}

				ENDHLSL
			}

			Pass //2 Blend with scene
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					TEXTURE2D_SAMPLER2D(GITexture, samplerGITexture);
					TEXTURE2D_SAMPLER2D(Reflections, samplerReflections);

					int DoReflections = 1;

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						/*#if UNITY_UV_STARTS_AT_TOP
							float2 coord = input.uv2.xy;
							float2 uv = input.uv2;
						#else*/
							float2 coord = input.texcoord.xy;
							float2 uv = input.texcoord;
						//#endif

						float4 albedoTex;
						float3 albedo;
						float3 gi = SAMPLE_TEXTURE2D(GITexture, samplerGITexture, coord).rgb;
						float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coord).rgb;
						float3 reflections = SAMPLE_TEXTURE2D(Reflections, samplerReflections, coord).rgb;

						float3 result;
						float smoothness;
						if (ForwardPath)
						{
							float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
							float4 worldViewVector = mul(CameraToWorld, float4(viewSpacePosition.xyz, 0.0));
							half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(_SEGICube, worldViewVector.xyz, 0);
							albedoTex = float4(DecodeHDR(probeData, _SEGICube_HDR), probeData.a);
							albedo = albedoTex.rgb;
							//smoothness = 1 - probeData.a;


							result = scene + gi.rgb *  albedoTex.a * albedoTex.rgb;
							//result += min(lerp(albedoTex.rgb, gi.rgb, albedoTex.a * 0.5 + 0.5), result) * 2;
						}
						else
						{
							albedoTex = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, coord);
							albedo = albedoTex.rgb;

							result = scene + gi.rgb * albedoTex.a * albedoTex.rgb;
						}


						if (DoReflections > 0)
						{
							float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
							float3 viewVector = normalize(viewSpacePosition.xyz);
							float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

							
							float3 specularColor;
							float3 worldNormal;
							if (ForwardPath) worldNormal = GetWorldNormal(coord);
							else worldNormal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, coord).rgb * 2.0 - 1.0);
							float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

							if (ForwardPath)
							{
								//half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(_SEGICube, reflectionKernel, 0);
								//half3 probeColor = DecodeHDR(probeData, _SEGICube_HDR);
								specularColor = albedoTex.rgb;
								smoothness = 1 - albedoTex.a;
							}
							else
							{
								smoothness = SAMPLE_TEXTURE2D(_CameraGBufferTexture1, sampler_CameraGBufferTexture1, coord).a * 0.5;
								specularColor = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, coord).rgb;
							}

							float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
							fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

							fresnel *= saturate(smoothness * 4.0);

							result = lerp(result, reflections, fresnel);

							//return float4(fresnel, 1.0);
						}

						return float4(result, 1.0);
					}

				ENDHLSL
			}

			Pass //3 Temporal blend (with unity motion vectors)
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					TEXTURE2D_SAMPLER2D(GITexture, samplerGITexture);
					TEXTURE2D_SAMPLER2D(PreviousDepth, samplerPreviousDepth);
					TEXTURE2D_SAMPLER2D(CurrentDepth, samplerCurrentDepth);
					TEXTURE2D_SAMPLER2D(PreviousLocalWorldPos, samplerPreviousLocalWorldPos);


					float4 CameraPosition;
					float4 CameraPositionPrev;
					float4x4 ProjectionPrev;
					float4x4 ProjectionPrevInverse;
					float4x4 WorldToCameraPrev;
					float4x4 CameraToWorldPrev;
					float DeltaTime;
					float BlendWeight;

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						float3 gi = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord.xy).rgb;

						float depth = GetDepthTexture(input.texcoord.xy);

						float4 currentPos = float4(input.texcoord.x * 2.0 - 1.0, input.texcoord.y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

						float4 fragpos = mul(ProjectionMatrixInverse, currentPos);
						fragpos = mul(CameraToWorld, fragpos);
						fragpos /= fragpos.w;
						float4 thisWorldPosition = fragpos;

						float2 motionVectors = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, float4(input.texcoord.xy, 0.0, 0.0)).xy;
						float2 reprojCoord = input.texcoord.xy - motionVectors.xy;

						float prevDepth = (SAMPLE_TEXTURE2D(PreviousDepth, samplerPreviousDepth, float4(reprojCoord + _MainTex_TexelSize.xy * 0.0, 0.0, 0.0)).x);
						#if defined(UNITY_REVERSED_Z)
						prevDepth = 1.0 - prevDepth;
						#endif

						float4 previousWorldPosition = mul(ProjectionPrevInverse, float4(reprojCoord.xy * 2.0 - 1.0, prevDepth * 2.0 - 1.0, 1.0));
						previousWorldPosition = mul(CameraToWorldPrev, previousWorldPosition);
						previousWorldPosition /= previousWorldPosition.w;

						float blendWeight = BlendWeight;

						float posSimilarity = saturate(1.0 - distance(previousWorldPosition.xyz, thisWorldPosition.xyz) * 1.0);
						blendWeight = lerp(1.0, blendWeight, posSimilarity);

						float3 minPrev = float3(10000, 10000, 10000);
						float3 maxPrev = float3(0, 0, 0);

						float3 s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoord.xy + _MainTex_TexelSize.xy * float2(0.5, 0.5), 0, 0)).rgb;
						minPrev = s0;
						maxPrev = s0;
						s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoord.xy + _MainTex_TexelSize.xy * float2(0.5, -0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);
						s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoord.xy + _MainTex_TexelSize.xy * float2(-0.5, 0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);
						s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoord.xy + _MainTex_TexelSize.xy * float2(-0.5, -0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);

						float3 prevGI = SAMPLE_TEXTURE2D(PreviousGITexture, samplerPreviousGITexture, float4(reprojCoord, 0.0, 0.0)).rgb;
						prevGI = lerp(prevGI, clamp(prevGI, minPrev, maxPrev), 0.25);

						gi = lerp(prevGI, gi, float3(blendWeight, blendWeight, blendWeight));

						float3 result = gi;
						return float4(result, 1.0);
					}

				ENDHLSL
			}

			Pass //4 Specular/reflections trace
			{
				ZTest Always

				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					//UNITY_DECLARE_TEX3D(SEGIVolumeTexture1);

					int FrameSwitch;


					float4 Frag(VaryingsSEGI input) : SV_Target
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						/*#if UNITY_UV_STARTS_AT_TOP
							float2 coord = input.uv2.xy;
							float2 uv = input.uv2;
						#else*/
							float2 coord = input.texcoord.xy;
							float2 uv = input.texcoord;
						//#endif

						float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
						float3 viewVector = normalize(viewSpacePosition.xyz);
						float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));


						float4 voxelSpacePosition = mul(CameraToWorld, viewSpacePosition);
						float3 worldPosition = voxelSpacePosition.xyz;
						//Get voxel space position
						voxelSpacePosition = mul(SEGIWorldToVoxel0, voxelSpacePosition);
						voxelSpacePosition = mul(SEGIVoxelProjection0, voxelSpacePosition);
						voxelSpacePosition.xyz = voxelSpacePosition.xyz * 0.5 + 0.5;

						float3 worldNormal;
						if (ForwardPath) worldNormal = GetWorldNormal(coord);
						else worldNormal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, input.texcoord).rgb * 2.0 - 1.0);

						float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.006 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;

						//float2 dither = rand(coord + (float)FrameSwitch * 0.11734);

						float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

						float smoothness;
						float3 specularColor;
						if (ForwardPath)
						{
							/*half4 probeData = UNITY_SAMPLE_TEXCUBE(_SEGICube, reflectionKernel);
							half3 probeColor = DecodeHDR(probeData, _SEGICube_HDR);
							specularColor = probeColor.rgb;
							smoothness = probeData.a;*/

							//float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
							//float4 worldViewVector = mul(CameraToWorld, float4(viewSpacePosition.xyz, 0.0));
							half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(_SEGICube, reflectionKernel, 0);
							specularColor = DecodeHDR(probeData, _SEGICube_HDR);
							smoothness = 1 - probeData.a * 0.5 - 0.25;
						}
						else
						{
							smoothness = SAMPLE_TEXTURE2D(_CameraGBufferTexture1, sampler_CameraGBufferTexture1, coord).a * 0.5;
							specularColor = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, coord).rgb;
						}

						float4 reflection = (0.0).xxxx;

						float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
						fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

						voxelOrigin += worldNormal.xyz * 0.002 * 1.25 / SEGIVoxelScaleFactor;
						reflection = SpecularConeTrace(voxelOrigin.xyz, reflectionKernel.xyz, worldNormal.xyz, smoothness, coord, 0);

						float3 skyReflection = (reflection.a * 1.0 * SEGISkyColor);

						reflection.rgb = reflection.rgb * 0.7 + skyReflection.rgb * 2.4015 * SkyReflectionIntensity;

						return float4(reflection.rgb, 1.0);
					}

				ENDHLSL
			}

			Pass //5 Get camera depth texture
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						#if defined (VRWORKS)
							float4 tex = tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(input.texcoord).xy);
						#else
							float4 tex = tex2D(_CameraDepthTexture, input.texcoord.xy);
						#endif
						return tex;
					}

				ENDHLSL
			}

			Pass //6 Get camera normals texture
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						float2 coord = input.texcoord.xy;
						#if defined(VRWORKS)
							float4 tex = SAMPLE_TEXTURE2D(VRWorksGetDepthNormalsSampler(), sampler_CameraDepthNormalsTexture, VRWorksRemapUV((input.texcoord).xy);
						#else
							float4 tex = SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture, sampler_CameraDepthNormalsTexture, input.texcoord.xy);
						#endif
						return tex;
					}

				ENDHLSL
			}


			Pass //7 Visualize GI
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					TEXTURE2D_SAMPLER2D(GITexture, samplerGITexture);

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						//float4 albedoTex = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, input.texcoord.xy);
						//float3 albedo = albedoTex.rgb;
						float3 gi = SAMPLE_TEXTURE2D(GITexture, samplerGITexture, input.texcoord.xy).rgb;
						return float4(gi, 1.0);
					}

				ENDHLSL
			}



			Pass //8 Write black
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						return float4(0.0, 0.0, 0.0, 1.0);
					}

				ENDHLSL
			}

			Pass //9 Visualize slice of GI Volume (CURRENTLY UNUSED)
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					float LayerToVisualize;
					int MipLevelToVisualize;

					//TEXTURE3D_SAMPLER3D(SEGIVolumeTexture1, );

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						return float4(tex3D(SEGIVolumeTexture1, float3(input.texcoord.xy, LayerToVisualize)).rgb, 1.0);
					}

				ENDHLSL
			}


	Pass //10 Visualize voxels (trace through GI volumes)
	{
ZTest Always
	
		HLSLPROGRAM
			#pragma vertex VertSEGI
			#pragma fragment Frag
			
			//float4x4 CameraToWorld;
			
			//sampler2D _CameraGBufferTexture2;
			
			float4 CameraPosition;
			
			float4 Frag(VaryingsSEGI input) : SV_Target
			{
				float2 coord = input.texcoord.xy;
				float2 uv = input.texcoord;
				
				float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
				float3 viewVector = normalize(viewSpacePosition.xyz);
				float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

				float4 voxelCameraPosition0 = mul(SEGIWorldToVoxel0, float4(CameraPosition.xyz, 1.0));
					   voxelCameraPosition0 = mul(SEGIVoxelProjection0, voxelCameraPosition0);
					   voxelCameraPosition0.xyz = voxelCameraPosition0.xyz * 0.5 + 0.5;

				float3 voxelCameraPosition1 = TransformClipSpace1(voxelCameraPosition0);
				float3 voxelCameraPosition2 = TransformClipSpace2(voxelCameraPosition0);
				float3 voxelCameraPosition3 = TransformClipSpace3(voxelCameraPosition0);
				float3 voxelCameraPosition4 = TransformClipSpace4(voxelCameraPosition0);
				float3 voxelCameraPosition5 = TransformClipSpace5(voxelCameraPosition0);


				float4 result = float4(0,0,0,1);
				float4 trace;


				trace = VisualConeTrace(voxelCameraPosition0.xyz, worldViewVector.xyz, 1.0, 0);
				result.rgb += trace.rgb;
				result.a *= trace.a;

				trace = VisualConeTrace(voxelCameraPosition1.xyz, worldViewVector.xyz, result.a, 1);
				result.rgb += trace.rgb;
				result.a *= trace.a;

				trace = VisualConeTrace(voxelCameraPosition2.xyz, worldViewVector.xyz, result.a, 2);
				result.rgb += trace.rgb;
				result.a *= trace.a;	

				trace = VisualConeTrace(voxelCameraPosition3.xyz, worldViewVector.xyz, result.a, 3);
				result.rgb += trace.rgb;
				result.a *= trace.a;

				trace = VisualConeTrace(voxelCameraPosition4.xyz, worldViewVector.xyz, result.a, 4);
				result.rgb += trace.rgb;
				result.a *= trace.a;

				trace = VisualConeTrace(voxelCameraPosition5.xyz, worldViewVector.xyz, result.a, 5);
				result.rgb += trace.rgb;  
				result.a *= trace.a;
				
				return float4(result.rgb, 1.0);
			}
			
		ENDHLSL
	}

			Pass //11 Bilateral upsample
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					float2 Kernel;

					float DepthTolerance;

					TEXTURE2D_SAMPLER2D(DepthNormalsLow, samplerDepthNormalsLow);
					TEXTURE2D_SAMPLER2D(DepthLow, samplerDepthLow);
					int SourceScale;
					TEXTURE2D_SAMPLER2D(CurrentDepth, samplerCurrentDepth);
					TEXTURE2D_SAMPLER2D(CurrentNormal, samplerCurrentNormal);


					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						/*#if UNITY_UV_STARTS_AT_TOP
							float2 coord = input.uv2.xy;
							float2 uv = input.uv2;
						#else*/
							float2 coord = input.texcoord.xy;
							float2 uv = input.texcoord;
						//#endif

						float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
						float4 blurredDumb = float4(0.0, 0.0, 0.0, 0.0);
						float validWeights = 0.0;
						#if defined(VRWORKS)
							float depth = LinearEyeDepth(tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(input.texcoord).xy).x);
							half3 normal = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(VRWorksGetDepthNormalsSampler(), sampler_CameraDepthNormalsTexture, VRWorksRemapUV(input.texcoord).xy));
						#else
							float depth = LinearEyeDepth(tex2D(_CameraDepthTexture, coord).x);
							half3 normal = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture, sampler_CameraDepthNormalsTexture, coord));
						#endif												
						float thresh = 0.26;

						float3 viewPosition = GetViewSpacePosition(coord, uv).xyz;
						float3 viewVector = normalize(viewPosition);

						float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
						thresh *= 1.0 + NdotV * 2.0;

						float4 sample00 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(coord + _MainTex_TexelSize.xy * float2(0.0, 0.0) * 1.0, 0.0, 0.0));
						float4 sample10 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(coord + _MainTex_TexelSize.xy * float2(1.0, 0.0) * 1.0, 0.0, 0.0));
						float4 sample11 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(coord + _MainTex_TexelSize.xy * float2(1.0, 1.0) * 1.0, 0.0, 0.0));
						float4 sample01 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(coord + _MainTex_TexelSize.xy * float2(0.0, 1.0) * 1.0, 0.0, 0.0));

						float4 depthSamples = float4(0,0,0,0);
						depthSamples.x = LinearEyeDepth(SAMPLE_TEXTURE2D(CurrentDepth, samplerCurrentDepth, float4(coord + _MainTex_TexelSize.xy * float2(0.0, 0.0), 0, 0)).x);
						depthSamples.y = LinearEyeDepth(SAMPLE_TEXTURE2D(CurrentDepth, samplerCurrentDepth, float4(coord + _MainTex_TexelSize.xy * float2(1.0, 0.0), 0, 0)).x);
						depthSamples.z = LinearEyeDepth(SAMPLE_TEXTURE2D(CurrentDepth, samplerCurrentDepth, float4(coord + _MainTex_TexelSize.xy * float2(1.0, 1.0), 0, 0)).x);
						depthSamples.w = LinearEyeDepth(SAMPLE_TEXTURE2D(CurrentDepth, samplerCurrentDepth, float4(coord + _MainTex_TexelSize.xy * float2(0.0, 1.0), 0, 0)).x);

						half3 normal00 = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(CurrentNormal, samplerCurrentNormal, coord + _MainTex_TexelSize.xy * float2(0.0, 0.0)));
						half3 normal10 = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(CurrentNormal, samplerCurrentNormal, coord + _MainTex_TexelSize.xy * float2(1.0, 0.0)));
						half3 normal11 = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(CurrentNormal, samplerCurrentNormal, coord + _MainTex_TexelSize.xy * float2(1.0, 1.0)));
						half3 normal01 = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(CurrentNormal, samplerCurrentNormal, coord + _MainTex_TexelSize.xy * float2(0.0, 1.0)));

						float4 depthWeights = saturate(1.0 - abs(depthSamples - depth.xxxx) / thresh);

						float4 normalWeights = float4(0,0,0,0);
						normalWeights.x = pow(saturate(dot(normal00, normal)), 24.0);
						normalWeights.y = pow(saturate(dot(normal10, normal)), 24.0);
						normalWeights.z = pow(saturate(dot(normal11, normal)), 24.0);
						normalWeights.w = pow(saturate(dot(normal01, normal)), 24.0);

						float4 weights = depthWeights * normalWeights;

						float weightSum = dot(weights, float4(1.0, 1.0, 1.0, 1.0));

						if (weightSum < 0.01)
						{
							weightSum = 4.0;
							weights = (1.0).xxxx;
						}

						weights /= weightSum;

						float2 fractCoord = frac(coord * _MainTex_TexelSize.zw * 1.0);

						float4 filteredX0 = lerp(sample00 * weights.x, sample10 * weights.y, fractCoord.x);
						float4 filteredX1 = lerp(sample01 * weights.w, sample11 * weights.z, fractCoord.x);

						float4 filtered = lerp(filteredX0, filteredX1, fractCoord.y);


						return filtered * 3.0;

						return blurred;
					}

				ENDHLSL
			}

			Pass //12 Temporal blending without motion vectors (for legacy support)
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma fragmentoption ARB_precision_hint_fastest
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif

					TEXTURE2D_SAMPLER2D(GITexture, samplerGITexture);
					TEXTURE2D_SAMPLER2D(PreviousDepth, samplerPreviousDepth);
					TEXTURE2D_SAMPLER2D(CurrentDepth, samplerCurrentDepth);
					TEXTURE2D_SAMPLER2D(PreviousLocalWorldPos, samplerPreviousLocalWorldPos);


					float4 CameraPosition;
					float4 CameraPositionPrev;
					float4x4 ProjectionPrev;
					float4x4 ProjectionPrevInverse;
					float4x4 WorldToCameraPrev;
					float4x4 CameraToWorldPrev;
					float DeltaTime;
					float BlendWeight;

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						float3 gi = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord.xy).rgb;

						float2 depthLookupCoord = round(input.texcoord.xy * _MainTex_TexelSize.zw) * _MainTex_TexelSize.xy;
						depthLookupCoord = input.texcoord.xy;
						float depth = GetDepthTexture(depthLookupCoord);

						float4 currentPos = float4(input.texcoord.x * 2.0 - 1.0, input.texcoord.y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

						float4 fragpos = mul(ProjectionMatrixInverse, currentPos);
						float4 thisViewPos = fragpos;
						fragpos = mul(CameraToWorld, fragpos);
						fragpos /= fragpos.w;
						float4 thisWorldPosition = fragpos;
						fragpos.xyz += CameraPosition.xyz * DeltaTime;

						float4 prevPos = fragpos;
						prevPos.xyz -= CameraPositionPrev.xyz * DeltaTime;
						prevPos = mul(WorldToCameraPrev, prevPos);
						prevPos = mul(ProjectionPrev, prevPos);
						prevPos /= prevPos.w;

						float2 diff = currentPos.xy - prevPos.xy;

						float2 reprojCoord = input.texcoord.xy - diff.xy * 0.5;
						float2 previousTexcoord = input.texcoord.xy + diff.xy * 0.5;


						float blendWeight = BlendWeight;

						float prevDepth = (SAMPLE_TEXTURE2D(PreviousDepth, samplerPreviousDepth, float4(reprojCoord + _MainTex_TexelSize.xy * 0.0, 0.0, 0.0)).x);

						float4 previousWorldPosition = mul(ProjectionPrevInverse, float4(reprojCoord.xy * 2.0 - 1.0, prevDepth * 2.0 - 1.0, 1.0));
						previousWorldPosition = mul(CameraToWorldPrev, previousWorldPosition);
						previousWorldPosition /= previousWorldPosition.w;

						if (distance(previousWorldPosition.xyz, thisWorldPosition.xyz) > 0.1 || reprojCoord.x > 1.0 || reprojCoord.x < 0.0 || reprojCoord.y > 1.0 || reprojCoord.y < 0.0)
						{
							blendWeight = 1.0;
						}

						float3 prevGI = SAMPLE_TEXTURE2D(PreviousGITexture, samplerPreviousGITexture, float4(reprojCoord, 0.0, 0.0)).rgb;

						gi = lerp(prevGI, gi, float3(blendWeight, blendWeight, blendWeight));

						float3 result = gi;
						return float4(result, 1.0);
					}

				ENDHLSL
			}

				Pass // 13 Blit
				{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing
					#if defined (VRWORKS)
						#pragma multi_compile VRWORKS_MRS VRWORKS_LMS VRWORKS_NONE
					#endif
					
					//half4 _MainTex_ST;

					/*struct Varyings
					{
						float2 uv : TEXCOORD0;
						float4 vertex : SV_POSITION;

						//UNITY_VERTEX_INPUT_INSTANCE_ID
						//UNITY_VERTEX_OUTPUT_STEREO
					};*/

					/*Varyings VertBlit(VaryingsSEGI v)
					{
						Varyings o;

						//UNITY_SETUP_INSTANCE_ID(v);
						//UNITY_INITIALIZE_OUTPUT(Varyings, o);
						//UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

						o.vertex = UnityObjectToClipPos(v.vertex);
						o.uv = UnityStereoScreenSpaceUVAdjust(v.texcoord, _MainTex_ST);
						return o;
					}*/

					half4 Frag(VaryingsSEGI input) : SV_Target
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
					}

				ENDHLSL
				}

	}

		Fallback off

}