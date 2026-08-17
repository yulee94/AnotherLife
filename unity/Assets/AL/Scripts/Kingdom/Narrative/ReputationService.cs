using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Core.Relationships;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class ReputationService : IReputationService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IRelationshipIdentityResolver _resolver;

        public ReputationService(ISaveGameService saveGameService)
            : this(saveGameService, RelationshipProductionResolver.Current)
        {
        }

        public ReputationService(
            ISaveGameService saveGameService,
            IRelationshipIdentityResolver resolver)
        {
            _saveGameService = saveGameService;
            _resolver = resolver;
        }

        public RelationshipValueQuery QueryAffinity(string npcId)
        {
            RelationshipConsumerSnapshot snapshot = Capture();
            return snapshot.QueryAffinity(_resolver, npcId);
        }

        public float GetAffinity(string npcId)
        {
            RelationshipValueQuery result = QueryAffinity(npcId);
            return result.Status == RelationshipValueQueryStatus.Found
                ? (float)result.Value
                : 0f;
        }

        [Obsolete("Compatibility mutation path. Use a revision-bound relationship transaction for narrative consequences.")]
        public void ChangeAffinity(string npcId, float delta)
        {
            RelationshipConsumerSnapshot captured = Capture();
            RelationshipMutationPlan plan = RelationshipPlanner.PlanAffinity(
                _resolver,
                captured.Affinity,
                npcId,
                delta,
                "legacy-reputation-change",
                "legacy-reputation-change");
            if (plan.Status != RelationshipPlanStatus.Prepared &&
                plan.Status != RelationshipPlanStatus.PreparedClamped &&
                plan.Status != RelationshipPlanStatus.NoChange)
            {
                return;
            }
            if (plan.Status == RelationshipPlanStatus.NoChange) return;

            if (!ProfileMutationContainment.TryGetMutableSave(
                    _saveGameService,
                    ProfileMutationSurfaceIds.Reputation,
                    out SaveGameData save) ||
                save.Reputation == null)
            {
                return;
            }

            List<NpcAffinityData> reputation = save.Reputation;
            NpcAffinityData data = reputation.FirstOrDefault(row => IsTarget(row, plan.CanonicalTargetId));
            if (data == null)
            {
                data = new NpcAffinityData { NpcId = plan.CanonicalTargetId };
                reputation.Add(data);
            }

            data.NpcId = plan.CanonicalTargetId;
            data.Affinity = (float)plan.NewValue;
            Debug.Log($"[Reputation] {plan.CanonicalTargetId} Affinity changed by {plan.AppliedDelta}. New: {data.Affinity}");
            _saveGameService.Save();
        }

        public string GetAffinityRank(string npcId)
        {
            float affinity = GetAffinity(npcId);
            if (affinity >= 80f) return "Exalted";
            if (affinity >= 50f) return "Friendly";
            if (affinity >= 0f) return "Neutral";
            if (affinity >= -50f) return "Hostile";
            return "Nemesis";
        }

        private RelationshipConsumerSnapshot Capture()
        {
            return RelationshipConsumerSnapshot.Capture(
                _resolver,
                _saveGameService?.CurrentSave,
                ProfileMutationContainment.ProductionWriteActivationEnabled);
        }

        private bool IsTarget(NpcAffinityData row, string canonicalId)
        {
            if (row == null) return false;
            RelationshipIdentityResolution resolution = _resolver.ResolveNpc(row.NpcId);
            return (resolution.Status == RelationshipResolutionStatus.Found ||
                    resolution.Status == RelationshipResolutionStatus.AliasResolved) &&
                   string.Equals(resolution.Identity.CanonicalId, canonicalId, StringComparison.Ordinal);
        }
    }
}
