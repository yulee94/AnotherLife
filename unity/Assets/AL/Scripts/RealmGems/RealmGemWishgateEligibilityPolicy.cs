using System;
using AL.Core;

namespace AL.RealmGems
{
    public enum RealmGemWishgatePolicyOutcome
    {
        Allowed,
        MissingContext,
        CatalogUnavailable,
        UnknownCatalogEntry,
        IneligibleActor,
        UnauthorizedRealm,
        DisallowedZone,
        EntitlementMissing,
        UnverifiableAuthority
    }

    public enum RealmGemMutationOutcome
    {
        Allowed,
        MissingContext,
        CatalogUnavailable,
        UnknownCatalogEntry,
        IneligibleActor,
        UnauthorizedRealm,
        DisallowedZone,
        EntitlementMissing,
        UnverifiableAuthority,
        InvalidState
    }

    public sealed class RealmGemWishgatePolicyResult
    {
        internal RealmGemWishgatePolicyResult(
            RealmGemWishgatePolicyOutcome outcome,
            string technicalCode,
            RealmGemWishgateAuthoritySnapshot authority)
        {
            Outcome = outcome;
            TechnicalCode = technicalCode ?? string.Empty;
            Authority = authority;
        }

        public RealmGemWishgatePolicyOutcome Outcome { get; }
        public string TechnicalCode { get; }
        public RealmGemWishgateAuthoritySnapshot Authority { get; }
        public bool IsAllowed => Outcome == RealmGemWishgatePolicyOutcome.Allowed;
    }

    public sealed class RealmGemMutationResult
    {
        internal RealmGemMutationResult(RealmGemMutationOutcome outcome, string technicalCode)
        {
            Outcome = outcome;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public RealmGemMutationOutcome Outcome { get; }
        public string TechnicalCode { get; }
        public bool IsAllowed => Outcome == RealmGemMutationOutcome.Allowed;
    }

    public sealed class RealmGemMutationRequest
    {
        public RealmGemMutationRequest(string gemId, string actorId, string zoneId)
        {
            GemId = gemId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
        }

        public string GemId { get; }
        public string ActorId { get; }
        public string ZoneId { get; }
    }

    public sealed class WishgateUseRequest
    {
        public WishgateUseRequest(string actorId, string zoneId)
        {
            ActorId = actorId ?? string.Empty;
            ZoneId = zoneId ?? string.Empty;
        }

        public string ActorId { get; }
        public string ZoneId { get; }
    }

    /// <summary>
    /// A server-authored, point-in-time decision source. Callers provide only actor
    /// and zone claims; implementations resolve and verify the authoritative facts.
    /// </summary>
    public interface IRealmGemWishgateAuthorityProvider
    {
        RealmGemWishgateAuthoritySnapshot Resolve(string actorId, string zoneId);
    }

    public sealed class RealmGemWishgateAuthoritySnapshot
    {
        public RealmGemWishgateAuthoritySnapshot(
            string authorityId,
            int authorityVersion,
            string actorId,
            bool actorEligible,
            RealmId actorRealm,
            string zoneId,
            RealmId controllingRealm,
            bool isNeutralZone,
            bool entitlementEligible,
            int verifiedGemCount)
        {
            AuthorityId = authorityId ?? string.Empty;
            AuthorityVersion = authorityVersion;
            ActorId = actorId ?? string.Empty;
            ActorEligible = actorEligible;
            ActorRealm = actorRealm;
            ZoneId = zoneId ?? string.Empty;
            ControllingRealm = controllingRealm;
            IsNeutralZone = isNeutralZone;
            EntitlementEligible = entitlementEligible;
            VerifiedGemCount = verifiedGemCount;
        }

        public string AuthorityId { get; }
        public int AuthorityVersion { get; }
        public string ActorId { get; }
        public bool ActorEligible { get; }
        public RealmId ActorRealm { get; }
        public string ZoneId { get; }
        public RealmId ControllingRealm { get; }
        public bool IsNeutralZone { get; }
        public bool EntitlementEligible { get; }
        public int VerifiedGemCount { get; }
    }

    /// <summary>
    /// Single fail-closed policy boundary shared by every Realm Gem mutation and
    /// Wishgate consumer. It resolves authority during the operation; a prior
    /// check cannot be reused to authorize a later mutation.
    /// </summary>
    public static class RealmGemWishgateEligibilityPolicy
    {
        public static RealmGemWishgatePolicyResult EvaluateRealmGem(
            RealmGemWishgateCatalogSnapshot catalog,
            IRealmGemWishgateAuthorityProvider authorityProvider,
            RealmGemMutationRequest request)
        {
            if (request == null ||
                !IsIdentifier(request.GemId) ||
                !IsIdentifier(request.ActorId) ||
                !IsIdentifier(request.ZoneId))
            {
                return Deny(RealmGemWishgatePolicyOutcome.MissingContext, "AL-RGW-POLICY-CONTEXT");
            }

            if (catalog == null || !catalog.CustodyAuthorityAvailable)
                return Deny(RealmGemWishgatePolicyOutcome.CatalogUnavailable, "AL-RGW-POLICY-CATALOG");
            if (!catalog.TryGetRealmGem(request.GemId, out RealmGemCatalogEntry entry) || entry == null)
                return Deny(RealmGemWishgatePolicyOutcome.UnknownCatalogEntry, "AL-RGW-POLICY-ENTRY");

            RealmGemWishgateAuthoritySnapshot authority = Resolve(authorityProvider, request.ActorId, request.ZoneId);
            RealmGemWishgatePolicyResult common = ValidateCommon(catalog, authority, request.ActorId, request.ZoneId);
            if (!common.IsAllowed) return common;
            if (!authority.ActorEligible)
                return Deny(RealmGemWishgatePolicyOutcome.IneligibleActor, "AL-RGW-POLICY-ACTOR", authority);
            if (authority.ActorRealm == RealmId.None ||
                authority.ControllingRealm == RealmId.None ||
                authority.ActorRealm != authority.ControllingRealm)
            {
                return Deny(RealmGemWishgatePolicyOutcome.UnauthorizedRealm, "AL-RGW-POLICY-REALM", authority);
            }
            if (authority.IsNeutralZone)
                return Deny(RealmGemWishgatePolicyOutcome.DisallowedZone, "AL-RGW-POLICY-NEUTRAL", authority);

            return Allow(authority);
        }

        public static RealmGemWishgatePolicyResult EvaluateWishgate(
            RealmGemWishgateCatalogSnapshot catalog,
            IRealmGemWishgateAuthorityProvider authorityProvider,
            WishgateUseRequest request)
        {
            if (request == null || !IsIdentifier(request.ActorId) || !IsIdentifier(request.ZoneId))
                return Deny(RealmGemWishgatePolicyOutcome.MissingContext, "AL-RGW-POLICY-CONTEXT");
            if (catalog == null ||
                !catalog.CustodyAuthorityAvailable ||
                catalog.Wishgate == null ||
                !catalog.Wishgate.EligibilityAuthorityAvailable)
                return Deny(RealmGemWishgatePolicyOutcome.CatalogUnavailable, "AL-RGW-POLICY-CATALOG");

            RealmGemWishgateAuthoritySnapshot authority = Resolve(authorityProvider, request.ActorId, request.ZoneId);
            RealmGemWishgatePolicyResult common = ValidateCommon(catalog, authority, request.ActorId, request.ZoneId);
            if (!common.IsAllowed) return common;
            if (!authority.ActorEligible || authority.ActorRealm == RealmId.None)
                return Deny(RealmGemWishgatePolicyOutcome.IneligibleActor, "AL-RGW-POLICY-ACTOR", authority);
            if (!authority.IsNeutralZone ||
                !string.Equals(request.ZoneId, catalog.Wishgate.EntryZoneId, StringComparison.Ordinal))
            {
                return Deny(RealmGemWishgatePolicyOutcome.DisallowedZone, "AL-RGW-POLICY-WISHGATE-ZONE", authority);
            }
            if (!authority.EntitlementEligible ||
                authority.VerifiedGemCount != catalog.Wishgate.RequiredGemCount)
            {
                return Deny(RealmGemWishgatePolicyOutcome.EntitlementMissing, "AL-RGW-POLICY-ENTITLEMENT", authority);
            }

            return Allow(authority);
        }

        private static RealmGemWishgatePolicyResult ValidateCommon(
            RealmGemWishgateCatalogSnapshot catalog,
            RealmGemWishgateAuthoritySnapshot authority,
            string actorId,
            string zoneId)
        {
            if (authority == null ||
                !string.Equals(authority.AuthorityId, catalog.AuthorityId, StringComparison.Ordinal) ||
                authority.AuthorityVersion != catalog.AuthorityVersion ||
                !IsIdentifier(authority.ActorId) ||
                !IsIdentifier(authority.ZoneId) ||
                !string.Equals(authority.ActorId, actorId, StringComparison.Ordinal) ||
                !string.Equals(authority.ZoneId, zoneId, StringComparison.Ordinal) ||
                authority.VerifiedGemCount < 0)
            {
                return Deny(RealmGemWishgatePolicyOutcome.UnverifiableAuthority, "AL-RGW-POLICY-AUTHORITY");
            }

            return Allow(authority);
        }

        private static RealmGemWishgateAuthoritySnapshot Resolve(
            IRealmGemWishgateAuthorityProvider provider,
            string actorId,
            string zoneId)
        {
            if (provider == null) return null;
            try { return provider.Resolve(actorId, zoneId); }
            catch (Exception) { return null; }
        }

        private static bool IsIdentifier(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Length <= 128 &&
            string.Equals(value, value.Trim(), StringComparison.Ordinal);

        private static RealmGemWishgatePolicyResult Allow(RealmGemWishgateAuthoritySnapshot authority) =>
            new RealmGemWishgatePolicyResult(RealmGemWishgatePolicyOutcome.Allowed, "AL-RGW-POLICY-ALLOWED", authority);

        private static RealmGemWishgatePolicyResult Deny(
            RealmGemWishgatePolicyOutcome outcome,
            string code,
            RealmGemWishgateAuthoritySnapshot authority = null) =>
            new RealmGemWishgatePolicyResult(outcome, code, authority);
    }
}
