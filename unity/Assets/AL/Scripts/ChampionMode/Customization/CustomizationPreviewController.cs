using System;
using AL.ChampionMode.Customization.Contracts;
using AL.Data.Runtime;

namespace AL.ChampionMode.Customization
{
    public sealed class CustomizationPreviewResult
    {
        internal CustomizationPreviewResult(
            bool succeeded,
            CustomizationEditResult edit,
            AppearancePrepareResult preparation,
            AppearanceApplyStatus applyStatus,
            AppearanceRollbackStatus rollbackStatus)
        {
            Succeeded = succeeded;
            Edit = edit;
            Preparation = preparation;
            ApplyStatus = applyStatus;
            RollbackStatus = rollbackStatus;
        }

        public bool Succeeded { get; }
        public CustomizationEditResult Edit { get; }
        public AppearancePrepareResult Preparation { get; }
        public AppearanceApplyStatus ApplyStatus { get; }
        public AppearanceRollbackStatus RollbackStatus { get; }
    }

    public sealed class CustomizationPreviewController
    {
        private readonly CustomizationCatalogSnapshot _catalog;
        private readonly ModelCapabilitySnapshot _model;
        private readonly IReversibleAppearanceAdapter _appearance;
        private readonly CustomizationDraft _initialDraft;
        private AppearancePlan _activePlan;
        private bool _disposed;

        public CustomizationPreviewController(
            CustomizationDraft draft,
            CustomizationCatalogSnapshot catalog,
            ModelCapabilitySnapshot model,
            IReversibleAppearanceAdapter appearance)
        {
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _initialDraft = draft;
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            if (!string.Equals(
                    draft.BaseCatalogFingerprint,
                    catalog.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    draft.BaseModelFingerprint,
                    model.Fingerprint,
                    StringComparison.Ordinal) ||
                appearance.Current == null ||
                !string.Equals(
                    draft.BaseEffective.Fingerprint,
                    appearance.Current.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Draft, catalog, model, and appearance must share one snapshot boundary.");
            }
        }

        public CustomizationDraft Draft { get; private set; }
        public bool HasPreview => _activePlan != null;
        public bool IsDisposed => _disposed;

        public CustomizationPreviewResult Preview(
            CustomizationEditRequest request)
        {
            if (_disposed)
            {
                CustomizationEditResult disposed = CustomizationDraftPlanner.Apply(
                    Draft,
                    request,
                    CustomizationCatalogAvailability.Ready,
                    _catalog,
                    _model,
                    Draft.BaseRawRevision,
                    true);
                return Result(false, disposed, null,
                    AppearanceApplyStatus.Disposed,
                    AppearanceRollbackStatus.Disposed);
            }

            if (_activePlan != null)
            {
                AppearanceRollbackStatus priorRollback =
                    _appearance.Rollback(_activePlan);
                if (priorRollback != AppearanceRollbackStatus.Restored)
                {
                    return Result(false, null, null,
                        AppearanceApplyStatus.FailedVerification,
                        priorRollback);
                }

                _activePlan = null;
            }

            CustomizationEditResult edit = CustomizationDraftPlanner.Apply(
                Draft,
                request,
                CustomizationCatalogAvailability.Ready,
                _catalog,
                _model,
                Draft.BaseRawRevision);
            if (edit.Status == CustomizationEditStatus.NoChange)
            {
                return Result(true, edit, null,
                    AppearanceApplyStatus.AppliedAndVerified,
                    AppearanceRollbackStatus.NotApplied);
            }

            if (edit.Status != CustomizationEditStatus.AppliedToDraft ||
                edit.Draft == null)
            {
                Draft = _initialDraft;
                return Result(false, edit, null,
                    AppearanceApplyStatus.RejectedStaleModel,
                    AppearanceRollbackStatus.NotApplied);
            }

            AppearancePrepareResult preparation =
                CustomizationAppearancePlanner.Prepare(
                    edit.Draft,
                    _catalog,
                    _model);
            if (preparation.Status != AppearancePrepareStatus.Prepared ||
                preparation.Plan == null)
            {
                Draft = _initialDraft;
                return Result(false, edit, preparation,
                    AppearanceApplyStatus.RejectedMissingCapability,
                    AppearanceRollbackStatus.NotApplied);
            }

            AppearanceApplyStatus applied = _appearance.ApplyAndVerify(
                preparation.Plan,
                _model);
            if (applied != AppearanceApplyStatus.AppliedAndVerified)
            {
                AppearanceRollbackStatus rollback =
                    _appearance.Rollback(preparation.Plan);
                Draft = _initialDraft;
                return Result(false, edit, preparation, applied, rollback);
            }

            Draft = edit.Draft;
            _activePlan = preparation.Plan;
            return Result(true, edit, preparation, applied,
                AppearanceRollbackStatus.NotApplied);
        }

        public AppearanceRollbackStatus Cancel()
        {
            if (_disposed)
            {
                return AppearanceRollbackStatus.Disposed;
            }

            if (_activePlan == null)
            {
                return AppearanceRollbackStatus.NotApplied;
            }

            AppearanceRollbackStatus status = _appearance.Rollback(_activePlan);
            if (status == AppearanceRollbackStatus.Restored)
            {
                _activePlan = null;
                Draft = _initialDraft;
            }

            return status;
        }

        public AppearanceRollbackStatus DisposePreview()
        {
            if (_disposed)
            {
                return AppearanceRollbackStatus.Disposed;
            }

            AppearanceRollbackStatus status = _appearance.DisposePreview();
            if (status != AppearanceRollbackStatus.Failed)
            {
                _activePlan = null;
                Draft = _initialDraft;
                _disposed = true;
            }

            return status;
        }

        private static CustomizationPreviewResult Result(
            bool succeeded,
            CustomizationEditResult edit,
            AppearancePrepareResult preparation,
            AppearanceApplyStatus applyStatus,
            AppearanceRollbackStatus rollbackStatus)
        {
            return new CustomizationPreviewResult(
                succeeded,
                edit,
                preparation,
                applyStatus,
                rollbackStatus);
        }
    }

    public sealed class ProceduralChampionAppearanceAdapter :
        IReversibleAppearanceAdapter
    {
        private readonly ChampionCustomizationController _target;
        private readonly ModelCapabilitySnapshot _model;
        private EffectiveAppearanceSnapshot _current;
        private AppearancePlan _activePlan;
        private bool _disposed;

        public ProceduralChampionAppearanceAdapter(
            ChampionCustomizationController target,
            ModelCapabilitySnapshot model,
            EffectiveAppearanceSnapshot initial)
        {
            _target = target != null
                ? target
                : throw new ArgumentNullException(nameof(target));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _current = initial ?? throw new ArgumentNullException(nameof(initial));
            if (!string.Equals(
                    initial.ModelFingerprint,
                    model.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Initial appearance and model capability fingerprints differ.");
            }

            Apply(initial);
        }

        public EffectiveAppearanceSnapshot Current => _current;
        public bool IsDisposed => _disposed;
        public bool HasPreview => _activePlan != null;

        public AppearanceApplyStatus ApplyAndVerify(
            AppearancePlan plan,
            ModelCapabilitySnapshot currentModel)
        {
            if (_disposed)
            {
                return AppearanceApplyStatus.Disposed;
            }

            if (plan == null || currentModel == null || _target == null ||
                !string.Equals(plan.ModelFingerprint, _model.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(currentModel.Fingerprint, _model.Fingerprint,
                    StringComparison.Ordinal) ||
                !string.Equals(_current.Fingerprint, plan.Prior.Fingerprint,
                    StringComparison.Ordinal))
            {
                return AppearanceApplyStatus.RejectedStaleModel;
            }

            for (int index = 0; index < plan.Operations.Count; index++)
            {
                AppearanceOperation operation = plan.Operations[index];
                if (!_model.Supports(
                        operation.Field,
                        operation.RequiredCapabilityId))
                {
                    return AppearanceApplyStatus.RejectedMissingCapability;
                }
            }

            try
            {
                ChampionCustomizationState applied = Apply(plan.Proposed);
                if (!Matches(applied, plan.Proposed.Values))
                {
                    Apply(plan.Prior);
                    return AppearanceApplyStatus.FailedVerification;
                }
            }
            catch (Exception)
            {
                TryRestore(plan.Prior);
                return AppearanceApplyStatus.FailedRequiredOperation;
            }

            _current = plan.Proposed;
            _activePlan = plan;
            return AppearanceApplyStatus.AppliedAndVerified;
        }

        public AppearanceRollbackStatus Rollback(AppearancePlan plan)
        {
            if (_disposed)
            {
                return AppearanceRollbackStatus.Disposed;
            }

            if (_activePlan == null || plan == null ||
                !string.Equals(
                    _activePlan.PlanHash,
                    plan.PlanHash,
                    StringComparison.Ordinal))
            {
                return AppearanceRollbackStatus.NotApplied;
            }

            try
            {
                ChampionCustomizationState restored = Apply(plan.Prior);
                if (!Matches(restored, plan.Prior.Values))
                {
                    return AppearanceRollbackStatus.Failed;
                }
            }
            catch (Exception)
            {
                return AppearanceRollbackStatus.Failed;
            }

            _current = plan.Prior;
            _activePlan = null;
            return AppearanceRollbackStatus.Restored;
        }

        public AppearanceRollbackStatus DisposePreview()
        {
            if (_disposed)
            {
                return AppearanceRollbackStatus.Disposed;
            }

            AppearanceRollbackStatus status = _activePlan == null
                ? AppearanceRollbackStatus.NotApplied
                : Rollback(_activePlan);
            if (status != AppearanceRollbackStatus.Failed)
            {
                _disposed = true;
            }

            return status;
        }

        private ChampionCustomizationState Apply(
            EffectiveAppearanceSnapshot appearance)
        {
            ChampionCustomizationState state = ToState(appearance.Values);
            _target.ApplyPresentation(state);
            return state;
        }

        private void TryRestore(EffectiveAppearanceSnapshot appearance)
        {
            try
            {
                Apply(appearance);
            }
            catch (Exception)
            {
                // The failed status remains authoritative; callers must not commit.
            }
        }

        private static ChampionCustomizationState ToState(
            CustomizationValues values)
        {
            return new ChampionCustomizationState
            {
                BodyPresetId = values.BodyPresetId,
                HairStyleId = values.HairStyleId,
                ArmorStyleId = values.ArmorStyleId,
                FaceMarkId = values.FaceMarkId,
                WeaponStyleId = values.WeaponStyleId,
                OffhandStyleId = values.OffhandStyleId,
                PrimaryR = values.PrimaryColor.Red,
                PrimaryG = values.PrimaryColor.Green,
                PrimaryB = values.PrimaryColor.Blue,
                HairR = values.HairColor.Red,
                HairG = values.HairColor.Green,
                HairB = values.HairColor.Blue,
                SkinR = values.SkinColor.Red,
                SkinG = values.SkinColor.Green,
                SkinB = values.SkinColor.Blue,
                EyeR = values.EyeColor.Red,
                EyeG = values.EyeColor.Green,
                EyeB = values.EyeColor.Blue,
                AccentR = values.AccentColor.Red,
                AccentG = values.AccentColor.Green,
                AccentB = values.AccentColor.Blue,
                CapeEnabled = values.CapeEnabled,
                HelmetEnabled = values.HelmetEnabled
            };
        }

        private static bool Matches(
            ChampionCustomizationState state,
            CustomizationValues values)
        {
            return state != null &&
                   string.Equals(state.BodyPresetId, values.BodyPresetId,
                       StringComparison.Ordinal) &&
                   string.Equals(state.HairStyleId, values.HairStyleId,
                       StringComparison.Ordinal) &&
                   string.Equals(state.ArmorStyleId, values.ArmorStyleId,
                       StringComparison.Ordinal) &&
                   string.Equals(state.FaceMarkId, values.FaceMarkId,
                       StringComparison.Ordinal) &&
                   string.Equals(state.WeaponStyleId, values.WeaponStyleId,
                       StringComparison.Ordinal) &&
                   string.Equals(state.OffhandStyleId, values.OffhandStyleId,
                       StringComparison.Ordinal) &&
                   state.PrimaryR.Equals(values.PrimaryColor.Red) &&
                   state.PrimaryG.Equals(values.PrimaryColor.Green) &&
                   state.PrimaryB.Equals(values.PrimaryColor.Blue) &&
                   state.HairR.Equals(values.HairColor.Red) &&
                   state.HairG.Equals(values.HairColor.Green) &&
                   state.HairB.Equals(values.HairColor.Blue) &&
                   state.SkinR.Equals(values.SkinColor.Red) &&
                   state.SkinG.Equals(values.SkinColor.Green) &&
                   state.SkinB.Equals(values.SkinColor.Blue) &&
                   state.EyeR.Equals(values.EyeColor.Red) &&
                   state.EyeG.Equals(values.EyeColor.Green) &&
                   state.EyeB.Equals(values.EyeColor.Blue) &&
                   state.AccentR.Equals(values.AccentColor.Red) &&
                   state.AccentG.Equals(values.AccentColor.Green) &&
                   state.AccentB.Equals(values.AccentColor.Blue) &&
                   state.CapeEnabled == values.CapeEnabled &&
                   state.HelmetEnabled == values.HelmetEnabled;
        }
    }
}
