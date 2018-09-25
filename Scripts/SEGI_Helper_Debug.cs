using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[ExecuteInEditMode]
public class SEGI_Helper_Debug : MonoBehaviour {

    public bool CollectData = false;

    public int SEGIRenderWidth;
    public int SEGIRenderHeight;

    public RenderTexture RT_FXAART;
    public RenderTexture RT_gi1;
    public RenderTexture RT_gi2;
    public RenderTexture RT_reflections;
    public RenderTexture RT_gi3;
    public RenderTexture RT_gi4;
    public RenderTexture RT_blur0;
    public RenderTexture RT_blur1;
    public RenderTexture RT_FXAARTluminance;

    public RenderTexture sunDepthTexture;
    public RenderTexture previousResult;
    public RenderTexture previousDepth;
    //public RenderTexture intTex1;
    //public RenderTexture[] volumeTextures;
    //public RenderTexture volumeTexture1;
    //public RenderTexture volumeTextureB;

    //public RenderTexture activeVolume;
    //public RenderTexture previousActiveVolume;

    //public RenderTexture dummyVoxelTexture;
    //public RenderTexture dummyVoxelTexture2;




    // Update is called once per frame
    void OnPostRender () {

        if (CollectData)
        {
            SEGIRenderWidth = SEGIRenderer.SEGIRenderWidth;
            SEGIRenderHeight = SEGIRenderer.SEGIRenderHeight;
            RT_FXAART = SEGIRenderer.RT_FXAART;
            RT_gi1 = SEGIRenderer.RT_gi1;
            RT_gi2 = SEGIRenderer.RT_gi2;
            RT_reflections = SEGIRenderer.RT_reflections;
            RT_gi3 = SEGIRenderer.RT_gi3;
            RT_gi4 = SEGIRenderer.RT_gi4;
            RT_blur0 = SEGIRenderer.RT_blur0;
            RT_blur1 = SEGIRenderer.RT_blur1;
            RT_FXAARTluminance = SEGIRenderer.RT_FXAARTluminance;
            sunDepthTexture = SEGIRenderer.sunDepthTexture;
            previousResult = SEGIRenderer.previousResult;
            previousDepth = SEGIRenderer.previousDepth;
            //intTex1 = SEGIRenderer.intTex1;
            //volumeTexture1 = SEGIRenderer.volumeTexture1;
            //volumeTextureB = SEGIRenderer.volumeTextureB;
            //activeVolume = SEGIRenderer.activeVolume;
            //previousActiveVolume = SEGIRenderer.previousActiveVolume;
            //dummyVoxelTexture = SEGIRenderer.dummyVoxelTexture;
            //dummyVoxelTexture2 = SEGIRenderer.dummyVoxelTexture2;
        }
    }
}
