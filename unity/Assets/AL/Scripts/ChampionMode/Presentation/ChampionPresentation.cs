using System;
using System.Collections.Generic;
using AL.Core;
using AL.UI.FirstUserIdentity;
using UnityEngine;

namespace AL.ChampionMode.Presentation
{
    /// <summary>
    /// Catalog identity used to dress the first-session 3D body.
    /// Mesh/prefab fields are recorded but not loaded as 3D until a promoted model exists.
    /// </summary>
    public readonly struct ChampionPresentationCatalogEntry
    {
        public ChampionPresentationCatalogEntry(
            string id,
            string displayName,
            RealmId realm,
            ClassFamily classFamily,
            string weaponStyleId,
            string offhandStyleId,
            string portraitAssetRef,
            string modelAssetRef)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Realm = realm;
            ClassFamily = classFamily;
            WeaponStyleId = weaponStyleId ?? string.Empty;
            OffhandStyleId = offhandStyleId ?? string.Empty;
            PortraitAssetRef = portraitAssetRef ?? string.Empty;
            ModelAssetRef = modelAssetRef ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public RealmId Realm { get; }
        public ClassFamily ClassFamily { get; }
        public string WeaponStyleId { get; }
        public string OffhandStyleId { get; }
        public string PortraitAssetRef { get; }
        public string ModelAssetRef { get; }
    }

    public sealed class ChampionPresentationSpec
    {
        public RealmId Realm;
        public string PeopleName = string.Empty;
        public ClassFamily ClassFamily;
        public string ChampionId = string.Empty;
        public string DisplayName = string.Empty;
        public string BodyPresetId = "average";
        public string HairStyleId = "short";
        public string ArmorStyleId = "heavy_plate";
        public string WeaponStyleId = "sword";
        public string OffhandStyleId = "shield";
        public string FaceMarkId = "none";
        public bool CapeEnabled;
        public bool HelmetEnabled;
        public Color Primary = new Color(0.20f, 0.40f, 1.00f);
        public Color Hair = new Color(0.08f, 0.06f, 0.04f);
        public Color Skin = new Color(0.72f, 0.56f, 0.42f);
        public Color Eye = new Color(0.25f, 0.58f, 0.92f);
        public Color Accent = new Color(0.85f, 0.62f, 0.18f);
        public string BodySource = ChampionPresentation.BindingSampleSource;
        public bool UsesPromotedMesh;
        public string PortraitAssetRef = string.Empty;
        public string ModelAssetRef = string.Empty;
        public string[] TemporaryParts = Array.Empty<string>();

        public string ClassFamilyTokenName =>
            FirstSessionChampionStart.ClassFamilyTokenPrefix + ClassFamily;

        public string PeopleTokenName =>
            FirstSessionChampionStart.PeopleTokenPrefix + PeopleName.Replace(" ", string.Empty);
    }

    /// <summary>
    /// Resolves first-session champion presentation from catalog identity.
    /// People stay locked to realm. Class family is visible as armor/loadout only —
    /// no invented abilities. Vanguard mesh stays unused until the user promotes it.
    /// </summary>
    public static class ChampionPresentation
    {
        public const string BindingSampleSource = "procedural_binding_sample";
        public const string VanguardCandidateSource = "champion_vanguard_working_v001";
        public const bool VanguardMeshPromoted = false;

        public static readonly string[] TemporaryParts =
        {
            "procedural_adult_body",
            "primitive_class_loadout",
            "citadel_greybox"
        };

        public static bool TryResolveFromSession(
            RealmId arenaRealm,
            ClassFamily? requestedClass,
            string requestedChampionId,
            IReadOnlyList<ChampionPresentationCatalogEntry> catalog,
            out ChampionPresentationSpec spec,
            out string diagnostic)
        {
            spec = null;
            diagnostic = string.Empty;
            if (!FirstUserIdentityDerivation.IsSupportedRealm(arenaRealm) ||
                !FirstUserIdentityDerivation.TryDeriveRace(arenaRealm, out FirstUserRace people))
            {
                diagnostic = "AL-CHAMPION-PRESENTATION-REALM: arena realm is not a supported people lock.";
                return false;
            }

            ChampionPresentationCatalogEntry realmEntry = default;
            bool hasRealmEntry = TryFindRealmEntry(arenaRealm, catalog, out realmEntry);
            if (!string.IsNullOrWhiteSpace(requestedChampionId) &&
                TryFindById(requestedChampionId, catalog, out ChampionPresentationCatalogEntry requested) &&
                requested.Realm != arenaRealm)
            {
                // No cross-realm body swap: ignore the foreign champion id.
                requestedChampionId = string.Empty;
            }

            ClassFamily classFamily;
            if (requestedClass.HasValue &&
                FirstUserIdentityDerivation.IsSupportedClassFamily(requestedClass.Value))
            {
                classFamily = requestedClass.Value;
            }
            else if (hasRealmEntry)
            {
                classFamily = realmEntry.ClassFamily;
            }
            else
            {
                classFamily = ClassFamily.Warrior;
            }

            ChampionPresentationCatalogEntry dressEntry = default;
            bool hasDressEntry = false;
            if (!string.IsNullOrWhiteSpace(requestedChampionId) &&
                TryFindById(requestedChampionId, catalog, out ChampionPresentationCatalogEntry byId) &&
                byId.Realm == arenaRealm)
            {
                dressEntry = byId;
                hasDressEntry = true;
            }
            else if (hasRealmEntry)
            {
                dressEntry = realmEntry;
                hasDressEntry = true;
            }

            spec = new ChampionPresentationSpec
            {
                Realm = arenaRealm,
                PeopleName = PeopleName(people),
                ClassFamily = classFamily,
                ChampionId = hasDressEntry ? dressEntry.Id : string.Empty,
                DisplayName = hasDressEntry ? dressEntry.DisplayName : classFamily.ToString(),
                PortraitAssetRef = hasDressEntry ? dressEntry.PortraitAssetRef : string.Empty,
                ModelAssetRef = hasDressEntry ? dressEntry.ModelAssetRef : string.Empty,
                BodySource = BindingSampleSource,
                UsesPromotedMesh = false,
                TemporaryParts = TemporaryParts
            };

            ApplyPeopleLock(spec, people, arenaRealm);
            ApplyClassFamilyLoadout(
                spec,
                classFamily,
                hasDressEntry && dressEntry.ClassFamily == classFamily ? dressEntry : default,
                hasDressEntry && dressEntry.ClassFamily == classFamily);
            return true;
        }

        public static string MapWeaponStyle(string catalogWeaponStyleId)
        {
            switch ((catalogWeaponStyleId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "greataxe":
                case "axe":
                    return "axe";
                case "staff":
                    return "staff";
                case "longbow":
                case "bow":
                    return "bow";
                case "hammer":
                    return "hammer";
                case "twinblades":
                case "sword":
                    return "sword";
                default:
                    return "sword";
            }
        }

        public static string MapOffhandStyle(string catalogOffhandStyleId)
        {
            switch ((catalogOffhandStyleId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "towershield":
                case "shield":
                    return "shield";
                case "tome":
                    return "tome";
                case "orb":
                    return "orb";
                case "shroud":
                case "dagger":
                    return "dagger";
                case "quiver":
                case "none":
                    return "none";
                default:
                    return "none";
            }
        }

        public static string PeopleName(FirstUserRace people)
        {
            switch (people)
            {
                case FirstUserRace.Humans:
                    return "Humans";
                case FirstUserRace.Dwarves:
                    return "Dwarves";
                case FirstUserRace.Elves:
                    return "Elves";
                case FirstUserRace.DarkElves:
                    return "Dark Elves";
                default:
                    return string.Empty;
            }
        }

        private static void ApplyPeopleLock(
            ChampionPresentationSpec spec,
            FirstUserRace people,
            RealmId realm)
        {
            spec.Primary = RealmAccent(realm);
            spec.Accent = Color.Lerp(spec.Primary, new Color(0.85f, 0.62f, 0.18f), 0.35f);
            switch (people)
            {
                case FirstUserRace.Dwarves:
                    spec.BodyPresetId = "stout";
                    spec.HairStyleId = "short";
                    spec.FaceMarkId = "beard";
                    spec.Skin = new Color(0.55f, 0.38f, 0.26f);
                    spec.Hair = new Color(0.08f, 0.06f, 0.04f);
                    spec.Eye = new Color(0.70f, 0.42f, 0.18f);
                    break;
                case FirstUserRace.Elves:
                    spec.BodyPresetId = "tall";
                    spec.HairStyleId = "long";
                    spec.FaceMarkId = "none";
                    spec.Skin = new Color(0.86f, 0.70f, 0.54f);
                    spec.Hair = new Color(0.85f, 0.78f, 0.55f);
                    spec.Eye = new Color(0.28f, 0.72f, 0.42f);
                    break;
                case FirstUserRace.DarkElves:
                    spec.BodyPresetId = "slim";
                    spec.HairStyleId = "braid";
                    spec.FaceMarkId = "none";
                    spec.Skin = new Color(0.42f, 0.34f, 0.40f);
                    spec.Hair = new Color(0.08f, 0.06f, 0.04f);
                    spec.Eye = new Color(0.78f, 0.72f, 0.88f);
                    break;
                default:
                    spec.BodyPresetId = "average";
                    spec.HairStyleId = "short";
                    spec.FaceMarkId = "none";
                    spec.Skin = new Color(0.72f, 0.56f, 0.42f);
                    spec.Hair = new Color(0.08f, 0.06f, 0.04f);
                    spec.Eye = new Color(0.25f, 0.58f, 0.92f);
                    break;
            }
        }

        private static void ApplyClassFamilyLoadout(
            ChampionPresentationSpec spec,
            ClassFamily classFamily,
            ChampionPresentationCatalogEntry catalogEntry,
            bool useCatalogLoadout)
        {
            switch (classFamily)
            {
                case ClassFamily.Mage:
                    spec.ArmorStyleId = "arcane_robes";
                    spec.WeaponStyleId = useCatalogLoadout ? MapWeaponStyle(catalogEntry.WeaponStyleId) : "staff";
                    spec.OffhandStyleId = useCatalogLoadout ? MapOffhandStyle(catalogEntry.OffhandStyleId) : "tome";
                    spec.CapeEnabled = true;
                    spec.HelmetEnabled = false;
                    break;
                case ClassFamily.Ranger:
                    spec.ArmorStyleId = "light_scout";
                    spec.WeaponStyleId = useCatalogLoadout ? MapWeaponStyle(catalogEntry.WeaponStyleId) : "bow";
                    spec.OffhandStyleId = useCatalogLoadout ? MapOffhandStyle(catalogEntry.OffhandStyleId) : "none";
                    spec.CapeEnabled = false;
                    spec.HelmetEnabled = false;
                    break;
                case ClassFamily.Assassin:
                    spec.ArmorStyleId = "assassin_leathers";
                    spec.WeaponStyleId = useCatalogLoadout ? MapWeaponStyle(catalogEntry.WeaponStyleId) : "sword";
                    spec.OffhandStyleId = useCatalogLoadout ? MapOffhandStyle(catalogEntry.OffhandStyleId) : "dagger";
                    spec.CapeEnabled = false;
                    spec.HelmetEnabled = false;
                    break;
                default:
                    spec.ArmorStyleId = "heavy_plate";
                    spec.WeaponStyleId = useCatalogLoadout ? MapWeaponStyle(catalogEntry.WeaponStyleId) : "axe";
                    spec.OffhandStyleId = useCatalogLoadout ? MapOffhandStyle(catalogEntry.OffhandStyleId) : "shield";
                    spec.CapeEnabled = true;
                    spec.HelmetEnabled = true;
                    break;
            }
        }

        private static Color RealmAccent(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold:
                    return new Color(0.84f, 0.68f, 0.42f);
                case RealmId.Eldergrove:
                    return new Color(0.34f, 1f, 0.56f);
                case RealmId.Crownlands:
                    return new Color(0.32f, 0.56f, 1f);
                case RealmId.Umbral:
                    return new Color(0.82f, 0.22f, 1f);
                default:
                    return new Color(0.72f, 0.78f, 0.84f);
            }
        }

        private static bool TryFindRealmEntry(
            RealmId realm,
            IReadOnlyList<ChampionPresentationCatalogEntry> catalog,
            out ChampionPresentationCatalogEntry entry)
        {
            entry = default;
            if (catalog == null)
            {
                return false;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].Realm == realm &&
                    FirstUserIdentityDerivation.IsSupportedClassFamily(catalog[i].ClassFamily))
                {
                    entry = catalog[i];
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindById(
            string id,
            IReadOnlyList<ChampionPresentationCatalogEntry> catalog,
            out ChampionPresentationCatalogEntry entry)
        {
            entry = default;
            if (catalog == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                if (string.Equals(catalog[i].Id, id, StringComparison.Ordinal))
                {
                    entry = catalog[i];
                    return true;
                }
            }

            return false;
        }
    }
}
