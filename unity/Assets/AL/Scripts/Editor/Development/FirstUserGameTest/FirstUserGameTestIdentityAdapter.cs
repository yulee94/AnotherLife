#if !UNITY_EDITOR
#error The first-user Game Test identity adapter is Editor-only.
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.UI.FirstUserIdentity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Editor.Development.FirstUserGameTest
{
    /// <summary>
    /// Editor-only presentation adapter for the isolated Game Test. Production identity flow,
    /// presenter, and copy remain unchanged; preview-on-hover and retained Back behavior live here.
    /// </summary>
    internal static class FirstUserGameTestIdentityAdapter
    {
        private static readonly RealmId[] RealmChoices =
        {
            RealmId.Crownlands,
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Umbral
        };

        internal static FirstUserIdentityDraftPresenter CreateStandalone()
        {
            var copy = new GameTestIdentityCopyProvider();
            FirstUserIdentityDraftPresenter presenter =
                FirstUserIdentityDraftPresenter.CreateStandalone(copy);
            AttachTransientRealmPreview(presenter, copy);
            return presenter;
        }

        internal static bool TryCreateRestoredClassDraft(
            FirstUserIdentityDraftSnapshot retained,
            out FirstUserIdentityDraftPresenter presenter,
            out string message)
        {
            presenter = null;
            message = string.Empty;
            if (retained == null || !retained.IsCustomizationReady ||
                !retained.HasRealm || !retained.HasClassFamily)
            {
                message = "The retained Editor-only identity draft was invalid.";
                return false;
            }

            try
            {
                presenter = CreateStandalone();
                presenter.GetRealmChoiceButton(retained.Realm).onClick.Invoke();
                presenter.ConfirmRealmButton.onClick.Invoke();
                presenter.GetClassFamilyChoiceButton(retained.ClassFamily.Value)
                    .onClick.Invoke();

                FirstUserIdentityDraftSnapshot restored = presenter.CurrentDraft;
                if (restored.Step != FirstUserIdentityDraftStep.ClassFamily ||
                    restored.Realm != retained.Realm ||
                    restored.Race != retained.Race ||
                    restored.ClassFamily != retained.ClassFamily ||
                    !restored.HasRealm || !restored.HasClassFamily)
                {
                    UnityEngine.Object.Destroy(presenter.transform.root.gameObject);
                    presenter = null;
                    message = "The Editor-only identity adapter could not restore the exact draft.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                if (presenter != null)
                {
                    UnityEngine.Object.Destroy(presenter.transform.root.gameObject);
                    presenter = null;
                }

                message =
                    "The Editor-only identity adapter threw " +
                    exception.GetType().Name + ".";
                return false;
            }
        }

        private static void AttachTransientRealmPreview(
            FirstUserIdentityDraftPresenter presenter,
            IFirstUserIdentityDraftCopyProvider copy)
        {
            Text summary = presenter.GetComponentsInChildren<Text>(true)
                .Single(text => string.Equals(
                    text.name,
                    "RealmPreviewSummary",
                    StringComparison.Ordinal));

            Action restoreSelected = () =>
            {
                FirstUserIdentityDraftSnapshot selected = presenter.CurrentDraft;
                if (!selected.HasRealm ||
                    !copy.TryGetRealmLabel(selected.Realm, out string realmLabel) ||
                    !copy.TryGetRaceLabel(selected.Race, out string raceLabel))
                {
                    summary.text = copy.RealmSelectionRequired;
                    return;
                }

                summary.text = copy.RealmAndRaceSummary(realmLabel, raceLabel);
            };

            for (int index = 0; index < RealmChoices.Length; index++)
            {
                RealmId realm = RealmChoices[index];
                if (!FirstUserIdentityDerivation.TryDeriveRace(
                        realm,
                        out FirstUserRace race) ||
                    !copy.TryGetRealmLabel(realm, out string realmLabel) ||
                    !copy.TryGetRaceLabel(race, out string raceLabel))
                {
                    throw new InvalidOperationException(
                        "The Editor-only realm preview copy was incomplete.");
                }

                string preview = copy.RealmAndRaceSummary(realmLabel, raceLabel);
                Button button = presenter.GetRealmChoiceButton(realm);
                var trigger = button.gameObject.AddComponent<EventTrigger>();
                trigger.triggers = new List<EventTrigger.Entry>(capacity: 4);
                AddTrigger(trigger, EventTriggerType.PointerEnter, () => summary.text = preview);
                AddTrigger(trigger, EventTriggerType.Select, () => summary.text = preview);
                AddTrigger(trigger, EventTriggerType.PointerExit, restoreSelected);
                AddTrigger(trigger, EventTriggerType.Deselect, restoreSelected);
                button.onClick.AddListener(() => summary.text = preview);
            }
        }

        private static void AddTrigger(
            EventTrigger trigger,
            EventTriggerType eventType,
            Action action)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private sealed class GameTestIdentityCopyProvider :
            IFirstUserIdentityDraftCopyProvider
        {
            private readonly DevelopmentFirstUserIdentityDraftCopyProvider _source =
                new DevelopmentFirstUserIdentityDraftCopyProvider();

            public string Disclosure => _source.Disclosure;
            public string Title => _source.Title;
            public string RealmHeading => _source.RealmHeading;
            public string RealmInstruction => _source.RealmInstruction;
            public string RealmSelectionRequired => _source.RealmSelectionRequired;
            public string ConfirmRealmAction => "Select Realm";
            public string ClassHeading => _source.ClassHeading;
            public string ClassInstruction => _source.ClassInstruction;
            public string ClassSelectionRequired => _source.ClassSelectionRequired;
            public string ReturnToRealmAction => _source.ReturnToRealmAction;
            public string ConfirmDraftAction => _source.ConfirmDraftAction;
            public string CustomizationReadyHeading => _source.CustomizationReadyHeading;

            public bool TryGetRealmLabel(RealmId realm, out string label) =>
                _source.TryGetRealmLabel(realm, out label);

            public bool TryGetRaceLabel(FirstUserRace race, out string label) =>
                _source.TryGetRaceLabel(race, out label);

            public bool TryGetClassFamilyLabel(
                ClassFamily classFamily,
                out string label) =>
                _source.TryGetClassFamilyLabel(classFamily, out label);

            public string SelectedChoice(string choiceLabel) =>
                _source.SelectedChoice(choiceLabel);

            public string RealmAndRaceSummary(string realmLabel, string raceLabel) =>
                _source.RealmAndRaceSummary(realmLabel, raceLabel);

            public string ClassSummary(string classLabel) =>
                _source.ClassSummary(classLabel);

            public string CustomizationReadySummary(
                string realmLabel,
                string raceLabel,
                string classLabel) =>
                _source.CustomizationReadySummary(
                    realmLabel,
                    raceLabel,
                    classLabel);
        }
    }
}
