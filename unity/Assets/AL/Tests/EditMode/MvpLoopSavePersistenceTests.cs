using System;
using System.IO;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.CharacterCreation;
using AL.UI.FirstUserIdentity;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public sealed class MvpLoopSavePersistenceTests
    {
        [SetUp]
        public void SetUp()
        {
            SliceRunState.Reset();
            CharacterCreationIdentity.ResetClaims();
        }

        [TearDown]
        public void TearDown()
        {
            SliceRunState.Reset();
            CharacterCreationIdentity.ResetClaims();
        }

        private const string CurrentSaveFormatId = "anotherlife.local-save";
        private const string LastResultId = "ch01_proof_of_worth:victory";
        private const string Username = "Banner_01";
        private const string PreChangeCustomizationJson =
            "{\"BodyPresetId\":\"average\",\"HairStyleId\":\"short\"," +
            "\"ArmorStyleId\":\"realm_basic\",\"FaceMarkId\":\"none\"," +
            "\"WeaponStyleId\":\"sword\",\"OffhandStyleId\":\"shield\"," +
            "\"PrimaryR\":0.2,\"PrimaryG\":0.4,\"PrimaryB\":1.0," +
            "\"HairR\":0.08,\"HairG\":0.06,\"HairB\":0.04," +
            "\"SkinR\":0.72,\"SkinG\":0.56,\"SkinB\":0.42," +
            "\"EyeR\":0.25,\"EyeG\":0.58,\"EyeB\":0.92," +
            "\"AccentR\":0.85,\"AccentG\":0.62,\"AccentB\":0.18," +
            "\"CapeEnabled\":true,\"HelmetEnabled\":false}";

        [Test]
        public void PersistenceSlotIsChampionCustomizationNotANewTopLevelField()
        {
            Assert.AreEqual(
                "SaveGameData.ChampionCustomization",
                MvpLoopSaveCodec.PersistenceSlot);
            Assert.AreEqual(
                "SaveGameData.Buildings",
                MvpLoopSaveCodec.OneBuildSlot);
            Assert.AreEqual(
                "SaveGameData.SelectedRealm",
                MvpLoopSaveCodec.RealmSlot);
            Assert.IsNull(typeof(SaveGameData).GetField("MvpLoop"));
            Assert.IsNull(typeof(SaveGameData).GetField("FirstUserIdentity"));
            Assert.IsNull(typeof(SaveGameData).GetField("Username"));
            Assert.IsNull(typeof(ChampionCustomizationState).GetField("People"));
            Assert.IsNull(typeof(ChampionCustomizationState).GetField("Race"));
            Assert.NotNull(typeof(ChampionCustomizationState).GetField("ClassFamilyId"));
            Assert.NotNull(typeof(ChampionCustomizationState).GetField("IdentityConfirmed"));
            Assert.NotNull(typeof(ChampionCustomizationState).GetField("LastResultId"));
            Assert.NotNull(typeof(ChampionCustomizationState).GetField("Username"));
        }

        [Test]
        public void PreChangeSchemaV1SaveLoadsWithoutIdentityAndDoesNotSkipCreate()
        {
            string root = NewRoot();
            try
            {
                File.WriteAllText(Path.Combine(root, "save.json"), PreChangeSchemaV1Json());
                ISaveGameService service = CreateSaveService(root);
                service.Load();

                Assert.NotNull(service.CurrentSave);
                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(service.CurrentSave);
                Assert.AreEqual(RealmId.Stonehold, snapshot.Realm);
                Assert.IsFalse(snapshot.ClassFamily.HasValue);
                Assert.IsFalse(snapshot.IdentityConfirmed);
                Assert.IsFalse(snapshot.ShouldSkipCreate);
                Assert.AreEqual(
                    "RealmSelection",
                    FirstUserBootDestinationResolver.ResolveSceneName(
                        service,
                        "RealmSelection",
                        gameplaySceneLoadable: true));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void FreshMvpSaveReloadsConfirmedChampionLastResultAndOneBuild()
        {
            string root = NewRoot();
            try
            {
                ISaveGameService writer = CreateSaveService(root);
                writer.CreateNewSave(RealmId.Eldergrove);

                MvpLoopCommitResult commit = MvpLoopSaveAuthority.TryCommit(
                    writer,
                    new MvpLoopCommitRequest(
                        Guid.NewGuid().ToString("N"),
                        RealmId.Eldergrove,
                        ClassFamily.Ranger,
                        true,
                        LastResultId,
                        MvpLoopSaveCodec.DefaultOneBuildId,
                        1,
                        Username));
                Assert.IsTrue(commit.Accepted, commit.Message);
                Assert.IsTrue(commit.Persisted, commit.Message);

                ISaveGameService reader = CreateSaveService(root);
                reader.Load();
                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(reader.CurrentSave);
                Assert.AreEqual(RealmId.Eldergrove, snapshot.Realm);
                Assert.AreEqual(ClassFamily.Ranger, snapshot.ClassFamily);
                Assert.AreEqual(FirstUserRace.Elves, snapshot.People);
                Assert.AreEqual(Username, snapshot.Username);
                Assert.IsTrue(snapshot.IdentityConfirmed);
                Assert.IsTrue(snapshot.ShouldSkipCreate);
                Assert.AreEqual(LastResultId, snapshot.LastResultId);
                Assert.AreEqual(MvpLoopSaveCodec.DefaultOneBuildId, snapshot.LastBuildId);
                Assert.AreEqual(1, snapshot.LastBuildLevel);
                Assert.AreEqual(
                    FirstUserBootDestinationResolver.GameplaySceneName,
                    FirstUserBootDestinationResolver.ResolveSceneName(
                        reader,
                        "RealmSelection",
                        gameplaySceneLoadable: true));
                Assert.AreEqual(
                    "RealmSelection",
                    FirstUserBootDestinationResolver.ResolveSceneName(
                        reader,
                        "RealmSelection",
                        gameplaySceneLoadable: false));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void DuplicateMvpCommitIsIdempotentAndDoesNotClearIdentity()
        {
            string root = NewRoot();
            try
            {
                ISaveGameService service = CreateSaveService(root);
                service.CreateNewSave(RealmId.Crownlands);
                var request = new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    RealmId.Crownlands,
                    ClassFamily.Warrior,
                    true,
                    LastResultId,
                    MvpLoopSaveCodec.DefaultOneBuildId,
                    1,
                    Username);
                Assert.IsTrue(MvpLoopSaveAuthority.TryCommit(service, request).Persisted);
                MvpLoopCommitResult replay = MvpLoopSaveAuthority.TryCommit(service, request);
                Assert.IsTrue(replay.Accepted, replay.Message);
                Assert.IsFalse(replay.Persisted, replay.Message);
                Assert.IsTrue(MvpLoopSaveCodec.Read(service.CurrentSave).ShouldSkipCreate);
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void BootResolverNeverRoutesConfirmedOrRealmOnlySavesToKingdom()
        {
            var realmOnly = new SaveGameData { SelectedRealm = RealmId.Umbral };
            Assert.AreEqual(
                "RealmSelection",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    realmOnly,
                    "RealmSelection",
                    gameplaySceneLoadable: true));

            var confirmed = new SaveGameData
            {
                SelectedRealm = RealmId.Umbral,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "assassin",
                    IdentityConfirmed = true,
                    LastResultId = LastResultId,
                    Username = Username
                }
            };
            Assert.AreNotEqual(
                "Kingdom",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    confirmed,
                    "RealmSelection",
                    gameplaySceneLoadable: true));
            Assert.AreNotEqual(
                "Kingdom",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    confirmed,
                    "RealmSelection",
                    gameplaySceneLoadable: false));
        }

        [Test]
        public void PreChangeCustomizationJsonStaysSemanticallyValid()
        {
            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                System.Text.Encoding.UTF8.GetBytes(PreChangeSchemaV1Json()),
                SaveCandidateSourceGeneration.Primary,
                CreateSemanticPolicy());
            Assert.AreEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            Assert.True(candidate.IsWritable);
        }

        [Test]
        public void InvalidClassFamilyIdIsRejectedBySemanticValidation()
        {
            string json = PreChangeSchemaV1Json().Replace(
                "\"HelmetEnabled\":false}",
                "\"HelmetEnabled\":false,\"ClassFamilyId\":\"paladin\"}");
            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                System.Text.Encoding.UTF8.GetBytes(json),
                SaveCandidateSourceGeneration.Primary,
                CreateSemanticPolicy());
            Assert.AreNotEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            bool sawInvalidClass = false;
            for (int i = 0; i < candidate.Diagnostics.Count; i++)
            {
                if (candidate.Diagnostics[i].Code == "SAVE_MVP_CLASS_FAMILY_INVALID")
                {
                    sawInvalidClass = true;
                    break;
                }
            }

            Assert.IsTrue(sawInvalidClass);
        }

        [Test]
        public void ConfirmedIdentityWithoutUsernameStaysOnCreate()
        {
            var nameless = new SaveGameData
            {
                SelectedRealm = RealmId.Stonehold,
                ChampionCustomization = new ChampionCustomizationState
                {
                    ClassFamilyId = "warrior",
                    IdentityConfirmed = true,
                    LastResultId = LastResultId
                }
            };

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(nameless);
            Assert.IsTrue(snapshot.IdentityConfirmed);
            Assert.IsFalse(snapshot.ShouldSkipCreate);
            Assert.AreEqual(FirstUserRace.Dwarves, snapshot.People);
            Assert.AreEqual(
                "RealmSelection",
                FirstUserBootDestinationResolver.ResolveSceneName(
                    nameless,
                    "RealmSelection",
                    gameplaySceneLoadable: true));
        }

        [Test]
        public void CreateThenReloadKeepsRealmClassUsername()
        {
            string root = NewRoot();
            try
            {
                ISaveGameService writer = CreateSaveService(root);
                writer.CreateNewSave(RealmId.Umbral);
                MvpLoopCommitResult commit = MvpLoopSaveAuthority.TryCommit(
                    writer,
                    new MvpLoopCommitRequest(
                        Guid.NewGuid().ToString("N"),
                        RealmId.Umbral,
                        ClassFamily.Assassin,
                        true,
                        string.Empty,
                        string.Empty,
                        0,
                        Username));
                Assert.IsTrue(commit.Accepted, commit.Message);
                Assert.IsTrue(commit.Persisted, commit.Message);

                SliceRunState.Reset();
                CharacterCreationIdentity.ResetClaims();

                ISaveGameService reader = CreateSaveService(root);
                reader.Load();
                MvpLoopSaveCodec.RestoreSessionIdentity(reader.CurrentSave);
                MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(reader.CurrentSave);
                Assert.AreEqual(RealmId.Umbral, snapshot.Realm);
                Assert.AreEqual(ClassFamily.Assassin, snapshot.ClassFamily);
                Assert.AreEqual(FirstUserRace.DarkElves, snapshot.People);
                Assert.AreEqual(Username, snapshot.Username);
                Assert.IsTrue(snapshot.ShouldSkipCreate);
                Assert.IsTrue(SliceRunState.HasConfirmedChampion);
                Assert.AreEqual(Username, SliceRunState.Champion.Username);
                Assert.AreEqual(ClassFamily.Assassin, SliceRunState.Champion.Family);
                Assert.AreEqual(RealmId.Umbral, SliceRunState.Champion.Realm);
                Assert.IsFalse(
                    CharacterCreationIdentity.TryClaim("banner_01", string.Empty, out _, out string error));
                Assert.That(error, Does.Contain("already taken"));
            }
            finally
            {
                DeleteRoot(root);
            }
        }

        [Test]
        public void InvalidUsernameIsRejectedBySemanticValidation()
        {
            string json = PreChangeSchemaV1Json().Replace(
                "\"HelmetEnabled\":false}",
                "\"HelmetEnabled\":false,\"Username\":\"bad name\"}");
            SaveSemanticCandidate candidate = SaveSemanticCandidateValidator.Validate(
                System.Text.Encoding.UTF8.GetBytes(json),
                SaveCandidateSourceGeneration.Primary,
                CreateSemanticPolicy());
            Assert.AreNotEqual(SaveSemanticCandidateOutcome.Valid, candidate.Outcome);
            bool sawInvalidUsername = false;
            for (int i = 0; i < candidate.Diagnostics.Count; i++)
            {
                if (candidate.Diagnostics[i].Code == "SAVE_MVP_USERNAME_INVALID")
                {
                    sawInvalidUsername = true;
                    break;
                }
            }

            Assert.IsTrue(sawInvalidUsername);
        }

        private static string PreChangeSchemaV1Json()
        {
            return "{" +
                   "\"SaveFormatId\":\"" + CurrentSaveFormatId + "\"," +
                   "\"SaveSchemaVersion\":1," +
                   "\"ProfileInitializationVersion\":1," +
                   "\"SelectedRealm\":1," +
                   "\"Resources\":[" +
                   "{\"Type\":0,\"Amount\":1000}," +
                   "{\"Type\":1,\"Amount\":1000}," +
                   "{\"Type\":2,\"Amount\":500}," +
                   "{\"Type\":3,\"Amount\":500}," +
                   "{\"Type\":4,\"Amount\":150}," +
                   "{\"Type\":5,\"Amount\":150}," +
                   "{\"Type\":6,\"Amount\":0}," +
                   "{\"Type\":7,\"Amount\":0}," +
                   "{\"Type\":8,\"Amount\":0}," +
                   "{\"Type\":9,\"Amount\":0}]," +
                   "\"Buildings\":[],\"Troops\":[],\"Researches\":[]," +
                   "\"Quests\":[],\"Reputation\":[],\"FactionReputations\":[]," +
                   "\"LordPersona\":{\"Warlord\":0,\"Diplomat\":0,\"Sage\":0,\"Rogue\":0}," +
                   "\"Territories\":[],\"RealmGems\":[]," +
                   "\"Wishgate\":{\"IsEarned\":false,\"EarnReason\":\"\",\"LastRewardId\":\"\",\"LastRewardChosenTimestamp\":0}," +
                   "\"CurrentChapterId\":\"C1\"," +
                   "\"Warmaster\":{\"EquippedSetId\":\"\",\"UnlockedSetIds\":[],\"PurchasedPieceIds\":[],\"IsTrueWarmaster\":false,\"Level\":0,\"Experience\":0}," +
                   "\"ChampionCustomization\":" + PreChangeCustomizationJson + "," +
                   "\"OwnedEquipment\":[],\"AppliedBossLootRewards\":[]," +
                   "\"Nvs01Progress\":{\"Version\":0,\"PacketVersion\":\"\",\"PacketSha256\":\"\"," +
                   "\"QuestId\":\"\",\"Revision\":0,\"StateId\":\"\",\"Objectives\":[]," +
                   "\"CurrentDialogueNodeId\":\"\",\"PendingChoice\":false," +
                   "\"PendingSemanticActionId\":\"\",\"CommittedRealmId\":\"\"," +
                   "\"EncounterStatus\":0,\"HasCurrentEncounter\":false," +
                   "\"CurrentEncounter\":{\"ContractVersion\":0,\"RequestId\":\"\"," +
                   "\"CorrelationId\":\"\",\"QuestId\":\"\",\"StateId\":\"\"," +
                   "\"ObjectiveId\":\"\",\"HookId\":\"\",\"LocationId\":\"\"," +
                   "\"RealmId\":\"\",\"SuccessEventId\":\"\",\"FailureEventId\":\"\"," +
                   "\"CancelledEventId\":\"\",\"UnavailableEventId\":\"\"," +
                   "\"ReturnScene\":\"\"},\"LastEncounterCorrelationId\":\"\"," +
                   "\"HasLastEncounterOutcome\":false,\"LastEncounterOutcome\":0," +
                   "\"LastEncounterEventId\":\"\",\"LastEncounterSnapshotVersion\":\"\"," +
                   "\"LastEncounterSnapshotReference\":\"\",\"HasLastOperation\":false," +
                   "\"LastOperation\":{\"OperationId\":\"\",\"PayloadFingerprint\":\"\"," +
                   "\"Status\":0,\"Revision\":0,\"StateId\":\"\",\"EventId\":\"\"," +
                   "\"CorrelationId\":\"\"},\"ConsequenceIntentIds\":[]," +
                   "\"AcquiredArtifactIds\":[],\"AppliedEffectKeys\":[]," +
                   "\"UnlockedChapterId\":\"\"}," +
                   "\"WarzoneCredits\":0,\"LastSavedTimestamp\":123}";
        }

        private static SaveSemanticValidationPolicy CreateSemanticPolicy()
        {
            var authority = new SaveSemanticValidationAuthority(
                new[] { 0, 1, 2, 3, 4 },
                new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                new[] { 0, 1, 2, 3 },
                new[] { 0, 1, 2, 3, 4, 5 },
                Array.Empty<SaveSemanticQuestRule>(),
                new[]
                {
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.Chapter, "C1"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.BodyPreset, "average"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.HairStyle, "short"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.ArmorStyle, "realm_basic"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.FaceMark, "none"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.WeaponStyle, "sword"),
                    new SaveSemanticStableIdRule(SaveSemanticStableIdKind.OffhandStyle, "shield"),
                    new SaveSemanticStableIdRule(
                        SaveSemanticStableIdKind.Building,
                        MvpLoopSaveCodec.DefaultOneBuildId)
                });
            return new SaveSemanticValidationPolicy(
                CurrentSaveFormatId,
                1,
                1,
                authority,
                maximumInputBytes: 1024 * 1024);
        }

        private static ISaveGameService CreateSaveService(string root)
        {
            Type serviceType = typeof(LocalSaveGameService);
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return (ISaveGameService)constructor.Invoke(new object[] { root });
        }

        private static string NewRoot()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "AnotherLife-MvpLoopSaveTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteRoot(string root)
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
