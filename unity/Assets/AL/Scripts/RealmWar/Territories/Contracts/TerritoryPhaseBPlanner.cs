using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AL.Core;

namespace AL.RealmWar.Territories.Contracts
{
    public sealed class TerritoryPhaseBPlanner
    {
        private readonly TerritoryContractPlanner _queryPlanner;
        private readonly object _queryProvenance = new object();
        private readonly object _planProvenance = new object();

        public TerritoryPhaseBPlanner(TerritoryPhaseBCatalog catalog)
        {
            Catalog = catalog ?? new TerritoryPhaseBCatalog(
                null,
                Array.Empty<TerritoryDefinition>(),
                Array.Empty<TerritoryCaptureRewardProfile>(),
                Array.Empty<TerritoryAliasDefinition>());
            _queryPlanner = new TerritoryContractPlanner(Catalog.Definitions, _queryProvenance);
        }

        public TerritoryPhaseBCatalog Catalog { get; }

        public static TerritoryPhaseBPlanner CreateCurrentBaseline()
        {
            TerritoryCaptureRewardProfile[] rewards =
            {
                new TerritoryCaptureRewardProfile(
                    TerritoryContractPlanner.CurrentRewardProfileId,
                    TerritoryContractPlanner.CaptureWarzoneCreditReward,
                    "CaptureTerritory",
                    TerritoryContractPlanner.CaptureQuestProgressReward)
            };
            RealmId[] owners =
            {
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Crownlands,
                RealmId.Umbral
            };
            TerritoryDefinition[] definitions =
            {
                BaselineDefinition("T1", "territory.iron_peaks", RealmId.Stonehold, ResourceType.Stone, 50, true, false, owners),
                BaselineDefinition("T2", "territory.silver_woods", RealmId.Eldergrove, ResourceType.Wood, 40, false, false, owners),
                BaselineDefinition("T3", "territory.golden_plains", RealmId.Crownlands, ResourceType.Gold, 20, false, false, owners),
                BaselineDefinition("T4", "territory.shadow_vale", RealmId.Umbral, ResourceType.Food, 60, true, false, owners),
                BaselineDefinition("T5", "territory.neutral_borderlands", RealmId.None, ResourceType.Gold, 10, false, true, owners)
            };
            TerritoryAliasDefinition[] aliases = Array.Empty<TerritoryAliasDefinition>();
            TerritoryCatalogIdentity identity = CreateIdentity(
                TerritoryContractPlanner.CurrentCatalogId,
                1,
                1,
                "territory-source-v1",
                definitions,
                rewards,
                aliases);
            return new TerritoryPhaseBPlanner(
                new TerritoryPhaseBCatalog(identity, definitions, rewards, aliases));
        }

        public static TerritoryCatalogIdentity CreateIdentity(
            string catalogId,
            int schemaVersion,
            int contentVersion,
            string sourceRevision,
            IEnumerable<TerritoryDefinition> definitions,
            IEnumerable<TerritoryCaptureRewardProfile> rewards,
            IEnumerable<TerritoryAliasDefinition> aliases)
        {
            return new TerritoryCatalogIdentity(
                catalogId,
                schemaVersion,
                contentVersion,
                sourceRevision,
                TerritorySemanticHasher.HashCatalogPayload(definitions, rewards, aliases));
        }

        public TerritoryCatalogValidationResult ValidateCatalog()
        {
            var diagnostics = new List<TerritoryDiagnostic>();
            string semanticHash = TerritorySemanticHasher.HashCatalogPayload(
                Catalog.Definitions,
                Catalog.RewardProfiles,
                Catalog.Aliases);

            ValidateIdentity(Catalog.Identity, semanticHash, diagnostics);
            if (Catalog.Definitions.Count > TerritoryTechnicalLimits.MaximumDefinitions)
            {
                diagnostics.Add(Error(
                    "DefinitionLimitExceeded",
                    string.Empty,
                    "Territory definitions exceed the bounded planner limit."));
            }

            if (Catalog.Definitions.Count == 0)
            {
                diagnostics.Add(Error(
                    "EmptyDefinitionCatalog",
                    string.Empty,
                    "Current territory catalog must contain at least one definition."));
            }

            if (Catalog.RewardProfiles.Count > TerritoryTechnicalLimits.MaximumRewardProfiles)
            {
                diagnostics.Add(Error(
                    "RewardProfileLimitExceeded",
                    string.Empty,
                    "Territory reward profiles exceed the bounded planner limit."));
            }

            if (Catalog.RewardProfiles.Count == 0)
            {
                diagnostics.Add(Error(
                    "EmptyRewardCatalog",
                    string.Empty,
                    "Current territory catalog must contain at least one reward profile."));
            }

            if (Catalog.Aliases.Count > TerritoryTechnicalLimits.MaximumAliases)
            {
                diagnostics.Add(Error(
                    "AliasLimitExceeded",
                    string.Empty,
                    "Territory aliases exceed the bounded planner limit."));
            }

            ValidateRewards(diagnostics);
            ValidateDefinitions(diagnostics);
            ValidateAliases(diagnostics);

            return new TerritoryCatalogValidationResult(
                diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error)
                    ? TerritoryCatalogValidationStatus.Invalid
                    : TerritoryCatalogValidationStatus.Valid,
                semanticHash,
                diagnostics);
        }

        public TerritoryQueryResult BuildQuery(
            IEnumerable<TerritoryStateRecord> rawStates,
            RealmId committedProfileRealm)
        {
            return BuildQuery(rawStates, committedProfileRealm, string.Empty);
        }

        public TerritoryQueryResult BuildQuery(
            IEnumerable<TerritoryStateRecord> rawStates,
            RealmId committedProfileRealm,
            string profileSessionId)
        {
            TerritoryCatalogValidationResult catalog = ValidateCatalog();
            var queryDiagnostics = catalog.Diagnostics.ToList();
            if (!IsTechnicalId(profileSessionId))
            {
                queryDiagnostics.Add(Error(
                    "InvalidProfileSession",
                    string.Empty,
                    "Territory query requires a valid committed profile/session identity."));
            }

            if (catalog.Status != TerritoryCatalogValidationStatus.Valid ||
                queryDiagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                return new TerritoryQueryResult(
                    TerritoryQueryStatus.Unavailable,
                    Catalog.Identity?.CatalogId ?? string.Empty,
                    string.Empty,
                    committedProfileRealm,
                    Array.Empty<TerritorySnapshot>(),
                    queryDiagnostics,
                    _queryProvenance,
                    Catalog.Identity,
                    profileSessionId);
            }

            TerritoryQueryResult query = _queryPlanner.BuildQuery(rawStates, committedProfileRealm);
            return new TerritoryQueryResult(
                query.Status,
                Catalog.Identity.CatalogId,
                query.StateRevisionHash,
                query.CommittedProfileRealm,
                query.Territories,
                query.Diagnostics,
                _queryProvenance,
                Catalog.Identity,
                profileSessionId);
        }

        public TerritoryMigrationPlan PlanInitialization(
            TerritoryInitializationRequest request,
            IEnumerable<TerritoryStateRecord> rawStates,
            IEnumerable<TerritoryOperationReceipt> receipts)
        {
            var diagnostics = new List<TerritoryDiagnostic>();
            TerritoryCatalogValidationResult catalogValidation = ValidateCatalog();
            diagnostics.AddRange(catalogValidation.Diagnostics);
            if (catalogValidation.Status != TerritoryCatalogValidationStatus.Valid)
            {
                return MigrationReject(
                    TerritoryMigrationStatus.Rejected,
                    request?.OperationId,
                    string.Empty,
                    string.Empty,
                    diagnostics,
                    null);
            }

            if (request == null)
            {
                diagnostics.Add(Error("NullInitializationRequest", string.Empty, "Territory initialization request is null."));
                return MigrationReject(TerritoryMigrationStatus.Rejected, string.Empty, string.Empty, string.Empty, diagnostics, null);
            }

            if (!IsTechnicalId(request.OperationId))
            {
                diagnostics.Add(Error("InvalidOperationId", string.Empty, "Territory initialization operation ID is invalid."));
            }

            if (!Enum.IsDefined(typeof(TerritoryInitializationMode), request.Mode))
            {
                diagnostics.Add(Error("InvalidInitializationMode", string.Empty, "Territory initialization mode is undefined."));
            }
            else if (request.Mode == TerritoryInitializationMode.FutureIntentionallyEmpty)
            {
                diagnostics.Add(Error("FutureSchemaUnsupported", string.Empty, "Future intentionally-empty territory state cannot be migrated by this planner."));
            }

            if (request.Mode == TerritoryInitializationMode.NewProfile &&
                !request.AuthorizeBaselineInitialization)
            {
                diagnostics.Add(Error("InitializationNotAuthorized", string.Empty, "New profile baseline initialization is not authorized."));
            }

            if (!CatalogIdentityMatches(request.ExpectedCatalogIdentity, Catalog.Identity))
            {
                diagnostics.Add(Error("CatalogMismatch", string.Empty, "Expected territory catalog identity is stale."));
            }

            if (!TerritorySemanticHasher.IsLowerSha256(request.ExpectedStateRevisionHash))
            {
                diagnostics.Add(Error("StaleStateRevision", string.Empty, "Expected territory state revision hash is malformed."));
            }

            List<TerritoryOperationReceipt> receiptRows = ValidateOperationReceipts(receipts, diagnostics);
            if (diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                TerritoryMigrationStatus invalidStatus = diagnostics.Any(item => item.Code == "StaleStateRevision")
                    ? TerritoryMigrationStatus.RejectedStaleRevision
                    : TerritoryMigrationStatus.Rejected;
                return MigrationReject(
                    invalidStatus,
                    request.OperationId,
                    string.Empty,
                    string.Empty,
                    diagnostics,
                    null);
            }

            string semanticHash = BuildMigrationSemanticHash(request);
            string resultId = BuildIdentity("territory-migration-result-", semanticHash);
            TerritoryOperationReceipt matching = receiptRows.SingleOrDefault(item =>
                string.Equals(item.OperationId, request.OperationId, StringComparison.Ordinal));
            if (matching != null)
            {
                if (!string.Equals(matching.SemanticHash, semanticHash, StringComparison.Ordinal) ||
                    !string.Equals(matching.ResultId, resultId, StringComparison.Ordinal))
                {
                    diagnostics.Add(Error("CorrelationConflict", string.Empty, "Initialization operation ID is already bound to different semantics or result identity."));
                    return new TerritoryMigrationPlan(
                        TerritoryMigrationStatus.CorrelationConflict,
                        request.OperationId,
                        semanticHash,
                        resultId,
                        request.ExpectedStateRevisionHash,
                        Array.Empty<TerritoryMigrationAction>(),
                        Array.Empty<TerritoryStateRecord>(),
                        Array.Empty<TerritoryStateRecord>(),
                        matching,
                        diagnostics);
                }

                if (matching.Durability == TerritoryOperationDurability.Committed)
                {
                    return new TerritoryMigrationPlan(
                        TerritoryMigrationStatus.AlreadyCommittedReplay,
                        request.OperationId,
                        semanticHash,
                        resultId,
                        request.ExpectedStateRevisionHash,
                        Array.Empty<TerritoryMigrationAction>(),
                        Array.Empty<TerritoryStateRecord>(),
                        Array.Empty<TerritoryStateRecord>(),
                        matching,
                        diagnostics);
                }

                if (matching.Durability == TerritoryOperationDurability.CommitUncertain)
                {
                    diagnostics.Add(Error("CommitUncertain", string.Empty, "Initialization operation requires reconciliation before retry."));
                    return new TerritoryMigrationPlan(
                        TerritoryMigrationStatus.CommitUncertain,
                        request.OperationId,
                        semanticHash,
                        resultId,
                        request.ExpectedStateRevisionHash,
                        Array.Empty<TerritoryMigrationAction>(),
                        Array.Empty<TerritoryStateRecord>(),
                        Array.Empty<TerritoryStateRecord>(),
                        matching,
                        diagnostics);
                }
            }

            if (request.Mode == TerritoryInitializationMode.Legacy && request.HasRicherBackup)
            {
                diagnostics.Add(new TerritoryDiagnostic(
                    TerritoryDiagnosticSeverity.Warning,
                    "RicherCandidateRequired",
                    string.Empty,
                    "A richer legacy candidate must be preferred before initialization."));
                return new TerritoryMigrationPlan(
                    TerritoryMigrationStatus.RequiresRicherCandidate,
                    request.OperationId,
                    semanticHash,
                    resultId,
                    request.ExpectedStateRevisionHash,
                    Array.Empty<TerritoryMigrationAction>(),
                    Array.Empty<TerritoryStateRecord>(),
                    Array.Empty<TerritoryStateRecord>(),
                    matching,
                    diagnostics);
            }

            if (rawStates == null)
            {
                diagnostics.Add(Error("NullStateCollection", string.Empty, "Territory state collection is null and cannot be treated as an empty candidate."));
                return MigrationReject(
                    TerritoryMigrationStatus.Rejected,
                    request.OperationId,
                    semanticHash,
                    string.Empty,
                    diagnostics,
                    matching);
            }

            bool stateLimitExceeded;
            List<TerritoryStateRecord> states = TerritoryPhaseBCollections.TakeBounded(
                rawStates,
                TerritoryTechnicalLimits.MaximumStateRows + 1,
                out stateLimitExceeded);
            if (stateLimitExceeded || states.Count > TerritoryTechnicalLimits.MaximumStateRows)
            {
                diagnostics.Add(Error("StateLimitExceeded", string.Empty, "Territory state rows exceed the bounded planner limit."));
            }

            string stateHash = TerritorySemanticHasher.HashStates(states);
            if (!string.Equals(request.ExpectedStateRevisionHash, stateHash, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("StaleStateRevision", string.Empty, "Expected territory state revision hash is stale."));
            }

            ValidateRawStateShape(states, diagnostics);
            if (diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                TerritoryMigrationStatus status = diagnostics.Any(item => item.Code == "StaleStateRevision")
                    ? TerritoryMigrationStatus.RejectedStaleRevision
                    : TerritoryMigrationStatus.Rejected;
                return MigrationReject(status, request.OperationId, semanticHash, stateHash, diagnostics, matching);
            }

            var actions = new List<TerritoryMigrationAction>();
            var outputs = new List<TerritoryStateRecord>();
            var unknown = new List<TerritoryStateRecord>();
            TerritoryMigrationStatus proposedStatus = BuildMigrationOutputs(
                request,
                states,
                actions,
                outputs,
                unknown,
                diagnostics);
            if (proposedStatus == TerritoryMigrationStatus.RequiresRicherCandidate ||
                proposedStatus == TerritoryMigrationStatus.Rejected)
            {
                return new TerritoryMigrationPlan(
                    proposedStatus,
                    request.OperationId,
                    semanticHash,
                    resultId,
                    stateHash,
                    Array.Empty<TerritoryMigrationAction>(),
                    Array.Empty<TerritoryStateRecord>(),
                    Array.Empty<TerritoryStateRecord>(),
                    null,
                    diagnostics);
            }

            SortMigrationCollections(actions, outputs, unknown);
            if (actions.Count > TerritoryTechnicalLimits.MaximumMigrationRows ||
                outputs.Count > TerritoryTechnicalLimits.MaximumMigrationRows)
            {
                diagnostics.Add(Error("MigrationOutputLimitExceeded", string.Empty, "Territory migration output exceeds the bounded result limit."));
                return MigrationReject(
                    TerritoryMigrationStatus.Rejected,
                    request.OperationId,
                    semanticHash,
                    stateHash,
                    diagnostics,
                    matching);
            }

            return new TerritoryMigrationPlan(
                proposedStatus,
                request.OperationId,
                semanticHash,
                resultId,
                stateHash,
                actions,
                outputs,
                unknown,
                matching,
                diagnostics);
        }

        public TerritoryCaptureTransactionPlan PlanCaptureTransaction(
            TerritoryQueryResult query,
            TerritoryCaptureTransactionRequest request,
            IEnumerable<TerritoryCaptureReceipt> receipts)
        {
            var diagnostics = new List<TerritoryDiagnostic>();
            TerritoryCatalogValidationResult catalogValidation = ValidateCatalog();
            diagnostics.AddRange(catalogValidation.Diagnostics);
            if (catalogValidation.Status != TerritoryCatalogValidationStatus.Valid)
            {
                return CaptureReject(
                    TerritoryCaptureStatus.RejectedDomainMalformed,
                    request?.CaptureRequest,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    null,
                    diagnostics);
            }

            if (request == null || request.CaptureRequest == null)
            {
                diagnostics.Add(Error("NullCaptureRequest", string.Empty, "Territory capture transaction request is null."));
                return CaptureReject(TerritoryCaptureStatus.RejectedDomainMalformed, null, string.Empty, string.Empty, string.Empty, null, diagnostics);
            }

            TerritoryCaptureRequest capture = request.CaptureRequest;
            if (!IsTechnicalId(capture.OperationId))
            {
                diagnostics.Add(Error("InvalidOperationId", capture.TerritoryId, "Capture operation ID is invalid."));
            }

            if (!IsTechnicalId(capture.TerritoryId))
            {
                diagnostics.Add(Error("InvalidTerritoryId", capture.TerritoryId, "Capture territory ID is invalid."));
            }

            if (!IsTechnicalId(request.ProfileSessionId))
            {
                diagnostics.Add(Error("InvalidProfileSession", capture.TerritoryId, "Profile/session identity is invalid."));
            }

            if (!CatalogIdentityMatches(request.ExpectedCatalogIdentity, Catalog.Identity))
            {
                diagnostics.Add(Error("CatalogMismatch", capture.TerritoryId, "Expected territory catalog identity is stale."));
            }

            if (!TerritorySemanticHasher.IsLowerSha256(request.ExpectedStateRevisionHash))
            {
                diagnostics.Add(Error("StaleStateRevision", capture.TerritoryId, "Expected territory state revision hash is malformed."));
            }

            if (!IsDefinedRealm(capture.CommittedProfileRealm) ||
                !IsDefinedRealm(capture.ExpectedCapturerRealm) ||
                !IsDefinedRealm(capture.ExpectedPreviousOwner) ||
                capture.CommittedProfileRealm == RealmId.None ||
                capture.ExpectedCapturerRealm == RealmId.None ||
                capture.ExpectedCapturerRealm != capture.CommittedProfileRealm ||
                capture.ExpectedRevision < 0)
            {
                diagnostics.Add(Error("MalformedCaptureRequest", capture.TerritoryId, "Capture realm or revision fields are malformed."));
            }

            TerritoryCaptureAuthorization authorization = capture.Authorization;
            if (authorization == null)
            {
                diagnostics.Add(Error("MissingAuthorization", capture.TerritoryId, "Capture authorization is missing."));
            }
            else if (!IsTechnicalId(authorization.AuthorizationId) ||
                !Enum.IsDefined(typeof(TerritoryCaptureAuthorizationSource), authorization.Source) ||
                authorization.Source == TerritoryCaptureAuthorizationSource.Undefined ||
                !IsTechnicalId(authorization.ProfileSessionId) ||
                !IsTechnicalId(authorization.TerritoryId) ||
                !IsDefinedRealm(authorization.CapturerRealm) ||
                !IsDefinedRealm(authorization.ExpectedPreviousOwner) ||
                authorization.ExpectedRevision < 0 ||
                !IsTechnicalId(authorization.SourceResultId) ||
                !TerritorySemanticHasher.IsLowerSha256(authorization.SourceResultHash) ||
                authorization.ExpiresAtUtcTicks <= 0 ||
                !Enum.IsDefined(typeof(TerritoryAuthorizationUsePolicy), authorization.UsePolicy) ||
                authorization.UsePolicy != TerritoryAuthorizationUsePolicy.SingleUse ||
                !string.Equals(authorization.ProfileSessionId, request.ProfileSessionId, StringComparison.Ordinal) ||
                !string.Equals(authorization.TerritoryId, capture.TerritoryId, StringComparison.Ordinal) ||
                authorization.CapturerRealm != capture.CommittedProfileRealm ||
                authorization.ExpectedPreviousOwner != capture.ExpectedPreviousOwner ||
                authorization.ExpectedRevision != capture.ExpectedRevision)
            {
                diagnostics.Add(Error("MalformedAuthorization", capture.TerritoryId, "Capture authorization is missing or malformed."));
            }
            else if (authorization.Source != TerritoryCaptureAuthorizationSource.FakeTestOutcome)
            {
                diagnostics.Add(Error("AuthorizationSourceUnavailable", capture.TerritoryId, "Production capture authorization sources remain unavailable in the pure unregistered planner."));
            }

            List<TerritoryCaptureReceipt> receiptRows = ValidateCaptureReceipts(receipts, diagnostics);
            TerritoryDefinition definition = Catalog.Definitions.SingleOrDefault(item =>
                item != null && string.Equals(item.Id, capture.TerritoryId, StringComparison.Ordinal));
            TerritoryCaptureRewardProfile reward = definition == null
                ? null
                : Catalog.RewardProfiles.SingleOrDefault(item =>
                    item != null && string.Equals(
                        item.RewardProfileId,
                        definition.CaptureRewardProfileId,
                        StringComparison.Ordinal));
            if (definition == null)
            {
                diagnostics.Add(Error("UnknownTerritory", capture.TerritoryId, "Capture target is not a supported catalog definition."));
            }

            if (definition != null && reward == null)
            {
                diagnostics.Add(Error("MissingRewardProfile", capture.TerritoryId, "Capture reward profile is unavailable."));
            }

            if (diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                TerritoryCaptureStatus status = diagnostics.Any(item => item.Code == "StaleStateRevision")
                    ? TerritoryCaptureStatus.RejectedStaleRevision
                    : diagnostics.Any(item => item.Code == "MissingRewardProfile")
                        ? TerritoryCaptureStatus.RejectedRewardPlan
                        : diagnostics.Any(item => item.Code == "MissingAuthorization" ||
                                                  item.Code == "MalformedAuthorization" ||
                                                  item.Code == "AuthorizationSourceUnavailable" ||
                                                  item.Code == "ExpiredAuthorization" ||
                                                  item.Code == "InvalidAuthorizationEvaluationTime")
                            ? TerritoryCaptureStatus.RejectedUnauthorized
                            : TerritoryCaptureStatus.RejectedDomainMalformed;
                return CaptureReject(status, capture, string.Empty, string.Empty, string.Empty, null, diagnostics);
            }

            string semanticHash = BuildCaptureSemanticHash(
                request,
                definition,
                reward);
            string resultId = BuildIdentity("territory-result-", semanticHash);
            string receiptId = BuildIdentity("territory-receipt-", semanticHash);
            string eventId = BuildIdentity("territory-event-", semanticHash);
            TerritoryCaptureReceipt matching = receiptRows.SingleOrDefault(item =>
                string.Equals(item.OperationId, capture.OperationId, StringComparison.Ordinal));
            if (matching != null)
            {
                if (!string.Equals(matching.SemanticHash, semanticHash, StringComparison.Ordinal))
                {
                    diagnostics.Add(Error("CorrelationConflict", capture.TerritoryId, "Capture operation ID is already bound to different semantics."));
                    return CaptureReject(
                        TerritoryCaptureStatus.CorrelationConflict,
                        capture,
                        semanticHash,
                        resultId,
                        receiptId,
                        matching,
                        diagnostics);
                }

                ValidateMatchingCaptureReceipt(
                    matching,
                    request,
                    reward,
                    resultId,
                    receiptId,
                    eventId,
                    diagnostics);
                if (diagnostics.Any(item => item.Code == "ReceiptIdentityMismatch"))
                {
                    return CaptureReject(
                        TerritoryCaptureStatus.CorrelationConflict,
                        capture,
                        semanticHash,
                        resultId,
                        receiptId,
                        matching,
                        diagnostics);
                }

                if (matching.Durability == TerritoryOperationDurability.Committed)
                {
                    TerritoryCapturePlan replayPlan = new TerritoryCapturePlan(
                        TerritoryCaptureStatus.AlreadyCommittedReplay,
                        capture.OperationId,
                        matching.TerritoryId,
                        matching.PreviousOwner,
                        matching.NewOwner,
                        matching.PreviousRevision,
                        matching.NewRevision,
                        0,
                        0,
                        Array.Empty<TerritoryDiagnostic>());
                    return new TerritoryCaptureTransactionPlan(
                        TerritoryCaptureStatus.AlreadyCommittedReplay,
                        semanticHash,
                        matching.ResultId,
                        matching.ReceiptId,
                        definition.CaptureRewardProfileId,
                        replayPlan,
                        null,
                        null,
                        null,
                        matching,
                        diagnostics,
                        _planProvenance);
                }

                if (matching.Durability == TerritoryOperationDurability.CommitUncertain)
                {
                    diagnostics.Add(Error("CommitUncertain", capture.TerritoryId, "Capture operation requires reconciliation before retry."));
                    return CaptureReject(
                        TerritoryCaptureStatus.CommitUncertain,
                        capture,
                        semanticHash,
                        resultId,
                        receiptId,
                        matching,
                        diagnostics);
                }
            }

            if (request.AuthorizationEvaluationUtcTicks <= 0)
            {
                diagnostics.Add(Error("InvalidAuthorizationEvaluationTime", capture.TerritoryId, "Capture authorization evaluation time is invalid."));
                return CaptureReject(
                    TerritoryCaptureStatus.RejectedUnauthorized,
                    capture,
                    semanticHash,
                    resultId,
                    receiptId,
                    matching,
                    diagnostics);
            }

            if (authorization.ExpiresAtUtcTicks <= request.AuthorizationEvaluationUtcTicks)
            {
                diagnostics.Add(Error("ExpiredAuthorization", capture.TerritoryId, "Capture authorization has expired."));
                return CaptureReject(
                    TerritoryCaptureStatus.RejectedUnauthorized,
                    capture,
                    semanticHash,
                    resultId,
                    receiptId,
                    matching,
                    diagnostics);
            }

            TerritoryCaptureReceipt sourceBinding = receiptRows.FirstOrDefault(item =>
                item != null &&
                !string.Equals(item.OperationId, capture.OperationId, StringComparison.Ordinal) &&
                string.Equals(item.AuthorizationSourceResultId, authorization.SourceResultId, StringComparison.Ordinal));
            TerritoryCaptureReceipt authorizationBinding = receiptRows.FirstOrDefault(item =>
                item != null &&
                !string.Equals(item.OperationId, capture.OperationId, StringComparison.Ordinal) &&
                string.Equals(item.AuthorizationId, authorization.AuthorizationId, StringComparison.Ordinal));
            if (sourceBinding != null &&
                !string.Equals(sourceBinding.AuthorizationSourceResultHash, authorization.SourceResultHash, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("AuthorizationSourceResultConflict", capture.TerritoryId, "Capture authorization source-result identity is bound to a different result hash."));
                return CaptureReject(
                    TerritoryCaptureStatus.CorrelationConflict,
                    capture,
                    semanticHash,
                    resultId,
                    receiptId,
                    sourceBinding,
                    diagnostics);
            }

            if (authorizationBinding != null &&
                (!string.Equals(authorizationBinding.AuthorizationSourceResultId, authorization.SourceResultId, StringComparison.Ordinal) ||
                 !string.Equals(authorizationBinding.AuthorizationSourceResultHash, authorization.SourceResultHash, StringComparison.Ordinal)))
            {
                diagnostics.Add(Error("AuthorizationIdentityConflict", capture.TerritoryId, "Capture authorization identity is bound to a different source result."));
                return CaptureReject(
                    TerritoryCaptureStatus.CorrelationConflict,
                    capture,
                    semanticHash,
                    resultId,
                    receiptId,
                    authorizationBinding,
                    diagnostics);
            }

            if (authorization.UsePolicy == TerritoryAuthorizationUsePolicy.SingleUse &&
                (sourceBinding != null || authorizationBinding != null))
            {
                diagnostics.Add(Error("AuthorizationAlreadyUsed", capture.TerritoryId, "Single-use capture authorization is already bound to another operation."));
                return CaptureReject(
                    TerritoryCaptureStatus.RejectedUnauthorized,
                    capture,
                    semanticHash,
                    resultId,
                    receiptId,
                    null,
                    diagnostics);
            }

            ValidateQueryAuthority(query, request, diagnostics);
            if (diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                TerritoryCaptureStatus invalidStatus = diagnostics.Any(item => item.Code == "StaleStateRevision")
                    ? TerritoryCaptureStatus.RejectedStaleRevision
                    : TerritoryCaptureStatus.RejectedDomainMalformed;
                return CaptureReject(
                    invalidStatus,
                    capture,
                    semanticHash,
                    resultId,
                    receiptId,
                    matching,
                    diagnostics);
            }

            TerritoryCapturePlan basePlan = _queryPlanner.PlanCapture(query, capture);
            diagnostics.AddRange(basePlan.Diagnostics);
            if (basePlan.Status != TerritoryCaptureStatus.Planned)
            {
                return new TerritoryCaptureTransactionPlan(
                    basePlan.Status,
                    semanticHash,
                    resultId,
                    receiptId,
                    definition.CaptureRewardProfileId,
                    basePlan,
                    null,
                    null,
                    null,
                    null,
                    diagnostics,
                    _planProvenance);
            }

            var capturePlan = new TerritoryCapturePlan(
                basePlan.Status,
                basePlan.OperationId,
                basePlan.TerritoryId,
                basePlan.PreviousOwner,
                basePlan.NewOwner,
                basePlan.PreviousRevision,
                basePlan.NewRevision,
                reward.WarzoneCredits,
                reward.QuestProgressDelta,
                basePlan.Diagnostics);

            var economyCommand = new TerritoryEconomyCommand(
                capture.OperationId,
                reward.RewardProfileId,
                reward.WarzoneCredits);
            var questCommand = new TerritoryQuestCommand(
                capture.OperationId,
                reward.QuestProgressType,
                reward.QuestProgressDelta);
            var committedEvent = new TerritoryCaptureCommittedEvent(
                eventId,
                capture.OperationId,
                capture.TerritoryId,
                capturePlan.PreviousOwner,
                capturePlan.NewOwner,
                capturePlan.PreviousRevision,
                capturePlan.NewRevision,
                Catalog.Identity,
                query.StateRevisionHash,
                request.ProfileSessionId,
                authorization.AuthorizationId,
                authorization.SourceResultId,
                authorization.SourceResultHash,
                receiptId);
            return new TerritoryCaptureTransactionPlan(
                TerritoryCaptureStatus.Planned,
                semanticHash,
                resultId,
                receiptId,
                reward.RewardProfileId,
                capturePlan,
                economyCommand,
                questCommand,
                committedEvent,
                null,
                diagnostics,
                _planProvenance);
        }

        private TerritoryCaptureReceipt CreateReceipt(
            TerritoryCaptureTransactionPlan plan,
            TerritoryOperationDurability durability)
        {
            if (plan == null ||
                !plan.HasPlannerProvenance(_planProvenance) ||
                !Enum.IsDefined(typeof(TerritoryOperationDurability), durability))
            {
                return null;
            }

            if (plan.ExistingReceipt != null &&
                plan.Status == TerritoryCaptureStatus.AlreadyCommittedReplay)
            {
                return plan.ExistingReceipt;
            }

            if (plan.Status != TerritoryCaptureStatus.Planned)
            {
                return null;
            }

            TerritoryCapturePlan capture = plan.CapturePlan;
            return new TerritoryCaptureReceipt(
                plan.ReceiptId,
                capture?.OperationId,
                plan.SemanticHash,
                durability,
                plan.ResultId,
                plan.Event?.EventId,
                capture?.TerritoryId,
                capture?.PreviousOwner ?? RealmId.None,
                capture?.NewOwner ?? RealmId.None,
                capture?.PreviousRevision ?? 0,
                capture?.NewRevision ?? 0,
                plan.EconomyCommand?.WarzoneCreditsDelta ?? 0,
                plan.QuestCommand?.ProgressDelta ?? 0,
                new TerritoryCatalogIdentity(
                    plan.Event?.CatalogId,
                    plan.Event?.CatalogSchemaVersion ?? 0,
                    plan.Event?.CatalogContentVersion ?? 0,
                    plan.Event?.CatalogSourceRevision,
                    plan.Event?.CatalogRawSha256),
                plan.Event?.StateRevisionHash,
                plan.Event?.ProfileSessionId,
                plan.Event?.AuthorizationId,
                plan.Event?.AuthorizationSourceResultId,
                plan.Event?.AuthorizationSourceResultHash);
        }

        public TerritoryCaptureApplicationResult ApplyCapture(
            TerritoryCaptureTransactionPlan plan,
            ITerritoryCandidateApplyTarget candidate,
            ITerritoryEconomyApplyTarget economy,
            ITerritoryQuestApplyTarget quest)
        {
            var diagnostics = new List<TerritoryDiagnostic>();
            if (plan == null)
            {
                diagnostics.Add(Error("NullPlan", string.Empty, "Territory capture plan is null."));
                return new TerritoryCaptureApplicationResult(TerritoryApplyDisposition.Rejected, null, null, null, diagnostics);
            }

            if (!plan.HasPlannerProvenance(_planProvenance))
            {
                diagnostics.Add(Error("PlannerProvenanceMissing", plan.CapturePlan?.TerritoryId, "Territory capture plan was not produced by this planner instance."));
                return new TerritoryCaptureApplicationResult(TerritoryApplyDisposition.Rejected, plan, null, null, diagnostics);
            }

            if (plan.Status == TerritoryCaptureStatus.AlreadyCommittedReplay &&
                plan.ExistingReceipt != null)
            {
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.Replayed,
                    plan,
                    plan.ExistingReceipt,
                    null,
                    diagnostics);
            }

            if (plan.Status == TerritoryCaptureStatus.NoChangeSameOwner)
            {
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.NoChange,
                    plan,
                    null,
                    null,
                    diagnostics);
            }

            if (plan.Status != TerritoryCaptureStatus.Planned ||
                plan.CapturePlan == null ||
                plan.EconomyCommand == null ||
                plan.QuestCommand == null ||
                plan.Event == null)
            {
                diagnostics.Add(Error("PlanUnavailable", plan.CapturePlan?.TerritoryId, "Territory capture plan is not applicable."));
                return new TerritoryCaptureApplicationResult(TerritoryApplyDisposition.Rejected, plan, null, null, diagnostics);
            }

            if (candidate == null || economy == null || quest == null)
            {
                diagnostics.Add(Error("DependencyUnavailable", plan.CapturePlan.TerritoryId, "A fake no-save application target is unavailable."));
                return new TerritoryCaptureApplicationResult(TerritoryApplyDisposition.Rejected, plan, null, null, diagnostics);
            }

            TerritoryCaptureReceipt provisional = CreateReceipt(
                plan,
                TerritoryOperationDurability.Committed);
            bool candidateApplied = false;
            bool economyApplied = false;
            bool questApplied = false;

            TerritoryApplyStepStatus ownership = InvokeStep(
                () => candidate.ApplyOwnership(plan),
                "CandidateOwnership",
                "CandidateOwnershipException",
                plan.CapturePlan.TerritoryId,
                diagnostics,
                out bool ownershipException);
            candidateApplied = ownership == TerritoryApplyStepStatus.Applied || ownershipException;
            if (ownership != TerritoryApplyStepStatus.Applied)
            {
                return RollbackApplication(plan, provisional, candidate, economy, quest, candidateApplied, false, false, diagnostics);
            }

            TerritoryApplyStepStatus economyStatus = InvokeStep(
                () => economy.Apply(plan.EconomyCommand),
                "EconomyApply",
                "EconomyApplyException",
                plan.CapturePlan.TerritoryId,
                diagnostics,
                out bool economyException);
            economyApplied = economyStatus == TerritoryApplyStepStatus.Applied || economyException;
            if (economyStatus != TerritoryApplyStepStatus.Applied)
            {
                return RollbackApplication(plan, provisional, candidate, economy, quest, true, economyApplied, false, diagnostics);
            }

            TerritoryApplyStepStatus questStatus = InvokeStep(
                () => quest.Apply(plan.QuestCommand),
                "QuestApply",
                "QuestApplyException",
                plan.CapturePlan.TerritoryId,
                diagnostics,
                out bool questException);
            questApplied = questStatus == TerritoryApplyStepStatus.Applied || questException;
            if (questStatus != TerritoryApplyStepStatus.Applied)
            {
                return RollbackApplication(plan, provisional, candidate, economy, quest, true, true, questApplied, diagnostics);
            }

            TerritoryApplyStepStatus receiptStatus = InvokeStep(
                () => candidate.ApplyReceipt(provisional),
                "CandidateReceipt",
                "CandidateReceiptException",
                plan.CapturePlan.TerritoryId,
                diagnostics,
                out bool ignoredReceiptException);
            if (receiptStatus != TerritoryApplyStepStatus.Applied)
            {
                return RollbackApplication(plan, provisional, candidate, economy, quest, true, true, true, diagnostics);
            }

            TerritoryApplyStepStatus outboxStatus = InvokeStep(
                () => candidate.ApplyOutbox(plan.Event),
                "CandidateOutbox",
                "CandidateOutboxException",
                plan.CapturePlan.TerritoryId,
                diagnostics,
                out bool ignoredOutboxException);
            if (outboxStatus != TerritoryApplyStepStatus.Applied)
            {
                return RollbackApplication(plan, provisional, candidate, economy, quest, true, true, true, diagnostics);
            }

            TerritoryCommitStatus commitStatus;
            try
            {
                commitStatus = candidate.Commit(plan);
            }
            catch (Exception exception)
            {
                diagnostics.Add(Error(
                    "CommitException",
                    plan.CapturePlan.TerritoryId,
                    exception.GetType().Name));
                commitStatus = TerritoryCommitStatus.Uncertain;
            }

            if (commitStatus == TerritoryCommitStatus.Uncertain)
            {
                diagnostics.Add(Error("CommitUncertain", plan.CapturePlan.TerritoryId, "Candidate commit outcome is uncertain and requires reconciliation."));
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.CommitUncertain,
                    plan,
                    CreateReceipt(plan, TerritoryOperationDurability.CommitUncertain),
                    null,
                    diagnostics);
            }

            if (!Enum.IsDefined(typeof(TerritoryCommitStatus), commitStatus))
            {
                diagnostics.Add(Error("InvalidCommitStatus", plan.CapturePlan.TerritoryId, "Candidate returned an undefined commit status; outcome requires reconciliation."));
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.CommitUncertain,
                    plan,
                    CreateReceipt(plan, TerritoryOperationDurability.CommitUncertain),
                    null,
                    diagnostics);
            }

            if (commitStatus == TerritoryCommitStatus.Rejected)
            {
                diagnostics.Add(Error("CommitRejected", plan.CapturePlan.TerritoryId, "Candidate commit was rejected before publication."));
                return RollbackApplication(plan, provisional, candidate, economy, quest, true, true, true, diagnostics);
            }

            return new TerritoryCaptureApplicationResult(
                TerritoryApplyDisposition.Committed,
                plan,
                provisional,
                plan.Event,
                diagnostics);
        }

        private TerritoryMigrationStatus BuildMigrationOutputs(
            TerritoryInitializationRequest request,
            List<TerritoryStateRecord> states,
            List<TerritoryMigrationAction> actions,
            List<TerritoryStateRecord> outputs,
            List<TerritoryStateRecord> unknown,
            List<TerritoryDiagnostic> diagnostics)
        {
            if (request.Mode == TerritoryInitializationMode.FutureIntentionallyEmpty)
            {
                diagnostics.Add(Error("FutureSchemaUnsupported", string.Empty, "Future intentionally-empty territory state is preserved but unsupported."));
                return TerritoryMigrationStatus.Rejected;
            }

            if (request.Mode == TerritoryInitializationMode.NewProfile)
            {
                if (states.Count != 0)
                {
                    diagnostics.Add(Error("NewProfileStateNotEmpty", string.Empty, "New profile initialization requires an empty candidate."));
                    return TerritoryMigrationStatus.Rejected;
                }

                if (!request.AuthorizeBaselineInitialization)
                {
                    diagnostics.Add(Error("InitializationNotAuthorized", string.Empty, "Baseline initialization is not authorized."));
                    return TerritoryMigrationStatus.Rejected;
                }

                AddMissingBaseline(actions, outputs, new HashSet<string>(StringComparer.Ordinal));
                return TerritoryMigrationStatus.Planned;
            }

            if (request.Mode == TerritoryInitializationMode.Legacy && states.Count == 0)
            {
                if (request.HasRicherBackup)
                {
                    diagnostics.Add(new TerritoryDiagnostic(
                        TerritoryDiagnosticSeverity.Warning,
                        "RicherCandidateRequired",
                        string.Empty,
                        "A richer legacy candidate must be preferred before initialization."));
                    return TerritoryMigrationStatus.RequiresRicherCandidate;
                }

                if (!request.AuthorizeBaselineInitialization)
                {
                    diagnostics.Add(Error("InitializationNotAuthorized", string.Empty, "Ambiguous legacy-empty state requires explicit initialization authorization."));
                    return TerritoryMigrationStatus.Rejected;
                }

                AddMissingBaseline(actions, outputs, new HashSet<string>(StringComparer.Ordinal));
                return TerritoryMigrationStatus.Planned;
            }

            var definitionMap = Catalog.Definitions
                .Where(item => item != null)
                .ToDictionary(item => item.Id, StringComparer.Ordinal);
            var aliasMap = Catalog.Aliases
                .Where(item => item != null)
                .ToDictionary(item => item.OldTerritoryId, item => item.NewTerritoryId, StringComparer.Ordinal);
            foreach (IGrouping<string, TerritoryStateRecord> collision in states
                         .Select(state => new
                         {
                             State = state,
                             Target = definitionMap.ContainsKey(state.Id)
                                 ? state.Id
                                 : ResolveAliasTarget(state.Id, aliasMap, definitionMap)
                         })
                         .Where(item => item.Target != null)
                         .GroupBy(item => item.Target, item => item.State, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error(
                    "AliasedStateCollision",
                    collision.Key,
                    "Multiple raw territory rows resolve to the same active territory through aliases."));
            }

            if (diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                return TerritoryMigrationStatus.Rejected;
            }

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (TerritoryStateRecord state in states)
            {
                if (definitionMap.TryGetValue(state.Id, out TerritoryDefinition definition))
                {
                    if (!IsDefinedRealm(state.Owner) ||
                        (state.Owner == RealmId.None && !definition.AllowsNeutralOwnership) ||
                        (state.Owner != RealmId.None && !definition.AllowedOwners.Contains(state.Owner)) ||
                        state.Revision < 0)
                    {
                        diagnostics.Add(Error("MalformedKnownState", state.Id, "Known territory state is invalid for the selected definition."));
                        continue;
                    }

                    present.Add(state.Id);
                    outputs.Add(state);
                    actions.Add(new TerritoryMigrationAction(TerritoryMigrationActionKind.PreserveKnown, state));
                }
                else if (ResolveAliasTarget(state.Id, aliasMap, definitionMap) is string aliasTarget)
                {
                    TerritoryDefinition targetDefinition = definitionMap[aliasTarget];
                    if (request.Mode == TerritoryInitializationMode.Initialized)
                    {
                        diagnostics.Add(Error("AliasInInitializedState", state.Id, "Initialized territory state still contains a legacy alias ID."));
                        continue;
                    }

                    if (!IsDefinedRealm(state.Owner) ||
                        (state.Owner == RealmId.None && !targetDefinition.AllowsNeutralOwnership) ||
                        (state.Owner != RealmId.None && !targetDefinition.AllowedOwners.Contains(state.Owner)) ||
                        state.Revision < 0)
                    {
                        diagnostics.Add(Error("MalformedAliasedState", state.Id, "Aliased territory state is invalid for the target definition."));
                        continue;
                    }

                    var normalized = new TerritoryStateRecord(aliasTarget, state.Owner, state.Revision);
                    present.Add(aliasTarget);
                    outputs.Add(normalized);
                    unknown.Add(state);
                    actions.Add(new TerritoryMigrationAction(TerritoryMigrationActionKind.MigrateAlias, normalized));
                }
                else
                {
                    outputs.Add(state);
                    unknown.Add(state);
                    actions.Add(new TerritoryMigrationAction(TerritoryMigrationActionKind.PreserveUnknown, state));
                }
            }

            if (diagnostics.Any(item => item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                return TerritoryMigrationStatus.Rejected;
            }

            string[] missing = Catalog.Definitions
                .Where(item => item != null && !present.Contains(item.Id))
                .Select(item => item.Id)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                if (request.Mode == TerritoryInitializationMode.Initialized ||
                    !request.AuthorizeBaselineInitialization)
                {
                    foreach (string id in missing)
                    {
                        diagnostics.Add(Error("MissingKnownTerritory", id, "Required known territory state is missing."));
                    }

                    return TerritoryMigrationStatus.Rejected;
                }

                AddMissingBaseline(actions, outputs, present);
            }

            return request.Mode == TerritoryInitializationMode.Initialized
                ? TerritoryMigrationStatus.AlreadyInitialized
                : TerritoryMigrationStatus.Planned;
        }

        private static string ResolveAliasTarget(
            string sourceId,
            IReadOnlyDictionary<string, string> aliases,
            IReadOnlyDictionary<string, TerritoryDefinition> definitions)
        {
            string current = sourceId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (current != null && aliases.TryGetValue(current, out string next))
            {
                if (!visited.Add(current))
                {
                    return null;
                }

                current = next;
            }

            return current != null && definitions.ContainsKey(current) &&
                   !string.Equals(current, sourceId, StringComparison.Ordinal)
                ? current
                : null;
        }

        private void AddMissingBaseline(
            List<TerritoryMigrationAction> actions,
            List<TerritoryStateRecord> outputs,
            HashSet<string> present)
        {
            foreach (TerritoryDefinition definition in Catalog.Definitions
                         .Where(item => item != null && !present.Contains(item.Id))
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var state = new TerritoryStateRecord(
                    definition.Id,
                    definition.InitialOwner,
                    0);
                outputs.Add(state);
                actions.Add(new TerritoryMigrationAction(
                    TerritoryMigrationActionKind.InitializeKnown,
                    state));
                present.Add(definition.Id);
            }
        }

        private static void ValidateRawStateShape(
            List<TerritoryStateRecord> states,
            List<TerritoryDiagnostic> diagnostics)
        {
            foreach (TerritoryStateRecord state in states)
            {
                if (state == null)
                {
                    diagnostics.Add(Error("NullState", string.Empty, "Territory state row is null."));
                }
                else if (!IsTechnicalId(state.Id))
                {
                    diagnostics.Add(Error("InvalidStateId", state.Id, "Territory state ID is invalid."));
                }
            }

            foreach (IGrouping<string, TerritoryStateRecord> duplicate in states
                         .Where(item => item != null && IsTechnicalId(item.Id))
                         .GroupBy(item => item.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateStateId", duplicate.Key, "Duplicate territory state IDs disable migration."));
            }
        }

        private static List<TerritoryOperationReceipt> ValidateOperationReceipts(
            IEnumerable<TerritoryOperationReceipt> receipts,
            List<TerritoryDiagnostic> diagnostics)
        {
            bool exceeded;
            List<TerritoryOperationReceipt> rows = TerritoryPhaseBCollections.TakeBounded(
                receipts,
                TerritoryTechnicalLimits.MaximumReceipts + 1,
                out exceeded);
            if (exceeded || rows.Count > TerritoryTechnicalLimits.MaximumReceipts)
            {
                diagnostics.Add(Error("ReceiptLimitExceeded", string.Empty, "Territory operation receipts exceed the bounded planner limit."));
            }

            foreach (TerritoryOperationReceipt receipt in rows)
            {
                if (receipt == null)
                {
                    diagnostics.Add(Error("NullReceipt", string.Empty, "Territory operation receipt is null."));
                    continue;
                }

                if (!IsTechnicalId(receipt.OperationId) ||
                    !TerritorySemanticHasher.IsLowerSha256(receipt.SemanticHash) ||
                    !IsTechnicalId(receipt.ResultId) ||
                    !Enum.IsDefined(typeof(TerritoryOperationDurability), receipt.Durability))
                {
                    diagnostics.Add(Error("MalformedReceipt", receipt.OperationId, "Territory operation receipt is malformed."));
                }
            }

            foreach (IGrouping<string, TerritoryOperationReceipt> duplicate in rows
                         .Where(item => item != null && IsTechnicalId(item.OperationId))
                         .GroupBy(item => item.OperationId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateReceiptOperationId", duplicate.Key, "Duplicate receipt operation IDs are ambiguous."));
            }

            foreach (IGrouping<string, TerritoryOperationReceipt> duplicate in rows
                         .Where(item => item != null && IsTechnicalId(item.ResultId))
                         .GroupBy(item => item.ResultId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateReceiptResultId", duplicate.Key, "Duplicate migration result IDs are ambiguous."));
            }

            return rows;
        }

        private static List<TerritoryCaptureReceipt> ValidateCaptureReceipts(
            IEnumerable<TerritoryCaptureReceipt> receipts,
            List<TerritoryDiagnostic> diagnostics)
        {
            bool exceeded;
            List<TerritoryCaptureReceipt> rows = TerritoryPhaseBCollections.TakeBounded(
                receipts,
                TerritoryTechnicalLimits.MaximumReceipts + 1,
                out exceeded);
            if (exceeded || rows.Count > TerritoryTechnicalLimits.MaximumReceipts)
            {
                diagnostics.Add(Error("ReceiptLimitExceeded", string.Empty, "Territory capture receipts exceed the bounded planner limit."));
            }

            foreach (TerritoryCaptureReceipt receipt in rows)
            {
                if (receipt == null)
                {
                    diagnostics.Add(Error("NullReceipt", string.Empty, "Territory capture receipt is null."));
                    continue;
                }

                if (!IsTechnicalId(receipt.OperationId) ||
                    !TerritorySemanticHasher.IsLowerSha256(receipt.SemanticHash) ||
                    !IsTechnicalId(receipt.ResultId) ||
                    !IsTechnicalId(receipt.ReceiptId) ||
                    !IsTechnicalId(receipt.EventId) ||
                    !IsTechnicalId(receipt.TerritoryId) ||
                    !IsDefinedRealm(receipt.PreviousOwner) ||
                    !IsDefinedRealm(receipt.NewOwner) ||
                    receipt.NewOwner == RealmId.None ||
                    receipt.NewOwner == receipt.PreviousOwner ||
                    receipt.PreviousRevision < 0 ||
                    receipt.PreviousRevision == long.MaxValue ||
                    receipt.NewRevision != receipt.PreviousRevision + 1 ||
                    receipt.WarzoneCreditsDelta < 0 ||
                    receipt.QuestProgressDelta < 0 ||
                    !IsTechnicalId(receipt.CatalogId) ||
                    receipt.CatalogSchemaVersion <= 0 ||
                    receipt.CatalogContentVersion <= 0 ||
                    !IsTechnicalId(receipt.CatalogSourceRevision) ||
                    !TerritorySemanticHasher.IsLowerSha256(receipt.CatalogRawSha256) ||
                    !TerritorySemanticHasher.IsLowerSha256(receipt.StateRevisionHash) ||
                    !IsTechnicalId(receipt.ProfileSessionId) ||
                    !IsTechnicalId(receipt.AuthorizationId) ||
                    !IsTechnicalId(receipt.AuthorizationSourceResultId) ||
                    !TerritorySemanticHasher.IsLowerSha256(receipt.AuthorizationSourceResultHash) ||
                    !Enum.IsDefined(typeof(TerritoryOperationDurability), receipt.Durability))
                {
                    diagnostics.Add(Error("MalformedReceipt", receipt.OperationId, "Territory capture receipt is malformed."));
                }
            }

            foreach (IGrouping<string, TerritoryCaptureReceipt> duplicate in rows
                         .Where(item => item != null && IsTechnicalId(item.OperationId))
                         .GroupBy(item => item.OperationId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateReceiptOperationId", duplicate.Key, "Duplicate receipt operation IDs are ambiguous."));
            }


            AddDuplicateCaptureReceiptIdentityDiagnostics(
                rows,
                item => item.ResultId,
                "DuplicateReceiptResultId",
                diagnostics);
            AddDuplicateCaptureReceiptIdentityDiagnostics(
                rows,
                item => item.ReceiptId,
                "DuplicateReceiptId",
                diagnostics);
            AddDuplicateCaptureReceiptIdentityDiagnostics(
                rows,
                item => item.EventId,
                "DuplicateReceiptEventId",
                diagnostics);
            AddDuplicateCaptureReceiptIdentityDiagnostics(
                rows,
                item => item.AuthorizationId,
                "DuplicateReceiptAuthorizationId",
                diagnostics);
            AddDuplicateCaptureReceiptIdentityDiagnostics(
                rows,
                item => item.AuthorizationSourceResultId,
                "DuplicateReceiptAuthorizationSourceResultId",
                diagnostics);

            return rows;
        }

        private static void AddDuplicateCaptureReceiptIdentityDiagnostics(
            IEnumerable<TerritoryCaptureReceipt> rows,
            Func<TerritoryCaptureReceipt, string> selector,
            string code,
            List<TerritoryDiagnostic> diagnostics)
        {
            foreach (IGrouping<string, TerritoryCaptureReceipt> duplicate in rows
                         .Where(item => item != null && IsTechnicalId(selector(item)))
                         .GroupBy(selector, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error(code, duplicate.Key, "Duplicate capture receipt identity is ambiguous."));
            }
        }

        private void ValidateQueryAuthority(
            TerritoryQueryResult query,
            TerritoryCaptureTransactionRequest request,
            List<TerritoryDiagnostic> diagnostics)
        {
            TerritoryCaptureRequest capture = request.CaptureRequest;
            if (query == null || query.Status != TerritoryQueryStatus.Available)
            {
                diagnostics.Add(Error("DomainUnavailable", capture.TerritoryId, "Territory query is unavailable."));
                return;
            }

            if (!query.HasPlannerProvenance(_queryProvenance))
            {
                diagnostics.Add(Error("QueryProvenanceMissing", capture.TerritoryId, "Territory query was not produced by this planner instance."));
            }

            if (!string.Equals(query.CatalogId, Catalog.Identity.CatalogId, StringComparison.Ordinal) ||
                query.CatalogSchemaVersion != Catalog.Identity.SchemaVersion ||
                query.CatalogContentVersion != Catalog.Identity.ContentVersion ||
                !string.Equals(query.CatalogSourceRevision, Catalog.Identity.SourceRevision, StringComparison.Ordinal) ||
                !string.Equals(query.CatalogRawSha256, Catalog.Identity.RawSha256, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("QueryCatalogMismatch", capture.TerritoryId, "Territory query catalog identity is stale or forged."));
            }

            if (!string.Equals(query.ProfileSessionId, request.ProfileSessionId, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("QueryProfileSessionMismatch", capture.TerritoryId, "Territory query profile/session identity is stale or forged."));
            }

            if (query.CommittedProfileRealm != capture.CommittedProfileRealm)
            {
                diagnostics.Add(Error("QueryProfileRealmMismatch", capture.TerritoryId, "Territory query realm does not match the committed request realm."));
            }

            if (query.Territories.Count > TerritoryTechnicalLimits.MaximumStateRows)
            {
                diagnostics.Add(Error("QueryStateLimitExceeded", capture.TerritoryId, "Territory query snapshots exceed the bounded limit."));
            }

            string recomputedHash = TerritorySemanticHasher.HashQueryStates(query.Territories);
            if (!TerritorySemanticHasher.IsLowerSha256(query.StateRevisionHash) ||
                !string.Equals(query.StateRevisionHash, recomputedHash, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("QueryStateHashMismatch", capture.TerritoryId, "Territory query state hash does not match its snapshots."));
            }

            if (!string.Equals(request.ExpectedStateRevisionHash, query.StateRevisionHash, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("StaleStateRevision", capture.TerritoryId, "Expected territory state revision hash is stale."));
            }

            if (query.Diagnostics.Any(item => item != null && item.Severity == TerritoryDiagnosticSeverity.Error))
            {
                diagnostics.Add(Error("QueryCarriesErrors", capture.TerritoryId, "Available territory query carries error diagnostics."));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var supported = new HashSet<string>(StringComparer.Ordinal);
            foreach (TerritorySnapshot snapshot in query.Territories)
            {
                if (snapshot == null || snapshot.State == null)
                {
                    diagnostics.Add(Error("NullQuerySnapshot", capture.TerritoryId, "Territory query contains a null snapshot or state."));
                    continue;
                }

                TerritoryStateRecord state = snapshot.State;
                if (!IsTechnicalId(state.Id))
                {
                    diagnostics.Add(Error("InvalidQueryStateId", state.Id, "Territory query state ID is invalid."));
                    continue;
                }

                if (!seen.Add(state.Id))
                {
                    diagnostics.Add(Error("DuplicateQueryStateId", state.Id, "Territory query contains duplicate state IDs."));
                    continue;
                }

                TerritoryDefinition definition = Catalog.Definitions.SingleOrDefault(item =>
                    item != null && string.Equals(item.Id, state.Id, StringComparison.Ordinal));
                if (snapshot.IsSupported)
                {
                    if (definition == null || !ReferenceEquals(snapshot.Definition, definition))
                    {
                        diagnostics.Add(Error("QueryDefinitionMismatch", state.Id, "Supported query snapshot is not bound to this planner catalog."));
                        continue;
                    }

                    if (!IsDefinedRealm(state.Owner) || state.Revision < 0)
                    {
                        diagnostics.Add(Error("MalformedQueryState", state.Id, "Known territory query owner or revision is malformed."));
                    }

                    if ((state.Owner == RealmId.None && !definition.AllowsNeutralOwnership) ||
                        (state.Owner != RealmId.None && !definition.AllowedOwners.Contains(state.Owner)))
                    {
                        diagnostics.Add(Error("QueryOwnerForbidden", state.Id, "Territory query owner is forbidden by the definition."));
                    }

                    supported.Add(state.Id);
                }
                else if (snapshot.Definition != null || definition != null)
                {
                    diagnostics.Add(Error("QuerySupportMismatch", state.Id, "Known query state is incorrectly marked unsupported."));
                }
            }

            foreach (TerritoryDefinition definition in Catalog.Definitions.Where(item => item != null))
            {
                if (!supported.Contains(definition.Id))
                {
                    diagnostics.Add(Error("QueryMissingKnownTerritory", definition.Id, "Territory query is missing a required supported state."));
                }
            }
        }

        private static void ValidateMatchingCaptureReceipt(
            TerritoryCaptureReceipt receipt,
            TerritoryCaptureTransactionRequest request,
            TerritoryCaptureRewardProfile reward,
            string resultId,
            string receiptId,
            string eventId,
            List<TerritoryDiagnostic> diagnostics)
        {
            TerritoryCaptureRequest capture = request.CaptureRequest;
            TerritoryCaptureAuthorization authorization = capture.Authorization;
            TerritoryCatalogIdentity catalog = request.ExpectedCatalogIdentity;
            long expectedNewRevision;
            try
            {
                expectedNewRevision = checked(capture.ExpectedRevision + 1);
            }
            catch (OverflowException)
            {
                expectedNewRevision = long.MinValue;
            }

            bool mismatch = capture.ExpectedPreviousOwner == capture.ExpectedCapturerRealm ||
                            expectedNewRevision == long.MinValue ||
                            !string.Equals(receipt.ResultId, resultId, StringComparison.Ordinal) ||
                            !string.Equals(receipt.ReceiptId, receiptId, StringComparison.Ordinal) ||
                            !string.Equals(receipt.EventId, eventId, StringComparison.Ordinal) ||
                            !string.Equals(receipt.TerritoryId, capture.TerritoryId, StringComparison.Ordinal) ||
                            receipt.PreviousOwner != capture.ExpectedPreviousOwner ||
                            receipt.NewOwner != capture.ExpectedCapturerRealm ||
                            receipt.PreviousRevision != capture.ExpectedRevision ||
                            receipt.NewRevision != expectedNewRevision ||
                            receipt.WarzoneCreditsDelta != reward.WarzoneCredits ||
                            receipt.QuestProgressDelta != reward.QuestProgressDelta ||
                            !string.Equals(receipt.CatalogId, catalog?.CatalogId, StringComparison.Ordinal) ||
                            receipt.CatalogSchemaVersion != (catalog?.SchemaVersion ?? 0) ||
                            receipt.CatalogContentVersion != (catalog?.ContentVersion ?? 0) ||
                            !string.Equals(receipt.CatalogSourceRevision, catalog?.SourceRevision, StringComparison.Ordinal) ||
                            !string.Equals(receipt.CatalogRawSha256, catalog?.RawSha256, StringComparison.Ordinal) ||
                            !string.Equals(receipt.StateRevisionHash, request.ExpectedStateRevisionHash, StringComparison.Ordinal) ||
                            !string.Equals(receipt.ProfileSessionId, request.ProfileSessionId, StringComparison.Ordinal) ||
                            !string.Equals(receipt.AuthorizationId, authorization?.AuthorizationId, StringComparison.Ordinal) ||
                            !string.Equals(receipt.AuthorizationSourceResultId, authorization?.SourceResultId, StringComparison.Ordinal) ||
                            !string.Equals(receipt.AuthorizationSourceResultHash, authorization?.SourceResultHash, StringComparison.Ordinal);
            if (mismatch)
            {
                diagnostics.Add(Error("ReceiptIdentityMismatch", capture.TerritoryId, "Persisted capture receipt does not match its deterministic request/result identity."));
            }
        }

        private void ValidateDefinitions(List<TerritoryDiagnostic> diagnostics)
        {
            foreach (TerritoryDefinition definition in Catalog.Definitions)
            {
                if (definition == null)
                {
                    diagnostics.Add(Error("NullDefinition", string.Empty, "Territory definition is null."));
                    continue;
                }

                if (!IsTechnicalId(definition.Id))
                {
                    diagnostics.Add(Error("InvalidDefinitionId", definition.Id, "Territory definition ID is invalid."));
                }

                if (!IsContentKey(definition.ContentKey))
                {
                    diagnostics.Add(Error("InvalidContentKey", definition.Id, "Territory content key is invalid."));
                }

                if (!IsDefinedRealm(definition.InitialOwner))
                {
                    diagnostics.Add(Error("InvalidInitialOwner", definition.Id, "Territory initial owner is undefined."));
                }
                else if (definition.InitialOwner == RealmId.None && !definition.AllowsNeutralOwnership)
                {
                    diagnostics.Add(Error("NeutralOwnerForbidden", definition.Id, "Neutral initial ownership is not allowed."));
                }

                if (!Enum.IsDefined(typeof(ResourceType), definition.BonusType))
                {
                    diagnostics.Add(Error("InvalidResource", definition.Id, "Territory bonus resource is undefined."));
                }

                if (definition.BonusAmount < 0 ||
                    definition.BonusAmount > TerritoryTechnicalLimits.MaximumBonusAmountPerTerritory)
                {
                    diagnostics.Add(Error("BonusAmountOutOfRange", definition.Id, "Territory bonus cannot be safely accumulated at maximum catalog cardinality."));
                }

                if (definition.AllowedOwners.Count == 0)
                {
                    diagnostics.Add(Error("NoAllowedOwners", definition.Id, "Territory definition has no allowed capture owner."));
                }

                if (definition.AllowedOwners.Count > TerritoryTechnicalLimits.MaximumAllowedOwners)
                {
                    diagnostics.Add(Error("AllowedOwnerLimitExceeded", definition.Id, "Territory allowed owners exceed the bounded limit."));
                }

                if (definition.AllowedOwners.Any(item => item == RealmId.None || !IsDefinedRealm(item)))
                {
                    diagnostics.Add(Error("InvalidAllowedOwner", definition.Id, "Territory allowed owner is invalid."));
                }

                if (definition.InitialOwner != RealmId.None &&
                    IsDefinedRealm(definition.InitialOwner) &&
                    !definition.AllowedOwners.Contains(definition.InitialOwner))
                {
                    diagnostics.Add(Error("InitialOwnerNotAllowed", definition.Id, "Territory initial owner is not present in allowed owners."));
                }

                if (definition.AllowedOwners.Distinct().Count() != definition.AllowedOwners.Count)
                {
                    diagnostics.Add(Error("DuplicateAllowedOwner", definition.Id, "Territory allowed owner is duplicated."));
                }

                ValidateReferenceList(definition.Id, "Prerequisite", definition.PrerequisiteIds, diagnostics);
                ValidateReferenceList(definition.Id, "Capability", definition.RequiredCapabilityIds, diagnostics);
                if (definition.PrerequisiteIds.Count > 0)
                {
                    diagnostics.Add(Error(
                        "PrerequisiteAuthorityUnavailable",
                        definition.Id,
                        "Territory prerequisites fail closed until a trusted eligibility snapshot is integrated."));
                }

                if (definition.RequiredCapabilityIds.Count > 0)
                {
                    diagnostics.Add(Error(
                        "CapabilityAuthorityUnavailable",
                        definition.Id,
                        "Territory capabilities fail closed until a trusted capability snapshot is integrated."));
                }

                int rewardMatches = Catalog.RewardProfiles.Count(item =>
                    item != null && string.Equals(
                        item.RewardProfileId,
                        definition.CaptureRewardProfileId,
                        StringComparison.Ordinal));
                if (rewardMatches != 1)
                {
                    diagnostics.Add(Error("MissingRewardProfile", definition.Id, "Territory definition must resolve exactly one reward profile."));
                }
            }

            foreach (IGrouping<string, TerritoryDefinition> duplicate in Catalog.Definitions
                         .Where(item => item != null && IsTechnicalId(item.Id))
                         .GroupBy(item => item.Id, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateDefinitionId", duplicate.Key, "Duplicate territory definition ID."));
            }

            foreach (IGrouping<string, TerritoryDefinition> duplicate in Catalog.Definitions
                         .Where(item => item != null && IsContentKey(item.ContentKey))
                         .GroupBy(item => item.ContentKey, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateContentKey", duplicate.Key, "Duplicate territory content key."));
            }

            ValidatePrerequisiteGraph(diagnostics);
        }

        private void ValidateRewards(List<TerritoryDiagnostic> diagnostics)
        {
            foreach (TerritoryCaptureRewardProfile reward in Catalog.RewardProfiles)
            {
                if (reward == null)
                {
                    diagnostics.Add(Error("NullRewardProfile", string.Empty, "Territory reward profile is null."));
                    continue;
                }

                if (!IsTechnicalId(reward.RewardProfileId))
                {
                    diagnostics.Add(Error("InvalidRewardProfileId", reward.RewardProfileId, "Territory reward profile ID is invalid."));
                }

                if (reward.WarzoneCredits < 0)
                {
                    diagnostics.Add(Error("RewardCreditsOutOfRange", reward.RewardProfileId, "Territory reward credits are negative."));
                }

                if (!IsTechnicalId(reward.QuestProgressType))
                {
                    diagnostics.Add(Error("InvalidRewardQuestType", reward.RewardProfileId, "Territory reward quest progress type is invalid."));
                }

                if (reward.QuestProgressDelta < 0)
                {
                    diagnostics.Add(Error("RewardQuestProgressOutOfRange", reward.RewardProfileId, "Territory quest progress delta is negative."));
                }

                if (string.Equals(reward.RewardProfileId, TerritoryContractPlanner.CurrentRewardProfileId, StringComparison.Ordinal) &&
                    (reward.WarzoneCredits != TerritoryContractPlanner.CaptureWarzoneCreditReward ||
                     !string.Equals(reward.QuestProgressType, "CaptureTerritory", StringComparison.Ordinal) ||
                     reward.QuestProgressDelta != TerritoryContractPlanner.CaptureQuestProgressReward))
                {
                    diagnostics.Add(Error("CurrentRewardProfileDrift", reward.RewardProfileId, "Current migration reward profile differs from +100 credits and CaptureTerritory +1."));
                }
            }

            foreach (IGrouping<string, TerritoryCaptureRewardProfile> duplicate in Catalog.RewardProfiles
                         .Where(item => item != null && IsTechnicalId(item.RewardProfileId))
                         .GroupBy(item => item.RewardProfileId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateRewardProfileId", duplicate.Key, "Duplicate territory reward profile ID."));
            }
        }

        private void ValidateAliases(List<TerritoryDiagnostic> diagnostics)
        {
            var definitionIds = new HashSet<string>(
                Catalog.Definitions.Where(item => item != null).Select(item => item.Id),
                StringComparer.Ordinal);
            foreach (TerritoryAliasDefinition alias in Catalog.Aliases)
            {
                if (alias == null)
                {
                    diagnostics.Add(Error("NullAlias", string.Empty, "Territory alias is null."));
                    continue;
                }

                if (!IsTechnicalId(alias.OldTerritoryId) || !IsTechnicalId(alias.NewTerritoryId))
                {
                    diagnostics.Add(Error("InvalidAliasId", alias.OldTerritoryId, "Territory alias identity is invalid."));
                }

                if (alias.IntroducedInVersion <= 0)
                {
                    diagnostics.Add(Error("InvalidAliasVersion", alias.OldTerritoryId, "Territory alias version is invalid."));
                }
                else if (Catalog.Identity != null &&
                         alias.IntroducedInVersion > Catalog.Identity.ContentVersion)
                {
                    diagnostics.Add(Error("FutureAliasVersion", alias.OldTerritoryId, "Territory alias is newer than the selected catalog content version."));
                }

                if (definitionIds.Contains(alias.OldTerritoryId))
                {
                    diagnostics.Add(Error("AliasDefinitionCollision", alias.OldTerritoryId, "Territory alias source collides with an active definition."));
                }
            }

            foreach (IGrouping<string, TerritoryAliasDefinition> duplicate in Catalog.Aliases
                         .Where(item => item != null && IsTechnicalId(item.OldTerritoryId))
                         .GroupBy(item => item.OldTerritoryId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                diagnostics.Add(Error("DuplicateAliasId", duplicate.Key, "Duplicate territory alias source ID."));
            }

            var map = Catalog.Aliases
                .Where(item => item != null && IsTechnicalId(item.OldTerritoryId) && IsTechnicalId(item.NewTerritoryId))
                .GroupBy(item => item.OldTerritoryId, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single().NewTerritoryId, StringComparer.Ordinal);
            foreach (string start in map.Keys.OrderBy(item => item, StringComparer.Ordinal))
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                string current = start;
                while (map.TryGetValue(current, out string next))
                {
                    if (!visited.Add(current))
                    {
                        diagnostics.Add(Error("AliasCycle", start, "Territory alias graph contains a cycle."));
                        break;
                    }

                    current = next;
                }

                if (!definitionIds.Contains(current) && !map.ContainsKey(current))
                {
                    diagnostics.Add(Error("AliasTargetMissing", start, "Territory alias target does not resolve to an active definition."));
                }
            }
        }

        private static void ValidateIdentity(
            TerritoryCatalogIdentity identity,
            string semanticHash,
            List<TerritoryDiagnostic> diagnostics)
        {
            if (identity == null)
            {
                diagnostics.Add(Error("MissingCatalogIdentity", string.Empty, "Territory catalog identity is missing."));
                return;
            }

            if (!IsTechnicalId(identity.CatalogId))
            {
                diagnostics.Add(Error("InvalidCatalogId", string.Empty, "Territory catalog ID is invalid."));
            }

            if (identity.SchemaVersion != 1)
            {
                diagnostics.Add(Error("UnsupportedSchemaVersion", string.Empty, "Territory catalog schema version is unsupported."));
            }

            if (identity.ContentVersion <= 0)
            {
                diagnostics.Add(Error("InvalidContentVersion", string.Empty, "Territory catalog content version is invalid."));
            }

            if (!IsTechnicalId(identity.SourceRevision))
            {
                diagnostics.Add(Error("InvalidSourceRevision", string.Empty, "Territory catalog source revision is invalid."));
            }

            if (!TerritorySemanticHasher.IsLowerSha256(identity.RawSha256) ||
                !string.Equals(identity.RawSha256, semanticHash, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("CatalogHashMismatch", string.Empty, "Territory catalog semantic hash does not match its identity."));
            }
        }

        private static void ValidateReferenceList(
            string territoryId,
            string label,
            IReadOnlyList<string> values,
            List<TerritoryDiagnostic> diagnostics)
        {
            if (values.Count > TerritoryTechnicalLimits.MaximumReferenceIds)
            {
                diagnostics.Add(Error(label + "LimitExceeded", territoryId, label + " IDs exceed the bounded limit."));
            }

            if (values.Any(item => !IsTechnicalId(item)))
            {
                diagnostics.Add(Error("Invalid" + label + "Id", territoryId, label + " ID is invalid."));
            }

            if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
            {
                diagnostics.Add(Error("Duplicate" + label + "Id", territoryId, label + " ID is duplicated."));
            }
        }

        private void ValidatePrerequisiteGraph(List<TerritoryDiagnostic> diagnostics)
        {
            Dictionary<string, TerritoryDefinition> definitions = Catalog.Definitions
                .Where(item => item != null && IsTechnicalId(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            foreach (TerritoryDefinition definition in definitions.Values.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                foreach (string prerequisiteId in definition.PrerequisiteIds
                             .Where(IsTechnicalId)
                             .OrderBy(item => item, StringComparer.Ordinal))
                {
                    if (!definitions.ContainsKey(prerequisiteId))
                    {
                        diagnostics.Add(Error(
                            "MissingPrerequisite",
                            definition.Id,
                            "Territory prerequisite does not resolve: " + prerequisiteId + "."));
                    }
                }

                var pending = new Stack<string>(definition.PrerequisiteIds
                    .Where(definitions.ContainsKey)
                    .OrderByDescending(item => item, StringComparer.Ordinal));
                var visited = new HashSet<string>(StringComparer.Ordinal);
                bool cycle = false;
                while (pending.Count > 0 && !cycle)
                {
                    string current = pending.Pop();
                    if (string.Equals(current, definition.Id, StringComparison.Ordinal))
                    {
                        cycle = true;
                        break;
                    }

                    if (!visited.Add(current))
                    {
                        continue;
                    }

                    foreach (string next in definitions[current].PrerequisiteIds
                                 .Where(definitions.ContainsKey)
                                 .OrderByDescending(item => item, StringComparer.Ordinal))
                    {
                        pending.Push(next);
                    }
                }

                if (cycle)
                {
                    diagnostics.Add(Error("PrerequisiteCycle", definition.Id, "Territory prerequisite graph contains a cycle."));
                }
            }
        }

        private string BuildMigrationSemanticHash(TerritoryInitializationRequest request)
        {
            return TerritorySemanticHasher.HashFrames(
                "territory-migration-plan-v2",
                request.OperationId,
                ((int)request.Mode).ToString(CultureInfo.InvariantCulture),
                request.ExpectedCatalogId,
                (request.ExpectedCatalogIdentity?.SchemaVersion ?? 0).ToString(CultureInfo.InvariantCulture),
                (request.ExpectedCatalogIdentity?.ContentVersion ?? 0).ToString(CultureInfo.InvariantCulture),
                request.ExpectedCatalogIdentity?.SourceRevision,
                request.ExpectedCatalogIdentity?.RawSha256,
                request.ExpectedStateRevisionHash,
                request.HasRicherBackup ? "1" : "0",
                request.AuthorizeBaselineInitialization ? "1" : "0",
                Catalog.Identity.CatalogId,
                Catalog.Identity.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                Catalog.Identity.ContentVersion.ToString(CultureInfo.InvariantCulture),
                Catalog.Identity.SourceRevision,
                Catalog.Identity.RawSha256);
        }

        private string BuildCaptureSemanticHash(
            TerritoryCaptureTransactionRequest request,
            TerritoryDefinition definition,
            TerritoryCaptureRewardProfile reward)
        {
            TerritoryCaptureRequest capture = request.CaptureRequest;
            TerritoryCaptureAuthorization authorization = capture.Authorization;
            return TerritorySemanticHasher.HashFrames(
                "territory-capture-plan-v2",
                capture.OperationId,
                capture.TerritoryId,
                ((int)capture.CommittedProfileRealm).ToString(CultureInfo.InvariantCulture),
                ((int)capture.ExpectedCapturerRealm).ToString(CultureInfo.InvariantCulture),
                ((int)capture.ExpectedPreviousOwner).ToString(CultureInfo.InvariantCulture),
                capture.ExpectedRevision.ToString(CultureInfo.InvariantCulture),
                authorization?.AuthorizationId,
                ((int)(authorization?.Source ?? TerritoryCaptureAuthorizationSource.Undefined)).ToString(CultureInfo.InvariantCulture),
                authorization?.ProfileSessionId,
                authorization?.TerritoryId,
                ((int)(authorization?.CapturerRealm ?? RealmId.None)).ToString(CultureInfo.InvariantCulture),
                ((int)(authorization?.ExpectedPreviousOwner ?? RealmId.None)).ToString(CultureInfo.InvariantCulture),
                (authorization?.ExpectedRevision ?? -1).ToString(CultureInfo.InvariantCulture),
                authorization?.SourceResultId,
                authorization?.SourceResultHash,
                (authorization?.ExpiresAtUtcTicks ?? 0).ToString(CultureInfo.InvariantCulture),
                ((int)(authorization?.UsePolicy ?? TerritoryAuthorizationUsePolicy.Undefined)).ToString(CultureInfo.InvariantCulture),
                request.ExpectedCatalogId,
                (request.ExpectedCatalogIdentity?.SchemaVersion ?? 0).ToString(CultureInfo.InvariantCulture),
                (request.ExpectedCatalogIdentity?.ContentVersion ?? 0).ToString(CultureInfo.InvariantCulture),
                request.ExpectedCatalogIdentity?.SourceRevision,
                request.ExpectedCatalogIdentity?.RawSha256,
                request.ExpectedStateRevisionHash,
                request.ProfileSessionId,
                definition.CaptureRewardProfileId,
                reward.WarzoneCredits.ToString(CultureInfo.InvariantCulture),
                reward.QuestProgressType,
                reward.QuestProgressDelta.ToString(CultureInfo.InvariantCulture),
                Catalog.Identity.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                Catalog.Identity.ContentVersion.ToString(CultureInfo.InvariantCulture),
                Catalog.Identity.SourceRevision,
                Catalog.Identity.RawSha256);
        }

        private static TerritoryCaptureApplicationResult RollbackApplication(
            TerritoryCaptureTransactionPlan plan,
            TerritoryCaptureReceipt provisional,
            ITerritoryCandidateApplyTarget candidate,
            ITerritoryEconomyApplyTarget economy,
            ITerritoryQuestApplyTarget quest,
            bool candidateApplied,
            bool economyApplied,
            bool questApplied,
            List<TerritoryDiagnostic> diagnostics)
        {
            bool rollbackSucceeded = true;
            if (questApplied)
            {
                rollbackSucceeded &= InvokeRollback(
                    () => quest.Rollback(plan.QuestCommand),
                    "QuestRollback",
                    "QuestRollbackException",
                    plan.CapturePlan.TerritoryId,
                    diagnostics);
            }

            if (economyApplied)
            {
                rollbackSucceeded &= InvokeRollback(
                    () => economy.Rollback(plan.EconomyCommand),
                    "EconomyRollback",
                    "EconomyRollbackException",
                    plan.CapturePlan.TerritoryId,
                    diagnostics);
            }

            if (candidateApplied)
            {
                rollbackSucceeded &= InvokeRollback(
                    () => candidate.Rollback(plan),
                    "CandidateRollback",
                    "CandidateRollbackException",
                    plan.CapturePlan.TerritoryId,
                    diagnostics);
            }

            if (!rollbackSucceeded)
            {
                diagnostics.Add(Error("RollbackUncertain", plan.CapturePlan.TerritoryId, "At least one fake target rollback failed; outcome is uncertain."));
                return new TerritoryCaptureApplicationResult(
                    TerritoryApplyDisposition.CommitUncertain,
                    plan,
                    new TerritoryCaptureReceipt(
                        provisional.ReceiptId,
                        provisional.OperationId,
                        provisional.SemanticHash,
                        TerritoryOperationDurability.CommitUncertain,
                        provisional.ResultId,
                        provisional.EventId,
                        provisional.TerritoryId,
                        provisional.PreviousOwner,
                        provisional.NewOwner,
                        provisional.PreviousRevision,
                        provisional.NewRevision,
                        provisional.WarzoneCreditsDelta,
                        provisional.QuestProgressDelta,
                        new TerritoryCatalogIdentity(
                            provisional.CatalogId,
                            provisional.CatalogSchemaVersion,
                            provisional.CatalogContentVersion,
                            provisional.CatalogSourceRevision,
                            provisional.CatalogRawSha256),
                        provisional.StateRevisionHash,
                        provisional.ProfileSessionId,
                        provisional.AuthorizationId,
                        provisional.AuthorizationSourceResultId,
                        provisional.AuthorizationSourceResultHash),
                    null,
                    diagnostics);
            }

            return new TerritoryCaptureApplicationResult(
                candidateApplied || economyApplied || questApplied
                    ? TerritoryApplyDisposition.RolledBack
                    : TerritoryApplyDisposition.Rejected,
                plan,
                null,
                null,
                diagnostics);
        }

        private static TerritoryApplyStepStatus InvokeStep(
            Func<TerritoryApplyStepStatus> action,
            string phaseCode,
            string exceptionCode,
            string territoryId,
            List<TerritoryDiagnostic> diagnostics,
            out bool threw)
        {
            threw = false;
            try
            {
                TerritoryApplyStepStatus status = action();
                if (!Enum.IsDefined(typeof(TerritoryApplyStepStatus), status))
                {
                    threw = true;
                    diagnostics.Add(Error(phaseCode + "InvalidStatus", territoryId, "Fake target returned an undefined " + phaseCode + " status."));
                    return TerritoryApplyStepStatus.Unavailable;
                }

                if (status != TerritoryApplyStepStatus.Applied)
                {
                    diagnostics.Add(Error(phaseCode + "Rejected", territoryId, "Fake target rejected or could not apply the " + phaseCode + " operation."));
                }

                return status;
            }
            catch (Exception exception)
            {
                threw = true;
                diagnostics.Add(Error(exceptionCode, territoryId, exception.GetType().Name));
                return TerritoryApplyStepStatus.Unavailable;
            }
        }

        private static bool InvokeRollback(
            Func<bool> action,
            string phaseCode,
            string exceptionCode,
            string territoryId,
            List<TerritoryDiagnostic> diagnostics)
        {
            try
            {
                bool result = action();
                if (!result)
                {
                    diagnostics.Add(Error(phaseCode + "Rejected", territoryId, "Fake target rejected the " + phaseCode + " operation."));
                }

                return result;
            }
            catch (Exception exception)
            {
                diagnostics.Add(Error(exceptionCode, territoryId, exception.GetType().Name));
                return false;
            }
        }

        private static void SortMigrationCollections(
            List<TerritoryMigrationAction> actions,
            List<TerritoryStateRecord> outputs,
            List<TerritoryStateRecord> unknown)
        {
            actions.Sort((left, right) =>
            {
                int byId = StringComparer.Ordinal.Compare(left.State.Id, right.State.Id);
                return byId != 0 ? byId : left.Kind.CompareTo(right.Kind);
            });
            outputs.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            unknown.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        }

        private static TerritoryMigrationPlan MigrationReject(
            TerritoryMigrationStatus status,
            string operationId,
            string semanticHash,
            string stateHash,
            IEnumerable<TerritoryDiagnostic> diagnostics,
            TerritoryOperationReceipt existingReceipt)
        {
            return new TerritoryMigrationPlan(
                status,
                operationId,
                semanticHash,
                TerritorySemanticHasher.IsLowerSha256(semanticHash)
                    ? BuildIdentity("territory-migration-result-", semanticHash)
                    : string.Empty,
                stateHash,
                Array.Empty<TerritoryMigrationAction>(),
                Array.Empty<TerritoryStateRecord>(),
                Array.Empty<TerritoryStateRecord>(),
                existingReceipt,
                diagnostics);
        }

        private TerritoryCaptureTransactionPlan CaptureReject(
            TerritoryCaptureStatus status,
            TerritoryCaptureRequest request,
            string semanticHash,
            string resultId,
            string receiptId,
            TerritoryCaptureReceipt existingReceipt,
            IEnumerable<TerritoryDiagnostic> diagnostics)
        {
            TerritoryCapturePlan basePlan = request == null
                ? null
                : new TerritoryCapturePlan(
                    status,
                    request.OperationId,
                    request.TerritoryId,
                    request.ExpectedPreviousOwner,
                    request.ExpectedPreviousOwner,
                    request.ExpectedRevision,
                    request.ExpectedRevision,
                    0,
                    0,
                    diagnostics);
            return new TerritoryCaptureTransactionPlan(
                status,
                semanticHash,
                resultId,
                receiptId,
                string.Empty,
                basePlan,
                null,
                null,
                null,
                existingReceipt,
                diagnostics,
                _planProvenance);
        }

        private static TerritoryDefinition BaselineDefinition(
            string id,
            string contentKey,
            RealmId initialOwner,
            ResourceType bonusType,
            long bonusAmount,
            bool isFortress,
            bool allowsNeutralOwnership,
            IEnumerable<RealmId> owners)
        {
            return new TerritoryDefinition(
                id,
                contentKey,
                initialOwner,
                bonusType,
                bonusAmount,
                isFortress,
                owners,
                TerritoryContractPlanner.CurrentRewardProfileId,
                allowsNeutralOwnership,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static string BuildIdentity(string prefix, string semanticHash)
        {
            return prefix + semanticHash;
        }

        private static bool IsDefinedRealm(RealmId realm)
        {
            return Enum.IsDefined(typeof(RealmId), realm);
        }

        private static bool IsContentKey(string value)
        {
            return IsToken(value, TerritoryTechnicalLimits.MaximumContentKeyUtf8Bytes);
        }

        private static bool CatalogIdentityMatches(
            TerritoryCatalogIdentity left,
            TerritoryCatalogIdentity right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.CatalogId, right.CatalogId, StringComparison.Ordinal) &&
                   left.SchemaVersion == right.SchemaVersion &&
                   left.ContentVersion == right.ContentVersion &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.RawSha256, right.RawSha256, StringComparison.Ordinal);
        }

        private static bool IsTechnicalId(string value)
        {
            return IsToken(value, TerritoryTechnicalLimits.MaximumTechnicalIdUtf8Bytes);
        }

        private static bool IsToken(string value, int maximumUtf8Bytes)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Encoding.UTF8.GetByteCount(value) > maximumUtf8Bytes)
            {
                return false;
            }

            return value.All(character =>
                (character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character == '.' ||
                character == '_' ||
                character == '-');
        }

        private static TerritoryDiagnostic Error(
            string code,
            string territoryId,
            string message)
        {
            return new TerritoryDiagnostic(
                TerritoryDiagnosticSeverity.Error,
                code,
                territoryId,
                message);
        }
    }
}
