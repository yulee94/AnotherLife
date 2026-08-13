#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Editor.Development.FirstUserGameTest;
using AL.UI.FirstUserIdentity;
using NUnit.Framework;
using UnityEditor.Compilation;
using UnityEngine;

namespace AL.Tests.EditMode.FirstUserGameTest
{
    [TestFixture]
    public sealed class FirstUserGameTestAdapterTests
    {
        private static IEnumerable<TestCaseData> SupportedIdentityCases()
        {
            RealmId[] realms =
            {
                RealmId.Crownlands,
                RealmId.Stonehold,
                RealmId.Eldergrove,
                RealmId.Umbral
            };
            ClassFamily[] classes =
            {
                ClassFamily.Warrior,
                ClassFamily.Mage,
                ClassFamily.Ranger,
                ClassFamily.Assassin
            };

            foreach (RealmId realm in realms)
            {
                foreach (ClassFamily classFamily in classes)
                {
                    yield return new TestCaseData(realm, classFamily)
                        .SetName("Commit_" + realm + "_" + classFamily + "_AdmitsOnlyIsolatedGameTest");
                }
            }
        }

        private static IEnumerable<TestCaseData> HostWritableCases()
        {
            yield return new TestCaseData(false, false, FirstUserRouteDestination.HostReadiness);
            yield return new TestCaseData(false, true, FirstUserRouteDestination.HostReadiness);
            yield return new TestCaseData(true, false, FirstUserRouteDestination.WritableAuthority);
            yield return new TestCaseData(true, true, FirstUserRouteDestination.IsolatedCharacterGameTest);
        }

        private static IEnumerable<TestCaseData> InvalidHandleCases()
        {
            yield return new TestCaseData(null).SetName("Handle_Null_Rejects");
            yield return new TestCaseData(string.Empty).SetName("Handle_Empty_Rejects");
            yield return new TestCaseData(" leading").SetName("Handle_LeadingWhitespace_Rejects");
            yield return new TestCaseData("trailing ").SetName("Handle_TrailingWhitespace_Rejects");
            yield return new TestCaseData("line\nfeed").SetName("Handle_Control_Rejects");
            yield return new TestCaseData("line\u2028separator").SetName("Handle_Separator_Rejects");
            yield return new TestCaseData("bad\uD800").SetName("Handle_UnpairedHighSurrogate_Rejects");
            yield return new TestCaseData("bad\uDC00").SetName("Handle_UnpairedLowSurrogate_Rejects");
            yield return new TestCaseData(new string('a', 33)).SetName("Handle_TooManyCodeUnits_Rejects");
            yield return new TestCaseData(new string('\u754C', 22)).SetName("Handle_TooManyUtf8Bytes_Rejects");
        }

        [TestCaseSource(nameof(SupportedIdentityCases))]
        public void SupportedRealmAndExplicitClassCommitToExactDevelopmentEvidence(
            RealmId realm,
            ClassFamily classFamily)
        {
            string session = Guid.NewGuid().ToString("N");
            var adapter = new FirstUserGameTestAdapter(session);
            FirstUserGameTestSelection selection = Selection(
                session,
                realm,
                classFamily,
                "average",
                "Dev Champion");

            FirstUserGameTestAdapterResult result = adapter.CommitAndEvaluate(
                selection,
                hostReady: true,
                writableVerifier: new FixedDevelopmentWritableVerifier(true));

            Assert.That(result.CanEnterIsolatedCharacterGameTest, Is.True);
            Assert.That(result.RoutePlan.AllowsIsolatedCharacterGameTest, Is.True);
            Assert.That(result.RoutePlan.AllowsGameplay, Is.False);
            Assert.That(result.Receipt.IsValid, Is.True);
            Assert.That(result.Projection.IsValid, Is.True);
            Assert.That(result.Selection, Is.SameAs(selection));
        }

        [TestCaseSource(nameof(HostWritableCases))]
        public void HostAndWritableRemainIndependentAfterExactEvidence(
            bool hostReady,
            bool writable,
            FirstUserRouteDestination expectedDestination)
        {
            string session = Guid.NewGuid().ToString("N");
            var adapter = new FirstUserGameTestAdapter(session);

            FirstUserGameTestAdapterResult result = adapter.CommitAndEvaluate(
                Selection(session),
                hostReady,
                new FixedDevelopmentWritableVerifier(writable));

            Assert.That(result.RoutePlan.Destination, Is.EqualTo(expectedDestination));
            Assert.That(
                result.CanEnterIsolatedCharacterGameTest,
                Is.EqualTo(hostReady && writable));
            Assert.That(result.RoutePlan.AllowsGameplay, Is.False);
        }

        [TestCaseSource(nameof(InvalidHandleCases))]
        public void MalformedDevelopmentHandleRejectsBeforeAuthority(string handle)
        {
            string session = Guid.NewGuid().ToString("N");
            var adapter = new FirstUserGameTestAdapter(session);
            FirstUserGameTestAdapterResult result = adapter.CommitAndEvaluate(
                Selection(session, handle: handle),
                hostReady: true,
                writableVerifier: new FixedDevelopmentWritableVerifier(true));

            Assert.That(result.Status, Is.EqualTo(FirstUserGameTestAdapterStatus.Rejected));
            Assert.That(result.Failure, Is.EqualTo(FirstUserGameTestAdapterFailure.DevelopmentHandleInvalid));
            Assert.That(result.Receipt, Is.Null);
            Assert.That(result.Projection, Is.Null);
            Assert.That(result.CanEnterIsolatedCharacterGameTest, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Uppercase")]
        [TestCase("double__underscore")]
        [TestCase("trailing_")]
        [TestCase("hyphen-id")]
        public void InvalidCustomizationIdRejects(string customizationId)
        {
            string session = Guid.NewGuid().ToString("N");
            var adapter = new FirstUserGameTestAdapter(session);
            FirstUserGameTestAdapterResult result = adapter.CommitAndEvaluate(
                Selection(session, customizationId: customizationId),
                hostReady: true,
                writableVerifier: new FixedDevelopmentWritableVerifier(true));

            Assert.That(result.Failure, Is.EqualTo(FirstUserGameTestAdapterFailure.CustomizationInvalid));
            Assert.That(result.CanEnterIsolatedCharacterGameTest, Is.False);
        }

        [Test]
        public void ForgedRealmRacePairRejectsBeforeAuthority()
        {
            string session = Guid.NewGuid().ToString("N");
            var identity = new FirstUserIdentityDraftSnapshot(
                FirstUserIdentityDraftStep.CustomizationReady,
                RealmId.Eldergrove,
                FirstUserRace.Humans,
                ClassFamily.Ranger);
            var adapter = new FirstUserGameTestAdapter(session);

            FirstUserGameTestAdapterResult result = adapter.CommitAndEvaluate(
                new FirstUserGameTestSelection(session, identity, "average", "Dev Ranger"),
                hostReady: true,
                writableVerifier: new FixedDevelopmentWritableVerifier(true));

            Assert.That(result.Failure, Is.EqualTo(FirstUserGameTestAdapterFailure.SelectionInvalid));
            Assert.That(result.Receipt, Is.Null);
        }

        [Test]
        public void RetainedStateRestoreReplaysTheExactReceiptAndProjection()
        {
            string session = Guid.NewGuid().ToString("N");
            FirstUserGameTestSelection selection = Selection(session);
            var original = new FirstUserGameTestAdapter(session);
            FirstUserGameTestAdapterResult committed = original.CommitAndEvaluate(
                selection,
                hostReady: true,
                writableVerifier: new FixedDevelopmentWritableVerifier(true));
            Assert.That(committed.CanEnterIsolatedCharacterGameTest, Is.True);

            Assert.That(FirstUserGameTestAdapter.TryRestore(
                session,
                original.CaptureAuthorityState(),
                original.CaptureProjectionState(),
                out FirstUserGameTestAdapter restored,
                out FirstUserGameTestAdapterFailure failure), Is.True, failure.ToString());

            FirstUserGameTestAdapterResult replay = restored.CommitAndEvaluate(
                selection,
                hostReady: true,
                writableVerifier: new FixedDevelopmentWritableVerifier(true));
            Assert.That(replay.CanEnterIsolatedCharacterGameTest, Is.True);
            Assert.That(replay.Receipt.Handle, Is.EqualTo(committed.Receipt.Handle));
            Assert.That(replay.Projection.Handle, Is.EqualTo(committed.Projection.Handle));
        }

        [Test]
        public void SameSessionOperationWithChangedPayloadFailsClosedAsCollision()
        {
            string session = Guid.NewGuid().ToString("N");
            var adapter = new FirstUserGameTestAdapter(session);
            Assert.That(adapter.CommitAndEvaluate(
                Selection(session, customizationId: "average"),
                true,
                new FixedDevelopmentWritableVerifier(true)).CanEnterIsolatedCharacterGameTest, Is.True);

            FirstUserGameTestAdapterResult collision = adapter.CommitAndEvaluate(
                Selection(session, customizationId: "broad"),
                true,
                new FixedDevelopmentWritableVerifier(true));

            Assert.That(collision.Status, Is.EqualTo(FirstUserGameTestAdapterStatus.Rejected));
            Assert.That(collision.Failure, Is.EqualTo(FirstUserGameTestAdapterFailure.AuthorityRejected));
            Assert.That(collision.AuthorityFailure, Is.EqualTo(
                AL.Editor.Development.OnboardingAuthority.DevelopmentAuthorityFailure.Collision));
            Assert.That(collision.CanEnterIsolatedCharacterGameTest, Is.False);
        }

        [Test]
        public void CommitmentIsCultureInvariantAndPreservesExactHandleEvidence()
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUi = CultureInfo.CurrentUICulture;
            string session = Guid.NewGuid().ToString("N");
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
                var first = new FirstUserGameTestAdapter(session);
                FirstUserGameTestAdapterResult turkish = first.CommitAndEvaluate(
                    Selection(session, handle: "Istanbul Scout"),
                    true,
                    new FixedDevelopmentWritableVerifier(true));

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar-SA");
                var second = new FirstUserGameTestAdapter(session);
                FirstUserGameTestAdapterResult arabic = second.CommitAndEvaluate(
                    Selection(session, handle: "Istanbul Scout"),
                    true,
                    new FixedDevelopmentWritableVerifier(true));

                Assert.That(turkish.Receipt.Handle, Is.EqualTo(arabic.Receipt.Handle));
                Assert.That(turkish.Projection.Handle, Is.EqualTo(arabic.Projection.Handle));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUi;
            }
        }

        [Test]
        public void AssemblyAndSourceRemainEditorOnlyAndAvoidSaveMutationOrProductionRouting()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string sourceRoot = Path.Combine(
                projectRoot,
                "Assets",
                "AL",
                "Scripts",
                "Editor",
                "Development",
                "FirstUserGameTest");
            string asmdefPath = Path.Combine(
                sourceRoot,
                "AL.Development.FirstUserGameTest.Editor.asmdef");
            string asmdef = File.ReadAllText(asmdefPath).Replace("\r\n", "\n");
            Assert.That(asmdef, Does.Contain("\"includePlatforms\": [\n        \"Editor\""));
            Assert.That(asmdef, Does.Contain("AL.Development.OnboardingAuthority.Emulator"));

            string driverPath = Path.Combine(
                projectRoot,
                "Assets",
                "AL",
                "Scripts",
                "Development",
                "EditorGameTestModeBootstrap.cs");
            string driverSource = File.ReadAllText(driverPath).Replace("\r\n", "\n");
            Assert.That(driverSource, Does.StartWith("#if UNITY_EDITOR\n"));
            Assert.That(driverSource, Does.Contain("class EditorGameTestModeHostDriver"));
            Assert.That(driverSource, Does.Not.Contain("OnboardingAuthority"));
            Assert.That(driverSource, Does.Not.Contain("DevelopmentReceipt"));
            Assert.That(driverSource, Does.Not.Contain("DevelopmentProjection"));

            string playModeAsmdefPath = Path.Combine(
                projectRoot,
                "Assets",
                "AL",
                "Tests",
                "PlayMode",
                "FirstUserGameTest",
                "AL.Development.FirstUserGameTest.PlayModeTests.asmdef");
            string playModeAsmdef = File.ReadAllText(playModeAsmdefPath).Replace("\r\n", "\n");
            Assert.That(playModeAsmdef, Does.Contain("\"includePlatforms\": []"));
            Assert.That(playModeAsmdef, Does.Contain("\"UNITY_EDITOR\""));

            string combined = string.Join(
                "\n",
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
            foreach (string forbidden in new[]
            {
                "SaveGameData", "LocalGameDataService", "PlayerPrefs", "persistentDataPath",
                "UnityWebRequest", "HttpClient", "Application.Quit",
                "SceneManager.LoadScene(\"Kingdom\"", "TrySelectRealm"
            })
            {
                Assert.That(combined, Does.Not.Contain(forbidden), forbidden);
            }

            UnityEditor.Compilation.Assembly[] playerAssemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            Assert.That(playerAssemblies.Any(assembly => string.Equals(
                assembly.name,
                "AL.Development.FirstUserGameTest.Editor",
                StringComparison.Ordinal)), Is.False);
            Assert.That(playerAssemblies.SelectMany(assembly => assembly.sourceFiles)
                .Any(path => path.Replace('\\', '/').Contains(
                    "/Scripts/Editor/Development/FirstUserGameTest/")), Is.False);
        }

        private static FirstUserGameTestSelection Selection(
            string session,
            RealmId realm = RealmId.Eldergrove,
            ClassFamily classFamily = ClassFamily.Ranger,
            string customizationId = "average",
            string handle = "Dev Ranger")
        {
            Assert.That(FirstUserIdentityDerivation.TryDeriveRace(
                realm,
                out FirstUserRace race), Is.True);
            var identity = new FirstUserIdentityDraftSnapshot(
                FirstUserIdentityDraftStep.CustomizationReady,
                realm,
                race,
                classFamily);
            return new FirstUserGameTestSelection(
                session,
                identity,
                customizationId,
                handle);
        }

        private sealed class FixedDevelopmentWritableVerifier :
            IFirstUserGameTestDevelopmentWritableVerifier
        {
            private readonly bool _writable;

            internal FixedDevelopmentWritableVerifier(bool writable)
            {
                _writable = writable;
            }

            public bool IsDevelopmentWritable(
                AL.Editor.Development.OnboardingAuthority.VerifiedDevelopmentReceipt receipt,
                AL.Editor.Development.OnboardingAuthority.VerifiedDevelopmentProjection projection)
            {
                return _writable && receipt != null && receipt.IsValid &&
                       projection != null && projection.IsValid;
            }
        }
    }
}
#endif
