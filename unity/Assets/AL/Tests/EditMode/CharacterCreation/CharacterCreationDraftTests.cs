using AL.Core;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.UI.CharacterCreation;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.CharacterCreation
{
    public sealed class CharacterCreationDraftTests
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
        public void RejectsDraftWithoutCommittedRealm()
        {
            Assert.IsFalse(CharacterCreationDraft.TryCreate(RealmId.None, out _, out string error));
            Assert.That(error, Does.Contain("realm"));
        }

        [Test]
        public void OffersAllFourClassFamiliesAfterRealmCommit()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Stonehold, out CharacterCreationDraft draft, out _));
            Assert.AreEqual(4, draft.AvailableFamilies.Length);
            Assert.Contains(ClassFamily.Warrior, draft.AvailableFamilies);
            Assert.Contains(ClassFamily.Mage, draft.AvailableFamilies);
            Assert.Contains(ClassFamily.Ranger, draft.AvailableFamilies);
            Assert.Contains(ClassFamily.Assassin, draft.AvailableFamilies);
        }

        [Test]
        public void PeopleCopyIsLockedToRealmAndIsNotAnEnumName()
        {
            Assert.IsTrue(CharacterCreationLook.TryPeopleLabel(RealmId.Stonehold, out string people));
            Assert.AreEqual("Dwarven people", people);
            Assert.IsFalse(people.Contains("Stonehold"));
            Assert.IsFalse(people.Contains("Dwarves"));
            Assert.IsTrue(CharacterCreationLook.TryClassLabel(ClassFamily.Ranger, out string classLabel));
            Assert.AreEqual("Ranger path", classLabel);
            Assert.AreNotEqual(nameof(ClassFamily.Ranger), classLabel);
        }

        [Test]
        public void SameRealmDraftsCanLookDifferent()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft first, out _));
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft second, out _));
            first.TrySelectClassFamily(ClassFamily.Warrior, out _);
            second.TrySelectClassFamily(ClassFamily.Warrior, out _);
            second.CycleArmorTint();
            second.CycleHairStyle();
            second.ToggleHelmet();
            second.ToggleCape();

            Assert.IsTrue(CharacterCreationLook.LooksDifferent(first.Customization, second.Customization));
            Assert.AreNotEqual(first.Customization.PrimaryR, second.Customization.PrimaryR);
            Assert.AreNotEqual(first.Customization.HairStyleId, second.Customization.HairStyleId);
            Assert.AreNotEqual(first.Customization.HelmetEnabled, second.Customization.HelmetEnabled);
            Assert.AreNotEqual(first.Customization.CapeEnabled, second.Customization.CapeEnabled);
        }

        [Test]
        public void PaletteControlsSetAndPersistSkinHairAndEyeColors()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(
                RealmId.Eldergrove,
                out CharacterCreationDraft draft,
                out _));
            draft.SetSkinToneIndex(3);
            draft.SetHairColorIndex(2);
            draft.SetEyeColorIndex(4);

            var copy = new ChampionCustomizationState();
            CharacterCreationLook.CopyInto(copy, draft.Customization);

            Assert.That(copy.SkinR, Is.EqualTo(CharacterCreationLook.BodyTints[3][0]));
            Assert.That(copy.HairR, Is.EqualTo(CharacterCreationLook.HairColors[2][0]));
            Assert.That(copy.EyeR, Is.EqualTo(CharacterCreationLook.EyeColors[4][0]));
            Assert.That(copy.EyeG, Is.EqualTo(CharacterCreationLook.EyeColors[4][1]));
            Assert.That(copy.EyeB, Is.EqualTo(CharacterCreationLook.EyeColors[4][2]));
            Assert.That(CharacterCreationLook.Matches(copy, draft.Customization), Is.True);
        }

        [Test]
        public void CyclesBetweenMaleAndFemaleBodyBasesAndPersistsDifference()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(
                RealmId.Crownlands,
                out CharacterCreationDraft male,
                out _));
            Assert.IsTrue(CharacterCreationDraft.TryCreate(
                RealmId.Crownlands,
                out CharacterCreationDraft female,
                out _));

            Assert.AreEqual("male", male.Customization.BodyBaseId);
            female.CycleBodyBase();

            Assert.AreEqual("female", female.Customization.BodyBaseId);
            Assert.IsTrue(CharacterCreationLook.LooksDifferent(
                male.Customization,
                female.Customization));
        }

        [Test]
        public void PreBodyBaseSaveJsonLoadsWithMaleCompatibilityDefault()
        {
            const string legacyJson =
                "{\"ChampionCustomization\":{\"ClassFamilyId\":\"warrior\"," +
                "\"BodyPresetId\":\"body_average\"}}";

            SaveGameData loaded = JsonUtility.FromJson<SaveGameData>(legacyJson);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.ChampionCustomization, Is.Not.Null);
            Assert.That(
                CharacterCreationLook.NormalizeBodyBaseId(
                    loaded.ChampionCustomization.BodyBaseId),
                Is.EqualTo("male"));
            var migrated = new ChampionCustomizationState();
            CharacterCreationLook.CopyInto(migrated, loaded.ChampionCustomization);
            Assert.That(migrated.EyeR, Is.EqualTo(CharacterCreationLook.EyeColors[0][0]));
        }

        [Test]
        public void BindChampionPrefersRealmAndClassThenRealm()
        {
            var warriors = CreateChampion("champion_stonehold_vanguard", RealmId.Stonehold, ClassFamily.Warrior);
            var mage = CreateChampion("champion_eldergrove_archmage", RealmId.Eldergrove, ClassFamily.Mage);
            var champions = new[] { warriors, mage };

            ChampionDefinition bound = CharacterCreationDraft.BindChampion(
                champions,
                RealmId.Stonehold,
                ClassFamily.Mage);
            Assert.AreSame(warriors, bound);
        }

        [Test]
        public void PrepareCandidatePersistsLookOnChampionCustomizationWithoutNewTopLevelField()
        {
            var candidate = new SaveGameData
            {
                SelectedRealm = RealmId.Umbral,
                ChampionCustomization = new ChampionCustomizationState()
            };
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Umbral, out CharacterCreationDraft draft, out _));
            Assert.IsTrue(draft.TrySelectClassFamily(ClassFamily.Assassin, out _));
            draft.CycleArmorTint();
            draft.ToggleHelmet();

            var request = new MvpLoopCommitRequest(
                "tx_creator_look",
                RealmId.Umbral,
                ClassFamily.Assassin,
                true,
                string.Empty,
                string.Empty,
                0,
                draft.Customization);

            MvpLoopPrepareDisposition disposition = MvpLoopSaveCodec.PrepareCandidate(
                candidate,
                request,
                out string message);

            Assert.AreEqual(MvpLoopPrepareDisposition.Prepared, disposition, message);
            Assert.AreEqual("assassin", candidate.ChampionCustomization.ClassFamilyId);
            Assert.IsTrue(candidate.ChampionCustomization.IdentityConfirmed);
            Assert.IsTrue(CharacterCreationLook.Matches(candidate.ChampionCustomization, draft.Customization));
            Assert.IsNull(typeof(SaveGameData).GetField("Username"));
            Assert.IsNull(typeof(SaveGameData).GetField("Appearance"));
            Assert.IsNull(typeof(SaveGameData).GetField("CharacterCreation"));
        }

        [Test]
        public void PrepareCandidatePersistsUsernameAndLookAtomically()
        {
            var candidate = new SaveGameData
            {
                SelectedRealm = RealmId.Crownlands,
                ChampionCustomization = new ChampionCustomizationState()
            };
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft draft, out _));
            Assert.IsTrue(draft.TrySelectClassFamily(ClassFamily.Warrior, out _));
            draft.CycleBodyBase();
            draft.CycleBodyPreset();

            MvpLoopPrepareDisposition disposition = MvpLoopSaveCodec.PrepareCandidate(
                candidate,
                new MvpLoopCommitRequest(
                    "tx_creator_identity_and_look",
                    RealmId.Crownlands,
                    ClassFamily.Warrior,
                    true,
                    string.Empty,
                    string.Empty,
                    0,
                    "CrownGuard",
                    draft.Customization),
                out string message);

            Assert.AreEqual(MvpLoopPrepareDisposition.Prepared, disposition, message);
            Assert.AreEqual("CrownGuard", candidate.ChampionCustomization.Username);
            Assert.AreEqual("female", candidate.ChampionCustomization.BodyBaseId);
            Assert.IsTrue(CharacterCreationLook.Matches(candidate.ChampionCustomization, draft.Customization));
        }

        [Test]
        public void TwoLooksOfSameClassAreNotDuplicates()
        {
            var candidate = new SaveGameData
            {
                SelectedRealm = RealmId.Eldergrove,
                ChampionCustomization = new ChampionCustomizationState()
            };
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Eldergrove, out CharacterCreationDraft first, out _));
            first.TrySelectClassFamily(ClassFamily.Ranger, out _);
            MvpLoopSaveCodec.PrepareCandidate(
                candidate,
                new MvpLoopCommitRequest(
                    "tx_a",
                    RealmId.Eldergrove,
                    ClassFamily.Ranger,
                    true,
                    string.Empty,
                    string.Empty,
                    0,
                    first.Customization),
                out _);

            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Eldergrove, out CharacterCreationDraft second, out _));
            second.TrySelectClassFamily(ClassFamily.Ranger, out _);
            second.CycleArmorTint();
            second.CycleHairColor();

            MvpLoopPrepareDisposition secondDisposition = MvpLoopSaveCodec.PrepareCandidate(
                candidate,
                new MvpLoopCommitRequest(
                    "tx_b",
                    RealmId.Eldergrove,
                    ClassFamily.Ranger,
                    true,
                    string.Empty,
                    string.Empty,
                    0,
                    second.Customization),
                out string message);

            Assert.AreEqual(MvpLoopPrepareDisposition.Prepared, secondDisposition, message);
            Assert.IsTrue(CharacterCreationLook.Matches(candidate.ChampionCustomization, second.Customization));
        }

        [Test]
        public void CreatorPreviewUsesProceduralAdultModelNotACapsule()
        {
            string controller = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    UnityEngine.Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationController.cs"));
            Assert.That(controller, Does.Contain("ChampionCustomizationController"));
            Assert.That(controller, Does.Contain("ApplyPresentation"));
            Assert.That(controller, Does.Contain("FirstSessionAuthoredVisualBinder.TryBindChampion"));
            Assert.That(controller, Does.Contain("CreatorPreview"));
            Assert.That(controller, Does.Not.Contain("PrimitiveType.Capsule"));
            Assert.That(controller, Does.Not.Contain("CreatePrimitive"));
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
