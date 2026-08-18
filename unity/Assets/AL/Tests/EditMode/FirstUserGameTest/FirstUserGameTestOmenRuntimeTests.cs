#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Editor.Development.FirstUserGameTest;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AL.Tests.EditMode.FirstUserGameTest
{
    [TestFixture]
    public sealed class FirstUserGameTestOmenRuntimeTests
    {
        private const string SessionA = "2123456789abcdef0123456789abcdef";
        private const string SessionB = "3123456789abcdef0123456789abcdef";
        private const string GenerationA =
            "2123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string GenerationB =
            "3123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static readonly RealmId[] PlayableRealms =
        {
            RealmId.Crownlands,
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Umbral
        };

        [SetUp]
        public void SetUp()
        {
            EraseKnownSessions();
        }

        [TearDown]
        public void TearDown()
        {
            EraseKnownSessions();
        }

        [Test]
        public void CanonicalLoaderPinsExactCurrentV003Artifact()
        {
            Assert.That(
                FirstUserGameTestOmenCatalogLoader.TryLoad(
                    out Nvs01VerifiedCatalog catalog,
                    out string diagnostic),
                Is.True,
                diagnostic);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.CatalogId, Is.EqualTo(Nvs01CatalogContract.CatalogId));
            Assert.That(catalog.Catalog.PacketVersion,
                Is.EqualTo("omen1-a1-2026-07-29-v003"));
            Assert.That(catalog.Catalog.QuestId, Is.EqualTo("OMEN_1"));
            Assert.That(catalog.CanonicalByteLength, Is.EqualTo(8317));
            Assert.That(catalog.CanonicalSha256,
                Is.EqualTo("8bec0bee9e591d0b19d16760f597f7c8e6c34f128ea7f98edd18c5a934dc4732"));
            Assert.That(catalog.Catalog.Placement.AutoAccept, Is.False);
            Assert.That(catalog.Catalog.Placement.OfferAction, Is.EqualTo("SELECT_VALERIUS"));
            Assert.That(
                AssetDatabase.GUIDToAssetPath(
                    AssetDatabase.AssetPathToGUID(
                        FirstUserGameTestOmenContract.CanonicalAssetPath)),
                Is.EqualTo(FirstUserGameTestOmenContract.CanonicalAssetPath));
        }

        [TestCase(RealmId.Crownlands, "crownlands")]
        [TestCase(RealmId.Stonehold, "stonehold")]
        [TestCase(RealmId.Eldergrove, "eldergrove")]
        [TestCase(RealmId.Umbral, "umbral")]
        public void SelectValeriusCommitsOnlyExactPendingOfferForEveryRealm(
            RealmId realm,
            string expectedRealmId)
        {
            FirstUserGameTestOmenInteraction interaction = Create(
                SessionA,
                GenerationA,
                realm);
            AssertInitial(interaction);

            Assert.That(interaction.TryOpenReport(
                out bool changed,
                out string friendly,
                out string diagnostic), Is.True, friendly + diagnostic);
            Assert.That(changed, Is.True);
            Assert.That(interaction.SelectValeriusInvocationCount, Is.EqualTo(1));
            Assert.That(interaction.CommitAttemptCount, Is.EqualTo(1));
            AssertPendingOffer(interaction, expectedRealmId);
        }

        [Test]
        public void DuplicateOpenIsInertAndCannotAcceptOrProgressOmen()
        {
            FirstUserGameTestOmenInteraction interaction = Create(
                SessionA,
                GenerationA,
                RealmId.Crownlands);
            Assert.That(interaction.TryOpenReport(
                out bool firstChanged,
                out string firstFriendly,
                out string firstDiagnostic), Is.True,
                firstFriendly + firstDiagnostic);
            Nvs01QuestSnapshot committed = interaction.Snapshot;

            Assert.That(interaction.TryOpenReport(
                out bool duplicateChanged,
                out string duplicateFriendly,
                out string duplicateDiagnostic), Is.True,
                duplicateFriendly + duplicateDiagnostic);
            Assert.That(firstChanged, Is.True);
            Assert.That(duplicateChanged, Is.False);
            Assert.That(interaction.SelectValeriusInvocationCount, Is.EqualTo(1));
            Assert.That(interaction.CommitAttemptCount, Is.EqualTo(1));
            Assert.That(interaction.Snapshot, Is.SameAs(committed));
            AssertPendingOffer(interaction, "crownlands");
        }

        [Test]
        public void ReconstructionRestoresPendingDialogueWithoutReinvokingProductionRuntime()
        {
            FirstUserGameTestOmenInteraction first = Create(
                SessionA,
                GenerationA,
                RealmId.Eldergrove);
            Assert.That(first.TryOpenReport(
                out bool changed,
                out string friendly,
                out string diagnostic), Is.True,
                friendly + diagnostic);
            Assert.That(changed, Is.True);

            FirstUserGameTestOmenInteraction reconstructed = Create(
                SessionA,
                GenerationA,
                RealmId.Eldergrove);
            Assert.That(reconstructed.IsReportOpen, Is.True);
            Assert.That(reconstructed.SelectValeriusInvocationCount, Is.Zero);
            Assert.That(reconstructed.CommitAttemptCount, Is.Zero);
            AssertPendingOffer(reconstructed, "eldergrove");
            Assert.That(reconstructed.TryOpenReport(
                out bool replayChanged,
                out friendly,
                out diagnostic), Is.True,
                friendly + diagnostic);
            Assert.That(replayChanged, Is.False);
            Assert.That(reconstructed.SelectValeriusInvocationCount, Is.Zero);
            Assert.That(reconstructed.CommitAttemptCount, Is.Zero);
        }

        [TestCase(true, false, TestName = "RetainedProjection_CrossGeneration_Rejects")]
        [TestCase(false, true, TestName = "RetainedProjection_CrossRealm_Rejects")]
        public void RetainedProjectionRejectsSessionBindingDrift(
            bool generationDrift,
            bool realmDrift)
        {
            FirstUserGameTestOmenInteraction original = Create(
                SessionA,
                GenerationA,
                RealmId.Crownlands);
            Assert.That(original.TryOpenReport(
                out _,
                out string friendly,
                out string diagnostic), Is.True,
                friendly + diagnostic);

            Assert.That(FirstUserGameTestOmenInteraction.TryCreate(
                SessionA,
                generationDrift ? GenerationB : GenerationA,
                realmDrift ? RealmId.Stonehold : RealmId.Crownlands,
                out FirstUserGameTestOmenInteraction drifted,
                out friendly,
                out diagnostic), Is.False);
            Assert.That(drifted, Is.Null);
            Assert.That(friendly, Is.Not.Empty);
            Assert.That(diagnostic, Is.EqualTo("OMEN_SESSION_PROJECTION_INVALID"));
        }

        [Test]
        public void CorruptRetainedProjectionFailsClosedWithoutReplacement()
        {
            const string corrupt = "corrupt-retained-omen-projection";
            FirstUserGameTestOmenSessionStore.SetRawForTests(SessionA, corrupt);

            Assert.That(FirstUserGameTestOmenInteraction.TryCreate(
                SessionA,
                GenerationA,
                RealmId.Crownlands,
                out FirstUserGameTestOmenInteraction interaction,
                out string friendly,
                out string diagnostic), Is.False);
            Assert.That(interaction, Is.Null);
            Assert.That(friendly, Is.Not.Empty);
            Assert.That(diagnostic, Is.EqualTo("OMEN_SESSION_PROJECTION_INVALID"));
            Assert.That(SessionState.GetString(
                "AL.FirstUserGameTest.Omen.v1." + SessionA,
                string.Empty), Is.EqualTo(corrupt));
        }

        [Test]
        public void ProjectionCodecRoundTripsExactPendingDialogueOnly()
        {
            FirstUserGameTestOmenProjection projection = CanonicalProjection();
            Assert.That(FirstUserGameTestOmenProjectionCodec.TryEncode(
                projection,
                out string payload), Is.True);
            Assert.That(payload.Length,
                Is.LessThanOrEqualTo(
                    FirstUserGameTestOmenContract.MaximumRetainedEnvelopeCharacters));
            Assert.That(FirstUserGameTestOmenProjectionCodec.TryDecode(
                payload,
                out FirstUserGameTestOmenProjection restored), Is.True);
            Assert.That(restored.ValueEquals(projection), Is.True);
        }

        private static IEnumerable<TestCaseData> InvalidProjectionPayloads()
        {
            yield return new TestCaseData(null).SetName("Projection_Null_Rejects");
            yield return new TestCaseData(string.Empty).SetName("Projection_Empty_Rejects");
            yield return new TestCaseData(new string('x', 1025))
                .SetName("Projection_Oversize_Rejects");
            yield return new TestCaseData(
                    FirstUserGameTestOmenContract.ContractVersion + "\r\ninvalid")
                .SetName("Projection_CRLF_Rejects");
            yield return new TestCaseData(CanonicalPayload().Replace("\n1\nOFFERED", "\n01\nOFFERED"))
                .SetName("Projection_NoncanonicalRevision_Rejects");
            yield return new TestCaseData(CanonicalPayload().Replace("\nOFFERED\n", "\nTALK_TO_VALERIUS\n"))
                .SetName("Projection_ProgressedState_Rejects");
            yield return new TestCaseData(CanonicalPayload().Replace("\nDLG_OMEN_1_OFFER\n", "\nDLG_OMEN_1_START\n"))
                .SetName("Projection_WrongDialogue_Rejects");
            yield return new TestCaseData(CanonicalPayload().Replace("\n1\nSELECT_VALERIUS", "\n0\nSELECT_VALERIUS"))
                .SetName("Projection_NonpendingChoice_Rejects");
            yield return new TestCaseData(CanonicalPayload().Replace("\nSELECT_VALERIUS", "\nQUEST_ACCEPTED"))
                .SetName("Projection_AcceptanceEvent_Rejects");
        }

        [TestCaseSource(nameof(InvalidProjectionPayloads))]
        public void ProjectionCodecRejectsMalformedOrProgressedPayload(string payload)
        {
            Assert.That(FirstUserGameTestOmenProjectionCodec.TryDecode(
                payload,
                out FirstUserGameTestOmenProjection projection), Is.False);
            Assert.That(projection, Is.Null);
        }

        [Test]
        public void FriendlyPresentationOmitsMachineIdsChoicesAndAcceptanceControl()
        {
            FirstUserGameTestOmenInteraction interaction = Create(
                SessionA,
                GenerationA,
                RealmId.Umbral);
            Assert.That(FirstUserGameTestPlaytestCopy.TryBuildOmenOfferDetails(
                interaction.View,
                out string offer), Is.True);
            AssertFriendly(offer);

            Assert.That(interaction.TryOpenReport(
                out _,
                out string friendly,
                out string diagnostic), Is.True,
                friendly + diagnostic);
            Assert.That(FirstUserGameTestPlaytestCopy.TryBuildValeriusReport(
                interaction.View,
                out string report), Is.True);
            AssertFriendly(report);
            Assert.That(report,
                Does.Contain("Quest acceptance is intentionally unavailable"));
            Assert.That(report, Does.Not.Contain("Tell me what happened."));
            Assert.That(report, Does.Not.Contain("Not yet."));
            Assert.That(interaction.View.Choices.Count, Is.EqualTo(2),
                "The exact production pending dialogue remains intact internally.");
            AssertPendingOffer(interaction, "umbral");
        }

        [Test]
        public void EditorBridgeSourceCannotInvokeAcceptanceProgressRewardsOrProductionStorage()
        {
            string editorDirectory = Path.Combine(
                Path.GetFullPath(Application.dataPath),
                "AL",
                "Scripts",
                "Editor",
                "Development",
                "FirstUserGameTest");
            string source = File.ReadAllText(Path.Combine(
                editorDirectory,
                "FirstUserGameTestOmenRuntime.cs"));
            string tutorialSource = File.ReadAllText(Path.Combine(
                editorDirectory,
                "FirstUserGameTestTutorialRuntime.cs"));
            string asmdef = File.ReadAllText(Path.Combine(
                editorDirectory,
                "AL.Development.FirstUserGameTest.Editor.asmdef"));

            Assert.That(source, Does.Contain("#if !UNITY_EDITOR"));
            Assert.That(asmdef, Does.Contain("\"Editor\""));
            Assert.That(source, Does.Contain("Nvs01QuestRuntime"),
                "The bridge must consume the current production runtime rather than fake it.");
            Assert.That(source, Does.Contain("SelectValerius"));
            foreach (string forbidden in new[]
                     {
                         "choice.omen1.accept",
                         "SelectDialogueChoice",
                         "SelectChoice(",
                         "TALK_TO_VALERIUS",
                         "QUEST_ACCEPTED",
                         "ConsequencePlanner",
                         "SaveGameData",
                         "LocalSaveGameService",
                         "ServiceLocator",
                         "persistentDataPath",
                         "PlayerPrefs",
                         "SceneManager",
                         "Addressables",
                         "HttpClient",
                         "UnityWebRequest",
                         "Application.Quit"
                     })
            {
                Assert.That(source + tutorialSource, Does.Not.Contain(forbidden), forbidden);
            }

            string[] productionReferences = Directory
                .EnumerateFiles(
                    Path.Combine(Path.GetFullPath(Application.dataPath), "AL", "Scripts"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => path.IndexOf(
                    Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => !path.EndsWith(
                    "EditorGameTestModeBootstrap.cs",
                    StringComparison.OrdinalIgnoreCase))
                .Where(path => File.ReadAllText(path).IndexOf(
                    "FirstUserGameTestOmen",
                    StringComparison.Ordinal) >= 0)
                .ToArray();
            Assert.That(productionReferences, Is.Empty,
                string.Join(Environment.NewLine, productionReferences));
        }

        private static FirstUserGameTestOmenInteraction Create(
            string sessionId,
            string generation,
            RealmId realm)
        {
            Assert.That(FirstUserGameTestOmenInteraction.TryCreate(
                sessionId,
                generation,
                realm,
                out FirstUserGameTestOmenInteraction interaction,
                out string friendly,
                out string diagnostic), Is.True, friendly + diagnostic);
            Assert.That(interaction, Is.Not.Null);
            return interaction;
        }

        private static void AssertInitial(FirstUserGameTestOmenInteraction interaction)
        {
            Assert.That(interaction.IsReportOpen, Is.False);
            Assert.That(interaction.SelectValeriusInvocationCount, Is.Zero);
            Assert.That(interaction.CommitAttemptCount, Is.Zero);
            Assert.That(interaction.Snapshot.Revision, Is.Zero);
            Assert.That(interaction.Snapshot.StateId, Is.EqualTo("OFFERED"));
            Assert.That(interaction.Snapshot.CurrentDialogueNodeId, Is.Empty);
            Assert.That(interaction.Snapshot.PendingChoice, Is.False);
            Assert.That(interaction.Snapshot.CommittedRealmId, Is.Empty);
            Assert.That(interaction.Snapshot.LastOperation, Is.Null);
            Assert.That(interaction.Snapshot.ConsequenceIntentIds, Is.Empty);
            Assert.That(interaction.View.HasDialogue, Is.False);
            Assert.That(interaction.View.Choices, Is.Empty);
        }

        private static void AssertPendingOffer(
            FirstUserGameTestOmenInteraction interaction,
            string expectedRealmId)
        {
            Nvs01QuestSnapshot snapshot = interaction.Snapshot;
            Assert.That(interaction.IsReportOpen, Is.True);
            Assert.That(snapshot.Revision, Is.EqualTo(1));
            Assert.That(snapshot.StateId, Is.EqualTo("OFFERED"));
            Assert.That(snapshot.CurrentDialogueNodeId, Is.EqualTo("DLG_OMEN_1_OFFER"));
            Assert.That(snapshot.PendingChoice, Is.True);
            Assert.That(snapshot.PendingSemanticActionId, Is.Empty);
            Assert.That(snapshot.CommittedRealmId, Is.EqualTo(expectedRealmId));
            Assert.That(snapshot.EncounterStatus, Is.EqualTo(Nvs01EncounterStatus.None));
            Assert.That(snapshot.CurrentEncounter, Is.Null);
            Assert.That(snapshot.ConsequenceIntentIds, Is.Empty);
            Assert.That(snapshot.LastOperation, Is.Not.Null);
            Assert.That(snapshot.LastOperation.Status,
                Is.EqualTo(Nvs01CommandStatus.Committed));
            Assert.That(snapshot.LastOperation.EventId, Is.EqualTo("SELECT_VALERIUS"));
            Assert.That(snapshot.LastOperation.Revision, Is.EqualTo(1));
            Assert.That(snapshot.LastOperation.StateId, Is.EqualTo("OFFERED"));
            Assert.That(interaction.View.StateId, Is.EqualTo("OFFERED"));
            Assert.That(interaction.View.HasDialogue, Is.True);
            Assert.That(interaction.View.Choices.Count, Is.EqualTo(2));
            Assert.That(interaction.View.HasDiagnostic, Is.False);
            Assert.That(interaction.View.EncounterRequest, Is.Null);
        }

        private static void AssertFriendly(string rendered)
        {
            Assert.That(rendered, Is.Not.Empty);
            foreach (string forbidden in new[]
                     {
                         "OMEN_1",
                         "OFFERED",
                         "DLG_",
                         "OBJ_",
                         "SELECT_",
                         "choice.",
                         "QUEST_ACCEPTED",
                         "TALK_TO_VALERIUS",
                         "receipt",
                         "projection",
                         "emulator",
                         "hash",
                         "byte",
                         "code-unit"
                     })
            {
                Assert.That(
                    rendered.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase),
                    Is.EqualTo(-1),
                    forbidden);
            }
        }

        private static FirstUserGameTestOmenProjection CanonicalProjection()
        {
            return new FirstUserGameTestOmenProjection(
                SessionA,
                GenerationA,
                "crownlands",
                "12345678-1234-1234-1234-123456789abc",
                new string('a', 64),
                1,
                "OFFERED",
                "DLG_OMEN_1_OFFER",
                true,
                "SELECT_VALERIUS");
        }

        private static string CanonicalPayload()
        {
            Assert.That(FirstUserGameTestOmenProjectionCodec.TryEncode(
                CanonicalProjection(),
                out string payload), Is.True);
            return payload;
        }

        private static void EraseKnownSessions()
        {
            FirstUserGameTestOmenSessionStore.EraseSession(SessionA);
            FirstUserGameTestOmenSessionStore.EraseSession(SessionB);
        }
    }
}
#endif
