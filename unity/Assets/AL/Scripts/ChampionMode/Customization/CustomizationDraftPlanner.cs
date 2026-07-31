using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AL.ChampionMode.Customization.Contracts;

namespace AL.ChampionMode.Customization
{
    public static class CustomizationDraftPlanner
    {
        public static CustomizationDraft Create(
            string draftId,
            CustomizationQueryResult query,
            CustomizationCompatibilityResult compatibility)
        {
            if (!CustomizationCatalogValidator.IsOperationId(draftId) ||
                query?.RawCommitted?.Values == null ||
                query.EffectivePresentation == null ||
                query.Model == null ||
                compatibility?.Raw == null ||
                !string.Equals(compatibility.Raw.Fingerprint,
                    query.RawCommitted.Fingerprint, StringComparison.Ordinal))
            {
                return null;
            }

            return new CustomizationDraft(
                draftId,
                query.RawCommitted.Revision,
                query.EffectivePresentation.CatalogFingerprint,
                query.Model.Fingerprint,
                query.EffectivePresentation,
                query.RawCommitted.Values,
                query.RawCommitted.Values,
                CustomizationField.None,
                CustomizationCompatibilityPlanner.PreservedUnknownMask(compatibility),
                "draft:create");
        }

        public static CustomizationEditResult Apply(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogAvailability availability,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model,
            long currentRawRevision,
            bool disposed = false)
        {
            if (disposed)
            {
                return Reject(CustomizationEditStatus.RejectedDisposed, draft,
                    "AL-CUS-DRAFT-DISPOSED", "draft");
            }

            if (draft == null || request == null || draft.ProposedRaw == null)
            {
                return Reject(CustomizationEditStatus.RejectedInvalidRequest, draft,
                    "AL-CUS-DRAFT-REQUEST", "draft");
            }

            if (availability == CustomizationCatalogAvailability.Pending)
            {
                return Reject(CustomizationEditStatus.RejectedCatalogPending, draft,
                    "AL-CUS-DRAFT-CATALOG-PENDING", "catalog");
            }

            if (availability != CustomizationCatalogAvailability.Ready || catalog == null)
            {
                return Reject(CustomizationEditStatus.RejectedCatalogInvalid, draft,
                    "AL-CUS-DRAFT-CATALOG-INVALID", "catalog");
            }

            if (model == null || currentRawRevision != draft.BaseRawRevision ||
                !string.Equals(catalog.Fingerprint, draft.BaseCatalogFingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(model.Fingerprint, draft.BaseModelFingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(CustomizationEditStatus.RejectedStaleDraft, draft,
                    "AL-CUS-DRAFT-STALE", "draft.revisions");
            }

            switch (request.Kind)
            {
                case CustomizationEditKind.SelectOption:
                    return SelectOption(draft, request, catalog, model);
                case CustomizationEditKind.SelectExactColor:
                    return SelectExactColor(draft, request, catalog, model);
                case CustomizationEditKind.SelectPaletteColor:
                    return SelectPaletteColor(draft, request, catalog, model);
                case CustomizationEditKind.SetFlag:
                    return SetFlag(draft, request, model);
                case CustomizationEditKind.ApplyPreset:
                    return ApplyPreset(draft, request, catalog, model);
                case CustomizationEditKind.ResetToApprovedDefaults:
                    return ApplyApprovedDefaults(
                        draft, request, catalog, model);
                case CustomizationEditKind.RandomizeWithSeed:
                    return Randomize(draft, request, catalog, model);
                default:
                    return Reject(CustomizationEditStatus.RejectedInvalidRequest, draft,
                        "AL-CUS-DRAFT-KIND", "request.kind");
            }
        }

        public static CustomizationCommitPlan BuildCommitPlan(
            string operationId,
            CustomizationDraft draft,
            AppearancePlan appearancePlan,
            long expectedSaveCandidateRevision)
        {
            if (!CustomizationCatalogValidator.IsOperationId(operationId) ||
                draft == null || appearancePlan == null ||
                draft.ChangedFields == CustomizationField.None ||
                expectedSaveCandidateRevision < 0L ||
                !string.Equals(draft.BaseModelFingerprint,
                    appearancePlan.ModelFingerprint, StringComparison.Ordinal) ||
                !string.Equals(draft.Fingerprint,
                    appearancePlan.PlanId, StringComparison.Ordinal) ||
                appearancePlan.Prior == null ||
                appearancePlan.Proposed == null ||
                !string.Equals(draft.BaseEffective?.Fingerprint,
                    appearancePlan.Prior.Fingerprint, StringComparison.Ordinal) ||
                !string.Equals(appearancePlan.ProposedRawFingerprint,
                    draft.ProposedRawFingerprint, StringComparison.Ordinal) ||
                appearancePlan.Proposed.Values == null ||
                !MatchesFields(
                    draft.ProposedRaw,
                    appearancePlan.Proposed.Values,
                    draft.ChangedFields) ||
                appearancePlan.Proposed.RawRevision != draft.BaseRawRevision ||
                !string.Equals(appearancePlan.Proposed.CatalogFingerprint,
                    draft.BaseCatalogFingerprint, StringComparison.Ordinal))
            {
                return null;
            }

            return new CustomizationCommitPlan(
                operationId, draft, appearancePlan, expectedSaveCandidateRevision);
        }

        private static CustomizationEditResult SelectOption(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            if (!CustomizationFieldMap.IsSingle(request.Field) ||
                (request.Field & CustomizationField.OptionFields) == 0)
            {
                return Reject(CustomizationEditStatus.RejectedWrongFamily, draft,
                    "AL-CUS-DRAFT-FAMILY", "request.field");
            }

            string family = CustomizationFieldMap.Family(request.Field);
            if (!catalog.TryGetOption(family, request.ValueId,
                    out CustomizationOptionDefinition option))
            {
                return Reject(CustomizationEditStatus.RejectedUnknownOption, draft,
                    "AL-CUS-DRAFT-OPTION", "request.optionId", request.ValueId);
            }

            if (!model.Supports(request.Field, option.RequiredCapabilityId))
            {
                return Reject(CustomizationEditStatus.RejectedUnavailableCapability,
                    draft, "AL-CUS-DRAFT-CAPABILITY", "request.optionId",
                    option.RequiredCapabilityId);
            }

            return ApplyValues(
                draft,
                draft.ProposedRaw.WithOption(request.Field, option.Id),
                request.Field,
                request.ConfirmPreservedUnknownReplacement,
                "option:" + family + ":" + option.Id);
        }

        private static CustomizationEditResult SelectExactColor(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            if (!CustomizationFieldMap.IsSingle(request.Field) ||
                (request.Field & CustomizationField.ColorFields) == 0)
            {
                return Reject(CustomizationEditStatus.RejectedWrongFamily, draft,
                    "AL-CUS-DRAFT-FAMILY", "request.field");
            }

            if (!catalog.Policy.AllowCustomExactColors ||
                !request.Color.IsFiniteUnitColor)
            {
                return Reject(CustomizationEditStatus.RejectedNumericInvalid, draft,
                    "AL-CUS-DRAFT-COLOR", "request.color");
            }

            if (!model.Supports(request.Field, string.Empty))
            {
                return Reject(CustomizationEditStatus.RejectedUnavailableCapability,
                    draft, "AL-CUS-DRAFT-CAPABILITY", "request.field");
            }

            return ApplyValues(
                draft,
                draft.ProposedRaw.WithColor(request.Field, request.Color),
                request.Field,
                true,
                "color:exact:" + request.Field);
        }

        private static CustomizationEditResult SelectPaletteColor(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            if (!CustomizationFieldMap.IsSingle(request.Field) ||
                (request.Field & CustomizationField.ColorFields) == 0)
            {
                return Reject(CustomizationEditStatus.RejectedWrongFamily, draft,
                    "AL-CUS-DRAFT-FAMILY", "request.field");
            }

            string family = CustomizationFieldMap.Family(request.Field);
            if (!catalog.TryGetOption(family, request.ValueId,
                    out CustomizationOptionDefinition option) ||
                !(option is CustomizationColorDefinition color))
            {
                return Reject(CustomizationEditStatus.RejectedUnknownOption, draft,
                    "AL-CUS-DRAFT-PALETTE", "request.optionId", request.ValueId);
            }

            if (!model.Supports(request.Field, option.RequiredCapabilityId))
            {
                return Reject(CustomizationEditStatus.RejectedUnavailableCapability,
                    draft, "AL-CUS-DRAFT-CAPABILITY", "request.optionId",
                    option.RequiredCapabilityId);
            }

            return ApplyValues(
                draft,
                draft.ProposedRaw.WithColor(request.Field, color.Color),
                request.Field,
                true,
                "color:palette:" + family + ":" + color.Id);
        }

        private static CustomizationEditResult SetFlag(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            ModelCapabilitySnapshot model)
        {
            if (!CustomizationFieldMap.IsSingle(request.Field) ||
                (request.Field & CustomizationField.FlagFields) == 0)
            {
                return Reject(CustomizationEditStatus.RejectedWrongFamily, draft,
                    "AL-CUS-DRAFT-FAMILY", "request.field");
            }

            if (!model.Supports(request.Field, string.Empty))
            {
                return Reject(CustomizationEditStatus.RejectedUnavailableCapability,
                    draft, "AL-CUS-DRAFT-CAPABILITY", "request.field");
            }

            return ApplyValues(
                draft,
                draft.ProposedRaw.WithFlag(request.Field, request.FlagValue),
                request.Field,
                true,
                "flag:" + request.Field + ":" +
                (request.FlagValue ? "1" : "0"));
        }

        private static CustomizationEditResult ApplyPreset(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            if (!catalog.TryGetPreset(request.ValueId,
                    out CustomizationPresetDefinition preset))
            {
                return Reject(CustomizationEditStatus.RejectedUnknownOption, draft,
                    "AL-CUS-DRAFT-PRESET", "request.presetId", request.ValueId);
            }

            foreach (string capability in preset.RequiredCapabilities)
            {
                if (!model.Capabilities.Contains(capability))
                {
                    return Reject(
                        CustomizationEditStatus.RejectedUnavailableCapability,
                        draft, "AL-CUS-DRAFT-CAPABILITY", "request.presetId",
                        capability);
                }
            }

            if (!SupportsValues(
                    preset.Values, preset.FieldMask, catalog, model,
                    out string missingCapability))
            {
                return Reject(
                    CustomizationEditStatus.RejectedUnavailableCapability,
                    draft, "AL-CUS-DRAFT-CAPABILITY", "request.presetId",
                    missingCapability);
            }

            return ApplyValues(
                draft,
                preset.Values,
                preset.FieldMask,
                request.ConfirmPreservedUnknownReplacement,
                "preset:" + preset.Id);
        }

        private static CustomizationEditResult ApplyApprovedDefaults(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            if (!SupportsValues(
                    catalog.Policy.ApprovedDefaults,
                    CustomizationField.All,
                    catalog,
                    model,
                    out string missingCapability))
            {
                return Reject(
                    CustomizationEditStatus.RejectedUnavailableCapability,
                    draft, "AL-CUS-DRAFT-CAPABILITY", "request.reset",
                    missingCapability);
            }

            return ApplyValues(
                draft,
                catalog.Policy.ApprovedDefaults,
                CustomizationField.All,
                request.ConfirmPreservedUnknownReplacement,
                "reset:approved_defaults");
        }

        private static CustomizationEditResult Randomize(
            CustomizationDraft draft,
            CustomizationEditRequest request,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            CustomizationField allowed = request.AllowedFields & CustomizationField.All;
            if (allowed == CustomizationField.None)
            {
                return Reject(CustomizationEditStatus.RejectedInvalidRequest, draft,
                    "AL-CUS-DRAFT-RANDOM-MASK", "request.allowedFields");
            }

            if (!request.ConfirmPreservedUnknownReplacement &&
                (allowed & draft.PreservedUnknownFields) != 0)
            {
                return Reject(
                    CustomizationEditStatus.RejectedPreservedUnknownReplacementNeedsConfirmation,
                    draft, "AL-CUS-DRAFT-PRESERVED-UNKNOWN",
                    "request.allowedFields");
            }

            ulong state = unchecked((ulong)request.Seed) ^ 0x9e3779b97f4a7c15UL;
            if (state == 0UL)
            {
                state = 0xd1b54a32d192ed03UL;
            }

            CustomizationValues values = draft.ProposedRaw;
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(allowed))
            {
                if (!model.Supports(field, string.Empty))
                {
                    return Reject(
                        CustomizationEditStatus.RejectedUnavailableCapability,
                        draft, "AL-CUS-DRAFT-CAPABILITY", "request.allowedFields",
                        field.ToString());
                }

                if ((field & CustomizationField.OptionFields) != 0)
                {
                    CustomizationOptionDefinition[] options = catalog
                        .GetOptions(CustomizationFieldMap.Family(field))
                        .Where(option => model.Supports(field,
                            option.RequiredCapabilityId)).ToArray();
                    if (options.Length == 0)
                    {
                        return Reject(
                            CustomizationEditStatus.RejectedUnavailableCapability,
                            draft, "AL-CUS-DRAFT-CAPABILITY", "request.allowedFields",
                            field.ToString());
                    }

                    values = values.WithOption(field,
                        options[NextIndex(ref state, options.Length)].Id);
                }
                else if ((field & CustomizationField.ColorFields) != 0)
                {
                    CustomizationColorDefinition[] colors = catalog
                        .GetOptions(CustomizationFieldMap.Family(field))
                        .OfType<CustomizationColorDefinition>()
                        .Where(option => model.Supports(field,
                            option.RequiredCapabilityId)).ToArray();
                    if (colors.Length == 0)
                    {
                        return Reject(
                            CustomizationEditStatus.RejectedUnavailableCapability,
                            draft, "AL-CUS-DRAFT-CAPABILITY", "request.allowedFields",
                            field.ToString());
                    }

                    values = values.WithColor(field,
                        colors[NextIndex(ref state, colors.Length)].Color);
                }
                else
                {
                    values = values.WithFlag(field, NextIndex(ref state, 2) == 1);
                }
            }

            return ApplyValues(
                draft,
                values,
                allowed,
                request.ConfirmPreservedUnknownReplacement,
                "random:" + request.Seed.ToString(CultureInfo.InvariantCulture) +
                ":" + ((int)allowed).ToString(CultureInfo.InvariantCulture));
        }

        private static bool SupportsValues(
            CustomizationValues values,
            CustomizationField mask,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model,
            out string missingCapability)
        {
            missingCapability = string.Empty;
            if (values == null || catalog == null || model == null)
            {
                missingCapability = "model_or_catalog_unavailable";
                return false;
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(mask))
            {
                string capability = string.Empty;
                if ((field & CustomizationField.OptionFields) != 0)
                {
                    string family = CustomizationFieldMap.Family(field);
                    if (!catalog.TryGetOption(
                            family,
                            values.GetOption(field),
                            out CustomizationOptionDefinition option))
                    {
                        missingCapability = family;
                        return false;
                    }

                    capability = option.RequiredCapabilityId;
                }

                if (!model.Supports(field, capability))
                {
                    missingCapability = string.IsNullOrEmpty(capability)
                        ? field.ToString()
                        : capability;
                    return false;
                }
            }

            return true;
        }

        private static CustomizationEditResult ApplyValues(
            CustomizationDraft draft,
            CustomizationValues source,
            CustomizationField mask,
            bool confirmUnknown,
            string provenance)
        {
            if (source == null || mask == CustomizationField.None ||
                (mask & ~CustomizationField.All) != 0)
            {
                return Reject(CustomizationEditStatus.RejectedInvalidRequest, draft,
                    "AL-CUS-DRAFT-PATCH", "request.mask");
            }

            if (!confirmUnknown && (mask & draft.PreservedUnknownFields) != 0)
            {
                return Reject(
                    CustomizationEditStatus.RejectedPreservedUnknownReplacementNeedsConfirmation,
                    draft, "AL-CUS-DRAFT-PRESERVED-UNKNOWN", "request.mask");
            }

            CustomizationValues next = draft.ProposedRaw;
            CustomizationField actualChanges = CustomizationField.None;
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(mask))
            {
                if ((field & CustomizationField.OptionFields) != 0)
                {
                    string value = source.GetOption(field);
                    if (!string.Equals(next.GetOption(field), value,
                            StringComparison.Ordinal))
                    {
                        next = next.WithOption(field, value);
                        actualChanges |= field;
                    }
                }
                else if ((field & CustomizationField.ColorFields) != 0)
                {
                    CustomizationColor value = source.GetColor(field);
                    if (!next.GetColor(field).Equals(value))
                    {
                        next = next.WithColor(field, value);
                        actualChanges |= field;
                    }
                }
                else
                {
                    bool value = source.GetFlag(field);
                    if (next.GetFlag(field) != value)
                    {
                        next = next.WithFlag(field, value);
                        actualChanges |= field;
                    }
                }
            }

            if (actualChanges == CustomizationField.None)
            {
                return new CustomizationEditResult(
                    CustomizationEditStatus.NoChange, draft,
                    Array.Empty<CustomizationDiagnostic>());
            }

            var nextDraft = new CustomizationDraft(
                draft.DraftId,
                draft.BaseRawRevision,
                draft.BaseCatalogFingerprint,
                draft.BaseModelFingerprint,
                draft.BaseEffective,
                draft.BaseRaw,
                next,
                DifferenceMask(
                    draft.BaseRaw,
                    next,
                    CustomizationField.All),
                draft.BasePreservedUnknownFields,
                provenance);
            return new CustomizationEditResult(
                CustomizationEditStatus.AppliedToDraft,
                nextDraft,
                Array.Empty<CustomizationDiagnostic>());
        }

        private static CustomizationField DifferenceMask(
            CustomizationValues left,
            CustomizationValues right,
            CustomizationField mask)
        {
            if (left == null || right == null)
            {
                return mask & CustomizationField.All;
            }

            CustomizationField differences = CustomizationField.None;
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(mask))
            {
                if ((field & CustomizationField.OptionFields) != 0)
                {
                    if (!string.Equals(left.GetOption(field), right.GetOption(field),
                            StringComparison.Ordinal))
                    {
                        differences |= field;
                    }
                }
                else if ((field & CustomizationField.ColorFields) != 0)
                {
                    if (!left.GetColor(field).Equals(right.GetColor(field)))
                    {
                        differences |= field;
                    }
                }
                else if (left.GetFlag(field) != right.GetFlag(field))
                {
                    differences |= field;
                }
            }

            return differences;
        }

        private static bool MatchesFields(
            CustomizationValues expected,
            CustomizationValues actual,
            CustomizationField mask)
        {
            if (expected == null || actual == null)
            {
                return false;
            }

            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(mask))
            {
                if ((field & CustomizationField.OptionFields) != 0)
                {
                    if (!string.Equals(expected.GetOption(field),
                            actual.GetOption(field), StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
                else if ((field & CustomizationField.ColorFields) != 0)
                {
                    if (!expected.GetColor(field).Equals(actual.GetColor(field)))
                    {
                        return false;
                    }
                }
                else if (expected.GetFlag(field) != actual.GetFlag(field))
                {
                    return false;
                }
            }

            return true;
        }

        private static int NextIndex(ref ulong state, int count)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            ulong value = state * 2685821657736338717UL;
            return (int)(value % (uint)count);
        }

        private static CustomizationEditResult Reject(
            CustomizationEditStatus status,
            CustomizationDraft draft,
            string code,
            string path,
            string recordId = "")
        {
            return new CustomizationEditResult(
                status,
                draft,
                new[] { new CustomizationDiagnostic(code, path, recordId) });
        }
    }
}
