using System;
using AL.Motion;
using UnityEditor;
using UnityEngine;

namespace AL.Editor.Motion
{
    public static class MotionConfigurationBuilder
    {
        public const string ProfileRoot = "Assets/AL/Resources/Motion/Profiles";
        public const string PresetRoot = "Assets/AL/Editor/Motion/ImportPresets";
        public const string RegistryPath =
            "Assets/AL/Editor/Motion/MotionImportPresetRegistry.asset";
        public const string RequiredMotionManifestPath =
            "Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json";

        [MenuItem("Another Life/Motion/Build Default Runtime And Import Profiles")]
        public static void BuildForCli()
        {
            EnsureFolder(ProfileRoot);
            EnsureFolder(PresetRoot);

            ConfigureProfile(
                ProfileRoot + "/ChampionMotionControllerProfile.asset",
                "rmc_runtime_champion_mobile_v001",
                MotionSubjectKind.Champion,
                MotionRigClassification.Humanoid,
                "rmc_skeleton_humanoid_shared_v001",
                "rmc_retarget_humanoid_shared_v001",
                "rmc_bind_humanoid_a_pose_v001",
                "rmc_layers_champion_mobile_v001",
                4,
                new[]
                {
                    "rmc_layer_champion_base_v001",
                    "rmc_layer_champion_upper_action_v001",
                    "rmc_layer_champion_aim_look_v001",
                    "rmc_layer_champion_reaction_v001"
                },
                new[] { false, false, true, true },
                new[] { 0, 20, 30, 40 });
            ConfigureProfile(
                ProfileRoot + "/NpcMotionControllerProfile.asset",
                "rmc_runtime_npc_mobile_v001",
                MotionSubjectKind.Npc,
                MotionRigClassification.Humanoid,
                "rmc_skeleton_humanoid_shared_v001",
                "rmc_retarget_humanoid_shared_v001",
                "rmc_bind_humanoid_a_pose_v001",
                "rmc_layers_npc_mobile_v001",
                3,
                new[]
                {
                    "rmc_layer_npc_base_v001",
                    "rmc_layer_npc_upper_action_v001",
                    "rmc_layer_npc_look_reaction_v001"
                },
                new[] { false, false, true },
                new[] { 0, 20, 30 });
            ConfigureProfile(
                ProfileRoot + "/BeastMotionControllerProfile.asset",
                "rmc_runtime_beast_mobile_v001",
                MotionSubjectKind.Beast,
                MotionRigClassification.Generic,
                "rmc_skeleton_nonhumanoid_grounded_v001",
                "rmc_retarget_generic_exact_v001",
                "rmc_bind_nonhumanoid_neutral_contact_v001",
                "rmc_layers_beast_mobile_v001",
                2,
                new[]
                {
                    "rmc_layer_beast_base_v001",
                    "rmc_layer_beast_reaction_v001"
                },
                new[] { false, true },
                new[] { 0, 40 });

            ConfigurePreset(
                PresetRoot + "/HumanoidMotionImportPreset.asset",
                "rmc_import_humanoid_shared_v001",
                "rmc_skeleton_humanoid_shared_v001",
                "rmc_retarget_humanoid_shared_v001",
                MotionRigClassification.Humanoid,
                MotionRetargetMode.UnityHumanoid,
                true,
                false);
            ConfigurePreset(
                PresetRoot + "/GenericExactMotionImportPreset.asset",
                "rmc_import_generic_exact_v001",
                "rmc_skeleton_nonhumanoid_grounded_v001",
                "rmc_retarget_generic_exact_v001",
                MotionRigClassification.Generic,
                MotionRetargetMode.GenericExactSignature,
                false,
                false);
            ConfigurePreset(
                PresetRoot + "/SlagwhistleExactMotionImportPreset.asset",
                "rmc_import_slagwhistle_exact_v001",
                "rmc_skeleton_nonhumanoid_grounded_v001",
                "rmc_retarget_slagwhistle_exact_v001",
                MotionRigClassification.Generic,
                MotionRetargetMode.GenericExactSignature,
                false,
                false);

            MotionImportPresetRegistry registry =
                CreateOrLoad<MotionImportPresetRegistry>(RegistryPath);
            var registryObject = new SerializedObject(registry);
            registryObject.FindProperty("bindings").arraySize = 0;
            registryObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[AL-MOTION-CONFIG] generated champion, NPC, beast, and import profiles; " +
                "registry remains empty until exact remediated source paths are admitted.");
        }

        private static void ConfigureProfile(
            string path,
            string profileId,
            MotionSubjectKind subjectKind,
            MotionRigClassification rigClassification,
            string skeletonProfileId,
            string retargetProfileId,
            string bindPoseId,
            string layerProfileId,
            int maximumLayers,
            string[] layerIds,
            bool[] additive,
            int[] priorities)
        {
            MotionControllerProfile profile = CreateOrLoad<MotionControllerProfile>(path);
            var serialized = new SerializedObject(profile);
            SetString(serialized, "standardId", "rmc_standard_rig_motion_v001");
            SetString(serialized, "profileId", profileId);
            serialized.FindProperty("subjectKind").intValue = (int)subjectKind;
            serialized.FindProperty("rigClassification").intValue = (int)rigClassification;
            SetString(serialized, "skeletonProfileId", skeletonProfileId);
            SetString(serialized, "retargetProfileId", retargetProfileId);
            SetString(serialized, "bindPoseId", bindPoseId);
            SetString(serialized, "layerProfileId", layerProfileId);
            SetString(serialized, "safeMotionKey", "idle.neutral");
            TextAsset requiredMotionManifest =
                AssetDatabase.LoadAssetAtPath<TextAsset>(RequiredMotionManifestPath);
            if (requiredMotionManifest == null)
            {
                throw new InvalidOperationException(
                    "Required motion manifest is missing: " + RequiredMotionManifestPath);
            }

            serialized.FindProperty("requiredMotionManifest").objectReferenceValue =
                requiredMotionManifest;
            serialized.FindProperty("maximumLayers").intValue = maximumLayers;
            SerializedProperty layers = serialized.FindProperty("layers");
            layers.arraySize = layerIds.Length;
            for (int index = 0; index < layerIds.Length; index++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(index);
                layer.FindPropertyRelative("layerId").stringValue = layerIds[index];
                layer.FindPropertyRelative("additive").boolValue = additive[index];
                layer.FindPropertyRelative("mask").objectReferenceValue = null;
                layer.FindPropertyRelative("priority").intValue = priorities[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            if (!profile.HasValidTechnicalIdentity())
            {
                throw new InvalidOperationException("Generated motion profile is invalid: " + path);
            }
        }

        private static void ConfigurePreset(
            string path,
            string presetId,
            string skeletonProfileId,
            string retargetProfileId,
            MotionRigClassification rigClassification,
            MotionRetargetMode retargetMode,
            bool importBlendShapes,
            bool optimizeGameObjects)
        {
            MotionImportPreset preset = CreateOrLoad<MotionImportPreset>(path);
            var serialized = new SerializedObject(preset);
            SetString(serialized, "presetId", presetId);
            SetString(serialized, "skeletonProfileId", skeletonProfileId);
            SetString(serialized, "retargetProfileId", retargetProfileId);
            serialized.FindProperty("rigClassification").intValue = (int)rigClassification;
            serialized.FindProperty("retargetMode").intValue = (int)retargetMode;
            serialized.FindProperty("sampleRateHz").intValue = 30;
            serialized.FindProperty("globalScale").floatValue = 1f;
            serialized.FindProperty("bakeAxisConversion").boolValue = true;
            serialized.FindProperty("preserveHierarchy").boolValue = true;
            serialized.FindProperty("importBlendShapes").boolValue = importBlendShapes;
            serialized.FindProperty("optimizeGameObjects").boolValue = optimizeGameObjects;
            serialized.FindProperty("rotationError").floatValue = 0.25f;
            serialized.FindProperty("positionError").floatValue = 0.5f;
            serialized.FindProperty("scaleError").floatValue = 0.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preset);
            if (!preset.HasValidTechnicalIdentity())
            {
                throw new InvalidOperationException("Generated motion import preset is invalid: " + path);
            }
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void SetString(SerializedObject serialized, string name, string value)
        {
            serialized.FindProperty(name).stringValue = value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string child = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
