using System;
using System.Linq;

namespace AL.Strongholds
{
    /// <summary>Pure fixture planner. Public observations are data, not trust. Never issues TerritoryCaptureAuthorization.</summary>
    public sealed class StrongholdPlanner
    {
        private readonly StrongholdCatalog catalog;
        public StrongholdPlanner(StrongholdCatalog catalog) { this.catalog = catalog; }
        public bool CanApplyProduction => false;
        public StrongholdUpgradeQuote QuoteUpgrade(StrongholdState state)
        {
            if (catalog == null || state == null || state.CatalogHash != catalog.Hash || state.Level < 1 || state.Level >= 10 ||
                state.GateBreached || state.Attempt != null || !catalog.OwnerRareResources.ContainsKey(state.Owner) ||
                !catalog.Territories.TryGetValue(state.TerritoryId, out var profile) || profile != state.ProfileId) return null;
            return new StrongholdUpgradeQuote(state, catalog.Levels[state.Level],
                state.Level == 9 ? catalog.OwnerRareCostProfileId : "",
                state.Level == 9 ? catalog.OwnerRareResources[state.Owner] : "");
        }

        public StrongholdPlan Plan(StrongholdState state, StrongholdRequest request, StrongholdObservation observation)
        {
            if (catalog == null || state == null || request == null || observation == null)
                return Reject("MissingInput", StrongholdPlanStatus.Unavailable);
            if (observation.Source != StrongholdObservationSource.FixtureOnly || !observation.ClockAvailable)
                return Reject("AuthorityUnavailable", StrongholdPlanStatus.Unavailable);
            if (!StrongholdCatalog.IsId(request.OperationId) || !Enum.IsDefined(typeof(StrongholdOperation), request.Operation) ||
                request.ActorRealm == null || !catalog.OwnerRareResources.ContainsKey(request.ActorRealm) ||
                observation.RequestFingerprint != request.Fingerprint || request.TerritoryId != state.TerritoryId ||
                request.InstanceId != state.InstanceId || request.ExpectedCatalogHash != catalog.Hash || state.CatalogHash != catalog.Hash ||
                !catalog.Territories.TryGetValue(state.TerritoryId, out var profile) || profile != state.ProfileId)
                return Reject("InvalidBinding");
            var replay = state.Receipts.SingleOrDefault(r => r.OperationId == request.OperationId);
            if (replay != null)
                return replay.Fingerprint == request.Fingerprint
                    ? new StrongholdPlan(StrongholdPlanStatus.Replayed, "ExactReplay", receipt: replay)
                    : Reject("OperationConflict", StrongholdPlanStatus.Conflict);
            if (request.ExpectedStateHash != state.Hash) return Reject("StaleState");
            if (observation.TrustedTimeMilliseconds < state.LastTrustedTime || observation.TrustedTimeMilliseconds < 0 ||
                observation.TrustedTimeMilliseconds > long.MaxValue - catalog.TakeoverDurationMilliseconds ||
                state.Revision == long.MaxValue || state.Generation == long.MaxValue || state.OwnershipEpoch == long.MaxValue ||
                state.Receipts.Count >= 1024)
                return Reject("ClockOrCapacityUnavailable", StrongholdPlanStatus.Unavailable);
            var definition = catalog.Strongholds.Single(d => d.Id == state.ProfileId);
            switch (request.Operation)
            {
                case StrongholdOperation.Upgrade:
                    var quote = QuoteUpgrade(state);
                    if (quote == null || request.Quote == null || quote.Fingerprint != request.Quote.Fingerprint ||
                        request.TargetId != definition.UpgradeNpcId || request.ActorRealm != state.Owner ||
                        !observation.InteractionValid || !observation.UpgradePermission || !observation.FundingAvailable)
                        return Reject("UpgradeIneligible");
                    return Prepare(state, request, observation, "UpgradePreparedFixtureOnly", level: quote.TargetLevel,
                        gate: false, commandDefeated: false, newGeneration: true);
                case StrongholdOperation.BreachGate:
                    if (request.TargetId != definition.GateId || !observation.CombatResultValid ||
                        request.ActorRealm == state.Owner || state.GateBreached) return Reject("InvalidBreach");
                    return Prepare(state, request, observation, "GateBreached", gate: true);
                case StrongholdOperation.DefeatCommandNpc:
                    if (request.TargetId != definition.CommandNpcId || !observation.CombatResultValid || !state.GateBreached ||
                        !catalog.Levels[state.Level - 1].CommandNpcRequired || request.ActorRealm == state.Owner ||
                        state.CommandNpcDefeated) return Reject("InvalidCommandDefeat");
                    return Prepare(state, request, observation, "CommandNpcDefeated", commandDefeated: true);
                case StrongholdOperation.ResealGate:
                    if (request.TargetId != definition.GateId || !observation.CombatResultValid ||
                        request.ActorRealm != state.Owner || !state.GateBreached) return Reject("InvalidReseal");
                    return Prepare(state, request, observation, "GateResealed", gate: false, commandDefeated: false, newGeneration: true);
                case StrongholdOperation.InteractStatue:
                    if (request.TargetId != definition.StatueId || !observation.InteractionValid) return Reject("StatueIneligible");
                    if (state.Attempt != null)
                        return request.ActorRealm == state.Attempt.Realm
                            ? Prepare(state, request, observation, "DeadlineUnchanged")
                            : Prepare(state, request, observation, "TakeoverCancelled", replaceAttempt: true);
                    if (!state.GateBreached || (catalog.Levels[state.Level - 1].CommandNpcRequired && !state.CommandNpcDefeated) ||
                        request.ActorRealm == state.Owner) return Reject("StatueIneligible");
                    var attempt = new StrongholdAttempt(request.OperationId, request.ActorRealm, observation.TrustedTimeMilliseconds,
                        observation.TrustedTimeMilliseconds + catalog.TakeoverDurationMilliseconds, state.OwnershipEpoch, state.Generation);
                    return Prepare(state, request, observation, "TakeoverStarted", attempt: attempt, replaceAttempt: true);
                case StrongholdOperation.CompleteTakeover:
                    if (request.TargetId != definition.StatueId || state.Attempt == null || request.AttemptId != state.Attempt.Id ||
                        request.ActorRealm != state.Attempt.Realm || state.Attempt.OwnershipEpoch != state.OwnershipEpoch ||
                        state.Attempt.Generation != state.Generation || !state.GateBreached ||
                        (catalog.Levels[state.Level - 1].CommandNpcRequired && !state.CommandNpcDefeated) ||
                        observation.TrustedTimeMilliseconds < state.Attempt.Deadline) return Reject("TakeoverNotReady");
                    return Prepare(state, request, observation, "Captured", gate: false, commandDefeated: false, level: 1,
                        owner: state.Attempt.Realm, replaceAttempt: true, newGeneration: true, newOwner: true);
                default: return Reject("UnsupportedOperation");
            }
        }

        private static StrongholdPlan Prepare(StrongholdState state, StrongholdRequest request, StrongholdObservation observation,
            string outcome, bool? gate = null, bool? commandDefeated = null, int? level = null,
            string owner = null, StrongholdAttempt attempt = null, bool replaceAttempt = false,
            bool newGeneration = false, bool newOwner = false)
        {
            var receipt = new StrongholdReceipt(request, state.Revision + 1, outcome);
            var next = new StrongholdState(state.TerritoryId, state.InstanceId, state.ProfileId, state.CatalogHash,
                owner ?? state.Owner, level ?? state.Level, receipt.Revision, state.OwnershipEpoch + (newOwner ? 1 : 0),
                state.Generation + (newGeneration ? 1 : 0), gate ?? state.GateBreached, commandDefeated ?? state.CommandNpcDefeated,
                replaceAttempt ? attempt : state.Attempt, observation.TrustedTimeMilliseconds, state.Receipts.Concat(new[] { receipt }));
            return new StrongholdPlan(StrongholdPlanStatus.Prepared, outcome, next, receipt);
        }

        private static StrongholdPlan Reject(string reason, StrongholdPlanStatus status = StrongholdPlanStatus.Rejected)
            => new StrongholdPlan(status, reason);

        public StrongholdState Fresh(string territoryId, string instanceId, string owner)
        {
            if (catalog == null || !StrongholdCatalog.IsId(territoryId) || !StrongholdCatalog.IsId(instanceId) ||
                owner == null || !catalog.OwnerRareResources.ContainsKey(owner) ||
                !catalog.Territories.TryGetValue(territoryId, out var profile) || profile == null) return null;
            return new StrongholdState(territoryId, instanceId, profile, catalog.Hash, owner);
        }
    }
}
