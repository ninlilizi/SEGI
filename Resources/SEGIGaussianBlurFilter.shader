Shader "Hidden/SEGI Gaussian Blur Filter"
{
    Properties
    {
        _MainTex("-", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

	UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
    float4 _MainTex_TexelSize;

	struct v2f
	{
		UNITY_VERTEX_OUTPUT_STEREO
	};

	v2f vert(appdata_img v)
	{
		v2f o;

		UNITY_SETUP_INSTANCE_ID(v);
		UNITY_TRANSFER_INSTANCE_ID(v2f, o);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

		return o;
	}

    // 9-tap Gaussian filter with linear sampling
    // http://rastergrid.com/blog/2010/09/efficient-gaussian-blur-with-linear-sampling/
    half4 gaussian_filter(float2 uv, float2 stride)
    {
        half4 s = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv) * 0.227027027;

        float2 d1 = stride * 1.3846153846;
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + d1) * 0.3162162162;
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv - d1) * 0.3162162162;

        float2 d2 = stride * 3.2307692308;
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + d2) * 0.0702702703;
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv - d2) * 0.0702702703;

        return s;
    }

    // Quarter downsampler
    half4 frag_quarter(v2f_img i) : SV_Target
    {
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, -1);
        half4 s;
        s  = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv) + d.xy);
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv) + d.xw);
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv) + d.zy);
        s += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, UnityStereoTransformScreenSpaceTex(i.uv) + d.zw);
        return s * 0.25;
    }

    // Separable Gaussian filters
    half4 frag_blur_h(v2f_img i) : SV_Target
    {
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        return gaussian_filter(UnityStereoTransformScreenSpaceTex(i.uv), float2(_MainTex_TexelSize.x, 0));
    }

    half4 frag_blur_v(v2f_img i) : SV_Target
    {
		UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        return gaussian_filter(UnityStereoTransformScreenSpaceTex(i.uv), float2(0, _MainTex_TexelSize.y));
    }

    ENDCG

    Subshader
    {

        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_quarter
			#pragma multi_compile_instancing

            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_blur_h
			#pragma multi_compile_instancing
            #pragma target 5.0

            ENDCG
        }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_blur_v
			#pragma multi_compile_instancing
            #pragma target 5.0

            ENDCG
        }
    }
}
