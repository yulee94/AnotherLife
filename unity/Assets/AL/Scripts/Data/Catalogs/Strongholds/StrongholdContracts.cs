using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Strongholds
{
    public enum StrongholdOperation { BreachGate, DefeatCommandNpc, ResealGate, InteractStatue, CompleteTakeover, Upgrade }
    public enum StrongholdPlanStatus { Prepared, Replayed, Rejected, Conflict, Unavailable }
    public enum StrongholdObservationSource { Untrusted, FixtureOnly }

    /// <summary>Immutable simulation input, never a server authentication token.</summary>
    public sealed class StrongholdObservation
    {
        public StrongholdObservation(string requestFingerprint, long trustedTimeMilliseconds,
            StrongholdObservationSource source = StrongholdObservationSource.Untrusted,
            bool clockAvailable = false, bool interactionValid = false, bool combatResultValid = false,
            bool upgradePermission = false, bool fundingAvailable = false)
        {
            RequestFingerprint = requestFingerprint; TrustedTimeMilliseconds = trustedTimeMilliseconds;
            Source = source; ClockAvailable = clockAvailable; InteractionValid = interactionValid;
            CombatResultValid = combatResultValid; UpgradePermission = upgradePermission; FundingAvailable = fundingAvailable;
        }
        public string RequestFingerprint { get; }
        public long TrustedTimeMilliseconds { get; }
        public StrongholdObservationSource Source { get; }
        public bool ClockAvailable { get; }
        // Fixture assertion covers authenticated live/direct, exact target, range, LOS, control, nonce and realm eligibility.
        public bool InteractionValid { get; }
        public bool CombatResultValid { get; }
        public bool UpgradePermission { get; }
        public bool FundingAvailable { get; }
    }

    public sealed class StrongholdRequest
    {
        public StrongholdRequest(string operationId, StrongholdOperation operation, string territoryId, string instanceId,
            string expectedCatalogHash, string expectedStateHash, string actorRealm, string targetId,
            string attemptId = "", StrongholdUpgradeQuote quote = null)
        {
            OperationId = operationId; Operation = operation; TerritoryId = territoryId; InstanceId = instanceId;
            ExpectedCatalogHash = expectedCatalogHash; ExpectedStateHash = expectedStateHash; ActorRealm = actorRealm;
            TargetId = targetId; AttemptId = attemptId; Quote = quote;
            Fingerprint = StrongholdHash.Of(operationId, operation, territoryId, instanceId, expectedCatalogHash,
                expectedStateHash, actorRealm, targetId, attemptId, quote?.Fingerprint);
        }
        public string OperationId { get; }
        public StrongholdOperation Operation { get; }
        public string TerritoryId { get; }
        public string InstanceId { get; }
        public string ExpectedCatalogHash { get; }
        public string ExpectedStateHash { get; }
        public string ActorRealm { get; }
        public string TargetId { get; }
        public string AttemptId { get; }
        public StrongholdUpgradeQuote Quote { get; }
        public string Fingerprint { get; }
    }

    public sealed class StrongholdUpgradeQuote
    {
        internal StrongholdUpgradeQuote(StrongholdState state, StrongholdLevel target, string costProfile, string rareResource)
        {
            StateHash = state.Hash; Owner = state.Owner; TargetLevel = target.Level;
            CostProfileId = target.UpgradeCostProfileId; OwnerRareCostProfileId = costProfile; RareResource = rareResource;
            Fingerprint = StrongholdHash.Of(StateHash, Owner, TargetLevel, CostProfileId, OwnerRareCostProfileId, RareResource);
        }
        public string StateHash { get; }
        public string Owner { get; }
        public int TargetLevel { get; }
        public string CostProfileId { get; }
        public string OwnerRareCostProfileId { get; }
        public string RareResource { get; }
        public bool NumericCostResolved => false;
        public bool CanDebit => false;
        public string Fingerprint { get; }
    }

    public sealed class StrongholdAttempt
    {
        internal StrongholdAttempt(string id, string realm, long startedAt, long deadline, long ownershipEpoch, long generation)
        { Id = id; Realm = realm; StartedAt = startedAt; Deadline = deadline; OwnershipEpoch = ownershipEpoch; Generation = generation; }
        public string Id { get; }
        public string Realm { get; }
        public long StartedAt { get; }
        public long Deadline { get; }
        public long OwnershipEpoch { get; }
        public long Generation { get; }
    }

    public sealed class StrongholdReceipt
    {
        internal StrongholdReceipt(StrongholdRequest request, long revision, string outcome)
        { OperationId = request.OperationId; Fingerprint = request.Fingerprint; Revision = revision; Outcome = outcome; }
        public string OperationId { get; }
        public string Fingerprint { get; }
        public long Revision { get; }
        public string Outcome { get; }
    }

    /// <summary>Candidate snapshot only. No persistence adapter or runtime consumer is supplied.</summary>
    public sealed class StrongholdState
    {
        internal StrongholdState(string territoryId, string instanceId, string profileId, string catalogHash, string owner,
            int level = 1, long revision = 0, long ownershipEpoch = 0, long generation = 1, bool gateBreached = false,
            bool commandNpcDefeated = false, StrongholdAttempt attempt = null, long lastTrustedTime = 0,
            IEnumerable<StrongholdReceipt> receipts = null)
        {
            TerritoryId = territoryId; InstanceId = instanceId; ProfileId = profileId; CatalogHash = catalogHash; Owner = owner;
            Level = level; Revision = revision; OwnershipEpoch = ownershipEpoch; Generation = generation;
            GateBreached = gateBreached; CommandNpcDefeated = commandNpcDefeated; Attempt = attempt; LastTrustedTime = lastTrustedTime;
            Receipts = Array.AsReadOnly((receipts ?? Array.Empty<StrongholdReceipt>()).ToArray());
            Hash = StrongholdHash.Of(territoryId, instanceId, profileId, catalogHash, owner, level, revision, ownershipEpoch,
                generation, gateBreached, commandNpcDefeated, attempt?.Id, attempt?.Realm, attempt?.StartedAt, attempt?.Deadline,
                attempt?.OwnershipEpoch, attempt?.Generation, lastTrustedTime, StrongholdHash.Of(Receipts.Select(r => r.Fingerprint).Cast<object>().ToArray()));
        }
        public string TerritoryId { get; }
        public string InstanceId { get; }
        public string ProfileId { get; }
        public string CatalogHash { get; }
        public string Owner { get; }
        public int Level { get; }
        public long Revision { get; }
        public long OwnershipEpoch { get; }
        // One fencing generation covers gate, NPC spawn, statue and guard/upgrade work in this inert model.
        public long Generation { get; }
        public bool GateBreached { get; }
        public bool CommandNpcDefeated { get; }
        public StrongholdAttempt Attempt { get; }
        public long LastTrustedTime { get; }
        public IReadOnlyList<StrongholdReceipt> Receipts { get; }
        public string Hash { get; }
    }

    public sealed class StrongholdPlan
    {
        internal StrongholdPlan(StrongholdPlanStatus status, string reason, StrongholdState candidate = null, StrongholdReceipt receipt = null)
        { Status = status; Reason = reason; Candidate = candidate; Receipt = receipt; }
        public StrongholdPlanStatus Status { get; }
        public string Reason { get; }
        public StrongholdState Candidate { get; }
        public StrongholdReceipt Receipt { get; }
        public bool CanApplyProduction => false;
    }

    internal static class StrongholdHash
    {
        internal static string Of(params object[] values)
        {
            var text = new StringBuilder();
            foreach (var value in values)
            {
                var token = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                text.Append(token.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(token);
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
    }
}
