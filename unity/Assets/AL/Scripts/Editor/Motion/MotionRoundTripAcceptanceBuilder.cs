using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AL.Motion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace AL.Editor.Motion
{
    [Serializable]
    public sealed class MotionRoundTripRigReport
    {
        public bool avatarValid;
        public bool isHuman;
        public int rootCount;
        public bool hasRoot;
        public bool hasMotionRoot;
        public string[] missingSockets = Array.Empty<string>();
        public string[] invalidBoneNames = Array.Empty<string>();
        public int invalidHierarchyCount;
        public float uniformScale;
        public float axisErrorDegrees;
        public float heightMeters;
        public int maximumInfluencesPerVertex;
        public int deformingBones;
        public int animatedTransforms;
        public int unweightedVertices;
    }

    [Serializable]
    public sealed class MotionRoundTripAnimationReport
    {
        public int residentClipCount;
        public float compressedMemoryMiB;
        public string compression;
        public string[] missingMotionKeys = Array.Empty<string>();
        public string[] missingEvents = Array.Empty<string>();
        public int duplicateEvents;
        public int invalidEventOrder;
        public int invalidHitboxWindows;
        public int droppedEvents;
        public int incompatibleRootMotion;
        public float trajectoryErrorMeters;
        public float footSlidingMeters;
        public float contactDriftMeters;
        public float transitionPositionDeltaMeters;
        public float transitionRotationDeltaDegrees;

        public string[] MissingMotionKeys => missingMotionKeys;
        public string[] MissingEvents => missingEvents;
    }

    [Serializable]
    public sealed class MotionRoundTripRuntimeReport
    {
        public bool controllerConfigured;
        public bool graphValid;
        public bool safePoseLoaded;
        public bool tPoseDetected;
        public bool fallbackPassed;
        public bool transitionPassed;
        public bool recoveryPassed;
        public bool attachmentsPassed;

        public bool ControllerConfigured => controllerConfigured;
        public bool GraphValid => graphValid;
        public bool TPoseDetected => tPoseDetected;
    }

    [Serializable]
    public sealed class MotionRoundTripRepresentativeReport
    {
        public string representativeProfileId;
        public string subjectKind;
        public string skeletonProfileId;
        public string budgetProfileId;
        public bool freshImport;
        public string importedRigPath;
        public string importedMotionPath;
        public MotionRoundTripRigReport rig = new MotionRoundTripRigReport();
        public MotionRoundTripAnimationReport animation = new MotionRoundTripAnimationReport();
        public MotionRoundTripRuntimeReport runtime = new MotionRoundTripRuntimeReport();
        public string[] failures = Array.Empty<string>();

        public string SubjectKind => subjectKind;
        public bool FreshImport => freshImport;
        public MotionRoundTripAnimationReport Animation => animation;
        public MotionRoundTripRuntimeReport Runtime => runtime;
    }

    [Serializable]
    public sealed class MotionRoundTripAcceptanceReport
    {
        public int schemaVersion = 1;
        public string pipelineId = "rmc_pipeline_unity_roundtrip_acceptance_v001";
        public string unityVersion;
        public string generatedUtc;
        public string scenePath;
        public string reportPath;
        public string status;
        public MotionRoundTripRepresentativeReport[] representatives =
            Array.Empty<MotionRoundTripRepresentativeReport>();
        public string[] reviewImages = Array.Empty<string>();

        public string Status => status;
        public string ScenePath => scenePath;
        public string ReportPath => reportPath;
        public MotionRoundTripRepresentativeReport[] Representatives => representatives;

        public string FormatFailures()
        {
            return string.Join(
                "\n",
                representatives.SelectMany(value => value.failures ?? Array.Empty<string>()));
        }
    }

    public static class MotionRoundTripAcceptanceBuilder
    {
        private const string GeneratedAssetRoot = "Assets/AL/Generated/MotionRoundTrip";
        private const string ScenePath = GeneratedAssetRoot + "/MotionRoundTripAcceptance.unity";
        private const string ReportRelativePath = "Logs/MotionRoundTrip/motion_roundtrip_acceptance_report.json";
        private const string StandardRelativePath =
            "Assets/AL/StreamingAssets/GameData/al_rig_motion_standard.json";
        private const string RequiredManifestRelativePath =
            "Assets/AL/StreamingAssets/GameData/al_required_motion_manifest.json";
        private const string RigManifestRelativePath =
            "ArtSource/RigPipeline/al_rig_cleanup_manifest.v1.json";
        private const string MotionCatalogRelativePath =
            "ArtSource/MotionLibrary/al_motion_library_catalog.v1.json";
        private static readonly Regex BoneNamePattern =
            new Regex("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        [Serializable]
        private sealed class StandardFile
        {
            public SkeletonFile[] skeletonProfiles = Array.Empty<SkeletonFile>();
            public BudgetFile[] qualityBudgets = Array.Empty<BudgetFile>();
            public ProfileFile[] representativeProfiles = Array.Empty<ProfileFile>();
        }

        [Serializable]
        private sealed class SkeletonFile
        {
            public string id;
            public string classification;
        }

        [Serializable]
        private sealed class BudgetFile
        {
            public string id;
            public SkinningBudgetFile skinning;
            public AnimationBudgetFile animation;
            public ContactBudgetFile contacts;
        }

        [Serializable]
        private sealed class SkinningBudgetFile
        {
            public int maximumInfluencesPerVertex;
            public int maximumDeformingBones;
            public int maximumAnimatedTransforms;
        }

        [Serializable]
        private sealed class AnimationBudgetFile
        {
            public int maximumResidentClipCount;
            public float maximumCompressedMemoryMiB;
        }

        [Serializable]
        private sealed class ContactBudgetFile
        {
            public float maximumLoopPositionErrorMeters;
            public float maximumPlantedHorizontalDriftMeters;
        }

        [Serializable]
        private sealed class ProfileFile
        {
            public string id;
            public string subjectKind;
            public string skeletonProfileId;
            public string budgetProfileId;
        }

        [Serializable]
        private sealed class RequiredManifestFile
        {
            public EventDefinitionFile[] eventDefinitions = Array.Empty<EventDefinitionFile>();
        }

        [Serializable]
        private sealed class EventDefinitionFile
        {
            public string id;
            public string eventName;
        }

        [Serializable]
        private sealed class RigManifestFile
        {
            public RigAssetFile[] assets = Array.Empty<RigAssetFile>();
        }

        [Serializable]
        private sealed class RigAssetFile
        {
            public string id;
            public string representativeProfileId;
            public string skeletonProfileId;
            public string budgetProfileId;
            public RigOutputFile output;
            public string[] requiredBones = Array.Empty<string>();
        }

        [Serializable]
        private sealed class RigOutputFile
        {
            public string fbxPath;
        }

        [Serializable]
        private sealed class MotionCatalogFile
        {
            public int sampleRateHz;
            public MotionAssetFile[] assets = Array.Empty<MotionAssetFile>();
            public MotionClipFile[] clips = Array.Empty<MotionClipFile>();
            public MotionBindingFile[] bindings = Array.Empty<MotionBindingFile>();
        }

        [Serializable]
        private sealed class MotionAssetFile
        {
            public string id;
            public string representativeProfileId;
            public string skeletonProfileId;
            public string fbxPath;
        }

        [Serializable]
        private sealed class MotionBindingFile
        {
            public string representativeProfileId;
            public string motionKey;
            public string clipId;
        }

        [Serializable]
        private sealed class MotionClipFile
        {
            public string id;
            public string motionKey;
            public string sourceTake;
            public bool loop;
            public int frameCount;
            public string rootTreatment;
            public MotionEventFile[] events = Array.Empty<MotionEventFile>();
            public HitboxWindowFile[] hitboxWindows = Array.Empty<HitboxWindowFile>();
        }

        [Serializable]
        private sealed class MotionEventFile
        {
            public string eventName;
            public int frame;
            public int eventOrdinal;
            public string phase;
            public string contactId;
            public string windowId;
            public string cueId;
        }

        [Serializable]
        private sealed class HitboxWindowFile
        {
            public string windowId;
            public int openFrame;
            public int closeFrame;
        }

        private sealed class ImportedRepresentative
        {
            public ProfileFile Profile;
            public RigAssetFile RigAsset;
            public MotionAssetFile MotionAsset;
            public MotionClipFile[] Clips;
            public string RigPath;
            public string MotionPath;
            public GameObject Instance;
            public AnimationClip[] ImportedClips;
            public ModelImporter MotionImporter;
            public IReadOnlyDictionary<string, string> EventDefinitionIds;
        }

        [MenuItem("Another Life/Motion/Build Round-Trip Acceptance")]
        public static void BuildFromMenu()
        {
            BuildForTests(renderReviewImages: true);
        }

        public static MotionRoundTripAcceptanceReport BuildForTests(bool renderReviewImages)
        {
            StandardFile standard = LoadJson<StandardFile>(StandardRelativePath);
            RequiredManifestFile required = LoadJson<RequiredManifestFile>(
                RequiredManifestRelativePath);
            RigManifestFile rigs = LoadJson<RigManifestFile>(RigManifestRelativePath);
            MotionCatalogFile catalog = LoadJson<MotionCatalogFile>(MotionCatalogRelativePath);

            RecreateGeneratedRoot();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateReviewEnvironment();

            var imported = new List<ImportedRepresentative>();
            try
            {
                foreach (ProfileFile profile in standard.representativeProfiles.OrderBy(value => value.id))
                {
                    RigAssetFile rigAsset = rigs.assets.SingleOrDefault(
                        value => value.representativeProfileId == profile.id);
                    MotionAssetFile motionAsset = catalog.assets.SingleOrDefault(
                        value => value.representativeProfileId == profile.id);
                    if (rigAsset == null || motionAsset == null)
                    {
                        throw new InvalidOperationException(
                            "Representative source artifacts are missing: " + profile.id);
                    }

                    MotionBindingFile[] bindings = catalog.bindings
                        .Where(value => value.representativeProfileId == profile.id)
                        .OrderBy(value => value.motionKey, StringComparer.Ordinal)
                        .ToArray();
                    var clipsById = catalog.clips.ToDictionary(value => value.id, StringComparer.Ordinal);
                    MotionClipFile[] clips = bindings.Select(value => clipsById[value.clipId]).ToArray();
                    imported.Add(
                        ImportRepresentative(profile, rigAsset, motionAsset, clips, required, catalog));
                }

                var reports = new List<MotionRoundTripRepresentativeReport>();
                for (int index = 0; index < imported.Count; index++)
                {
                    ImportedRepresentative value = imported[index];
                    value.Instance.transform.position = new Vector3((index - 1) * 4f, 0f, 0f);
                    value.Instance.name = "Acceptance_" + value.Profile.subjectKind;
                    AddLabel(value.Instance.transform.position, value.Profile.subjectKind.ToUpperInvariant());
                    reports.Add(EvaluateRepresentative(value, standard));
                }

                EditorSceneManager.SaveScene(scene, ScenePath);
                var report = new MotionRoundTripAcceptanceReport
                {
                    unityVersion = Application.unityVersion,
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    scenePath = ScenePath,
                    reportPath = ReportRelativePath.Replace('\\', '/'),
                    representatives = reports.ToArray(),
                    status = reports.All(value => value.failures.Length == 0) ? "passed" : "failed"
                };
                if (renderReviewImages)
                {
                    report.reviewImages = RenderReviewImages(imported);
                }

                WriteReport(report);
                AssetDatabase.SaveAssets();
                return report;
            }
            finally
            {
                foreach (ImportedRepresentative value in imported)
                {
                    MotionRuntimeController controller = value.Instance != null
                        ? value.Instance.GetComponent<MotionRuntimeController>()
                        : null;
                    controller?.Release();
                }
            }
        }

        private static ImportedRepresentative ImportRepresentative(
            ProfileFile profile,
            RigAssetFile rigAsset,
            MotionAssetFile motionAsset,
            MotionClipFile[] clips,
            RequiredManifestFile required,
            MotionCatalogFile catalog)
        {
            string slug = profile.subjectKind.ToLowerInvariant();
            string rigPath = GeneratedAssetRoot + "/" + slug + "_rig.fbx";
            string motionPath = GeneratedAssetRoot + "/" + slug + "_motion.fbx";
            CopyAndImport(rigAsset.output.fbxPath, rigPath);
            CopyAndImport(motionAsset.fbxPath, motionPath);

            bool humanoid = profile.subjectKind != "beast";
            MotionImportPreset preset = CreatePreset(profile, humanoid, catalog.sampleRateHz);
            try
            {
                var rigBinding = new MotionImportBinding(
                    rigPath,
                    preset,
                    Array.Empty<MotionImportClip>());
                ModelImporter rigImporter = RequireImporter(rigPath);
                MotionModelImportPostprocessor.Apply(rigImporter, rigBinding);
                rigImporter.SaveAndReimport();

                Dictionary<string, string> eventIds = required.eventDefinitions.ToDictionary(
                    value => value.eventName,
                    value => value.id,
                    StringComparer.Ordinal);
                MotionImportClip[] importClips = clips.Select(
                        clip => new MotionImportClip(
                            clip.id,
                            clip.motionKey,
                            clip.sourceTake,
                            1,
                            clip.frameCount,
                            clip.loop,
                            clip.rootTreatment == "in_place_motor_owned"
                                ? MotionRootMode.InPlace
                                : MotionRootMode.Bounded,
                            clip.events.Select(
                                motionEvent => new MotionImportEvent(
                                    eventIds[motionEvent.eventName],
                                    motionEvent.frame,
                                    motionEvent.eventOrdinal,
                                    new MotionStaticPayload
                                    {
                                        Phase = motionEvent.phase,
                                        ContactId = motionEvent.contactId,
                                        WindowId = motionEvent.windowId,
                                        CueId = motionEvent.cueId
                                    }))))
                    .ToArray();
                var motionBinding = new MotionImportBinding(motionPath, preset, importClips);
                ModelImporter motionImporter = RequireImporter(motionPath);
                MotionModelImportPostprocessor.Apply(motionImporter, motionBinding);
                motionImporter.SaveAndReimport();

                GameObject rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);
                if (rigPrefab == null)
                {
                    throw new InvalidOperationException("Fresh rig import failed: " + rigPath);
                }

                if (humanoid)
                {
                    Avatar rigAvatar = AssetDatabase.LoadAllAssetsAtPath(rigPath)
                        .OfType<Avatar>()
                        .FirstOrDefault(value => value != null && value.isValid && value.isHuman);
                    bool motionHasHumanAvatar = AssetDatabase.LoadAllAssetsAtPath(motionPath)
                        .OfType<Avatar>()
                        .Any(value => value != null && value.isValid && value.isHuman);
                    if (rigAvatar != null && !motionHasHumanAvatar)
                    {
                        motionImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        motionImporter.sourceAvatar = rigAvatar;
                        motionImporter.SaveAndReimport();
                    }
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
                AnimationClip[] importedClips = LoadImportedClips(motionPath)
                    .OrderBy(value => value.name, StringComparer.Ordinal)
                    .ToArray();
                return new ImportedRepresentative
                {
                    Profile = profile,
                    RigAsset = rigAsset,
                    MotionAsset = motionAsset,
                    Clips = clips,
                    RigPath = rigPath,
                    MotionPath = motionPath,
                    Instance = instance,
                    ImportedClips = importedClips,
                    MotionImporter = motionImporter,
                    EventDefinitionIds = eventIds
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }

        private static MotionRoundTripRepresentativeReport EvaluateRepresentative(
            ImportedRepresentative value,
            StandardFile standard)
        {
            var failures = new List<string>();
            Transform[] transforms = value.Instance.GetComponentsInChildren<Transform>(true);
            Animator animator = value.Instance.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = value.Instance.AddComponent<Animator>();
            }

            AnimationClip[] clips = value.ImportedClips;
            var definitions = new List<MotionClipDefinition>();
            foreach (MotionClipFile expected in value.Clips)
            {
                AnimationClip clip = FindImportedClip(expected, clips);
                if (clip == null)
                {
                    continue;
                }

                definitions.Add(
                    new MotionClipDefinition(
                        expected.id,
                        expected.motionKey,
                        clip,
                        null,
                        expected.rootTreatment == "in_place_motor_owned"
                            ? MotionRootMode.InPlace
                            : MotionRootMode.Bounded,
                        PriorityFor(expected.motionKey),
                        expected.loop,
                        false));
            }

            MotionClipDefinition[] residentDefinitions = definitions
                .Where(item => item.MotionKey == "idle.neutral")
                .Concat(definitions.Where(item => item.MotionKey != "idle.neutral").Take(1))
                .ToArray();
            MotionRoundTripRigReport rig = EvaluateRig(value, transforms, animator, clips);
            MotionRoundTripAnimationReport animation = EvaluateAnimation(
                value,
                clips,
                residentDefinitions.Select(item => item.ClipId).ToArray());

            MotionRuntimeController controller = value.Instance.AddComponent<MotionRuntimeController>();
            bool controllerConfigured = false;
            bool graphValid = false;
            bool safePoseLoaded = false;
            bool fallbackPassed = false;
            bool transitionPassed = false;
            bool recoveryPassed = false;
            if (residentDefinitions.Any(item => item.MotionKey == "idle.neutral"))
            {
                var snapshot = new MotionCatalogSnapshot("idle.neutral", residentDefinitions);
                controller.Configure(
                    animator,
                    snapshot,
                    new[] { new MotionLayerDefinition("base", false, null, 0) });
                controller.Tick(0f);
                controllerConfigured = true;
                graphValid = controller.IsGraphValid;
                safePoseLoaded = controller.CurrentMotionKey == "idle.neutral";
                fallbackPassed = controller.RequestMotion("acceptance.missing", 1L) &&
                                 controller.LastRequestUsedFallback &&
                                 controller.CurrentMotionKey == "idle.neutral";
                MotionClipDefinition transitionTarget = residentDefinitions.FirstOrDefault(
                    item => item.MotionKey != "idle.neutral");
                transitionPassed = transitionTarget != null &&
                                   controller.RequestMotion(transitionTarget.MotionKey, 2L) &&
                                   controller.CurrentMotionKey == transitionTarget.MotionKey;
                controller.CompleteCurrent();
                controller.Tick(0f);
                recoveryPassed = controller.CurrentMotionKey == "idle.neutral";
            }

            bool attachmentsPassed = CreateSocketAttachments(value, transforms);
            bool tPoseDetected = DetectTPose(value.Profile.subjectKind, transforms);
            var runtime = new MotionRoundTripRuntimeReport
            {
                controllerConfigured = controllerConfigured,
                graphValid = graphValid,
                safePoseLoaded = safePoseLoaded,
                tPoseDetected = tPoseDetected,
                fallbackPassed = fallbackPassed,
                transitionPassed = transitionPassed,
                recoveryPassed = recoveryPassed,
                attachmentsPassed = attachmentsPassed
            };

            BudgetFile budget = standard.qualityBudgets.Single(
                item => item.id == value.Profile.budgetProfileId);
            AddFailure(!rig.avatarValid, "InvalidAvatar", failures);
            AddFailure(
                rig.isHuman != (value.Profile.subjectKind != "beast"),
                "AvatarClassificationMismatch",
                failures);
            AddFailure(rig.rootCount != 1 || !rig.hasRoot, "MissingRequiredRoot:root", failures);
            AddFailure(!rig.hasMotionRoot, "MissingRequiredRoot:motion_root", failures);
            AddFailure(rig.missingSockets.Length > 0, "MissingRequiredSocket", failures);
            AddFailure(rig.invalidBoneNames.Length > 0, "InvalidBoneName", failures);
            AddFailure(rig.invalidHierarchyCount != 0, "InvalidBoneHierarchy", failures);
            AddFailure(Mathf.Abs(rig.uniformScale - 1f) > 0.0001f, "InvalidImportScale", failures);
            AddFailure(rig.axisErrorDegrees > 0.1f, "InvalidImportAxis", failures);
            AddFailure(rig.heightMeters <= 0f, "InvalidImportedBounds", failures);
            AddFailure(
                rig.maximumInfluencesPerVertex > budget.skinning.maximumInfluencesPerVertex,
                "SkinInfluenceBudgetExceeded",
                failures);
            AddFailure(
                rig.deformingBones > budget.skinning.maximumDeformingBones,
                "DeformingBoneBudgetExceeded",
                failures);
            AddFailure(
                rig.animatedTransforms > budget.skinning.maximumAnimatedTransforms,
                "AnimatedTransformBudgetExceeded",
                failures);
            AddFailure(rig.unweightedVertices != 0, "UnweightedVertices", failures);
            AddFailure(
                animation.residentClipCount > budget.animation.maximumResidentClipCount,
                "ResidentClipBudgetExceeded",
                failures);
            AddFailure(
                animation.compressedMemoryMiB > budget.animation.maximumCompressedMemoryMiB,
                "AnimationMemoryBudgetExceeded",
                failures);
            AddFailure(animation.missingMotionKeys.Length > 0, "MissingRequiredMotion", failures);
            AddFailure(animation.missingEvents.Length > 0, "MissingRequiredEvent", failures);
            AddFailure(animation.duplicateEvents != 0, "DuplicateEvent", failures);
            AddFailure(animation.invalidEventOrder != 0, "InvalidEventOrder", failures);
            AddFailure(animation.invalidHitboxWindows != 0, "InvalidHitboxWindow", failures);
            AddFailure(animation.droppedEvents != 0, "DroppedEvent", failures);
            AddFailure(animation.incompatibleRootMotion != 0, "IncompatibleRootMotion", failures);
            AddFailure(
                animation.trajectoryErrorMeters > budget.contacts.maximumLoopPositionErrorMeters,
                "TrajectoryConsistencyExceeded",
                failures);
            AddFailure(
                animation.footSlidingMeters >
                budget.contacts.maximumPlantedHorizontalDriftMeters,
                "FootSlidingExceeded",
                failures);
            AddFailure(
                animation.contactDriftMeters >
                budget.contacts.maximumPlantedHorizontalDriftMeters,
                "ContactDriftExceeded",
                failures);
            AddFailure(animation.transitionPositionDeltaMeters > 0.03f, "TransitionPosition", failures);
            AddFailure(animation.transitionRotationDeltaDegrees > 6f, "TransitionRotation", failures);
            AddFailure(!runtime.controllerConfigured, "RuntimeControllerFailure", failures);
            AddFailure(!runtime.graphValid, "RuntimeGraphFailure", failures);
            AddFailure(!runtime.safePoseLoaded, "SafePoseFailure", failures);
            AddFailure(runtime.tPoseDetected, "TPoseDetected", failures);
            AddFailure(!runtime.fallbackPassed, "FallbackFailure", failures);
            AddFailure(!runtime.transitionPassed, "TransitionFailure", failures);
            AddFailure(!runtime.recoveryPassed, "RecoveryFailure", failures);
            AddFailure(!runtime.attachmentsPassed, "BrokenAttachment", failures);

            return new MotionRoundTripRepresentativeReport
            {
                representativeProfileId = value.Profile.id,
                subjectKind = value.Profile.subjectKind,
                skeletonProfileId = value.Profile.skeletonProfileId,
                budgetProfileId = value.Profile.budgetProfileId,
                freshImport = value.RigPath.StartsWith(GeneratedAssetRoot, StringComparison.Ordinal) &&
                              value.MotionPath.StartsWith(GeneratedAssetRoot, StringComparison.Ordinal),
                importedRigPath = value.RigPath,
                importedMotionPath = value.MotionPath,
                rig = rig,
                animation = animation,
                runtime = runtime,
                failures = failures.ToArray()
            };
        }

        private static MotionRoundTripRigReport EvaluateRig(
            ImportedRepresentative value,
            Transform[] transforms,
            Animator animator,
            AnimationClip[] clips)
        {
            Transform skeletonRoot = transforms.FirstOrDefault(item => item.name == "root");
            Transform[] skeletonTransforms = skeletonRoot != null
                ? skeletonRoot.GetComponentsInChildren<Transform>(true)
                : Array.Empty<Transform>();
            string[] names = skeletonTransforms.Select(item => item.name).ToArray();
            string[] missingSockets = ExpectedSocketNames(value.Profile.subjectKind)
                .Where(name => !names.Contains(name, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] invalidNames = names
                .Where(name => !BoneNamePattern.IsMatch(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var renderers = value.Instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = CalculateBounds(renderers, value.Instance.transform.position);
            var skinned = value.Instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int maxInfluences = 0;
            int unweighted = 0;
            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                using (var counts = mesh.GetBonesPerVertex())
                {
                    for (int index = 0; index < counts.Length; index++)
                    {
                        maxInfluences = Mathf.Max(maxInfluences, counts[index]);
                        if (counts[index] == 0)
                        {
                            unweighted++;
                        }
                    }
                }
            }

            var weightedBones = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Transform[] bones = renderer.bones;
                using (var weights = mesh.GetAllBoneWeights())
                {
                    for (int index = 0; index < weights.Length; index++)
                    {
                        int boneIndex = weights[index].boneIndex;
                        if (weights[index].weight > 0f && boneIndex >= 0 &&
                            boneIndex < bones.Length && bones[boneIndex] != null)
                        {
                            weightedBones.Add(bones[boneIndex]);
                        }
                    }
                }
            }

            int deformingBones = weightedBones.Count;
            int animatedTransforms = clips.Select(
                    clip => AnimationUtility.GetCurveBindings(clip)
                        .Select(binding => binding.path)
                        .Distinct(StringComparer.Ordinal)
                        .Count())
                .DefaultIfEmpty(0)
                .Max();
            Vector3 scale = value.Instance.transform.lossyScale;
            return new MotionRoundTripRigReport
            {
                avatarValid = animator.avatar != null && animator.avatar.isValid,
                isHuman = animator.avatar != null && animator.avatar.isHuman,
                rootCount = transforms.Count(item => item.name == "root"),
                hasRoot = names.Contains("root", StringComparer.Ordinal),
                hasMotionRoot = names.Contains("motion_root", StringComparer.Ordinal),
                missingSockets = missingSockets,
                invalidBoneNames = invalidNames,
                invalidHierarchyCount = transforms.Count(
                    item => item != value.Instance.transform && item.parent == null),
                uniformScale = Mathf.Max(scale.x, scale.y, scale.z) -
                               Mathf.Min(scale.x, scale.y, scale.z) <= 0.0001f
                    ? scale.x
                    : 0f,
                axisErrorDegrees = Vector3.Angle(value.Instance.transform.forward, Vector3.forward),
                heightMeters = bounds.size.y,
                maximumInfluencesPerVertex = maxInfluences,
                deformingBones = deformingBones,
                animatedTransforms = animatedTransforms,
                unweightedVertices = unweighted
            };
        }

        private static MotionRoundTripAnimationReport EvaluateAnimation(
            ImportedRepresentative value,
            AnimationClip[] clips,
            IReadOnlyCollection<string> residentClipIds)
        {
            Dictionary<string, AnimationClip> imported = value.Clips
                .Select(item => new { Expected = item, Clip = FindImportedClip(item, clips) })
                .Where(item => item.Clip != null)
                .ToDictionary(item => item.Expected.id, item => item.Clip, StringComparer.Ordinal);
            string[] missingKeys = value.Clips
                .Where(item => !imported.ContainsKey(item.id))
                .Select(item => item.motionKey)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var missingEvents = new List<string>();
            int duplicates = 0;
            int invalidOrder = 0;
            int invalidHitboxes = 0;
            int dropped = 0;
            int incompatibleRoot = 0;
            foreach (MotionClipFile expected in value.Clips)
            {
                if (!imported.TryGetValue(expected.id, out AnimationClip clip))
                {
                    dropped += expected.events.Length;
                    continue;
                }

                MotionAnimationEventPayload[] payloads = AnimationUtility.GetAnimationEvents(clip)
                    .Where(item => item.functionName == "AL_MotionEventV1")
                    .Select(item => JsonUtility.FromJson<MotionAnimationEventPayload>(item.stringParameter))
                    .Where(item => item != null)
                    .ToArray();
                int[] ordinals = payloads.Select(item => item.eventOrdinal).ToArray();
                duplicates += ordinals.Length - ordinals.Distinct().Count();
                invalidOrder += ordinals.SequenceEqual(ordinals.OrderBy(item => item)) ? 0 : 1;
                dropped += Mathf.Max(0, expected.events.Length - payloads.Length);
                foreach (MotionEventFile motionEvent in expected.events)
                {
                    if (!payloads.Any(
                            payload => payload.eventOrdinal == motionEvent.eventOrdinal &&
                                       payload.eventId ==
                                       value.EventDefinitionIds[motionEvent.eventName] &&
                                       SameOptional(payload.phase, motionEvent.phase) &&
                                       SameOptional(payload.contactId, motionEvent.contactId) &&
                                       SameOptional(payload.windowId, motionEvent.windowId) &&
                                       SameOptional(payload.cueId, motionEvent.cueId)))
                    {
                        missingEvents.Add(expected.motionKey + ":" + motionEvent.eventName);
                    }
                }

                foreach (HitboxWindowFile window in expected.hitboxWindows)
                {
                    MotionEventFile begin = expected.events.SingleOrDefault(
                        item => item.eventName == "al.motion.hitbox.request_begin" &&
                                item.windowId == window.windowId);
                    MotionEventFile end = expected.events.SingleOrDefault(
                        item => item.eventName == "al.motion.hitbox.request_end" &&
                                item.windowId == window.windowId);
                    if (begin == null || end == null || begin.frame != window.openFrame ||
                        end.frame != window.closeFrame || window.openFrame >= window.closeFrame)
                    {
                        invalidHitboxes++;
                    }
                }

                ModelImporterClipAnimation importedSettings = value.MotionImporter.clipAnimations
                    .SingleOrDefault(item => item.name == expected.id);
                bool inPlaceCompatible = expected.rootTreatment != "in_place_motor_owned" ||
                                         (importedSettings != null &&
                                          importedSettings.lockRootPositionXZ &&
                                          importedSettings.lockRootRotation);
                if (!inPlaceCompatible)
                {
                    incompatibleRoot++;
                }
            }

            MeasureQuality(
                value.Instance,
                value.Clips,
                imported,
                out float trajectory,
                out float footSliding,
                out float contactDrift,
                out float transitionPosition,
                out float transitionRotation);
            AnimationClip[] residentClips = imported
                .Where(item => residentClipIds.Contains(item.Key))
                .Select(item => item.Value)
                .Distinct()
                .ToArray();
            long memoryBytes = residentClips.Sum(Profiler.GetRuntimeMemorySizeLong);
            return new MotionRoundTripAnimationReport
            {
                residentClipCount = residentClips.Length,
                compressedMemoryMiB = memoryBytes / (1024f * 1024f),
                compression = value.MotionImporter.animationCompression.ToString(),
                missingMotionKeys = missingKeys,
                missingEvents = missingEvents.Distinct(StringComparer.Ordinal).ToArray(),
                duplicateEvents = duplicates,
                invalidEventOrder = invalidOrder,
                invalidHitboxWindows = invalidHitboxes,
                droppedEvents = dropped,
                incompatibleRootMotion = incompatibleRoot,
                trajectoryErrorMeters = trajectory,
                footSlidingMeters = footSliding,
                contactDriftMeters = contactDrift,
                transitionPositionDeltaMeters = transitionPosition,
                transitionRotationDeltaDegrees = transitionRotation
            };
        }

        private static void MeasureQuality(
            GameObject instance,
            MotionClipFile[] expected,
            IReadOnlyDictionary<string, AnimationClip> imported,
            out float trajectory,
            out float footSliding,
            out float contactDrift,
            out float transitionPosition,
            out float transitionRotation)
        {
            trajectory = 0f;
            footSliding = 0f;
            contactDrift = 0f;
            transitionPosition = 0f;
            transitionRotation = 0f;
            GameObject qualityInstance = UnityEngine.Object.Instantiate(instance);
            qualityInstance.name = instance.name + "_QualityProbe";
            qualityInstance.hideFlags = HideFlags.HideAndDontSave;
            MotionRuntimeController copiedController =
                qualityInstance.GetComponent<MotionRuntimeController>();
            if (copiedController != null)
            {
                copiedController.Release();
                UnityEngine.Object.DestroyImmediate(copiedController);
            }

            try
            {
                Animator animator = qualityInstance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid)
                {
                    footSliding = float.PositiveInfinity;
                    contactDrift = float.PositiveInfinity;
                    return;
                }

                animator.fireEvents = false;

                foreach (MotionClipFile definition in expected)
                {
                    if (!imported.TryGetValue(definition.id, out AnimationClip clip))
                    {
                        continue;
                    }

                    PlayableGraph graph = PlayableGraph.Create(
                        "MotionRoundTripQuality_" + definition.id);
                    try
                    {
                        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                            graph,
                            "QualityOutput",
                            animator);
                        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                        playable.SetApplyFootIK(true);
                        playable.SetApplyPlayableIK(true);
                        output.SetSourcePlayable(playable);
                        graph.Play();

                    void SampleRuntimePose(float time)
                    {
                        playable.SetTime(Mathf.Clamp(time, 0f, clip.length));
                        graph.Evaluate(0f);
                    }

                    bool hasExplicitMotionRootCurves = AnimationUtility.GetCurveBindings(clip)
                        .Any(binding => binding.path.Split('/').LastOrDefault() == "motion_root");
                    if (definition.loop && hasExplicitMotionRootCurves)
                    {
                        Transform motionRoot = FindTransform(
                            qualityInstance.transform,
                            "motion_root") ?? qualityInstance.transform;
                        SampleRuntimePose(0f);
                        Vector3 rootStart = motionRoot.position;
                        Quaternion rotationStart = motionRoot.rotation;
                        SampleRuntimePose(clip.length);
                        Vector3 rootEnd = motionRoot.position;
                        Quaternion rotationEnd = motionRoot.rotation;
                        float rootDelta = Vector3.Distance(rootStart, rootEnd);
                        trajectory = Mathf.Max(trajectory, rootDelta);
                        transitionPosition = Mathf.Max(transitionPosition, rootDelta);
                        transitionRotation = Mathf.Max(
                            transitionRotation,
                            Quaternion.Angle(rotationStart, rotationEnd));
                    }

                    foreach (MotionEventFile begin in definition.events.Where(
                                 item => item.eventName == "al.motion.contact.begin" &&
                                         !string.IsNullOrWhiteSpace(item.contactId)))
                    {
                        MotionEventFile end = definition.events.FirstOrDefault(
                            item => item.eventName == "al.motion.contact.end" &&
                                    item.contactId == begin.contactId && item.frame > begin.frame);
                        Transform contact = FindTransform(
                            qualityInstance.transform,
                            begin.contactId);
                        if (end == null || contact == null)
                        {
                            contactDrift = float.PositiveInfinity;
                            footSliding = float.PositiveInfinity;
                            continue;
                        }

                        float frameSpan = Mathf.Max(1f, definition.frameCount - 1f);
                        float beginTime = (begin.frame - 1f) / frameSpan * clip.length;
                        float endTime = (end.frame - 1f) / frameSpan * clip.length;
                        SampleRuntimePose(beginTime);
                        Vector3 plantedPosition = contact.position;
                        SampleRuntimePose(endTime);
                        float drift = HorizontalDistance(plantedPosition, contact.position);
                        contactDrift = Mathf.Max(contactDrift, drift);
                        footSliding = Mathf.Max(footSliding, drift);
                    }
                    }
                    finally
                    {
                        if (graph.IsValid())
                        {
                            graph.Destroy();
                        }
                    }

                    animator.Rebind();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(qualityInstance);
            }
        }

        private static bool CreateSocketAttachments(
            ImportedRepresentative value,
            Transform[] transforms)
        {
            bool passed = true;
            foreach (string socketName in ExpectedSocketNames(value.Profile.subjectKind))
            {
                Transform socket = transforms.FirstOrDefault(item => item.name == socketName);
                if (socket == null)
                {
                    passed = false;
                    continue;
                }

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Attachment_" + socketName;
                marker.transform.SetParent(socket, false);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localScale = Vector3.one * 0.06f;
                passed &= marker.transform.parent == socket;
            }

            return passed;
        }

        private static bool DetectTPose(string subjectKind, Transform[] transforms)
        {
            if (subjectKind == "beast")
            {
                return false;
            }

            Transform upper = transforms.FirstOrDefault(item => item.name == "upper_arm_l");
            Transform lower = transforms.FirstOrDefault(item => item.name == "lower_arm_l");
            Transform hand = transforms.FirstOrDefault(item => item.name == "hand_l");
            if (upper == null || lower == null || hand == null)
            {
                return true;
            }

            Vector3 upperDirection = (lower.position - upper.position).normalized;
            Vector3 lowerDirection = (hand.position - lower.position).normalized;
            bool horizontal = Mathf.Abs(upperDirection.y) < 0.08f &&
                              Mathf.Abs(lowerDirection.y) < 0.08f;
            return horizontal && Vector3.Angle(upperDirection, lowerDirection) < 8f;
        }

        private static IReadOnlyList<string> ExpectedSocketNames(string subjectKind)
        {
            if (subjectKind == "beast")
            {
                return new[]
                {
                    "socket_attack_origin",
                    "socket_camera_focus",
                    "socket_contact_front_l",
                    "socket_contact_front_r",
                    "socket_contact_rear_l",
                    "socket_contact_rear_r",
                    "socket_vfx_chest"
                };
            }

            return new[]
            {
                "socket_back",
                "socket_camera_focus",
                "socket_cape",
                "socket_chest",
                "socket_cloth_waist",
                "socket_hair",
                "socket_hand_l",
                "socket_hand_r",
                "socket_head",
                "socket_pelvis",
                "socket_vfx_chest",
                "socket_vfx_hand_l",
                "socket_vfx_hand_r",
                "socket_weapon_main",
                "socket_weapon_off"
            };
        }

        private static void RecreateGeneratedRoot()
        {
            if (AssetDatabase.IsValidFolder(GeneratedAssetRoot))
            {
                AssetDatabase.DeleteAsset(GeneratedAssetRoot);
            }

            string current = "Assets";
            foreach (string segment in GeneratedAssetRoot.Split('/').Skip(1))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }

        private static void CopyAndImport(string repositoryRelativePath, string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string source = Path.GetFullPath(
                Path.Combine(projectRoot, repositoryRelativePath.Replace("unity/", string.Empty)));
            string destination = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (!File.Exists(source))
            {
                throw new FileNotFoundException("Representative FBX is missing.", source);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, overwrite: true);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static MotionImportPreset CreatePreset(
            ProfileFile profile,
            bool humanoid,
            int sampleRateHz)
        {
            MotionImportPreset preset = ScriptableObject.CreateInstance<MotionImportPreset>();
            preset.name = "AcceptancePreset_" + profile.subjectKind;
            var serialized = new SerializedObject(preset);
            serialized.FindProperty("presetId").stringValue =
                "rmc_import_acceptance_" + profile.subjectKind + "_v001";
            serialized.FindProperty("skeletonProfileId").stringValue = profile.skeletonProfileId;
            serialized.FindProperty("retargetProfileId").stringValue = humanoid
                ? "rmc_retarget_humanoid_shared_v001"
                : "rmc_retarget_nonhumanoid_semantic_v001";
            serialized.FindProperty("rigClassification").enumValueIndex = humanoid
                ? (int)MotionRigClassification.Humanoid
                : (int)MotionRigClassification.Generic;
            serialized.FindProperty("retargetMode").enumValueIndex = humanoid
                ? (int)MotionRetargetMode.UnityHumanoid
                : (int)MotionRetargetMode.GenericExactSignature;
            serialized.FindProperty("sampleRateHz").intValue = sampleRateHz;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return preset;
        }

        private static ModelImporter RequireImporter(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            return importer ?? throw new InvalidOperationException(
                "Model importer is unavailable: " + assetPath);
        }

        private static AnimationClip[] LoadImportedClips(string assetPath)
        {
            var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            UnityEngine.Object[] loadedAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (AnimationClip clip in loadedAssets.OfType<AnimationClip>())
            {
                clips[clip.name] = clip;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model != null)
            {
                foreach (AnimationClip clip in AnimationUtility.GetAnimationClips(model))
                {
                    clips[clip.name] = clip;
                }
            }

            foreach (string clipName in RequireImporter(assetPath).clipAnimations
                         .Select(value => value.name))
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    assetPath + "[" + clipName + "]");
                if (clip != null)
                {
                    clips[clip.name] = clip;
                }
            }

            return clips.Values
                .Where(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
        }

        private static AnimationClip FindImportedClip(
            MotionClipFile expected,
            IEnumerable<AnimationClip> clips)
        {
            return clips.FirstOrDefault(
                clip => string.Equals(clip.name, expected.id, StringComparison.Ordinal)) ??
                   clips.FirstOrDefault(
                       clip => string.Equals(
                           clip.name,
                           expected.sourceTake,
                           StringComparison.Ordinal));
        }

        private static T LoadJson<T>(string projectRelativePath)
        {
            string absolute = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                projectRelativePath);
            if (!File.Exists(absolute))
            {
                throw new FileNotFoundException("Acceptance input is missing.", absolute);
            }

            T value = JsonUtility.FromJson<T>(File.ReadAllText(absolute));
            return value ?? throw new InvalidOperationException(
                "Acceptance input is malformed: " + projectRelativePath);
        }

        private static MotionPriority PriorityFor(string motionKey)
        {
            if (motionKey.StartsWith("attack", StringComparison.Ordinal))
            {
                return MotionPriority.Attack;
            }

            if (motionKey.StartsWith("skill", StringComparison.Ordinal))
            {
                return MotionPriority.Skill;
            }

            if (motionKey.StartsWith("reaction", StringComparison.Ordinal))
            {
                return MotionPriority.Reaction;
            }

            if (motionKey == "defeat")
            {
                return MotionPriority.Defeat;
            }

            return motionKey.StartsWith("locomotion", StringComparison.Ordinal)
                ? MotionPriority.Locomotion
                : MotionPriority.Idle;
        }

        private static Bounds CalculateBounds(Renderer[] renderers, Vector3 fallback)
        {
            if (renderers.Length == 0)
            {
                return new Bounds(fallback, Vector3.zero);
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1))
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == name);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static bool SameOptional(string a, string b)
        {
            return string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
        }

        private static void AddFailure(bool condition, string token, ICollection<string> failures)
        {
            if (condition)
            {
                failures.Add(token);
            }
        }

        private static void CreateReviewEnvironment()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "AcceptanceFloor";
            floor.transform.localScale = new Vector3(2.2f, 1f, 1.1f);
            var lightObject = new GameObject("AcceptanceKeyLight");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var cameraObject = new GameObject("AcceptanceCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2.2f, -10f);
            cameraObject.transform.LookAt(new Vector3(0f, 1f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
        }

        private static void AddLabel(Vector3 position, string text)
        {
            var labelObject = new GameObject("Label_" + text);
            labelObject.transform.position = position + new Vector3(0f, 2.6f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.08f;
            label.color = Color.white;
        }

        private static string[] RenderReviewImages(IEnumerable<ImportedRepresentative> imported)
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Logs/MotionRoundTrip/ReviewImages");
            Directory.CreateDirectory(outputDirectory);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var paths = new List<string>();
            foreach (ImportedRepresentative value in imported)
            {
                camera.transform.position = value.Instance.transform.position + new Vector3(0f, 1.8f, -4f);
                camera.transform.LookAt(value.Instance.transform.position + Vector3.up);
                var texture = new RenderTexture(800, 800, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                var image = new Texture2D(800, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, 800f, 800f), 0, 0);
                image.Apply();
                string path = Path.Combine(outputDirectory, value.Profile.subjectKind + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                paths.Add(Path.GetRelativePath(
                        Directory.GetParent(Application.dataPath).FullName,
                        path)
                    .Replace('\\', '/'));
                UnityEngine.Object.DestroyImmediate(image);
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return paths.ToArray();
        }

        private static void WriteReport(MotionRoundTripAcceptanceReport report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolute = Path.Combine(projectRoot, ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, JsonUtility.ToJson(report, true));
        }
    }
}
