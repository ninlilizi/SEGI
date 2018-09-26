//UNITY_SHADER_NO_UPGRADE

#define UNITY_MATRIX_P glstate_matrix_projection
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
