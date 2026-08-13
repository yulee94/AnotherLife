using System.Linq;
using AL.Core;
using AL.UI.FirstUserIdentity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.FirstUserIdentity
{
    public sealed class FirstUserIdentityDraftPresenterTests
    {
        private GameObject _host;
        private FirstUserIdentityDraftPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("IdentityDraftTestHost", typeof(RectTransform));
            _presenter = FirstUserIdentityDraftPresenter.Create(_host.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
        }

        [Test]
        public void ViewStartsAtRealmWithNoInferredClassAndDisabledConfirmation()
        {
            FirstUserIdentityDraftSnapshot draft = _presenter.CurrentDraft;

            Assert.That(draft.Step, Is.EqualTo(FirstUserIdentityDraftStep.Realm));
            Assert.That(draft.HasRealm, Is.False);
            Assert.That(draft.ClassFamily, Is.Null);
            Assert.That(_presenter.ConfirmRealmButton.interactable, Is.False);
            Assert.That(_presenter.ConfirmDraftButton.interactable, Is.False);
            Assert.That(_presenter.GetComponentsInChildren<Button>().Length, Is.EqualTo(5),
                "The active realm step exposes four choices and one gated confirmation.");
        }

        [Test]
        public void RealmClickShowsDerivedRaceBeforeExplicitRealmConfirmation()
        {
            _presenter.GetRealmChoiceButton(RealmId.Eldergrove).onClick.Invoke();

            FirstUserIdentityDraftSnapshot draft = _presenter.CurrentDraft;
            Assert.That(draft.Step, Is.EqualTo(FirstUserIdentityDraftStep.Realm));
            Assert.That(draft.Realm, Is.EqualTo(RealmId.Eldergrove));
            Assert.That(draft.Race, Is.EqualTo(FirstUserRace.Elves));
            Assert.That(draft.ClassFamily, Is.Null);
            Assert.That(_presenter.ConfirmRealmButton.interactable, Is.True);

            string[] visible = VisibleText();
            Assert.That(visible, Does.Contain("Selected — Eldergrove realm"));
            Assert.That(visible, Does.Contain(
                "Realm preview: Eldergrove realm\nDerived people: Elven heritage"));
        }

        [Test]
        public void FullButtonJourneyEmitsOneImmutableCustomizationBoundaryPayload()
        {
            int readyCount = 0;
            FirstUserIdentityDraftSnapshot payload = null;
            _presenter.CustomizationReady += draft =>
            {
                readyCount++;
                payload = draft;
            };

            _presenter.GetRealmChoiceButton(RealmId.Stonehold).onClick.Invoke();
            _presenter.ConfirmRealmButton.onClick.Invoke();

            Assert.That(_presenter.CurrentDraft.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.ClassFamily));
            Assert.That(_presenter.CurrentDraft.ClassFamily, Is.Null);
            Assert.That(_presenter.GetComponentsInChildren<Button>().Length, Is.EqualTo(6),
                "The active class step exposes four choices, back, and gated confirmation.");

            _presenter.GetClassFamilyChoiceButton(ClassFamily.Ranger).onClick.Invoke();
            Assert.That(_presenter.ConfirmDraftButton.interactable, Is.True);
            _presenter.ConfirmDraftButton.onClick.Invoke();

            Assert.That(readyCount, Is.EqualTo(1));
            Assert.That(payload, Is.Not.Null);
            Assert.That(payload.IsCustomizationReady, Is.True);
            Assert.That(payload.Realm, Is.EqualTo(RealmId.Stonehold));
            Assert.That(payload.Race, Is.EqualTo(FirstUserRace.Dwarves));
            Assert.That(payload.ClassFamily, Is.EqualTo(ClassFamily.Ranger));
            Assert.That(_presenter.GetComponentsInChildren<Button>().Length, Is.Zero,
                "The slice stops at the customization boundary without another action.");
            Assert.That(VisibleText(), Does.Contain(
                "Draft ready: Stonehold realm • Dwarven heritage • Ranger path. " +
                "Nothing has been saved."));

            _presenter.ConfirmDraftButton.onClick.Invoke();
            Assert.That(readyCount, Is.EqualTo(1),
                "A repeated callback cannot republish the terminal draft.");
        }

        [Test]
        public void BackToRealmClearsClassAndKeepsOnlyDerivedRealmContext()
        {
            _presenter.GetRealmChoiceButton(RealmId.Crownlands).onClick.Invoke();
            _presenter.ConfirmRealmButton.onClick.Invoke();
            _presenter.GetClassFamilyChoiceButton(ClassFamily.Mage).onClick.Invoke();

            _presenter.ReturnToRealmButton.onClick.Invoke();

            Assert.That(_presenter.CurrentDraft.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.Realm));
            Assert.That(_presenter.CurrentDraft.Realm, Is.EqualTo(RealmId.Crownlands));
            Assert.That(_presenter.CurrentDraft.Race, Is.EqualTo(FirstUserRace.Humans));
            Assert.That(_presenter.CurrentDraft.ClassFamily, Is.Null);
            Assert.That(_presenter.ConfirmDraftButton.interactable, Is.False);
        }

        [Test]
        public void DefaultVisibleCopyNeverEqualsRawEnumOrCanonicalMachineId()
        {
            string[] visible = _presenter
                .GetComponentsInChildren<Text>(includeInactive: true)
                .Select(text => text.text)
                .ToArray();
            string[] rawValues =
            {
                "crownlands",
                "stonehold",
                "eldergrove",
                "umbral",
                "Crownlands",
                "Stonehold",
                "Eldergrove",
                "Umbral",
                "Humans",
                "Dwarves",
                "Elves",
                "DarkElves",
                "Warrior",
                "Mage",
                "Ranger",
                "Assassin",
                "None"
            };

            for (int i = 0; i < rawValues.Length; i++)
            {
                Assert.That(visible, Does.Not.Contain(rawValues[i]),
                    "Visible values must come from the explicit preview copy provider.");
            }

            Assert.That(visible, Does.Contain(
                "DEVELOPMENT PREVIEW — IDENTITY DRAFT IS NOT SAVED"));
        }

        private string[] VisibleText()
        {
            return _presenter
                .GetComponentsInChildren<Text>()
                .Select(text => text.text)
                .ToArray();
        }
    }
}
