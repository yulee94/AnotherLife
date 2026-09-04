using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization.Contracts;

namespace AL.ChampionMode.Customization
{
    public static class ProductionCustomizationCatalogAdapter
    {
        private const string DefaultBody = "average";
        private const string DefaultHair = "short";
        private const string DefaultArmor = "realm_basic";
        private const string DefaultFace = "none";
        private const string DefaultWeapon = "sword";
        private const string DefaultOffhand = "shield";

        public static bool TryAdapt(
            CharacterCustomizationCatalogData source,
            out CustomizationCatalogSnapshot catalog,
            out IReadOnlyList<CustomizationDiagnostic> diagnostics)
        {
            catalog = null;
            if (!HasRequiredSource(source))
            {
                diagnostics = Failure("AL-CUS-PRODUCTION-SOURCE", "catalog.source");
                return false;
            }

            try
            {
                CustomizationCatalogCandidate candidate = BuildCandidate(source);
                CustomizationCatalogValidationResult validation =
                    CustomizationCatalogValidator.Validate(candidate);
                diagnostics = validation.Diagnostics;
                catalog = validation.Snapshot;
                return validation.IsValid;
            }
            catch (Exception)
            {
                diagnostics = Failure("AL-CUS-PRODUCTION-PROJECTION", "catalog.source");
                return false;
            }
        }

        private static CustomizationCatalogCandidate BuildCandidate(
            CharacterCustomizationCatalogData source)
        {
            CustomizationBodyPresetCandidate[] bodies = source.bodyPresets
                .Select((item, index) => new CustomizationBodyPresetCandidate(
                    item.id,
                    ContentKey(CustomizationFamilies.BodyPresets, item.id),
                    Capability(CustomizationFamilies.BodyPresets, item.id),
                    index,
                    item.scale))
                .ToArray();
            CustomizationOptionCandidate[] options =
                Options(source.hairStyles, CustomizationFamilies.HairStyles)
                    .Concat(Options(source.armorStyles, CustomizationFamilies.ArmorStyles))
                    .Concat(Options(source.faceMarks, CustomizationFamilies.FaceMarks))
                    .Concat(Options(source.weaponStyles, CustomizationFamilies.WeaponStyles))
                    .Concat(Options(source.offhandStyles, CustomizationFamilies.OffhandStyles))
                    .ToArray();
            CustomizationColorCandidate[] colors =
                Colors(source.primaryColors, CustomizationFamilies.PrimaryColors)
                    .Concat(Colors(source.hairColors, CustomizationFamilies.HairColors))
                    .Concat(Colors(source.skinColors, CustomizationFamilies.SkinColors))
                    .Concat(Colors(source.eyeColors, CustomizationFamilies.EyeColors))
                    .Concat(Colors(source.accentColors, CustomizationFamilies.AccentColors))
                    .ToArray();
            CustomizationValues defaults = new CustomizationValues(
                DefaultBody,
                DefaultHair,
                DefaultArmor,
                DefaultFace,
                DefaultWeapon,
                DefaultOffhand,
                FindColor(source.primaryColors, "crown_blue"),
                FindColor(source.hairColors, "raven"),
                FindColor(source.skinColors, "sunlit"),
                FindColor(source.eyeColors, "storm_blue"),
                FindColor(source.accentColors, "royal_gold"),
                true,
                false);
            CustomizationPresetCandidate[] presets = source.forgePresets
                .Select(item => Preset(item))
                .ToArray();
            var placeholders = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CustomizationFamilies.BodyPresets] = DefaultBody,
                [CustomizationFamilies.HairStyles] = DefaultHair,
                [CustomizationFamilies.ArmorStyles] = DefaultArmor,
                [CustomizationFamilies.FaceMarks] = DefaultFace,
                [CustomizationFamilies.WeaponStyles] = DefaultWeapon,
                [CustomizationFamilies.OffhandStyles] = DefaultOffhand
            };
            CustomizationScale minimum = ScaleBound(bodies, true);
            CustomizationScale maximum = ScaleBound(bodies, false);

            return new CustomizationCatalogCandidate(
                new CustomizationCatalogIdentity(
                    "another_life",
                    "character_customization_v1",
                    CustomizationTechnicalLimits.ExpectedCatalogId,
                    CustomizationTechnicalLimits.ExpectedFamilyId,
                    "wire_schema_v" + source.schemaVersion,
                    source.version,
                    source.sourceRevision,
                    source.sourceSha256,
                    CharacterCustomizationCatalog.CatalogRelativePath),
                bodies,
                options,
                colors,
                Array.Empty<CustomizationAliasCandidate>(),
                presets,
                new CustomizationPolicyCandidate(
                    defaults,
                    minimum,
                    maximum,
                    placeholders,
                    true));
        }

        private static IEnumerable<CustomizationOptionCandidate> Options(
            StyleOptionData[] source,
            string family)
        {
            return source.Select((item, index) =>
                new CustomizationOptionCandidate(
                    family,
                    item.id,
                    ContentKey(family, item.id),
                    Capability(family, item.id),
                    index));
        }

        private static IEnumerable<CustomizationColorCandidate> Colors(
            ColorOptionData[] source,
            string family)
        {
            return source.Select((item, index) =>
                new CustomizationColorCandidate(
                    family,
                    item.id,
                    ContentKey(family, item.id),
                    Capability(family, item.id),
                    index,
                    item.rgb));
        }

        private static CustomizationPresetCandidate Preset(
            ChampionForgePresetData source)
        {
            CustomizationValues values = new CustomizationValues(
                source.bodyPresetId,
                source.hairStyleId,
                source.armorStyleId,
                source.faceMarkId,
                source.weaponStyleId,
                source.offhandStyleId,
                Color(source.primaryColor),
                Color(source.hairColor),
                Color(source.skinColor),
                Color(source.eyeColor),
                Color(source.accentColor),
                source.capeEnabled,
                source.helmetEnabled);
            string[] capabilities = new[]
            {
                "preset." + source.id,
                Capability(CustomizationFamilies.BodyPresets, source.bodyPresetId),
                Capability(CustomizationFamilies.HairStyles, source.hairStyleId),
                Capability(CustomizationFamilies.ArmorStyles, source.armorStyleId),
                Capability(CustomizationFamilies.FaceMarks, source.faceMarkId),
                Capability(CustomizationFamilies.WeaponStyles, source.weaponStyleId),
                Capability(CustomizationFamilies.OffhandStyles, source.offhandStyleId),
                Capability(CustomizationFamilies.PrimaryColors, string.Empty),
                Capability(CustomizationFamilies.HairColors, string.Empty),
                Capability(CustomizationFamilies.SkinColors, string.Empty),
                Capability(CustomizationFamilies.EyeColors, string.Empty),
                Capability(CustomizationFamilies.AccentColors, string.Empty),
                "model.flag.cape",
                "model.flag.helmet"
            }.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            return new CustomizationPresetCandidate(
                source.id,
                "customization.preset." + source.id + ".name",
                CustomizationField.All,
                values,
                capabilities);
        }

        private static string ContentKey(string family, string id)
        {
            string prefix;
            switch (family)
            {
                case CustomizationFamilies.BodyPresets:
                    prefix = "body";
                    break;
                case CustomizationFamilies.HairStyles:
                    prefix = "hair";
                    break;
                case CustomizationFamilies.ArmorStyles:
                    prefix = "armor";
                    break;
                case CustomizationFamilies.FaceMarks:
                    prefix = "face";
                    break;
                case CustomizationFamilies.WeaponStyles:
                    prefix = "weapon";
                    break;
                case CustomizationFamilies.OffhandStyles:
                    prefix = "offhand";
                    break;
                default:
                    prefix = family;
                    break;
            }

            return "customization." + prefix + "." + id + ".name";
        }

        internal static string Capability(string family, string id)
        {
            switch (family)
            {
                case CustomizationFamilies.BodyPresets:
                    return "model.body_scale." + id;
                case CustomizationFamilies.HairStyles:
                    return "model.hair_style." + id;
                case CustomizationFamilies.ArmorStyles:
                    return "model.armor_style." + id;
                case CustomizationFamilies.FaceMarks:
                    return "model.face_mark." + id;
                case CustomizationFamilies.WeaponStyles:
                    return "model.weapon_style." + id;
                case CustomizationFamilies.OffhandStyles:
                    return "model.offhand_style." + id;
                case CustomizationFamilies.PrimaryColors:
                    return "material.primary_color";
                case CustomizationFamilies.HairColors:
                    return "material.hair_color";
                case CustomizationFamilies.SkinColors:
                    return "material.skin_color";
                case CustomizationFamilies.EyeColors:
                    return "material.eye_color";
                case CustomizationFamilies.AccentColors:
                    return "material.accent_color";
                default:
                    return string.Empty;
            }
        }

        private static CustomizationColor FindColor(
            IEnumerable<ColorOptionData> colors,
            string id)
        {
            ColorOptionData match = colors.First(item =>
                string.Equals(item.id, id, StringComparison.Ordinal));
            return Color(match.rgb);
        }

        private static CustomizationColor Color(float[] rgb)
        {
            if (rgb == null || rgb.Length != 3)
            {
                throw new ArgumentException("Color must have exactly three channels.");
            }

            return new CustomizationColor(rgb[0], rgb[1], rgb[2]);
        }

        private static CustomizationScale ScaleBound(
            IEnumerable<CustomizationBodyPresetCandidate> bodies,
            bool minimum)
        {
            float x = minimum ? float.PositiveInfinity : float.NegativeInfinity;
            float y = x;
            float z = x;
            foreach (CustomizationBodyPresetCandidate body in bodies)
            {
                x = minimum ? Math.Min(x, body.Scale[0]) : Math.Max(x, body.Scale[0]);
                y = minimum ? Math.Min(y, body.Scale[1]) : Math.Max(y, body.Scale[1]);
                z = minimum ? Math.Min(z, body.Scale[2]) : Math.Max(z, body.Scale[2]);
            }

            return new CustomizationScale(x, y, z);
        }

        private static bool HasRequiredSource(CharacterCustomizationCatalogData source)
        {
            return source != null &&
                   source.schemaVersion > 0 &&
                   !string.IsNullOrEmpty(source.version) &&
                   !string.IsNullOrEmpty(source.sourceRevision) &&
                   !string.IsNullOrEmpty(source.sourceSha256) &&
                   source.bodyPresets?.Length > 0 &&
                   source.hairStyles?.Length > 0 &&
                   source.armorStyles?.Length > 0 &&
                   source.faceMarks?.Length > 0 &&
                   source.weaponStyles?.Length > 0 &&
                   source.offhandStyles?.Length > 0 &&
                   source.primaryColors?.Length > 0 &&
                   source.hairColors?.Length > 0 &&
                   source.skinColors?.Length > 0 &&
                   source.eyeColors?.Length > 0 &&
                   source.accentColors?.Length > 0 &&
                   source.forgePresets?.Length > 0;
        }

        private static IReadOnlyList<CustomizationDiagnostic> Failure(
            string code,
            string path)
        {
            return new[]
            {
                new CustomizationDiagnostic(code, path, string.Empty)
            };
        }
    }

    public static class ProceduralChampionPreviewModelCapabilities
    {
        private const string CapabilityId = "procedural_champion_preview_v1";
        private const string SourceIdentity =
            "procedural_champion_model_builder_preview_v1";

        public static ModelCapabilitySnapshot Create(
            CustomizationCatalogSnapshot catalog)
        {
            if (catalog == null)
            {
                return null;
            }

            string[] capabilities = catalog.BodyPresets
                .Select(item => item.RequiredCapabilityId)
                .Concat(catalog.Options.Select(item => item.RequiredCapabilityId))
                .Concat(catalog.Presets.SelectMany(item => item.RequiredCapabilities))
                .Concat(new[] { "model.flag.cape", "model.flag.helmet" })
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            ModelCapabilityValidationResult result =
                CustomizationModelCapabilityValidator.Validate(
                    new ModelCapabilityCandidate(
                        CapabilityId,
                        1L,
                        SourceIdentity,
                        CustomizationField.All,
                        capabilities));
            return result.IsValid ? result.Snapshot : null;
        }
    }
}
