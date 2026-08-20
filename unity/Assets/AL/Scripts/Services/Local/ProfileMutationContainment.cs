using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AL.Core.Interfaces;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public enum ProfileMutationSurfaceDisposition
    {
        ReadOnly = 0,
        ContainedWriter = 1,
        NarrowLegacyOperation = 2,
        Dormant = 3,
        IndirectCaller = 4
    }

    public sealed class ProfileMutationSurfaceDescriptor
    {
        internal ProfileMutationSurfaceDescriptor(
            string stableId,
            Type contractType,
            Type implementationType,
            ProfileMutationSurfaceDisposition disposition,
            bool bootRegistration)
        {
            StableId = stableId;
            ContractType = contractType;
            ImplementationType = implementationType;
            Disposition = disposition;
            IsBootRegistration = bootRegistration;
        }

        public string StableId { get; }
        public Type ContractType { get; }
        public Type ImplementationType { get; }
        public ProfileMutationSurfaceDisposition Disposition { get; }
        public bool IsBootRegistration { get; }
    }

    public sealed class ProfileMutationCatalogValidationResult
    {
        private readonly IReadOnlyList<string> _diagnostics;

        internal ProfileMutationCatalogValidationResult(
            IEnumerable<string> diagnostics)
        {
            _diagnostics = new ReadOnlyCollection<string>(
                (diagnostics ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray());
        }

        public bool IsValid => _diagnostics.Count == 0;
        public IReadOnlyList<string> Diagnostics => _diagnostics;
    }

    public static class ProfileMutationSurfaceIds
    {
        public const string ManualSave = "profile.save.manual";
        public const string LifecycleSave = "profile.save.lifecycle";
        public const string DeleteSave = "profile.save.delete";
        public const string RealmSelection = "profile.realm-selection.schema1";
        public const string Nvs01Progress = "profile.nvs01.schema1";
        public const string Resource = "profile.resource";
        public const string Research = "profile.research";
        public const string Building = "profile.building";
        public const string Training = "profile.training";
        public const string WarzoneCredit = "profile.warzone-credit";
        public const string Warmaster = "profile.warmaster";
        public const string Territory = "profile.territory";
        public const string Reputation = "profile.reputation";
        public const string Faction = "profile.faction";
        public const string Persona = "profile.persona";
        public const string Quest = "profile.quest";
        public const string RealmGem = "profile.realm-gem";
        public const string BossLoot = "profile.boss-loot";
        public const string SideQuest = "profile.side-quest.dormant";
        public const string ChampionCustomization = "profile.champion-customization";
        public const string MvpLoop = "profile.mvp-loop.schema1";
        public const string ChampionArena = "profile.champion-arena.indirect";
        public const string DemoInitializer = "profile.demo-initializer.indirect";
        public const string KingdomNvs01 = "profile.kingdom-nvs01.indirect";
    }

    /// <summary>
    /// Closed executable inventory of the production boot registrations and
    /// every current direct, dormant, or indirect profile-mutation surface.
    /// Adding a boot service or a mutation caller must update this catalog and
    /// its completeness tests before publication can succeed.
    /// </summary>
    public static class ProfileMutationSurfaceCatalog
    {
        private const string Version = "al-profile-mutation-surface-v1";
        private const int MaximumRegistrationCount = 64;

        private static readonly ProfileMutationSurfaceDescriptor[] DescriptorArray =
        {
            Boot("boot.game-data", typeof(IGameDataService), typeof(LocalGameDataService), ProfileMutationSurfaceDisposition.ReadOnly),
            Boot("boot.save-root", typeof(ISaveGameService), typeof(LocalSaveGameService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.RealmSelection, typeof(IRealmService), typeof(LocalRealmService), ProfileMutationSurfaceDisposition.NarrowLegacyOperation),
            Boot(ProfileMutationSurfaceIds.Resource, typeof(IResourceService), typeof(LocalResourceService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Research, typeof(IResearchService), typeof(LocalResearchService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Building, typeof(IBuildingService), typeof(LocalBuildingService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Training, typeof(ITrainingService), typeof(LocalTrainingService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot("boot.battle-simulator", typeof(IBattleSimulator), typeof(AL.Battle.Simulator.FixedPointBattleSimulator), ProfileMutationSurfaceDisposition.ReadOnly),
            Boot(ProfileMutationSurfaceIds.WarzoneCredit, typeof(IWarzoneCreditService), typeof(LocalWarzoneCreditService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Warmaster, typeof(IWarmasterService), typeof(LocalWarmasterService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Territory, typeof(ITerritoryService), typeof(AL.RealmWar.Warzone.WarzoneService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot("boot.world-state", typeof(IWorldStateService), typeof(WorldStateService), ProfileMutationSurfaceDisposition.ReadOnly),
            Boot(ProfileMutationSurfaceIds.Reputation, typeof(IReputationService), typeof(ReputationService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Faction, typeof(IFactionService), typeof(FactionService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Persona, typeof(IPersonaService), typeof(PersonaService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot(ProfileMutationSurfaceIds.Quest, typeof(IQuestService), typeof(LocalQuestService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot("boot.story", typeof(IStoryService), typeof(LocalStoryService), ProfileMutationSurfaceDisposition.ReadOnly),
            Boot("boot.notification", typeof(INotificationService), typeof(LocalNotificationService), ProfileMutationSurfaceDisposition.ReadOnly),
            Boot(ProfileMutationSurfaceIds.RealmGem, typeof(IRealmGemService), typeof(LocalRealmGemService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Boot("boot.world-atlas", typeof(IWorldAtlasService), typeof(AL.RealmWar.World.LocalWorldAtlasService), ProfileMutationSurfaceDisposition.ReadOnly),
            Boot(ProfileMutationSurfaceIds.BossLoot, typeof(IBossLootService), typeof(LocalBossLootService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Extra(ProfileMutationSurfaceIds.ManualSave, typeof(ISaveGameService), typeof(LocalSaveGameService), ProfileMutationSurfaceDisposition.ContainedWriter),
            Extra(ProfileMutationSurfaceIds.LifecycleSave, typeof(ISaveGameService), typeof(AL.Core.Bootloader), ProfileMutationSurfaceDisposition.IndirectCaller),
            Extra(ProfileMutationSurfaceIds.DeleteSave, typeof(ISaveGameService), typeof(LocalSaveGameService), ProfileMutationSurfaceDisposition.Dormant),
            Extra(ProfileMutationSurfaceIds.Nvs01Progress, typeof(ISaveGameCandidateStore), typeof(AL.Narrative.Nvs01.Nvs01SaveGameMutationCommitter), ProfileMutationSurfaceDisposition.NarrowLegacyOperation),
            Extra(ProfileMutationSurfaceIds.MvpLoop, typeof(ILegacyMvpLoopCandidateStore), typeof(LocalSaveGameService), ProfileMutationSurfaceDisposition.NarrowLegacyOperation),
            Extra(ProfileMutationSurfaceIds.SideQuest, typeof(ISideQuestService), typeof(SideQuestService), ProfileMutationSurfaceDisposition.Dormant),
            Extra(ProfileMutationSurfaceIds.ChampionCustomization, null, typeof(AL.ChampionMode.Customization.ChampionCustomizationController), ProfileMutationSurfaceDisposition.ContainedWriter),
            Extra(ProfileMutationSurfaceIds.ChampionArena, null, typeof(AL.ChampionMode.ChampionArenaSceneController), ProfileMutationSurfaceDisposition.IndirectCaller),
            Extra(ProfileMutationSurfaceIds.DemoInitializer, null, typeof(AL.Utilities.DemoInitializer), ProfileMutationSurfaceDisposition.IndirectCaller),
            Extra(ProfileMutationSurfaceIds.KingdomNvs01, null, typeof(AL.UI.Kingdom.KingdomSceneController), ProfileMutationSurfaceDisposition.IndirectCaller)
        };

        private static readonly IReadOnlyList<ProfileMutationSurfaceDescriptor>
            Descriptors = new ReadOnlyCollection<ProfileMutationSurfaceDescriptor>(
                DescriptorArray);

        private static readonly HashSet<string> KnownSurfaceIds =
            new HashSet<string>(
                DescriptorArray.Select(descriptor => descriptor.StableId),
                StringComparer.Ordinal);

        public static string ContractVersion => Version;
        public static IReadOnlyList<ProfileMutationSurfaceDescriptor> ProductionSurfaces =>
            Descriptors;

        public static ProfileMutationCatalogValidationResult
            ValidateProductionCoverage(IEnumerable<Type> registeredContractTypes)
        {
            var diagnostics = new List<string>();
            Type[] supplied;
            try
            {
                supplied = registeredContractTypes == null
                    ? null
                    : registeredContractTypes.Take(MaximumRegistrationCount + 1).ToArray();
            }
            catch
            {
                return Invalid("AL-PROFILE-SURFACE-REGISTRATION-ENUMERATION");
            }

            if (supplied == null)
            {
                return Invalid("AL-PROFILE-SURFACE-REGISTRATIONS-MISSING");
            }

            if (supplied.Length > MaximumRegistrationCount)
            {
                diagnostics.Add("AL-PROFILE-SURFACE-REGISTRATION-BOUND");
            }

            if (supplied.Any(type => type == null))
            {
                diagnostics.Add("AL-PROFILE-SURFACE-REGISTRATION-NULL");
            }

            Type[] nonNull = supplied.Where(type => type != null).ToArray();
            if (nonNull.Distinct().Count() != nonNull.Length)
            {
                diagnostics.Add("AL-PROFILE-SURFACE-REGISTRATION-DUPLICATE");
            }

            Type[] expected = DescriptorArray
                .Where(descriptor => descriptor.IsBootRegistration)
                .Select(descriptor => descriptor.ContractType)
                .ToArray();
            foreach (Type missing in expected.Except(nonNull))
            {
                diagnostics.Add("AL-PROFILE-SURFACE-REGISTRATION-MISSING:" +
                    StableTypeName(missing));
            }

            foreach (Type unknown in nonNull.Except(expected))
            {
                diagnostics.Add("AL-PROFILE-SURFACE-REGISTRATION-UNKNOWN:" +
                    StableTypeName(unknown));
            }

            if (DescriptorArray.Any(descriptor =>
                    string.IsNullOrWhiteSpace(descriptor.StableId)) ||
                DescriptorArray.Select(descriptor => descriptor.StableId)
                    .Distinct(StringComparer.Ordinal).Count() != DescriptorArray.Length)
            {
                diagnostics.Add("AL-PROFILE-SURFACE-STABLE-ID-CATALOG");
            }

            return new ProfileMutationCatalogValidationResult(diagnostics);
        }

        internal static bool IsKnownSurface(string stableId) =>
            !string.IsNullOrWhiteSpace(stableId) &&
            KnownSurfaceIds.Contains(stableId);

        private static ProfileMutationCatalogValidationResult Invalid(string code) =>
            new ProfileMutationCatalogValidationResult(new[] { code });

        private static string StableTypeName(Type type) =>
            type?.FullName ?? "<null>";

        private static ProfileMutationSurfaceDescriptor Boot(
            string stableId,
            Type contractType,
            Type implementationType,
            ProfileMutationSurfaceDisposition disposition) =>
            new ProfileMutationSurfaceDescriptor(
                stableId,
                contractType,
                implementationType,
                disposition,
                true);

        private static ProfileMutationSurfaceDescriptor Extra(
            string stableId,
            Type contractType,
            Type implementationType,
            ProfileMutationSurfaceDisposition disposition) =>
            new ProfileMutationSurfaceDescriptor(
                stableId,
                contractType,
                implementationType,
                disposition,
                false);
    }

    /// <summary>
    /// Hard dormant activation boundary for ordinary profile mutation. This
    /// train deliberately has no setter, configuration key, environment flag,
    /// or authority-provider path capable of enabling production writes.
    /// Schema-v1 realm bootstrap and NVS-01 use separate typed adapters and do
    /// not pass through this latch.
    /// </summary>
    public static class ProfileMutationContainment
    {
        public static bool ProductionWriteActivationEnabled => false;

        internal static bool TryGetMutableSave(
            ISaveGameService saveGameService,
            string surfaceId,
            out SaveGameData save)
        {
            save = null;
            if (saveGameService == null ||
                !ProfileMutationSurfaceCatalog.IsKnownSurface(surfaceId) ||
                !ProductionWriteActivationEnabled)
            {
                return false;
            }

            // Intentionally unreachable until a separately reviewed train
            // replaces the hard latch with profile-bound authority.
            return false;
        }

        internal static bool CanInvokeManualSave(ISaveGameService saveGameService) =>
            saveGameService != null && ProductionWriteActivationEnabled;

        internal static bool CanInvokeLifecycleSave(ISaveGameService saveGameService) =>
            saveGameService != null && ProductionWriteActivationEnabled;

        internal static bool CanInvokeDeleteSave(ISaveGameService saveGameService) =>
            saveGameService != null && ProductionWriteActivationEnabled;
    }
}
