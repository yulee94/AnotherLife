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
        // Optional schema-v2 extension. Omitted legacy schema-2 saves stay
        // loadable; the first profile-bound death transaction installs
        // progression and death-penalty authority together.
        public ChampionProgressionState ChampionProgression;
        public DeathPenaltyAuthorityState DeathPenalty;
        // Optional schema-2 extension, installed at zero without resource conversion.
        public AL.Data.Catalogs.OathmarkWalletState OathmarkWallet;
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
        // Optional schema-v2 extension. Omitted legacy saves stay loadable as
        // an unearned Wishgate snapshot; the durable transaction installs this
        // authority only after a verified profile-bound commit.
        public WishgateTransactionState WishgateTransaction;
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
        // Optional schema-v2 extension. Missing legacy/schema-2 saves have no
        // active world event and do not invent one until a committed start.
        public WorldStatePersistentState WorldState;
        // Optional schema-v2 extension. Missing legacy/schema-2 saves admit
        // empty notification history/outbox until a durable record is committed.
        public NotificationHistoryPersistentState NotificationHistory;
        // Optional schema-v2 extension. Missing legacy/schema-2 saves admit
        // empty Guild City seasons until a trusted-clock commit persists them.
        public AL.Guilds.GuildCitySeasonPersistentState GuildCitySeason;
        // Optional schema-v2 extension. Missing legacy/schema-2 saves migrate
        // to empty raid-call/closed-instance authority until a trusted command commits.
        public AL.Guilds.GuildRaidMusterPersistentState GuildRaidMuster;
        public int WarzoneCredits;
        public long LastSavedTimestamp;
        // Optional schema-v1 extension. Missing legacy saves admit empty
        // receipts/outbox and derive ownership revisions as 0 from Territories.
        public TerritoryCaptureLedgerData TerritoryCaptureLedger;
        // Optional schema-v2 extension. Missing legacy schema-2 saves admit
        // no catch-up receipt; OfflineProgressApplied stays false until a
        // verified catch-up commit installs this marker.
        public OfflineProductionCatchUpState OfflineProductionCatchUp;
    }

    [Serializable]
    public sealed class OfflineProductionCatchUpState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public string OperationId = string.Empty;
        public string ReceiptId = string.Empty;
        public string ProfileId = string.Empty;
        public string VerifiedGenerationFingerprint = string.Empty;
        public long LastVerifiedTimestamp;
        public long CatchUpUntilTimestamp;
        public long CappedElapsedSeconds;
        public string CatalogId = string.Empty;
        public string CatalogSha256 = string.Empty;
        public string SourceRevision = string.Empty;
        public List<OfflineProductionDeltaRecord> Deltas =
            new List<OfflineProductionDeltaRecord>();
    }

    [Serializable]
    public sealed class OfflineProductionDeltaRecord
    {
        public ResourceType ResourceType;
        public long Amount;
    }

    [Serializable]
    public sealed class TerritoryCaptureLedgerData
    {
        public const int CurrentVersion = 1;

        public int Version;
        public string CatalogId = string.Empty;
        public string CatalogRawSha256 = string.Empty;
        public string StateRevisionHash = string.Empty;
        public string ProfileSessionId = string.Empty;
        public List<TerritoryOwnershipRevisionData> Revisions =
            new List<TerritoryOwnershipRevisionData>();
        public List<TerritoryCaptureReceiptRecord> Receipts =
            new List<TerritoryCaptureReceiptRecord>();
        public List<TerritoryCaptureOutboxRecord> Outbox =
            new List<TerritoryCaptureOutboxRecord>();
    }

    [Serializable]
    public sealed class TerritoryOwnershipRevisionData
    {
        public string TerritoryId = string.Empty;
        public long Revision;
    }

    [Serializable]
    public sealed class TerritoryCaptureReceiptRecord
    {
        public string ReceiptId = string.Empty;
        public string OperationId = string.Empty;
        public string SemanticHash = string.Empty;
        public int Durability;
        public string ResultId = string.Empty;
        public string EventId = string.Empty;
        public string TerritoryId = string.Empty;
        public RealmId PreviousOwner;
        public RealmId NewOwner;
        public long PreviousRevision;
        public long NewRevision;
        public int WarzoneCreditsDelta;
        public int QuestProgressDelta;
        public string CatalogId = string.Empty;
        public int CatalogSchemaVersion;
        public int CatalogContentVersion;
        public string CatalogSourceRevision = string.Empty;
        public string CatalogRawSha256 = string.Empty;
        public string StateRevisionHash = string.Empty;
        public string ProfileSessionId = string.Empty;
        public string AuthorizationId = string.Empty;
        public string AuthorizationSourceResultId = string.Empty;
        public string AuthorizationSourceResultHash = string.Empty;

        public static TerritoryCaptureReceiptRecord FromReceipt(
            AL.RealmWar.Territories.Contracts.TerritoryCaptureReceipt receipt)
        {
            if (receipt == null)
            {
                return null;
            }

            return new TerritoryCaptureReceiptRecord
            {
                ReceiptId = receipt.ReceiptId,
                OperationId = receipt.OperationId,
                SemanticHash = receipt.SemanticHash,
                Durability = (int)receipt.Durability,
                ResultId = receipt.ResultId,
                EventId = receipt.EventId,
                TerritoryId = receipt.TerritoryId,
                PreviousOwner = receipt.PreviousOwner,
                NewOwner = receipt.NewOwner,
                PreviousRevision = receipt.PreviousRevision,
                NewRevision = receipt.NewRevision,
                WarzoneCreditsDelta = receipt.WarzoneCreditsDelta,
                QuestProgressDelta = receipt.QuestProgressDelta,
                CatalogId = receipt.CatalogId,
                CatalogSchemaVersion = receipt.CatalogSchemaVersion,
                CatalogContentVersion = receipt.CatalogContentVersion,
                CatalogSourceRevision = receipt.CatalogSourceRevision,
                CatalogRawSha256 = receipt.CatalogRawSha256,
                StateRevisionHash = receipt.StateRevisionHash,
                ProfileSessionId = receipt.ProfileSessionId,
                AuthorizationId = receipt.AuthorizationId,
                AuthorizationSourceResultId = receipt.AuthorizationSourceResultId,
                AuthorizationSourceResultHash = receipt.AuthorizationSourceResultHash
            };
        }

        public AL.RealmWar.Territories.Contracts.TerritoryCaptureReceipt ToReceipt()
        {
            return new AL.RealmWar.Territories.Contracts.TerritoryCaptureReceipt(
                ReceiptId,
                OperationId,
                SemanticHash,
                (AL.RealmWar.Territories.Contracts.TerritoryOperationDurability)Durability,
                ResultId,
                EventId,
                TerritoryId,
                PreviousOwner,
                NewOwner,
                PreviousRevision,
                NewRevision,
                WarzoneCreditsDelta,
                QuestProgressDelta,
                new AL.RealmWar.Territories.Contracts.TerritoryCatalogIdentity(
                    CatalogId,
                    CatalogSchemaVersion,
                    CatalogContentVersion,
                    CatalogSourceRevision,
                    CatalogRawSha256),
                StateRevisionHash,
                ProfileSessionId,
                AuthorizationId,
                AuthorizationSourceResultId,
                AuthorizationSourceResultHash);
        }
    }

    [Serializable]
    public sealed class TerritoryCaptureOutboxRecord
    {
        public string EventId = string.Empty;
        public string CaptureOperationId = string.Empty;
        public string TerritoryId = string.Empty;
        public RealmId PreviousOwner;
        public RealmId NewOwner;
        public long PreviousRevision;
        public long NewRevision;
        public string CatalogId = string.Empty;
        public int CatalogSchemaVersion;
        public int CatalogContentVersion;
        public string CatalogSourceRevision = string.Empty;
        public string CatalogRawSha256 = string.Empty;
        public string StateRevisionHash = string.Empty;
        public string ProfileSessionId = string.Empty;
        public string AuthorizationId = string.Empty;
        public string AuthorizationSourceResultId = string.Empty;
        public string AuthorizationSourceResultHash = string.Empty;
        public string ReceiptId = string.Empty;

        public static TerritoryCaptureOutboxRecord FromEvent(
            AL.RealmWar.Territories.Contracts.TerritoryCaptureCommittedEvent committedEvent)
        {
            if (committedEvent == null)
            {
                return null;
            }

            return new TerritoryCaptureOutboxRecord
            {
                EventId = committedEvent.EventId,
                CaptureOperationId = committedEvent.CaptureOperationId,
                TerritoryId = committedEvent.TerritoryId,
                PreviousOwner = committedEvent.PreviousOwner,
                NewOwner = committedEvent.NewOwner,
                PreviousRevision = committedEvent.PreviousRevision,
                NewRevision = committedEvent.NewRevision,
                CatalogId = committedEvent.CatalogId,
                CatalogSchemaVersion = committedEvent.CatalogSchemaVersion,
                CatalogContentVersion = committedEvent.CatalogContentVersion,
                CatalogSourceRevision = committedEvent.CatalogSourceRevision,
                CatalogRawSha256 = committedEvent.CatalogRawSha256,
                StateRevisionHash = committedEvent.StateRevisionHash,
                ProfileSessionId = committedEvent.ProfileSessionId,
                AuthorizationId = committedEvent.AuthorizationId,
                AuthorizationSourceResultId = committedEvent.AuthorizationSourceResultId,
                AuthorizationSourceResultHash = committedEvent.AuthorizationSourceResultHash,
                ReceiptId = committedEvent.ReceiptId
            };
        }
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
        public List<string> AppliedOperationIds = new List<string>();
        public List<Nvs01ConsequenceApplicationReceiptData> ApplicationReceipts =
            new List<Nvs01ConsequenceApplicationReceiptData>();
        public string UnlockedChapterId = string.Empty;
    }

    [Serializable]
    public class Nvs01ConsequenceApplicationReceiptData
    {
        public int ContractVersion;
        public int Kind;
        public string OperationId = string.Empty;
        public string ProfileId = string.Empty;
        public string ExpectedGenerationFingerprint = string.Empty;
        public string CausalOperationId = string.Empty;
        public string CausalPayloadFingerprint = string.Empty;
        public string PredecessorReceiptFingerprint = string.Empty;
        public string PredecessorExpectedGenerationFingerprint = string.Empty;
        public string RealmId = string.Empty;
        public string CorrelationId = string.Empty;
        public long ExpectedQuestRevision;
        public long CandidateQuestRevision;
        public List<string> EffectKeys = new List<string>();
        public string TargetChapterId = string.Empty;
        public string TechnicalCurrencyId = string.Empty;
        public long PreviousGoldBalance;
        public long ResultingGoldBalance;
        public float PreviousValeriusAffinity;
        public float ResultingValeriusAffinity;
        public string PreviousChapterId = string.Empty;
        public string ResultingChapterId = string.Empty;
        public string PlanFingerprint = string.Empty;
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
    public sealed class ChampionProgressionState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public string ProfileId = string.Empty;
        public string CharacterId = string.Empty;
        public string AccountId = string.Empty;
        public int CurrentLevel;
        public int MaximumLevel;
        public long InLevelExperienceUnits;
        public long ExperienceUnitsPerLevel;
        public string ProgressionRevision = string.Empty;
        public string LevelCapPolicyId = string.Empty;
        public string LevelCapPolicyRevision = string.Empty;
    }

    [Serializable]
    public sealed class DeathPenaltyAuthorityState
    {
        public const int CurrentVersion = 1;
        public const int OutcomeNone = 0;
        public const int OutcomeBelowMaxCommitted = 1;
        public const int OutcomeOathmarkPaymentRequired = 2;

        public int Version;
        public int Status;
        public int Outcome;
        public string ProfileId = string.Empty;
        public string CharacterId = string.Empty;
        public string AccountId = string.Empty;
        public string DeathEventId = string.Empty;
        public string CombatSessionId = string.Empty;
        public string EncounterAttemptId = string.Empty;
        public string InstanceId = string.Empty;
        public long DeathOrdinal;
        public string DeathStateRevision = string.Empty;
        public string OperationId = string.Empty;
        public string RequestFingerprint = string.Empty;
        public string DeathFingerprint = string.Empty;
        public string ReceiptHash = string.Empty;
        public int Branch;
        public string AfterProgressionRevision = string.Empty;
        public string LedgerVersion = string.Empty;
        public string LedgerRevision = string.Empty;
        public string ExpectedGenerationFingerprint = string.Empty;
        public long Revision;
        public System.Collections.Generic.List<DeathPenaltyReceiptState> Receipts =
            new System.Collections.Generic.List<DeathPenaltyReceiptState>();
    }

    [Serializable]
    public sealed class DeathPenaltyReceiptState
    {
        public string OperationId = string.Empty;
        public string RequestFingerprint = string.Empty;
        public string DeathFingerprint = string.Empty;
        public string ReceiptHash = string.Empty;
        public string AccountId = string.Empty;
        public string ProfileId = string.Empty;
        public string CharacterId = string.Empty;
        public string PolicyVersion = string.Empty;
        public string LevelCapPolicyId = string.Empty;
        public string LevelCapPolicyRevision = string.Empty;
        public int Branch;
        public int BeforeLevel;
        public int AfterLevel;
        public int MaximumLevel;
        public long ExperienceUnitsPerLevel;
        public long BeforeInLevelExperienceUnits;
        public long AfterInLevelExperienceUnits;
        public string BeforeProgressionRevision = string.Empty;
        public string AfterProgressionRevision = string.Empty;
        public string PlanHash = string.Empty;
        public bool RequiresProgressionWrite;
        public bool RevivalCommitted;
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
    public sealed class WishgateTransactionState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public int Status;
        public long Revision;
        public int Phase;
        public string EntitlementId = string.Empty;
        public string EarnReasonId = string.Empty;
        public string RewardId = string.Empty;
        public string RewardApplicationId = string.Empty;
        public long EarnedUtcSeconds;
        public long SelectedUtcSeconds;
        public long AppliedUtcSeconds;
        public long CommittedUtcSeconds;
        public long EntitlementRevision;
        public bool EntitlementIsSupported = true;
        public bool IsComplete = true;
        public string LastOperationId = string.Empty;
        public string LastEventId = string.Empty;
        public string LastRequestFingerprint = string.Empty;
        public string ReceiptHash = string.Empty;
        public string PostCommitNotificationCorrelationId = string.Empty;
        public string AppliedRewardApplicationId = string.Empty;
        public System.Collections.Generic.List<WishgateTransitionRecordState> Records =
            new System.Collections.Generic.List<WishgateTransitionRecordState>();
    }

    [Serializable]
    public sealed class WishgateTransitionRecordState
    {
        public string OperationId = string.Empty;
        public string EventId = string.Empty;
        public string CorrelationId = string.Empty;
        public int Operation;
        public string RequestFingerprint = string.Empty;
        public string EntitlementId = string.Empty;
        public string EarnReasonId = string.Empty;
        public string RewardId = string.Empty;
        public string RewardApplicationId = string.Empty;
        public int ResultingPhase;
        public long ResultingSnapshotRevision;
        public long ResultingEntitlementRevision;
        public long PlannedUtcSeconds;
        public string ResultingStateHash = string.Empty;
        public string PlanHash = string.Empty;
        public string PostCommitNotificationCorrelationId = string.Empty;
        public bool IsSupported = true;
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

    [Serializable]
    public sealed class WorldStatePersistentState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public long SnapshotRevision;
        public long EffectRevision;
        public long LastTrustedUtcSeconds;
        public string PolicyRevision = string.Empty;
        public string CatalogRevision = string.Empty;
        public bool HasActiveInstance;
        public WorldStateInstanceRecord ActiveInstance = new WorldStateInstanceRecord();
        public List<WorldStateInstanceRecord> CompletedHistory =
            new List<WorldStateInstanceRecord>();
        public List<WorldStateReceiptRecord> OperationReceipts =
            new List<WorldStateReceiptRecord>();
    }

    [Serializable]
    public sealed class WorldStateInstanceRecord
    {
        public string InstanceId = string.Empty;
        public string DefinitionId = string.Empty;
        public int DefinitionSchemaVersion;
        public string DefinitionContentVersion = string.Empty;
        public string DefinitionSourceRevision = string.Empty;
        public string CorrelationId = string.Empty;
        public string OperationId = string.Empty;
        public string SourceSystemId = string.Empty;
        public string ExclusiveGroup = string.Empty;
        public int State;
        public long ScheduledAtUtcSeconds;
        public long StartedAtUtcSeconds;
        public long ExpectedEndAtUtcSeconds;
        public long CompletedAtUtcSeconds;
        public int CompletionReason;
        public long Revision;
        public long CommittedEffectRevision;
        public List<WorldStateResolvedEffectRecord> ResolvedEffects =
            new List<WorldStateResolvedEffectRecord>();
    }

    [Serializable]
    public sealed class WorldStateResolvedEffectRecord
    {
        public string EffectId = string.Empty;
        public string ConsumerId = string.Empty;
        public int Operation;
        public string ParameterHash = string.Empty;
        public int ConsumerPlanSchemaVersion;
        public bool Required;
        public int RemovalOrder;
        public List<WorldStateEffectParameterRecord> Parameters =
            new List<WorldStateEffectParameterRecord>();
    }

    [Serializable]
    public sealed class WorldStateEffectParameterRecord
    {
        public string Name = string.Empty;
        public int Kind;
        public long IntegerValue;
        public double NumberValue;
        public bool BooleanValue;
        public string ReferenceValue = string.Empty;
    }

    [Serializable]
    public sealed class WorldStateReceiptRecord
    {
        public string OperationId = string.Empty;
        public string CorrelationId = string.Empty;
        public string SemanticHash = string.Empty;
        public int TransitionKind;
        public string InstanceId = string.Empty;
        public long CommittedRevision;
        public WorldStateInstanceRecord ResultingInstance = new WorldStateInstanceRecord();
    }

    [Serializable]
    public sealed class NotificationHistoryPersistentState
    {
        public const int CurrentVersion = 1;

        public int Version;
        public List<NotificationHistoryRecord> Records = new List<NotificationHistoryRecord>();
        public List<NotificationHistoryRecord> Outbox = new List<NotificationHistoryRecord>();
    }

    [Serializable]
    public sealed class NotificationHistoryRecord
    {
        public string RecordId = string.Empty;
        public int NotificationSchemaVersion;
        public string DefinitionId = string.Empty;
        public int DefinitionVersion;
        public string SourceSystemId = string.Empty;
        public string CorrelationId = string.Empty;
        public long OccurredAtUtcTicks;
        public List<NotificationHistoryParameterRecord> Parameters =
            new List<NotificationHistoryParameterRecord>();
        public int State;
        public long AcknowledgedAtUtcTicks;
        public long DismissedAtUtcTicks;
        public long ExpiresAtUtcTicks;
        public long LastDeliveryAttemptUtcTicks;
        public int DeliveryAttemptCount;
        public string SupersededByRecordId = string.Empty;
        public int DurabilityPolicy;
        public int PrivacyClass;
        public bool RequiresAcknowledgement;
    }

    [Serializable]
    public sealed class NotificationHistoryParameterRecord
    {
        public string Name = string.Empty;
        public int Kind;
        public string TextValue = string.Empty;
    }
}
