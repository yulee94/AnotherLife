using System;
using AL.Core.Interfaces;
using AL.Core.Relationships;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class PersonaService : IPersonaService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IRelationshipIdentityResolver _resolver;

        public PersonaService(ISaveGameService saveGameService)
            : this(saveGameService, RelationshipProductionResolver.Current)
        {
        }

        public PersonaService(
            ISaveGameService saveGameService,
            IRelationshipIdentityResolver resolver)
        {
            _saveGameService = saveGameService;
            _resolver = resolver;
        }

        public RelationshipPersonaSnapshot CaptureSnapshot()
        {
            return RelationshipConsumerSnapshot.Capture(
                _resolver,
                _saveGameService?.CurrentSave,
                ProfileMutationContainment.ProductionWriteActivationEnabled).Persona;
        }

        public int GetTraitValue(PersonaTrait trait)
        {
            RelationshipPersonaSnapshot snapshot = CaptureSnapshot();
            string id = TraitId(trait);
            int value;
            return id != null && snapshot.Values.TryGetValue(id, out value) ? value : 0;
        }

        [Obsolete("Compatibility mutation path. Use an owning revision-bound transaction for narrative consequences.")]
        public void AdjustTrait(PersonaTrait trait, int delta)
        {
            if (!ProfileMutationContainment.TryGetMutableSave(
                    _saveGameService,
                    ProfileMutationSurfaceIds.Persona,
                    out SaveGameData save) ||
                save.LordPersona == null)
            {
                return;
            }

            PersonaData persona = save.LordPersona;
            switch (trait)
            {
                case PersonaTrait.Warlord: persona.Warlord = checked(persona.Warlord + delta); break;
                case PersonaTrait.Diplomat: persona.Diplomat = checked(persona.Diplomat + delta); break;
                case PersonaTrait.Sage: persona.Sage = checked(persona.Sage + delta); break;
                case PersonaTrait.Rogue: persona.Rogue = checked(persona.Rogue + delta); break;
                default: return;
            }

            Debug.Log($"[Persona] {trait} adjusted by {delta}. Current: {GetTraitValue(trait)}");
            _saveGameService.Save();
        }

        [Obsolete("Ambiguous compatibility projection. Consume CaptureSnapshot and handle AllZero/Tie explicitly.")]
        public PersonaTrait GetDominantTrait()
        {
            RelationshipPersonaSnapshot snapshot = CaptureSnapshot();
            if (snapshot.Classification != RelationshipPersonaClassification.UniqueDominant)
            {
                return PersonaTrait.Sage;
            }

            switch (snapshot.DominantTraitIds[0])
            {
                case "warlord": return PersonaTrait.Warlord;
                case "diplomat": return PersonaTrait.Diplomat;
                case "rogue": return PersonaTrait.Rogue;
                default: return PersonaTrait.Sage;
            }
        }

        private static string TraitId(PersonaTrait trait)
        {
            switch (trait)
            {
                case PersonaTrait.Warlord: return "warlord";
                case PersonaTrait.Diplomat: return "diplomat";
                case PersonaTrait.Sage: return "sage";
                case PersonaTrait.Rogue: return "rogue";
                default: return null;
            }
        }
    }
}
