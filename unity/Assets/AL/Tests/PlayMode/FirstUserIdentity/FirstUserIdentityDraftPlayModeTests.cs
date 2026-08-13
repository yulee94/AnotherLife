using System.Collections;
using AL.Core;
using AL.UI.FirstUserIdentity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode.FirstUserIdentity
{
    public sealed class FirstUserIdentityDraftPlayModeTests
    {
        [UnityTest]
        public IEnumerator StandaloneUguiJourneyStopsAtCustomizationWithoutNavigation()
        {
            FirstUserIdentityDraftPresenter presenter =
                FirstUserIdentityDraftPresenter.CreateStandalone();
            GameObject host = presenter.transform.root.gameObject;
            int readyCount = 0;
            FirstUserIdentityDraftSnapshot ready = null;
            presenter.CustomizationReady += snapshot =>
            {
                readyCount++;
                ready = snapshot;
            };

            yield return null;

            Assert.That(host.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(host.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);
            Assert.That(presenter.CurrentDraft.ClassFamily, Is.Null);

            presenter.GetRealmChoiceButton(RealmId.Umbral).onClick.Invoke();
            yield return null;
            Assert.That(presenter.CurrentDraft.Race, Is.EqualTo(FirstUserRace.DarkElves));
            Assert.That(presenter.CurrentDraft.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.Realm));

            presenter.ConfirmRealmButton.onClick.Invoke();
            yield return null;
            Assert.That(presenter.CurrentDraft.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.ClassFamily));
            Assert.That(presenter.CurrentDraft.ClassFamily, Is.Null);

            presenter.GetClassFamilyChoiceButton(ClassFamily.Assassin).onClick.Invoke();
            yield return null;
            presenter.ConfirmDraftButton.onClick.Invoke();
            yield return null;

            Assert.That(readyCount, Is.EqualTo(1));
            Assert.That(ready, Is.Not.Null);
            Assert.That(ready.IsCustomizationReady, Is.True);
            Assert.That(ready.Realm, Is.EqualTo(RealmId.Umbral));
            Assert.That(ready.Race, Is.EqualTo(FirstUserRace.DarkElves));
            Assert.That(ready.ClassFamily, Is.EqualTo(ClassFamily.Assassin));
            Assert.That(presenter.GetComponentsInChildren<Button>().Length, Is.Zero);
            Assert.That(presenter.gameObject.scene.name, Is.EqualTo(host.scene.name),
                "The boundary remains in the current test scene.");

            presenter.ConfirmDraftButton.onClick.Invoke();
            yield return null;
            Assert.That(readyCount, Is.EqualTo(1));

            Object.Destroy(host);
            yield return null;
        }
    }
}
