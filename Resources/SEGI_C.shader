Shader "Hidden/SEGI_C" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	HLSLINCLUDE
	#include "PostProcessing/Shaders/StdLib.hlsl"
	#include "SEGI_HLSL_Helpers.cginc"
	#include "SEGI_C.cginc"
	#pragma target 5.0

	struct AttributesSEGI
	{
		float4 vertex : POSITION;
		half2 texcoord : TEXCOORD0;
		float2 texcoordStereo : TEXCOORD1;
	};

	struct VaryingsSEGI
	{
		float4 vertex : SV_POSITION;
		float2 texcoord : TEXCOORD0;
		float2 texcoordStereo : TEXCOORD1;
		//#if UNITY_UV_STARTS_AT_TOP
		//	half4 uv2 : TEXCOORD2;
		//#endif
	};

	VaryingsSEGI VertSEGI(AttributesSEGI v)
	{
		if (StereoEnabled)
		{
			VaryingsSEGI o;
			o.vertex = float4(v.texcoord.x - 0.5, v.texcoord.y + 0.5, 0, 0.5);
			o.texcoord = float4(v.texcoord.x, v.texcoord.y, 0, 1);
			o.texcoordStereo = UnityStereoScreenSpaceUVAdjust(o.texcoord, float4(1, 1, 0, 0));
			o.texcoordStereo = TransformStereoScreenSpaceTex(o.texcoordStereo, 1.0);

			#if UNITY_UV_STARTS_AT_TOP
					o.vertex.y = 1 - o.vertex.y;
			#endif

			return o;
		}
		else
		{
			VaryingsSEGI o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.texcoord = float4(v.texcoord.xy, 1, 1);
			o.texcoordStereo = TransformStereoScreenSpaceTex(o.texcoord, 1.0);

			#if UNITY_UV_STARTS_AT_TOP
				//o.uv2 = float4(v.texcoord.xy, 1, 1);
				if (_MainTex_TexelSize.y < 0.0)
				o.texcoord.y = 1.0 - o.texcoord.y;
			#endif

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
				#pragma multi_compile_instancing

				int FrameSwitch;

				//SAMPLER3D(SEGIVolumeTexture1);

				TEXTURE2D_SAMPLER2D(NoiseTexture, samplerNoiseTexture);

				float4 Frag(VaryingsSEGI input) : SV_Target
				{
					//UNITY_SETUP_INSTANCE_ID(input);
					//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

					/*#if UNITY_UV_STARTS_AT_TOP
						float2 coord = input.uv2.xy;
						float2 uv = input.uv2;
					#else*/
						float2 coord = input.texcoordStereo.xy;
						float2 uv = input.texcoordStereo;
					//#endif

						//Get view space position and view vector
						float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
						float3 viewVector = normalize(viewSpacePosition.xyz);

						//Get voxel space position
						float4 voxelSpacePosition = mul(CameraToWorld, viewSpacePosition);
						voxelSpacePosition = mul(SEGIWorldToVoxel, voxelSpacePosition);
						voxelSpacePosition = mul(SEGIVoxelProjection, voxelSpacePosition);
						voxelSpacePosition.xyz = voxelSpacePosition.xyz * 0.5 + 0.5;

						//Prepare for cone trace
						float2 dither = rand(coord + (float)FrameSwitch * 0.011734);

						float3 worldNormal;
						if (ForwardPath) worldNormal = GetWorldNormal(coord).rgb;
						else worldNormal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, uv).rgb * 2.0 - 1.0);

						float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.003 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;

						float3 gi = float3(0.0, 0.0, 0.0);
						float4 traceResult = float4(0,0,0,0);

						const float phi = 1.618033988;
						const float gAngle = phi * PI * 1.0;

						//Get blue noise
						float2 noiseCoord = (input.texcoordStereo.xy * _MainTex_TexelSize.zw) / (64.0).xx;
						float3 blueNoise = SAMPLE_TEXTURE2D_LOD(NoiseTexture, samplerNoiseTexture, noiseCoord, 0);

						//Trace GI cones
						int numSamples = TraceDirections;
						for (int i = 0; i < numSamples; i++)
						{
							float fi = (float)i + blueNoise * StochasticSampling;
							float fiN = fi / numSamples;
							float longitude = gAngle * fi;
							float latitude = asin(fiN * 2.0 - 1.0);

							float3 kernel;
							kernel.x = cos(latitude) * cos(longitude);
							kernel.z = cos(latitude) * sin(longitude);
							kernel.y = sin(latitude);

							kernel = normalize(kernel + worldNormal.xyz * 1.0);

							traceResult += ConeTrace(voxelOrigin.xyz, kernel.xyz, worldNormal.xyz, coord, dither.y, TraceSteps, ConeSize, 1.0, 1.0, viewVector);
						}

						traceResult /= numSamples;
						gi = traceResult.rgb * 20.0;


						float fadeout = saturate((distance(voxelSpacePosition.xyz, float3(0.5, 0.5, 0.5)) - 0.5f) * 5.0);

						float3 fakeGI = saturate(dot(worldNormal, float3(0, 1, 0)) * 0.5 + 0.5) * SEGISkyColor.rgb * 5.0;

						gi.rgb = lerp(gi.rgb, fakeGI, fadeout);

						gi *= 0.75;// +(float)GIResolution * 0.25;


						return float4(gi, 1.0);
					}

				ENDHLSL
			}

			Pass //1 Bilateral Blur
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing

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
							float2 coord = input.texcoordStereo.xy;
							float2 uv = input.texcoordStereo;
						//#endif

						float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
						float validWeights = 0.0;
						float depth = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, coord).x);
						half3 normal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, coord).rgb * 2.0 - 1.0);
						float thresh = 0.26;

						float3 viewPosition = GetViewSpacePosition(coord, uv).xyz;
						float3 viewVector = normalize(viewPosition);

						float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
						thresh *= 1.0 + NdotV * 2.0;

						for (int i = -4; i <= 4; i++)
						{
							float2 offs = Kernel.xy * (i)* _MainTex_TexelSize.xy * 1.0;
							float sampleDepth = LinearEyeDepth(SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, coord + offs.xy * 1, 0).x);
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
					#pragma multi_compile_instancing

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
							float2 coord = input.texcoordStereo.xy;
							float2 uv = input.texcoordStereo;
						//#endif

						float4 albedoTex;
						if (ForwardPath) albedoTex = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, coord);
						else albedoTex = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, coord);
						float3 albedo = albedoTex.rgb;
						float3 gi = SAMPLE_TEXTURE2D(GITexture, samplerGITexture, coord).rgb;
						float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, coord).rgb;
						float3 reflections = SAMPLE_TEXTURE2D(Reflections, samplerReflections, coord).rgb;

						float3 result = scene + gi * albedoTex.a * albedoTex.rgb;

						float4 spec;
						float smoothness;
						float3 specularColor;
						if (DoReflections > 0)
						{
							float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
							float3 viewVector = normalize(viewSpacePosition.xyz);
							float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

							/*if (useReflectionProbes  && ForwardPath)
							{
								float3 worldNormal = GetWorldNormal(coord).rgb;
								//float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
								//float3 viewVector = normalize(viewSpacePosition.xyz);
								float3 reflectedDir = reflect(viewVector, worldNormal);
								half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, worldNormal, 0);
								half3 probeColor = DecodeHDR(probeData, unity_SpecCube0_HDR);
								smoothness = probeData.a * 0.5;
								specularColor = probeColor.rgb * 0.5;
							}
							else
							{*/
								spec = SAMPLE_TEXTURE2D(_CameraGBufferTexture1, sampler_CameraGBufferTexture1, coord);
								smoothness = spec.a;
								specularColor = spec.rgb;
							//}

							float3 worldNormal;
							if (ForwardPath) worldNormal = GetWorldNormal(coord).rgb;
							else worldNormal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, coord).rgb * 2.0 - 1.0);

							float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

							float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
							fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

							fresnel *= saturate(smoothness * 4.0);

							result = lerp(result, reflections, fresnel);
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
					#pragma multi_compile_instancing

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

						float3 gi = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoordStereo.xy).rgb;

						float depth = GetDepthTexture(input.texcoordStereo.xy);

						float4 currentPos = float4(input.texcoordStereo.x * 2.0 - 1.0, input.texcoordStereo.y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

						float4 fragpos = mul(ProjectionMatrixInverse, currentPos);
						fragpos = mul(CameraToWorld, fragpos);
						fragpos /= fragpos.w;
						float4 thisWorldPosition = fragpos;

						float2 motionVectors = SAMPLE_TEXTURE2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture, float4(input.texcoordStereo.xy, 0.0, 0.0)).xy;
						float2 reprojCoord = input.texcoordStereo.xy - motionVectors.xy;

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

						float3 s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoordStereo.xy + _MainTex_TexelSize.xy * float2(0.5, 0.5), 0, 0)).rgb;
						minPrev = s0;
						maxPrev = s0;
						s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoordStereo.xy + _MainTex_TexelSize.xy * float2(0.5, -0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);
						s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoordStereo.xy + _MainTex_TexelSize.xy * float2(-0.5, 0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);
						s0 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float4(input.texcoordStereo.xy + _MainTex_TexelSize.xy * float2(-0.5, -0.5), 0, 0)).rgb;
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
					#pragma multi_compile_instancing

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
							float2 coord = input.texcoordStereo.xy;
							float2 uv = input.texcoordStereo;
						//#endif

						float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
						float3 viewVector = normalize(viewSpacePosition.xyz);
						float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));


						float4 voxelSpacePosition = mul(CameraToWorld, viewSpacePosition);
						float3 worldPosition = voxelSpacePosition.xyz;
						voxelSpacePosition = mul(SEGIWorldToVoxel, voxelSpacePosition);
						voxelSpacePosition = mul(SEGIVoxelProjection, voxelSpacePosition);
						voxelSpacePosition.xyz = voxelSpacePosition.xyz * 0.5 + 0.5;

						float3 worldNormal;
						if (ForwardPath) worldNormal = GetWorldNormal(coord).rgb;
						else worldNormal = normalize(SAMPLE_TEXTURE2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2, input.texcoordStereo).rgb * 2.0 - 1.0);

						float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.006 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;

						float2 dither = rand(coord + (float)FrameSwitch * 0.11734);

						float smoothness;
						float3 specularColor;
						/*if (ForwardPath)
						{
							float3 reflectedDir = reflect(viewVector, worldNormal);
							half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, worldNormal, 0);
							half3 probeColor = DecodeHDR(probeData, unity_SpecCube0_HDR);
							smoothness = probeData.a * 0.5;
							specularColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, coord).rgb;
						}
						else
						{*/
							smoothness = SAMPLE_TEXTURE2D(_CameraGBufferTexture1, sampler_CameraGBufferTexture1, coord).a * 0.5;
							specularColor = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, coord).rgb;
						//}

						float4 reflection = (0.0).xxxx;

						float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

						float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
						fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

						voxelOrigin += worldNormal.xyz * 0.002 * 1.25 / SEGIVoxelScaleFactor;
						reflection = SpecularConeTrace(voxelOrigin.xyz, reflectionKernel.xyz, worldNormal.xyz, smoothness, coord, dither.x, viewVector);

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
					#pragma multi_compile_instancing

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						float4 tex = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, input.texcoordStereo.xy);
						return tex;
					}

				ENDHLSL
			}

			Pass //6 Get camera normals texture
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						float2 coord = input.texcoordStereo.xy;
						float4 tex = SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture, sampler_CameraDepthNormalsTexture, coord);
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

					TEXTURE2D_SAMPLER2D(GITexture, samplerGITexture);

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						float4 albedoTex = SAMPLE_TEXTURE2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0, input.texcoordStereo.xy);
						float3 albedo = albedoTex.rgb;
						float3 gi = SAMPLE_TEXTURE2D(GITexture, samplerGITexture, input.texcoordStereo.xy).rgb;
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

					float LayerToVisualize;
					int MipLevelToVisualize;

					//TEXTURE3D_SAMPLER3D(SEGIVolumeTexture1, );

					float4 Frag(VaryingsSEGI input) : COLOR0
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						return float4(SAMPLE_TEXTURE3D(SEGIVolumeTexture1, samplerSEGIVolumeTexture1, float3(input.texcoordStereo.xy, LayerToVisualize)).rgb, 1.0);
					}

				ENDHLSL
			}


			Pass //10 Visualize voxels (trace through GI volumes)
			{
		ZTest Always

				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing

					float4 CameraPosition;

					float4 Frag(VaryingsSEGI input) : SV_Target
					{
						//UNITY_SETUP_INSTANCE_ID(input);
						//UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

						/*#if UNITY_UV_STARTS_AT_TOP
							float2 coord = input.uv2.xy;
							float2 uv = input.texcoordStereo;
						#else*/
							float2 coord = input.texcoordStereo.xy;
							float2 uv = input.texcoordStereo;
						//#endif

						float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
						float3 viewVector = normalize(viewSpacePosition.xyz);
						float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

						float4 voxelCameraPosition = mul(SEGIWorldToVoxel, float4(CameraPosition.xyz, 1.0));
							   voxelCameraPosition = mul(SEGIVoxelProjection, voxelCameraPosition);
							   voxelCameraPosition.xyz = voxelCameraPosition.xyz * 0.5 + 0.5;

						float4 result = VisualConeTrace(voxelCameraPosition.xyz, worldViewVector.xyz);

						return float4(result.rgb, 1.0);
					}

				ENDHLSL
			}

			Pass //11 Bilateral upsample
			{
				HLSLPROGRAM
					#pragma vertex VertSEGI
					#pragma fragment Frag
					#pragma multi_compile_instancing

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
							float2 coord = input.texcoordStereo.xy;
							float2 uv = input.texcoordStereo;
						//#endif

						float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
						float4 blurredDumb = float4(0.0, 0.0, 0.0, 0.0);
						float validWeights = 0.0;
						float depth = LinearEyeDepth(SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, coord).x);
						half3 normal = DecodeViewNormalStereo(SAMPLE_TEXTURE2D(_CameraDepthNormalsTexture, sampler_CameraDepthNormalsTexture, coord));
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
					#pragma multi_compile_instancing

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

						float3 gi = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoordStereo.xy).rgb;

						float2 depthLookupCoord = round(input.texcoordStereo.xy * _MainTex_TexelSize.zw) * _MainTex_TexelSize.xy;
						depthLookupCoord = input.texcoordStereo.xy;
						float depth = GetDepthTexture(depthLookupCoord);

						float4 currentPos = float4(input.texcoordStereo.x * 2.0 - 1.0, input.texcoordStereo.y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

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

						float2 reprojCoord = input.texcoordStereo.xy - diff.xy * 0.5;
						float2 previousTexcoord = input.texcoordStereo.xy + diff.xy * 0.5;


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

						return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoordStereo);
					}

				ENDHLSL
				}

	}

		Fallback off

}