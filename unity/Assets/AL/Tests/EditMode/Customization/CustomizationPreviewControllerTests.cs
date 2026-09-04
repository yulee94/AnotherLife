using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Customization
{
    public sealed class CustomizationPreviewControllerTests
    {
        [Test]
        public void ProductionWireCatalogBuildsValidatedPlannerCatalogAndStablePreviewModel()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "character_customization.v1.json");
            string json = File.ReadAllText(path);

            Assert.That(
                CharacterCustomizationCatalog.TryParsePlannerCatalog(
                    json,
                    out CustomizationCatalogSnapshot catalog,
                    out IReadOnlyList<CustomizationDiagnostic> diagnostics),
                Is.True,
                string.Join("\n", diagnostics.Select(item =>
                    item.Code + ":" + item.FieldPath)));

            ModelCapabilitySnapshot first =
                ProceduralChampionPreviewModelCapabilities.Create(catalog);
            ModelCapabilitySnapshot second =
                ProceduralChampionPreviewModelCapabilities.Create(catalog);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Identity.RawSha256, Has.Length.EqualTo(64));
            Assert.That(catalog.Identity.PackagedRelativePath,
                Is.EqualTo(CharacterCustomizationCatalog.CatalogRelativePath));
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.SourceIdentity,
                Is.EqualTo("procedural_champion_model_builder_preview_v1"));
            Assert.That(first.Capabilities,
                Contains.Item("model.hair_style.long"));
            Assert.That(first.Capabilities,
                Contains.Item("material.primary_color"));
        }

        [Test]
        public void PreviewPreservesUnknownRawFieldsAndCancelRestoresAppearance()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model =
                CustomizationPlannerTestFixtures.Model();
            RawCustomizationSnapshot raw =
                CustomizationPlannerTestFixtures.Raw(
                    CustomizationPlannerTestFixtures.Values(
                        hair: "future_hair_v2"));
            CustomizationCompatibilityResult compatibility =
                CustomizationCompatibilityPlanner.Classify(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    catalog);
            CustomizationQueryResult query =
                CustomizationCompatibilityPlanner.Resolve(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    catalog,
                    model);
            CustomizationDraft draft = CustomizationDraftPlanner.Create(
                "draft_preview_unknown_001",
                query,
                compatibility);
            var appearance = new FakeReversibleAppearanceAdapter(
                query.EffectivePresentation);
            var preview = new CustomizationPreviewController(
                draft,
                catalog,
                model,
                appearance);
            string rawBefore = raw.Fingerprint;
            string draftBefore = draft.Fingerprint;
            string appearanceBefore = appearance.Current.Fingerprint;

            CustomizationPreviewResult result = preview.Preview(
                CustomizationEditRequest.SelectOption(
                    CustomizationField.BodyPreset,
                    "broad"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(preview.Draft.ProposedRaw.HairStyleId,
                Is.EqualTo("future_hair_v2"));
            Assert.That(raw.Fingerprint, Is.EqualTo(rawBefore));
            Assert.That(appearance.Current.Fingerprint,
                Is.Not.EqualTo(appearanceBefore));
            Assert.That(preview.Cancel(),
                Is.EqualTo(AppearanceRollbackStatus.Restored));
            Assert.That(appearance.Current.Fingerprint,
                Is.EqualTo(appearanceBefore));
            Assert.That(preview.Draft.Fingerprint, Is.EqualTo(draftBefore));
        }

        [Test]
        public void FailedVisualVerificationRollsBackAndRejectsDraftAdvance()
        {
            CustomizationCatalogSnapshot catalog =
                CustomizationPlannerTestFixtures.Catalog();
            ModelCapabilitySnapshot model =
                CustomizationPlannerTestFixtures.Model();
            RawCustomizationSnapshot raw =
                CustomizationPlannerTestFixtures.Raw();
            CustomizationCompatibilityResult compatibility =
                CustomizationCompatibilityPlanner.Classify(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    catalog);
            CustomizationQueryResult query =
                CustomizationCompatibilityPlanner.Resolve(
                    raw,
                    CustomizationCatalogAvailability.Ready,
                    catalog,
                    model);
            CustomizationDraft draft = CustomizationDraftPlanner.Create(
                "draft_preview_sabotage_001",
                query,
                compatibility);
            var appearance = new PartiallyFailingAppearanceAdapter(
                query.EffectivePresentation);
            var preview = new CustomizationPreviewController(
                draft,
                catalog,
                model,
                appearance);
            string draftBefore = preview.Draft.Fingerprint;
            string appearanceBefore = appearance.Current.Fingerprint;

            CustomizationPreviewResult result = preview.Preview(
                CustomizationEditRequest.SelectOption(
                    CustomizationField.HairStyle,
                    "long"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ApplyStatus,
                Is.EqualTo(AppearanceApplyStatus.FailedVerification));
            Assert.That(result.RollbackStatus,
                Is.EqualTo(AppearanceRollbackStatus.Restored));
            Assert.That(appearance.RollbackCount, Is.EqualTo(1));
            Assert.That(appearance.Current.Fingerprint,
                Is.EqualTo(appearanceBefore));
            Assert.That(preview.Draft.Fingerprint, Is.EqualTo(draftBefore));
        }

        private sealed class PartiallyFailingAppearanceAdapter :
            IReversibleAppearanceAdapter
        {
            private EffectiveAppearanceSnapshot _current;

            internal PartiallyFailingAppearanceAdapter(
                EffectiveAppearanceSnapshot initial)
            {
                _current = initial;
            }

            public EffectiveAppearanceSnapshot Current => _current;
            public bool HasPreview { get; private set; }
            public bool IsDisposed { get; private set; }
            internal int RollbackCount { get; private set; }

            public AppearanceApplyStatus ApplyAndVerify(
                AppearancePlan plan,
                ModelCapabilitySnapshot currentModel)
            {
                _current = plan.Proposed;
                HasPreview = true;
                return AppearanceApplyStatus.FailedVerification;
            }

            public AppearanceRollbackStatus Rollback(AppearancePlan plan)
            {
                RollbackCount++;
                _current = plan.Prior;
                HasPreview = false;
                return AppearanceRollbackStatus.Restored;
            }

            public AppearanceRollbackStatus DisposePreview()
            {
                IsDisposed = true;
                return AppearanceRollbackStatus.NotApplied;
            }
        }
    }
}
