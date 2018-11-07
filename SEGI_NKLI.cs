using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.Rendering.PostProcessing;
using System.Runtime.Serialization;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using Unity.Jobs;
using System.IO;
using System;
#if VRWORKS
using NVIDIA;
#endif

namespace UnityEngine.Rendering.PostProcessing
{
    [Serializable]
    public sealed class SEGISun : ParameterOverride<Light> { }

    [Serializable]
    public sealed class VoxelResolution : ParameterOverride<VoxelResolutionEnum> { }

    [Serializable]
    public sealed class TraceCacheResolution : ParameterOverride<TraceCacheResolutionEnum> { }

    [Serializable]
    public enum VoxelResolutionEnum
    {
        Medium = 128,
        High = 256
    }

    [Serializable]
    public enum TraceCacheResolutionEnum
    {
        VeryLow = 32,
        Low = 64,
        Medium = 128,
        High = 256
    }

    [Serializable]
    public sealed class SEGILayerMask : ParameterOverride<LayerMask> { }

    [Serializable]
    public sealed class SEGITransform : ParameterOverride<Transform> { }

    [Serializable]
    [PostProcess(typeof(SEGIRenderer), PostProcessEvent.AfterStack, "NKLI/SEGI")]

    public sealed class SEGI_NKLI : PostProcessEffectSettings
    {


        public VoxelResolution voxelResolution = new VoxelResolution { value = VoxelResolutionEnum.High };
        public TraceCacheResolution traceCacheResolution = new TraceCacheResolution { value = TraceCacheResolutionEnum.High };
        public FloatParameter voxelSpaceSize = new FloatParameter { value = 25.0f };
        public BoolParameter updateVoxelsAfterX = new BoolParameter { value = false };
        public IntParameter updateVoxelsAfterXInterval = new IntParameter { value = 1 };
        public BoolParameter voxelAA = new BoolParameter { value = false };
        [Range(0, 2)]
        public IntParameter innerOcclusionLayers = new IntParameter { value = 1 };
        public BoolParameter infiniteBounces = new BoolParameter { value = true };


        public BoolParameter useReflectionProbes = new BoolParameter { value = true };
        [Range(0, 2)]
        public FloatParameter reflectionProbeIntensity = new FloatParameter { value = 0.5f };
        //[Range(0, 2)]
        //public FloatParameter reflectionProbeAttribution = new FloatParameter { value = 1f };
        public BoolParameter doReflections = new BoolParameter { value = true };

        [Range(0.01f, 1.0f)]
        public FloatParameter temporalBlendWeight = new FloatParameter { value = 1.0f };
        public BoolParameter useBilateralFiltering = new BoolParameter { value = true };// Actually used?
        [Range(1, 4)]
        public IntParameter GIResolution = new IntParameter { value = 1 };
        public BoolParameter stochasticSampling = new BoolParameter { value = true };
        public BoolParameter updateGI = new BoolParameter { value = true };

        [Range(1, 128)]
        public IntParameter cones = new IntParameter { value = 4 };
        [Range(1, 32)]
        public IntParameter coneTraceSteps = new IntParameter { value = 10 };
        [Range(0.1f, 2.0f)]
        public FloatParameter coneLength = new FloatParameter { value = 1.0f };
        [Range(0.5f, 12.0f)]
        public FloatParameter coneWidth = new FloatParameter { value = 3.9f };
        [Range(0.0f, 4.0f)]
        public FloatParameter coneTraceBias = new FloatParameter { value = 2.8f };
        [Range(0.0f, 4.0f)]
        public FloatParameter occlusionStrength = new FloatParameter { value = 0.15f };
        [Range(0.0f, 4.0f)]
        public FloatParameter nearOcclusionStrength = new FloatParameter { value = 0.5f };
        [Range(0.001f, 4.0f)]
        public FloatParameter occlusionPower = new FloatParameter { value = 0.65f };
        [Range(0.0f, 4.0f)]
        public FloatParameter nearLightGain = new FloatParameter { value = 0.36f };
        [Range(0.0f, 4.0f)]
        public FloatParameter giGain = new FloatParameter { value = 1.0f };
        [Range(0.0f, 2.0f)]
        public FloatParameter secondaryBounceGain = new FloatParameter { value = 0.9f };
        [Range(6, 128)]
        public IntParameter reflectionSteps = new IntParameter { value = 12 };
        [Range(0.001f, 4.0f)]
        public FloatParameter reflectionOcclusionPower = new FloatParameter { value = 1.0f };
        [Range(0.0f, 1.0f)]
        public FloatParameter skyReflectionIntensity = new FloatParameter { value = 1.0f };
        public BoolParameter gaussianMipFilter = new BoolParameter { value = false };

        [Range(0.1f, 4.0f)]
        public FloatParameter farOcclusionStrength = new FloatParameter { value = 1.0f };
        [Range(0.1f, 4.0f)]
        public FloatParameter farthestOcclusionStrength = new FloatParameter { value = 1.0f };

        [Range(3, 16)]
        public IntParameter secondaryCones = new IntParameter { value = 6 };
        [Range(0.1f, 4.0f)]
        public FloatParameter secondaryOcclusionStrength = new FloatParameter { value = 0.27f };

        public BoolParameter useFXAA = new BoolParameter { value = false };

        public BoolParameter visualizeGI = new BoolParameter { value = false };
        public BoolParameter visualizeGIPathCache = new BoolParameter { value = false };
        public BoolParameter visualizeVoxels = new BoolParameter { value = false };
        public BoolParameter visualizeSunDepthTexture = new BoolParameter { value = false };

        //public SEGISun Sun = new SEGISun { value = null };
        public static Light Sun;

        public SEGILayerMask giCullingMask = new SEGILayerMask { value = 2147483647 };
        public SEGILayerMask reflectionProbeLayerMask = new SEGILayerMask { value = 2147483647 };

        //public SEGITransform followTransform = new SEGITransform { value = null };
        public static Transform followTransform;

        [Range(0.0f, 16.0f)]
        public FloatParameter softSunlight = new FloatParameter { value = 0.0f };
        public ColorParameter skyColor = new ColorParameter { value = Color.black };
        public BoolParameter MatchAmbiantColor = new BoolParameter { value = false };
        [Range(0.0f, 8.0f)]
        public FloatParameter skyIntensity = new FloatParameter { value = 1.0f };
        public BoolParameter sphericalSkylight = new BoolParameter { value = false };

        //VR
        public BoolParameter NVIDIAVRWorksEnable = new BoolParameter { value = false };

    }

    [ExecuteInEditMode]
    [ImageEffectAllowedInSceneView]
    public sealed class SEGIRenderer : PostProcessEffectRenderer<SEGI_NKLI>
    {
        public bool initChecker = false;

        public Material material;
        public Camera attachedCamera;
        public Transform shadowCamTransform;

        public Camera shadowCam;
        public GameObject shadowCamGameObject;
        public Texture2D[] blueNoise;

        //public ReflectionProbe reflectionProbe;
        public Camera reflectionCamera;
        public GameObject reflectionProbeGameObject;

        public float shadowSpaceSize = 50.0f;

        struct Pass
        {
            public static int DiffuseTrace = 0;
            public static int BilateralBlur = 1;
            public static int BlendWithScene = 2;
            public static int TemporalBlend = 3;
            public static int SpecularTrace = 4;
            public static int GetCameraDepthTexture = 5;
            public static int GetWorldNormals = 6;
            public static int VisualizeGI = 7;
            public static int WriteBlack = 8;
            public static int VisualizeVoxels = 10;
            public static int BilateralUpsample = 11;
        }

        public static RenderTexture RT_FXAART;
        public static RenderTexture RT_gi1;
        public static RenderTexture RT_gi2;
        public static RenderTexture RT_reflections;
        public static RenderTexture RT_gi3;
        public static RenderTexture RT_gi4;
        public static RenderTexture RT_blur0;
        public static RenderTexture RT_blur1;
        public static RenderTexture RT_FXAARTluminance;
        public static RenderTexture RT_Albedo;
        public static RenderTexture RT_AlbedoX2;

        public static int SEGIRenderWidth;
        public static int SEGIRenderHeight;


        public struct SystemSupported
        {
            public bool hdrTextures;
            public bool rIntTextures;
            public bool dx11;
            public bool volumeTextures;
            public bool postShader;
            public bool sunDepthShader;
            public bool voxelizationShader;
            public bool tracingShader;

            public bool fullFunctionality
            {
                get
                {
                    return hdrTextures && rIntTextures && dx11 && volumeTextures && postShader && sunDepthShader && voxelizationShader && tracingShader;
                }
            }
        }

        /// <summary>
        /// Contains info on system compatibility of required hardware functionality
        /// </summary>
        public SystemSupported systemSupported;



        public FilterMode filterMode = FilterMode.Point;
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGBHalf;



        //public bool gaussianMipFilter = false;

        int mipFilterKernel
        {
            get
            {
                return settings.gaussianMipFilter.value ? 1 : 0;
            }
        }

        //public bool voxelAA = false;

        int DummyVoxelResolution
        {
            get
            {
                return (int)settings.voxelResolution.value * (settings.voxelAA.value ? 2 : 1);
            }
        }

        int sunShadowResolution = 256;
        int prevSunShadowResolution;

        public Shader sunDepthShader;

        float shadowSpaceDepthRatio = 10.0f;

        int frameSwitch = 0;

        ///<summary>This is a volume texture that is immediately written to in the voxelization shader. The RInt format enables atomic writes to avoid issues where multiple fragments are trying to write to the same voxel in the volume.</summary>
        RenderTexture integerVolume;

        ///<summary>A 2D texture with the size of [voxel resolution, voxel resolution] that must be used as the active render texture when rendering the scene for voxelization. This texture scales depending on whether Voxel AA is enabled to ensure correct voxelization.</summary>
        RenderTexture dummyVoxelTextureAAScaled;

        ///<summary>A 2D texture with the size of [voxel resolution, voxel resolution] that must be used as the active render texture when rendering the scene for voxelization. This texture is always the same size whether Voxel AA is enabled or not.</summary>
        RenderTexture dummyVoxelTextureFixed;

        ///<summary>The main GI data clipmaps that hold GI data referenced during GI tracing</summary>
        Clipmap[] clipmaps;

        ///<summary>The secondary clipmaps that hold irradiance data for infinite bounces</summary>
        Clipmap[] irradianceClipmaps;

        public static RenderTexture tracedTexture0;
        public static RenderTexture tracedTexture1;

        public int tracedTexture1UpdateCount;

        public static RenderTexture sunDepthTexture;
        public static RenderTexture previousGIResult;
        public static RenderTexture previousDepth;

        public bool notReadyToRender = false;

        public Shader voxelizationShader;
        public Shader voxelTracingShader;

        public ComputeShader clearCompute;
        public ComputeShader clearComputeCache;
        public ComputeShader transferIntsCompute;
        public ComputeShader transferIntsTraceCacheCompute;
        public ComputeShader mipFilterCompute;

        const int numClipmaps = 6;
        int clipmapCounter = 0;
        int currentClipmapIndex = 0;

        const int numMipLevels = 6;

        public Camera voxelCamera;
        public GameObject voxelCameraGO;
        public GameObject leftViewPoint;
        public GameObject topViewPoint;

        float voxelScaleFactor
        {
            get
            {
                return (float)settings.voxelResolution.value / 256.0f;
            }
        }

        float traceCacheScaleFactor
        {
            get
            {
                return (float)settings.traceCacheResolution.value / 256.0f;
            }
        }

        public int prevTraceCacheResolution = 0;

        public Vector3 voxelSpaceOrigin;
        public Vector3 previousVoxelSpaceOrigin;
        public Vector3 voxelSpaceOriginDelta;


        public Quaternion rotationFront = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
        public Quaternion rotationLeft = new Quaternion(0.0f, 0.7f, 0.0f, 0.7f);
        public Quaternion rotationTop = new Quaternion(0.7f, 0.0f, 0.0f, 0.7f);

        public int voxelFlipFlop = 0;

        public enum RenderState
        {
            Voxelize,
            Bounce
        }

        public RenderState renderState = RenderState.Voxelize;

        //CommandBuffer refactor
        public CommandBuffer SEGIBuffer;

        //Gaussian Filter
        private Shader Gaussian_Shader;
        private Material Gaussian_Material;

        //FXAA
        //public bool useFXAA;
        private Shader FXAA_Shader;
        private Material FXAA_Material;

        private Shader CubeMap_Shader;

        //Color Correction
        //private Shader ColorCorrection_Shader;
        //private Material ColorCorrection_Material;

        //Forward Rendering
        //public bool useReflectionProbes = true;
        //[Range(0, 2)]
        //public float reflectionProbeIntensity = 0.5f;
        //[Range(0, 2)]
        //public float reflectionProbeAttribution = 1f;

        //Delayed voxelization
        public bool updateVoxelsAfterXDoUpdate = false;
        private double updateVoxelsAfterXPrevX = 9223372036854775807;
        private double updateVoxelsAfterXPrevY = 9223372036854775807;
        private double updateVoxelsAfterXPrevZ = 9223372036854775807;

        public int GIResolutionPrev = 0;

        //public LightShadows ShadowStateCache;

        public bool VRWorksActuallyEnabled;



        //[ImageEffectOpaque]
        public override void Render(PostProcessRenderContext context)
        {
            // Update
            InitCheck();

            if (!context.isSceneView)
            {
                if (SEGIRenderWidth != context.width || SEGIRenderHeight != context.height || settings.GIResolution.value != GIResolutionPrev)
                {
                    Debug.Log("<SEGI> Context != Cached Dimensions. Resizing buffers");
                    GIResolutionPrev = settings.GIResolution.value;
                    SEGIRenderWidth = context.width;
                    SEGIRenderHeight = context.height;

                    ResizeAllTextures();
                }

                if (prevTraceCacheResolution != (int)settings.traceCacheResolution.value)
                {
                    Debug.Log("<SEGI> Path trace cache resolution changed. Resizing volumes");
                    prevTraceCacheResolution = (int)settings.traceCacheResolution.value;
                    CreateVolumeTextures();
                }
            }


            if ((int)settings.traceCacheResolution.value == 0)
            {
                Debug.Log("<SEGI> Path trace cache zero'd. Resizing volumes");
                prevTraceCacheResolution = (int)settings.traceCacheResolution.value;
                CreateVolumeTextures();
            }

            if (SEGI_NKLI.Sun == null)
            {
                Debug.Log("<SEGI> Scipt 'SEGI_SunLight.cs' Must be attached to your main directional light!");
                return;
            }

            if (notReadyToRender)
                return;

            if (!attachedCamera)
            {
                return;
            }

            if (!context.isSceneView)
            {

                if (previousGIResult == null)
                {
                    Debug.Log("<SEGI> PreviousGIResult == null. Resizing Render Textures.");
                    ResizeAllTextures();
                }

                if (previousGIResult.width != context.width || previousGIResult.height != context.height)
                {
                    Debug.Log("<SEGI> previousGIResult != Expected Dimensions. Resizing Render Textures");
                    ResizeAllTextures();
                }
            }

            if ((int)sunShadowResolution != prevSunShadowResolution)
            {
                ResizeSunShadowBuffer();
            }

            prevSunShadowResolution = (int)sunShadowResolution;

            if (clipmaps[0].resolution != (int)settings.voxelResolution.value)
            {
                clipmaps[0].resolution = (int)settings.voxelResolution.value;
                clipmaps[0].UpdateTextures();
            }

            if (dummyVoxelTextureAAScaled.width != DummyVoxelResolution)
            {
                ResizeDummyTexture();
            }

            if (attachedCamera != context.camera) attachedCamera = context.camera;

            if (!shadowCam)
            {
                Debug.Log("<SEGI> Shadow Camera not found!");
                return;
            }

            //VRWorks
#if VRWORKS
            if (settings.NVIDIAVRWorksEnable)
            {
                if (!VRWorksActuallyEnabled)
                {
                    VRWorks VRWorksComponent = context.camera.GetComponent<VRWorks>();
                    if (!VRWorksComponent)
                    {
                        VRWorksComponent = context.camera.gameObject.AddComponent<VRWorks>();
                        context.camera.gameObject.AddComponent<VRWorksPresent>();
                    }
                    if (VRWorksComponent.IsFeatureAvailable(VRWorks.Feature.SinglePassStereo))
                    {
                        VRWorksComponent.SetActiveFeature(VRWorks.Feature.SinglePassStereo);
                    }
                    material.EnableKeyword("VRWORKS");
                    VRWorksActuallyEnabled = true;
                }
                NVIDIA.VRWorks.SetKeywords(material);
            }
            else
            {
                if (VRWorksActuallyEnabled)
                {
                    VRWorks VRWorksComponent = context.camera.GetComponent<VRWorks>();
                    if (VRWorksComponent)
                    {
                        VRWorksComponent.SetActiveFeature(VRWorks.Feature.None);
                    }
                    material.DisableKeyword("VRWORKS");
                    VRWorksActuallyEnabled = false;
                }
            }
#endif
            //END VRWorks

            // OnPreRender

            //Force reinitialization to make sure that everything is working properly if one of the cameras was unexpectedly destroyed
            //if (!voxelCamera || !false;

            if (context.camera.renderingPath == RenderingPath.Forward)
            {
                //reflectionProbe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
                //reflectionProbe.intensity = settings.reflectionProbeIntensity.value;
                reflectionCamera.cullingMask = settings.reflectionProbeLayerMask.GetValue<LayerMask>();
                reflectionCamera.farClipPlane = context.camera.farClipPlane;

                //Cache Shadow State
                LightShadows ShadowStateCache = SEGI_NKLI.Sun.shadows;
                Color ambientColorCache = RenderSettings.ambientLight;
                AmbientMode ambientModeCache = RenderSettings.ambientMode;
                float IntensityCache = SEGI_NKLI.Sun.intensity;
                float ambientCache = RenderSettings.ambientIntensity;

                //SEGI_NKLI.Sun.shadows = LightShadows.None;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;//SEGI_NKLI.Sun == null ? Color.black : new Color(Mathf.Pow(SEGI_NKLI.Sun.color.r, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.color.g, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.color.b, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.intensity, 2.2f));
                RenderSettings.ambientIntensity = 1;
                SEGI_NKLI.Sun.intensity = 1;

                var faceToRender = Time.frameCount % 6;
                var faceMask = 1 << faceToRender;
                //reflectionProbe.RenderProbe();
                //reflectionProbe.enabled = false;
                reflectionCamera.SetReplacementShader(CubeMap_Shader, "");
                reflectionCamera.RenderToCubemap(RT_Albedo, faceMask, Camera.MonoOrStereoscopicEye.Mono);
                //reflectionProbe.enabled = true;
                
                //Restore Shadow State
                SEGI_NKLI.Sun.shadows = ShadowStateCache;
                RenderSettings.ambientLight = ambientColorCache;
                RenderSettings.ambientMode = ambientModeCache;
                RenderSettings.ambientIntensity = ambientCache;
                SEGI_NKLI.Sun.intensity = IntensityCache;
            }
            /*else
            {
                reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            }*/

            // only use main camera for voxel simulations
            /*if (attachedCamera != Camera.main)
            {
                Debug.Log("<SEGI> Instance not attached to Main Camera. Please ensure the attached camera has the 'MainCamera' tag.");
                return;
            }*/

            //Update SkyColor
            if (settings.MatchAmbiantColor)
            {
                settings.skyColor.value = RenderSettings.ambientLight;
                settings.skyIntensity.value = RenderSettings.ambientIntensity;
            }


            //Cache the previous active render texture to avoid issues with other Unity rendering going on
            RenderTexture previousActive = RenderTexture.active;

            Shader.SetGlobalInt("SEGIVoxelAA", settings.voxelAA.value ? 1 : 0);

            //Temporarily disable rendering of shadows on the directional light during voxelization pass. Cache the result to set it back to what it was after voxelization is done
            /*LightShadows prevSunShadowSetting = LightShadows.None;
            if (SEGI_NKLI.Sun != null)
            {
                prevSunShadowSetting = SEGI_NKLI.Sun.shadows;
                SEGI_NKLI.Sun.shadows = LightShadows.None;
            }*/

            if (!settings.updateVoxelsAfterX.value) updateVoxelsAfterXDoUpdate = true;
            if (attachedCamera.transform.position.x - updateVoxelsAfterXPrevX >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevX - attachedCamera.transform.position.x >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;

            if (attachedCamera.transform.position.y - updateVoxelsAfterXPrevY >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevY - attachedCamera.transform.position.y >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;

            if (attachedCamera.transform.position.z - updateVoxelsAfterXPrevZ >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevZ - attachedCamera.transform.position.z >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;

            if (settings.updateGI.value)
            {

                //Main voxelization work
                if (renderState == RenderState.Voxelize)
                {
                    currentClipmapIndex = SelectCascadeBinary(clipmapCounter);      //Determine which clipmap to update during this frame

                    Clipmap activeClipmap = clipmaps[currentClipmapIndex];          //Set the active clipmap based on which one is determined to render this frame

                    //If we're not updating the base level 0 clipmap, get the previous clipmap
                    Clipmap prevClipmap = null;
                    if (currentClipmapIndex != 0)
                    {
                        prevClipmap = clipmaps[currentClipmapIndex - 1];
                    }

                    float clipmapShadowSize = shadowSpaceSize * activeClipmap.localScale;
                    float clipmapSize = settings.voxelSpaceSize * activeClipmap.localScale;  //Determine the current clipmap's size in world units based on its scale
                                                                                             //float voxelTexel = (1.0f * clipmapSize) / activeClipmap.resolution * 0.5f;	//Calculate the size of a voxel texel in world-space units





                    //Setup the voxel volume origin position
                    float interval = (clipmapSize) / 8.0f;                          //The interval at which the voxel volume will be "locked" in world-space
                    Vector3 origin;
                    if (SEGI_NKLI.followTransform)
                    {
                        origin = SEGI_NKLI.followTransform.position;
                    }
                    else
                    {
                        //GI is still flickering a bit when the scene view and the game view are opened at the same time
                        origin = context.camera.transform.position + context.camera.transform.forward * clipmapSize / 4.0f;
                    }
                    //Lock the voxel volume origin based on the interval
                    activeClipmap.previousOrigin = activeClipmap.origin;
                    activeClipmap.origin = new Vector3(Mathf.Round(origin.x / interval) * interval, Mathf.Round(origin.y / interval) * interval, Mathf.Round(origin.z / interval) * interval);


                    //Clipmap delta movement for scrolling secondary bounce irradiance volume when this clipmap has changed origin
                    activeClipmap.originDelta = activeClipmap.origin - activeClipmap.previousOrigin;
                    Shader.SetGlobalVector("SEGIVoxelSpaceOriginDelta", activeClipmap.originDelta / (settings.voxelSpaceSize * activeClipmap.localScale));






                    //Calculate the relative origin and overlap/size of the previous cascade as compared to the active cascade. This is used to avoid voxelizing areas that have already been voxelized by previous (smaller) cascades
                    Vector3 prevClipmapRelativeOrigin = Vector3.zero;
                    float prevClipmapOccupance = 0.0f;
                    if (currentClipmapIndex != 0)
                    {
                        prevClipmapRelativeOrigin = (prevClipmap.origin - activeClipmap.origin) / clipmapSize;
                        prevClipmapOccupance = prevClipmap.localScale / activeClipmap.localScale;
                    }
                    Shader.SetGlobalVector("SEGIClipmapOverlap", new Vector4(prevClipmapRelativeOrigin.x, prevClipmapRelativeOrigin.y, prevClipmapRelativeOrigin.z, prevClipmapOccupance));

                    //Calculate the relative origin and scale of this cascade as compared to the first (level 0) cascade. This is used during GI tracing/data lookup to ensure tracing is done in the correct space
                    for (int i = 1; i < numClipmaps; i++)
                    {
                        Vector3 clipPosFromMaster = Vector3.zero;
                        float clipScaleFromMaster = 1.0f;

                        clipPosFromMaster = (clipmaps[i].origin - clipmaps[0].origin) / (settings.voxelSpaceSize.value * clipmaps[i].localScale);
                        clipScaleFromMaster = clipmaps[0].localScale / clipmaps[i].localScale;

                        Shader.SetGlobalVector("SEGIClipTransform" + i.ToString(), new Vector4(clipPosFromMaster.x, clipPosFromMaster.y, clipPosFromMaster.z, clipScaleFromMaster));
                    }

                    //Set the voxel camera (proxy camera used to render the scene for voxelization) parameters
                    voxelCamera.enabled = false;
                    voxelCamera.orthographic = true;
                    voxelCamera.orthographicSize = clipmapSize * 0.5f;
                    voxelCamera.nearClipPlane = 0.0f;
                    voxelCamera.farClipPlane = clipmapSize;
                    voxelCamera.useOcclusionCulling = false;
                    voxelCamera.depth = -2;
                    voxelCamera.renderingPath = RenderingPath.Forward;
                    voxelCamera.clearFlags = CameraClearFlags.Color;
                    voxelCamera.backgroundColor = Color.black;
                    voxelCamera.cullingMask = settings.giCullingMask.GetValue<LayerMask>();

                    //Move the voxel camera game object and other related objects to the above calculated voxel space origin
                    voxelCameraGO.transform.position = activeClipmap.origin - Vector3.forward * clipmapSize * 0.5f;
                    voxelCameraGO.transform.rotation = rotationFront;

                    leftViewPoint.transform.position = activeClipmap.origin + Vector3.left * clipmapSize * 0.5f;
                    leftViewPoint.transform.rotation = rotationLeft;
                    topViewPoint.transform.position = activeClipmap.origin + Vector3.up * clipmapSize * 0.5f;
                    topViewPoint.transform.rotation = rotationTop;




                    //Set matrices needed for voxelization
                    //Shader.SetGlobalMatrix("WorldToGI", shadowCam.worldToCameraMatrix);
                    //Shader.SetGlobalMatrix("GIToWorld", shadowCam.cameraToWorldMatrix);
                    //Shader.SetGlobalMatrix("GIProjection", shadowCam.projectionMatrix);
                    //Shader.SetGlobalMatrix("GIProjectionInverse", shadowCam.projectionMatrix.inverse);
                    Shader.SetGlobalMatrix("WorldToCamera", attachedCamera.worldToCameraMatrix);
                    Shader.SetGlobalFloat("GIDepthRatio", shadowSpaceDepthRatio);

                    Matrix4x4 frontViewMatrix = TransformViewMatrix(voxelCamera.transform.worldToLocalMatrix);
                    Matrix4x4 leftViewMatrix = TransformViewMatrix(leftViewPoint.transform.worldToLocalMatrix);
                    Matrix4x4 topViewMatrix = TransformViewMatrix(topViewPoint.transform.worldToLocalMatrix);

                    Shader.SetGlobalMatrix("SEGIVoxelViewFront", frontViewMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelViewLeft", leftViewMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelViewTop", topViewMatrix);
                    Shader.SetGlobalMatrix("SEGIWorldToVoxel", voxelCamera.worldToCameraMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelProjection", voxelCamera.projectionMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelProjectionInverse", voxelCamera.projectionMatrix.inverse);

                    Shader.SetGlobalMatrix("SEGIVoxelVPFront", GL.GetGPUProjectionMatrix(voxelCamera.projectionMatrix, true) * frontViewMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelVPLeft", GL.GetGPUProjectionMatrix(voxelCamera.projectionMatrix, true) * leftViewMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelVPTop", GL.GetGPUProjectionMatrix(voxelCamera.projectionMatrix, true) * topViewMatrix);

                    Shader.SetGlobalMatrix("SEGIWorldToVoxel" + currentClipmapIndex.ToString(), voxelCamera.worldToCameraMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelProjection" + currentClipmapIndex.ToString(), voxelCamera.projectionMatrix);

                    Matrix4x4 voxelToGIProjection = shadowCam.projectionMatrix * shadowCam.worldToCameraMatrix * voxelCamera.cameraToWorldMatrix;
                    Shader.SetGlobalMatrix("SEGIVoxelToGIProjection", voxelToGIProjection);
                    Shader.SetGlobalVector("SEGISunlightVector", SEGI_NKLI.Sun ? Vector3.Normalize(SEGI_NKLI.Sun.transform.forward) : Vector3.up);


                    //Set paramteters
                    Shader.SetGlobalInt("SEGIVoxelResolution", (int)settings.voxelResolution.value);

                    Shader.SetGlobalColor("GISunColor", SEGI_NKLI.Sun == null ? Color.black : new Color(Mathf.Pow(SEGI_NKLI.Sun.color.r, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.color.g, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.color.b, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.intensity, 2.2f)));
                    Shader.SetGlobalColor("SEGISkyColor", new Color(Mathf.Pow(settings.skyColor.value.r * settings.skyIntensity * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.g * settings.skyIntensity * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.b * settings.skyIntensity * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.a, 2.2f)));
                    Shader.SetGlobalFloat("GIGain", settings.giGain);
                    Shader.SetGlobalFloat("SEGISecondaryBounceGain", settings.infiniteBounces ? settings.secondaryBounceGain : 0.0f);
                    Shader.SetGlobalFloat("SEGISoftSunlight", settings.softSunlight);
                    Shader.SetGlobalInt("SEGISphericalSkylight", settings.sphericalSkylight ? 1 : 0);
                    Shader.SetGlobalInt("SEGIInnerOcclusionLayers", settings.innerOcclusionLayers);




                    //Render the depth texture from the sun's perspective in order to inject sunlight with shadows during voxelization
                    if (SEGI_NKLI.Sun != null)
                    {
                        shadowCam.cullingMask = settings.giCullingMask.GetValue<LayerMask>();

                        Vector3 shadowCamPosition = activeClipmap.origin + Vector3.Normalize(-SEGI_NKLI.Sun.transform.forward) * clipmapShadowSize * 0.5f * shadowSpaceDepthRatio;

                        shadowCamTransform.position = shadowCamPosition;
                        shadowCamTransform.LookAt(activeClipmap.origin, Vector3.up);

                        shadowCam.renderingPath = RenderingPath.Forward;
                        shadowCam.depthTextureMode |= DepthTextureMode.None;

                        shadowCam.orthographicSize = clipmapShadowSize;
                        shadowCam.farClipPlane = clipmapShadowSize * 2.0f * shadowSpaceDepthRatio;

                        //Shader.SetGlobalMatrix("WorldToGI", shadowCam.worldToCameraMatrix);
                        //Shader.SetGlobalMatrix("GIToWorld", shadowCam.cameraToWorldMatrix);
                        //Shader.SetGlobalMatrix("GIProjection", shadowCam.projectionMatrix);
                        //Shader.SetGlobalMatrix("GIProjectionInverse", shadowCam.projectionMatrix.inverse);
                        voxelToGIProjection = shadowCam.projectionMatrix * shadowCam.worldToCameraMatrix * voxelCamera.cameraToWorldMatrix;
                        Shader.SetGlobalMatrix("SEGIVoxelToGIProjection", voxelToGIProjection);


                        Graphics.SetRenderTarget(sunDepthTexture);
                        shadowCam.SetTargetBuffers(sunDepthTexture.colorBuffer, sunDepthTexture.depthBuffer);

                        shadowCam.RenderWithShader(sunDepthShader, "");

                        Shader.SetGlobalTexture("SEGISunDepth", sunDepthTexture);
                    }

                    //Clear the volume texture that is immediately written to in the voxelization scene shader
                    clearCompute.SetTexture(0, "RG0", integerVolume);
                    clearCompute.SetInt("Resolution", activeClipmap.resolution);
                    clearCompute.Dispatch(0, activeClipmap.resolution / 16, activeClipmap.resolution / 16, 1);

                    //Set irradiance "secondary bounce" texture
                    Shader.SetGlobalTexture("SEGICurrentIrradianceVolume", irradianceClipmaps[currentClipmapIndex].volumeTexture0);


                    Graphics.SetRandomWriteTarget(1, integerVolume);
                    voxelCamera.targetTexture = dummyVoxelTextureAAScaled;
                    voxelCamera.RenderWithShader(voxelizationShader, "");
                    Graphics.ClearRandomWriteTargets();


                    //Transfer the data from the volume integer texture to the main volume texture used for GI tracing. 
                    transferIntsCompute.SetTexture(0, "Result", activeClipmap.volumeTexture0);
                    transferIntsCompute.SetTexture(0, "RG0", integerVolume);
                    transferIntsCompute.SetInt("VoxelAA", settings.voxelAA ? 3 : 0);
                    transferIntsCompute.SetInt("Resolution", activeClipmap.resolution);
                    transferIntsCompute.Dispatch(0, activeClipmap.resolution / 16, activeClipmap.resolution / 16, 1);



                    //Push current voxelization result to higher levels
                    for (int i = 0 + 1; i < numClipmaps; i++)
                    {
                        Clipmap sourceClipmap = clipmaps[i - 1];
                        Clipmap targetClipmap = clipmaps[i];


                        Vector3 sourceRelativeOrigin = Vector3.zero;
                        float sourceOccupance = 0.0f;

                        sourceRelativeOrigin = (sourceClipmap.origin - targetClipmap.origin) / (targetClipmap.localScale * settings.voxelSpaceSize.value);
                        sourceOccupance = sourceClipmap.localScale / targetClipmap.localScale;

                        mipFilterCompute.SetTexture(0, "Source", sourceClipmap.volumeTexture0);
                        mipFilterCompute.SetTexture(0, "Destination", targetClipmap.volumeTexture0);
                        mipFilterCompute.SetVector("ClipmapOverlap", new Vector4(sourceRelativeOrigin.x, sourceRelativeOrigin.y, sourceRelativeOrigin.z, sourceOccupance));
                        mipFilterCompute.SetInt("destinationRes", targetClipmap.resolution);
                        mipFilterCompute.Dispatch(0, targetClipmap.resolution / 16, targetClipmap.resolution / 16, 1);
                    }



                    for (int i = 0; i < numClipmaps; i++)
                    {
                        Shader.SetGlobalTexture("SEGIVolumeLevel" + i.ToString(), clipmaps[i].volumeTexture0);
                    }


                    if (settings.infiniteBounces)
                    {
                        renderState = RenderState.Bounce;
                    }
                    else
                    {
                        //Increment clipmap counter
                        clipmapCounter++;
                        if (clipmapCounter >= (int)Mathf.Pow(2.0f, numClipmaps))
                        {
                            clipmapCounter = 0;
                        }
                    }
                }
                else if (renderState == RenderState.Bounce)
                {
                    //Calculate the relative position and scale of the current clipmap as compared to the first (level 0) clipmap. Used to ensure tracing is performed in the correct space
                    Vector3 translateToZero = Vector3.zero;
                    translateToZero = (clipmaps[currentClipmapIndex].origin - clipmaps[0].origin) / (settings.voxelSpaceSize * clipmaps[currentClipmapIndex].localScale);
                    float scaleToZero = 1.0f / clipmaps[currentClipmapIndex].localScale;
                    Shader.SetGlobalVector("SEGICurrentClipTransform", new Vector4(translateToZero.x, translateToZero.y, translateToZero.z, scaleToZero));

                    //Clear the volume texture that is immediately written to in the voxelization scene shader
                    clearCompute.SetTexture(0, "RG0", integerVolume);
                    clearCompute.SetInt("Resolution", clipmaps[currentClipmapIndex].resolution);
                    clearCompute.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                    //Only render infinite bounces for clipmaps 0, 1, and 2
                    if (currentClipmapIndex <= 2)
                    {
                        Shader.SetGlobalInt("SEGISecondaryCones", settings.secondaryCones);
                        Shader.SetGlobalFloat("SEGISecondaryOcclusionStrength", settings.secondaryOcclusionStrength);

                        Graphics.SetRandomWriteTarget(1, integerVolume);
                        voxelCamera.targetTexture = dummyVoxelTextureFixed;
                        voxelCamera.RenderWithShader(voxelTracingShader, "");
                        Graphics.ClearRandomWriteTargets();

                        transferIntsCompute.SetTexture(1, "Result", irradianceClipmaps[currentClipmapIndex].volumeTexture0);
                        transferIntsCompute.SetTexture(1, "RG0", integerVolume);
                        transferIntsCompute.SetInt("Resolution", (int)settings.voxelResolution.value);
                        transferIntsCompute.Dispatch(1, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);
                    }

                    //Increment clipmap counter
                    clipmapCounter++;
                    if (clipmapCounter >= (int)Mathf.Pow(2.0f, numClipmaps))
                    {
                        clipmapCounter = 0;
                    }

                    renderState = RenderState.Voxelize;

                }
            }

                Matrix4x4 giToVoxelProjection = voxelCamera.projectionMatrix * voxelCamera.worldToCameraMatrix * shadowCam.cameraToWorldMatrix;
                Shader.SetGlobalMatrix("GIToVoxelProjection", giToVoxelProjection);

                //Set the sun's shadow setting back to what it was before voxelization
                if (SEGI_NKLI.Sun != null)
            {
                //SEGI_NKLI.Sun.shadows = prevSunShadowSetting;
            }

            //Fix stereo rendering matrix
            if (attachedCamera.stereoEnabled)
            {
                // Left and Right Eye inverse View Matrices
                Matrix4x4 leftToWorld = attachedCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                Matrix4x4 rightToWorld = attachedCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
                context.command.SetGlobalMatrix("_LeftEyeToWorld", leftToWorld);
                context.command.SetGlobalMatrix("_RightEyeToWorld", rightToWorld);

                Matrix4x4 leftEye = attachedCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                Matrix4x4 rightEye = attachedCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                // Compensate for RenderTexture...
                leftEye = GL.GetGPUProjectionMatrix(leftEye, true).inverse;
                rightEye = GL.GetGPUProjectionMatrix(rightEye, true).inverse;
                // Negate [1,1] to reflect Unity's CBuffer state
                leftEye[1, 1] *= -1;
                rightEye[1, 1] *= -1;

                context.command.SetGlobalMatrix("_LeftEyeProjection", leftEye);
                context.command.SetGlobalMatrix("_RightEyeProjection", rightEye);
            }
            //Fix stereo rendering matrix/


            // OnRenderImage #################################################################

            if (notReadyToRender)
            {
                context.command.Blit(context.source, context.destination);
                return;
            }

            if (settings.visualizeSunDepthTexture.value && sunDepthTexture != null && sunDepthTexture != null)
            {
                context.command.Blit(sunDepthTexture, context.destination, material, 13);
                return;
            }

            else if (tracedTexture1UpdateCount > 48)
            {
                context.command.SetComputeIntParam(clearComputeCache, "Resolution", (int)settings.traceCacheResolution.value);
                context.command.SetComputeTextureParam(clearComputeCache, 1, "RG1", tracedTexture1);
                context.command.SetComputeIntParam(clearComputeCache, "zStagger", tracedTexture1UpdateCount - 48);
                context.command.DispatchCompute(clearComputeCache, 1, (int)settings.traceCacheResolution.value / 16, (int)settings.traceCacheResolution.value / 16, 1);
            }
            else if (tracedTexture1UpdateCount > 32)
            {
                context.command.SetComputeTextureParam(transferIntsCompute, 3, "Result", tracedTexture0);
                context.command.SetComputeTextureParam(transferIntsCompute, 3, "RG1", tracedTexture1);
                context.command.SetComputeIntParam(transferIntsCompute, "zStagger", tracedTexture1UpdateCount - 32);
                context.command.SetComputeIntParam(transferIntsCompute, "Resolution", (int)settings.traceCacheResolution.value);
                context.command.DispatchCompute(transferIntsCompute, 3, (int)settings.traceCacheResolution.value / 16, (int)settings.traceCacheResolution.value / 16, 1);
            }
            tracedTexture1UpdateCount = (tracedTexture1UpdateCount + 1) % (65);




            context.command.SetGlobalFloat("SEGIVoxelScaleFactor", voxelScaleFactor);
            context.command.SetGlobalFloat("SEGITraceCacheScaleFactor", traceCacheScaleFactor);

            context.command.SetGlobalMatrix("CameraToWorld", context.camera.cameraToWorldMatrix);
            context.command.SetGlobalMatrix("WorldToCamera", context.camera.worldToCameraMatrix);
            context.command.SetGlobalMatrix("ProjectionMatrixInverse", context.camera.projectionMatrix.inverse);
            context.command.SetGlobalMatrix("ProjectionMatrix", context.camera.projectionMatrix);
            context.command.SetGlobalInt("FrameSwitch", frameSwitch);
            context.command.SetGlobalInt("SEGIFrameSwitch", frameSwitch);
            context.command.SetGlobalVector("CameraPosition", context.camera.transform.position);
            context.command.SetGlobalFloat("DeltaTime", Time.deltaTime);

            context.command.SetGlobalInt("StochasticSampling", settings.stochasticSampling.value ? 1 : 0);
            //context.command.SetGlobalInt("TraceDirections", settings.cones);
            context.command.SetGlobalInt("TraceSteps", settings.coneTraceSteps);
            context.command.SetGlobalFloat("TraceLength", settings.coneLength);
            context.command.SetGlobalFloat("ConeSize", settings.coneWidth);
            context.command.SetGlobalFloat("OcclusionStrength", settings.occlusionStrength);
            context.command.SetGlobalFloat("OcclusionPower", settings.occlusionPower);
            context.command.SetGlobalFloat("ConeTraceBias", settings.coneTraceBias);
            context.command.SetGlobalFloat("GIGain", settings.giGain);
            context.command.SetGlobalFloat("NearLightGain", settings.nearLightGain);
            context.command.SetGlobalFloat("NearOcclusionStrength", settings.nearOcclusionStrength);
            context.command.SetGlobalInt("DoReflections", settings.doReflections ? 1 : 0);
            context.command.SetGlobalInt("GIResolution", settings.GIResolution);
            context.command.SetGlobalInt("ReflectionSteps", settings.reflectionSteps);
            context.command.SetGlobalFloat("ReflectionOcclusionPower", settings.reflectionOcclusionPower);
            context.command.SetGlobalFloat("SkyReflectionIntensity", settings.skyReflectionIntensity);
            context.command.SetGlobalFloat("FarOcclusionStrength", settings.farOcclusionStrength);
            context.command.SetGlobalFloat("FarthestOcclusionStrength", settings.farthestOcclusionStrength);
            context.command.SetGlobalTexture("NoiseTexture", blueNoise[frameSwitch % 64]);
            context.command.SetGlobalFloat("BlendWeight", settings.temporalBlendWeight);
            //context.command.SetGlobalInt("useReflectionProbes", settings.useReflectionProbes ? 1 : 0);
            //context.command.SetGlobalFloat("reflectionProbeIntensity", settings.reflectionProbeIntensity);
            //material.SetFloat("reflectionProbeAttribution", settings.reflectionProbeAttribution.value);
            context.command.SetGlobalInt("StereoEnabled", context.stereoActive ? 1 : 0);
            context.command.SetGlobalInt("SEGIRenderWidth", SEGIRenderWidth);
            context.command.SetGlobalInt("SEGIRenderHeight", SEGIRenderHeight);
            //context.command.SetGlobalFloat("voxelSpaceSize", settings.voxelSpaceSize);
            context.command.SetGlobalInt("tracedTexture1UpdateCount", tracedTexture1UpdateCount);
            context.command.SetGlobalInt("visualizeGIPathCache", settings.visualizeGIPathCache ? 1 : 0);

            //Blit once to downsample if required
            context.command.Blit(context.source, RT_gi1);

            if (context.camera.renderingPath == RenderingPath.Forward)
            {
                //context.command.Blit(context.source, RT_Albedo, ColorCorrection_Material, 0);
                //context.command.Blit(RT_Albedo, RT_AlbedoX2, material, Pass.BilateralUpsample);
                context.command.SetGlobalTexture("_SEGICube", RT_Albedo);
                //context.command.SetGlobalTexture("_SEGICubeX2", RT_AlbedoX2);
                //context.command.SetGlobalTexture("_SEGIReflectCube", reflectionProbe.texture);
                context.command.SetGlobalInt("ForwardPath", 1);
            }
            else context.command.SetGlobalInt("ForwardPath", 0);

            //If Visualize Voxels is enabled, just render the voxel visualization shader pass and return
            if (settings.visualizeVoxels.value)
            {
                context.command.Blit(context.source, context.destination, material, Pass.VisualizeVoxels);
                return;
            }

            //Set the previous GI result and camera depth textures to access them in the shader
            context.command.SetGlobalTexture("PreviousGITexture", previousGIResult);
            context.command.SetGlobalTexture("PreviousDepth", previousDepth);

            //Render diffuse GI tracing result
            context.command.SetRandomWriteTarget(1, tracedTexture0);
            context.command.SetRandomWriteTarget(2, tracedTexture1);
            context.command.Blit(RT_gi1, RT_gi2, material, Pass.DiffuseTrace);

            //Render GI reflections result
            if (settings.doReflections.value)
            {
                context.command.Blit(RT_gi1, RT_reflections, material, Pass.SpecularTrace);
                context.command.SetGlobalTexture("Reflections", RT_reflections);
            }

            //If Half Resolution tracing is enabled
            if (settings.GIResolution.value >= 2)
            {
                //Prepare the half-resolution diffuse GI result to be bilaterally upsampled
                //SEGIBuffer.Blit(RT_gi2, RT_gi4);

                //Perform bilateral upsampling on half-resolution diffuse GI result
                context.command.SetGlobalVector("Kernel", new Vector2(1.0f, 0.0f));
                context.command.Blit(RT_gi2, RT_gi3, material, Pass.BilateralUpsample);
                context.command.SetGlobalVector("Kernel", new Vector2(0.0f, 1.0f));
                context.command.Blit(RT_gi3, RT_blur0, Gaussian_Material);
                context.command.SetGlobalTexture("BlurredGI", RT_blur0);

                //Perform temporal reprojection and blending
                if (settings.temporalBlendWeight.value < 1.0f)
                {
                    context.command.Blit(RT_gi4, RT_gi3, material, Pass.TemporalBlend);
                    //SEGIBuffer.Blit(RT_gi4, RT_gi3, material, Pass.TemporalBlend);
                    context.command.Blit(RT_gi3, previousGIResult);
                    context.command.Blit(RT_gi1, previousDepth, material, Pass.GetCameraDepthTexture);
                }


                //Set the result to be accessed in the shader
                context.command.SetGlobalTexture("GITexture", RT_gi3);

                //Actually apply the GI to the scene using gbuffer data
                context.command.Blit(context.source, RT_FXAART, material, settings.visualizeGI.value ? Pass.VisualizeGI : Pass.BlendWithScene);
            }
            else    //If Half Resolution tracing is disabled
            {

                if (settings.temporalBlendWeight.value < 1.0f)
                {
                    //Perform a bilateral blur to be applied in newly revealed areas that are still noisy due to not having previous data blended with it
                    //material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                    //SEGIBuffer.Blit(RT_gi2, RT_blur1, material, Pass.BilateralBlur);
                    //material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    //SEGIBuffer.Blit(RT_blur1, RT_blur0, material, Pass.BilateralBlur);

                    //context.command.Blit(RT_gi2, RT_blur1, Gaussian_Material);
                    //material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    context.command.Blit(RT_gi3, RT_blur0, Gaussian_Material);
                    context.command.SetGlobalTexture("BlurredGI", RT_blur0);

                    //Perform temporal reprojection and blending
                    context.command.Blit(RT_gi2, RT_gi1, material, Pass.TemporalBlend);
                    context.command.Blit(RT_gi1, previousGIResult);
                    context.command.Blit(RT_gi1, previousDepth, material, Pass.GetCameraDepthTexture);
                }

                //Actually apply the GI to the scene using gbuffer data
                context.command.SetGlobalTexture("GITexture", RT_gi2);
                context.command.Blit(context.source, RT_FXAART, material, settings.visualizeGI.value ? Pass.VisualizeGI : Pass.BlendWithScene);
            }
            if (settings.useFXAA.value)
            {
                context.command.Blit(RT_FXAART, RT_FXAARTluminance, FXAA_Material, 0);
                context.command.Blit(RT_FXAARTluminance, context.destination, FXAA_Material, 1);
            }
            else context.command.Blit(RT_FXAART, context.destination);

            //ENDCommandBuffer         

            context.command.SetGlobalMatrix("ProjectionPrev", context.camera.projectionMatrix);
            context.command.SetGlobalMatrix("ProjectionPrevInverse", context.camera.projectionMatrix.inverse);
            context.command.SetGlobalMatrix("WorldToCameraPrev", context.camera.worldToCameraMatrix);
            context.command.SetGlobalMatrix("CameraToWorldPrev", context.camera.cameraToWorldMatrix);
            context.command.SetGlobalVector("CameraPositionPrev", context.camera.transform.position);

            //Advance the frame counter
            frameSwitch = (frameSwitch + 1) % (64);
        }

        public override void Init()
        {
            if (SEGIRenderWidth == 0) return;

            //Gaussian Filter
            Gaussian_Shader = Shader.Find("Hidden/SEGI Gaussian Blur Filter");
            Gaussian_Material = new Material(Gaussian_Shader);
            Gaussian_Material.enableInstancing = true;

            //FXAA
            FXAA_Shader = Shader.Find("Hidden/SEGIFXAA");
            FXAA_Material = new Material(FXAA_Shader);
            FXAA_Material.enableInstancing = true;
            FXAA_Material.SetFloat("_ContrastThreshold", 0.063f);
            FXAA_Material.SetFloat("_RelativeThreshold", 0.063f);
            FXAA_Material.SetFloat("_SubpixelBlending", 1f);
            FXAA_Material.DisableKeyword("LUMINANCE_GREEN");

            CubeMap_Shader = Shader.Find("Hidden/SEGIUnLitCubemap");

            //Color Correction
            //ColorCorrection_Shader = Shader.Find("Hidden/Delighter/ColorCorrection");
            //ColorCorrection_Material = new Material(ColorCorrection_Shader);

            //Setup shaders and materials
            sunDepthShader = Shader.Find("Hidden/SEGIRenderSunDepth_C");
            clearCompute = Resources.Load("SEGIClear_C") as ComputeShader;
            clearComputeCache = Resources.Load("SEGIClear_Cache") as ComputeShader;
            transferIntsCompute = Resources.Load("SEGITransferInts_C") as ComputeShader;
            mipFilterCompute = Resources.Load("SEGIMipFilter_C") as ComputeShader;
            voxelizationShader = Shader.Find("Hidden/SEGIVoxelizeScene_C");
            voxelTracingShader = Shader.Find("Hidden/SEGITraceScene_C");

            if (!material)
            {
                material = new Material(Shader.Find("Hidden/SEGI_C"));
                material.enableInstancing = true;
                material.hideFlags = HideFlags.HideAndDontSave;
            }

            //Get the camera attached to this game object
            attachedCamera = Camera.main;
            attachedCamera.depthTextureMode |= DepthTextureMode.Depth;
            attachedCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
#if UNITY_5_4_OR_NEWER
            attachedCamera.depthTextureMode |= DepthTextureMode.MotionVectors;
#endif

            //Find the proxy reflection render probe if it exists
            reflectionProbeGameObject = GameObject.Find("SEGI_REFLECTIONPROBE");
            if (!reflectionProbeGameObject)
            {
                reflectionProbeGameObject = new GameObject("SEGI_REFLECTIONPROBE");
            }
            /*reflectionProbe = reflectionProbeGameObject.GetComponent<ReflectionProbe>();
            if (!reflectionProbe)
            {
                reflectionProbe = reflectionProbeGameObject.AddComponent<ReflectionProbe>();

            }*/
            reflectionCamera = reflectionProbeGameObject.GetComponent<Camera>();
            if (!reflectionCamera)
            {
                reflectionCamera = reflectionProbeGameObject.AddComponent<Camera>();

            }
            /*if (!reflectionProbe)
            {
                reflectionProbe = reflectionProbeGameObject.AddComponent<ReflectionProbe>();
            }*/
            reflectionProbeGameObject.hideFlags = HideFlags.HideAndDontSave;
            reflectionProbeGameObject.transform.parent = attachedCamera.transform;
            reflectionProbeGameObject.transform.localPosition = new Vector3(0, 0, 0);
            reflectionProbeGameObject.transform.localRotation = Quaternion.identity;
            /*reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            reflectionProbe.cullingMask = settings.reflectionProbeLayerMask.GetValue<LayerMask>();
            reflectionProbe.size = new Vector3(settings.voxelSpaceSize.value, settings.voxelSpaceSize.value, settings.voxelSpaceSize.value);
            reflectionProbe.shadowDistance = 0;// settings.voxelSpaceSize.value;
            reflectionProbe.farClipPlane = settings.voxelSpaceSize.value;
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            //reflectionProbe.customBakedTexture = RT_Albedo;
            reflectionProbe.backgroundColor = Color.black;
            reflectionProbe.boxProjection = true;
            reflectionProbe.resolution = 128;
            reflectionProbe.importance = 1;
            reflectionProbe.enabled = false;
            reflectionProbe.hdr = false;*/
            reflectionCamera.cullingMask = settings.reflectionProbeLayerMask.GetValue<LayerMask>();
            reflectionCamera.farClipPlane = settings.voxelSpaceSize.value;
            reflectionCamera.renderingPath = RenderingPath.Forward;
            reflectionCamera.clearFlags = CameraClearFlags.Color;
            reflectionCamera.backgroundColor = Color.black;
            reflectionCamera.allowHDR = false;
            reflectionCamera.enabled = false;
            reflectionCamera.aspect = 1;



            reflectionCamera.backgroundColor = Color.black;


            //Find the proxy shadow rendering camera if it exists
            shadowCamGameObject = GameObject.Find("SEGI_SHADOWCAM");
            if (!shadowCamGameObject)
            {
                shadowCamGameObject = new GameObject("SEGI_SHADOWCAM");
            }
            shadowCam = shadowCamGameObject.GetComponent<Camera>();
            if (!shadowCam)
            {
                shadowCam = shadowCamGameObject.AddComponent<Camera>();

            }
            shadowCamGameObject.hideFlags = HideFlags.HideAndDontSave;
            shadowCam.enabled = false;
            shadowCam.depth = attachedCamera.depth - 1;
            shadowCam.orthographic = true;
            shadowCam.stereoTargetEye = StereoTargetEyeMask.None;
            shadowCam.orthographicSize = shadowSpaceSize;
            shadowCam.clearFlags = CameraClearFlags.SolidColor;
            shadowCam.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            shadowCam.farClipPlane = shadowSpaceSize * 2.0f * shadowSpaceDepthRatio;
            //shadowCam.stereoTargetEye = StereoTargetEyeMask.None;
            shadowCam.cullingMask = settings.giCullingMask.GetValue<LayerMask>();
            shadowCam.useOcclusionCulling = false;
            shadowCamTransform = shadowCamGameObject.transform;

            if (sunDepthTexture)
            {
                //sunDepthTexture.DiscardContents();
                sunDepthTexture.Release();
                //DestroyImmediate(sunDepthTexture);
            }
            sunDepthTexture = new RenderTexture(sunShadowResolution, sunShadowResolution, 32, RenderTextureFormat.RHalf, RenderTextureReadWrite.Default);
            sunDepthTexture.vrUsage = VRTextureUsage.None;
            sunDepthTexture.wrapMode = TextureWrapMode.Clamp;
            sunDepthTexture.filterMode = FilterMode.Point;
            sunDepthTexture.Create();
            sunDepthTexture.hideFlags = HideFlags.HideAndDontSave;



            //Get blue noise textures
            blueNoise = new Texture2D[64];
            for (int i = 0; i < 64; i++)
            {
                string fileName = "LDR_RGBA_" + i.ToString();
                Texture2D blueNoiseTexture = Resources.Load("Noise Textures/" + fileName) as Texture2D;

                if (blueNoiseTexture == null)
                {
                    Debug.LogWarning("Unable to find noise texture \"Assets/SEGI/Resources/Noise Textures/" + fileName + "\" for SEGI!");
                }

                blueNoise[i] = blueNoiseTexture;

            }


            voxelCameraGO = GameObject.Find("SEGI_VOXEL_CAMERA");
            if (!voxelCameraGO)
            {
                voxelCameraGO = new GameObject("SEGI_VOXEL_CAMERA");
            }
            voxelCamera = voxelCameraGO.GetComponent<Camera>();
            if (!voxelCamera)
            {
                voxelCamera = voxelCameraGO.AddComponent<Camera>();
            }
            voxelCameraGO.hideFlags = HideFlags.HideAndDontSave;
            voxelCamera.enabled = false;
            voxelCamera.orthographic = true;
            voxelCamera.orthographicSize = settings.voxelSpaceSize.value * 0.5f;
            voxelCamera.nearClipPlane = 0.0f;
            voxelCamera.farClipPlane = settings.voxelSpaceSize.value;
            voxelCamera.depth = -2;
            voxelCamera.stereoTargetEye = StereoTargetEyeMask.None;
            voxelCamera.renderingPath = RenderingPath.Forward;
            voxelCamera.clearFlags = CameraClearFlags.Color;
            voxelCamera.backgroundColor = Color.black;
            voxelCamera.useOcclusionCulling = false;


            leftViewPoint = GameObject.Find("SEGI_LEFT_VOXEL_VIEW");
            if (!leftViewPoint)
            {
                leftViewPoint = new GameObject("SEGI_LEFT_VOXEL_VIEW");
                leftViewPoint.hideFlags = HideFlags.HideAndDontSave;
            }

            topViewPoint = GameObject.Find("SEGI_TOP_VOXEL_VIEW");
            if (!topViewPoint)
            {
                topViewPoint = new GameObject("SEGI_TOP_VOXEL_VIEW");
                topViewPoint.hideFlags = HideFlags.HideAndDontSave;
            }

            //CreateVolumeTextures();
            //BuildClipmaps();
            ResizeAllTextures();

            initChecker = true;

        }

        void CreateVolumeTextures()
        {
            if (integerVolume)
            {
                //integerVolume.DiscardContents();
                integerVolume.Release();
                //DestroyImmediate(integerVolume);
            }
            integerVolume = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear);
#if UNITY_5_4_OR_NEWER
            integerVolume.dimension = TextureDimension.Tex3D;
#else
		integerVolume.isVolume = true;
#endif
            integerVolume.volumeDepth = (int)settings.voxelResolution.value;
            integerVolume.enableRandomWrite = true;
            integerVolume.filterMode = FilterMode.Point;
            integerVolume.Create();
            integerVolume.hideFlags = HideFlags.HideAndDontSave;

            CleanupTexture(ref tracedTexture0);
            tracedTexture0 = new RenderTexture((int)settings.traceCacheResolution.value, (int)settings.traceCacheResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            tracedTexture0.wrapMode = TextureWrapMode.Clamp;
            #if UNITY_5_4_OR_NEWER
                tracedTexture0.dimension = TextureDimension.Tex3D;
            #else
	            tracedTexture0.isVolume = true;
            #endif
            tracedTexture0.volumeDepth = (int)settings.traceCacheResolution.value;
            tracedTexture0.enableRandomWrite = true;
            tracedTexture0.filterMode = FilterMode.Bilinear;
            #if UNITY_5_4_OR_NEWER
                tracedTexture0.autoGenerateMips = false;
            #else
	            tracedTexture0.generateMips = false;
            #endif
            tracedTexture0.useMipMap = false;
            tracedTexture0.Create();
            tracedTexture0.hideFlags = HideFlags.HideAndDontSave;

            CleanupTexture(ref tracedTexture1);
            tracedTexture1 = new RenderTexture((int)settings.traceCacheResolution.value, (int)settings.traceCacheResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            tracedTexture1.wrapMode = TextureWrapMode.Clamp;
            #if UNITY_5_4_OR_NEWER
            tracedTexture1.dimension = TextureDimension.Tex3D;
            #else
	            tracedTexture1.isVolume = true;
            #endif
            tracedTexture1.volumeDepth = (int)settings.traceCacheResolution.value;
            tracedTexture1.enableRandomWrite = true;
            tracedTexture1.filterMode = FilterMode.Bilinear;
            #if UNITY_5_4_OR_NEWER
            tracedTexture1.autoGenerateMips = false;
            #else
	            tracedTexture1.generateMips = false;
            #endif
            tracedTexture1.useMipMap = false;
            tracedTexture1.Create();
            tracedTexture1.hideFlags = HideFlags.HideAndDontSave;

            ResizeDummyTexture();
        }

        void ResizeDummyTexture()
        {
            CleanupTexture(ref dummyVoxelTextureAAScaled);
            dummyVoxelTextureAAScaled = new RenderTexture(DummyVoxelResolution, DummyVoxelResolution, 0, RenderTextureFormat.R8);
            dummyVoxelTextureAAScaled.Create();
            dummyVoxelTextureAAScaled.hideFlags = HideFlags.HideAndDontSave;

            CleanupTexture(ref dummyVoxelTextureFixed);
            dummyVoxelTextureFixed = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.R8);
            dummyVoxelTextureFixed.Create();
            dummyVoxelTextureFixed.hideFlags = HideFlags.HideAndDontSave;
        }

        void InitCheck()
        {
            if (!initChecker)
            {
                Init();
            }
        }

        void BuildClipmaps()
        {
            if (clipmaps != null)
            {
                for (int i = 0; i < numClipmaps; i++)
                {
                    if (clipmaps[i] != null)
                    {
                        clipmaps[i].CleanupTextures();
                    }
                }
            }

            clipmaps = new Clipmap[numClipmaps];

            for (int i = 0; i < numClipmaps; i++)
            {
                clipmaps[i] = new Clipmap();
                clipmaps[i].localScale = Mathf.Pow(2.0f, (float)i);
                clipmaps[i].resolution = (int)settings.voxelResolution.value;
                clipmaps[i].filterMode = FilterMode.Bilinear;
                clipmaps[i].renderTextureFormat = RenderTextureFormat.ARGBHalf;
                clipmaps[i].UpdateTextures();
            }

            if (irradianceClipmaps != null)
            {
                for (int i = 0; i < numClipmaps; i++)
                {
                    if (irradianceClipmaps[i] != null)
                    {
                        irradianceClipmaps[i].CleanupTextures();
                    }
                }
            }

            irradianceClipmaps = new Clipmap[numClipmaps];

            for (int i = 0; i < numClipmaps; i++)
            {
                irradianceClipmaps[i] = new Clipmap();
                irradianceClipmaps[i].localScale = Mathf.Pow(2.0f, i);
                irradianceClipmaps[i].resolution = (int)settings.voxelResolution.value;
                irradianceClipmaps[i].filterMode = FilterMode.Point;
                irradianceClipmaps[i].renderTextureFormat = RenderTextureFormat.ARGBHalf;
                irradianceClipmaps[i].UpdateTextures();
            }
        }

        Matrix4x4 TransformViewMatrix(Matrix4x4 mat)
        {
            //Since the third column of the view matrix needs to be reversed if using reversed z-buffer, do so here
#if UNITY_5_5_OR_NEWER
            if (SystemInfo.usesReversedZBuffer)
            {
                mat[2, 0] = -mat[2, 0];
                mat[2, 1] = -mat[2, 1];
                mat[2, 2] = -mat[2, 2];
                mat[2, 3] = -mat[2, 3];
            }
#endif
            return mat;
        }

        public void ResizeAllTextures()
        {
            CleanupTextures();

            BuildClipmaps();
            CreateVolumeTextures();
            ResizeSunShadowBuffer();

            //StopCoroutine(updateVoxels());

            if (SEGIRenderWidth == 0) SEGIRenderWidth = attachedCamera.scaledPixelWidth;
            if (SEGIRenderHeight == 0) SEGIRenderHeight = attachedCamera.scaledPixelHeight;

            //SEGIRenderWidth = attachedCamera.scaledPixelWidth == 0 ? 2 : attachedCamera.scaledPixelWidth;
            //SEGIRenderHeight = attachedCamera.scaledPixelHeight == 0 ? 2 : attachedCamera.scaledPixelHeight;

            if (previousGIResult) previousGIResult.Release();
            previousGIResult = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf);
            previousGIResult.wrapMode = TextureWrapMode.Clamp;
            previousGIResult.filterMode = FilterMode.Bilinear;
            previousGIResult.useMipMap = true;
#if UNITY_5_4_OR_NEWER
            previousGIResult.autoGenerateMips = false;
#else
		        previousResult.generateMips = false;
#endif
            previousGIResult.Create();
            previousGIResult.hideFlags = HideFlags.HideAndDontSave;

            if (previousDepth)
            {
                //previousDepth.DiscardContents();
                previousDepth.Release();
                //DestroyImmediate(previousDepth);
            }
            previousDepth = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            previousDepth.wrapMode = TextureWrapMode.Clamp;
            previousDepth.filterMode = FilterMode.Bilinear;
            previousDepth.Create();
            previousDepth.hideFlags = HideFlags.HideAndDontSave;

            if (RT_FXAART) RT_FXAART.Release();
            RT_FXAART = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) RT_FXAART.vrUsage = VRTextureUsage.TwoEyes;
            RT_FXAART.Create();

            if (RT_gi1) RT_gi1.Release();
            RT_gi1 = new RenderTexture(SEGIRenderWidth / (int)settings.GIResolution.value, SEGIRenderHeight / (int)settings.GIResolution.value, 0, RenderTextureFormat.ARGBHalf);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi1.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi1.filterMode = FilterMode.Bilinear;
            RT_gi1.Create();

            if (RT_gi2) RT_gi2.Release();
            RT_gi2 = new RenderTexture(SEGIRenderWidth / (int)settings.GIResolution.value, SEGIRenderHeight / (int)settings.GIResolution.value, 0, RenderTextureFormat.ARGBHalf);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi2.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi2.filterMode = FilterMode.Bilinear;
            RT_gi2.Create();

            if (RT_reflections) RT_reflections.Release();
            RT_reflections = new RenderTexture(SEGIRenderWidth / (int)settings.GIResolution.value, SEGIRenderHeight / (int)settings.GIResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) RT_reflections.vrUsage = VRTextureUsage.TwoEyes;
            RT_reflections.Create();

            if (RT_gi3) RT_gi3.Release();
            RT_gi3 = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi3.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi3.filterMode = FilterMode.Bilinear;
            RT_gi3.Create();

            if (RT_gi4) RT_gi4.Release();
            RT_gi4 = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi4.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi4.filterMode = FilterMode.Bilinear;
            RT_gi4.Create();

            if (RT_blur0) RT_blur0.Release();
            RT_blur0 = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) RT_blur0.vrUsage = VRTextureUsage.TwoEyes;
            RT_blur0.Create();

            if (RT_blur1) RT_blur1.Release();
            RT_blur1 = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) RT_blur1.vrUsage = VRTextureUsage.TwoEyes;
            RT_blur1.Create();

            if (RT_FXAARTluminance) RT_FXAARTluminance.Release();
            RT_FXAARTluminance = new RenderTexture(SEGIRenderWidth, SEGIRenderHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) RT_FXAARTluminance.vrUsage = VRTextureUsage.TwoEyes;
            RT_FXAARTluminance.Create();

            int RT_AlbedoResolution;
            if (RT_Albedo) RT_Albedo.Release();
            if (RT_AlbedoX2) RT_Albedo.Release();
            if (attachedCamera != null) if (attachedCamera.renderingPath == RenderingPath.Forward)
            {
                if (XR.XRSettings.enabled) RT_AlbedoResolution = Mathf.NextPowerOfTwo((SEGIRenderWidth + SEGIRenderHeight) / 4);
                else RT_AlbedoResolution = Mathf.NextPowerOfTwo((SEGIRenderWidth + SEGIRenderHeight) / 2);
                RT_Albedo = new RenderTexture(RT_AlbedoResolution, RT_AlbedoResolution, 16, renderTextureFormat);
                RT_Albedo.dimension = TextureDimension.Cube;
                RT_Albedo.filterMode = FilterMode.Trilinear;
                RT_Albedo.isPowerOfTwo = true;
                RT_Albedo.Create();
                RT_AlbedoX2 = new RenderTexture(RT_AlbedoResolution * 2, RT_AlbedoResolution * 2, 16, renderTextureFormat);
                RT_AlbedoX2.dimension = TextureDimension.Cube;
                RT_AlbedoX2.filterMode = FilterMode.Point;
                RT_AlbedoX2.isPowerOfTwo = true;
                RT_AlbedoX2.Create();
            }

            Debug.Log("<SEGI> Render Textures resized");

            //SEGIBufferInit();
            //StartCoroutine(updateVoxels());
            updateVoxelsAfterXDoUpdate = true;
        }

        void ResizeSunShadowBuffer()
        {

            if (sunDepthTexture)
            {
                //sunDepthTexture.DiscardContents();
                sunDepthTexture.Release();
                //DestroyImmediate(sunDepthTexture);
            }
            sunDepthTexture = new RenderTexture(sunShadowResolution, sunShadowResolution, 16, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            sunDepthTexture.vrUsage = VRTextureUsage.None;
            sunDepthTexture.wrapMode = TextureWrapMode.Clamp;
            sunDepthTexture.filterMode = FilterMode.Point;
            sunDepthTexture.Create();
            sunDepthTexture.hideFlags = HideFlags.HideAndDontSave;
        }



        public override void Release()
        {
            CleanupTextures();
        }

        void CleanupTextures()
        {
            CleanupTexture(ref sunDepthTexture);
            CleanupTexture(ref previousGIResult);
            CleanupTexture(ref previousDepth);
            CleanupTexture(ref integerVolume);
            CleanupTexture(ref dummyVoxelTextureAAScaled);
            CleanupTexture(ref dummyVoxelTextureFixed);

            if (clipmaps != null)
            {
                for (int i = 0; i < numClipmaps; i++)
                {
                    if (clipmaps[i] != null)
                    {
                        clipmaps[i].CleanupTextures();
                    }
                }
            }

            if (irradianceClipmaps != null)
            {
                for (int i = 0; i < numClipmaps; i++)
                {
                    if (irradianceClipmaps[i] != null)
                    {
                        irradianceClipmaps[i].CleanupTextures();
                    }
                }
            }

            CleanupTexture(ref tracedTexture0);
            CleanupTexture(ref tracedTexture1);

            if (RT_FXAART) RT_FXAART.Release();
            if (RT_gi1) RT_gi1.Release();
            if (RT_gi2) RT_gi2.Release();
            if (RT_reflections) RT_reflections.Release();
            if (RT_gi3) RT_gi3.Release();
            if (RT_gi4) RT_gi4.Release();
            if (RT_blur0) RT_blur0.Release();
            if (RT_blur1) RT_blur1.Release();
            if (RT_FXAARTluminance) RT_FXAARTluminance.Release();
            CleanupTexture(ref RT_Albedo);
            CleanupTexture(ref RT_AlbedoX2);
        }

        void CleanupTexture(ref RenderTexture texture)
        {
            if (texture)
            {
                texture.Release();
            }
        }

        int SelectCascadeBinary(int c)
        {
            float counter = c + 0.01f;

            int result = 0;
            for (int i = 1; i < numClipmaps; i++)
            {
                float level = Mathf.Pow(2.0f, i);
                result += Mathf.CeilToInt(((counter / level) % 1.0f) - ((level - 1.0f) / level));
            }

            return result;
        }
    }

    class Clipmap
    {
        public Vector3 origin;
        public Vector3 originDelta;
        public Vector3 previousOrigin;
        public float localScale;

        public int resolution;

        public RenderTexture volumeTexture0;

        public FilterMode filterMode = FilterMode.Bilinear;
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGBHalf;

        public void UpdateTextures()
        {
            if (volumeTexture0)
            {
                //volumeTexture0.DiscardContents();
                volumeTexture0.Release();
                //DestroyImmediate(volumeTexture0);
            }
            volumeTexture0 = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            volumeTexture0.wrapMode = TextureWrapMode.Clamp;
#if UNITY_5_4_OR_NEWER
            volumeTexture0.dimension = TextureDimension.Tex3D;
#else
			volumeTexture0.isVolume = true;
#endif
            volumeTexture0.volumeDepth = resolution;
            volumeTexture0.enableRandomWrite = true;
            volumeTexture0.filterMode = filterMode;
#if UNITY_5_4_OR_NEWER
            volumeTexture0.autoGenerateMips = false;
#else
			volumeTexture0.generateMips = false;
#endif
            volumeTexture0.useMipMap = false;
            volumeTexture0.Create();
            volumeTexture0.hideFlags = HideFlags.HideAndDontSave;
        }

        public void CleanupTextures()
        {
            if (volumeTexture0)
            {
                //volumeTexture0.DiscardContents();
                volumeTexture0.Release();
                //DestroyImmediate(volumeTexture0);
            }
        }
    }
}

//####################################################################################################################################
//####################################################################################################################################
//####################################################################################################################################

//####################################################################################################################################
//####################################################################################################################################
//####################################################################################################################################

