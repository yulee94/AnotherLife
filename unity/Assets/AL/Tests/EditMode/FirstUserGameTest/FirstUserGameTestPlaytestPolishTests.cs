#if !UNITY_EDITOR
#error The isolated first-user playtest polish tests are Editor-only.
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.ChampionMode.Customization;
using AL.Core;
using AL.Editor.Development.FirstUserGameTest;
using AL.EditorTools;
using AL.UI.FirstUserIdentity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Tests.EditMode.FirstUserGameTest
{
    public sealed class FirstUserGameTestPlaytestPolishTests
    {
        private static readonly string[] ForbiddenPlayerCopy =
        {
            "DEVELOPMENT_EMULATOR_V1",
            "receipt",
            "projection",
            "hash",
            "code-unit",
            "byte",
            "customizationId",
            "developmentHandle",
            "TUTORIAL_FIRST_WORLD_ENTRY",
            "EVENT_TUTORIAL",
            "ACTION_FOLLOW",
            "RESULT_ACTIVE",
            "OMEN_1",
            "DarkElves"
        };

        private readonly List<GameObject> _ownedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _ownedObjects.Count - 1; index >= 0; index--)
            {
                if (_ownedObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[index]);
                }
            }

            _ownedObjects.Clear();
        }

        [TestCase((int)FirstUserGameTestPlaytestPhase.Loading, "[Loading]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.Identity, "[Identity]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.AppearanceAndName, "[Appearance & Name]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.WorldTutorial, "[World Tutorial]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.Omen, "[OMEN]")]
        public void BreadcrumbUsesExactFriendlyOrderedPhase(
            int phaseValue,
            string activeLabel)
        {
            FirstUserGameTestPlaytestPhase phase = (FirstUserGameTestPlaytestPhase)phaseValue;
            string breadcrumb = FirstUserGameTestPlaytestCopy.Breadcrumb(phase);

            StringAssert.Contains(activeLabel, breadcrumb);
            Assert.That(breadcrumb.IndexOf("Loading", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("Identity", StringComparison.Ordinal)));
            Assert.That(breadcrumb.IndexOf("Identity", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("Appearance & Name", StringComparison.Ordinal)));
            Assert.That(breadcrumb.IndexOf("Appearance & Name", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("World Tutorial", StringComparison.Ordinal)));
            Assert.That(breadcrumb.IndexOf("World Tutorial", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("OMEN", StringComparison.Ordinal)));
            AssertFriendlyPlayerCopy(breadcrumb);
        }

        [TestCase(RealmId.Crownlands, FirstUserRace.Humans, ClassFamily.Warrior,
            "Crownlands realm", "Human heritage", "Warrior path")]
        [TestCase(RealmId.Stonehold, FirstUserRace.Dwarves, ClassFamily.Mage,
            "Stonehold realm", "Dwarven heritage", "Mage path")]
        [TestCase(RealmId.Eldergrove, FirstUserRace.Elves, ClassFamily.Ranger,
            "Eldergrove realm", "Elven heritage", "Ranger path")]
        [TestCase(RealmId.Umbral, FirstUserRace.DarkElves, ClassFamily.Assassin,
            "Umbral realm", "Dark Elven heritage", "Assassin path")]
        public void IdentitySummaryUsesAuthoredLabelsWithoutEnumStringFallback(
            RealmId realm,
            FirstUserRace race,
            ClassFamily classFamily,
            string realmLabel,
            string raceLabel,
            string classLabel)
        {
            var identity = new FirstUserIdentityDraftSnapshot(
                FirstUserIdentityDraftStep.CustomizationReady,
                realm,
                race,
                classFamily);

            Assert.That(
                FirstUserGameTestPlaytestCopy.TryDescribeIdentity(identity, out string summary),
                Is.True);
            StringAssert.Contains(realmLabel, summary);
            StringAssert.Contains(raceLabel, summary);
            StringAssert.Contains(classLabel, summary);
            StringAssert.DoesNotContain("DarkElves", summary);
        }

        [Test]
        public void PlayerCopyInventoryContainsNoContractOrTransportTerminology()
        {
            string[] copy = typeof(FirstUserGameTestPlaytestCopy)
                .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(field => field.FieldType == typeof(string))
                .Select(field => field.GetValue(null) as string)
                .Where(value => value != null)
                .ToArray();

            Assert.That(copy, Is.Not.Empty);
            foreach (string value in copy)
            {
                AssertFriendlyPlayerCopy(value);
            }

            Assert.That(FirstUserGameTestPlaytestCopy.MoveObjective, Is.EqualTo("Move your character"));
            Assert.That(FirstUserGameTestPlaytestCopy.AttackObjective, Is.EqualTo("Use Basic Attack"));
            Assert.That(
                FirstUserGameTestPlaytestCopy.OmenDetail,
                Is.EqualTo("A new quest is available—open it to review."));
        }

        [Test]
        public void CustomizationPanelRendersFriendlyCopyWhilePreservingInternalSelection()
        {
            GameObject exitRoot = Own(new GameObject("PlaytestPolishExitRoot", typeof(RectTransform)));
            Button exit = FirstUserGameTestRuntimeHost.CreateButton(
                exitRoot.transform,
                "Exit",
                FirstUserGameTestPlaytestCopy.ExitAction,
                FirstUserGameTestRuntimeHost.BuiltInFont(),
                Vector2.zero,
                new Vector2(214f, 52f),
                Vector2.zero);
            var identity = new FirstUserIdentityDraftSnapshot(
                FirstUserIdentityDraftStep.CustomizationReady,
                RealmId.Eldergrove,
                FirstUserRace.Elves,
                ClassFamily.Ranger);
            var presets = new[]
            {
                new BodyPresetData
                {
                    id = "preset_internal_key",
                    displayName = "Friendly Shape",
                    scale = new[] { 1f, 1f, 1f }
                }
            };
            FirstUserGameTestCustomizationPanel panel =
                FirstUserGameTestCustomizationPanel.Create(
                    presets,
                    identity,
                    (_, __) => { },
                    () => { },
                    exit);
            _ownedObjects.Add(panel.gameObject);

            panel.SelectForTests("preset_internal_key");
            panel.HandleInput.text = "Eldergrove Scout";
            panel.SetBusy(false, FirstUserGameTestPlaytestCopy.ReadyForTutorial);
            Canvas.ForceUpdateCanvases();

            Assert.That(panel.SelectedCustomizationId, Is.EqualTo("preset_internal_key"));
            Assert.That(panel.HandleInput.text, Is.EqualTo("Eldergrove Scout"));
            Assert.That(
                ((Text)panel.HandleInput.placeholder).text,
                Is.EqualTo(FirstUserGameTestPlaytestCopy.NamePlaceholder));
            foreach (Text text in panel.GetComponentsInChildren<Text>(includeInactive: true))
            {
                AssertFriendlyPlayerCopy(text.text);
                StringAssert.DoesNotContain("preset_internal_key", text.text);
            }

            foreach (Button button in panel.GetComponentsInChildren<Button>(includeInactive: true))
            {
                Rect rect = button.GetComponent<RectTransform>().rect;
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(48f), button.name);
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(48f), button.name);
            }

            Button choice = panel.ChoiceButtons[0];
            Assert.That(choice.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(choice.navigation.selectOnLeft, Is.EqualTo(panel.BackButton));
            Assert.That(choice.navigation.selectOnRight, Is.EqualTo(panel.ConfirmButton));
            Assert.That(choice.navigation.selectOnUp, Is.EqualTo(exit));
            Assert.That(choice.navigation.selectOnDown, Is.EqualTo(panel.HandleInput));

            Assert.That(panel.HandleInput.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(panel.HandleInput.navigation.selectOnLeft, Is.EqualTo(panel.BackButton));
            Assert.That(panel.HandleInput.navigation.selectOnRight, Is.EqualTo(panel.ConfirmButton));
            Assert.That(panel.HandleInput.navigation.selectOnUp, Is.EqualTo(choice));
            Assert.That(panel.HandleInput.navigation.selectOnDown, Is.EqualTo(panel.ConfirmButton));

            Assert.That(panel.BackButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(panel.BackButton.navigation.selectOnLeft, Is.EqualTo(exit));
            Assert.That(panel.BackButton.navigation.selectOnRight, Is.EqualTo(panel.ConfirmButton));
            Assert.That(panel.BackButton.navigation.selectOnUp, Is.EqualTo(panel.HandleInput));
            Assert.That(panel.BackButton.navigation.selectOnDown, Is.EqualTo(exit));

            Assert.That(panel.ConfirmButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(panel.ConfirmButton.navigation.selectOnLeft, Is.EqualTo(panel.BackButton));
            Assert.That(panel.ConfirmButton.navigation.selectOnRight, Is.EqualTo(exit));
            Assert.That(panel.ConfirmButton.navigation.selectOnUp, Is.EqualTo(panel.HandleInput));
            Assert.That(panel.ConfirmButton.navigation.selectOnDown, Is.EqualTo(exit));

            Assert.That(exit.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            Assert.That(exit.navigation.selectOnLeft, Is.EqualTo(panel.ConfirmButton));
            Assert.That(exit.navigation.selectOnRight, Is.EqualTo(choice));
            Assert.That(exit.navigation.selectOnUp, Is.EqualTo(panel.BackButton));
            Assert.That(exit.navigation.selectOnDown, Is.EqualTo(choice));
        }

        [Test]
        public void CustomizationDraftRoundTripRetainsExactAppearanceAndNameForBackRetry()
        {
            GameObject exitRoot = Own(new GameObject("DraftRetentionExitRoot", typeof(RectTransform)));
            Button exit = FirstUserGameTestRuntimeHost.CreateButton(
                exitRoot.transform,
                "Exit",
                FirstUserGameTestPlaytestCopy.ExitAction,
                FirstUserGameTestRuntimeHost.BuiltInFont(),
                Vector2.zero,
                new Vector2(214f, 52f),
                Vector2.zero);
            var identity = new FirstUserIdentityDraftSnapshot(
                FirstUserIdentityDraftStep.CustomizationReady,
                RealmId.Stonehold,
                FirstUserRace.Dwarves,
                ClassFamily.Warrior);
            var presets = new[]
            {
                new BodyPresetData
                {
                    id = "preset_retained",
                    displayName = "Retained Shape",
                    scale = new[] { 1f, 1f, 1f }
                }
            };
            FirstUserGameTestCustomizationPanel first =
                FirstUserGameTestCustomizationPanel.Create(
                    presets,
                    identity,
                    (_, __) => { },
                    () => { },
                    exit);
            _ownedObjects.Add(first.gameObject);
            first.SelectForTests("preset_retained");
            first.HandleInput.text = "Stonehold Scout";

            FirstUserGameTestCustomizationDraft retained = first.CaptureDraft();
            UnityEngine.Object.DestroyImmediate(first.gameObject);

            FirstUserGameTestCustomizationPanel restored =
                FirstUserGameTestCustomizationPanel.Create(
                    presets,
                    identity,
                    (_, __) => { },
                    () => { },
                    exit,
                    retained);
            _ownedObjects.Add(restored.gameObject);

            Assert.That(restored.SelectedCustomizationId, Is.EqualTo("preset_retained"));
            Assert.That(restored.HandleInput.text, Is.EqualTo("Stonehold Scout"));
            Assert.That(restored.ConfirmButton.interactable, Is.True);
            Assert.That(restored.CaptureDraft().CustomizationId, Is.EqualTo(
                retained.CustomizationId));
            Assert.That(restored.CaptureDraft().DevelopmentHandle, Is.EqualTo(
                retained.DevelopmentHandle));
        }

        [Test]
        public void RetainedCustomizationDraftRejectsCatalogDriftBeforePanelConstruction()
        {
            var retained = new FirstUserGameTestCustomizationDraft(
                "preset_removed",
                "Stonehold Scout");
            var currentCatalog = new HashSet<string>(StringComparer.Ordinal)
            {
                "preset_current"
            };

            Assert.That(
                FirstUserGameTestRuntimeHost.TryValidateRetainedCustomizationDraft(
                    default,
                    currentCatalog,
                    out string emptyMessage),
                Is.True);
            Assert.That(emptyMessage, Is.Empty);

            Assert.That(
                FirstUserGameTestRuntimeHost.TryValidateRetainedCustomizationDraft(
                    retained,
                    currentCatalog,
                    out string message),
                Is.False);
            Assert.That(message, Is.Not.Empty);
            Assert.That(retained.CustomizationId, Is.EqualTo("preset_removed"));
            Assert.That(retained.DevelopmentHandle, Is.EqualTo("Stonehold Scout"));
        }

        [Test]
        public void EditorIdentityAdapterOwnsPreviewBehaviorWithoutChangingPlayerCopy()
        {
            FirstUserIdentityDraftPresenter production =
                FirstUserIdentityDraftPresenter.CreateStandalone();
            _ownedObjects.Add(production.transform.root.gameObject);
            Text productionAction = production.ConfirmRealmButton
                .GetComponentInChildren<Text>(true);
            Assert.That(
                productionAction.text,
                Is.EqualTo("Continue to class"));
            Assert.That(
                production.GetRealmChoiceButton(RealmId.Umbral)
                    .GetComponent<EventTrigger>(),
                Is.Null,
                "Production AL.Runtime must retain its baseline click-only realm behavior.");

            UnityEngine.Object.DestroyImmediate(production.transform.root.gameObject);
            FirstUserIdentityDraftPresenter editor =
                FirstUserGameTestIdentityAdapter.CreateStandalone();
            _ownedObjects.Add(editor.transform.root.gameObject);
            Text editorAction = editor.ConfirmRealmButton
                .GetComponentInChildren<Text>(true);
            Assert.That(editorAction.text, Is.EqualTo("Continue to class"));

            Button previewButton = editor.GetRealmChoiceButton(RealmId.Umbral);
            Assert.That(previewButton.GetComponent<EventTrigger>(), Is.Not.Null);
            ExecuteEvents.Execute(
                previewButton.gameObject,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.selectHandler);
            Assert.That(editor.CurrentDraft.HasRealm, Is.False,
                "Editor hover/focus preview cannot select a production draft.");
            previewButton.onClick.Invoke();
            Assert.That(editor.CurrentDraft.Realm, Is.EqualTo(RealmId.Umbral));
        }

        [Test]
        public void EditorIdentityAdapterRestoresExactClassDraftAfterCustomizationBack()
        {
            var retained = new FirstUserIdentityDraftSnapshot(
                FirstUserIdentityDraftStep.CustomizationReady,
                RealmId.Eldergrove,
                FirstUserRace.Elves,
                ClassFamily.Ranger);

            Assert.That(
                FirstUserGameTestIdentityAdapter.TryCreateRestoredClassDraft(
                    retained,
                    out FirstUserIdentityDraftPresenter presenter,
                    out string message),
                Is.True,
                message);
            _ownedObjects.Add(presenter.transform.root.gameObject);
            Assert.That(presenter.CurrentDraft.Step, Is.EqualTo(
                FirstUserIdentityDraftStep.ClassFamily));
            Assert.That(presenter.CurrentDraft.Realm, Is.EqualTo(retained.Realm));
            Assert.That(presenter.CurrentDraft.Race, Is.EqualTo(retained.Race));
            Assert.That(
                presenter.CurrentDraft.ClassFamily,
                Is.EqualTo(retained.ClassFamily));
        }

        [TestCase(false, false, false, false, true, "Ready to begin.",
            GameTestModeControlPanelPresentation.StartAction, true)]
        [TestCase(false, false, false, false, false, "Start is temporarily unavailable.",
            GameTestModeControlPanelPresentation.StartAction, false)]
        [TestCase(false, false, false, true, false,
            "Another Unity Play session is running.",
            GameTestModeControlPanelPresentation.StartAction, false)]
        [TestCase(true, false, false, true, false,
            "The First User Experience is running.",
            GameTestModeControlPanelPresentation.StopAction, true)]
        [TestCase(true, false, false, false, false,
            "The First User Experience is running.",
            GameTestModeControlPanelPresentation.StopAction, true)]
        [TestCase(false, true, false, false, false,
            "A previous isolated test is waiting for cleanup.",
            GameTestModeControlPanelPresentation.CleanupAction, true)]
        [TestCase(false, true, false, true, false,
            "A previous isolated test is waiting for cleanup.",
            GameTestModeControlPanelPresentation.CleanupAction, false)]
        [TestCase(false, true, true, false, false,
            "Safe cleanup needs attention.",
            GameTestModeControlPanelPresentation.ForgetAction, true)]
        [TestCase(false, true, true, true, false,
            "Safe cleanup needs attention.",
            GameTestModeControlPanelPresentation.ForgetAction, false)]
        public void ControlPanelPresentationIsFriendlyAndDoesNotEchoRawEvidence(
            bool active,
            bool recovery,
            bool invalid,
            bool playing,
            bool canStart,
            string expectedState,
            string expectedAction,
            bool expectedActionEnabled)
        {
            const string raw =
                "DEVELOPMENT_EMULATOR_V1 receipt projection hash code-unit byte C:\\private\\root";

            GameTestModeControlPanelView view = GameTestModeControlPanelPresentation.Build(
                active,
                recovery,
                invalid,
                playing,
                canStart,
                raw,
                raw);

            Assert.That(view.CurrentState, Is.EqualTo(expectedState));
            Assert.That(view.PrimaryAction, Is.EqualTo(expectedAction));
            Assert.That(view.PrimaryActionEnabled, Is.EqualTo(expectedActionEnabled));
            AssertFriendlyPlayerCopy(view.CurrentState);
            AssertFriendlyPlayerCopy(view.Blocker);
            Assert.That(raw, Does.Contain("receipt"),
                "The renderer must not modify or replace retained internal audit evidence.");
        }

        [Test]
        public void ControlPanelCommandGateMakesSuccessfulStartAndCleanupOneShot()
        {
            var startGate = new GameTestModeControlPanelCommandGate();
            int starts = 0;
            Assert.That(startGate.TryStart(() => { starts++; return true; }), Is.True);
            Assert.That(startGate.TryStart(() => { starts++; return true; }), Is.False);
            Assert.That(starts, Is.EqualTo(1));

            var cleanupGate = new GameTestModeControlPanelCommandGate();
            int cleanups = 0;
            Assert.That(cleanupGate.TryCleanUp(() => cleanups++), Is.True);
            Assert.That(cleanupGate.TryCleanUp(() => cleanups++), Is.False);
            Assert.That(cleanups, Is.EqualTo(1));
        }

        [Test]
        public void ControlPanelTruthfullyBlocksWhenRealAssetsAreNotAdmitted()
        {
            GameTestModeControlPanelView view = GameTestModeControlPanelPresentation.Build(
                sessionActive: false,
                recoveryPending: false,
                invalidRecoveryRecord: false,
                playModeActive: false,
                canStart: false,
                rawStatus: string.Empty,
                rawBlocker:
                    "The authored onboarding module and its admitted real assets are unavailable.");

            Assert.That(view.CurrentState, Is.EqualTo("Start is temporarily unavailable."));
            Assert.That(view.PrimaryActionEnabled, Is.False);
            Assert.That(view.Blocker, Does.Contain("real champion"));
            Assert.That(view.Blocker, Does.Contain("playtest remains locked"));
        }

        [Test]
        public void FailedStartCanBeRetriedWithoutDuplicatingSuccessfulTransition()
        {
            var gate = new GameTestModeControlPanelCommandGate();
            int attempts = 0;
            Assert.That(gate.TryStart(() => { attempts++; return false; }), Is.False);
            Assert.That(gate.TryStart(() => { attempts++; return true; }), Is.True);
            Assert.That(gate.TryStart(() => { attempts++; return true; }), Is.False);
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void ControlPanelSourceUsesProminentFriendlyActionAndDoesNotRenderRawPath()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "AL",
                "Scripts",
                "Editor",
                "GameTestModeWindow.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.Contains(GameTestModeControlPanelPresentation.StartAction, source);
            StringAssert.Contains("GUILayout.Height(58f)", source);
            StringAssert.DoesNotContain("Last isolated root", source);
            StringAssert.DoesNotContain(
                "EditorGUILayout.SelectableLabel(\n                GameTestModeEditorCoordinator.CurrentStatus",
                source.Replace("\r\n", "\n"));
        }

        private GameObject Own(GameObject gameObject)
        {
            _ownedObjects.Add(gameObject);
            return gameObject;
        }

        private static void AssertFriendlyPlayerCopy(string value)
        {
            value = value ?? string.Empty;
            foreach (string forbidden in ForbiddenPlayerCopy)
            {
                Assert.That(
                    value.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase),
                    Is.EqualTo(-1),
                    "Player-visible copy leaked '" + forbidden + "': " + value);
            }
        }
    }
}
