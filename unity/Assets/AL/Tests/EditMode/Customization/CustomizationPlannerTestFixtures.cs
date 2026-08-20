using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;

namespace AL.Tests.EditMode.Customization
{
    internal static class CustomizationPlannerTestFixtures
    {
        internal static readonly CustomizationColor Blue =
            new CustomizationColor(0.20f, 0.40f, 1.00f);
        internal static readonly CustomizationColor Bronze =
            new CustomizationColor(0.45f, 0.38f, 0.30f);
        internal static readonly CustomizationColor Hair =
            new CustomizationColor(0.08f, 0.06f, 0.04f);
        internal static readonly CustomizationColor HairAlt =
            new CustomizationColor(0.55f, 0.36f, 0.16f);
        internal static readonly CustomizationColor Skin =
            new CustomizationColor(0.72f, 0.56f, 0.42f);
        internal static readonly CustomizationColor SkinAlt =
            new CustomizationColor(0.55f, 0.38f, 0.26f);
        internal static readonly CustomizationColor Eye =
            new CustomizationColor(0.25f, 0.58f, 0.92f);
        internal static readonly CustomizationColor EyeAlt =
            new CustomizationColor(0.28f, 0.72f, 0.42f);
        internal static readonly CustomizationColor Accent =
            new CustomizationColor(0.85f, 0.62f, 0.18f);
        internal static readonly CustomizationColor AccentAlt =
            new CustomizationColor(0.30f, 0.75f, 1.00f);

        internal static CustomizationValues Values(
            string body = "average",
            string hair = "short",
            string armor = "realm_basic",
            string face = "none",
            string weapon = "sword",
            string offhand = "shield",
            CustomizationColor? primary = null,
            CustomizationColor? hairColor = null,
            CustomizationColor? skin = null,
            CustomizationColor? eye = null,
            CustomizationColor? accent = null,
            bool cape = true,
            bool helmet = false)
        {
            return new CustomizationValues(
                body, hair, armor, face, weapon, offhand,
                primary ?? Blue,
                hairColor ?? Hair,
                skin ?? Skin,
                eye ?? Eye,
                accent ?? Accent,
                cape,
                helmet);
        }

        internal static CustomizationCatalogIdentity Identity(
            string catalogId = CustomizationTechnicalLimits.ExpectedCatalogId,
            string hash = null)
        {
            return new CustomizationCatalogIdentity(
                "another_life",
                "catalog_set_test",
                catalogId,
                CustomizationTechnicalLimits.ExpectedFamilyId,
                "customization_schema_v1",
                "0.5.0-test",
                "fixture_v1",
                hash ?? new string('0', 64),
                "GameData/character_customization.v1.json");
        }

        internal static CustomizationCatalogCandidate Candidate(
            CustomizationCatalogIdentity identity = null,
            IEnumerable<CustomizationBodyPresetCandidate> bodyPresets = null,
            IEnumerable<CustomizationOptionCandidate> options = null,
            IEnumerable<CustomizationColorCandidate> colors = null,
            IEnumerable<CustomizationAliasCandidate> aliases = null,
            IEnumerable<CustomizationPresetCandidate> presets = null,
            CustomizationPolicyCandidate policy = null)
        {
            CustomizationValues defaults = Values();
            return new CustomizationCatalogCandidate(
                identity ?? Identity(),
                bodyPresets ?? BodyPresets(),
                options ?? Options(),
                colors ?? Colors(),
                aliases ?? new[]
                {
                    new CustomizationAliasCandidate(
                        CustomizationFamilies.HairStyles,
                        "cropped",
                        "short",
                        "0.5.0-test",
                        false)
                },
                presets ?? Presets(),
                policy ?? new CustomizationPolicyCandidate(
                    defaults,
                    new CustomizationScale(0.5f, 0.5f, 0.5f),
                    new CustomizationScale(2f, 2f, 2f),
                    Placeholders(),
                    true));
        }

        internal static CustomizationCatalogSnapshot Catalog(
            CustomizationCatalogCandidate candidate = null)
        {
            CustomizationCatalogValidationResult result =
                CustomizationCatalogValidator.Validate(candidate ?? Candidate());
            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Join(",", result.Diagnostics
                    .Select(item => item.Code + ":" + item.FieldPath)));
            }

            return result.Snapshot;
        }

        internal static ModelCapabilitySnapshot Model(
            CustomizationField supported = CustomizationField.All,
            IEnumerable<string> capabilities = null,
            long revision = 1L)
        {
            ModelCapabilityValidationResult result =
                CustomizationModelCapabilityValidator.Validate(
                    new ModelCapabilityCandidate(
                        "model_capability_test",
                        revision,
                        "procedural_champion_fixture",
                        supported,
                        capabilities ?? AllCapabilities()));
            if (!result.IsValid)
            {
                throw new InvalidOperationException(string.Join(",", result.Diagnostics
                    .Select(item => item.Code + ":" + item.FieldPath)));
            }

            return result.Snapshot;
        }

        internal static RawCustomizationSnapshot Raw(
            CustomizationValues values = null,
            int schemaVersion = 1,
            long revision = 7L,
            bool metadata = false)
        {
            return new RawCustomizationSnapshot(
                schemaVersion,
                revision,
                metadata,
                values ?? Values());
        }

        internal static CustomizationQueryResult Query(
            RawCustomizationSnapshot raw = null,
            CustomizationCatalogSnapshot catalog = null,
            ModelCapabilitySnapshot model = null)
        {
            return CustomizationCompatibilityPlanner.Resolve(
                raw ?? Raw(),
                CustomizationCatalogAvailability.Ready,
                catalog ?? Catalog(),
                model ?? Model());
        }

        internal static CustomizationDraft Draft(
            RawCustomizationSnapshot raw = null,
            CustomizationCatalogSnapshot catalog = null,
            ModelCapabilitySnapshot model = null)
        {
            raw = raw ?? Raw();
            catalog = catalog ?? Catalog();
            model = model ?? Model();
            CustomizationCompatibilityResult compatibility =
                CustomizationCompatibilityPlanner.Classify(
                    raw, CustomizationCatalogAvailability.Ready, catalog);
            return CustomizationDraftPlanner.Create(
                "draft_fixture_001",
                Query(raw, catalog, model),
                compatibility);
        }

        internal static CustomizationBodyPresetCandidate[] BodyPresets()
        {
            return new[]
            {
                new CustomizationBodyPresetCandidate(
                    "average", "customization.body.average.name", "cap_body_average", 0,
                    new[] { 1f, 1f, 1f }),
                new CustomizationBodyPresetCandidate(
                    "broad", "customization.body.broad.name", "cap_body_broad", 1,
                    new[] { 1.16f, 1f, 1.06f })
            };
        }

        internal static CustomizationOptionCandidate[] Options()
        {
            return new[]
            {
                Option(CustomizationFamilies.HairStyles, "short", "cap_hair_short", 0),
                Option(CustomizationFamilies.HairStyles, "long", "cap_hair_long", 1),
                Option(CustomizationFamilies.ArmorStyles, "realm_basic", "cap_armor_basic", 0),
                Option(CustomizationFamilies.ArmorStyles, "heavy_plate", "cap_armor_heavy", 1),
                Option(CustomizationFamilies.FaceMarks, "none", "cap_face_none", 0),
                Option(CustomizationFamilies.FaceMarks, "scar", "cap_face_scar", 1),
                Option(CustomizationFamilies.WeaponStyles, "sword", "cap_weapon_sword", 0),
                Option(CustomizationFamilies.WeaponStyles, "axe", "cap_weapon_axe", 1),
                Option(CustomizationFamilies.OffhandStyles, "shield", "cap_offhand_shield", 0),
                Option(CustomizationFamilies.OffhandStyles, "orb", "cap_offhand_orb", 1)
            };
        }

        internal static CustomizationColorCandidate[] Colors()
        {
            return new[]
            {
                Color(CustomizationFamilies.PrimaryColors, "crown_blue", Blue, "cap_primary", 0),
                Color(CustomizationFamilies.PrimaryColors, "stone_bronze", Bronze, "cap_primary", 1),
                Color(CustomizationFamilies.HairColors, "raven", Hair, "cap_hair_color", 0),
                Color(CustomizationFamilies.HairColors, "chestnut", HairAlt, "cap_hair_color", 1),
                Color(CustomizationFamilies.SkinColors, "sunlit", Skin, "cap_skin_color", 0),
                Color(CustomizationFamilies.SkinColors, "deep_earth", SkinAlt, "cap_skin_color", 1),
                Color(CustomizationFamilies.EyeColors, "storm_blue", Eye, "cap_eye_color", 0),
                Color(CustomizationFamilies.EyeColors, "grove_green", EyeAlt, "cap_eye_color", 1),
                Color(CustomizationFamilies.AccentColors, "royal_gold", Accent, "cap_accent_color", 0),
                Color(CustomizationFamilies.AccentColors, "arcane_blue", AccentAlt, "cap_accent_color", 1)
            };
        }

        internal static CustomizationPresetCandidate[] Presets()
        {
            return new[]
            {
                new CustomizationPresetCandidate(
                    "vanguard",
                    "customization.preset.vanguard.name",
                    CustomizationField.All,
                    Values(
                        body: "broad",
                        hair: "short",
                        armor: "heavy_plate",
                        face: "scar",
                        weapon: "sword",
                        offhand: "shield",
                        primary: Bronze,
                        hairColor: Hair,
                        skin: SkinAlt,
                        eye: EyeAlt,
                        accent: Accent,
                        cape: true,
                        helmet: true),
                    AllCapabilities()),
                new CustomizationPresetCandidate(
                    "scout_partial",
                    "customization.preset.scout_partial.name",
                    CustomizationField.HairStyle | CustomizationField.CapeEnabled,
                    Values(hair: "long", cape: false),
                    new[] { "cap_hair_long" })
            };
        }

        internal static IReadOnlyDictionary<string, string> Placeholders()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CustomizationFamilies.BodyPresets] = "average",
                [CustomizationFamilies.HairStyles] = "short",
                [CustomizationFamilies.ArmorStyles] = "realm_basic",
                [CustomizationFamilies.FaceMarks] = "none",
                [CustomizationFamilies.WeaponStyles] = "sword",
                [CustomizationFamilies.OffhandStyles] = "shield"
            };
        }

        internal static string[] AllCapabilities()
        {
            return BodyPresets().Select(item => item.RequiredCapabilityId)
                .Concat(Options().Select(item => item.RequiredCapabilityId))
                .Concat(Colors().Select(item => item.RequiredCapabilityId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
        }

        private static CustomizationOptionCandidate Option(
            string family,
            string id,
            string capability,
            int order)
        {
            return new CustomizationOptionCandidate(
                family, id, "customization." + family + "." + id + ".name",
                capability, order);
        }

        private static CustomizationColorCandidate Color(
            string family,
            string id,
            CustomizationColor value,
            string capability,
            int order)
        {
            return new CustomizationColorCandidate(
                family, id, "customization." + family + "." + id + ".name",
                capability, order,
                new[] { value.Red, value.Green, value.Blue });
        }
    }
}
