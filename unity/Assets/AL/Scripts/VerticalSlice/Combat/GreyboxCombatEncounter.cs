using System;
using AL.VerticalSlice;
using UnityEngine;
using UnityEngine.UI;

namespace AL.VerticalSlice.Combat
{
    /// <summary>
    /// Greybox champion combat encounter for the DemoInitializer arena. Self-contained and authority-free:
    /// reads the selected champion from <see cref="SliceRunState"/> (falling back to a default), builds the
    /// hardcoded opponent, runs a turn-based Attack/Defend/Special duel, shows win/lose feedback, writes the
    /// result to <see cref="SliceRunState"/>, and returns control to the command loop via <see cref="ReturnRequested"/>.
    ///
    /// Tuning is exposed through <see cref="CombatEncounterConfig"/> (Inspector fields on this component).
    /// </summary>
    public sealed class GreyboxCombatEncounter : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private CombatEncounterConfig _config = new CombatEncounterConfig();

        /// <summary>Fired when the encounter reaches a terminal state (win/lose), with the committed result.</summary>
        public event Action<SliceCombatResult> Completed;

        /// <summary>Fired when the player chooses to return to the command loop after the encounter.</summary>
        public event Action ReturnRequested;

        public SliceCombatResult Result { get; private set; }
        public bool IsRunning { get; private set; }

        private CombatEncounterSim _sim;
        private GameObject _canvasObject;
        private Text _turnText;
        private Text _championReadout;
        private Text _opponentReadout;
        private Text _logText;
        private Button _attackButton;
        private Button _defendButton;
        private Button _specialButton;
        private GameObject _resultPanel;
        private Text _resultTitle;
        private Text _resultSummary;

        public void BeginEncounter()
        {
            SliceChampionProfile champion = SliceRunState.SelectedChampion ?? SliceChampionProfile.CreateDefault();
            SliceRunState.SelectedChampion = champion; // ensure the selected champion is populated for this run
            SliceOpponentProfile opponent = _config.Opponent ?? SliceOpponentProfile.CreateDefault();

            int seed = Environment.TickCount ^ champion.Id.GetHashCode();
            _sim = new CombatEncounterSim(champion, opponent, _config, seed);
            Result = null;
            IsRunning = true;

            BuildUI(champion, opponent);
            Render();
        }

        /// <summary>Ends the encounter early without a win/lose outcome; still records an Aborted result.</summary>
        public void AbortEncounter()
        {
            if (_sim == null)
            {
                return;
            }

            Result = _sim.BuildResult(NewAttemptId());
            Result.Outcome = CombatEncounterOutcome.Aborted;
            SliceRunState.LastCombatResult = Result;
            IsRunning = false;
            Completed?.Invoke(Result);
            TearDownUI();
        }

        private static string NewAttemptId() => Guid.NewGuid().ToString("N");

        // ----------------------------------------------------------------- UI construction

        private void BuildUI(SliceChampionProfile champion, SliceOpponentProfile opponent)
        {
            TearDownUI();

            _canvasObject = new GameObject("GreyboxCombatEncounter_UI");
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // above the DemoInitializer command board
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasObject.AddComponent<GraphicRaycaster>();

            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(_canvasObject.transform, false);
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0.01f, 0.015f, 0.02f, 0.86f);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            var panel = CreatePanel(_canvasObject.transform, "DuelPanel", new Vector2(0f, 0f), new Vector2(780f, 600f), new Color(0.03f, 0.05f, 0.07f, 0.97f));

            Text title = CreateText(panel.transform, "DuelTitle", new Vector2(0f, -26f), new Vector2(700f, 44f), 30, new Color(1f, 0.88f, 0.62f));
            title.text = "CHAMPION DUEL";
            title.alignment = TextAnchor.MiddleCenter;

            _turnText = CreateText(panel.transform, "DuelTurn", new Vector2(0f, -66f), new Vector2(700f, 26f), 16, new Color(0.72f, 0.82f, 0.92f));
            _turnText.alignment = TextAnchor.MiddleCenter;

            _championReadout = CreateText(panel.transform, "ChampionReadout", new Vector2(-360f, -108f), new Vector2(340f, 150f), 18, new Color(0.70f, 0.85f, 1f));
            _opponentReadout = CreateText(panel.transform, "OpponentReadout", new Vector2(20f, -108f), new Vector2(340f, 150f), 18, new Color(1f, 0.55f, 0.50f));
            _opponentReadout.alignment = TextAnchor.UpperRight;

            _logText = CreateText(panel.transform, "DuelLog", new Vector2(0f, -270f), new Vector2(700f, 110f), 17, Color.white);
            _logText.alignment = TextAnchor.UpperLeft;

            _attackButton = CreateButton(panel.transform, "ATTACK", new Vector2(-250f, -430f), new Vector2(200f, 54f), () => ApplyAction(CombatAction.Attack));
            _defendButton = CreateButton(panel.transform, "DEFEND", new Vector2(0f, -430f), new Vector2(200f, 54f), () => ApplyAction(CombatAction.Defend));
            _specialButton = CreateButton(panel.transform, "SPECIAL", new Vector2(250f, -430f), new Vector2(200f, 54f), () => ApplyAction(CombatAction.Special));

            _resultPanel = new GameObject("ResultPanel");
            _resultPanel.transform.SetParent(panel.transform, false);
            var resultRect = _resultPanel.AddComponent<RectTransform>();
            resultRect.anchorMin = Vector2.zero;
            resultRect.anchorMax = Vector2.one;
            resultRect.offsetMin = Vector2.zero;
            resultRect.offsetMax = Vector2.zero;
            var resultBackdrop = _resultPanel.AddComponent<Image>();
            resultBackdrop.color = new Color(0.02f, 0.03f, 0.04f, 0.96f);

            _resultTitle = CreateText(_resultPanel.transform, "ResultTitle", new Vector2(0f, -160f), new Vector2(700f, 70f), 42, new Color(1f, 0.86f, 0.4f));
            _resultTitle.alignment = TextAnchor.MiddleCenter;

            _resultSummary = CreateText(_resultPanel.transform, "ResultSummary", new Vector2(0f, -260f), new Vector2(640f, 160f), 18, Color.white);
            _resultSummary.alignment = TextAnchor.UpperCenter;

            CreateButton(_resultPanel.transform, "RETURN TO COMMAND", new Vector2(0f, -440f), new Vector2(320f, 58f), OnReturnRequested);

            _resultPanel.SetActive(false);
        }

        private void ApplyAction(CombatAction action)
        {
            if (_sim == null || _sim.IsFinished)
            {
                return;
            }

            _sim.PerformPlayerAction(action);
            Render();

            if (_sim.IsFinished)
            {
                Finish();
            }
        }

        private void Finish()
        {
            Result = _sim.BuildResult(NewAttemptId());
            SliceRunState.LastCombatResult = Result;
            IsRunning = false;
            ShowResultPanel();
            Completed?.Invoke(Result);
        }

        private void ShowResultPanel()
        {
            _resultPanel.SetActive(true);
            _resultTitle.text = Result.Won ? "VICTORY" : Result.Lost ? "DEFEAT" : "ENCOUNTER ENDED";
            _resultTitle.color = Result.Won ? new Color(0.55f, 0.95f, 0.5f) : Result.Lost ? new Color(1f, 0.4f, 0.35f) : Color.white;
            _resultSummary.text =
                $"{Result.ChampionDisplayName} vs {Result.OpponentDisplayName}\n" +
                $"Outcome: {Result.Outcome}\n" +
                $"Turns: {Result.TurnsTaken}   Damage dealt: {Result.DamageDealt}   Damage taken: {Result.DamageTaken}\n" +
                $"Specials used: {Result.SpecialsUsed}\n" +
                $"Champion HP remaining: {Result.ChampionHealthRemaining}/{Result.ChampionMaxHealth}";
        }

        private void OnReturnRequested()
        {
            ReturnRequested?.Invoke();
            TearDownUI();
        }

        private void Render()
        {
            if (_sim == null)
            {
                return;
            }

            _turnText.text = $"Turn {_sim.TurnNumber}";
            _championReadout.text =
                $"{ChampionName()}\n" +
                $"HP  {_sim.ChampionHealth}/{_sim.ChampionMaxHealth}\n" +
                $"MP  {_sim.ChampionMana}/{_sim.ChampionMaxMana}\n" +
                $"{( _sim.IsChampionDefending ? "GUARDING" : "READY")}\n" +
                $"Special: {(_sim.CanUseSpecial ? "READY" : "COOLDOWN " + _sim.SpecialCooldownRemaining)}";
            _opponentReadout.text =
                $"{OpponentName()}\n" +
                $"HP  {_sim.OpponentHealth}/{_sim.OpponentMaxHealth}";

            _logText.text = _sim.LastLog;
        }

        private string ChampionName()
        {
            return SliceRunState.SelectedChampion?.DisplayName ?? "Champion";
        }

        private string OpponentName()
        {
            return _config.Opponent?.DisplayName ?? "Opponent";
        }

        private void TearDownUI()
        {
            if (_canvasObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_canvasObject);
                }
                else
                {
                    DestroyImmediate(_canvasObject);
                }

                _canvasObject = null;
            }

            _turnText = null;
            _championReadout = null;
            _opponentReadout = null;
            _logText = null;
            _attackButton = null;
            _defendButton = null;
            _specialButton = null;
            _resultPanel = null;
            _resultTitle = null;
            _resultSummary = null;
        }

        private void OnDestroy()
        {
            TearDownUI();
        }

        // ----------------------------------------------------------------- UI helpers (mirrors DemoInitializer style)

        private static Image CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 sizeDelta, Action action)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.32f, 0.95f);
            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(() => action());
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var labelText = CreateText(buttonObject.transform, label + "_Text", Vector2.zero, sizeDelta, 20, Color.white);
            labelText.text = label;
            labelText.alignment = TextAnchor.MiddleCenter;
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }
    }
}
