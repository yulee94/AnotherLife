using System;
using System.Collections.Generic;
using AL.ChampionMode.Customization;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Character creator after realm commit: class family (realm-gated), appearance rack,
    /// username, and an adult procedural preview. Confirmed look + class persist through
    /// <see cref="SaveGameData.ChampionCustomization"/> — no new top-level save fields.
    /// </summary>
    public class CharacterCreationController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private string _combatSceneName = "ChampionArena";

        private readonly Color _panel = new Color(0.030f, 0.039f, 0.052f, 0.92f);
        private readonly Color _textDim = new Color(0.84f, 0.88f, 0.92f, 1f);
        private readonly List<ChampionDefinition> _champions = new List<ChampionDefinition>();
        private readonly Dictionary<ClassFamily, Image> _classCards = new Dictionary<ClassFamily, Image>();

        private CharacterCreationDraft _draft;
        private Text _statusText;
        private Text _peopleText;
        private Text _lookText;
        private InputField _usernameField;
        private Button _confirmButton;
        private Text _confirmLabel;
        private ChampionCustomizationController _preview;
        private bool _committing;
        private bool _alreadyConfirmed;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            EnsureSaveLoaded();
            EnsureEventSystem();
            LoadChampions();
            if (!TryBuildDraft())
            {
                return;
            }

            BuildPreview();
            BuildUi();
            RefreshPreview();
        }

        private void EnsureSaveLoaded()
        {
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            if (save != null && save.CurrentSave == null)
            {
                save.Load();
            }
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void LoadChampions()
        {
            _champions.Clear();
            IGameDataService data = ServiceLocator.Get<IGameDataService>();
            if (data == null)
            {
                return;
            }

            foreach (ChampionDefinition champion in data.GetAllChampions())
            {
                if (champion != null)
                {
                    _champions.Add(champion);
                }
            }
        }

        private bool TryBuildDraft()
        {
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            SaveGameData current = save?.CurrentSave;
            RealmId realm = current != null ? current.SelectedRealm : RealmId.None;
            if (!CharacterCreationDraft.TryCreate(realm, out _draft, out string error))
            {
                BuildErrorUi(error);
                return false;
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(current);
            _alreadyConfirmed = snapshot.HasConfirmedChampion || SliceRunState.HasConfirmedChampion;
            if (snapshot.ClassFamily.HasValue)
            {
                _draft.TrySelectClassFamily(snapshot.ClassFamily.Value, out _);
            }

            if (current?.ChampionCustomization != null && snapshot.HasConfirmedChampion)
            {
                CharacterCreationLook.CopyInto(_draft.Customization, current.ChampionCustomization);
            }

            return true;
        }

        private void BuildPreview()
        {
            if (FindObjectOfType<Light>() == null)
            {
                var lightObject = new GameObject("CreatorKeyLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.96f, 0.90f);
                lightObject.transform.rotation = Quaternion.Euler(28f, 140f, 0f);
            }

            var previewObject = new GameObject("CreatorPreview");
            previewObject.transform.position = new Vector3(0.85f, 0f, 3.4f);
            previewObject.transform.rotation = Quaternion.Euler(0f, 168f, 0f);
            _preview = previewObject.AddComponent<ChampionCustomizationController>();
            _preview.ApplyPresentation(_draft.Customization);

            var cameraObject = new GameObject("CreatorPreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f, 1f);
            camera.fieldOfView = 28f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 24f;
            camera.depth = 8f;
            camera.rect = new Rect(0.46f, 0.04f, 0.52f, 0.92f);
            cameraObject.transform.position = new Vector3(0.85f, 1.05f, 1.05f);
            cameraObject.transform.LookAt(new Vector3(0.85f, 0.72f, 3.4f));
        }

        private void BuildErrorUi(string error)
        {
            var canvasObject = CreateCanvas();
            Font font = ResolveFont();
            var title = CreateText(canvasObject.transform, "Title", font, "CHARACTER CREATION", 32,
                new Vector2(64f, -28f), new Vector2(900f, 40f));
            title.color = new Color(1f, 0.88f, 0.62f);
            _statusText = CreateText(canvasObject.transform, "Status", font, error, 18,
                new Vector2(64f, -90f), new Vector2(1100f, 48f));
            _statusText.color = new Color(0.94f, 0.42f, 0.38f);
        }

        private void BuildUi()
        {
            var canvasObject = CreateCanvas();
            Font font = ResolveFont();

            CreatePanel(canvasObject.transform, "LeftVeil", new Color(0.012f, 0.016f, 0.024f, 0.88f),
                new Vector2(0f, 0f), new Vector2(0.46f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);

            var title = CreateText(canvasObject.transform, "Title", font, "SHAPE YOUR CHAMPION", 30,
                new Vector2(48f, -24f), new Vector2(780f, 40f));
            title.color = new Color(1f, 0.88f, 0.62f);

            CharacterCreationLook.TryRealmLabel(_draft.Realm, out string realmLabel);
            CharacterCreationLook.TryPeopleLabel(_draft.Realm, out string peopleLabel);
            _peopleText = CreateText(canvasObject.transform, "People", font,
                realmLabel + "  ·  " + peopleLabel + "  —  people are locked to this realm.",
                16, new Vector2(48f, -70f), new Vector2(800f, 28f));
            _peopleText.color = new Color(0.62f, 0.82f, 0.94f);

            var classHeading = CreateText(canvasObject.transform, "ClassHeading", font, "CLASS PATH", 14,
                new Vector2(48f, -108f), new Vector2(400f, 22f));
            classHeading.color = _accentDim();

            float classX = 48f;
            foreach (ClassFamily family in _draft.AvailableFamilies)
            {
                CharacterCreationLook.TryClassLabel(family, out string label);
                Button button = CreateButton(canvasObject.transform, "Class_" + family, label, font,
                    new Vector2(classX, -138f), new Vector2(176f, 44f), () => SelectClass(family));
                _classCards[family] = button.GetComponent<Image>();
                classX += 188f;
            }

            var lookHeading = CreateText(canvasObject.transform, "LookHeading", font, "APPEARANCE", 14,
                new Vector2(48f, -200f), new Vector2(400f, 22f));
            lookHeading.color = _accentDim();

            CreateButton(canvasObject.transform, "ArmorTint", "ARMOR TINT", font,
                new Vector2(48f, -232f), new Vector2(176f, 40f), () => MutateLook(_draft.CycleArmorTint));
            CreateButton(canvasObject.transform, "BodyTint", "BODY TINT", font,
                new Vector2(236f, -232f), new Vector2(176f, 40f), () => MutateLook(_draft.CycleBodyTint));
            CreateButton(canvasObject.transform, "HairStyle", "HAIR", font,
                new Vector2(424f, -232f), new Vector2(176f, 40f), () => MutateLook(_draft.CycleHairStyle));
            CreateButton(canvasObject.transform, "HairColor", "HAIR COLOR", font,
                new Vector2(48f, -282f), new Vector2(176f, 40f), () => MutateLook(_draft.CycleHairColor));
            CreateButton(canvasObject.transform, "BodyPreset", "BODY", font,
                new Vector2(236f, -282f), new Vector2(176f, 40f), () => MutateLook(_draft.CycleBodyPreset));
            CreateButton(canvasObject.transform, "Helmet", "HELMET", font,
                new Vector2(424f, -282f), new Vector2(176f, 40f), () => MutateLook(_draft.ToggleHelmet));
            CreateButton(canvasObject.transform, "Cape", "CAPE", font,
                new Vector2(48f, -332f), new Vector2(176f, 40f), () => MutateLook(_draft.ToggleCape));

            _lookText = CreateText(canvasObject.transform, "LookSummary", font, string.Empty, 16,
                new Vector2(48f, -390f), new Vector2(780f, 72f));
            _lookText.color = _textDim;

            var nameHeading = CreateText(canvasObject.transform, "NameHeading", font, "USERNAME", 14,
                new Vector2(48f, -470f), new Vector2(400f, 22f));
            nameHeading.color = _accentDim();

            _usernameField = CreateUsernameField(canvasObject.transform, font, new Vector2(48f, -502f), new Vector2(360f, 48f));
            if (_alreadyConfirmed && !string.IsNullOrWhiteSpace(SliceRunState.Champion.Username))
            {
                _usernameField.text = SliceRunState.Champion.Username;
            }

            _confirmButton = CreateButton(canvasObject.transform, "ConfirmChampion", "ENTER THE REALM", font,
                new Vector2(48f, -568f), new Vector2(360f, 54f), ConfirmChampion);
            _confirmLabel = _confirmButton.GetComponentInChildren<Text>();

            _statusText = CreateText(canvasObject.transform, "Status", font, "Choose a class path, then a look.",
                16, new Vector2(48f, -640f), new Vector2(800f, 48f));
            _statusText.color = _textDim;

            RefreshSelection();
        }

        private void SelectClass(ClassFamily family)
        {
            if (_committing)
            {
                return;
            }

            if (!_draft.TrySelectClassFamily(family, out string error))
            {
                SetStatus(error);
                return;
            }

            RefreshSelection();
            RefreshPreview();
        }

        private void MutateLook(Action mutation)
        {
            if (_committing)
            {
                return;
            }

            mutation();
            RefreshSelection();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            _preview?.ApplyPresentation(_draft.Customization);
        }

        private void RefreshSelection()
        {
            foreach (KeyValuePair<ClassFamily, Image> pair in _classCards)
            {
                bool selected = _draft.ClassFamily.HasValue && _draft.ClassFamily.Value == pair.Key;
                pair.Value.color = selected
                    ? new Color(0.28f, 0.22f, 0.12f, 0.96f)
                    : new Color(0.12f, 0.22f, 0.32f, 0.92f);
            }

            if (_lookText != null)
            {
                ChampionCustomizationState look = _draft.Customization;
                _lookText.text =
                    "Hair " + look.HairStyleId +
                    "  ·  body " + look.BodyPresetId +
                    "  ·  armor " + look.ArmorStyleId +
                    "  ·  helm " + (look.HelmetEnabled ? "on" : "off") +
                    "  ·  cape " + (look.CapeEnabled ? "on" : "off");
            }

            if (_confirmButton != null)
            {
                _confirmButton.interactable = _draft.ClassFamily.HasValue && !_committing;
            }

            if (_confirmLabel != null)
            {
                _confirmLabel.text = _alreadyConfirmed ? "CONTINUE TO ARENA" : "ENTER THE REALM";
            }
        }

        private void ConfirmChampion()
        {
            if (_committing || _draft == null || !_draft.ClassFamily.HasValue)
            {
                SetStatus("Choose a class path before entering the realm.");
                return;
            }

            _committing = true;

            string alreadyOwned = _alreadyConfirmed ? SliceRunState.Champion.Username : string.Empty;
            if (!CharacterCreationIdentity.TryClaim(
                    _usernameField != null ? _usernameField.text : string.Empty,
                    alreadyOwned,
                    out string username,
                    out string usernameError))
            {
                _committing = false;
                SetStatus(usernameError);
                return;
            }

            ClassFamily family = _draft.ClassFamily.Value;
            ChampionDefinition bound = CharacterCreationDraft.BindChampion(_champions, _draft.Realm, family);
            ChampionState state = bound != null
                ? BuildChampionState(bound)
                : new ChampionState { Id = "champion_unbound", DisplayName = "Champion", Realm = _draft.Realm };
            state.Family = family;
            state.Realm = _draft.Realm;
            state.Username = username;
            SliceRunState.ConfirmChampion(state);

            PersistLookAndClass(family);

            SetStatus("Champion confirmed — " + username + " on the " +
                      (CharacterCreationLook.TryClassLabel(family, out string classLabel) ? classLabel : "chosen path") +
                      ". Advancing to the inner realm...");
            AdvanceToCombat();
        }

        private void PersistLookAndClass(ClassFamily family)
        {
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            SaveGameData current = save?.CurrentSave;
            if (save == null || current == null)
            {
                return;
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(current);
            MvpLoopCommitResult commit = MvpLoopSaveAuthority.TryCommit(
                save,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    _draft.Realm,
                    family,
                    true,
                    snapshot.LastResultId,
                    snapshot.LastBuildId,
                    snapshot.LastBuildLevel,
                    _draft.Customization));
            if (commit == null || !commit.Accepted)
            {
                Debug.LogWarning("[AL-CHARACTER-CREATION] Look persist declined: " + (commit != null ? commit.Message : "null"));
            }
        }

        private void AdvanceToCombat()
        {
            if (string.IsNullOrWhiteSpace(_combatSceneName))
            {
                Debug.LogError("[AL-CHARACTER-CREATION] No combat scene configured on _combatSceneName.");
                _committing = false;
                SetStatus("Combat scene not configured.");
                return;
            }

            try
            {
                SceneManager.LoadScene(_combatSceneName);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AL-CHARACTER-CREATION] Could not load combat scene '" + _combatSceneName + "': " + ex.Message);
                _committing = false;
                SetStatus("Combat scene unavailable — see console.");
            }
        }

        private static ChampionState BuildChampionState(ChampionDefinition champion)
        {
            var state = new ChampionState
            {
                Id = champion.Id,
                DisplayName = champion.DisplayName,
                Family = champion.Family,
                Subclass = champion.Subclass,
                Realm = champion.Realm,
                MaxHealth = champion.BaseStats.MaxHealth,
                MaxMana = champion.BaseStats.MaxMana,
                Attack = champion.BaseStats.Attack,
                Defense = champion.BaseStats.Defense,
                Speed = champion.BaseStats.Speed,
                CritRate = champion.BaseStats.CritRate,
                WeaponStyleId = champion.WeaponStyleId,
                OffhandStyleId = champion.OffhandStyleId
            };

            if (champion.BaseSkills != null)
            {
                foreach (SkillDefinition skill in champion.BaseSkills)
                {
                    if (skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                    {
                        state.SkillIds.Add(skill.Id);
                    }
                }
            }

            return state;
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }

            Debug.Log("[AL-CHARACTER-CREATION] " + message);
        }

        private static Color _accentDim()
        {
            return new Color(0.78f, 0.62f, 0.32f, 1f);
        }

        private static Font ResolveFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static GameObject CreateCanvas()
        {
            var canvasObject = new GameObject("CharacterCreationCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasObject;
        }

        private static Image CreatePanel(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private static Text CreateText(Transform parent, string name, Font font, string content, int fontSize,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = content;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Font font,
            Vector2 anchoredPosition, Vector2 sizeDelta, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.32f, 0.92f);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var labelText = CreateText(buttonObject.transform, "Label", font, label, 16, Vector2.zero, sizeDelta);
            labelText.alignment = TextAnchor.MiddleCenter;
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        private static InputField CreateUsernameField(Transform parent, Font font, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var fieldObject = new GameObject("Username");
            fieldObject.transform.SetParent(parent, false);

            var image = fieldObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);

            var rect = fieldObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = CreateText(fieldObject.transform, "Text", font, string.Empty, 18, Vector2.zero, sizeDelta);
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.supportRichText = false;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            var placeholder = CreateText(fieldObject.transform, "Placeholder", font, "USERNAME", 16, Vector2.zero, sizeDelta);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.color = new Color(0.62f, 0.70f, 0.78f, 0.7f);
            placeholder.fontStyle = FontStyle.Italic;
            var placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 4f);
            placeholderRect.offsetMax = new Vector2(-12f, -4f);

            var field = fieldObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = CharacterCreationIdentity.MaxLength;
            field.contentType = InputField.ContentType.Standard;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }
    }
}
