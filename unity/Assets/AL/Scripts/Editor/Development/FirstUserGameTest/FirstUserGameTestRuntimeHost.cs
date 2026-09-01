#if !UNITY_EDITOR
#error The isolated first-user Game Test host is Editor-only.
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
[assembly: InternalsVisibleTo("AL.Editor")]

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
                string sessionId = SessionState.GetString(
                    EditorGameTestModeBootstrap.SessionIdKey,
                    string.Empty);
                FirstUserGameTestOmenSessionStore.EraseSession(sessionId);
                FirstUserGameTestTutorialSessionStore.EraseSession(sessionId);
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
        IFirstUserGameTestDevelopmentWritableVerifier,
        IFirstUserGameTestMutationBoundary
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
        private const int MaximumCustomizationCatalogBytes = 256 * 1024;
        private const int MaximumEnvironmentFactoryComponents = 16384;
        private const int MaximumEnvironmentFactoryServices = 256;
        private const int MaximumEnvironmentFactorySerializedStateCodeUnits =
            8 * 1024 * 1024;
        private const float DestinationLoadTimeoutSeconds = 30f;
        private const string CustomizationCatalogFileName =
            "character_customization.v1.json";

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly FieldInfo ServiceLocatorServicesField =
            typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);

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
        private Button _failureExitButton;
        private EventSystem _failureEventSystem;
        private BaseInputModule _failureInputModule;
        private int _retainedFailureInputActivationGraceTicks;
        private FirstUserGameTestAdapter _adapter;
        private FirstUserGameTestAdapterResult _verifiedResult;
        private ISaveGameService _boundIsolatedSaveService;
        private bool _developmentEvidenceBound;
        private DevelopmentReceiptHandle _boundReceiptHandle;
        private DevelopmentProjectionHandle _boundProjectionHandle;
        private int _destinationLoadRequestCount;
        private FirstUserIdentityDraftSnapshot _identitySnapshot;
        private FirstUserGameTestCustomizationDraft _retainedCustomizationDraft;
        private FirstUserGameTestDestinationMarker _destinationMarker;
        private FirstUserGameTestTutorialPresenter _tutorialPresenter;
        private EditorGameTestModeHostDriver _driver;
        private FirstUserGameTestPlaytestPhase _playtestPhase;
        private bool _exitRequested;
        private FirstUserExitState _exitState;
        private bool _terminalFailure;
        private bool _technicalBannerSuppressed;
        private bool _focusSuspended;
        private int _focusEpoch = -1;
        private bool _focusResumeValidated;
        private bool _neutralInputFrameObserved;
        private bool _focusInputRestorePending;
        private int _focusInputRestoreActivationWaitFrames;
        private EventSystem _focusOwnedEventSystem;
        private bool _focusOwnedEventSystemWasEnabled;
        private BaseInputModule _focusOwnedInputModule;
        private bool _focusOwnedInputModuleWasEnabled;
        private GameObject _focusOwnedSelectedObject;
        private FocusResumeStateSnapshot _focusResumeStateSnapshot;
        private IFirstUserOnboardingEnvironmentLease _environmentLease;
        private IFirstUserOnboardingEnvironmentFactory _environmentFactory;
        private IFirstUserOnboardingAssetInventoryVerifier _environmentInventoryVerifier;
        private int _environmentGeneration;
        private GameObject _environmentOwnedRoot;
        private EnvironmentLeaseIdentity _environmentIdentity;
        private FirstUserGameTestEnemyAttackResolver _environmentAttackResolver;

        internal static FirstUserGameTestRuntimeHost Active { get; private set; }

        internal static bool TrySynchronizeFocusSuspension(
            EditorGameTestModeFocusSnapshot focus,
            out string message)
        {
            FirstUserGameTestRuntimeHost host = Active;
            if (host == null || !host._initialized || host._terminalFailure ||
                focus.State != EditorGameTestModeFocusState.Suspended ||
                !string.Equals(focus.SessionId, host._sessionId, StringComparison.Ordinal))
            {
                message =
                    "The exact isolated first-user host was unavailable for focus suspension.";
                return false;
            }

            try
            {
                return host.TrySynchronizeFocusSuspension(focus.Epoch, out message);
            }
            catch (Exception exception)
            {
                message =
                    "The isolated focus suspension threw " +
                    exception.GetType().Name + ".";
                return false;
            }
        }

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
        internal FirstUserExitState ExitState => _exitState;
        internal Button FailureExitButton => _failureExitButton;
        internal bool ReverifyRetainedFailureBoundaryForTests() =>
            TryVerifyRetainedFailureBoundary(out _);
        internal bool ReverifyVerifiedDevelopmentBoundaryForTests() =>
            _verifiedResult != null &&
            IsDevelopmentWritable(
                _verifiedResult.Receipt,
                _verifiedResult.Projection);
        internal bool ReverifyFocusContinuityForTests(out string message) =>
            TryValidateCapturedFocusResumeState(out _, out message);

        internal bool RequestExitForTests(
            Action transition,
            Func<bool> confirmation = null)
        {
            return IsExitCommandAvailable() &&
                   TryRequestExit(transition, confirmation ?? (() => true));
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
            if (_environmentLease != null)
            {
                if (!TryDisposeEnvironmentLease(
                        _environmentLease,
                        out string disposalMessage))
                {
                    Debug.LogError(
                        "[AL-FIRST-USER-GAME-TEST-CLEANUP] " + disposalMessage);
                }
                else
                {
                    ClearEnvironmentIdentity();
                }
            }

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
            _failureExitButton = null;
            _failureEventSystem = null;
            _failureInputModule = null;
            _retainedFailureInputActivationGraceTicks = 0;
            _tutorialPresenter = null;
            _destinationMarker = null;
            _retainedCustomizationDraft = default;
            _exitRequested = false;
            _exitState = FirstUserExitState.Inactive;
            _terminalFailure = false;
            _technicalBannerSuppressed = false;
            _focusSuspended = false;
            _focusEpoch = -1;
            _focusResumeValidated = false;
            _neutralInputFrameObserved = false;
            _focusInputRestorePending = false;
            _focusInputRestoreActivationWaitFrames = 0;
            _focusOwnedEventSystem = null;
            _focusOwnedEventSystemWasEnabled = false;
            _focusOwnedInputModule = null;
            _focusOwnedInputModuleWasEnabled = false;
            _focusOwnedSelectedObject = null;
            _focusResumeStateSnapshot = null;

            _initialized = false;
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }

            // If the scene owner was already destroyed, release immediately. Otherwise the
            // scene-unloaded callback releases it after teardown; retaining a live disabled
            // Bootloader until then is intentional and fail-closed.
            FirstUserIsolatedRuntimePolicy.TryForgetDestroyedSceneOwner(out _);
        }

        private void HandleTick()
        {
            if (!_initialized || _exitRequested)
            {
                return;
            }

            if (_terminalFailure)
            {
                if (!TryVerifyRetainedFailureBoundary(out string retainedMessage))
                {
                    _exitRequested = true;
                    EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                        "The retained failure boundary drifted: " + retainedMessage);
                    return;
                }

                if (IsCancelPressed())
                {
                    RequestExitIsolatedTest();
                }

                return;
            }

            if (!FirstUserIsolatedRuntimePolicy.TryAdvanceTickBoundary(
                    out bool productionTickBoundaryReady,
                    out string productionTickBoundaryMessage))
            {
                FailClosed(productionTickBoundaryMessage);
                return;
            }

            if (!productionTickBoundaryReady)
            {
                return;
            }

            if (!HandleFocusLifecycle())
            {
                return;
            }

            if (_commitInProgress)
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

        private readonly struct ResourceCountSnapshot
        {
            internal ResourceCountSnapshot(ResourceType type, long count)
            {
                Type = type;
                Count = count;
            }

            internal ResourceType Type { get; }
            internal long Count { get; }
        }

        private sealed class FocusResumeStateSnapshot
        {
            internal string SessionId;
            internal int FocusEpoch;
            internal int SceneHandle;
            internal string ScenePath;
            internal FirstUserGameTestPlaytestPhase Phase;
            internal EventSystem EventSystem;
            internal BaseInputModule InputModule;
            internal GameObject SelectedObject;
            internal bool CommitInProgress;
            internal bool DestinationAuthorized;
            internal bool DestinationBuilt;
            internal int DestinationLoadRequestCount;
            internal FirstUserIdentityDraftPresenter IdentityPresenter;
            internal FirstUserIdentityDraftSnapshot IdentityPresenterDraft;
            internal FirstUserIdentityDraftSnapshot CommittedIdentityDraft;
            internal FirstUserGameTestCustomizationPanel CustomizationPanel;
            internal FirstUserGameTestCustomizationDraft CustomizationDraft;
            internal FirstUserGameTestCustomizationDraft RetainedCustomizationDraft;
            internal FirstUserGameTestAdapterResult VerifiedResult;
            internal FirstUserGameTestSelection VerifiedSelection;
            internal FirstUserGameTestAdapter Adapter;
            internal byte[] AuthorityState;
            internal byte[] ProjectionState;
            internal bool DevelopmentEvidenceBound;
            internal DevelopmentReceiptHandle ReceiptHandle;
            internal DevelopmentProjectionHandle ProjectionHandle;
            internal DevelopmentReceiptHandle BoundReceiptHandle;
            internal DevelopmentProjectionHandle BoundProjectionHandle;
            internal ulong ReceiptGeneration;
            internal FirstUserGameTestTutorialPresenter TutorialPresenter;
            internal FirstUserGameTestTutorialState TutorialState;
            internal FirstUserGameTestOmenProjection OmenProjection;
            internal bool OmenDetailsOpen;
            internal FirstUserGameTestOmenInteraction OmenInteraction;
            internal object OmenView;
            internal object OmenSnapshot;
            internal bool OmenReportOpen;
            internal int OmenSelectInvocationCount;
            internal int OmenCommitAttemptCount;
            internal ChampionController Controller;
            internal Vector3 ControllerPosition;
            internal int ControllerAttackSequence;
            internal bool ControllerControlsLocked;
            internal bool ControllerIsAttacking;
            internal bool TutorialMovementIntentPending;
            internal FirstUserAttackProofState TutorialAttackProofState;
            internal bool TutorialAttackMechanicsConfirmed;
            internal ISaveGameService SaveService;
            internal string BoundIsolatedSaveRoot;
            internal string SaveSnapshot;
            internal IResourceService ResourceService;
            internal ResourceCountSnapshot[] ResourceCounts;
            internal IFirstUserOnboardingEnvironmentLease EnvironmentLease;
            internal EnvironmentLeaseIdentity EnvironmentIdentity;
            internal int EnvironmentGeneration;
            internal GameObject EnvironmentOwnedRoot;
        }

        private bool TryCaptureFocusResumeState(
            int focusEpoch,
            out FocusResumeStateSnapshot snapshot,
            out string message)
        {
            snapshot = null;
            message = string.Empty;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!TryGetSingleEventInputBoundary(
                    out EventSystem eventSystem,
                    out BaseInputModule inputModule,
                    out string inputBoundaryMessage) ||
                !activeScene.IsValid() || !activeScene.isLoaded ||
                (_focusSuspended &&
                 (!ReferenceEquals(eventSystem, _focusOwnedEventSystem) ||
                  !ReferenceEquals(inputModule, _focusOwnedInputModule) ||
                  (_focusInputRestorePending
                      ? !eventSystem.enabled ||
                        !inputModule.enabled ||
                        !ReferenceEquals(EventSystem.current, eventSystem) ||
                        !ReferenceEquals(
                            eventSystem.currentSelectedGameObject,
                            _focusOwnedSelectedObject)
                      : eventSystem.enabled ||
                        inputModule.enabled ||
                        EventSystem.current != null ||
                        eventSystem.currentSelectedGameObject != null))))
            {
                message = string.IsNullOrEmpty(inputBoundaryMessage)
                    ? "The exact scene or input owner was unavailable for focus retention."
                    : inputBoundaryMessage;
                return false;
            }

            FirstUserIdentityDraftSnapshot presenterDraft =
                _identityPresenter == null ? null : _identityPresenter.CurrentDraft;
            if (_identityPresenter != null && presenterDraft == null)
            {
                message = "The identity draft was unavailable for focus retention.";
                return false;
            }

            FirstUserGameTestCustomizationDraft customizationDraft =
                _customizationPanel == null
                    ? default
                    : _customizationPanel.CaptureDraft();

            FirstUserGameTestAdapter adapter = _adapter;
            byte[] authorityState;
            byte[] projectionState;
            try
            {
                authorityState = adapter == null ? null : adapter.CaptureAuthorityState();
                projectionState = adapter == null ? null : adapter.CaptureProjectionState();
            }
            catch (Exception exception)
            {
                message =
                    "The retained development authority state threw " +
                    exception.GetType().Name + ".";
                return false;
            }

            int maximumEnvelopeBytes =
                DevelopmentOnboardingAuthorityContracts.MaxRetainedEnvelopeBytes;
            if (adapter == null || authorityState == null || projectionState == null ||
                authorityState.Length > maximumEnvelopeBytes ||
                projectionState.Length > maximumEnvelopeBytes)
            {
                message = "The retained development authority state was invalid or oversized.";
                return false;
            }

            FirstUserGameTestSelection verifiedSelection =
                _verifiedResult == null ? null : _verifiedResult.Selection;
            DevelopmentReceiptHandle receiptHandle = default;
            DevelopmentProjectionHandle projectionHandle = default;
            ulong receiptGeneration = 0;
            if (_verifiedResult != null)
            {
                if (!_verifiedResult.CanEnterIsolatedCharacterGameTest ||
                    verifiedSelection == null ||
                    _verifiedResult.Receipt == null ||
                    _verifiedResult.Projection == null ||
                    _verifiedResult.Receipt.Receipt == null)
                {
                    message = "The verified development evidence was incomplete during focus retention.";
                    return false;
                }

                receiptHandle = _verifiedResult.Receipt.Handle;
                projectionHandle = _verifiedResult.Projection.Handle;
                receiptGeneration = _verifiedResult.Receipt.Receipt.CommittedGeneration;
            }

            FirstUserGameTestTutorialState tutorialState = null;
            FirstUserGameTestOmenProjection omenProjection = null;
            bool omenDetailsOpen = false;
            FirstUserGameTestOmenInteraction omenInteraction = null;
            object omenView = null;
            object omenSnapshot = null;
            bool omenReportOpen = false;
            int omenSelectInvocationCount = 0;
            int omenCommitAttemptCount = 0;
            ChampionController controller = null;
            Vector3 controllerPosition = default;
            int controllerAttackSequence = 0;
            bool controllerControlsLocked = false;
            bool controllerIsAttacking = false;
            bool tutorialMovementIntentPending = false;
            FirstUserAttackProofState tutorialAttackProofState =
                FirstUserAttackProofState.Invalid;
            bool tutorialAttackMechanicsConfirmed = false;
            if (_tutorialPresenter != null)
            {
                if (!_tutorialPresenter.TryCaptureRetainedState(
                        out tutorialState,
                        out message) ||
                    verifiedSelection == null || verifiedSelection.Identity == null ||
                    !FirstUserGameTestOmenContract.TryGetRealmId(
                        verifiedSelection.Identity.Realm,
                        out string realmId))
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        message = "The tutorial session identity was unavailable for focus retention.";
                    }

                    return false;
                }

                var omenStore = new FirstUserGameTestOmenSessionStore(
                    _sessionId,
                    tutorialState.Generation,
                    realmId);
                if (!omenStore.TryLoad(out omenProjection, out string omenDiagnostic))
                {
                    message = string.IsNullOrEmpty(omenDiagnostic)
                        ? "The retained OMEN projection was unavailable during focus retention."
                        : omenDiagnostic;
                    return false;
                }

                omenInteraction = _tutorialPresenter.OmenInteraction;
                controller = _destinationMarker == null
                    ? null
                    : _destinationMarker.Controller;
                if (omenInteraction == null || controller == null ||
                    !_tutorialPresenter.TryInspectChampionInputForTests(
                        out controllerControlsLocked,
                        out controllerIsAttacking))
                {
                    message = "The tutorial controller or OMEN boundary was unavailable during focus retention.";
                    return false;
                }

                omenDetailsOpen = _tutorialPresenter.OmenDetailsOpen;
                omenView = omenInteraction.View;
                omenSnapshot = omenInteraction.Snapshot;
                omenReportOpen = omenInteraction.IsReportOpen;
                omenSelectInvocationCount = omenInteraction.SelectValeriusInvocationCount;
                omenCommitAttemptCount = omenInteraction.CommitAttemptCount;
                controllerPosition = controller.transform.position;
                controllerAttackSequence = controller.EditorBasicAttackSequence;
                tutorialMovementIntentPending =
                    _tutorialPresenter.MovementIntentPendingForTests;
                tutorialAttackProofState =
                    _tutorialPresenter.AttackProofStateForTests;
                tutorialAttackMechanicsConfirmed =
                    _tutorialPresenter.AttackMechanicsConfirmedForTests;
            }
            else if (_destinationMarker != null)
            {
                message = "The destination marker existed without its tutorial state owner.";
                return false;
            }

            if (!ServiceLocator.TryGet<ISaveGameService>(out ISaveGameService saveService) ||
                saveService == null || saveService.CurrentSave == null ||
                !ServiceLocator.TryGet<IResourceService>(out IResourceService resourceService) ||
                resourceService == null)
            {
                message = "The isolated save or resource read boundary was unavailable.";
                return false;
            }

            string saveSnapshot;
            ResourceType[] resourceTypes =
                (ResourceType[])Enum.GetValues(typeof(ResourceType));
            var resourceCounts = new ResourceCountSnapshot[resourceTypes.Length];
            try
            {
                saveSnapshot = JsonUtility.ToJson(saveService.CurrentSave);
                if (string.IsNullOrEmpty(saveSnapshot) ||
                    saveSnapshot.Length > MaximumEnvironmentFactorySerializedStateCodeUnits)
                {
                    message = "The isolated save snapshot was empty or oversized.";
                    return false;
                }

                for (int index = 0; index < resourceTypes.Length; index++)
                {
                    resourceCounts[index] = new ResourceCountSnapshot(
                        resourceTypes[index],
                        resourceService.GetResourceCount(resourceTypes[index]));
                }
            }
            catch (Exception exception)
            {
                message =
                    "The isolated save or resource snapshot threw " +
                    exception.GetType().Name + ".";
                return false;
            }

            snapshot = new FocusResumeStateSnapshot
            {
                SessionId = _sessionId,
                FocusEpoch = focusEpoch,
                SceneHandle = activeScene.handle,
                ScenePath = activeScene.path ?? string.Empty,
                Phase = _playtestPhase,
                EventSystem = eventSystem,
                InputModule = inputModule,
                SelectedObject = _focusSuspended
                    ? _focusOwnedSelectedObject
                    : eventSystem.currentSelectedGameObject,
                CommitInProgress = _commitInProgress,
                DestinationAuthorized = _destinationAuthorized,
                DestinationBuilt = _destinationBuilt,
                DestinationLoadRequestCount = _destinationLoadRequestCount,
                IdentityPresenter = _identityPresenter,
                IdentityPresenterDraft = presenterDraft,
                CommittedIdentityDraft = _identitySnapshot,
                CustomizationPanel = _customizationPanel,
                CustomizationDraft = customizationDraft,
                RetainedCustomizationDraft = _retainedCustomizationDraft,
                VerifiedResult = _verifiedResult,
                VerifiedSelection = verifiedSelection,
                Adapter = adapter,
                AuthorityState = authorityState,
                ProjectionState = projectionState,
                DevelopmentEvidenceBound = _developmentEvidenceBound,
                ReceiptHandle = receiptHandle,
                ProjectionHandle = projectionHandle,
                BoundReceiptHandle = _boundReceiptHandle,
                BoundProjectionHandle = _boundProjectionHandle,
                ReceiptGeneration = receiptGeneration,
                TutorialPresenter = _tutorialPresenter,
                TutorialState = tutorialState,
                OmenProjection = omenProjection,
                OmenDetailsOpen = omenDetailsOpen,
                OmenInteraction = omenInteraction,
                OmenView = omenView,
                OmenSnapshot = omenSnapshot,
                OmenReportOpen = omenReportOpen,
                OmenSelectInvocationCount = omenSelectInvocationCount,
                OmenCommitAttemptCount = omenCommitAttemptCount,
                Controller = controller,
                ControllerPosition = controllerPosition,
                ControllerAttackSequence = controllerAttackSequence,
                ControllerControlsLocked = controllerControlsLocked,
                ControllerIsAttacking = controllerIsAttacking,
                TutorialMovementIntentPending = tutorialMovementIntentPending,
                TutorialAttackProofState = tutorialAttackProofState,
                TutorialAttackMechanicsConfirmed = tutorialAttackMechanicsConfirmed,
                SaveService = saveService,
                BoundIsolatedSaveRoot = _boundIsolatedSaveRoot,
                SaveSnapshot = saveSnapshot,
                ResourceService = resourceService,
                ResourceCounts = resourceCounts,
                EnvironmentLease = _environmentLease,
                EnvironmentIdentity = _environmentIdentity,
                EnvironmentGeneration = _environmentGeneration,
                EnvironmentOwnedRoot = _environmentOwnedRoot
            };
            return true;
        }

        private bool TryValidateCapturedFocusResumeState(
            out FirstUserResumeEvidence evidence,
            out string message)
        {
            evidence = default;
            message = string.Empty;
            FocusResumeStateSnapshot expected = _focusResumeStateSnapshot;
            FocusResumeStateSnapshot current = null;
            bool captured = false;
            try
            {
                captured = expected != null &&
                           TryCaptureFocusResumeState(
                               _focusEpoch,
                               out current,
                               out message);
            }
            catch (Exception exception)
            {
                message =
                    "The isolated focus continuity check threw " +
                    exception.GetType().Name + ".";
            }

            if (!captured)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "No exact pre-focus state was retained for resume.";
                }

                return false;
            }

            bool sessionMatches =
                string.Equals(expected.SessionId, current.SessionId, StringComparison.Ordinal) &&
                string.Equals(expected.SessionId, _sessionId, StringComparison.Ordinal);
            bool generationMatches =
                expected.ReceiptGeneration == current.ReceiptGeneration &&
                expected.EnvironmentGeneration == current.EnvironmentGeneration;
            bool phaseAndSceneMatch =
                expected.SceneHandle == current.SceneHandle &&
                string.Equals(expected.ScenePath, current.ScenePath, StringComparison.Ordinal) &&
                expected.Phase == current.Phase;
            bool identityAndCustomizationMatch =
                ReferenceEquals(expected.IdentityPresenter, current.IdentityPresenter) &&
                IdentityDraftsMatch(
                    expected.IdentityPresenterDraft,
                    current.IdentityPresenterDraft) &&
                IdentityDraftsMatch(
                    expected.CommittedIdentityDraft,
                    current.CommittedIdentityDraft) &&
                ReferenceEquals(expected.CustomizationPanel, current.CustomizationPanel) &&
                CustomizationDraftsMatch(
                    expected.CustomizationDraft,
                    current.CustomizationDraft) &&
                CustomizationDraftsMatch(
                    expected.RetainedCustomizationDraft,
                    current.RetainedCustomizationDraft);
            bool runtimeHostMatches =
                expected.CommitInProgress == current.CommitInProgress &&
                expected.DestinationAuthorized == current.DestinationAuthorized &&
                expected.DestinationBuilt == current.DestinationBuilt &&
                expected.DestinationLoadRequestCount == current.DestinationLoadRequestCount &&
                ReferenceEquals(expected.Adapter, current.Adapter) &&
                ReferenceEquals(expected.VerifiedResult, current.VerifiedResult);
            bool isolatedRootMatches = string.Equals(
                expected.BoundIsolatedSaveRoot,
                current.BoundIsolatedSaveRoot,
                Application.platform == RuntimePlatform.WindowsEditor
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
            bool profileServiceMatches =
                ReferenceEquals(expected.SaveService, current.SaveService) &&
                string.Equals(
                    expected.SaveSnapshot,
                    current.SaveSnapshot,
                    StringComparison.Ordinal) &&
                ReferenceEquals(expected.ResourceService, current.ResourceService) &&
                ResourceCountsMatch(expected.ResourceCounts, current.ResourceCounts);
            bool verifiedResultMatches =
                ReferenceEquals(expected.VerifiedResult, current.VerifiedResult);
            bool verifiedSelectionMatches = FirstUserSelectionsMatch(
                expected.VerifiedSelection,
                current.VerifiedSelection);
            bool authorityStateMatches =
                ByteArraysMatch(expected.AuthorityState, current.AuthorityState);
            bool projectionStateMatches =
                ByteArraysMatch(expected.ProjectionState, current.ProjectionState);
            bool evidenceAndHandlesMatch =
                expected.DevelopmentEvidenceBound == current.DevelopmentEvidenceBound &&
                ReceiptHandlesMatch(expected.ReceiptHandle, current.ReceiptHandle) &&
                ProjectionHandlesMatch(
                    expected.ProjectionHandle,
                    current.ProjectionHandle) &&
                ReceiptHandlesMatch(
                    expected.BoundReceiptHandle,
                    current.BoundReceiptHandle) &&
                ProjectionHandlesMatch(
                    expected.BoundProjectionHandle,
                    current.BoundProjectionHandle) &&
                expected.ReceiptGeneration == current.ReceiptGeneration;
            bool receiptAndProjectionMatch =
                verifiedResultMatches &&
                verifiedSelectionMatches &&
                authorityStateMatches &&
                projectionStateMatches &&
                evidenceAndHandlesMatch;

            bool tutorialAndOmenMatch =
                ReferenceEquals(expected.TutorialPresenter, current.TutorialPresenter) &&
                TutorialStatesMatch(expected.TutorialState, current.TutorialState) &&
                OmenProjectionsMatch(expected.OmenProjection, current.OmenProjection) &&
                expected.OmenDetailsOpen == current.OmenDetailsOpen &&
                ReferenceEquals(expected.OmenInteraction, current.OmenInteraction) &&
                ReferenceEquals(expected.OmenView, current.OmenView) &&
                ReferenceEquals(expected.OmenSnapshot, current.OmenSnapshot) &&
                expected.OmenReportOpen == current.OmenReportOpen &&
                expected.OmenSelectInvocationCount == current.OmenSelectInvocationCount &&
                expected.OmenCommitAttemptCount == current.OmenCommitAttemptCount &&
                ReferenceEquals(expected.Controller, current.Controller) &&
                expected.ControllerPosition.Equals(current.ControllerPosition) &&
                expected.ControllerAttackSequence == current.ControllerAttackSequence &&
                expected.ControllerControlsLocked == current.ControllerControlsLocked &&
                expected.ControllerIsAttacking == current.ControllerIsAttacking &&
                expected.TutorialMovementIntentPending ==
                    current.TutorialMovementIntentPending &&
                expected.TutorialAttackProofState == current.TutorialAttackProofState &&
                expected.TutorialAttackMechanicsConfirmed ==
                    current.TutorialAttackMechanicsConfirmed;

            bool eventSystemAndInputModuleMatch =
                ReferenceEquals(expected.EventSystem, current.EventSystem) &&
                ReferenceEquals(expected.InputModule, current.InputModule) &&
                ReferenceEquals(expected.SelectedObject, current.SelectedObject) &&
                current.EventSystem != null &&
                current.EventSystem.enabled == _focusInputRestorePending &&
                current.InputModule != null &&
                current.InputModule.enabled == _focusInputRestorePending;
            bool environmentLeaseMatches =
                ReferenceEquals(expected.EnvironmentLease, current.EnvironmentLease) &&
                ReferenceEquals(expected.EnvironmentIdentity, current.EnvironmentIdentity) &&
                ReferenceEquals(expected.EnvironmentOwnedRoot, current.EnvironmentOwnedRoot) &&
                expected.EnvironmentGeneration == current.EnvironmentGeneration;

            evidence = new FirstUserResumeEvidence(
                sessionMatches,
                generationMatches,
                phaseAndSceneMatch,
                runtimeHostMatches,
                identityAndCustomizationMatch,
                isolatedRootMatches,
                profileServiceMatches,
                productionProfileRemainsNonWritable: true,
                productionTickSuppressed: true,
                receiptAndProjectionMatch: receiptAndProjectionMatch,
                tutorialAndOmenMatch: tutorialAndOmenMatch,
                eventSystemAndInputModuleMatch: eventSystemAndInputModuleMatch,
                environmentLeaseMatches: environmentLeaseMatches);
            if (!evidence.IsExact)
            {
                message =
                    "The isolated first-user state changed while focus was suspended. " +
                    "Session=" + sessionMatches +
                    ", Generation=" + generationMatches +
                    ", PhaseScene=" + phaseAndSceneMatch +
                    ", RuntimeHost=" + runtimeHostMatches +
                    ", IdentityCustomization=" + identityAndCustomizationMatch +
                    ", Root=" + isolatedRootMatches +
                    ", ProfileResources=" + profileServiceMatches +
                    ", ReceiptProjection=" + receiptAndProjectionMatch +
                    "[Result=" + verifiedResultMatches +
                    ", Selection=" + verifiedSelectionMatches +
                    ", Authority=" + authorityStateMatches +
                    ", Projection=" + projectionStateMatches +
                    ", Handles=" + evidenceAndHandlesMatch + "]" +
                    ", TutorialOmen=" + tutorialAndOmenMatch +
                    ", Input=" + eventSystemAndInputModuleMatch +
                    ", Environment=" + environmentLeaseMatches + ".";
                return false;
            }

            return true;
        }

        private static bool ByteArraysMatch(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return left == null && right == null;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReceiptHandlesMatch(
            DevelopmentReceiptHandle left,
            DevelopmentReceiptHandle right)
        {
            bool leftAbsent =
                string.IsNullOrEmpty(left.AuthorityInstanceId) &&
                string.IsNullOrEmpty(left.ContractVersion) &&
                string.IsNullOrEmpty(left.ReceiptId) &&
                !left.BodyDigest.IsValid;
            bool rightAbsent =
                string.IsNullOrEmpty(right.AuthorityInstanceId) &&
                string.IsNullOrEmpty(right.ContractVersion) &&
                string.IsNullOrEmpty(right.ReceiptId) &&
                !right.BodyDigest.IsValid;
            return (leftAbsent && rightAbsent) || left.Equals(right);
        }

        private static bool ProjectionHandlesMatch(
            DevelopmentProjectionHandle left,
            DevelopmentProjectionHandle right)
        {
            bool leftAbsent =
                string.IsNullOrEmpty(left.ProjectionInstanceId) &&
                string.IsNullOrEmpty(left.ContractVersion) &&
                string.IsNullOrEmpty(left.MarkerId) &&
                !left.MarkerDigest.IsValid;
            bool rightAbsent =
                string.IsNullOrEmpty(right.ProjectionInstanceId) &&
                string.IsNullOrEmpty(right.ContractVersion) &&
                string.IsNullOrEmpty(right.MarkerId) &&
                !right.MarkerDigest.IsValid;
            return (leftAbsent && rightAbsent) || left.Equals(right);
        }

        private static bool IdentityDraftsMatch(
            FirstUserIdentityDraftSnapshot left,
            FirstUserIdentityDraftSnapshot right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            return left.Step == right.Step && left.Realm == right.Realm &&
                   left.Race == right.Race && left.ClassFamily == right.ClassFamily;
        }

        private static bool CustomizationDraftsMatch(
            FirstUserGameTestCustomizationDraft left,
            FirstUserGameTestCustomizationDraft right)
        {
            return string.Equals(
                       left.CustomizationId,
                       right.CustomizationId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.DevelopmentHandle,
                       right.DevelopmentHandle,
                       StringComparison.Ordinal);
        }

        private static bool FirstUserSelectionsMatch(
            FirstUserGameTestSelection left,
            FirstUserGameTestSelection right)
        {
            if (left == null || right == null)
            {
                return left == null && right == null;
            }

            return string.Equals(left.SessionId, right.SessionId, StringComparison.Ordinal) &&
                   IdentityDraftsMatch(left.Identity, right.Identity) &&
                   string.Equals(
                       left.CustomizationId,
                       right.CustomizationId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       left.DevelopmentHandle,
                       right.DevelopmentHandle,
                       StringComparison.Ordinal);
        }

        private static bool TutorialStatesMatch(
            FirstUserGameTestTutorialState left,
            FirstUserGameTestTutorialState right)
        {
            return left == null || right == null
                ? left == null && right == null
                : left.ValueEquals(right);
        }

        private static bool OmenProjectionsMatch(
            FirstUserGameTestOmenProjection left,
            FirstUserGameTestOmenProjection right)
        {
            return left == null || right == null
                ? left == null && right == null
                : left.ValueEquals(right);
        }

        private static bool ResourceCountsMatch(
            ResourceCountSnapshot[] left,
            ResourceCountSnapshot[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return left == null && right == null;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index].Type != right[index].Type ||
                    left[index].Count != right[index].Count)
                {
                    return false;
                }
            }

            return true;
        }

        private bool HandleFocusLifecycle()
        {
            EditorGameTestModeFocusSnapshot focus =
                EditorGameTestModeBootstrap.FocusSnapshot;
            if (!string.Equals(focus.SessionId, _sessionId, StringComparison.Ordinal))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "The isolated focus session changed");
                return false;
            }

            switch (focus.State)
            {
                case EditorGameTestModeFocusState.Active:
                    if (_focusSuspended)
                    {
                        EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                            "Focus became active before the isolated input boundary was restored");
                        return false;
                    }

                    return true;
                case EditorGameTestModeFocusState.Suspended:
                    return SuspendForFocusLoss(focus.Epoch);
                case EditorGameTestModeFocusState.ResumePending:
                    return BeginFocusResume(focus.Epoch);
                case EditorGameTestModeFocusState.AwaitingNeutralInput:
                    return CompleteFocusResume(focus.Epoch);
                case EditorGameTestModeFocusState.FailClosed:
                case EditorGameTestModeFocusState.Inactive:
                default:
                    return false;
            }
        }

        private bool SuspendForFocusLoss(int epoch)
        {
            if (!TrySynchronizeFocusSuspension(epoch, out string message))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(message);
            }

            return false;
        }

        private bool TrySynchronizeFocusSuspension(int epoch, out string message)
        {
            message = string.Empty;
            if (_focusSuspended)
            {
                if (_focusInputRestorePending)
                {
                    if (!TryApplyOwnedFocusInputSuspension(out message))
                    {
                        return false;
                    }

                    _focusInputRestorePending = false;
                    _focusInputRestoreActivationWaitFrames = 0;
                }

                if (epoch < _focusEpoch)
                {
                    message = "The isolated focus suspension epoch moved backwards.";
                    return false;
                }

                if (_focusOwnedEventSystem == null ||
                    _focusOwnedEventSystem.enabled ||
                    _focusOwnedInputModule == null ||
                    _focusOwnedInputModule.enabled ||
                    _focusOwnedEventSystem.currentSelectedGameObject != null)
                {
                    message = "The isolated EventSystem suspension ownership changed.";
                    return false;
                }

                if (epoch == _focusEpoch)
                {
                    return true;
                }

                if (_focusResumeStateSnapshot == null ||
                    !TryValidateCapturedFocusResumeState(
                        out FirstUserResumeEvidence retainedEvidence,
                        out message) ||
                    !retainedEvidence.IsExact)
                {
                    if (string.IsNullOrEmpty(message))
                    {
                        message =
                            "The original isolated focus continuity baseline was unavailable.";
                    }

                    return false;
                }

                _focusEpoch = epoch;
                _focusResumeValidated = false;
                _neutralInputFrameObserved = false;

                return true;
            }

            if (!TryGetSingleEventInputBoundary(
                    out EventSystem eventSystem,
                    out BaseInputModule inputModule,
                    out message) ||
                !ReferenceEquals(EventSystem.current, eventSystem) ||
                !eventSystem.enabled ||
                !inputModule.enabled)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message =
                        "The isolated EventSystem boundary was unavailable during focus suspension.";
                }

                return false;
            }

            _focusOwnedEventSystem = eventSystem;
            _focusOwnedEventSystemWasEnabled = eventSystem.enabled;
            _focusOwnedInputModule = inputModule;
            _focusOwnedInputModuleWasEnabled = inputModule.enabled;
            _focusOwnedSelectedObject = eventSystem.currentSelectedGameObject;
            if (!TryApplyOwnedFocusInputSuspension(out message))
            {
                return false;
            }

            _focusSuspended = true;
            _focusEpoch = epoch;
            _focusResumeValidated = false;
            _neutralInputFrameObserved = false;
            _focusInputRestorePending = false;
            _focusInputRestoreActivationWaitFrames = 0;
            _tutorialPresenter?.SetFocusSuspended(true);
            if (_terminalFailure ||
                !TryCaptureFocusResumeState(
                    epoch,
                    out _focusResumeStateSnapshot,
                    out message))
            {
                _focusResumeStateSnapshot = null;
                if (string.IsNullOrEmpty(message))
                {
                    message =
                        "The isolated first-user state could not be retained at focus loss.";
                }

                return false;
            }

            if (_progressBreadcrumb != null)
            {
                _progressBreadcrumb.text =
                    "Playtest suspended — return to the Editor to continue safely.";
            }

            return true;
        }

        private bool TryApplyOwnedFocusInputSuspension(out string message)
        {
            message = string.Empty;
            if (_focusOwnedEventSystem == null || _focusOwnedInputModule == null)
            {
                message = "The isolated EventSystem ownership was unavailable.";
                return false;
            }

            _focusOwnedEventSystem.SetSelectedGameObject(null);
            _focusOwnedEventSystem.enabled = false;
            _focusOwnedInputModule.enabled = false;
            if (_focusOwnedEventSystem.enabled ||
                _focusOwnedInputModule.enabled ||
                EventSystem.current != null ||
                _focusOwnedEventSystem.currentSelectedGameObject != null)
            {
                message = "The isolated EventSystem could not be suspended.";
                return false;
            }

            return true;
        }

        private bool BeginFocusResume(int epoch)
        {
            if (!_focusSuspended)
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "Focus returned without a synchronous isolated suspension baseline");
                return false;
            }

            if (!_focusSuspended || _focusEpoch != epoch ||
                _focusOwnedEventSystem == null ||
                _focusOwnedEventSystem.enabled ||
                _focusOwnedInputModule == null ||
                _focusOwnedInputModule.enabled ||
                _focusOwnedEventSystem.currentSelectedGameObject != null)
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "The isolated focus suspension ownership changed before resume");
                return false;
            }

            if (_focusResumeValidated)
            {
                return false;
            }

            if (!TryValidateFocusResumeBoundary(
                    out bool ready,
                    out string validationMessage))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    validationMessage);
                return false;
            }

            if (!ready)
            {
                return false;
            }

            if (!EditorGameTestModeBootstrap.TryMarkFocusResumeValidated(
                    _sessionId,
                    epoch,
                    out string resumeMessage))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(resumeMessage);
                return false;
            }

            _focusResumeValidated = true;
            return false;
        }

        private bool CompleteFocusResume(int epoch)
        {
            if (!_focusSuspended || !_focusResumeValidated || _focusEpoch != epoch)
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "The isolated focus resume completion was stale");
                return false;
            }

            if (_focusInputRestorePending)
            {
                return CompleteFocusInputRestoration(epoch);
            }

            bool neutral = AreGameplayInputsNeutral();
            if (!neutral)
            {
                _neutralInputFrameObserved = false;
                EditorGameTestModeBootstrap.TryCompleteFocusResume(
                    _sessionId,
                    epoch,
                    allGameplayInputNeutral: false,
                    out _);
                return false;
            }

            if (!_neutralInputFrameObserved)
            {
                _neutralInputFrameObserved = true;
                return false;
            }

            if (!TryValidateFocusResumeBoundary(
                    out bool boundaryReady,
                    out string boundaryMessage))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    boundaryMessage);
                return false;
            }

            if (!boundaryReady)
            {
                return false;
            }

            if (!TryBeginFocusInputRestoration(out string restorationMessage))
            {
                TryApplyOwnedFocusInputSuspension(out _);
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    restorationMessage);
                return false;
            }

            _focusInputRestorePending = true;
            _focusInputRestoreActivationWaitFrames = 0;
            _neutralInputFrameObserved = false;
            return false;
        }

        private bool TryBeginFocusInputRestoration(out string message)
        {
            message = string.Empty;
            if (_focusOwnedEventSystem == null || _focusOwnedInputModule == null)
            {
                message = "The isolated EventSystem ownership was unavailable for restoration.";
                return false;
            }

            if (_focusOwnedInputModuleWasEnabled)
            {
                _focusOwnedInputModule.enabled = true;
            }

            if (_focusOwnedEventSystemWasEnabled)
            {
                _focusOwnedEventSystem.enabled = true;
            }

            if ((_focusOwnedInputModuleWasEnabled &&
                 !_focusOwnedInputModule.enabled) ||
                (_focusOwnedEventSystemWasEnabled &&
                 !_focusOwnedEventSystem.enabled))
            {
                message =
                    "The isolated EventSystem could not enter its validated restoration phase.";
                return false;
            }

            EventSystem.current = _focusOwnedEventSystem;
            _focusOwnedEventSystem.SetSelectedGameObject(
                _focusOwnedSelectedObject);
            if (!ReferenceEquals(EventSystem.current, _focusOwnedEventSystem) ||
                !ReferenceEquals(
                    _focusOwnedEventSystem.currentSelectedGameObject,
                    _focusOwnedSelectedObject))
            {
                message = "The isolated UI focus owner could not begin exact restoration.";
                return false;
            }

            return true;
        }

        private bool CompleteFocusInputRestoration(int epoch)
        {
            if (!AreGameplayInputsNeutral())
            {
                if (!TryApplyOwnedFocusInputSuspension(
                        out string neutralGateMessage))
                {
                    _focusInputRestorePending = false;
                    _focusInputRestoreActivationWaitFrames = 0;
                    _neutralInputFrameObserved = false;
                    EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                        neutralGateMessage);
                    return false;
                }

                _focusInputRestorePending = false;
                _focusInputRestoreActivationWaitFrames = 0;
                _neutralInputFrameObserved = false;
                EditorGameTestModeBootstrap.TryCompleteFocusResume(
                    _sessionId,
                    epoch,
                    allGameplayInputNeutral: false,
                    out _);
                return false;
            }

            bool inputModuleActivationPending =
                _focusOwnedEventSystem != null &&
                ReferenceEquals(EventSystem.current, _focusOwnedEventSystem) &&
                _focusOwnedEventSystem.enabled &&
                _focusOwnedInputModule != null &&
                _focusOwnedInputModule.enabled &&
                _focusOwnedEventSystem.currentInputModule == null;
            if (inputModuleActivationPending &&
                _focusInputRestoreActivationWaitFrames == 0)
            {
                if (!TryValidateFocusResumeBoundary(
                        out bool pendingBoundaryReady,
                        out string pendingBoundaryMessage,
                        allowInputModuleActivationPending: true) ||
                    !pendingBoundaryReady)
                {
                    if (!TryApplyOwnedFocusInputSuspension(
                            out string pendingGateMessage))
                    {
                        _focusInputRestorePending = false;
                        _focusInputRestoreActivationWaitFrames = 0;
                        _neutralInputFrameObserved = false;
                        EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                            pendingGateMessage);
                        return false;
                    }

                    _focusInputRestorePending = false;
                    _focusInputRestoreActivationWaitFrames = 0;
                    _neutralInputFrameObserved = false;
                    if (!string.IsNullOrEmpty(pendingBoundaryMessage))
                    {
                        EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                            pendingBoundaryMessage);
                    }

                    return false;
                }

                // A newly re-enabled EventSystem is not added to the current
                // frame's Update list. Its first following Update activates the
                // exact module with changedModule=true and therefore processes
                // no command. The early host tick may wait for that one frame,
                // then it must observe the exact active module or fail closed.
                _focusInputRestoreActivationWaitFrames = 1;
                return false;
            }

            if (inputModuleActivationPending)
            {
                TryApplyOwnedFocusInputSuspension(out _);
                _focusInputRestorePending = false;
                _focusInputRestoreActivationWaitFrames = 0;
                _neutralInputFrameObserved = false;
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "The isolated input module did not activate within its single safe restoration frame");
                return false;
            }

            if (!TryValidateFocusResumeBoundary(
                    out bool boundaryReady,
                    out string boundaryMessage))
            {
                TryApplyOwnedFocusInputSuspension(out _);
                _focusInputRestorePending = false;
                _focusInputRestoreActivationWaitFrames = 0;
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    boundaryMessage);
                return false;
            }

            if (!boundaryReady)
            {
                if (!TryApplyOwnedFocusInputSuspension(
                        out string boundaryGateMessage))
                {
                    _focusInputRestorePending = false;
                    _focusInputRestoreActivationWaitFrames = 0;
                    _neutralInputFrameObserved = false;
                    EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                        boundaryGateMessage);
                    return false;
                }

                _focusInputRestorePending = false;
                _focusInputRestoreActivationWaitFrames = 0;
                _neutralInputFrameObserved = false;
                return false;
            }

            if (!EditorGameTestModeBootstrap.TryCompleteFocusResume(
                    _sessionId,
                    epoch,
                    allGameplayInputNeutral: true,
                    out string message))
            {
                TryApplyOwnedFocusInputSuspension(out _);
                _focusInputRestorePending = false;
                _focusInputRestoreActivationWaitFrames = 0;
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(message);
                return false;
            }

            _tutorialPresenter?.SetFocusSuspended(false);
            if (_terminalFailure)
            {
                _tutorialPresenter?.SetFocusSuspended(true);
                TryApplyOwnedFocusInputSuspension(out _);
                _focusInputRestorePending = false;
                _focusInputRestoreActivationWaitFrames = 0;
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "The isolated tutorial input boundary failed during focus restoration");
                return false;
            }

            _focusSuspended = false;
            _focusResumeValidated = false;
            _neutralInputFrameObserved = false;
            _focusInputRestorePending = false;
            _focusInputRestoreActivationWaitFrames = 0;
            _focusOwnedEventSystem = null;
            _focusOwnedEventSystemWasEnabled = false;
            _focusOwnedInputModule = null;
            _focusOwnedInputModuleWasEnabled = false;
            _focusOwnedSelectedObject = null;
            _focusResumeStateSnapshot = null;
            if (_progressBreadcrumb != null)
            {
                _progressBreadcrumb.text =
                    FirstUserGameTestPlaytestCopy.Breadcrumb(_playtestPhase);
            }

            return true;
        }

        private bool TryValidateFocusResumeBoundary(
            out bool ready,
            out string message,
            bool allowInputModuleActivationPending = false)
        {
            ready = false;
            EventSystem focusEventSystem = _focusOwnedEventSystem;
            GameObject selectedBeforeProviderVerification =
                focusEventSystem == null
                    ? null
                    : focusEventSystem.currentSelectedGameObject;
            bool boundaryValid = TryVerifyDevelopmentRuntimeBoundary(
                out bool hostReady,
                out string boundaryMessage);
            bool policyValid = FirstUserIsolatedRuntimePolicy.TryAdvanceAndVerify(
                out bool policyReady,
                out string policyMessage);
            bool destinationValid = !_destinationBuilt ||
                                    (_destinationMarker != null &&
                                     _destinationMarker.IsReady &&
                                     _tutorialPresenter != null);
            string environmentMessage = string.Empty;
            bool environmentValid = !_destinationBuilt ||
                                    TryRevalidateAuthoredEnvironment(
                                        out environmentMessage);
            bool continuityValid = TryValidateCapturedFocusResumeState(
                out FirstUserResumeEvidence continuity,
                out string continuityMessage);
            bool hasExactInputBoundary = TryGetSingleEventInputBoundary(
                out EventSystem exactEventSystem,
                out BaseInputModule exactInputModule,
                out string inputBoundaryMessage);
            bool inputValid = focusEventSystem != null &&
                              hasExactInputBoundary &&
                              ReferenceEquals(
                                  exactEventSystem,
                                  focusEventSystem) &&
                              ReferenceEquals(exactInputModule, _focusOwnedInputModule) &&
                              ReferenceEquals(
                                  focusEventSystem.currentSelectedGameObject,
                                  selectedBeforeProviderVerification) &&
                              (_focusInputRestorePending
                                  ? ReferenceEquals(
                                        EventSystem.current,
                                        focusEventSystem) &&
                                    focusEventSystem.enabled &&
                                    _focusOwnedInputModule.enabled &&
                                    (ReferenceEquals(
                                         focusEventSystem.currentInputModule,
                                         _focusOwnedInputModule) ||
                                     (allowInputModuleActivationPending &&
                                      _focusInputRestoreActivationWaitFrames == 0 &&
                                      focusEventSystem.currentInputModule == null)) &&
                                    ReferenceEquals(
                                        selectedBeforeProviderVerification,
                                        _focusOwnedSelectedObject)
                                  : EventSystem.current == null &&
                                    !focusEventSystem.enabled &&
                                    !_focusOwnedInputModule.enabled &&
                                    selectedBeforeProviderVerification == null);
            var evidence = new FirstUserResumeEvidence(
                continuityValid && continuity.SessionMatches,
                continuityValid && continuity.GenerationMatches,
                continuityValid && continuity.PhaseAndSceneMatch,
                continuityValid && continuity.RuntimeHostMatches && boundaryValid && hostReady,
                continuityValid && continuity.IdentityAndCustomizationMatch,
                continuityValid && continuity.IsolatedRootMatches && boundaryValid,
                continuityValid && continuity.ProfileServiceMatches && boundaryValid,
                productionProfileRemainsNonWritable: boundaryValid && hostReady,
                productionTickSuppressed: policyValid,
                receiptAndProjectionMatch:
                    continuityValid && continuity.ReceiptAndProjectionMatch,
                tutorialAndOmenMatch: continuityValid && continuity.TutorialAndOmenMatch,
                eventSystemAndInputModuleMatch:
                    continuityValid &&
                    continuity.EventSystemAndInputModuleMatch &&
                    inputValid,
                environmentLeaseMatches:
                    continuityValid &&
                    continuity.EnvironmentLeaseMatches &&
                    environmentValid);
            if (!evidence.IsExact || !destinationValid)
            {
                message = !string.IsNullOrEmpty(boundaryMessage)
                    ? boundaryMessage
                    : !string.IsNullOrEmpty(policyMessage)
                        ? policyMessage
                        : !string.IsNullOrEmpty(environmentMessage)
                            ? environmentMessage
                            : !string.IsNullOrEmpty(inputBoundaryMessage)
                                ? inputBoundaryMessage
                                : !string.IsNullOrEmpty(continuityMessage)
                                    ? continuityMessage
                                    : "The isolated focus resume boundary could not be revalidated";
                return false;
            }

            ready = policyReady;
            message = string.Empty;
            return true;
        }

        private static bool AreGameplayInputsNeutral()
        {
            return !UnityEngine.Input.anyKey &&
                   UnityEngine.Input.touchCount == 0 &&
                   !UnityEngine.Input.GetMouseButton(0) &&
                   !UnityEngine.Input.GetMouseButton(1) &&
                   !UnityEngine.Input.GetMouseButton(2) &&
                   Mathf.Abs(UnityEngine.Input.GetAxisRaw("Horizontal")) < 0.001f &&
                   Mathf.Abs(UnityEngine.Input.GetAxisRaw("Vertical")) < 0.001f;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_initialized || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            if (FirstUserCoreGameplayPlanner.RequiresHardStopForSceneLoad(
                    _terminalFailure,
                    scene.IsValid(),
                    scene.isLoaded))
            {
                DisableBehavioursInScene<MonoBehaviour>(scene);
                _exitRequested = true;
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "A scene loaded after the isolated test retained a fail-closed panel.");
                return;
            }

            if (!FirstUserIsolatedRuntimePolicy.TrySecureScene(
                    scene,
                    out string runtimePolicyMessage))
            {
                DisableBehavioursInScene<MonoBehaviour>(scene);
                FailClosed(runtimePolicyMessage);
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
            _identityPresenter = FirstUserGameTestIdentityAdapter.CreateStandalone();
            _identityPresenter.BindExitAction(_exitButton);
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
            if (!IsInteractiveFocusActive())
            {
                return;
            }

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

            if (!TryValidateRetainedCustomizationDraft(
                    _retainedCustomizationDraft,
                    _approvedCustomizationIds,
                    out string retainedDraftMessage))
            {
                FailClosed(retainedDraftMessage);
                return;
            }

            _identitySnapshot = snapshot;
            if (_identityCanvas != null)
            {
                _identityCanvas.SetActive(false);
            }
            SetPlaytestPhase(FirstUserGameTestPlaytestPhase.AppearanceAndName);
            _customizationPanel = FirstUserGameTestCustomizationPanel.Create(
                bodyPresets,
                snapshot,
                HandleCustomizationConfirmed,
                ReturnToIdentitySelection,
                _exitButton,
                _retainedCustomizationDraft);
        }

        private void ReturnToIdentitySelection()
        {
            if (!IsInteractiveFocusActive() || _commitInProgress || _destinationAuthorized)
            {
                return;
            }

            if (_customizationPanel != null)
            {
                FirstUserGameTestCustomizationDraft retainedDraft =
                    _customizationPanel.CaptureDraft();
                if (!TryValidateRetainedCustomizationDraft(
                        retainedDraft,
                        _approvedCustomizationIds,
                        out string retainedDraftMessage))
                {
                    FailClosed(retainedDraftMessage);
                    return;
                }

                _retainedCustomizationDraft = retainedDraft;
                _customizationPanel.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_customizationPanel.gameObject);
                _customizationPanel = null;
            }

            if (_identityPresenter == null || _identityCanvas == null ||
                _identitySnapshot == null)
            {
                FailClosed("The retained identity draft could not return from customization.");
                return;
            }

            _identityPresenter.CustomizationReady -= HandleCustomizationReady;
            _identityCanvas.SetActive(false);
            UnityEngine.Object.Destroy(_identityCanvas);
            _identityPresenter = null;
            _identityCanvas = null;

            if (!FirstUserGameTestIdentityAdapter.TryCreateRestoredClassDraft(
                    _identitySnapshot,
                    out _identityPresenter,
                    out string restoreMessage))
            {
                FailClosed(string.IsNullOrEmpty(restoreMessage)
                    ? "The retained identity draft could not return from customization."
                    : restoreMessage);
                return;
            }

            _identityPresenter.BindExitAction(_exitButton);
            _identityCanvas = _identityPresenter.transform.parent == null
                ? _identityPresenter.gameObject
                : _identityPresenter.transform.parent.gameObject;
            _identityCanvas.name = "FirstUserGameTestIdentityCanvas";
            Canvas identityCanvas = _identityCanvas.GetComponent<Canvas>();
            if (identityCanvas != null)
            {
                identityCanvas.sortingOrder = 2000;
            }

            _identityPresenter.CustomizationReady += HandleCustomizationReady;

            SetPlaytestPhase(FirstUserGameTestPlaytestPhase.Identity);
        }

        private void HandleCustomizationConfirmed(string customizationId, string handle)
        {
            if (!IsInteractiveFocusActive() || _commitInProgress || _destinationAuthorized)
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
            _retainedCustomizationDraft = default;
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

            int enabledDestinationCount = EditorBuildSettings.scenes.Count(scene =>
                scene.enabled &&
                string.Equals(scene.path, ChampionArenaPath, StringComparison.Ordinal));
            if (enabledDestinationCount != 1)
            {
                FailClosed(
                    "The production first-session ChampionArena destination must be enabled " +
                    "exactly once in Build Settings.");
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
                EditorGameTestModeFocusSnapshot focus =
                    EditorGameTestModeBootstrap.FocusSnapshot;
                if (!string.Equals(focus.SessionId, _sessionId, StringComparison.Ordinal) ||
                    focus.State == EditorGameTestModeFocusState.FailClosed ||
                    focus.State == EditorGameTestModeFocusState.Inactive)
                {
                    FailClosed("The isolated focus boundary changed during destination loading.");
                    yield break;
                }

                if (focus.State != EditorGameTestModeFocusState.Active)
                {
                    started = Time.realtimeSinceStartup;
                    yield return null;
                    continue;
                }

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

        private bool TryRevalidateAuthoredEnvironment(out string message)
        {
            message = string.Empty;
            try
            {
                if (_environmentLease == null || _environmentLease.IsDisposed ||
                    _environmentFactory == null ||
                    _environmentInventoryVerifier == null ||
                    _environmentGeneration <= 0 ||
                    _environmentLease.SourceKind !=
                        FirstUserOnboardingEnvironmentSourceKind.AuthoredModule ||
                    !FirstUserOnboardingEnvironmentRegistry.TryResolve(
                        out IFirstUserOnboardingEnvironmentFactory currentFactory,
                        out IFirstUserOnboardingAssetInventoryVerifier currentVerifier) ||
                    !ReferenceEquals(currentFactory, _environmentFactory) ||
                    !ReferenceEquals(currentVerifier, _environmentInventoryVerifier) ||
                    !MatchesCapturedEnvironmentIdentity(_environmentLease))
                {
                    message = "The authored onboarding environment provider changed.";
                    return false;
                }

                GameObject root = _environmentLease.OwnedRoot;
                Scene scene = root == null ? default : root.scene;
                if (!scene.IsValid() || !scene.isLoaded ||
                    !string.Equals(scene.path, ChampionArenaPath, StringComparison.Ordinal) ||
                    scene != SceneManager.GetActiveScene())
                {
                    message = "The authored onboarding environment scene ownership changed.";
                    return false;
                }

                var request = new FirstUserOnboardingEnvironmentRequest(
                    _sessionId,
                    _environmentGeneration,
                    scene,
                    allowUnitTestDouble: false,
                    assetInventoryVerifier: _environmentInventoryVerifier);
                if (!TryValidateAuthoredEnvironmentProviderBoundary(
                        request,
                        _environmentLease,
                        out FirstUserOnboardingEnvironmentValidation validation,
                        out message))
                {
                    return false;
                }

                if (!validation.IsValid)
                {
                    string providerDiagnostic = string.Empty;
                    if (validation.Failure ==
                            FirstUserOnboardingEnvironmentFailure.ForbiddenAuthorityPresent)
                    {
                        _environmentInventoryVerifier.TryVerifyRuntimeComponentInventory(
                            _environmentLease,
                            out providerDiagnostic);
                    }

                    message =
                        "The authored onboarding environment changed: " +
                        validation.Failure +
                        (string.IsNullOrEmpty(providerDiagnostic)
                            ? "."
                            : " (" + providerDiagnostic + ").");
                    return false;
                }

                if (_environmentAttackResolver == null ||
                    _environmentLease.EnemyEncounter == null ||
                    _environmentLease.EnemyEncounter.ResetSequence !=
                        _environmentAttackResolver.ExpectedEncounterResetSequence)
                {
                    message =
                        "The authored onboarding enemy reset sequence changed outside " +
                        "the exact combat resolver.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                message =
                    "The authored onboarding environment revalidation threw " +
                    exception.GetType().Name + ".";
                return false;
            }
        }

        private bool TryValidateAuthoredEnvironmentProviderBoundary(
            FirstUserOnboardingEnvironmentRequest request,
            IFirstUserOnboardingEnvironmentLease lease,
            out FirstUserOnboardingEnvironmentValidation validation,
            out string message)
        {
            validation = default;
            message = string.Empty;
            if (!TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot providerBoundary,
                    out message))
            {
                return false;
            }

            Exception providerException = null;
            try
            {
                validation = FirstUserOnboardingEnvironmentValidator.Validate(
                    request,
                    lease);
            }
            catch (Exception exception)
            {
                providerException = exception;
            }

            if (!TryValidateNonAuthoritativeEncounterBoundary(
                    providerBoundary,
                    out string boundaryMessage))
            {
                message = string.IsNullOrEmpty(boundaryMessage)
                    ? "An authored asset verifier crossed its non-authoritative boundary."
                    : boundaryMessage;
                return false;
            }

            if (providerException != null)
            {
                message =
                    "The authored onboarding environment validator threw " +
                    providerException.GetType().Name + ".";
                return false;
            }

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
            if (!FirstUserOnboardingEnvironmentRegistry.TryResolve(
                    out IFirstUserOnboardingEnvironmentFactory environmentFactory,
                    out IFirstUserOnboardingAssetInventoryVerifier inventoryVerifier))
            {
                FailClosedWithVisibleRecovery(
                    "The authored onboarding environment is not installed. " +
                    "Primitive fallback is forbidden for a user playtest.");
                return;
            }

            ulong committedGeneration = _verifiedResult.Receipt.Receipt.CommittedGeneration;
            if (committedGeneration == 0UL || committedGeneration > int.MaxValue)
            {
                FailClosed("The authored onboarding environment generation was unavailable.");
                return;
            }

            var environmentRequest = new FirstUserOnboardingEnvironmentRequest(
                _sessionId,
                checked((int)committedGeneration),
                scene,
                allowUnitTestDouble: false,
                assetInventoryVerifier: inventoryVerifier);
            if (!TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot factoryBoundaryBefore,
                    out string factoryBoundaryMessage))
            {
                FailClosed(factoryBoundaryMessage);
                return;
            }

            IFirstUserOnboardingEnvironmentLease environmentLease = null;
            string environmentMessage = string.Empty;
            bool environmentCreated;
            try
            {
                environmentCreated = environmentFactory.TryCreate(
                    environmentRequest,
                    out environmentLease,
                    out environmentMessage);
            }
            catch (Exception exception)
            {
                environmentCreated = false;
                environmentMessage =
                    "The authored onboarding environment factory threw " +
                    exception.GetType().Name + ".";
            }

            try
            {
                if (environmentLease != null)
                {
                    _environmentLease = environmentLease;
                    _environmentOwnedRoot = environmentLease.OwnedRoot;
                    _environmentFactory = environmentFactory;
                    _environmentInventoryVerifier = inventoryVerifier;
                    _environmentGeneration = checked((int)committedGeneration);
                }

                if (!TryValidateEnvironmentFactoryBoundary(
                        scene,
                        factoryBoundaryBefore,
                        environmentLease,
                        out factoryBoundaryMessage))
                {
                    environmentCreated = false;
                    environmentMessage = factoryBoundaryMessage;
                }
            }
            catch (Exception exception)
            {
                environmentCreated = false;
                environmentMessage =
                    "The authored onboarding environment lease threw during adoption: " +
                    exception.GetType().Name + ".";
            }

            if (!environmentCreated || environmentLease == null)
            {
                FailClosed(string.IsNullOrWhiteSpace(environmentMessage)
                    ? "The authored onboarding environment could not be created."
                    : environmentMessage);
                return;
            }

            if (!TryValidateAuthoredEnvironmentProviderBoundary(
                    environmentRequest,
                    environmentLease,
                    out FirstUserOnboardingEnvironmentValidation environmentValidation,
                    out environmentMessage))
            {
                FailClosed(string.IsNullOrEmpty(environmentMessage)
                    ? "The authored onboarding environment validation boundary failed."
                    : environmentMessage);
                return;
            }

            if (!environmentValidation.IsValid)
            {
                FailClosed(
                    "The authored onboarding environment failed validation: " +
                    environmentValidation.Failure + ".");
                return;
            }

            if (!ValidateSingleEventSystem(out string postEnvironmentEventMessage))
            {
                FailClosed(postEnvironmentEventMessage);
                return;
            }

            if (!TryCaptureEnvironmentIdentity(environmentLease, out environmentMessage))
            {
                FailClosed(environmentMessage);
                return;
            }

            GameObject root = null;
            ChampionController controller = null;
            Canvas canvas = null;
            Button attackButton = null;
            Button moveForwardButton = null;
            FirstUserGameTestTutorialPresenter tutorialPresenter = null;
            string tutorialMessage = string.Empty;
            try
            {
                root = environmentLease.OwnedRoot;
                controller = environmentLease.PlayerChampion;
                root.name = DestinationRootName;
                controller.ConfigureRealmContext(_verifiedResult.Selection.Identity.Realm);
                if (!FirstUserGameTestEnemyAttackResolver.TryCreate(
                        controller,
                        environmentLease.EnemyEncounter,
                        this,
                        out _environmentAttackResolver,
                        out tutorialMessage) ||
                    !controller.TryBindEditorBasicAttackResolver(
                        _environmentAttackResolver))
                {
                    if (string.IsNullOrEmpty(tutorialMessage))
                    {
                        tutorialMessage =
                            "The admitted enemy could not bind the exact combat resolution seam.";
                    }
                }
                else
                {
                    canvas = BuildDestinationHud(
                        root.transform,
                        controller,
                        _environmentAttackResolver,
                        tutorialStore,
                        out attackButton,
                        out moveForwardButton,
                        out tutorialPresenter,
                        out tutorialMessage);
                }
            }
            catch (Exception exception)
            {
                tutorialMessage =
                    "The development tutorial destination threw " +
                    exception.GetType().Name + ".";
            }

            if (canvas == null || tutorialPresenter == null)
            {
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

        private sealed class EnvironmentFactoryComponentRecord
        {
            internal Component Component;
            internal GameObject Owner;
            internal Type ComponentType;
            internal int SceneHandle;
            internal Transform Parent;
            internal string OwnerName;
            internal string OwnerTag;
            internal int OwnerLayer;
            internal bool OwnerIsStatic;
            internal HideFlags OwnerHideFlags;
            internal HideFlags ComponentHideFlags;
            internal bool ActiveSelf;
            internal Vector3 LocalPosition;
            internal Quaternion LocalRotation;
            internal Vector3 LocalScale;
            internal bool HasEnabledState;
            internal bool EnabledState;
            internal string SerializedState;
        }

        private sealed class EnvironmentFactoryBoundarySnapshot
        {
            internal Dictionary<int, EnvironmentFactoryComponentRecord> Components;
            internal Dictionary<Type, object> Services;
            internal string SaveSnapshot;
        }

        private sealed class EnvironmentProviderDisposalBoundary
        {
            internal EnvironmentFactoryBoundarySnapshot Snapshot;
            internal HashSet<int> OwnedComponentIds;
        }

        private bool TryCaptureEnvironmentFactoryBoundary(
            out EnvironmentFactoryBoundarySnapshot snapshot,
            out string message)
        {
            snapshot = null;
            message = string.Empty;
            if (_boundIsolatedSaveService == null ||
                !ServiceLocator.TryGet(
                    out ISaveGameService currentSaveService) ||
                !ReferenceEquals(currentSaveService, _boundIsolatedSaveService) ||
                currentSaveService.CurrentSave == null)
            {
                message =
                    "The isolated save service changed before environment construction.";
                return false;
            }

            string saveSnapshot;
            try
            {
                saveSnapshot = JsonUtility.ToJson(currentSaveService.CurrentSave);
            }
            catch (Exception exception)
            {
                message =
                    "The isolated save snapshot failed before environment construction: " +
                    exception.GetType().Name + ".";
                return false;
            }

            if (!TryCaptureEnvironmentFactoryComponents(
                    out Dictionary<int, EnvironmentFactoryComponentRecord> components,
                    out message) ||
                !TryCaptureServiceRegistrations(
                    out Dictionary<Type, object> services,
                    out message))
            {
                return false;
            }

            snapshot = new EnvironmentFactoryBoundarySnapshot
            {
                Components = components,
                Services = services,
                SaveSnapshot = saveSnapshot
            };
            return true;
        }

        internal bool TryCaptureNonAuthoritativeEncounterBoundary(
            out object boundary,
            out string message)
        {
            boundary = null;
            if (!TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot snapshot,
                    out message))
            {
                return false;
            }

            boundary = snapshot;
            return true;
        }

        internal bool TryValidateNonAuthoritativeEncounterBoundary(
            object boundary,
            out string message)
        {
            message = string.Empty;
            if (!(boundary is EnvironmentFactoryBoundarySnapshot before) ||
                !TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot after,
                    out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The non-authoritative encounter boundary was invalid.";
                }

                return false;
            }

            if (!string.Equals(
                    before.SaveSnapshot,
                    after.SaveSnapshot,
                    StringComparison.Ordinal) ||
                !ServiceRegistrationsMatch(before.Services, after.Services) ||
                before.Components.Count != after.Components.Count)
            {
                message =
                    "The encounter callback changed save, service, or scene authority.";
                return false;
            }

            foreach (KeyValuePair<int, EnvironmentFactoryComponentRecord> entry in
                     before.Components)
            {
                if (!after.Components.TryGetValue(
                        entry.Key,
                        out EnvironmentFactoryComponentRecord current) ||
                    !FactoryComponentMatches(entry.Value, current))
                {
                    message =
                        "The encounter callback changed a loaded scene component.";
                    return false;
                }
            }

            return true;
        }

        bool IFirstUserGameTestMutationBoundary.TryCapture(
            out object boundary,
            out string diagnostic)
        {
            return TryCaptureNonAuthoritativeEncounterBoundary(
                out boundary,
                out diagnostic);
        }

        bool IFirstUserGameTestMutationBoundary.TryValidate(
            object boundary,
            out string diagnostic)
        {
            return TryValidateNonAuthoritativeEncounterBoundary(
                boundary,
                out diagnostic);
        }

        private bool TryCaptureProviderDisposalBoundary(
            GameObject ownedRoot,
            out EnvironmentProviderDisposalBoundary boundary,
            out string message)
        {
            boundary = null;
            message = string.Empty;
            if (ownedRoot == null ||
                !TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot snapshot,
                    out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The environment disposal root was unavailable.";
                }

                return false;
            }

            Component[] ownedComponents = ownedRoot.GetComponentsInChildren<Component>(true);
            var ownedIds = new HashSet<int>();
            for (int index = 0; index < ownedComponents.Length; index++)
            {
                Component component = ownedComponents[index];
                if (component == null || !ownedIds.Add(component.GetInstanceID()) ||
                    !snapshot.Components.ContainsKey(component.GetInstanceID()))
                {
                    message =
                        "The environment disposal component inventory was invalid.";
                    return false;
                }
            }

            if (ownedIds.Count == 0)
            {
                message = "The environment disposal component inventory was empty.";
                return false;
            }

            boundary = new EnvironmentProviderDisposalBoundary
            {
                Snapshot = snapshot,
                OwnedComponentIds = ownedIds
            };
            return true;
        }

        private bool TryValidateProviderDisposalBoundary(
            EnvironmentProviderDisposalBoundary boundary,
            out string message)
        {
            message = string.Empty;
            if (boundary == null || boundary.Snapshot == null ||
                boundary.OwnedComponentIds == null ||
                !TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot after,
                    out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The environment disposal boundary was invalid.";
                }

                return false;
            }

            EnvironmentFactoryBoundarySnapshot before = boundary.Snapshot;
            if (!string.Equals(
                    before.SaveSnapshot,
                    after.SaveSnapshot,
                    StringComparison.Ordinal) ||
                !ServiceRegistrationsMatch(before.Services, after.Services))
            {
                message =
                    "Environment disposal changed save or runtime-service authority.";
                return false;
            }

            foreach (KeyValuePair<int, EnvironmentFactoryComponentRecord> entry in
                     before.Components)
            {
                bool wasOwned = boundary.OwnedComponentIds.Contains(entry.Key);
                bool remains = after.Components.TryGetValue(
                    entry.Key,
                    out EnvironmentFactoryComponentRecord current);
                if (wasOwned)
                {
                    if (remains)
                    {
                        message =
                            "Environment disposal left an owned component behind.";
                        return false;
                    }

                    continue;
                }

                if (!remains || !FactoryComponentMatches(entry.Value, current))
                {
                    message =
                        "Environment disposal changed an external scene component.";
                    return false;
                }
            }

            foreach (int currentId in after.Components.Keys)
            {
                if (!before.Components.ContainsKey(currentId))
                {
                    message =
                        "Environment disposal created an unexpected scene component.";
                    return false;
                }
            }

            return true;
        }

        private bool TryValidateEnvironmentFactoryBoundary(
            Scene scene,
            EnvironmentFactoryBoundarySnapshot before,
            IFirstUserOnboardingEnvironmentLease lease,
            out string message)
        {
            message = string.Empty;
            if (before == null || before.Components == null || before.Services == null ||
                !scene.IsValid() || !scene.isLoaded ||
                !ServiceLocator.TryGet(
                    out ISaveGameService currentSaveService) ||
                !ReferenceEquals(currentSaveService, _boundIsolatedSaveService) ||
                currentSaveService.CurrentSave == null)
            {
                message =
                    "The isolated service boundary changed during environment construction.";
                return false;
            }

            string saveAfter;
            try
            {
                saveAfter = JsonUtility.ToJson(currentSaveService.CurrentSave);
            }
            catch (Exception exception)
            {
                message =
                    "The isolated save snapshot failed after environment construction: " +
                    exception.GetType().Name + ".";
                return false;
            }

            if (!string.Equals(before.SaveSnapshot, saveAfter, StringComparison.Ordinal))
            {
                message = "The isolated save changed during environment construction.";
                return false;
            }

            if (!TryCaptureServiceRegistrations(
                    out Dictionary<Type, object> servicesAfter,
                    out message) ||
                !ServiceRegistrationsMatch(before.Services, servicesAfter))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message =
                        "A runtime service registration changed during environment construction.";
                }

                return false;
            }

            if (!TryCaptureEnvironmentFactoryComponents(
                    out Dictionary<int, EnvironmentFactoryComponentRecord> componentsAfter,
                    out message))
            {
                return false;
            }

            if (!PreexistingFactoryComponentsMatch(
                    before.Components,
                    componentsAfter,
                    out message))
            {
                return false;
            }

            GameObject ownedRoot = lease == null ? null : lease.OwnedRoot;
            if (ownedRoot == null)
            {
                if (componentsAfter.Count != before.Components.Count)
                {
                    message =
                        "A failed environment construction left scene components behind.";
                    return false;
                }

                return true;
            }

            if (ownedRoot.transform == null || ownedRoot.transform.parent != null ||
                ownedRoot.scene != scene ||
                before.Components.ContainsKey(ownedRoot.transform.GetInstanceID()))
            {
                message =
                    "Environment construction did not produce one exact owned scene root.";
                return false;
            }

            int newComponentCount = 0;
            foreach (KeyValuePair<int, EnvironmentFactoryComponentRecord> entry in
                     componentsAfter)
            {
                if (before.Components.ContainsKey(entry.Key))
                {
                    continue;
                }

                newComponentCount++;
                Component candidate = entry.Value.Component;
                if (candidate == null || candidate.transform == null ||
                    candidate.gameObject.scene != scene ||
                    !IsOwnedByExactRoot(ownedRoot, candidate.transform))
                {
                    message =
                        "Environment construction created a component outside its owned root.";
                    return false;
                }
            }

            if (newComponentCount == 0)
            {
                message = "Environment construction produced no owned scene components.";
                return false;
            }

            return true;
        }

        private static bool TryCaptureEnvironmentFactoryComponents(
            out Dictionary<int, EnvironmentFactoryComponentRecord> components,
            out string message)
        {
            components = new Dictionary<int, EnvironmentFactoryComponentRecord>();
            message = string.Empty;
            int serializedStateCodeUnits = 0;
            Component[] candidates = Resources.FindObjectsOfTypeAll<Component>();
            for (int index = 0; index < candidates.Length; index++)
            {
                Component candidate = candidates[index];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                Scene scene = candidate.gameObject.scene;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                int instanceId = candidate.GetInstanceID();
                if (components.ContainsKey(instanceId) ||
                    components.Count >= MaximumEnvironmentFactoryComponents)
                {
                    message =
                        "The loaded-scene component inventory was invalid or exceeded its bound.";
                    return false;
                }

                TryReadEnabledState(
                    candidate,
                    out bool hasEnabledState,
                    out bool enabledState);
                string serializedState;
                try
                {
                    serializedState = EditorJsonUtility.ToJson(candidate, false);
                    serializedStateCodeUnits = checked(
                        serializedStateCodeUnits + serializedState.Length);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException ||
                    exception is OverflowException ||
                    exception is UnityException)
                {
                    message =
                        "A loaded-scene component state could not be captured exactly.";
                    return false;
                }

                if (serializedStateCodeUnits >
                    MaximumEnvironmentFactorySerializedStateCodeUnits)
                {
                    message =
                        "The loaded-scene component state inventory exceeded its bound.";
                    return false;
                }

                Transform transform = candidate.transform;
                components.Add(
                    instanceId,
                    new EnvironmentFactoryComponentRecord
                    {
                        Component = candidate,
                        Owner = candidate.gameObject,
                        ComponentType = candidate.GetType(),
                        SceneHandle = scene.handle,
                        Parent = transform.parent,
                        OwnerName = candidate.gameObject.name,
                        OwnerTag = candidate.gameObject.tag,
                        OwnerLayer = candidate.gameObject.layer,
                        OwnerIsStatic = candidate.gameObject.isStatic,
                        OwnerHideFlags = candidate.gameObject.hideFlags,
                        ComponentHideFlags = candidate.hideFlags,
                        ActiveSelf = candidate.gameObject.activeSelf,
                        LocalPosition = transform.localPosition,
                        LocalRotation = transform.localRotation,
                        LocalScale = transform.localScale,
                        HasEnabledState = hasEnabledState,
                        EnabledState = enabledState,
                        SerializedState = serializedState
                    });
            }

            return true;
        }

        private static bool TryCaptureServiceRegistrations(
            out Dictionary<Type, object> snapshot,
            out string message)
        {
            snapshot = null;
            message = string.Empty;
            if (ServiceLocatorServicesField == null ||
                ServiceLocatorServicesField.FieldType !=
                    typeof(Dictionary<Type, object>))
            {
                message = "The exact runtime-service registry inventory was unavailable.";
                return false;
            }

            try
            {
                var services = (Dictionary<Type, object>)
                    ServiceLocatorServicesField.GetValue(null);
                if (services == null || services.Count > MaximumEnvironmentFactoryServices)
                {
                    message =
                        "The runtime-service registry was invalid or exceeded its bound.";
                    return false;
                }

                snapshot = new Dictionary<Type, object>(services);
                return true;
            }
            catch (Exception exception) when (
                exception is FieldAccessException ||
                exception is TargetException ||
                exception is ArgumentException)
            {
                message =
                    "The exact runtime-service registry inventory could not be inspected.";
                return false;
            }
        }

        private static bool ServiceRegistrationsMatch(
            Dictionary<Type, object> before,
            Dictionary<Type, object> after)
        {
            if (before == null || after == null || before.Count != after.Count)
            {
                return false;
            }

            foreach (KeyValuePair<Type, object> entry in before)
            {
                if (!after.TryGetValue(entry.Key, out object current) ||
                    !ReferenceEquals(entry.Value, current))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FactoryComponentMatches(
            EnvironmentFactoryComponentRecord before,
            EnvironmentFactoryComponentRecord after)
        {
            return before != null && after != null &&
                   ReferenceEquals(before.Component, after.Component) &&
                   ReferenceEquals(before.Owner, after.Owner) &&
                   before.ComponentType == after.ComponentType &&
                   before.SceneHandle == after.SceneHandle &&
                   ReferenceEquals(before.Parent, after.Parent) &&
                   string.Equals(before.OwnerName, after.OwnerName, StringComparison.Ordinal) &&
                   string.Equals(before.OwnerTag, after.OwnerTag, StringComparison.Ordinal) &&
                   before.OwnerLayer == after.OwnerLayer &&
                   before.OwnerIsStatic == after.OwnerIsStatic &&
                   before.OwnerHideFlags == after.OwnerHideFlags &&
                   before.ComponentHideFlags == after.ComponentHideFlags &&
                   before.ActiveSelf == after.ActiveSelf &&
                   before.LocalPosition.Equals(after.LocalPosition) &&
                   before.LocalRotation.Equals(after.LocalRotation) &&
                   before.LocalScale.Equals(after.LocalScale) &&
                   before.HasEnabledState == after.HasEnabledState &&
                   (!before.HasEnabledState || before.EnabledState == after.EnabledState) &&
                   string.Equals(
                       before.SerializedState,
                       after.SerializedState,
                       StringComparison.Ordinal);
        }

        private static bool PreexistingFactoryComponentsMatch(
            Dictionary<int, EnvironmentFactoryComponentRecord> before,
            Dictionary<int, EnvironmentFactoryComponentRecord> after,
            out string message)
        {
            message = string.Empty;
            if (before == null || after == null)
            {
                message = "The environment factory component boundary was unavailable.";
                return false;
            }

            foreach (KeyValuePair<int, EnvironmentFactoryComponentRecord> entry in before)
            {
                if (!after.TryGetValue(
                        entry.Key,
                        out EnvironmentFactoryComponentRecord current) ||
                    !FactoryComponentMatches(entry.Value, current))
                {
                    message =
                        "Environment construction mutated or removed a pre-existing " +
                        "scene component.";
                    return false;
                }
            }

            return true;
        }

        internal static bool TryVerifyEnvironmentFactoryMutationForTests(
            Action mutation,
            out string message)
        {
            message = string.Empty;
            if (mutation == null ||
                !TryCaptureEnvironmentFactoryComponents(
                    out Dictionary<int, EnvironmentFactoryComponentRecord> before,
                    out message) ||
                !TryCaptureServiceRegistrations(
                    out Dictionary<Type, object> servicesBefore,
                    out message))
            {
                return false;
            }

            try
            {
                mutation();
            }
            catch (Exception exception)
            {
                message =
                    "The environment factory mutation test threw " +
                    exception.GetType().Name + ".";
                return false;
            }

            return TryCaptureServiceRegistrations(
                       out Dictionary<Type, object> servicesAfter,
                       out message) &&
                   ServiceRegistrationsMatch(servicesBefore, servicesAfter) &&
                   TryCaptureEnvironmentFactoryComponents(
                       out Dictionary<int, EnvironmentFactoryComponentRecord> after,
                       out message) &&
                   PreexistingFactoryComponentsMatch(before, after, out message);
        }

        internal static bool TryVerifyAssetInventoryCallbackMutationForTests(
            Action verifierCallback,
            out string message)
        {
            return TryVerifyEnvironmentFactoryMutationForTests(
                verifierCallback,
                out message);
        }

        private static bool IsOwnedByExactRoot(GameObject root, Transform candidate)
        {
            return root != null && candidate != null &&
                   (ReferenceEquals(root.transform, candidate) ||
                    candidate.IsChildOf(root.transform));
        }

        private static void TryReadEnabledState(
            Component component,
            out bool hasEnabledState,
            out bool enabledState)
        {
            hasEnabledState = true;
            if (component is Behaviour behaviour)
            {
                enabledState = behaviour.enabled;
                return;
            }

            if (component is Collider collider)
            {
                enabledState = collider.enabled;
                return;
            }

            if (component is Renderer renderer)
            {
                enabledState = renderer.enabled;
                return;
            }

            hasEnabledState = false;
            enabledState = false;
        }

        private sealed class EnvironmentLeaseIdentity
        {
            private readonly object[] _references;
            private readonly string[] _strings;
            private readonly int[] _integers;
            private readonly Bounds _walkableBounds;
            private readonly Vector3 _movementProofStart;
            private readonly Vector3 _movementProofEnd;
            private readonly Bounds _attackSafeBounds;

            private EnvironmentLeaseIdentity(IFirstUserOnboardingEnvironmentLease lease)
            {
                _references = CaptureReferences(lease);
                _strings = CaptureStrings(lease);
                _integers = CaptureIntegers(lease);
                _walkableBounds = lease.WalkableBounds;
                _movementProofStart = lease.MovementProofStart;
                _movementProofEnd = lease.MovementProofEnd;
                _attackSafeBounds = lease.AttackSafeBounds;
            }

            internal GameObject OwnedRoot => (GameObject)_references[0];

            internal static bool TryCapture(
                IFirstUserOnboardingEnvironmentLease lease,
                out EnvironmentLeaseIdentity identity)
            {
                identity = null;
                if (lease == null || lease.IsDisposed)
                {
                    return false;
                }

                try
                {
                    identity = new EnvironmentLeaseIdentity(lease);
                    return identity.Matches(lease);
                }
                catch (Exception)
                {
                    identity = null;
                    return false;
                }
            }

            internal bool Matches(IFirstUserOnboardingEnvironmentLease lease)
            {
                if (lease == null || lease.IsDisposed ||
                    !_walkableBounds.Equals(lease.WalkableBounds) ||
                    !_movementProofStart.Equals(lease.MovementProofStart) ||
                    !_movementProofEnd.Equals(lease.MovementProofEnd) ||
                    !_attackSafeBounds.Equals(lease.AttackSafeBounds))
                {
                    return false;
                }

                object[] references = CaptureReferences(lease);
                if (references.Length != _references.Length)
                {
                    return false;
                }

                for (int index = 0; index < references.Length; index++)
                {
                    if (!ReferenceEquals(_references[index], references[index]))
                    {
                        return false;
                    }
                }

                string[] strings = CaptureStrings(lease);
                if (strings.Length != _strings.Length)
                {
                    return false;
                }

                for (int index = 0; index < strings.Length; index++)
                {
                    if (!string.Equals(
                            _strings[index],
                            strings[index],
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }

                int[] integers = CaptureIntegers(lease);
                if (integers.Length != _integers.Length)
                {
                    return false;
                }

                for (int index = 0; index < integers.Length; index++)
                {
                    if (_integers[index] != integers[index])
                    {
                        return false;
                    }
                }

                return true;
            }

            private static object[] CaptureReferences(
                IFirstUserOnboardingEnvironmentLease lease)
            {
                return new object[]
                {
                    lease.OwnedRoot,
                    lease.EnvironmentModuleSourceAsset,
                    lease.NeutralEnvironmentRoot,
                    lease.SceneAnchor,
                    lease.SpawnAnchor,
                    lease.PlayerController,
                    lease.PlayerChampion,
                    lease.PrimaryCamera,
                    lease.PrimaryCameraAnchor,
                    lease.PrimaryCameraTarget,
                    lease.OmenAnchor,
                    lease.LightingHook,
                    lease.PresentationHook,
                    lease.ModularChampionRoot,
                    lease.ChampionSourceAsset,
                    lease.SelectedArmorRoot,
                    lease.ArmorSourceAsset,
                    lease.SelectedWeaponRoot,
                    lease.WeaponSourceAsset,
                    lease.EnemyRoot,
                    lease.EnemySourceAsset,
                    lease.EnemyEncounter,
                    lease.EnemySpawnAnchor,
                    lease.KingdomStructureRoot,
                    lease.KingdomStructureSourceAsset,
                    lease.FloorMaterial,
                    lease.WallMaterial,
                    lease.TrimMaterial,
                    lease.PropsRoot,
                    lease.FloorModuleRoot,
                    lease.WallModuleRoot,
                    lease.InnerCornerModuleRoot,
                    lease.OuterCornerModuleRoot,
                    lease.DoorwayModuleRoot,
                    lease.CeilingBeamModuleRoot,
                    lease.TrimModuleRoot,
                    lease.BrazierPropRoot,
                    lease.BannerStandPropRoot,
                    lease.CrateBarrelPropRoot
                };
            }

            private static string[] CaptureStrings(
                IFirstUserOnboardingEnvironmentLease lease)
            {
                return new[]
                {
                    lease.SessionId,
                    lease.ModuleId,
                    lease.ContentFingerprint,
                    lease.AssetInventoryFingerprint,
                    lease.EnvironmentModuleAssetId,
                    lease.ChampionAssetId,
                    lease.ArmorAssetId,
                    lease.WeaponAssetId,
                    lease.EnemyAssetId,
                    lease.KingdomStructureAssetId,
                    lease.FloorMaterialAssetId,
                    lease.WallMaterialAssetId,
                    lease.TrimMaterialAssetId
                };
            }

            private static int[] CaptureIntegers(
                IFirstUserOnboardingEnvironmentLease lease)
            {
                return new[]
                {
                    lease.Generation,
                    (int)lease.SourceKind,
                    (int)lease.EnemyCandidateKind,
                    (int)lease.EncounterMode,
                    (int)lease.KingdomStructureMode,
                    lease.EffectiveTexelsPerMeter
                };
            }
        }

        private bool TryCaptureEnvironmentIdentity(
            IFirstUserOnboardingEnvironmentLease lease,
            out string message)
        {
            message = string.Empty;
            if (!EnvironmentLeaseIdentity.TryCapture(
                    lease,
                    out EnvironmentLeaseIdentity identity) ||
                identity.OwnedRoot == null ||
                !ReferenceEquals(identity.OwnedRoot, _environmentOwnedRoot))
            {
                message = "The authored onboarding environment identity was incomplete.";
                return false;
            }

            _environmentIdentity = identity;
            return true;
        }

        private bool MatchesCapturedEnvironmentIdentity(
            IFirstUserOnboardingEnvironmentLease lease)
        {
            return _environmentIdentity != null &&
                   ReferenceEquals(_environmentOwnedRoot, _environmentIdentity.OwnedRoot) &&
                   _environmentIdentity.Matches(lease);
        }

        private void ClearEnvironmentIdentity()
        {
            if (_environmentAttackResolver != null &&
                _environmentAttackResolver.Controller != null)
            {
                _environmentAttackResolver.Controller
                    .TryUnbindEditorBasicAttackResolver(
                        _environmentAttackResolver);
            }

            _environmentLease = null;
            _environmentFactory = null;
            _environmentInventoryVerifier = null;
            _environmentGeneration = 0;
            _environmentOwnedRoot = null;
            _environmentIdentity = null;
            _environmentAttackResolver = null;
        }

        private bool TryDisposeEnvironmentLease(
            IFirstUserOnboardingEnvironmentLease lease,
            out string message)
        {
            message = string.Empty;
            if (lease == null)
            {
                return true;
            }

            try
            {
                if (!TryReadEnvironmentLeaseDisposalState(
                        lease,
                        out bool wasDisposed,
                        out GameObject reportedRoot,
                        out message))
                {
                    return false;
                }

                if (wasDisposed)
                {
                    if (reportedRoot != null)
                    {
                        message =
                            "The authored onboarding environment reported disposal but retained its root.";
                        return false;
                    }

                    return true;
                }

                GameObject ownedRoot = ReferenceEquals(lease, _environmentLease)
                    ? _environmentOwnedRoot
                    : reportedRoot;
                if (ownedRoot == null || !ReferenceEquals(ownedRoot, reportedRoot))
                {
                    message =
                        "The authored onboarding environment changed its exact owned root.";
                    return false;
                }

                if (!TryCaptureProviderDisposalBoundary(
                        ownedRoot,
                        out EnvironmentProviderDisposalBoundary disposalBoundary,
                        out message))
                {
                    return false;
                }

                Exception disposalException = null;
                try
                {
                    lease.Dispose();
                }
                catch (Exception exception)
                {
                    disposalException = exception;
                }

                if (!TryValidateProviderDisposalBoundary(
                        disposalBoundary,
                        out message))
                {
                    return false;
                }

                if (disposalException != null)
                {
                    message =
                        "The authored onboarding environment cleanup threw " +
                        disposalException.GetType().Name + ".";
                    return false;
                }

                if (!TryReadEnvironmentLeaseDisposalState(
                        lease,
                        out bool isDisposed,
                        out GameObject remainingRoot,
                        out message))
                {
                    return false;
                }

                if (!isDisposed || ownedRoot != null || remainingRoot != null)
                {
                    message =
                        "The authored onboarding environment did not confirm exact-root disposal.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                message =
                    "The authored onboarding environment cleanup threw " +
                    exception.GetType().Name + ".";
                return false;
            }
        }

        private bool TryReadEnvironmentLeaseDisposalState(
            IFirstUserOnboardingEnvironmentLease lease,
            out bool isDisposed,
            out GameObject ownedRoot,
            out string message)
        {
            isDisposed = false;
            ownedRoot = null;
            message = string.Empty;
            if (lease == null ||
                !TryCaptureEnvironmentFactoryBoundary(
                    out EnvironmentFactoryBoundarySnapshot getterBoundary,
                    out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The environment lease getter boundary was unavailable.";
                }

                return false;
            }

            Exception getterException = null;
            try
            {
                isDisposed = lease.IsDisposed;
                ownedRoot = lease.OwnedRoot;
            }
            catch (Exception exception)
            {
                getterException = exception;
            }

            if (!TryValidateNonAuthoritativeEncounterBoundary(
                    getterBoundary,
                    out string boundaryMessage))
            {
                message = string.IsNullOrEmpty(boundaryMessage)
                    ? "An environment lease getter crossed its non-authoritative boundary."
                    : boundaryMessage;
                return false;
            }

            if (getterException != null)
            {
                message =
                    "The authored onboarding environment cleanup getter threw " +
                    getterException.GetType().Name + ".";
                return false;
            }

            return true;
        }

        private Canvas BuildDestinationHud(
            Transform parent,
            ChampionController controller,
            FirstUserGameTestEnemyAttackResolver attackResolver,
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
                    attackResolver,
                    tutorialStore,
                    selection.Identity.Realm,
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

        internal static bool TryValidateRetainedCustomizationDraft(
            FirstUserGameTestCustomizationDraft draft,
            ICollection<string> approvedCustomizationIds,
            out string message)
        {
            if ((draft.DevelopmentHandle ?? string.Empty).Length >
                FirstUserGameTestAdapter.MaximumHandleCodeUnits)
            {
                message =
                    "The retained appearance and name draft exceeded its bounded input envelope.";
                return false;
            }

            if (!string.IsNullOrEmpty(draft.CustomizationId) &&
                (approvedCustomizationIds == null ||
                 !approvedCustomizationIds.Contains(draft.CustomizationId)))
            {
                message =
                    "The retained appearance choice is no longer in the exact catalog.";
                return false;
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
                return CharacterCustomizationCatalog.TryParse(json, out catalog);
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
            FailClosedCore(message, visibleRecoveryExplicitlyAllowed: false);
        }

        private void FailClosedWithVisibleRecovery(string message)
        {
            FailClosedCore(message, visibleRecoveryExplicitlyAllowed: true);
        }

        private void FailClosedCore(
            string message,
            bool visibleRecoveryExplicitlyAllowed)
        {
            if (_terminalFailure)
            {
                return;
            }

            bool exactTickPolicyVerified =
                FirstUserIsolatedRuntimePolicy.TryVerifyActive(
                    out string tickPolicyMessage);
            FirstUserRuntimeFailureDisposition disposition =
                FirstUserCoreGameplayPlanner.ClassifyRuntimeFailure(
                    visibleRecoveryExplicitlyAllowed,
                    _initialized,
                    EditorApplication.isPlaying,
                    exactTickPolicyVerified);
            _terminalFailure = true;
            _focusResumeStateSnapshot = null;
            _lastFailure = string.IsNullOrWhiteSpace(message)
                ? "The isolated first-user Game Test failed closed."
                : message;
            _commitInProgress = false;
            _destinationAuthorized = false;
            _tutorialPresenter?.SetFocusSuspended(true);
            ChampionController ownedController = _destinationMarker == null
                ? null
                : _destinationMarker.Controller;
            if (ownedController != null)
            {
                ownedController.SetExternalMoveInput(Vector2.zero);
                ownedController.SetControlLocked(true);
                ownedController.enabled = false;
            }

            bool environmentCleanupComplete = true;
            if (_environmentLease != null)
            {
                if (TryDisposeEnvironmentLease(
                        _environmentLease,
                        out string disposalMessage))
                {
                    ClearEnvironmentIdentity();
                    _destinationBuilt = false;
                    _destinationMarker = null;
                    _tutorialPresenter = null;
                }
                else
                {
                    environmentCleanupComplete = false;
                    Debug.LogError(
                        "[AL-FIRST-USER-GAME-TEST-CLEANUP] " + disposalMessage);
                }
            }

            DestroyOwnedSelectionUi();
            Debug.LogError("[AL-FIRST-USER-GAME-TEST-BLOCKED] " + _lastFailure);

            if (environmentCleanupComplete && disposition ==
                FirstUserRuntimeFailureDisposition.RetainBlockedPanel)
            {
                if (TryBuildFailurePanel(out string panelMessage))
                {
                    // The exact Bootloader tick owner is already disabled. Retain a
                    // visible, recovery-only panel while replacing the save factory with
                    // the throwing boundary and marking all gameplay focus fail-closed.
                    EditorGameTestModeBootstrap.EnterFailClosedState(
                        _sessionId,
                        _lastFailure);
                    return;
                }

                _lastFailure += " Recovery input: " + panelMessage;
            }

            string hardStopDiagnostic = _lastFailure;
            if (!exactTickPolicyVerified && !string.IsNullOrEmpty(tickPolicyMessage))
            {
                hardStopDiagnostic += " Tick suppression: " + tickPolicyMessage;
            }

            // Initialization, scene-ownership, and tick-policy failures cannot leave
            // an enabled production Bootloader running behind a diagnostic overlay.
            EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                hardStopDiagnostic);
        }

        private bool TryBuildFailurePanel(out string message)
        {
            message = string.Empty;
            if (_failureCanvas != null)
            {
                return ValidateSingleEventSystem(out message);
            }

            if (!TryGetSingleEventInputBoundary(
                    out EventSystem recoveryEventSystem,
                    out BaseInputModule recoveryInputModule,
                    out message))
            {
                return false;
            }

            EventSystem.current = recoveryEventSystem;
            recoveryEventSystem.enabled = true;
            recoveryInputModule.enabled = true;
            recoveryEventSystem.UpdateModules();
            recoveryInputModule.ActivateModule();

            _failureCanvas = new GameObject(
                FailureCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = _failureCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            CanvasScaler scaler = _failureCanvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Image backing = new GameObject(
                "FirstUserGameTestFailureBackdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)).GetComponent<Image>();
            backing.transform.SetParent(_failureCanvas.transform, false);
            RectTransform backingRect = backing.rectTransform;
            backingRect.anchorMin = Vector2.zero;
            backingRect.anchorMax = Vector2.one;
            backingRect.offsetMin = Vector2.zero;
            backingRect.offsetMax = Vector2.zero;
            backing.color = new Color(0.018f, 0.024f, 0.034f, 0.97f);
            backing.raycastTarget = true;
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
            text.raycastTarget = false;

            if (_exitButton != null)
            {
                _exitButton.gameObject.SetActive(false);
            }

            _failureExitButton = CreateButton(
                _failureCanvas.transform,
                "ExitBlockedIsolatedTest",
                FirstUserGameTestPlaytestCopy.ExitAction,
                BuiltInFont(),
                new Vector2(0f, 36f),
                new Vector2(240f, 56f),
                new Vector2(0.5f, 0f));
            _failureExitButton.onClick.AddListener(RequestExitIsolatedTest);
            Navigation recoveryNavigation = _failureExitButton.navigation;
            recoveryNavigation.mode = Navigation.Mode.None;
            _failureExitButton.navigation = recoveryNavigation;
            DisableCompetingRecoveryUi(_failureCanvas, _failureExitButton);
            _failureEventSystem = recoveryEventSystem;
            _failureInputModule = recoveryInputModule;
            _failureEventSystem.firstSelectedGameObject =
                _failureExitButton.gameObject;
            _failureEventSystem.SetSelectedGameObject(
                _failureExitButton.gameObject);

            BaseInputModule selectedInputModule =
                _failureEventSystem.currentInputModule;
            _retainedFailureInputActivationGraceTicks = selectedInputModule == null ? 1 : 0;
            if (!TryGetSingleEventInputBoundary(
                    out EventSystem verifiedEventSystem,
                    out BaseInputModule verifiedInputModule,
                    out message) ||
                !ReferenceEquals(EventSystem.current, _failureEventSystem) ||
                !ReferenceEquals(verifiedEventSystem, _failureEventSystem) ||
                !ReferenceEquals(verifiedInputModule, _failureInputModule) ||
                (selectedInputModule != null &&
                 !ReferenceEquals(selectedInputModule, _failureInputModule)) ||
                !_failureEventSystem.enabled || !_failureInputModule.enabled)
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The recovery panel could not retain the sole input owner.";
                }

                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryVerifyRetainedFailureBoundary(out string message)
        {
            message = string.Empty;
            EditorGameTestModeFocusSnapshot focus =
                EditorGameTestModeBootstrap.FocusSnapshot;
            BaseInputModule selectedInputModule = _failureEventSystem == null
                ? null
                : _failureEventSystem.currentInputModule;
            if (_failureEventSystem != null && _failureExitButton != null &&
                !ReferenceEquals(
                    _failureEventSystem.currentSelectedGameObject,
                    _failureExitButton.gameObject))
            {
                _failureEventSystem.SetSelectedGameObject(
                    _failureExitButton.gameObject);
            }

            if (!_terminalFailure || _failureCanvas == null ||
                !_failureCanvas.activeInHierarchy || _failureExitButton == null ||
                !_failureExitButton.gameObject.activeInHierarchy ||
                !_failureExitButton.interactable || _failureEventSystem == null ||
                !ReferenceEquals(EventSystem.current, _failureEventSystem) ||
                !_failureEventSystem.enabled || _failureInputModule == null ||
                !_failureInputModule.enabled ||
                (selectedInputModule == null &&
                 _retainedFailureInputActivationGraceTicks <= 0) ||
                (selectedInputModule != null &&
                 !ReferenceEquals(selectedInputModule, _failureInputModule)) ||
                !ReferenceEquals(
                    _failureEventSystem.currentSelectedGameObject,
                    _failureExitButton.gameObject) ||
                HasCompetingRecoveryUi(_failureCanvas, _failureExitButton) ||
                !string.Equals(focus.SessionId, _sessionId, StringComparison.Ordinal) ||
                focus.State != EditorGameTestModeFocusState.FailClosed)
            {
                message =
                    "The recovery-only panel, input owner, or fail-closed focus changed.";
                return false;
            }

            if (selectedInputModule == null)
            {
                _retainedFailureInputActivationGraceTicks--;
            }
            else
            {
                _retainedFailureInputActivationGraceTicks = 0;
            }

            if (!FirstUserIsolatedRuntimePolicy.TryVerifyActive(
                    out string policyMessage))
            {
                message = string.IsNullOrEmpty(policyMessage)
                    ? "The isolated runtime policy changed behind the recovery panel."
                    : policyMessage;
                return false;
            }

            if (!TryGetSingleEventInputBoundary(
                    out EventSystem verifiedEventSystem,
                    out BaseInputModule verifiedInputModule,
                    out message))
            {
                return false;
            }

            if (!ReferenceEquals(verifiedEventSystem, _failureEventSystem) ||
                !ReferenceEquals(verifiedInputModule, _failureInputModule))
            {
                message = "The exact recovery EventSystem or input module changed.";
                return false;
            }

            return true;
        }

        private static void DisableCompetingRecoveryUi(
            GameObject failureCanvas,
            Button failureExitButton)
        {
            GraphicRaycaster[] raycasters =
                Resources.FindObjectsOfTypeAll<GraphicRaycaster>();
            for (int index = 0; index < raycasters.Length; index++)
            {
                GraphicRaycaster raycaster = raycasters[index];
                if (raycaster != null && raycaster.gameObject.scene.IsValid() &&
                    raycaster.gameObject.scene.isLoaded &&
                    (failureCanvas == null ||
                     !ReferenceEquals(raycaster.gameObject, failureCanvas)))
                {
                    raycaster.enabled = false;
                }
            }

            Selectable[] selectables = Resources.FindObjectsOfTypeAll<Selectable>();
            for (int index = 0; index < selectables.Length; index++)
            {
                Selectable selectable = selectables[index];
                if (selectable != null && selectable.gameObject.scene.IsValid() &&
                    selectable.gameObject.scene.isLoaded &&
                    !ReferenceEquals(selectable, failureExitButton))
                {
                    selectable.interactable = false;
                }
            }
        }

        private static bool HasCompetingRecoveryUi(
            GameObject failureCanvas,
            Button failureExitButton)
        {
            GraphicRaycaster[] raycasters =
                Resources.FindObjectsOfTypeAll<GraphicRaycaster>();
            for (int index = 0; index < raycasters.Length; index++)
            {
                GraphicRaycaster raycaster = raycasters[index];
                if (raycaster != null && raycaster.enabled &&
                    raycaster.gameObject.activeInHierarchy &&
                    raycaster.gameObject.scene.IsValid() &&
                    raycaster.gameObject.scene.isLoaded &&
                    (failureCanvas == null ||
                     !ReferenceEquals(raycaster.gameObject, failureCanvas)))
                {
                    return true;
                }
            }

            Selectable[] selectables = Resources.FindObjectsOfTypeAll<Selectable>();
            for (int index = 0; index < selectables.Length; index++)
            {
                Selectable selectable = selectables[index];
                if (selectable != null && selectable.interactable &&
                    selectable.gameObject.activeInHierarchy &&
                    selectable.gameObject.scene.IsValid() &&
                    selectable.gameObject.scene.isLoaded &&
                    !ReferenceEquals(selectable, failureExitButton))
                {
                    return true;
                }
            }

            return false;
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
            if (!IsExitCommandAvailable())
            {
                return;
            }

            TryRequestExit(
                () => EditorApplication.isPlaying = false,
                ConfirmExitWithEditorDialog);
        }

        private bool IsInteractiveFocusActive()
        {
            EditorGameTestModeFocusSnapshot focus =
                EditorGameTestModeBootstrap.FocusSnapshot;
            return !_focusSuspended &&
                   string.Equals(focus.SessionId, _sessionId, StringComparison.Ordinal) &&
                   focus.State == EditorGameTestModeFocusState.Active;
        }

        private bool IsExitCommandAvailable()
        {
            EditorGameTestModeFocusSnapshot focus =
                EditorGameTestModeBootstrap.FocusSnapshot;
            if (!string.Equals(focus.SessionId, _sessionId, StringComparison.Ordinal))
            {
                return false;
            }

            return IsInteractiveFocusActive() ||
                   (_terminalFailure &&
                    focus.State == EditorGameTestModeFocusState.FailClosed);
        }

        private bool TryRequestExit(Action transition, Func<bool> confirmation)
        {
            if (_exitRequested || transition == null || confirmation == null)
            {
                return false;
            }

            FirstUserExitTransition requested =
                FirstUserCoreGameplayPlanner.RequestExit(_exitState);
            if (requested.Status != FirstUserCoreTransitionStatus.Applied)
            {
                return false;
            }

            _exitState = requested.State;
            bool confirmed;
            try
            {
                confirmed = confirmation();
            }
            catch (Exception exception)
            {
                _exitState = FirstUserExitState.Inactive;
                FailClosed(
                    "The isolated exit confirmation threw " +
                    exception.GetType().Name + ".");
                return false;
            }

            FirstUserExitTransition decided = confirmed
                ? FirstUserCoreGameplayPlanner.ConfirmExit(_exitState)
                : FirstUserCoreGameplayPlanner.CancelExit(_exitState);
            if (decided.Status != FirstUserCoreTransitionStatus.Applied)
            {
                FailClosed("The isolated exit decision was invalid.");
                return false;
            }

            _exitState = decided.State;
            if (!confirmed)
            {
                RestoreExitActionAfterCancel();
                return true;
            }

            _exitRequested = true;
            Button activeExitButton = GetActiveExitButton();
            if (activeExitButton != null)
            {
                activeExitButton.interactable = false;
                Text label = activeExitButton.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = FirstUserGameTestPlaytestCopy.ExitingStatus;
                }
            }

            transition();
            return true;
        }

        private static bool ConfirmExitWithEditorDialog()
        {
            return EditorUtility.DisplayDialog(
                FirstUserGameTestPlaytestCopy.ExitAction,
                "Leave this isolated development playtest?",
                FirstUserGameTestPlaytestCopy.ExitAction,
                "Keep Playing");
        }

        private void RestoreExitActionAfterCancel()
        {
            Button activeExitButton = GetActiveExitButton();
            if (activeExitButton == null)
            {
                return;
            }

            activeExitButton.interactable = true;
            Text label = activeExitButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = FirstUserGameTestPlaytestCopy.ExitAction;
            }

            if (IsExitCommandAvailable() && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(activeExitButton.gameObject);
            }
        }

        private Button GetActiveExitButton()
        {
            return _terminalFailure && _failureExitButton != null
                ? _failureExitButton
                : _exitButton;
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
            return TryGetSingleEventInputBoundary(out _, out _, out message);
        }

        private static bool TryGetSingleEventInputBoundary(
            out EventSystem eventSystem,
            out BaseInputModule inputModule,
            out string message)
        {
            eventSystem = null;
            inputModule = null;
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

            eventSystem = eventSystems[0];
            inputModule = modules[0];
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

    internal readonly struct FirstUserGameTestCustomizationDraft
    {
        internal FirstUserGameTestCustomizationDraft(
            string customizationId,
            string developmentHandle)
        {
            CustomizationId = customizationId ?? string.Empty;
            DevelopmentHandle = developmentHandle ?? string.Empty;
        }

        internal string CustomizationId { get; }
        internal string DevelopmentHandle { get; }
        internal bool HasAnyValue =>
            !string.IsNullOrEmpty(CustomizationId) ||
            !string.IsNullOrEmpty(DevelopmentHandle);
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
            Button exitButton,
            FirstUserGameTestCustomizationDraft retainedDraft = default)
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
            panel.Build(
                bodyPresets,
                identity,
                confirmed,
                back,
                exitButton,
                retainedDraft);
            return panel;
        }

        private void Build(
            IReadOnlyList<BodyPresetData> bodyPresets,
            FirstUserIdentityDraftSnapshot identity,
            Action<string, string> confirmed,
            Action back,
            Button exitButton,
            FirstUserGameTestCustomizationDraft retainedDraft)
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
            RestoreDraft(bodyPresets, retainedDraft);
            Refresh();

            if (EventSystem.current != null)
            {
                GameObject initialFocus = !string.IsNullOrEmpty(_selectedId)
                    ? _handleInput.gameObject
                    : _choiceButtons.Count > 0
                        ? _choiceButtons[0].gameObject
                        : _backButton.gameObject;
                EventSystem.current.SetSelectedGameObject(initialFocus);
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

        internal FirstUserGameTestCustomizationDraft CaptureDraft()
        {
            return new FirstUserGameTestCustomizationDraft(
                _selectedId,
                _handleInput == null ? string.Empty : _handleInput.text);
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

        private void RestoreDraft(
            IReadOnlyList<BodyPresetData> bodyPresets,
            FirstUserGameTestCustomizationDraft draft)
        {
            if (!draft.HasAnyValue)
            {
                return;
            }

            bool customizationStillAvailable = false;
            for (int index = 0; index < bodyPresets.Count; index++)
            {
                BodyPresetData preset = bodyPresets[index];
                if (preset != null && string.Equals(
                        preset.id,
                        draft.CustomizationId,
                        StringComparison.Ordinal))
                {
                    customizationStillAvailable = true;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(draft.CustomizationId) &&
                !customizationStillAvailable)
            {
                throw new InvalidOperationException(
                    "The retained customization draft no longer exists in the exact catalog.");
            }

            _selectedId = draft.CustomizationId;
            _handleInput.SetTextWithoutNotify(draft.DevelopmentHandle);
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
