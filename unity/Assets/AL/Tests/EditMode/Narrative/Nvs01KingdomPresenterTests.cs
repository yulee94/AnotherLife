using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using AL.UI.Kingdom;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01KingdomPresenterTests
    {
        private const string Offered = "OFFERED";
        private const string Talk = "TALK_TO_VALERIUS";
        private const string Investigate = "INVESTIGATE_SKY_CASTLE";
        private const string Failed = "FAILED";
        private const string Report = "REPORT_TO_VALERIUS";
        private const string Completed = "COMPLETED";
        private const string RequestArena = "REQUEST_SKY_CASTLE_ARENA";
        private const string RetryArena = "RETRY_SKY_CASTLE_ARENA";

        private static Nvs01VerifiedCatalog _verifiedCatalog;

        [Test]
        public void InitialOfferAndDialogueUsePacketLocalizationAndExplicitChoices()
        {
            var fixture = new PresenterFixture();
            Nvs01KingdomView initial = fixture.Presenter.Present();

            AssertViewIdentity(initial, Offered);
            Assert.AreEqual(Localize("quest.omen1.title"), initial.Title);
            Assert.AreEqual(Localize("quest.omen1.description"), initial.Description);
            Assert.AreEqual(Localize("objective.omen1.talk"), initial.ObjectiveText);
            Assert.AreEqual(Localize("npc.valerius.name"), initial.SpeakerName);
            Assert.AreEqual(Localize("npc.valerius.role.veil_watch_liaison"), initial.SpeakerRole);
            Assert.AreEqual(Nvs01KingdomActionKind.SelectValerius, initial.PrimaryAction);
            Assert.AreEqual(initial.SpeakerName, initial.PrimaryActionLabel);
            Assert.IsEmpty(initial.DialogueText);
            Assert.IsEmpty(initial.Choices);

            Nvs01KingdomActionResult offer = fixture.Presenter.SelectValerius();
            AssertCommitted(offer, Offered);
            Assert.AreEqual(Localize("dialogue.omen1.offer"), offer.View.DialogueText);
            AssertChoiceLabels(
                offer.View,
                "choice.omen1.accept",
                "choice.omen1.decline");

            Nvs01KingdomActionResult accepted = fixture.Presenter.SelectChoice("choice.omen1.accept");
            AssertCommitted(accepted, Talk);
            Assert.AreEqual(Localize("dialogue.omen1.start"), accepted.View.DialogueText);
            AssertChoiceLabels(
                accepted.View,
                "choice.omen1.investigate",
                "choice.omen1.ask_more");

            Nvs01KingdomActionResult lore = fixture.Presenter.SelectChoice("choice.omen1.ask_more");
            AssertCommitted(lore, Talk);
            Assert.AreEqual(Localize("dialogue.omen1.lore"), lore.View.DialogueText);
            AssertChoiceLabels(lore.View, "choice.omen1.depart");

            Nvs01KingdomActionResult go = fixture.Presenter.SelectChoice("choice.omen1.depart");
            AssertCommitted(go, Talk);
            Assert.AreEqual(Localize("dialogue.omen1.go"), go.View.DialogueText);
            AssertChoiceLabels(go.View, "choice.omen1.deploy");

            Nvs01KingdomActionResult arenaStart = fixture.Presenter.SelectChoice("choice.omen1.deploy");
            AssertCommitted(arenaStart, Talk);
            Assert.AreEqual(Localize("dialogue.omen1.arena_start"), arenaStart.View.DialogueText);
            Assert.AreEqual(Nvs01KingdomActionKind.InvokeSemanticAction, arenaStart.View.PrimaryAction);
            Assert.AreEqual(Localize("choice.omen1.deploy"), arenaStart.View.PrimaryActionLabel);
            Assert.AreEqual(RequestArena, fixture.Runtime.Snapshot.PendingSemanticActionId);
        }

        [Test]
        public void DeployPublishesCorrelatedRequestAndResumeDoesNotMutate()
        {
            var fixture = new PresenterFixture();
            fixture.AdvanceToArenaStart();

            long beforeRevision = fixture.Runtime.Snapshot.Revision;
            Nvs01KingdomActionResult deploy = fixture.Presenter.InvokePrimaryAction();

            AssertCommitted(deploy, Investigate);
            Assert.True(deploy.ShouldEnterEncounter);
            Assert.NotNull(deploy.EncounterRequest);
            Assert.AreSame(fixture.Runtime.Snapshot.CurrentEncounter, deploy.EncounterRequest);
            Assert.AreEqual("crownlands", deploy.EncounterRequest.RealmId);
            Assert.AreEqual(Nvs01KingdomActionKind.ResumeEncounter, deploy.View.PrimaryAction);
            Assert.AreEqual(Localize("choice.omen1.deploy"), deploy.View.PrimaryActionLabel);
            Assert.AreEqual(beforeRevision + 1, fixture.Runtime.Snapshot.Revision);

            long committedRevision = fixture.Runtime.Snapshot.Revision;
            Nvs01KingdomActionResult resume = fixture.Presenter.InvokePrimaryAction();

            Assert.True(resume.ShouldEnterEncounter);
            Assert.IsNull(resume.Disposition);
            Assert.AreSame(deploy.EncounterRequest, resume.EncounterRequest);
            Assert.AreEqual(committedRevision, fixture.Runtime.Snapshot.Revision);
        }

        [Test]
        public void MissingArenaCapabilityIsVisibleAndNonMutating()
        {
            var fixture = new PresenterFixture();
            fixture.AdvanceToArenaStart();
            fixture.Capabilities = CapabilitiesExcept("ACTION_DEPLOY_CHAMPION");
            Nvs01QuestSnapshot before = fixture.Runtime.Snapshot;

            Nvs01KingdomActionResult unavailable = fixture.Presenter.InvokePrimaryAction();

            Assert.NotNull(unavailable.Disposition);
            Assert.AreEqual(Nvs01CommandStatus.DependencyUnavailable, unavailable.Disposition.Status);
            Assert.AreSame(before, fixture.Runtime.Snapshot);
            Assert.AreEqual(Nvs01KingdomViewStatus.Attention, unavailable.View.Status);
            Assert.AreEqual("AL-NVS01-DEPENDENCY-UNAVAILABLE", unavailable.View.DiagnosticCode);
            Assert.That(unavailable.View.PlayerMessage, Does.Contain(unavailable.View.DiagnosticCode));
            Assert.AreEqual(Nvs01KingdomActionKind.InvokeSemanticAction, unavailable.View.PrimaryAction);
            Assert.AreEqual(Localize("choice.omen1.deploy"), unavailable.View.PrimaryActionLabel);
        }

        [Test]
        public void FailureRetryAndManualReportRemainExplicit()
        {
            var fixture = new PresenterFixture();
            fixture.AdvanceToRequest();
            string failedCorrelation = fixture.Runtime.Snapshot.CurrentEncounter.CorrelationId;

            Nvs01CommandDisposition failure = fixture.ApplyResult(NvsEncounterOutcome.Failure);
            Assert.True(failure.IsCommitted);
            Assert.AreEqual(Failed, fixture.Runtime.Snapshot.StateId);

            Nvs01KingdomView failed = fixture.Presenter.Present();
            Assert.AreEqual(Localize("dialogue.omen1.failure"), failed.DialogueText);
            AssertChoiceLabels(failed, "choice.omen1.retry");

            Nvs01KingdomActionResult retryChoice = fixture.Presenter.SelectChoice("choice.omen1.retry");
            AssertCommitted(retryChoice, Failed);
            Assert.AreEqual(RetryArena, fixture.Runtime.Snapshot.PendingSemanticActionId);
            Assert.AreEqual(Nvs01KingdomActionKind.InvokeSemanticAction, retryChoice.View.PrimaryAction);
            Assert.AreEqual(Localize("choice.omen1.retry"), retryChoice.View.PrimaryActionLabel);

            Nvs01KingdomActionResult retry = fixture.Presenter.InvokePrimaryAction();
            AssertCommitted(retry, Investigate);
            Assert.True(retry.ShouldEnterEncounter);
            Assert.AreNotEqual(failedCorrelation, retry.EncounterRequest.CorrelationId);
            Assert.AreEqual(Localize("choice.omen1.retry"), retry.View.PrimaryActionLabel);

            Nvs01CommandDisposition success = fixture.ApplyResult(NvsEncounterOutcome.Success);
            Assert.True(success.IsCommitted);
            Assert.AreEqual(Report, fixture.Runtime.Snapshot.StateId);
            Nvs01KingdomView reportReady = fixture.Presenter.Present();
            Assert.AreEqual(Nvs01KingdomActionKind.SelectValerius, reportReady.PrimaryAction);
            Assert.AreEqual(Localize("objective.omen1.report"), reportReady.ObjectiveText);

            Nvs01KingdomActionResult report = fixture.Presenter.SelectValerius();
            AssertCommitted(report, Report);
            Assert.AreEqual(Localize("dialogue.omen1.report"), report.View.DialogueText);
            AssertChoiceLabels(report.View, "choice.omen1.present_tear");

            Nvs01KingdomActionResult conclusion = fixture.Presenter.SelectChoice("choice.omen1.present_tear");
            AssertCommitted(conclusion, Completed);
            Assert.AreEqual(Nvs01KingdomViewStatus.Completed, conclusion.View.Status);
            Assert.AreEqual(Localize("dialogue.omen1.report_conclusion"), conclusion.View.DialogueText);
            AssertChoiceLabels(conclusion.View, "choice.omen1.continue");

            Nvs01KingdomActionResult close = fixture.Presenter.SelectChoice("choice.omen1.continue");
            AssertCommitted(close, Completed);
            Assert.IsFalse(close.View.HasDialogue);
            Assert.IsEmpty(close.View.Choices);
            Assert.AreEqual(Nvs01KingdomActionKind.None, close.View.PrimaryAction);
            Assert.IsFalse(close.View.CanAbandon);
        }

        [Test]
        public void InvalidRealmAndCatalogFailureStayVisibleAndReadOnly()
        {
            var fixture = new PresenterFixture
            {
                RealmContext = Nvs01RealmContext.Invalid()
            };
            Nvs01QuestSnapshot before = fixture.Runtime.Snapshot;

            Nvs01KingdomActionResult rejected = fixture.Presenter.SelectValerius();

            Assert.AreSame(before, fixture.Runtime.Snapshot);
            Assert.NotNull(rejected.Disposition);
            Assert.AreEqual(Nvs01CommandStatus.Rejected, rejected.Disposition.Status);
            Assert.AreEqual("AL-NVS01-EVENT-MISMATCH", rejected.View.DiagnosticCode);
            Assert.AreEqual(Nvs01KingdomViewStatus.Attention, rejected.View.Status);
            Assert.That(rejected.View.PlayerMessage, Does.Contain(rejected.View.DiagnosticCode));

            var catalogDiagnostic = new Nvs01CatalogDiagnostic(
                "CATALOG-MISSING",
                Nvs01CatalogContract.StreamingAssetsRelativePath,
                "missing",
                "catalog",
                "missing");
            Nvs01KingdomView unavailable = Nvs01KingdomView.CatalogUnavailable(catalogDiagnostic);

            Assert.AreEqual(Nvs01KingdomViewStatus.Unavailable, unavailable.Status);
            Assert.AreEqual("AL-NVS01-CATALOG-MISSING", unavailable.DiagnosticCode);
            Assert.AreEqual(Nvs01KingdomActionKind.None, unavailable.PrimaryAction);
            Assert.IsFalse(unavailable.CanAbandon);
            Assert.That(unavailable.PlayerMessage, Does.Contain(unavailable.DiagnosticCode));
        }

        [Test]
        public void PresentationModelsAreImmutableAndDoNotDuplicateNarrativeText()
        {
            foreach (Type type in new[]
                     {
                         typeof(Nvs01KingdomChoice),
                         typeof(Nvs01KingdomView),
                         typeof(Nvs01KingdomActionResult),
                         typeof(Nvs01KingdomPresenter)
                     })
            {
                Assert.False(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .Any(property => property.CanWrite),
                    type.FullName);
            }

            var sourcePath = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "UI",
                "Kingdom",
                "Nvs01KingdomPresenter.cs");
            string source = File.ReadAllText(sourcePath);
            foreach (string narrativeText in VerifiedCatalog().Catalog.Localization.Values)
            {
                StringAssert.DoesNotContain(narrativeText, source);
            }
            StringAssert.DoesNotContain("SceneManager", source);
            StringAssert.DoesNotContain("ServiceLocator", source);
            StringAssert.DoesNotContain("SaveGameData", source);
        }

        private static void AssertViewIdentity(Nvs01KingdomView view, string state)
        {
            Assert.AreEqual(Nvs01KingdomViewStatus.Ready, view.Status);
            Assert.AreEqual(state, view.StateId);
            Assert.IsFalse(view.HasDiagnostic);
            Assert.IsEmpty(view.PlayerMessage);
        }

        private static void AssertChoiceLabels(Nvs01KingdomView view, params string[] keys)
        {
            CollectionAssert.AreEqual(keys, view.Choices.Select(choice => choice.Key).ToArray());
            CollectionAssert.AreEqual(
                keys.Select(Localize).ToArray(),
                view.Choices.Select(choice => choice.Label).ToArray());
        }

        private static void AssertCommitted(Nvs01KingdomActionResult result, string state)
        {
            Assert.NotNull(result);
            Assert.NotNull(result.Disposition);
            Assert.True(result.Disposition.IsCommitted, result.View.DiagnosticCode);
            Assert.AreEqual(state, result.Disposition.Snapshot.StateId);
            Assert.AreEqual(state, result.View.StateId);
        }

        private static string Localize(string key)
        {
            string value;
            Assert.True(VerifiedCatalog().Catalog.TryGetLocalization(key, out value), key);
            return value;
        }

        private static Nvs01VerifiedCatalog VerifiedCatalog()
        {
            if (_verifiedCatalog != null) return _verifiedCatalog;

            var path = Path.Combine(
                Application.dataPath,
                "StreamingAssets",
                Nvs01CatalogContract.StreamingAssetsRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Nvs01CatalogValidationResult validation =
                Nvs01CatalogValidator.ValidateCanonicalArtifact(File.ReadAllBytes(path));
            Assert.True(validation.IsAccepted, string.Join(
                Environment.NewLine,
                validation.Diagnostics.Select(item => item.Code + " " + item.Path)));
            _verifiedCatalog = validation.VerifiedCatalog;
            return _verifiedCatalog;
        }

        private static Nvs01CapabilitySnapshot AllCapabilities()
        {
            return CapabilitiesExcept(string.Empty);
        }

        private static Nvs01CapabilitySnapshot CapabilitiesExcept(string unavailable)
        {
            var values = VerifiedCatalog().Catalog.ExternalCapabilities
                .Where(item => item.Id == "LOCATION_SKY_CASTLE_MARKER" ||
                               item.Id == "ACTION_DEPLOY_CHAMPION" ||
                               item.Id == "HOOK_SKY_CASTLE_ARENA" ||
                               item.Id.StartsWith("EVENT_SKY_CASTLE_ARENA_", StringComparison.Ordinal))
                .ToDictionary(
                    item => item.Id,
                    item => !string.Equals(item.Id, unavailable, StringComparison.Ordinal),
                    StringComparer.Ordinal);
            return new Nvs01CapabilitySnapshot(values);
        }

        private static string GuidValue(int value)
        {
            return "00000000-0000-4000-8000-" + value.ToString("D12");
        }

        private sealed class PresenterFixture
        {
            private int _runtimeGuid = 1;
            private int _operationGuid = 1000;

            internal PresenterFixture()
            {
                Runtime = new Nvs01QuestRuntime(
                    VerifiedCatalog(),
                    new TestCommitter(),
                    () => GuidValue(_runtimeGuid++));
                RealmContext = new Nvs01RealmContext(
                    Nvs01RealmContextStatus.CommittedValid,
                    "crownlands");
                Capabilities = AllCapabilities();
                Presenter = new Nvs01KingdomPresenter(
                    Runtime,
                    () => RealmContext,
                    () => Capabilities,
                    () => GuidValue(_operationGuid++),
                    () => _operationGuid * 1000L);
            }

            internal Nvs01QuestRuntime Runtime { get; }
            internal Nvs01KingdomPresenter Presenter { get; }
            internal Nvs01RealmContext RealmContext { get; set; }
            internal Nvs01CapabilitySnapshot Capabilities { get; set; }

            internal void AdvanceToArenaStart()
            {
                AssertCommitted(Presenter.SelectValerius(), Offered);
                AssertCommitted(Presenter.SelectChoice("choice.omen1.accept"), Talk);
                AssertCommitted(Presenter.SelectChoice("choice.omen1.investigate"), Talk);
                AssertCommitted(Presenter.SelectChoice("choice.omen1.deploy"), Talk);
            }

            internal void AdvanceToRequest()
            {
                AdvanceToArenaStart();
                AssertCommitted(Presenter.InvokePrimaryAction(), Investigate);
            }

            internal Nvs01CommandDisposition ApplyResult(NvsEncounterOutcome outcome)
            {
                NvsEncounterRequest request = Runtime.Snapshot.CurrentEncounter;
                Assert.NotNull(request);
                return Runtime.ApplyEncounterResult(new NvsEncounterResult(
                    request.ContractVersion,
                    request.CorrelationId,
                    request.QuestId,
                    request.HookId,
                    request.RealmId,
                    outcome,
                    request.GetEventId(outcome),
                    outcome == NvsEncounterOutcome.Success ? "arena-v1" : string.Empty,
                    outcome == NvsEncounterOutcome.Success ? "snapshot://presenter-test" : string.Empty));
            }
        }

        private sealed class TestCommitter : INvs01MutationCommitter
        {
            public bool TryCommit(
                Nvs01MutationPlan plan,
                out Nvs01QuestSnapshot committed,
                out Nvs01RuntimeDiagnostic diagnostic)
            {
                committed = plan.Candidate;
                diagnostic = null;
                return true;
            }
        }
    }
}
