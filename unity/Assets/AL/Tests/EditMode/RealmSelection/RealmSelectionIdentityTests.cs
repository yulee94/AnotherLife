using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Data.Definitions;
using AL.RealmSelection;
using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmSelection
{
    public sealed class RealmSelectionIdentityTests
    {
        private RealmCatalogSnapshot _catalog;
        private readonly List<RealmDefinition> _definitions = new List<RealmDefinition>(4);

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData", "al_realm_catalog.json");
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            _catalog = result.Snapshot;
            _definitions.Add(CreateDefinition(RealmId.Crownlands));
            _definitions.Add(CreateDefinition(RealmId.Stonehold));
            _definitions.Add(CreateDefinition(RealmId.Eldergrove));
            _definitions.Add(CreateDefinition(RealmId.Umbral));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                Object.DestroyImmediate(_definitions[i]);
            }

            _definitions.Clear();
        }

        [Test]
        public void CanonicalCatalogPublishesPeopleAndStructuralIdentityForExactlyFourRealms()
        {
            Assert.That(_catalog.Realms, Has.Count.EqualTo(4));
            AssertPeople("crownlands", "Crownlands Humans", "Celestial Meridian", "four-point meridian", "aged silver, pale stone");
            AssertPeople("stonehold", "Stonehold Dwarves", "Tectonic Axis", "squared orthogonal mass", "basalt, dark iron, aged steel");
            AssertPeople("eldergrove", "Eldergrove Elves", "Living Orbit", "three-arc seed void", "bark, lichen, weathered bronze");
            AssertPeople("umbral", "Umbral Dark Elves", "Severed Eclipse", "offset eclipse and diagonal void", "obsidian, smoked metal");
        }

        [Test]
        public void IdentityTilesAreGreyscaleDistinguishableByStructureNotHue()
        {
            var keys = new HashSet<string>();
            var frames = new HashSet<RealmStructuralFrameKind>();
            foreach (RealmDefinition definition in _definitions)
            {
                RealmIdentityPresentation identity = RealmSelectionIdentity.Resolve(definition, _catalog);
                Assert.That(identity.HasStructuralIdentity, Is.True, definition.Id.ToString());
                Assert.That(keys.Add(identity.GreyscaleKey), Is.True, identity.GreyscaleKey);
                Assert.That(frames.Add(identity.FrameKind), Is.True, identity.FrameKind.ToString());
                Assert.That(identity.GreyscaleKey, Does.Not.Contain("#"));
                Assert.That(identity.PeopleName, Does.Contain(identity.RealmName).IgnoreCase);
            }

            Assert.That(keys, Has.Count.EqualTo(4));
            Assert.That(frames, Has.Count.EqualTo(4));
        }

        [Test]
        public void PresentationFontIsNotLegacyRuntime()
        {
            Font font = RealmSelectionIdentity.ResolvePresentationFont();
            Assert.That(font, Is.Not.Null);
            Assert.That(font.name, Does.Not.Contain("LegacyRuntime"));
        }

        [Test]
        public void TallerIdentityGridStillFitsFourRealmCards()
        {
            RealmSelectionGridSpec portrait = RealmSelectionViewportLayout.CalculateGrid(920f, portrait: true);
            float fourCardHeight = portrait.CellSize.y * 4f + portrait.Spacing.y * 3f;
            Assert.That(portrait.ColumnCount, Is.EqualTo(1));
            Assert.That(portrait.CellSize.y, Is.GreaterThanOrEqualTo(280f));
            Assert.That(fourCardHeight, Is.LessThanOrEqualTo(1700f));

            RealmSelectionGridSpec landscape = RealmSelectionViewportLayout.CalculateGrid(1700f, portrait: false);
            Assert.That(landscape.ColumnCount, Is.EqualTo(2));
            Assert.That(landscape.CellSize.y, Is.GreaterThanOrEqualTo(260f));
            Assert.That(landscape.CellSize.x * 2f + landscape.Spacing.x, Is.LessThanOrEqualTo(1700f));
        }

        private void AssertPeople(
            string catalogId,
            string peopleName,
            string markName,
            string silhouette,
            string material)
        {
            RealmCatalogEntry match = null;
            for (int i = 0; i < _catalog.Realms.Count; i++)
            {
                if (_catalog.Realms[i].Id == catalogId)
                {
                    match = _catalog.Realms[i];
                    break;
                }
            }

            Assert.That(match, Is.Not.Null, catalogId);
            Assert.That(match.PeopleName, Is.EqualTo(peopleName));
            Assert.That(match.MarkName, Is.EqualTo(markName));
            Assert.That(match.SilhouetteLanguage, Is.EqualTo(silhouette));
            Assert.That(match.MaterialLanguage, Is.EqualTo(material));
        }

        private static RealmDefinition CreateDefinition(RealmId id)
        {
            var definition = ScriptableObject.CreateInstance<RealmDefinition>();
            definition.Id = id;
            definition.RealmName = id.ToString();
            return definition;
        }
    }
}
