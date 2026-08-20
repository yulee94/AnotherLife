using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Core.Scenes;
using AL.Data.Definitions;
using AL.RealmSelection;
using AL.UI.CharacterCreation;
using AL.UI.RealmSelection;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.CharacterCreation
{
    public sealed class CharacterCreationPresentationTests
    {
        private RealmCatalogSnapshot _catalog;
        private readonly List<ChampionDefinition> _champions = new List<ChampionDefinition>(4);

        [SetUp]
        public void SetUp()
        {
            string path = Path.Combine(Application.dataPath, "AL", "StreamingAssets", "GameData", "realm_specialized.v1.json");
            RealmCatalogLoadResult result = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            Assert.That(result.IsSuccess, Is.True, result.TechnicalCode);
            _catalog = result.Snapshot;
            _champions.Add(CreateChampion("champion_stonehold_vanguard", RealmId.Stonehold, ClassFamily.Warrior));
            _champions.Add(CreateChampion("champion_eldergrove_archmage", RealmId.Eldergrove, ClassFamily.Mage));
            _champions.Add(CreateChampion("champion_crownlands_sharpshooter", RealmId.Crownlands, ClassFamily.Ranger));
            _champions.Add(CreateChampion("champion_umbral_shadowblade", RealmId.Umbral, ClassFamily.Assassin));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _champions.Count; i++)
            {
                Object.DestroyImmediate(_champions[i]);
            }

            _champions.Clear();
        }

        [Test]
        public void CommittedRealmShowsOnlyThatRealmLoadout()
        {
            CharacterCreationPresentationPlan plan = CharacterCreationPresentation.Build(
                RealmId.Stonehold,
                _champions,
                _catalog);

            Assert.That(plan.HasCommittedRealm, Is.True);
            Assert.That(plan.VisibleChampionIds, Is.EquivalentTo(new[] { "champion_stonehold_vanguard" }));
            Assert.That(plan.VisibleChampionIds, Does.Not.Contain("champion_eldergrove_archmage"));
            Assert.That(plan.VisibleChampionIds, Does.Not.Contain("champion_crownlands_sharpshooter"));
            Assert.That(plan.VisibleChampionIds, Does.Not.Contain("champion_umbral_shadowblade"));
        }

        [Test]
        public void IdentityIsStructuralNotColorOnly()
        {
            CharacterCreationPresentationPlan plan = CharacterCreationPresentation.Build(
                RealmId.Umbral,
                _champions,
                _catalog);

            Assert.That(plan.HasStructuralIdentity, Is.True);
            Assert.That(plan.IsColorOnly, Is.False);
            Assert.That(plan.Identity.PeopleName, Is.EqualTo("Umbral Dark Elves"));
            Assert.That(plan.Identity.MarkName, Is.EqualTo("Severed Eclipse"));
            Assert.That(plan.Identity.FrameKind, Is.EqualTo(RealmStructuralFrameKind.SeveredEclipse));
            Assert.That(plan.PeopleCopy, Does.Contain("people are locked"));
            Assert.That(plan.HeraldryCopy, Does.Contain("Severed Eclipse"));
            Assert.That(plan.HeraldryCopy, Does.Not.Contain("#"));
        }

        [Test]
        public void UncommittedRealmDoesNotOfferAnAllRealmPicker()
        {
            CharacterCreationPresentationPlan plan = CharacterCreationPresentation.Build(
                RealmId.None,
                _champions,
                _catalog);

            Assert.That(plan.HasCommittedRealm, Is.False);
            Assert.That(plan.VisibleChampionIds, Is.Empty);
            Assert.That(plan.BindRealmError, Is.EqualTo(CharacterCreationPresentation.BindRealmError));
            Assert.That(plan.Title, Is.Not.EqualTo(CharacterCreationPresentation.AllRealmPickerForbidden));
        }

        [Test]
        public void RemainingLoadoutIsLabelledTemporaryAndDropsDebugBark()
        {
            CharacterCreationPresentationPlan plan = CharacterCreationPresentation.Build(
                RealmId.Eldergrove,
                _champions,
                _catalog);

            Assert.That(plan.TemporaryBadge, Does.Contain("TEMPORARY"));
            Assert.That(plan.Title, Does.Not.Contain(CharacterCreationPresentation.DebugBarkForbidden));
            Assert.That(plan.PeopleCopy, Does.Not.Contain(CharacterCreationPresentation.DebugBarkForbidden));
            Assert.That(plan.HeraldryCopy, Does.Not.Contain(CharacterCreationPresentation.DebugBarkForbidden));
        }

        [Test]
        public void ApprovedEmblemLoadsForCommittedRealm()
        {
            Sprite emblem = CharacterCreationPresentation.TryLoadEmblem(RealmId.Stonehold);
            Assert.That(emblem, Is.Not.Null, "Approved Arcane Axis mark must resolve from RuntimeExports.");
            Assert.That(emblem.texture.width, Is.GreaterThan(8));
            Object.DestroyImmediate(emblem.texture);
            Object.DestroyImmediate(emblem);
        }

        [Test]
        public void ProductionScenesRejectDemoInitializer()
        {
            Assert.That(ProductionDebugChrome.AllowsDemoInitializer("Boot"), Is.False);
            Assert.That(ProductionDebugChrome.AllowsDemoInitializer("RealmSelection"), Is.False);
            Assert.That(ProductionDebugChrome.AllowsDemoInitializer("CharacterCreation"), Is.False);
            Assert.That(ProductionDebugChrome.AllowsDemoInitializer("ChampionArena"), Is.False);
            Assert.That(ProductionDebugChrome.AllowsDemoInitializer("Kingdom"), Is.False);
            Assert.That(ProductionDebugChrome.AllowsDemoInitializer("DemoHarness"), Is.True);
        }

        private static ChampionDefinition CreateChampion(string id, RealmId realm, ClassFamily family)
        {
            var champion = ScriptableObject.CreateInstance<ChampionDefinition>();
            champion.Id = id;
            champion.DisplayName = id;
            champion.Realm = realm;
            champion.Family = family;
            return champion;
        }
    }
}
