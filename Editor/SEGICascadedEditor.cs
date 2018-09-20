using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Rendering.PostProcessing;
using UnityEditor.Rendering.PostProcessing;

[CustomEditor(typeof(SEGICascaded))]
public class SEGICascadedEditor : Editor
{
    SerializedObject serObj;

    SerializedProperty voxelResolution;
    SerializedProperty visualizeSunDepthTexture;
    SerializedProperty visualizeGI;
    SerializedProperty sun;
    SerializedProperty giCullingMask;
    SerializedProperty shadowVolumeMask;
    SerializedProperty showVolumeObjects;
    SerializedProperty shadowSpaceSize;
    SerializedProperty temporalBlendWeight;
    SerializedProperty visualizeVoxels;
    SerializedProperty visualizeShadowmapCopy;
    SerializedProperty useVolumeRayCast;
    SerializedProperty useUnityShadowMap;
    SerializedProperty shadowmapCopySize;
    SerializedProperty updateGI;
    SerializedProperty MatchAmbientColor;
    SerializedProperty skyColor;
    SerializedProperty voxelSpaceSize;
    SerializedProperty useBilateralFiltering;
    SerializedProperty GIResolution;
    SerializedProperty stochasticSampling;
    SerializedProperty infiniteBounces;
    SerializedProperty infiniteBouncesRerenderObjects;
    SerializedProperty followTransform;
    SerializedProperty noiseDistribution;
    SerializedProperty cones;
    SerializedProperty coneTraceSteps;
    SerializedProperty coneLength;
    SerializedProperty coneWidth;
    SerializedProperty occlusionStrength;
    SerializedProperty nearOcclusionStrength;
    SerializedProperty occlusionPower;
    SerializedProperty coneTraceBias;
    SerializedProperty nearLightGain;
    SerializedProperty giGain;
    SerializedProperty secondaryBounceGain;
    SerializedProperty softSunlight;

    SerializedProperty voxelAA;
    SerializedProperty reflectionSteps;
    SerializedProperty skyReflectionIntensity;
    SerializedProperty reflectionOcclusionPower;
    SerializedProperty farOcclusionStrength;
    SerializedProperty farthestOcclusionStrength;
    SerializedProperty secondaryCones;
    SerializedProperty secondaryOcclusionStrength;
    SerializedProperty skyIntensity;
    SerializedProperty sphericalSkylight;
    SerializedProperty innerOcclusionLayers;
    SerializedProperty sunDepthTextureDepth;
    SerializedProperty useReflectionProbes;
    SerializedProperty reflectionProbeIntensity;
    SerializedProperty reflectionProbeAttribution;
    SerializedProperty reflectionProbeLayerMask;

    SerializedProperty useFXAA;

    SEGICascaded instance;

    string presetPath = "Assets/SEGI/Resources/Cascaded Presets";

    GUIStyle headerStyle;
    GUIStyle vramLabelStyle
    {
        get
        {
            GUIStyle s = new GUIStyle(EditorStyles.boldLabel);
            s.fontStyle = FontStyle.Italic;
            return s;
        }
    }

    static bool showMainConfig = true;
    static bool showForwardConfig = true;
    static bool showVolumeConfig = false;
    static bool showDebugTools = true;
    static bool showTracingProperties = true;
    static bool showEnvironmentProperties = true;
    static bool showPresets = true;
    static bool showReflectionProperties = true;
    static bool showPostEffect = true;

    string presetToSaveName;

    int presetPopupIndex;

    public void OnEnable()
    {
        //Object[] selection = Selection.GetFiltered(typeof(SEGICascadedPreset), SelectionMode.Assets);

        serObj = new SerializedObject(target);

        voxelResolution = serObj.FindProperty("voxelResolution");
        visualizeSunDepthTexture = serObj.FindProperty("visualizeSunDepthTexture");
        visualizeGI = serObj.FindProperty("visualizeGI");
        sun = serObj.FindProperty("sun");
        giCullingMask = serObj.FindProperty("giCullingMask");
        shadowVolumeMask = serObj.FindProperty("shadowVolumeMask");
        showVolumeObjects = serObj.FindProperty("showVolumeObjects");
        shadowSpaceSize = serObj.FindProperty("shadowSpaceSize");
        temporalBlendWeight = serObj.FindProperty("temporalBlendWeight");
        visualizeVoxels = serObj.FindProperty("visualizeVoxels");
        visualizeShadowmapCopy = serObj.FindProperty("visualizeShadowmapCopy");
        useVolumeRayCast = serObj.FindProperty("useVolumeRayCast");
        useUnityShadowMap = serObj.FindProperty("useUnityShadowMap");
        shadowmapCopySize = serObj.FindProperty("shadowmapCopySize");
        updateGI = serObj.FindProperty("updateGI");
        MatchAmbientColor = serObj.FindProperty("MatchAmbiantColor");
        skyColor = serObj.FindProperty("skyColor");
        voxelSpaceSize = serObj.FindProperty("voxelSpaceSize");
        useBilateralFiltering = serObj.FindProperty("useBilateralFiltering");
        GIResolution = serObj.FindProperty("GIResolution");
        stochasticSampling = serObj.FindProperty("stochasticSampling");
        infiniteBounces = serObj.FindProperty("infiniteBounces");
        infiniteBouncesRerenderObjects = serObj.FindProperty("infiniteBouncesRerenderObjects");
        followTransform = serObj.FindProperty("followTransform");
        noiseDistribution = serObj.FindProperty("noiseDistribution");
        cones = serObj.FindProperty("cones");
        coneTraceSteps = serObj.FindProperty("coneTraceSteps");
        coneLength = serObj.FindProperty("coneLength");
        coneWidth = serObj.FindProperty("coneWidth");
        occlusionStrength = serObj.FindProperty("occlusionStrength");
        nearOcclusionStrength = serObj.FindProperty("nearOcclusionStrength");
        occlusionPower = serObj.FindProperty("occlusionPower");
        coneTraceBias = serObj.FindProperty("coneTraceBias");
        nearLightGain = serObj.FindProperty("nearLightGain");
        giGain = serObj.FindProperty("giGain");
        secondaryBounceGain = serObj.FindProperty("secondaryBounceGain");
        softSunlight = serObj.FindProperty("softSunlight");
        voxelAA = serObj.FindProperty("voxelAA");
        reflectionSteps = serObj.FindProperty("reflectionSteps");
        skyReflectionIntensity = serObj.FindProperty("skyReflectionIntensity");
        //gaussianMipFilter = serObj.FindProperty("gaussianMipFilter");
        reflectionOcclusionPower = serObj.FindProperty("reflectionOcclusionPower");
        farOcclusionStrength = serObj.FindProperty("farOcclusionStrength");
        farthestOcclusionStrength = serObj.FindProperty("farthestOcclusionStrength");
        secondaryCones = serObj.FindProperty("secondaryCones");
        secondaryOcclusionStrength = serObj.FindProperty("secondaryOcclusionStrength");
        skyIntensity = serObj.FindProperty("skyIntensity");
        sphericalSkylight = serObj.FindProperty("sphericalSkylight");
        innerOcclusionLayers = serObj.FindProperty("innerOcclusionLayers");
        sunDepthTextureDepth = serObj.FindProperty("sunDepthTextureDepth");
        useReflectionProbes = serObj.FindProperty("useReflectionProbes");
        reflectionProbeIntensity = serObj.FindProperty("reflectionProbeIntensity");
        reflectionProbeAttribution = serObj.FindProperty("reflectionProbeAttribution");
        reflectionProbeLayerMask = serObj.FindProperty("reflectionProbeLayerMask");
        useFXAA = serObj.FindProperty("useFXAA");


        instance = target as SEGICascaded;
    }

    public override void OnInspectorGUI()
    {
        serObj.Update();

        EditorGUILayout.HelpBox("This is a preview of the work-in-progress version of SEGI with cascaded GI volumes. Behavior is not final and is subject to change.", MessageType.Info);

        //Presets
        showPresets = EditorGUILayout.Foldout(showPresets, new GUIContent("Presets"));
        if (showPresets)
        {
            string path = "Assets/SEGI";
            //#if UNITY_EDITOR
            //MonoScript ms = MonoScript.FromScriptableObject(new SEGICascadedPreset());
            //path = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(ms));
            //#endif
            presetPath = path + "/Resources/Cascaded Presets";
            EditorGUI.indentLevel++;
            string[] presetGUIDs = AssetDatabase.FindAssets("t:SEGICascadedPreset", new string[1] { presetPath });
            string[] presetNames = new string[presetGUIDs.Length];
            string[] presetPaths = new string[presetGUIDs.Length];

            for (int i = 0; i < presetGUIDs.Length; i++)
            {
                presetPaths[i] = AssetDatabase.GUIDToAssetPath(presetGUIDs[i]);
                presetNames[i] = System.IO.Path.GetFileNameWithoutExtension(presetPaths[i]);
            }

            EditorGUILayout.BeginHorizontal();
            presetPopupIndex = EditorGUILayout.Popup("", presetPopupIndex, presetNames);

            if (GUILayout.Button("Load"))
            {
                if (presetPaths.Length > 0)
                {
                    SEGICascadedPreset preset = AssetDatabase.LoadAssetAtPath<SEGICascadedPreset>(presetPaths[presetPopupIndex]);
                    instance.ApplyPreset(preset);
                    EditorUtility.SetDirty(target);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            presetToSaveName = EditorGUILayout.TextField(presetToSaveName);

            if (GUILayout.Button("Save"))
            {
                SavePreset(presetToSaveName);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        //Main Configuration
        showMainConfig = EditorGUILayout.Foldout(showMainConfig, new GUIContent("Main Configuration"));
        if (showMainConfig)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical();
            EditorGUILayout.PropertyField(voxelResolution, new GUIContent("Voxel Resolution", "The resolution of the voxel texture used to calculate GI."));
            EditorGUILayout.PropertyField(voxelAA, new GUIContent("Voxel AA", "Enables anti-aliasing during voxelization for higher precision voxels."));
            EditorGUILayout.PropertyField(innerOcclusionLayers, new GUIContent("Inner Occlusion Layers", "Enables the writing of additional black occlusion voxel layers on the back face of geometry. Can help with light leaking but may cause artifacts with small objects."));
            //EditorGUILayout.PropertyField(gaussianMipFilter, new GUIContent("Gaussian Mip Filter", "Enables gaussian filtering during mipmap generation. This can improve visual smoothness and consistency, particularly with large moving objects."));
            EditorGUILayout.PropertyField(voxelSpaceSize, new GUIContent("Voxel Space Size", "The size of the voxel volume in world units. Everything inside the voxel volume will contribute to GI."));
            EditorGUILayout.PropertyField(shadowSpaceSize, new GUIContent("Shadow Space Size", "The size of the sun shadow texture used to inject sunlight with shadows into the voxels in world units. It is recommended to set this value similar to Voxel Space Size."));
            EditorGUILayout.PropertyField(giCullingMask, new GUIContent("GI Culling Mask", "Which layers should be voxelized and contribute to GI."));
            EditorGUILayout.PropertyField(useVolumeRayCast, new GUIContent("Use VolumeRayCasting", "If enabled, VolumeRayCasting shadows will be used."));
            if(!instance.useVolumeRayCast) GUI.enabled = false;
            showVolumeConfig = EditorGUILayout.Foldout(showVolumeConfig, new GUIContent("VolumeRayCasting Configuration"));
            if (showVolumeConfig)
            {
                EditorGUILayout.PropertyField(showVolumeObjects, new GUIContent("Show Volume Cube", "Show Cube that gets used for VolumeRayCasting in Hierarchy (Runtime)."));
                EditorGUILayout.PropertyField(shadowVolumeMask, new GUIContent("Volume Culling Mask", "Which layer has the Volume Object in it."));
            }
            GUI.enabled = true;
            EditorGUILayout.PropertyField(useUnityShadowMap, new GUIContent("Use Unity ShadowMap", "If enabled, unity's shadowmap will be used instead of creating new onces."));
            GUI.enabled = false;
            if (instance.useUnityShadowMap) {
                GUI.enabled = true;
                EditorGUILayout.HelpBox("This currently works only with no-cascades shadow (one shadowmap).", MessageType.Info);
            }
            EditorGUILayout.PropertyField(shadowmapCopySize, new GUIContent("Set Unity ShadowMapCopy Size", "Sets the size of the copied shadowmap."));
            GUI.enabled = true;
            EditorGUILayout.PropertyField(updateGI, new GUIContent("Update GI", "Whether voxelization and multi-bounce rendering should update every frame. When disabled, GI tracing will use cached data from the last time this was enabled."));
            EditorGUILayout.PropertyField(infiniteBounces, new GUIContent("Infinite Bounces", "Enables infinite bounces. This is expensive for complex scenes and is still experimental."));
            EditorGUILayout.PropertyField(infiniteBouncesRerenderObjects, new GUIContent("Infinite Bounces Rerender Objects", "This re-renders the scene via the voxel camera. If disabled data will be reused to calc bounces."));
            EditorGUILayout.PropertyField(followTransform, new GUIContent("Follow Transform", "If provided, the voxel volume will follow and be centered on this object instead of the camera. Useful for top-down scenes."));
            EditorGUILayout.PropertyField(sunDepthTextureDepth, new GUIContent("SunTexture Depth Bits", "Set the depth of the shadow texture(s)."));        
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VRAM Usage: " + instance.vramUsage.ToString("F2") + " MB", vramLabelStyle);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        //Forward
        showForwardConfig = EditorGUILayout.Foldout(showForwardConfig, new GUIContent("Forward Rendering Configuration"));
        if (showForwardConfig)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(useReflectionProbes, new GUIContent("Use Reflection Probe", "Approximates path traced Specular values using a Reflection Probe."));
            EditorGUILayout.PropertyField(reflectionProbeIntensity, new GUIContent("Reflection Probe Intensity", "Intensity of Reflection Probe influence."));
            EditorGUILayout.PropertyField(reflectionProbeAttribution, new GUIContent("Reflection Probe Attribution", "How much Reflection Probes contribute to GI"));
            EditorGUILayout.PropertyField(reflectionProbeLayerMask, new GUIContent("Reflection Probe Layer Mask", "Enables the writing of additional black occlusio"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();


        //Environment
        showEnvironmentProperties = EditorGUILayout.Foldout(showEnvironmentProperties, new GUIContent("Environment Properties"));
        if (instance.sun == null)
        {
            showEnvironmentProperties = true;
        }
        if (showEnvironmentProperties)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(sun, new GUIContent("Sun", "The main directional light that will cast indirect light into the scene (sunlight or moonlight)."));
            EditorGUILayout.PropertyField(softSunlight, new GUIContent("Soft Sunlight", "The amount of soft diffuse sunlight that will be added to the scene. Use this to simulate the effect of clouds/haze scattering soft sunlight onto the scene."));
            EditorGUILayout.PropertyField(MatchAmbientColor, new GUIContent("Match Scene Lighting", "Sync Sky Color and intensity to scene lighting"));
            EditorGUILayout.PropertyField(skyColor, new GUIContent("Sky Color", "The color of the light scattered onto the scene coming from the sky."));
            EditorGUILayout.PropertyField(skyIntensity, new GUIContent("Sky Intensity", "The brightness of the sky light."));
            EditorGUILayout.PropertyField(sphericalSkylight, new GUIContent("Spherical Skylight", "If enabled, light from the sky will come from all directions. If disabled, light from the sky will only come from the top hemisphere."));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();


        //Tracing properties
        showTracingProperties = EditorGUILayout.Foldout(showTracingProperties, new GUIContent("Tracing Properties"));
        if (showTracingProperties)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(temporalBlendWeight, new GUIContent("Temporal Blend Weight", "The lower the value, the more previous frames will be blended with the current frame. Lower values result in smoother GI that updates less quickly."));
            //EditorGUILayout.PropertyField(useBilateralFiltering, new GUIContent("Bilateral Filtering", "Enables filtering of the GI result to reduce noise."));
            EditorGUILayout.PropertyField(stochasticSampling, new GUIContent("Stochastic Sampling", "If enabled, uses random jitter to reduce banding and discontinuities during GI tracing."));
            EditorGUILayout.PropertyField(GIResolution, new GUIContent("Subsampling Resolution", "GI tracing resolution will be subsampled at this screen resolution. Improves speed of GI tracing."));

            EditorGUILayout.PropertyField(noiseDistribution, new GUIContent("Noise Distribution", "Lower values increase performance at expense of lowered shadow resolution"));
            EditorGUILayout.PropertyField(cones, new GUIContent("Cones", "The number of cones that will be traced in different directions for diffuse GI tracing. More cones result in a smoother result at the cost of performance."));
            EditorGUILayout.PropertyField(coneTraceSteps, new GUIContent("Cone Trace Steps", "The number of tracing steps for each cone. Too few results in skipping thin features. Higher values result in more accuracy at the cost of performance."));
            EditorGUILayout.PropertyField(coneLength, new GUIContent("Cone length", "The number of cones that will be traced in different directions for diffuse GI tracing. More cones result in a smoother result at the cost of performance."));
            EditorGUILayout.PropertyField(coneWidth, new GUIContent("Cone Width", "The width of each cone. Wider cones cause a softer and smoother result but affect accuracy and incrase over-occlusion. Thinner cones result in more accurate tracing with less coherent (more noisy) results and a higher tracing cost."));
            EditorGUILayout.PropertyField(coneTraceBias, new GUIContent("Cone Trace Bias", "The amount of offset above a surface that cone tracing begins. Higher values reduce \"voxel acne\" (similar to \"shadow acne\"). Values that are too high result in light-leaking."));
            EditorGUILayout.PropertyField(occlusionStrength, new GUIContent("Occlusion Strength", "The strength of shadowing solid objects will cause. Affects the strength of all indirect shadows."));
            EditorGUILayout.PropertyField(nearOcclusionStrength, new GUIContent("Near Occlusion Strength", "The strength of shadowing nearby solid objects will cause. Only affects the strength of very close blockers."));
            EditorGUILayout.PropertyField(farOcclusionStrength, new GUIContent("Far Occlusion Strength", "How much light far occluders block. This value gives additional light blocking proportional to the width of the cone at each trace step."));
            EditorGUILayout.PropertyField(farthestOcclusionStrength, new GUIContent("Farthest Occlusion Strength", "How much light the farthest occluders block. This value gives additional light blocking proportional to (cone width)^2 at each trace step."));
            EditorGUILayout.PropertyField(occlusionPower, new GUIContent("Occlusion Power", "The strength of shadowing far solid objects will cause. Only affects the strength of far blockers. Decrease this value if wide cones are causing over-occlusion."));
            EditorGUILayout.PropertyField(nearLightGain, new GUIContent("Near Light Gain", "Affects the attenuation of indirect light. Higher values allow for more close-proximity indirect light. Lower values reduce close-proximity indirect light, sometimes resulting in a cleaner result."));
            EditorGUILayout.PropertyField(giGain, new GUIContent("GI Gain", "The overall brightness of indirect light. For Near Light Gain values around 1, a value of 1 for this property is recommended for a physically-accurate result."));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(secondaryBounceGain, new GUIContent("Secondary Bounce Gain", "Affects the strength of secondary/infinite bounces. Be careful, values above 1 can cause runaway light bouncing and flood areas with extremely bright light!"));
            EditorGUILayout.PropertyField(secondaryCones, new GUIContent("Secondary Cones", "The number of secondary cones that will be traced for calculating infinte bounces. Increasing this value improves the accuracy of secondary bounces at the cost of performance. Note: the performance cost of this scales with voxelized scene complexity."));
            EditorGUILayout.PropertyField(secondaryOcclusionStrength, new GUIContent("Secondary Occlusion Strength", "The strength of light blocking during secondary bounce tracing. Be careful, a value too low can cause runaway light bouncing and flood areas with extremely bright light!"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        
		showReflectionProperties = EditorGUILayout.Foldout(showReflectionProperties, new GUIContent("Reflection Properties"));
		if (showReflectionProperties)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.PropertyField(reflectionSteps, new GUIContent("Reflection Steps", "Number of reflection trace steps."));
			EditorGUILayout.PropertyField(reflectionOcclusionPower, new GUIContent("Reflection Occlusion Power", "Strength of light blocking during reflection tracing."));
			EditorGUILayout.PropertyField(skyReflectionIntensity, new GUIContent("Sky Reflection Intensity", "Intensity of sky reflections."));
			EditorGUI.indentLevel--;
		}

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        showPostEffect = EditorGUILayout.Foldout(showPostEffect, new GUIContent("Post Processing"));
        if (showReflectionProperties)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(useFXAA, new GUIContent("FXAA", "Apply FXAA Anti-aliasing to the final image."));
            EditorGUI.indentLevel--;
        }


        EditorGUILayout.Space();
        EditorGUILayout.Space();

        //Debug tools
        showDebugTools = EditorGUILayout.Foldout(showDebugTools, new GUIContent("Debug Tools"));
        if (showDebugTools)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(visualizeSunDepthTexture, new GUIContent("Visualize Sun Depth Texture", "Visualize the depth texture used to render proper shadows while injecting sunlight into voxel data."));
            EditorGUILayout.PropertyField(visualizeGI, new GUIContent("Visualize GI", "Visualize GI result only (no textures)."));
            EditorGUILayout.PropertyField(visualizeVoxels, new GUIContent("Visualize Voxels", "Directly view the voxels in the scene."));
            EditorGUILayout.PropertyField(visualizeShadowmapCopy, new GUIContent("Visualize Shadowmap Copy", "Directly view the Shadowmap of the Sun Light."));

            EditorGUI.indentLevel--;
        }


        serObj.ApplyModifiedProperties();
    }

    void SavePreset(string name)
    {
        if (name == "")
        {
            Debug.LogWarning("SEGI: Type in a name for the preset to be saved!");
            return;
        }

        //SEGIPreset preset = new SEGIPreset();
        SEGICascadedPreset preset = ScriptableObject.CreateInstance<SEGICascadedPreset>();

        preset.voxelResolution = instance.voxelResolution;
        preset.voxelAA = instance.voxelAA;
        preset.innerOcclusionLayers = instance.innerOcclusionLayers;
        preset.infiniteBounces = instance.infiniteBounces;

        preset.temporalBlendWeight = instance.temporalBlendWeight;
        preset.useBilateralFiltering = instance.useBilateralFiltering;
        preset.GIResolution = instance.GIResolution;
        preset.stochasticSampling = instance.stochasticSampling;

        preset.noiseDistribution = instance.noiseDistribution;
        preset.cones = instance.cones;
        preset.coneTraceSteps = instance.coneTraceSteps;
        preset.coneLength = instance.coneLength;
        preset.coneWidth = instance.coneWidth;
        preset.coneTraceBias = instance.coneTraceBias;
        preset.occlusionStrength = instance.occlusionStrength;
        preset.nearOcclusionStrength = instance.nearOcclusionStrength;
        preset.occlusionPower = instance.occlusionPower;
        preset.nearLightGain = instance.nearLightGain;
        preset.giGain = instance.giGain;
        preset.secondaryBounceGain = instance.secondaryBounceGain;

        preset.reflectionSteps = instance.reflectionSteps;
        preset.reflectionOcclusionPower = instance.reflectionOcclusionPower;
        preset.skyReflectionIntensity = instance.skyReflectionIntensity;
        preset.gaussianMipFilter = instance.gaussianMipFilter;

        preset.farOcclusionStrength = instance.farOcclusionStrength;
        preset.farthestOcclusionStrength = instance.farthestOcclusionStrength;
        preset.secondaryCones = instance.secondaryCones;
        preset.secondaryOcclusionStrength = instance.secondaryOcclusionStrength;

        preset.useReflectionProbes = instance.useReflectionProbes;
        preset.reflectionProbeIntensity = instance.reflectionProbeIntensity;
        preset.reflectionProbeAttribution = instance.reflectionProbeAttribution;

        preset.useFXAA = instance.useFXAA;

        string path = "Assets/Plugins/Features/SEGI";
#if UNITY_EDITOR
        MonoScript ms = MonoScript.FromScriptableObject(preset);
        path = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(ms));
#endif
        presetPath = path + "/Resources/Cascaded Presets";
        path = presetPath + "/";

        AssetDatabase.CreateAsset(preset, path + name + ".asset");

        AssetDatabase.SaveAssets();
    }

    void LoadPreset()
    {

    }
}

[PostProcessEditor(typeof(SEGICascadedPreset))]
public sealed class SEGICascadedEditorSRP : PostProcessEffectEditor<SEGICascadedPreset>
{
    SerializedObject serObj;

    SerializedParameterOverride voxelResolution;
    SerializedProperty visualizeSunDepthTexture;
    SerializedProperty visualizeGI;
    SerializedProperty sun;
    SerializedProperty giCullingMask;
    SerializedProperty shadowVolumeMask;
    SerializedProperty showVolumeObjects;
    SerializedProperty shadowSpaceSize;
    SerializedProperty temporalBlendWeight;
    SerializedProperty visualizeVoxels;
    SerializedProperty visualizeShadowmapCopy;
    SerializedProperty useVolumeRayCast;
    SerializedProperty useUnityShadowMap;
    SerializedProperty shadowmapCopySize;
    SerializedProperty updateGI;
    SerializedProperty MatchAmbientColor;
    SerializedProperty skyColor;
    SerializedProperty voxelSpaceSize;
    SerializedProperty useBilateralFiltering;
    SerializedProperty GIResolution;
    SerializedProperty stochasticSampling;
    SerializedProperty infiniteBounces;
    SerializedProperty infiniteBouncesRerenderObjects;
    SerializedProperty followTransform;
    SerializedProperty noiseDistribution;
    SerializedProperty cones;
    SerializedProperty coneTraceSteps;
    SerializedProperty coneLength;
    SerializedProperty coneWidth;
    SerializedProperty occlusionStrength;
    SerializedProperty nearOcclusionStrength;
    SerializedProperty occlusionPower;
    SerializedProperty coneTraceBias;
    SerializedProperty nearLightGain;
    SerializedProperty giGain;
    SerializedProperty secondaryBounceGain;
    SerializedProperty softSunlight;

    SerializedProperty voxelAA;
    SerializedProperty reflectionSteps;
    SerializedProperty skyReflectionIntensity;
    SerializedProperty reflectionOcclusionPower;
    SerializedProperty farOcclusionStrength;
    SerializedProperty farthestOcclusionStrength;
    SerializedProperty secondaryCones;
    SerializedProperty secondaryOcclusionStrength;
    SerializedProperty skyIntensity;
    SerializedProperty sphericalSkylight;
    SerializedProperty innerOcclusionLayers;
    SerializedProperty sunDepthTextureDepth;
    SerializedProperty useReflectionProbes;
    SerializedProperty reflectionProbeIntensity;
    SerializedProperty reflectionAttribution;
    SerializedProperty reflectionProbeLayerMask;

    SerializedProperty useFXAA;
}
