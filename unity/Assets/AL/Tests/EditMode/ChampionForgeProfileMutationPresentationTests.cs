using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Skills;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Runtime;
using AL.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AL.Tests.EditMode
{
    public sealed class ChampionForgeProfileMutationPresentationTests
    {
        private const string ProfileId =
            "alp_11111111111111111111111111111111";
        private const string AuthorityEpoch =
            "11111111111111112222222222222222";
        private const string GenerationFingerprint =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static readonly string[] MutationButtonNames =
        {
            "Primary",
            "Hair",
            "Skin",
            "Hair Style",
            "Body",
            "Armor",
            "Eyes",
            "Accent",
            "Face",
            "Weapon",
            "Offhand",
            "Cape",
            "Vanguard",
            "Arcanist",
            "Nightblade",
            "Dread",
            "Oracle",
            "Duelist",
            "Inquisitor",
            "Warden",
            "Spellblade",
            "Random",
            "Reset",
            "Helmet"
        };

        private GameObject _controllerHost;
        private GameObject _championHost;

        [SetUp]
        public void SetUp()
        {
            ServicesDictionary().Clear();
            DestroyImmediateIfPresent(GameObject.Find("ChampionMode_HUD"));
            DestroyImmediateIfPresent(GameObject.Find("EventSystem"));
        }

        [TearDown]
        public void TearDown()
        {
            ServicesDictionary().Clear();
            DestroyImmediateIfPresent(_controllerHost);
            DestroyImmediateIfPresent(_championHost);
            _controllerHost = null;
            _championHost = null;
            DestroyImmediateIfPresent(GameObject.Find("ChampionMode_HUD"));
            DestroyImmediateIfPresent(GameObject.Find("EventSystem"));
        }

        [Test]
        public void SurfaceResolutionRequiresProfileAndSeparateCustomizationAuthority()
        {
            var writableProvider = new AuthoritySaveService(
                CreateAuthority(ProfileWriteAuthorityStatus.Writable));
            var migrationProvider = new AuthoritySaveService(
                CreateAuthority(ProfileWriteAuthorityStatus.MigrationRequired));

            ProfileMutationPresentationState writableProfile =
                ProfileMutationPresentationPolicy.Capture(
                    writableProvider,
                    productionWriteActivationEnabled: true);
            ProfileMutationPresentationState migrationProfile =
                ProfileMutationPresentationPolicy.Capture(
                    migrationProvider,
                    productionWriteActivationEnabled: true);

            ProfileMutationSurfacePresentationState surfaceInactive =
                ProfileMutationPresentationPolicy.ResolveSurface(
                    writableProfile,
                    surfaceActivationEnabled: false);
            ProfileMutationSurfacePresentationState profileBlocked =
                ProfileMutationPresentationPolicy.ResolveSurface(
                    migrationProfile,
                    surfaceActivationEnabled: true);
            ProfileMutationSurfacePresentationState fullyEnabled =
                ProfileMutationPresentationPolicy.ResolveSurface(
                    writableProfile,
                    surfaceActivationEnabled: true);

            Assert.False(surfaceInactive.MutationCommandsEnabled);
            Assert.AreEqual(
                "PROFILE WRITES NOT ACTIVATED",
                surfaceInactive.ReasonText);
            Assert.False(profileBlocked.MutationCommandsEnabled);
            Assert.AreEqual(
                "PROFILE MIGRATION REQUIRED",
                profileBlocked.ReasonText);
            Assert.True(fullyEnabled.MutationCommandsEnabled);
            Assert.AreEqual(
                "PROFILE AUTHORITY VERIFIED",
                fullyEnabled.ReasonText);
            Assert.AreEqual(1, writableProvider.AuthorityReadCount);
            Assert.AreEqual(1, migrationProvider.AuthorityReadCount);
        }

        [Test]
        public void ForgeCapturesOnceDisablesExactMutationSetAndPreservesControls()
        {
            var save = new AuthoritySaveService(
                CreateAuthority(ProfileWriteAuthorityStatus.MigrationRequired));
            ServiceLocator.Register<ISaveGameService>(save);

            _championHost = new GameObject("ChampionForgePresentationChampion");
            ChampionCombat combat = _championHost.AddComponent<ChampionCombat>();
            ChampionController player =
                _championHost.AddComponent<ChampionController>();
            SkillCaster skills = _championHost.GetComponent<SkillCaster>();
            ChampionCustomizationController customization =
                _championHost.AddComponent<ChampionCustomizationController>();

            _controllerHost = new GameObject("ChampionForgePresentationController");
            var controller =
                _controllerHost.AddComponent<ChampionArenaSceneController>();
            SetField(controller, "_playerCombat", combat);
            SetField(controller, "_playerController", player);
            SetField(controller, "_playerSkillCaster", skills);
            SetField(controller, "_playerCustomization", customization);
            SetField(controller, "_encounterClearShown", true);

            Invoke(controller, "BuildHud");

            GameObject hud = GameObject.Find("ChampionMode_HUD");
            Assert.NotNull(hud);

            FieldInfo activationGate = typeof(ChampionArenaSceneController)
                .GetField(
                    "ChampionCustomizationSurfaceActivationEnabled",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(activationGate);
            Assert.True(activationGate.IsLiteral);
            Assert.False((bool)activationGate.GetRawConstantValue());

            var registeredButtons =
                (Button[])GetField(controller, "_forgeMutationButtons");
            int registeredCount =
                (int)GetField(controller, "_forgeMutationButtonCount");
            Assert.AreEqual(24, registeredButtons.Length);
            Assert.AreEqual(24, registeredCount);
            Assert.That(
                registeredButtons.Select(button => button.name),
                Is.EquivalentTo(MutationButtonNames));
            Assert.False(registeredButtons.Any(button => button == null));

            Text profileStatus =
                (Text)GetField(controller, "_appearanceProfileText");
            Assert.AreEqual(
                "READ-ONLY — PROFILE MIGRATION REQUIRED",
                profileStatus.text);
            Assert.True(profileStatus.resizeTextForBestFit);
            Assert.AreEqual(6, profileStatus.resizeTextMinSize);
            Assert.AreEqual(11, profileStatus.resizeTextMaxSize);
            AssertColor(
                new Color(0.66f, 0.70f, 0.75f, 0.94f),
                profileStatus.color,
                "read-only status color");
            Image profilePlate =
                (Image)GetField(controller, "_appearanceProfilePlate");
            AssertColor(
                new Color(0.018f, 0.022f, 0.028f, 0.94f),
                profilePlate.color,
                "read-only profile plate");

            string initialState = JsonUtility.ToJson(
                save.CurrentSave.ChampionCustomization);
            SetField(controller, "_playerCustomization", null);
            foreach (Button button in registeredButtons)
            {
                Assert.False(button.interactable, button.name);
                Assert.AreEqual(
                    Selectable.Transition.None,
                    button.transition,
                    button.name);

                Image background = button.GetComponent<Image>();
                Text label = button.GetComponentInChildren<Text>(true);
                Outline outline = button.GetComponent<Outline>();
                AssertColor(
                    new Color(0.028f, 0.032f, 0.038f, 0.96f),
                    background.color,
                    button.name + " muted background");
                AssertColor(
                    new Color(0.58f, 0.62f, 0.67f, 0.86f),
                    label.color,
                    button.name + " muted label");
                AssertColor(
                    new Color(0.24f, 0.28f, 0.34f, 0.20f),
                    outline.effectColor,
                    button.name + " muted outline");
                Color backgroundColor = background.color;
                Color labelColor = label.color;
                Vector3 scale = button.transform.localScale;
                var pointer = new PointerEventData(EventSystem.current);

                button.OnPointerEnter(pointer);
                button.OnPointerDown(pointer);
                button.OnPointerUp(pointer);
                button.OnPointerExit(pointer);
                Assert.DoesNotThrow(
                    () => button.onClick.Invoke(),
                    button.name +
                    " must stop at the shared guard before dereferencing customization.");

                AssertColor(backgroundColor, background.color, button.name);
                AssertColor(labelColor, label.color, button.name);
                Assert.AreEqual(scale, button.transform.localScale, button.name);
            }
            SetField(controller, "_playerCustomization", customization);
            Assert.AreEqual(
                24,
                (int)GetField(controller, "_forgeMutationGuardInvocationCount"),
                "Every exact Forge listener must traverse the shared guard.");

            int guardedCallbackCount = 0;
            Invoke(
                controller,
                "TryInvokeForgeMutation",
                new UnityAction(() => guardedCallbackCount++));
            Assert.AreEqual(0, guardedCallbackCount);
            Assert.AreEqual(
                25,
                (int)GetField(controller, "_forgeMutationGuardInvocationCount"));
            Assert.AreEqual(
                initialState,
                JsonUtility.ToJson(save.CurrentSave.ChampionCustomization));
            Assert.AreEqual(0, save.SaveCount);

            AssertButtonsInteractable(hud, "Attack", 1);
            AssertButtonsInteractable(hud, "Dodge", 1);
            AssertButtonsInteractable(hud, "Manual", 1);
            AssertButtonsInteractable(hud, "Assist", 1);
            AssertButtonsInteractable(hud, "Auto", 1);
            AssertButtonsInteractable(hud, "^", 1);
            AssertButtonsInteractable(hud, "<", 1);
            AssertButtonsInteractable(hud, ">", 1);
            AssertButtonsInteractable(hud, "v", 1);
            AssertButtonsInteractable(hud, "Inspect", 3);
            AssertButtonsInteractable(hud, "Retry", 2);
            AssertButtonsInteractable(hud, "Kingdom", 3);
            for (int slot = 1; slot <= 4; slot++)
            {
                Button[] skillButtons = hud.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith(
                        slot + ". ",
                        StringComparison.Ordinal))
                    .ToArray();
                Assert.AreEqual(1, skillButtons.Length, "skill " + slot);
                Assert.True(skillButtons[0].interactable, "skill " + slot);
            }

            var swatches = (Image[])GetField(controller, "_appearanceSwatches");
            Assert.AreEqual(5, swatches.Length);
            Assert.False(swatches.Any(swatch => swatch == null));
            string summary = ((Text)GetField(
                controller,
                "_appearanceSummaryText")).text;

            Invoke(controller, "RefreshAppearanceText");
            Assert.AreEqual(
                "READ-ONLY — PROFILE MIGRATION REQUIRED",
                profileStatus.text);
            Assert.AreEqual(
                summary,
                ((Text)GetField(controller, "_appearanceSummaryText")).text);

            Invoke(controller, "ToggleAppearanceInspection");
            Assert.AreEqual(
                "READ-ONLY — PROFILE MIGRATION REQUIRED",
                profileStatus.text);
            AssertColor(
                new Color(0.66f, 0.70f, 0.75f, 0.94f),
                profileStatus.color,
                "Inspect must not brighten read-only status");
            AssertColor(
                new Color(0.018f, 0.022f, 0.028f, 0.94f),
                profilePlate.color,
                "Inspect must not brighten read-only plate");

            SetField(controller, "_skillHudTimer", 0.25f);
            Invoke(controller, "Update");
            Assert.AreEqual(
                "READ-ONLY — PROFILE MIGRATION REQUIRED",
                profileStatus.text);
            Assert.AreEqual(
                1,
                save.AuthorityReadCount,
                "Build/refresh/update/Inspect must consume one cached authority snapshot.");
            Assert.AreEqual(0, save.SaveCount);
            Assert.AreEqual(
                initialState,
                JsonUtility.ToJson(save.CurrentSave.ChampionCustomization));
        }

        [Test]
        public void MissingCustomizationStillShowsCachedReadOnlyAuthority()
        {
            var save = new AuthoritySaveService(
                CreateAuthority(ProfileWriteAuthorityStatus.MigrationRequired));
            ServiceLocator.Register<ISaveGameService>(save);

            _championHost = new GameObject("ChampionForgeMissingCustomization");
            ChampionCombat combat = _championHost.AddComponent<ChampionCombat>();
            ChampionController player =
                _championHost.AddComponent<ChampionController>();
            SkillCaster skills = _championHost.GetComponent<SkillCaster>();

            _controllerHost = new GameObject(
                "ChampionForgeMissingCustomizationController");
            var controller =
                _controllerHost.AddComponent<ChampionArenaSceneController>();
            SetField(controller, "_playerCombat", combat);
            SetField(controller, "_playerController", player);
            SetField(controller, "_playerSkillCaster", skills);
            SetField(controller, "_encounterClearShown", true);

            Assert.DoesNotThrow(() => Invoke(controller, "BuildHud"));

            Text status = (Text)GetField(controller, "_appearanceProfileText");
            Assert.AreEqual(
                "READ-ONLY — PROFILE MIGRATION REQUIRED",
                status.text);
            Assert.AreEqual(1, save.AuthorityReadCount);

            var buttons = (Button[])GetField(
                controller,
                "_forgeMutationButtons");
            Assert.AreEqual(24, buttons.Length);
            Assert.False(buttons.Any(button => button == null));
            Assert.False(buttons.Any(button => button.interactable));
        }

        [Test]
        public void MutationButtonCardinalityFailsClosedBelowOrAboveTwentyFour()
        {
            _controllerHost = new GameObject(
                "ChampionForgeCardinalityController");
            var controller =
                _controllerHost.AddComponent<ChampionArenaSceneController>();

            SetField(controller, "_forgeMutationCommandsEnabled", true);
            Invoke(controller, "FinalizeForgeMutationButtons");
            Assert.False((bool)GetField(
                controller,
                "_forgeMutationCommandsEnabled"));

            var buttonRoot = new GameObject("ForgeCardinalityButtons");
            buttonRoot.transform.SetParent(_controllerHost.transform, false);
            Font font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            SetField(controller, "_forgeMutationCommandsEnabled", true);

            Button overflow = null;
            for (int index = 0; index <= 24; index++)
            {
                overflow = (Button)Invoke(
                    controller,
                    "CreateForgeMutationButton",
                    buttonRoot.transform,
                    font,
                    "Probe " + index,
                    Vector2.zero,
                    new Vector2(112f, 32f),
                    new UnityAction(() => { }),
                    13,
                    new Color(0.1f, 0.1f, 0.1f, 0.95f));
            }

            Assert.AreEqual(
                24,
                (int)GetField(controller, "_forgeMutationButtonCount"));
            Assert.False((bool)GetField(
                controller,
                "_forgeMutationCommandsEnabled"));
            Assert.NotNull(overflow);
            Assert.False(overflow.interactable);
            Assert.AreEqual(Selectable.Transition.None, overflow.transition);
            var registered = (Button[])GetField(
                controller,
                "_forgeMutationButtons");
            Assert.False(registered.Any(button => button.interactable));

            registered[7] = null;
            for (int index = 0; index < registered.Length; index++)
            {
                if (registered[index] != null)
                {
                    registered[index].interactable = true;
                }
            }

            SetField(controller, "_forgeMutationCommandsEnabled", true);
            Invoke(controller, "FinalizeForgeMutationButtons");
            Assert.False((bool)GetField(
                controller,
                "_forgeMutationCommandsEnabled"));
            Assert.False(registered.Any(
                button => button != null && button.interactable));
        }

        private static ProfileWriteAuthoritySnapshot CreateAuthority(
            ProfileWriteAuthorityStatus status)
        {
            switch (status)
            {
                case ProfileWriteAuthorityStatus.Writable:
                    return ProfileWriteAuthoritySnapshotFactory.Writable(
                        ProfileId,
                        AuthorityEpoch,
                        GenerationFingerprint,
                        ProfileAuthoritySourceGeneration.Primary,
                        Array.Empty<string>());
                case ProfileWriteAuthorityStatus.MigrationRequired:
                    return ProfileWriteAuthoritySnapshotFactory.MigrationRequired(
                        ProfileAuthoritySourceGeneration.Primary,
                        new[] { "AL-TEST-MIGRATION" });
                default:
                    return ProfileWriteAuthoritySnapshotFactory.Unavailable(
                        "AL-TEST-AUTHORITY-UNAVAILABLE");
            }
        }

        private static void AssertButtonsInteractable(
            GameObject root,
            string name,
            int expectedCount)
        {
            Button[] matches = root.GetComponentsInChildren<Button>(true)
                .Where(button => string.Equals(
                    button.name,
                    name,
                    StringComparison.Ordinal))
                .ToArray();
            Assert.AreEqual(expectedCount, matches.Length, name);
            Assert.False(
                matches.Any(button => !button.interactable),
                name);
        }

        private static void AssertColor(
            Color expected,
            Color actual,
            string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), message);
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), message);
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), message);
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), message);
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            return method.Invoke(instance, arguments);
        }

        private static object GetField(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            return field.GetValue(instance);
        }

        private static void SetField(
            object instance,
            string fieldName,
            object value)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private static IDictionary ServicesDictionary() =>
            (IDictionary)typeof(ServiceLocator)
                .GetField(
                    "Services",
                    BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);

        private static void DestroyImmediateIfPresent(UnityEngine.Object value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }

        private sealed class AuthoritySaveService :
            ISaveGameService,
            IProfileWriteAuthorityProvider
        {
            private readonly ProfileWriteAuthoritySnapshot _authority;

            public AuthoritySaveService(
                ProfileWriteAuthoritySnapshot authority)
            {
                _authority = authority;
                CurrentSave = new SaveGameData
                {
                    SaveFormatId = SaveGameData.CurrentSaveFormatId,
                    SaveSchemaVersion = SaveGameData.CurrentSaveSchemaVersion,
                    ProfileInitializationVersion =
                        SaveGameData.CurrentProfileInitializationVersion,
                    SelectedRealm = RealmId.Stonehold
                };
            }

            public int AuthorityReadCount { get; private set; }
            public int SaveCount { get; private set; }
            public int LoadCount { get; private set; }
            public int DeleteCount { get; private set; }
            public SaveGameData CurrentSave { get; private set; }
            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public ProfileWriteAuthoritySnapshot GetCurrentAuthority()
            {
                AuthorityReadCount++;
                return _authority;
            }

            public void Save() => SaveCount++;
            public void Load() => LoadCount++;
            public bool HasSave() => CurrentSave != null;

            public void CreateNewSave(RealmId realmId)
            {
                CurrentSave = new SaveGameData { SelectedRealm = realmId };
            }

            public void DeleteSave()
            {
                DeleteCount++;
                CurrentSave = null;
            }
        }
    }
}
