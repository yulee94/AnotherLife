using System;
using System.Collections.Generic;

namespace AL.RealmSelection
{
    public sealed class WishgateCatalogTransactionAuthority : IWishgateTransactionAuthority
    {
        public WishgateLookupStatus ResolveEarnReason(string earnReasonId)
        {
            if (string.IsNullOrWhiteSpace(earnReasonId))
            {
                return WishgateLookupStatus.Unknown;
            }

            return string.Equals(
                    earnReasonId,
                    WishgateEngineeringIds.EarnAllRealmGemSignatures,
                    StringComparison.Ordinal)
                ? WishgateLookupStatus.Found
                : WishgateLookupStatus.Unknown;
        }

        public WishgateLookupStatus ResolveReward(string rewardId)
        {
            // Wish emphases remain epilogue/cosmetic source and are not reward
            // IDs or balance authority. No owner-approved reward catalog exists.
            if (string.IsNullOrWhiteSpace(rewardId))
            {
                return WishgateLookupStatus.Unknown;
            }

            return WishgateLookupStatus.Unknown;
        }

        public WishgateDecisionStatus EvaluateEligibility(
            WishgateTransactionRequest request,
            RealmGemCatalogSnapshot realmGemCatalog,
            RealmGemCustodySnapshot custodySnapshot)
        {
            if (realmGemCatalog == null || custodySnapshot == null)
            {
                return WishgateDecisionStatus.Unavailable;
            }

            if (custodySnapshot.Status == RealmGemCustodySnapshotStatus.Unavailable)
            {
                return WishgateDecisionStatus.Unavailable;
            }

            if (custodySnapshot.Status != RealmGemCustodySnapshotStatus.Available ||
                realmGemCatalog.Entries.Count == 0)
            {
                return WishgateDecisionStatus.Rejected;
            }

            var atHome = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < custodySnapshot.Records.Count; i++)
            {
                RealmGemCustodyRecord record = custodySnapshot.Records[i];
                if (record == null ||
                    !record.IsSupported ||
                    record.State != RealmGemCustodyState.AtHome)
                {
                    continue;
                }

                atHome.Add(record.GemId);
            }

            for (int i = 0; i < realmGemCatalog.Entries.Count; i++)
            {
                if (!atHome.Contains(realmGemCatalog.Entries[i].Id))
                {
                    return WishgateDecisionStatus.Rejected;
                }
            }

            // Overlay ladder requires winning the Accordant/center-isle FFA
            // before the wish. That evidence is not yet a writable save
            // authority, so production eligibility stays fail-closed.
            return WishgateDecisionStatus.Rejected;
        }

        public WishgateDecisionStatus Authorize(
            WishgateTransactionRequest request,
            WishgateEntitlementState currentEntitlement)
        {
            if (request == null)
            {
                return WishgateDecisionStatus.Unavailable;
            }

            return string.Equals(
                    request.ActorId,
                    WishgateEngineeringIds.ActorId,
                    StringComparison.Ordinal)
                ? WishgateDecisionStatus.Accepted
                : WishgateDecisionStatus.Rejected;
        }
    }
}
