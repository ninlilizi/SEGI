using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
#if UNITY_EDITOR
using UnityEditor;
#endif
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Sonic Ether/SEGI (Cascaded)")]
public class SEGICascaded : MonoBehaviour
{
    #region Parameters
    [Serializable]
    public enum VoxelResolution
    {
        potato = 16,
        low = 32,
        medium = 64,
        high = 128
    }

    public VoxelResolution voxelResolution = VoxelResolution.high;

    public bool visualizeSunDepthTexture = false;
    public bool visualizeGI = false;

    public Light sun;
    public LayerMask giCullingMask = 2147483647;
    public LayerMask shadowVolumeMask = 0;
    public float shadowSpaceSize = 50.0f;

    [Range(0.01f, 1.0f)]
    public float temporalBlendWeight = 1.0f;

    public bool visualizeVoxels = false;
    public bool visualizeShadowmapCopy = false;
    public int shadowmapCopySize = 512;
    public bool updateGI = true;
    public bool useVolumeRayCast = false;
    bool useVolumeRayCastPrev = false;

    public bool MatchAmbiantColor;
    public Color skyColor;

    public float voxelSpaceSize = 25.0f;

    public bool useBilateralFiltering = false;

    [Range(0, 2)]
    public int innerOcclusionLayers = 1;

    [Range(1, 4)]
    public int GIResolution = 1;
    public bool stochasticSampling = true;
    public bool infiniteBounces = false;
    public bool infiniteBouncesRerenderObjects = false;
    public Transform followTransform;
    [Range(1, 128)]
    public int cones = 4;
    [Range(1, 32)]
    public int coneTraceSteps = 10;
    [Range(0.1f, 2.0f)]
    public float coneLength = 1.0f;
    [Range(0.5f, 12.0f)]
    public float coneWidth = 3.9f;
    [Range(0.0f, 2.0f)]
    public float occlusionStrength = 0.15f;
    [Range(0.0f, 4.0f)]
    public float nearOcclusionStrength = 0.5f;
    [Range(0.001f, 4.0f)]
    public float occlusionPower = 0.65f;
    [Range(0.0f, 4.0f)]
    public float coneTraceBias = 2.8f;
    [Range(0.0f, 4.0f)]
    public float nearLightGain = 0.36f;
    [Range(0.0f, 4.0f)]
    public float giGain = 1.0f;
    [Range(0.0f, 4.0f)]
    public float secondaryBounceGain = 0.9f;
    [Range(0.0f, 16.0f)]
    public float softSunlight = 0.0f;

    [Range(0.0f, 8.0f)]
    public float skyIntensity = 1.0f;

    [HideInInspector]
    public bool doReflections = false;
    /*{
        get
        {
            return false;   //Locked to keep reflections disabled since they're in a broken state with cascades at the moment
        }
        set
        {
            value = false;
        }
    }*/

    [Range(6, 128)]
    public int reflectionSteps = 12;
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
    [Range(0.1f, 2.0f)]
    public float secondaryOcclusionStrength = 0.27f;

    public bool sphericalSkylight = false;

    public bool useUnityShadowMap = false;
    #endregion // Parameters

    #region InternalVariables
    object initChecker;
    Material material;
    Material m_CopyShadowParamsMaterial;
    Camera attachedCamera;
    Transform shadowCamTransform;
    public Camera shadowCam;
    GameObject shadowCamGameObject;
    public ReflectionProbe reflectionProbe;
    GameObject reflectionProbeGameObject;
    //public GameObject volumeGroup;
    public bool showVolumeObjects = false;
    public GameObject volumeCube;
    public Camera volumeFrontCam;
    public Camera volumeBackCam;
    public float SEGIShadowBias = 0.2525f;
    public float SEGIShadowScale = 1;
    public RenderTexture FrontRT;
    public RenderTexture BackRT;

    [Range(0.001f, 0.5f)]
    public float noiseDistribution = 0.35f;

    Texture2D[] blueNoise;

    public int sunShadowResolution = 128;
    int prevSunShadowResolution;

    Shader sunDepthShader;
    Shader sunVolumeRayCastShader;
    Shader frontRayCastShader;
    Shader backRayCastShader;
    float shadowSpaceDepthRatio = 10.0f;

    int frameCounter = 0;

    public RenderTexture m_ShadowmapCopy;

    RenderTexture[] sunDepthTexture;
    public int sunDepthTextureDepth = 32;
    RenderTexture previousGIResult;
    RenderTexture previousDepth;

    ///<summary>This is a volume texture that is immediately written to in the voxelization shader. The RInt format enables atomic writes to avoid issues where multiple fragments are trying to write to the same voxel in the volume.</summary>
   // RenderTexture integerVolume;
    public RenderTexture integerVolumeArray;

    ///<summary>A 2D texture with the size of [voxel resolution, voxel resolution] that must be used as the active render texture when rendering the scene for voxelization. This texture scales depending on whether Voxel AA is enabled to ensure correct voxelization.</summary>
    RenderTexture dummyVoxelTextureAAScaled;

    ///<summary>A 2D texture with the size of [voxel resolution, voxel resolution] that must be used as the active render texture when rendering the scene for voxelization. This texture is always the same size whether Voxel AA is enabled or not.</summary>
    RenderTexture dummyVoxelTextureFixed;

    ///<summary>The main GI data clipmaps that hold GI data referenced during GI tracing</summary>
    public Clipmap[] clipmaps;

    ///<summary>The secondary clipmaps that hold irradiance data for infinite bounces</summary>
    Clipmap[] irradianceClipmaps;

    bool notReadyToRender = false;

    Shader voxelizationShader;
    Shader voxelizationShaderNoShadows;
    Shader voxelTracingShader;
    Shader m_CopyShadowParamsShader;

    //ComputeShader clearCompute;
    public ComputeShader transferIntsCompute;
    ComputeShader mipFilterCompute;

    const int numClipmaps = 6;
    int clipmapCounter = 0;
    public int currentClipmapIndex = 0;

    public Camera voxelCamera;
    GameObject voxelCameraGO;
    GameObject leftViewPoint;
    GameObject topViewPoint;

    float voxelScaleFactor
    {
        get
        {
            return (float)voxelResolution / 256.0f;
        }
    }

    Quaternion rotationFront = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);
    Quaternion rotationLeft = new Quaternion(0.0f, 0.7f, 0.0f, 0.7f);
    Quaternion rotationTop = new Quaternion(0.7f, 0.0f, 0.0f, 0.7f);

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

    ComputeBuffer m_ShadowParamsCB;
    public Vector4 minPosVoxel;

    Matrix4x4[] voxelToGIProjection;
    Matrix4x4[] voxelProjectionInverse;
    public Vector4 vecGridSize;

    /// <summary>
    /// Did we already calculate and only need to display the results?
    /// </summary>
    internal static SEGICascaded calculationSEGIObject = null;

    int updateGIcounter = 0;
    public int updateGIevery = 1;

    #endregion // InternalVariables



    #region SupportingObjectsAndProperties
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
    public float vramUsage  //TODO: Update vram usage calculation
    {
        get
        {
            if (!enabled)
            {
                return 0.0f;
            }
            long v = 0;

            if (sunDepthTexture != null)
                for (int i = 0; i < sunDepthTexture.Length; i++)
                {
                    if (sunDepthTexture[i] != null)
                        v += sunDepthTexture[i].width * sunDepthTexture[i].height * sunDepthTextureDepth;
                }

            if (previousGIResult != null)
                v += previousGIResult.width * previousGIResult.height * 16 * 4;

            if (previousDepth != null)
                v += previousDepth.width * previousDepth.height * 32;

            if (integerVolumeArray != null)
                v += integerVolumeArray.width * integerVolumeArray.height * 32 * integerVolumeArray.volumeDepth;// integerVolume.volumeDepth * 32;

            if (dummyVoxelTextureAAScaled != null)
                v += dummyVoxelTextureAAScaled.width * dummyVoxelTextureAAScaled.height * 8;

            if (dummyVoxelTextureFixed != null)
                v += dummyVoxelTextureFixed.width * dummyVoxelTextureFixed.height * 8;

            if (clipmaps != null)
            {
                for (int i = 0; i < numClipmaps; i++)
                {
                    if (clipmaps[i] != null)
                    {
                        v += clipmaps[i].volumeTexture0.width * clipmaps[i].volumeTexture0.height * clipmaps[i].volumeTexture0.volumeDepth * 16 * 4;
                    }
                }
            }

            if (irradianceClipmaps != null)
            {
                for (int i = 0; i < numClipmaps; i++)
                {
                    if (irradianceClipmaps[i] != null)
                    {
                        v += irradianceClipmaps[i].volumeTexture0.width * irradianceClipmaps[i].volumeTexture0.height * irradianceClipmaps[i].volumeTexture0.volumeDepth * 16 * 4;
                    }
                }
            }

            float vram = (v / 8388608.0f);

            return vram;
        }
    }

    public class Clipmap
    {
        public Vector3 origin;
        public Vector3 originDelta;
        public Vector3 previousOrigin;
        public float localScale;

        public int resolution;

        public RenderTexture volumeTexture0;

        public FilterMode filterMode = FilterMode.Point;
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGBHalf;

        public void UpdateTextures()
        {
            if (volumeTexture0)
            {
                //volumeTexture0.DiscardContents();
                volumeTexture0.Release();
                //DestroyImmediate(volumeTexture0);
            }
            volumeTexture0 = new RenderTexture(resolution, resolution, 0, renderTextureFormat, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) volumeTexture0.vrUsage = VRTextureUsage.TwoEyes;
            volumeTexture0.wrapMode = TextureWrapMode.Clamp;
#if UNITY_5_4_OR_NEWER
            volumeTexture0.dimension = TextureDimension.Tex3D;
#else
			volumeTexture0.isVolume = true;
#endif
            volumeTexture0.volumeDepth = resolution;
            volumeTexture0.filterMode = filterMode;
#if UNITY_5_4_OR_NEWER
            volumeTexture0.autoGenerateMips = true;
#else
			volumeTexture0.generateMips = false;
#endif
            volumeTexture0.enableRandomWrite = true;
            volumeTexture0.useMipMap = true;
            volumeTexture0.Create();
            volumeTexture0.hideFlags = HideFlags.HideAndDontSave;
        }

        public void CleanupTextures()
        {
            if (volumeTexture0)
            {
                //volumeTexture0.DiscardContents();
                volumeTexture0.Release();
                //DestroyImmediate(volumeTexture0);//TODO needed? unity should handle this
            }
        }
    }

    public bool gaussianMipFilter
    {
        get
        {
            return false;
        }
        set
        {
            value = false;
        }
    }

    int mipFilterKernel
    {
        get
        {
            return gaussianMipFilter ? 1 : 0;
        }
    }

    public bool voxelAA = false;

    int dummyVoxelResolution
    {
        get
        {
            return (int)voxelResolution * (voxelAA ? 4 : 1);
        }
    }

    //GaussianBlur
    private Shader GaussianBlur_Shader;
    private Material GaussianBlur_Material;
    public float sigma = 10f;
    public enum BlurQuality
    {
        LITTLE_KERNEL,
        MEDIUM_KERNEL,
        BIG_KERNEL
    };

    //Forward Rendering
    public bool useReflectionProbes = true;

    #endregion // SupportingObjectsAndProperties



    public void ApplyPreset(SEGICascadedPreset preset)
    {
        voxelResolution = preset.voxelResolution;
        voxelAA = preset.voxelAA;
        innerOcclusionLayers = preset.innerOcclusionLayers;
        infiniteBounces = preset.infiniteBounces;

        temporalBlendWeight = preset.temporalBlendWeight;
        useBilateralFiltering = preset.useBilateralFiltering;
        GIResolution = preset.GIResolution;
        stochasticSampling = preset.stochasticSampling;
        doReflections = preset.doReflections;

        noiseDistribution = preset.noiseDistribution;
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

        useReflectionProbes = preset.useReflectionProbes;
    }

    public virtual void Start()
    {
        InitCheck();
        InitShadowmapCopy();

    }

    void InitCheck()
    {
        if (initChecker == null)
        {
            Init();
        }
    }

    public virtual void InitShadowmapCopy()
    {
        CheckShadowKeyword("SEGI_UNITY_SHADOWMAP_", useUnityShadowMap ? "ON" : "OFF");
        if (useUnityShadowMap)
        {
            //if (ngss.m_ShadowmapCopy != null)
            //{
            //    Debug.Log("SEGI using external m_ShadowmapCopy");
            //    m_ShadowmapCopy = ngss.m_ShadowmapCopy;
            //}
            //Debug.Log("m_ShadowmapCopy " + m_ShadowmapCopy);
            if (m_ShadowmapCopy == null)
            {
                RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;
                m_ShadowmapCopy = new RenderTexture(shadowmapCopySize, shadowmapCopySize, 0);//TODO
                if (UnityEngine.XR.XRSettings.enabled) m_ShadowmapCopy.vrUsage = VRTextureUsage.TwoEyes;
                CommandBuffer cb = new CommandBuffer();

                // Change shadow sampling mode for sun's shadowmap.
                cb.SetShadowSamplingMode(shadowmap, ShadowSamplingMode.RawDepth);

                // The shadowmap values can now be sampled normally - copy it to a different render texture.
                cb.Blit(shadowmap, new RenderTargetIdentifier(m_ShadowmapCopy));

                // Execute after the shadowmap has been filled.
                sun.AddCommandBuffer(LightEvent.AfterShadowMap, cb);

                // Sampling mode is restored automatically after this command buffer completes, so shadows will render normally.
            }
        }
    }

    void CreateVolumeTextures()
    {
        var size2D = FindTextureSize((int)voxelResolution);
        int chucks2D = Mathf.CeilToInt(size2D[0] / (float)voxelResolution);
        vecGridSize = new Vector4((int)voxelResolution, (int)voxelResolution, (int)voxelResolution, chucks2D);

        if (integerVolumeArray)
        {
            //integerVolume.DiscardContents();
            integerVolumeArray.Release();
            //DestroyImmediate(integerVolume);
        }

        integerVolumeArray = new RenderTexture((int)size2D[0], (int)size2D[1], 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear);
        if (UnityEngine.XR.XRSettings.enabled) integerVolumeArray.vrUsage = VRTextureUsage.TwoEyes;
        integerVolumeArray.dimension = TextureDimension.Tex2DArray;
        integerVolumeArray.enableRandomWrite = true;
        integerVolumeArray.volumeDepth = useVolumeRayCast ? 6 : 1;
        integerVolumeArray.useMipMap = true;
        integerVolumeArray.autoGenerateMips = true;
        integerVolumeArray.filterMode = FilterMode.Point;
        integerVolumeArray.hideFlags = HideFlags.HideAndDontSave;
        integerVolumeArray.Create();

        ResizeDummyTexture();
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
            clipmaps[i].resolution = (int)voxelResolution;
            clipmaps[i].filterMode = FilterMode.Point;
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
            irradianceClipmaps[i].resolution = (int)voxelResolution;
            irradianceClipmaps[i].filterMode = FilterMode.Point;
            irradianceClipmaps[i].renderTextureFormat = RenderTextureFormat.ARGBHalf;
            irradianceClipmaps[i].UpdateTextures();
        }
    }

    void ResizeDummyTexture()
    {
        if (dummyVoxelTextureAAScaled)
        {
            //dummyVoxelTextureAAScaled.DiscardContents();
            dummyVoxelTextureAAScaled.Release();
            //DestroyImmediate(dummyVoxelTextureAAScaled);
        }
        dummyVoxelTextureAAScaled = new RenderTexture(dummyVoxelResolution, dummyVoxelResolution, 0, RenderTextureFormat.ARGBHalf);
        //if (UnityEngine.XR.XRSettings.enabled) dummyVoxelTextureAAScaled.vrUsage = VRTextureUsage.TwoEyes;
        dummyVoxelTextureAAScaled.Create();
        dummyVoxelTextureAAScaled.hideFlags = HideFlags.HideAndDontSave;

        if (dummyVoxelTextureFixed)
        {
            //dummyVoxelTextureFixed.DiscardContents();
            dummyVoxelTextureFixed.Release();
            //DestroyImmediate(dummyVoxelTextureFixed);
        }
        dummyVoxelTextureFixed = new RenderTexture((int)voxelResolution, (int)voxelResolution, 0, RenderTextureFormat.ARGBHalf);
        //if (UnityEngine.XR.XRSettings.enabled) dummyVoxelTextureFixed.vrUsage = VRTextureUsage.TwoEyes;
        dummyVoxelTextureFixed.Create();
        dummyVoxelTextureFixed.hideFlags = HideFlags.HideAndDontSave;
    }

    void GetBlueNoiseTextures()
    {
        blueNoise = null;
        blueNoise = new Texture2D[64];
        for (int i = 0; i < 64; i++)
        {
            string filename = "LDR_RGBA_" + i.ToString();
            Texture2D blueNoiseTexture = Resources.Load("Noise Textures/" + filename) as Texture2D;

            if (blueNoiseTexture == null)
            {
                Debug.LogWarning("Unable to find noise texture \"Assets/SEGI/Resources/Noise Textures/" + filename + "\" for SEGI!");
            }

            blueNoise[i] = blueNoiseTexture;
        }
    }

    void SetupSunDepthTexture()
    {
        int length = useVolumeRayCast ? numClipmaps : 1;
        //Debug.Log("sunDepthTexture " + sunDepthTexture);

        if (sunDepthTexture == null)// || sunDepthTexture[0] == null || length != sunDepthTexture.Length)
        {
            sunDepthTexture = new RenderTexture[length];
            ResizeSunShadowBuffer();
            //Debug.Log("sunDepthTexture " + sunDepthTexture + " "+ sunDepthTexture.Length);
        }
    }

    void SetupVolumeRayCasting()
    {
        useVolumeRayCastPrev = useVolumeRayCast;

        SetupSunDepthTexture();
        CreateVolumeTextures();

        if (useVolumeRayCast)
        {
            voxelToGIProjection = new Matrix4x4[numClipmaps];
            voxelProjectionInverse = new Matrix4x4[numClipmaps];
            for (int i = 0; i < numClipmaps; i++)
            {
                voxelToGIProjection[i] = Matrix4x4.identity;
                voxelProjectionInverse[i] = Matrix4x4.identity;
            }

            volumeCube = GameObject.Find("SEGICubeVolume");
            if (!volumeCube)
            {
                volumeCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                volumeCube.name = "SEGICubeVolume";
                DestroyImmediate(volumeCube.GetComponent<Collider>());
            }
            volumeCube.layer = 19;//TODO use front layer
            volumeCube.GetComponent<Renderer>().sharedMaterial = (Material)Resources.Load("SEGIFrontRC", typeof(Material));
            volumeCube.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            volumeCube.hideFlags = showVolumeObjects ? HideFlags.None : HideFlags.HideAndDontSave;

            shadowVolumeMask = 1 << volumeCube.layer;

            if (!FrontRT)
            {
                FrontRT = new RenderTexture(sunShadowResolution, sunShadowResolution, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                if (UnityEngine.XR.XRSettings.enabled) FrontRT.vrUsage = VRTextureUsage.TwoEyes;
                FrontRT.autoGenerateMips = false;
                FrontRT.wrapMode = TextureWrapMode.Clamp;
                FrontRT.filterMode = FilterMode.Point;
                FrontRT.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!BackRT)
            {
                BackRT = new RenderTexture(sunShadowResolution, sunShadowResolution, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                if (UnityEngine.XR.XRSettings.enabled) BackRT.vrUsage = VRTextureUsage.TwoEyes;
                BackRT.autoGenerateMips = false;
                BackRT.wrapMode = TextureWrapMode.Clamp;
                BackRT.filterMode = FilterMode.Point;
                BackRT.hideFlags = HideFlags.HideAndDontSave;
            }

            FixRes(FrontRT);
            FixRes(BackRT);

            Shader.SetGlobalTexture("SEGIFrontS", FrontRT);
            Shader.SetGlobalTexture("SEGIBackS", BackRT);

            GameObject frontCam = GameObject.Find("VolumeFrontCam");
            if (frontCam != null) volumeFrontCam = frontCam.GetComponent<Camera>();
            if (!volumeFrontCam)
            {
                var goFrontCam = new GameObject("VolumeFrontCam");
                goFrontCam.hideFlags = HideFlags.HideAndDontSave;
                volumeFrontCam = goFrontCam.AddComponent<Camera>();
            }
            volumeFrontCam.CopyFrom(shadowCam);
            volumeFrontCam.cullingMask = shadowVolumeMask;
            volumeFrontCam.depth = -99;
            volumeFrontCam.targetTexture = FrontRT;
            volumeFrontCam.enabled = false;

            GameObject backCam = GameObject.Find("VolumeBackCam");
            if (backCam != null) volumeBackCam = backCam.GetComponent<Camera>();
            if (!volumeBackCam)
            {
                var goBackCam = new GameObject("VolumeBackCam");
                goBackCam.hideFlags = HideFlags.HideAndDontSave;
                volumeBackCam = goBackCam.AddComponent<Camera>();
            }
            volumeBackCam.CopyFrom(shadowCam);
            volumeBackCam.cullingMask = shadowVolumeMask;
            volumeBackCam.depth = -99;
            volumeBackCam.targetTexture = BackRT;
            volumeBackCam.enabled = false;
        }
    }

    void Init()
    {
        //GaussianBlur
        GaussianBlur_Shader = Shader.Find("hidden/two_pass_linear_sampling_gaussian_blur");
        GaussianBlur_Material = new Material(GaussianBlur_Shader);
        GaussianBlur_Material.EnableKeyword("SMALL_KERNEL");
        GaussianBlur_Material.enableInstancing = false;

        //Setup shaders and materials
        sunDepthShader = Shader.Find("Hidden/SEGIRenderSunDepth_C");
        sunVolumeRayCastShader = Shader.Find("Custom/VolumeRayCasting");//TODO
        frontRayCastShader = Shader.Find("Custom/FrontPos");
        backRayCastShader = Shader.Find("Custom/BackPos");
        //clearCompute = Resources.Load("SEGIClear_C") as ComputeShader;
        transferIntsCompute = Resources.Load("SEGITransferInts_C") as ComputeShader;
        mipFilterCompute = Resources.Load("SEGIMipFilter_C") as ComputeShader;
        voxelizationShader = Shader.Find("Hidden/SEGIVoxelizeScene_C");
        voxelizationShaderNoShadows = Shader.Find("Hidden/SEGIVoxelizeSceneNoShadows_C");
        voxelTracingShader = Shader.Find("Hidden/SEGITraceScene_C");
        m_CopyShadowParamsShader = Shader.Find("Hidden/SEGICopyShadowParams");

        if (!material)
        {
            material = new Material(Shader.Find("Hidden/SEGI_C"));
            material.enableInstancing = true;
            material.hideFlags = HideFlags.HideAndDontSave;
        }
        if (!m_CopyShadowParamsMaterial)
        {
            m_CopyShadowParamsMaterial = new Material(m_CopyShadowParamsShader);
            m_CopyShadowParamsMaterial.enableInstancing = true;
            m_CopyShadowParamsMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        //Get the camera attached to this game object
        attachedCamera = this.GetComponent<Camera>();
        attachedCamera.depthTextureMode |= DepthTextureMode.Depth;
        attachedCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
#if UNITY_5_4_OR_NEWER
        attachedCamera.depthTextureMode |= DepthTextureMode.MotionVectors;
#endif

        //Find the proxy shadow rendering camera if it exists
        GameObject rfgo = GameObject.Find("SEGI_REFLECTIONPROBE");


        //If not, create it
        if (!rfgo)
        {
            reflectionProbeGameObject = new GameObject("SEGI_REFLECTIONPROBE");
            reflectionProbe = reflectionProbeGameObject.AddComponent<ReflectionProbe>();
            reflectionProbeGameObject.hideFlags = HideFlags.HideAndDontSave;

            reflectionProbeGameObject.transform.parent = attachedCamera.transform;
            reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            reflectionProbe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;
            reflectionProbe.mode = ReflectionProbeMode.Realtime;
            reflectionProbe.enabled = false;

            reflectionProbe.transform.localPosition = new Vector3(0, 0, 0);
        }
        else
        {
            reflectionProbeGameObject = rfgo;
            reflectionProbe = rfgo.GetComponent<ReflectionProbe>();
        }


        //Find the proxy shadow rendering camera if it exists
        GameObject scgo = GameObject.Find("SEGI_SHADOWCAM");

        //If not, create it
        if (!scgo)
        {
            shadowCamGameObject = new GameObject("SEGI_SHADOWCAM");
            shadowCam = shadowCamGameObject.AddComponent<Camera>();
            shadowCamGameObject.hideFlags = HideFlags.HideAndDontSave;


            shadowCam.enabled = false;
            shadowCam.depth = attachedCamera.depth - 1;
            shadowCam.orthographic = true;
            shadowCam.orthographicSize = shadowSpaceSize;
            shadowCam.clearFlags = CameraClearFlags.SolidColor;
            shadowCam.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 1.0f);
            shadowCam.farClipPlane = shadowSpaceSize * 2.0f * shadowSpaceDepthRatio;
            shadowCam.cullingMask = giCullingMask;
            shadowCam.useOcclusionCulling = false;
            shadowCam.allowMSAA = false;
            shadowCam.renderingPath = RenderingPath.Forward;
            shadowCamTransform = shadowCamGameObject.transform;
        }
        else    //Otherwise, it already exists, just get it
        {
            shadowCamGameObject = scgo;
            shadowCam = scgo.GetComponent<Camera>();
            shadowCamTransform = shadowCamGameObject.transform;
        }

        SetupVolumeRayCasting();

        //Create the proxy camera objects responsible for rendering the scene to voxelize the scene. If they already exist, destroy them
        GameObject vcgo = GameObject.Find("SEGI_VOXEL_CAMERA");

        if (!vcgo)
        {
            voxelCameraGO = new GameObject("SEGI_VOXEL_CAMERA");
            voxelCameraGO.hideFlags = HideFlags.HideAndDontSave;

            voxelCamera = voxelCameraGO.AddComponent<Camera>();
            voxelCamera.enabled = false;
            voxelCamera.orthographic = true;
            voxelCamera.orthographicSize = voxelSpaceSize * 0.5f;
            voxelCamera.nearClipPlane = 0.0f;
            voxelCamera.farClipPlane = voxelSpaceSize;
            voxelCamera.depth = -2;
            voxelCamera.renderingPath = RenderingPath.Forward;
            voxelCamera.clearFlags = CameraClearFlags.Color;
            voxelCamera.backgroundColor = Color.black;
            voxelCamera.useOcclusionCulling = false;
        }
        else
        {
            voxelCameraGO = vcgo;
            voxelCamera = vcgo.GetComponent<Camera>();
        }

        GameObject lvp = GameObject.Find("SEGI_LEFT_VOXEL_VIEW");

        if (!lvp)
        {
            leftViewPoint = new GameObject("SEGI_LEFT_VOXEL_VIEW");
            leftViewPoint.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            leftViewPoint = lvp;
        }

        GameObject tvp = GameObject.Find("SEGI_TOP_VOXEL_VIEW");

        if (!tvp)
        {
            topViewPoint = new GameObject("SEGI_TOP_VOXEL_VIEW");
            topViewPoint.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            topViewPoint = tvp;
        }

        //Setup sun depth texture
        SetupSunDepthTexture();
        CreateVolumeTextures();
        BuildClipmaps();
        GetBlueNoiseTextures();



        initChecker = new object();
    }

    void CheckSupport()
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
            enabled = false;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!enabled)
            return;
        Color prevColor = Gizmos.color;
        Gizmos.color = new Color(1.0f, 0.25f, 0.0f, 0.5f);
        if (clipmaps != null && clipmaps[numClipmaps - 1] != null)
        {
            float scale = clipmaps[numClipmaps - 1].localScale;
            Gizmos.DrawCube(clipmaps[0].origin, new Vector3(voxelSpaceSize * scale, voxelSpaceSize * scale, voxelSpaceSize * scale));
        }
        Gizmos.color = new Color(1.0f, 0.0f, 0.0f, 0.1f);

        Gizmos.color = prevColor;
    }

    void CleanupTexture(ref RenderTexture texture)
    {
        if (texture)
        {
            //texture.DiscardContents();
            texture.Release();
            //DestroyImmediate(texture);//TODO needed?
        }
    }

    void CleanupTextures()
    {
        for (int i = 0; i < sunDepthTexture.Length; i++)
            CleanupTexture(ref sunDepthTexture[i]);
        CleanupTexture(ref previousGIResult);
        CleanupTexture(ref previousDepth);
        //CleanupTexture(ref integerVolume);
        CleanupTexture(ref integerVolumeArray);
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
    }

    void Cleanup()
    {
        if (m_ShadowParamsCB != null)
            m_ShadowParamsCB.Release();
        m_ShadowParamsCB = null;

        DestroyImmediate(material);
        DestroyImmediate(m_CopyShadowParamsMaterial);
        DestroyImmediate(voxelCameraGO);
        DestroyImmediate(leftViewPoint);
        DestroyImmediate(topViewPoint);
        DestroyImmediate(shadowCamGameObject);
        DestroyImmediate(volumeCube);
        DestroyImmediate(reflectionProbeGameObject);
        initChecker = null;
        CleanupTextures();
    }

    void OnEnable()
    {
        InitCheck();
        ResizeRenderTextures();

        CheckSupport();
    }

    void OnDisable()
    {
        Cleanup();
    }

    void ResizeRenderTextures()
    {
        if (previousGIResult)
        {
            //previousGIResult.DiscardContents();
            previousGIResult.Release();
            //DestroyImmediate(previousGIResult);
        }

        int width = attachedCamera.pixelWidth == 0 ? 2 : attachedCamera.pixelWidth;
        int height = attachedCamera.pixelHeight == 0 ? 2 : attachedCamera.pixelHeight;

        previousGIResult = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
        if (UnityEngine.XR.XRSettings.enabled) previousGIResult.vrUsage = VRTextureUsage.TwoEyes;
        previousGIResult.wrapMode = TextureWrapMode.Clamp;
        previousGIResult.filterMode = FilterMode.Point;
        previousGIResult.Create();
        previousGIResult.hideFlags = HideFlags.HideAndDontSave;

        if (previousDepth)
        {
            //previousDepth.DiscardContents();
            previousDepth.Release();
            //DestroyImmediate(previousDepth);
        }
        previousDepth = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        if (UnityEngine.XR.XRSettings.enabled) previousDepth.vrUsage = VRTextureUsage.TwoEyes;
        previousDepth.wrapMode = TextureWrapMode.Clamp;
        previousDepth.filterMode = FilterMode.Point;
        previousDepth.Create();
        previousDepth.hideFlags = HideFlags.HideAndDontSave;
    }

    void ResizeSunShadowBuffer()
    {

        //sunDepthTexture = new RenderTexture[numClipmaps];
        for (int i = 0; i < sunDepthTexture.Length; i++)
        {
            if (sunDepthTexture[i])
            {
                //sunDepthTexture[i].DiscardContents();
                sunDepthTexture[i].Release();
                //DestroyImmediate(sunDepthTexture[i]);
            }
            sunDepthTexture[i] = new RenderTexture(sunShadowResolution, sunShadowResolution, sunDepthTextureDepth, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            if (UnityEngine.XR.XRSettings.enabled) sunDepthTexture[i].vrUsage = VRTextureUsage.TwoEyes;
            sunDepthTexture[i].wrapMode = TextureWrapMode.Clamp;
            sunDepthTexture[i].filterMode = FilterMode.Point;
            sunDepthTexture[i].Create();
            sunDepthTexture[i].hideFlags = HideFlags.HideAndDontSave;
        }
    }

    void Update()
    {
        calculationSEGIObject = null;
        if (notReadyToRender)
            return;

        if (previousGIResult == null)
        {
            ResizeRenderTextures();
        }

        if (previousGIResult.width != attachedCamera.pixelWidth || previousGIResult.height != attachedCamera.pixelHeight)
        {
            ResizeRenderTextures();
        }

        if ((int)sunShadowResolution != prevSunShadowResolution)
        {
            ResizeSunShadowBuffer();
        }

        prevSunShadowResolution = (int)sunShadowResolution;

        if (clipmaps[0].resolution != (int)voxelResolution)
        {
            clipmaps[0].resolution = (int)voxelResolution;
            clipmaps[0].UpdateTextures();
        }

        if (dummyVoxelTextureAAScaled.width != dummyVoxelResolution)
        {
            ResizeDummyTexture();
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

    bool useUnityShadowMapPrev = false;
    void CheckShadowKeyword(string strKeyword, string strState)
    {
        useUnityShadowMapPrev = useUnityShadowMap;
#if UNITY_EDITOR
        int line_to_edit = 1; // Warning: 1-based indexing!

        string path = "Assets/Plugins/Features/SEGI";

        MonoScript ms = MonoScript.FromScriptableObject(new SEGICascadedPreset());
        path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ms));

        string filePath = path + "/Resources/" + "SEGIUnityShadowInput.cginc";

        // Read the appropriate line from the file.
        string lineToWrite = null;
        using (StreamReader reader = new StreamReader(filePath))
        {
            for (int i = 1; i <= line_to_edit; ++i)
                lineToWrite = reader.ReadLine();
        }
        if (lineToWrite != null)
        {
            if (!lineToWrite.Contains(strKeyword + strState))
                useUnityShadowMapPrev = !useUnityShadowMap;
        }
#endif
    }

    void SetShadowKeyword(string strKeyword, string strState)
    {
#if UNITY_EDITOR
        if (useUnityShadowMap != useUnityShadowMapPrev)
        {
            useUnityShadowMapPrev = useUnityShadowMap;
            string path = "Assets/Plugins/Features/SEGI";

            MonoScript ms = MonoScript.FromScriptableObject(new SEGICascadedPreset());
            path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(ms));

            string filePath = path + "/Resources/" + "SEGIUnityShadowInput.cginc";

            // Read the old file.
            string[] lines = File.ReadAllLines(filePath);

            // Write the new file over the old file.
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                for (int currentLine = 0; currentLine < lines.Length; ++currentLine)
                {
                    if (currentLine == 0)
                    {
                        writer.WriteLine("#define " + strKeyword + strState);
                    }
                    else
                    {
                        writer.WriteLine(lines[currentLine]);
                    }
                }
            }
            AssetDatabase.Refresh();
        }
#endif
    }

    void FixRes(RenderTexture rt)
    {
        if (rt.width != sunShadowResolution || rt.height != sunShadowResolution)
        {
            rt.Release();
            rt.width = rt.height = sunShadowResolution;
            rt.Create();
        }
    }

    public virtual void CustomVoxelScene(Clipmap activeClipmap)
    {
        //this gets overridden by external Voxel-Octree class
    }

    public virtual void CustomSunSetup()
    {
        //this gets overridden by external Voxel-Octree class
    }

    public virtual void OnPreRender()
    {
        //Force reinitialization to make sure that everything is working properly if one of the cameras was unexpectedly destroyed
        if (!voxelCamera || !shadowCam)
            initChecker = null;

        InitCheck();

        if (notReadyToRender)
            return;

        if (!updateGI)
        {
            return;
        }

        //MINE
        updateGIcounter++;
        if (!updateGI || (updateGIevery > 1 && updateGIcounter < updateGIevery))
        {//MINE			
            return;
        }
        else
        {
            updateGIcounter = 0;//MINE
        }

        if (attachedCamera.renderingPath == RenderingPath.Forward)
        {
            if (useReflectionProbes)
            {
                reflectionProbe.enabled = true;
            }
            else
            {
                reflectionProbe.enabled = false;
            }


        }
        else
        {
            reflectionProbe.enabled = false;
        }

        if (useVolumeRayCast != useVolumeRayCastPrev) SetupVolumeRayCasting();

        // only use main camera for voxel simulations
        if (attachedCamera != Camera.main)
        {
            return;
        }

        //Update SkyColor
        if (MatchAmbiantColor)
        {
            skyColor = RenderSettings.ambientLight;
            skyIntensity = RenderSettings.ambientIntensity;
        }

        //calculationSEGIObject = this;
        //Debug.Log(Camera.current.name + "," + Camera.current.stereoActiveEye + ", " + calculationSEGIObject.name + ", " + Time.frameCount + ", " + Time.renderedFrameCount);
        //Cache the previous active render texture to avoid issues with other Unity rendering going on
        RenderTexture previousActive = RenderTexture.active;
        Shader.SetGlobalInt("SEGIVoxelAA", voxelAA ? 1 : 0);

        CustomSunSetup();

        //Temporarily disable rendering of shadows on the directional light during voxelization pass. Cache the result to set it back to what it was after voxelization is done
        LightShadows prevSunShadowSetting = LightShadows.None;
        if (sun != null)
        {
            prevSunShadowSetting = sun.shadows;
            sun.shadows = LightShadows.None;
        }

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
            float clipmapSize = voxelSpaceSize * activeClipmap.localScale;  //Determine the current clipmap's size in world units based on its scale

            //Setup the voxel volume origin position
            float interval = (clipmapSize) / 8.0f;                          //The interval at which the voxel volume will be "locked" in world-space
            Vector3 origin;
            if (followTransform)
            {
                origin = followTransform.position;
            }
            else
            {
                //GI is still flickering a bit when the scene view and the game view are opened at the same time
                origin = transform.position + transform.forward * clipmapSize / 4.0f;
            }
            //Lock the voxel volume origin based on the interval
            activeClipmap.previousOrigin = activeClipmap.origin;
            activeClipmap.origin = new Vector3(Mathf.Round(origin.x / interval) * interval, Mathf.Round(origin.y / interval) * interval, Mathf.Round(origin.z / interval) * interval);


            //Clipmap delta movement for scrolling secondary bounce irradiance volume when this clipmap has changed origin
            activeClipmap.originDelta = activeClipmap.origin - activeClipmap.previousOrigin;
            Shader.SetGlobalVector("SEGIVoxelSpaceOriginDelta", activeClipmap.originDelta / (voxelSpaceSize * activeClipmap.localScale));

            //Calculate the relative origin and overlap/size of the previous cascade as compared to the active cascade. This is used to avoid voxelizing areas that have already been voxelized by previous (smaller) cascades
            Vector3 prevClipmapRelativeOrigin = Vector3.zero;
            float prevClipmapOccupance = 0.0f;
            if (currentClipmapIndex != 0)
            {
                prevClipmapRelativeOrigin = (prevClipmap.origin - activeClipmap.origin) / clipmapSize;
                prevClipmapOccupance = prevClipmap.localScale / activeClipmap.localScale;
            }
            Shader.SetGlobalVector("SEGIClipmapOverlap", new Vector4(prevClipmapRelativeOrigin.x, prevClipmapRelativeOrigin.y, prevClipmapRelativeOrigin.z, prevClipmapOccupance));
            transferIntsCompute.SetVector("SEGIClipmapOverlap", new Vector4(prevClipmapRelativeOrigin.x, prevClipmapRelativeOrigin.y, prevClipmapRelativeOrigin.z, prevClipmapOccupance));

            //Calculate the relative origin and scale of this cascade as compared to the first (level 0) cascade. This is used during GI tracing/data lookup to ensure tracing is done in the correct space
            for (int i = 1; i < numClipmaps; i++)
            {
                Vector3 clipPosFromMaster = Vector3.zero;
                float clipScaleFromMaster = 1.0f;

                clipPosFromMaster = (clipmaps[i].origin - clipmaps[0].origin) / (voxelSpaceSize * clipmaps[i].localScale);
                clipScaleFromMaster = clipmaps[0].localScale / clipmaps[i].localScale;

                Shader.SetGlobalVector("SEGIClipTransform" + i.ToString(), new Vector4(clipPosFromMaster.x, clipPosFromMaster.y, clipPosFromMaster.z, clipScaleFromMaster));
                transferIntsCompute.SetVector("SEGIClipTransform" + i.ToString(), new Vector4(clipPosFromMaster.x, clipPosFromMaster.y, clipPosFromMaster.z, clipScaleFromMaster));
            }

            //Set the voxel camera (proxy camera used to render the scene for voxelization) parameters
            voxelCamera.enabled = false;
            voxelCamera.orthographic = true;
            voxelCamera.orthographicSize = clipmapSize * 0.5f;
            voxelCamera.nearClipPlane = 0.0f;
            voxelCamera.farClipPlane = clipmapSize;
            voxelCamera.depth = -2;
            voxelCamera.renderingPath = RenderingPath.Forward;
            voxelCamera.clearFlags = CameraClearFlags.Color;
            voxelCamera.backgroundColor = Color.black;
            voxelCamera.cullingMask = giCullingMask;

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
            //Shader.SetGlobalMatrix("SEGIVoxelProjectionInverse" + currentClipmapIndex.ToString(), voxelCamera.projectionMatrix.inverse);
            //if (useVolumeRayCast && voxelProjectionInverse != null && voxelProjectionInverse.Length > 0)
            if (useVolumeRayCast)voxelProjectionInverse[currentClipmapIndex] = voxelCamera.projectionMatrix.inverse;

            Shader.SetGlobalMatrix("SEGIVoxelVPFront", GL.GetGPUProjectionMatrix(voxelCamera.projectionMatrix, true) * frontViewMatrix);
            Shader.SetGlobalMatrix("SEGIVoxelVPLeft", GL.GetGPUProjectionMatrix(voxelCamera.projectionMatrix, true) * leftViewMatrix);
            Shader.SetGlobalMatrix("SEGIVoxelVPTop", GL.GetGPUProjectionMatrix(voxelCamera.projectionMatrix, true) * topViewMatrix);

            Shader.SetGlobalMatrix("SEGIWorldToVoxel" + currentClipmapIndex, voxelCamera.worldToCameraMatrix);
            Shader.SetGlobalMatrix("SEGIVoxelProjection" + currentClipmapIndex, voxelCamera.projectionMatrix);
            Shader.SetGlobalVector("SEGISunlightVector", sun ? Vector3.Normalize(sun.transform.forward) : Vector3.up);
            transferIntsCompute.SetVector("SEGISunlightVector", sun ? Vector3.Normalize(sun.transform.forward) : Vector3.up);

            //Set paramteters
            Shader.SetGlobalInt("SEGIVoxelResolution", (int)voxelResolution);

            Shader.SetGlobalColor("GISunColor", sun == null ? Color.black : new Color(Mathf.Pow(sun.color.r, 2.2f), Mathf.Pow(sun.color.g, 2.2f), Mathf.Pow(sun.color.b, 2.2f), Mathf.Pow(sun.intensity, 2.2f)));
            transferIntsCompute.SetVector("GISunColor", sun == null ? Color.black : new Color(Mathf.Pow(sun.color.r, 2.2f), Mathf.Pow(sun.color.g, 2.2f), Mathf.Pow(sun.color.b, 2.2f), Mathf.Pow(sun.intensity, 2.2f)));

            Color skySunColor = sun.color * skyColor; //TODO?

            Shader.SetGlobalColor("SEGISkyColor", new Color(Mathf.Pow(skySunColor.r * skyIntensity * 0.5f, 2.2f), Mathf.Pow(skySunColor.g * skyIntensity * 0.5f, 2.2f), Mathf.Pow(skySunColor.b * skyIntensity * 0.5f, 2.2f), Mathf.Pow(skySunColor.a, 2.2f)));
            transferIntsCompute.SetVector("SEGISkyColor", new Color(Mathf.Pow(skySunColor.r * skyIntensity * 0.5f, 2.2f), Mathf.Pow(skySunColor.g * skyIntensity * 0.5f, 2.2f), Mathf.Pow(skySunColor.b * skyIntensity * 0.5f, 2.2f), Mathf.Pow(skySunColor.a, 2.2f)));

            Shader.SetGlobalFloat("GIGain", giGain);
            Shader.SetGlobalFloat("SEGISecondaryBounceGain", infiniteBounces ? secondaryBounceGain : 0.0f);
            Shader.SetGlobalFloat("SEGISoftSunlight", softSunlight);
            transferIntsCompute.SetFloat("SEGISoftSunlight", softSunlight);

            Shader.SetGlobalInt("SEGISphericalSkylight", sphericalSkylight ? 1 : 0);
            transferIntsCompute.SetInt("SEGISphericalSkylight", sphericalSkylight ? 1 : 0);
            transferIntsCompute.SetInt("VoxelAA", voxelAA ? 3 : 0);

            Shader.SetGlobalInt("SEGIInnerOcclusionLayers", innerOcclusionLayers);

            //Set irradiance "secondary bounce" texture
            Shader.SetGlobalTexture("SEGICurrentIrradianceVolume", irradianceClipmaps[currentClipmapIndex].volumeTexture0);
            Shader.SetGlobalVector("SEGI_GRID_SIZE", vecGridSize);

            minPosVoxel = activeClipmap.origin - Vector3.one * clipmapSize * 0.5f;
            minPosVoxel.w = clipmapSize;
            Shader.SetGlobalVector("SEGIMinPosVoxel", minPosVoxel);

            int kernel = 0;
            if (useVolumeRayCast)//Calculate voxels without Shadows and use result RG0 in VolumeRayCasting
            {
                //Clear the volume texture that is immediately written to in the voxelization scene shader
                Graphics.SetRenderTarget(integerVolumeArray, 0, CubemapFace.Unknown, -1);// -1 = clear all array textures
                GL.Clear(false, true, Color.clear);

                Shader.SetGlobalInt("SEGICurrentClipmapIndex", currentClipmapIndex);


                Graphics.SetRandomWriteTarget(1, integerVolumeArray);
                voxelCamera.targetTexture = dummyVoxelTextureAAScaled;
                voxelCamera.RenderWithShader(voxelizationShaderNoShadows, "");
                Graphics.ClearRandomWriteTargets();

                CustomVoxelScene(activeClipmap);
            }

            //Render the depth texture from the sun's perspective in order to inject sunlight with shadows during voxelization
            if (sun != null)
            {
                //if (currentClipmapIndex <= 2)
                shadowCam.cullingMask = useVolumeRayCast ? shadowVolumeMask : giCullingMask;

                Vector3 shadowCamPosition = activeClipmap.origin + Vector3.Normalize(-sun.transform.forward) * clipmapShadowSize * 0.5f * shadowSpaceDepthRatio;

                shadowCamTransform.position = shadowCamPosition;
                shadowCamTransform.LookAt(activeClipmap.origin, Vector3.up);
                shadowCam.renderingPath = RenderingPath.Forward;
                shadowCam.depthTextureMode |= DepthTextureMode.None;
                shadowCam.orthographicSize = clipmapShadowSize;
                shadowCam.farClipPlane = clipmapShadowSize * 2.0f * shadowSpaceDepthRatio;

                if (useVolumeRayCast && voxelToGIProjection != null && voxelToGIProjection.Length > 0)
                {
                    voxelToGIProjection[currentClipmapIndex] = shadowCam.projectionMatrix * shadowCam.worldToCameraMatrix * voxelCamera.cameraToWorldMatrix;

                    volumeCube.transform.position = activeClipmap.origin;
                    volumeCube.transform.localScale = Vector3.one * clipmapSize;

                    volumeFrontCam.transform.position = shadowCamPosition;
                    volumeFrontCam.transform.LookAt(activeClipmap.origin, Vector3.up);
                    volumeFrontCam.orthographicSize = clipmapShadowSize;
                    volumeFrontCam.farClipPlane = clipmapShadowSize * 2.0f * shadowSpaceDepthRatio;
                    volumeFrontCam.RenderWithShader(frontRayCastShader, "RenderType");

                    volumeBackCam.transform.position = shadowCamPosition;
                    volumeBackCam.transform.LookAt(activeClipmap.origin, Vector3.up);
                    volumeBackCam.orthographicSize = clipmapShadowSize;
                    volumeBackCam.farClipPlane = clipmapShadowSize * 2.0f * shadowSpaceDepthRatio;
                    volumeBackCam.RenderWithShader(backRayCastShader, "RenderType");

                    Shader.SetGlobalTexture("SEGIRG0", integerVolumeArray);
                    //Shader.SetGlobalTexture("SEGIActiveClipmapVolume", activeClipmap.volumeTexture0);
                }
                else
                {
                    var voxelToGIProj = shadowCam.projectionMatrix * shadowCam.worldToCameraMatrix * voxelCamera.cameraToWorldMatrix;
                    Shader.SetGlobalMatrix("SEGIVoxelToGIProjection", voxelToGIProj);
                }

                int currentIndex = useVolumeRayCast ? currentClipmapIndex : 0;

                shadowCam.targetTexture = sunDepthTexture[currentIndex];//TODO use array tex and geo shader with -> uint sliceIndex : SV_RenderTargetArrayIndex
                //shadowCam.SetTargetBuffers(sunDepthTexture[currentIndex].colorBuffer, sunDepthTexture[currentIndex].depthBuffer);
                shadowCam.RenderWithShader(useVolumeRayCast ? sunVolumeRayCastShader : sunDepthShader, useVolumeRayCast ? "RenderType" : "");
                Shader.SetGlobalTexture("SEGISunDepth", sunDepthTexture[currentIndex]);

                if (useUnityShadowMap == false)
                {
                    SetShadowKeyword("SEGI_UNITY_SHADOWMAP_", "OFF");
                }
                else
                {
                    if (m_ShadowmapCopy == null) InitShadowmapCopy();//TODO use NGSS shadowCopy
                    SetShadowKeyword("SEGI_UNITY_SHADOWMAP_", "ON");
                    Shader.SetGlobalTexture("SEGIShadowmapCopy", m_ShadowmapCopy);
                }

                Shader.SetGlobalFloat("SEGIShadowBias", useVolumeRayCast ? -(1 - SEGIShadowBias) : SEGIShadowBias);
            }

            if (useVolumeRayCast == false)
            {
                //Clear the volume texture that is immediately written to in the voxelization scene shader
                Graphics.SetRenderTarget(integerVolumeArray, 0, CubemapFace.Unknown, 0);
                GL.Clear(false, true, Color.clear);

                Graphics.SetRandomWriteTarget(1, integerVolumeArray);
                voxelCamera.targetTexture = dummyVoxelTextureAAScaled;
                voxelCamera.RenderWithShader(voxelizationShader, "");
                Graphics.ClearRandomWriteTargets();

                //Transfer the data from the volume integer texture to the main volume texture used for GI tracing. 
                kernel = 0;
                transferIntsCompute.SetVector("SEGI_GRID_SIZE", vecGridSize);
                transferIntsCompute.SetInt("Resolution", activeClipmap.resolution);
                transferIntsCompute.SetTexture(kernel, "Result", activeClipmap.volumeTexture0);
                transferIntsCompute.SetTexture(kernel, "RG0", integerVolumeArray);
                transferIntsCompute.Dispatch(kernel, activeClipmap.resolution / 8, activeClipmap.resolution / 8, activeClipmap.resolution / 8);
            }
            else // if (voxelProjectionInverse != null && voxelProjectionInverse.Length > 0)//useVolumeRayCast //TODO add unity shadowmap and emission texture
            {
                kernel = 2;

                if (useUnityShadowMap)
                {
                    // Copy directional shadowmap params - they're only set for regular shaders, but we need them in compute
                    if (m_ShadowParamsCB == null)
                        m_ShadowParamsCB = new ComputeBuffer(2, 336);//sizeof(float) * 16 * 4 + sizeof(float) * 4 * 4 + sizeof(float) * 4
                    Graphics.SetRandomWriteTarget(2, m_ShadowParamsCB);
                    m_CopyShadowParamsMaterial.SetPass(0);
                    Graphics.DrawProcedural(MeshTopology.Points, 1);
                    Graphics.ClearRandomWriteTargets();

                    transferIntsCompute.SetVector("SEGIMinPosVoxel", minPosVoxel);
                    transferIntsCompute.SetBuffer(kernel, "_ShadowParams", m_ShadowParamsCB);
                    transferIntsCompute.SetTexture(kernel, "SEGIShadowmapCopy", m_ShadowmapCopy);
                }
                //Transfer the data from the volume integer texture to the main volume texture used for GI tracing and apply shadows here
                transferIntsCompute.SetInt("SEGICurrentClipmapIndex", currentClipmapIndex);
                for (int i = 1; i < numClipmaps; i++)
                {
                    Vector3 clipPosFromMaster = Vector3.zero;
                    float clipScaleFromMaster = 1.0f;
                    clipPosFromMaster = (clipmaps[i].origin - clipmaps[currentClipmapIndex].origin) / (voxelSpaceSize * clipmaps[i].localScale);
                    clipScaleFromMaster = clipmaps[currentClipmapIndex].localScale / clipmaps[i].localScale;

                    transferIntsCompute.SetVector("SEGIShadowClipTransform" + i, new Vector4(clipPosFromMaster.x, clipPosFromMaster.y, clipPosFromMaster.z, clipScaleFromMaster));
                    transferIntsCompute.SetFloats("SEGIVoxelProjectionInverse" + i, MatrixToFloats(voxelProjectionInverse[i]));
                    transferIntsCompute.SetFloats("SEGIVoxelToGIProjection" + i, MatrixToFloats(voxelToGIProjection[i]));
                    transferIntsCompute.SetTexture(kernel, "SEGISunDepth" + i, sunDepthTexture[i]);
                }

                transferIntsCompute.SetFloat("SEGIShadowBias", -(1 - SEGIShadowBias));
                transferIntsCompute.SetFloats("SEGIVoxelProjectionInverse", MatrixToFloats(voxelCamera.projectionMatrix.inverse));
                transferIntsCompute.SetFloats("SEGIVoxelToGIProjection", MatrixToFloats(voxelToGIProjection[currentClipmapIndex]));
                transferIntsCompute.SetTexture(kernel, "SEGISunDepth", sunDepthTexture[currentClipmapIndex]);

                transferIntsCompute.SetFloat("SEGISecondaryBounceGain", infiniteBounces ? secondaryBounceGain : 0.0f);
                transferIntsCompute.SetVector("SEGIVoxelSpaceOriginDelta", activeClipmap.originDelta / (voxelSpaceSize * activeClipmap.localScale));
                transferIntsCompute.SetTexture(kernel, "SEGICurrentIrradianceVolume", irradianceClipmaps[currentClipmapIndex].volumeTexture0);

                transferIntsCompute.SetVector("SEGI_GRID_SIZE", vecGridSize);
                transferIntsCompute.SetInt("Resolution", activeClipmap.resolution);
                transferIntsCompute.SetTexture(kernel, "Result", activeClipmap.volumeTexture0);
                transferIntsCompute.SetTexture(kernel, "RG0", integerVolumeArray);
                transferIntsCompute.Dispatch(kernel, activeClipmap.resolution / 8, activeClipmap.resolution / 8, activeClipmap.resolution / 8);
            }

            //Push current voxelization result to higher levels
            for (int i = 1; i < numClipmaps; i++)
            {
                Clipmap sourceClipmap = clipmaps[i - 1];
                Clipmap targetClipmap = clipmaps[i];

                Vector3 sourceRelativeOrigin = Vector3.zero;
                float sourceOccupance = 0.0f;

                sourceRelativeOrigin = (sourceClipmap.origin - targetClipmap.origin) / (targetClipmap.localScale * voxelSpaceSize);
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


            if (infiniteBounces)
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
            translateToZero = (clipmaps[currentClipmapIndex].origin - clipmaps[0].origin) / (voxelSpaceSize * clipmaps[currentClipmapIndex].localScale);
            float scaleToZero = 1.0f / clipmaps[currentClipmapIndex].localScale;
            Shader.SetGlobalVector("SEGICurrentClipTransform", new Vector4(translateToZero.x, translateToZero.y, translateToZero.z, scaleToZero));
            transferIntsCompute.SetVector("SEGICurrentClipTransform", new Vector4(translateToZero.x, translateToZero.y, translateToZero.z, scaleToZero));

            //Only render infinite bounces for clipmaps 0, 1, and 2
            if (currentClipmapIndex <= 2)
            {
                int kernel = 3;

                //Clear the volume texture that is immediately written to in the voxelization scene shader
                Graphics.SetRenderTarget(integerVolumeArray, 0, CubemapFace.Unknown, 0);
                GL.Clear(false, true, Color.clear);

                if (infiniteBouncesRerenderObjects || !useVolumeRayCast)
                {
                    Shader.SetGlobalInt("SEGISecondaryCones", secondaryCones);
                    Shader.SetGlobalFloat("SEGISecondaryOcclusionStrength", secondaryOcclusionStrength);

                    Graphics.SetRandomWriteTarget(1, integerVolumeArray);
                    voxelCamera.targetTexture = dummyVoxelTextureFixed;
                    voxelCamera.RenderWithShader(voxelTracingShader, "");
                    Graphics.ClearRandomWriteTargets();
                }
                else
                {
                    for (int i = 0; i < numClipmaps; i++)
                    {
                        transferIntsCompute.SetTexture(kernel, "SEGIVolumeLevel" + i, clipmaps[i].volumeTexture0);
                    }

                    transferIntsCompute.SetInt("SEGISecondaryCones", secondaryCones);
                    transferIntsCompute.SetFloat("SEGISecondaryOcclusionStrength", secondaryOcclusionStrength);

                    transferIntsCompute.SetVector("SEGI_GRID_SIZE", vecGridSize);
                    transferIntsCompute.SetInt("Resolution", (int)voxelResolution);
                    transferIntsCompute.SetInt("currentClipmapIndex", currentClipmapIndex);
                    transferIntsCompute.SetTexture(kernel, "RG0", integerVolumeArray);
                    transferIntsCompute.Dispatch(kernel, (int)voxelResolution / 8, (int)voxelResolution / 8, (int)voxelResolution / 8);
                }
                kernel = 1;
                transferIntsCompute.SetVector("SEGI_GRID_SIZE", vecGridSize);
                transferIntsCompute.SetInt("Resolution", (int)voxelResolution);
                transferIntsCompute.SetTexture(kernel, "Result", irradianceClipmaps[currentClipmapIndex].volumeTexture0);
                transferIntsCompute.SetTexture(kernel, "RG0", integerVolumeArray);
                transferIntsCompute.Dispatch(kernel, (int)voxelResolution / 8, (int)voxelResolution / 8, (int)voxelResolution / 8);
            }

            //Increment clipmap counter
            clipmapCounter++;
            if (clipmapCounter >= (int)Mathf.Pow(2.0f, numClipmaps))
            {
                clipmapCounter = 0;
            }

            renderState = RenderState.Voxelize;

        }
        Matrix4x4 giToVoxelProjection = voxelCamera.projectionMatrix * voxelCamera.worldToCameraMatrix * shadowCam.cameraToWorldMatrix;
        Shader.SetGlobalMatrix("GIToVoxelProjection", giToVoxelProjection);

        //Fix stereo rendering matrix
        Camera cam = GetComponent<Camera>();
        if (cam.stereoEnabled)
        {
            // Left and Right Eye inverse View Matrices
            Matrix4x4 leftToWorld = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Left).inverse;
            Matrix4x4 rightToWorld = cam.GetStereoViewMatrix(Camera.StereoscopicEye.Right).inverse;
            material.SetMatrix("_LeftEyeToWorld", leftToWorld);
            material.SetMatrix("_RightEyeToWorld", rightToWorld);

            Matrix4x4 leftEye = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            Matrix4x4 rightEye = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);

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
        if (sun != null)
        {
            sun.shadows = prevSunShadowSetting;
        }
    }

    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (notReadyToRender)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (GetComponent<Camera>().renderingPath == RenderingPath.Forward)
        {
            material.SetInt("ForwardPath", 1);
            material.SetTexture("_Albedo", source);
        }
        else
        {
            material.SetInt("ForwardPath", 0);
        }

        //Set parameters
        Shader.SetGlobalFloat("SEGIVoxelScaleFactor", voxelScaleFactor);//TODO needed?
        Shader.SetGlobalInt("SEGIFrameSwitch", frameCounter);//TODO needed?

        material.SetMatrix("CameraToWorld", attachedCamera.cameraToWorldMatrix);
        material.SetMatrix("WorldToCamera", attachedCamera.worldToCameraMatrix);
        material.SetMatrix("ProjectionMatrixInverse", attachedCamera.projectionMatrix.inverse);
        material.SetMatrix("ProjectionMatrix", attachedCamera.projectionMatrix);
        material.SetInt("FrameSwitch", frameCounter);
        material.SetVector("CameraPosition", transform.position);
        material.SetFloat("DeltaTime", Time.deltaTime);

        material.SetInt("StochasticSampling", stochasticSampling ? 1 : 0);
        material.SetInt("TraceDirections", cones);
        material.SetInt("TraceSteps", coneTraceSteps);
        material.SetFloat("TraceLength", coneLength);
        material.SetFloat("ConeSize", coneWidth);
        material.SetFloat("OcclusionStrength", occlusionStrength);
        material.SetFloat("OcclusionPower", occlusionPower);
        material.SetFloat("ConeTraceBias", coneTraceBias);
        material.SetFloat("GIGain", giGain);
        material.SetFloat("NearLightGain", nearLightGain);
        material.SetFloat("NearOcclusionStrength", nearOcclusionStrength);
        material.SetInt("DoReflections", doReflections ? 1 : 0);
        material.SetInt("GIResolution", GIResolution);
        material.SetInt("ReflectionSteps", reflectionSteps);
        material.SetFloat("ReflectionOcclusionPower", reflectionOcclusionPower);
        material.SetFloat("SkyReflectionIntensity", skyReflectionIntensity);
        material.SetFloat("FarOcclusionStrength", farOcclusionStrength);
        material.SetFloat("FarthestOcclusionStrength", farthestOcclusionStrength);
        material.SetTexture("NoiseTexture", blueNoise[frameCounter]);
        material.SetFloat("BlendWeight", temporalBlendWeight);
        material.SetFloat("noiseDistribution", noiseDistribution);
        material.SetFloat("currentClipmapIndex", currentClipmapIndex);
        material.SetInt("useReflectionProbes", useReflectionProbes ? 1 : 0);

        if (visualizeSunDepthTexture && sunDepthTexture != null && sunDepthTexture[0] != null)//[currentClipmapIndex]?
        {
            Graphics.Blit(sunDepthTexture[0], destination);
            return;
        }
        //If Visualize Shadowmap Copy is enabled, just render the Shadowmap visualization and return
        if (visualizeShadowmapCopy && m_ShadowmapCopy != null)
        {
            //Camera.main.rect = new Rect(0, 0, 0.5f, 0.5f);
            Graphics.Blit(m_ShadowmapCopy, destination);
            //Camera.main.rect = new Rect(0, 0, 1, 1);
            return;
        }

        //If Visualize Voxels is enabled, just render the voxel visualization shader pass and return
        if (visualizeVoxels)
        {
            Graphics.Blit(source, destination, material, Pass.VisualizeVoxels);
            return;
        }

        //Setup temporary textures
        RenderTexture gi1;
        RenderTexture gi2;
        if (UnityEngine.XR.XRSettings.enabled)
        {
            gi1 = RenderTexture.GetTemporary(source.width / giRenderRes, source.height / giRenderRes, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes, true);
            gi2 = RenderTexture.GetTemporary(source.width / giRenderRes, source.height / giRenderRes, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes, true);
        }
        else
        {
            gi1 = RenderTexture.GetTemporary(source.width / giRenderRes, source.height / giRenderRes, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            gi2 = RenderTexture.GetTemporary(source.width / giRenderRes, source.height / giRenderRes, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        }
        RenderTexture reflections = null;

        //If reflections are enabled, create a temporary render buffer to hold them
        if (doReflections)
        {
            if (UnityEngine.XR.XRSettings.enabled)
            {
                reflections = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);
            }
            else
            {
                reflections = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }

        }

        ////Get the camera depth and normals
        //RenderTexture currentDepth = RenderTexture.GetTemporary(source.width / giRenderRes, source.height / giRenderRes, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        //currentDepth.filterMode = FilterMode.Point;

        RenderTexture currentNormal = RenderTexture.GetTemporary(source.width / giRenderRes, source.height / giRenderRes, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        currentNormal.filterMode = FilterMode.Point;
        if (GetComponent<Camera>().renderingPath == RenderingPath.Forward)
        {
            ////Get the camera depth and normals
            //Graphics.Blit(source, currentDepth, material, Pass.GetCameraDepthTexture);//TODO needed?
            //material.SetTexture("CurrentDepth", currentDepth);
            
            Graphics.Blit(source, currentNormal, material, Pass.GetWorldNormals);//TODO needed?
            material.SetTexture("CurrentNormal", currentNormal);
        }

        //Set the previous GI result and camera depth textures to access them in the shader
        material.SetTexture("PreviousGITexture", previousGIResult);
        Shader.SetGlobalTexture("PreviousGITexture", previousGIResult);
        material.SetTexture("PreviousDepth", previousDepth);

        //Render diffuse GI tracing result
        Graphics.Blit(source, gi2, material, Pass.DiffuseTrace);

        if (doReflections)
        {
            //Render GI reflections result
            if (currentClipmapIndex <= 2)
            {
                Graphics.Blit(source, reflections, material, Pass.SpecularTrace);
                material.SetTexture("Reflections", reflections);
            }
        }

        
        //Perform bilateral filtering
        if (useBilateralFiltering && temporalBlendWeight >= 0.99999f)
        {
            material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
            Graphics.Blit(gi2, gi1, material, Pass.BilateralBlur);

            material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
            Graphics.Blit(gi1, gi2, material, Pass.BilateralBlur);

            /*material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
            Graphics.Blit(gi2, gi1, material, Pass.BilateralBlur);

            material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
            Graphics.Blit(gi1, gi2, material, Pass.BilateralBlur);*/
        }

        //If Half Resolution tracing is enabled
        if (giRenderRes >= 2)
        {
            RenderTexture.ReleaseTemporary(gi1);

            //Setup temporary textures
            RenderTexture gi3;
            RenderTexture gi4;
            if (UnityEngine.XR.XRSettings.enabled)
            {
                gi3 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);
                gi4 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);
            }
            else
            {
                gi3 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                gi4 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }

            //Prepare the half-resolution diffuse GI result to be bilaterally upsampled
            gi2.filterMode = FilterMode.Point;
            Graphics.Blit(gi2, gi4);

            RenderTexture.ReleaseTemporary(gi2);

            gi4.filterMode = FilterMode.Point;
            gi3.filterMode = FilterMode.Point;

            //Perform bilateral upsampling on half-resolution diffuse GI result
            material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
            Graphics.Blit(gi4, gi3, material, Pass.BilateralUpsample);
            material.SetVector("Kernel", new Vector2(0.0f, 1.0f));

            //Perform a bilateral blur to be applied in newly revealed areas that are still noisy due to not having previous data blended with it
            RenderTexture blur0;
            RenderTexture blur1;
            if (UnityEngine.XR.XRSettings.enabled)
            {
                blur0 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);
                blur1 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);
            }
            else
            {
                blur0 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                blur1 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }


            material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
            Graphics.Blit(gi3, blur1, material, Pass.BilateralBlur);

            material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
            Graphics.Blit(blur1, blur0, material, Pass.BilateralBlur);

            /*material.SetVector("Kernel", new Vector2(0.0f, 2.0f));
            Graphics.Blit(blur0, blur1, material, Pass.BilateralBlur);

            material.SetVector("Kernel", new Vector2(2.0f, 0.0f));
            Graphics.Blit(blur1, blur0, material, Pass.BilateralBlur);*/

            material.SetTexture("BlurredGI", blur0);
            
            //Perform temporal reprojection and blending
            if (temporalBlendWeight < 1.0f)
            {
                Graphics.Blit(gi3, gi4);
                Graphics.Blit(gi4, gi3, material, Pass.TemporalBlend);
                Graphics.Blit(gi3, previousGIResult);
                Graphics.Blit(source, previousDepth, material, Pass.GetCameraDepthTexture);


                //Perform bilateral filtering on temporally blended result
                if (useBilateralFiltering)
                {
                    material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                    Graphics.Blit(gi3, gi4, material, Pass.BilateralBlur);

                    material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    Graphics.Blit(gi4, gi3, material, Pass.BilateralBlur);

                    /*material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                    Graphics.Blit(gi3, gi4, material, Pass.BilateralBlur);

                    material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    Graphics.Blit(gi4, gi3, material, Pass.BilateralBlur);*/
                }
            }
            /*if (GIResolution >= 3 && GetComponent<Camera>().renderingPath == RenderingPath.DeferredShading)
            {
                GaussianBlur_Material.SetFloat("_Sigma", sigma);
                Graphics.Blit(gi3, gi4, GaussianBlur_Material);
                Graphics.Blit(gi4, gi3, GaussianBlur_Material);
            }*/
            if (GIResolution >= 3)
            {
                material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                Graphics.Blit(gi3, gi4, material, Pass.BilateralBlur);

                material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                Graphics.Blit(gi4, gi3, material, Pass.BilateralBlur);
            }

            //Set the result to be accessed in the shader
            material.SetTexture("GITexture", gi3);

            //Actually apply the GI to the scene using gbuffer data
            Graphics.Blit(source, destination, material, visualizeGI ? Pass.VisualizeGI : Pass.BlendWithScene);

            //Release temporary textures
            RenderTexture.ReleaseTemporary(blur0);
            RenderTexture.ReleaseTemporary(blur1);
            RenderTexture.ReleaseTemporary(gi3);
            RenderTexture.ReleaseTemporary(gi4);
        }
        else    //If Half Resolution tracing is disabled
        {
            
            if (temporalBlendWeight < 1.0f)
            {
                //Perform a bilateral blur to be applied in newly revealed areas that are still noisy due to not having previous data blended with it
                RenderTexture blur0 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);
                RenderTexture blur1 = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Default, 1, RenderTextureMemoryless.None, VRTextureUsage.TwoEyes);

                material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                Graphics.Blit(gi2, blur1, material, Pass.BilateralBlur);

                material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                Graphics.Blit(blur1, blur0, material, Pass.BilateralBlur);

                material.SetVector("Kernel", new Vector2(0.0f, 2.0f));
                Graphics.Blit(blur0, blur1, material, Pass.BilateralBlur);

                material.SetVector("Kernel", new Vector2(2.0f, 0.0f));
                Graphics.Blit(blur1, blur0, material, Pass.BilateralBlur);

                material.SetTexture("BlurredGI", blur0);




                //Perform temporal reprojection and blending
                Graphics.Blit(gi2, gi1, material, Pass.TemporalBlend);
                Graphics.Blit(gi1, previousGIResult);
                Graphics.Blit(source, previousDepth, material, Pass.GetCameraDepthTexture);



                //Perform bilateral filtering on temporally blended result
                if (useBilateralFiltering)
                {
                    material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                    Graphics.Blit(gi1, gi2, material, Pass.BilateralBlur);

                    material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    Graphics.Blit(gi2, gi1, material, Pass.BilateralBlur);

                    /*material.SetVector("Kernel", new Vector2(0.0f, 1.0f));
                    Graphics.Blit(gi1, gi2, material, Pass.BilateralBlur);

                    material.SetVector("Kernel", new Vector2(1.0f, 0.0f));
                    Graphics.Blit(gi2, gi1, material, Pass.BilateralBlur);*/
                }


                RenderTexture.ReleaseTemporary(blur0);
                RenderTexture.ReleaseTemporary(blur1);
            }

            //Actually apply the GI to the scene using gbuffer data
            material.SetTexture("GITexture", gi2);
            Graphics.Blit(source, destination, material, visualizeGI ? Pass.VisualizeGI : Pass.BlendWithScene);

            //Release temporary textures
            RenderTexture.ReleaseTemporary(gi1);
            RenderTexture.ReleaseTemporary(gi2);
        }

        ////Release temporary textures
        //RenderTexture.ReleaseTemporary(currentDepth);
        RenderTexture.ReleaseTemporary(currentNormal);


        //Release the temporary reflections result texture
        if (doReflections)
        {
            RenderTexture.ReleaseTemporary(reflections);
        }

        //Set matrices/vectors for use during temporal reprojection
        material.SetMatrix("ProjectionPrev", attachedCamera.projectionMatrix);
        material.SetMatrix("ProjectionPrevInverse", attachedCamera.projectionMatrix.inverse);
        material.SetMatrix("WorldToCameraPrev", attachedCamera.worldToCameraMatrix);
        material.SetMatrix("CameraToWorldPrev", attachedCamera.cameraToWorldMatrix);
        material.SetVector("CameraPositionPrev", transform.position);

        //Advance the frame counter
        frameCounter = (frameCounter + 1) % (64);
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
