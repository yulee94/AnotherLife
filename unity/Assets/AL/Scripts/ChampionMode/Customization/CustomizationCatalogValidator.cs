using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization.Contracts;

namespace AL.ChampionMode.Customization
{
    public static class CustomizationCatalogValidator
    {
        private static readonly HashSet<string> ColorFamilies = new HashSet<string>(
            new[]
            {
                CustomizationFamilies.PrimaryColors,
                CustomizationFamilies.HairColors,
                CustomizationFamilies.SkinColors,
                CustomizationFamilies.EyeColors,
                CustomizationFamilies.AccentColors
            },
            StringComparer.Ordinal);

        public static CustomizationCatalogValidationResult Validate(
            CustomizationCatalogCandidate candidate)
        {
            var diagnostics = new List<CustomizationDiagnostic>();
            if (candidate == null)
            {
                Error(diagnostics, "AL-CUS-CATALOG-NULL", "catalog", string.Empty);
                return Result(null, diagnostics);
            }

            ValidateIdentity(candidate.Identity, diagnostics);
            ValidateCollection(candidate.BodyPresets, "bodyPresets", diagnostics);
            ValidateCollection(candidate.Options, "options", diagnostics);
            ValidateCollection(candidate.Colors, "colors", diagnostics);
            ValidateOptionalCollection(candidate.Aliases, "aliases", diagnostics,
                CustomizationTechnicalLimits.MaximumAliases);
            ValidateCollection(candidate.Presets, "presets", diagnostics,
                CustomizationTechnicalLimits.MaximumPresets);

            if (candidate.Policy == null)
            {
                Error(diagnostics, "AL-CUS-POLICY-NULL", "catalog.policy", string.Empty);
            }

            if (diagnostics.Count != 0)
            {
                return Result(null, diagnostics);
            }

            var bodyDefinitions = new List<CustomizationBodyPresetDefinition>();
            var optionDefinitions = new List<CustomizationOptionDefinition>();
            var colorDefinitions = new List<CustomizationColorDefinition>();
            var seenOptions = new HashSet<string>(StringComparer.Ordinal);
            var seenOrders = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < candidate.BodyPresets.Count; index++)
            {
                CustomizationBodyPresetCandidate item = candidate.BodyPresets[index];
                string path = "catalog.bodyPresets[" + index + "]";
                if (item == null)
                {
                    Error(diagnostics, "AL-CUS-OPTION-NULL", path, string.Empty);
                    continue;
                }

                ValidateOptionBase(item, path, seenOptions, seenOrders, diagnostics);
                if (!TryReadScale(item.Scale, out CustomizationScale scale))
                {
                    Error(diagnostics, "AL-CUS-SCALE-SHAPE", path + ".scale", item.Id);
                }
                else if (!scale.IsFinitePositive)
                {
                    Error(diagnostics, "AL-CUS-SCALE-NUMERIC", path + ".scale", item.Id);
                }
                else if (!Within(scale, candidate.Policy.MinimumBodyScale,
                             candidate.Policy.MaximumBodyScale))
                {
                    Error(diagnostics, "AL-CUS-SCALE-BOUNDS", path + ".scale", item.Id);
                }

                if (HasRecordErrors(diagnostics, path))
                {
                    continue;
                }

                bodyDefinitions.Add(new CustomizationBodyPresetDefinition(
                    item.Id, item.ContentKey, item.RequiredCapabilityId,
                    item.Order, scale));
            }

            for (int index = 0; index < candidate.Options.Count; index++)
            {
                CustomizationOptionCandidate item = candidate.Options[index];
                string path = "catalog.options[" + index + "]";
                if (item == null)
                {
                    Error(diagnostics, "AL-CUS-OPTION-NULL", path, string.Empty);
                    continue;
                }

                if (!CustomizationFamilies.Required.Contains(item.FamilyId) ||
                    item.FamilyId == CustomizationFamilies.BodyPresets ||
                    ColorFamilies.Contains(item.FamilyId))
                {
                    Error(diagnostics, "AL-CUS-FAMILY-WRONG", path + ".familyId", item.Id);
                }

                ValidateOptionBase(item, path, seenOptions, seenOrders, diagnostics);
                if (!HasRecordErrors(diagnostics, path))
                {
                    optionDefinitions.Add(new CustomizationOptionDefinition(
                        item.FamilyId, item.Id, item.ContentKey,
                        item.RequiredCapabilityId, item.Order));
                }
            }

            for (int index = 0; index < candidate.Colors.Count; index++)
            {
                CustomizationColorCandidate item = candidate.Colors[index];
                string path = "catalog.colors[" + index + "]";
                if (item == null)
                {
                    Error(diagnostics, "AL-CUS-COLOR-NULL", path, string.Empty);
                    continue;
                }

                if (!ColorFamilies.Contains(item.FamilyId))
                {
                    Error(diagnostics, "AL-CUS-FAMILY-WRONG", path + ".familyId", item.Id);
                }

                ValidateOptionBase(item, path, seenOptions, seenOrders, diagnostics);
                if (!TryReadColor(item.Rgb, out CustomizationColor color))
                {
                    Error(diagnostics, "AL-CUS-COLOR-SHAPE", path + ".rgb", item.Id);
                }
                else if (!color.IsFiniteUnitColor)
                {
                    Error(diagnostics, "AL-CUS-COLOR-NUMERIC", path + ".rgb", item.Id);
                }

                if (!HasRecordErrors(diagnostics, path))
                {
                    colorDefinitions.Add(new CustomizationColorDefinition(
                        item.FamilyId, item.Id, item.ContentKey,
                        item.RequiredCapabilityId, item.Order, color));
                }
            }

            ValidateRequiredFamilies(seenOptions, diagnostics);
            ValidatePolicy(candidate.Policy, seenOptions, diagnostics);

            List<CustomizationAliasDefinition> aliases = ValidateAliases(
                candidate.Aliases, seenOptions, diagnostics);
            Dictionary<string, HashSet<CustomizationColor>> paletteColors =
                colorDefinitions.GroupBy(item => item.FamilyId, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => new HashSet<CustomizationColor>(
                            group.Select(item => item.Color)),
                        StringComparer.Ordinal);
            Dictionary<string, string> optionCapabilities = bodyDefinitions
                .Cast<CustomizationOptionDefinition>()
                .Concat(optionDefinitions)
                .Concat(colorDefinitions)
                .ToDictionary(
                    item => Key(item.FamilyId, item.Id),
                    item => item.RequiredCapabilityId,
                    StringComparer.Ordinal);
            List<CustomizationPresetDefinition> presets = ValidatePresets(
                candidate.Presets, seenOptions, optionCapabilities,
                paletteColors, candidate.Policy, diagnostics);

            if (diagnostics.Any(item =>
                    item.Severity == CustomizationDiagnosticSeverity.Error))
            {
                return Result(null, diagnostics);
            }

            bodyDefinitions.Sort(CompareOptions);
            optionDefinitions.Sort(CompareOptions);
            colorDefinitions.Sort(CompareOptions);
            aliases.Sort((left, right) =>
            {
                int family = string.CompareOrdinal(left.FamilyId, right.FamilyId);
                return family != 0 ? family : string.CompareOrdinal(left.OldId, right.OldId);
            });
            presets.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));

            var policy = new CustomizationPolicySnapshot(
                candidate.Policy.ApprovedDefaults,
                candidate.Policy.MinimumBodyScale,
                candidate.Policy.MaximumBodyScale,
                candidate.Policy.PlaceholderOptionIds.ToDictionary(
                    item => item.Key, item => item.Value, StringComparer.Ordinal),
                candidate.Policy.AllowCustomExactColors);
            var snapshot = new CustomizationCatalogSnapshot(
                candidate.Identity,
                bodyDefinitions,
                optionDefinitions,
                colorDefinitions,
                aliases,
                presets,
                policy);
            return Result(snapshot, diagnostics);
        }

        public static bool IsTechnicalId(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > CustomizationTechnicalLimits.MaximumIdLength ||
                value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool priorUnderscore = false;
            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool underscore = character == '_';
                if (!underscore &&
                    (character < 'a' || character > 'z') &&
                    (character < '0' || character > '9'))
                {
                    return false;
                }

                if (underscore && priorUnderscore)
                {
                    return false;
                }

                priorUnderscore = underscore;
            }

            return !priorUnderscore;
        }

        public static bool IsContentKey(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > CustomizationTechnicalLimits.MaximumContentKeyLength ||
                value[0] == '.' || value[value.Length - 1] == '.')
            {
                return false;
            }

            string[] segments = value.Split('.');
            return segments.Length >= 2 && segments.All(IsTechnicalId);
        }

        public static bool IsCapabilityId(string value)
        {
            return IsTechnicalId(value) || IsContentKey(value);
        }

        public static bool IsOperationId(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > CustomizationTechnicalLimits.MaximumContentKeyLength ||
                value.Trim().Length != value.Length)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateIdentity(
            CustomizationCatalogIdentity identity,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            if (identity == null)
            {
                Error(diagnostics, "AL-CUS-IDENTITY-NULL", "catalog.identity", string.Empty);
                return;
            }

            if (!IsTechnicalId(identity.GameId))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-GAME", "catalog.identity.gameId", string.Empty);
            }

            if (!IsTechnicalId(identity.CatalogSetId))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-SET", "catalog.identity.catalogSetId", string.Empty);
            }

            if (!string.Equals(identity.CatalogId,
                    CustomizationTechnicalLimits.ExpectedCatalogId,
                    StringComparison.Ordinal))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-CATALOG", "catalog.identity.catalogId", string.Empty);
            }

            if (!string.Equals(identity.FamilyId,
                    CustomizationTechnicalLimits.ExpectedFamilyId,
                    StringComparison.Ordinal))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-FAMILY", "catalog.identity.familyId", string.Empty);
            }

            ValidateVersion(identity.SchemaVersion, "schemaVersion", diagnostics);
            ValidateVersion(identity.ContentVersion, "contentVersion", diagnostics);
            ValidateVersion(identity.SourceRevision, "sourceRevision", diagnostics);
            if (!IsLowerSha256(identity.RawSha256))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-HASH", "catalog.identity.rawSha256", string.Empty);
            }

            if (string.IsNullOrEmpty(identity.PackagedRelativePath) ||
                identity.PackagedRelativePath.StartsWith("/", StringComparison.Ordinal) ||
                identity.PackagedRelativePath.Contains("..") ||
                identity.PackagedRelativePath.Contains("\\"))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-PATH", "catalog.identity.packagedRelativePath", string.Empty);
            }
        }

        private static void ValidateVersion(
            string value,
            string name,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64 ||
                value.Trim().Length != value.Length ||
                value.Any(character => char.IsControl(character)))
            {
                Error(diagnostics, "AL-CUS-IDENTITY-VERSION",
                    "catalog.identity." + name, string.Empty);
            }
        }

        private static bool IsLowerSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
        }

        private static void ValidateOptionBase(
            CustomizationOptionCandidate item,
            string path,
            ISet<string> seenOptions,
            ISet<string> seenOrders,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            if (!IsTechnicalId(item.Id))
            {
                Error(diagnostics, "AL-CUS-OPTION-ID", path + ".id", item.Id);
            }

            if (!IsContentKey(item.ContentKey))
            {
                Error(diagnostics, "AL-CUS-OPTION-CONTENT", path + ".contentKey", item.Id);
            }

            if (!IsCapabilityId(item.RequiredCapabilityId))
            {
                Error(diagnostics, "AL-CUS-OPTION-CAPABILITY",
                    path + ".requiredCapabilityId", item.Id);
            }

            if (item.Order < 0)
            {
                Error(diagnostics, "AL-CUS-OPTION-ORDER", path + ".order", item.Id);
            }

            string key = Key(item.FamilyId, item.Id);
            if (!seenOptions.Add(key))
            {
                Error(diagnostics, "AL-CUS-OPTION-DUPLICATE", path + ".id", item.Id);
            }

            string orderKey = Key(item.FamilyId, item.Order.ToString());
            if (!seenOrders.Add(orderKey))
            {
                Error(diagnostics, "AL-CUS-OPTION-ORDER-DUPLICATE", path + ".order", item.Id);
            }
        }

        private static void ValidateRequiredFamilies(
            ISet<string> seenOptions,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            foreach (string family in CustomizationFamilies.Required)
            {
                if (!seenOptions.Any(key => key.StartsWith(
                        family + "\u001f", StringComparison.Ordinal)))
                {
                    Error(diagnostics, "AL-CUS-FAMILY-MISSING",
                        "catalog." + family, family);
                }
            }
        }

        private static void ValidatePolicy(
            CustomizationPolicyCandidate policy,
            ISet<string> seenOptions,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            if (policy == null)
            {
                return;
            }

            if (policy.ApprovedDefaults == null)
            {
                Error(diagnostics, "AL-CUS-POLICY-DEFAULTS", "catalog.policy.defaults", string.Empty);
                return;
            }

            if (!policy.MinimumBodyScale.IsFinitePositive ||
                !policy.MaximumBodyScale.IsFinitePositive ||
                policy.MinimumBodyScale.X > policy.MaximumBodyScale.X ||
                policy.MinimumBodyScale.Y > policy.MaximumBodyScale.Y ||
                policy.MinimumBodyScale.Z > policy.MaximumBodyScale.Z)
            {
                Error(diagnostics, "AL-CUS-POLICY-SCALE-BOUNDS", "catalog.policy.bodyScale", string.Empty);
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.OptionFields))
            {
                string family = CustomizationFieldMap.Family(field);
                string id = policy.ApprovedDefaults.GetOption(field);
                if (!seenOptions.Contains(Key(family, id)))
                {
                    Error(diagnostics, "AL-CUS-POLICY-DEFAULT-MISSING",
                        "catalog.policy.defaults." + field, id);
                }
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.ColorFields))
            {
                if (!policy.ApprovedDefaults.GetColor(field).IsFiniteUnitColor)
                {
                    Error(diagnostics, "AL-CUS-POLICY-COLOR",
                        "catalog.policy.defaults." + field, string.Empty);
                }
            }

            if (policy.PlaceholderOptionIds == null)
            {
                Error(diagnostics, "AL-CUS-POLICY-PLACEHOLDERS",
                    "catalog.policy.placeholders", string.Empty);
                return;
            }

            if (policy.PlaceholderEntryCount >
                    CustomizationTechnicalLimits.MaximumPlaceholderOptions ||
                policy.PlaceholderEntryCount != policy.PlaceholderOptionIds.Count)
            {
                Error(diagnostics, "AL-CUS-POLICY-PLACEHOLDERS",
                    "catalog.policy.placeholders", string.Empty);
            }

            var requiredFamilies = new HashSet<string>(
                CustomizationFieldMap.Enumerate(CustomizationField.OptionFields)
                    .Select(CustomizationFieldMap.Family),
                StringComparer.Ordinal);
            foreach (string family in policy.PlaceholderOptionIds.Keys)
            {
                if (!requiredFamilies.Contains(family))
                {
                    Error(diagnostics, "AL-CUS-POLICY-PLACEHOLDER-UNEXPECTED",
                        "catalog.policy.placeholders." + family, family);
                }
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.OptionFields))
            {
                string family = CustomizationFieldMap.Family(field);
                if (!policy.PlaceholderOptionIds.TryGetValue(family, out string id) ||
                    !seenOptions.Contains(Key(family, id)))
                {
                    Error(diagnostics, "AL-CUS-POLICY-PLACEHOLDER-MISSING",
                        "catalog.policy.placeholders." + family, id);
                }
            }
        }

        private static List<CustomizationAliasDefinition> ValidateAliases(
            IReadOnlyList<CustomizationAliasCandidate> candidates,
            ISet<string> seenOptions,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            var result = new List<CustomizationAliasDefinition>();
            var aliases = new Dictionary<string, CustomizationAliasCandidate>(
                StringComparer.Ordinal);
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Count; index++)
            {
                CustomizationAliasCandidate item = candidates[index];
                string path = "catalog.aliases[" + index + "]";
                if (item == null)
                {
                    Error(diagnostics, "AL-CUS-ALIAS-NULL", path, string.Empty);
                    continue;
                }

                if (!CustomizationFamilies.Required.Contains(item.FamilyId) ||
                    !IsTechnicalId(item.OldId) || !IsTechnicalId(item.NewId) ||
                    string.Equals(item.OldId, item.NewId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(item.IntroducedIn))
                {
                    Error(diagnostics, "AL-CUS-ALIAS-INVALID", path, item.OldId);
                    continue;
                }

                string oldKey = Key(item.FamilyId, item.OldId);
                if (seenOptions.Contains(oldKey) || !aliases.TryAdd(oldKey, item))
                {
                    Error(diagnostics, "AL-CUS-ALIAS-COLLISION", path, item.OldId);
                    continue;
                }

                paths.Add(oldKey, path);
            }

            foreach (KeyValuePair<string, CustomizationAliasCandidate> entry in
                     aliases.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                string currentKey = entry.Key;
                CustomizationAliasCandidate current = entry.Value;
                bool requiresConfirmation = false;
                string terminalId = null;
                bool failed = false;
                while (true)
                {
                    if (!visited.Add(currentKey))
                    {
                        Error(diagnostics, "AL-CUS-ALIAS-CYCLE",
                            paths[entry.Key], entry.Value.OldId);
                        failed = true;
                        break;
                    }

                    requiresConfirmation |= current.RequiresUserConfirmation;
                    string targetKey = Key(current.FamilyId, current.NewId);
                    if (seenOptions.Contains(targetKey))
                    {
                        terminalId = current.NewId;
                        break;
                    }

                    if (!aliases.TryGetValue(targetKey, out current))
                    {
                        Error(diagnostics, "AL-CUS-ALIAS-COLLISION",
                            paths[entry.Key], entry.Value.OldId);
                        failed = true;
                        break;
                    }

                    currentKey = targetKey;
                }

                if (!failed)
                {
                    result.Add(new CustomizationAliasDefinition(
                        entry.Value.FamilyId,
                        entry.Value.OldId,
                        terminalId,
                        entry.Value.IntroducedIn,
                        requiresConfirmation));
                }
            }

            return result;
        }

        private static List<CustomizationPresetDefinition> ValidatePresets(
            IReadOnlyList<CustomizationPresetCandidate> candidates,
            ISet<string> seenOptions,
            IReadOnlyDictionary<string, string> optionCapabilities,
            IReadOnlyDictionary<string, HashSet<CustomizationColor>> paletteColors,
            CustomizationPolicyCandidate policy,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            var result = new List<CustomizationPresetDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < candidates.Count; index++)
            {
                CustomizationPresetCandidate item = candidates[index];
                string path = "catalog.presets[" + index + "]";
                if (item == null)
                {
                    Error(diagnostics, "AL-CUS-PRESET-NULL", path, string.Empty);
                    continue;
                }

                if (!IsTechnicalId(item.Id) || !ids.Add(item.Id) ||
                    !IsContentKey(item.ContentKey) || item.Values == null ||
                    item.FieldMask == CustomizationField.None ||
                    (item.FieldMask & ~CustomizationField.All) != 0)
                {
                    Error(diagnostics, "AL-CUS-PRESET-INVALID", path, item.Id);
                    continue;
                }

                ValidateValues(item.Values, item.FieldMask, path, seenOptions,
                    paletteColors, policy, diagnostics);
                IReadOnlyList<string> capabilities = item.RequiredCapabilityIds ??
                                                     Array.Empty<string>();
                if (capabilities.Count > CustomizationTechnicalLimits.MaximumCapabilities ||
                    capabilities.Any(value => !IsCapabilityId(value)) ||
                    capabilities.Distinct(StringComparer.Ordinal).Count() != capabilities.Count)
                {
                    Error(diagnostics, "AL-CUS-PRESET-CAPABILITY", path + ".capabilities", item.Id);
                }

                foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                             item.FieldMask & CustomizationField.OptionFields))
                {
                    string optionKey = Key(
                        CustomizationFieldMap.Family(field),
                        item.Values.GetOption(field));
                    if (optionCapabilities.TryGetValue(optionKey,
                            out string requiredCapability) &&
                        !capabilities.Contains(requiredCapability,
                            StringComparer.Ordinal))
                    {
                        Error(diagnostics,
                            "AL-CUS-PRESET-CAPABILITY-REFERENCE",
                            path + ".capabilities", requiredCapability);
                    }
                }

                if (!HasRecordErrors(diagnostics, path))
                {
                    result.Add(new CustomizationPresetDefinition(
                        item.Id, item.ContentKey, item.FieldMask, item.Values,
                        capabilities.OrderBy(value => value, StringComparer.Ordinal)));
                }
            }

            return result;
        }

        private static void ValidateValues(
            CustomizationValues values,
            CustomizationField mask,
            string path,
            ISet<string> seenOptions,
            IReadOnlyDictionary<string, HashSet<CustomizationColor>> paletteColors,
            CustomizationPolicyCandidate policy,
            ICollection<CustomizationDiagnostic> diagnostics)
        {
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         mask & CustomizationField.OptionFields))
            {
                string id = values.GetOption(field);
                if (!seenOptions.Contains(Key(CustomizationFieldMap.Family(field), id)))
                {
                    Error(diagnostics, "AL-CUS-PRESET-REFERENCE",
                        path + "." + field, id);
                }
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         mask & CustomizationField.ColorFields))
            {
                if (!values.GetColor(field).IsFiniteUnitColor)
                {
                    Error(diagnostics, "AL-CUS-PRESET-COLOR",
                        path + "." + field, string.Empty);
                }
                else if (policy == null || !policy.AllowCustomExactColors)
                {
                    string family = CustomizationFieldMap.Family(field);
                    if (!paletteColors.TryGetValue(family,
                            out HashSet<CustomizationColor> familyColors) ||
                        !familyColors.Contains(values.GetColor(field)))
                    {
                        Error(diagnostics, "AL-CUS-PRESET-EXACT-COLOR-DISALLOWED",
                            path + "." + field, family);
                    }
                }
            }
        }

        private static bool TryReadColor(
            IReadOnlyList<float> values,
            out CustomizationColor color)
        {
            color = default;
            if (values == null || values.Count != 3)
            {
                return false;
            }

            color = new CustomizationColor(values[0], values[1], values[2]);
            return true;
        }

        private static bool TryReadScale(
            IReadOnlyList<float> values,
            out CustomizationScale scale)
        {
            scale = default;
            if (values == null || values.Count != 3)
            {
                return false;
            }

            scale = new CustomizationScale(values[0], values[1], values[2]);
            return true;
        }

        private static bool Within(
            CustomizationScale value,
            CustomizationScale minimum,
            CustomizationScale maximum)
        {
            return minimum.IsFinitePositive && maximum.IsFinitePositive &&
                   value.X >= minimum.X && value.X <= maximum.X &&
                   value.Y >= minimum.Y && value.Y <= maximum.Y &&
                   value.Z >= minimum.Z && value.Z <= maximum.Z;
        }

        private static void ValidateCollection<T>(
            IReadOnlyList<T> values,
            string name,
            ICollection<CustomizationDiagnostic> diagnostics,
            int maximum = CustomizationTechnicalLimits.MaximumOptions)
        {
            if (values == null || values.Count == 0 || values.Count > maximum)
            {
                Error(diagnostics, "AL-CUS-COLLECTION-INVALID",
                    "catalog." + name, string.Empty);
            }
        }

        private static void ValidateOptionalCollection<T>(
            IReadOnlyList<T> values,
            string name,
            ICollection<CustomizationDiagnostic> diagnostics,
            int maximum)
        {
            if (values == null || values.Count > maximum)
            {
                Error(diagnostics, "AL-CUS-COLLECTION-INVALID",
                    "catalog." + name, string.Empty);
            }
        }

        private static bool HasRecordErrors(
            IEnumerable<CustomizationDiagnostic> diagnostics,
            string path)
        {
            return diagnostics.Any(item => item.FieldPath.StartsWith(
                path, StringComparison.Ordinal));
        }

        private static int CompareOptions(
            CustomizationOptionDefinition left,
            CustomizationOptionDefinition right)
        {
            int family = string.CompareOrdinal(left.FamilyId, right.FamilyId);
            if (family != 0) return family;
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
        }

        private static string Key(string familyId, string id)
        {
            return (familyId ?? string.Empty) + "\u001f" + (id ?? string.Empty);
        }

        private static CustomizationCatalogValidationResult Result(
            CustomizationCatalogSnapshot snapshot,
            IEnumerable<CustomizationDiagnostic> diagnostics)
        {
            return new CustomizationCatalogValidationResult(snapshot, diagnostics);
        }

        private static void Error(
            ICollection<CustomizationDiagnostic> diagnostics,
            string code,
            string path,
            string recordId)
        {
            diagnostics.Add(new CustomizationDiagnostic(code, path, recordId));
        }
    }
}
