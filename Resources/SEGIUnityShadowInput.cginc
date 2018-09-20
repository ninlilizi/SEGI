#define SEGI_UNITY_SHADOWMAP_OFF

//float4 SEGISunDepth_TexelSize;
float4 SEGIMinPosVoxel;
sampler2D SEGIShadowmapCopy;

#ifndef UNITY_SHADER_VARIABLES_INCLUDED

#define UNITY_REVERSED_Z //TODO

struct ShadowParams
{
	float4x4 worldToShadow[4];
	float4 shadowSplitSpheres[4];
	float4 shadowSplitSqRadii;
};
StructuredBuffer<ShadowParams> _ShadowParams;

inline float4 getCascadeWeights_splitSpheres(float3 wpos)
{
	float3 fromCenter0 = wpos.xyz - _ShadowParams[0].shadowSplitSpheres[0].xyz;
	float3 fromCenter1 = wpos.xyz - _ShadowParams[0].shadowSplitSpheres[1].xyz;
	float3 fromCenter2 = wpos.xyz - _ShadowParams[0].shadowSplitSpheres[2].xyz;
	float3 fromCenter3 = wpos.xyz - _ShadowParams[0].shadowSplitSpheres[3].xyz;
	float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));
	float4 weights = float4(distances2 < _ShadowParams[0].shadowSplitSqRadii);
	weights.yzw = saturate(weights.yzw - weights.xyz);
	return weights;
}

/**
* Returns the shadowmap coordinates for the given fragment based on the world position and z-depth.
* These coordinates belong to the shadowmap atlas that contains the maps for all cascades.
*/
inline float4 getShadowCoord(float4 wpos, float4 cascadeWeights)
{
	float3 sc0 = mul(_ShadowParams[0].worldToShadow[0], wpos).xyz;
	float3 sc1 = mul(_ShadowParams[0].worldToShadow[1], wpos).xyz;
	float3 sc2 = mul(_ShadowParams[0].worldToShadow[2], wpos).xyz;
	float3 sc3 = mul(_ShadowParams[0].worldToShadow[3], wpos).xyz;
	float4 shadowMapCoordinate = float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);
#if defined(UNITY_REVERSED_Z)
	float  noCascadeWeights = 1 - dot(cascadeWeights, float4(1, 1, 1, 1));
	shadowMapCoordinate.z += noCascadeWeights;
#endif
	return shadowMapCoordinate;
}

/**
* Same as the getShadowCoord; but optimized for single cascade
*/
inline float4 getShadowCoord_SingleCascade(float4 wpos)
{
	return float4(mul(_ShadowParams[0].worldToShadow[0], wpos).xyz, 0);
}

#else

//SamplerComparisonState samplerSEGISunDepth;

//changed in 5.6
//#if UNITY_VERSION >= 560
//UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
//#else
//sampler2D_float _CameraDepthTexture;
//#endif

// sizes of cascade projections, relative to first one
//float4 unity_ShadowCascadeScales;

//UNITY_DECLARE_SHADOWMAP(_ShadowMapTexture);
//float4 _ShadowMapTexture_TexelSize;

//
// Keywords based defines
//
#if defined (SHADOWS_SPLIT_SPHERES)
#define GET_CASCADE_WEIGHTS(wpos, z)    getCascadeWeights_splitSpheres(wpos)
#else
#define GET_CASCADE_WEIGHTS(wpos, z)	getCascadeWeights( wpos, z )
#endif

#if defined (SHADOWS_SINGLE_CASCADE)
#define GET_SHADOW_COORDINATES(wpos,cascadeWeights)	getShadowCoord_SingleCascade(wpos)
#else
#define GET_SHADOW_COORDINATES(wpos,cascadeWeights)	getShadowCoord(wpos,cascadeWeights)
#endif

// prototypes 
//inline float3 computeCameraSpacePosFromDepth(v2f i);
//inline fixed4 getCascadeWeights(float3 wpos, float z);		// calculates the cascade weights based on the world position of the fragment and plane positions
inline float4 getCascadeWeights_splitSpheres(float3 wpos);	// calculates the cascade weights based on world pos and split spheres positions
inline float4 getShadowCoord_SingleCascade(float4 wpos);	// converts the shadow coordinates for shadow map using the world position of fragment (optimized for single fragment)
inline float4 getShadowCoord(float4 wpos, float4 cascadeWeights);// converts the shadow coordinates for shadow map using the world position of fragment

																 /**
																 * Gets the cascade weights based on the world position of the fragment.
																 * Returns a float4 with only one component set that corresponds to the appropriate cascade.
																 */
//inline fixed4 getCascadeWeights(float3 wpos, float z)
//{
//	fixed4 zNear = float4(z >= _LightSplitsNear);
//	fixed4 zFar = float4(z < _LightSplitsFar);
//	fixed4 weights = zNear * zFar;
//	return weights;
//}

/**
* Gets the cascade weights based on the world position of the fragment and the poisitions of the split spheres for each cascade.
* Returns a float4 with only one component set that corresponds to the appropriate cascade.
*/
inline float4 getCascadeWeights_splitSpheres(float3 wpos)
{
	float3 fromCenter0 = wpos.xyz - unity_ShadowSplitSpheres[0].xyz;
	float3 fromCenter1 = wpos.xyz - unity_ShadowSplitSpheres[1].xyz;
	float3 fromCenter2 = wpos.xyz - unity_ShadowSplitSpheres[2].xyz;
	float3 fromCenter3 = wpos.xyz - unity_ShadowSplitSpheres[3].xyz;
	float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));
	float4 weights = float4(distances2 < unity_ShadowSplitSqRadii);
	weights.yzw = saturate(weights.yzw - weights.xyz);
	return weights;
}

/**
* Returns the shadowmap coordinates for the given fragment based on the world position and z-depth.
* These coordinates belong to the shadowmap atlas that contains the maps for all cascades.
*/
inline float4 getShadowCoord(float4 wpos, float4 cascadeWeights)
{
	float3 sc0 = mul(unity_WorldToShadow[0], wpos).xyz;
	float3 sc1 = mul(unity_WorldToShadow[1], wpos).xyz;
	float3 sc2 = mul(unity_WorldToShadow[2], wpos).xyz;
	float3 sc3 = mul(unity_WorldToShadow[3], wpos).xyz;
	float4 shadowMapCoordinate = float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);
#if defined(UNITY_REVERSED_Z)
	float  noCascadeWeights = 1 - dot(cascadeWeights, float4(1, 1, 1, 1));
	shadowMapCoordinate.z += noCascadeWeights;
#endif
	return shadowMapCoordinate;
}

/**
* Same as the getShadowCoord; but optimized for single cascade
*/
inline float4 getShadowCoord_SingleCascade(float4 wpos)
{
	return float4(mul(unity_WorldToShadow[0], wpos).xyz, 0);
}

//float3 UnityGetReceiverPlaneDepthBias(float3 shadowCoord, float biasMultiply)
//{
//	// Should receiver plane bias be used? This estimates receiver slope using derivatives,
//	// and tries to tilt the PCF kernel along it. However, when doing it in screenspace from the depth texture
//	// (ie all light in deferred and directional light in both forward and deferred), the derivatives are wrong
//	// on edges or intersections of objects, leading to shadow artifacts. Thus it is disabled by default.
//	float3 biasUVZ = 0;

//	float3 dx = ddx(shadowCoord);
//	float3 dy = ddy(shadowCoord);

//	biasUVZ.x = dy.y * dx.z - dx.y * dy.z;
//	biasUVZ.y = dx.x * dy.z - dy.x * dx.z;
//	biasUVZ.xy *= biasMultiply / ((dx.x * dy.y) - (dx.y * dy.x));

//	// Static depth biasing to make up for incorrect fractional sampling on the shadow map grid.
//	//const float UNITY_RECEIVER_PLANE_MIN_FRACTIONAL_ERROR = 0.01f;
//	float fractionalSamplingError = dot(SEGISunDepth_TexelSize.xy, abs(biasUVZ.xy));
//	biasUVZ.z = -min(fractionalSamplingError, 0.1);
//	#if defined(UNITY_REVERSED_Z)
//	biasUVZ.z *= -1;
//	#endif
//	return biasUVZ;
//}
#endif

