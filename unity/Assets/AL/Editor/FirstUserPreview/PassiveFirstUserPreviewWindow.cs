using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: InternalsVisibleTo("AL.FirstUserPreview.Editor.Tests")]

namespace AL.EditorTools.FirstUserPreview
{
    internal enum PassiveFirstUserPreviewScreen
    {
        FinishedLoading = 0,
        ChampionHud = 1
    }

    internal static class FirstUserPreviewAssetContract
    {
        internal const string AssetRoot = "Assets/Editor Default Resources/AL/FirstUserPreview";
        internal const string FinishedLoadingKey = "AL/FirstUserPreview/FinishedLoading.png";
        internal const string ChampionHudKey = "AL/FirstUserPreview/ChampionHud.png";
        internal const string FinishedLoadingPath = AssetRoot + "/FinishedLoading.png";
        internal const string ChampionHudPath = AssetRoot + "/ChampionHud.png";
        internal const string FinishedLoadingGuid = "ecc9917fa4fb43c89db080f0b4bff9c5";
        internal const string ChampionHudGuid = "e62f9e7928da4735a1ff13d105721815";
    }

    internal interface IFirstUserPreviewAssetLoader
    {
        bool TryLoad(string resourceKey, string expectedAssetPath, out Texture2D texture);
    }

    internal sealed class EditorDefaultResourceFirstUserPreviewAssetLoader : IFirstUserPreviewAssetLoader
    {
        public bool TryLoad(string resourceKey, string expectedAssetPath, out Texture2D texture)
        {
            texture = EditorGUIUtility.Load(resourceKey) as Texture2D;
            if (texture == null)
            {
                return false;
            }

            string actualPath = AssetDatabase.GetAssetPath(texture);
            if (string.Equals(actualPath, expectedAssetPath, StringComparison.Ordinal))
            {
                return true;
            }

            texture = null;
            return false;
        }
    }

    public sealed class PassiveFirstUserPreviewWindow : EditorWindow
    {
        internal const string MenuPath = "Window/Another Life/Passive First-User UX Preview";
        internal const string WindowTitle = "First-User UX Preview";
        internal const string ActionText = "Preview Champion HUD";
        internal const string DisclosureText =
            "EDITOR DEVELOPMENT PREVIEW — SIMULATED INPUT READY — STATIC REFERENCE ART ONLY — " +
            "NO LOADING, PATCHING, GAMEPLAY, OR ACCOUNT ACTION OCCURS.";
        internal const string AssetUnavailableText =
            "PREVIEW UNAVAILABLE — THE APPROVED EDITOR-ONLY REFERENCE IS MISSING OR HAS AN UNEXPECTED ASSET PATH.";
        internal const string DisclosureElementName = "first-user-preview-disclosure";
        internal const string ImageElementName = "first-user-preview-image";
        internal const string ActionElementName = "preview-champion-hud-action";
        internal const string ErrorElementName = "first-user-preview-error";
        internal const float UnfocusedBorderWidth = 1f;
        internal const float FocusedBorderWidth = 5f;

        [NonSerialized] private PassiveFirstUserPreviewScreen _screen;
        [NonSerialized] private IFirstUserPreviewAssetLoader _assetLoader;
        [NonSerialized] private Button _action;
        [NonSerialized] private bool _initialFocusRequested;

        internal PassiveFirstUserPreviewScreen ScreenForTests => _screen;
        internal Button ActionForTests => _action;
        internal bool InitialFocusRequestedForTests => _initialFocusRequested;

        internal IFirstUserPreviewAssetLoader AssetLoaderForTests
        {
            set => _assetLoader = value;
        }

        [MenuItem(MenuPath, priority = 2250)]
        public static void ShowWindow()
        {
            PassiveFirstUserPreviewWindow window = GetWindow<PassiveFirstUserPreviewWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(720f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            ResetSession();
            EnsureAssetLoader();
        }

        private void OnDisable()
        {
            TearDownSession();
        }

        private void OnDestroy()
        {
            TearDownSession();
        }

        public void CreateGUI()
        {
            ResetSession();
            EnsureAssetLoader();
            BuildCurrentScreen();
        }

        internal bool TryShowChampionHudForTests()
        {
            return TryShowChampionHud();
        }

        internal bool TryReturnToFinishedLoadingForTests()
        {
            return TryReturnToFinishedLoading();
        }

        internal void ResetSessionForTests()
        {
            ResetSession();
            BuildCurrentScreen();
        }

        internal void RebuildCurrentScreenForTests()
        {
            BuildCurrentScreen();
        }

        private void EnsureAssetLoader()
        {
            if (_assetLoader == null)
            {
                _assetLoader = new EditorDefaultResourceFirstUserPreviewAssetLoader();
            }
        }

        private void ResetSession()
        {
            _screen = PassiveFirstUserPreviewScreen.FinishedLoading;
            _initialFocusRequested = false;
        }

        private void TearDownSession()
        {
            DetachActionHandlers();
            VisualElement root = rootVisualElement;
            root.UnregisterCallback<KeyDownEvent>(OnRootKeyDown);
            root.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root.Clear();
            _screen = PassiveFirstUserPreviewScreen.FinishedLoading;
            _assetLoader = null;
            _initialFocusRequested = false;
        }

        private void BuildCurrentScreen()
        {
            EnsureAssetLoader();
            DetachActionHandlers();

            VisualElement root = rootVisualElement;
            root.UnregisterCallback<KeyDownEvent>(OnRootKeyDown);
            root.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root.Clear();
            ConfigureRoot(root);
            root.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
            root.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);

            Label disclosure = new Label(DisclosureText)
            {
                name = DisclosureElementName,
                focusable = false,
                pickingMode = PickingMode.Ignore
            };
            disclosure.style.unityTextAlign = TextAnchor.MiddleCenter;
            disclosure.style.whiteSpace = WhiteSpace.Normal;
            disclosure.style.fontSize = 13f;
            disclosure.style.unityFontStyleAndWeight = FontStyle.Bold;
            disclosure.style.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            disclosure.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            disclosure.style.paddingTop = 8f;
            disclosure.style.paddingBottom = 8f;
            disclosure.style.paddingLeft = 12f;
            disclosure.style.paddingRight = 12f;
            root.Add(disclosure);

            string resourceKey = _screen == PassiveFirstUserPreviewScreen.FinishedLoading
                ? FirstUserPreviewAssetContract.FinishedLoadingKey
                : FirstUserPreviewAssetContract.ChampionHudKey;
            string expectedPath = _screen == PassiveFirstUserPreviewScreen.FinishedLoading
                ? FirstUserPreviewAssetContract.FinishedLoadingPath
                : FirstUserPreviewAssetContract.ChampionHudPath;

            if (!_assetLoader.TryLoad(resourceKey, expectedPath, out Texture2D texture))
            {
                AddUnavailableState(root);
                root.Focus();
                return;
            }

            Image image = new Image
            {
                name = ImageElementName,
                image = texture,
                scaleMode = ScaleMode.ScaleToFit,
                focusable = false,
                pickingMode = PickingMode.Ignore
            };
            image.style.flexGrow = 1f;
            image.style.flexShrink = 1f;
            image.style.minHeight = 0f;
            image.style.backgroundColor = Color.black;
            root.Add(image);

            if (_screen == PassiveFirstUserPreviewScreen.FinishedLoading)
            {
                AddPreviewAction(root);
            }
            else
            {
                root.Focus();
            }
        }

        private static void ConfigureRoot(VisualElement root)
        {
            root.name = "passive-first-user-preview-root";
            root.focusable = true;
            root.tabIndex = -1;
            root.pickingMode = PickingMode.Position;
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1f;
            root.style.backgroundColor = Color.black;
        }

        private static void AddUnavailableState(VisualElement root)
        {
            Label error = new Label(AssetUnavailableText)
            {
                name = ErrorElementName,
                focusable = false,
                pickingMode = PickingMode.Ignore
            };
            error.style.flexGrow = 1f;
            error.style.unityTextAlign = TextAnchor.MiddleCenter;
            error.style.whiteSpace = WhiteSpace.Normal;
            error.style.fontSize = 14f;
            error.style.color = new Color(1f, 0.78f, 0.3f, 1f);
            error.style.paddingLeft = 32f;
            error.style.paddingRight = 32f;
            root.Add(error);
        }

        private void AddPreviewAction(VisualElement root)
        {
            _action = new Button
            {
                name = ActionElementName,
                text = ActionText,
                focusable = true,
                tabIndex = 0,
                tooltip = "Shows the approved passive Champion HUD reference. No game action occurs."
            };
            _action.clicked += OnPreviewChampionHudRequested;
            _action.RegisterCallback<FocusInEvent>(OnActionFocused);
            _action.RegisterCallback<FocusOutEvent>(OnActionBlurred);
            _action.style.height = 44f;
            _action.style.marginTop = 10f;
            _action.style.marginBottom = 12f;
            _action.style.marginLeft = 24f;
            _action.style.marginRight = 24f;
            _action.style.fontSize = 15f;
            _action.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetActionBorderWidth(_action, UnfocusedBorderWidth);
            root.Add(_action);

            _action.Focus();
            _initialFocusRequested = true;
        }

        private void DetachActionHandlers()
        {
            if (_action == null)
            {
                return;
            }

            _action.clicked -= OnPreviewChampionHudRequested;
            _action.UnregisterCallback<FocusInEvent>(OnActionFocused);
            _action.UnregisterCallback<FocusOutEvent>(OnActionBlurred);
            _action = null;
        }

        private void OnPreviewChampionHudRequested()
        {
            TryShowChampionHud();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || !TryReturnToFinishedLoading())
            {
                return;
            }

            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (!TryReturnToFinishedLoading())
            {
                return;
            }

            evt.StopImmediatePropagation();
            evt.PreventDefault();
        }

        private static void OnActionFocused(FocusInEvent evt)
        {
            if (evt.currentTarget is Button button)
            {
                SetActionBorderWidth(button, FocusedBorderWidth);
            }
        }

        private static void OnActionBlurred(FocusOutEvent evt)
        {
            if (evt.currentTarget is Button button)
            {
                SetActionBorderWidth(button, UnfocusedBorderWidth);
            }
        }

        private static void SetActionBorderWidth(VisualElement action, float width)
        {
            action.style.borderTopWidth = width;
            action.style.borderRightWidth = width;
            action.style.borderBottomWidth = width;
            action.style.borderLeftWidth = width;
        }

        private bool TryShowChampionHud()
        {
            if (_screen != PassiveFirstUserPreviewScreen.FinishedLoading || _action == null)
            {
                return false;
            }

            _screen = PassiveFirstUserPreviewScreen.ChampionHud;
            _initialFocusRequested = false;
            BuildCurrentScreen();
            return true;
        }

        private bool TryReturnToFinishedLoading()
        {
            if (_screen != PassiveFirstUserPreviewScreen.ChampionHud)
            {
                return false;
            }

            _screen = PassiveFirstUserPreviewScreen.FinishedLoading;
            BuildCurrentScreen();
            return true;
        }
    }
}
