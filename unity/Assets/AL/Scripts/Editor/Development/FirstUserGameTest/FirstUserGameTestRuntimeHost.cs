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
using AL.Input;
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
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                FirstUserGameTestTutorialSessionStore.EraseSession(
                    SessionState.GetString(
                        EditorGameTestModeBootstrap.SessionIdKey,
                        string.Empty));
                return;
            }

            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            if (!FirstUserGameTestRuntimeHost.TrySuppressLegacyTechnicalBannerNow(
                    out string disclosureMessage))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    disclosureMessage);
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
            Button attackButton,
            Button moveForwardButton,
            FirstUserGameTestTutorialPresenter tutorialPresenter)
        {
            Selection = selection;
            Controller = controller;
            AttackButton = attackButton;
            MoveForwardButton = moveForwardButton;
            TutorialPresenter = tutorialPresenter;
            IsReady = selection != null && controller != null && attackButton != null &&
                      moveForwardButton != null && tutorialPresenter != null;
        }

        internal FirstUserGameTestSelection Selection { get; private set; }
        internal ChampionController Controller { get; private set; }
        internal Button AttackButton { get; private set; }
        internal Button MoveForwardButton { get; private set; }
        internal FirstUserGameTestTutorialPresenter TutorialPresenter { get; private set; }
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
        internal const string LegacyTechnicalBannerObjectName =
            "[AL] Isolated Game Test Mode";
        internal const string LegacyTechnicalBannerTypeName =
            "AL.Development.EditorGameTestModeBanner";
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
        private Text _progressBreadcrumb;
        private Button _exitButton;
        private FirstUserGameTestAdapter _adapter;
        private FirstUserGameTestAdapterResult _verifiedResult;
        private ISaveGameService _boundIsolatedSaveService;
        private bool _developmentEvidenceBound;
        private DevelopmentReceiptHandle _boundReceiptHandle;
        private DevelopmentProjectionHandle _boundProjectionHandle;
        private int _destinationLoadRequestCount;
        private FirstUserIdentityDraftSnapshot _identitySnapshot;
        private FirstUserGameTestDestinationMarker _destinationMarker;
        private FirstUserGameTestTutorialPresenter _tutorialPresenter;
        private EditorGameTestModeHostDriver _driver;
        private FirstUserGameTestPlaytestPhase _playtestPhase;
        private bool _exitRequested;
        private bool _technicalBannerSuppressed;

        internal static FirstUserGameTestRuntimeHost Active { get; private set; }

        internal FirstUserIdentityDraftPresenter IdentityPresenter => _identityPresenter;
        internal FirstUserGameTestCustomizationPanel CustomizationPanel => _customizationPanel;
        internal FirstUserGameTestDestinationMarker DestinationMarker => _destinationMarker;
        internal FirstUserGameTestTutorialPresenter TutorialPresenter => _tutorialPresenter;
        internal Text ProgressBreadcrumb => _progressBreadcrumb;
        internal Button ExitButton => _exitButton;
        internal FirstUserGameTestPlaytestPhase PlaytestPhase => _playtestPhase;
        internal string LastFailure => _lastFailure;
        internal int DestinationLoadRequestCount => _destinationLoadRequestCount;
        internal bool ExitRequested => _exitRequested;
        internal bool ReverifyVerifiedDevelopmentBoundaryForTests() =>
            _verifiedResult != null &&
            IsDevelopmentWritable(
                _verifiedResult.Receipt,
                _verifiedResult.Projection);

        internal bool RequestExitForTests(Action transition)
        {
            return TryRequestExit(transition);
        }

        internal static bool TrySuppressLegacyTechnicalBannerNow(out string message)
        {
            MonoBehaviour[] candidates = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(candidate =>
                    candidate != null &&
                    candidate.gameObject != null &&
                    string.Equals(
                        candidate.gameObject.name,
                        LegacyTechnicalBannerObjectName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.GetType().FullName,
                        LegacyTechnicalBannerTypeName,
                        StringComparison.Ordinal))
                .ToArray();
            if (candidates.Length == 0)
            {
                message = string.Empty;
                return true;
            }

            if (candidates.Length != 1)
            {
                message = "The isolated Editor disclosure boundary was ambiguous.";
                return false;
            }

            candidates[0].enabled = false;
            if (candidates[0].enabled)
            {
                message = "The isolated Editor disclosure boundary could not be secured.";
                return false;
            }

            message = string.Empty;
            return true;
        }

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

            _progressBreadcrumb = null;
            _exitButton = null;
            _tutorialPresenter = null;
            _destinationMarker = null;
            _exitRequested = false;
            _technicalBannerSuppressed = false;

            _initialized = false;
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
        }

        private void HandleTick()
        {
            if (!_initialized || _commitInProgress || _exitRequested)
            {
                return;
            }

            if (!TrySuppressLegacyTechnicalBanner())
            {
                return;
            }

            if (_destinationBuilt)
            {
                _tutorialPresenter?.Tick();
                SetPlaytestPhase(
                    _tutorialPresenter != null &&
                    _tutorialPresenter.State != null &&
                    _tutorialPresenter.State.IsOmenOffered
                        ? FirstUserGameTestPlaytestPhase.Omen
                        : FirstUserGameTestPlaytestPhase.WorldTutorial);
                if (IsCancelPressed())
                {
                    RequestExitIsolatedTest();
                }

                return;
            }

            if (!IsCancelPressed())
            {
                return;
            }

            if (_customizationPanel != null)
            {
                ReturnToIdentitySelection();
                return;
            }

            if (_identityPresenter != null &&
                _identityPresenter.CurrentDraft.Step ==
                FirstUserIdentityDraftStep.ClassFamily &&
                _identityPresenter.ReturnToRealmButton != null &&
                _identityPresenter.ReturnToRealmButton.interactable)
            {
                _identityPresenter.ReturnToRealmButton.onClick.Invoke();
                return;
            }

            RequestExitIsolatedTest();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_initialized || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            _technicalBannerSuppressed = false;
            if (!TrySuppressLegacyTechnicalBanner())
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
                SetPlaytestPhase(FirstUserGameTestPlaytestPhase.Loading);
                if (_destinationAuthorized || _verifiedResult != null)
                {
                    DisableBehavioursInScene<BootController>(scene);
                    FailClosed("Boot cannot be re-entered after development authority was verified.");
                }

                return;
            }

            if (string.Equals(scene.path, RealmSelectionPath, StringComparison.Ordinal))
            {
                SetPlaytestPhase(FirstUserGameTestPlaytestPhase.Identity);
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
            SetPlaytestPhase(FirstUserGameTestPlaytestPhase.Identity);
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
            SetPlaytestPhase(FirstUserGameTestPlaytestPhase.AppearanceAndName);
            _customizationPanel = FirstUserGameTestCustomizationPanel.Create(
                bodyPresets,
                snapshot,
                HandleCustomizationConfirmed,
                ReturnToIdentitySelection,
                _exitButton);
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
                Debug.LogWarning(
                    "[AL-FIRST-USER-GAME-TEST-WAITING] " + firstBoundaryMessage);
                _customizationPanel?.SetBusy(
                    false,
                    FirstUserGameTestPlaytestCopy.FriendlyBlockedStatus);
                return;
            }

            _commitInProgress = true;
            _customizationPanel?.SetBusy(
                true,
                FirstUserGameTestPlaytestCopy.PreparingWorld);
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
                Debug.LogError(
                    "[AL-FIRST-USER-GAME-TEST-BLOCKED] Development verification failed: " +
                    first.Failure + " / " + first.AuthorityFailure + " / " +
                    first.RoutePlan.Diagnostic);
                _customizationPanel?.SetBusy(
                    false,
                    FirstUserGameTestPlaytestCopy.FriendlyBlockedStatus);
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
            if (!FirstUserGameTestTutorialGeneration.TryCreate(
                    _sessionId,
                    _verifiedResult.Receipt,
                    _verifiedResult.Projection,
                    out string tutorialGeneration))
            {
                FailClosed("The exact development tutorial generation could not be derived.");
                return;
            }

            FirstUserGameTestTutorialSessionStore tutorialStore;
            try
            {
                tutorialStore = new FirstUserGameTestTutorialSessionStore(
                    _sessionId,
                    tutorialGeneration);
            }
            catch (ArgumentException)
            {
                FailClosed("The exact development tutorial state boundary was invalid.");
                return;
            }

            SetPlaytestPhase(FirstUserGameTestPlaytestPhase.WorldTutorial);
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
                tutorialStore,
                out Button attackButton,
                out Button moveForwardButton,
                out FirstUserGameTestTutorialPresenter tutorialPresenter,
                out string tutorialMessage);
            if (canvas == null || tutorialPresenter == null)
            {
                UnityEngine.Object.Destroy(root);
                FailClosed(string.IsNullOrEmpty(tutorialMessage)
                    ? "The development tutorial HUD could not be created."
                    : tutorialMessage);
                return;
            }

            canvas.sortingOrder = 1800;

            _destinationMarker = root.AddComponent<FirstUserGameTestDestinationMarker>();
            _tutorialPresenter = tutorialPresenter;
            _destinationMarker.Configure(
                _verifiedResult.Selection,
                controller,
                attackButton,
                moveForwardButton,
                tutorialPresenter);
            _destinationBuilt = true;
            _commitInProgress = false;
        }

        private Canvas BuildDestinationHud(
            Transform parent,
            ChampionController controller,
            FirstUserGameTestTutorialSessionStore tutorialStore,
            out Button attackButton,
            out Button moveForwardButton,
            out FirstUserGameTestTutorialPresenter tutorialPresenter,
            out string tutorialMessage)
        {
            attackButton = null;
            moveForwardButton = null;
            tutorialPresenter = null;
            tutorialMessage = string.Empty;
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
                FirstUserGameTestPlaytestCopy.NonProductionBadge,
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
            if (!FirstUserGameTestPlaytestCopy.TryDescribeIdentity(
                    selection.Identity,
                    out string identityDescription))
            {
                tutorialMessage = "The friendly identity summary was unavailable.";
                UnityEngine.Object.Destroy(canvasObject);
                return null;
            }

            Text summary = CreateText(
                canvasObject.transform,
                "IsolatedDestinationSummary",
                identityDescription + "  •  Appearance chosen",
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
                "Move with keys, controller, or touch  •  Use Basic Attack with the action key or on-screen button",
                font,
                17,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                controls.rectTransform,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 12f),
                new Vector2(-20f, 34f));

            if (!FirstUserGameTestTutorialPresenter.TryCreate(
                    canvasObject.transform,
                    font,
                    controller,
                    tutorialStore,
                    FailClosed,
                    out tutorialPresenter,
                    out tutorialMessage))
            {
                UnityEngine.Object.Destroy(canvasObject);
                return null;
            }

            FirstUserGameTestTutorialPresenter presenter = tutorialPresenter;
            Button moveLeftButton = CreateMoveButton(
                canvasObject.transform,
                font,
                controller,
                presenter,
                "MoveLeft",
                "◀",
                new Vector2(56f, 104f),
                Vector2.left);
            Button moveRightButton = CreateMoveButton(
                canvasObject.transform,
                font,
                controller,
                presenter,
                "MoveRight",
                "▶",
                new Vector2(168f, 104f),
                Vector2.right);
            moveForwardButton = CreateMoveButton(canvasObject.transform, font, controller, presenter, "MoveForward", "▲", new Vector2(112f, 160f), Vector2.up);
            Button moveBackButton = CreateMoveButton(
                canvasObject.transform,
                font,
                controller,
                presenter,
                "MoveBack",
                "▼",
                new Vector2(112f, 48f),
                Vector2.down);

            attackButton = CreateButton(
                canvasObject.transform,
                "IsolatedBasicAttack",
                "Basic Attack",
                font,
                new Vector2(-86f, 90f),
                new Vector2(148f, 72f),
                new Vector2(1f, 0f));
            attackButton.onClick.AddListener(() => presenter.RequestPlayerBasicAttack());
            ConfigureDestinationNavigation(
                moveLeftButton,
                moveRightButton,
                moveForwardButton,
                moveBackButton,
                attackButton,
                presenter.TitleAction,
                presenter.ObjectiveAction,
                _exitButton);
            presenter.BindNavigationActions(
                moveForwardButton,
                attackButton,
                _exitButton);
            return canvas;
        }

        private Button CreateMoveButton(
            Transform parent,
            Font font,
            ChampionController controller,
            FirstUserGameTestTutorialPresenter tutorialPresenter,
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
            button.onClick.AddListener(() =>
            {
                if (tutorialPresenter.RecordPlayerMovementIntent(direction))
                {
                    _driver.RunCoroutine(PulseMove(controller, direction));
                }
            });
            return button;
        }

        private static IEnumerator PulseMove(ChampionController controller, Vector2 direction)
        {
            if (controller == null)
            {
                yield break;
            }

            controller.SetExternalMoveInput(direction);
            float until = Time.unscaledTime + 0.18f;
            int completedFrames = 0;
            while (controller != null &&
                   (completedFrames < 2 || Time.unscaledTime < until))
            {
                yield return null;
                completedFrames++;
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
                FirstUserGameTestPlaytestCopy.NonProductionBadge,
                BuiltInFont(),
                14,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                text.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(-250f, 26f));
            text.color = new Color(1f, 0.76f, 0.30f, 1f);
            text.raycastTarget = false;

            _progressBreadcrumb = CreateText(
                _disclosureCanvas.transform,
                "FirstUserGameTestProgressBreadcrumb",
                FirstUserGameTestPlaytestCopy.Breadcrumb(
                    FirstUserGameTestPlaytestPhase.Loading),
                BuiltInFont(),
                15,
                TextAnchor.MiddleCenter);
            SetAnchoredRect(
                _progressBreadcrumb.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-110f, -28f),
                new Vector2(-300f, 32f));
            _progressBreadcrumb.color = new Color(0.84f, 0.90f, 1f, 1f);
            _progressBreadcrumb.raycastTarget = false;

            _exitButton = CreateButton(
                _disclosureCanvas.transform,
                "ExitIsolatedTest",
                FirstUserGameTestPlaytestCopy.ExitAction,
                BuiltInFont(),
                new Vector2(-16f, -8f),
                new Vector2(214f, 52f),
                Vector2.one);
            _exitButton.onClick.AddListener(RequestExitIsolatedTest);
            SetPlaytestPhase(FirstUserGameTestPlaytestPhase.Loading);
            TrySuppressLegacyTechnicalBanner();
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
                BuildFailurePanel();
            }
        }

        private void BuildFailurePanel()
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
                FirstUserGameTestPlaytestCopy.FriendlyFailurePanel,
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

        private static bool IsCancelPressed()
        {
            return GameInput.CancelPressed();
        }

        private bool TrySuppressLegacyTechnicalBanner()
        {
            if (_technicalBannerSuppressed)
            {
                return true;
            }

            if (!TrySuppressLegacyTechnicalBannerNow(out string message))
            {
                FailClosed(message);
                return false;
            }

            _technicalBannerSuppressed = true;
            return true;
        }

        private void SetPlaytestPhase(FirstUserGameTestPlaytestPhase phase)
        {
            if (phase == FirstUserGameTestPlaytestPhase.Invalid)
            {
                phase = FirstUserGameTestPlaytestPhase.Loading;
            }

            if (_playtestPhase == phase)
            {
                return;
            }

            _playtestPhase = phase;
            if (_progressBreadcrumb != null)
            {
                _progressBreadcrumb.text = FirstUserGameTestPlaytestCopy.Breadcrumb(phase);
            }
        }

        private void RequestExitIsolatedTest()
        {
            TryRequestExit(() => EditorApplication.isPlaying = false);
        }

        private bool TryRequestExit(Action transition)
        {
            if (_exitRequested || transition == null)
            {
                return false;
            }

            _exitRequested = true;
            if (_exitButton != null)
            {
                _exitButton.interactable = false;
                Text label = _exitButton.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = FirstUserGameTestPlaytestCopy.ExitingStatus;
                }
            }

            transition();
            return true;
        }

        private static void ConfigureDestinationNavigation(
            Button moveLeft,
            Button moveRight,
            Button moveForward,
            Button moveBack,
            Button attack,
            Button title,
            Button objective,
            Button exit)
        {
            SetExplicitNavigation(moveLeft, exit, moveForward, moveForward, moveBack);
            SetExplicitNavigation(moveForward, moveLeft, moveRight, exit, moveBack);
            SetExplicitNavigation(moveRight, moveForward, exit, exit, moveBack);
            SetExplicitNavigation(moveBack, moveLeft, moveRight, moveForward, attack);
            SetExplicitNavigation(attack, moveBack, exit, moveBack, exit);
            SetExplicitNavigation(title, moveForward, exit, exit, objective);
            SetExplicitNavigation(objective, moveForward, exit, title, attack);
            SetExplicitNavigation(exit, attack, moveLeft, objective, moveForward);
        }

        internal static void SetExplicitNavigation(
            Selectable selectable,
            Selectable left,
            Selectable right,
            Selectable up,
            Selectable down)
        {
            if (selectable == null)
            {
                return;
            }

            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = left;
            navigation.selectOnRight = right;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            selectable.navigation = navigation;
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
        private Button _backButton;

        internal string SelectedCustomizationId => _selectedId;
        internal InputField HandleInput => _handleInput;
        internal Button ConfirmButton => _confirmButton;
        internal Button BackButton => _backButton;
        internal Text Status => _status;
        internal IReadOnlyList<Button> ChoiceButtons => _choiceButtons;

        internal static FirstUserGameTestCustomizationPanel Create(
            IReadOnlyList<BodyPresetData> bodyPresets,
            FirstUserIdentityDraftSnapshot identity,
            Action<string, string> confirmed,
            Action back,
            Button exitButton)
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
            panel.Build(bodyPresets, identity, confirmed, back, exitButton);
            return panel;
        }

        private void Build(
            IReadOnlyList<BodyPresetData> bodyPresets,
            FirstUserIdentityDraftSnapshot identity,
            Action<string, string> confirmed,
            Action back,
            Button exitButton)
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
                FirstUserGameTestPlaytestCopy.NonProductionBadge,
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
                FirstUserGameTestPlaytestCopy.AppearanceHeading,
                font,
                30,
                TextAnchor.MiddleCenter);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                heading.rectTransform,
                new Vector2(0.08f, 0.78f),
                new Vector2(0.92f, 0.87f),
                Vector2.zero,
                Vector2.zero);

            if (!FirstUserGameTestPlaytestCopy.TryDescribeIdentity(
                    identity,
                    out string identityDescription))
            {
                throw new ArgumentException(
                    "A supported identity draft is required.",
                    nameof(identity));
            }

            Text identitySummary = FirstUserGameTestRuntimeHost.CreateText(
                transform,
                "IdentitySummary",
                identityDescription,
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
                FirstUserGameTestPlaytestCopy.NamePlaceholder,
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
                FirstUserGameTestPlaytestCopy.AppearancePrompt,
                font,
                17,
                TextAnchor.MiddleCenter);
            FirstUserGameTestRuntimeHost.SetAnchoredRect(
                _status.rectTransform,
                new Vector2(0.12f, 0.17f),
                new Vector2(0.88f, 0.24f),
                Vector2.zero,
                Vector2.zero);

            _backButton = FirstUserGameTestRuntimeHost.CreateButton(
                transform,
                "BackToRealmAndClass",
                "Back",
                font,
                new Vector2(30f, 34f),
                new Vector2(170f, 58f),
                Vector2.zero);
            _backButton.onClick.AddListener(() => _back());

            _confirmButton = FirstUserGameTestRuntimeHost.CreateButton(
                transform,
                "VerifyDevelopmentHandle",
                "Continue to World Tutorial",
                font,
                new Vector2(-30f, 34f),
                new Vector2(300f, 58f),
                new Vector2(1f, 0f));
            _confirmButton.onClick.AddListener(Confirm);
            ConfigureNavigation(exitButton);
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
                ? FirstUserGameTestPlaytestCopy.AppearanceRequired
                : !FirstUserGameTestAdapter.IsValidDevelopmentHandle(_handleInput.text)
                    ? FirstUserGameTestPlaytestCopy.NameRequired
                    : FirstUserGameTestPlaytestCopy.ReadyForTutorial;
        }

        private void ConfigureNavigation(Button exitButton)
        {
            for (int index = 0; index < _choiceButtons.Count; index++)
            {
                Button current = _choiceButtons[index];
                Button previous = index > 0 ? _choiceButtons[index - 1] : _backButton;
                Button next = index + 1 < _choiceButtons.Count
                    ? _choiceButtons[index + 1]
                    : _confirmButton;
                FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                    current,
                    previous,
                    next,
                    exitButton,
                    _handleInput);
            }

            Selectable firstChoice = _choiceButtons.Count > 0 ? _choiceButtons[0] : _backButton;
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                _handleInput,
                _backButton,
                _confirmButton,
                firstChoice,
                _confirmButton);
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                _backButton,
                exitButton,
                _confirmButton,
                _handleInput,
                exitButton);
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                _confirmButton,
                _backButton,
                exitButton,
                _handleInput,
                exitButton);
            FirstUserGameTestRuntimeHost.SetExplicitNavigation(
                exitButton,
                _confirmButton,
                firstChoice,
                _backButton,
                firstChoice);
        }

        private bool CanConfirm()
        {
            return !string.IsNullOrEmpty(_selectedId) &&
                   FirstUserGameTestAdapter.IsValidDevelopmentHandle(_handleInput.text);
        }
    }
}
