using System;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Relationships;

namespace AL.Services.Relationships
{
    public sealed class RelationshipLegacyCompatibilityAdapter :
        IReputationService,
        IFactionService,
        IPersonaService
    {
        private readonly RelationshipDurableService _service;

        public RelationshipLegacyCompatibilityAdapter(RelationshipDurableService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public float GetAffinity(string npcId)
        {
            return (float)_service.QueryNpcAffinity(npcId).Value;
        }

        public void ChangeAffinity(string npcId, float delta)
        {
            _service.Commit(
                RelationshipMutationRequest.Affinity(
                    npcId,
                    delta,
                    NextId("corr-legacy-affinity"),
                    NextId("op-legacy-affinity"),
                    "al_relationship_legacy_wrapper"));
        }

        public string GetAffinityRank(string npcId)
        {
            float affinity = GetAffinity(npcId);
            if (affinity >= 80f)
            {
                return "Exalted";
            }

            if (affinity >= 50f)
            {
                return "Friendly";
            }

            if (affinity >= 0f)
            {
                return "Neutral";
            }

            if (affinity >= -50f)
            {
                return "Hostile";
            }

            return "Nemesis";
        }

        public int GetReputation(string factionId)
        {
            return (int)_service.QueryFactionReputation(factionId).Value;
        }

        public void AdjustReputation(string factionId, int delta)
        {
            _service.Commit(
                RelationshipMutationRequest.Faction(
                    factionId,
                    delta,
                    NextId("corr-legacy-faction"),
                    NextId("op-legacy-faction"),
                    "al_relationship_legacy_wrapper"));
        }

        public string GetFactionAffiliation(string factionId)
        {
            int reputation = GetReputation(factionId);
            if (reputation >= 500)
            {
                return "Ally";
            }

            if (reputation >= 100)
            {
                return "Supporter";
            }

            if (reputation <= -500)
            {
                return "Enemy";
            }

            if (reputation <= -100)
            {
                return "Opponent";
            }

            return "Neutral";
        }

        public int GetTraitValue(PersonaTrait trait)
        {
            RelationshipSnapshot snapshot = _service.Snapshot();
            if (!snapshot.PersonaDomain.Values.IsPresent)
            {
                return 0;
            }

            try
            {
                return snapshot.PersonaDomain.Values.Get(trait);
            }
            catch (ArgumentOutOfRangeException)
            {
                return 0;
            }
        }

        public void AdjustTrait(PersonaTrait trait, int delta)
        {
            _service.Commit(
                RelationshipMutationRequest.Persona(
                    trait,
                    delta,
                    NextId("corr-legacy-persona"),
                    NextId("op-legacy-persona"),
                    "al_relationship_legacy_wrapper"));
        }

        public PersonaTrait GetDominantTrait()
        {
            PersonaClassificationResult classification = _service.ClassifyPersona();
            if (classification != null &&
                classification.Status == PersonaClassificationStatus.UniqueDominant &&
                classification.DominantTrait.HasValue)
            {
                return classification.DominantTrait.Value;
            }

            if (classification == null ||
                classification.Status == PersonaClassificationStatus.Unavailable ||
                classification.Status == PersonaClassificationStatus.Malformed)
            {
                return PersonaTrait.Sage;
            }

            return PersonaTrait.Warlord;
        }

        private static string NextId(string prefix)
        {
            return prefix + "/" + Guid.NewGuid().ToString("N");
        }
    }
}
