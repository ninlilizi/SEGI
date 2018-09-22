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
	};

	v2f vert(appdata_img v)
	{
		v2f o;

		o.pos = UnityObjectToClipPos(v.vertex);
		o.uv = float4(v.texcoord.xy, 1, 1);

#if UNITY_UV_STARTS_AT_TOP
		o.uv2 = float4(v.texcoord.xy, 1, 1);
		if (_MainTex_TexelSize.y < 0.0)
			o.uv.y = 1.0 - o.uv.y;
#endif

		return o;
	}

#define PI 3.147159265


	ENDCG


		SubShader
	{
		ZTest Off
		Cull Off
		ZWrite Off
		Fog { Mode off }

		Pass //0
		{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				int FrameSwitch;

				UNITY_DECLARE_TEX3D(SEGIVolumeTexture1);

				UNITY_DECLARE_SCREENSPACE_TEXTURE(NoiseTexture);

				float4 frag(v2f input) : SV_Target
				{
					#if UNITY_UV_STARTS_AT_TOP
						float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
						float2 uv = UnityStereoTransformScreenSpaceTex(input.uv2);
					#else
						float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
						float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
					#endif

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
						else worldNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv)).rgb * 2.0 - 1.0);

						float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.003 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;

						float3 gi = float3(0.0, 0.0, 0.0);
						float4 traceResult = float4(0,0,0,0);

						const float phi = 1.618033988;
						const float gAngle = phi * PI * 1.0;

						//Get blue noise
						float2 noiseCoord = (UnityStereoTransformScreenSpaceTex(input.uv).xy * _MainTex_TexelSize.zw) / (64.0).xx;
						float3 blueNoise = UNITY_SAMPLE_SCREENSPACE_TEXTURE(NoiseTexture, float4(noiseCoord, 0.0, 0.0));
	
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

						gi *= 0.75 + (float)GIResolution * 0.25;


						return float4(gi, 1.0);
					}

				ENDCG
			}

			Pass //1 Bilateral Blur
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					float2 Kernel;

					float DepthTolerance;

					sampler2D DepthNormalsLow;
					sampler2D DepthLow;
					int SourceScale;


					float4 frag(v2f input) : COLOR0
					{
						#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv2);
						#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
						#endif

						float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
						float validWeights = 0.0;
						float depth = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy).x);
						half3 normal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb * 2.0 - 1.0);
						float thresh = 0.26;

						float3 viewPosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, uv).xyz;
						float3 viewVector = normalize(viewPosition);

						float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
						thresh *= 1.0 + NdotV * 2.0;

						for (int i = -4; i <= 4; i++)
						{
							float2 offs = Kernel.xy * (i)* _MainTex_TexelSize.xy * 1.0;
							float sampleDepth = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy * 1, 0, 0)).x);
							half3 sampleNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy * 1, 0, 0)).rgb * 2.0 - 1.0);

							float weight = saturate(1.0 - abs(depth - sampleDepth) / thresh);
							weight *= pow(saturate(dot(sampleNormal, normal)), 24.0);

							float4 blurSample = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + offs.xy, 0, 0)).rgba;
							blurred += blurSample * weight;
							validWeights += weight;
						}

						blurred /= validWeights + 0.001;

						return blurred;
					}

				ENDCG
			}

			Pass //2 Blend with scene
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					sampler2D GITexture;
					sampler2D Reflections;

					int DoReflections = 1;

					float4 frag(v2f input) : COLOR0
					{
						#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv2);
						#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
						#endif

						float4 albedoTex;
						if (ForwardPath) albedoTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, UnityStereoTransformScreenSpaceTex(input.uv).xy);
						else albedoTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture0, UnityStereoTransformScreenSpaceTex(input.uv).xy);
						float3 albedo = albedoTex.rgb;
						float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(GITexture, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;
						float3 scene = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;
						float3 reflections = UNITY_SAMPLE_SCREENSPACE_TEXTURE(Reflections, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;

						float3 result = scene + gi * albedoTex.a * albedoTex.rgb;

						float4 spec;
						float smoothness;
						float3 specularColor;
						if (DoReflections > 0)
						{
							float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
							float3 viewVector = normalize(viewSpacePosition.xyz);
							float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

							if (ForwardPath)
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
							{
								spec = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture1, coord);
								smoothness = spec.a;
								specularColor = spec.rgb;
							}

							float3 worldNormal;
							if (ForwardPath) worldNormal = GetWorldNormal(coord).rgb;
							else worldNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv)).rgb * 2.0 - 1.0);

							float3 reflectionKernel = reflect(worldViewVector.xyz, worldNormal);

							float3 fresnel = pow(saturate(dot(worldViewVector.xyz, reflectionKernel.xyz)) * (smoothness * 0.5 + 0.5), 5.0);
							fresnel = lerp(fresnel, (1.0).xxx, specularColor.rgb);

							fresnel *= saturate(smoothness * 4.0);

							result = lerp(result, reflections, fresnel);
						}

						return float4(result, 1.0);
					}

				ENDCG
			}

			Pass //3 Temporal blend (with unity motion vectors)
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					sampler2D GITexture;
					UNITY_DECLARE_SCREENSPACE_TEXTURE(PreviousDepth);
					UNITY_DECLARE_SCREENSPACE_TEXTURE(CurrentDepth);
					sampler2D PreviousLocalWorldPos;


					float4 CameraPosition;
					float4 CameraPositionPrev;
					float4x4 ProjectionPrev;
					float4x4 ProjectionPrevInverse;
					float4x4 WorldToCameraPrev;
					float4x4 CameraToWorldPrev;
					float DeltaTime;
					float BlendWeight;

					float4 frag(v2f input) : COLOR0
					{
						float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;


						float depth = GetDepthTexture(UnityStereoTransformScreenSpaceTex(input.uv).xy);

						float4 currentPos = float4(UnityStereoTransformScreenSpaceTex(input.uv).x * 2.0 - 1.0, UnityStereoTransformScreenSpaceTex(input.uv).y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

						float4 fragpos = mul(ProjectionMatrixInverse, currentPos);
						fragpos = mul(CameraToWorld, fragpos);
						fragpos /= fragpos.w;
						float4 thisWorldPosition = fragpos;




						float2 motionVectors = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraMotionVectorsTexture, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy, 0.0, 0.0)).xy;
						float2 reprojCoord = UnityStereoTransformScreenSpaceTex(input.uv).xy - motionVectors.xy;



						float prevDepth = (UNITY_SAMPLE_SCREENSPACE_TEXTURE(PreviousDepth, float4(reprojCoord + _MainTex_TexelSize.xy * 0.0, 0.0, 0.0)).x);
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

						float3 s0 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.5, 0.5), 0, 0)).rgb;
						minPrev = s0;
						maxPrev = s0;
						s0 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.5, -0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);
						s0 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(-0.5, 0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);
						s0 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(-0.5, -0.5), 0, 0)).rgb;
						minPrev = min(minPrev, s0);
						maxPrev = max(maxPrev, s0);



						float3 prevGI = UNITY_SAMPLE_SCREENSPACE_TEXTURE(PreviousGITexture, float4(reprojCoord, 0.0, 0.0)).rgb;
						prevGI = lerp(prevGI, clamp(prevGI, minPrev, maxPrev), 0.25);

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

					UNITY_DECLARE_TEX3D(SEGIVolumeTexture1);

					int FrameSwitch;


					float4 frag(v2f input) : SV_Target
					{
						#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv2);
						#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
						#endif

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
						else worldNormal = normalize(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2, UnityStereoTransformScreenSpaceTex(input.uv)).rgb * 2.0 - 1.0);

						float3 voxelOrigin = voxelSpacePosition.xyz + worldNormal.xyz * 0.006 * ConeTraceBias * 1.25 / SEGIVoxelScaleFactor;

						float2 dither = rand(coord + (float)FrameSwitch * 0.11734);

						float smoothness;
						float3 specularColor;
						if (ForwardPath)
						{
							float3 reflectedDir = reflect(viewVector, worldNormal);
							half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, worldNormal, 0);
							half3 probeColor = DecodeHDR(probeData, unity_SpecCube0_HDR);
							smoothness = probeData.a * 0.5;
							specularColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_Albedo, coord).rgb;
						}
						else
						{
							smoothness = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture1, coord).a * 0.5;
							specularColor = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture0, coord).rgb;
						}

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

				ENDCG
			}

			Pass //5 Get camera depth texture
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					float4 frag(v2f input) : COLOR0
					{
						float4 tex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy);
						return tex;
					}

				ENDCG
			}

			Pass //6 Get camera normals texture
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag


					float4 frag(v2f input) : COLOR0
					{
						float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
						float4 tex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, coord);
						return tex;
					}

				ENDCG
			}


			Pass //7 Visualize GI
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					sampler2D GITexture;

					float4 frag(v2f input) : COLOR0
					{
						float4 albedoTex = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraGBufferTexture0, UnityStereoTransformScreenSpaceTex(input.uv).xy);
						float3 albedo = albedoTex.rgb;
						float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(GITexture, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;
						return float4(gi, 1.0);
					}

				ENDCG
			}



			Pass //8 Write black
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					float4 frag(v2f input) : COLOR0
					{
						return float4(0.0, 0.0, 0.0, 1.0);
					}

				ENDCG
			}

			Pass //9 Visualize slice of GI Volume (CURRENTLY UNUSED)
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					float LayerToVisualize;
					int MipLevelToVisualize;

					UNITY_DECLARE_TEX3D(SEGIVolumeTexture1);

					float4 frag(v2f input) : COLOR0
					{
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

					float4 CameraPosition;

					float4 frag(v2f input) : SV_Target
					{
						#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv2);
						#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
						#endif

						float4 viewSpacePosition = GetViewSpacePosition(coord, uv);
						float3 viewVector = normalize(viewSpacePosition.xyz);
						float4 worldViewVector = mul(CameraToWorld, float4(viewVector.xyz, 0.0));

						float4 voxelCameraPosition = mul(SEGIWorldToVoxel, float4(CameraPosition.xyz, 1.0));
							   voxelCameraPosition = mul(SEGIVoxelProjection, voxelCameraPosition);
							   voxelCameraPosition.xyz = voxelCameraPosition.xyz * 0.5 + 0.5;

						float4 result = VisualConeTrace(voxelCameraPosition.xyz, worldViewVector.xyz);

						return float4(result.rgb, 1.0);
					}

				ENDCG
			}

			Pass //11 Bilateral upsample
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					float2 Kernel;

					float DepthTolerance;

					sampler2D DepthNormalsLow;
					sampler2D DepthLow;
					int SourceScale;
					UNITY_DECLARE_SCREENSPACE_TEXTURE(CurrentDepth);
					sampler2D CurrentNormal;


					float4 frag(v2f input) : COLOR0
					{
						#if UNITY_UV_STARTS_AT_TOP
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv2).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv2);
						#else
							float2 coord = UnityStereoTransformScreenSpaceTex(input.uv).xy;
							float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
						#endif

						float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
						float4 blurredDumb = float4(0.0, 0.0, 0.0, 0.0);
						float validWeights = 0.0;
						float depth = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy).x);
						half3 normal = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, UnityStereoTransformScreenSpaceTex(input.uv).xy));
						float thresh = 0.26;

						float3 viewPosition = GetViewSpacePosition(UnityStereoTransformScreenSpaceTex(input.uv).xy, uv).xyz;
						float3 viewVector = normalize(viewPosition);

						float NdotV = 1.0 / (saturate(dot(-viewVector, normal.xyz)) + 0.1);
						thresh *= 1.0 + NdotV * 2.0;

						float4 sample00 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0) * 1.0, 0.0, 0.0));
						float4 sample10 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 0.0) * 1.0, 0.0, 0.0));
						float4 sample11 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 1.0) * 1.0, 0.0, 0.0));
						float4 sample01 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 1.0) * 1.0, 0.0, 0.0));

						float4 depthSamples = float4(0,0,0,0);
						depthSamples.x = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentDepth, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0), 0, 0)).x);
						depthSamples.y = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentDepth, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 0.0), 0, 0)).x);
						depthSamples.z = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentDepth, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 1.0), 0, 0)).x);
						depthSamples.w = LinearEyeDepth(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentDepth, float4(UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 1.0), 0, 0)).x);

						half3 normal00 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentNormal, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 0.0)));
						half3 normal10 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentNormal, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 0.0)));
						half3 normal11 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentNormal, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(1.0, 1.0)));
						half3 normal01 = DecodeViewNormalStereo(UNITY_SAMPLE_SCREENSPACE_TEXTURE(CurrentNormal, UnityStereoTransformScreenSpaceTex(input.uv).xy + _MainTex_TexelSize.xy * float2(0.0, 1.0)));

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

						float2 fractCoord = frac(UnityStereoTransformScreenSpaceTex(input.uv).xy * _MainTex_TexelSize.zw * 1.0);

						float4 filteredX0 = lerp(sample00 * weights.x, sample10 * weights.y, fractCoord.x);
						float4 filteredX1 = lerp(sample01 * weights.w, sample11 * weights.z, fractCoord.x);

						float4 filtered = lerp(filteredX0, filteredX1, fractCoord.y);


						return filtered * 3.0;

						return blurred;
					}

				ENDCG
			}

			Pass //12 Temporal blending without motion vectors (for legacy support)
			{
				CGPROGRAM
					#pragma vertex vert
					#pragma fragment frag

					sampler2D GITexture;
					UNITY_DECLARE_SCREENSPACE_TEXTURE(PreviousDepth);
					UNITY_DECLARE_SCREENSPACE_TEXTURE(CurrentDepth);
					sampler2D PreviousLocalWorldPos;


					float4 CameraPosition;
					float4 CameraPositionPrev;
					float4x4 ProjectionPrev;
					float4x4 ProjectionPrevInverse;
					float4x4 WorldToCameraPrev;
					float4x4 CameraToWorldPrev;
					float DeltaTime;
					float BlendWeight;

					float4 frag(v2f input) : COLOR0
					{
						float3 gi = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(input.uv).xy).rgb;

						float2 depthLookupCoord = round(UnityStereoTransformScreenSpaceTex(input.uv).xy * _MainTex_TexelSize.zw) * _MainTex_TexelSize.xy;
						depthLookupCoord = UnityStereoTransformScreenSpaceTex(UnityStereoTransformScreenSpaceTex(input.uv)).xy;
						float depth = GetDepthTexture(depthLookupCoord);

						float4 currentPos = float4(UnityStereoTransformScreenSpaceTex(input.uv).x * 2.0 - 1.0, UnityStereoTransformScreenSpaceTex(input.uv).y * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);

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

						float2 reprojCoord = UnityStereoTransformScreenSpaceTex(input.uv).xy - diff.xy * 0.5;
						float2 previousTexcoord = UnityStereoTransformScreenSpaceTex(input.uv).xy + diff.xy * 0.5;


						float blendWeight = BlendWeight;

						float prevDepth = (UNITY_SAMPLE_SCREENSPACE_TEXTURE(PreviousDepth, float4(reprojCoord + _MainTex_TexelSize.xy * 0.0, 0.0, 0.0)).x);

						float4 previousWorldPosition = mul(ProjectionPrevInverse, float4(reprojCoord.xy * 2.0 - 1.0, prevDepth * 2.0 - 1.0, 1.0));
						previousWorldPosition = mul(CameraToWorldPrev, previousWorldPosition);
						previousWorldPosition /= previousWorldPosition.w;

						if (distance(previousWorldPosition.xyz, thisWorldPosition.xyz) > 0.1 || reprojCoord.x > 1.0 || reprojCoord.x < 0.0 || reprojCoord.y > 1.0 || reprojCoord.y < 0.0)
						{
							blendWeight = 1.0;
						}

						float3 prevGI = UNITY_SAMPLE_SCREENSPACE_TEXTURE(PreviousGITexture, float3(reprojCoord, UnityStereoTransformScreenSpaceTex(input.uv).z)).rgb;

						gi = lerp(prevGI, gi, float3(blendWeight, blendWeight, blendWeight));

						float3 result = gi;
						return float4(result, 1.0);
					}

				ENDCG
			}


	}

		Fallback off

}