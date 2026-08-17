using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmGems;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalRealmGemService : IRealmGemService
    {
        private const int WarmasterCreditRewardAmount = 300;
        private const string WarmasterCreditRewardId = "warmaster_credits";

        private static readonly ConditionalWeakTable<ISaveGameService, TransactionState> TransactionStates =
            new ConditionalWeakTable<ISaveGameService, TransactionState>();

        private readonly ISaveGameService _saveGameService;
        private readonly Func<RealmGemWishgateCatalogSnapshot> _catalogProvider;
        private readonly IRealmGemWishgateAuthorityProvider _authorityProvider;
        private readonly Func<SaveGameData> _mutableSaveProvider;
        private readonly IWishgateOutcomePublisher _outcomePublisher;



        public LocalRealmGemService(ISaveGameService saveGameService)
            : this(saveGameService, () => RealmGemWishgateRuntimeCatalog.Current, null)
        {
        }

        internal LocalRealmGemService(
            ISaveGameService saveGameService,
            Func<RealmGemWishgateCatalogSnapshot> catalogProvider)
            : this(saveGameService, catalogProvider, null)
        {
        }

        public LocalRealmGemService(
            ISaveGameService saveGameService,
            Func<RealmGemWishgateCatalogSnapshot> catalogProvider,
            IRealmGemWishgateAuthorityProvider authorityProvider)
            : this(saveGameService, catalogProvider, authorityProvider, null, null)
        {
        }

        public LocalRealmGemService(
            ISaveGameService saveGameService,
            Func<RealmGemWishgateCatalogSnapshot> catalogProvider,
            IRealmGemWishgateAuthorityProvider authorityProvider,
            IWishgateOutcomePublisher outcomePublisher)
            : this(saveGameService, catalogProvider, authorityProvider, null, outcomePublisher)
        {
        }

        internal LocalRealmGemService(
            ISaveGameService saveGameService,
            Func<RealmGemWishgateCatalogSnapshot> catalogProvider,
            IRealmGemWishgateAuthorityProvider authorityProvider,
            Func<SaveGameData> mutableSaveProvider,
            IWishgateOutcomePublisher outcomePublisher = null)
        {
            _saveGameService = saveGameService;
            _catalogProvider = catalogProvider ?? (() => null);
            _authorityProvider = authorityProvider;
            _mutableSaveProvider = mutableSaveProvider;
            _outcomePublisher = outcomePublisher;
        }

        public IEnumerable<RealmGemState> GetRealmGems()
        {
            var gems = _saveGameService.CurrentSave?.RealmGems;
            if (gems == null || gems.Count == 0)
            {
                return Enumerable.Empty<RealmGemState>();
            }

            return gems
                .Where(gem => gem != null)
                .Select(CloneGem)
                .ToArray();
        }

        public WishgateState GetWishgateState()
        {
            return CloneWishgate(_saveGameService.CurrentSave?.Wishgate);
        }

        // Compatibility entry points intentionally provide no zone authority and
        // therefore fail closed. Production consumers must use typed requests.
        public bool PickUpGem(string gemId, string carrierId) =>
            PickUpGem(new RealmGemMutationRequest(gemId, carrierId, string.Empty)).IsAllowed;

        public RealmGemMutationResult PickUpGem(RealmGemMutationRequest request)
        {
            lock (GetTransactionGate())
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                RealmGemWishgateCatalogSnapshot catalog = GetCatalogAuthority();
                RealmGemWishgatePolicyResult policy = AuthorizeRealmGem(catalog, request);
                if (!policy.IsAllowed) return Denied(policy);

                if (!TryGetMutableGem(catalog, request.GemId, now, out RealmGemState gem) || IsCarried(gem))
                    return InvalidState();
                if (gem.IsDropped && now - gem.LastDroppedTimestamp < 10)
                    return InvalidState();

                gem.IsAtHome = false;
                gem.IsDropped = false;
                gem.CarrierId = policy.Authority.ActorId;
                _saveGameService.Save();
                Debug.Log($"Realm Gem {request.GemId} picked up by {policy.Authority.ActorId}");
                return Allowed(policy);
            }
        }

        public void DropGem(string gemId)
        {
            DropGem(new RealmGemMutationRequest(gemId, string.Empty, string.Empty));
        }

        public RealmGemMutationResult DropGem(RealmGemMutationRequest request)
        {
            lock (GetTransactionGate())
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                RealmGemWishgateCatalogSnapshot catalog = GetCatalogAuthority();
                RealmGemWishgatePolicyResult policy = AuthorizeRealmGem(catalog, request);
                if (!policy.IsAllowed) return Denied(policy);

                if (!TryGetMutableGem(catalog, request.GemId, now, out RealmGemState gem) ||
                    !IsCarried(gem) ||
                    !string.Equals(gem.CarrierId, policy.Authority.ActorId, StringComparison.Ordinal))
                {
                    return InvalidState();
                }

                gem.IsAtHome = false;
                gem.IsDropped = true;
                gem.CarrierId = null;
                gem.LastDroppedTimestamp = now;
                _saveGameService.Save();
                return Allowed(policy);
            }
        }

        public void ReturnGemHome(string gemId)
        {
            ReturnGemHome(new RealmGemMutationRequest(gemId, string.Empty, string.Empty));
        }

        public RealmGemMutationResult ReturnGemHome(RealmGemMutationRequest request)
        {
            lock (GetTransactionGate())
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                RealmGemWishgateCatalogSnapshot catalog = GetCatalogAuthority();
                RealmGemWishgatePolicyResult policy = AuthorizeRealmGem(catalog, request);
                if (!policy.IsAllowed) return Denied(policy);

                if (!TryGetMutableGem(catalog, request.GemId, now, out RealmGemState gem) ||
                    gem.IsAtHome ||
                    policy.Authority.ControllingRealm != gem.HomeRealm ||
                    (IsCarried(gem) &&
                     !string.Equals(gem.CarrierId, policy.Authority.ActorId, StringComparison.Ordinal)))
                    return InvalidState();

                gem.IsAtHome = true;
                gem.IsDropped = false;
                gem.CarrierId = null;
                gem.LastDroppedTimestamp = 0;
                _saveGameService.Save();
                return Allowed(policy);
            }
        }

        // These untyped Wishgate writers cannot prove actor, realm, location, or
        // entitlement authority and remain deliberately unavailable.
        public void MarkWishgateEarned(string reason) { }
        public void ChooseWishReward(string rewardId) { }

        public WishgateRewardResult ApplyWishgateReward(WishgateRewardRequest request)
        {
            if (request == null || !request.HasValidIdentity)
                return RewardFailure(WishgateRewardStatus.MissingContext, "AL-RGW-REWARD-CONTEXT");
            if (!TryGetMutableSave(out SaveGameData save) || save.Wishgate == null)
                return RewardFailure(WishgateRewardStatus.InvalidState, "AL-RGW-REWARD-STATE");

            lock (GetTransactionGate())
            {

                string fingerprint = request.PayloadFingerprint();
                WishgateRewardReceiptData prior = save.Wishgate.CommittedReward;
                if (save.Wishgate.HasCommittedReward)
                {
                    if (!IsValidCommittedReceipt(
                            save.Wishgate,
                            prior,
                            allowMissingOutcomeIdentity: true))
                        return RewardFailure(WishgateRewardStatus.InvalidState, "AL-RGW-REWARD-RECEIPT-MALFORMED");
                    if (!string.Equals(prior.OperationId, request.OperationId, StringComparison.Ordinal))
                        return RewardFailure(
                            WishgateRewardStatus.EntitlementAlreadyConsumed,
                            "AL-RGW-REWARD-ENTITLEMENT-CONSUMED");
                    if (!string.Equals(prior.PayloadFingerprint, fingerprint, StringComparison.Ordinal))
                    {
                        return RewardFailure(
                            WishgateRewardStatus.IdempotencyConflict,
                            "AL-RGW-REWARD-IDEMPOTENCY-CONFLICT");
                    }

                    if (prior.CommitUncertain)
                    {
                        _saveGameService.Save();
                        if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                            return WishgateRewardResult.FromReceipt(prior, WishgateRewardStatus.CommitUncertain);
                        if (!TryReacquireCommittedReceipt(
                                request.OperationId,
                                fingerprint,
                                out save,
                                out prior))
                        {
                            return RewardFailure(
                                WishgateRewardStatus.InvalidState,
                                "AL-RGW-REWARD-RECEIPT-REACQUIRE");
                        }

                        prior.CommitUncertain = false;
                        _saveGameService.Save();
                        if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                        {
                            prior.CommitUncertain = true;
                            return WishgateRewardResult.FromReceipt(prior, WishgateRewardStatus.CommitUncertain);
                        }
                        if (!TryReacquireCommittedReceipt(
                                request.OperationId,
                                fingerprint,
                                out save,
                                out prior) ||
                            prior.CommitUncertain)
                        {
                            return RewardFailure(
                                WishgateRewardStatus.CommitUncertain,
                                "AL-RGW-REWARD-COMMIT-UNCERTAIN");
                        }
                    }

                    if (!EnsureOutcomeIdentityCommitted(
                            request.OperationId,
                            fingerprint,
                            ref save,
                            ref prior))
                    {
                        return RewardFailure(
                            WishgateRewardStatus.CommitUncertain,
                            "AL-RGW-REWARD-OUTCOME-IDENTITY-COMMIT");
                    }

                    DeliverCommittedOutcome(prior);
                    return WishgateRewardResult.FromReceipt(prior, WishgateRewardStatus.AlreadyCommitted);
                }

                if (save.Wishgate.IsEarned ||
                    !string.IsNullOrWhiteSpace(save.Wishgate.LastRewardId) ||
                    save.Wishgate.LastRewardChosenTimestamp != 0 ||
                    save.WarzoneCredits < 0)
                {
                    return RewardFailure(WishgateRewardStatus.InvalidState, "AL-RGW-REWARD-STATE");
                }

                RealmGemWishgateCatalogSnapshot catalog = GetCatalogAuthority();
                if (catalog == null || catalog.Wishgate == null || !catalog.Wishgate.RewardAuthorityAvailable)
                    return RewardFailure(WishgateRewardStatus.CatalogUnavailable, "AL-RGW-REWARD-CATALOG");
                if (!catalog.IsApprovedReward(request.RewardId))
                    return RewardFailure(WishgateRewardStatus.UnknownReward, "AL-RGW-REWARD-UNKNOWN");
                if (!string.Equals(request.RewardId, WarmasterCreditRewardId, StringComparison.Ordinal))
                    return RewardFailure(WishgateRewardStatus.RewardMutationRejected, "AL-RGW-REWARD-UNSUPPORTED");

                RealmGemWishgatePolicyResult policy = RealmGemWishgateEligibilityPolicy.EvaluateWishgate(
                    catalog,
                    _authorityProvider,
                    new WishgateUseRequest(request.ActorId, request.ZoneId));
                if (!policy.IsAllowed) return RewardDenied(policy);

                int previousCredits = save.WarzoneCredits;
                bool previousEarned = save.Wishgate.IsEarned;
                string previousReason = save.Wishgate.EarnReason;
                string previousRewardId = save.Wishgate.LastRewardId;
                long previousTimestamp = save.Wishgate.LastRewardChosenTimestamp;
                bool previousHasReceipt = save.Wishgate.HasCommittedReward;
                WishgateRewardReceiptData previousReceipt = save.Wishgate.CommittedReward;
                bool persistenceStarted = false;

                try
                {
                    int nextCredits = checked(previousCredits + WarmasterCreditRewardAmount);
                    long committedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var receipt = new WishgateRewardReceiptData
                    {
                        OperationId = request.OperationId,
                        PayloadFingerprint = fingerprint,
                        ActorId = policy.Authority.ActorId,
                        ZoneId = request.ZoneId,
                        RewardId = request.RewardId,
                        WarzoneCreditsAwarded = WarmasterCreditRewardAmount,
                        CommittedTimestamp = committedTimestamp,
                        CommitUncertain = true
                    };
                    receipt.OutcomeIdentity = WishgateCommittedOutcome.ComputeOutcomeIdentity(receipt);

                    save.WarzoneCredits = nextCredits;
                    save.Wishgate.IsEarned = true;
                    save.Wishgate.EarnReason = "complete_eight_gems";
                    save.Wishgate.LastRewardId = request.RewardId;
                    save.Wishgate.LastRewardChosenTimestamp = committedTimestamp;
                    save.Wishgate.HasCommittedReward = true;
                    save.Wishgate.CommittedReward = receipt;

                    persistenceStarted = true;
                    _saveGameService.Save();
                    if (_saveGameService.LastSaveStatus == SaveOperationStatus.CommitUncertain)
                    {
                        return WishgateRewardResult.FromReceipt(receipt, WishgateRewardStatus.CommitUncertain);
                    }
                    if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                    {
                        RestoreWishgateReward(
                            save,
                            previousCredits,
                            previousEarned,
                            previousReason,
                            previousRewardId,
                            previousTimestamp,
                            previousHasReceipt,
                            previousReceipt);
                        return RewardFailure(
                            WishgateRewardStatus.SaveFailedRolledBack,
                            "AL-RGW-REWARD-SAVE-FAILED");
                    }

                    if (!TryReacquireCommittedReceipt(
                            request.OperationId,
                            fingerprint,
                            out save,
                            out receipt))
                    {
                        return RewardFailure(
                            WishgateRewardStatus.CommitUncertain,
                            "AL-RGW-REWARD-COMMIT-REACQUIRE");
                    }

                    receipt.CommitUncertain = false;
                    _saveGameService.Save();
                    if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                    {
                        receipt.CommitUncertain = true;
                        return WishgateRewardResult.FromReceipt(receipt, WishgateRewardStatus.CommitUncertain);
                    }

                    if (!TryReacquireCommittedReceipt(
                            request.OperationId,
                            fingerprint,
                            out save,
                            out receipt) ||
                        receipt.CommitUncertain)
                    {
                        return RewardFailure(
                            WishgateRewardStatus.CommitUncertain,
                            "AL-RGW-REWARD-COMMIT-REVERIFY");
                    }

                    WishgateRewardResult committed =
                        WishgateRewardResult.FromReceipt(receipt, WishgateRewardStatus.Committed);
                    DeliverCommittedOutcome(receipt);
                    return committed;
                }
                catch (OverflowException)
                {
                    RestoreWishgateReward(
                        save,
                        previousCredits,
                        previousEarned,
                        previousReason,
                        previousRewardId,
                        previousTimestamp,
                        previousHasReceipt,
                        previousReceipt);
                    return RewardFailure(
                        WishgateRewardStatus.RewardMutationRejected,
                        "AL-RGW-REWARD-OVERFLOW");
                }
                catch (Exception)
                {
                    if (!persistenceStarted ||
                        _saveGameService.LastSaveStatus == SaveOperationStatus.SaveFailedPreviousPreserved)
                    {
                        RestoreWishgateReward(
                            save,
                            previousCredits,
                            previousEarned,
                            previousReason,
                            previousRewardId,
                            previousTimestamp,
                            previousHasReceipt,
                            previousReceipt);
                        return RewardFailure(
                            WishgateRewardStatus.SaveFailedRolledBack,
                            "AL-RGW-REWARD-SAVE-EXCEPTION-ROLLED-BACK");
                    }

                    return RewardFailure(
                        WishgateRewardStatus.CommitUncertain,
                        "AL-RGW-REWARD-COMMIT-UNCERTAIN");
                }
            }
        }

        public WishgateOutcomeDeliveryResult RecoverPendingWishgateOutcome()
        {
            if (!TryGetMutableSave(out SaveGameData save) || save.Wishgate == null)
                return new WishgateOutcomeDeliveryResult(WishgateOutcomeDeliveryStatus.NoCommittedOutcome);

            lock (GetTransactionGate())
            {
                WishgateRewardReceiptData receipt = save.Wishgate.CommittedReward;
                if (!save.Wishgate.HasCommittedReward || receipt == null)
                    return new WishgateOutcomeDeliveryResult(WishgateOutcomeDeliveryStatus.NoCommittedOutcome);
                if (!IsValidCommittedReceipt(save.Wishgate, receipt, allowMissingOutcomeIdentity: true))
                    return new WishgateOutcomeDeliveryResult(WishgateOutcomeDeliveryStatus.InvalidCommittedOutcome);
                if (string.IsNullOrWhiteSpace(receipt.OutcomeIdentity))
                {
                    receipt.OutcomeIdentity = WishgateCommittedOutcome.ComputeOutcomeIdentity(receipt);
                    _saveGameService.Save();
                    if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary ||
                        !TryReacquireCommittedReceipt(
                            receipt.OperationId,
                            receipt.PayloadFingerprint,
                            out save,
                            out receipt))
                    {
                        return new WishgateOutcomeDeliveryResult(
                            WishgateOutcomeDeliveryStatus.DeliveryReceiptNotCommitted,
                            receipt.OperationId,
                            receipt.OutcomeIdentity);
                    }
                }
                return DeliverCommittedOutcome(receipt);
            }
        }

        private WishgateOutcomeDeliveryResult DeliverCommittedOutcome(WishgateRewardReceiptData receipt)
        {
            if (receipt.CommitUncertain)
            {
                return new WishgateOutcomeDeliveryResult(
                    WishgateOutcomeDeliveryStatus.CommitUncertain,
                    receipt.OperationId,
                    receipt.OutcomeIdentity);
            }
            if (receipt.OutcomeNotificationDelivered)
            {
                return new WishgateOutcomeDeliveryResult(
                    WishgateOutcomeDeliveryStatus.AlreadyDelivered,
                    receipt.OperationId,
                    receipt.OutcomeIdentity);
            }
            if (_outcomePublisher == null)
            {
                return new WishgateOutcomeDeliveryResult(
                    WishgateOutcomeDeliveryStatus.PublisherUnavailable,
                    receipt.OperationId,
                    receipt.OutcomeIdentity);
            }
            TransactionState transactionState = GetTransactionState();
            if (transactionState.OutcomeDeliveryInProgress)
            {
                return new WishgateOutcomeDeliveryResult(
                    WishgateOutcomeDeliveryStatus.DeliveryFailed,
                    receipt.OperationId,
                    receipt.OutcomeIdentity);
            }

            try
            {
                transactionState.OutcomeDeliveryInProgress = true;
                _outcomePublisher.Publish(new WishgateCommittedOutcome(receipt));
            }
            catch (Exception)
            {
                return new WishgateOutcomeDeliveryResult(
                    WishgateOutcomeDeliveryStatus.DeliveryFailed,
                    receipt.OperationId,
                    receipt.OutcomeIdentity);
            }
            finally
            {
                transactionState.OutcomeDeliveryInProgress = false;
            }

            try
            {
                if (!TryReacquireCommittedReceipt(
                        receipt.OperationId,
                        receipt.PayloadFingerprint,
                        out _,
                        out receipt) ||
                    receipt.CommitUncertain ||
                    receipt.OutcomeNotificationDelivered)
                {
                    return new WishgateOutcomeDeliveryResult(
                        receipt != null && receipt.OutcomeNotificationDelivered
                            ? WishgateOutcomeDeliveryStatus.AlreadyDelivered
                            : WishgateOutcomeDeliveryStatus.DeliveryReceiptNotCommitted,
                        receipt?.OperationId,
                        receipt?.OutcomeIdentity);
                }

                receipt.OutcomeNotificationDelivered = true;
                _saveGameService.Save();
                if (_saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary ||
                    !TryReacquireCommittedReceipt(
                        receipt.OperationId,
                        receipt.PayloadFingerprint,
                        out _,
                        out WishgateRewardReceiptData persistedReceipt) ||
                    !persistedReceipt.OutcomeNotificationDelivered)
                {
                    receipt.OutcomeNotificationDelivered = false;
                    return new WishgateOutcomeDeliveryResult(
                        WishgateOutcomeDeliveryStatus.DeliveryReceiptNotCommitted,
                        receipt.OperationId,
                        receipt.OutcomeIdentity);
                }
            }
            catch (Exception)
            {
                receipt.OutcomeNotificationDelivered = false;
                return new WishgateOutcomeDeliveryResult(
                    WishgateOutcomeDeliveryStatus.DeliveryReceiptNotCommitted,
                    receipt.OperationId,
                    receipt.OutcomeIdentity);
            }

            return new WishgateOutcomeDeliveryResult(
                WishgateOutcomeDeliveryStatus.Delivered,
                receipt.OperationId,
                receipt.OutcomeIdentity);
        }

        private RealmGemWishgatePolicyResult AuthorizeRealmGem(
            RealmGemWishgateCatalogSnapshot catalog,
            RealmGemMutationRequest request) =>
            RealmGemWishgateEligibilityPolicy.EvaluateRealmGem(
                catalog,
                _authorityProvider,
                request);

        private static RealmGemState CloneGem(RealmGemState source)
        {
            return new RealmGemState
            {
                GemId = source.GemId,
                HomeRealm = source.HomeRealm,
                GemIndex = source.GemIndex,
                IsAtHome = source.IsAtHome,
                IsDropped = source.IsDropped,
                CarrierId = source.CarrierId,
                LastDroppedTimestamp = source.LastDroppedTimestamp
            };
        }

        private static WishgateState CloneWishgate(WishgateState source)
        {
            if (source == null) return new WishgateState();
            return new WishgateState
            {
                IsEarned = source.IsEarned,
                EarnReason = source.EarnReason,
                LastRewardId = source.LastRewardId,
                LastRewardChosenTimestamp = source.LastRewardChosenTimestamp,
                HasCommittedReward = source.HasCommittedReward,
                CommittedReward = CloneReceipt(source.CommittedReward)
            };
        }

        private static WishgateRewardReceiptData CloneReceipt(WishgateRewardReceiptData source)
        {
            if (source == null) return null;
            return new WishgateRewardReceiptData
            {
                OperationId = source.OperationId,
                PayloadFingerprint = source.PayloadFingerprint,
                OutcomeIdentity = source.OutcomeIdentity,
                ActorId = source.ActorId,
                ZoneId = source.ZoneId,
                RewardId = source.RewardId,
                WarzoneCreditsAwarded = source.WarzoneCreditsAwarded,
                CommittedTimestamp = source.CommittedTimestamp,
                CommitUncertain = source.CommitUncertain,
                OutcomeNotificationDelivered = source.OutcomeNotificationDelivered
            };
        }

        private bool TryGetMutableGem(
            RealmGemWishgateCatalogSnapshot catalog,
            string gemId,
            long now,
            out RealmGemState gem)
        {
            gem = null;
            if (string.IsNullOrWhiteSpace(gemId)) return false;

            if (catalog == null ||
                !catalog.CustodyAuthorityAvailable ||
                !catalog.TryGetRealmGem(gemId, out RealmGemCatalogEntry catalogEntry))
            {
                return false;
            }

            if (!TryGetMutableSave(out SaveGameData save) || save.RealmGems == null) return false;

            RealmGemState[] matches = save.RealmGems
                .Where(candidate =>
                    candidate != null &&
                    string.Equals(candidate.GemId, gemId, StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (matches.Length != 1 ||
                matches[0].HomeRealm != catalogEntry.RuntimeRealmId ||
                matches[0].GemIndex != catalogEntry.GemIndex ||
                !HasValidCustody(matches[0], now))
            {
                return false;
            }

            gem = matches[0];
            return true;
        }

        private RealmGemWishgateCatalogSnapshot GetCatalogAuthority()
        {
            try { return _catalogProvider(); }
            catch (Exception) { return null; }
        }

        private bool TryGetMutableSave(out SaveGameData save)
        {
            if (_mutableSaveProvider != null)
            {
                try
                {
                    save = _mutableSaveProvider();
                    return save != null;
                }
                catch (Exception)
                {
                    save = null;
                    return false;
                }
            }

            return ProfileMutationContainment.TryGetMutableSave(
                _saveGameService,
                ProfileMutationSurfaceIds.RealmGem,
                out save);
        }

        private object GetTransactionGate()
        {
            return GetTransactionState().Gate;
        }

        private TransactionState GetTransactionState()
        {
            return _saveGameService == null
                ? new TransactionState(this)
                : TransactionStates.GetValue(_saveGameService, _ => new TransactionState());
        }

        private bool EnsureOutcomeIdentityCommitted(
            string operationId,
            string fingerprint,
            ref SaveGameData save,
            ref WishgateRewardReceiptData receipt)
        {
            if (!string.IsNullOrWhiteSpace(receipt.OutcomeIdentity)) return true;

            receipt.OutcomeIdentity = WishgateCommittedOutcome.ComputeOutcomeIdentity(receipt);
            _saveGameService.Save();
            return _saveGameService.LastSaveStatus == SaveOperationStatus.SavedPrimary &&
                   TryReacquireCommittedReceipt(
                       operationId,
                       fingerprint,
                       out save,
                       out receipt) &&
                   !string.IsNullOrWhiteSpace(receipt.OutcomeIdentity);
        }

        private sealed class TransactionState
        {
            public TransactionState(object gate = null)
            {
                Gate = gate ?? new object();
            }

            public object Gate { get; }
            public bool OutcomeDeliveryInProgress { get; set; }
        }

        private static bool HasValidCustody(RealmGemState gem, long now) =>
            gem != null &&
            gem.HomeRealm != RealmId.None &&
            gem.GemIndex > 0 &&
            gem.LastDroppedTimestamp >= 0 &&
            ((!gem.IsDropped &&
              ((gem.IsAtHome && string.IsNullOrWhiteSpace(gem.CarrierId)) ||
               (!gem.IsAtHome && !string.IsNullOrWhiteSpace(gem.CarrierId)))) ||
             (!gem.IsAtHome &&
              gem.IsDropped &&
              string.IsNullOrWhiteSpace(gem.CarrierId) &&
              gem.LastDroppedTimestamp > 0 &&
              gem.LastDroppedTimestamp <= now));

        private static bool IsCarried(RealmGemState gem) =>
            !gem.IsAtHome &&
            !gem.IsDropped &&
            !string.IsNullOrWhiteSpace(gem.CarrierId);

        private bool TryReacquireCommittedReceipt(
            string operationId,
            string fingerprint,
            out SaveGameData save,
            out WishgateRewardReceiptData receipt)
        {
            receipt = null;
            if (!TryGetMutableSave(out save) || save.Wishgate == null)
                return false;
            receipt = save.Wishgate.CommittedReward;
            return save.Wishgate.HasCommittedReward &&
                   IsValidCommittedReceipt(save.Wishgate, receipt) &&
                   string.Equals(receipt.OperationId, operationId, StringComparison.Ordinal) &&
                   string.Equals(receipt.PayloadFingerprint, fingerprint, StringComparison.Ordinal);
        }

        private static bool IsValidCommittedReceipt(
            WishgateState wishgate,
            WishgateRewardReceiptData receipt,
            bool allowMissingOutcomeIdentity = false) =>
            wishgate != null &&
            receipt != null &&
            wishgate.IsEarned &&
            !string.IsNullOrWhiteSpace(receipt.OperationId) &&
            !string.IsNullOrWhiteSpace(receipt.PayloadFingerprint) &&
            (allowMissingOutcomeIdentity || !string.IsNullOrWhiteSpace(receipt.OutcomeIdentity)) &&
            !string.IsNullOrWhiteSpace(receipt.ActorId) &&
            !string.IsNullOrWhiteSpace(receipt.ZoneId) &&
            !string.IsNullOrWhiteSpace(receipt.RewardId) &&
            string.Equals(receipt.RewardId, WarmasterCreditRewardId, StringComparison.Ordinal) &&
            string.Equals(wishgate.LastRewardId, receipt.RewardId, StringComparison.Ordinal) &&
            wishgate.LastRewardChosenTimestamp == receipt.CommittedTimestamp &&
            receipt.WarzoneCreditsAwarded == WarmasterCreditRewardAmount &&
            (string.IsNullOrWhiteSpace(receipt.OutcomeIdentity)
                ? allowMissingOutcomeIdentity
                : string.Equals(
                    receipt.OutcomeIdentity,
                    WishgateCommittedOutcome.ComputeOutcomeIdentity(receipt),
                    StringComparison.Ordinal)) &&
            string.Equals(
                receipt.PayloadFingerprint,
                new WishgateRewardRequest(
                    receipt.OperationId,
                    receipt.ActorId,
                    receipt.ZoneId,
                    receipt.RewardId).PayloadFingerprint(),
                StringComparison.Ordinal) &&
            receipt.CommittedTimestamp > 0;

        private static void RestoreWishgateReward(
            SaveGameData save,
            int credits,
            bool isEarned,
            string earnReason,
            string rewardId,
            long rewardTimestamp,
            bool hasCommittedReward,
            WishgateRewardReceiptData receipt)
        {
            save.WarzoneCredits = credits;
            save.Wishgate.IsEarned = isEarned;
            save.Wishgate.EarnReason = earnReason;
            save.Wishgate.LastRewardId = rewardId;
            save.Wishgate.LastRewardChosenTimestamp = rewardTimestamp;
            save.Wishgate.HasCommittedReward = hasCommittedReward;
            save.Wishgate.CommittedReward = receipt;
        }

        private static WishgateRewardResult RewardFailure(
            WishgateRewardStatus status,
            string technicalCode) =>
            new WishgateRewardResult(status, technicalCode);

        private static WishgateRewardResult RewardDenied(RealmGemWishgatePolicyResult policy)
        {
            WishgateRewardStatus status;
            switch (policy.Outcome)
            {
                case RealmGemWishgatePolicyOutcome.MissingContext:
                    status = WishgateRewardStatus.MissingContext;
                    break;
                case RealmGemWishgatePolicyOutcome.CatalogUnavailable:
                    status = WishgateRewardStatus.CatalogUnavailable;
                    break;
                case RealmGemWishgatePolicyOutcome.IneligibleActor:
                    status = WishgateRewardStatus.IneligibleActor;
                    break;
                case RealmGemWishgatePolicyOutcome.UnauthorizedRealm:
                    status = WishgateRewardStatus.UnauthorizedRealm;
                    break;
                case RealmGemWishgatePolicyOutcome.DisallowedZone:
                    status = WishgateRewardStatus.DisallowedZone;
                    break;
                case RealmGemWishgatePolicyOutcome.EntitlementMissing:
                    status = WishgateRewardStatus.EntitlementMissing;
                    break;
                default:
                    status = WishgateRewardStatus.UnverifiableAuthority;
                    break;
            }

            return RewardFailure(status, policy.TechnicalCode);
        }

        private static RealmGemMutationResult Allowed(RealmGemWishgatePolicyResult policy) =>
            new RealmGemMutationResult(RealmGemMutationOutcome.Allowed, policy.TechnicalCode);

        private static RealmGemMutationResult InvalidState() =>
            new RealmGemMutationResult(RealmGemMutationOutcome.InvalidState, "AL-RGW-MUTATION-STATE");

        private static RealmGemMutationResult Denied(RealmGemWishgatePolicyResult policy) =>
            new RealmGemMutationResult(MapOutcome(policy.Outcome), policy.TechnicalCode);

        private static RealmGemMutationOutcome MapOutcome(RealmGemWishgatePolicyOutcome outcome)
        {
            switch (outcome)
            {
                case RealmGemWishgatePolicyOutcome.MissingContext: return RealmGemMutationOutcome.MissingContext;
                case RealmGemWishgatePolicyOutcome.CatalogUnavailable: return RealmGemMutationOutcome.CatalogUnavailable;
                case RealmGemWishgatePolicyOutcome.UnknownCatalogEntry: return RealmGemMutationOutcome.UnknownCatalogEntry;
                case RealmGemWishgatePolicyOutcome.IneligibleActor: return RealmGemMutationOutcome.IneligibleActor;
                case RealmGemWishgatePolicyOutcome.UnauthorizedRealm: return RealmGemMutationOutcome.UnauthorizedRealm;
                case RealmGemWishgatePolicyOutcome.DisallowedZone: return RealmGemMutationOutcome.DisallowedZone;
                case RealmGemWishgatePolicyOutcome.EntitlementMissing: return RealmGemMutationOutcome.EntitlementMissing;
                default: return RealmGemMutationOutcome.UnverifiableAuthority;
            }
        }
    }
}
