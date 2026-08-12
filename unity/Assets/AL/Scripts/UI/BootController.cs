using System.Collections;
using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI
{
    /// <summary>
    /// Cross-platform launch presentation. It owns the truthful service/media preparation surface,
    /// then waits for a fresh player action before entering the committed realm flow.
    /// </summary>
    public sealed class BootController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string _realmSelectionScene = "RealmSelection";
        [SerializeField] private string _kingdomScene = "Kingdom";

        [Header("Presentation")]
        [SerializeField] private bool _buildRuntimeSplash = true;
        [SerializeField] private string _buildLabel = "PRE-ALPHA RUNTIME";
        [SerializeField] private string _continueLabel = "ENTER ANOTHER LIFE";
        [SerializeField] private Texture2D _brandMark;

        private const float IndicatorTrackWidth = 420f;
        private const float IndicatorSegmentWidth = 92f;
        private const float IndicatorHeight = 3f;

        private static readonly Color MidnightSlate = Hex(0x07, 0x10, 0x17);
        private static readonly Color DeepInk = Hex(0x0d, 0x16, 0x20);
        private static readonly Color MoonIvory = Hex(0xee, 0xe6, 0xd2);
        private static readonly Color AgedGold = Hex(0xb9, 0x93, 0x55);
        private static readonly Color QuietSteel = Hex(0x8b, 0x98, 0xa3);

        private LaunchCinematicLifecycle _launchLifecycle;
        private CanvasGroup _loadingGroup;
        private CanvasGroup _standbyGroup;
        private RectTransform _indicatorSegment;
        private RectTransform _continueButtonRect;
        private Button _continueButton;
        private Text _statusText;
        private bool _continueRequested;
        private int _inputArmedFrame = int.MaxValue;
        private int _activeTouchFingerId = -1;

        private IEnumerator Start()
        {
            Debug.Log("AL Boot Sequence Started...");
            _launchLifecycle = new LaunchCinematicLifecycle();

            if (_buildRuntimeSplash)
            {
                BuildLaunchPresentation();
                // Give the loading surface one rendered frame before synchronous service setup.
                yield return null;
            }

            _launchLifecycle.MarkPreparing();
            Bootloader.InitializeIfMissing();

            // The current build has no approved cinematic encode. Waiting one end-of-frame proves the
            // Unity UI is present without manufacturing a progress percentage or a fake patch delay.
            // Batch-mode verification has no rendered end-of-frame, so advance one frame there instead.
            if (Application.isBatchMode)
            {
                yield return null;
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }
            _launchLifecycle.MarkFallbackReady("approved-media-unavailable");
            _launchLifecycle.MarkAwaitingContinue();

            if (_buildRuntimeSplash)
            {
                ShowStandby();
                while (!_continueRequested)
                {
                    AnimateStandby();
                    if (CanAcceptDeliberateContinue())
                    {
                        RequestContinue();
                    }

                    yield return null;
                }
            }

            if (!_launchLifecycle.TryContinue())
            {
                Debug.LogError("AL launch entry gate rejected the committed Continue action.");
                yield break;
            }

            LoadCommittedDestination();
        }

        private void BuildLaunchPresentation()
        {
            var canvasObject = new GameObject("LaunchFallbackCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            CreatePanel(
                canvasObject.transform,
                "LaunchBackground",
                MidnightSlate,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            BuildThresholdFrame(canvasObject.transform);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");

            Text thresholdMessage = CreateText(
                canvasObject.transform,
                "ThresholdMessage",
                font,
                "THE VEIL REMEMBERS",
                15,
                new Vector2(0f, 510f),
                new Vector2(720f, 40f),
                TextAnchor.MiddleCenter);
            thresholdMessage.color = QuietSteel;
            thresholdMessage.fontStyle = FontStyle.Bold;

            if (_brandMark != null)
            {
                CreateBrandMark(canvasObject.transform, _brandMark);
            }

            Text title = CreateText(
                canvasObject.transform,
                "Title",
                font,
                "ANOTHER\nLIFE",
                58,
                new Vector2(0f, 4f),
                new Vector2(520f, 164f),
                TextAnchor.MiddleCenter);
            title.color = MoonIvory;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 36;
            title.resizeTextMaxSize = 58;

            Text chapter = CreateText(
                canvasObject.transform,
                "Chapter",
                font,
                "A world shaped by the life you choose—\nand the one you leave behind.",
                19,
                new Vector2(0f, -156f),
                new Vector2(720f, 88f),
                TextAnchor.MiddleCenter);
            chapter.color = MoonIvory;

            _loadingGroup = CreateGroup(canvasObject.transform, "LoadingState");
            _statusText = CreateText(
                _loadingGroup.transform,
                "LoadingStatus",
                font,
                "Preparing your realm",
                22,
                new Vector2(0f, -452f),
                new Vector2(720f, 48f),
                TextAnchor.MiddleCenter);
            _statusText.color = QuietSteel;
            BuildIndeterminateIndicator(_loadingGroup.transform);

            _standbyGroup = CreateGroup(canvasObject.transform, "StandbyState");
            _standbyGroup.alpha = 0f;
            _standbyGroup.interactable = false;
            _standbyGroup.blocksRaycasts = false;

            Text readyText = CreateText(
                _standbyGroup.transform,
                "ReadyStatus",
                font,
                "Ready when you are",
                22,
                new Vector2(0f, -466f),
                new Vector2(720f, 48f),
                TextAnchor.MiddleCenter);
            readyText.color = QuietSteel;

            _continueButton = CreateContinueButton(_standbyGroup.transform, font);

            string inputHintValue = Application.isMobilePlatform
                ? "Tap the gold button to continue"
                : "Press Return, Space, or controller A";
            Text inputHint = CreateText(
                _standbyGroup.transform,
                "InputHint",
                font,
                inputHintValue,
                16,
                new Vector2(0f, -692f),
                new Vector2(720f, 38f),
                TextAnchor.MiddleCenter);
            inputHint.color = new Color(QuietSteel.r, QuietSteel.g, QuietSteel.b, 0.78f);

            Text buildLabel = CreateText(
                canvasObject.transform,
                "BuildLabel",
                font,
                _buildLabel,
                14,
                new Vector2(0f, -770f),
                new Vector2(680f, 36f),
                TextAnchor.MiddleCenter);
            buildLabel.color = new Color(QuietSteel.r, QuietSteel.g, QuietSteel.b, 0.66f);
        }

        private static void BuildThresholdFrame(Transform parent)
        {
            const float width = 760f;
            const float height = 1380f;
            Color line = new Color(AgedGold.r, AgedGold.g, AgedGold.b, 0.42f);

            CreatePanel(parent, "ThresholdTop", line,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, height * 0.5f),
                new Vector2(width, 2f));
            CreatePanel(parent, "ThresholdLeft", line,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(-width * 0.5f, 0f),
                new Vector2(2f, height));
            CreatePanel(parent, "ThresholdRight", line,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(width * 0.5f, 0f),
                new Vector2(2f, height));
        }

        private void BuildIndeterminateIndicator(Transform parent)
        {
            CreatePanel(parent, "IndicatorTrack", DeepInk,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -522f),
                new Vector2(IndicatorTrackWidth, IndicatorHeight));

            Image segment = CreatePanel(parent, "IndicatorSegment", MoonIvory,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-(IndicatorTrackWidth - IndicatorSegmentWidth) * 0.5f, -522f),
                new Vector2(IndicatorSegmentWidth, IndicatorHeight));
            _indicatorSegment = segment.rectTransform;
        }

        private Button CreateContinueButton(Transform parent, Font font)
        {
            CreatePanel(parent, "ContinueButtonFocusRing", new Color(MoonIvory.r, MoonIvory.g, MoonIvory.b, 0.72f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -590f),
                new Vector2(656f, 120f));

            var buttonObject = new GameObject("ContinueButton");
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = AgedGold;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (Application.isMobilePlatform)
            {
                // Mobile entry is handled from a fresh touch-down/touch-up pair below. Leaving the
                // Button callback disconnected prevents startup submit or stale pointer events from
                // skipping the player's deliberate choice.
                button.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            else
            {
                button.onClick.AddListener(RequestContinue);
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            }
            ColorBlock colors = button.colors;
            colors.normalColor = AgedGold;
            colors.highlightedColor = Hex(0xd0, 0xb0, 0x75);
            colors.pressedColor = Hex(0x93, 0x70, 0x3d);
            colors.selectedColor = Hex(0xd0, 0xb0, 0x75);
            colors.disabledColor = new Color(AgedGold.r, AgedGold.g, AgedGold.b, 0.35f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -590f);
            rect.sizeDelta = new Vector2(640f, 104f);
            _continueButtonRect = rect;

            Text label = CreateText(
                buttonObject.transform,
                "Label",
                font,
                _continueLabel,
                24,
                Vector2.zero,
                Vector2.zero,
                TextAnchor.MiddleCenter,
                stretch: true);
            label.color = MidnightSlate;
            label.fontStyle = FontStyle.Bold;
            return button;
        }

        private void ShowStandby()
        {
            if (_loadingGroup != null)
            {
                _loadingGroup.alpha = 0f;
                _loadingGroup.interactable = false;
                _loadingGroup.blocksRaycasts = false;
            }

            if (_standbyGroup != null)
            {
                _standbyGroup.alpha = 1f;
                _standbyGroup.interactable = true;
                _standbyGroup.blocksRaycasts = true;
            }

            _inputArmedFrame = Time.frameCount + 1;
            // Mobile entry is touch-only. Selecting the button on iOS/Android lets a startup
            // controller submit event activate it without the player touching the screen.
            if (!Application.isMobilePlatform && _continueButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_continueButton.gameObject);
            }
        }

        private void AnimateStandby()
        {
            if (_indicatorSegment != null && _loadingGroup != null && _loadingGroup.alpha > 0f)
            {
                float range = IndicatorTrackWidth - IndicatorSegmentWidth;
                float offset = Mathf.PingPong(Time.unscaledTime * 190f, range) - range * 0.5f;
                _indicatorSegment.anchoredPosition = new Vector2(offset, -522f);
            }
        }

        private static void CreateBrandMark(Transform parent, Texture2D texture)
        {
            var markObject = new GameObject("ApprovedBrandMark");
            markObject.transform.SetParent(parent, false);
            var mark = markObject.AddComponent<RawImage>();
            mark.texture = texture;
            mark.color = Color.white;
            mark.raycastTarget = false;

            RectTransform rect = mark.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 278f);
            rect.sizeDelta = new Vector2(280f, 280f);
        }

        private bool CanAcceptDeliberateContinue()
        {
            if (Application.isMobilePlatform)
            {
                return CanAcceptMobileTouchContinue();
            }

            if (Time.frameCount < _inputArmedFrame)
            {
                return false;
            }

            return Input.GetKeyDown(KeyCode.Return) ||
                   Input.GetKeyDown(KeyCode.KeypadEnter) ||
                   Input.GetKeyDown(KeyCode.Space) ||
                   Input.GetKeyDown(KeyCode.JoystickButton0);
        }

        private bool CanAcceptMobileTouchContinue()
        {
            if (Time.frameCount < _inputArmedFrame || _continueButtonRect == null)
            {
                return false;
            }

            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                bool insideButton = RectTransformUtility.RectangleContainsScreenPoint(
                    _continueButtonRect,
                    touch.position);

                if (touch.phase == TouchPhase.Began && _activeTouchFingerId < 0 && insideButton)
                {
                    _activeTouchFingerId = touch.fingerId;
                    continue;
                }

                if (touch.fingerId != _activeTouchFingerId)
                {
                    continue;
                }

                if (touch.phase == TouchPhase.Canceled)
                {
                    _activeTouchFingerId = -1;
                    continue;
                }

                if (touch.phase == TouchPhase.Ended)
                {
                    _activeTouchFingerId = -1;
                    return insideButton;
                }
            }

            return false;
        }

        private void RequestContinue()
        {
            if (Time.frameCount >= _inputArmedFrame)
            {
                _continueRequested = true;
            }
        }

        private void LoadCommittedDestination()
        {
            IRealmService realmService = ServiceLocator.Get<IRealmService>();
            if (realmService.CurrentRealmId == RealmId.None)
            {
                Debug.Log("No Realm Selected. Transitioning to Realm Selection...");
                SceneManager.LoadScene(_realmSelectionScene);
                return;
            }

            Debug.Log($"Realm {realmService.CurrentRealmId} detected. Loading Kingdom...");
            SceneManager.LoadScene(_kingdomScene);
        }

        private static CanvasGroup CreateGroup(Transform parent, string name)
        {
            var groupObject = new GameObject(name);
            groupObject.transform.SetParent(parent, false);
            var rect = groupObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return groupObject.AddComponent<CanvasGroup>();
        }

        private static Image CreatePanel(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            string textValue,
            int fontSize,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAnchor alignment,
            bool stretch = false)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.text = textValue;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;

            RectTransform rect = text.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = sizeDelta;
            }

            return text;
        }

        private static Color Hex(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 0xff);
        }
    }
}
