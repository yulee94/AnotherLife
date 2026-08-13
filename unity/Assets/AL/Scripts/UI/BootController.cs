using System;
using System.Collections;
using AL.RealmSelection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI
{
    public class BootController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private string _realmSelectionScene = "RealmSelection";

        // Retained only for scene/descriptor compatibility while the first-user route contract is
        // integrated. Realm identity is deliberately never used to activate this route.
        [SerializeField] private string _kingdomScene = "Kingdom";

        [Header("Presentation")]
        [SerializeField] private bool _buildRuntimeSplash = true;
        [SerializeField] private string _buildLabel = "PRE-ALPHA RUNTIME";

        private LaunchReadinessCoordinator _readiness;
        private LaunchCinematicLifecycle _launchLifecycle;
        private CurrentBootLoadReceipt _bootReceipt;
        private CurrentRealmCatalogReceipt _catalogReceipt;

        private RectTransform _safeAreaRoot;
        private Text _statusText;
        private Text _detailText;
        private Button _continueButton;
        private Button _retryButton;
        private int _readyFrame = -1;
        private int _focusedGeneration;
        private Button _focusedAction;
        private bool _submitArmed;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private Rect _lastSafeArea = new Rect(-1f, -1f, -1f, -1f);
        private LaunchReadinessState _lastRenderedState = (LaunchReadinessState)(-1);
        private LaunchReadinessFailure _lastRenderedFailure = (LaunchReadinessFailure)(-1);
        private int _lastRenderedGeneration = -1;

        private void Start()
        {
            Debug.Log("AL Boot Sequence Started...");
            _readiness = new LaunchReadinessCoordinator();
            _launchLifecycle = new LaunchCinematicLifecycle();
            _launchLifecycle.MarkPreparing();

            if (_buildRuntimeSplash)
            {
                BuildRuntimeSplash();
            }
            else
            {
                // The current production Boot scene has no authored launch UI. Keep the gate visible
                // even if an old serialized flag is disabled instead of silently auto-routing.
                BuildRuntimeSplash();
            }

            _launchLifecycle.FailToFallback("approved-media-unavailable");
            _readiness.TryEstablishMedia(
                _readiness.AttemptGeneration,
                LaunchMediaPresentation.StaticFallbackEstablished);
            RefreshPresentation(force: true);
        }

        private void Update()
        {
            if (_readiness == null)
            {
                return;
            }

            UpdateSafeAreaIfNeeded();
            RefreshReadinessEvidence();
            RefreshPresentation(force: false);
            PollExplicitSubmit();
        }

        private void OnDisable()
        {
            _submitArmed = false;
            _readyFrame = -1;
            _focusedGeneration = 0;
            _focusedAction = null;
        }

        private void OnDestroy()
        {
            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveListener(OnContinueRequested);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveListener(OnRetryRequested);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _submitArmed = false;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                _submitArmed = false;
            }
        }

        private void RefreshReadinessEvidence()
        {
            LaunchReadinessSnapshot snapshot = _readiness.Snapshot;
            if (snapshot.State == LaunchReadinessState.Failed ||
                snapshot.State == LaunchReadinessState.Transitioning)
            {
                return;
            }

            int generation = snapshot.AttemptGeneration;
            if (_bootReceipt == null)
            {
                BootLoadReadinessProbeStatus bootStatus =
                    BootLoadReadinessProbe.TryCapture(generation, out _bootReceipt);
                if (bootStatus == BootLoadReadinessProbeStatus.Ready)
                {
                    _readiness.TryPublishBootLoad(_bootReceipt.Evidence);
                }
                else if (bootStatus == BootLoadReadinessProbeStatus.Unavailable)
                {
                    _readiness.TryFail(
                        generation,
                        LaunchReadinessFailure.BootLoadUnavailable,
                        retryAllowed: false);
                    return;
                }
            }
            else if (!BootLoadReadinessProbe.IsCurrent(_bootReceipt))
            {
                _readiness.TryFail(
                    generation,
                    LaunchReadinessFailure.EvidenceStale,
                    retryAllowed: false);
                return;
            }

            if (_catalogReceipt == null)
            {
                if (RealmCatalogReadinessProbe.TryCapture(generation, out _catalogReceipt))
                {
                    _readiness.TryPublishCatalog(_catalogReceipt.Evidence);
                }
                else if (RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Unavailable)
                {
                    _readiness.TryFail(
                        generation,
                        LaunchReadinessFailure.RequiredCatalogUnavailable,
                        retryAllowed: true);
                    return;
                }
            }
            else if (!RealmCatalogReadinessProbe.IsCurrent(_catalogReceipt))
            {
                _readiness.TryFail(
                    generation,
                    LaunchReadinessFailure.EvidenceStale,
                    retryAllowed: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(_realmSelectionScene) ||
                string.Equals(
                    _realmSelectionScene,
                    _kingdomScene,
                    StringComparison.Ordinal))
            {
                _readiness.TryFail(
                    generation,
                    LaunchReadinessFailure.DestinationUnavailable,
                    retryAllowed: false);
            }
            else if (Application.CanStreamedLevelBeLoaded(_realmSelectionScene))
            {
                _readiness.TryPublishDestination(
                    new LaunchDestinationEvidence(generation, _realmSelectionScene));
            }
            else if (_catalogReceipt != null)
            {
                _readiness.TryFail(
                    generation,
                    LaunchReadinessFailure.DestinationUnavailable,
                    retryAllowed: true);
            }
        }

        private void RefreshPresentation(bool force)
        {
            LaunchReadinessSnapshot snapshot = _readiness.Snapshot;
            if (!force &&
                snapshot.State == _lastRenderedState &&
                snapshot.Failure == _lastRenderedFailure &&
                snapshot.AttemptGeneration == _lastRenderedGeneration)
            {
                return;
            }

            _lastRenderedState = snapshot.State;
            _lastRenderedFailure = snapshot.Failure;
            _lastRenderedGeneration = snapshot.AttemptGeneration;

            bool ready = snapshot.CanContinue;
            bool failed = snapshot.State == LaunchReadinessState.Failed;
            if (_continueButton != null)
            {
                _continueButton.gameObject.SetActive(ready);
                _continueButton.interactable = ready;
            }

            if (_retryButton != null)
            {
                _retryButton.gameObject.SetActive(failed && snapshot.RetryAllowed);
                _retryButton.interactable = failed && snapshot.RetryAllowed;
            }

            if (_statusText != null)
            {
                _statusText.text = StatusFor(snapshot);
            }

            if (_detailText != null)
            {
                _detailText.text = DetailFor(snapshot);
            }

            if (ready)
            {
                _launchLifecycle.MarkAwaitingContinue(mandatoryReadinessReady: true);
                _readyFrame = Time.frameCount;
                _submitArmed = false;
                FocusCurrentAction(snapshot.AttemptGeneration, _continueButton);
            }
            else if (failed && snapshot.RetryAllowed)
            {
                _readyFrame = Time.frameCount;
                _submitArmed = false;
                FocusCurrentAction(snapshot.AttemptGeneration, _retryButton);
            }
            else
            {
                _readyFrame = -1;
                _submitArmed = false;
                ClearFocusedAction();
            }
        }

        private void FocusCurrentAction(int generation, Button button)
        {
            if (button == null)
            {
                return;
            }

            bool alreadyFocused =
                _focusedGeneration == generation &&
                _focusedAction == button;
            _focusedGeneration = generation;
            _focusedAction = button;

            if (EventSystem.current == null ||
                (alreadyFocused &&
                 EventSystem.current.currentSelectedGameObject == button.gameObject))
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private void ClearFocusedAction()
        {
            if (_focusedAction != null &&
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == _focusedAction.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            _focusedAction = null;
        }

        private void PollExplicitSubmit()
        {
            LaunchReadinessSnapshot snapshot = _readiness.Snapshot;
            bool canContinue = snapshot.CanContinue;
            bool canRetry =
                snapshot.State == LaunchReadinessState.Failed &&
                snapshot.RetryAllowed;
            if ((!canContinue && !canRetry) || Time.frameCount <= _readyFrame)
            {
                return;
            }

            if (!_submitArmed)
            {
                if (!AnySubmitControlHeld())
                {
                    _submitArmed = true;
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) ||
                Input.GetButtonDown("Submit"))
            {
                if (canContinue)
                {
                    OnContinueRequested();
                }
                else
                {
                    OnRetryRequested();
                }
            }
        }

        private static bool AnySubmitControlHeld()
        {
            return Input.GetKey(KeyCode.Return) ||
                Input.GetKey(KeyCode.KeypadEnter) ||
                Input.GetKey(KeyCode.Space) ||
                Input.GetButton("Submit");
        }

        private void OnContinueRequested()
        {
            if (_readiness == null || Time.frameCount <= _readyFrame)
            {
                return;
            }

            int generation = _readiness.AttemptGeneration;
            if (!_readiness.TryBeginTransition(generation))
            {
                return;
            }

            if (_continueButton != null)
            {
                _continueButton.interactable = false;
            }

            _submitArmed = false;
            _launchLifecycle.TryContinue(mandatoryReadinessReady: true);
            RefreshPresentation(force: true);
            StartCoroutine(LoadFirstUserDestination(generation));
        }

        private IEnumerator LoadFirstUserDestination(int attemptGeneration)
        {
            AsyncOperation operation = null;
            try
            {
                operation = SceneManager.LoadSceneAsync(_realmSelectionScene, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogError("[AL-LAUNCH-DESTINATION-FAILED] " + exception.GetType().Name);
            }

            if (operation == null)
            {
                _readiness.TryFailTransition(
                    attemptGeneration,
                    LaunchReadinessFailure.DestinationUnavailable,
                    retryAllowed: true);
                RefreshPresentation(force: true);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private void OnRetryRequested()
        {
            if (_readiness == null || Time.frameCount <= _readyFrame)
            {
                return;
            }

            LaunchReadinessFailure previousFailure = _readiness.Snapshot.Failure;
            if (!_readiness.TryBeginRetry())
            {
                RefreshPresentation(force: true);
                return;
            }

            _bootReceipt = null;
            _catalogReceipt = null;
            _focusedGeneration = 0;
            _focusedAction = null;
            _launchLifecycle = new LaunchCinematicLifecycle();
            _launchLifecycle.MarkPreparing();
            _launchLifecycle.FailToFallback("approved-media-unavailable");
            _readiness.TryEstablishMedia(
                _readiness.AttemptGeneration,
                LaunchMediaPresentation.StaticFallbackEstablished);

            if (previousFailure == LaunchReadinessFailure.RequiredCatalogUnavailable ||
                previousFailure == LaunchReadinessFailure.EvidenceStale)
            {
                RealmCatalogRuntime.TryRetry();
            }

            RefreshPresentation(force: true);
        }

        private void BuildRuntimeSplash()
        {
            var canvasObject = new GameObject("LaunchReadinessCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Treat layout units as mobile-density-independent planning units so the 64-unit
            // action floor remains comfortably above the 48 dp interaction target on narrow screens.
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            CreatePanel(
                canvasObject.transform,
                "StaticFallbackBackground",
                new Color(0.006f, 0.008f, 0.014f, 1f),
                Vector2.zero,
                Vector2.one);

            var safeAreaObject = new GameObject("SafeArea");
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            _safeAreaRoot = safeAreaObject.AddComponent<RectTransform>();

            var contentObject = new GameObject("LaunchContent");
            contentObject.transform.SetParent(_safeAreaRoot, false);
            var contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.08f, 0.14f);
            contentRect.anchorMax = new Vector2(0.92f, 0.86f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                Resources.GetBuiltinResource<Font>("Arial.ttf");

            Text title = CreateText(
                contentObject.transform,
                "Title",
                font,
                "ANOTHER LIFE",
                52,
                84f);
            title.color = new Color(0.92f, 0.92f, 0.88f, 1f);

            Text build = CreateText(
                contentObject.transform,
                "BuildLabel",
                font,
                _buildLabel,
                16,
                36f);
            build.color = new Color(0.66f, 0.69f, 0.72f, 0.9f);

            _statusText = CreateText(
                contentObject.transform,
                "ReadinessStatus",
                font,
                "Loading saved journey",
                30,
                72f);
            _statusText.color = new Color(0.86f, 0.90f, 0.95f, 1f);

            _detailText = CreateText(
                contentObject.transform,
                "ReadinessDetail",
                font,
                "Required game data is still being verified.",
                20,
                76f);
            _detailText.color = new Color(0.70f, 0.76f, 0.82f, 1f);

            _continueButton = CreateButton(
                contentObject.transform,
                "FinishedLoadingAction",
                font,
                "Continue");
            _continueButton.onClick.AddListener(OnContinueRequested);
            _continueButton.gameObject.SetActive(false);

            _retryButton = CreateButton(
                contentObject.transform,
                "RetryReadinessAction",
                font,
                "Retry");
            _retryButton.onClick.AddListener(OnRetryRequested);
            _retryButton.gameObject.SetActive(false);

            UpdateSafeAreaIfNeeded();
        }

        private void UpdateSafeAreaIfNeeded()
        {
            if (_safeAreaRoot == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            if (_lastScreenWidth == Screen.width &&
                _lastScreenHeight == Screen.height &&
                _lastSafeArea == safeArea)
            {
                return;
            }

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            _lastSafeArea = safeArea;
            _safeAreaRoot.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height);
            _safeAreaRoot.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height);
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;
        }

        private static string StatusFor(LaunchReadinessSnapshot snapshot)
        {
            switch (snapshot.State)
            {
                case LaunchReadinessState.WaitingForBootLoad:
                    return "Loading saved journey";
                case LaunchReadinessState.WaitingForRequiredCatalogs:
                    return "Loading realm choices";
                case LaunchReadinessState.WaitingForMediaPresentation:
                    return "Preparing launch presentation";
                case LaunchReadinessState.WaitingForDestination:
                    return "Preparing character setup";
                case LaunchReadinessState.AwaitingExplicitContinue:
                    return "Finished Loading";
                case LaunchReadinessState.Transitioning:
                    return "Opening character setup";
                case LaunchReadinessState.Failed:
                    return "Launch needs attention";
                default:
                    return "Preparing launch";
            }
        }

        private static string DetailFor(LaunchReadinessSnapshot snapshot)
        {
            if (snapshot.State != LaunchReadinessState.Failed)
            {
                return snapshot.CanContinue
                    ? "Press Continue, Enter, Space, or your controller Submit button."
                    : "Required game data is still being verified.";
            }

            switch (snapshot.Failure)
            {
                case LaunchReadinessFailure.RequiredCatalogUnavailable:
                    return snapshot.RetryAllowed
                        ? "Realm choices could not be loaded. Retry this launch check."
                        : "Realm choices could not be loaded. Restart the game.";
                case LaunchReadinessFailure.DestinationUnavailable:
                    return snapshot.RetryAllowed
                        ? "Character setup is unavailable. Retry this launch check."
                        : "Character setup is unavailable. Restart the game.";
                case LaunchReadinessFailure.RetryLimitReached:
                    return "Launch could not recover after several attempts. Restart the game.";
                case LaunchReadinessFailure.EvidenceStale:
                    return snapshot.RetryAllowed
                        ? "Launch data changed while loading. Retry this launch check."
                        : "Launch data changed unexpectedly. Restart the game.";
                default:
                    return "Your saved journey could not be verified. Restart the game.";
            }
        }

        private static Image CreatePanel(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            var image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            string value,
            int fontSize,
            float preferredHeight)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(14, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            var layout = textObject.AddComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            Font font,
            string label)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.18f, 0.38f, 0.58f, 1f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 72f;
            layout.minHeight = 64f;

            Text text = CreateText(
                buttonObject.transform,
                "Label",
                font,
                label,
                25,
                64f);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 8f);
            textRect.offsetMax = new Vector2(-18f, -8f);
            text.color = Color.white;
            return button;
        }
    }
}
