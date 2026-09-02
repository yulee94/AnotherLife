using System;
using System.Linq;
using AL.Motion;
using UnityEditor;
using UnityEngine;

namespace AL.Editor.Motion
{
    public sealed class MotionModelImportPostprocessor : AssetPostprocessor
    {
        private const string RegistryPath =
            "Assets/AL/Editor/Motion/MotionImportPresetRegistry.asset";

        private void OnPreprocessModel()
        {
            MotionImportPresetRegistry registry =
                AssetDatabase.LoadAssetAtPath<MotionImportPresetRegistry>(RegistryPath);
            if (registry == null ||
                !registry.TryResolve(assetPath, out MotionImportBinding binding))
            {
                return;
            }

            registry.ValidateOrThrow();
            Apply((ModelImporter)assetImporter, binding);
        }

        public static void Apply(ModelImporter importer, MotionImportBinding binding)
        {
            if (importer == null)
            {
                throw new ArgumentNullException(nameof(importer));
            }

            if (binding?.Preset == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            MotionImportPreset preset = binding.Preset;
            if (!preset.HasValidTechnicalIdentity())
            {
                throw new InvalidOperationException(
                    "Motion import preset is not technically valid: " + preset.name);
            }

            importer.globalScale = preset.GlobalScale;
            importer.useFileScale = true;
            importer.useFileUnits = true;
            importer.bakeAxisConversion = preset.BakeAxisConversion;
            importer.preserveHierarchy = preset.PreserveHierarchy;
            importer.importBlendShapes = preset.ImportBlendShapes;
            importer.optimizeGameObjects = preset.OptimizeGameObjects;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importConstraints = false;
            importer.addCollider = false;
            importer.importAnimation = true;
            importer.resampleCurves = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.animationRotationError = preset.RotationError;
            importer.animationPositionError = preset.PositionError;
            importer.animationScaleError = preset.ScaleError;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.animationType = preset.RigClassification == MotionRigClassification.Humanoid
                ? ModelImporterAnimationType.Human
                : ModelImporterAnimationType.Generic;
            ApplyHumanoidMapping(importer);

            MotionImportClip[] clips = binding.Clips
                .Where(value => value != null)
                .OrderBy(value => value.ClipId, StringComparer.Ordinal)
                .ToArray();
            var importedClips = new ModelImporterClipAnimation[clips.Length];
            ModelImporterClipAnimation[] sourceTakes = importer.defaultClipAnimations;
            for (int index = 0; index < clips.Length; index++)
            {
                importedClips[index] = BuildClip(clips[index], sourceTakes);
            }

            importer.clipAnimations = importedClips;
        }

        private static ModelImporterClipAnimation BuildClip(
            MotionImportClip source,
            ModelImporterClipAnimation[] sourceTakes)
        {
            if (string.IsNullOrWhiteSpace(source.ClipId) ||
                string.IsNullOrWhiteSpace(source.MotionKey) ||
                string.IsNullOrWhiteSpace(source.SourceTake) ||
                source.FirstFrameInclusive < 0 ||
                source.LastFrameInclusive < source.FirstFrameInclusive)
            {
                throw new InvalidOperationException(
                    "Motion import clip binding is incomplete or has an invalid frame range.");
            }

            var clip = new ModelImporterClipAnimation
            {
                name = source.ClipId,
                takeName = ResolveTakeName(source.SourceTake, sourceTakes),
                firstFrame = source.FirstFrameInclusive,
                lastFrame = source.LastFrameInclusive,
                loopTime = source.Loop,
                loopPose = source.Loop,
                lockRootRotation = source.RootMode == MotionRootMode.InPlace,
                lockRootHeightY = source.RootMode != MotionRootMode.Authored,
                lockRootPositionXZ = source.RootMode == MotionRootMode.InPlace,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
                heightFromFeet = true
            };

            int frameSpan = Math.Max(
                1,
                source.LastFrameInclusive - source.FirstFrameInclusive);
            clip.events = source.Events
                .Where(value => value != null)
                .OrderBy(value => value.SourceFrame)
                .ThenBy(value => value.EventOrdinal)
                .Select(value =>
                {
                    float normalizedTime = Mathf.Clamp01(
                        (float)(value.SourceFrame - source.FirstFrameInclusive) / frameSpan);
                    return new AnimationEvent
                    {
                        functionName = "AL_MotionEventV1",
                        time = normalizedTime,
                        stringParameter = JsonUtility.ToJson(
                            new MotionAnimationEventPayload
                            {
                                schemaVersion = 1,
                                eventId = value.EventDefinitionId,
                                actionSequence = 0,
                                eventOrdinal = value.EventOrdinal,
                                normalizedTime = normalizedTime,
                                phase = value.StaticPayload?.Phase,
                                contactId = value.StaticPayload?.ContactId,
                                windowId = value.StaticPayload?.WindowId,
                                cueId = value.StaticPayload?.CueId
                            })
                    };
                })
                .ToArray();
            return clip;
        }

        private static string ResolveTakeName(
            string configuredTake,
            ModelImporterClipAnimation[] sourceTakes)
        {
            ModelImporterClipAnimation[] candidates = (sourceTakes ??
                    Array.Empty<ModelImporterClipAnimation>())
                .Where(value => value != null &&
                                (string.Equals(
                                     value.takeName,
                                     configuredTake,
                                     StringComparison.Ordinal) ||
                                 value.takeName.EndsWith(
                                     "|" + configuredTake,
                                     StringComparison.Ordinal)))
                .ToArray();
            if (candidates.Length > 1)
            {
                throw new InvalidOperationException(
                    "Motion source take is ambiguous: " + configuredTake);
            }

            return candidates.Length == 1 ? candidates[0].takeName : configuredTake;
        }

        internal static void ApplyHumanoidMapping(ModelImporter importer)
        {
            if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
            {
                return;
            }

            HumanDescription description = importer.humanDescription;
            description.human = new[]
            {
                CreateHumanBone("pelvis", "Hips"),
                CreateHumanBone("spine_01", "Spine"),
                CreateHumanBone("spine_02", "Chest"),
                CreateHumanBone("chest", "UpperChest"),
                CreateHumanBone("neck_01", "Neck"),
                CreateHumanBone("head", "Head"),
                CreateHumanBone("clavicle_l", "LeftShoulder"),
                CreateHumanBone("upper_arm_l", "LeftUpperArm"),
                CreateHumanBone("lower_arm_l", "LeftLowerArm"),
                CreateHumanBone("hand_l", "LeftHand"),
                CreateHumanBone("clavicle_r", "RightShoulder"),
                CreateHumanBone("upper_arm_r", "RightUpperArm"),
                CreateHumanBone("lower_arm_r", "RightLowerArm"),
                CreateHumanBone("hand_r", "RightHand"),
                CreateHumanBone("upper_leg_l", "LeftUpperLeg"),
                CreateHumanBone("lower_leg_l", "LeftLowerLeg"),
                CreateHumanBone("foot_l", "LeftFoot"),
                CreateHumanBone("toe_l", "LeftToes"),
                CreateHumanBone("upper_leg_r", "RightUpperLeg"),
                CreateHumanBone("lower_leg_r", "RightLowerLeg"),
                CreateHumanBone("foot_r", "RightFoot"),
                CreateHumanBone("toe_r", "RightToes")
            };
            importer.humanDescription = description;
        }

        private static HumanBone CreateHumanBone(string boneName, string humanName)
        {
            return new HumanBone
            {
                boneName = boneName,
                humanName = humanName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }
    }
}
