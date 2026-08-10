using System.Collections.Generic;
using System.Linq;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class PersonaService : IPersonaService
    {
        private readonly ISaveGameService _saveGameService;

        public PersonaService(ISaveGameService saveGameService)
        {
            _saveGameService = saveGameService;
        }

        private PersonaData Persona => _saveGameService.CurrentSave?.LordPersona;

        public int GetTraitValue(PersonaTrait trait)
        {
            if (Persona == null) return 0;
            return trait switch
            {
                PersonaTrait.Warlord => Persona.Warlord,
                PersonaTrait.Diplomat => Persona.Diplomat,
                PersonaTrait.Sage => Persona.Sage,
                PersonaTrait.Rogue => Persona.Rogue,
                _ => 0
            };
        }

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
                case PersonaTrait.Warlord: persona.Warlord += delta; break;
                case PersonaTrait.Diplomat: persona.Diplomat += delta; break;
                case PersonaTrait.Sage: persona.Sage += delta; break;
                case PersonaTrait.Rogue: persona.Rogue += delta; break;
            }

            int current = trait switch
            {
                PersonaTrait.Warlord => persona.Warlord,
                PersonaTrait.Diplomat => persona.Diplomat,
                PersonaTrait.Sage => persona.Sage,
                PersonaTrait.Rogue => persona.Rogue,
                _ => 0
            };
            Debug.Log($"[Persona] {trait} adjusted by {delta}. Current: {current}");
            _saveGameService.Save();
        }

        public PersonaTrait GetDominantTrait()
        {
            if (Persona == null) return PersonaTrait.Sage;

            var scores = new Dictionary<PersonaTrait, int>
            {
                { PersonaTrait.Warlord, Persona.Warlord },
                { PersonaTrait.Diplomat, Persona.Diplomat },
                { PersonaTrait.Sage, Persona.Sage },
                { PersonaTrait.Rogue, Persona.Rogue }
            };

            return scores.OrderByDescending(x => x.Value).First().Key;
        }
    }
}
