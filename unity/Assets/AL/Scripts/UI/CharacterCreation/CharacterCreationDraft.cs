using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.UI.FirstUserIdentity;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// In-memory creator draft: realm-locked people, class family, and appearance.
    /// Not a four-archetype picker.
    /// </summary>
    public sealed class CharacterCreationDraft
    {
        private CharacterCreationDraft(RealmId realm)
        {
            Realm = realm;
            Customization = new ChampionCustomizationState();
            CharacterCreationLook.ApplyRealmDefaults(Customization, realm);
        }

        public RealmId Realm { get; }
        public ChampionCustomizationState Customization { get; }
        public ClassFamily? ClassFamily { get; private set; }

        public ClassFamily[] AvailableFamilies => CharacterCreationLook.FamiliesForRealm(Realm);

        public static bool TryCreate(RealmId realm, out CharacterCreationDraft draft, out string error)
        {
            if (!FirstUserIdentityDerivation.IsSupportedRealm(realm))
            {
                draft = null;
                error = "Commit a realm before creating a champion.";
                return false;
            }

            draft = new CharacterCreationDraft(realm);
            error = string.Empty;
            return true;
        }

        public bool TrySelectClassFamily(ClassFamily family, out string error)
        {
            ClassFamily[] available = AvailableFamilies;
            for (int i = 0; i < available.Length; i++)
            {
                if (available[i] == family)
                {
                    ClassFamily = family;
                    CharacterCreationLook.ApplyClassLoadout(Customization, family);
                    error = string.Empty;
                    return true;
                }
            }

            error = "That class path is not available in this realm.";
            return false;
        }

        public void CycleArmorTint()
        {
            int index = CharacterCreationLook.IndexOfRgb(
                Customization.PrimaryR,
                Customization.PrimaryG,
                Customization.PrimaryB,
                CharacterCreationLook.ArmorTints);
            float[] next = CharacterCreationLook.ArmorTints[(index + 1) % CharacterCreationLook.ArmorTints.Length];
            CharacterCreationLook.CopyRgb(next, out Customization.PrimaryR, out Customization.PrimaryG, out Customization.PrimaryB);
        }

        public void CycleBodyBase()
        {
            int index = CharacterCreationLook.IndexOfId(
                CharacterCreationLook.NormalizeBodyBaseId(Customization.BodyBaseId),
                CharacterCreationLook.BodyBases);
            Customization.BodyBaseId = CharacterCreationLook.BodyBases[
                (index + 1) % CharacterCreationLook.BodyBases.Length];
        }

        public void CycleBodyTint()
        {
            int index = CharacterCreationLook.IndexOfRgb(
                Customization.SkinR,
                Customization.SkinG,
                Customization.SkinB,
                CharacterCreationLook.BodyTints);
            float[] next = CharacterCreationLook.BodyTints[(index + 1) % CharacterCreationLook.BodyTints.Length];
            CharacterCreationLook.CopyRgb(next, out Customization.SkinR, out Customization.SkinG, out Customization.SkinB);
        }

        public void CycleHairStyle()
        {
            int index = CharacterCreationLook.IndexOfId(Customization.HairStyleId, CharacterCreationLook.HairStyles);
            Customization.HairStyleId = CharacterCreationLook.HairStyles[(index + 1) % CharacterCreationLook.HairStyles.Length];
        }

        public void CycleHairColor()
        {
            int index = CharacterCreationLook.IndexOfRgb(
                Customization.HairR,
                Customization.HairG,
                Customization.HairB,
                CharacterCreationLook.HairColors);
            float[] next = CharacterCreationLook.HairColors[(index + 1) % CharacterCreationLook.HairColors.Length];
            CharacterCreationLook.CopyRgb(next, out Customization.HairR, out Customization.HairG, out Customization.HairB);
        }

        public void CycleBodyPreset()
        {
            int index = CharacterCreationLook.IndexOfId(Customization.BodyPresetId, CharacterCreationLook.BodyPresets);
            Customization.BodyPresetId = CharacterCreationLook.BodyPresets[(index + 1) % CharacterCreationLook.BodyPresets.Length];
        }

        public void ToggleHelmet()
        {
            Customization.HelmetEnabled = !Customization.HelmetEnabled;
        }

        public void ToggleCape()
        {
            Customization.CapeEnabled = !Customization.CapeEnabled;
        }

        public static ChampionDefinition BindChampion(
            IEnumerable<ChampionDefinition> champions,
            RealmId realm,
            ClassFamily family)
        {
            if (champions == null)
            {
                return null;
            }

            ChampionDefinition realmMatch = null;
            ChampionDefinition familyMatch = null;
            foreach (ChampionDefinition champion in champions)
            {
                if (champion == null)
                {
                    continue;
                }

                if (champion.Realm == realm && champion.Family == family)
                {
                    return champion;
                }

                if (realmMatch == null && champion.Realm == realm)
                {
                    realmMatch = champion;
                }

                if (familyMatch == null && champion.Family == family)
                {
                    familyMatch = champion;
                }
            }

            return realmMatch ?? familyMatch;
        }
    }
}
