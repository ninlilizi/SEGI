Shader "Hidden/SEGI_C" {
	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

	CGINCLUDE
	#include "UnityCG.cginc"
	#include "SEGI_C.cginc"
	#pragma target 5.0


	struct v2f
	{
		float4 pos : SV_POSITION;
		float4 uv : TEXCOORD0;

		#if UNITY_UV_STARTS_AT_TOP
		half4 uv2 : TEXCOORD1;
		#endif

		UNITY_VERTEX_INPUT_INSTANCE_ID
		UNITY_VERTEX_OUTPUT_STEREO
	};

	v2f vert(appdata_base v)
	{
		v2f o;

		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_INITIALIZE_OUTPUT(v2f, o);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
		UNITY_TRANSFER_INSTANCE_ID(v, o);

		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = float4(v.texcoord.xy, 1, 1);

		#if UNITY_UV_STARTS_AT_TOP
		o.uv2 = float4(v.texcoord.xy, 1, 1);
		if (_MainTex_TexelSize.y < 0.0)
			o.uv.y = 1.0 - o.uv.y;
		#endif

		return o;
	}

	ENDCG


	SubShader
	{
		ZTest Off
		Cull Off
		ZWrite Off
		Fog { Mode off }

		Pass //0 diffuse GI trace
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			UNITY_DECLARE_SCREENSPACE_TEXTURE(NoiseTexture);
			int HalfResolution;

			float4 frag(v2f input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				UNITY_SETUP_INSTANCE_ID(input);


			#if UNITY_UV_STARTS_AT_TOP
				float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
			#else
				float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
			#endif

			//Get view space position and view vector
			float3 viewSpacePosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, input.uv).xyz;
			//Get voxel space position
			float4 voxelSpacePosition = float4(viewSpacePosition.xy, viewSpacePosition.z, 0);

			float3 viewDir = WorldSpaceViewDir(voxelSpacePosition);

			voxelSpacePosition = mul(SEGIWorldToVoxel0, voxelSpacePosition);
			voxelSpacePosition = mul(SEGIVoxelProjection0, voxelSpacePosition);
			voxelSpacePosition.xyz = voxelSpacePosition.xyz * 0.5 + 0.5;


			//Prepare for cone trace			
			float3 gi = float3(0.0, 0.0, 0.0);

			float3 worldNormal;
			if (ForwardPath == 0)
			{
				worldNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv)).rgb * 2.0 - 1.0);
			}
			else
			{
				worldNormal = GetWorldNormal(coord).rgb;
			}
			float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.003 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;	//Apply bias of cone trace origin towards the surface normal to avoid self-occlusion artifacts

			//float3 gi = float3(0.0, 0.0, 0.0);
			float4 traceResult = float4(0,0,0,0);

			const float phi = 1.618033988;
			const float gAngle = phi * PI;

			//Get blue noise
			float2 noiseCoord = (UnityStereoTransformScreenSpaceTex(input.uv).xy * _MainTex_TexelSize.zw) / (64.0).xx;
			float blueNoise = UNITY_SAMPLE_SCREENSPACE_TEXTURE(NoiseTexture, float4(noiseCoord, 0.0, 0.0)).x;


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

				kernel = normalize(kernel + worldNormal.xyz);

				traceResult += ConeTrace(voxelOrigin.xyz, kernel.xyz, worldNormal.xyz, UnityStereoTransformScreenSpaceTex(input.uv), blueNoise.x, TraceSteps, ConeSize, 1.0, 1.0, viewDir);
			}

			traceResult /= numSamples;
			gi = traceResult.rgb * 1.18;

			float fadeout = saturate((distance(voxelSpacePosition.xyz, float3(0.5, 0.5, 0.5)) - 0.5f) * 5.0);
			float3 fakeGI = saturate(dot(worldNormal, float3(0, 1, 0)) * 0.5 + 0.5) * SEGISkyColor.rgb * 5.0;
			gi.rgb = lerp(gi.rgb, fakeGI, fadeout);
			gi *= 0.75 + (float)HalfResolution * 0.25;

			return float4(gi, 1.0);
		}

		ENDCG
	}

		Pass //1 Bilateral Blur
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			float2 Kernel;

			float4 frag(v2f input) : COLOR0
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
					UNITY_SETUP_INSTANCE_ID(input);

					float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
					float validWeights = 1;
					float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy).x;
					half3 normal;
					if (ForwardPath)
					{
						normal = GetWorldNormal(UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb * 2.0 - 1.0;
					}
					else
					{
						normal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb * 2.0 - 1.0);
					}
					float thresh = 0.26;

					float3 viewPosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, input.uv).xyz;
					float3 viewVector = normalize(viewPosition);

					float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
					thresh *= 1.0 + NdotV * 2.0;

				for (int i = -4; i <= 4; i++)
				{
					float2 offs = Kernel.xy * (i) * _MainTex_TexelSize.xy * 1.0;
					//float 
					float sampleDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy * 1, 0, 0)).x);
					half3 sampleNormal;
					if (ForwardPath)
					{
						float4 sampleXY = float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy * 1, 0, 0);
						float depth_tmp;
						DecodeDepthNormal(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, sampleXY), depth_tmp, sampleNormal);
						sampleNormal = sampleNormal.rgb * 2.0 - 1.0;
					}
					else
					{
						sampleNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy * 1, 0, 0)).rgb * 2.0 - 1.0);
					}
					
					float weight = saturate(1.0 - abs(depth - sampleDepth) / thresh);
					weight *= pow(saturate(dot(sampleNormal, normal)), 24.0);
					
					float4 blurSample = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy, 0, 0)).rgba;
					blurred += blurSample * weight;
					validWeights += weight;
				}

					blurred /= validWeights + 0.0001;

					return blurred;
				}

				ENDCG
				}

				Pass //2 Blend with scene
				{
					CGPROGRAM
						#pragma vertex vert
						#pragma fragment frag
						#pragma multi_compile_instancing

						UNITY_DECLARE_SCREENSPACE_TEXTURE(GITexture);
						UNITY_DECLARE_SCREENSPACE_TEXTURE(Reflections);
											   
						int GIResolution;
						int useBilateralFiltering;

						float4 frag(v2f input) : COLOR0
						{
							UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
							UNITY_SETUP_INSTANCE_ID(input);

			#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
			#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
			#endif
							float3 result;
							float4 reflections = UNITY_SAMPLE_SCREENSPACE_TEXTURE(Reflections, UnityStereoTransformScreenSpaceTex(input.uv));
							float4 albedoTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, UnityStereoTransformScreenSpaceTex(input.uv));
							float3 albedo = albedoTex.rgb;
							float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(GITexture, UnityStereoTransformScreenSpaceTex(input.uv)).rgb;
							float3 scene = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv));
							//if (ForwardPath && useReflectionProbes)
							//{
								float3 viewSpacePosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, input.uv).xyz;
								float3 viewVector = normalize(viewSpacePosition.xyz);
								float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

								float4 spec = tex2D(_CameraGBufferTexture1, coord);
								float smoothness = spec.a;
								float3 specularColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, coord).rgb;

								float3 worldNormal;
								if (ForwardPath) worldNormal = GetWorldNormal(coord).rgb;
								else worldNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv)).rgb * 2.0 - 1.0);
								float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

								float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
								fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

								fresnel *= saturate(smoothness * 4.0);

								result = lerp(result, reflections, fresnel);

								//This causes lighting to flash
								//gi += min(reflections * reflectionProbeAttribution, gi);

								//scene += reflections;
							//}

							result = scene + gi * albedoTex.a * albedoTex.rgb;
							result *= 2;

							return float4(result, 1.0);
						}

					ENDCG
				}

				Pass //3 Temporal blend (with unity motion vectors)
				{
					CGPROGRAM
						#pragma vertex vert
						#pragma fragment frag
						#pragma multi_compile_instancing

						UNITY_DECLARE_SCREENSPACE_TEXTURE(GITexture);
						UNITY_DECLARE_SCREENSPACE_TEXTURE(PreviousDepth);
						UNITY_DECLARE_SCREENSPACE_TEXTURE(PreviousLocalWorldPos);


						float4 CameraPosition;
						float4 CameraPositionPrev;
						float4x4 ProjectionPrev;
						float4x4 ProjectionPrevInverse;
						float4x4 WorldToCameraPrev;
						float4x4 CameraToWorldPrev;
						float DeltaTime;
						float BlendWeight;

						UNITY_DECLARE_SCREENSPACE_TEXTURE(BlurredGI);

						float4 frag(v2f input) : COLOR0
						{
							UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
							UNITY_SETUP_INSTANCE_ID(input);

							float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv)).rgb;

							//Calculate moments and width of color deviation of neighbors for color clamping
							float3 m1, m2 = (0.0).xxx;
							{
								float width = 0.7;
								float3 samp = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy + float2(width, width) * _MainTex_TexelSize.xy).rgb;
								m1 = samp;
								m2 = samp * samp;
								samp = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy + float2(width, width) * _MainTex_TexelSize.xy).rgb;
								m1 += samp;
								m2 += samp * samp;
								samp = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy + float2(width, width) * _MainTex_TexelSize.xy).rgb;
								m1 += samp;
								m2 += samp * samp;
								samp = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy + float2(width, width) * _MainTex_TexelSize.xy).rgb;
								m1 += samp;
								m2 += samp * samp;
							}

							float3 mu = m1 * 0.25;
							float3 sigma = sqrt(max((0.0).xxx, m2 * 0.25 - mu * mu));
							float errorWindow = 0.2 / BlendWeight;
							float3 minc = mu - (errorWindow)* sigma;
							float3 maxc = mu + (errorWindow)* sigma;




							//Calculate world space position for current frame
							float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy, 0.0, 0.0)).x;

							#if defined(UNITY_REVERSED_Z)
							depth = 1.0 - depth;
							#endif

							float4 currentPos = float4(UnityStereoTransformScreenSpaceTex(input.uv).x * 2.0 - 1.0, UnityStereoTransformScreenSpaceTex(input.uv).y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

							float4 fragpos = mul(ProjectionMatrixInverse, currentPos);
							float4 thisViewPos = fragpos;
							fragpos = mul(CameraToWorld, fragpos);
							fragpos /= fragpos.w;
							float4 thisWorldPosition = fragpos;


							//Get motion vectors and calculate reprojection coord
							float2 motionVectors = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraMotionVectorsTexture, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy, 0.0, 0.0)).xy;
							float2 reprojCoord = UnityStereoTransformScreenSpaceTex(input.uv).xy - motionVectors.xy;


							//Calculate world space position for the previous frame reprojected to the current frame
							float prevDepth = (UNITY_SAMPLE_SCREENSPACE_TEXTURE(PreviousDepth, UnityStereoTransformScreenSpaceTex(float4(reprojCoord + _MainTex_TexelSize.xy * 0.0, 0.0, 0.0))).x);

							#if defined(UNITY_REVERSED_Z)
							prevDepth = 1.0 - prevDepth;
							#endif

							float4 previousWorldPosition = mul(ProjectionPrevInverse, float4(reprojCoord.xy * 2.0 - 1.0, prevDepth * 2.0 - 1.0, 1.0));
							previousWorldPosition = mul(CameraToWorldPrev, previousWorldPosition);
							previousWorldPosition /= previousWorldPosition.w;


							//Apply blending
							float blendWeight = BlendWeight;

							float3 blurredGI = UNITY_SAMPLE_SCREENSPACE_TEXTURE(BlurredGI, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;

							if (reprojCoord.x > 1.0 || reprojCoord.x < 0.0 || reprojCoord.y > 1.0 || reprojCoord.y < 0.0)
							{
								blendWeight = 1.0;
								gi = blurredGI;
							}

							float posSimilarity = saturate(1.0 - distance(previousWorldPosition.xyz, thisWorldPosition.xyz) * 2.0);
							blendWeight = lerp(1.0, blendWeight, posSimilarity);
							gi = lerp(blurredGI, gi, posSimilarity);


							float3 prevGI = UNITY_SAMPLE_SCREENSPACE_TEXTURE(PreviousGITexture, reprojCoord).rgb;
							prevGI = clamp(prevGI, minc, maxc);
							gi = lerp(prevGI, gi, float3(blendWeight, blendWeight, blendWeight));

							float3 result = gi;
							return float4(result, 1.0);
						}

					ENDCG
				}

				Pass //4 Specular/reflections trace
				{
					ZTest Always

					CGPROGRAM
						#pragma vertex vert
						#pragma fragment frag
						#pragma multi_compile_instancing

						//UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2);

						int FrameSwitch;

						float4 frag(v2f input) : SV_Target
						{
							UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
							UNITY_SETUP_INSTANCE_ID(input);

						#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
						#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
						#endif

						float3 viewSpacePosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, input.uv).xyz;
						float3 viewVector = normalize(viewSpacePosition.xyz);
						float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

						float3 worldNormal;
						if (ForwardPath == 0)
						{
							worldNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv)).rgb * 2.0 - 1.0);
						}
						else
						{
							worldNormal = GetWorldNormal(coord);
						}
						float4 voxelSpacePosition = float4(viewSpacePosition.xy, viewSpacePosition.z, 0);
						float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.006 * ConeTraceBias;

						float2 dither = rand(coord + (float)FrameSwitch * 0.11734);

						float4 spec;
						float smoothness;
						float3 specularColor;
						if (useReflectionProbes && ForwardPath)
						{
							float3 viewDir = WorldSpaceViewDir(voxelSpacePosition);
							float3 reflectedDir = reflect(viewDir, worldNormal);
							half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, worldNormal, 0);
							specularColor = DecodeHDR(probeData, unity_SpecCube0_HDR);
							smoothness = probeData.a * 0.5;
							specularColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, UnityStereoTransformScreenSpaceTex(input.uv));
						}
						else
						{
							spec = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture1, UnityStereoTransformScreenSpaceTex(input.uv));
							smoothness = spec.a;
							specularColor = spec.rgb;
						}

						float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

						float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
						fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

						voxelOrigin += worldNormal.xyz * 0.002;
						float4 reflection = SpecularConeTrace(voxelOrigin.xyz, reflectionKernel.xyz, worldNormal.xyz, smoothness, UnityStereoTransformScreenSpaceTex(input.uv), dither.x, viewVector);

						float3 skyReflection = (reflection.a * SEGISkyColor);

						reflection.rgb = reflection.rgb * 0.7 + skyReflection.rgb * 2.4015 * SkyReflectionIntensity;

						reflection.rgb = lerp(reflection.rgb, (0.5).xxx, fresnel.rgb);

						return float4(reflection.rgb, 1.0);
					}

				ENDCG
			}

			Pass //5 Get camera depth texture
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag
					#pragma multi_compile_instancing

					half4 _CameraDepthTexture_ST;

					float4 frag(v2f input) : COLOR0
					{
						UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
						UNITY_SETUP_INSTANCE_ID(input);

					//float2 coord = input.uv.xy;
					float4 tex = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoScreenSpaceUVAdjust(input.uv, _CameraDepthTexture_ST));
					return tex;
				}

					ENDCG
		}

			Pass //6 Get camera normals texture
				{
					CGPROGRAM
						#pragma vertex vert
						#pragma fragment frag
						#pragma multi_compile_instancing

				float4 frag(v2f input) : COLOR0
				{
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
					UNITY_SETUP_INSTANCE_ID(input);

					float depthValue;
					float3 normalValues;
					DecodeDepthNormal(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv)), depthValue, normalValues);
				
					return float4(normalValues, 1);
			}

		ENDCG
			}


				Pass //7 Visualize GI
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag
					#pragma multi_compile_instancing

					UNITY_DECLARE_SCREENSPACE_TEXTURE(GITexture);


			float4 frag(v2f input) : COLOR0
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				UNITY_SETUP_INSTANCE_ID(input);
				float4 albedoTex;
				if (ForwardPath == 0)
				{
					albedoTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture0, UnityStereoTransformScreenSpaceTex(input.uv));
				}
				else
				{
					albedoTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, UnityStereoTransformScreenSpaceTex(input.uv));
				}
				float3 albedo = albedoTex.rgb;
				float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(GITexture, UnityStereoTransformScreenSpaceTex(input.uv)).rgb;
				return float4(gi, 1.0);
			}

		ENDCG
	}



	Pass //8 Write black
	{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			float4 frag(v2f input) : COLOR0
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				UNITY_SETUP_INSTANCE_ID(input);
				return float4(0.0, 0.0, 0.0, 1.0);
			}

		ENDCG
	}

	Pass //9 Visualize slice of GI Volume (CURRENTLY UNUSED)
	{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			float LayerToVisualize;
			int MipLevelToVisualize;

			UNITY_DECLARE_TEX3D(SEGIVolumeTexture1);

			float4 frag(v2f input) : COLOR0
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				UNITY_SETUP_INSTANCE_ID(input);
				return float4(UNITY_SAMPLE_TEX3D(SEGIVolumeTexture1, float3(UnityStereoTransformScreenSpaceTex(input.uv).xy, LayerToVisualize)).rgb, 1.0);
			}

		ENDCG
	}


	Pass //10 Visualize voxels (trace through GI volumes)
	{
ZTest Always

	CGPROGRAM
	#pragma vertex vert
	#pragma fragment frag
	#pragma multi_compile_instancing

	float4 CameraPosition;

	float4 frag(v2f input) : SV_Target
	{
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
		UNITY_SETUP_INSTANCE_ID(input);

		#if UNITY_UV_STARTS_AT_TOP
			float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
		#else
			float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
		#endif

		float3 viewSpacePosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, input.uv).xyz;
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

	ENDCG
	}

	Pass //11 Bilateral upsample
	{
		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

		float4 frag(v2f input) : COLOR0
		{
			UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
			UNITY_SETUP_INSTANCE_ID(input);

		float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
		float4 blurredDumb = float4(0.0, 0.0, 0.0, 0.0);
		float validWeights = 0.0;
		float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv)).x);

		half3 normal = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv)));
		float thresh = 0.26;

		float3 viewPosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, input.uv).xyz;
		float3 viewVector = normalize(viewPosition);

		float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
		thresh *= 1.0 + NdotV * 2.0;

		float4 sample00 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0), 0.0, 0.0));
		float4 sample10 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 0.0), 0.0, 0.0));
		float4 sample11 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 1.0), 0.0, 0.0));
		float4 sample01 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 1.0), 0.0, 0.0));

		float4 depthSamples = float4(0,0,0,0);
		depthSamples.x = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0).x));
		depthSamples.y = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0).x));
		depthSamples.z = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0).x));
		depthSamples.w = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0).x));

		half3 normal00 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0)));//TODO? use CurrentNormal ....
		half3 normal10 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 0.0)));
		half3 normal11 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 1.0)));
		half3 normal01 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 1.0)));

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

		float2 fractCoord = frac(input.uv.xy * _MainTex_TexelSize.zw);

		float4 filteredX0 = lerp(sample00 * weights.x, sample10 * weights.y, fractCoord.x);
		float4 filteredX1 = lerp(sample01 * weights.w, sample11 * weights.z, fractCoord.x);

		float4 filtered = lerp(filteredX0, filteredX1, fractCoord.y);


		return filtered * 3.0;

		return blurred;
	}

ENDCG
}


	}

		Fallback off

}