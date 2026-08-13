#if !UNITY_EDITOR
#error The isolated first-user Game Test host is Editor-only.
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using AL.ChampionMode;
using AL.ChampionMode.Control;
using AL.ChampionMode.Customization;
using AL.ChampionMode.UI;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Development;
using AL.Editor.Development.OnboardingAuthority;
using AL.UI;
using AL.UI.FirstUserIdentity;
using AL.UI.RealmSelection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[assembly: InternalsVisibleTo("AL.Development.FirstUserGameTest.Editor.Tests")]
[assembly: InternalsVisibleTo("AL.Development.FirstUserGameTest.PlayModeTests")]

namespace AL.Editor.Development.FirstUserGameTest
{
    [InitializeOnLoad]
    internal static class FirstUserGameTestEditorLauncher
    {
        static FirstUserGameTestEditorLauncher()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            EditorApplication.delayCall += InstallForActiveSession;
        }

        private static void InstallForActiveSession()
        {
            if (!EditorApplication.isPlaying ||
                !SessionState.GetBool(EditorGameTestModeBootstrap.SessionActiveKey, false) ||
                !EditorGameTestModeBootstrap.IsArmed)
            {
                return;
            }

            string sessionId = SessionState.GetString(
                EditorGameTestModeBootstrap.SessionIdKey,
                string.Empty);
            if (!string.Equals(
                    sessionId,
                    EditorGameTestModeBootstrap.ActiveSessionId,
                    StringComparison.Ordinal))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "First-user Game Test host session mismatch");
                return;
            }

            FirstUserGameTestRuntimeHost.Install(sessionId);
        }
    }

    internal sealed class FirstUserGameTestDestinationMarker : MonoBehaviour
    {
        internal void Configure(
            FirstUserGameTestSelection selection,
            ChampionController controller,
            Button attackButton)
        {
            Selection = selection;
            Controller = controller;
            AttackButton = attackButton;
            IsReady = selection != null && controller != null && attackButton != null;
        }

        internal FirstUserGameTestSelection Selection { get; private set; }
        internal ChampionController Controller { get; private set; }
        internal Button AttackButton { get; private set; }
        internal bool IsReady { get; private set; }
    }

    internal sealed class FirstUserGameTestRuntimeHost :
        IFirstUserGameTestDevelopmentWritableVerifier
    {
        internal const string HostObjectName = "[AL] First User Game Test Host";
        internal const string DisclosureObjectName = "FirstUserGameTestDisclosure";
        internal const string CustomizationCanvasName = "FirstUserGameTestCustomizationCanvas";
        internal const string FailureCanvasName = "FirstUserGameTestFailureCanvas";
        internal const string DestinationRootName = "[AL DEV] Isolated Character Game Test";
        internal const string ChampionArenaPath = "Assets/AL/Scenes/ChampionArena.unity";
        internal const string ChampionArenaGuid = "9c8e973279bb149b49b9938b1781c775";
        internal const string RealmSelectionPath = "Assets/AL/Scenes/RealmSelection.unity";
        internal const string KingdomPath = "Assets/AL/Scenes/Kingdom.unity";

        private const int MaximumBodyPresetChoices = 16;
        private const int MaximumCustomizationCatalogBytes = 64 * 1024;
        private const float DestinationLoadTimeoutSeconds = 30f;
        private const string CustomizationCatalogFileName =
            "al_character_customization_catalog.json";

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly HashSet<string> _approvedCustomizationIds =
            new HashSet<string>(StringComparer.Ordinal);

        private string _sessionId = string.Empty;
        private string _boundIsolatedSaveRoot = string.Empty;
        private string _runtimeBoundaryFailure = string.Empty;
        private bool _initialized;
        private bool _commitInProgress;
        private bool _destinationAuthorized;
        private bool _destinationBuilt;
        private string _lastFailure = string.Empty;
        private FirstUserIdentityDraftPresenter _identityPresenter;
        private GameObject _identityCanvas;
        private FirstUserGameTestCustomizationPanel _customizationPanel;
        private GameObject _disclosureCanvas;
        private GameObject _failureCanvas;
        private FirstUserGameTestAdapter _adapter;
        private FirstUserGameTestAdapterResult _verifiedResult;
        private ISaveGameService _boundIsolatedSaveService;
        private bool _developmentEvidenceBound;
        private DevelopmentReceiptHandle _boundReceiptHandle;
        private DevelopmentProjectionHandle _boundProjectionHandle;
        private int _destinationLoadRequestCount;
        private FirstUserIdentityDraftSnapshot _identitySnapshot;
        private FirstUserGameTestDestinationMarker _destinationMarker;
        private EditorGameTestModeHostDriver _driver;

        internal static FirstUserGameTestRuntimeHost Active { get; private set; }

        internal FirstUserIdentityDraftPresenter IdentityPresenter => _identityPresenter;
        internal FirstUserGameTestCustomizationPanel CustomizationPanel => _customizationPanel;
        internal FirstUserGameTestDestinationMarker DestinationMarker => _destinationMarker;
        internal string LastFailure => _lastFailure;
        internal int DestinationLoadRequestCount => _destinationLoadRequestCount;
        internal bool ReverifyVerifiedDevelopmentBoundaryForTests() =>
            _verifiedResult != null &&
            IsDevelopmentWritable(
                _verifiedResult.Receipt,
                _verifiedResult.Projection);

        internal static FirstUserGameTestRuntimeHost Install(string sessionId)
        {
            if (Active != null)
            {
                return Active;
            }

            var hostObject = new GameObject(HostObjectName);
            var driver = hostObject.AddComponent<EditorGameTestModeHostDriver>();
            var host = new FirstUserGameTestRuntimeHost();
            host.Initialize(sessionId, driver);
            if (!ReferenceEquals(Active, host))
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }

            return host;
        }

        internal static void DisposeActiveForTests()
        {
            Active?.Dispose();
        }

        private void Initialize(string sessionId, EditorGameTestModeHostDriver driver)
        {
            if (_initialized)
            {
                return;
            }

            _driver = driver;

            if (string.IsNullOrEmpty(sessionId) ||
                !EditorGameTestModeBootstrap.IsArmed ||
                !string.Equals(
                    sessionId,
                    EditorGameTestModeBootstrap.ActiveSessionId,
                    StringComparison.Ordinal))
            {
                FailClosed("The isolated host was not bound to the armed Game Test session.");
                return;
            }

            _sessionId = sessionId;
            try
            {
                _boundIsolatedSaveRoot = Path.GetFullPath(
                    EditorGameTestModeBootstrap.ActiveSaveRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                FailClosed("The armed isolated save root was invalid.");
                return;
            }

            _adapter = new FirstUserGameTestAdapter(sessionId);
            _initialized = true;
            Active = this;
            _driver.Tick += HandleTick;
            _driver.Destroyed += HandleDriverDestroyed;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BuildPersistentDisclosure();
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void HandleDriverDestroyed()
        {
            _driver = null;
            TearDownHostState();
        }

        private void Dispose()
        {
            EditorGameTestModeHostDriver driver = _driver;
            if (driver != null)
            {
                driver.Tick -= HandleTick;
                driver.Destroyed -= HandleDriverDestroyed;
            }

            _driver = null;
            TearDownHostState();
            if (driver != null)
            {
                UnityEngine.Object.DestroyImmediate(driver.gameObject);
            }
        }

        private void TearDownHostState()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            DestroyOwnedSelectionUi();
            if (_disclosureCanvas != null)
            {
                UnityEngine.Object.Destroy(_disclosureCanvas);
                _disclosureCanvas = null;
            }

            if (_failureCanvas != null)
            {
                UnityEngine.Object.Destroy(_failureCanvas);
                _failureCanvas = null;
            }

            _initialized = false;
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        private void HandleTick()
        {
            if (!_initialized || _destinationBuilt || _commitInProgress)
            {
                return;
            }

            if (_customizationPanel != null && Input.GetKeyDown(KeyCode.Escape))
            {
                ReturnToIdentitySelection();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_initialized || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (string.Equals(scene.path, KingdomPath, StringComparison.Ordinal))
            {
                DisableBehavioursInScene<MonoBehaviour>(scene);
                FailClosed("Kingdom is never an isolated first-user Game Test destination.");
                return;
            }

            if (string.Equals(
                    scene.path,
                    EditorGameTestModeBootstrap.ExpectedBootScenePath,
                    StringComparison.Ordinal))
            {
                if (_destinationAuthorized || _verifiedResult != null)
                {
                    DisableBehavioursInScene<BootController>(scene);
                    FailClosed("Boot cannot be re-entered after development authority was verified.");
                }

                return;
            }

            if (string.Equals(scene.path, RealmSelectionPath, StringComparison.Ordinal))
            {
                if (_destinationAuthorized)
                {
                    DisableBehavioursInScene<RealmSelectionController>(scene);
                    FailClosed("RealmSelection was re-entered after development authority was verified.");
                    return;
                }

                MountIdentitySelection(scene);
                return;
            }

            if (string.Equals(scene.path, ChampionArenaPath, StringComparison.Ordinal))
            {
                if (!_destinationAuthorized || _verifiedResult == null ||
                    !_verifiedResult.CanEnterIsolatedCharacterGameTest ||
                    !IsDevelopmentWritable(
                        _verifiedResult.Receipt,
                        _verifiedResult.Projection))
                {
                    DisableBehavioursInScene<ChampionArenaSceneController>(scene);
                    FailClosed("ChampionArena was requested without exact development receipt and projection evidence.");
                    return;
                }

                BuildIsolatedDestination(scene);
                return;
            }

            if (!string.IsNullOrEmpty(scene.path))
            {
                DisableBehavioursInScene<MonoBehaviour>(scene);
                FailClosed("Unexpected scene entered the isolated Game Test: " + scene.path);
            }
        }

        private void MountIdentitySelection(Scene scene)
        {
            int suppressed = DisableBehavioursInScene<RealmSelectionController>(scene);
            if (suppressed != 1)
            {
                FailClosed(
                    "The isolated host expected exactly one production RealmSelection controller; found " +
                    suppressed + ".");
                return;
            }

            if (!ValidateSingleEventSystem(out string eventMessage))
            {
                FailClosed(eventMessage);
                return;
            }

            DestroyOwnedSelectionUi();
            _identityPresenter = FirstUserIdentityDraftPresenter.CreateStandalone();
            _identityCanvas = _identityPresenter.transform.parent == null
                ? _identityPresenter.gameObject
                : _identityPresenter.transform.parent.gameObject;
            _identityCanvas.name = "FirstUserGameTestIdentityCanvas";
            Canvas canvas = _identityCanvas.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 2000;
            }

            _identityPresenter.CustomizationReady += HandleCustomizationReady;
        }

        private void HandleCustomizationReady(FirstUserIdentityDraftSnapshot snapshot)
        {
            if (_commitInProgress || snapshot == null || !snapshot.IsCustomizationReady)
            {
                FailClosed("The identity draft did not reach a valid customization boundary.");
                return;
            }

            if (!FirstUserIdentityDerivation.TryDeriveRace(
                    snapshot.Realm,
                    out FirstUserRace derivedRace) ||
                derivedRace != snapshot.Race ||
                !snapshot.ClassFamily.HasValue ||
                !FirstUserIdentityDerivation.IsSupportedClassFamily(snapshot.ClassFamily.Value))
            {
                FailClosed("Realm-derived people or explicit class-family evidence was inconsistent.");
                return;
            }

            if (!TryLoadCustomizationChoices(
                    out BodyPresetData[] bodyPresets,
                    out string loadMessage))
            {
                FailClosed(loadMessage);
                return;
            }

            _identitySnapshot = snapshot;
            if (_identityPresenter != null)
            {
                _identityPresenter.CustomizationReady -= HandleCustomizationReady;
            }

            if (_identityCanvas != null)
            {
                UnityEngine.Object.Destroy(_identityCanvas);
            }

            _identityPresenter = null;
            _identityCanvas = null;
            _customizationPanel = FirstUserGameTestCustomizationPanel.Create(
                bodyPresets,
                snapshot,
                HandleCustomizationConfirmed,
                ReturnToIdentitySelection);
        }

        private void ReturnToIdentitySelection()
        {
            if (_commitInProgress || _destinationAuthorized)
            {
                return;
            }

            if (_customizationPanel != null)
            {
                UnityEngine.Object.Destroy(_customizationPanel.gameObject);
                _customizationPanel = null;
            }

            MountIdentitySelection(SceneManager.GetActiveScene());
        }

        private void HandleCustomizationConfirmed(string customizationId, string handle)
        {
            if (_commitInProgress || _destinationAuthorized)
            {
                return;
            }

            if (!_approvedCustomizationIds.Contains(customizationId))
            {
                FailClosed("The selected customization was not an exact member of the loaded catalog.");
                return;
            }

            if (!TryVerifyDevelopmentRuntimeBoundary(
                    out bool firstHostReady,
                    out string firstBoundaryMessage))
            {
                _customizationPanel?.SetBusy(false, firstBoundaryMessage);
                return;
            }

            _commitInProgress = true;
            _customizationPanel?.SetBusy(true, "Verifying development receipt and local projection…");
            var selection = new FirstUserGameTestSelection(
                _sessionId,
                _identitySnapshot,
                customizationId,
                handle);
            FirstUserGameTestAdapterResult first = _adapter.CommitAndEvaluate(
                selection,
                firstHostReady,
                this);
            if (!first.CanEnterIsolatedCharacterGameTest)
            {
                _commitInProgress = false;
                _customizationPanel?.SetBusy(
                    false,
                    "Development verification failed: " + first.Failure + " / " +
                    first.AuthorityFailure + " / " + first.RoutePlan.Diagnostic);
                return;
            }

            byte[] authorityState = _adapter.CaptureAuthorityState();
            byte[] projectionState = _adapter.CaptureProjectionState();
            if (!FirstUserGameTestAdapter.TryRestore(
                    _sessionId,
                    authorityState,
                    projectionState,
                    out FirstUserGameTestAdapter restored,
                    out FirstUserGameTestAdapterFailure restoreFailure))
            {
                FailClosed("Development evidence restart verification failed: " + restoreFailure + ".");
                return;
            }

            if (!TryVerifyDevelopmentRuntimeBoundary(
                    out bool replayHostReady,
                    out string replayBoundaryMessage))
            {
                FailClosed(replayBoundaryMessage);
                return;
            }

            FirstUserGameTestAdapterResult replay = restored.CommitAndEvaluate(
                selection,
                replayHostReady,
                this);
            if (!replay.CanEnterIsolatedCharacterGameTest ||
                !first.Receipt.Handle.Equals(replay.Receipt.Handle) ||
                !first.Projection.Handle.Equals(replay.Projection.Handle))
            {
                FailClosed("Development evidence did not replay to the exact receipt and projection.");
                return;
            }

            _adapter = restored;
            _verifiedResult = replay;
            _destinationAuthorized = true;
            _destinationLoadRequestCount++;
            if (_destinationLoadRequestCount != 1)
            {
                FailClosed("The isolated destination was requested more than once.");
                return;
            }

            _driver.RunCoroutine(LoadIsolatedDestination());
        }

        private IEnumerator LoadIsolatedDestination()
        {
            if (_verifiedResult == null ||
                !IsDevelopmentWritable(
                    _verifiedResult.Receipt,
                    _verifiedResult.Projection))
            {
                FailClosed(
                    string.IsNullOrEmpty(_runtimeBoundaryFailure)
                        ? "The isolated runtime boundary drifted before destination load."
                        : _runtimeBoundaryFailure);
                yield break;
            }

            if (EditorBuildSettings.scenes.Any(scene =>
                    scene.enabled &&
                    string.Equals(scene.path, ChampionArenaPath, StringComparison.Ordinal)))
            {
                FailClosed("ChampionArena must remain excluded from production Build Settings.");
                yield break;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ChampionArenaPath) == null)
            {
                FailClosed("The exact isolated ChampionArena scene asset is unavailable.");
                yield break;
            }

            if (!string.Equals(
                    AssetDatabase.AssetPathToGUID(ChampionArenaPath),
                    ChampionArenaGuid,
                    StringComparison.Ordinal))
            {
                FailClosed("The isolated ChampionArena scene GUID did not match the canonical descriptor.");
                yield break;
            }

            AsyncOperation operation;
            try
            {
                operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ChampionArenaPath,
                    new LoadSceneParameters(LoadSceneMode.Single));
            }
            catch (Exception exception)
            {
                FailClosed("The isolated destination load threw " + exception.GetType().Name + ".");
                yield break;
            }

            if (operation == null)
            {
                FailClosed("The isolated destination load did not produce an operation.");
                yield break;
            }

            float started = Time.realtimeSinceStartup;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - started > DestinationLoadTimeoutSeconds)
                {
                    FailClosed("The isolated destination load timed out.");
                    yield break;
                }

                yield return null;
            }
        }

        public bool IsDevelopmentWritable(
            VerifiedDevelopmentReceipt receipt,
            VerifiedDevelopmentProjection projection)
        {
            bool hostReady = false;
            string message = string.Empty;
            if (receipt == null || !receipt.IsValid ||
                projection == null || !projection.IsValid ||
                !TryVerifyDevelopmentRuntimeBoundary(out hostReady, out message) ||
                !hostReady)
            {
                _runtimeBoundaryFailure = string.IsNullOrEmpty(message)
                    ? "Exact development receipt, projection, or host evidence was unavailable."
                    : message;
                return false;
            }

            if (!_developmentEvidenceBound)
            {
                _boundReceiptHandle = receipt.Handle;
                _boundProjectionHandle = projection.Handle;
                _developmentEvidenceBound = true;
            }
            else if (!_boundReceiptHandle.Equals(receipt.Handle) ||
                     !_boundProjectionHandle.Equals(projection.Handle))
            {
                _runtimeBoundaryFailure =
                    "Development receipt or local projection evidence changed after verification.";
                return false;
            }

            _runtimeBoundaryFailure = string.Empty;
            return true;
        }

        private bool TryVerifyDevelopmentRuntimeBoundary(
            out bool hostReady,
            out string message)
        {
            hostReady = EditorGameTestModeBootstrap.TryVerifyActiveRuntime(
                out EditorGameTestModeFailure failure,
                out message);
            if (!hostReady)
            {
                message = "Isolated runtime verification failed: " + failure + " / " + message;
                return false;
            }

            if (!EditorGameTestModeBootstrap.IsArmed ||
                !string.Equals(
                    EditorGameTestModeBootstrap.ActiveSessionId,
                    _sessionId,
                    StringComparison.Ordinal))
            {
                message = "The armed Game Test session changed during development verification.";
                return false;
            }

            string currentRoot;
            try
            {
                currentRoot = Path.GetFullPath(EditorGameTestModeBootstrap.ActiveSaveRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                message = "The active isolated save root became invalid.";
                return false;
            }

            StringComparison pathComparison =
                Application.platform == RuntimePlatform.WindowsEditor
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            if (string.IsNullOrEmpty(_boundIsolatedSaveRoot) ||
                !string.Equals(currentRoot, _boundIsolatedSaveRoot, pathComparison) ||
                !Directory.Exists(currentRoot))
            {
                message = "The active isolated save root no longer matches the armed session.";
                return false;
            }

            if (!ServiceLocator.TryGet<ISaveGameService>(out ISaveGameService registeredSave) ||
                registeredSave == null)
            {
                message = "The exact isolated save service is not registered.";
                return false;
            }

            if (_boundIsolatedSaveService == null)
            {
                _boundIsolatedSaveService = registeredSave;
            }
            else if (!ReferenceEquals(_boundIsolatedSaveService, registeredSave))
            {
                message = "The registered isolated save service instance changed.";
                return false;
            }

            if (registeredSave.LastLoadStatus != SaveLoadStatus.CreatedNew ||
                registeredSave.CurrentSave == null ||
                registeredSave.CurrentSave.SelectedRealm != RealmId.None)
            {
                message = "The isolated save service no longer exposes the exact fresh profile.";
                return false;
            }

            if (!(registeredSave is IProfileWriteAuthorityProvider productionProvider) ||
                ProfileWriteAuthorityProviderGuard.IsCurrentWritable(productionProvider))
            {
                message =
                    "Production profile write authority must remain explicitly non-writable in Game Test.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void BuildIsolatedDestination(Scene scene)
        {
            if (_destinationBuilt)
            {
                return;
            }

            int suppressed = DisableBehavioursInScene<ChampionArenaSceneController>(scene);
            string eventMessage = string.Empty;
            if (suppressed != 1 ||
                !ValidateSingleEventSystem(out eventMessage))
            {
                FailClosed(suppressed != 1
                    ? "The isolated destination expected exactly one dormant ChampionArena controller."
                    : eventMessage);
                return;
            }

            DestroyOwnedSelectionUi();
            var root = new GameObject(DestinationRootName);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "[AL DEV] Training Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            groundRenderer.material.color = new Color(0.08f, 0.11f, 0.14f, 1f);

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "[AL DEV] Player Champion";
            player.tag = "Player";
            player.transform.SetParent(root.transform, false);
            player.transform.position = new Vector3(0f, 1.1f, 0f);
            Collider primitiveCollider = player.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                UnityEngine.Object.Destroy(primitiveCollider);
            }

            ChampionController controller = player.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(_verifiedResult.Selection.Identity.Realm);
            ProceduralChampionModelBuilder.EnsureModel(player);

            var cameraObject = new GameObject("[AL DEV] Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 8.5f, -11.5f);
            cameraObject.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.024f, 0.035f, 1f);
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("[AL DEV] Directional Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;

            Canvas canvas = BuildDestinationHud(
                root.transform,
                controller,
                out Button attackButton);
            canvas.sortingOrder = 1800;

            _destinationMarker = root.AddComponent<FirstUserGameTestDestinationMarker>();
            _destinationMarker.Configure(_verifiedResult.Selection, controller, attackButton);
            _destinationBuilt = true;
            _commitInProgress = false;
        }

        private Canvas BuildDestinationHud(
            Transform parent,
            ChampionController controller,
            out Button attackButton)
        {
            var canvasObject = new GameObject(
                "FirstUserGameTestDestinationCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = BuiltInFont();
            Text disclosure = CreateText(
                canvasObject.transform,
                "IsolatedDestinationDisclosure",
                "DEVELOPMENT GAME TEST — MEMORY ONLY — NOT PRODUCTION / NOT SAVED",
                font,
                20,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                disclosure.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -10f),
                new Vector2(-20f, 44f));
            disclosure.color = new Color(1f, 0.72f, 0.22f, 1f);

            FirstUserGameTestSelection selection = _verifiedResult.Selection;
            Text summary = CreateText(
                canvasObject.transform,
                "IsolatedDestinationSummary",
                selection.Identity.Realm + " / " + selection.Identity.Race + " / " +
                selection.Identity.ClassFamily.Value + " / " + selection.CustomizationId +
                " / " + selection.DevelopmentHandle,
                font,
                18,
                TextAnchor.MiddleLeft);
            SetAnchoredRect(
                summary.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(18f, -56f),
                new Vector2(-36f, 36f));
            summary.color = new Color(0.80f, 0.88f, 1f, 1f);

            Text controls = CreateText(
                canvasObject.transform,
                "IsolatedDestinationControls",
                "Move: W/A/S/D, arrows, controller, or touch controls    Attack: left mouse or ATTACK",
                font,
                17,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                controls.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 12f),
                new Vector2(-20f, 34f));

            CreateMoveButton(canvasObject.transform, font, controller, "MoveLeft", "◀", new Vector2(56f, 104f), Vector2.left);
            CreateMoveButton(canvasObject.transform, font, controller, "MoveRight", "▶", new Vector2(168f, 104f), Vector2.right);
            CreateMoveButton(canvasObject.transform, font, controller, "MoveForward", "▲", new Vector2(112f, 160f), Vector2.up);
            CreateMoveButton(canvasObject.transform, font, controller, "MoveBack", "▼", new Vector2(112f, 48f), Vector2.down);

            attackButton = CreateButton(
                canvasObject.transform,
                "IsolatedBasicAttack",
                "ATTACK",
                font,
                new Vector2(-86f, 90f),
                new Vector2(148f, 72f),
                new Vector2(1f, 0f));
            attackButton.onClick.AddListener(controller.RequestBasicAttack);
            return canvas;
        }

        private void CreateMoveButton(
            Transform parent,
            Font font,
            ChampionController controller,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 direction)
        {
            Button button = CreateButton(
                parent,
                name,
                label,
                font,
                anchoredPosition,
                new Vector2(64f, 64f),
                Vector2.zero);
            button.gameObject.AddComponent<ChampionMoveButton>().Setup(controller, direction);
            button.onClick.AddListener(() => _driver.RunCoroutine(PulseMove(controller, direction)));
        }

        private static IEnumerator PulseMove(ChampionController controller, Vector2 direction)
        {
            if (controller == null)
            {
                yield break;
            }

            controller.SetExternalMoveInput(direction);
            float until = Time.unscaledTime + 0.18f;
            while (controller != null && Time.unscaledTime < until)
            {
                yield return null;
            }

            controller?.SetExternalMoveInput(Vector2.zero);
        }

        private bool TryLoadCustomizationChoices(
            out BodyPresetData[] bodyPresets,
            out string message)
        {
            bodyPresets = Array.Empty<BodyPresetData>();
            _approvedCustomizationIds.Clear();
            if (!TryLoadEditorCustomizationCatalog(out CharacterCustomizationCatalogData catalog) ||
                catalog == null || catalog.bodyPresets == null ||
                catalog.bodyPresets.Length == 0 ||
                catalog.bodyPresets.Length > MaximumBodyPresetChoices)
            {
                message = "The bounded packaged character customization catalog is unavailable.";
                return false;
            }

            bodyPresets = new BodyPresetData[catalog.bodyPresets.Length];
            for (int index = 0; index < catalog.bodyPresets.Length; index++)
            {
                BodyPresetData candidate = catalog.bodyPresets[index];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.displayName) ||
                    !FirstUserGameTestAdapter.IsCanonicalCustomizationId(candidate.id) ||
                    !_approvedCustomizationIds.Add(candidate.id))
                {
                    message = "The character customization catalog contains an invalid or duplicate body preset.";
                    bodyPresets = Array.Empty<BodyPresetData>();
                    _approvedCustomizationIds.Clear();
                    return false;
                }

                bodyPresets[index] = candidate;
            }

            message = string.Empty;
            return true;
        }

        private static bool TryLoadEditorCustomizationCatalog(
            out CharacterCustomizationCatalogData catalog)
        {
            catalog = null;
            string path;
            try
            {
                path = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    CustomizationCatalogFileName));
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                return false;
            }

            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 ||
                    file.Length > MaximumCustomizationCatalogBytes ||
                    (file.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length != file.Length ||
                    bytes.Length > MaximumCustomizationCatalogBytes)
                {
                    return false;
                }

                string json = StrictUtf8.GetString(bytes);
                catalog = JsonUtility.FromJson<CharacterCustomizationCatalogData>(json);
                return catalog?.bodyPresets != null &&
                       catalog.bodyPresets.Length > 0 &&
                       catalog.hairStyles != null &&
                       catalog.hairStyles.Length > 0 &&
                       catalog.armorStyles != null &&
                       catalog.armorStyles.Length > 0;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is DecoderFallbackException ||
                exception is ArgumentException)
            {
                catalog = null;
                return false;
            }
        }

        private void BuildPersistentDisclosure()
        {
            if (_disclosureCanvas != null)
            {
                return;
            }

            _disclosureCanvas = new GameObject(
                DisclosureObjectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            _disclosureCanvas.transform.SetParent(_driver.transform, false);
            Canvas canvas = _disclosureCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = _disclosureCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Text text = CreateText(
                _disclosureCanvas.transform,
                "PersistentGameTestDisclosure",
                "ISOLATED EDITOR GAME TEST — DEVELOPMENT EMULATOR — NO PRODUCTION AUTHORITY",
                BuiltInFont(),
                14,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                text.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 24f));
            text.color = new Color(1f, 0.76f, 0.30f, 1f);
            text.raycastTarget = false;
        }

        private void DestroyOwnedSelectionUi()
        {
            if (_identityPresenter != null)
            {
                _identityPresenter.CustomizationReady -= HandleCustomizationReady;
            }

            if (_identityCanvas != null)
            {
                UnityEngine.Object.Destroy(_identityCanvas);
            }

            if (_customizationPanel != null)
            {
                UnityEngine.Object.Destroy(_customizationPanel.gameObject);
            }

            _identityPresenter = null;
            _identityCanvas = null;
            _customizationPanel = null;
        }

        private void FailClosed(string message)
        {
            _lastFailure = string.IsNullOrWhiteSpace(message)
                ? "The isolated first-user Game Test failed closed."
                : message;
            _commitInProgress = false;
            _destinationAuthorized = false;
            DestroyOwnedSelectionUi();
            Debug.LogError("[AL-FIRST-USER-GAME-TEST-BLOCKED] " + _lastFailure);

            if (_initialized && EditorApplication.isPlaying)
            {
                BuildFailurePanel(_lastFailure);
            }
        }

        private void BuildFailurePanel(string message)
        {
            if (_failureCanvas != null)
            {
                return;
            }

            _failureCanvas = new GameObject(
                FailureCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = _failureCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            Text text = CreateText(
                _failureCanvas.transform,
                "FirstUserGameTestFailure",
                "ISOLATED GAME TEST BLOCKED\n\n" + message +
                "\n\nExit Play Mode. No production route or save was activated.",
                BuiltInFont(),
                24,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                text.rectTransform,
                new Vector2(0.08f, 0.18f),
                new Vector2(0.92f, 0.82f),
                Vector2.zero,
                Vector2.zero);
            text.color = new Color(1f, 0.55f, 0.42f, 1f);
        }

        private static int DisableBehavioursInScene<T>(Scene scene) where T : Behaviour
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T behaviour in root.GetComponentsInChildren<T>(true))
                {
                    behaviour.enabled = false;
                    count++;
                }
            }

            return count;
        }

        private static bool ValidateSingleEventSystem(out string message)
        {
            EventSystem[] eventSystems = UnityEngine.Object
                .FindObjectsOfType<EventSystem>(includeInactive: true)
                .Where(system => system.gameObject.activeInHierarchy)
                .ToArray();
            BaseInputModule[] modules = UnityEngine.Object
                .FindObjectsOfType<BaseInputModule>(includeInactive: true)
                .Where(module => module.gameObject.activeInHierarchy)
                .ToArray();
            if (eventSystems.Length != 1 || modules.Length != 1 ||
                modules[0].gameObject != eventSystems[0].gameObject)
            {
                message = "The isolated Game Test requires exactly one active EventSystem and input module.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        internal static Font BuiltInFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                        Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                throw new InvalidOperationException("A built-in uGUI font is required.");
            }

            return font;
        }

        internal static Text CreateText(
            Transform parent,
            string name,
            string value,
            Font font,
            int fontSize,
            TextAnchor alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.text = value;
            text.color = Color.white;
            return text;
        }

        internal static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Font font,
            Vector2 anchoredPosition,
            Vector2 size,
            Vector2 anchor)
        {
            var buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.34f, 0.96f);
            Text text = CreateText(
                buttonObject.transform,
                name + "Label",
                label,
                font,
                18,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                text.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            return buttonObject.GetComponent<Button>();
        }

        internal static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }

    internal sealed class FirstUserGameTestCustomizationPanel : MonoBehaviour
    {
        private readonly List<Button> _choiceButtons = new List<Button>();
        private Action<string, string> _confirmed;
        private Action _back;
        private string _selectedId = string.Empty;
        private InputField _handleInput;
        private Text _status;
        private Button _confirmButton;

        internal string SelectedCustomizationId => _selectedId;
        internal InputField HandleInput => _handleInput;
        internal Button ConfirmButton => _confirmButton;

        internal static FirstUserGameTestCustomizationPanel Create(
            IReadOnlyList<BodyPresetData> bodyPresets,
            FirstUserIdentityDraftSnapshot identity,
            Action<string, string> confirmed,
            Action back)
        {
            var canvasObject = new GameObject(
                FirstUserGameTestRuntimeHost.CustomizationCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var panel = canvasObject.AddComponent<FirstUserGameTestCustomizationPanel>();
            panel.Build(bodyPresets, identity, confirmed, back);
            return panel;
        }

        private void Build(
            IReadOnlyList<BodyPresetData> bodyPresets,
            FirstUserIdentityDraftSnapshot identity,
            Action<string, string> confirmed,
            Action back)
        {
            _confirmed = confirmed ?? throw new ArgumentNullException(nameof(confirmed));
            _back = back ?? throw new ArgumentNullException(nameof(back));
            Font font = FirstUserGameTestRuntimeHost.BuiltInFont();

            var backdropObject = new GameObject("CustomizationBackdrop", typeof(RectTransform), typeof(Image));
            backdropObject.transform.SetParent(transform, false);
            RectTransform backdrop = backdropObject.GetComponent<RectTransform>();
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                backdrop,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            backdropObject.GetComponent<Image>().color = new Color(0.012f, 0.018f, 0.028f, 1f);

            Text disclosure = FirstUserGameTestRuntimeHost.CreateText(
                transform,
                "CustomizationDisclosure",
                "DEVELOPMENT GAME TEST — SESSION ONLY — NO PRODUCTION USERNAME OR SAVE",
                font,
                18,
                TextAnchor.MiddleCenter);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                disclosure.rectTransform,
                new Vector2(0.08f, 0.88f),
                new Vector2(0.92f, 0.97f),
                Vector2.zero,
                Vector2.zero);
            disclosure.color = new Color(1f, 0.74f, 0.28f, 1f);

            Text heading = FirstUserGameTestRuntimeHost.CreateText(
                transform,
                "CustomizationHeading",
                "Choose a cosmetic body preset",
                font,
                30,
                TextAnchor.MiddleCenter);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                heading.rectTransform,
                new Vector2(0.08f, 0.78f),
                new Vector2(0.92f, 0.87f),
                Vector2.zero,
                Vector2.zero);

            Text identitySummary = FirstUserGameTestRuntimeHost.CreateText(
                transform,
                "IdentitySummary",
                identity.Realm + " → " + identity.Race + " • " + identity.ClassFamily.Value,
                font,
                20,
                TextAnchor.MiddleCenter);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                identitySummary.rectTransform,
                new Vector2(0.08f, 0.71f),
                new Vector2(0.92f, 0.78f),
                Vector2.zero,
                Vector2.zero);
            identitySummary.color = new Color(0.75f, 0.86f, 1f, 1f);

            var choicesObject = new GameObject(
                "CustomizationChoices",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            choicesObject.transform.SetParent(transform, false);
            RectTransform choicesRect = choicesObject.GetComponent<RectTransform>();
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                choicesRect,
                new Vector2(0.16f, 0.39f),
                new Vector2(0.84f, 0.70f),
                Vector2.zero,
                Vector2.zero);
            GridLayoutGroup grid = choicesObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(190f, 54f);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (int index = 0; index < bodyPresets.Count; index++)
            {
                BodyPresetData preset = bodyPresets[index];
                Button button = FirstUserGameTestRuntimeHost.CreateButton(
                    choicesObject.transform,
                    "CustomizationChoice_" + preset.id,
                    preset.displayName,
                    font,
                    Vector2.zero,
                    grid.cellSize,
                    new Vector2(0.5f, 0.5f));
                string capturedId = preset.id;
                button.onClick.AddListener(() => Select(capturedId));
                _choiceButtons.Add(button);
            }

            var inputObject = new GameObject(
                "DevelopmentHandleInput",
                typeof(RectTransform),
                typeof(Image),
                typeof(InputField));
            inputObject.transform.SetParent(transform, false);
            RectTransform inputRect = inputObject.GetComponent<RectTransform>();
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                inputRect,
                new Vector2(0.25f, 0.26f),
                new Vector2(0.75f, 0.34f),
                Vector2.zero,
                Vector2.zero);
            inputObject.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 1f);
            Text inputText = FirstUserGameTestRuntimeHost.CreateText(
                inputObject.transform,
                "DevelopmentHandleText",
                string.Empty,
                font,
                22,
                TextAnchor.MiddleLeft);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                inputText.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-24f, -8f));
            Text placeholder = FirstUserGameTestRuntimeHost.CreateText(
                inputObject.transform,
                "DevelopmentHandlePlaceholder",
                "Development handle (transport-only, not reserved)",
                font,
                18,
                TextAnchor.MiddleLeft);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                placeholder.rectTransform,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                new Vector2(-24f, -8f));
            placeholder.color = new Color(0.55f, 0.62f, 0.70f, 1f);
            _handleInput = inputObject.GetComponent<InputField>();
            _handleInput.textComponent = inputText;
            _handleInput.placeholder = placeholder;
            _handleInput.characterLimit = FirstUserGameTestAdapter.MaximumHandleCodeUnits;
            _handleInput.onValueChanged.AddListener(_ => Refresh());

            _status = FirstUserGameTestRuntimeHost.CreateText(
                transform,
                "DevelopmentAuthorityStatus",
                "Select a cosmetic preset and enter a development handle.",
                font,
                17,
                TextAnchor.MiddleCenter);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                _status.rectTransform,
                new Vector2(0.12f, 0.17f),
                new Vector2(0.88f, 0.24f),
                Vector2.zero,
                Vector2.zero);

            Button backButton = FirstUserGameTestRuntimeHost.CreateButton(
                transform,
                "BackToRealmAndClass",
                "Back",
                font,
                new Vector2(30f, 34f),
                new Vector2(170f, 58f),
                Vector2.zero);
            backButton.onClick.AddListener(() => _back());

            _confirmButton = FirstUserGameTestRuntimeHost.CreateButton(
                transform,
                "VerifyDevelopmentHandle",
                "Verify & Enter Isolated Test",
                font,
                new Vector2(-30f, 34f),
                new Vector2(300f, 58f),
                new Vector2(1f, 0f));
            _confirmButton.onClick.AddListener(Confirm);
            Refresh();

            if (_choiceButtons.Count > 0 && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_choiceButtons[0].gameObject);
            }
        }

        internal void SetBusy(bool busy, string status)
        {
            foreach (Button button in _choiceButtons)
            {
                button.interactable = !busy;
            }

            _handleInput.interactable = !busy;
            _confirmButton.interactable = !busy && CanConfirm();
            _status.text = status ?? string.Empty;
        }

        internal void SelectForTests(string id)
        {
            Select(id);
        }

        private void Select(string id)
        {
            _selectedId = id ?? string.Empty;
            Refresh();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_handleInput.gameObject);
            }
        }

        private void Confirm()
        {
            if (!CanConfirm())
            {
                Refresh();
                return;
            }

            _confirmed(_selectedId, _handleInput.text);
        }

        private void Refresh()
        {
            bool canConfirm = CanConfirm();
            _confirmButton.interactable = canConfirm;
            _status.text = string.IsNullOrEmpty(_selectedId)
                ? "Choose one catalog-backed cosmetic preset."
                : !FirstUserGameTestAdapter.IsValidDevelopmentHandle(_handleInput.text)
                    ? "Enter a 1–32 code-unit / 64-byte development handle without boundary whitespace or controls."
                    : "Ready to verify a DEVELOPMENT_EMULATOR_V1 receipt and local projection.";
        }

        private bool CanConfirm()
        {
            return !string.IsNullOrEmpty(_selectedId) &&
                   FirstUserGameTestAdapter.IsValidDevelopmentHandle(_handleInput.text);
        }
    }
}
