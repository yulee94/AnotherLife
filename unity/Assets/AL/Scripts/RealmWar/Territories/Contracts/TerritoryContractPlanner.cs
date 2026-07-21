using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.RealmWar.Territories.Contracts
{
    public enum TerritoryDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum TerritoryQueryStatus
    {
        Available,
        Empty,
        Unavailable
    }

    public enum TerritoryCaptureStatus
    {
        Planned,
        NoChangeSameOwner,
        RejectedBlankId,
        RejectedUnknownTerritory,
        RejectedDomainMalformed,
        RejectedNoCommittedRealm,
        RejectedInvalidCapturer,
        RejectedUnauthorized,
        RejectedStaleOwner,
        RejectedStaleRevision,
        RejectedOverflow
    }

    public enum TerritoryIncomeStatus
    {
        Available,
        Unavailable
    }

    public sealed class TerritoryDefinition
    {
        public TerritoryDefinition(string id, string contentKey, RealmId initialOwner, ResourceType bonusType, long bonusAmount, bool isFortress, IEnumerable<RealmId> allowedOwners)
        {
            Id = id ?? string.Empty;
            ContentKey = contentKey ?? string.Empty;
            InitialOwner = initialOwner;
            BonusType = bonusType;
            BonusAmount = bonusAmount;
            IsFortress = isFortress;
            AllowedOwners = TerritoryContractPlanner.Freeze(allowedOwners ?? Array.Empty<RealmId>());
        }

        public string Id { get; }
        public string ContentKey { get; }
        public RealmId InitialOwner { get; }
        public ResourceType BonusType { get; }
        public long BonusAmount { get; }
        public bool IsFortress { get; }
        public IReadOnlyList<RealmId> AllowedOwners { get; }
    }

    public sealed class TerritoryStateRecord
    {
        public TerritoryStateRecord(string id, RealmId owner, long revision)
        {
            Id = id ?? string.Empty;
            Owner = owner;
            Revision = revision;
        }

        public string Id { get; }
        public RealmId Owner { get; }
        public long Revision { get; }
    }

    public sealed class TerritoryDiagnostic
    {
        public TerritoryDiagnostic(TerritoryDiagnosticSeverity severity, string code, string territoryId, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public TerritoryDiagnosticSeverity Severity { get; }
        public string Code { get; }
        public string TerritoryId { get; }
        public string Message { get; }
    }

    public sealed class TerritorySnapshot
    {
        public TerritorySnapshot(TerritoryDefinition definition, TerritoryStateRecord state, bool isSupported)
        {
            Definition = definition;
            State = state;
            IsSupported = isSupported;
        }

        public TerritoryDefinition Definition { get; }
        public TerritoryStateRecord State { get; }
        public bool IsSupported { get; }
    }

    public sealed class TerritoryQueryResult
    {
        public TerritoryQueryResult(TerritoryQueryStatus status, string catalogId, string stateRevisionHash, RealmId committedProfileRealm, IEnumerable<TerritorySnapshot> territories, IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            Status = status;
            CatalogId = catalogId ?? string.Empty;
            StateRevisionHash = stateRevisionHash ?? string.Empty;
            CommittedProfileRealm = committedProfileRealm;
            Territories = TerritoryContractPlanner.Freeze(territories ?? Array.Empty<TerritorySnapshot>());
            Diagnostics = TerritoryContractPlanner.FreezeDiagnostics(diagnostics ?? Array.Empty<TerritoryDiagnostic>());
        }

        public TerritoryQueryStatus Status { get; }
        public string CatalogId { get; }
        public string StateRevisionHash { get; }
        public RealmId CommittedProfileRealm { get; }
        public IReadOnlyList<TerritorySnapshot> Territories { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
    }

    public sealed class TerritoryCaptureAuthorization
    {
        public TerritoryCaptureAuthorization(string authorizationId, string territoryId, RealmId capturerRealm, RealmId expectedPreviousOwner, long expectedRevision)
        {
            AuthorizationId = authorizationId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            CapturerRealm = capturerRealm;
            ExpectedPreviousOwner = expectedPreviousOwner;
            ExpectedRevision = expectedRevision;
        }

        public string AuthorizationId { get; }
        public string TerritoryId { get; }
        public RealmId CapturerRealm { get; }
        public RealmId ExpectedPreviousOwner { get; }
        public long ExpectedRevision { get; }
    }

    public sealed class TerritoryCaptureRequest
    {
        public TerritoryCaptureRequest(string operationId, string territoryId, RealmId committedProfileRealm, RealmId expectedCapturerRealm, RealmId expectedPreviousOwner, long expectedRevision, TerritoryCaptureAuthorization authorization)
        {
            OperationId = operationId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            CommittedProfileRealm = committedProfileRealm;
            ExpectedCapturerRealm = expectedCapturerRealm;
            ExpectedPreviousOwner = expectedPreviousOwner;
            ExpectedRevision = expectedRevision;
            Authorization = authorization;
        }

        public string OperationId { get; }
        public string TerritoryId { get; }
        public RealmId CommittedProfileRealm { get; }
        public RealmId ExpectedCapturerRealm { get; }
        public RealmId ExpectedPreviousOwner { get; }
        public long ExpectedRevision { get; }
        public TerritoryCaptureAuthorization Authorization { get; }
    }

    public sealed class TerritoryCapturePlan
    {
        public TerritoryCapturePlan(TerritoryCaptureStatus status, string operationId, string territoryId, RealmId previousOwner, RealmId newOwner, long previousRevision, long newRevision, int warzoneCreditsDelta, int questProgressDelta, IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            Status = status;
            OperationId = operationId ?? string.Empty;
            TerritoryId = territoryId ?? string.Empty;
            PreviousOwner = previousOwner;
            NewOwner = newOwner;
            PreviousRevision = previousRevision;
            NewRevision = newRevision;
            WarzoneCreditsDelta = warzoneCreditsDelta;
            QuestProgressDelta = questProgressDelta;
            Diagnostics = TerritoryContractPlanner.FreezeDiagnostics(diagnostics ?? Array.Empty<TerritoryDiagnostic>());
        }

        public TerritoryCaptureStatus Status { get; }
        public string OperationId { get; }
        public string TerritoryId { get; }
        public RealmId PreviousOwner { get; }
        public RealmId NewOwner { get; }
        public long PreviousRevision { get; }
        public long NewRevision { get; }
        public int WarzoneCreditsDelta { get; }
        public int QuestProgressDelta { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
    }

    public sealed class TerritoryIncomeContribution
    {
        public TerritoryIncomeContribution(string territoryId, RealmId owner, ResourceType resourceType, long amountPerMinute)
        {
            TerritoryId = territoryId ?? string.Empty;
            Owner = owner;
            ResourceType = resourceType;
            AmountPerMinute = amountPerMinute;
        }

        public string TerritoryId { get; }
        public RealmId Owner { get; }
        public ResourceType ResourceType { get; }
        public long AmountPerMinute { get; }
    }

    public sealed class TerritoryIncomeSnapshot
    {
        public TerritoryIncomeSnapshot(TerritoryIncomeStatus status, string stateRevisionHash, IEnumerable<TerritoryIncomeContribution> contributions, IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            Status = status;
            StateRevisionHash = stateRevisionHash ?? string.Empty;
            Contributions = TerritoryContractPlanner.Freeze(contributions ?? Array.Empty<TerritoryIncomeContribution>());
            Diagnostics = TerritoryContractPlanner.FreezeDiagnostics(diagnostics ?? Array.Empty<TerritoryDiagnostic>());
        }

        public TerritoryIncomeStatus Status { get; }
        public string StateRevisionHash { get; }
        public IReadOnlyList<TerritoryIncomeContribution> Contributions { get; }
        public IReadOnlyList<TerritoryDiagnostic> Diagnostics { get; }
    }

    public sealed class TerritoryContractPlanner
    {
        public const string CurrentCatalogId = "territory_current_v1";
        public const int CaptureWarzoneCreditReward = 100;
        public const int CaptureQuestProgressReward = 1;

        private readonly IReadOnlyList<TerritoryDefinition> _definitions;

        public TerritoryContractPlanner(IEnumerable<TerritoryDefinition> definitions)
        {
            _definitions = Freeze(definitions ?? Array.Empty<TerritoryDefinition>());
        }

        public static TerritoryContractPlanner CreateCurrentBaseline()
        {
            var owners = new[] { RealmId.Stonehold, RealmId.Eldergrove, RealmId.Crownlands, RealmId.Umbral };
            return new TerritoryContractPlanner(new[]
            {
                new TerritoryDefinition("T1", "territory.iron_peaks", RealmId.Stonehold, ResourceType.Stone, 50, true, owners),
                new TerritoryDefinition("T2", "territory.silver_woods", RealmId.Eldergrove, ResourceType.Wood, 40, false, owners),
                new TerritoryDefinition("T3", "territory.golden_plains", RealmId.Crownlands, ResourceType.Gold, 20, false, owners),
                new TerritoryDefinition("T4", "territory.shadow_vale", RealmId.Umbral, ResourceType.Food, 60, true, owners),
                new TerritoryDefinition("T5", "territory.neutral_borderlands", RealmId.None, ResourceType.Gold, 10, false, owners),
            });
        }

        public TerritoryQueryResult BuildQuery(IEnumerable<TerritoryStateRecord> rawStates, RealmId committedProfileRealm)
        {
            var diagnostics = ValidateDefinitions().ToList();
            var states = (rawStates ?? Array.Empty<TerritoryStateRecord>()).ToList();
            var definitionsById = _definitions.Where(definition => !string.IsNullOrWhiteSpace(definition?.Id)).ToDictionary(definition => definition.Id, StringComparer.Ordinal);
            var snapshots = new List<TerritorySnapshot>();

            foreach (var state in states)
            {
                if (state == null)
                {
                    diagnostics.Add(Error("NullState", string.Empty, "Territory state row is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(state.Id))
                {
                    diagnostics.Add(Error("BlankStateId", state.Id, "Territory state ID is blank."));
                    continue;
                }

                if (!IsDefinedRealm(state.Owner))
                {
                    diagnostics.Add(Error("InvalidOwner", state.Id, "Territory owner enum is undefined."));
                }

                if (state.Revision < 0)
                {
                    diagnostics.Add(Error("InvalidRevision", state.Id, "Territory ownership revision is negative."));
                }
            }

            foreach (var group in states.Where(state => state != null && !string.IsNullOrWhiteSpace(state.Id)).GroupBy(state => state.Id, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    diagnostics.Add(Error("DuplicateStateId", group.Key, "Duplicate territory state IDs disable the group."));
                    continue;
                }

                TerritoryStateRecord state = group.Single();
                if (!definitionsById.TryGetValue(state.Id, out TerritoryDefinition definition))
                {
                    diagnostics.Add(new TerritoryDiagnostic(TerritoryDiagnosticSeverity.Warning, "PreservedUnknownTerritory", state.Id, "Unknown future territory is preserved but unsupported."));
                    snapshots.Add(new TerritorySnapshot(null, state, false));
                    continue;
                }

                if (!definition.AllowedOwners.Contains(state.Owner) && state.Owner != RealmId.None)
                {
                    diagnostics.Add(Error("OwnerForbidden", state.Id, "Territory owner is not allowed by the definition."));
                    continue;
                }

                if (definition.BonusAmount < 0)
                {
                    diagnostics.Add(Error("NegativeBonus", state.Id, "Territory bonus is negative."));
                    continue;
                }

                snapshots.Add(new TerritorySnapshot(definition, state, true));
            }

            foreach (var definition in _definitions.OrderBy(definition => definition.Id, StringComparer.Ordinal))
            {
                if (!snapshots.Any(snapshot => snapshot.Definition?.Id == definition.Id) && !diagnostics.Any(diagnostic => diagnostic.Code == "DuplicateStateId" && diagnostic.TerritoryId == definition.Id))
                {
                    diagnostics.Add(Error("MissingKnownTerritory", definition.Id, "Required known territory state is missing."));
                }
            }

            TerritoryQueryStatus status = diagnostics.Any(diagnostic => diagnostic.Severity == TerritoryDiagnosticSeverity.Error)
                ? TerritoryQueryStatus.Unavailable
                : snapshots.Count == 0 ? TerritoryQueryStatus.Empty : TerritoryQueryStatus.Available;

            return new TerritoryQueryResult(status, CurrentCatalogId, BuildStateHash(snapshots), committedProfileRealm, snapshots.OrderBy(snapshot => snapshot.State.Id, StringComparer.Ordinal), diagnostics);
        }

        public TerritoryCapturePlan PlanCapture(TerritoryQueryResult query, TerritoryCaptureRequest request)
        {
            var diagnostics = new List<TerritoryDiagnostic>();
            if (request == null)
            {
                return Reject(TerritoryCaptureStatus.RejectedDomainMalformed, string.Empty, string.Empty, RealmId.None, RealmId.None, 0, 0, diagnostics, "NullRequest", string.Empty, "Capture request is null.");
            }

            if (string.IsNullOrWhiteSpace(request.TerritoryId))
            {
                return Reject(TerritoryCaptureStatus.RejectedBlankId, request.OperationId, request.TerritoryId, RealmId.None, RealmId.None, 0, 0, diagnostics, "BlankTerritoryId", request.TerritoryId, "Capture territory ID is blank.");
            }

            if (query == null || query.Status != TerritoryQueryStatus.Available)
            {
                return Reject(TerritoryCaptureStatus.RejectedDomainMalformed, request.OperationId, request.TerritoryId, RealmId.None, RealmId.None, 0, 0, diagnostics, "DomainUnavailable", request.TerritoryId, "Territory domain is unavailable.");
            }

            TerritorySnapshot snapshot = query.Territories.SingleOrDefault(candidate => candidate.State.Id == request.TerritoryId);
            if (snapshot == null)
            {
                return Reject(TerritoryCaptureStatus.RejectedUnknownTerritory, request.OperationId, request.TerritoryId, RealmId.None, RealmId.None, 0, 0, diagnostics, "UnknownTerritory", request.TerritoryId, "Territory is unknown.");
            }

            if (!snapshot.IsSupported || snapshot.Definition == null)
            {
                return Reject(TerritoryCaptureStatus.RejectedUnknownTerritory, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "PreservedUnknownTerritory", request.TerritoryId, "Preserved unknown territory cannot be captured.");
            }

            if (request.CommittedProfileRealm == RealmId.None)
            {
                return Reject(TerritoryCaptureStatus.RejectedNoCommittedRealm, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "NoCommittedRealm", request.TerritoryId, "Committed profile realm is None.");
            }

            if (!IsDefinedRealm(request.CommittedProfileRealm) || !snapshot.Definition.AllowedOwners.Contains(request.CommittedProfileRealm))
            {
                return Reject(TerritoryCaptureStatus.RejectedInvalidCapturer, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "InvalidCapturer", request.TerritoryId, "Committed profile realm cannot capture this territory.");
            }

            if (request.ExpectedCapturerRealm != request.CommittedProfileRealm)
            {
                return Reject(TerritoryCaptureStatus.RejectedInvalidCapturer, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "CapturerMismatch", request.TerritoryId, "Expected capturer realm does not match committed profile realm.");
            }

            if (request.Authorization == null || string.IsNullOrWhiteSpace(request.Authorization.AuthorizationId))
            {
                return Reject(TerritoryCaptureStatus.RejectedUnauthorized, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "MissingAuthorization", request.TerritoryId, "Capture authorization is missing.");
            }

            if (request.Authorization.TerritoryId != request.TerritoryId || request.Authorization.CapturerRealm != request.CommittedProfileRealm)
            {
                return Reject(TerritoryCaptureStatus.RejectedUnauthorized, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "AuthorizationMismatch", request.TerritoryId, "Capture authorization does not match request.");
            }

            if (request.ExpectedPreviousOwner != snapshot.State.Owner || request.Authorization.ExpectedPreviousOwner != snapshot.State.Owner)
            {
                return Reject(TerritoryCaptureStatus.RejectedStaleOwner, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "StaleOwner", request.TerritoryId, "Expected previous owner does not match current owner.");
            }

            if (request.ExpectedRevision != snapshot.State.Revision || request.Authorization.ExpectedRevision != snapshot.State.Revision)
            {
                return Reject(TerritoryCaptureStatus.RejectedStaleRevision, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "StaleRevision", request.TerritoryId, "Expected revision does not match current revision.");
            }

            if (snapshot.State.Owner == request.CommittedProfileRealm)
            {
                return new TerritoryCapturePlan(TerritoryCaptureStatus.NoChangeSameOwner, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, 0, 0, diagnostics);
            }

            long newRevision;
            try
            {
                newRevision = checked(snapshot.State.Revision + 1);
            }
            catch (OverflowException)
            {
                return Reject(TerritoryCaptureStatus.RejectedOverflow, request.OperationId, request.TerritoryId, snapshot.State.Owner, snapshot.State.Owner, snapshot.State.Revision, snapshot.State.Revision, diagnostics, "RevisionOverflow", request.TerritoryId, "Ownership revision would overflow.");
            }

            return new TerritoryCapturePlan(TerritoryCaptureStatus.Planned, request.OperationId, request.TerritoryId, snapshot.State.Owner, request.CommittedProfileRealm, snapshot.State.Revision, newRevision, CaptureWarzoneCreditReward, CaptureQuestProgressReward, diagnostics);
        }

        public TerritoryIncomeSnapshot PlanIncome(TerritoryQueryResult query, RealmId selectedRealm)
        {
            if (query == null || query.Status != TerritoryQueryStatus.Available)
            {
                return new TerritoryIncomeSnapshot(TerritoryIncomeStatus.Unavailable, query?.StateRevisionHash ?? string.Empty, Array.Empty<TerritoryIncomeContribution>(), new[] { Error("DomainUnavailable", string.Empty, "Territory domain is unavailable.") });
            }

            var diagnostics = new List<TerritoryDiagnostic>();
            var contributions = new List<TerritoryIncomeContribution>();
            foreach (var snapshot in query.Territories.Where(snapshot => snapshot.IsSupported && snapshot.Definition != null && snapshot.State.Owner == selectedRealm).OrderBy(snapshot => snapshot.State.Id, StringComparer.Ordinal))
            {
                if (snapshot.Definition.BonusAmount < 0)
                {
                    diagnostics.Add(Error("NegativeBonus", snapshot.State.Id, "Territory bonus is negative."));
                    continue;
                }

                contributions.Add(new TerritoryIncomeContribution(snapshot.State.Id, snapshot.State.Owner, snapshot.Definition.BonusType, snapshot.Definition.BonusAmount));
            }

            return new TerritoryIncomeSnapshot(diagnostics.Count == 0 ? TerritoryIncomeStatus.Available : TerritoryIncomeStatus.Unavailable, query.StateRevisionHash, contributions, diagnostics);
        }

        public static IReadOnlyList<TerritoryStateRecord> CreateCurrentBaselineStates()
        {
            return Freeze(new[]
            {
                new TerritoryStateRecord("T1", RealmId.Stonehold, 0),
                new TerritoryStateRecord("T2", RealmId.Eldergrove, 0),
                new TerritoryStateRecord("T3", RealmId.Crownlands, 0),
                new TerritoryStateRecord("T4", RealmId.Umbral, 0),
                new TerritoryStateRecord("T5", RealmId.None, 0),
            });
        }

        internal static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>((values ?? Array.Empty<T>()).ToList());
        }

        internal static ReadOnlyCollection<TerritoryDiagnostic> FreezeDiagnostics(IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            return new ReadOnlyCollection<TerritoryDiagnostic>((diagnostics ?? Array.Empty<TerritoryDiagnostic>())
                .OrderBy(diagnostic => diagnostic.TerritoryId, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ToList());
        }

        private IEnumerable<TerritoryDiagnostic> ValidateDefinitions()
        {
            foreach (var definition in _definitions)
            {
                if (definition == null)
                {
                    yield return Error("NullDefinition", string.Empty, "Territory definition is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    yield return Error("BlankDefinitionId", definition.Id, "Territory definition ID is blank.");
                }

                if (string.IsNullOrWhiteSpace(definition.ContentKey))
                {
                    yield return Error("BlankContentKey", definition.Id, "Territory content key is blank.");
                }

                if (!IsDefinedRealm(definition.InitialOwner))
                {
                    yield return Error("InvalidInitialOwner", definition.Id, "Territory initial owner enum is undefined.");
                }

                if (!Enum.IsDefined(typeof(ResourceType), definition.BonusType))
                {
                    yield return Error("InvalidResource", definition.Id, "Territory bonus resource enum is undefined.");
                }

                if (definition.BonusAmount < 0)
                {
                    yield return Error("NegativeBonus", definition.Id, "Territory bonus is negative.");
                }

                if (definition.AllowedOwners == null || definition.AllowedOwners.Count == 0)
                {
                    yield return Error("NoAllowedOwners", definition.Id, "Territory definition has no allowed owners.");
                }
                else if (definition.AllowedOwners.Any(owner => owner == RealmId.None || !IsDefinedRealm(owner)))
                {
                    yield return Error("InvalidAllowedOwner", definition.Id, "Territory definition has an invalid allowed owner.");
                }
            }

            foreach (var group in _definitions.Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.Id)).GroupBy(definition => definition.Id, StringComparer.Ordinal))
            {
                if (group.Count() > 1)
                {
                    yield return Error("DuplicateDefinitionId", group.Key, "Duplicate territory definition ID.");
                }
            }
        }

        private static bool IsDefinedRealm(RealmId realm)
        {
            return Enum.IsDefined(typeof(RealmId), realm);
        }

        private static string BuildStateHash(IEnumerable<TerritorySnapshot> snapshots)
        {
            return string.Join("|", snapshots
                .Where(snapshot => snapshot?.State != null)
                .OrderBy(snapshot => snapshot.State.Id, StringComparer.Ordinal)
                .Select(snapshot => $"{snapshot.State.Id}:{snapshot.State.Owner}:{snapshot.State.Revision}:{(snapshot.IsSupported ? "supported" : "unsupported")}"));
        }

        private static TerritoryCapturePlan Reject(TerritoryCaptureStatus status, string operationId, string territoryId, RealmId previousOwner, RealmId newOwner, long previousRevision, long newRevision, List<TerritoryDiagnostic> diagnostics, string code, string diagnosticTerritoryId, string message)
        {
            diagnostics.Add(Error(code, diagnosticTerritoryId, message));
            return new TerritoryCapturePlan(status, operationId, territoryId, previousOwner, newOwner, previousRevision, newRevision, 0, 0, diagnostics);
        }

        private static TerritoryDiagnostic Error(string code, string territoryId, string message)
        {
            return new TerritoryDiagnostic(TerritoryDiagnosticSeverity.Error, code, territoryId, message);
        }
    }
}
