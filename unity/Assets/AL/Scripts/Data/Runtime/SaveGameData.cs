using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Catalogs.MapDisclosure;

namespace AL.Data.Runtime
{
    [Serializable]
    public class SaveGameData
    {
        public const string CurrentSaveFormatId = "anotherlife.local-save";
        public const int CurrentSaveSchemaVersion = 2;
        public const int CurrentProfileInitializationVersion = 1;

        public string SaveFormatId;
        public int SaveSchemaVersion;
        public int ProfileInitializationVersion;
        // Canonical schema-v2 identity. Schema v1 serializes this as blank and
        // remains MigrationRequired until the witnessed installer persists it.
        public string ProfileId = string.Empty;
        public RealmId SelectedRealm;
        // Optional schema-v2 extension. Omitted identity-only or legacy
        // schema-2 saves keep SelectedRealm unchanged until the profile-bound
        // realm transaction installs committed metadata/receipt.
        public RealmSelectionAuthorityState RealmSelection;
        public List<ResourceData> Resources = new List<ResourceData>();
        public List<BuildingState> Buildings = new List<BuildingState>();
        public List<TroopInventoryData> Troops = new List<TroopInventoryData>();
        public List<AL.Core.Interfaces.ResearchState> Researches = new List<AL.Core.Interfaces.ResearchState>();
        public List<AL.Core.Interfaces.QuestState> Quests = new List<AL.Core.Interfaces.QuestState>();
        public List<NpcAffinityData> Reputation = new List<NpcAffinityData>();
        public List<FactionRepData> FactionReputations = new List<FactionRepData>();
        public PersonaData LordPersona = new PersonaData();
        public List<AL.Core.Interfaces.TerritoryData> Territories = new List<AL.Core.Interfaces.TerritoryData>();
        public List<RealmGemState> RealmGems = new List<RealmGemState>();
        public WishgateState Wishgate = new WishgateState();
        public string CurrentChapterId;
        public WarmasterState Warmaster = new WarmasterState();
        public ChampionCustomizationState ChampionCustomization = new ChampionCustomizationState();
        public List<OwnedEquipmentState> OwnedEquipment = new List<OwnedEquipmentState>();
        public List<AppliedBossLootRewardState> AppliedBossLootRewards = new List<AppliedBossLootRewardState>();
        public Nvs01ProgressData Nvs01Progress = new Nvs01ProgressData();
        // Optional schema-v1 extension. A missing value is an admitted legacy
        // save and resolves to the first tutorial step (or reconciles from an
        // already committed lordship result). Once present, Version and the
        // complete topology are validated fail-closed.
        public FirstWorldProgressData FirstWorldProgress;
        // Optional schema-v1 extension. Missing legacy state is admitted but
        // remains hidden until a matching authoritative snapshot is received.
        public MapDisclosurePersistentState MapDisclosure;
        public int WarzoneCredits;
        public long LastSavedTimestamp;
    }

    [Serializable]
    public sealed class FirstWorldProgressData
    {
        public const int CurrentVersion = 1;

        public int Version;
        public long Revision;
        public int TutorialStep;
        public int TeachingBeat;
        public int MovementConfirmationCount;
        public int BasicAttackConfirmationCount;
        public int CompletionEventCount;
        public int OmenOfferCount;
        public bool BlockTaught;
        public bool HandoffCommitted;
        public int ProofPhase;
        public string ProofQuestId = string.Empty;
        public string ProofQuestStateId = string.Empty;
        public string ProofObjectiveId = string.Empty;
        public string ProofDialogueId = string.Empty;
        public string ProofLastEventId = string.Empty;
        public string ProofChapterVariantId = string.Empty;
        public bool ProofOmenAccepted;
        public bool ProofAutoAccept;
        public string LastOperationId = string.Empty;
    }

    [Serializable]
    public class Nvs01ProgressData
    {
        public const int CurrentVersion = 1;

        public int Version;
        public string PacketVersion = string.Empty;
        public string PacketSha256 = string.Empty;
        public string QuestId = string.Empty;
        public long Revision;
        public string StateId = string.Empty;
        public List<Nvs01ObjectiveProgressData> Objectives =
            new List<Nvs01ObjectiveProgressData>();
        public string CurrentDialogueNodeId = string.Empty;
        public bool PendingChoice;
        public string PendingSemanticActionId = string.Empty;
        public string CommittedRealmId = string.Empty;
        public int EncounterStatus;
        public bool HasCurrentEncounter;
        public Nvs01EncounterRequestData CurrentEncounter =
            new Nvs01EncounterRequestData();
        public string LastEncounterCorrelationId = string.Empty;
        public bool HasLastEncounterOutcome;
        public int LastEncounterOutcome;
        public string LastEncounterEventId = string.Empty;
        public string LastEncounterSnapshotVersion = string.Empty;
        public string LastEncounterSnapshotReference = string.Empty;
        public bool HasLastOperation;
        public Nvs01OperationReceiptData LastOperation =
            new Nvs01OperationReceiptData();
        public List<string> ConsequenceIntentIds = new List<string>();
        public List<string> AcquiredArtifactIds = new List<string>();
        public List<string> AppliedEffectKeys = new List<string>();
        public string UnlockedChapterId = string.Empty;
    }

    [Serializable]
    public class Nvs01ObjectiveProgressData
    {
        public string ObjectiveId = string.Empty;
        public int Status;
    }

    [Serializable]
    public class Nvs01EncounterRequestData
    {
        public int ContractVersion;
        public string RequestId = string.Empty;
        public string CorrelationId = string.Empty;
        public string QuestId = string.Empty;
        public string StateId = string.Empty;
        public string ObjectiveId = string.Empty;
        public string HookId = string.Empty;
        public string LocationId = string.Empty;
        public string RealmId = string.Empty;
        public string SuccessEventId = string.Empty;
        public string FailureEventId = string.Empty;
        public string CancelledEventId = string.Empty;
        public string UnavailableEventId = string.Empty;
        public string ReturnScene = string.Empty;
    }

    [Serializable]
    public class Nvs01OperationReceiptData
    {
        public string OperationId = string.Empty;
        public string PayloadFingerprint = string.Empty;
        public int Status;
        public long Revision;
        public string StateId = string.Empty;
        public string EventId = string.Empty;
        public string CorrelationId = string.Empty;
        // Captures the verified generation that authorized this operation.
        // Schema-v1 operations must leave it blank.
        public string ExpectedGenerationFingerprint = string.Empty;
    }

    [Serializable]
    public class ResourceData
    {
        public ResourceType Type;
        public long Amount;
    }

    [Serializable]
    public class TroopInventoryData
    {
        public TroopType Type;
        public int Count;
        public int WoundedCount;
    }

    [Serializable]
    public class OwnedEquipmentState
    {
        public string EquipmentId;
        public string DisplayName;
        public EquipmentSlot Slot;
        public int AttackBonus;
        public int DefenseBonus;
        public int HealthBonus;
        public int Quantity;
        public string SourceBossId;
        public bool AnnounceWorldDrop;
        public long FirstAcquiredTimestamp;
        public long LastAcquiredTimestamp;
    }

    [Serializable]
    public class AppliedBossLootRewardState
    {
        public string EncounterId;
        public string RewardResultId;
        public string BossId;
        public string RewardDigest;
        public long CommittedTimestamp;
    }

    [Serializable]
    public class ChampionCustomizationState
    {
        // Additive nested slot. Omitted legacy saves resolve to the male base.
        public string BodyBaseId = "male";
        public string BodyPresetId = "average";
        public string HairStyleId = "short";
        public string ArmorStyleId = "realm_basic";
        public string FaceMarkId = "none";
        public string WeaponStyleId = "sword";
        public string OffhandStyleId = "shield";
        public float PrimaryR = 0.2f;
        public float PrimaryG = 0.4f;
        public float PrimaryB = 1.0f;
        public float HairR = 0.08f;
        public float HairG = 0.06f;
        public float HairB = 0.04f;
        public float SkinR = 0.72f;
        public float SkinG = 0.56f;
        public float SkinB = 0.42f;
        public float EyeR = 0.25f;
        public float EyeG = 0.58f;
        public float EyeB = 0.92f;
        public float AccentR = 0.85f;
        public float AccentG = 0.62f;
        public float AccentB = 0.18f;
        public bool CapeEnabled = true;
        public bool HelmetEnabled;
        // Additive schema-v1 nested slot for the 3D-first MVP loop.
        // Omitted on pre-change saves; default empty/false and stay loadable.
        public string ClassFamilyId = string.Empty;
        public bool IdentityConfirmed;
        public string LastResultId = string.Empty;
        // Additive schema-v1 nested slot. Omitted on pre-change saves.
        // People stay derived from SelectedRealm and are never stored here.
        public string Username = string.Empty;
    }

    [Serializable]
    public class RealmGemState
    {
        public string GemId;
        public RealmId HomeRealm;
        public int GemIndex;
        public bool IsAtHome = true;
        public bool IsDropped;
        public string CarrierId;
        public long LastDroppedTimestamp;
    }

    [Serializable]
    public sealed class RealmSelectionAuthorityState
    {
        public int Version;
        public bool Committed;
        public int SelectedRealm;
        public string ProfileId = string.Empty;
        public string TransactionId = string.Empty;
        public string CorrelationId = string.Empty;
        public string OperationId = string.Empty;
        public string EventId = string.Empty;
        public string CatalogVersion = string.Empty;
        public string Provenance = string.Empty;
        public string ReceiptFingerprint = string.Empty;
        public string ExpectedGenerationFingerprint = string.Empty;
        public long Revision;
    }

    [Serializable]
    public class WishgateState
    {
        public bool IsEarned;
        public string EarnReason;
        public string LastRewardId;
        public long LastRewardChosenTimestamp;
    }

    [Serializable]
    public class NpcAffinityData
    {
        public string NpcId;
        public float Affinity;
    }

    [Serializable]
    public class FactionRepData
    {
        public string FactionId;
        public int Reputation;
    }

    [Serializable]
    public class PersonaData
    {
        public int Warlord;
        public int Diplomat;
        public int Sage;
        public int Rogue;
    }
}
