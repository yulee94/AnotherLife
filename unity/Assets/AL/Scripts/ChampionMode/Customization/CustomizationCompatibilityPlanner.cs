using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization.Contracts;

namespace AL.ChampionMode.Customization
{
    public static class CustomizationCompatibilityPlanner
    {
        public static CustomizationCompatibilityResult Classify(
            RawCustomizationSnapshot raw,
            CustomizationCatalogAvailability availability,
            CustomizationCatalogSnapshot catalog)
        {
            var fields = new Dictionary<CustomizationField, CustomizationFieldCompatibility>();
            var diagnostics = new List<CustomizationDiagnostic>();
            if (raw == null || raw.Values == null || raw.Revision < 0L)
            {
                diagnostics.Add(Diagnostic("AL-CUS-RAW-MISSING", "raw", string.Empty));
                return new CustomizationCompatibilityResult(
                    CustomizationDomainStatus.Malformed,
                    raw,
                    fields,
                    diagnostics);
            }

            if (raw.SchemaVersion > CustomizationTechnicalLimits.SupportedRawStateSchemaVersion)
            {
                foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                             CustomizationField.All))
                {
                    fields[field] = new CustomizationFieldCompatibility(
                        field,
                        CustomizationFieldStatus.RawUnsupportedFutureSchema,
                        RawIdentity(raw.Values, field),
                        null);
                }

                diagnostics.Add(Diagnostic(
                    "AL-CUS-RAW-FUTURE-SCHEMA", "raw.schemaVersion", string.Empty));
                return new CustomizationCompatibilityResult(
                    CustomizationDomainStatus.FutureSchemaUnsupported,
                    raw,
                    fields,
                    diagnostics);
            }

            bool malformed = raw.SchemaVersion <= 0;
            if (malformed)
            {
                diagnostics.Add(Diagnostic(
                    "AL-CUS-RAW-SCHEMA-INVALID", "raw.schemaVersion",
                    raw.SchemaVersion.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }

            bool alias = false;
            bool unknown = false;
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.OptionFields))
            {
                string rawId = raw.Values.GetOption(field);
                string family = CustomizationFieldMap.Family(field);
                if (!CustomizationCatalogValidator.IsTechnicalId(rawId))
                {
                    malformed = true;
                    fields[field] = new CustomizationFieldCompatibility(
                        field, CustomizationFieldStatus.RawBlankInvalid,
                        rawId, null);
                    diagnostics.Add(Diagnostic(
                        "AL-CUS-RAW-OPTION-INVALID", "raw." + field, rawId));
                    continue;
                }

                if (availability != CustomizationCatalogAvailability.Ready ||
                    catalog == null)
                {
                    fields[field] = new CustomizationFieldCompatibility(
                        field,
                        availability == CustomizationCatalogAvailability.Pending
                            ? CustomizationFieldStatus.UnavailableCatalogPending
                            : CustomizationFieldStatus.UnavailableCatalogInvalid,
                        rawId,
                        null);
                    continue;
                }

                if (catalog.TryGetOption(family, rawId, out _))
                {
                    fields[field] = new CustomizationFieldCompatibility(
                        field, CustomizationFieldStatus.RawValidResolved,
                        rawId, rawId);
                }
                else if (catalog.TryGetAlias(family, rawId,
                             out CustomizationAliasDefinition aliasDefinition))
                {
                    alias = true;
                    fields[field] = new CustomizationFieldCompatibility(
                        field, CustomizationFieldStatus.RawValidAliasAvailable,
                        rawId, aliasDefinition.NewId);
                }
                else
                {
                    unknown = true;
                    fields[field] = new CustomizationFieldCompatibility(
                        field, CustomizationFieldStatus.RawPreservedUnknown,
                        rawId, null);
                    diagnostics.Add(Diagnostic(
                        "AL-CUS-RAW-PRESERVED-UNKNOWN", "raw." + field, rawId,
                        CustomizationDiagnosticSeverity.Warning));
                }
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.ColorFields))
            {
                CustomizationColor color = raw.Values.GetColor(field);
                if (color.IsFiniteUnitColor)
                {
                    fields[field] = new CustomizationFieldCompatibility(
                        field, CustomizationFieldStatus.RawValidResolved,
                        color.CanonicalText(), null);
                }
                else
                {
                    malformed = true;
                    fields[field] = new CustomizationFieldCompatibility(
                        field, CustomizationFieldStatus.RawNumericInvalid,
                        "invalid", null);
                    diagnostics.Add(Diagnostic(
                        "AL-CUS-RAW-COLOR-INVALID", "raw." + field, string.Empty));
                }
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.FlagFields))
            {
                fields[field] = new CustomizationFieldCompatibility(
                    field, CustomizationFieldStatus.RawValidResolved,
                    raw.Values.GetFlag(field) ? "true" : "false", null);
            }

            CustomizationDomainStatus status;
            if (malformed)
            {
                status = CustomizationDomainStatus.Malformed;
            }
            else if (availability == CustomizationCatalogAvailability.Pending)
            {
                status = CustomizationDomainStatus.CatalogPending;
            }
            else if (availability != CustomizationCatalogAvailability.Ready || catalog == null)
            {
                status = CustomizationDomainStatus.CatalogUnavailable;
            }
            else if (alias)
            {
                status = CustomizationDomainStatus.NeedsAliasMigration;
            }
            else if (unknown)
            {
                status = CustomizationDomainStatus.PreservedUnknown;
            }
            else
            {
                status = raw.HasCatalogMetadata
                    ? CustomizationDomainStatus.Valid
                    : CustomizationDomainStatus.ValidLegacyNoMetadata;
            }

            return new CustomizationCompatibilityResult(
                status, raw, fields, diagnostics);
        }

        public static CustomizationQueryResult Resolve(
            RawCustomizationSnapshot raw,
            CustomizationCatalogAvailability availability,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model,
            EffectiveAppearanceSnapshot retainedPrior = null)
        {
            CustomizationCompatibilityResult compatibility = Classify(
                raw, availability, catalog);
            var diagnostics = compatibility.Diagnostics.ToList();

            if (catalog == null || availability != CustomizationCatalogAvailability.Ready)
            {
                EffectiveAppearanceSnapshot retained =
                    retainedPrior != null && model != null &&
                    string.Equals(retainedPrior.ModelFingerprint, model.Fingerprint,
                        StringComparison.Ordinal)
                        ? retainedPrior
                        : null;
                return new CustomizationQueryResult(
                    compatibility.Status,
                    raw,
                    retained,
                    catalog?.Identity,
                    model,
                    diagnostics);
            }

            if (raw?.Values == null || model == null)
            {
                diagnostics.Add(Diagnostic(
                    "AL-CUS-MODEL-UNAVAILABLE", "model", string.Empty));
                return new CustomizationQueryResult(
                    model == null
                        ? CustomizationDomainStatus.ModelCapabilityUnavailable
                        : compatibility.Status,
                    raw,
                    null,
                    catalog.Identity,
                    model,
                    diagnostics);
            }

            CustomizationValues values = raw.Values;
            var statuses = new Dictionary<CustomizationField, CustomizationFieldStatus>();
            bool missingCapability = false;

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.OptionFields))
            {
                CustomizationFieldCompatibility fieldCompatibility =
                    compatibility.Fields[field];
                string family = CustomizationFieldMap.Family(field);
                string selectedId = fieldCompatibility.ResolvedId;
                CustomizationFieldStatus status = fieldCompatibility.Status;

                if (string.IsNullOrEmpty(selectedId) ||
                    !catalog.TryGetOption(family, selectedId,
                        out CustomizationOptionDefinition option))
                {
                    if (!catalog.Policy.TryGetPlaceholder(family, out selectedId) ||
                        !catalog.TryGetOption(family, selectedId, out option))
                    {
                        diagnostics.Add(Diagnostic(
                            "AL-CUS-PLACEHOLDER-UNAVAILABLE", "effective." + field,
                            fieldCompatibility.RawId));
                        continue;
                    }

                    status = CustomizationFieldStatus.EffectivePlaceholder;
                }

                if (!model.Supports(field, option.RequiredCapabilityId))
                {
                    missingCapability = true;
                    status = CustomizationFieldStatus.UnavailableMissingCapability;
                    if (catalog.Policy.TryGetPlaceholder(family, out string placeholderId) &&
                        catalog.TryGetOption(family, placeholderId,
                            out CustomizationOptionDefinition placeholder) &&
                        model.Supports(field, placeholder.RequiredCapabilityId))
                    {
                        selectedId = placeholderId;
                    }
                    else
                    {
                        diagnostics.Add(Diagnostic(
                            "AL-CUS-CAPABILITY-UNAVAILABLE", "effective." + field,
                            option.RequiredCapabilityId));
                    }
                }

                values = values.WithOption(field, selectedId);
                statuses[field] = status;
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.ColorFields))
            {
                CustomizationColor rawColor = raw.Values.GetColor(field);
                CustomizationColor effectiveColor = rawColor.IsFiniteUnitColor
                    ? rawColor
                    : catalog.Policy.ApprovedDefaults.GetColor(field);
                CustomizationFieldStatus status = rawColor.IsFiniteUnitColor
                    ? CustomizationFieldStatus.RawValidResolved
                    : CustomizationFieldStatus.EffectivePlaceholder;
                if (!model.Supports(field, string.Empty))
                {
                    missingCapability = true;
                    status = CustomizationFieldStatus.UnavailableMissingCapability;
                    effectiveColor = catalog.Policy.ApprovedDefaults.GetColor(field);
                }

                values = values.WithColor(field, effectiveColor);
                statuses[field] = status;
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.FlagFields))
            {
                bool flag = raw.Values.GetFlag(field);
                CustomizationFieldStatus status = CustomizationFieldStatus.RawValidResolved;
                if (!model.Supports(field, string.Empty))
                {
                    missingCapability = true;
                    status = CustomizationFieldStatus.UnavailableMissingCapability;
                    flag = catalog.Policy.ApprovedDefaults.GetFlag(field);
                }

                values = values.WithFlag(field, flag);
                statuses[field] = status;
            }

            CustomizationScale scale = catalog.Policy.MinimumBodyScale;
            if (catalog.TryGetOption(
                    CustomizationFamilies.BodyPresets,
                    values.BodyPresetId,
                    out CustomizationOptionDefinition body) &&
                body is CustomizationBodyPresetDefinition bodyPreset)
            {
                scale = bodyPreset.Scale;
            }

            var effective = new EffectiveAppearanceSnapshot(
                values,
                scale,
                statuses,
                catalog.Fingerprint,
                model.Fingerprint,
                raw.Revision);
            return new CustomizationQueryResult(
                missingCapability
                    ? CustomizationDomainStatus.ModelCapabilityUnavailable
                    : compatibility.Status,
                raw,
                effective,
                catalog.Identity,
                model,
                diagnostics);
        }

        public static CustomizationField PreservedUnknownMask(
            CustomizationCompatibilityResult result)
        {
            if (result == null)
            {
                return CustomizationField.None;
            }

            CustomizationField mask = CustomizationField.None;
            foreach (KeyValuePair<CustomizationField, CustomizationFieldCompatibility> item
                     in result.Fields)
            {
                if (item.Value.Status == CustomizationFieldStatus.RawPreservedUnknown)
                {
                    mask |= item.Key;
                }
            }

            return mask;
        }

        private static string RawIdentity(
            CustomizationValues values,
            CustomizationField field)
        {
            if ((field & CustomizationField.OptionFields) != 0)
            {
                return values.GetOption(field);
            }

            if ((field & CustomizationField.ColorFields) != 0)
            {
                return values.GetColor(field).CanonicalText();
            }

            return values.GetFlag(field) ? "true" : "false";
        }

        private static CustomizationDiagnostic Diagnostic(
            string code,
            string path,
            string recordId,
            CustomizationDiagnosticSeverity severity = CustomizationDiagnosticSeverity.Error)
        {
            return new CustomizationDiagnostic(code, path, recordId, severity);
        }
    }

    internal static class CustomizationColorCompatibilityExtensions
    {
        internal static string CanonicalText(this CustomizationColor color)
        {
            return color.Red.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "," + color.Green.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                   "," + color.Blue.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
