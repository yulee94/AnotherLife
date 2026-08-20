using System.Linq;
using AL.ChampionMode;
using AL.ChampionMode.Quests;
using AL.Core;
using AL.Core.Scenes;
using AL.Data.Runtime;
using AL.UI.CharacterCreation;
using NUnit.Framework;
using UnityEditor;

namespace AL.Tests.EditMode.ChampionMode
{
    /// <summary>
    /// 3D-first spine + C1/lordship lock. Do not retarget to Kingdom-first.
    /// Kingdom-duel coverage lives on a different loop.
    /// </summary>
    public sealed class FirstSessionSpineAndC1LockTests
    {
        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [Test]
        public void EditorBuildSettingsContainsEveryFirstSessionScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.IsNotEmpty(scenes, "EditorBuildSettings must list the first-session path.");

            AssertEnabled("Assets/AL/Scenes/Boot.unity");
            AssertEnabled("Assets/AL/Scenes/RealmSelection.unity");
            AssertEnabled("Assets/AL/Scenes/CharacterCreation.unity");
            AssertEnabled("Assets/AL/Scenes/ChampionArena.unity");

            string[] paths = scenes.Select(scene => scene.path).ToArray();
            int create = System.Array.IndexOf(paths, "Assets/AL/Scenes/CharacterCreation.unity");
            int arena = System.Array.IndexOf(paths, "Assets/AL/Scenes/ChampionArena.unity");
            int kingdom = System.Array.IndexOf(paths, "Assets/AL/Scenes/Kingdom.unity");

            Assert.GreaterOrEqual(create, 0);
            Assert.GreaterOrEqual(arena, 0);
            Assert.Less(create, arena, "CharacterCreation must precede ChampionArena in Build Settings.");
            if (kingdom >= 0)
            {
                Assert.Less(arena, kingdom, "Kingdom may be present but is not the first post-create scene.");
            }
        }

        [Test]
        public void CreateAdvancesToChampionArenaNotKingdom()
        {
            Assert.IsTrue(
                ProductionSceneDescriptor.TryGetById(
                    ProductionSceneDescriptor.CharacterCreationSceneId,
                    out ProductionSceneRecord create));

            SceneTransition arena = null;
            SceneTransition kingdom = null;
            foreach (SceneTransition transition in create.TransitionTargets)
            {
                if (transition.TargetSceneId == ProductionSceneDescriptor.ChampionArenaSceneId)
                {
                    arena = transition;
                }

                if (transition.TargetSceneId == ProductionSceneDescriptor.KingdomSceneId)
                {
                    kingdom = transition;
                }
            }

            Assert.NotNull(arena, "CharacterCreation must load ChampionArena.");
            Assert.AreEqual(TransitionStatus.Active, arena.Status);
            Assert.AreEqual("ChampionArena", arena.SerializedValue);
            Assert.IsNull(kingdom, "CharacterCreation must not target Kingdom as the first post-create scene.");
            Assert.AreEqual("ChampionArena", FirstSessionChampionStart.DestinationSceneName);
            Assert.AreNotEqual("Kingdom", FirstSessionChampionStart.DestinationSceneName);
        }

        [Test]
        public void ChampionArenaDoesNotStartWithoutClassAndUsernameCommit()
        {
            Assert.IsFalse(SliceRunState.HasConfirmedChampion);
            Assert.IsFalse(
                CharacterCreationIdentity.TryClaim(string.Empty, string.Empty, out _, out string blankError));
            Assert.That(blankError, Does.Contain("username").IgnoreCase);

            var realmOnly = new SaveGameData { SelectedRealm = RealmId.Stonehold };
            Assert.AreEqual(
                "RealmSelection",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    realmOnly,
                    "RealmSelection",
                    gameplaySceneLoadable: true));
            Assert.AreNotEqual(
                "ChampionArena",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    realmOnly,
                    "RealmSelection",
                    gameplaySceneLoadable: true));

            var classWithoutConfirm = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = false
                }
            };
            Assert.IsFalse(MvpLoopSaveCodec.Read(classWithoutConfirm).ShouldSkipCreate);
            Assert.AreEqual(
                "RealmSelection",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    classWithoutConfirm,
                    "RealmSelection",
                    gameplaySceneLoadable: true));

            Assert.IsTrue(CharacterCreationIdentity.TryClaim("Stonewarden", string.Empty, out string username, out _));
            SliceRunState.ConfirmChampion(new ChampionState
            {
                Id = "champion_stonehold_vanguard",
                DisplayName = "Vanguard",
                Username = username,
                Family = ClassFamily.Warrior,
                Realm = RealmId.Stonehold
            });
            Assert.IsTrue(SliceRunState.HasConfirmedChampion);
            Assert.AreEqual("Stonewarden", SliceRunState.Champion.Username);
            Assert.AreEqual(ClassFamily.Warrior, SliceRunState.Champion.Family);

            var committed = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true
                }
            };
            Assert.AreEqual(
                FirstUserBootDestinationResolver.GameplaySceneName,
                FirstUserBootDestinationResolver.ResolveSceneName(
                    committed,
                    "RealmSelection",
                    gameplaySceneLoadable: true));
            Assert.AreEqual("ChampionArena", FirstUserBootDestinationResolver.GameplaySceneName);
            Assert.AreNotEqual("Kingdom", FirstUserBootDestinationResolver.GameplaySceneName);
        }

        [Test]
        public void SharedMenuKingdomModuleIsLockedNarrativeUntilC1Mark()
        {
            Assert.AreEqual("MENU_MODULE_KINGDOM_MANAGEMENT", ProofOfWorthLordship.SharedMenuKingdomModuleId);
            Assert.AreEqual("LockedNarrative", ProofOfWorthLordship.SharedMenuLockedNarrative);
            Assert.AreEqual("Available", ProofOfWorthLordship.SharedMenuAvailable);

            var fresh = new SaveGameData();
            Assert.IsFalse(ProofOfWorthLordship.IsGranted(fresh));
            Assert.AreEqual(
                ProofOfWorthLordship.SharedMenuLockedNarrative,
                ProofOfWorthLordship.ResolveSharedMenuKingdomAvailability(fresh));

            var identityOnly = new SaveGameData
            {
                SelectedRealm = RealmId.Crownlands,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "mage",
                    IdentityConfirmed = true,
                    LastResultId = string.Empty
                }
            };
            Assert.AreEqual(
                ProofOfWorthLordship.SharedMenuLockedNarrative,
                ProofOfWorthLordship.ResolveSharedMenuKingdomAvailability(identityOnly));

            var leftoverVictory = new SaveGameData
            {
                SelectedRealm = RealmId.Crownlands,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "mage",
                    IdentityConfirmed = true,
                    LastResultId = "ch01_proof_of_worth:victory"
                }
            };
            Assert.AreEqual(
                ProofOfWorthLordship.SharedMenuLockedNarrative,
                ProofOfWorthLordship.ResolveSharedMenuKingdomAvailability(leftoverVictory));

            Assert.IsTrue(ProofOfWorthLordship.TryWriteMark(identityOnly, ProofOfWorthIds.CrownlandsVariantId));
            Assert.AreEqual("ch01_crownlands", identityOnly.ChampionCustomization.LastResultId);
            Assert.AreEqual(
                ProofOfWorthLordship.SharedMenuAvailable,
                ProofOfWorthLordship.ResolveSharedMenuKingdomAvailability(identityOnly));
        }

        [Test]
        public void Omen1IsOfferedWithAutoAcceptFalse()
        {
            Assert.AreEqual("OMEN_1", ProofOfWorthIds.OmenQuestId);
            Assert.IsFalse(ProofOfWorthIds.AutoAccept);

            ProofOfWorthState offered = ProofOfWorthPlanner.CreateOffered(RealmId.Umbral);
            Assert.AreEqual(ProofOfWorthIds.OmenQuestId, offered.QuestId);
            Assert.AreEqual(ProofOfWorthIds.OmenTalkObjectiveId, offered.ObjectiveId);
            Assert.AreEqual(ProofOfWorthIds.OfferDialogueId, offered.DialogueId);
            Assert.IsTrue(offered.IsOmenOffered);
            Assert.IsFalse(offered.OmenAccepted);
            Assert.IsFalse(offered.AutoAccept);
            Assert.IsFalse(offered.LordshipGranted);

            ProofOfWorthTransition decline = ProofOfWorthPlanner.Apply(
                offered,
                ProofOfWorthCommand.DeclineOffer);
            Assert.AreEqual(ProofOfWorthStatus.DuplicateIgnored, decline.Status);
            Assert.IsTrue(decline.State.IsOmenOffered);
            Assert.IsFalse(decline.State.OmenAccepted);
            Assert.IsFalse(decline.State.AutoAccept);
        }

        private static void AssertEnabled(string path)
        {
            EditorBuildSettingsScene match = null;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path == path)
                {
                    match = scene;
                    break;
                }
            }

            Assert.NotNull(match, path + " must be in EditorBuildSettings.");
            Assert.IsTrue(match.enabled, path + " must be enabled.");
        }
    }
}
