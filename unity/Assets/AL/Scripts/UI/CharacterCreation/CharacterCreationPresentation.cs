using System;
using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Data.Catalogs;
using AL.Data.Definitions;
using AL.RealmSelection;
using AL.UI.RealmSelection;
using UnityEngine;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Anti-toy contract for the production create screen. People stay locked to the
    /// committed realm. Visible loadouts never include other realms. Identity is
    /// structural (name + people + Arcane Axis mark + frame), never hue-only.
    /// Remaining champion cards are labelled TEMPORARY until authored creator chrome lands.
    /// </summary>
    public readonly struct CharacterCreationPresentationPlan
    {
        public CharacterCreationPresentationPlan(
            RealmId realm,
            RealmIdentityPresentation identity,
            IReadOnlyList<string> visibleChampionIds,
            string title,
            string peopleCopy,
            string heraldryCopy,
            string temporaryBadge,
            string bindRealmError)
        {
            Realm = realm;
            Identity = identity;
            VisibleChampionIds = visibleChampionIds ?? Array.Empty<string>();
            Title = title ?? string.Empty;
            PeopleCopy = peopleCopy ?? string.Empty;
            HeraldryCopy = heraldryCopy ?? string.Empty;
            TemporaryBadge = temporaryBadge ?? string.Empty;
            BindRealmError = bindRealmError ?? string.Empty;
        }

        public RealmId Realm { get; }
        public RealmIdentityPresentation Identity { get; }
        public IReadOnlyList<string> VisibleChampionIds { get; }
        public string Title { get; }
        public string PeopleCopy { get; }
        public string HeraldryCopy { get; }
        public string TemporaryBadge { get; }
        public string BindRealmError { get; }

        public bool HasCommittedRealm => Realm != RealmId.None;

        public bool HasStructuralIdentity => Identity.HasStructuralIdentity;

        public bool IsColorOnly =>
            HasCommittedRealm && !HasStructuralIdentity;
    }

    public static class CharacterCreationPresentation
    {
        public const string Title = "SWEAR YOUR NAME";
        public const string TemporaryBadge = "TEMPORARY — procedural loadout. Vanguard mesh not promoted.";
        public const string BindRealmError = "Bind a realm before shaping a champion.";
        public const string DebugBarkForbidden = "NVS-01";
        public const string AllRealmPickerForbidden = "CHOOSE YOUR CHAMPION";

        public static CharacterCreationPresentationPlan Build(
            RealmId committedRealm,
            IEnumerable<ChampionDefinition> catalog,
            RealmCatalogSnapshot snapshot)
        {
            var visible = new List<string>();
            if (committedRealm == RealmId.None)
            {
                return new CharacterCreationPresentationPlan(
                    RealmId.None,
                    default,
                    visible,
                    Title,
                    string.Empty,
                    string.Empty,
                    TemporaryBadge,
                    BindRealmError);
            }

            if (catalog != null)
            {
                foreach (ChampionDefinition champion in catalog)
                {
                    if (champion != null &&
                        champion.Realm == committedRealm &&
                        !string.IsNullOrWhiteSpace(champion.Id))
                    {
                        visible.Add(champion.Id);
                    }
                }
            }

            var definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = committedRealm;
            definition.RealmName = committedRealm.ToString();
            RealmIdentityPresentation identity = RealmSelectionIdentity.Resolve(definition, snapshot);
            UnityEngine.Object.DestroyImmediate(definition);

            string peopleCopy = identity.RealmName + "  ·  " + identity.PeopleName +
                                "  —  people are locked to this realm.";
            string heraldryCopy = identity.MarkName + "  ·  " + identity.SilhouetteLanguage +
                                  "  ·  " + identity.MaterialLanguage;

            return new CharacterCreationPresentationPlan(
                committedRealm,
                identity,
                visible,
                Title,
                peopleCopy,
                heraldryCopy,
                TemporaryBadge,
                string.Empty);
        }

        public static Sprite TryLoadEmblem(RealmId realm)
        {
            if (!GameDataRealmReferences.TryGetByLegacyIdentity(
                    realm.ToString(),
                    (int)realm,
                    out GameDataRealmReference reference) ||
                string.IsNullOrEmpty(reference.AssetReference))
            {
                return null;
            }

            string relative = reference.AssetReference
                .Replace("Assets/", string.Empty)
                .Replace('/', Path.DirectorySeparatorChar);
            string path = Path.Combine(Application.dataPath, relative);
            if (!File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
