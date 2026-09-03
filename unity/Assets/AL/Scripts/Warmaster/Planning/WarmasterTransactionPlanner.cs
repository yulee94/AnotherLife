using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Warmaster.Planning
{
    public sealed class WarmasterTransactionPlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const int MaximumSets = 64;
        private const int MaximumPieces = 1024;
        private const int MaximumMemberReferences = 1024;
        private const int MaximumPurchasedPieces = 1024;
        private const int MaximumUnlockedSets = 64;
        private const int MaximumTransactionRecords = 256;

        private readonly WarmasterCatalogSnapshot catalog;
        private readonly IWarmasterTransactionAuthority authority;

        public WarmasterTransactionPlanner(
            WarmasterCatalogSnapshot catalog,
            IWarmasterTransactionAuthority authority)
        {
            this.catalog = catalog;
            this.authority = authority;
        }

        public WarmasterPlanningResult Plan(
            WarmasterTransactionRequest request,
            WarmasterStateSnapshot state,
            WarmasterWalletSnapshot wallet = null)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    WarmasterPlanStatus.InvalidRequest,
                    "AL-WARMASTER-REQUEST-INVALID",
                    request?.OperationId,
                    "Warmaster request identity, operation fields, or revisions are invalid.");
            }

            string requestFingerprint = RequestFingerprint(request);
            WarmasterPlanningResult receiptReplay = ClassifyPriorReceipt(
                request,
                requestFingerprint);
            if (receiptReplay != null)
            {
                return receiptReplay;
            }

            WarmasterPlanningResult catalogGate = TryBuildCatalogIndex(
                catalog,
                out CatalogIndex index);
            if (catalogGate != null)
            {
                return catalogGate;
            }

            if (!BindingEquals(catalog.Binding, request.ExpectedCatalogBinding))
            {
                return Reject(
                    WarmasterPlanStatus.StaleCatalog,
                    "AL-WARMASTER-CATALOG-STALE",
                    request.OperationId,
                    "The request does not bind the current accepted Warmaster catalog.");
            }

            WarmasterPlanningResult stateGate = ValidateState(state, index);
            if (stateGate != null)
            {
                return stateGate;
            }

            WarmasterPlanningResult ledgerReplay = ClassifyLedgerReplay(
                request,
                requestFingerprint,
                state.TransactionRecords);
            if (ledgerReplay != null)
            {
                return ledgerReplay;
            }

            WarmasterPlanningResult futureCollision =
                ClassifyUnknownFutureDefinitionCollision(request, state);
            if (futureCollision != null)
            {
                return futureCollision;
            }

            if (!string.Equals(state.ProfileId, request.ProfileId, StringComparison.Ordinal))
            {
                return Reject(
                    WarmasterPlanStatus.Conflict,
                    "AL-WARMASTER-PROFILE-CONFLICT",
                    request.ProfileId,
                    "Warmaster state belongs to another profile.");
            }

            if (state.Revision != request.ExpectedStateRevision)
            {
                return Reject(
                    WarmasterPlanStatus.StaleState,
                    "AL-WARMASTER-STATE-STALE",
                    request.OperationId,
                    "Expected Warmaster state revision is stale.");
            }

            if (authority == null)
            {
                return Reject(
                    WarmasterPlanStatus.Unavailable,
                    "AL-WARMASTER-AUTHORITY-UNAVAILABLE",
                    request.ActorId,
                    "Warmaster actor authority is unavailable.");
            }

            WarmasterAuthorizationStatus authorization = authority.Authorize(request, state);
            if (authorization == WarmasterAuthorizationStatus.Unavailable)
            {
                return Reject(
                    WarmasterPlanStatus.Unavailable,
                    "AL-WARMASTER-AUTHORITY-UNAVAILABLE",
                    request.ActorId,
                    "Warmaster actor authority is unavailable.");
            }

            if (authorization != WarmasterAuthorizationStatus.Allowed)
            {
                return Reject(
                    WarmasterPlanStatus.Unauthorized,
                    "AL-WARMASTER-ACTOR-UNAUTHORIZED",
                    request.ActorId,
                    "Actor is not authorized for this Warmaster operation.");
            }

            if (state.Revision == long.MaxValue)
            {
                return Reject(
                    WarmasterPlanStatus.Overflow,
                    "AL-WARMASTER-REVISION-OVERFLOW",
                    request.OperationId,
                    "Warmaster state revision cannot advance.");
            }

            if (state.TransactionRecords.Count >= MaximumTransactionRecords)
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-LEDGER-CAPACITY",
                    request.OperationId,
                    "Warmaster transaction ledger cannot safely accept another record.");
            }

            long candidateStateRevision = checked(state.Revision + 1);
            WarmasterTransactionRecord revisionCollision = state.TransactionRecords
                .SingleOrDefault(record =>
                    record.ResultingStateRevision == candidateStateRevision);
            if (revisionCollision != null)
            {
                return Reject(
                    revisionCollision.IsSupported
                        ? WarmasterPlanStatus.Malformed
                        : WarmasterPlanStatus.Unsupported,
                    "AL-WARMASTER-REVISION-COLLISION",
                    revisionCollision.OperationId,
                    "The next Warmaster revision is already reserved by transition history.");
            }

            try
            {
                switch (request.Operation)
                {
                    case WarmasterOperation.PurchasePiece:
                        return PlanPurchase(
                            request,
                            requestFingerprint,
                            state,
                            wallet,
                            index,
                            candidateStateRevision);
                    case WarmasterOperation.UnlockSet:
                        return PlanUnlock(
                            request,
                            requestFingerprint,
                            state,
                            index,
                            candidateStateRevision);
                    case WarmasterOperation.EquipSet:
                        return PlanEquip(
                            request,
                            requestFingerprint,
                            state,
                            index,
                            candidateStateRevision);
                    default:
                        return Reject(
                            WarmasterPlanStatus.InvalidRequest,
                            "AL-WARMASTER-OPERATION-INVALID",
                            request.OperationId,
                            "Warmaster operation is invalid.");
                }
            }
            catch (OverflowException)
            {
                return Reject(
                    WarmasterPlanStatus.Overflow,
                    "AL-WARMASTER-ARITHMETIC-OVERFLOW",
                    request.OperationId,
                    "Warmaster candidate arithmetic overflowed.");
            }
        }

        internal static bool TryVerifyAdapterCommitAndCreateReceipt(
            WarmasterTransactionPlan plan,
            WarmasterStateSnapshot verifiedState,
            WarmasterWalletSnapshot verifiedWallet,
            string verifiedGenerationFingerprint,
            out WarmasterVerifiedReceipt receipt)
        {
            receipt = null;
            if (!IsPreparedPlanShape(plan) ||
                !IsSha256(verifiedGenerationFingerprint) ||
                !StateEquals(verifiedState, plan.CandidateState))
            {
                return false;
            }

            long verifiedEconomyRevision = -1;
            if (plan.RequiresEconomyDebit)
            {
                WarmasterEconomyDebitIntent debit = plan.EconomyDebit;
                if (verifiedWallet == null ||
                    verifiedWallet.Status != WarmasterWalletStatus.Available ||
                    !verifiedWallet.IsComplete ||
                    !string.Equals(
                        verifiedWallet.CurrencyId,
                        debit.CurrencyId,
                        StringComparison.Ordinal) ||
                    verifiedWallet.Balance != debit.CandidateBalance ||
                    verifiedWallet.Revision != debit.CandidateRevision)
                {
                    return false;
                }

                verifiedEconomyRevision = verifiedWallet.Revision;
            }
            else if (verifiedWallet != null)
            {
                return false;
            }

            WarmasterTransactionRecord committedRecord = verifiedState.TransactionRecords
                .SingleOrDefault(record =>
                    TransactionRecordEquals(record, plan.TransactionRecord));
            if (committedRecord == null)
            {
                return false;
            }

            var unsigned = new WarmasterVerifiedReceipt(
                committedRecord,
                verifiedGenerationFingerprint,
                verifiedState.Revision,
                verifiedEconomyRevision,
                string.Empty);
            receipt = new WarmasterVerifiedReceipt(
                committedRecord,
                verifiedGenerationFingerprint,
                verifiedState.Revision,
                verifiedEconomyRevision,
                ReceiptHash(unsigned));
            return IsValidReceipt(receipt);
        }

        private WarmasterPlanningResult PlanPurchase(
            WarmasterTransactionRequest request,
            string requestFingerprint,
            WarmasterStateSnapshot state,
            WarmasterWalletSnapshot wallet,
            CatalogIndex index,
            long candidateStateRevision)
        {
            if (!index.PiecesById.TryGetValue(
                    request.PieceId,
                    out WarmasterPieceDefinition piece) ||
                !index.SetsById.TryGetValue(
                    request.SetId,
                    out WarmasterSetDefinition set))
            {
                return Reject(
                    WarmasterPlanStatus.UnknownDefinition,
                    "AL-WARMASTER-DEFINITION-UNKNOWN",
                    request.PieceId,
                    "Warmaster set or piece is not present in the accepted catalog.");
            }

            if (!string.Equals(piece.SetId, set.SetId, StringComparison.Ordinal))
            {
                return Reject(
                    WarmasterPlanStatus.Conflict,
                    "AL-WARMASTER-MEMBERSHIP-CONFLICT",
                    request.PieceId,
                    "Requested piece does not belong to the requested Warmaster set.");
            }

            if (piece.Availability != WarmasterPieceAvailability.Available)
            {
                return Reject(
                    WarmasterPlanStatus.Ineligible,
                    "AL-WARMASTER-PIECE-UNAVAILABLE",
                    request.PieceId,
                    "Warmaster piece is not currently available for purchase.");
            }

            if (state.PurchasedPieces.Any(record =>
                    record.IsSupported &&
                    string.Equals(record.PieceId, request.PieceId, StringComparison.Ordinal)))
            {
                return Reject(
                    WarmasterPlanStatus.AlreadyOwned,
                    "AL-WARMASTER-PIECE-ALREADY-OWNED",
                    request.PieceId,
                    "Warmaster piece is already owned; no debit or progression is planned.");
            }

            if (state.PurchasedPieces.Count >= MaximumPurchasedPieces)
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-OWNED-CAPACITY",
                    request.PieceId,
                    "Warmaster owned-piece state cannot safely accept another row.");
            }

            WarmasterPlanningResult walletGate = ValidateWallet(
                request,
                wallet,
                catalog.Binding.CurrencyId);
            if (walletGate != null)
            {
                return walletGate;
            }

            if (wallet.Balance < piece.PriceAmount)
            {
                return Reject(
                    WarmasterPlanStatus.InsufficientFunds,
                    "AL-WARMASTER-FUNDS-INSUFFICIENT",
                    request.ProfileId,
                    "Accepted wallet balance is insufficient for the definition-owned price.");
            }

            int candidateLevel = state.Level;
            int candidateExperience = state.Experience;
            if (piece.Progression.Mode == WarmasterProgressionMode.AddDeltas)
            {
                candidateLevel = checked(state.Level + piece.Progression.LevelDelta);
                candidateExperience = checked(
                    state.Experience + piece.Progression.ExperienceDelta);
            }

            IReadOnlyList<WarmasterOwnedPieceRecord> candidatePieces = InsertPiece(
                state.PurchasedPieces,
                new WarmasterOwnedPieceRecord(piece.PieceId, true));
            IReadOnlyList<WarmasterUnlockedSetRecord> candidateSets = state.UnlockedSets;
            string candidateEquippedSetId = state.EquippedSetId;
            bool isComplete = IsSetComplete(set, candidatePieces);
            if (isComplete &&
                set.UnlockPolicy == WarmasterUnlockPolicy.AutomaticOnCompletion &&
                !ContainsSupportedSet(candidateSets, set.SetId))
            {
                if (candidateSets.Count >= MaximumUnlockedSets)
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-UNLOCKED-CAPACITY",
                        request.SetId,
                        "Warmaster unlocked-set state cannot safely accept another row.");
                }

                candidateSets = InsertSet(
                    candidateSets,
                    new WarmasterUnlockedSetRecord(set.SetId, true));
                if (set.EquipPolicy == WarmasterEquipPolicy.AutomaticOnUnlock)
                {
                    candidateEquippedSetId = set.SetId;
                }
            }

            long candidateEconomyRevision = checked(wallet.Revision + 1);
            long candidateBalance = checked(wallet.Balance - piece.PriceAmount);
            var debit = new WarmasterEconomyDebitIntent(
                catalog.Binding.CurrencyId,
                piece.PriceAmount,
                wallet.Revision,
                candidateEconomyRevision,
                candidateBalance,
                HashParts(
                    "warmaster_economy_debit_v1",
                    request.OperationId,
                    request.ProfileId,
                    catalog.Binding.CurrencyId,
                    piece.PriceAmount.ToString(CultureInfo.InvariantCulture)));
            return CreatePlan(
                request,
                requestFingerprint,
                state,
                candidateStateRevision,
                candidatePieces,
                candidateSets,
                candidateEquippedSetId,
                candidateLevel,
                candidateExperience,
                PieceDefinitionFingerprint(catalog.Binding, set, piece),
                debit,
                piece.PriceAmount,
                candidateEconomyRevision);
        }

        private WarmasterPlanningResult PlanUnlock(
            WarmasterTransactionRequest request,
            string requestFingerprint,
            WarmasterStateSnapshot state,
            CatalogIndex index,
            long candidateStateRevision)
        {
            if (!index.SetsById.TryGetValue(
                    request.SetId,
                    out WarmasterSetDefinition set))
            {
                return Reject(
                    WarmasterPlanStatus.UnknownDefinition,
                    "AL-WARMASTER-SET-UNKNOWN",
                    request.SetId,
                    "Warmaster set is not present in the accepted catalog.");
            }

            if (ContainsSupportedSet(state.UnlockedSets, set.SetId))
            {
                return Reject(
                    WarmasterPlanStatus.NoChange,
                    "AL-WARMASTER-SET-ALREADY-UNLOCKED",
                    request.SetId,
                    "Warmaster set is already unlocked.");
            }

            if (!IsSetComplete(set, state.PurchasedPieces))
            {
                return Reject(
                    WarmasterPlanStatus.Ineligible,
                    "AL-WARMASTER-SET-INCOMPLETE",
                    request.SetId,
                    "Exact accepted Warmaster set membership is not complete.");
            }


            if (state.UnlockedSets.Count >= MaximumUnlockedSets)
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-UNLOCKED-CAPACITY",
                    request.SetId,
                    "Warmaster unlocked-set state cannot safely accept another row.");
            }

            IReadOnlyList<WarmasterUnlockedSetRecord> candidateSets = InsertSet(
                state.UnlockedSets,
                new WarmasterUnlockedSetRecord(set.SetId, true));
            string candidateEquippedSetId =
                set.EquipPolicy == WarmasterEquipPolicy.AutomaticOnUnlock
                    ? set.SetId
                    : state.EquippedSetId;
            return CreatePlan(
                request,
                requestFingerprint,
                state,
                candidateStateRevision,
                state.PurchasedPieces,
                candidateSets,
                candidateEquippedSetId,
                state.Level,
                state.Experience,
                SetDefinitionFingerprint(catalog.Binding, set),
                null,
                0,
                -1);
        }

        private WarmasterPlanningResult PlanEquip(
            WarmasterTransactionRequest request,
            string requestFingerprint,
            WarmasterStateSnapshot state,
            CatalogIndex index,
            long candidateStateRevision)
        {
            if (!index.SetsById.ContainsKey(request.SetId))
            {
                return Reject(
                    WarmasterPlanStatus.UnknownDefinition,
                    "AL-WARMASTER-SET-UNKNOWN",
                    request.SetId,
                    "Warmaster set is not present in the accepted catalog.");
            }

            if (!ContainsSupportedSet(state.UnlockedSets, request.SetId))
            {
                return Reject(
                    WarmasterPlanStatus.Ineligible,
                    "AL-WARMASTER-SET-LOCKED",
                    request.SetId,
                    "Warmaster set is not unlocked by verified entitlement.");
            }

            if (string.Equals(
                    state.EquippedSetId,
                    request.SetId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WarmasterPlanStatus.NoChange,
                    "AL-WARMASTER-SET-ALREADY-EQUIPPED",
                    request.SetId,
                    "Warmaster set is already equipped.");
            }

            return CreatePlan(
                request,
                requestFingerprint,
                state,
                candidateStateRevision,
                state.PurchasedPieces,
                state.UnlockedSets,
                request.SetId,
                state.Level,
                state.Experience,
                SetDefinitionFingerprint(
                    catalog.Binding,
                    index.SetsById[request.SetId]),
                null,
                0,
                -1);
        }

        private WarmasterPlanningResult CreatePlan(
            WarmasterTransactionRequest request,
            string requestFingerprint,
            WarmasterStateSnapshot state,
            long candidateStateRevision,
            IReadOnlyList<WarmasterOwnedPieceRecord> candidatePieces,
            IReadOnlyList<WarmasterUnlockedSetRecord> candidateSets,
            string candidateEquippedSetId,
            int candidateLevel,
            int candidateExperience,
            string definitionFingerprint,
            WarmasterEconomyDebitIntent debit,
            long debitAmount,
            long candidateEconomyRevision)
        {
            var stateWithoutRecord = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                state.ProfileId,
                candidateStateRevision,
                catalog.Binding,
                candidatePieces,
                candidateSets,
                candidateEquippedSetId,
                false,
                candidateLevel,
                candidateExperience,
                state.TransactionRecords,
                true);
            string candidateStateHash = StateHash(stateWithoutRecord);
            string notificationCorrelationId = HashParts(
                "warmaster_post_commit_notification_v1",
                request.ProfileId,
                request.OperationId,
                request.EventId,
                request.SetId,
                request.PieceId);
            string planHash = HashParts(
                "warmaster_plan_v1",
                requestFingerprint,
                candidateStateHash,
                candidateStateRevision.ToString(CultureInfo.InvariantCulture),
                definitionFingerprint,
                debitAmount.ToString(CultureInfo.InvariantCulture),
                candidateEconomyRevision.ToString(CultureInfo.InvariantCulture),
                debit?.CandidateBalance.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                notificationCorrelationId);
            var record = new WarmasterTransactionRecord(
                request.OperationId,
                request.EventId,
                request.CorrelationId,
                request.ProfileId,
                request.Operation,
                requestFingerprint,
                catalog.Binding,
                request.SetId,
                request.PieceId,
                debit?.CurrencyId ?? string.Empty,
                debitAmount,
                definitionFingerprint,
                candidateStateRevision,
                candidateEconomyRevision,
                candidateStateHash,
                planHash,
                notificationCorrelationId,
                true);
            IReadOnlyList<WarmasterTransactionRecord> candidateRecords = InsertRecord(
                state.TransactionRecords,
                record);
            var candidateState = new WarmasterStateSnapshot(
                WarmasterStateStatus.Valid,
                state.ProfileId,
                candidateStateRevision,
                catalog.Binding,
                candidatePieces,
                candidateSets,
                candidateEquippedSetId,
                false,
                candidateLevel,
                candidateExperience,
                candidateRecords,
                true);
            var plan = new WarmasterTransactionPlan(
                request.Operation,
                requestFingerprint,
                catalog.Binding,
                state,
                candidateState,
                record,
                debit,
                planHash);
            return new WarmasterPlanningResult(
                WarmasterPlanStatus.Prepared,
                plan,
                null,
                null,
                Array.Empty<WarmasterDiagnostic>());
        }

        private static WarmasterPlanningResult ValidateWallet(
            WarmasterTransactionRequest request,
            WarmasterWalletSnapshot wallet,
            string expectedCurrencyId)
        {
            if (wallet == null || wallet.Status == WarmasterWalletStatus.Unavailable)
            {
                return Reject(
                    WarmasterPlanStatus.Unavailable,
                    "AL-WARMASTER-WALLET-UNAVAILABLE",
                    request.ProfileId,
                    "Warmaster currency authority is unavailable.");
            }

            if (wallet.Status == WarmasterWalletStatus.CommitUncertain)
            {
                return Reject(
                    WarmasterPlanStatus.CommitUncertain,
                    "AL-WARMASTER-WALLET-COMMIT-UNCERTAIN",
                    request.ProfileId,
                    "Wallet authority requires reconciliation before another purchase.");
            }

            if (wallet.Status != WarmasterWalletStatus.Available ||
                !wallet.IsComplete ||
                !IsStableId(wallet.CurrencyId) ||
                wallet.Balance < 0 ||
                wallet.Revision < 0 ||
                !string.Equals(
                    wallet.CurrencyId,
                    expectedCurrencyId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-WALLET-MALFORMED",
                    request.ProfileId,
                    "Warmaster wallet identity, balance, or revision is malformed.");
            }

            if (wallet.Revision != request.ExpectedEconomyRevision)
            {
                return Reject(
                    WarmasterPlanStatus.StaleEconomy,
                    "AL-WARMASTER-WALLET-STALE",
                    request.ProfileId,
                    "Expected Warmaster wallet revision is stale.");
            }

            if (wallet.Revision == long.MaxValue)
            {
                return Reject(
                    WarmasterPlanStatus.Overflow,
                    "AL-WARMASTER-WALLET-REVISION-OVERFLOW",
                    request.ProfileId,
                    "Warmaster wallet revision cannot advance.");
            }

            return null;
        }

        private static WarmasterPlanningResult TryBuildCatalogIndex(
            WarmasterCatalogSnapshot candidate,
            out CatalogIndex index)
        {
            index = null;
            if (candidate == null || candidate.Status == WarmasterCatalogStatus.Unavailable)
            {
                return Reject(
                    WarmasterPlanStatus.Unavailable,
                    "AL-WARMASTER-CATALOG-UNAVAILABLE",
                    string.Empty,
                    "Warmaster catalog authority is unavailable.");
            }

            if (candidate.Status == WarmasterCatalogStatus.ApprovalMissing)
            {
                return Reject(
                    WarmasterPlanStatus.ApprovalMissing,
                    "AL-WARMASTER-CATALOG-APPROVAL-MISSING",
                    string.Empty,
                    "Warmaster catalog approval is missing.");
            }

            if (candidate.Status == WarmasterCatalogStatus.UnsupportedVersion)
            {
                return Reject(
                    WarmasterPlanStatus.Unsupported,
                    "AL-WARMASTER-CATALOG-UNSUPPORTED",
                    string.Empty,
                    "Warmaster catalog version is unsupported.");
            }

            if (candidate.Status == WarmasterCatalogStatus.Incomplete)
            {
                return Reject(
                    WarmasterPlanStatus.Unavailable,
                    "AL-WARMASTER-CATALOG-INCOMPLETE",
                    string.Empty,
                    "Warmaster catalog is incomplete.");
            }

            if (candidate.Status != WarmasterCatalogStatus.Ready)
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-CATALOG-MALFORMED",
                    string.Empty,
                    "Warmaster catalog authority is malformed.");
            }

            if (candidate.Binding != null &&
                !IsOpaqueId(candidate.Binding.ApprovalRevision))
            {
                return Reject(
                    WarmasterPlanStatus.ApprovalMissing,
                    "AL-WARMASTER-CATALOG-APPROVAL-MISSING",
                    string.Empty,
                    "Warmaster catalog approval is missing.");
            }

            if (!candidate.IsComplete ||
                !IsValidBinding(candidate.Binding, requireApproval: true) ||
                candidate.Sets == null ||
                candidate.Pieces == null ||
                candidate.Sets.Count == 0 ||
                candidate.Sets.Count > MaximumSets ||
                candidate.Pieces.Count == 0 ||
                candidate.Pieces.Count > MaximumPieces ||
                !IsStrictlyOrdered(
                    candidate.Sets,
                    set => set?.SetId) ||
                !IsStrictlyOrdered(
                    candidate.Pieces,
                    piece => piece?.PieceId))
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-CATALOG-CONTRADICTORY",
                    string.Empty,
                    "Warmaster catalog identity, bounds, or ordering are contradictory.");
            }

            var setsById = new Dictionary<string, WarmasterSetDefinition>(
                candidate.Sets.Count,
                StringComparer.Ordinal);
            var piecesById = new Dictionary<string, WarmasterPieceDefinition>(
                candidate.Pieces.Count,
                StringComparer.Ordinal);
            var memberOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var memberCount = 0;

            foreach (WarmasterSetDefinition set in candidate.Sets)
            {
                if (set == null ||
                    !IsStableId(set.SetId) ||
                    !set.IsApproved ||
                    set.MemberPieceIds == null ||
                    set.MemberPieceIds.Count == 0 ||
                    !Enum.IsDefined(typeof(WarmasterCompletionPolicy), set.CompletionPolicy) ||
                    !Enum.IsDefined(typeof(WarmasterUnlockPolicy), set.UnlockPolicy) ||
                    !Enum.IsDefined(typeof(WarmasterEquipPolicy), set.EquipPolicy) ||
                    !IsStrictlyOrdered(set.MemberPieceIds, value => value) ||
                    setsById.ContainsKey(set.SetId))
                {
                    return Reject(
                        set != null && !set.IsApproved
                            ? WarmasterPlanStatus.ApprovalMissing
                            : WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-SET-DEFINITION-INVALID",
                        set?.SetId,
                        "Warmaster set definition is invalid or unapproved.");
                }

                setsById.Add(set.SetId, set);

                foreach (string memberPieceId in set.MemberPieceIds)
                {
                    memberCount++;
                    if (memberCount > MaximumMemberReferences ||
                        !IsStableId(memberPieceId) ||
                        memberOwners.ContainsKey(memberPieceId))
                    {
                        return Reject(
                            WarmasterPlanStatus.Malformed,
                            "AL-WARMASTER-MEMBERSHIP-INVALID",
                            memberPieceId,
                            "Warmaster membership is duplicated, malformed, or over capacity.");
                    }

                    memberOwners.Add(memberPieceId, set.SetId);
                }
            }

            foreach (WarmasterPieceDefinition piece in candidate.Pieces)
            {
                bool progressionValid = IsValidProgression(piece?.Progression);
                if (piece == null ||
                    !IsStableId(piece.PieceId) ||
                    !IsStableId(piece.SetId) ||
                    !piece.IsApproved ||
                    piece.PriceAmount <= 0 ||
                    !Enum.IsDefined(typeof(WarmasterPieceAvailability), piece.Availability) ||
                    !progressionValid ||
                    piecesById.ContainsKey(piece.PieceId))
                {
                    bool approvalMissing = piece != null &&
                                           (!piece.IsApproved ||
                                            piece.Progression == null ||
                                            !piece.Progression.IsApproved);
                    return Reject(
                        approvalMissing
                            ? WarmasterPlanStatus.ApprovalMissing
                            : WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-PIECE-DEFINITION-INVALID",
                        piece?.PieceId,
                        "Warmaster piece definition is invalid or unapproved.");
                }

                piecesById.Add(piece.PieceId, piece);

                if (!memberOwners.TryGetValue(piece.PieceId, out string ownerSetId) ||
                    !string.Equals(ownerSetId, piece.SetId, StringComparison.Ordinal) ||
                    !setsById.ContainsKey(piece.SetId))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-MEMBERSHIP-CONTRADICTORY",
                        piece.PieceId,
                        "Warmaster piece ownership disagrees with exact set membership.");
                }
            }

            if (memberOwners.Count != piecesById.Count)
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-MEMBERSHIP-INCOMPLETE",
                    string.Empty,
                    "Warmaster set membership and piece definitions are incomplete.");
            }

            index = new CatalogIndex(candidate.Binding, setsById, piecesById);
            return null;
        }

        private static WarmasterPlanningResult ValidateState(
            WarmasterStateSnapshot state,
            CatalogIndex index)
        {
            if (state == null || state.Status == WarmasterStateStatus.Unavailable)
            {
                return Reject(
                    WarmasterPlanStatus.Unavailable,
                    "AL-WARMASTER-STATE-UNAVAILABLE",
                    string.Empty,
                    "Warmaster saved-state authority is unavailable.");
            }

            if (state.Status == WarmasterStateStatus.MigrationRequired)
            {
                return Reject(
                    WarmasterPlanStatus.MigrationRequired,
                    "AL-WARMASTER-MIGRATION-REQUIRED",
                    state.ProfileId,
                    "Warmaster historical state requires explicit witnessed migration.");
            }

            if (state.Status == WarmasterStateStatus.UnsupportedReadOnly)
            {
                return Reject(
                    WarmasterPlanStatus.Unsupported,
                    "AL-WARMASTER-STATE-UNSUPPORTED",
                    state.ProfileId,
                    "Unknown-future Warmaster state is preserved but cannot be mutated.");
            }

            if (state.Status == WarmasterStateStatus.CommitUncertain)
            {
                return Reject(
                    WarmasterPlanStatus.CommitUncertain,
                    "AL-WARMASTER-COMMIT-UNCERTAIN",
                    state.ProfileId,
                    "Warmaster state requires reconciliation before another operation.");
            }

            if (state.Status != WarmasterStateStatus.Valid ||
                !state.IsComplete ||
                !IsOpaqueId(state.ProfileId) ||
                state.Revision < 0 ||
                state.CatalogBinding == null ||
                state.PurchasedPieces == null ||
                state.UnlockedSets == null ||
                state.TransactionRecords == null ||
                state.PurchasedPieces.Count > MaximumPurchasedPieces ||
                state.UnlockedSets.Count > MaximumUnlockedSets ||
                state.TransactionRecords.Count > MaximumTransactionRecords ||
                state.Level < 0 ||
                state.Experience < 0 ||
                !BindingEquals(state.CatalogBinding, index.Binding) ||
                !IsStrictlyOrdered(state.PurchasedPieces, record => record?.PieceId) ||
                !IsStrictlyOrdered(state.UnlockedSets, record => record?.SetId) ||
                !AreTransactionRecordsOrdered(state.TransactionRecords))
            {
                return Reject(
                    WarmasterPlanStatus.Malformed,
                    "AL-WARMASTER-STATE-MALFORMED",
                    state?.ProfileId,
                    "Warmaster saved state is incomplete, malformed, or bound to another catalog.");
            }

            if (state.LegacyTrueWarmasterFlag)
            {
                return Reject(
                    WarmasterPlanStatus.MigrationRequired,
                    "AL-WARMASTER-LEGACY-FLAG-AMBIGUOUS",
                    state.ProfileId,
                    "Historical True Warmaster flag is evidence requiring explicit migration.");
            }

            foreach (WarmasterOwnedPieceRecord record in state.PurchasedPieces)
            {
                if (record == null || !IsStableId(record.PieceId) ||
                    (record.IsSupported
                        ? !index.PiecesById.ContainsKey(record.PieceId)
                        : index.PiecesById.ContainsKey(record.PieceId)))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-OWNED-ROW-INVALID",
                        record?.PieceId,
                        "Warmaster owned-piece row is malformed or misclassified.");
                }
            }

            foreach (WarmasterUnlockedSetRecord record in state.UnlockedSets)
            {
                if (record == null || !IsStableId(record.SetId) ||
                    (record.IsSupported
                        ? !index.SetsById.ContainsKey(record.SetId)
                        : index.SetsById.ContainsKey(record.SetId)))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-UNLOCKED-ROW-INVALID",
                        record?.SetId,
                        "Warmaster unlocked-set row is malformed or misclassified.");
                }
            }

            if (!string.IsNullOrEmpty(state.EquippedSetId))
            {
                if (!IsStableId(state.EquippedSetId))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-EQUIPPED-SET-INVALID",
                        state.EquippedSetId,
                        "Equipped Warmaster set identity is malformed.");
                }

                if (!index.SetsById.ContainsKey(state.EquippedSetId))
                {
                    return Reject(
                        WarmasterPlanStatus.Unsupported,
                        "AL-WARMASTER-EQUIPPED-SET-UNSUPPORTED",
                        state.EquippedSetId,
                        "Unknown-future equipped set is preserved but excludes mutation.");
                }

                if (!ContainsSupportedSet(state.UnlockedSets, state.EquippedSetId))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-EQUIPPED-SET-LOCKED",
                        state.EquippedSetId,
                        "Equipped Warmaster set is not unlocked.");
                }
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var revisions = new HashSet<long>();
            foreach (WarmasterTransactionRecord record in state.TransactionRecords)
            {
                if (!IsStructurallyValidRecord(record) ||
                    !operationIds.Add(record.OperationId) ||
                    !eventIds.Add(record.EventId) ||
                    !revisions.Add(record.ResultingStateRevision) ||
                    (record.IsSupported && !IsSemanticallyValidRecord(record, index)))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-LEDGER-ROW-INVALID",
                        record?.OperationId,
                        "Warmaster transaction row is malformed, contradictory, or duplicated.");
                }
            }

            if (state.Revision == 0)
            {
                if (state.TransactionRecords.Any(record => record.IsSupported) ||
                    state.PurchasedPieces.Any(record => record.IsSupported) ||
                    state.UnlockedSets.Any(record => record.IsSupported) ||
                    !string.IsNullOrEmpty(state.EquippedSetId))
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-INITIAL-STATE-CONTRADICTORY",
                        state.ProfileId,
                        "Revision-zero Warmaster state cannot contain supported mutations.");
                }
            }
            else
            {
                string currentStateHash = StateHash(state);
                int matchingCurrentRecords = state.TransactionRecords.Count(record =>
                    record.IsSupported &&
                    record.ResultingStateRevision == state.Revision &&
                    string.Equals(
                        record.ResultingStateHash,
                        currentStateHash,
                        StringComparison.Ordinal));
                if (matchingCurrentRecords != 1)
                {
                    return Reject(
                        WarmasterPlanStatus.Malformed,
                        "AL-WARMASTER-STATE-LEDGER-MISMATCH",
                        state.ProfileId,
                        "Current Warmaster state is not backed by one supported transaction row.");
                }
            }

            return null;
        }

        private static WarmasterPlanningResult ClassifyPriorReceipt(
            WarmasterTransactionRequest request,
            string requestFingerprint)
        {
            WarmasterVerifiedReceipt receipt = request.PriorReceipt;
            if (receipt == null)
            {
                return null;
            }

            if (IsValidReceipt(receipt) &&
                RecordMatchesRequest(receipt.TransactionRecord, request, requestFingerprint))
            {
                return new WarmasterPlanningResult(
                    WarmasterPlanStatus.AlreadyCommitted,
                    null,
                    receipt.TransactionRecord,
                    receipt,
                    new[]
                    {
                        Diagnostic(
                            "AL-WARMASTER-RECEIPT-REPLAY",
                            request.OperationId,
                            "Verified Warmaster receipt already satisfies this operation.")
                    });
            }

            return Reject(
                WarmasterPlanStatus.Conflict,
                "AL-WARMASTER-RECEIPT-CONFLICT",
                request.OperationId,
                "Prior Warmaster receipt does not match this request.");
        }

        private static WarmasterPlanningResult ClassifyLedgerReplay(
            WarmasterTransactionRequest request,
            string requestFingerprint,
            IReadOnlyList<WarmasterTransactionRecord> records)
        {
            WarmasterTransactionRecord operationMatch = records.SingleOrDefault(record =>
                string.Equals(record.OperationId, request.OperationId, StringComparison.Ordinal));
            if (operationMatch != null)
            {
                if (!operationMatch.IsSupported)
                {
                    return Reject(
                        WarmasterPlanStatus.Unsupported,
                        "AL-WARMASTER-REPLAY-UNSUPPORTED",
                        request.OperationId,
                        "Operation identity belongs to unknown-future Warmaster history.");
                }

                return RecordMatchesRequest(operationMatch, request, requestFingerprint)
                    ? new WarmasterPlanningResult(
                        WarmasterPlanStatus.AlreadyCommitted,
                        null,
                        operationMatch,
                        null,
                        new[]
                        {
                            Diagnostic(
                                "AL-WARMASTER-LEDGER-REPLAY",
                                request.OperationId,
                                "Committed Warmaster ledger row already satisfies this operation.")
                        })
                    : Reject(
                        WarmasterPlanStatus.Conflict,
                        "AL-WARMASTER-OPERATION-CONFLICT",
                        request.OperationId,
                        "Operation identity is already bound to different Warmaster semantics.");
            }

            WarmasterTransactionRecord eventMatch = records.SingleOrDefault(record =>
                string.Equals(record.EventId, request.EventId, StringComparison.Ordinal));
            if (eventMatch != null)
            {
                return Reject(
                    eventMatch.IsSupported
                        ? WarmasterPlanStatus.Conflict
                        : WarmasterPlanStatus.Unsupported,
                    "AL-WARMASTER-EVENT-CONFLICT",
                    request.EventId,
                    "Event identity is already bound to another Warmaster operation.");
            }

            return null;
        }

        private static WarmasterPlanningResult ClassifyUnknownFutureDefinitionCollision(
            WarmasterTransactionRequest request,
            WarmasterStateSnapshot state)
        {
            bool pieceCollision = !string.IsNullOrEmpty(request.PieceId) &&
                state.PurchasedPieces.Any(record =>
                    !record.IsSupported &&
                    string.Equals(record.PieceId, request.PieceId, StringComparison.Ordinal));
            bool setCollision = state.UnlockedSets.Any(record =>
                !record.IsSupported &&
                string.Equals(record.SetId, request.SetId, StringComparison.Ordinal));
            WarmasterTransactionRecord recordCollision = state.TransactionRecords
                .FirstOrDefault(record =>
                    !record.IsSupported &&
                    (string.Equals(record.SetId, request.SetId, StringComparison.Ordinal) ||
                     (!string.IsNullOrEmpty(request.PieceId) &&
                      string.Equals(record.PieceId, request.PieceId, StringComparison.Ordinal))));
            if (!pieceCollision && !setCollision && recordCollision == null)
            {
                return null;
            }

            return Reject(
                WarmasterPlanStatus.Unsupported,
                "AL-WARMASTER-FUTURE-DEFINITION-COLLISION",
                recordCollision?.OperationId ??
                    (!string.IsNullOrEmpty(request.PieceId)
                        ? request.PieceId
                        : request.SetId),
                "Unknown-future Warmaster evidence collides with the requested definition.");
        }

        private static bool RecordMatchesRequest(
            WarmasterTransactionRecord record,
            WarmasterTransactionRequest request,
            string requestFingerprint)
        {
            if (record == null ||
                !record.IsSupported ||
                request.ExpectedStateRevision == long.MaxValue ||
                (request.Operation == WarmasterOperation.PurchasePiece &&
                 request.ExpectedEconomyRevision == long.MaxValue))
            {
                return false;
            }

            long expectedEconomyResult = request.Operation == WarmasterOperation.PurchasePiece
                ? request.ExpectedEconomyRevision + 1
                : -1;
            return string.Equals(record.OperationId, request.OperationId, StringComparison.Ordinal) &&
                   string.Equals(record.EventId, request.EventId, StringComparison.Ordinal) &&
                   string.Equals(record.CorrelationId, request.CorrelationId, StringComparison.Ordinal) &&
                   string.Equals(record.ProfileId, request.ProfileId, StringComparison.Ordinal) &&
                   record.Operation == request.Operation &&
                   string.Equals(record.RequestFingerprint, requestFingerprint, StringComparison.Ordinal) &&
                   BindingEquals(record.CatalogBinding, request.ExpectedCatalogBinding) &&
                   string.Equals(record.SetId, request.SetId, StringComparison.Ordinal) &&
                   string.Equals(record.PieceId, request.PieceId, StringComparison.Ordinal) &&
                   record.ResultingStateRevision == request.ExpectedStateRevision + 1 &&
                   record.ResultingEconomyRevision == expectedEconomyResult;
        }

        private static bool IsValidRequest(WarmasterTransactionRequest request)
        {
            if (request == null ||
                !Enum.IsDefined(typeof(WarmasterOperation), request.Operation) ||
                !IsOpaqueId(request.ProfileId) ||
                !IsOpaqueId(request.ActorId) ||
                !IsOpaqueId(request.OperationId) ||
                !IsOpaqueId(request.EventId) ||
                !IsOpaqueId(request.CorrelationId) ||
                !IsStableId(request.SetId) ||
                request.ExpectedStateRevision < 0 ||
                !IsValidBinding(request.ExpectedCatalogBinding, requireApproval: true))
            {
                return false;
            }

            switch (request.Operation)
            {
                case WarmasterOperation.PurchasePiece:
                    return IsStableId(request.PieceId) &&
                           request.ExpectedEconomyRevision >= 0;
                case WarmasterOperation.UnlockSet:
                case WarmasterOperation.EquipSet:
                    return string.IsNullOrEmpty(request.PieceId) &&
                           request.ExpectedEconomyRevision == -1;
                default:
                    return false;
            }
        }

        private static bool IsValidBinding(
            WarmasterCatalogBinding binding,
            bool requireApproval)
        {
            return binding != null &&
                   binding.SchemaVersion > 0 &&
                   IsOpaqueId(binding.ContentVersion) &&
                   IsOpaqueId(binding.SourceRevision) &&
                   IsSha256(binding.CatalogHash) &&
                   (!requireApproval || IsOpaqueId(binding.ApprovalRevision)) &&
                   IsStableId(binding.CurrencyId);
        }

        private static bool BindingEquals(
            WarmasterCatalogBinding left,
            WarmasterCatalogBinding right)
        {
            return left != null &&
                   right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   string.Equals(left.ContentVersion, right.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CatalogHash, right.CatalogHash, StringComparison.Ordinal) &&
                   string.Equals(left.ApprovalRevision, right.ApprovalRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CurrencyId, right.CurrencyId, StringComparison.Ordinal);
        }

        private static bool IsValidProgression(WarmasterProgressionRule rule)
        {
            if (rule == null ||
                !rule.IsApproved ||
                !Enum.IsDefined(typeof(WarmasterProgressionMode), rule.Mode) ||
                rule.LevelDelta < 0 ||
                rule.ExperienceDelta < 0)
            {
                return false;
            }

            return rule.Mode == WarmasterProgressionMode.NoChange
                ? rule.LevelDelta == 0 && rule.ExperienceDelta == 0
                : rule.LevelDelta != 0 || rule.ExperienceDelta != 0;
        }

        private static bool IsStructurallyValidRecord(WarmasterTransactionRecord record)
        {
            return record != null &&
                   IsOpaqueId(record.OperationId) &&
                   IsOpaqueId(record.EventId) &&
                   IsOpaqueId(record.CorrelationId) &&
                   IsOpaqueId(record.ProfileId) &&
                   IsSha256(record.RequestFingerprint) &&
                   IsValidBinding(record.CatalogBinding, requireApproval: true) &&
                   IsStableId(record.SetId) &&
                   IsSha256(record.DefinitionFingerprint) &&
                   record.ResultingStateRevision > 0 &&
                   IsSha256(record.ResultingStateHash) &&
                   IsSha256(record.PlanHash) &&
                   IsSha256(record.PostCommitNotificationCorrelationId);
        }

        private static bool IsSemanticallyValidRecord(
            WarmasterTransactionRecord record)
        {
            if (!Enum.IsDefined(typeof(WarmasterOperation), record.Operation))
            {
                return false;
            }

            switch (record.Operation)
            {
                case WarmasterOperation.PurchasePiece:
                    return IsStableId(record.PieceId) &&
                           IsStableId(record.CurrencyId) &&
                           record.DebitAmount > 0 &&
                           record.ResultingEconomyRevision > 0;
                case WarmasterOperation.UnlockSet:
                case WarmasterOperation.EquipSet:
                    return string.IsNullOrEmpty(record.PieceId) &&
                           string.IsNullOrEmpty(record.CurrencyId) &&
                           record.DebitAmount == 0 &&
                           record.ResultingEconomyRevision == -1;
                default:
                    return false;
            }
        }

        private static bool IsSemanticallyValidRecord(
            WarmasterTransactionRecord record,
            CatalogIndex index)
        {
            if (!IsSemanticallyValidRecord(record) ||
                !BindingEquals(record.CatalogBinding, index.Binding) ||
                !index.SetsById.TryGetValue(
                    record.SetId,
                    out WarmasterSetDefinition set))
            {
                return false;
            }

            switch (record.Operation)
            {
                case WarmasterOperation.PurchasePiece:
                    return index.PiecesById.TryGetValue(
                               record.PieceId,
                               out WarmasterPieceDefinition piece) &&
                           string.Equals(piece.SetId, set.SetId, StringComparison.Ordinal) &&
                           string.Equals(
                               record.CurrencyId,
                               index.Binding.CurrencyId,
                               StringComparison.Ordinal) &&
                           record.DebitAmount == piece.PriceAmount &&
                           record.ResultingEconomyRevision > 0 &&
                           string.Equals(
                               record.DefinitionFingerprint,
                               PieceDefinitionFingerprint(index.Binding, set, piece),
                               StringComparison.Ordinal);
                case WarmasterOperation.UnlockSet:
                case WarmasterOperation.EquipSet:
                    return string.IsNullOrEmpty(record.PieceId) &&
                           string.IsNullOrEmpty(record.CurrencyId) &&
                           record.DebitAmount == 0 &&
                           record.ResultingEconomyRevision == -1 &&
                           string.Equals(
                               record.DefinitionFingerprint,
                               SetDefinitionFingerprint(index.Binding, set),
                               StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        private static bool IsPreparedPlanShape(WarmasterTransactionPlan plan)
        {
            if (plan == null ||
                plan.ExpectedCatalogBinding == null ||
                plan.ExpectedState == null ||
                plan.CandidateState == null ||
                plan.TransactionRecord == null ||
                !IsSha256(plan.RequestFingerprint) ||
                !IsSha256(plan.PlanHash) ||
                plan.ExpectedState.Revision == long.MaxValue ||
                plan.CandidateState.Revision != plan.ExpectedState.Revision + 1 ||
                !BindingEquals(plan.ExpectedCatalogBinding, plan.CandidateState.CatalogBinding) ||
                !string.Equals(
                    plan.RequestFingerprint,
                    plan.TransactionRecord.RequestFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    plan.PlanHash,
                    plan.TransactionRecord.PlanHash,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return plan.Operation == WarmasterOperation.PurchasePiece
                ? plan.EconomyDebit != null &&
                  plan.EconomyDebit.ExpectedRevision != long.MaxValue &&
                  plan.EconomyDebit.CandidateRevision == plan.EconomyDebit.ExpectedRevision + 1
                : plan.EconomyDebit == null;
        }

        private static bool IsValidReceipt(WarmasterVerifiedReceipt receipt)
        {
            if (receipt?.TransactionRecord == null ||
                !receipt.TransactionRecord.IsSupported ||
                !IsStructurallyValidRecord(receipt.TransactionRecord) ||
                !IsSemanticallyValidRecord(receipt.TransactionRecord) ||
                !IsSha256(receipt.VerifiedGenerationFingerprint) ||
                receipt.VerifiedStateRevision !=
                    receipt.TransactionRecord.ResultingStateRevision ||
                receipt.VerifiedEconomyRevision !=
                    receipt.TransactionRecord.ResultingEconomyRevision ||
                !IsSha256(receipt.ReceiptHash))
            {
                return false;
            }

            return string.Equals(
                receipt.ReceiptHash,
                ReceiptHash(receipt),
                StringComparison.Ordinal);
        }

        private static string ReceiptHash(WarmasterVerifiedReceipt receipt)
        {
            return HashParts(
                "warmaster_verified_receipt_v1",
                receipt.TransactionRecord.OperationId,
                receipt.TransactionRecord.RequestFingerprint,
                receipt.TransactionRecord.PlanHash,
                receipt.VerifiedGenerationFingerprint,
                receipt.VerifiedStateRevision.ToString(CultureInfo.InvariantCulture),
                receipt.VerifiedEconomyRevision.ToString(CultureInfo.InvariantCulture));
        }

        private static string RequestFingerprint(WarmasterTransactionRequest request)
        {
            return HashParts(
                "warmaster_request_v1",
                ((int)request.Operation).ToString(CultureInfo.InvariantCulture),
                request.ProfileId,
                request.ActorId,
                request.OperationId,
                request.EventId,
                request.CorrelationId,
                request.SetId,
                request.PieceId,
                request.ExpectedStateRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedEconomyRevision.ToString(CultureInfo.InvariantCulture),
                BindingHash(request.ExpectedCatalogBinding));
        }

        internal static string StateHash(WarmasterStateSnapshot state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            IEnumerable<string> pieceParts = state.PurchasedPieces == null
                ? new[] { "<null>" }
                : state.PurchasedPieces.Select(record =>
                    (record?.PieceId ?? "<null>") + ":" +
                    (record != null && record.IsSupported ? "1" : "0"));
            IEnumerable<string> setParts = state.UnlockedSets == null
                ? new[] { "<null>" }
                : state.UnlockedSets.Select(record =>
                    (record?.SetId ?? "<null>") + ":" +
                    (record != null && record.IsSupported ? "1" : "0"));
            return HashParts(
                new[]
                {
                    "warmaster_state_v1",
                    state.ProfileId,
                    state.Revision.ToString(CultureInfo.InvariantCulture),
                    BindingHash(state.CatalogBinding),
                    state.EquippedSetId,
                    state.LegacyTrueWarmasterFlag ? "1" : "0",
                    state.Level.ToString(CultureInfo.InvariantCulture),
                    state.Experience.ToString(CultureInfo.InvariantCulture)
                }
                .Concat(pieceParts)
                .Concat(new[] { "<sets>" })
                .Concat(setParts)
                .ToArray());
        }

        private static string BindingHash(WarmasterCatalogBinding binding)
        {
            if (binding == null)
            {
                return string.Empty;
            }

            return HashParts(
                "warmaster_catalog_binding_v1",
                binding.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                binding.ContentVersion,
                binding.SourceRevision,
                binding.CatalogHash,
                binding.ApprovalRevision,
                binding.CurrencyId);
        }

        private static string SetDefinitionFingerprint(
            WarmasterCatalogBinding binding,
            WarmasterSetDefinition set)
        {
            return HashParts(
                new[]
                {
                    "warmaster_set_definition_v1",
                    BindingHash(binding),
                    set.SetId,
                    ((int)set.CompletionPolicy).ToString(CultureInfo.InvariantCulture),
                    ((int)set.UnlockPolicy).ToString(CultureInfo.InvariantCulture),
                    ((int)set.EquipPolicy).ToString(CultureInfo.InvariantCulture),
                    set.IsApproved ? "1" : "0"
                }
                .Concat(set.MemberPieceIds)
                .ToArray());
        }

        private static string PieceDefinitionFingerprint(
            WarmasterCatalogBinding binding,
            WarmasterSetDefinition set,
            WarmasterPieceDefinition piece)
        {
            return HashParts(
                "warmaster_piece_definition_v1",
                SetDefinitionFingerprint(binding, set),
                piece.PieceId,
                piece.SetId,
                piece.PriceAmount.ToString(CultureInfo.InvariantCulture),
                ((int)piece.Availability).ToString(CultureInfo.InvariantCulture),
                ((int)piece.Progression.Mode).ToString(CultureInfo.InvariantCulture),
                piece.Progression.LevelDelta.ToString(CultureInfo.InvariantCulture),
                piece.Progression.ExperienceDelta.ToString(CultureInfo.InvariantCulture),
                piece.Progression.IsApproved ? "1" : "0",
                piece.IsApproved ? "1" : "0");
        }

        private static IReadOnlyList<WarmasterOwnedPieceRecord> InsertPiece(
            IReadOnlyList<WarmasterOwnedPieceRecord> records,
            WarmasterOwnedPieceRecord candidate)
        {
            return records
                .Concat(new[] { candidate })
                .OrderBy(record => record.PieceId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<WarmasterUnlockedSetRecord> InsertSet(
            IReadOnlyList<WarmasterUnlockedSetRecord> records,
            WarmasterUnlockedSetRecord candidate)
        {
            return records
                .Concat(new[] { candidate })
                .OrderBy(record => record.SetId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<WarmasterTransactionRecord> InsertRecord(
            IReadOnlyList<WarmasterTransactionRecord> records,
            WarmasterTransactionRecord candidate)
        {
            return records
                .Concat(new[] { candidate })
                .OrderBy(record => record.ResultingStateRevision)
                .ThenBy(record => record.OperationId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsSetComplete(
            WarmasterSetDefinition set,
            IReadOnlyList<WarmasterOwnedPieceRecord> pieces)
        {
            var owned = new HashSet<string>(
                pieces.Where(record => record.IsSupported).Select(record => record.PieceId),
                StringComparer.Ordinal);
            return set.CompletionPolicy == WarmasterCompletionPolicy.AllMembers &&
                   set.MemberPieceIds.All(owned.Contains);
        }

        private static bool ContainsSupportedSet(
            IReadOnlyList<WarmasterUnlockedSetRecord> sets,
            string setId)
        {
            return sets.Any(record =>
                record.IsSupported &&
                string.Equals(record.SetId, setId, StringComparison.Ordinal));
        }

        private static bool StateEquals(
            WarmasterStateSnapshot left,
            WarmasterStateSnapshot right)
        {
            if (left == null ||
                right == null ||
                left.Status != right.Status ||
                !string.Equals(left.ProfileId, right.ProfileId, StringComparison.Ordinal) ||
                left.Revision != right.Revision ||
                !BindingEquals(left.CatalogBinding, right.CatalogBinding) ||
                !string.Equals(left.EquippedSetId, right.EquippedSetId, StringComparison.Ordinal) ||
                left.LegacyTrueWarmasterFlag != right.LegacyTrueWarmasterFlag ||
                left.Level != right.Level ||
                left.Experience != right.Experience ||
                left.IsComplete != right.IsComplete ||
                !SequenceEquals(
                    left.PurchasedPieces,
                    right.PurchasedPieces,
                    OwnedPieceEquals) ||
                !SequenceEquals(
                    left.UnlockedSets,
                    right.UnlockedSets,
                    UnlockedSetEquals) ||
                !SequenceEquals(
                    left.TransactionRecords,
                    right.TransactionRecords,
                    TransactionRecordEquals))
            {
                return false;
            }

            return true;
        }

        private static bool TransactionRecordEquals(
            WarmasterTransactionRecord left,
            WarmasterTransactionRecord right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.OperationId, right.OperationId, StringComparison.Ordinal) &&
                   string.Equals(left.EventId, right.EventId, StringComparison.Ordinal) &&
                   string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal) &&
                   string.Equals(left.ProfileId, right.ProfileId, StringComparison.Ordinal) &&
                   left.Operation == right.Operation &&
                   string.Equals(left.RequestFingerprint, right.RequestFingerprint, StringComparison.Ordinal) &&
                   BindingEquals(left.CatalogBinding, right.CatalogBinding) &&
                   string.Equals(left.SetId, right.SetId, StringComparison.Ordinal) &&
                   string.Equals(left.PieceId, right.PieceId, StringComparison.Ordinal) &&
                   string.Equals(left.CurrencyId, right.CurrencyId, StringComparison.Ordinal) &&
                   left.DebitAmount == right.DebitAmount &&
                   string.Equals(
                       left.DefinitionFingerprint,
                       right.DefinitionFingerprint,
                       StringComparison.Ordinal) &&
                   left.ResultingStateRevision == right.ResultingStateRevision &&
                   left.ResultingEconomyRevision == right.ResultingEconomyRevision &&
                   string.Equals(left.ResultingStateHash, right.ResultingStateHash, StringComparison.Ordinal) &&
                   string.Equals(left.PlanHash, right.PlanHash, StringComparison.Ordinal) &&
                   string.Equals(
                       left.PostCommitNotificationCorrelationId,
                       right.PostCommitNotificationCorrelationId,
                       StringComparison.Ordinal) &&
                   left.IsSupported == right.IsSupported;
        }

        private static bool OwnedPieceEquals(
            WarmasterOwnedPieceRecord left,
            WarmasterOwnedPieceRecord right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.PieceId, right.PieceId, StringComparison.Ordinal) &&
                   left.IsSupported == right.IsSupported;
        }

        private static bool UnlockedSetEquals(
            WarmasterUnlockedSetRecord left,
            WarmasterUnlockedSetRecord right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.SetId, right.SetId, StringComparison.Ordinal) &&
                   left.IsSupported == right.IsSupported;
        }

        private static bool SequenceEquals<T>(
            IReadOnlyList<T> left,
            IReadOnlyList<T> right,
            Func<T, T, bool> equals)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (!equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStrictlyOrdered<T>(
            IReadOnlyList<T> values,
            Func<T, string> selector)
        {
            if (values == null)
            {
                return false;
            }

            string previous = null;
            for (var index = 0; index < values.Count; index++)
            {
                string current = selector(values[index]);
                if (current == null ||
                    (previous != null &&
                     string.CompareOrdinal(previous, current) >= 0))
                {
                    return false;
                }

                previous = current;
            }

            return true;
        }

        private static bool AreTransactionRecordsOrdered(
            IReadOnlyList<WarmasterTransactionRecord> records)
        {
            if (records == null)
            {
                return false;
            }

            WarmasterTransactionRecord previous = null;
            foreach (WarmasterTransactionRecord current in records)
            {
                if (current == null)
                {
                    return false;
                }

                if (previous != null &&
                    (previous.ResultingStateRevision > current.ResultingStateRevision ||
                     (previous.ResultingStateRevision == current.ResultingStateRevision &&
                      string.CompareOrdinal(previous.OperationId, current.OperationId) >= 0)))
                {
                    return false;
                }

                previous = current;
            }

            return true;
        }

        private static string HashParts(params string[] parts)
        {
            var canonical = new StringBuilder();
            foreach (string part in parts)
            {
                string value = part ?? string.Empty;
                canonical.Append(
                    Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture));
                canonical.Append(':');
                canonical.Append(value);
            }

            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static bool IsOpaqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Encoding.UTF8.GetByteCount(value) > MaximumIdentityUtf8Bytes)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character) || char.IsWhiteSpace(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStableId(string value)
        {
            if (!IsOpaqueId(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool previousUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool isLetter = character >= 'a' && character <= 'z';
                bool isNumber = character >= '0' && character <= '9';
                bool isUnderscore = character == '_';
                if ((!isLetter && !isNumber && !isUnderscore) ||
                    (isUnderscore && previousUnderscore))
                {
                    return false;
                }

                previousUnderscore = isUnderscore;
            }

            return value[value.Length - 1] != '_';
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        private static WarmasterPlanningResult Reject(
            WarmasterPlanStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new WarmasterPlanningResult(
                status,
                null,
                null,
                null,
                new[] { Diagnostic(code, subjectId, message) });
        }

        private static WarmasterDiagnostic Diagnostic(
            string code,
            string subjectId,
            string message)
        {
            return new WarmasterDiagnostic(code, subjectId, message);
        }

        private sealed class CatalogIndex
        {
            public CatalogIndex(
                WarmasterCatalogBinding binding,
                IReadOnlyDictionary<string, WarmasterSetDefinition> setsById,
                IReadOnlyDictionary<string, WarmasterPieceDefinition> piecesById)
            {
                Binding = binding;
                SetsById = setsById;
                PiecesById = piecesById;
            }

            public WarmasterCatalogBinding Binding { get; }
            public IReadOnlyDictionary<string, WarmasterSetDefinition> SetsById { get; }
            public IReadOnlyDictionary<string, WarmasterPieceDefinition> PiecesById { get; }
        }
    }
}
