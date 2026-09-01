using System;
using AL.Core;
using AL.Data.Runtime;
using AL.UI.FirstUserIdentity;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Appearance + class snapshot for character creation. Writes only existing
    /// <see cref="ChampionCustomizationState"/> nested fields — no new SaveGameData top-level slot.
    /// </summary>
    public static class CharacterCreationLook
    {
        public static readonly ClassFamily[] Families =
        {
            ClassFamily.Warrior,
            ClassFamily.Mage,
            ClassFamily.Ranger,
            ClassFamily.Assassin
        };

        public static readonly string[] HairStyles =
        {
            "short", "long", "braid", "mohawk", "topknot"
        };

        public static readonly string[] BodyBases =
        {
            "male", "female"
        };

        public static readonly float[][] ArmorTints =
        {
            new[] { 0.20f, 0.40f, 1.00f },
            new[] { 0.45f, 0.38f, 0.30f },
            new[] { 0.18f, 0.58f, 0.32f },
            new[] { 0.85f, 0.62f, 0.18f },
            new[] { 0.22f, 0.08f, 0.28f }
        };

        public static readonly float[][] HairColors =
        {
            new[] { 0.08f, 0.06f, 0.04f },
            new[] { 0.55f, 0.36f, 0.16f },
            new[] { 0.85f, 0.78f, 0.55f },
            new[] { 0.80f, 0.82f, 0.90f },
            new[] { 0.25f, 0.05f, 0.08f }
        };

        public static readonly float[][] BodyTints =
        {
            new[] { 0.72f, 0.56f, 0.42f },
            new[] { 0.55f, 0.38f, 0.26f },
            new[] { 0.86f, 0.70f, 0.54f },
            new[] { 0.64f, 0.50f, 0.46f },
            new[] { 0.42f, 0.34f, 0.40f }
        };

        public static readonly float[][] EyeColors =
        {
            new[] { 0.25f, 0.58f, 0.92f },
            new[] { 0.24f, 0.72f, 0.42f },
            new[] { 0.74f, 0.48f, 0.18f },
            new[] { 0.62f, 0.28f, 0.88f },
            new[] { 0.82f, 0.84f, 0.88f }
        };

        public static readonly string[] BodyPresets =
        {
            "average", "slim", "broad", "tall", "stout"
        };

        public static ClassFamily[] FamiliesForRealm(RealmId realm)
        {
            return FirstUserIdentityDerivation.IsSupportedRealm(realm)
                ? Families
                : Array.Empty<ClassFamily>();
        }

        public static bool TryPeopleLabel(RealmId realm, out string label)
        {
            if (!FirstUserIdentityDerivation.TryDeriveRace(realm, out FirstUserRace race))
            {
                label = string.Empty;
                return false;
            }

            switch (race)
            {
                case FirstUserRace.Humans:
                    label = "Human people";
                    return true;
                case FirstUserRace.Dwarves:
                    label = "Dwarven people";
                    return true;
                case FirstUserRace.Elves:
                    label = "Elven people";
                    return true;
                case FirstUserRace.DarkElves:
                    label = "Dark Elven people";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        public static bool TryRealmLabel(RealmId realm, out string label)
        {
            switch (realm)
            {
                case RealmId.Crownlands:
                    label = "Crownlands";
                    return true;
                case RealmId.Stonehold:
                    label = "Stonehold";
                    return true;
                case RealmId.Eldergrove:
                    label = "Eldergrove";
                    return true;
                case RealmId.Umbral:
                    label = "Umbral";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        public static bool TryClassLabel(ClassFamily family, out string label)
        {
            switch (family)
            {
                case ClassFamily.Warrior:
                    label = "Warrior path";
                    return true;
                case ClassFamily.Mage:
                    label = "Mage path";
                    return true;
                case ClassFamily.Ranger:
                    label = "Ranger path";
                    return true;
                case ClassFamily.Assassin:
                    label = "Assassin path";
                    return true;
                default:
                    label = string.Empty;
                    return false;
            }
        }

        public static void ApplyRealmDefaults(ChampionCustomizationState state, RealmId realm)
        {
            if (state == null)
            {
                return;
            }

            state.BodyBaseId = BodyBases[0];
            switch (realm)
            {
                case RealmId.Stonehold:
                    state.BodyPresetId = "stout";
                    state.HairStyleId = "braid";
                    CopyRgb(ArmorTints[1], out state.PrimaryR, out state.PrimaryG, out state.PrimaryB);
                    CopyRgb(BodyTints[1], out state.SkinR, out state.SkinG, out state.SkinB);
                    break;
                case RealmId.Eldergrove:
                    state.BodyPresetId = "tall";
                    state.HairStyleId = "long";
                    CopyRgb(ArmorTints[2], out state.PrimaryR, out state.PrimaryG, out state.PrimaryB);
                    CopyRgb(BodyTints[0], out state.SkinR, out state.SkinG, out state.SkinB);
                    break;
                case RealmId.Crownlands:
                    state.BodyPresetId = "average";
                    state.HairStyleId = "short";
                    CopyRgb(ArmorTints[3], out state.PrimaryR, out state.PrimaryG, out state.PrimaryB);
                    CopyRgb(BodyTints[2], out state.SkinR, out state.SkinG, out state.SkinB);
                    break;
                case RealmId.Umbral:
                    state.BodyPresetId = "slim";
                    state.HairStyleId = "topknot";
                    CopyRgb(ArmorTints[4], out state.PrimaryR, out state.PrimaryG, out state.PrimaryB);
                    CopyRgb(BodyTints[4], out state.SkinR, out state.SkinG, out state.SkinB);
                    break;
                default:
                    state.BodyPresetId = "average";
                    state.HairStyleId = "short";
                    break;
            }

            CopyRgb(HairColors[0], out state.HairR, out state.HairG, out state.HairB);
            CopyRgb(EyeColors[0], out state.EyeR, out state.EyeG, out state.EyeB);
            state.CapeEnabled = true;
            state.HelmetEnabled = false;
            state.ArmorStyleId = "realm_basic";
        }

        public static void ApplyClassLoadout(ChampionCustomizationState state, ClassFamily family)
        {
            if (state == null)
            {
                return;
            }

            switch (family)
            {
                case ClassFamily.Warrior:
                    state.ArmorStyleId = "heavy_plate";
                    state.WeaponStyleId = "sword";
                    state.OffhandStyleId = "shield";
                    break;
                case ClassFamily.Mage:
                    state.ArmorStyleId = "arcane_robes";
                    state.WeaponStyleId = "staff";
                    state.OffhandStyleId = "orb";
                    break;
                case ClassFamily.Ranger:
                    state.ArmorStyleId = "light_scout";
                    state.WeaponStyleId = "bow";
                    state.OffhandStyleId = "none";
                    break;
                case ClassFamily.Assassin:
                    state.ArmorStyleId = "assassin_leathers";
                    state.WeaponStyleId = "sword";
                    state.OffhandStyleId = "dagger";
                    break;
            }
        }

        public static void CopyInto(ChampionCustomizationState target, ChampionCustomizationState source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.BodyBaseId = NormalizeBodyBaseId(source.BodyBaseId);
            target.BodyPresetId = source.BodyPresetId;
            target.HairStyleId = source.HairStyleId;
            target.ArmorStyleId = source.ArmorStyleId;
            target.WeaponStyleId = source.WeaponStyleId;
            target.OffhandStyleId = source.OffhandStyleId;
            target.PrimaryR = source.PrimaryR;
            target.PrimaryG = source.PrimaryG;
            target.PrimaryB = source.PrimaryB;
            target.HairR = source.HairR;
            target.HairG = source.HairG;
            target.HairB = source.HairB;
            target.SkinR = source.SkinR;
            target.SkinG = source.SkinG;
            target.SkinB = source.SkinB;
            if (Math.Abs(source.EyeR) + Math.Abs(source.EyeG) + Math.Abs(source.EyeB) < 0.001f)
            {
                CopyRgb(EyeColors[0], out target.EyeR, out target.EyeG, out target.EyeB);
            }
            else
            {
                target.EyeR = source.EyeR;
                target.EyeG = source.EyeG;
                target.EyeB = source.EyeB;
            }
            target.CapeEnabled = source.CapeEnabled;
            target.HelmetEnabled = source.HelmetEnabled;
        }

        public static bool Matches(ChampionCustomizationState left, ChampionCustomizationState right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            return string.Equals(
                       NormalizeBodyBaseId(left.BodyBaseId),
                       NormalizeBodyBaseId(right.BodyBaseId),
                       StringComparison.Ordinal) &&
                   string.Equals(left.BodyPresetId, right.BodyPresetId, StringComparison.Ordinal) &&
                   string.Equals(left.HairStyleId, right.HairStyleId, StringComparison.Ordinal) &&
                   string.Equals(left.ArmorStyleId, right.ArmorStyleId, StringComparison.Ordinal) &&
                   string.Equals(left.WeaponStyleId, right.WeaponStyleId, StringComparison.Ordinal) &&
                   string.Equals(left.OffhandStyleId, right.OffhandStyleId, StringComparison.Ordinal) &&
                   NearlyEqual(left.PrimaryR, right.PrimaryR) &&
                   NearlyEqual(left.PrimaryG, right.PrimaryG) &&
                   NearlyEqual(left.PrimaryB, right.PrimaryB) &&
                   NearlyEqual(left.HairR, right.HairR) &&
                   NearlyEqual(left.HairG, right.HairG) &&
                   NearlyEqual(left.HairB, right.HairB) &&
                   NearlyEqual(left.SkinR, right.SkinR) &&
                   NearlyEqual(left.SkinG, right.SkinG) &&
                   NearlyEqual(left.SkinB, right.SkinB) &&
                   NearlyEqual(left.EyeR, right.EyeR) &&
                   NearlyEqual(left.EyeG, right.EyeG) &&
                   NearlyEqual(left.EyeB, right.EyeB) &&
                   left.CapeEnabled == right.CapeEnabled &&
                   left.HelmetEnabled == right.HelmetEnabled;
        }

        public static bool LooksDifferent(ChampionCustomizationState left, ChampionCustomizationState right)
        {
            return !Matches(left, right);
        }

        public static int IndexOfRgb(float r, float g, float b, float[][] palette)
        {
            for (int i = 0; i < palette.Length; i++)
            {
                if (NearlyEqual(r, palette[i][0]) &&
                    NearlyEqual(g, palette[i][1]) &&
                    NearlyEqual(b, palette[i][2]))
                {
                    return i;
                }
            }

            return 0;
        }

        public static int IndexOfId(string id, string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(id, ids[i], StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return 0;
        }

        public static int NormalizePaletteIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return Math.Max(0, Math.Min(index, count - 1));
        }

        public static string NormalizeBodyBaseId(string id)
        {
            return string.Equals(id, BodyBases[1], StringComparison.Ordinal)
                ? BodyBases[1]
                : BodyBases[0];
        }

        public static void CopyRgb(float[] rgb, out float r, out float g, out float b)
        {
            r = rgb[0];
            g = rgb[1];
            b = rgb[2];
        }

        private static bool NearlyEqual(float a, float b)
        {
            return Math.Abs(a - b) < 0.001f;
        }
    }
}
