using UnityEngine;
using System.Collections;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Rendering.PostProcessing;

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
    [PostProcess(typeof(SEGIRenderer), PostProcessEvent.BeforeTransparent, "NKLI/SEGI")]
    public sealed class SEGICascaded : PostProcessEffectSettings
    {


        public VoxelResolution voxelResolution = new VoxelResolution { value = VoxelResolutionEnum.High };
        public FloatParameter voxelSpaceSize = new FloatParameter { value = 50.0f };
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
        public SEGISun Sun = new SEGISun { value = null };

        public SEGILayerMask giCullingMask = new SEGILayerMask { value = 2147483647 };
        public SEGILayerMask reflectionProbeLayerMask = new SEGILayerMask { value = 2147483647 };

        public SEGITransform followTransform = new SEGITransform { value = null };

        [Range(0.0f, 16.0f)]
        public FloatParameter softSunlight = new FloatParameter { value = 0.0f };
        public ColorParameter skyColor = new ColorParameter { value = Color.black };
        public BoolParameter MatchAmbiantColor = new BoolParameter { value = false };
        [Range(0.0f, 8.0f)]
        public FloatParameter skyIntensity = new FloatParameter { value = 1.0f };
        public BoolParameter sphericalSkylight = new BoolParameter { value = false };
    }

    [ExecuteInEditMode]
    [ImageEffectAllowedInSceneView]
    public sealed class SEGIRenderer : PostProcessEffectRenderer<SEGICascaded>
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

        struct SEGICMDBufferRT
        {
            // 0    - FXAART
            // 1    - gi1
            // 2    - gi2
            // 3    - reflections
            // 4    - gi3
            // 5    - gi4
            // 6    - blur0
            // 7    - blur1
            // 8    - FXAARTluminance
            public static int FXAART = 0;
            public static int gi1 = 1;
            public static int gi2 = 2;
            public static int reflections = 3;
            public static int gi3 = 4;
            public static int gi4 = 5;
            public static int blur0 = 6;
            public static int blur1 = 7;
            public static int FXAARTluminance = 8;
        }
        public RenderTexture RT_FXAART;
        public RenderTexture RT_gi1;
        public RenderTexture RT_gi2;
        public RenderTexture RT_reflections;
        public RenderTexture RT_gi3;
        public RenderTexture RT_gi4;
        public RenderTexture RT_blur0;
        public RenderTexture RT_blur1;
        public RenderTexture RT_FXAARTluminance;

        //public RenderTexture SEGIRenderSource;
        //public RenderTexture SEGIRenderDestination;
        public int SEGIRenderWidth;
        public int SEGIRenderHeight;


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

        /// <summary>
        /// Estimates the VRAM usage of all the render textures used to render GI.
        /// </summary>
        public float vramUsage
        {
            get
            {
                long v = 0;

                if (sunDepthTexture != null)
                    v += sunDepthTexture.width * sunDepthTexture.height * 16;

                if (previousResult != null)
                    v += previousResult.width * previousResult.height * 16 * 4;

                if (previousDepth != null)
                    v += previousDepth.width * previousDepth.height * 32;

                if (intTex1 != null)
                    v += intTex1.width * intTex1.height * intTex1.volumeDepth * 32;

                if (volumeTextures != null)
                {
                    for (int i = 0; i < volumeTextures.Length; i++)
                    {
                        if (volumeTextures[i] != null)
                            v += volumeTextures[i].width * volumeTextures[i].height * volumeTextures[i].volumeDepth * 16 * 4;
                    }
                }

                if (volumeTexture1 != null)
                    v += volumeTexture1.width * volumeTexture1.height * volumeTexture1.volumeDepth * 16 * 4;

                if (volumeTextureB != null)
                    v += volumeTextureB.width * volumeTextureB.height * volumeTextureB.volumeDepth * 16 * 4;

                if (dummyVoxelTexture != null)
                    v += dummyVoxelTexture.width * dummyVoxelTexture.height * 8;

                if (dummyVoxelTexture2 != null)
                    v += dummyVoxelTexture2.width * dummyVoxelTexture2.height * 8;

                float vram = (v / 8388608.0f);

                return vram;
            }
        }

        public FilterMode filterMode = FilterMode.Point;
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGBHalf;



        //public bool gaussianMipFilter = false;

        int mipFilterKernel
        {
            get
            {
                return settings.gaussianMipFilter ? 1 : 0;
            }
        }

        //public bool voxelAA = false;

        int dummyvoxelResolution
        {
            get
            {
                return (int)settings.voxelResolution.value * (settings.voxelAA ? 2 : 1);
            }
        }

        int sunShadowResolution = 256;
        int prevSunShadowResolution;





        public Shader sunDepthShader;

        float shadowSpaceDepthRatio = 10.0f;

        int frameSwitch = 0;

        public RenderTexture sunDepthTexture;
        public RenderTexture previousResult;
        public RenderTexture previousDepth;
        public RenderTexture intTex1;
        public RenderTexture[] volumeTextures;
        public RenderTexture volumeTexture1;
        public RenderTexture volumeTextureB;

        public RenderTexture activeVolume;
        public RenderTexture previousActiveVolume;

        public RenderTexture dummyVoxelTexture;
        public RenderTexture dummyVoxelTexture2;

        public bool notReadyToRender = false;

        public Shader voxelizationShader;
        public Shader voxelTracingShader;

        public ComputeShader clearCompute;
        public ComputeShader transferInts;
        public ComputeShader mipFilter;

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


        public int giRenderRes
        {
            get
            {
                return settings.GIResolution;
            }
        }

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
        //public int updateVoxelsAfterXInterval = 1;
        private double updateVoxelsAfterXPrevX = 9223372036854775807;
        private double updateVoxelsAfterXPrevY = 9223372036854775807;
        private double updateVoxelsAfterXPrevZ = 9223372036854775807;




        public override void Render(PostProcessRenderContext context)
        {
            // Update

            if (SEGIRenderWidth != context.width || SEGIRenderHeight != context.height)
            {
                SEGIRenderWidth = context.width;
                SEGIRenderHeight = context.height;
                InitCheck();
                ResizeRenderTextures();
            }

            if (settings.Sun.GetValue<Light>() == null)
            {
                Debug.Log("<SEGI> Light property must be connected to a prefab!");
                return;
            }

            if (!initChecker)
            {
                return;
            }

            if (notReadyToRender)
                return;

            if (previousResult == null)
            {
                ResizeRenderTextures();
            }

            if (previousResult.width != attachedCamera.pixelWidth || previousResult.height != attachedCamera.pixelHeight)
            {
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

            if (dummyVoxelTexture.width != dummyvoxelResolution)
            {
                ResizeDummyTexture();
            }

            if (attachedCamera != context.camera) attachedCamera = context.camera;

            if (!shadowCam)
            {
                Debug.Log("<SEGI> Shadow Camera not found!");
                return;
            }


            // OnPreRender

            //Force reinitialization to make sure that everything is working properly if one of the cameras was unexpectedly destroyed
            //if (!voxelCamera || !false;

            InitCheck();

            if (!settings.updateGI)
            {
                return;
            }

            if (attachedCamera.renderingPath == RenderingPath.Forward && reflectionProbe.enabled)
            {
                reflectionProbe.enabled = true;
                reflectionProbe.intensity = settings.reflectionProbeIntensity;
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

            //calculationSEGIObject = this;
            //Debug.Log(Camera.current.name + "," + Camera.current.stereoActiveEye + ", " + calculationSEGIObject.name + ", " + Time.frameCount + ", " + Time.renderedFrameCount);
            //Cache the previous active render texture to avoid issues with other Unity rendering going on
            RenderTexture previousActive = RenderTexture.active;
            Shader.SetGlobalInt("SEGIVoxelAA", settings.voxelAA ? 1 : 0);

            //Temporarily disable rendering of shadows on the directional light during voxelization pass. Cache the result to set it back to what it was after voxelization is done
            LightShadows prevSunShadowSetting = LightShadows.None;
            if (settings.Sun.GetValue<Light>() != null)
            {
                prevSunShadowSetting = settings.Sun.GetValue<Light>().shadows;
                settings.Sun.GetValue<Light>().shadows = LightShadows.None;
            }

            context.command.SetGlobalMatrix("WorldToGI", shadowCam.worldToCameraMatrix);
            Shader.SetGlobalMatrix("GIToWorld", shadowCam.cameraToWorldMatrix);
            Shader.SetGlobalMatrix("GIProjection", shadowCam.projectionMatrix);
            Shader.SetGlobalMatrix("GIProjectionInverse", shadowCam.projectionMatrix.inverse);
            Shader.SetGlobalMatrix("WorldToCamera", attachedCamera.worldToCameraMatrix);
            Shader.SetGlobalFloat("GIDepthRatio", shadowSpaceDepthRatio);

            Shader.SetGlobalColor("GISunColor", settings.Sun.GetValue<Light>() == null ? Color.black : new Color(Mathf.Pow(settings.Sun.GetValue<Light>().color.r, 2.2f), Mathf.Pow(settings.Sun.GetValue<Light>().color.g, 2.2f), Mathf.Pow(settings.Sun.GetValue<Light>().color.b, 2.2f), Mathf.Pow(settings.Sun.GetValue<Light>().intensity, 2.2f)));
            Shader.SetGlobalColor("SEGISkyColor", new Color(Mathf.Pow(settings.skyColor.value.r * settings.skyIntensity.value * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.g * settings.skyIntensity.value * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.b * settings.skyIntensity.value * 0.5f, 2.2f), Mathf.Pow(settings.skyColor.value.a, 2.2f)));
            Shader.SetGlobalFloat("GIGain", settings.giGain);

            Shader.SetGlobalInt("SEGIvoxelResolution", (int)settings.voxelResolution.value);

            Shader.SetGlobalMatrix("SEGIVoxelViewFront", TransformViewMatrix(voxelCamera.transform.worldToLocalMatrix));
            Shader.SetGlobalMatrix("SEGIVoxelViewLeft", TransformViewMatrix(leftViewPoint.transform.worldToLocalMatrix));
            Shader.SetGlobalMatrix("SEGIVoxelViewTop", TransformViewMatrix(topViewPoint.transform.worldToLocalMatrix));
            Shader.SetGlobalMatrix("SEGIWorldToVoxel", voxelCamera.worldToCameraMatrix);
            Shader.SetGlobalMatrix("SEGIVoxelProjection", voxelCamera.projectionMatrix);
            Shader.SetGlobalMatrix("SEGIVoxelProjectionInverse", voxelCamera.projectionMatrix.inverse);

            Shader.SetGlobalFloat("SEGISecondaryBounceGain", settings.infiniteBounces ? settings.secondaryBounceGain : 0.0f);
            Shader.SetGlobalFloat("SEGISoftSunlight", settings.softSunlight);
            Shader.SetGlobalInt("SEGISphericalSkylight", settings.sphericalSkylight ? 1 : 0);
            Shader.SetGlobalInt("SEGIInnerOcclusionLayers", settings.innerOcclusionLayers);

            Matrix4x4 voxelToGIProjection = (shadowCam.projectionMatrix) * (shadowCam.worldToCameraMatrix) * (voxelCamera.cameraToWorldMatrix);
            Shader.SetGlobalMatrix("SEGIVoxelToGIProjection", voxelToGIProjection);
            Shader.SetGlobalVector("SEGISunlightVector", settings.Sun.GetValue<Light>() ? Vector3.Normalize(settings.Sun.GetValue<Light>().transform.forward) : Vector3.up);


            if (attachedCamera.transform.position.x - updateVoxelsAfterXPrevX >= settings.updateVoxelsAfterXInterval) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevX - attachedCamera.transform.position.x >= settings.updateVoxelsAfterXInterval) updateVoxelsAfterXDoUpdate = true;

            if (attachedCamera.transform.position.y - updateVoxelsAfterXPrevY >= settings.updateVoxelsAfterXInterval) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevY - attachedCamera.transform.position.y >= settings.updateVoxelsAfterXInterval) updateVoxelsAfterXDoUpdate = true;

            if (attachedCamera.transform.position.z - updateVoxelsAfterXPrevZ >= settings.updateVoxelsAfterXInterval) updateVoxelsAfterXDoUpdate = true;
            if (updateVoxelsAfterXPrevZ - attachedCamera.transform.position.z >= settings.updateVoxelsAfterXInterval) updateVoxelsAfterXDoUpdate = true;


            if (renderState == RenderState.Voxelize && updateVoxelsAfterXDoUpdate == true)
            {
                //voxelCamera.rect = new Rect(0f, 0f, 0.5f, 0.5f);

                activeVolume = voxelFlipFlop == 0 ? volumeTextures[0] : volumeTextureB;
                previousActiveVolume = voxelFlipFlop == 0 ? volumeTextureB : volumeTextures[0];

                float voxelTexel = (1.0f * settings.voxelSpaceSize) / (int)settings.voxelResolution.value * 0.5f;

                float interval = settings.voxelSpaceSize / 8.0f;
                Vector3 origin;
                if (settings.followTransform.GetValue<Transform>())
                {
                    origin = settings.followTransform.GetValue<Transform>().position;
                }
                else
                {
                    origin = attachedCamera.transform.position + attachedCamera.transform.forward * settings.voxelSpaceSize / 4.0f;
                }
                voxelSpaceOrigin = new Vector3(Mathf.Round(origin.x / interval) * interval, Mathf.Round(origin.y / interval) * interval, Mathf.Round(origin.z / interval) * interval) + new Vector3(1.0f, 1.0f, 1.0f) * ((float)voxelFlipFlop * 2.0f - 1.0f) * voxelTexel * 0.0f;

                voxelSpaceOriginDelta = voxelSpaceOrigin - previousVoxelSpaceOrigin;
                Shader.SetGlobalVector("SEGIVoxelSpaceOriginDelta", voxelSpaceOriginDelta / settings.voxelSpaceSize);

                previousVoxelSpaceOrigin = voxelSpaceOrigin;

                if (settings.Sun != null)
                {
                    shadowCam.cullingMask = settings.giCullingMask.GetValue<LayerMask>();

                    Vector3 shadowCamPosition = voxelSpaceOrigin + Vector3.Normalize(-settings.Sun.GetValue<Light>().transform.forward) * shadowSpaceSize * 0.5f * shadowSpaceDepthRatio;

                    shadowCamTransform.position = shadowCamPosition;
                    shadowCamTransform.LookAt(voxelSpaceOrigin, Vector3.up);

                    shadowCam.renderingPath = RenderingPath.Forward;
                    shadowCam.depthTextureMode |= DepthTextureMode.None;

                    shadowCam.orthographicSize = shadowSpaceSize;
                    shadowCam.farClipPlane = shadowSpaceSize * 2.0f * shadowSpaceDepthRatio;

                    Graphics.SetRenderTarget(sunDepthTexture);
                    shadowCam.SetTargetBuffers(sunDepthTexture.colorBuffer, sunDepthTexture.depthBuffer);
                    shadowCam.forceIntoRenderTexture = true;
                    shadowCam.RenderWithShader(sunDepthShader, "");

                    Shader.SetGlobalTexture("SEGISunDepth", sunDepthTexture);
                }


                voxelCamera.enabled = false;
                voxelCamera.orthographic = true;
                voxelCamera.orthographicSize = settings.voxelSpaceSize * 0.5f;
                voxelCamera.nearClipPlane = 0.0f;
                voxelCamera.farClipPlane = settings.voxelSpaceSize;
                voxelCamera.depth = -2;
                //voxelCamera.stereoTargetEye = StereoTargetEyeMask.None;
                voxelCamera.renderingPath = RenderingPath.Forward;
                voxelCamera.clearFlags = CameraClearFlags.Color;
                voxelCamera.backgroundColor = Color.black;
                voxelCamera.cullingMask = settings.giCullingMask.GetValue<LayerMask>();




                voxelFlipFlop += 1;
                voxelFlipFlop = voxelFlipFlop % 2;

                voxelCameraGO.transform.position = voxelSpaceOrigin - Vector3.forward * settings.voxelSpaceSize * 0.5f;
                voxelCameraGO.transform.rotation = rotationFront;

                leftViewPoint.transform.position = voxelSpaceOrigin + Vector3.left * settings.voxelSpaceSize * 0.5f;
                leftViewPoint.transform.rotation = rotationLeft;
                topViewPoint.transform.position = voxelSpaceOrigin + Vector3.up * settings.voxelSpaceSize * 0.5f;
                topViewPoint.transform.rotation = rotationTop;

                clearCompute.SetTexture(0, "RG0", intTex1);
                clearCompute.SetInt("Res", (int)settings.voxelResolution.value);
                clearCompute.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);


                Graphics.SetRandomWriteTarget(1, intTex1);
                voxelCamera.targetTexture = dummyVoxelTexture;
                voxelCamera.RenderWithShader(voxelizationShader, "");
                Graphics.ClearRandomWriteTargets();

                transferInts.SetTexture(0, "Result", activeVolume);
                transferInts.SetTexture(0, "PrevResult", previousActiveVolume);
                transferInts.SetTexture(0, "RG0", intTex1);
                transferInts.SetInt("VoxelAA", settings.voxelAA ? 1 : 0);
                transferInts.SetInt("Resolution", (int)settings.voxelResolution.value);
                transferInts.SetVector("VoxelOriginDelta", (voxelSpaceOriginDelta / settings.voxelSpaceSize) * (int)settings.voxelResolution.value);
                transferInts.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                Shader.SetGlobalTexture("SEGIVolumeLevel0", activeVolume);

                for (int i = 0; i < numMipLevels - 1; i++)
                {
                    RenderTexture source = volumeTextures[i];

                    if (i == 0)
                    {
                        source = activeVolume;
                    }

                    int destinationRes = (int)settings.voxelResolution.value / Mathf.RoundToInt(Mathf.Pow((float)2, (float)i + 1.0f));
                    mipFilter.SetInt("destinationRes", destinationRes);
                    mipFilter.SetTexture(mipFilterKernel, "Source", source);
                    mipFilter.SetTexture(mipFilterKernel, "Destination", volumeTextures[i + 1]);
                    mipFilter.Dispatch(mipFilterKernel, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);
                    Shader.SetGlobalTexture("SEGIVolumeLevel" + (i + 1).ToString(), volumeTextures[i + 1]);
                }

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

                clearCompute.SetTexture(0, "RG0", intTex1);
                clearCompute.Dispatch(0, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                Shader.SetGlobalInt("SEGISecondaryCones", settings.secondaryCones);
                Shader.SetGlobalFloat("SEGISecondaryOcclusionStrength", settings.secondaryOcclusionStrength);

                Graphics.SetRandomWriteTarget(1, intTex1);
                voxelCamera.targetTexture = dummyVoxelTexture2;
                voxelCamera.RenderWithShader(voxelTracingShader, "");
                Graphics.ClearRandomWriteTargets();

                transferInts.SetTexture(1, "Result", volumeTexture1);
                transferInts.SetTexture(1, "RG0", intTex1);
                transferInts.SetInt("Resolution", (int)settings.voxelResolution.value);
                transferInts.Dispatch(1, (int)settings.voxelResolution.value / 16, (int)settings.voxelResolution.value / 16, 1);

                Shader.SetGlobalTexture("SEGIVolumeTexture1", volumeTexture1);

                renderState = RenderState.Voxelize;
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

            RenderTexture.active = previousActive;

            //Set the sun's shadow setting back to what it was before voxelization
            if (settings.Sun.GetValue<Light>() != null)
            {
                settings.Sun.GetValue<Light>().shadows = prevSunShadowSetting;
            }



            // OnRenderImage #################################################################

            if (notReadyToRender)
            {
                context.command.Blit(context.source, context.destination);
                return;
            }

            if (settings.visualizeSunDepthTexture && sunDepthTexture != null && sunDepthTexture != null)
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

            material.SetInt("StochasticSampling", settings.stochasticSampling ? 1 : 0);
            material.SetInt("TraceDirections", settings.cones);
            material.SetInt("TraceSteps", settings.coneTraceSteps);
            material.SetFloat("TraceLength", settings.coneLength);
            material.SetFloat("ConeSize", settings.coneWidth);
            material.SetFloat("OcclusionStrength", settings.occlusionStrength);
            material.SetFloat("OcclusionPower", settings.occlusionPower);
            material.SetFloat("ConeTraceBias", settings.coneTraceBias);
            material.SetFloat("GIGain", settings.giGain);
            material.SetFloat("NearLightGain", settings.nearLightGain);
            material.SetFloat("NearOcclusionStrength", settings.nearOcclusionStrength);
            material.SetInt("DoReflections", settings.doReflections ? 1 : 0);
            material.SetInt("GIResolution", settings.GIResolution);
            material.SetInt("ReflectionSteps", settings.reflectionSteps);
            material.SetFloat("ReflectionOcclusionPower", settings.reflectionOcclusionPower);
            material.SetFloat("SkyReflectionIntensity", settings.skyReflectionIntensity);
            material.SetFloat("FarOcclusionStrength", settings.farOcclusionStrength);
            material.SetFloat("FarthestOcclusionStrength", settings.farthestOcclusionStrength);
            material.SetTexture("NoiseTexture", blueNoise[frameSwitch % 64]);
            material.SetFloat("BlendWeight", settings.temporalBlendWeight);
            material.SetInt("useReflectionProbes", settings.useReflectionProbes ? 1 : 0);
            material.SetFloat("reflectionProbeIntensity", settings.reflectionProbeIntensity);
            material.SetFloat("reflectionProbeAttribution", settings.reflectionProbeAttribution);
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
            if (settings.visualizeVoxels)
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
            if (settings.doReflections)
            {
                context.command.Blit(RT_gi1, RT_reflections, material, Pass.SpecularTrace);
                context.command.SetGlobalTexture("Reflections", RT_reflections);
            }

            //If Half Resolution tracing is enabled
            if (giRenderRes >= 2)
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
                if (settings.temporalBlendWeight < 1.0f)
                {
                    context.command.Blit(RT_gi3, RT_gi4, material, Pass.TemporalBlend);
                    //SEGIBuffer.Blit(RT_gi4, RT_gi3, material, Pass.TemporalBlend);
                    context.command.Blit(RT_gi4, previousResult);
                    context.command.Blit(RT_gi1, previousDepth, material, Pass.GetCameraDepthTexture);
                }

                if (settings.GIResolution >= 3)
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
                context.command.Blit(context.source, RT_FXAART, material, settings.visualizeGI ? Pass.VisualizeGI : Pass.BlendWithScene);
            }
            else    //If Half Resolution tracing is disabled
            {

                if (settings.temporalBlendWeight < 1.0f)
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
                context.command.Blit(context.source, RT_FXAART, material, settings.visualizeGI ? Pass.VisualizeGI : Pass.BlendWithScene);
            }
            if (settings.useFXAA)
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

            //if (!settings.Sun.GetValue<Light>()) settings.Sun.GetValue<Light>() = GameObject.Find("Enviro Directional Light").GetComponent<Light>();
            //if (!Sun) Sun = GameObject.Find("Directional Light").GetComponent<Light>();

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
            transferInts = Resources.Load("SEGITransferInts_C") as ComputeShader;
            mipFilter = Resources.Load("SEGIMipFilter_C") as ComputeShader;
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
            reflectionProbe.size = new Vector3(settings.updateVoxelsAfterXInterval * 2.5f, settings.updateVoxelsAfterXInterval * 2.5f, settings.updateVoxelsAfterXInterval * 2.5f);
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.shadowDistance = settings.voxelSpaceSize;
            reflectionProbe.farClipPlane = settings.voxelSpaceSize;
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
            voxelCamera.orthographicSize = settings.voxelSpaceSize * 0.5f;
            voxelCamera.nearClipPlane = 0.0f;
            voxelCamera.farClipPlane = settings.voxelSpaceSize;
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

        void CreateVolumeTextures()
        {
            volumeTextures = new RenderTexture[numMipLevels];

            for (int i = 0; i < numMipLevels; i++)
            {
                if (volumeTextures[i])
                {
                    //volumeTextures[i].DiscardContents();
                    volumeTextures[i].Release();
                    //DestroyImmediate(volumeTextures[i]);
                }
                int resolution = (int)settings.voxelResolution.value / Mathf.RoundToInt(Mathf.Pow((float)2, (float)i));
                volumeTextures[i] = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default);
                volumeTextures[i].dimension = TextureDimension.Tex3D;
                volumeTextures[i].volumeDepth = resolution;
                volumeTextures[i].enableRandomWrite = true;
                volumeTextures[i].filterMode = FilterMode.Bilinear;
                volumeTextures[i].autoGenerateMips = false;
                volumeTextures[i].useMipMap = false;
                volumeTextures[i].Create();
                volumeTextures[i].hideFlags = HideFlags.HideAndDontSave;
            }

            if (volumeTextureB)
            {
                //volumeTextureB.DiscardContents();
                volumeTextureB.Release();
                //DestroyImmediate(volumeTextureB);
            }
            volumeTextureB = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default);
            volumeTextureB.dimension = TextureDimension.Tex3D;
            volumeTextureB.volumeDepth = (int)settings.voxelResolution.value;
            volumeTextureB.enableRandomWrite = true;
            volumeTextureB.filterMode = FilterMode.Bilinear;
            volumeTextureB.autoGenerateMips = false;
            volumeTextureB.useMipMap = false;
            volumeTextureB.Create();
            volumeTextureB.hideFlags = HideFlags.HideAndDontSave;

            if (volumeTexture1)
            {
                //volumeTexture1.DiscardContents();
                volumeTexture1.Release();
                //DestroyImmediate(volumeTexture1);
            }
            volumeTexture1 = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default);
            volumeTexture1.dimension = TextureDimension.Tex3D;
            volumeTexture1.volumeDepth = (int)settings.voxelResolution.value;
            volumeTexture1.enableRandomWrite = true;
            volumeTexture1.filterMode = FilterMode.Point;
            volumeTexture1.autoGenerateMips = false;
            volumeTexture1.useMipMap = false;
            volumeTexture1.antiAliasing = 1;
            volumeTexture1.Create();
            volumeTexture1.hideFlags = HideFlags.HideAndDontSave;



            if (intTex1)
            {
                //intTex1.DiscardContents();
                intTex1.Release();
                //DestroyImmediate(intTex1);
            }
            intTex1 = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Default);
            intTex1.dimension = TextureDimension.Tex3D;
            intTex1.volumeDepth = (int)settings.voxelResolution.value;
            intTex1.enableRandomWrite = true;
            intTex1.filterMode = FilterMode.Point;
            intTex1.Create();
            intTex1.hideFlags = HideFlags.HideAndDontSave;

            ResizeDummyTexture();
        }

        void ResizeDummyTexture()
        {
            if (dummyVoxelTexture)
            {
                //dummyVoxelTexture.DiscardContents();
                dummyVoxelTexture.Release();
                //DestroyImmediate(dummyVoxelTexture);
            }
            dummyVoxelTexture = new RenderTexture(dummyvoxelResolution, dummyvoxelResolution, 0, RenderTextureFormat.R8);
            dummyVoxelTexture.Create();
            dummyVoxelTexture.hideFlags = HideFlags.HideAndDontSave;

            if (dummyVoxelTexture2)
            {
                //dummyVoxelTexture2.DiscardContents();
                dummyVoxelTexture2.Release();
                //DestroyImmediate(dummyVoxelTexture2);
            }
            dummyVoxelTexture2 = new RenderTexture((int)settings.voxelResolution.value, (int)settings.voxelResolution.value, 0, RenderTextureFormat.R8);
            dummyVoxelTexture2.Create();
            dummyVoxelTexture2.hideFlags = HideFlags.HideAndDontSave;
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
            if (previousResult) previousResult.Release();
            //StopCoroutine(updateVoxels());

            if (SEGIRenderWidth == 0) SEGIRenderWidth = attachedCamera.scaledPixelWidth;
            if (SEGIRenderHeight == 0) SEGIRenderHeight = attachedCamera.scaledPixelHeight;

            RenderTextureDescriptor RT_Disc0 = new RenderTextureDescriptor(SEGIRenderWidth, SEGIRenderHeight, renderTextureFormat, 24);
            RenderTextureDescriptor RT_Disc1 = new RenderTextureDescriptor(SEGIRenderWidth / (int)giRenderRes, SEGIRenderHeight / (int)giRenderRes, renderTextureFormat, 24);

            //SEGIRenderWidth = attachedCamera.scaledPixelWidth == 0 ? 2 : attachedCamera.scaledPixelWidth;
            //SEGIRenderHeight = attachedCamera.scaledPixelHeight == 0 ? 2 : attachedCamera.scaledPixelHeight;

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

            /*if (SEGIRenderSource) SEGIRenderSource.Release();
            SEGIRenderSource = new RenderTexture(RT_Disc0);
            if (attachedCamera.stereoEnabled) SEGIRenderSource.vrUsage = VRTextureUsage.TwoEyes;
            SEGIRenderSource.wrapMode = TextureWrapMode.Clamp;
            SEGIRenderSource.filterMode = FilterMode.Point;
            SEGIRenderSource.Create();
            SEGIRenderSource.hideFlags = HideFlags.HideAndDontSave;

            if (SEGIRenderDestination) SEGIRenderDestination.Release();
            SEGIRenderDestination = new RenderTexture(RT_Disc0);
            if (UnityEngine.XR.XRSettings.enabled) SEGIRenderDestination.vrUsage = VRTextureUsage.TwoEyes;
            SEGIRenderDestination.wrapMode = TextureWrapMode.Clamp;
            SEGIRenderDestination.filterMode = FilterMode.Point;
            SEGIRenderDestination.Create();
            SEGIRenderDestination.hideFlags = HideFlags.HideAndDontSave;*/

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

            //SEGIBufferInit();
            //StartCoroutine(updateVoxels());
        }

        /*void CheckSupport()
        {
            systemSupported.hdrTextures = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf);
            systemSupported.rIntTextures = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RInt);
            systemSupported.dx11 = SystemInfo.graphicsShaderLevel >= 50 && SystemInfo.supportsComputeShaders;
            systemSupported.volumeTextures = SystemInfo.supports3DTextures;

            systemSupported.postShader = material.shader.isSupported;
            systemSupported.sunDepthShader = sunDepthShader.isSupported;
            systemSupported.voxelizationShader = voxelizationShader.isSupported;
            systemSupported.tracingShader = voxelTracingShader.isSupported;

            if (!systemSupported.fullFunctionality)
            {
                Debug.LogWarning("SEGI is not supported on the current platform. Check for shader compile errors in SEGI/Resources");
                //enabled = false;
            }
        }*/

        void ResizeSunShadowBuffer()
        {

            if (sunDepthTexture)
            {
                //sunDepthTexture.DiscardContents();
                sunDepthTexture.Release();
                //DestroyImmediate(sunDepthTexture);
            }
            sunDepthTexture = new RenderTexture(sunShadowResolution, sunShadowResolution, 24, RenderTextureFormat.RHalf, RenderTextureReadWrite.Default);
            if (UnityEngine.XR.XRSettings.enabled) sunDepthTexture.vrUsage = VRTextureUsage.TwoEyes;
            sunDepthTexture.wrapMode = TextureWrapMode.Clamp;
            sunDepthTexture.filterMode = FilterMode.Point;
            sunDepthTexture.Create();
            sunDepthTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        public override void Release()
        {
            if (sunDepthTexture) sunDepthTexture.Release();
            if (previousResult) previousResult.Release();
            if (previousDepth) previousDepth.Release();
            if (intTex1) intTex1.Release();
            for (int i = 0; i < volumeTextures.Length; i++)
            {
                if (volumeTextures[i]) volumeTextures[i].Release();
            }
            volumeTexture1.Release();
            volumeTextureB.Release();
            dummyVoxelTexture.Release();
            dummyVoxelTexture2.Release();
            //SEGIRenderSource.Release();
            //SEGIRenderDestination.Release();

            RT_FXAART.Release();
            RT_gi1.Release();
            RT_gi2.Release();
            RT_reflections.Release();
            RT_gi3.Release();
            RT_gi4.Release();
            RT_blur0.Release();
            RT_blur1.Release();
            RT_FXAARTluminance.Release();
        }

    }

    //####################################################################################################################################
    //####################################################################################################################################
    //####################################################################################################################################

    //####################################################################################################################################
    //####################################################################################################################################
    //####################################################################################################################################

    /*
    public class SEGICascadedBase : MonoBehaviour
    {
        object initChecker;

        Material material;
        Camera attachedCamera;
        Transform shadowCamTransform;

        Camera shadowCam;
        GameObject shadowCamGameObject;
        Texture2D[] blueNoise;

        public ReflectionProbe reflectionProbe;
        GameObject reflectionProbeGameObject;

        [Serializable]
        public enum voxelResolution.value
        {
            low = 64,
            medium = 128,
            high = 256
        }

        public voxelResolution.value voxelResolution.value = voxelResolution.value.high;

        public bool visualizeSunDepthTexture = false;
        public bool visualizeGI = false;

        public Light sun;
        public LayerMask giCullingMask = 2147483647;

        public float shadowSpaceSize = 50.0f;

        [Range(0.01f, 1.0f)]
        public float temporalBlendWeight = 0.1f;

        public bool visualizeVoxels = false;

        public bool updateGI = true;


        public Color skyColor;
        public bool MatchAmbiantColor;

        public float voxelSpaceSize = 50.0f;

        public bool useBilateralFiltering = false;

        [Range(0, 2)]
        public int innerOcclusionLayers = 1;


        [Range(1, 16)]
        public int GIResolution = 1;
        public bool stochasticSampling = true;
        public bool infiniteBounces = false;
        public Transform followTransform;
        [Range(1, 128)]
        public int cones = 6;
        [Range(1, 32)]
        public int coneTraceSteps = 14;
        [Range(0.1f, 2.0f)]
        public float coneLength = 1.0f;
        [Range(0.5f, 6.0f)]
        public float coneWidth = 5.5f;
        [Range(0.0f, 4.0f)]
        public float occlusionStrength = 1.0f;
        [Range(0.0f, 4.0f)]
        public float nearOcclusionStrength = 0.5f;
        [Range(0.001f, 4.0f)]
        public float occlusionPower = 1.5f;
        [Range(0.0f, 4.0f)]
        public float coneTraceBias = 1.0f;
        [Range(0.0f, 4.0f)]
        public float nearLightGain = 1.0f;
        [Range(0.0f, 4.0f)]
        public float giGain = 1.0f;
        [Range(0.0f, 4.0f)]
        public float secondaryBounceGain = 1.0f;
        [Range(0.0f, 16.0f)]
        public float softSunlight = 0.0f;

        [Range(0.0f, 8.0f)]
        public float skyIntensity = 1.0f;

        public bool doReflections = true;
        [Range(12, 128)]
        public int reflectionSteps = 64;
        [Range(0.001f, 4.0f)]
        public float reflectionOcclusionPower = 1.0f;
        [Range(0.0f, 1.0f)]
        public float skyReflectionIntensity = 1.0f;



        [Range(0.1f, 4.0f)]
        public float farOcclusionStrength = 1.0f;
        [Range(0.1f, 4.0f)]
        public float farthestOcclusionStrength = 1.0f;

        [Range(3, 16)]
        public int secondaryCones = 6;
        [Range(0.1f, 4.0f)]
        public float secondaryOcclusionStrength = 1.0f;

        public bool sphericalSkylight = false;


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

        struct SEGICMDBufferRT
        {
            // 0    - FXAART
            // 1    - gi1
            // 2    - gi2
            // 3    - reflections
            // 4    - gi3
            // 5    - gi4
            // 6    - blur0
            // 7    - blur1
            // 8    - FXAARTluminance
            public static int FXAART = 0;
            public static int gi1 = 1;
            public static int gi2 = 2;
            public static int reflections = 3;
            public static int gi3 = 4;
            public static int gi4 = 5;
            public static int blur0 = 6;
            public static int blur1 = 7;
            public static int FXAARTluminance = 8;
        }
        private RenderTexture RT_FXAART;
        private RenderTexture RT_gi1;
        private RenderTexture RT_gi2;
        private RenderTexture RT_reflections;
        private RenderTexture RT_gi3;
        private RenderTexture RT_gi4;
        private RenderTexture RT_blur0;
        private RenderTexture RT_blur1;
        private RenderTexture RT_FXAARTluminance;

        public RenderTexture SEGIRenderSource;
        public RenderTexture SEGIRenderDestination;
        public int SEGIRenderWidth;
        public int SEGIRenderHeight;


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

        /// <summary>
        /// Estimates the VRAM usage of all the render textures used to render GI.
        /// </summary>
        public float vramUsage
        {
            get
            {
                long v = 0;

                if (sunDepthTexture != null)
                    v += sunDepthTexture.width * sunDepthTexture.height * 16;

                if (previousResult != null)
                    v += previousResult.width * previousResult.height * 16 * 4;

                if (previousDepth != null)
                    v += previousDepth.width * previousDepth.height * 32;

                if (intTex1 != null)
                    v += intTex1.width * intTex1.height * intTex1.volumeDepth * 32;

                if (volumeTextures != null)
                {
                    for (int i = 0; i < volumeTextures.Length; i++)
                    {
                        if (volumeTextures[i] != null)
                            v += volumeTextures[i].width * volumeTextures[i].height * volumeTextures[i].volumeDepth * 16 * 4;
                    }
                }

                if (volumeTexture1 != null)
                    v += volumeTexture1.width * volumeTexture1.height * volumeTexture1.volumeDepth * 16 * 4;

                if (volumeTextureB != null)
                    v += volumeTextureB.width * volumeTextureB.height * volumeTextureB.volumeDepth * 16 * 4;

                if (dummyVoxelTexture != null)
                    v += dummyVoxelTexture.width * dummyVoxelTexture.height * 8;

                if (dummyVoxelTexture2 != null)
                    v += dummyVoxelTexture2.width * dummyVoxelTexture2.height * 8;

                float vram = (v / 8388608.0f);

                return vram;
            }
        }

        public FilterMode filterMode = FilterMode.Point;
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGBHalf;



        public bool gaussianMipFilter = false;

        int mipFilterKernel
        {
            get
            {
                return gaussianMipFilter ? 1 : 0;
            }
        }

        public bool voxelAA = false;

        int dummyvoxelResolution.value
        {
            get
            {
                return (int)voxelResolution.value * (voxelAA ? 2 : 1);
            }
        }

        int sunShadowResolution = 256;
        int prevSunShadowResolution;





        Shader sunDepthShader;

        float shadowSpaceDepthRatio = 10.0f;

        int frameSwitch = 0;

        RenderTexture sunDepthTexture;
        RenderTexture previousResult;
        RenderTexture previousDepth;
        RenderTexture intTex1;
        RenderTexture[] volumeTextures;
        RenderTexture volumeTexture1;
        RenderTexture volumeTextureB;

        RenderTexture activeVolume;
        RenderTexture previousActiveVolume;

        RenderTexture dummyVoxelTexture;
        RenderTexture dummyVoxelTexture2;

        public bool notReadyToRender = false;

        Shader voxelizationShader;
        Shader voxelTracingShader;

        ComputeShader clearCompute;
        ComputeShader transferInts;
        ComputeShader mipFilter;

        const int numMipLevels = 6;

        Camera voxelCamera;
        GameObject voxelCameraGO;
        GameObject leftViewPoint;
        GameObject topViewPoint;

        float voxelScaleFactor
        {
            get
            {
                return (float)voxelResolution.value / 256.0f;
            }
        }

        Vector3 voxelSpaceOrigin;
        Vector3 previousVoxelSpaceOrigin;
        Vector3 voxelSpaceOriginDelta;


        Quaternion rotationFront = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
        Quaternion rotationLeft = new Quaternion(0.0f, 0.7f, 0.0f, 0.7f);
        Quaternion rotationTop = new Quaternion(0.7f, 0.0f, 0.0f, 0.7f);

        int voxelFlipFlop = 0;


        int giRenderRes
        {
            get
            {
                return GIResolution;
            }
        }

        enum RenderState
        {
            Voxelize,
            Bounce
        }

        RenderState renderState = RenderState.Voxelize;

        //CommandBuffer refactor
        public CommandBuffer SEGIBuffer;

        //Gaussian Filter
        private Shader Gaussian_Shader;
        private Material Gaussian_Material;

        //FXAA
        public bool useFXAA;
        private Shader FXAA_Shader;
        private Material FXAA_Material;

        //Forward Rendering
        public bool useReflectionProbes = true;
        [Range(0, 2)]
        public float reflectionProbeIntensity = 0.5f;
        [Range(0, 2)]
        public float reflectionProbeAttribution = 1f;
        public LayerMask reflectionProbeLayerMask = 2147483647;

        //Delayed voxelization
        public bool updateVoxelsAfterXDoUpdate = false;
        public int updateVoxelsAfterXInterval = 1;
        private double updateVoxelsAfterXPrevX = 9223372036854775807;
        private double updateVoxelsAfterXPrevY = 9223372036854775807;
        private double updateVoxelsAfterXPrevZ = 9223372036854775807;


        public virtual void OnPreRender()
        {
        }
        /*
        public void LoadAndApplyPreset(string path)
        {
            SEGICascadedPreset preset = Resources.Load<SEGICascadedPreset>(path);

            ApplyPreset(preset);
        }
        */
    /*   public void ApplyPreset(SEGICascadedPreset preset)
       {
           //voxelResolution.value = preset.voxelResolution.value;
           voxelAA = preset.voxelAA;
           innerOcclusionLayers = preset.innerOcclusionLayers;
           infiniteBounces = preset.infiniteBounces;

           temporalBlendWeight = preset.temporalBlendWeight;
           useBilateralFiltering = preset.useBilateralFiltering;
           GIResolution = preset.GIResolution;
           stochasticSampling = preset.stochasticSampling;
           doReflections = preset.doReflections;

           cones = preset.cones;
           coneTraceSteps = preset.coneTraceSteps;
           coneLength = preset.coneLength;
           coneWidth = preset.coneWidth;
           coneTraceBias = preset.coneTraceBias;
           occlusionStrength = preset.occlusionStrength;
           nearOcclusionStrength = preset.nearOcclusionStrength;
           occlusionPower = preset.occlusionPower;
           nearLightGain = preset.nearLightGain;
           giGain = preset.giGain;
           secondaryBounceGain = preset.secondaryBounceGain;

           reflectionSteps = preset.reflectionSteps;
           reflectionOcclusionPower = preset.reflectionOcclusionPower;
           skyReflectionIntensity = preset.skyReflectionIntensity;
           gaussianMipFilter = preset.gaussianMipFilter;

           farOcclusionStrength = preset.farOcclusionStrength;
           farthestOcclusionStrength = preset.farthestOcclusionStrength;
           secondaryCones = preset.secondaryCones;
           secondaryOcclusionStrength = preset.secondaryOcclusionStrength;
       }
       */









    /*
        void OnDrawGizmosSelected()
        {
            Color prevColor = Gizmos.color;
            Gizmos.color = new Color(1.0f, 0.25f, 0.0f, 0.5f);

            Gizmos.DrawCube(voxelSpaceOrigin, new Vector3(voxelSpaceSize, voxelSpaceSize, voxelSpaceSize));

            Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.1f);

            Gizmos.color = prevColor;
        }

        /*void CleanupTexture(ref RenderTexture texture)
        {
            DestroyImmediate(texture);

        }*/


    /*
        void FixRes(RenderTexture rt)
        {
            if (rt.width != sunShadowResolution || rt.height != sunShadowResolution)
            {
                rt.Release();
                rt.width = rt.height = sunShadowResolution;
                rt.Create();
            }
        }

        IEnumerator<int> updateVoxels()
        {
            /*
            while (true)
            {
                /*for (int i = 0; i < numMipLevels - 1; i++)
                {
                    RenderTexture source = activeVolume;

                    if (i == 0)
                    {
                        source = activeVolume;
                    }

                    int destinationRes = (int)voxelResolution.value / Mathf.RoundToInt(Mathf.Pow((float)2, (float)i + 1.0f));
                    mipFilter.SetInt("destinationRes", destinationRes);
                    mipFilter.SetTexture(mipFilterKernel, "Source", source);
                    mipFilter.SetTexture(mipFilterKernel, "Destination", volumeTextures[i + 1]);
                    mipFilter.Dispatch(mipFilterKernel, (int)voxelResolution.value / 16, (int)voxelResolution.value / 16, 1);
                    Shader.SetGlobalTexture("SEGIVolumeLevel" + (i + 1).ToString(), volumeTextures[i + 1]);
                    yield return 0;
                }
                yield return 0;
            }
            yield break;
        }

        struct ExecuteSEGIBufer : IJob
        {
            //public NativeArray<Material.> result;

            public void Execute()
            {

            }
        }

        public void SEGIBufferInit()
        {
            //CommandBuffer
            if (SEGIBuffer != null) SEGIBuffer.Clear();
            else return;

            updateVoxelsAfterXPrevX = 9223372036854775807;
            updateVoxelsAfterXPrevY = 9223372036854775807;
            updateVoxelsAfterXPrevZ = 9223372036854775807;


        }

        float[] Vector4ArrayToFloats(Vector4[] vecArray)
        {
            float[] temp = new float[vecArray.Length * 4];
            for (int i = 0; i < vecArray.Length; i++)
            {
                temp[i * 4 + 0] = vecArray[i].x;
                temp[i * 4 + 1] = vecArray[i].y;
                temp[i * 4 + 2] = vecArray[i].z;
                temp[i * 4 + 3] = vecArray[i].w;
            }
            return temp;
        }

        float[] MatrixArrayToFloats(Matrix4x4[] mats)
        {
            float[] temp = new float[mats.Length * 16];
            for (int i = 0; i < mats.Length; i++)
            {
                for (int n = 0; n < 16; n++)
                {
                    temp[i * 16 + n] = mats[i][n];
                }
            }
            return temp;
        }

        float[] MatrixToFloats(Matrix4x4 mat)
        {
            float[] temp = new float[16];
            for (int i = 0; i < 16; i++)
            {
                temp[i] = mat[i];
            }
            return temp;
        }
        float[] MatrixToFloats(Matrix4x4 mat, bool transpose)
        {
            Matrix4x4 matTranspose = mat;
            if (transpose)
                matTranspose = Matrix4x4.Transpose(mat);
            float[] temp = new float[16];
            for (int i = 0; i < 16; i++)
            {
                temp[i] = matTranspose[i];
            }
            return temp;
        }

        static int[] FindTextureSize(int pCellCount)
        {
            if (pCellCount <= 0)
            {
                Debug.LogError("pCellCount has to be > 0");
                return null;
            }
            int size = pCellCount;
            while (size != 1)
            {
                if (size % 2 != 0)
                {
                    Debug.LogError("pCellCount is not a power of two");
                    return null;
                }
                size /= 2;
            }
            int repeat_x = 2;
            int repeat_y = 0;
            while (true)
            {
                size = repeat_x * pCellCount;
                while (size != 1)
                {
                    if (size % 2 != 0)
                    {
                        break;
                    }
                    size /= 2;
                }
                if (size == 1)
                { //if it is a power of two size is 1
                    repeat_y = pCellCount / repeat_x;
                    if (pCellCount % repeat_x != 0)
                        repeat_y++;
                    if (repeat_y <= repeat_x)
                    {
                        return new int[]
                        {
                                repeat_x * pCellCount,
                                repeat_y * pCellCount,
                                repeat_x,
                                repeat_y
                        };
                    }
                }
                repeat_x++;
            }
        }

    }

    */
}