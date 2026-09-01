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

        [TestCase((int)FirstUserGameTestPlaytestPhase.Loading, "[Preparing]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.Identity, "[Origin]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.AppearanceAndName, "[Appearance]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.WorldTutorial, "[First Steps]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.Omen, "[Valerius]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.SkyCastle, "[Sky Castle]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.ValeriusReturn, "[Return]")]
        [TestCase((int)FirstUserGameTestPlaytestPhase.RealmReady, "[Realm Ready]")]
        public void BreadcrumbUsesExactFriendlyOrderedPhase(
            int phaseValue,
            string activeLabel)
        {
            FirstUserGameTestPlaytestPhase phase = (FirstUserGameTestPlaytestPhase)phaseValue;
            string breadcrumb = FirstUserGameTestPlaytestCopy.Breadcrumb(phase);

            StringAssert.Contains(activeLabel, breadcrumb);
            Assert.That(breadcrumb.IndexOf("Preparing", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("Origin", StringComparison.Ordinal)));
            Assert.That(breadcrumb.IndexOf("Origin", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("Appearance", StringComparison.Ordinal)));
            Assert.That(breadcrumb.IndexOf("Appearance", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("First Steps", StringComparison.Ordinal)));
            Assert.That(breadcrumb.IndexOf("First Steps", StringComparison.Ordinal), Is.LessThan(
                breadcrumb.IndexOf("Valerius", StringComparison.Ordinal)));
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

            Assert.That(FirstUserGameTestPlaytestCopy.MoveObjective, Is.EqualTo("Move your Champion"));
            Assert.That(FirstUserGameTestPlaytestCopy.AttackObjective, Is.EqualTo("Use Basic Attack"));
            Assert.That(
                FirstUserGameTestPlaytestCopy.OmenDetail,
                Is.EqualTo("The Veil Watch has sent an urgent dispatch from the Sky Castle."));
            Assert.That(
                FirstUserGameTestPlaytestCopy.OmenObjective,
                Is.EqualTo("Hear Valerius's report"));
            Assert.That(
                FirstUserGameTestPlaytestCopy.OmenOpenedStatus,
                Is.EqualTo("Choose your response"));
            Assert.That(
                FirstUserGameTestPlaytestCopy.OmenDeploymentStatus,
                Is.EqualTo("Mission accepted · Deployment prepared"));
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
            Assert.That(choice.GetComponentInChildren<Text>().text,
                Is.EqualTo("Selected: Friendly Shape"));
            Assert.That(choice.GetComponent<Outline>().effectDistance,
                Is.EqualTo(new Vector2(3f, -3f)));
            Assert.That(panel.BackButton.GetComponentInChildren<Text>().text,
                Is.EqualTo("Change realm or class"));
            Assert.That(panel.ConfirmButton.GetComponentInChildren<Text>().text,
                Is.EqualTo("Enter the world"));
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

            panel.SetBusy(true, FirstUserGameTestPlaytestCopy.PreparingWorld);
            Assert.That(panel.BackButton.interactable, Is.False,
                "Back must not remain as an enabled no-op while world entry is committed.");
            Assert.That(panel.ConfirmButton.interactable, Is.False);
            Assert.That(choice.interactable, Is.False);
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
