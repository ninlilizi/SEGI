//UNITY_SHADER_NO_UPGRADE

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

float3 rgb2hcv(in float3 RGB)
{
	// Based on work by Sam Hocevar and Emil Persson
	float4 P = lerp(float4(RGB.bg, -1.0, 2.0 / 3.0), float4(RGB.gb, 0.0, -1.0 / 3.0), step(RGB.b, RGB.g));
	float4 Q = lerp(float4(P.xyw, RGB.r), float4(RGB.r, P.yzx), step(P.x, RGB.r));
	float C = Q.x - min(Q.w, Q.y);
	float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
	return float3(H, C, Q.x);
}

float3 rgb2hsl(in float3 RGB)
{
	float3 HCV = rgb2hcv(RGB);
	float L = HCV.z - HCV.y * 0.5;
	float S = HCV.y / (1 - abs(L * 2 - 1) + Epsilon);
	return float3(HCV.x, S, L);
}

float3 hsl2rgb(float3 c)
{
	c = float3(frac(c.x), clamp(c.yz, 0.0, 1.0));
	float3 rgb = clamp(abs(fmod(c.x * 6.0 + float3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
	return c.z + c.y * (rgb - 0.5) * (1.0 - abs(2.0 * c.z - 1.0));
}
//END Colospace conversion
