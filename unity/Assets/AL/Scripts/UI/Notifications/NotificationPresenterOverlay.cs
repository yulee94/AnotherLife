using System;
using AL.UI.DesignSystem;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.UI.Notifications
{
    [DisallowMultipleComponent]
    public sealed class NotificationPresenterOverlay : MonoBehaviour
    {
        private const int SortingOrder = 1200;
        private readonly UiAccessibilityFocusScope focusScope =
            new UiAccessibilityFocusScope();

        private RectTransform safeAreaRoot;
        private Image veil;
        private Action onAction;
        private Rect lastViewport;
        private Rect lastSafeArea;
        private bool built;

        public Image Panel { get; private set; }
        public Text SeverityLabel { get; private set; }
        public Text TitleLabel { get; private set; }
        public Text BodyLabel { get; private set; }
        public Button ActionButton { get; private set; }
        public Text ActionLabel { get; private set; }
        public string AccessibilityAnnouncement { get; private set; }
        public bool BlocksBackground { get; private set; }
        public bool IsShowing => Panel != null && Panel.gameObject.activeSelf;

        public static NotificationPresenterOverlay Mount(Transform parent)
        {
            var root = new GameObject(
                "NotificationPresenterOverlay",
                typeof(RectTransform));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            return root.AddComponent<NotificationPresenterOverlay>();
        }

        public void Show(NotificationPresentationPlan plan, Action action)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            EnsureBuilt();
            focusScope.RestorePreviousFocus();
            onAction = action;
            BlocksBackground = plan.BlocksBackground;
            AccessibilityAnnouncement = plan.Content.AccessibilityAnnouncement;
            SeverityLabel.text = plan.SeverityMarker;
            TitleLabel.text = plan.Content.Title;
            BodyLabel.text = plan.Content.Body;
            ActionLabel.text = plan.ActionLabel ?? string.Empty;

            bool hasAction = plan.Action != NotificationPresentationAction.None;
            veil.gameObject.SetActive(plan.BlocksBackground);
            Panel.gameObject.SetActive(true);
            Panel.raycastTarget = plan.BlocksBackground || hasAction;
            ActionButton.gameObject.SetActive(hasAction);
            ActionButton.onClick.RemoveAllListeners();
            if (hasAction)
            {
                ActionButton.onClick.AddListener(InvokeAction);
            }

            UiAccessibilityRuntime.ApplyPreferences(gameObject);
            UiAccessibilityRuntime.EnsureMinimumTouchTarget(ActionButton.transform as RectTransform);
            RectTransform panelRect = Panel.rectTransform;
            float textScale = Mathf.Max(1f, UiAccessibilityPreferences.TextScale);
            panelRect.sizeDelta = new Vector2(
                720f,
                280f + 160f * (textScale - 1f));
            RefreshSafeArea();
            if (plan.MovesFocus && hasAction)
            {
                focusScope.Activate(
                    EventSystem.current ?? FindAnyObjectByType<EventSystem>(),
                    safeAreaRoot,
                    new Selectable[] { ActionButton },
                    ActionButton);
            }
        }

        public void Hide()
        {
            focusScope.RestorePreviousFocus();
            onAction = null;
            AccessibilityAnnouncement = null;
            BlocksBackground = false;
            if (!built)
            {
                return;
            }

            ActionButton.onClick.RemoveAllListeners();
            ActionButton.gameObject.SetActive(false);
            Panel.gameObject.SetActive(false);
            veil.gameObject.SetActive(false);
        }

        public static void ApplySafeArea(
            RectTransform target,
            Rect viewport,
            Rect safeArea)
        {
            if (target == null || viewport.width <= 0f || viewport.height <= 0f)
            {
                return;
            }

            float xMin = Mathf.Clamp(safeArea.xMin, viewport.xMin, viewport.xMax);
            float yMin = Mathf.Clamp(safeArea.yMin, viewport.yMin, viewport.yMax);
            float xMax = Mathf.Clamp(safeArea.xMax, xMin, viewport.xMax);
            float yMax = Mathf.Clamp(safeArea.yMax, yMin, viewport.yMax);
            target.anchorMin = new Vector2(
                (xMin - viewport.xMin) / viewport.width,
                (yMin - viewport.yMin) / viewport.height);
            target.anchorMax = new Vector2(
                (xMax - viewport.xMin) / viewport.width,
                (yMax - viewport.yMin) / viewport.height);
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        private void Awake()
        {
            EnsureBuilt();
            Hide();
        }

        private void Update()
        {
            if (IsShowing)
            {
                RefreshSafeArea();
                focusScope.Refresh();
            }
        }

        private void OnDisable()
        {
            focusScope.RestorePreviousFocus();
        }

        private void InvokeAction()
        {
            Action callback = onAction;
            if (callback != null)
            {
                callback.Invoke();
            }
        }

        private void EnsureBuilt()
        {
            if (built)
            {
                return;
            }

            built = true;
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            PresentationChrome.ApplyCanvasScaler(gameObject.AddComponent<CanvasScaler>());
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform root = transform as RectTransform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            veil = PresentationChrome.CreatePlate(
                transform,
                "NotificationVeil",
                PresentationChrome.Veil,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero,
                raycastTarget: true);

            var safeArea = new GameObject("SafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(transform, false);
            safeAreaRoot = safeArea.GetComponent<RectTransform>();

            Panel = PresentationChrome.CreatePlate(
                safeAreaRoot,
                "NotificationPlate",
                PresentationChrome.StonePlate,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -PresentationChrome.SpaceMd),
                new Vector2(720f, 280f));
            AddMetalEdge(Panel.transform);

            Font font = PresentationChrome.ResolveFont();
            SeverityLabel = PresentationChrome.CreateLabel(
                Panel.transform,
                "Severity",
                font,
                string.Empty,
                PresentationChrome.TitleSize,
                PresentationChrome.MetalEdge,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(32f, -28f),
                new Vector2(52f, 44f));
            TitleLabel = PresentationChrome.CreateLabel(
                Panel.transform,
                "Title",
                font,
                string.Empty,
                PresentationChrome.TitleSize,
                PresentationChrome.Ink,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(88f, -30f),
                new Vector2(-120f, 46f));
            BodyLabel = PresentationChrome.CreateLabel(
                Panel.transform,
                "Body",
                font,
                string.Empty,
                PresentationChrome.BodySize,
                PresentationChrome.InkMuted,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(32f, -92f),
                new Vector2(-64f, 96f));

            ActionButton = PresentationChrome.CreateHit(
                Panel.transform,
                "Action",
                PresentationChrome.MetalDim,
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-32f, 28f),
                new Vector2(220f, PresentationChrome.MinHit));
            ActionLabel = PresentationChrome.CreateLabel(
                ActionButton.transform,
                "ActionLabel",
                font,
                string.Empty,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            ActionButton.gameObject.AddComponent<UiFocusVisibility>();
            UiAccessibilityRuntime.EnsureMinimumTouchTarget(
                ActionButton.transform as RectTransform);
        }

        private void RefreshSafeArea()
        {
            Rect viewport = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect safeArea = Screen.safeArea;
            if (viewport == lastViewport && safeArea == lastSafeArea)
            {
                return;
            }

            lastViewport = viewport;
            lastSafeArea = safeArea;
            ApplySafeArea(safeAreaRoot, viewport, safeArea);
        }

        private static void AddMetalEdge(Transform parent)
        {
            var outline = parent.gameObject.AddComponent<Outline>();
            outline.effectColor = PresentationChrome.MetalEdge;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;
        }
    }
}
