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
    public enum VoxelResolutionEnum
    {
        Low = 64,
        Medium = 128,
        High = 256
    }

    [Serializable]
    public sealed class SEGILayerMask : ParameterOverride<LayerMask> { }

    [Serializable]
    public sealed class SEGITransform : ParameterOverride<Transform> { }

    [Serializable]
    [PostProcess(typeof(SEGIRenderer), PostProcessEvent.BeforeStack, "NKLI/SEGI")]

    public sealed class SEGI_NKLI : PostProcessEffectSettings
    {


        public IntParameter voxelResolution = new IntParameter { value = 256 }; // VoxelResolutionEnum.High };
        public FloatParameter voxelSpaceSize = new FloatParameter { value = 50.0f };
        public BoolParameter updateVoxelsAfterX = new BoolParameter { value = false };
        public IntParameter updateVoxelsAfterXInterval = new IntParameter { value = 1 };
        public BoolParameter voxelAA = new BoolParameter { value = false };
        [Range(0, 2)]
        public IntParameter innerOcclusionLayers = new IntParameter { value = 1 };
        public BoolParameter infiniteBounces = new BoolParameter { value = true };


        public BoolParameter useReflectionProbes = new BoolParameter { value = true };
        [Range(0, 2)]
        public FloatParameter reflectionProbeIntensity = new FloatParameter { value = 0.5f };
        [Range(0, 2)]
        public FloatParameter reflectionProbeAttribution = new FloatParameter { value = 1f };
        public BoolParameter doReflections = new BoolParameter { value = true };

        [Range(0.01f, 1.0f)]
        public FloatParameter temporalBlendWeight = new FloatParameter { value = 1.0f };
        public BoolParameter useBilateralFiltering = new BoolParameter { value = true };// Actually used?
        [Range(1, 16)]
        public IntParameter GIResolution = new IntParameter { value = 1 };
        public BoolParameter stochasticSampling = new BoolParameter { value = true };
        public BoolParameter updateGI = new BoolParameter { value = true };

        [Range(1, 128)]
        public IntParameter cones = new IntParameter { value = 13 };
        [Range(1, 32)]
        public IntParameter coneTraceSteps = new IntParameter { value = 8 };
        [Range(0.1f, 2.0f)]
        public FloatParameter coneLength = new FloatParameter { value = 1.0f };
        [Range(0.5f, 12.0f)]
        public FloatParameter coneWidth = new FloatParameter { value = 6.0f };
        [Range(0.0f, 4.0f)]
        public FloatParameter coneTraceBias = new FloatParameter { value = 0.63f };
        [Range(0.0f, 4.0f)]
        public FloatParameter occlusionStrength = new FloatParameter { value = 1.0f };
        [Range(0.0f, 4.0f)]
        public FloatParameter nearOcclusionStrength = new FloatParameter { value = 0.0f };
        [Range(0.001f, 4.0f)]
        public FloatParameter occlusionPower = new FloatParameter { value = 1.0f };
        [Range(0.0f, 4.0f)]
        public FloatParameter nearLightGain = new FloatParameter { value = 1.0f };
        [Range(0.0f, 4.0f)]
        public FloatParameter giGain = new FloatParameter { value = 1.0f };
        [Range(0.0f, 2.0f)]
        public FloatParameter secondaryBounceGain = new FloatParameter { value = 1.0f };
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
        public FloatParameter secondaryOcclusionStrength = new FloatParameter { value = 1.0f };

        public BoolParameter useFXAA = new BoolParameter { value = false };

        public BoolParameter visualizeGI = new BoolParameter { value = false };
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

        public ReflectionProbe reflectionProbe;
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

        ///<summary>An array of volume textures where each element is a mip/LOD level. Each volume is half the resolution of the previous volume. Separate textures for each mip level are required for manual mip-mapping of the main GI volume texture.</summary>
        RenderTexture[] volumeTextures;

        ///<summary>The secondary volume texture that holds irradiance calculated during the in-volume GI tracing that occurs when Infinite Bounces is enabled. </summary>
        RenderTexture secondaryIrradianceVolume;

        ///<summary>The alternate mip level 0 main volume texture needed to avoid simultaneous read/write errors while performing temporal stabilization on the main voxel volume.</summary>
        RenderTexture volumeTextureB;

        ///<summary>The current active volume texture that holds GI information to be read during GI tracing.</summary>
        RenderTexture activeVolume;

        ///<summary>The volume texture that holds GI information to be read during GI tracing that was used in the previous frame.</summary>
        RenderTexture previousActiveVolume;

        ///<summary>A 2D texture with the size of [voxel resolution, voxel resolution] that must be used as the active render texture when rendering the scene for voxelization. This texture scales depending on whether Voxel AA is enabled to ensure correct voxelization.</summary>
        RenderTexture dummyVoxelTextureAAScaled;

        ///<summary>A 2D texture with the size of [voxel resolution, voxel resolution] that must be used as the active render texture when rendering the scene for voxelization. This texture is always the same size whether Voxel AA is enabled or not.</summary>
        RenderTexture dummyVoxelTextureFixed;

        public static RenderTexture sunDepthTexture;
        public static RenderTexture previousGIResult;
        public static RenderTexture previousResult;
        public static RenderTexture previousDepth;

        public bool notReadyToRender = false;

        public Shader voxelizationShader;
        public Shader voxelTracingShader;

        public ComputeShader clearCompute;
        public ComputeShader transferIntsCompute;
        public ComputeShader mipFilterCompute;

        const int numMipLevels = 6;

        public Camera voxelCamera;
        public GameObject voxelCameraGO;
        public GameObject leftViewPoint;
        public GameObject topViewPoint;

        float voxelScaleFactor
        {
            get
            {
                return (float)(int)settings.voxelResolution.value / 256.0f;
            }
        }

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



        [ImageEffectOpaque]
        public override void Render(PostProcessRenderContext context)
        {
            // Update
            InitCheck();

            if (SEGIRenderWidth != context.width || SEGIRenderHeight != context.height || settings.GIResolution.value != GIResolutionPrev)
            {
                Debug.Log("<SEGI> Context != Cached Dimensions. Resizing buffers");
                GIResolutionPrev = settings.GIResolution.value;
                SEGIRenderWidth = context.width;
                SEGIRenderHeight = context.height;

                ResizeAllTextures();
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

            if (previousGIResult == null)
            {
                Debug.Log("<SEGI> PreviousGIResult == null. Resizing Render Textures.");
                ResizeRenderTextures();
            }

            if (previousGIResult.width != context.width || previousGIResult.height != context.height)
            {
                Debug.Log("<SEGI> previousGIResult != Expected Dimensions. Resizing Render Textures");
                ResizeRenderTextures();
            }

            if ((int)sunShadowResolution != prevSunShadowResolution)
            {
                ResizeSunShadowBuffer();
            }

            prevSunShadowResolution = (int)sunShadowResolution;

            if (volumeTextures[0].width != (int)settings.voxelResolution.value)
            {
                CreateVolumeTextures();
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
                    /*if (VRWorksComponent.IsFeatureAvailable(VRWorks.Feature.LensMatchedShading))
                    {
                        VRWorksComponent.SetActiveFeature(VRWorks.Feature.LensMatchedShading);
                    }
                    else*/ if (VRWorksComponent.IsFeatureAvailable(VRWorks.Feature.SinglePassStereo))
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

            if (attachedCamera.renderingPath == RenderingPath.Forward && reflectionProbe.enabled)
            {
                reflectionProbe.enabled = true;
                reflectionProbe.intensity = settings.reflectionProbeIntensity.value;
                reflectionProbe.cullingMask = settings.reflectionProbeLayerMask.GetValue<LayerMask>();
            }
            else
            {
                reflectionProbe.enabled = false;
            }

            // only use main camera for voxel simulations
            if (attachedCamera != Camera.main)
            {
                Debug.Log("<SEGI> Instance not attached to Main Camera. Please ensure the attached camera has the 'MainCamera' tag.");
                return;
            }

            //Update SkyColor
            if (settings.MatchAmbiantColor)
            {
                settings.skyColor.value = RenderSettings.ambientLight;
                settings.skyIntensity.value = RenderSettings.ambientIntensity;
            }


            //Cache the previous active render texture to avoid issues with other Unity rendering going on
            RenderTexture previousActive = RenderTexture.active;

            Shader.SetGlobalInt("SEGIVoxelAA", settings.voxelAA.value ? 1 : 0);



            if (!settings.updateVoxelsAfterX.value) updateVoxelsAfterXDoUpdate = true;
            if (attachedCamera.transform.position.x - updateVoxelsAfterXPrevX >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevX - attachedCamera.transform.position.x >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;

            if (attachedCamera.transform.position.y - updateVoxelsAfterXPrevY >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevY - attachedCamera.transform.position.y >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;

            if (attachedCamera.transform.position.z - updateVoxelsAfterXPrevZ >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevZ - attachedCamera.transform.position.z >= settings.updateVoxelsAfterXInterval.value) updateVoxelsAfterXDoUpdate = true;

            if (settings.updateGI.value)
            {

                if (renderState == RenderState.Voxelize && updateVoxelsAfterXDoUpdate == true)
                {
                    activeVolume = voxelFlipFlop == 0 ? volumeTextures[0] : volumeTextureB;             //Flip-flopping volume textures to avoid simultaneous read and write errors in shaders
                    previousActiveVolume = voxelFlipFlop == 0 ? volumeTextureB : volumeTextures[0];

                    //float voxelTexel = (1.0f * voxelSpaceSize) / (int)voxelResolution * 0.5f;			//Calculate the size of a voxel texel in world-space units



                    //Setup the voxel volume origin position
                    float interval = settings.voxelSpaceSize.value / 8.0f;                                             //The interval at which the voxel volume will be "locked" in world-space
                    Vector3 origin;
                    if (SEGI_NKLI.followTransform)
                    {
                        origin = SEGI_NKLI.followTransform.position;
                    }
                    else
                    {
                        //GI is still flickering a bit when the scene view and the game view are opened at the same time
                        origin = attachedCamera.transform.position + attachedCamera.transform.forward * settings.voxelSpaceSize.value / 4.0f;
                    }
                    //Lock the voxel volume origin based on the interval
                    voxelSpaceOrigin = new Vector3(Mathf.Round(origin.x / interval) * interval, Mathf.Round(origin.y / interval) * interval, Mathf.Round(origin.z / interval) * interval);

                    //Calculate how much the voxel origin has moved since last voxelization pass. Used for scrolling voxel data in shaders to avoid ghosting when the voxel volume moves in the world
                    voxelSpaceOriginDelta = voxelSpaceOrigin - previousVoxelSpaceOrigin;
                    Shader.SetGlobalVector("SEGIVoxelSpaceOriginDelta", voxelSpaceOriginDelta / settings.voxelSpaceSize.value);

                    previousVoxelSpaceOrigin = voxelSpaceOrigin;

                    //Set the voxel camera (proxy camera used to render the scene for voxelization) parameters
                    voxelCamera.enabled = false;
                    voxelCamera.orthographic = true;
                    voxelCamera.orthographicSize = settings.voxelSpaceSize.value * 0.5f;
                    voxelCamera.nearClipPlane = 0.0f;
                    voxelCamera.farClipPlane = settings.voxelSpaceSize.value;
                    voxelCamera.depth = -2;
                    voxelCamera.renderingPath = RenderingPath.Forward;
                    voxelCamera.clearFlags = CameraClearFlags.Color;
                    voxelCamera.backgroundColor = Color.black;
                    voxelCamera.cullingMask = settings.giCullingMask.GetValue<LayerMask>();

                    //Move the voxel camera game object and other related objects to the above calculated voxel space origin
                    voxelCameraGO.transform.position = voxelSpaceOrigin - Vector3.forward * settings.voxelSpaceSize.value * 0.5f;
                    voxelCameraGO.transform.rotation = rotationFront;

                    leftViewPoint.transform.position = voxelSpaceOrigin + Vector3.left * settings.voxelSpaceSize.value * 0.5f;
                    leftViewPoint.transform.rotation = rotationLeft;
                    topViewPoint.transform.position = voxelSpaceOrigin + Vector3.up * settings.voxelSpaceSize.value * 0.5f;
                    topViewPoint.transform.rotation = rotationTop;

                    //Set matrices needed for voxelization
                    Shader.SetGlobalMatrix("WorldToCamera", attachedCamera.worldToCameraMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelViewFront", TransformViewMatrix(voxelCamera.transform.worldToLocalMatrix));
                    Shader.SetGlobalMatrix("SEGIVoxelViewLeft", TransformViewMatrix(leftViewPoint.transform.worldToLocalMatrix));
                    Shader.SetGlobalMatrix("SEGIVoxelViewTop", TransformViewMatrix(topViewPoint.transform.worldToLocalMatrix));
                    Shader.SetGlobalMatrix("SEGIWorldToVoxel", voxelCamera.worldToCameraMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelProjection", voxelCamera.projectionMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelProjectionInverse", voxelCamera.projectionMatrix.inverse);

                    Shader.SetGlobalInt("SEGIVoxelResolution", (int)settings.voxelResolution.value);

                    Matrix4x4 voxelToGIProjection = (shadowCam.projectionMatrix) * (shadowCam.worldToCameraMatrix) * (voxelCamera.cameraToWorldMatrix);
                    Shader.SetGlobalMatrix("SEGIVoxelToGIProjection", voxelToGIProjection);
                    Shader.SetGlobalVector("SEGISunlightVector", SEGI_NKLI.Sun ? Vector3.Normalize(SEGI_NKLI.Sun.transform.forward) : Vector3.up);

                    //Set paramteters
                    Shader.SetGlobalColor("GISunColor", SEGI_NKLI.Sun == null ? Color.black : new Color(Mathf.Pow(SEGI_NKLI.Sun.color.r, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.color.g, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.color.b, 2.2f), Mathf.Pow(SEGI_NKLI.Sun.intensity, 2.2f)));
                    Shader.SetGlobalColor("SEGISkyColor", new Color(Mathf.Pow(settings.skyColor.value.r * settings.skyIntensity.value * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.g * settings.skyIntensity.value * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.b * settings.skyIntensity.value * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.a, 2.2f)));
                    Shader.SetGlobalFloat("GIGain", settings.giGain.value);
                    Shader.SetGlobalFloat("SEGISecondaryBounceGain", settings.infiniteBounces.value ? settings.secondaryBounceGain.value : 0.0f);
                    Shader.SetGlobalFloat("SEGISoftSunlight", settings.softSunlight.value);
                    Shader.SetGlobalInt("SEGISphericalSkylight", settings.sphericalSkylight.value ? 1 : 0);
                    Shader.SetGlobalInt("SEGIInnerOcclusionLayers", settings.innerOcclusionLayers.value);


                    //Render the depth texture from the sun's perspective in order to inject sunlight with shadows during voxelization
                    if (SEGI_NKLI.Sun != null)
                    {
                        //Cache Shadow State
                        //ShadowStateCache = SEGI_NKLI.Sun.shadows;

                        shadowCam.cullingMask = settings.giCullingMask.GetValue<LayerMask>();

                        Vector3 shadowCamPosition = voxelSpaceOrigin + Vector3.Normalize(-SEGI_NKLI.Sun.transform.forward) * shadowSpaceSize * 0.5f * shadowSpaceDepthRatio;

                        shadowCamTransform.position = shadowCamPosition;
                        shadowCamTransform.LookAt(voxelSpaceOrigin, Vector3.up);

                        shadowCam.renderingPath = RenderingPath.Forward;
                        shadowCam.depthTextureMode |= DepthTextureMode.None;

                        shadowCam.orthographicSize = shadowSpaceSize;
                        shadowCam.farClipPlane = shadowSpaceSize * 2.0f * shadowSpaceDepthRatio;


                        Graphics.SetRenderTarget(sunDepthTexture);
                        shadowCam.SetTargetBuffers(sunDepthTexture.colorBuffer, sunDepthTexture.depthBuffer);

                        shadowCam.RenderWithShader(sunDepthShader, "");

                        Shader.SetGlobalTexture("SEGISunDepth", sunDepthTexture);

                        //Restore Shadow State
                        //SEGI_NKLI.Sun.shadows = ShadowStateCache;
                    }

                    //Clear the volume texture that is immediately written to in the voxelization scene shader
                    clearCompute.SetTexture(0, "RG0", integerVolume);
                    clearCompute.SetInt("Res", (int)settings.voxelResolution.value);
                    clearCompute.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                    //Cache Shadow State
                    //ShadowStateCache = SEGI_NKLI.Sun.shadows;

                    //Render the scene with the voxel proxy camera object with the voxelization shader to voxelize the scene to the volume integer texture
                    Graphics.SetRandomWriteTarget(1, integerVolume);
                    voxelCamera.targetTexture = dummyVoxelTextureAAScaled;
                    voxelCamera.RenderWithShader(voxelizationShader, "");
                    Graphics.ClearRandomWriteTargets();

                    //Restore Shadow State
                    //SEGI_NKLI.Sun.shadows = ShadowStateCache;

                    //Transfer the data from the volume integer texture to the main volume texture used for GI tracing. 
                    transferIntsCompute.SetTexture(0, "Result", activeVolume);
                    transferIntsCompute.SetTexture(0, "PrevResult", previousActiveVolume);
                    transferIntsCompute.SetTexture(0, "RG0", integerVolume);
                    transferIntsCompute.SetInt("VoxelAA", settings.voxelAA.value ? 1 : 0);
                    transferIntsCompute.SetInt("Resolution", (int)settings.voxelResolution.value);
                    transferIntsCompute.SetVector("VoxelOriginDelta", (voxelSpaceOriginDelta / settings.voxelSpaceSize.value) * (int)settings.voxelResolution.value);
                    transferIntsCompute.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                    Shader.SetGlobalTexture("SEGIVolumeLevel0", activeVolume);

                    //Manually filter/render mip maps
                    for (int i = 0; i < numMipLevels - 1; i++)
                    {
                        RenderTexture source = volumeTextures[i];

                        if (i == 0)
                        {
                            source = activeVolume;
                        }

                        int destinationRes = (int)settings.voxelResolution.value / Mathf.RoundToInt(Mathf.Pow((float)2, (float)i + 1.0f));
                        mipFilterCompute.SetInt("destinationRes", destinationRes);
                        mipFilterCompute.SetTexture(mipFilterKernel, "Source", source);
                        mipFilterCompute.SetTexture(mipFilterKernel, "Destination", volumeTextures[i + 1]);
                        mipFilterCompute.Dispatch(mipFilterKernel, destinationRes / 8, destinationRes / 8, 1);
                        Shader.SetGlobalTexture("SEGIVolumeLevel" + (i + 1).ToString(), volumeTextures[i + 1]);
                    }

                    //Advance the voxel flip flop counter
                    voxelFlipFlop += 1;
                    voxelFlipFlop = voxelFlipFlop % 2;

                    if (settings.infiniteBounces)
                    {
                        renderState = RenderState.Bounce;
                    }
                    else
                    {
                        updateVoxelsAfterXDoUpdate = false;
                        updateVoxelsAfterXPrevX = attachedCamera.transform.position.x;
                        updateVoxelsAfterXPrevY = attachedCamera.transform.position.y;
                        updateVoxelsAfterXPrevZ = attachedCamera.transform.position.z;
                    }
                }
                else if (renderState == RenderState.Bounce && updateVoxelsAfterXDoUpdate == true)
                {

                    //Clear the volume texture that is immediately written to in the voxelization scene shader
                    clearCompute.SetTexture(0, "RG0", integerVolume);
                    clearCompute.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                    //Set secondary tracing parameters
                    Shader.SetGlobalInt("SEGISecondaryCones", settings.secondaryCones.value);
                    Shader.SetGlobalFloat("SEGISecondaryOcclusionStrength", settings.secondaryOcclusionStrength.value);

                    //Cache Shadow State
                    //ShadowStateCache = SEGI_NKLI.Sun.shadows;

                    //Render the scene from the voxel camera object with the voxel tracing shader to render a bounce of GI into the irradiance volume
                    Graphics.SetRandomWriteTarget(1, integerVolume);
                    voxelCamera.targetTexture = dummyVoxelTextureFixed;
                    voxelCamera.RenderWithShader(voxelTracingShader, "");
                    Graphics.ClearRandomWriteTargets();

                    //Restore Shadow State
                    //SEGI_NKLI.Sun.shadows = ShadowStateCache;


                    //Transfer the data from the volume integer texture to the irradiance volume texture. This result is added to the next main voxelization pass to create a feedback loop for infinite bounces
                    transferIntsCompute.SetTexture(1, "Result", secondaryIrradianceVolume);
                    transferIntsCompute.SetTexture(1, "RG0", integerVolume);
                    transferIntsCompute.SetInt("Resolution", (int)settings.voxelResolution.value);
                    transferIntsCompute.Dispatch(1, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                    Shader.SetGlobalTexture("SEGIVolumeTexture1", secondaryIrradianceVolume);

                    renderState = RenderState.Voxelize;

                    updateVoxelsAfterXDoUpdate = false;
                    updateVoxelsAfterXPrevX = attachedCamera.transform.position.x;
                    updateVoxelsAfterXPrevY = attachedCamera.transform.position.y;
                    updateVoxelsAfterXPrevZ = attachedCamera.transform.position.z;
                }

                RenderTexture.active = previousActive;

            }
            //voxelCamera.rect = new Rect(0.0f, 0f, 1.0f, 1.0f);

            Matrix4x4 giToVoxelProjection = voxelCamera.projectionMatrix * voxelCamera.worldToCameraMatrix * shadowCam.cameraToWorldMatrix;
            Shader.SetGlobalMatrix("GIToVoxelProjection", giToVoxelProjection);

            //Fix stereo rendering matrix
            if (attachedCamera.stereoEnabled)
            {
                // Left and Right Eye inverse View Matrices
                Matrix4x4 leftToWorld = attachedCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
                Matrix4x4 rightToWorld = attachedCamera.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
                material.SetMatrix("_LeftEyeToWorld", leftToWorld);
                material.SetMatrix("_RightEyeToWorld", rightToWorld);

                Matrix4x4 leftEye = attachedCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                Matrix4x4 rightEye = attachedCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

                // Compensate for RenderTexture...
                leftEye = GL.GetGPUProjectionMatrix(leftEye, true).inverse;
                rightEye = GL.GetGPUProjectionMatrix(rightEye, true).inverse;
                // Negate [1,1] to reflect Unity's CBuffer state
                leftEye[1, 1] *= -1;
                rightEye[1, 1] *= -1;

                material.SetMatrix("_LeftEyeProjection", leftEye);
                material.SetMatrix("_RightEyeProjection", rightEye);
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

            Shader.SetGlobalFloat("SEGIVoxelScaleFactor", voxelScaleFactor);

            material.SetMatrix("CameraToWorld", context.camera.cameraToWorldMatrix);
            material.SetMatrix("WorldToCamera", context.camera.worldToCameraMatrix);
            material.SetMatrix("ProjectionMatrixInverse", context.camera.projectionMatrix.inverse);
            material.SetMatrix("ProjectionMatrix", context.camera.projectionMatrix);
            material.SetInt("FrameSwitch", frameSwitch);
            Shader.SetGlobalInt("SEGIFrameSwitch", frameSwitch);
            material.SetVector("CameraPosition", context.camera.transform.position);
            material.SetFloat("DeltaTime", Time.deltaTime);

            material.SetInt("StochasticSampling", settings.stochasticSampling.value ? 1 : 0);
            material.SetInt("TraceDirections", settings.cones.value);
            material.SetInt("TraceSteps", settings.coneTraceSteps.value);
            material.SetFloat("TraceLength", settings.coneLength.value);
            material.SetFloat("ConeSize", settings.coneWidth.value);
            material.SetFloat("OcclusionStrength", settings.occlusionStrength.value);
            material.SetFloat("OcclusionPower", settings.occlusionPower.value);
            material.SetFloat("ConeTraceBias", settings.coneTraceBias.value);
            material.SetFloat("GIGain", settings.giGain.value);
            material.SetFloat("NearLightGain", settings.nearLightGain.value);
            material.SetFloat("NearOcclusionStrength", settings.nearOcclusionStrength.value);
            material.SetInt("DoReflections", settings.doReflections.value ? 1 : 0);
            material.SetInt("GIResolution", settings.GIResolution.value);
            material.SetInt("ReflectionSteps", settings.reflectionSteps.value);
            material.SetFloat("ReflectionOcclusionPower", settings.reflectionOcclusionPower.value);
            material.SetFloat("SkyReflectionIntensity", settings.skyReflectionIntensity.value);
            material.SetFloat("FarOcclusionStrength", settings.farOcclusionStrength.value);
            material.SetFloat("FarthestOcclusionStrength", settings.farthestOcclusionStrength.value);
            material.SetTexture("NoiseTexture", blueNoise[frameSwitch % 64]);
            material.SetFloat("BlendWeight", settings.temporalBlendWeight.value);
            material.SetInt("useReflectionProbes", settings.useReflectionProbes.value ? 1 : 0);
            material.SetFloat("reflectionProbeIntensity", settings.reflectionProbeIntensity.value);
            material.SetFloat("reflectionProbeAttribution", settings.reflectionProbeAttribution.value);
            material.SetInt("StereoEnabled", context.stereoActive ? 1 : 0);

            //Blit once to downsample if required
            context.command.Blit(context.source, RT_gi1);

            if (context.camera.renderingPath == RenderingPath.Forward)
            {
                context.command.SetGlobalInt("ForwardPath", 1);
                context.command.SetGlobalTexture("_Albedo", context.source);
            }
            else context.command.SetGlobalInt("ForwardPath", 0);

            //If Visualize Voxels is enabled, just render the voxel visualization shader pass and return
            if (settings.visualizeVoxels.value)
            {
                context.command.Blit(context.source, context.destination, material, Pass.VisualizeVoxels);
                return;
            }

            //Set the previous GI result and camera depth textures to access them in the shader
            context.command.SetGlobalTexture("PreviousGITexture", previousResult);
            context.command.SetGlobalTexture("PreviousDepth", previousDepth);

            //Render diffuse GI tracing result
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

                //Perform a bilateral blur to be applied in newly revealed areas that are still noisy due to not having previous data blended with it

                //material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                //SEGIBuffer.Blit(RT_gi3, RT_blur0, material, Pass.BilateralBlur);
                //material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                //SEGIBuffer.Blit(RT_blur0, RT_gi3, material, Pass.BilateralBlur);

                context.command.Blit(RT_gi3, RT_blur0, Gaussian_Material);
                context.command.Blit(RT_blur0, RT_gi3, Gaussian_Material);
                context.command.SetGlobalTexture("BlurredGI", RT_blur0);

                //Perform temporal reprojection and blending
                if (settings.temporalBlendWeight.value < 1.0f)
                {
                    context.command.Blit(RT_gi3, RT_gi4, material, Pass.TemporalBlend);
                    //SEGIBuffer.Blit(RT_gi4, RT_gi3, material, Pass.TemporalBlend);
                    context.command.Blit(RT_gi4, previousResult);
                    context.command.Blit(RT_gi1, previousDepth, material, Pass.GetCameraDepthTexture);
                }

                if (settings.GIResolution.value >= 3)
                {
                    //material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                    //SEGIBuffer.Blit(RT_gi3, RT_gi4, material, Pass.BilateralBlur);
                    //material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    //SEGIBuffer.Blit(RT_gi4, RT_gi3, material, Pass.BilateralBlur);

                    context.command.Blit(RT_gi3, RT_gi4, Gaussian_Material);
                    context.command.Blit(RT_gi4, RT_gi3, Gaussian_Material);
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

                    context.command.Blit(RT_gi2, RT_blur1, Gaussian_Material);
                    context.command.Blit(RT_blur1, RT_blur0, Gaussian_Material);
                    context.command.SetGlobalTexture("BlurredGI", RT_blur0);

                    //Perform temporal reprojection and blending
                    context.command.Blit(RT_gi2, RT_gi1, material, Pass.TemporalBlend);
                    context.command.Blit(RT_gi1, previousResult);
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


            //Advance the frame counter
            frameSwitch = (frameSwitch + 1) % (64);



            material.SetMatrix("ProjectionPrev", context.camera.projectionMatrix);
            material.SetMatrix("ProjectionPrevInverse", context.camera.projectionMatrix.inverse);
            material.SetMatrix("WorldToCameraPrev", context.camera.worldToCameraMatrix);
            material.SetMatrix("CameraToWorldPrev", context.camera.cameraToWorldMatrix);
            material.SetVector("CameraPositionPrev", context.camera.transform.position);

            frameSwitch = (frameSwitch + 1) % (128);
            //material.ref
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

            //Setup shaders and materials
            sunDepthShader = Shader.Find("Hidden/SEGIRenderSunDepth_C");
            clearCompute = Resources.Load("SEGIClear_C") as ComputeShader;
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
            reflectionProbe = reflectionProbeGameObject.GetComponent<ReflectionProbe>();
            if (!reflectionProbe)
            {
                reflectionProbe = reflectionProbeGameObject.AddComponent<ReflectionProbe>();

            }
            /*if (!reflectionProbe)
            {
                reflectionProbe = reflectionProbeGameObject.AddComponent<ReflectionProbe>();
            }*/
            reflectionProbeGameObject.hideFlags = HideFlags.HideAndDontSave;
            reflectionProbeGameObject.transform.parent = attachedCamera.transform;
            reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            reflectionProbe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            reflectionProbe.cullingMask = settings.reflectionProbeLayerMask.GetValue<LayerMask>();
            reflectionProbe.size = new Vector3(settings.updateVoxelsAfterXInterval.value * 2.5f, settings.updateVoxelsAfterXInterval.value * 2.5f, settings.updateVoxelsAfterXInterval.value * 2.5f);
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.shadowDistance = settings.voxelSpaceSize.value;
            reflectionProbe.farClipPlane = settings.voxelSpaceSize.value;
            reflectionProbe.backgroundColor = Color.black;
            reflectionProbe.boxProjection = true;

            reflectionProbe.resolution = 128;
            reflectionProbe.importance = 0;
            reflectionProbe.enabled = true;
            reflectionProbe.hdr = false;


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

            CreateVolumeTextures();

            ResizeRenderTextures();

            initChecker = true;

        }

        void ResizeAllTextures()
        {
            CreateVolumeTextures();
            ResizeRenderTextures();
            ResizeDummyTexture();
        }

        void CreateVolumeTextures()
        {
            {
                if (volumeTextures != null)
                {
                    for (int i = 0; i < numMipLevels; i++)
                    {
                        if (volumeTextures[i] != null)
                        {
                            volumeTextures[i].Release();
                        }
                    }
                }

                volumeTextures = new RenderTexture[numMipLevels];

                for (int i = 0; i < numMipLevels; i++)
                {
                    int resolution = (int)settings.voxelResolution.value / Mathf.RoundToInt(Mathf.Pow((float)2, (float)i));
                    volumeTextures[i] = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
#if UNITY_5_4_OR_NEWER
                    volumeTextures[i].dimension = TextureDimension.Tex3D;
#else
            			volumeTextures[i].isVolume = true;
#endif
                    volumeTextures[i].volumeDepth = resolution;
                    volumeTextures[i].enableRandomWrite = true;
                    volumeTextures[i].filterMode = FilterMode.Bilinear;
#if UNITY_5_4_OR_NEWER
                    volumeTextures[i].autoGenerateMips = false;
#else
	            		volumeTextures[i].generateMips = false;
#endif
                    volumeTextures[i].useMipMap = false;
                    volumeTextures[i].Create();
                    volumeTextures[i].hideFlags = HideFlags.HideAndDontSave;
                }

                if (volumeTextureB) volumeTextureB.Release();
                volumeTextureB = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
#if UNITY_5_4_OR_NEWER
                volumeTextureB.dimension = TextureDimension.Tex3D;
#else
	            	volumeTextureB.isVolume = true;
#endif
                volumeTextureB.volumeDepth = (int)settings.voxelResolution.value;
                volumeTextureB.enableRandomWrite = true;
                volumeTextureB.filterMode = FilterMode.Bilinear;
#if UNITY_5_4_OR_NEWER
                volumeTextureB.autoGenerateMips = false;
#else
	        	volumeTextureB.generateMips = false;
#endif
                volumeTextureB.useMipMap = false;
                volumeTextureB.Create();
                volumeTextureB.hideFlags = HideFlags.HideAndDontSave;

                if (secondaryIrradianceVolume) secondaryIrradianceVolume.Release();
                secondaryIrradianceVolume = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
#if UNITY_5_4_OR_NEWER
                secondaryIrradianceVolume.dimension = TextureDimension.Tex3D;
#else
	        	    secondaryIrradianceVolume.isVolume = true;
#endif
                secondaryIrradianceVolume.volumeDepth = (int)settings.voxelResolution.value;
                secondaryIrradianceVolume.enableRandomWrite = true;
                secondaryIrradianceVolume.filterMode = FilterMode.Point;
#if UNITY_5_4_OR_NEWER
                secondaryIrradianceVolume.autoGenerateMips = false;
#else
	        	    secondaryIrradianceVolume.generateMips = false;
#endif
                secondaryIrradianceVolume.useMipMap = false;
                secondaryIrradianceVolume.antiAliasing = 1;
                secondaryIrradianceVolume.Create();
                secondaryIrradianceVolume.hideFlags = HideFlags.HideAndDontSave;



                if (integerVolume) integerVolume.Release();
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
            }
        }

        void ResizeDummyTexture()
        {
            if (dummyVoxelTextureAAScaled) dummyVoxelTextureAAScaled.Release();
            dummyVoxelTextureAAScaled = new RenderTexture(DummyVoxelResolution, DummyVoxelResolution, 0, RenderTextureFormat.R8);
            dummyVoxelTextureAAScaled.Create();
            dummyVoxelTextureAAScaled.hideFlags = HideFlags.HideAndDontSave;

            if (dummyVoxelTextureFixed) dummyVoxelTextureFixed.Release();
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

        Matrix4x4 TransformViewMatrix(Matrix4x4 mat)
        {
#if UNITY_5_5_OR_NEWER
            if (SystemInfo.usesReversedZBuffer)
            {
                mat[2, 0] = -mat[2, 0];
                mat[2, 1] = -mat[2, 1];
                mat[2, 2] = -mat[2, 2];
                mat[2, 3] = -mat[2, 3];
                // mat[3, 2] += 0.0f;
            }
#endif
            return mat;
        }

        public void ResizeRenderTextures()
        {

            //StopCoroutine(updateVoxels());

            if (SEGIRenderWidth == 0) SEGIRenderWidth = attachedCamera.scaledPixelWidth;
            if (SEGIRenderHeight == 0) SEGIRenderHeight = attachedCamera.scaledPixelHeight;

            RenderTextureDescriptor RT_Disc0 = new RenderTextureDescriptor(SEGIRenderWidth, SEGIRenderHeight, renderTextureFormat, 32);
            RenderTextureDescriptor RT_Disc1 = new RenderTextureDescriptor(SEGIRenderWidth / (int)settings.GIResolution.value, SEGIRenderHeight / (int)settings.GIResolution.value, renderTextureFormat, 32);

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

            if (previousResult) previousResult.Release();
            previousResult = new RenderTexture(RT_Disc0);
            previousResult.wrapMode = TextureWrapMode.Clamp;
            previousResult.filterMode = FilterMode.Bilinear;
            previousResult.useMipMap = true;
            previousResult.autoGenerateMips = true;
            previousResult.Create();
            previousResult.hideFlags = HideFlags.HideAndDontSave;

            if (previousDepth)
            {
                //previousDepth.DiscardContents();
                previousDepth.Release();
                //DestroyImmediate(previousDepth);
            }
            previousDepth = new RenderTexture(RT_Disc0);
            previousDepth.wrapMode = TextureWrapMode.Clamp;
            previousDepth.filterMode = FilterMode.Bilinear;
            previousDepth.Create();
            previousDepth.hideFlags = HideFlags.HideAndDontSave;

            if (RT_FXAART) RT_FXAART.Release();
            RT_FXAART = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) RT_FXAART.vrUsage = VRTextureUsage.TwoEyes;
            RT_FXAART.Create();

            if (RT_gi1) RT_gi1.Release();
            RT_gi1 = new RenderTexture(RT_Disc1);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi1.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi1.Create();

            if (RT_gi2) RT_gi2.Release();
            RT_gi2 = new RenderTexture(RT_Disc1);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi2.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi2.Create();

            if (RT_reflections) RT_reflections.Release();
            RT_reflections = new RenderTexture(RT_Disc1);
            if (UnityEngine.XR.XRSettings.enabled) RT_reflections.vrUsage = VRTextureUsage.TwoEyes;
            RT_reflections.Create();

            if (RT_gi3) RT_gi3.Release();
            RT_gi3 = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi3.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi3.Create();

            if (RT_gi4) RT_gi4.Release();
            RT_gi4 = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) RT_gi4.vrUsage = VRTextureUsage.TwoEyes;
            RT_gi4.Create();

            if (RT_blur0) RT_blur0.Release();
            RT_blur0 = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) RT_blur0.vrUsage = VRTextureUsage.TwoEyes;
            RT_blur0.Create();

            if (RT_blur1) RT_blur1.Release();
            RT_blur1 = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) RT_blur1.vrUsage = VRTextureUsage.TwoEyes;
            RT_blur1.Create();

            if (RT_FXAARTluminance) RT_FXAARTluminance.Release();
            RT_FXAARTluminance = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) RT_FXAARTluminance.vrUsage = VRTextureUsage.TwoEyes;
            RT_FXAARTluminance.Create();

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
            sunDepthTexture = new RenderTexture(sunShadowResolution, sunShadowResolution, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Default);
            if (UnityEngine.XR.XRSettings.enabled) sunDepthTexture.vrUsage = VRTextureUsage.TwoEyes;
            sunDepthTexture.wrapMode = TextureWrapMode.Clamp;
            sunDepthTexture.filterMode = FilterMode.Point;
            sunDepthTexture.Create();
            sunDepthTexture.hideFlags = HideFlags.HideAndDontSave;
        }



        public override void Release()
        {
            CleanupTexture(ref sunDepthTexture);
            CleanupTexture(ref previousGIResult);
            //CleanupTexture(ref previousCameraDepth);
            CleanupTexture(ref integerVolume);
            for (int i = 0; i < volumeTextures.Length; i++)
            {
                CleanupTexture(ref volumeTextures[i]);
            }
            CleanupTexture(ref secondaryIrradianceVolume);
            CleanupTexture(ref volumeTextureB);
            CleanupTexture(ref dummyVoxelTextureAAScaled);
            CleanupTexture(ref dummyVoxelTextureFixed);

            if (RT_FXAART) RT_FXAART.Release();
            if (RT_gi1) RT_gi1.Release();
            if (RT_gi2) RT_gi2.Release();
            if (RT_reflections) RT_reflections.Release();
            if (RT_gi3) RT_gi3.Release();
            if (RT_gi4) RT_gi4.Release();
            if (RT_blur0) RT_blur0.Release();
            if (RT_blur1) RT_blur1.Release();
            if (RT_FXAARTluminance) RT_FXAARTluminance.Release();
        }

        void CleanupTexture(ref RenderTexture texture)
        {
            if (texture)
            {
                texture.Release();
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

   