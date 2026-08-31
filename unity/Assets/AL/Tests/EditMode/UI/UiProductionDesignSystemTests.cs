using System.Collections.Generic;
using System.Linq;
using AL.UI.DesignSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.UI
{
    public sealed class UiProductionDesignSystemTests
    {
        private readonly List<Object> _owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    Object.DestroyImmediate(_owned[i]);
                }
            }

            _owned.Clear();
        }

        [Test]
        public void ProductionTokenAssetCoversTypographyMaterialsAndSemanticStates()
        {
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();

            Assert.That(tokens, Is.Not.Null);
            Assert.That(tokens.SystemId, Is.EqualTo("al.ui.production.v1"));
            Assert.That(tokens.MinimumHitTarget, Is.GreaterThanOrEqualTo(48f));
            Assert.That(tokens.Typography.Select(token => token.Role), Is.EquivalentTo(new[]
            {
                UiTypographyRole.Display,
                UiTypographyRole.Title,
                UiTypographyRole.Body,
                UiTypographyRole.Action,
                UiTypographyRole.Caption,
                UiTypographyRole.Numeric
            }));
            Assert.That(tokens.Typography, Has.All.Matches<UiTypographyToken>(token =>
                !string.IsNullOrWhiteSpace(token.Family) &&
                token.BaseSize > 0f &&
                token.LineHeight >= 1f));
            Assert.That(tokens.Spacing, Is.Ordered);
            Assert.That(tokens.Spacing.First(), Is.GreaterThan(0f));
            Assert.That(tokens.SurfaceOpacity, Is.InRange(0.7f, 1f));
            Assert.That(tokens.GlyphGlowOpacity, Is.InRange(0.08f, 0.45f));
            Assert.That(tokens.ElevationLevels, Is.Ordered);

            UiSemanticState[] requiredStates =
            {
                UiSemanticState.Neutral,
                UiSemanticState.Friendly,
                UiSemanticState.Hostile,
                UiSemanticState.Warning,
                UiSemanticState.Success,
                UiSemanticState.Disabled,
                UiSemanticState.Stale,
                UiSemanticState.Focused
            };
            foreach (UiSemanticState state in requiredStates)
            {
                UiStateTreatment treatment = tokens.GetStateTreatment(state);
                Assert.That(treatment, Is.Not.Null, state.ToString());
                Assert.That(treatment.NonColorCue, Is.Not.EqualTo(UiNonColorCue.None), state.ToString());
                Assert.That(treatment.Pattern, Is.Not.EqualTo(UiSurfacePattern.None), state.ToString());
                Assert.That(treatment.LabelPrefix, Is.Not.Empty, state.ToString());
            }
        }

        [Test]
        public void AccessibilityVariantsReduceEffectsWithoutRemovingStateTruth()
        {
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();
            Assert.That(tokens, Is.Not.Null);

            UiAccessibilityPresentation standard = tokens.ResolveAccessibility(
                new UiAccessibilitySettings(1f, false, false, false));
            UiAccessibilityPresentation reduced = tokens.ResolveAccessibility(
                new UiAccessibilitySettings(2f, true, true, true));

            Assert.That(standard.TextScale, Is.EqualTo(1f));
            Assert.That(reduced.TextScale, Is.EqualTo(2f));
            Assert.That(reduced.PanelTransitionSeconds, Is.LessThan(standard.PanelTransitionSeconds));
            Assert.That(reduced.AmbientMotionScale, Is.EqualTo(0f));
            Assert.That(reduced.FlashOpacity, Is.LessThanOrEqualTo(0.08f));
            Assert.That(reduced.VfxDensity, Is.LessThanOrEqualTo(0.35f));
            Assert.That(reduced.FocusHoldSeconds, Is.GreaterThanOrEqualTo(standard.FocusHoldSeconds));

            foreach (UiSemanticState state in System.Enum.GetValues(typeof(UiSemanticState)))
            {
                Assert.That(tokens.GetStateTreatment(state).NonColorCue, Is.Not.EqualTo(UiNonColorCue.None));
            }
        }

        [Test]
        public void EveryRequiredFormFactorHasAnAuthoredAndDistinctComposition()
        {
            HudResponsiveCompositionSet set = HudResponsiveCompositionSet.LoadDefault();
            Assert.That(set, Is.Not.Null);
            Assert.That(set.Compositions, Has.Length.EqualTo(4));

            UiFormFactor[] requiredFactors =
            {
                UiFormFactor.PhoneLandscape,
                UiFormFactor.TabletLandscape,
                UiFormFactor.Pc16By9,
                UiFormFactor.PcUltrawide
            };
            var signatures = new HashSet<string>();
            foreach (UiFormFactor formFactor in requiredFactors)
            {
                Assert.That(set.TryGet(formFactor, out HudCompositionDefinition composition), Is.True);
                Assert.That(composition.ReferenceResolution.x, Is.GreaterThan(0));
                Assert.That(composition.ReferenceResolution.y, Is.GreaterThan(0));
                Assert.That(composition.TextScaleMinimum, Is.LessThanOrEqualTo(1f));
                Assert.That(composition.TextScaleMaximum, Is.GreaterThanOrEqualTo(2f));
                Assert.That(composition.SafeAreaPadding, Is.Not.EqualTo(Vector4.zero));
                signatures.Add(composition.Signature);

                HudSlotId[] requiredSlots =
                {
                    HudSlotId.PlayerVitals,
                    HudSlotId.CurrentTarget,
                    HudSlotId.HostileTelegraphs,
                    HudSlotId.PartySupport,
                    HudSlotId.Objectives,
                    HudSlotId.Route,
                    HudSlotId.Allegiance
                };
                Assert.That(composition.Slots.Select(slot => slot.Id), Is.EquivalentTo(requiredSlots));
                foreach (HudSlotDefinition slot in composition.Slots)
                {
                    AssertNormalized(slot.NormalizedRect);
                    if (!slot.IsWorldCueLayer)
                    {
                        Assert.That(
                            slot.NormalizedRect.Overlaps(composition.ProtectedScanPath),
                            Is.False,
                            $"{formFactor}/{slot.Id} enters the protected PvP scan path.");
                    }
                }
            }

            Assert.That(signatures, Has.Count.EqualTo(4), "Required layouts must be authored, not scaled clones.");
        }

        [Test]
        public void ResolverSeparatesTouchLayoutsFromPcAndUltrawideLayouts()
        {
            HudResponsiveCompositionSet set = HudResponsiveCompositionSet.LoadDefault();

            Assert.That(set.Resolve(2400, 1080, true).FormFactor, Is.EqualTo(UiFormFactor.PhoneLandscape));
            Assert.That(set.Resolve(2732, 2048, true).FormFactor, Is.EqualTo(UiFormFactor.TabletLandscape));
            Assert.That(set.Resolve(1920, 1080, false).FormFactor, Is.EqualTo(UiFormFactor.Pc16By9));
            Assert.That(set.Resolve(3440, 1440, false).FormFactor, Is.EqualTo(UiFormFactor.PcUltrawide));
        }

        [Test]
        public void ProjectionHonorsExtremeSafeAreaAndLargeText()
        {
            HudResponsiveCompositionSet set = HudResponsiveCompositionSet.LoadDefault();
            HudCompositionDefinition phone = set.Resolve(2400, 1080, true);
            var safeArea = new Rect(132f, 48f, 2136f, 984f);
            Rect usableArea = HudLayoutProjection.ApplySafeAreaPadding(safeArea, phone);

            Assert.That(usableArea.xMin, Is.GreaterThan(safeArea.xMin));
            Assert.That(usableArea.yMin, Is.GreaterThan(safeArea.yMin));
            Assert.That(usableArea.xMax, Is.LessThan(safeArea.xMax));
            Assert.That(usableArea.yMax, Is.LessThan(safeArea.yMax));

            Rect scan = HudLayoutProjection.Project(usableArea, phone.ProtectedScanPath);
            Assert.That(safeArea.Contains(scan.min), Is.True);
            Assert.That(safeArea.Contains(scan.max), Is.True);
            Assert.That(HudLayoutProjection.ClampTextScale(phone, 2.5f), Is.EqualTo(2f));

            foreach (HudSlotDefinition slot in phone.Slots)
            {
                Rect projected = HudLayoutProjection.Project(usableArea, slot.NormalizedRect);
                Assert.That(safeArea.Contains(projected.min), Is.True, slot.Id.ToString());
                Assert.That(safeArea.Contains(projected.max), Is.True, slot.Id.ToString());
                if (!slot.IsWorldCueLayer)
                {
                    Assert.That(projected.Overlaps(scan), Is.False, slot.Id.ToString());
                }
            }
        }

        [TestCase(UiFormFactor.PhoneLandscape)]
        [TestCase(UiFormFactor.TabletLandscape)]
        [TestCase(UiFormFactor.Pc16By9)]
        [TestCase(UiFormFactor.PcUltrawide)]
        public void RepresentativeCompositionBuildsRenderableHierarchy(UiFormFactor formFactor)
        {
            UiProductionDesignTokens tokens = UiProductionDesignTokens.LoadDefault();
            HudResponsiveCompositionSet set = HudResponsiveCompositionSet.LoadDefault();
            Assert.That(set.TryGet(formFactor, out HudCompositionDefinition composition), Is.True);

            var canvas = new GameObject("DesignSystemPreview", typeof(RectTransform));
            _owned.Add(canvas);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = composition.ReferenceResolution;

            HudCompositionRenderResult result = HudCompositionPreviewRenderer.Build(
                canvasRect,
                composition,
                tokens,
                new UiAccessibilitySettings(2f, true, true, true));

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ProtectedScanPath, Is.Not.Null);
            Assert.That(result.ProtectedScanPath.gameObject.name, Is.EqualTo("ProtectedPvpScanPath"));
            Assert.That(result.SlotViews, Has.Count.EqualTo(composition.Slots.Length));
            Assert.That(result.SlotViews.Select(view => view.Definition.Id),
                Is.EquivalentTo(composition.Slots.Select(slot => slot.Id)));
            Assert.That(result.SlotViews, Has.All.Matches<HudSlotView>(view =>
                view.Root != null && view.Label != null && !string.IsNullOrWhiteSpace(view.Label.text)));
            foreach (HudSlotView view in result.SlotViews)
            {
                int expectedSize = Mathf.RoundToInt(
                    tokens.GetTypography(view.Definition.TypographyRole).BaseSize * 2f);
                Assert.That(view.Label.fontSize, Is.EqualTo(expectedSize), view.Definition.Id.ToString());
                Assert.That(view.NonColorCueRoot, Is.Not.Null, view.Definition.Id.ToString());
                Assert.That(view.PatternRoot, Is.Not.Null, view.Definition.Id.ToString());
                Assert.That(
                    view.NonColorCueRoot.GetComponentsInChildren<Image>(true).Length,
                    Is.GreaterThan(0),
                    view.Definition.Id.ToString());
                Assert.That(
                    view.PatternRoot.GetComponentsInChildren<Image>(true).Length,
                    Is.GreaterThan(0),
                    view.Definition.Id.ToString());
            }
        }

        private static void AssertNormalized(Rect rect)
        {
            Assert.That(rect.xMin, Is.InRange(0f, 1f));
            Assert.That(rect.yMin, Is.InRange(0f, 1f));
            Assert.That(rect.xMax, Is.InRange(0f, 1f));
            Assert.That(rect.yMax, Is.InRange(0f, 1f));
            Assert.That(rect.width, Is.GreaterThan(0f));
            Assert.That(rect.height, Is.GreaterThan(0f));
        }
    }
}
