using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.UI.RealmSelection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// First-session create after realm commit. People stay locked to the sworn realm.
    /// Visible loadouts are that realm only. Heraldry is structural, not a hue swatch.
    /// Remaining champion cards are labelled TEMPORARY.
    /// </summary>
    public class CharacterCreationController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private string _combatSceneName = "ChampionArena";

        private readonly Color _panel = new Color(0.030f, 0.032f, 0.034f, 0.96f);
        private readonly Color _textDim = new Color(0.78f, 0.76f, 0.70f, 1f);

        private readonly List<ChampionDefinition> _champions = new List<ChampionDefinition>();
        private CharacterCreationPresentationPlan _plan;
        private ChampionDefinition _selected;
        private Text _statusText;
        private Text _nvs01Text;
        private Text _detailText;
        private InputField _usernameField;
        private Button _confirmButton;
        private Text _confirmLabel;
        private bool _committing;
        private bool _alreadyConfirmed;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            EnsureSaveLoaded();
            EnsureEventSystem();
            LoadChampions();
            BuildUi();
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

            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            SaveGameData current = save?.CurrentSave;
            RealmId committedRealm = current != null ? current.SelectedRealm : RealmId.None;

            var catalog = new List<ChampionDefinition>();
            IGameDataService data = ServiceLocator.Get<IGameDataService>();
            if (data != null)
            {
                foreach (ChampionDefinition champion in data.GetAllChampions())
                {
                    if (champion != null)
                    {
                        catalog.Add(champion);
                    }
                }
            }

            _plan = CharacterCreationPresentation.Build(
                committedRealm,
                catalog,
                RealmCatalogRuntime.Current);

            var allowed = new HashSet<string>(_plan.VisibleChampionIds, StringComparer.Ordinal);
            foreach (ChampionDefinition champion in catalog)
            {
                if (champion != null && allowed.Contains(champion.Id))
                {
                    _champions.Add(champion);
                }
            }

            if (SliceRunState.HasConfirmedChampion)
            {
                _alreadyConfirmed = true;
                _selected = _champions.Find(champion => champion.Id == SliceRunState.Champion.Id)
                    ?? (_champions.Count > 0 ? _champions[0] : null);
            }
            else
            {
                _alreadyConfirmed = false;
                _selected = _champions.Count > 0 ? _champions[0] : null;
            }
        }

        private void BuildUi()
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

            CreatePanel(canvasObject.transform, "Background", new Color(0.014f, 0.018f, 0.025f, 1f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Font font = RealmSelectionIdentity.ResolvePresentationFont();

            CreatePanel(canvasObject.transform, "TopRule", new Color(0.78f, 0.76f, 0.70f, 0.72f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-96f, 3f));

            var title = CreateText(canvasObject.transform, "Title", font, _plan.Title, 34,
                new Vector2(64f, -26f), new Vector2(980f, 44f));
            title.color = new Color(0.94f, 0.92f, 0.86f);

            var markHost = new GameObject("CommittedRealmMark");
            markHost.transform.SetParent(canvasObject.transform, false);
            var markRect = markHost.AddComponent<RectTransform>();
            markRect.anchorMin = new Vector2(1f, 1f);
            markRect.anchorMax = new Vector2(1f, 1f);
            markRect.pivot = new Vector2(1f, 1f);
            markRect.anchoredPosition = new Vector2(-72f, -28f);
            markRect.sizeDelta = new Vector2(168f, 168f);
            if (_plan.HasStructuralIdentity)
            {
                RealmSelectionIdentity.BuildStructuralFrame(markHost.transform, _plan.Identity.FrameKind);
                Sprite emblem = CharacterCreationPresentation.TryLoadEmblem(_plan.Realm);
                if (emblem != null)
                {
                    var emblemImage = CreatePanel(
                        markHost.transform,
                        "RealmEmblem",
                        Color.white,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        Vector2.zero,
                        new Vector2(96f, 96f));
                    emblemImage.sprite = emblem;
                    emblemImage.preserveAspect = true;
                }
            }

            _nvs01Text = CreateText(canvasObject.transform, "RealmIdentity", font,
                string.IsNullOrEmpty(_plan.PeopleCopy) ? _plan.BindRealmError : _plan.PeopleCopy,
                17, new Vector2(64f, -78f), new Vector2(1200f, 26f));
            _nvs01Text.color = new Color(0.78f, 0.76f, 0.70f);

            var heraldry = CreateText(canvasObject.transform, "Heraldry", font, _plan.HeraldryCopy, 15,
                new Vector2(64f, -106f), new Vector2(1200f, 22f));
            heraldry.color = new Color(0.70f, 0.70f, 0.68f);

            var temporary = CreateText(canvasObject.transform, "TemporaryBadge", font, _plan.TemporaryBadge, 14,
                new Vector2(64f, -132f), new Vector2(1200f, 20f));
            temporary.color = new Color(0.86f, 0.80f, 0.62f);

            var cardY = -168f;
            foreach (ChampionDefinition champion in _champions)
            {
                BuildChampionCard(canvasObject.transform, font, champion, cardY);
                cardY -= 132f;
            }

            // Detail panel.
            _detailText = CreateText(canvasObject.transform, "Detail", font, string.Empty, 19,
                new Vector2(980f, -128f), new Vector2(880f, 560f));
            _detailText.color = _textDim;
            _detailText.alignment = TextAnchor.UpperLeft;

            // Confirm action + local username (uniqueness is in-process only).
            _usernameField = CreateUsernameField(canvasObject.transform, font, new Vector2(64f, -780f), new Vector2(360f, 48f));
            if (_alreadyConfirmed && !string.IsNullOrWhiteSpace(SliceRunState.Champion.Username))
            {
                _usernameField.text = SliceRunState.Champion.Username;
            }

            _confirmButton = CreateButton(canvasObject.transform, "ConfirmChampion", "SWEAR THIS NAME", font,
                new Vector2(64f, -852f), new Vector2(360f, 54f), ConfirmChampion);
            _confirmLabel = _confirmButton.GetComponentInChildren<Text>();

            _statusText = CreateText(canvasObject.transform, "Status", font,
                string.IsNullOrEmpty(_plan.BindRealmError)
                    ? "Name this champion, then enter the inner realm."
                    : _plan.BindRealmError,
                16, new Vector2(64f, -920f), new Vector2(1200f, 28f));
            _statusText.color = _textDim;

            RefreshSelection();
        }

        private void BuildChampionCard(Transform parent, Font font, ChampionDefinition champion, float y)
        {
            var cardObject = new GameObject("Card_" + champion.Id);
            cardObject.transform.SetParent(parent, false);

            var image = cardObject.AddComponent<Image>();
            image.color = _panel;

            var rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(64f, y);
            rect.sizeDelta = new Vector2(820f, 112f);

            var mark = new GameObject("StructuralMark");
            mark.transform.SetParent(cardObject.transform, false);
            var markRect = mark.AddComponent<RectTransform>();
            markRect.anchorMin = new Vector2(0f, 0.5f);
            markRect.anchorMax = new Vector2(0f, 0.5f);
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = Vector2.zero;
            markRect.sizeDelta = new Vector2(112f, 112f);
            RealmSelectionIdentity.BuildStructuralFrame(mark.transform, _plan.Identity.FrameKind);
            Sprite emblem = CharacterCreationPresentation.TryLoadEmblem(champion.Realm);
            if (emblem != null)
            {
                var emblemImage = CreatePanel(
                    mark.transform,
                    "RealmEmblem",
                    Color.white,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(72f, 72f));
                emblemImage.sprite = emblem;
                emblemImage.preserveAspect = true;
            }

            var nameText = CreateText(cardObject.transform, "Name", font, champion.DisplayName, 22,
                new Vector2(136f, -16f), new Vector2(620f, 28f));
            nameText.alignment = TextAnchor.UpperLeft;
            nameText.color = new Color(0.94f, 0.92f, 0.86f);

            var classText = CreateText(cardObject.transform, "Class", font,
                champion.Family + "  ·  " + _plan.Identity.PeopleName + "  ·  " + _plan.Identity.MarkName, 15,
                new Vector2(136f, -48f), new Vector2(620f, 22f));
            classText.alignment = TextAnchor.UpperLeft;
            classText.color = _textDim;

            var statsText = CreateText(cardObject.transform, "Stats", font, _plan.TemporaryBadge,
                14, new Vector2(136f, -74f), new Vector2(660f, 20f));
            statsText.alignment = TextAnchor.UpperLeft;
            statsText.color = new Color(0.70f, 0.70f, 0.68f);

            var button = cardObject.AddComponent<Button>();
            var captured = champion;
            button.onClick.AddListener(() => SelectChampion(captured));
            var colors = button.colors;
            colors.highlightedColor = new Color(0.10f, 0.10f, 0.11f, 1f);
            colors.pressedColor = new Color(0.06f, 0.06f, 0.07f, 1f);
            button.colors = colors;
        }

        private void SelectChampion(ChampionDefinition champion)
        {
            if (_committing || champion == null)
            {
                return;
            }

            _selected = champion;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            if (_detailText != null)
            {
                _detailText.text = _selected == null
                    ? "No champion archetype available."
                    : BuildDetail(_selected);
            }

            if (_confirmButton != null)
            {
                _confirmButton.interactable = _selected != null && !_committing;
            }

            if (_confirmLabel != null)
            {
                _confirmLabel.text = _alreadyConfirmed ? "ENTER THE INNER REALM" : "SWEAR THIS NAME";
            }
        }

        private string BuildDetail(ChampionDefinition champion)
        {
            var skills = new System.Text.StringBuilder();
            if (champion.BaseSkills != null)
            {
                foreach (SkillDefinition skill in champion.BaseSkills)
                {
                    if (skill == null) continue;
                    skills.Append("  • ").Append(skill.DisplayName).Append('\n');
                }
            }

            return
                $"{champion.DisplayName.ToUpperInvariant()}\n\n" +
                $"{champion.Family} / {champion.Subclass}\n" +
                $"Home realm: {champion.Realm}\n\n" +
                "BASE STATS\n" +
                $"  Health  {champion.BaseStats.MaxHealth}\n" +
                $"  Mana    {champion.BaseStats.MaxMana}\n" +
                $"  Attack  {champion.BaseStats.Attack}\n" +
                $"  Defense {champion.BaseStats.Defense}\n" +
                $"  Speed   {champion.BaseStats.Speed}\n" +
                $"  Crit    {champion.BaseStats.CritRate}%\n\n" +
                "LOADOUT\n" +
                $"  {champion.WeaponStyleId} / {champion.OffhandStyleId}\n" +
                "SKILLS\n" + (skills.Length > 0 ? skills.ToString() : "  (none)");
        }

        private void ConfirmChampion()
        {
            if (_committing || _selected == null)
            {
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

            ChampionState state = BuildChampionState(_selected);
            state.Username = username;
            SliceRunState.ConfirmChampion(state);

            Debug.Log(
                $"[AL-CHARACTER-CREATION] Champion confirmed: id={state.Id} name={state.DisplayName} " +
                $"username={state.Username} family={state.Family} subclass={state.Subclass} realm={state.Realm} " +
                $"hp={state.MaxHealth} atk={state.Attack} def={state.Defense} skills={state.SkillIds.Count}");
            SetStatus($"Champion confirmed — {state.DisplayName} as {state.Username}. Advancing to the inner realm...");
            AdvanceToCombat();
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
                    $"[AL-CHARACTER-CREATION] Could not load combat scene '{_combatSceneName}': {ex.Message}. " +
                    "Add the scene to Build Settings or rewire _combatSceneName.");
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

            var labelText = CreateText(buttonObject.transform, "Label", font, label, 18, Vector2.zero, sizeDelta);
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
