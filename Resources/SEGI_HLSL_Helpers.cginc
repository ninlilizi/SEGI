//UNITY_SHADER_NO_UPGRADE

static float4x4 unity_MatrixMVP = mul(unity_MatrixVP, unity_ObjectToWorld);
#define UNITY_MATRIX_MVP    unity_MatrixMVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_VP unity_MatrixVP

CBUFFER_START(UnityPerDraw)
    float4x4 unity_WorldToObject;
    float4 unity_LODFade; // x is the fade value ranging within [0,1]. y is x quantized into 16 levels
    float4 unity_WorldTransformParams; // w is usually 1.0, or -1.0 for odd-negative scale transforms
CBUFFER_END

CBUFFER_START(UnityPerFrame)

float4 glstate_lightmodel_ambient;
float4 unity_AmbientSky;
float4 unity_AmbientEquator;
float4 unity_AmbientGround;
float4 unity_IndirectSpecColor;

#if !defined(USING_STEREO_MATRICES)
float4x4 glstate_matrix_projection;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
//float4x4 unity_MatrixVP;
//int unity_StereoEyeIndex;
#endif

float4 unity_ShadowColor;

CBUFFER_END

CBUFFER_START(UnityLighting)

#ifdef USING_DIRECTIONAL_LIGHT
half4 _WorldSpaceLightPos0;
#else
float4 _WorldSpaceLightPos0;
#endif

CBUFFER_END


// HLSLSupport.cginc Cubemaps
#define UNITY_DECLARE_TEXCUBE(tex) TextureCube tex; SamplerState sampler##tex
#define UNITY_ARGS_TEXCUBE(tex) TextureCube tex, SamplerState sampler##tex
#define UNITY_PASS_TEXCUBE(tex) tex, sampler##tex
#define UNITY_PASS_TEXCUBE_SAMPLER(tex,samplertex) tex, sampler##samplertex
#define UNITY_PASS_TEXCUBE_SAMPLER_LOD(tex, samplertex, lod) tex, sampler##samplertex, lod
#define UNITY_DECLARE_TEXCUBE_NOSAMPLER(tex) TextureCube tex
#define UNITY_SAMPLE_TEXCUBE(tex,coord) tex.Sample (sampler##tex,coord)
#define UNITY_SAMPLE_TEXCUBE_LOD(tex,coord,lod) tex.SampleLevel (sampler##tex,coord, lod)
#define UNITY_SAMPLE_TEXCUBE_SAMPLER(tex,samplertex,coord) tex.Sample (sampler##samplertex,coord)
#define UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(tex, samplertex, coord, lod) tex.SampleLevel (sampler##samplertex, coord, lod)
//END HLSLSupport.cginc Cubemaps


//UnityShaderVariables.cginc Cubemaps
UNITY_DECLARE_TEXCUBE(unity_SpecCube0);
UNITY_DECLARE_TEXCUBE_NOSAMPLER(unity_SpecCube1);

CBUFFER_START(UnityReflectionProbes)
float4 unity_SpecCube0_BoxMax;
float4 unity_SpecCube0_BoxMin;
float4 unity_SpecCube0_ProbePosition;
half4  unity_SpecCube0_HDR;

float4 unity_SpecCube1_BoxMax;
float4 unity_SpecCube1_BoxMin;
float4 unity_SpecCube1_ProbePosition;
half4  unity_SpecCube1_HDR;
CBUFFER_END
//END UnityShaderVariables.cginc Cubemaps


//UnityCG.cginc - Decode Cubemaps
// Decodes HDR textures
// handles dLDR, RGBM formats
inline half3 DecodeHDR(half4 data, half4 decodeInstructions)
{
	// Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
	half alpha = decodeInstructions.w * (data.a - 1.0) + 1.0;

	// If Linear mode is not supported we can skip exponent part
#if defined(UNITY_COLORSPACE_GAMMA)
	return (decodeInstructions.x * alpha) * data.rgb;
#else
#   if defined(UNITY_USE_NATIVE_HDR)
	return decodeInstructions.x * data.rgb; // Multiplier for future HDRI relative to absolute conversion.
#   else
	return (decodeInstructions.x * PositivePow(alpha, decodeInstructions.y)) * data.rgb;
#   endif
#endif
}
//ENDUnityCG.cginc - Decode Cubemaps

inline float4 ComputeNonStereoScreenPos(float4 pos) {
	float4 o = pos * 0.5f;
	o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w;
	o.zw = pos.zw;
	return o;
}

inline float4 ComputeScreenPos(float4 pos) {
	float4 o = ComputeNonStereoScreenPos(pos);
#if defined(UNITY_SINGLE_PASS_STEREO)
	o.xy = TransformStereoScreenSpaceTex(o.xy, pos.w);
#endif
	return o;
}

// Tranforms position from object to homogenous space -- CG Includes added for SRP conversion
inline float4 UnityObjectToClipPos(in float3 pos)
{
	// More efficient than computing M*VP matrix product
	return mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(pos, 1.0)));
}
inline float4 UnityObjectToClipPos(float4 pos) // overload for float4; avoids "implicit truncation" warning for existing shaders
{
	return UnityObjectToClipPos(pos.xyz);
}

// Transforms normal from object to world space
inline float3 UnityObjectToWorldNormal( in float3 norm )
{
#ifdef UNITY_ASSUME_UNIFORM_SCALING
    return UnityObjectToWorldDir(norm);
#else
    // mul(IT_M, norm) => mul(norm, I_M) => {dot(norm, I_M.col0), dot(norm, I_M.col1), dot(norm, I_M.col2)}
    return normalize(mul(norm, (float3x3)unity_WorldToObject));
#endif
}

// Convert rgb to luminance
// with rgb in linear space with sRGB primaries and D65 white point
half LinearRgbToLuminance(half3 linearRgb)
{
	return dot(linearRgb, half3(0.2126729f, 0.7151522f, 0.0721750f));
}

//Colospace conversion
float Epsilon = 1e-10;

// Encoding/decoding [0..1) floats into 8 bit/channel RGBA. Note that 1.0 will not be encoded properly.
inline float4 EncodeFloatRGBA(float v)
{
	float4 kEncodeMul = float4(1.0, 255.0, 65025.0, 160581375.0);
	float kEncodeBit = 1.0 / 255.0;
	float4 enc = kEncodeMul * v;
	enc = frac(enc);
	enc -= enc.yzww * kEncodeBit;
	return enc;
}
inline float DecodeFloatRGBA(float4 enc)
{
	float4 kDecodeDot = float4(1.0, 1 / 255.0, 1 / 65025.0, 1 / 160581375.0);
	return dot(enc, kDecodeDot);
}

float3 rgb2hsv(float3 c)
{
	float4 k = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	float4 p = lerp(float4(c.bg, k.wz), float4(c.gb, k.xy), step(c.b, c.g));
	float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;

	return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 hsv2rgb(float3 c)
{
	float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	float3 p = abs(frac(c.xxx + k.xyz) * 6.0 - k.www);
	return c.z * lerp(k.xxx, saturate(p - k.xxx), c.y);
}
//END Colospace conversion


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

uint EncodeRGBAuint(float4 color)
{
	//7[HHHHHHH] 7[SSSSSSS] 11[VVVVVVVVVVV] 7[AAAAAAAA]
	float3 hsv = rgb2hsv(color.rgb);
	hsv.z = pow(hsv.z, 1.0 / 3.0);

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

void interlockedAddFloat4(RWTexture3D<uint> destination, int3 coord, float4 value)
{
	uint writeValue = EncodeRGBAuint(value);
	uint compareValue = 0;
	uint originalValue;

	[allow_uav_condition]
	while (true)
	{
		InterlockedCompareExchange(destination[coord], compareValue, writeValue, originalValue);
		if (compareValue == originalValue)
			break;
		compareValue = originalValue;
		float4 originalValueFloats = DecodeRGBAuint(originalValue);
		writeValue = EncodeRGBAuint(originalValueFloats + value);
	}
}

void interlockedAddInt(RWTexture3D<uint> destination, int3 coord, int value)
{
	uint writeValue = value;
	uint compareValue = 0;
	uint originalValue;

	[allow_uav_condition]
	while (true)
	{
		InterlockedCompareExchange(destination[coord], compareValue, writeValue, originalValue);
		if (compareValue == originalValue)
			break;
		compareValue = originalValue;
		float4 originalValueFloats = originalValue;
		writeValue = originalValueFloats + value;
	}
}

void interlockedSetFloat4(RWTexture3D<uint> destination, int3 coord, float4 value)
{
	uint writeValue = EncodeRGBAuint(value);
	uint compareValue = 0;
	uint originalValue;

	[allow_uav_condition]
	while (true)
	{
		InterlockedCompareExchange(destination[coord], compareValue, writeValue, originalValue);
		if (compareValue == originalValue)
			break;
		compareValue = originalValue;
		//writeValue = EncodeRGBAuint(value);
	}
}