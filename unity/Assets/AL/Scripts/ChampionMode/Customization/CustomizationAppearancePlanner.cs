using System;
using System.Collections.Generic;
using System.Linq;
using AL.ChampionMode.Customization.Contracts;

namespace AL.ChampionMode.Customization
{
    public static class CustomizationAppearancePlanner
    {
        public static AppearancePrepareResult Prepare(
            CustomizationDraft draft,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model)
        {
            if (draft?.BaseEffective == null || catalog == null ||
                draft.BaseRaw == null || draft.ProposedRaw == null ||
                (draft.ChangedFields & ~CustomizationField.All) != 0 ||
                RawDifferenceMask(draft.BaseRaw, draft.ProposedRaw) !=
                draft.ChangedFields ||
                draft.BaseEffective.RawRevision != draft.BaseRawRevision)
            {
                return Reject(AppearancePrepareStatus.RejectedInvalidDraft,
                    "AL-CUS-APPEARANCE-INPUT", "appearance");
            }

            if (model == null)
            {
                return Reject(AppearancePrepareStatus.RejectedMissingCapability,
                    "AL-CUS-APPEARANCE-MODEL", "appearance.model");
            }

            if (!string.Equals(draft.BaseCatalogFingerprint, catalog.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(draft.BaseEffective.CatalogFingerprint,
                    catalog.Fingerprint, StringComparison.Ordinal))
            {
                return Reject(AppearancePrepareStatus.RejectedStaleCatalog,
                    "AL-CUS-APPEARANCE-STALE-CATALOG", "appearance.catalog");
            }

            if (!string.Equals(draft.BaseModelFingerprint, model.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(draft.BaseEffective.ModelFingerprint,
                    model.Fingerprint, StringComparison.Ordinal))
            {
                return Reject(AppearancePrepareStatus.RejectedStaleModel,
                    "AL-CUS-APPEARANCE-STALE-MODEL", "appearance.model");
            }

            var proposedRaw = new RawCustomizationSnapshot(
                CustomizationTechnicalLimits.SupportedRawStateSchemaVersion,
                draft.BaseRawRevision,
                true,
                draft.ProposedRaw);
            CustomizationQueryResult proposedQuery =
                CustomizationCompatibilityPlanner.Resolve(
                    proposedRaw,
                    CustomizationCatalogAvailability.Ready,
                    catalog,
                    model);
            if (proposedQuery.EffectivePresentation == null ||
                proposedQuery.Status == CustomizationDomainStatus.Malformed)
            {
                return new AppearancePrepareResult(
                    AppearancePrepareStatus.RejectedInvalidDraft,
                    null,
                    proposedQuery.Diagnostics);
            }

            if (proposedQuery.Status ==
                CustomizationDomainStatus.ModelCapabilityUnavailable)
            {
                return new AppearancePrepareResult(
                    AppearancePrepareStatus.RejectedMissingCapability,
                    null,
                    proposedQuery.Diagnostics);
            }

            var operations = new List<AppearanceOperation>();
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         draft.ChangedFields))
            {
                if (FieldEquals(
                        draft.BaseEffective.Values,
                        proposedQuery.EffectivePresentation.Values,
                        field))
                {
                    continue;
                }

                string capability = string.Empty;
                if ((field & (CustomizationField.OptionFields |
                              CustomizationField.ColorFields)) != 0)
                {
                    string family = CustomizationFieldMap.Family(field);
                    string optionId = (field & CustomizationField.OptionFields) != 0
                        ? proposedQuery.EffectivePresentation.Values.GetOption(field)
                        : FindExactColorOption(
                            catalog,
                            family,
                            proposedQuery.EffectivePresentation.Values.GetColor(field));
                    if (!string.IsNullOrEmpty(optionId) &&
                        catalog.TryGetOption(family, optionId,
                            out CustomizationOptionDefinition definition))
                    {
                        capability = definition.RequiredCapabilityId;
                    }
                }

                if (!model.Supports(field, capability))
                {
                    return Reject(AppearancePrepareStatus.RejectedMissingCapability,
                        "AL-CUS-APPEARANCE-CAPABILITY", "appearance." + field,
                        capability);
                }

                operations.Add(new AppearanceOperation(field, capability, true));
            }

            var plan = new AppearancePlan(
                draft.Fingerprint,
                model.Fingerprint,
                draft.BaseEffective,
                proposedQuery.EffectivePresentation,
                draft.ProposedRawFingerprint,
                operations);
            return new AppearancePrepareResult(
                AppearancePrepareStatus.Prepared,
                plan,
                Array.Empty<CustomizationDiagnostic>());
        }

        private static string FindExactColorOption(
            CustomizationCatalogSnapshot catalog,
            string family,
            CustomizationColor color)
        {
            CustomizationColorDefinition option = catalog.GetOptions(family)
                .OfType<CustomizationColorDefinition>()
                .FirstOrDefault(item => item.Color.Equals(color));
            return option?.Id;
        }

        private static bool FieldEquals(
            CustomizationValues left,
            CustomizationValues right,
            CustomizationField field)
        {
            if ((field & CustomizationField.OptionFields) != 0)
            {
                return string.Equals(left.GetOption(field), right.GetOption(field),
                    StringComparison.Ordinal);
            }

            if ((field & CustomizationField.ColorFields) != 0)
            {
                return left.GetColor(field).Equals(right.GetColor(field));
            }

            return left.GetFlag(field) == right.GetFlag(field);
        }

        private static CustomizationField RawDifferenceMask(
            CustomizationValues left,
            CustomizationValues right)
        {
            CustomizationField differences = CustomizationField.None;
            foreach (CustomizationField field in CustomizationFieldMap.Enumerate(
                         CustomizationField.All))
            {
                if (!FieldEquals(left, right, field))
                {
                    differences |= field;
                }
            }

            return differences;
        }

        private static AppearancePrepareResult Reject(
            AppearancePrepareStatus status,
            string code,
            string path,
            string recordId = "")
        {
            return new AppearancePrepareResult(
                status,
                null,
                new[] { new CustomizationDiagnostic(code, path, recordId) });
        }
    }

    public sealed class FakeReversibleAppearanceAdapter
    {
        private EffectiveAppearanceSnapshot _current;
        private AppearancePlan _appliedPlan;
        private bool _disposed;

        public FakeReversibleAppearanceAdapter(EffectiveAppearanceSnapshot initial)
        {
            _current = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        public int FailRequiredOperationIndex { get; set; } = -1;
        public bool FailVerification { get; set; }
        public bool FailRollback { get; set; }
        public EffectiveAppearanceSnapshot Current => _current;
        public bool IsDisposed => _disposed;
        public bool HasPreview => _appliedPlan != null;

        public AppearanceApplyStatus ApplyAndVerify(
            AppearancePlan plan,
            ModelCapabilitySnapshot currentModel)
        {
            if (_disposed)
            {
                return AppearanceApplyStatus.Disposed;
            }

            if (plan == null || currentModel == null ||
                !string.Equals(plan.ModelFingerprint, currentModel.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(_current.Fingerprint, plan.Prior.Fingerprint,
                    StringComparison.Ordinal))
            {
                return AppearanceApplyStatus.RejectedStaleModel;
            }

            for (int index = 0; index < plan.Operations.Count; index++)
            {
                AppearanceOperation operation = plan.Operations[index];
                if (!currentModel.Supports(
                        operation.Field,
                        operation.RequiredCapabilityId))
                {
                    return AppearanceApplyStatus.RejectedMissingCapability;
                }

                if (operation.Required && index == FailRequiredOperationIndex)
                {
                    return AppearanceApplyStatus.FailedRequiredOperation;
                }
            }

            if (FailVerification)
            {
                return AppearanceApplyStatus.FailedVerification;
            }

            _current = plan.Proposed;
            _appliedPlan = plan;
            return AppearanceApplyStatus.AppliedAndVerified;
        }

        public AppearanceRollbackStatus Rollback(AppearancePlan plan)
        {
            if (_disposed)
            {
                return AppearanceRollbackStatus.Disposed;
            }

            if (_appliedPlan == null || plan == null ||
                !string.Equals(_appliedPlan.PlanHash, plan.PlanHash,
                    StringComparison.Ordinal))
            {
                return AppearanceRollbackStatus.NotApplied;
            }

            if (FailRollback)
            {
                return AppearanceRollbackStatus.Failed;
            }

            _current = plan.Prior;
            _appliedPlan = null;
            return AppearanceRollbackStatus.Restored;
        }

        public AppearanceRollbackStatus DisposePreview()
        {
            if (_disposed)
            {
                return AppearanceRollbackStatus.Disposed;
            }

            AppearanceRollbackStatus status = _appliedPlan == null
                ? AppearanceRollbackStatus.NotApplied
                : Rollback(_appliedPlan);
            if (status != AppearanceRollbackStatus.Failed)
            {
                _disposed = true;
            }

            return status;
        }
    }
}
