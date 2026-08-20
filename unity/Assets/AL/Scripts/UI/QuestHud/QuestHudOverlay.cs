using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AL.UI.QuestHud
{
    /// <summary>
    /// BDO-bar quest tracker: title, one-line what-to-do, location, and
    /// Accept / Continue / Complete. Mounts into QuestHudSlot when present.
    /// </summary>
    public sealed class QuestHudOverlay : MonoBehaviour
    {
        public QuestHudModel Model { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text WhatLabel { get; private set; }
        public Text WhereLabel { get; private set; }
        public Button AcceptButton { get; private set; }
        public Button ContinueButton { get; private set; }
        public Button CompleteButton { get; private set; }
        public Button AutoQuestButton { get; private set; }
        public Text AutoQuestLabel { get; private set; }

        private UnityAction _onPrimary;
        private UnityAction _onAutoQuestToggled;
        private string _lastAutoSignature;
        private bool _built;

        public static QuestHudOverlay Mount(Transform parent = null)
        {
            QuestHudOverlay existing = FindObjectOfType<QuestHudOverlay>();
            if (existing != null)
            {
                return existing;
            }

            Transform slot = FindSlot(parent);
            GameObject root;
            if (slot != null)
            {
                root = slot.gameObject;
                QuestHudOverlay onSlot = root.GetComponent<QuestHudOverlay>();
                if (onSlot != null)
                {
                    return onSlot;
                }

                return root.AddComponent<QuestHudOverlay>();
            }

            root = new GameObject(QuestHudCopy.RootName, typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            return root.AddComponent<QuestHudOverlay>();
        }

        public void Bind(QuestHudModel model, UnityAction onPrimary, UnityAction onAutoQuestToggled = null)
        {
            Model = model;
            _onPrimary = onPrimary;
            _onAutoQuestToggled = onAutoQuestToggled;
            EnsureBuilt();
            Apply(model);
            ConsiderAutoQuest();
        }

        public void FirePrimary()
        {
            if (_onPrimary != null)
            {
                _onPrimary.Invoke();
            }
        }

        public void ToggleAutoQuest()
        {
            QuestHudAutoQuest.SetEnabled(!QuestHudAutoQuest.Enabled);
            if (_onAutoQuestToggled != null)
            {
                _onAutoQuestToggled.Invoke();
            }
            else if (Model != null)
            {
                Bind(
                    new QuestHudModel(
                        Model.Title,
                        Model.WhatToDo,
                        Model.LocationName,
                        Model.LocationKey,
                        Model.StepId,
                        Model.Action,
                        Model.Surface,
                        QuestHudAutoQuest.Enabled),
                    _onPrimary,
                    _onAutoQuestToggled);
            }
        }

        public void ConsiderAutoQuest()
        {
            if (Model == null || !QuestHudAutoQuest.ShouldFire(Model))
            {
                return;
            }

            string signature = Model.StepId + "|" + Model.Action;
            if (signature == _lastAutoSignature)
            {
                return;
            }

            _lastAutoSignature = signature;
            FirePrimary();
        }

        public bool UsesLegacyRuntimeFont()
        {
            Text[] labels = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (QuestHudChrome.IsLegacyRuntime(labels[i].font))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureBuilt()
        {
            if (_built)
            {
                return;
            }

            EnsureCanvas();
            Font font = QuestHudChrome.ResolveFont();
            Image plate = transform.Find(QuestHudCopy.RootName) != null
                ? transform.Find(QuestHudCopy.RootName).GetComponent<Image>()
                : null;
            if (plate == null)
            {
                bool selfNamed = gameObject.name == QuestHudCopy.SlotName ||
                                 gameObject.name == QuestHudCopy.RootName;
                Transform plateParent = selfNamed ? transform : transform;
                plate = QuestHudChrome.CreatePlate(
                    plateParent,
                    QuestHudCopy.RootName,
                    QuestHudChrome.StonePlate,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(-24f, -92f),
                    new Vector2(QuestHudChrome.PlateWidth, QuestHudChrome.PlateHeight));
                if (selfNamed)
                {
                    RectTransform plateRect = plate.rectTransform;
                    plateRect.anchorMin = Vector2.zero;
                    plateRect.anchorMax = Vector2.one;
                    plateRect.pivot = new Vector2(0.5f, 0.5f);
                    plateRect.anchoredPosition = Vector2.zero;
                    plateRect.sizeDelta = Vector2.zero;
                    plateRect.offsetMin = Vector2.zero;
                    plateRect.offsetMax = Vector2.zero;
                }
            }

            QuestHudChrome.CreatePlate(
                plate.transform,
                "QuestHudCap",
                QuestHudChrome.MetalEdge,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                Vector2.zero,
                new Vector2(0f, 2f));

            TitleLabel = QuestHudChrome.CreateLabel(
                plate.transform,
                QuestHudCopy.TitleName,
                font,
                string.Empty,
                QuestHudChrome.TitleSize,
                QuestHudChrome.Ink,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -12f),
                new Vector2(-32f, 26f));

            WhatLabel = QuestHudChrome.CreateLabel(
                plate.transform,
                QuestHudCopy.WhatName,
                font,
                string.Empty,
                QuestHudChrome.BodySize,
                QuestHudChrome.InkMuted,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -40f),
                new Vector2(-32f, 40f));

            WhereLabel = QuestHudChrome.CreateLabel(
                plate.transform,
                QuestHudCopy.WhereName,
                font,
                string.Empty,
                QuestHudChrome.CaptionSize,
                QuestHudChrome.InkFaint,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -82f),
                new Vector2(-32f, 22f));

            AcceptButton = CreateAction(
                plate.transform,
                font,
                QuestHudCopy.AcceptName,
                QuestHudCopy.Accept,
                new Vector2(16f, 12f));
            ContinueButton = CreateAction(
                plate.transform,
                font,
                QuestHudCopy.ContinueName,
                QuestHudCopy.Continue,
                new Vector2(136f, 12f));
            CompleteButton = CreateAction(
                plate.transform,
                font,
                QuestHudCopy.CompleteName,
                QuestHudCopy.Complete,
                new Vector2(256f, 12f));

            AutoQuestButton = QuestHudChrome.CreateHit(
                plate.transform,
                QuestHudCopy.AutoQuestName,
                QuestHudChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(16f, -108f),
                new Vector2(168f, 28f));
            AutoQuestButton.onClick.AddListener(ToggleAutoQuest);
            AutoQuestLabel = QuestHudChrome.CreateLabel(
                AutoQuestButton.transform,
                "Label",
                font,
                QuestHudCopy.AutoQuestOff,
                12,
                QuestHudChrome.InkMuted,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            _built = true;
        }

        private Button CreateAction(Transform parent, Font font, string name, string label, Vector2 position)
        {
            Button button = QuestHudChrome.CreateHit(
                parent,
                name,
                QuestHudChrome.StoneInset,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                position,
                new Vector2(108f, 36f));
            button.onClick.AddListener(FirePrimary);
            QuestHudChrome.CreateLabel(
                button.transform,
                "Label",
                font,
                label,
                QuestHudChrome.ActionSize,
                QuestHudChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private void Apply(QuestHudModel model)
        {
            if (model == null)
            {
                return;
            }

            if (TitleLabel != null)
            {
                TitleLabel.text = model.Title;
            }

            if (WhatLabel != null)
            {
                WhatLabel.text = model.WhatToDo;
            }

            if (WhereLabel != null)
            {
                WhereLabel.text = QuestHudPlanner.SanitizeLocation(model.LocationName);
            }

            SetActionVisible(AcceptButton, model.Action == QuestHudAction.Accept);
            SetActionVisible(ContinueButton, model.Action == QuestHudAction.Continue);
            SetActionVisible(CompleteButton, model.Action == QuestHudAction.Complete);
            if (AutoQuestLabel != null)
            {
                AutoQuestLabel.text = model.AutoQuestLabel;
            }
        }

        private static void SetActionVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void EnsureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (canvas != null)
            {
                CanvasScaler existingScaler = canvas.GetComponent<CanvasScaler>();
                if (existingScaler != null)
                {
                    QuestHudChrome.ApplyScaler(existingScaler);
                }

                return;
            }

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 72;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            QuestHudChrome.ApplyScaler(scaler);
            gameObject.AddComponent<GraphicRaycaster>();
        }

        private static Transform FindSlot(Transform parent)
        {
            if (parent != null)
            {
                Transform direct = parent.Find(QuestHudCopy.SlotName);
                if (direct != null)
                {
                    return direct;
                }
            }

            GameObject named = GameObject.Find(QuestHudCopy.SlotName);
            return named != null ? named.transform : null;
        }
    }
}
