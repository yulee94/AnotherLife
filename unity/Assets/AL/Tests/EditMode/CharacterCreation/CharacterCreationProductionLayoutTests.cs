using AL.Core;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.UI.CharacterCreation;
using AL.UI.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.CharacterCreation
{
    public sealed class CharacterCreationProductionLayoutTests
    {
        [SetUp]
        public void SetUp()
        {
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [Test]
        public void ProductionSurfaceUsesSharedChromeNotLegacyRuntime()
        {
            string controller = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationController.cs"));
            string layout = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationProductionLayout.cs"));

            Assert.That(controller, Does.Contain("PresentationChrome"));
            Assert.That(controller, Does.Contain("CharacterCreationProductionLayout"));
            Assert.That(controller, Does.Contain("ChampionCustomizationController"));
            Assert.That(controller, Does.Contain("CreatorPreview"));
            Assert.That(controller, Does.Contain("ChampionArena"));
            Assert.That(controller, Does.Not.Contain("LegacyRuntime"));
            Assert.That(controller, Does.Not.Contain("PrimitiveType.Capsule"));
            Assert.That(controller, Does.Not.Contain("CreatePrimitive"));
            Assert.That(controller, Does.Not.Contain("Kingdom"));
            Assert.That(layout, Does.Contain("PresentationChrome"));
            Assert.That(layout, Does.Contain("ValidationBanner"));
            Assert.That(layout, Does.Contain("CommittedHeraldry"));
            Assert.That(layout, Does.Not.Contain("LegacyRuntime"));
        }

        [Test]
        public void UsernameValidationIsPresentedOnTheBannerNotLogged()
        {
            string controller = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationController.cs"));
            Assert.That(controller, Does.Contain("PresentValidation"));
            Assert.That(controller, Does.Contain("usernameError"));
            Assert.That(controller, Does.Not.Contain("Debug.Log"));
        }

        [Test]
        public void PeopleCopyIsLockedAndHeraldryResolvesForEachRealm()
        {
            Assert.IsTrue(CharacterCreationLook.TryPeopleLabel(RealmId.Stonehold, out string people));
            Assert.AreEqual("Dwarven people", people);
            Assert.That(people, Does.Not.Contain("Dwarves"));
            Assert.That(
                CharacterCreationProductionLayout.ResolveHeraldryCopy(RealmId.Crownlands),
                Does.Contain("Celestial Meridian"));
            Assert.That(
                CharacterCreationProductionLayout.ResolveHeraldryCopy(RealmId.Umbral),
                Does.Contain("Severed Eclipse"));
        }

        [Test]
        public void TwoSameRealmLooksStayDifferentOnTheSurface()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft first, out _));
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft second, out _));
            first.TrySelectClassFamily(ClassFamily.Warrior, out _);
            second.TrySelectClassFamily(ClassFamily.Warrior, out _);
            second.CycleArmorTint();
            second.CycleHairStyle();
            second.ToggleHelmet();

            string left = CharacterCreationProductionLayout.FormatLookSummary(first.Customization);
            string right = CharacterCreationProductionLayout.FormatLookSummary(second.Customization);
            Assert.AreNotEqual(left, right);
            Assert.IsTrue(CharacterCreationLook.LooksDifferent(first.Customization, second.Customization));
        }

        [Test]
        public void ProductionCreatorExposesAndSummarizesBodyBaseSelection()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(
                RealmId.Crownlands,
                out CharacterCreationDraft draft,
                out _));
            Font font = PresentationChrome.ResolveFont();
            CharacterCreationProductionScreen screen = CharacterCreationProductionLayout.Build(
                draft,
                font,
                _ => { },
                () => { },
                () => { },
                _ => { },
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { },
                () => { },
                () => { });

            Assert.IsNotNull(screen.CanvasObject.transform.Find("BodyBase"));
            Assert.That(
                CharacterCreationProductionLayout.FormatLookSummary(draft.Customization),
                Does.Contain("base male"));
            draft.CycleBodyBase();
            Assert.That(
                CharacterCreationProductionLayout.FormatLookSummary(draft.Customization),
                Does.Contain("base female"));

            Object.DestroyImmediate(screen.CanvasObject);
        }

        [Test]
        public void BuildPresentsClassLookUsernameAndValidationBanner()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Stonehold, out CharacterCreationDraft draft, out _));
            Font font = PresentationChrome.ResolveFont();
            CharacterCreationProductionScreen screen = CharacterCreationProductionLayout.Build(
                draft,
                font,
                _ => { },
                () => { },
                () => { },
                _ => { },
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { },
                () => { },
                () => { });

            Assert.That(screen.CanvasObject.name, Is.EqualTo(CharacterCreationProductionLayout.CanvasName));
            Assert.AreEqual(4, screen.ClassCards.Count);
            Assert.IsNotNull(screen.Username);
            Assert.IsNotNull(screen.Confirm);
            Assert.IsNotNull(screen.SkinTone);
            Assert.IsNotNull(screen.HairColor);
            Assert.IsNotNull(screen.EyeColor);
            Assert.IsNotNull(screen.SkinSwatch);
            Assert.IsNotNull(screen.HairSwatch);
            Assert.IsNotNull(screen.EyeSwatch);
            Assert.IsNotNull(screen.StatusPlate);
            Assert.AreEqual(CharacterCreationProductionLayout.ValidationBannerName, screen.StatusPlate.gameObject.name);
            Assert.IsFalse(screen.StatusPlate.gameObject.activeSelf);
            Assert.That(screen.People.text, Does.Contain("Dwarven people"));
            Assert.That(screen.People.text, Does.Not.Contain("enum"));

            CharacterCreationProductionLayout.PresentValidation(screen, "Enter a username.");
            Assert.IsTrue(screen.StatusPlate.gameObject.activeSelf);
            Assert.AreEqual("Enter a username.", screen.Status.text);

            CharacterCreationIdentity.TryClaim("TakenName", string.Empty, out _, out _);
            Assert.IsFalse(CharacterCreationIdentity.TryClaim("takenname", string.Empty, out _, out string duplicate));
            CharacterCreationProductionLayout.PresentValidation(screen, duplicate);
            Assert.AreEqual("That username is already taken.", screen.Status.text);

            Object.DestroyImmediate(screen.CanvasObject);
        }

        [Test]
        public void EmblemPathMatchesApprovedArcaneAxisAsset()
        {
            Assert.IsTrue(GameDataRealmReferences.TryGetByLegacyIdentity(
                "Stonehold",
                (int)RealmId.Stonehold,
                out GameDataRealmReference reference));
            Assert.That(reference.AssetReference, Does.Contain("S_ArcaneAxis_Stonehold_Flat_256_v001.png"));
            string path = System.IO.Path.Combine(
                Application.dataPath,
                reference.AssetReference.Replace("Assets/", string.Empty).Replace('/', System.IO.Path.DirectorySeparatorChar));
            Assert.IsTrue(System.IO.File.Exists(path), path);
        }
    }
}
