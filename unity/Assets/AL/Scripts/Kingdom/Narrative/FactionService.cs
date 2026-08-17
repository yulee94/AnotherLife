using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Core.Relationships;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class FactionService : IFactionService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IRelationshipIdentityResolver _resolver;

        public FactionService(ISaveGameService saveGameService)
            : this(saveGameService, RelationshipProductionResolver.Current)
        {
        }

        public FactionService(
            ISaveGameService saveGameService,
            IRelationshipIdentityResolver resolver)
        {
            _saveGameService = saveGameService;
            _resolver = resolver;
        }

        public RelationshipValueQuery QueryReputation(string factionId)
        {
            RelationshipConsumerSnapshot snapshot = Capture();
            return snapshot.QueryFaction(_resolver, factionId);
        }

        public int GetReputation(string factionId)
        {
            RelationshipValueQuery result = QueryReputation(factionId);
            return result.Status == RelationshipValueQueryStatus.Found
                ? checked((int)result.Value)
                : 0;
        }

        [Obsolete("Compatibility mutation path. Use a revision-bound relationship transaction for narrative consequences.")]
        public void AdjustReputation(string factionId, int delta)
        {
            RelationshipConsumerSnapshot captured = Capture();
            RelationshipMutationPlan plan = RelationshipPlanner.PlanFaction(
                _resolver,
                captured.Faction,
                factionId,
                delta,
                "legacy-faction-change",
                "legacy-faction-change");
            if (plan.Status != RelationshipPlanStatus.Prepared &&
                plan.Status != RelationshipPlanStatus.NoChange)
            {
                return;
            }
            if (plan.Status == RelationshipPlanStatus.NoChange) return;

            if (!ProfileMutationContainment.TryGetMutableSave(
                    _saveGameService,
                    ProfileMutationSurfaceIds.Faction,
                    out SaveGameData save) ||
                save.FactionReputations == null)
            {
                return;
            }

            List<FactionRepData> factions = save.FactionReputations;
            FactionRepData data = factions.FirstOrDefault(row => IsTarget(row, plan.CanonicalTargetId));
            if (data == null)
            {
                data = new FactionRepData { FactionId = plan.CanonicalTargetId };
                factions.Add(data);
            }

            data.FactionId = plan.CanonicalTargetId;
            data.Reputation = checked((int)plan.NewValue);
            Debug.Log($"[Faction] {plan.CanonicalTargetId} reputation changed by {plan.AppliedDelta}. New: {data.Reputation}");
            _saveGameService.Save();
        }

        public string GetFactionAffiliation(string factionId)
        {
            int reputation = GetReputation(factionId);
            if (reputation >= 500) return "Ally";
            if (reputation >= 100) return "Supporter";
            if (reputation <= -500) return "Enemy";
            if (reputation <= -100) return "Opponent";
            return "Neutral";
        }

        private RelationshipConsumerSnapshot Capture()
        {
            return RelationshipConsumerSnapshot.Capture(
                _resolver,
                _saveGameService?.CurrentSave,
                ProfileMutationContainment.ProductionWriteActivationEnabled);
        }

        private bool IsTarget(FactionRepData row, string canonicalId)
        {
            if (row == null) return false;
            RelationshipIdentityResolution resolution = _resolver.ResolveFaction(row.FactionId);
            return (resolution.Status == RelationshipResolutionStatus.Found ||
                    resolution.Status == RelationshipResolutionStatus.AliasResolved) &&
                   string.Equals(resolution.Identity.CanonicalId, canonicalId, StringComparison.Ordinal);
        }
    }
}
