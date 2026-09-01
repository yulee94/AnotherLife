using System.Collections.Generic;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Presentation;
using AL.Core;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionPresentationTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void VanguardMeshIsNotPromotedSoBindingSampleIsUsed()
        {
            Assert.IsFalse(ChampionPresentation.VanguardMeshPromoted);
            Assert.AreEqual("procedural_binding_sample", ChampionPresentation.BindingSampleSource);
        }

        [Test]
        public void CrownlandsLocksPeopleToHumansAndCatalogRangerLoadout()
        {
            Assert.IsTrue(ChampionPresentation.TryResolveFromSession(
                RealmId.Crownlands,
                null,
                null,
                Catalog(),
                out ChampionPresentationSpec spec,
                out string diagnostic));
            Assert.AreEqual(string.Empty, diagnostic);
            Assert.AreEqual("Humans", spec.PeopleName);
            Assert.AreEqual(ClassFamily.Ranger, spec.ClassFamily);
            Assert.AreEqual("light_scout", spec.ArmorStyleId);
            Assert.AreEqual("bow", spec.WeaponStyleId);
            Assert.AreEqual("champion_crownlands_sharpshooter", spec.ChampionId);
            Assert.AreEqual(ChampionPresentation.BindingSampleSource, spec.BodySource);
            Assert.IsFalse(spec.UsesPromotedMesh);
            Assert.That(spec.TemporaryParts, Does.Contain("procedural_adult_body"));
            Assert.AreEqual("ClassFamily_Ranger", spec.ClassFamilyTokenName);
            Assert.AreEqual("People_Humans", spec.PeopleTokenName);
        }

        [Test]
        public void StoneholdMageKeepsDwarfPeopleAndShowsMageLoadout()
        {
            Assert.IsTrue(ChampionPresentation.TryResolveFromSession(
                RealmId.Stonehold,
                ClassFamily.Mage,
                "champion_eldergrove_archmage",
                Catalog(),
                out ChampionPresentationSpec spec,
                out _));
            Assert.AreEqual("Dwarves", spec.PeopleName);
            Assert.AreEqual("stout", spec.BodyPresetId);
            Assert.AreEqual(ClassFamily.Mage, spec.ClassFamily);
            Assert.AreEqual("arcane_robes", spec.ArmorStyleId);
            Assert.AreEqual("staff", spec.WeaponStyleId);
            Assert.AreEqual("tome", spec.OffhandStyleId);
            Assert.AreEqual("champion_stonehold_vanguard", spec.ChampionId);
        }

        [Test]
        public void ForeignChampionIdDoesNotSwapPeople()
        {
            Assert.IsTrue(ChampionPresentation.TryResolveFromSession(
                RealmId.Crownlands,
                ClassFamily.Warrior,
                "champion_umbral_shadowblade",
                Catalog(),
                out ChampionPresentationSpec spec,
                out _));
            Assert.AreEqual("Humans", spec.PeopleName);
            Assert.AreNotEqual("Dark Elves", spec.PeopleName);
            Assert.AreEqual(ClassFamily.Warrior, spec.ClassFamily);
            Assert.AreEqual("heavy_plate", spec.ArmorStyleId);
            Assert.AreEqual("shield", spec.OffhandStyleId);
            Assert.AreNotEqual("champion_umbral_shadowblade", spec.ChampionId);
        }

        [Test]
        public void EachClassFamilyHasVisibleLoadoutWithoutNewAbilityIds()
        {
            AssertLoadout(ClassFamily.Warrior, "heavy_plate", "axe", "shield");
            AssertLoadout(ClassFamily.Mage, "arcane_robes", "staff", "tome");
            AssertLoadout(ClassFamily.Ranger, "light_scout", "bow", "none");
            AssertLoadout(ClassFamily.Assassin, "assassin_leathers", "sword", "dagger");
        }

        [Test]
        public void CatalogWeaponsMapOntoExistingParts()
        {
            Assert.AreEqual("axe", ChampionPresentation.MapWeaponStyle("greataxe"));
            Assert.AreEqual("bow", ChampionPresentation.MapWeaponStyle("longbow"));
            Assert.AreEqual("sword", ChampionPresentation.MapWeaponStyle("twinblades"));
            Assert.AreEqual("staff", ChampionPresentation.MapWeaponStyle("staff"));
            Assert.AreEqual("shield", ChampionPresentation.MapOffhandStyle("towershield"));
            Assert.AreEqual("none", ChampionPresentation.MapOffhandStyle("quiver"));
            Assert.AreEqual("dagger", ChampionPresentation.MapOffhandStyle("shroud"));
            Assert.AreEqual("tome", ChampionPresentation.MapOffhandStyle("tome"));
        }

        [Test]
        public void UnsupportedRealmFailsClosed()
        {
            Assert.IsFalse(ChampionPresentation.TryResolveFromSession(
                RealmId.None,
                ClassFamily.Warrior,
                null,
                Catalog(),
                out _,
                out string diagnostic));
            Assert.That(diagnostic, Does.Contain("AL-CHAMPION-PRESENTATION-REALM"));
        }

        [Test]
        public void FirstSessionRootIsNotAVisibleCapsule()
        {
            _root = ChampionPresentationBinder.CreateChampionRoot(new Vector3(0f, 1.1f, -7.4f));
            Assert.AreEqual(FirstSessionChampionStart.PlayerObjectName, _root.name);
            Assert.IsFalse(ChampionPresentationBinder.RootLooksLikeCapsule(_root));
            Assert.IsNull(_root.GetComponent<MeshRenderer>());
            Assert.IsNull(_root.GetComponent<MeshFilter>());
            CharacterController controller = _root.GetComponent<CharacterController>();
            Assert.NotNull(controller);
            Assert.IsNull(_root.GetComponent<CapsuleCollider>());
            Assert.That(controller.center, Is.EqualTo(Vector3.zero));
            Assert.That(controller.height, Is.EqualTo(2f));
            Assert.That(controller.radius, Is.EqualTo(0.45f));
            Assert.That(controller.minMoveDistance, Is.Zero);
        }

        [Test]
        public void ChampionMotorPreservesThePresentationOwnedPivotContract()
        {
            _root = new GameObject("FootPivotChampionContract");
            CharacterController controller = _root.AddComponent<CharacterController>();
            controller.center = Vector3.up;
            controller.height = 2f;
            controller.radius = 0.34f;
            controller.stepOffset = 0.3f;

            ChampionController motor = _root.AddComponent<ChampionController>();
            MethodInfo awake = typeof(ChampionController).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(motor, null);

            Assert.That(controller.center, Is.EqualTo(Vector3.up));
            Assert.That(controller.height, Is.EqualTo(2f));
            Assert.That(controller.radius, Is.EqualTo(0.34f));
            Assert.That(controller.stepOffset, Is.EqualTo(0.3f));
            Assert.That(controller.minMoveDistance, Is.Zero);
        }

        [Test]
        public void FirstSessionAuthoredPresentationHasNoTemporaryPlaqueContract()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            Assert.That(FirstSessionChampionStart.EnvironmentRootName,
                Does.Not.Contain("TEMPORARY"));
            Assert.That(FirstSessionChampionStart.LandingFeedCopy,
                Does.Not.Contain("TEMPORARY"));
        }

        private static void AssertLoadout(
            ClassFamily classFamily,
            string armor,
            string weapon,
            string offhand)
        {
            Assert.IsTrue(ChampionPresentation.TryResolveFromSession(
                RealmId.Crownlands,
                classFamily,
                null,
                Catalog(),
                out ChampionPresentationSpec spec,
                out _));
            Assert.AreEqual("Humans", spec.PeopleName);
            Assert.AreEqual(armor, spec.ArmorStyleId);
            Assert.AreEqual(weapon, spec.WeaponStyleId);
            Assert.AreEqual(offhand, spec.OffhandStyleId);
        }

        private static IReadOnlyList<ChampionPresentationCatalogEntry> Catalog()
        {
            return new[]
            {
                new ChampionPresentationCatalogEntry(
                    "champion_stonehold_vanguard",
                    "Bronn Ironhide",
                    RealmId.Stonehold,
                    ClassFamily.Warrior,
                    "greataxe",
                    "towershield",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_stonehold_vanguard_turnaround_v001.png",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_stonehold_vanguard_turnaround_v001.png"),
                new ChampionPresentationCatalogEntry(
                    "champion_eldergrove_archmage",
                    "Lyra Moonshadow",
                    RealmId.Eldergrove,
                    ClassFamily.Mage,
                    "staff",
                    "tome",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_eldergrove_vanguard_turnaround_v001.png",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_eldergrove_vanguard_turnaround_v001.png"),
                new ChampionPresentationCatalogEntry(
                    "champion_crownlands_sharpshooter",
                    "Aurelia Dawnblade",
                    RealmId.Crownlands,
                    ClassFamily.Ranger,
                    "longbow",
                    "quiver",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_crownlands_vanguard_turnaround_v001.png"),
                new ChampionPresentationCatalogEntry(
                    "champion_umbral_shadowblade",
                    "Vex Nocturne",
                    RealmId.Umbral,
                    ClassFamily.Assassin,
                    "twinblades",
                    "shroud",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_umbral_vanguard_turnaround_v001.png",
                    "Assets/AL/Art/Champions/ConceptSheets/champion_umbral_vanguard_turnaround_v001.png")
            };
        }
    }
}
