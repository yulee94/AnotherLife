using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Greybox character creation screen for the legacy runtime vertical slice.
    ///
    /// Surfaces the hardcoded LocalGameDataService champion archetypes, lets the player select and
    /// confirm exactly one champion, writes the resulting <see cref="ChampionState"/> into the local
    /// slice run state (<see cref="SliceRunState"/>), then advances to the combat encounter.
    ///
    /// Deliberately does NOT depend on catalog/save/determinism authority: SaveGameData is
    /// schema-v1 authority-locked (any new top-level field fails semantic validation), so the
    /// slice keeps its cross-scene run state in <see cref="SliceRunState"/>. Persistence of that
    /// state is owned by the save/reload slice task; this screen only mutates the in-memory state.
    /// </summary>
    public class CharacterCreationController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private string _combatSceneName = "ChampionArena";

        private readonly Color _accent = new Color(0.92f, 0.66f, 0.30f, 1f);
        private readonly Color _panel = new Color(0.030f, 0.039f, 0.052f, 0.92f);
        private readonly Color _textDim = new Color(0.84f, 0.88f, 0.92f, 1f);

        private readonly List<ChampionDefinition> _champions = new List<ChampionDefinition>();
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

            IGameDataService data = ServiceLocator.Get<IGameDataService>();
            if (data != null)
            {
                foreach (ChampionDefinition champion in data.GetAllChampions())
                {
                    if (champion != null)
                    {
                        _champions.Add(champion);
                    }
                }
            }

            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            SaveGameData current = save?.CurrentSave;
            if (SliceRunState.HasConfirmedChampion)
            {
                // Exactly one champion already exists: pre-select it and present a re-confirm path.
                _alreadyConfirmed = true;
                _selected = _champions.Find(champion => champion.Id == SliceRunState.Champion.Id);
            }
            else
            {
                _alreadyConfirmed = false;
                RealmId realm = current != null ? current.SelectedRealm : RealmId.None;
                _selected = _champions.Find(champion => champion.Realm == realm)
                    ?? (_champions.Count > 0 ? _champions[0] : null);
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

            var background = CreatePanel(canvasObject.transform, "Background", new Color(0.012f, 0.016f, 0.024f, 1f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var topRule = CreatePanel(canvasObject.transform, "TopRule", new Color(0.88f, 0.62f, 0.24f, 0.55f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-96f, 3f));

            var title = CreateText(canvasObject.transform, "Title", font, "CHOOSE YOUR CHAMPION", 34,
                new Vector2(64f, -26f), new Vector2(720f, 44f));
            title.color = new Color(1f, 0.88f, 0.62f);

            _nvs01Text = CreateText(canvasObject.transform, "Nvs01Bark", font, string.Empty, 17,
                new Vector2(64f, -78f), new Vector2(1100f, 26f));
            _nvs01Text.color = new Color(0.52f, 0.78f, 0.92f);
            _nvs01Text.text = "NVS-01 // Awaiting champion confirmation. Select one vanguard; exactly one will be committed to the run.";

            // Champion cards (placeholder art = realm-accent swatch + name/class/stats).
            var cardY = -128f;
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

            _confirmButton = CreateButton(canvasObject.transform, "ConfirmChampion", "CONFIRM CHAMPION", font,
                new Vector2(64f, -852f), new Vector2(360f, 54f), ConfirmChampion);
            _confirmLabel = _confirmButton.GetComponentInChildren<Text>();

            _statusText = CreateText(canvasObject.transform, "Status", font, "Select a champion to begin.",
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

            // Placeholder portrait art: a realm-accent swatch on the left.
            Color realmColor = RealmAccent(champion.Realm);
            var swatch = CreatePanel(cardObject.transform, "Art", realmColor,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(112f, 112f));
            swatch.raycastTarget = false;

            var nameText = CreateText(cardObject.transform, "Name", font, champion.DisplayName, 22,
                new Vector2(136f, -16f), new Vector2(620f, 28f));
            nameText.alignment = TextAnchor.UpperLeft;
            nameText.color = Color.Lerp(realmColor, Color.white, 0.5f);

            var classText = CreateText(cardObject.transform, "Class", font,
                $"{champion.Family} // {champion.Subclass}  —  {champion.Realm}", 15,
                new Vector2(136f, -48f), new Vector2(620f, 22f));
            classText.alignment = TextAnchor.UpperLeft;
            classText.color = _textDim;

            var statsText = CreateText(cardObject.transform, "Stats", font,
                $"HP {champion.BaseStats.MaxHealth}   ATK {champion.BaseStats.Attack}   DEF {champion.BaseStats.Defense}   SPD {champion.BaseStats.Speed}   CRIT {champion.BaseStats.CritRate}%",
                14, new Vector2(136f, -74f), new Vector2(660f, 20f));
            statsText.alignment = TextAnchor.UpperLeft;
            statsText.color = new Color(0.62f, 0.70f, 0.78f);

            var button = cardObject.AddComponent<Button>();
            var captured = champion;
            button.onClick.AddListener(() => SelectChampion(captured));
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(_panel, realmColor, 0.18f);
            colors.pressedColor = Color.Lerp(_panel, Color.black, 0.25f);
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
                _confirmLabel.text = _alreadyConfirmed ? "CONTINUE TO ARENA" : "CONFIRM CHAMPION";
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

            Debug.Log("[AL-CHARACTER-CREATION] " + message);
        }

        private static Color RealmAccent(RealmId realm)
        {
            switch (realm)
            {
                case RealmId.Stonehold: return new Color(0.80f, 0.52f, 0.24f, 1f);
                case RealmId.Eldergrove: return new Color(0.30f, 0.70f, 0.42f, 1f);
                case RealmId.Crownlands: return new Color(0.92f, 0.66f, 0.30f, 1f);
                case RealmId.Umbral: return new Color(0.52f, 0.34f, 0.78f, 1f);
                default: return new Color(0.52f, 0.58f, 0.64f, 1f);
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
