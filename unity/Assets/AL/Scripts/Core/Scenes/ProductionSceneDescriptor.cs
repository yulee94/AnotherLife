using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AL.Core.Scenes
{
    /// <summary>
    /// Single immutable source of truth for the AnotherLife production scene inventory.
    /// The startup marker, the editor scene generator, the drift validator, the EditMode/PlayMode
    /// tests, and (later) the #150 Build Settings apply/validate path all read these records so no
    /// duplicated scene list can drift (spec sections 5, 9, 10; #223 "Required architecture").
    ///
    /// This type performs no build-settings mutation and no scene loading. It only describes intent.
    /// </summary>
    public static class ProductionSceneDescriptor
    {
        /// <summary>
        /// Descriptor source version stamped into every production scene startup marker and asserted
        /// by the marker and validator. Bumped only by a descriptor-changing PR (#223 seam, decision D4).
        /// </summary>
        public const string SourceVersion = "223.2";

        public const string SceneFolder = "Assets/AL/Scenes";

        public const string ShellFoundationProfile = "ShellFoundation";

        // Scene identifiers (spec section 5 "Recommended IDs").
        public const string BootSceneId = "al_scene_boot";
        public const string RealmSelectionSceneId = "al_scene_realm_selection";
        public const string CharacterCreationSceneId = "al_scene_character_creation";
        public const string KingdomSceneId = "al_scene_kingdom";
        public const string ChampionArenaSceneId = "al_scene_champion_arena";
        public const string TestSceneId = "al_scene_test_representative";

        // Role tokens emitted verbatim in the [AL-SCENE-ACTIVE] marker log (spec section 6).
        public const string RoleProductionEntry = "production_entry";
        public const string RoleOnboardingSelection = "onboarding_selection";
        public const string RoleOnboardingCreation = "onboarding_creation";
        public const string RoleProductionHub = "production_hub";
        public const string RoleFirstSessionGameplay = "first_session_gameplay";
        public const string RoleDeferredGameplay = "deferred_gameplay";
        public const string RoleRepresentativeTestOnly = "representative_test_only";

        // Status tokens describing authoring/build applicability separately (spec decision 6).
        public const string StatusCommittedActive = "committed_active";
        public const string StatusCommittedDeferred = "committed_deferred";
        public const string StatusTestOnly = "test_only";

        // ChampionArenaSceneController reloads the active scene by name (GetActiveScene().name,
        // ~ChampionArenaSceneController.cs:2072). Outside Build Settings that cannot resolve. #223
        // classifies this as a known deferred-gameplay limitation and does NOT fix it (spec section 8,
        // plan WI-1 step 5 scene-flow contract).
        public const string ChampionArenaSelfReloadClassification = "unresolvable_outside_build_settings";

        private static readonly ProductionSceneRecord BootRecord = new ProductionSceneRecord(
            sceneId: BootSceneId,
            assetPath: SceneFolder + "/Boot.unity",
            sceneName: "Boot",
            role: RoleProductionEntry,
            assetGuid: "14733c561820e497a96a3f2467d247cc",
            requiredControllerType: "AL.UI.BootController",
            startupMarkerId: BootSceneId,
            transitionTargets: new[]
            {
                new SceneTransition(RealmSelectionSceneId, "_realmSelectionScene", "RealmSelection", TransitionStatus.Active),
                new SceneTransition(KingdomSceneId, "_kingdomScene", "Kingdom", TransitionStatus.Active)
            },
            buildProfiles: new[] { ShellFoundationProfile },
            requiredUpstreamIssues: Array.Empty<int>(),
            status: StatusCommittedActive,
            isProductionScene: true);

        private static readonly ProductionSceneRecord RealmSelectionRecord = new ProductionSceneRecord(
            sceneId: RealmSelectionSceneId,
            assetPath: SceneFolder + "/RealmSelection.unity",
            sceneName: "RealmSelection",
            role: RoleOnboardingSelection,
            assetGuid: "5c580ae0acb5e4b4db4b57a93077e308",
            requiredControllerType: "AL.UI.RealmSelection.RealmSelectionController",
            startupMarkerId: RealmSelectionSceneId,
            transitionTargets: new[]
            {
                new SceneTransition(CharacterCreationSceneId, "_nextScene", "CharacterCreation", TransitionStatus.Active)
            },
            buildProfiles: new[] { ShellFoundationProfile },
            requiredUpstreamIssues: Array.Empty<int>(),
            status: StatusCommittedActive,
            isProductionScene: true);

        private static readonly ProductionSceneRecord CharacterCreationRecord = new ProductionSceneRecord(
            sceneId: CharacterCreationSceneId,
            assetPath: SceneFolder + "/CharacterCreation.unity",
            sceneName: "CharacterCreation",
            role: RoleOnboardingCreation,
            assetGuid: "c3d4e5f60718293a4b5c6d7e8f901234",
            requiredControllerType: "AL.UI.CharacterCreation.CharacterCreationController",
            startupMarkerId: CharacterCreationSceneId,
            transitionTargets: new[]
            {
                new SceneTransition(ChampionArenaSceneId, "_combatSceneName", "ChampionArena", TransitionStatus.Active)
            },
            buildProfiles: new[] { ShellFoundationProfile },
            requiredUpstreamIssues: Array.Empty<int>(),
            status: StatusCommittedActive,
            isProductionScene: true);

        private static readonly ProductionSceneRecord KingdomRecord = new ProductionSceneRecord(
            sceneId: KingdomSceneId,
            assetPath: SceneFolder + "/Kingdom.unity",
            sceneName: "Kingdom",
            role: RoleProductionHub,
            assetGuid: "dea6fe75315c3402fbfa8de764bd9873",
            requiredControllerType: "AL.UI.Kingdom.KingdomSceneController",
            startupMarkerId: KingdomSceneId,
            // Kingdom -> ChampionArena is a descriptor-only active record so the first-session 3D
            // scene stays loadable. Current KingdomSceneController still has no arena scene field.
            // The unsafe reset-to-Boot route is NOT an accepted production transition.
            transitionTargets: new[]
            {
                new SceneTransition(ChampionArenaSceneId, null, "ChampionArena", TransitionStatus.Active)
            },
            buildProfiles: new[] { ShellFoundationProfile },
            requiredUpstreamIssues: Array.Empty<int>(),
            status: StatusCommittedActive,
            isProductionScene: true);

        private static readonly ProductionSceneRecord ChampionArenaRecord = new ProductionSceneRecord(
            sceneId: ChampionArenaSceneId,
            assetPath: SceneFolder + "/ChampionArena.unity",
            sceneName: "ChampionArena",
            role: RoleFirstSessionGameplay,
            assetGuid: "9c8e973279bb149b49b9938b1781c775",
            requiredControllerType: "AL.ChampionMode.ChampionArenaSceneController",
            startupMarkerId: ChampionArenaSceneId,
            transitionTargets: new[]
            {
                new SceneTransition(KingdomSceneId, "_kingdomSceneName", "Kingdom", TransitionStatus.Active)
            },
            // First-session 3D destination after CharacterCreation. Shared Menu / 2.5D kingdom stay
            // locked until lordship; this only makes the scene loadable.
            buildProfiles: new[] { ShellFoundationProfile },
            requiredUpstreamIssues: Array.Empty<int>(),
            status: StatusCommittedActive,
            isProductionScene: true);

        private static readonly ProductionSceneRecord TestRecord = new ProductionSceneRecord(
            sceneId: TestSceneId,
            assetPath: "Assets/Test.unity",
            sceneName: "Test",
            role: RoleRepresentativeTestOnly,
            assetGuid: "30477a26c93c85546a8612b1c5716a60",
            requiredControllerType: string.Empty,
            startupMarkerId: string.Empty,
            transitionTargets: Array.Empty<SceneTransition>(),
            // Test carries no production build profile and is prohibited from normal Build Settings
            // (spec decision 1, section 6.5). It exists in inventory/test policy only.
            buildProfiles: Array.Empty<string>(),
            requiredUpstreamIssues: Array.Empty<int>(),
            status: StatusTestOnly,
            isProductionScene: false);

        private static readonly ReadOnlyCollection<ProductionSceneRecord> AllRecords =
            new ReadOnlyCollection<ProductionSceneRecord>(new[]
            {
                BootRecord,
                RealmSelectionRecord,
                CharacterCreationRecord,
                KingdomRecord,
                ChampionArenaRecord,
                TestRecord
            });

        private static readonly ReadOnlyCollection<ProductionSceneRecord> ProductionRecords =
            new ReadOnlyCollection<ProductionSceneRecord>(
                AllRecords.Where(record => record.IsProductionScene).ToArray());

        // Boot(0), RealmSelection(1), CharacterCreation(2), ChampionArena(3), Kingdom(4).
        // First-session spine is Boot → Realm → Create → ChampionArena. Kingdom stays loadable
        // for Boot/ChampionArena return routes and post-lordship hub work.
        private static readonly ReadOnlyCollection<ProductionSceneRecord> ShellFoundationRecords =
            new ReadOnlyCollection<ProductionSceneRecord>(new[]
            {
                BootRecord,
                RealmSelectionRecord,
                CharacterCreationRecord,
                ChampionArenaRecord,
                KingdomRecord
            });

        /// <summary>All six descriptor records (five production scenes plus the representative test scene).</summary>
        public static IReadOnlyList<ProductionSceneRecord> All => AllRecords;

        /// <summary>The five committed production scenes (excludes the representative Test scene).</summary>
        public static IReadOnlyList<ProductionSceneRecord> ProductionScenes => ProductionRecords;

        /// <summary>Boot, RealmSelection, CharacterCreation, ChampionArena, Kingdom in ShellFoundation order.</summary>
        public static IReadOnlyList<ProductionSceneRecord> ShellFoundationOrdered => ShellFoundationRecords;

        /// <summary>
        /// reset-to-Boot is an unsafe prototype route (stale spec section 4.3), never an accepted
        /// production transition. It has no descriptor transition and is not production reachable (#178/#137).
        /// </summary>
        public static bool IsResetToBootProductionReachable => false;

        public static bool TryGetById(string sceneId, out ProductionSceneRecord record)
        {
            record = AllRecords.FirstOrDefault(item => string.Equals(item.SceneId, sceneId, StringComparison.Ordinal));
            return record != null;
        }

        public static bool TryGetByAssetPath(string assetPath, out ProductionSceneRecord record)
        {
            record = AllRecords.FirstOrDefault(item => string.Equals(item.AssetPath, assetPath, StringComparison.Ordinal));
            return record != null;
        }

        public static bool TryGetBySceneName(string sceneName, out ProductionSceneRecord record)
        {
            record = AllRecords.FirstOrDefault(item => string.Equals(item.SceneName, sceneName, StringComparison.Ordinal));
            return record != null;
        }

        public static bool TryGetByControllerType(string controllerTypeName, out ProductionSceneRecord record)
        {
            record = string.IsNullOrEmpty(controllerTypeName)
                ? null
                : AllRecords.FirstOrDefault(item =>
                    string.Equals(item.RequiredControllerType, controllerTypeName, StringComparison.Ordinal));
            return record != null;
        }
    }

    public enum TransitionStatus
    {
        /// <summary>Wired in a serialized controller field on main and reachable in its owning scene.</summary>
        Active,

        /// <summary>Committed intent whose target is excluded from the active build profile (spec section 8).</summary>
        Deferred,

        /// <summary>Unsafe prototype/reset route that must not be production reachable (spec section 8).</summary>
        BlockedUnsafe
    }

    /// <summary>Immutable description of one scene-to-scene transition intent.</summary>
    public sealed class SceneTransition
    {
        public SceneTransition(string targetSceneId, string serializedFieldName, string serializedValue, TransitionStatus status)
        {
            TargetSceneId = targetSceneId ?? string.Empty;
            SerializedFieldName = serializedFieldName ?? string.Empty;
            SerializedValue = serializedValue ?? string.Empty;
            Status = status;
        }

        public string TargetSceneId { get; }

        /// <summary>Serialized controller field carrying the target scene name, or empty for descriptor-only intent.</summary>
        public string SerializedFieldName { get; }

        /// <summary>Exact, case-sensitive scene name string the transition resolves to.</summary>
        public string SerializedValue { get; }

        public TransitionStatus Status { get; }

        public bool HasSerializedField => SerializedFieldName.Length > 0;
    }

    /// <summary>Immutable production scene record. All collections are read-only copies.</summary>
    public sealed class ProductionSceneRecord
    {
        public ProductionSceneRecord(
            string sceneId,
            string assetPath,
            string sceneName,
            string role,
            string assetGuid,
            string requiredControllerType,
            string startupMarkerId,
            IEnumerable<SceneTransition> transitionTargets,
            IEnumerable<string> buildProfiles,
            IEnumerable<int> requiredUpstreamIssues,
            string status,
            bool isProductionScene)
        {
            SceneId = sceneId ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            Role = role ?? string.Empty;
            AssetGuid = assetGuid ?? string.Empty;
            RequiredControllerType = requiredControllerType ?? string.Empty;
            StartupMarkerId = startupMarkerId ?? string.Empty;
            TransitionTargets = Array.AsReadOnly((transitionTargets ?? Array.Empty<SceneTransition>()).ToArray());
            BuildProfiles = Array.AsReadOnly((buildProfiles ?? Array.Empty<string>()).ToArray());
            RequiredUpstreamIssues = Array.AsReadOnly((requiredUpstreamIssues ?? Array.Empty<int>()).ToArray());
            Status = status ?? string.Empty;
            IsProductionScene = isProductionScene;
        }

        public string SceneId { get; }
        public string AssetPath { get; }
        public string SceneName { get; }
        public string Role { get; }
        public string AssetGuid { get; }
        public string RequiredControllerType { get; }
        public string StartupMarkerId { get; }
        public IReadOnlyList<SceneTransition> TransitionTargets { get; }
        public IReadOnlyList<string> BuildProfiles { get; }
        public IReadOnlyList<int> RequiredUpstreamIssues { get; }
        public string Status { get; }
        public bool IsProductionScene { get; }

        /// <summary>True only for scenes that carry a startup marker (every production scene; never Test).</summary>
        public bool HasStartupMarker => IsProductionScene && StartupMarkerId.Length > 0;

        public bool IsInShellFoundation =>
            BuildProfiles.Contains(ProductionSceneDescriptor.ShellFoundationProfile);
    }
}
