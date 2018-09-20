#define PI 3.14159265

float SEGIVoxelScaleFactor;

int StochasticSampling;
int TraceDirections;
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

half4 GISunColor;

UNITY_DECLARE_TEX3D(SEGIVolumeLevel0);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel1);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel2);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel3);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel4);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel5);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel6);
UNITY_DECLARE_TEX3D(SEGIVolumeLevel7);
UNITY_DECLARE_TEX3D(VolumeTexture1);
UNITY_DECLARE_TEX3D(VolumeTexture2);
UNITY_DECLARE_TEX3D(VolumeTexture3);

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

float reflectionProbeIntensity;
int useReflectionProbes;
int ReflectionSteps;
int ForwardPath;


uniform half4 _MainTex_TexelSize;

float4x4 WorldToCamera;
float4x4 ProjectionMatrix;
float4x4 ProjectionMatrixInverse;

UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_Albedo);
UNITY_DECLARE_SCREENSPACE_TEXTURE(PreviousGITexture);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraGBufferTexture0);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraGBufferTexture1);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraGBufferTexture2);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraMotionVectorsTexture);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture);
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

int SEGISphericalSkylight;

//Fix Stereo View Matrix
float4x4 _LeftEyeProjection;
float4x4 _RightEyeProjection;
float4x4 _LeftEyeToWorld;
float4x4 _RightEyeToWorld;
//Fix Stereo View Matrix/

float3 TransformClipSpace(float3 pos, float4 transform)
{
	pos = pos * 2.0 - 1.0;
	pos *= transform.w;
	pos = pos * 0.5 + 0.5;
	pos -= transform.xyz;

	return pos;
}

float3 TransformClipSpace0(float3 pos)
{
	return TransformClipSpace(pos, SEGIClipTransform0);
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
	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, float4(coord.x, coord.y, 0.0, 0.0)).x;

#if defined(UNITY_REVERSED_Z)
	depth = 1.0 - depth;
#endif

	//Fix Stereo View Matrix
	float d = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv)); // non-linear Z
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
	float4 clipPos = float4(uvClip, d, 1.0);
	float4 viewPos = mul(proj, clipPos); // inverse projection by clip position
	viewPos /= viewPos.w; // perspective division
	float3 worldPos = mul(eyeToWorld, viewPos).xyz;
	//Fix Stereo View Matrix/

	float4 viewPosition = mul(ProjectionMatrixInverse, float4(coord.x * 2.0 - 1.0, coord.y * 2.0 - 1.0, 2.0 * depth - 1.0, 1.0));
	viewPosition /= viewPosition.w;

	return viewPosition;
}

float3 ProjectBack(float4 viewPos)
{
	viewPos = mul(ProjectionMatrix, float4(viewPos.xyz, 0.0));
	viewPos.xyz /= viewPos.w;
	viewPos.xyz = viewPos.xyz * 0.5 + 0.5;
	return viewPos.xyz;
}


float2 rand(float2 coord)
{
	float noiseX = saturate(frac(sin(dot(coord, float2(12.9898, 78.223))) * 43758.5453));
	float noiseY = saturate(frac(sin(dot(coord, float2(12.9898, 78.223)*2.0)) * 43758.5453));
	return float2(noiseX, noiseY);
}

float2 rand(float3 coord)
{
	float noiseX = saturate(frac(sin(dot(coord, float3(12.9898, 78.223, 35.3820))) * 43758.5453));
	float noiseY = saturate(frac(sin(dot(coord, float3(12.9898, 78.223, 35.2879)*2.0)) * 43758.5453));
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

float4 ConeTrace(float3 voxelOrigin, float3 kernel, float3 worldNormal, float2 uv, float dither, int steps, float width, float lengthMult, float skyMult, float3 viewDir)
{
	float skyVisibility = 1.0;

	float3 gi = float3(0, 0, 0);

	int numSteps = (int)(steps * lerp(SEGIVoxelScaleFactor, 1.0, 0.5));

	float3 adjustedKernel = normalize(kernel.xyz + worldNormal.xyz * 0.00 * width);

	float dist = length(voxelOrigin * 2.0 - 1.0);


	int startMipLevel = 0;

	voxelOrigin.xyz += worldNormal.xyz * 0.016 * (exp2(startMipLevel) - 1);

	for (int i = 0; i < numSteps; i++)
	{
		float fi = ((float)i + dither) / numSteps;
		fi = lerp(fi, 1.0, 0.0);


		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;


		float coneSize = coneDistance * width * 10.3;

		float3 voxelCheckCoord = voxelOrigin.xyz + adjustedKernel.xyz * (coneDistance * 1.12 * TraceLength * lengthMult + 0.000);


		float4 giSample = float4(0.0, 0.0, 0.0, 0.0);
		int mipLevel = max(startMipLevel, log2(pow(fi, 1.3) * 24.0 * width + 1.0));
		if (mipLevel == 0)
		{
			voxelCheckCoord = TransformClipSpace0(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel0, float4(float3(voxelCheckCoord.xy, voxelCheckCoord.z), coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		if (mipLevel == 1)
		{
			voxelCheckCoord = TransformClipSpace1(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else if (mipLevel == 2)
		{
			voxelCheckCoord = TransformClipSpace2(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else if (mipLevel == 3)
		{
			voxelCheckCoord = TransformClipSpace3(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel3, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else if (mipLevel == 4)
		{
			voxelCheckCoord = TransformClipSpace4(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel4, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else
		{
			voxelCheckCoord = TransformClipSpace5(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel5, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}

		float occlusion = skyVisibility;

		float falloffFix = pow(fi, 1.0) * 4.0 + NearLightGain;

		giSample.a *= lerp(saturate(coneSize / 1.0), 1.0, NearOcclusionStrength);
		giSample.a *= (0.8 / (fi * fi * 2.0 + 0.15));
		gi.rgb += giSample.rgb * occlusion * (coneDistance + NearLightGain) * 80.0 * (1.0 - fi * fi);

		skyVisibility *= pow(saturate(1.0 - giSample.a * OcclusionStrength * (1.0 + coneDistance * FarOcclusionStrength)), 1.0 * OcclusionPower);
	}
	float NdotL = pow(saturate(dot(worldNormal, kernel) * 1.0 - 0.0), 0.5);

	gi *= NdotL;
	skyVisibility *= NdotL;
	if (StochasticSampling > 0)
	{
		skyVisibility *= lerp(saturate(dot(kernel, float3(0.0, 1.0, 0.0)) * 10.0 + 0.0), 1.0, SEGISphericalSkylight);
	}
	else
	{
		skyVisibility *= lerp(saturate(dot(kernel, float3(0.0, 1.0, 0.0)) * 10.0 + 0.0), 1.0, SEGISphericalSkylight);
	}

	float3 skyColor = float3(0.0, 0.0, 0.0);

	float upGradient = saturate(dot(kernel, float3(0.0, 1.0, 0.0)));
	float sunGradient = saturate(dot(kernel, -SEGISunlightVector.xyz));

	if (useReflectionProbes  && ForwardPath)
	{
		float3 reflectedDir = reflect(viewDir, worldNormal);
		half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, worldNormal, 0);
		half3 probeColor = DecodeHDR(probeData, unity_SpecCube0_HDR);

		//half4 lightColor = UNITY_SAMPLE_TEX2D(_LightmapTexture, uv);

		skyColor += lerp(SEGISkyColor.rgb * 1.0, SEGISkyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
		skyColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;
		//probeColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;
		//probeColor = (skyColor.rgb + probeColor.rgb) * 0.25;
		probeColor = lerp(skyColor.rgb * 0.5, (0.5).xxx, (probeColor.rgb * 0.5) * reflectionProbeIntensity);

		gi.rgb *= GIGain * 0.15;
		gi += probeColor * skyVisibility * 10.0;
	}
	else
	{
		skyColor += lerp(SEGISkyColor.rgb * 1.0, SEGISkyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
		skyColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;

		gi.rgb *= GIGain * 0.15;
		gi += skyColor * skyVisibility * 10.0;
	}

	return float4(gi.rgb * 0.8, 0.0f);
}




float ReflectionOcclusionPower;
float SkyReflectionIntensity;

float4 SpecularConeTrace(float3 voxelOrigin, float3 kernel, float3 worldNormal, float smoothness, float2 uv, float dither, float3 viewDir)
{
	float skyVisibility = 1.0;

	float3 gi = float3(0, 0, 0);

	float coneLength = 6.0;
	float coneSizeScalar = lerp(1.3, 0.05, smoothness) * coneLength;

	float3 adjustedKernel = normalize(kernel.xyz + worldNormal.xyz * 0.2 * (1.0 - smoothness));

	int numSamples = (int)(lerp(uint(ReflectionSteps) / uint(5), ReflectionSteps, smoothness));

	float4 giSample;
	for (int i = 0; i < numSamples; i++)
	{
		float fi = ((float)i) / numSamples;

		float coneSize = fi * coneSizeScalar;

		float coneDistance = (exp2(fi * coneSizeScalar) - 0.998) / exp2(coneSizeScalar);

		float3 voxelCheckCoord = voxelOrigin.xyz + adjustedKernel.xyz * (coneDistance * 0.12 * coneLength + 0.001);

		giSample = float4(0.0, 0.0, 0.0, 0.0);
		coneSize = pow(coneSize / 5.0, 2.0) * 5.0;
		int mipLevel = floor(coneSize);
		if (mipLevel == 0)
		{
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel0, float4(voxelCheckCoord.xyz, coneSize), 0);
		}
		else if (mipLevel == 1)
		{
			voxelCheckCoord = TransformClipSpace1(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, coneSize), 0);
		}
		else if (mipLevel == 2)
		{
			voxelCheckCoord = TransformClipSpace2(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, coneSize), 0);
		}
		else if (mipLevel == 3)
		{
			voxelCheckCoord = TransformClipSpace3(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel3, float4(voxelCheckCoord.xyz, coneSize), 0);
		}
		else if (mipLevel == 4)
		{
			voxelCheckCoord = TransformClipSpace4(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel4, float4(voxelCheckCoord.xyz, coneSize), 0);
		}
		else
		{
			voxelCheckCoord = TransformClipSpace5(voxelCheckCoord);
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel5, float4(voxelCheckCoord.xyz, coneSize), 0);
		}

		float occlusion = skyVisibility;

		float falloffFix = fi * 6.0 + 0.6;

		gi.rgb += giSample.rgb * (coneSize * 5.0 + 1.0) * occlusion * 0.5;
		giSample.a *= lerp(saturate(fi / 0.2), 1.0, NearOcclusionStrength);
		skyVisibility *= pow(saturate(1.0 - giSample.a * 0.5), (lerp(4.0, 1.0, smoothness) + coneSize * 0.5) * ReflectionOcclusionPower);
	}

	if (ForwardPath)
	{
		float3 reflectedDir = reflect(viewDir, worldNormal);
		half4 probeData = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, worldNormal, 0);
		half3 probeColor = DecodeHDR(probeData, unity_SpecCube0_HDR);

		//half4 lightColor = UNITY_SAMPLE_TEX2D(_LightmapTexture, uv);

		//skyColor += lerp(SEGISkyColor.rgb * 1.0, SEGISkyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
		//skyColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;
		//probeColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;
		//probeColor = (skyColor.rgb + probeColor.rgb) * 0.25;
		probeColor = (probeColor.rgb * 0.5) * (1 - reflectionProbeIntensity);

		//gi.rgb *= GIGain * 0.15;
		gi = (gi + probeColor) * 0.5;// *skyVisibility * skyMult * 10.0;
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
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel0, float4(voxelCheckCoord.xyz, 0.0), 0);
		else if (volumeLevel == 1)
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, 0.0), 0);
		else if (volumeLevel == 2)
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, 0.0), 0);
		else if (volumeLevel == 3)
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel3, float4(voxelCheckCoord.xyz, 0.0), 0);
		else if (volumeLevel == 4)
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel4, float4(voxelCheckCoord.xyz, 0.0), 0);
		else
			giSample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel5, float4(voxelCheckCoord.xyz, 0.0), 0);


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

float3 rgb2hsv(float3 c)
{
	const float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
	float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

	float d = q.x - min(q.w, q.y);
	const float e = 1.0e-10;

	return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 hsv2rgb(float3 c)
{
	const float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	float3 p = abs(frac(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * lerp(k.xxx, saturate(p - k.xxx), c.y);
}

//Split normal up in 10 - 12 - 10 bits
//1010000111 111101000011 1010100110

float3 DecodeVectorUint(uint value)
{
	const uint maskX = ((1 << 10) - 1);
	uint tempX = (value & maskX);

	const uint maskY = ((1 << 12) - 1) << 10;
	uint tempY = (value & maskY) >> 10;

	const uint maskZ = ((1 << 10) - 1) << 22;
	uint tempZ = ((value & maskZ) >> 22) & ((1 << 10) - 1);

	const float div = 1.0f / 1023.0f;

	float3 normal = float3(tempX * div, tempY / 4095.0f, tempZ * div);
	normal = normal * 2.0f - 1.0f;
	return normal;
}

uint EncodeVectorUint(float3 normal)
{
	normal = (normal + 1.0f) * 0.5f;
	return (uint)(normal.x * 1023) + ((uint)(normal.y * 4095) << 10) + ((uint)(normal.z * 1023) << 22);
}

float4 DecodeRGBAuint(uint value)
{
	//const float div = 1.0f / 255.0f;
	//float4 colorOut = float4((value & 0xFF) * div, ((value & 0xFFFF) >> 8) * div, ((value & 0xFFFFFF) >> 16) * div, (value >> 24) * div);
	//colorOut.a *= 2.0;
	//return colorOut;

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

uint EncodeRGBAuint(float4 color)
{
	//return uint(color.r * 255) + (uint(color.g * 255) << 8) + (uint(color.b * 255) << 16) + (uint(color.a * 255) << 24);

	//7[HHHHHHH] 7[SSSSSSS] 11[VVVVVVVVVVV] 7[AAAAAAAA]
	float3 hsv = rgb2hsv(color.rgb);
	hsv.z = pow(abs(hsv.z), 1.0 / 3.0);

	uint result = 0;

	uint a = min(127, uint(color.a / 2.0));
	uint v = min(2047, uint((hsv.z / 10.0) * 2047));
	uint s = uint(hsv.y * 127);
	uint h = uint(hsv.x * 127);

	result += a;
	result += v * 0x00000080; // << 7
	result += s * 0x00040000; // << 18
	result += h * 0x02000000; // << 25

	return result;
}

float4 SEGI_GRID_SIZE;

void interlockedAddFloat4(RWTexture2D<uint> destination, uint2 coord, float4 value)
{
	uint writeValue = EncodeRGBAuint(value);
	InterlockedMax(destination[coord], writeValue);
	//uint compareValue = 0;
	//uint originalValue;

	//[allow_uav_condition]
	//for (int i = 0; i < 12; i++) //performance?
	////while (true)
	//{
	//	InterlockedCompareExchange(destination[coord], compareValue, writeValue, originalValue);
	//	if (compareValue == originalValue)
	//		break;
	//	compareValue = originalValue;
	//	float4 originalValueFloats = DecodeRGBAuint(originalValue);
	//	writeValue = EncodeRGBAuint(originalValueFloats + value);
	//}
}

void interlockedAddFloat4(RWTexture2DArray<uint> destination, uint3 coord, float4 value)
{
	uint writeValue = EncodeRGBAuint(value);
	InterlockedMax(destination[coord], writeValue);
}

void interlockedAddFloat4(RWTexture2DArray<uint> destination, uint2 coord, float4 value, float3 shaded, float3 emission, float3 normal, uint normalIndex)
{
	uint writeValue = EncodeRGBAuint(value);
	uint writeValue1 = EncodeRGBAuint(float4(shaded, value.a));
	uint writeValue2 = EncodeRGBAuint(float4(emission, value.a));

	InterlockedMax(destination[uint3(coord, 0)], writeValue);
	InterlockedMax(destination[uint3(coord, 1)], writeValue1);
	InterlockedMax(destination[uint3(coord, 2)], writeValue2);

	if (normalIndex < 3) {
		uint writeValue3 = EncodeVectorUint(normal);
		InterlockedMax(destination[uint3(coord, 3 + normalIndex)], writeValue3);
	}
}

void interlockedAddFloat4b(RWTexture2D<uint> destination, uint2 coord, float4 value)
{
	uint writeValue = EncodeRGBAuint(value);
	InterlockedAdd(destination[coord], writeValue);
	//uint compareValue = 0;
	//uint originalValue;

	//[allow_uav_condition]
	//[unroll(1)]
	//for (int i = 0; i < 1; i++) //better performance
	////while (true)
	//{
	//	InterlockedCompareExchange(destination[coord], compareValue, writeValue, originalValue);
	//	if (compareValue == originalValue)
	//		break;
	//	compareValue = originalValue;
	//	float4 originalValueFloats = DecodeRGBAuint(originalValue);
	//	writeValue = EncodeRGBAuint(originalValueFloats + value);
	//}
}

void interlockedAddFloat4c(RWTexture2DArray<uint> destination, uint2 coord, float4 value)
{
	uint writeValue = EncodeRGBAuint(value);
	InterlockedAdd(destination[uint3(coord, 0)], writeValue);
}

//float4x4 SEGIVoxelToGIProjection;
//float4x4 SEGIVoxelProjectionInverse;
//sampler2D SEGIGIDepthNormalsTexture;
//int SEGIFrameSwitch;
int SEGISecondaryCones;
float SEGISecondaryOcclusionStrength;

//sampler3D SEGIVolumeTexture0;
//int SEGIVoxelAA;

float4 SEGICurrentClipTransform;
float4 SEGIClipmapOverlap;

float3 TransformClipSpaceInverse(float3 pos, float4 transform)
{
	pos += transform.xyz;
	pos = pos * 2.0 - 1.0;
	pos /= transform.w;
	pos = pos * 0.5 + 0.5;

	return pos;
}

float4 ConeTrace(float3 voxelOrigin, float3 kernel, float3 worldNormal)
{


	float skyVisibility = 1.0;

	float3 gi = float3(0, 0, 0);

	const int numSteps = 7;

	float3 adjustedKernel = normalize(kernel + worldNormal * 0.2);



	float dist = length(voxelOrigin * 2.0 - 1.0);

	int startMipLevel = 0;

	voxelOrigin = TransformClipSpaceInverse(voxelOrigin, SEGICurrentClipTransform);
	voxelOrigin.xyz += worldNormal.xyz * 0.016;


	const float width = 3.38;
	const float farOcclusionStrength = 4.0;
	const float occlusionPower = 1.05;


	for (int i = 0; i < numSteps; i++)
	{
		float fi = ((float)i) / numSteps;
		fi = abs(lerp(fi, 1.0, 0.001));//TODO abs() needed for pow -> replace pow?

		float coneDistance = (exp2(fi * 4.0) - 0.99) / 8.0;

		float coneSize = coneDistance * width * 10.3;

		float3 voxelCheckCoord = voxelOrigin.xyz + adjustedKernel.xyz * (coneDistance * 1.12 * 1.0);

		float4 sample = float4(0.0, 0.0, 0.0, 0.0);
		int mipLevel = floor(coneSize);

		mipLevel = max(startMipLevel, log2(pow(fi, 1.3) * 24.0 * width + 1.0));



		if (mipLevel == 0 || mipLevel == 1)
		{
			voxelCheckCoord = TransformClipSpace1(voxelCheckCoord);
			sample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel1, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else if (mipLevel == 2)
		{
			voxelCheckCoord = TransformClipSpace2(voxelCheckCoord);
			sample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel2, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else if (mipLevel == 3)
		{
			voxelCheckCoord = TransformClipSpace3(voxelCheckCoord);
			sample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel3, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else if (mipLevel == 4)
		{
			voxelCheckCoord = TransformClipSpace4(voxelCheckCoord);
			sample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel4, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}
		else
		{
			voxelCheckCoord = TransformClipSpace5(voxelCheckCoord);
			sample = UNITY_SAMPLE_TEX3D_LOD(SEGIVolumeLevel5, float4(voxelCheckCoord.xyz, coneSize), 0) * GISampleWeight(voxelCheckCoord);
		}

		float occlusion = skyVisibility;

		float falloffFix = pow(fi, 2.0) * 4.0 + 0.0;

		gi.rgb += sample.rgb * (coneSize * 1.0 + 1.0) * occlusion * falloffFix;

		skyVisibility *= pow(saturate(1.0 - sample.a * SEGISecondaryOcclusionStrength * (1.0 + coneDistance * farOcclusionStrength)), 1.0 * occlusionPower);


	}


	float NdotL = pow(saturate(dot(worldNormal, kernel) * 1.0 - 0.0), 1.0);

	gi *= NdotL;
	skyVisibility *= NdotL;

	skyVisibility *= lerp(saturate(dot(kernel, float3(0.0, 1.0, 0.0)) * 10.0 + 0.0), 1.0, SEGISphericalSkylight);

	float3 skyColor = float3(0.0, 0.0, 0.0);

	float upGradient = saturate(dot(kernel, float3(0.0, 1.0, 0.0)));
	float sunGradient = saturate(dot(kernel, -SEGISunlightVector.xyz));

	//float3 probe = unity_SpecCube0.Sample(samplerunity_SpecCube0, worldNormal).rgb;//UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldNormal);
	//probe = DecodeHDR(probe, unity_SpecCube0_HDR);

	skyColor += lerp(SEGISkyColor.rgb * 1.0, SEGISkyColor.rgb * 0.5, pow(upGradient, (0.5).xxx));
	skyColor += GISunColor.rgb * pow(sunGradient, (4.0).xxx) * SEGISoftSunlight;


	gi += skyColor * skyVisibility * 10.0;

	return float4(gi.rgb, 0.0f);
}
float3 GetWorldNormal(float2 screenspaceUV)
{
	float4 dn = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthNormalsTexture, screenspaceUV);
	float3 n = DecodeViewNormalStereo(dn);
	float3 worldN = mul((float3x3)CameraToWorld, n);

	return worldN;
}