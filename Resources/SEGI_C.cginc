#pragma warning (disable : 3206)
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
#pragma exclude_renderers d3d11 gles

float SEGIVoxelScaleFactor;
float SEGITraceCacheScaleFactor;

int StochasticSampling;
int TraceSteps;
float TraceLength;
float ConeSize;
float OcclusionStrength;
float OcclusionPower;
float ConeTraceBias;
float GIGain;
float NearLightGain;
float NearOcclusionStrength;
float SEGISoftSunlight;
float FarOcclusionStrength;
float FarthestOcclusionStrength;
float voxelSpaceSize;


half4 GISunColor;

sampler3D SEGIVolumeLevel0;
sampler3D SEGIVolumeLevel1;
sampler3D SEGIVolumeLevel2;
sampler3D SEGIVolumeLevel3;
sampler3D SEGIVolumeLevel4;
sampler3D SEGIVolumeLevel5;
sampler3D SEGIVolumeLevel6;
sampler3D SEGIVolumeLevel7;
sampler3D SEGIVolumeTexture1;
//TEXTURE3D_SAMPLER3D(VolumeTexture2, samplerVolumeTexture2);
//TEXTURE3D_SAMPLER3D(VolumeTexture3, samplerVolumeTexture3);

RWTexture3D<half4> tracedTexture0;
RWTexture3D<float4> tracedTexture1;

int tracedTexture1UpdateCount;

//TEXTURE3D_SAMPLER3D(tracedTexture0, samplertracedTexture0);
//sampler3D tracedTexture0;

float4x4 SEGIVoxelProjection;
float4x4 SEGIVoxelProjection0;
float4x4 SEGIVoxelProjection1;
float4x4 SEGIVoxelProjection2;
float4x4 SEGIVoxelProjection3;
float4x4 SEGIVoxelProjection4;
float4x4 SEGIVoxelProjection5;
float4x4 SEGIWorldToVoxel;
float4x4 SEGIWorldToVoxel0;
float4x4 SEGIWorldToVoxel1;
float4x4 SEGIWorldToVoxel2;
float4x4 SEGIWorldToVoxel3;
float4x4 SEGIWorldToVoxel4;
float4x4 SEGIWorldToVoxel5;
float4x4 GIProjectionInverse;
float4x4 GIToWorld;

float4x4 GIToVoxelProjection;
float4x4 CameraToWorld;


half4 SEGISkyColor;

float4 SEGISunlightVector;
float4 SEGIClipTransform0;
float4 SEGIClipTransform1;
float4 SEGIClipTransform2;
float4 SEGIClipTransform3;
float4 SEGIClipTransform4;
float4 SEGIClipTransform5;

float4 _MainTex_ST;

//float reflectionProbeAttribution;
//float reflectionProbeIntensity;
//int useReflectionProbes;
int ReflectionSteps;
int StereoEnabled;
int GIResolution;
int ForwardPath;

int visualizeGIPathCache;

uint SEGIRenderWidth;
uint SEGIRenderHeight;


uniform half4 _MainTex_TexelSize;

float4x4 ProjectionMatrixInverse;

TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
TEXTURE2D_SAMPLER2D(PreviousGITexture, samplerPreviousGITexture);
TEXTURE2D_SAMPLER2D(_CameraGBufferTexture0, sampler_CameraGBufferTexture0);
TEXTURE2D_SAMPLER2D(_CameraGBufferTexture1, sampler_CameraGBufferTexture1);
TEXTURE2D_SAMPLER2D(_CameraGBufferTexture2, sampler_CameraGBufferTexture2);
TEXTURE2D_SAMPLER2D(_CameraMotionVectorsTexture, sampler_CameraMotionVectorsTexture);
sampler2D _CameraDepthNormalsTexture;
sampler2D _CameraDepthTexture;

UNITY_DECLARE_TEXCUBE(_SEGICube);
//UNITY_DECLARE_TEXCUBE(_SEGICubeX2);
half4 _SEGICube_HDR;
//half4 _SEGICubeX2_HDR;


float4x4 WorldToCamera;
float4x4 ProjectionMatrix;

int SEGISphericalSkylight;

//Fix Stereo View Matrix
float4x4 _LeftEyeProjection;
float4x4 _RightEyeProjection;
float4x4 _LeftEyeToWorld;
float4x4 _RightEyeToWorld;
//Fix Stereo View Matrix/

float GetDepthTexture(float2 uv)
{
#if defined(UNITY_REVERSED_Z)
#if defined(VRWORKS)
	return 1.0 - tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
	return 1.0 - tex2Dlod(_CameraDepthTexture, float4(uv.x, uv.y, 0.0, 0.0)).x;
#endif
#else
#if defined(VRWORKS)
	return tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x;
#else
	return tex2Dlod(_CameraDepthTexture, float4(uv.x, uv.y, 0.0, 0.0)).x;
#endif
#endif
}

inline float SEGILinear01Depth(float z)
{
	// Values used to linearize the Z buffer
	// (http://www.humus.name/temp/Linearize%20depth.txt)
	// x = 1-far/near
	// y = far/near
	// z = x/far
	// w = y/far
	int x = voxelSpaceSize / 0.001;
	return 1.0 / (x * z + _ZBufferParams.y);
}

float GetDepthTextureTraceCache(float2 uv)
{
#if defined(UNITY_REVERSED_Z)
#if defined(VRWORKS)
	return 1.0 - SEGILinear01Depth(tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x);
#else
	return 1.0 - SEGILinear01Depth(tex2D(_CameraDepthTexture, uv).x);
#endif
#else
#if defined(VRWORKS)
	return SEGILinear01Depth(tex2D(VRWorksGetDepthSampler(), VRWorksRemapUV(uv)).x);
#else
	return SEGILinear01Depth(tex2D(_CameraDepthTexture, uv).x);
#endif
#endif
}

float3 TransformClipSpace(float3 pos, float4 transform)
{
	pos = pos * 2.0 - 1.0;
	pos *= transform.w;
	pos = pos * 0.5 + 0.5;
	pos -= transform.xyz;

	return pos;
}

float3 TransformClipSpace1(float3 pos)
{
	return TransformClipSpace(pos, SEGIClipTransform1);
}

float3 TransformClipSpace2(float3 pos)
{
	return TransformClipSpace(pos, SEGIClipTransform2);
}

float3 TransformClipSpace3(float3 pos)
{
	return TransformClipSpace(pos, SEGIClipTransform3);
}

float3 TransformClipSpace4(float3 pos)
{
	return TransformClipSpace(pos, SEGIClipTransform4);
}

float3 TransformClipSpace5(float3 pos)
{
	return TransformClipSpace(pos, SEGIClipTransform5);
}

float4 GetViewSpacePosition(float2 coord, float2 uv)
{
	float depth = GetDepthTexture(coord);

	if (StereoEnabled)
	{
		//Fix Stereo View Matrix
		float depth = GetDepthTexture(coord);
		float4x4 proj, eyeToWorld;

		if (uv.x < .5) // Left Eye
		{
			uv.x = saturate(uv.x * 2); // 0..1 for left side of buffer
			proj = _LeftEyeProjection;
			eyeToWorld = _LeftEyeToWorld;
		}
		else // Right Eye
		{
			uv.x = saturate((uv.x - 0.5) * 2); // 0..1 for right side of buffer
			proj = _RightEyeProjection;
			eyeToWorld = _RightEyeToWorld;
		}

		float2 uvClip = uv * 2.0 - 1.0;
		float4 clipPos = float4(uvClip, 1 - depth, 1.0);
		float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
		viewPos /= viewPos.w; // perspective division
		float3 worldPos = mul(eyeToWorld, viewPos).xyz;
		//Fix Stereo View Matrix/

		return viewPos;
	}
	else
	{
		float4 viewPosition = mul(ProjectionMatrixInverse, float4(coord.x * 2.0 - 1.0, coord.y * 2.0 - 1.0, 2.0 * depth - 1.0, 1.0));
		viewPosition /= viewPosition.w;

		return viewPosition;
	}
}

float2 rand(float2 coord)
{
	float noiseX = saturate(frac(sin(dot(coord, float2(12.9898, 78.223))) * 43758.5453));
	float noiseY = saturate(frac(sin(dot(coord, float2(12.9898, 78.223)*2.0)) * 43758.5453));

	return float2(noiseX, noiseY);
}

float GISampleWeight(float3 pos)
{
	float weight = 1.0;

	if (pos.x < 0.0 || pos.x > 1.0 ||
		pos.y < 0.0 || pos.y > 1.0 ||
		pos.z < 0.0 || pos.z > 1.0)
	{
		weight = 0.0;
	}

	return weight;
}

float4 ConeTrace(float3 voxelOrigin, float3 kernel, float3 worldNormal, float2 uv, float dither, int steps, float width, float lengthMult, float skyMult, float depth)
{
	float skyVisibility = 1.0;

	float3 gi = float3(0, 0, 0);

	uint numSteps = (int)(steps * lerp(SEGIVoxelScaleFactor, 1.0, 0.5));

	float3 adjustedKernel = normalize(kernel.xyz + worldNormal.xyz * 0.00 * width);

	float3 voxelCheckCoord1 = float3(0, 0, 0);

	float4 giSample = float4(0.0, 0.0, 0.0, 0.0);
	int startMipLevel = 0;
	float voxelDepth;

	voxelOrigin.xyz += worldNormal.xyz * 0.016 * (exp2(startMipLevel) - 1);

	for (uint i = 0; i < numSteps; i++)
	{
		float fi = ((float)i + dither) / numSteps;
		fi = lerp(fi, 1.0, 0.0);

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;

		float coneSize = coneDistance * width * 10.3;

		float3 voxelCheckCoord = voxelOrigin.xyz + adjustedKernel.xyz * (coneDistance * 1.12 * TraceLength * lengthMult + 0.000);

		int mipLevel = max(startMipLevel, log2(pow(fi, 1.3) * 24.0 * width + 1.0));
		if (coneDistance < depth)
		{
			voxelDepth = 256 / numSteps * i * SEGITraceCacheScaleFactor;
			voxelCheckCoord1 = voxelCheckCoord;
			if (mipLevel == 1 || mipLevel == 0)
			{
				voxelCheckCoord = TransformClipSpace1(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, coneSize)) * GISampleWeight(voxelCheckCoord);
			}
			else if (mipLevel == 2)
			{
				voxelCheckCoord = TransformClipSpace2(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, coneSize)) * GISampleWeight(voxelCheckCoord);
			}
			else if (mipLevel == 3)
			{
				voxelCheckCoord = TransformClipSpace3(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel3, float4(voxelCheckCoord.xyz, coneSize)) * GISampleWeight(voxelCheckCoord);
			}
			else if (mipLevel == 4)
			{
				voxelCheckCoord = TransformClipSpace4(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel4, float4(voxelCheckCoord.xyz, coneSize)) * GISampleWeight(voxelCheckCoord);
			}
			else
			{
				voxelCheckCoord = TransformClipSpace5(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel5, float4(voxelCheckCoord.xyz, coneSize)) * GISampleWeight(voxelCheckCoord);
			}
		}

		float occlusion = skyVisibility;

		float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

		giSample.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
		giSample.a *= (0.8 / (fi * fi * 2.0 + 0.15));
		gi.rgb += giSample.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi);

		skyVisibility *= pow(saturate(1.0 - giSample.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
	}
	//Write current cone to cache
	voxelCheckCoord1.z *= voxelDepth;
	tracedTexture1[uint3(voxelCheckCoord1)] = float4(tracedTexture1[uint3(voxelCheckCoord1)] + gi.rgb, giSample.a);

	//Read cached cones from cache
	half4 cachedResult = float4(0, 0, 0, 0);
	float scaledDepth = 256 * SEGITraceCacheScaleFactor;
	cachedResult.rgb = tracedTexture0[uint3(voxelCheckCoord1)].rgba;

	gi.rgb = lerp(gi.rgb, cachedResult, 0.75);

	//Output for debug option 'Trace ONLY path cache'
	if (visualizeGIPathCache) gi.rgb = cachedResult.rgb;
	
	//Calculate lighting attribution
	float NdotL = pow(saturate(dot(worldNormal, kernel) * 1.0 - 0.0), 0.5);

	gi *= NdotL;
	skyVisibility *= NdotL;
	skyVisibility *= lerp(saturate(dot(kernel, float3(0.0, 1.0, 0.0)) * 10.0 + 0.0), 1.0, SEGISphericalSkylight);
	float3 skyColor = float3(0.0, 0.0, 0.0);

	float upGradient = saturate(dot(kernel, float3(0.0, 1.0, 0.0)));
	float sunGradient = saturate(dot(kernel, -SEGISunlightVector.xyz));
	skyColor += lerp(SEGISkyColor.rgb * 1.0, SEGISkyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
	skyColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;

	gi.rgb *= GIGain * 0.15;

	gi += skyColor * skyVisibility * skyMult * 10.0;

	return float4(gi.rgb * 0.8, 0.0f);
}





float ReflectionOcclusionPower;
float SkyReflectionIntensity;

float4 SpecularConeTrace(float3 voxelOrigin, float3 kernel, float3 worldNormal, float smoothness, float2 uv, float dither)
{
	float skyVisibility = 1.0;

	float3 gi = float3(0, 0, 0);

	float coneLength = 6.0;
	float coneSizeScalar = lerp(1.3, 0.05, smoothness) * coneLength;

	float3 adjustedKernel = normalize(kernel.xyz + worldNormal.xyz * 0.2 * (1.0 - smoothness));

	int numSamples = (int)(lerp(uint(ReflectionSteps) / uint(5), ReflectionSteps, smoothness));

	//[unroll(32)]
	for (int i = 0; i < numSamples; i++)
	{
		float fi = ((float)i) / numSamples;

		float coneSize = fi * coneSizeScalar;

		float coneDistance = (exp2(fi * coneSizeScalar) - 0.998) / exp2(coneSizeScalar);

		float3 voxelCheckCoord = voxelOrigin.xyz + adjustedKernel.xyz * (coneDistance * 0.12 * coneLength + 0.001);

		float4 giSample = float4(0.0, 0.0, 0.0, 0.0);
		coneSize = pow(coneSize / 5.0, 2.0) * 5.0;
		int mipLevel = floor(coneSize);
		if (mipLevel <= 2)
		{
			if (mipLevel == 0)
			{
				giSample = tex3Dlod(SEGIVolumeLevel0, float4(voxelCheckCoord.xyz, coneSize));
			}
			else if (mipLevel <= 2)
			{
				voxelCheckCoord = TransformClipSpace1(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, coneSize));
			}
			else if (mipLevel == 2)
			{
				voxelCheckCoord = TransformClipSpace2(voxelCheckCoord);
				giSample = tex3Dlod(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, coneSize));
			}
		}

		float occlusion = skyVisibility;

		float falloffFix = fi * 6.0 + 0.6;

		gi.rgb += giSample.rgb * (coneSize * 5.0 + 1.0) * occlusion * 0.5;
		giSample.a *= lerp(saturate(fi / 0.2), 1.0, NearOcclusionStrength);
		skyVisibility *= pow(saturate(1.0 - giSample.a * 0.5), (lerp(4.0, 1.0, smoothness) + coneSize * 0.5) * ReflectionOcclusionPower);
	}

	skyVisibility *= saturate(dot(worldNormal, kernel) * 0.7 + 0.3);
	skyVisibility *= lerp(saturate(dot(kernel, float3(0.0, 1.0, 0.0)) * 10.0), 1.0, SEGISphericalSkylight);

	gi *= saturate(dot(worldNormal, kernel) * 10.0);

	return float4(gi.rgb * 4.0, skyVisibility);
}

float4 VisualConeTrace(float3 voxelOrigin, float3 kernel, float skyVisibility, int volumeLevel)
{
	float3 gi = float3(0, 0, 0);

	float coneLength = 6.0;
	float coneSizeScalar = 0.25 * coneLength;

	for (int i = 0; i < 200; i++)
	{
		float fi = ((float)i) / 200;

		if (skyVisibility <= 0.0)
			break;

		float coneSize = fi * coneSizeScalar;

		float3 voxelCheckCoord = voxelOrigin.xyz + kernel.xyz * (0.18 * coneLength * fi * fi + 0.05);

		float4 giSample = float4(0.0, 0.0, 0.0, 0.0);


		if (volumeLevel == 0)
			giSample = tex3Dlod(SEGIVolumeLevel0, float4(voxelCheckCoord.xyz, 0.0));
		else if (volumeLevel == 1)
			giSample = tex3Dlod(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, 0.0));
		else if (volumeLevel == 2)
			giSample = tex3Dlod(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, 0.0));
		else if (volumeLevel == 3)
			giSample = tex3Dlod(SEGIVolumeLevel3, float4(voxelCheckCoord.xyz, 0.0));
		else if (volumeLevel == 4)
			giSample = tex3Dlod(SEGIVolumeLevel4, float4(voxelCheckCoord.xyz, 0.0));
		else
			giSample = tex3Dlod(SEGIVolumeLevel5, float4(voxelCheckCoord.xyz, 0.0));


		if (voxelCheckCoord.x < 0.0 || voxelCheckCoord.x > 1.0 ||
			voxelCheckCoord.y < 0.0 || voxelCheckCoord.y > 1.0 ||
			voxelCheckCoord.z < 0.0 || voxelCheckCoord.z > 1.0)
		{
			giSample = float4(0, 0, 0, 0);
		}


		float occlusion = skyVisibility;

		float falloffFix = fi * 6.0 + 0.6;

		gi.rgb += giSample.rgb * (coneSize * 5.0 + 1.0) * occlusion * 0.5;
		skyVisibility *= saturate(1.0 - giSample.a);
	}

	return float4(gi.rgb, skyVisibility);
}

float3 GetWorldNormal(float2 screenspaceUV)
{
#if defined(VRWORKS)
	float4 dn = tex2D(VRWorksGetDepthNormalsSampler(), VRWorksRemapUV(screenspaceUV));
#else
	float4 dn = tex2D(_CameraDepthNormalsTexture, screenspaceUV);
#endif
	float3 n = DecodeViewNormalStereo(dn);
	float3 worldN = mul((float3x3)CameraToWorld, n);

	return worldN;
}

// 9-tap Gaussian filter with linear sampling
// http://rastergrid.com/blog/2010/09/efficient-gaussian-blur-with-linear-sampling/
/*half4 gaussian_filter(float2 stride, uint3 coord)
{
	float4 s = RG1[uint3(coord)] * 0.227027027;

	float2 d1 = stride * 1.3846153846;
	s += RG1[uint3(coord)] * 0.3162162162;
	s += RG1[uint3(coord)] * 0.3162162162;

	float2 d2 = stride * 3.2307692308;
	s += RG1[uint3(coord)] * 0.0702702703;
	s += RG1[uint3(coord)] * 0.0702702703;

	return s;
}*/