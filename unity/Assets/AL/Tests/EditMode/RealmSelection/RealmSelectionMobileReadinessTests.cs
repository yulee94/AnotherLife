using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmSelectionMobileReadinessTests
    {
        [Test]
        public void SafeAreaNormalizesInsideThePhysicalViewport()
        {
            RealmSelectionViewportLayout.NormalizeSafeArea(
                new Rect(0f, 102f, 1179f, 2454f),
                new Vector2(1179f, 2556f),
                out Vector2 anchorMin,
                out Vector2 anchorMax);

            Assert.That(anchorMin.x, Is.EqualTo(0f));
            Assert.That(anchorMin.y, Is.EqualTo(102f / 2556f).Within(0.0001f));
            Assert.That(anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void InvalidViewportFallsBackToTheFullCanvas()
        {
            RealmSelectionViewportLayout.NormalizeSafeArea(
                new Rect(12f, 34f, 56f, 78f),
                Vector2.zero,
                out Vector2 anchorMin,
                out Vector2 anchorMax);

            Assert.That(anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void PortraitLayoutKeepsAllFourRealmCardsInOneBoundedColumn()
        {
            RealmSelectionGridSpec spec = RealmSelectionViewportLayout.CalculateGrid(920f, portrait: true);
            float fourCardHeight = spec.CellSize.y * 4f + spec.Spacing.y * 3f;

            Assert.That(spec.ColumnCount, Is.EqualTo(1));
            Assert.That(spec.CellSize.x, Is.LessThanOrEqualTo(920f));
            Assert.That(fourCardHeight, Is.LessThanOrEqualTo(1700f));
        }

        [Test]
        public void LandscapeLayoutKeepsTwoColumnsInsideTheAvailableWidth()
        {
            const float availableWidth = 1700f;
            RealmSelectionGridSpec spec = RealmSelectionViewportLayout.CalculateGrid(
                availableWidth,
                portrait: false);
            float occupiedWidth = spec.CellSize.x * 2f + spec.Spacing.x;

            Assert.That(spec.ColumnCount, Is.EqualTo(2));
            Assert.That(occupiedWidth, Is.LessThanOrEqualTo(availableWidth));
        }
    }
}
