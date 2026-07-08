namespace AL.Core.Interfaces
{
    public enum PersonaTrait
    {
        Warlord,
        Diplomat,
        Sage,
        Rogue
    }

    public interface IPersonaService
    {
        int GetTraitValue(PersonaTrait trait);
        void AdjustTrait(PersonaTrait trait, int delta);
        PersonaTrait GetDominantTrait();
    }
}
