using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.UI.FirstUserIdentity
{
    public sealed class FirstUserIdentityDraftPresenter : MonoBehaviour
    {
        private static readonly RealmId[] RealmChoices =
        {
            RealmId.Crownlands,
            RealmId.Stonehold,
            RealmId.Eldergrove,
            RealmId.Umbral
        };

        private static readonly ClassFamily[] ClassChoices =
        {
            ClassFamily.Warrior,
            ClassFamily.Mage,
            ClassFamily.Ranger,
            ClassFamily.Assassin
        };

        private readonly Dictionary<RealmId, Button> _realmButtons =
            new Dictionary<RealmId, Button>();
        private readonly Dictionary<RealmId, Text> _realmButtonLabels =
            new Dictionary<RealmId, Text>();
        private readonly Dictionary<ClassFamily, Button> _classButtons =
            new Dictionary<ClassFamily, Button>();
        private readonly Dictionary<ClassFamily, Text> _classButtonLabels =
            new Dictionary<ClassFamily, Text>();

        private readonly FirstUserIdentityDraftFlow _flow =
            new FirstUserIdentityDraftFlow();

        private IFirstUserIdentityDraftCopyProvider _copy;
        private Font _font;
        private GameObject _realmStep;
        private GameObject _classStep;
        private GameObject _customizationReadyStep;
        private Text _realmSummary;
        private Text _classRealmSummary;
        private Text _classSummary;
        private Text _customizationReadySummary;
        private Button _confirmRealmButton;
        private Button _returnToRealmButton;
        private Button _confirmDraftButton;

        public event Action<FirstUserIdentityDraftSnapshot> CustomizationReady;

        public FirstUserIdentityDraftSnapshot CurrentDraft => _flow.Snapshot;
        public Button ConfirmRealmButton => _confirmRealmButton;
        public Button ReturnToRealmButton => _returnToRealmButton;
        public Button ConfirmDraftButton => _confirmDraftButton;

        public static FirstUserIdentityDraftPresenter Create(
            Transform parent,
            IFirstUserIdentityDraftCopyProvider copyProvider = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var root = new GameObject(
                "FirstUserIdentityDraft",
                typeof(RectTransform),
                typeof(Image));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var presenter = root.AddComponent<FirstUserIdentityDraftPresenter>();
            presenter.Initialize(
                copyProvider ?? new DevelopmentFirstUserIdentityDraftCopyProvider());
            return presenter;
        }

        public static FirstUserIdentityDraftPresenter CreateStandalone(
            IFirstUserIdentityDraftCopyProvider copyProvider = null)
        {
            var canvasObject = new GameObject(
                "FirstUserIdentityDraftCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (EventSystem.current == null)
            {
                var eventSystemObject = new GameObject(
                    "FirstUserIdentityDraftEventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(canvasObject.transform, false);
            }

            return Create(canvasObject.transform, copyProvider);
        }

        public Button GetRealmChoiceButton(RealmId realm)
        {
            if (!_realmButtons.TryGetValue(realm, out Button button))
            {
                throw new ArgumentOutOfRangeException(nameof(realm));
            }

            return button;
        }

        public Button GetClassFamilyChoiceButton(ClassFamily classFamily)
        {
            if (!_classButtons.TryGetValue(classFamily, out Button button))
            {
                throw new ArgumentOutOfRangeException(nameof(classFamily));
            }

            return button;
        }

        private void Initialize(IFirstUserIdentityDraftCopyProvider copyProvider)
        {
            _copy = copyProvider ?? throw new ArgumentNullException(nameof(copyProvider));
            ValidateCopyProvider(_copy);

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                    Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null)
            {
                throw new InvalidOperationException("A built-in uGUI font is required.");
            }

            BuildView();
            RefreshView();
            Focus(GetRealmChoiceButton(RealmId.Crownlands));
        }

        private void BuildView()
        {
            Image background = GetComponent<Image>();
            background.color = new Color(0.014f, 0.018f, 0.026f, 1f);

            GameObject panel = CreatePanel(transform, "IdentityDraftPanel");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.06f, 0.04f);
            panelRect.anchorMax = new Vector2(0.94f, 0.96f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text disclosure = CreateText(
                panel.transform,
                "PreviewDisclosure",
                _copy.Disclosure,
                24,
                46f,
                TextAnchor.MiddleCenter);
            disclosure.color = new Color(1f, 0.76f, 0.34f, 1f);

            Text title = CreateText(
                panel.transform,
                "IdentityTitle",
                _copy.Title,
                42,
                68f,
                TextAnchor.MiddleCenter);
            title.color = new Color(0.96f, 0.90f, 0.76f, 1f);

            _realmStep = CreateStage(panel.transform, "RealmStep");
            BuildRealmStep(_realmStep.transform);

            _classStep = CreateStage(panel.transform, "ClassFamilyStep");
            BuildClassStep(_classStep.transform);

            _customizationReadyStep = CreateStage(
                panel.transform,
                "CustomizationReadyStep");
            BuildCustomizationReadyStep(_customizationReadyStep.transform);
        }

        private void BuildRealmStep(Transform parent)
        {
            CreateHeading(parent, "RealmHeading", _copy.RealmHeading);
            CreateInstruction(parent, "RealmInstruction", _copy.RealmInstruction);

            for (int i = 0; i < RealmChoices.Length; i++)
            {
                RealmId realm = RealmChoices[i];
                string label = ResolveRealmLabel(realm);
                Button button = CreateChoiceButton(
                    parent,
                    "RealmChoice" + i,
                    label,
                    out Text buttonLabel);
                RealmId capturedRealm = realm;
                button.onClick.AddListener(() => PreviewRealm(capturedRealm));
                _realmButtons.Add(realm, button);
                _realmButtonLabels.Add(realm, buttonLabel);
            }

            _realmSummary = CreateText(
                parent,
                "RealmPreviewSummary",
                _copy.RealmSelectionRequired,
                25,
                92f,
                TextAnchor.MiddleLeft);
            _realmSummary.color = new Color(0.80f, 0.86f, 0.94f, 1f);

            _confirmRealmButton = CreatePrimaryButton(
                parent,
                "ConfirmRealmPreview",
                _copy.ConfirmRealmAction);
            _confirmRealmButton.onClick.AddListener(ConfirmRealmPreview);
        }

        private void BuildClassStep(Transform parent)
        {
            CreateHeading(parent, "ClassHeading", _copy.ClassHeading);
            _classRealmSummary = CreateText(
                parent,
                "ClassRealmSummary",
                string.Empty,
                24,
                82f,
                TextAnchor.MiddleLeft);
            _classRealmSummary.color = new Color(0.80f, 0.86f, 0.94f, 1f);
            CreateInstruction(parent, "ClassInstruction", _copy.ClassInstruction);

            for (int i = 0; i < ClassChoices.Length; i++)
            {
                ClassFamily classFamily = ClassChoices[i];
                string label = ResolveClassFamilyLabel(classFamily);
                Button button = CreateChoiceButton(
                    parent,
                    "ClassFamilyChoice" + i,
                    label,
                    out Text buttonLabel);
                ClassFamily capturedClassFamily = classFamily;
                button.onClick.AddListener(() => PreviewClassFamily(capturedClassFamily));
                _classButtons.Add(classFamily, button);
                _classButtonLabels.Add(classFamily, buttonLabel);
            }

            _classSummary = CreateText(
                parent,
                "ClassPreviewSummary",
                _copy.ClassSelectionRequired,
                25,
                62f,
                TextAnchor.MiddleLeft);
            _classSummary.color = new Color(0.80f, 0.86f, 0.94f, 1f);

            GameObject actions = CreateHorizontalActions(parent, "ClassActions");
            _returnToRealmButton = CreateSecondaryButton(
                actions.transform,
                "ReturnToRealmPreview",
                _copy.ReturnToRealmAction);
            _returnToRealmButton.onClick.AddListener(ReturnToRealmPreview);

            _confirmDraftButton = CreatePrimaryButton(
                actions.transform,
                "ConfirmDraftForCustomization",
                _copy.ConfirmDraftAction);
            _confirmDraftButton.onClick.AddListener(ConfirmDraftForCustomization);
        }

        private void BuildCustomizationReadyStep(Transform parent)
        {
            CreateHeading(
                parent,
                "CustomizationReadyHeading",
                _copy.CustomizationReadyHeading);
            _customizationReadySummary = CreateText(
                parent,
                "CustomizationReadySummary",
                string.Empty,
                28,
                180f,
                TextAnchor.MiddleCenter);
            _customizationReadySummary.color = new Color(0.86f, 0.92f, 1f, 1f);
        }

        private void PreviewRealm(RealmId realm)
        {
            Apply(_flow.PreviewRealm(realm));
        }

        private void ConfirmRealmPreview()
        {
            FirstUserIdentityDraftTransitionResult result =
                _flow.ConfirmRealmPreview();
            Apply(result);
            if (result.WasApplied)
            {
                Focus(GetClassFamilyChoiceButton(ClassFamily.Warrior));
            }
        }

        private void PreviewClassFamily(ClassFamily classFamily)
        {
            Apply(_flow.PreviewClassFamily(classFamily));
        }

        private void ReturnToRealmPreview()
        {
            FirstUserIdentityDraftTransitionResult result =
                _flow.ReturnToRealmPreview();
            Apply(result);
            if (result.WasApplied)
            {
                Focus(GetRealmChoiceButton(result.Snapshot.Realm));
            }
        }

        private void ConfirmDraftForCustomization()
        {
            FirstUserIdentityDraftTransitionResult result =
                _flow.ConfirmDraftForCustomization();
            Apply(result);
            if (!result.WasApplied)
            {
                return;
            }

            PersistDraftIdentity(result.Snapshot);
            ClearOwnedFocus();
            CustomizationReady?.Invoke(result.Snapshot);
        }

        private static void PersistDraftIdentity(FirstUserIdentityDraftSnapshot snapshot)
        {
            if (snapshot == null ||
                !snapshot.HasRealm ||
                !snapshot.HasClassFamily ||
                !ServiceLocator.TryGet(out ISaveGameService saveGameService))
            {
                return;
            }

            MvpLoopSaveAuthority.TryCommit(
                saveGameService,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    snapshot.Realm,
                    snapshot.ClassFamily.Value,
                    false,
                    string.Empty,
                    string.Empty,
                    0));
        }

        private void Apply(FirstUserIdentityDraftTransitionResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            RefreshView();
        }

        private void RefreshView()
        {
            FirstUserIdentityDraftSnapshot snapshot = _flow.Snapshot;
            _realmStep.SetActive(snapshot.Step == FirstUserIdentityDraftStep.Realm);
            _classStep.SetActive(snapshot.Step == FirstUserIdentityDraftStep.ClassFamily);
            _customizationReadyStep.SetActive(
                snapshot.Step == FirstUserIdentityDraftStep.CustomizationReady);

            RefreshRealmButtons(snapshot);
            RefreshClassButtons(snapshot);

            _confirmRealmButton.interactable = snapshot.HasRealm;
            _confirmDraftButton.interactable = snapshot.HasClassFamily;

            if (snapshot.HasRealm)
            {
                string realmLabel = ResolveRealmLabel(snapshot.Realm);
                string raceLabel = ResolveRaceLabel(snapshot.Race);
                string realmSummary = _copy.RealmAndRaceSummary(realmLabel, raceLabel);
                _realmSummary.text = realmSummary;
                _classRealmSummary.text = realmSummary;
            }
            else
            {
                _realmSummary.text = _copy.RealmSelectionRequired;
                _classRealmSummary.text = string.Empty;
            }

            _classSummary.text = snapshot.HasClassFamily
                ? _copy.ClassSummary(ResolveClassFamilyLabel(snapshot.ClassFamily.Value))
                : _copy.ClassSelectionRequired;

            if (snapshot.IsCustomizationReady)
            {
                _customizationReadySummary.text = _copy.CustomizationReadySummary(
                    ResolveRealmLabel(snapshot.Realm),
                    ResolveRaceLabel(snapshot.Race),
                    ResolveClassFamilyLabel(snapshot.ClassFamily.Value));
            }
            else
            {
                _customizationReadySummary.text = string.Empty;
            }
        }

        private void RefreshRealmButtons(FirstUserIdentityDraftSnapshot snapshot)
        {
            for (int i = 0; i < RealmChoices.Length; i++)
            {
                RealmId realm = RealmChoices[i];
                bool selected = snapshot.HasRealm && snapshot.Realm == realm;
                string label = ResolveRealmLabel(realm);
                _realmButtonLabels[realm].text = selected
                    ? _copy.SelectedChoice(label)
                    : label;
                ApplyChoiceColors(_realmButtons[realm], selected);
            }
        }

        private void RefreshClassButtons(FirstUserIdentityDraftSnapshot snapshot)
        {
            for (int i = 0; i < ClassChoices.Length; i++)
            {
                ClassFamily classFamily = ClassChoices[i];
                bool selected = snapshot.HasClassFamily &&
                                snapshot.ClassFamily.Value == classFamily;
                string label = ResolveClassFamilyLabel(classFamily);
                _classButtonLabels[classFamily].text = selected
                    ? _copy.SelectedChoice(label)
                    : label;
                ApplyChoiceColors(_classButtons[classFamily], selected);
            }
        }

        private static void ApplyChoiceColors(Button button, bool selected)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = selected
                ? new Color(0.38f, 0.29f, 0.12f, 1f)
                : new Color(0.09f, 0.12f, 0.17f, 1f);
            colors.highlightedColor = selected
                ? new Color(0.48f, 0.36f, 0.15f, 1f)
                : new Color(0.15f, 0.20f, 0.28f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            colors.disabledColor = new Color(0.05f, 0.06f, 0.08f, 0.72f);
            button.colors = colors;
        }

        private string ResolveRealmLabel(RealmId realm)
        {
            if (!_copy.TryGetRealmLabel(realm, out string label) ||
                string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidOperationException("Preview realm copy is incomplete.");
            }

            return label;
        }

        private string ResolveRaceLabel(FirstUserRace race)
        {
            if (!_copy.TryGetRaceLabel(race, out string label) ||
                string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidOperationException("Preview race copy is incomplete.");
            }

            return label;
        }

        private string ResolveClassFamilyLabel(ClassFamily classFamily)
        {
            if (!_copy.TryGetClassFamilyLabel(classFamily, out string label) ||
                string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidOperationException("Preview class copy is incomplete.");
            }

            return label;
        }

        private static void ValidateCopyProvider(
            IFirstUserIdentityDraftCopyProvider copyProvider)
        {
            RequireText(copyProvider.Disclosure);
            RequireText(copyProvider.Title);
            RequireText(copyProvider.RealmHeading);
            RequireText(copyProvider.RealmInstruction);
            RequireText(copyProvider.RealmSelectionRequired);
            RequireText(copyProvider.ConfirmRealmAction);
            RequireText(copyProvider.ClassHeading);
            RequireText(copyProvider.ClassInstruction);
            RequireText(copyProvider.ClassSelectionRequired);
            RequireText(copyProvider.ReturnToRealmAction);
            RequireText(copyProvider.ConfirmDraftAction);
            RequireText(copyProvider.CustomizationReadyHeading);

            for (int i = 0; i < RealmChoices.Length; i++)
            {
                if (!copyProvider.TryGetRealmLabel(RealmChoices[i], out string label))
                {
                    throw new InvalidOperationException("Preview realm copy is incomplete.");
                }

                RequireText(label);
            }

            FirstUserRace[] races =
            {
                FirstUserRace.Humans,
                FirstUserRace.Dwarves,
                FirstUserRace.Elves,
                FirstUserRace.DarkElves
            };
            for (int i = 0; i < races.Length; i++)
            {
                if (!copyProvider.TryGetRaceLabel(races[i], out string label))
                {
                    throw new InvalidOperationException("Preview race copy is incomplete.");
                }

                RequireText(label);
            }

            for (int i = 0; i < ClassChoices.Length; i++)
            {
                if (!copyProvider.TryGetClassFamilyLabel(
                        ClassChoices[i],
                        out string label))
                {
                    throw new InvalidOperationException("Preview class copy is incomplete.");
                }

                RequireText(label);
            }
        }

        private static void RequireText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Visible preview copy is incomplete.");
            }
        }

        private GameObject CreatePanel(Transform parent, string objectName)
        {
            var panel = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = new Color(0.035f, 0.045f, 0.062f, 0.98f);
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.82f, 0.64f, 0.31f, 0.55f);
            outline.effectDistance = new Vector2(2f, -2f);
            return panel;
        }

        private static GameObject CreateStage(Transform parent, string objectName)
        {
            var stage = new GameObject(objectName, typeof(RectTransform));
            stage.transform.SetParent(parent, false);
            var layout = stage.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var layoutElement = stage.AddComponent<LayoutElement>();
            layoutElement.flexibleHeight = 1f;
            return stage;
        }

        private void CreateHeading(Transform parent, string objectName, string value)
        {
            Text heading = CreateText(
                parent,
                objectName,
                value,
                34,
                58f,
                TextAnchor.MiddleLeft);
            heading.color = new Color(0.98f, 0.84f, 0.54f, 1f);
        }

        private void CreateInstruction(
            Transform parent,
            string objectName,
            string value)
        {
            Text instruction = CreateText(
                parent,
                objectName,
                value,
                24,
                62f,
                TextAnchor.MiddleLeft);
            instruction.color = new Color(0.78f, 0.84f, 0.92f, 1f);
        }

        private Text CreateText(
            Transform parent,
            string objectName,
            string value,
            int fontSize,
            float preferredHeight,
            TextAnchor alignment,
            bool participatesInLayout = true)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            if (participatesInLayout)
            {
                textObject.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            }

            return text;
        }

        private Button CreateChoiceButton(
            Transform parent,
            string objectName,
            string value,
            out Text label)
        {
            Button button = CreateButton(parent, objectName, value, 64f, out label);
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.81f, 0.90f, 0.42f);
            outline.effectDistance = new Vector2(1f, -1f);
            ApplyChoiceColors(button, false);
            return button;
        }

        private Button CreatePrimaryButton(
            Transform parent,
            string objectName,
            string value)
        {
            Button button = CreateButton(parent, objectName, value, 70f, out _);
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.52f, 0.35f, 0.10f, 1f);
            colors.highlightedColor = new Color(0.68f, 0.48f, 0.16f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.34f, 0.23f, 0.08f, 1f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.68f);
            button.colors = colors;
            return button;
        }

        private Button CreateSecondaryButton(
            Transform parent,
            string objectName,
            string value)
        {
            Button button = CreateButton(parent, objectName, value, 70f, out _);
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.10f, 0.14f, 0.20f, 1f);
            colors.highlightedColor = new Color(0.17f, 0.23f, 0.32f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            button.colors = colors;
            return button;
        }

        private Button CreateButton(
            Transform parent,
            string objectName,
            string value,
            float preferredHeight,
            out Text label)
        {
            var buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<LayoutElement>().preferredHeight = preferredHeight;

            Image image = buttonObject.GetComponent<Image>();
            image.color = Color.white;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            label = CreateText(
                buttonObject.transform,
                "VisibleLabel",
                value,
                25,
                preferredHeight,
                TextAnchor.MiddleCenter,
                participatesInLayout: false);
            RectTransform labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(18f, 4f);
            labelRect.offsetMax = new Vector2(-18f, -4f);
            return button;
        }

        private static GameObject CreateHorizontalActions(
            Transform parent,
            string objectName)
        {
            var actions = new GameObject(objectName, typeof(RectTransform));
            actions.transform.SetParent(parent, false);
            var layout = actions.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            actions.AddComponent<LayoutElement>().preferredHeight = 70f;
            return actions;
        }

        private void Focus(Button button)
        {
            if (button != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(button.gameObject);
            }
        }

        private void ClearOwnedFocus()
        {
            if (EventSystem.current == null ||
                EventSystem.current.currentSelectedGameObject == null)
            {
                return;
            }

            Transform selected = EventSystem.current.currentSelectedGameObject.transform;
            if (selected == transform || selected.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
