using System;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using NUnit.Framework;

namespace AL.Tests.EditMode.Customization
{
    public class CustomizationDraftPlannerTests
    {
        [Test]
        public void DraftStartsFromRawNotPlaceholderAndTracksPreservedUnknown()
        {
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw(
                CustomizationPlannerTestFixtures.Values(hair: "future_hair_v2"));
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationCompatibilityResult compatibility =
                CustomizationCompatibilityPlanner.Classify(
                    raw, CustomizationCatalogAvailability.Ready, catalog);

            CustomizationDraft draft = CustomizationDraftPlanner.Create(
                "draft_unknown_001",
                CustomizationPlannerTestFixtures.Query(raw, catalog, model),
                compatibility);

            Assert.That(draft.ProposedRaw.HairStyleId,
                Is.EqualTo("future_hair_v2"));
            Assert.That(draft.BaseEffective.Values.HairStyleId, Is.EqualTo("short"));
            Assert.That(draft.PreservedUnknownFields,
                Is.EqualTo(CustomizationField.HairStyle));
            Assert.That(draft.ChangedFields, Is.EqualTo(CustomizationField.None));
        }

        [Test]
        public void OptionEditChangesOnlyDeclaredField()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);

            CustomizationEditResult result = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle, "long"),
                CustomizationCatalogAvailability.Ready,
                catalog,
                model,
                draft.BaseRawRevision);

            Assert.That(result.Status,
                Is.EqualTo(CustomizationEditStatus.AppliedToDraft));
            Assert.That(result.Draft.ProposedRaw.HairStyleId, Is.EqualTo("long"));
            Assert.That(result.Draft.ProposedRaw.BodyPresetId,
                Is.EqualTo(draft.ProposedRaw.BodyPresetId));
            Assert.That(result.Draft.ProposedRaw.PrimaryColor,
                Is.EqualTo(draft.ProposedRaw.PrimaryColor));
            Assert.That(result.Draft.ChangedFields,
                Is.EqualTo(CustomizationField.HairStyle));
        }

        [Test]
        public void SameValueSelectionIsNoOpAndKeepsDraftIdentity()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);

            CustomizationEditResult result = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle, "short"),
                CustomizationCatalogAvailability.Ready,
                catalog,
                model,
                draft.BaseRawRevision);

            Assert.That(result.Status, Is.EqualTo(CustomizationEditStatus.NoChange));
            Assert.That(result.Draft, Is.SameAs(draft));
        }

        [Test]
        public void RevertingAnEditClearsTheNetChangedField()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);

            CustomizationEditResult changed = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle, "long"),
                CustomizationCatalogAvailability.Ready,
                catalog,
                model,
                draft.BaseRawRevision);
            CustomizationEditResult reverted = CustomizationDraftPlanner.Apply(
                changed.Draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle, "short"),
                CustomizationCatalogAvailability.Ready,
                catalog,
                model,
                draft.BaseRawRevision);

            Assert.That(reverted.Status,
                Is.EqualTo(CustomizationEditStatus.AppliedToDraft));
            Assert.That(reverted.Draft.ProposedRaw.Equals(draft.BaseRaw), Is.True);
            Assert.That(reverted.Draft.ChangedFields,
                Is.EqualTo(CustomizationField.None));
        }

        [Test]
        public void WrongFamilyUnknownAndInvalidColorRejectWithoutDraftMutation()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);

            CustomizationEditResult wrongFamily = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.PrimaryColor, "long"),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);
            CustomizationEditResult unknown = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle, "unknown"),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);
            CustomizationEditResult numeric = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectExactColor(
                    CustomizationField.PrimaryColor,
                    new CustomizationColor(float.NaN, 0f, 0f)),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);

            Assert.That(wrongFamily.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedWrongFamily));
            Assert.That(unknown.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedUnknownOption));
            Assert.That(numeric.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedNumericInvalid));
            Assert.That(wrongFamily.Draft.Fingerprint, Is.EqualTo(draft.Fingerprint));
            Assert.That(unknown.Draft.Fingerprint, Is.EqualTo(draft.Fingerprint));
            Assert.That(numeric.Draft.Fingerprint, Is.EqualTo(draft.Fingerprint));
        }

        [Test]
        public void PaletteSelectionWritesExactCatalogRgbWithoutInference()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);

            CustomizationEditResult result = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SelectPaletteColor(
                    CustomizationField.PrimaryColor, "stone_bronze"),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);

            Assert.That(result.Status,
                Is.EqualTo(CustomizationEditStatus.AppliedToDraft));
            Assert.That(result.Draft.ProposedRaw.PrimaryColor,
                Is.EqualTo(CustomizationPlannerTestFixtures.Bronze));
            Assert.That(result.Draft.Provenance,
                Is.EqualTo("color:palette:primary_colors:stone_bronze"));
        }

        [Test]
        public void PresetRequiresExplicitConfirmationBeforeReplacingUnknown()
        {
            RawCustomizationSnapshot raw = CustomizationPlannerTestFixtures.Raw(
                CustomizationPlannerTestFixtures.Values(hair: "future_hair_v2"));
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                raw, catalog, model);

            CustomizationEditResult rejected = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.ApplyPreset("vanguard"),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);
            CustomizationEditResult accepted = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.ApplyPreset("vanguard", true),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);

            Assert.That(rejected.Status, Is.EqualTo(
                CustomizationEditStatus.RejectedPreservedUnknownReplacementNeedsConfirmation));
            Assert.That(rejected.Draft.ProposedRaw.HairStyleId,
                Is.EqualTo("future_hair_v2"));
            Assert.That(accepted.Status,
                Is.EqualTo(CustomizationEditStatus.AppliedToDraft));
            Assert.That(accepted.Draft.ProposedRaw.HairStyleId, Is.EqualTo("short"));
            Assert.That(accepted.Draft.PreservedUnknownFields,
                Is.EqualTo(CustomizationField.None));
        }

        [Test]
        public void PartialPresetAndSubsequentEditDoNotOverwriteUnmaskedFields()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);

            CustomizationEditResult preset = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.ApplyPreset("scout_partial"),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);
            CustomizationEditResult edit = CustomizationDraftPlanner.Apply(
                preset.Draft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.WeaponStyle, "axe"),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);

            Assert.That(preset.Draft.ProposedRaw.HairStyleId, Is.EqualTo("long"));
            Assert.That(preset.Draft.ProposedRaw.CapeEnabled, Is.False);
            Assert.That(preset.Draft.ProposedRaw.ArmorStyleId,
                Is.EqualTo("realm_basic"));
            Assert.That(edit.Draft.ProposedRaw.WeaponStyleId, Is.EqualTo("axe"));
            Assert.That(edit.Draft.ProposedRaw.HairStyleId, Is.EqualTo("long"));
            Assert.That(edit.Draft.ChangedFields, Is.EqualTo(
                CustomizationField.HairStyle |
                CustomizationField.CapeEnabled |
                CustomizationField.WeaponStyle));
        }

        [Test]
        public void CatalogModelAndRawRevisionDriftEachRejectAsStale()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);
            CustomizationCatalogSnapshot newerCatalog =
                CustomizationPlannerTestFixtures.Catalog(
                    CustomizationPlannerTestFixtures.Candidate(
                        identity: CustomizationPlannerTestFixtures.Identity(
                            hash: new string('1', 64))));
            ModelCapabilitySnapshot newerModel =
                CustomizationPlannerTestFixtures.Model(revision: 2L);
            CustomizationEditRequest request = CustomizationEditRequest.SetFlag(
                CustomizationField.HelmetEnabled, true);

            CustomizationEditResult catalogStale = CustomizationDraftPlanner.Apply(
                draft, request, CustomizationCatalogAvailability.Ready,
                newerCatalog, model, draft.BaseRawRevision);
            CustomizationEditResult modelStale = CustomizationDraftPlanner.Apply(
                draft, request, CustomizationCatalogAvailability.Ready,
                catalog, newerModel, draft.BaseRawRevision);
            CustomizationEditResult rawStale = CustomizationDraftPlanner.Apply(
                draft, request, CustomizationCatalogAvailability.Ready,
                catalog, model, draft.BaseRawRevision + 1L);

            Assert.That(catalogStale.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedStaleDraft));
            Assert.That(modelStale.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedStaleDraft));
            Assert.That(rawStale.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedStaleDraft));
        }

        [Test]
        public void SeededRandomizationIsDeterministicAndBoundedToMask()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);
            CustomizationField mask = CustomizationField.BodyPreset |
                                      CustomizationField.HairStyle |
                                      CustomizationField.PrimaryColor |
                                      CustomizationField.HelmetEnabled;

            CustomizationEditResult first = CustomizationDraftPlanner.Apply(
                draft, CustomizationEditRequest.Randomize(424242L, mask),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);
            CustomizationEditResult second = CustomizationDraftPlanner.Apply(
                draft, CustomizationEditRequest.Randomize(424242L, mask),
                CustomizationCatalogAvailability.Ready, catalog, model,
                draft.BaseRawRevision);

            Assert.That(first.Status,
                Is.EqualTo(CustomizationEditStatus.AppliedToDraft));
            Assert.That(second.Draft.Fingerprint, Is.EqualTo(first.Draft.Fingerprint));
            Assert.That(first.Draft.ChangedFields & ~mask,
                Is.EqualTo(CustomizationField.None));
            Assert.That(first.Draft.ProposedRaw.ArmorStyleId,
                Is.EqualTo(draft.ProposedRaw.ArmorStyleId));
            Assert.That(first.Draft.ProposedRaw.AccentColor,
                Is.EqualTo(draft.ProposedRaw.AccentColor));
        }

        [Test]
        public void MissingCapabilityAndDisposedDraftRejectWithoutMutation()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot fullModel = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: fullModel);
            string[] capabilities = CustomizationPlannerTestFixtures.AllCapabilities()
                .Where(item => item != "cap_hair_long").ToArray();
            ModelCapabilitySnapshot limited = CustomizationPlannerTestFixtures.Model(
                capabilities: capabilities);
            CustomizationDraft limitedDraft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: limited);

            CustomizationEditResult unavailable = CustomizationDraftPlanner.Apply(
                limitedDraft,
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle, "long"),
                CustomizationCatalogAvailability.Ready, catalog, limited,
                limitedDraft.BaseRawRevision);
            CustomizationEditResult disposed = CustomizationDraftPlanner.Apply(
                draft,
                CustomizationEditRequest.SetFlag(
                    CustomizationField.HelmetEnabled, true),
                CustomizationCatalogAvailability.Ready, catalog, fullModel,
                draft.BaseRawRevision,
                disposed: true);

            Assert.That(unavailable.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedUnavailableCapability));
            Assert.That(disposed.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedDisposed));
            Assert.That(unavailable.Draft.Fingerprint,
                Is.EqualTo(limitedDraft.Fingerprint));
            Assert.That(disposed.Draft.Fingerprint, Is.EqualTo(draft.Fingerprint));
        }

        [Test]
        public void PresetAndResetCannotIntroduceUnsupportedSelections()
        {
            CustomizationCatalogSnapshot presetCatalog =
                CustomizationPlannerTestFixtures.Catalog();
            string[] withoutLong = CustomizationPlannerTestFixtures.AllCapabilities()
                .Where(item => item != "cap_hair_long").ToArray();
            ModelCapabilitySnapshot presetModel =
                CustomizationPlannerTestFixtures.Model(capabilities: withoutLong);
            CustomizationDraft presetDraft =
                CustomizationPlannerTestFixtures.Draft(
                    catalog: presetCatalog, model: presetModel);

            CustomizationEditResult preset = CustomizationDraftPlanner.Apply(
                presetDraft,
                CustomizationEditRequest.ApplyPreset("scout_partial"),
                CustomizationCatalogAvailability.Ready,
                presetCatalog,
                presetModel,
                presetDraft.BaseRawRevision);

            string[] withoutShort = CustomizationPlannerTestFixtures.AllCapabilities()
                .Where(item => item != "cap_hair_short").ToArray();
            ModelCapabilitySnapshot resetModel =
                CustomizationPlannerTestFixtures.Model(capabilities: withoutShort);
            RawCustomizationSnapshot longHair = CustomizationPlannerTestFixtures.Raw(
                CustomizationPlannerTestFixtures.Values(hair: "long"));
            CustomizationCatalogSnapshot resetCatalog =
                CustomizationPlannerTestFixtures.Catalog();
            CustomizationDraft resetDraft =
                CustomizationPlannerTestFixtures.Draft(
                    longHair, resetCatalog, resetModel);
            CustomizationEditResult reset = CustomizationDraftPlanner.Apply(
                resetDraft,
                CustomizationEditRequest.Reset(),
                CustomizationCatalogAvailability.Ready,
                resetCatalog,
                resetModel,
                resetDraft.BaseRawRevision);

            Assert.That(preset.Status, Is.EqualTo(
                CustomizationEditStatus.RejectedUnavailableCapability));
            Assert.That(reset.Status, Is.EqualTo(
                CustomizationEditStatus.RejectedUnavailableCapability));
            Assert.That(preset.Draft, Is.SameAs(presetDraft));
            Assert.That(reset.Draft, Is.SameAs(resetDraft));
        }

        [Test]
        public void PendingAndInvalidCatalogReturnDistinctStatuses()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model = CustomizationPlannerTestFixtures.Model();
            CustomizationDraft draft = CustomizationPlannerTestFixtures.Draft(
                catalog: catalog, model: model);
            CustomizationEditRequest request = CustomizationEditRequest.SetFlag(
                CustomizationField.HelmetEnabled, true);

            CustomizationEditResult pending = CustomizationDraftPlanner.Apply(
                draft, request, CustomizationCatalogAvailability.Pending,
                null, model, draft.BaseRawRevision);
            CustomizationEditResult invalid = CustomizationDraftPlanner.Apply(
                draft, request, CustomizationCatalogAvailability.Invalid,
                null, model, draft.BaseRawRevision);

            Assert.That(pending.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedCatalogPending));
            Assert.That(invalid.Status,
                Is.EqualTo(CustomizationEditStatus.RejectedCatalogInvalid));
        }
    }
}
