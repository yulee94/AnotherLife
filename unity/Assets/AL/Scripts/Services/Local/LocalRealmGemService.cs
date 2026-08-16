using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmGems;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalRealmGemService : IRealmGemService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly Func<RealmGemWishgateCatalogSnapshot> _catalogProvider;
        private readonly IRealmGemWishgateAuthorityProvider _authorityProvider;
        private readonly Func<SaveGameData> _mutableSaveProvider;

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
            : this(saveGameService, catalogProvider, authorityProvider, null)
        {
        }

        internal LocalRealmGemService(
            ISaveGameService saveGameService,
            Func<RealmGemWishgateCatalogSnapshot> catalogProvider,
            IRealmGemWishgateAuthorityProvider authorityProvider,
            Func<SaveGameData> mutableSaveProvider)
        {
            _saveGameService = saveGameService;
            _catalogProvider = catalogProvider ?? (() => null);
            _authorityProvider = authorityProvider;
            _mutableSaveProvider = mutableSaveProvider;
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

        public void DropGem(string gemId)
        {
            DropGem(new RealmGemMutationRequest(gemId, string.Empty, string.Empty));
        }

        public RealmGemMutationResult DropGem(RealmGemMutationRequest request)
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

        public void ReturnGemHome(string gemId)
        {
            ReturnGemHome(new RealmGemMutationRequest(gemId, string.Empty, string.Empty));
        }

        public RealmGemMutationResult ReturnGemHome(RealmGemMutationRequest request)
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

        // These untyped Wishgate writers cannot prove actor, realm, location, or
        // entitlement authority and remain deliberately unavailable. The durable
        // transaction installs a typed consumer on top of EvaluateWishgate.
        public void MarkWishgateEarned(string reason) { }
        public void ChooseWishReward(string rewardId) { }

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
                LastRewardChosenTimestamp = source.LastRewardChosenTimestamp
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
