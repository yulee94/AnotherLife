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
            if (Persona == null) return;

            switch (trait)
            {
                case PersonaTrait.Warlord: Persona.Warlord += delta; break;
                case PersonaTrait.Diplomat: Persona.Diplomat += delta; break;
                case PersonaTrait.Sage: Persona.Sage += delta; break;
                case PersonaTrait.Rogue: Persona.Rogue += delta; break;
            }

            Debug.Log($"[Persona] {trait} adjusted by {delta}. Current: {GetTraitValue(trait)}");
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
