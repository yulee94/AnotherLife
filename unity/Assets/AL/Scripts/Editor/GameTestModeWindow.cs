#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.CompilerServices;
using AL.Core.Scenes;
using AL.Development;
using AL.Editor.Development.FirstUserGameTest;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[assembly: InternalsVisibleTo("AL.Development.FirstUserGameTest.Editor.Tests")]

namespace AL.EditorTools
{
    internal static class GameTestModeSessionKeys
    {
        internal const string Active = EditorGameTestModeBootstrap.SessionActiveKey;
        internal const string SessionId = EditorGameTestModeBootstrap.SessionIdKey;
        internal const string TemporaryRoot = EditorGameTestModeBootstrap.SessionTemporaryRootKey;
        internal const string PersistentRoot = EditorGameTestModeBootstrap.SessionPersistentRootKey;
        internal const string IsolatedRoot = EditorGameTestModeBootstrap.SessionIsolatedRootKey;
        internal const string BootScenePath = EditorGameTestModeBootstrap.SessionBootScenePathKey;
        internal const string BootSceneGuid = EditorGameTestModeBootstrap.SessionBootSceneGuidKey;
        internal const string FullDomainReload = EditorGameTestModeBootstrap.SessionFullDomainReloadKey;
        internal const string FullSceneReload = EditorGameTestModeBootstrap.SessionFullSceneReloadKey;
        internal const string PreviousStartScenePath = "AL.GameTestMode.PreviousStartScenePath";
        internal const string PreviousStartSceneGuid = "AL.GameTestMode.PreviousStartSceneGuid";
        internal const string PreviousStartSceneWasNull = "AL.GameTestMode.PreviousStartSceneWasNull";
        internal const string RecoveryPending = "AL.GameTestMode.RecoveryPending";
        internal const string TransitioningToPlay = "AL.GameTestMode.TransitioningToPlay";
        internal const string PreAwakeRearmCompleted =
            "AL.GameTestMode.PreAwakeRearmCompleted";
        internal const string Stopping = "AL.GameTestMode.Stopping";
        internal const string ReloadInterrupted = "AL.GameTestMode.ReloadInterrupted";
        internal const string LastStatus = "AL.GameTestMode.LastStatus";
        internal const string LastRoot = "AL.GameTestMode.LastRoot";
    }

    internal readonly struct GameTestModeControlPanelView
    {
        internal GameTestModeControlPanelView(
            string currentState,
            string blocker,
            string primaryAction,
            bool primaryActionEnabled)
        {
            CurrentState = currentState ?? string.Empty;
            Blocker = blocker ?? string.Empty;
            PrimaryAction = primaryAction ?? string.Empty;
            PrimaryActionEnabled = primaryActionEnabled;
        }

        internal string CurrentState { get; }
        internal string Blocker { get; }
        internal string PrimaryAction { get; }
        internal bool PrimaryActionEnabled { get; }
    }

    internal static class GameTestModeControlPanelPresentation
    {
        internal const string StartAction = "Start First User Experience";
        internal const string StopAction = "Stop & Clean Up Safely";
        internal const string CleanupAction = "Clean Up Previous Isolated Test";
        internal const string ForgetAction =
            "Forget Invalid Recovery Record (Delete No Files)";
        internal const string JourneyChecklist =
            "1. Loading\n" +
            "2. Identity\n" +
            "3. Appearance & Name\n" +
            "4. World Tutorial\n" +
            "5. OMEN";
        internal const string StartControlName = "AL.FirstUserExperience.Start";
        internal const string StopControlName = "AL.FirstUserExperience.Stop";
        internal const string CleanupControlName = "AL.FirstUserExperience.Cleanup";
        internal const string ForgetControlName = "AL.FirstUserExperience.Forget";

        internal static GameTestModeControlPanelView Build(
            bool sessionActive,
            bool recoveryPending,
            bool invalidRecoveryRecord,
            bool playModeActive,
            bool canStart,
            string rawStatus,
            string rawBlocker)
        {
            _ = rawStatus;
            if (invalidRecoveryRecord)
            {
                return new GameTestModeControlPanelView(
                    "Safe cleanup needs attention.",
                    "A previous isolated test record is unreadable. Forgetting this record removes project-scoped Editor metadata only and deletes no files.",
                    ForgetAction,
                    !playModeActive);
            }

            if (sessionActive)
            {
                return new GameTestModeControlPanelView(
                    "The First User Experience is running.",
                    "Use Exit Isolated Test in the playtest, or stop and clean up safely here.",
                    StopAction,
                    true);
            }

            if (recoveryPending)
            {
                return new GameTestModeControlPanelView(
                    "A previous isolated test is waiting for cleanup.",
                    "Clean up the isolated session before starting again. Your normal save remains untouched.",
                    CleanupAction,
                    !playModeActive);
            }

            if (playModeActive)
            {
                return new GameTestModeControlPanelView(
                    "Another Unity Play session is running.",
                    "Stop the current Play session before starting this experience.",
                    StartAction,
                    false);
            }

            if (canStart)
            {
                return new GameTestModeControlPanelView(
                    "Ready to begin.",
                    string.Empty,
                    StartAction,
                    true);
            }

            return new GameTestModeControlPanelView(
                "Start is temporarily unavailable.",
                FriendlyBlocker(rawBlocker),
                StartAction,
                false);
        }

        private static string FriendlyBlocker(string rawBlocker)
        {
            if (string.IsNullOrWhiteSpace(rawBlocker))
            {
                return "Wait for the Unity Editor to become ready, then try again.";
            }

            if (rawBlocker.IndexOf("compil", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Scripts are still compiling. Start will unlock when compilation finishes.";
            }

            if (rawBlocker.IndexOf("Asset Database", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Project assets are still updating. Start will unlock when the update finishes.";
            }

            if (rawBlocker.IndexOf("Play Mode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Stop the current Play session before starting this experience.";
            }

            if (rawBlocker.IndexOf("reload", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Enable full Domain Reload and Scene Reload in the Editor before starting.";
            }

            if (rawBlocker.IndexOf("Boot", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "The required launch scene is unavailable. Review the Console before continuing.";
            }

            if (rawBlocker.IndexOf(
                    "authored onboarding",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawBlocker.IndexOf(
                    "admitted real assets",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    "The required real champion, enemy, kingdom structure, and neutral environment assets are not admitted yet. The user playtest remains locked.";
            }

            if (rawBlocker.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawBlocker.IndexOf("recover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                rawBlocker.IndexOf("clean", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Clean up the previous isolated test before starting again.";
            }

            return "The isolated playtest is not ready. Review the Console, then try again.";
        }
    }

    internal sealed class GameTestModeControlPanelCommandGate
    {
        private bool _commandIssued;

        internal bool TryStart(Func<bool> start)
        {
            if (_commandIssued || start == null)
            {
                return false;
            }

            _commandIssued = true;
            bool started = false;
            try
            {
                started = start();
                return started;
            }
            finally
            {
                if (!started)
                {
                    _commandIssued = false;
                }
            }
        }

        internal bool TryCleanUp(Action cleanUp)
        {
            if (_commandIssued || cleanUp == null)
            {
                return false;
            }

            _commandIssued = true;
            cleanUp();
            return true;
        }

        internal void Reset()
        {
            _commandIssued = false;
        }
    }

    [InitializeOnLoad]
    internal static class GameTestModeEditorCoordinator
    {
        private const string MenuRoot = "Another Life/Test Mode/";

        static GameTestModeEditorCoordinator()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.focusChanged -= HandleEditorFocusChanged;
            EditorApplication.focusChanged += HandleEditorFocusChanged;
            EditorApplication.pauseStateChanged -= HandleEditorPauseStateChanged;
            EditorApplication.pauseStateChanged += HandleEditorPauseStateChanged;
            RecoverDurableRecordAfterEditorRestart();
            EditorApplication.delayCall += RecoverOrVerifyAfterReload;
        }

        internal static bool IsSessionActive =>
            SessionState.GetBool(GameTestModeSessionKeys.Active, false);

        internal static bool IsRecoveryPending =>
            SessionState.GetBool(GameTestModeSessionKeys.RecoveryPending, false) ||
            EditorGameTestModeBootstrap.HasDurableRecoveryRecord;

        internal static bool HasInvalidDurableRecoveryRecord
        {
            get
            {
                return EditorGameTestModeBootstrap.HasDurableRecoveryRecord &&
                       !EditorGameTestModeBootstrap.TryReadDurableRecoveryRecord(
                           out _,
                           out _,
                           out _);
            }
        }

        internal static string CurrentStatus =>
            SessionState.GetString(
                GameTestModeSessionKeys.LastStatus,
                "Ready for an isolated fresh-profile run.");

        internal static string LastRoot
        {
            get
            {
                string sessionRoot = SessionState.GetString(
                    GameTestModeSessionKeys.LastRoot,
                    string.Empty);
                if (!string.IsNullOrWhiteSpace(sessionRoot))
                {
                    return sessionRoot;
                }

                return EditorGameTestModeBootstrap.TryReadDurableRecoveryRecord(
                        out EditorGameTestModeRecoveryRecord record,
                        out _,
                        out _)
                    ? record.Plan.IsolatedSaveRoot
                    : string.Empty;
            }
        }

        [MenuItem(MenuRoot + "Fresh First User (Isolated)", priority = 10)]
        private static void StartFreshFromMenu()
        {
            if (!TryStartFreshFirstUser(out string message))
            {
                EditorUtility.DisplayDialog("Another Life Test Mode", message, "Close");
            }
        }

        [MenuItem(MenuRoot + "Fresh First User (Isolated)", validate = true)]
        private static bool ValidateStartFreshFromMenu()
        {
            return CanStart(out _);
        }

        [InitializeOnEnterPlayMode]
        private static void ReArmAfterDomainReload(EnterPlayModeOptions options)
        {
            string expectedSession = string.Empty;
            try
            {
                if (!SessionState.GetBool(GameTestModeSessionKeys.Active, false))
                {
                    return;
                }

                expectedSession = SessionState.GetString(
                    GameTestModeSessionKeys.SessionId,
                    string.Empty);
                GetReloadPolicy(options, out bool fullDomainReload, out bool fullSceneReload);
                if (!TryReadPlan(
                        fullDomainReload,
                        fullSceneReload,
                        out EditorGameTestModePlan plan,
                        out string message) ||
                    !EditorGameTestModeBootstrap.TryArm(plan, out _, out message) ||
                    !EditorGameTestModeBootstrap.TryVerifyPreAwakeClean(out _, out message))
                {
                    FailClosedAndAbort(
                        expectedSession,
                        "Isolation could not be re-armed before Play Mode: " + message);
                    return;
                }

                SessionState.SetBool(
                    GameTestModeSessionKeys.PreAwakeRearmCompleted,
                    true);
            }
            catch (Exception ex)
            {
                FailClosedAndAbort(
                    expectedSession,
                    "The re-arm callback threw unexpectedly and was blocked: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        internal static bool CanStart(out string message)
        {
            if (EditorApplication.isCompiling)
            {
                message = "Wait for script compilation to finish.";
                return false;
            }

            if (EditorApplication.isUpdating)
            {
                message = "Wait for the Asset Database update to finish.";
                return false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "Exit Play Mode before starting a fresh isolated run.";
                return false;
            }

            if (IsSessionActive || IsRecoveryPending || EditorGameTestModeBootstrap.IsArmed)
            {
                message = IsRecoveryPending
                    ? "A retained isolated test root must be recovered or cleaned before another run."
                    : "Another isolated game-test session is already active.";
                return false;
            }

            GetReloadPolicy(out bool fullDomainReload, out bool fullSceneReload);
            if (!fullDomainReload || !fullSceneReload)
            {
                message =
                    "Isolated mode requires full Domain Reload and Scene Reload. In Project Settings > Editor, disable Enter Play Mode Options (or enable both reloads). The tool will not change this setting for you.";
                return false;
            }

            ProductionSceneRecord boot = ProductionSceneDescriptor.ShellFoundationOrdered[0];
            if (!string.Equals(boot.AssetPath, EditorGameTestModeBootstrap.ExpectedBootScenePath, StringComparison.Ordinal) ||
                !string.Equals(boot.AssetGuid, EditorGameTestModeBootstrap.ExpectedBootSceneGuid, StringComparison.Ordinal) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(boot.AssetPath) == null ||
                !string.Equals(AssetDatabase.AssetPathToGUID(boot.AssetPath), boot.AssetGuid, StringComparison.Ordinal))
            {
                message = "The exact production Boot scene path/GUID is unavailable or has drifted.";
                return false;
            }

            if (!FirstUserOnboardingEnvironmentRegistry.IsReadyForUserPlaytest)
            {
                message =
                    "The authored onboarding module and its admitted real assets are unavailable.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        internal static bool TryStartFreshFirstUser(out string message)
        {
            if (!CanStart(out message))
            {
                SetStatus(message);
                return false;
            }

            string sessionId = Guid.NewGuid().ToString("N");
            string temporaryRoot = Path.GetTempPath();
            string persistentRoot = Application.persistentDataPath;
            string isolatedRoot = EditorGameTestModeBootstrap.BuildExpectedIsolatedRoot(
                temporaryRoot,
                sessionId);
            ProductionSceneRecord boot = ProductionSceneDescriptor.ShellFoundationOrdered[0];
            GetReloadPolicy(out bool fullDomainReload, out bool fullSceneReload);

            if (!EditorGameTestModeBootstrap.TryCreatePlan(
                    sessionId,
                    temporaryRoot,
                    persistentRoot,
                    isolatedRoot,
                    boot.AssetPath,
                    boot.AssetGuid,
                    fullDomainReload,
                    fullSceneReload,
                    out EditorGameTestModePlan plan,
                    out _,
                    out message))
            {
                SetStatus(message);
                return false;
            }

            SceneAsset bootScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(plan.BootScenePath);
            SceneAsset priorStartScene = EditorSceneManager.playModeStartScene;
            bool priorStartSceneWasNull = priorStartScene == null;
            string priorStartScenePath = priorStartScene == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(priorStartScene);
            string priorStartSceneGuid = string.IsNullOrEmpty(priorStartScenePath)
                ? string.Empty
                : AssetDatabase.AssetPathToGUID(priorStartScenePath);
            var recoveryRecord = new EditorGameTestModeRecoveryRecord(
                plan,
                EditorGameTestModeRecoveryStage.Starting,
                priorStartSceneGuid,
                priorStartSceneWasNull);
            if (!EditorGameTestModeBootstrap.TryWriteDurableRecoveryRecord(
                    recoveryRecord,
                    out _,
                    out message))
            {
                SetStatus(message);
                return false;
            }

            bool rootMayExist = false;

            try
            {
                rootMayExist = true;
                if (!EditorGameTestModeBootstrap.TryCreateOwnedRoot(
                        plan,
                        out _,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                if (!EditorGameTestModeBootstrap.TryArm(plan, out _, out message))
                {
                    throw new InvalidOperationException(message);
                }

                WriteActiveSession(
                    plan,
                    priorStartScenePath,
                    priorStartSceneGuid,
                    priorStartSceneWasNull);
                EditorSceneManager.playModeStartScene = bootScene;
                SetStatus("Starting fresh isolated profile " + plan.SessionId.Substring(0, 8) + "…");
                SessionState.SetBool(GameTestModeSessionKeys.TransitioningToPlay, true);
                SessionState.EraseBool(
                    GameTestModeSessionKeys.PreAwakeRearmCompleted);
                SessionState.EraseBool(GameTestModeSessionKeys.Stopping);
                SessionState.EraseBool(GameTestModeSessionKeys.ReloadInterrupted);
                EditorApplication.isPlaying = true;
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                EditorGameTestModeBootstrap.Disarm();
                EditorSceneManager.playModeStartScene = priorStartScene;

                if (rootMayExist && Directory.Exists(plan.IsolatedSaveRoot) &&
                    !EditorGameTestModeBootstrap.TryDeleteOwnedRoot(plan, out _, out string cleanupMessage))
                {
                    WriteRecoveryPlan(
                        plan,
                        priorStartSceneGuid,
                        priorStartSceneWasNull);
                    SessionState.SetString(GameTestModeSessionKeys.LastRoot, plan.IsolatedSaveRoot);
                    message = ex.Message + " " + cleanupMessage;
                }
                else
                {
                    ClearActiveSession(
                        retainRecoveryPlan: false,
                        expectedSessionId: plan.SessionId);
                    message = ex.Message;
                }

                SetStatus(message);
                return false;
            }
        }

        internal static void StopActiveSession()
        {
            if (!IsSessionActive)
            {
                if (IsRecoveryPending)
                {
                    CompletePendingRecovery();
                }

                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetStatus("Stopping isolated game-test session…");
                EditorApplication.isPlaying = false;
                return;
            }

            CompleteActiveSession();
        }

        [MenuItem(MenuRoot + "Recover / Clean Up Isolated Session", priority = 20)]
        private static void RecoverIsolatedSession()
        {
            StopActiveSession();
        }

        [MenuItem(MenuRoot + "Recover / Clean Up Isolated Session", validate = true)]
        private static bool ValidateRecoverIsolatedSession()
        {
            return IsSessionActive || IsRecoveryPending;
        }

        [MenuItem(
            MenuRoot + "Forget Invalid Recovery Record (Delete No Files)",
            priority = 21)]
        private static void ForgetInvalidRecoveryRecordFromMenu()
        {
            ForgetInvalidRecoveryRecordWithoutDeletingFiles();
        }

        [MenuItem(
            MenuRoot + "Forget Invalid Recovery Record (Delete No Files)",
            validate = true)]
        private static bool ValidateForgetInvalidRecoveryRecordFromMenu()
        {
            return HasInvalidDurableRecoveryRecord &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        internal static void ForgetInvalidRecoveryRecordWithoutDeletingFiles()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SetStatus(
                    "Exit Play Mode before forgetting an invalid recovery record. No files were changed.");
                return;
            }

            if (!HasInvalidDurableRecoveryRecord)
            {
                SetStatus("No invalid isolated-test recovery record was found.");
                return;
            }

            EditorGameTestModeBootstrap.ForgetInvalidDurableRecoveryRecordWithoutDeletingFiles();
            ClearActiveSession(retainRecoveryPlan: false, expectedSessionId: string.Empty);
            SetStatus(
                "The invalid project-scoped recovery record was forgotten. No file or directory was deleted.");
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!IsSessionActive)
            {
                if (state == PlayModeStateChange.ExitingEditMode && IsRecoveryPending)
                {
                    SetStatus(
                        "Play Mode was blocked because an isolated-test recovery record still requires recovery or an explicit no-file-delete forget action.");
                    EditorApplication.isPlaying = false;
                }

                return;
            }

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                {
                    string expectedSession = SessionState.GetString(
                        GameTestModeSessionKeys.SessionId,
                        string.Empty);
                    EditorApplication.delayCall += () => VerifyEnteredPlayMode(expectedSession);
                    break;
                }
                case PlayModeStateChange.ExitingPlayMode:
                    SessionState.SetBool(GameTestModeSessionKeys.Stopping, true);
                    SetStatus("Stopping isolated game-test session…");
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    CompleteActiveSession();
                    break;
            }
        }

        private static void HandleBeforeAssemblyReload()
        {
            if (!IsSessionActive ||
                !EditorApplication.isPlaying ||
                SessionState.GetBool(GameTestModeSessionKeys.Stopping, false))
            {
                return;
            }

            bool transitioning = SessionState.GetBool(
                GameTestModeSessionKeys.TransitioningToPlay,
                false);
            bool preAwakeRearmCompleted = SessionState.GetBool(
                GameTestModeSessionKeys.PreAwakeRearmCompleted,
                false);
            if (transitioning && !preAwakeRearmCompleted)
            {
                // The initial full-domain reload has not yet reached the
                // pre-Awake re-arm callback. Once re-armed, any further reload
                // in the transition-verification window must fail closed.
                return;
            }

            string sessionId = SessionState.GetString(
                GameTestModeSessionKeys.SessionId,
                string.Empty);
            SessionState.SetBool(GameTestModeSessionKeys.ReloadInterrupted, true);
            EditorGameTestModeBootstrap.TryUpdateDurableRecoveryStage(
                sessionId,
                EditorGameTestModeRecoveryStage.Recovery,
                out _);
            SetStatus(
                "An unexpected script/domain reload interrupted the isolated run. Play Mode is stopping; the exact temp root will be retained or safely cleaned in Edit Mode.");
            EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                "Unexpected script/domain reload");
        }

        private static void HandleEditorFocusChanged(bool hasFocus)
        {
            if (!IsSessionActive || !EditorApplication.isPlaying)
            {
                return;
            }

            if (!EditorGameTestModeBootstrap.TryNotifyEditorFocusChanged(
                    hasFocus,
                    out EditorGameTestModeFocusSnapshot snapshot,
                    out string message))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    string.IsNullOrEmpty(message)
                        ? "Editor focus lifecycle mismatch"
                        : message);
                return;
            }

            if (!hasFocus &&
                !FirstUserGameTestRuntimeHost.TrySynchronizeFocusSuspension(
                    snapshot,
                    out string suspensionMessage))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    string.IsNullOrEmpty(suspensionMessage)
                        ? "The isolated first-user state could not be suspended synchronously"
                        : suspensionMessage);
                return;
            }

            SetStatus(hasFocus
                ? "Editor focus returned. Revalidating the isolated session before input resumes…"
                : "Playtest suspended while the Editor is out of focus. Progress and gameplay input are paused safely.");
        }

        private static void HandleEditorPauseStateChanged(PauseState state)
        {
            if (state != PauseState.Paused || !IsSessionActive || !EditorApplication.isPlaying)
            {
                return;
            }

            SetStatus("The Editor was paused. The isolated run is stopping before it can resume with stale authority.");
            EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary("Editor pause");
        }

        private static void RecoverDurableRecordAfterEditorRestart()
        {
            if (SessionState.GetBool(GameTestModeSessionKeys.Active, false) ||
                SessionState.GetBool(GameTestModeSessionKeys.RecoveryPending, false) ||
                !EditorGameTestModeBootstrap.HasDurableRecoveryRecord)
            {
                return;
            }

            if (!EditorGameTestModeBootstrap.TryReadDurableRecoveryRecord(
                    out EditorGameTestModeRecoveryRecord record,
                    out _,
                    out string message))
            {
                SessionState.SetBool(GameTestModeSessionKeys.RecoveryPending, true);
                SetStatus(
                    "A malformed project-scoped recovery record was retained without deleting any path. Preference key: " +
                    EditorGameTestModeBootstrap.DurableRecoveryPreferenceKey + ". " + message);
                return;
            }

            WriteRecoveryPlan(
                record.Plan,
                record.PreviousStartSceneGuid,
                record.PreviousStartSceneWasNull);
            SessionState.SetString(
                GameTestModeSessionKeys.PreviousStartScenePath,
                record.PreviousStartSceneWasNull
                    ? string.Empty
                    : AssetDatabase.GUIDToAssetPath(record.PreviousStartSceneGuid));
            SessionState.SetString(
                GameTestModeSessionKeys.LastRoot,
                record.Plan.IsolatedSaveRoot);

            bool startSceneRestored = RestorePreviousStartScene(
                record.Plan.SessionId,
                out string restoreMessage);
            SetStatus(
                "Recovered a stale isolated-session record. No path was auto-deleted. " +
                (startSceneRestored
                    ? "The prior Play Mode start scene was restored. "
                    : restoreMessage + " ") +
                "Review or clean the exact retained root: " + record.Plan.IsolatedSaveRoot);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGameTestModeBootstrap.EnterFailClosedState(
                    record.Plan.SessionId,
                    "A stale durable recovery record was found while Play Mode was active.");
                EditorApplication.isPlaying = false;
            }
        }

        private static void RecoverOrVerifyAfterReload()
        {
            RecoverDurableRecordAfterEditorRestart();

            if (EditorApplication.isPlaying &&
                EditorGameTestModeBootstrap.TryReadDurableRecoveryRecord(
                    out EditorGameTestModeRecoveryRecord durableRecord,
                    out _,
                    out _) &&
                durableRecord.Stage != EditorGameTestModeRecoveryStage.Starting &&
                !SessionState.GetBool(GameTestModeSessionKeys.TransitioningToPlay, false))
            {
                FailClosedAndAbort(
                    durableRecord.Plan.SessionId,
                    "A running isolated session crossed an unexpected script/domain reload and was blocked.");
                return;
            }

            if (!IsSessionActive)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                string expectedSession = SessionState.GetString(
                    GameTestModeSessionKeys.SessionId,
                    string.Empty);
                VerifyEnteredPlayMode(expectedSession);
            }
            else if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CompleteActiveSession();
            }
        }

        private static void VerifyEnteredPlayMode(string expectedSession)
        {
            if (!IsSessionActive ||
                !EditorApplication.isPlaying ||
                !string.Equals(
                    expectedSession,
                    SessionState.GetString(GameTestModeSessionKeys.SessionId, string.Empty),
                    StringComparison.Ordinal))
            {
                return;
            }

            if (!EditorGameTestModeBootstrap.TryVerifyActiveRuntime(out _, out string message))
            {
                FailClosedAndAbort(
                    expectedSession,
                    "Runtime isolation verification failed: " + message);
                return;
            }

            if (!EditorGameTestModeBootstrap.TryUpdateDurableRecoveryStage(
                    expectedSession,
                    EditorGameTestModeRecoveryStage.Running,
                    out message))
            {
                FailClosedAndAbort(
                    expectedSession,
                    "Durable recovery publication failed: " + message);
                return;
            }

            SessionState.EraseBool(GameTestModeSessionKeys.TransitioningToPlay);
            SessionState.EraseBool(GameTestModeSessionKeys.PreAwakeRearmCompleted);
            SessionState.EraseBool(GameTestModeSessionKeys.ReloadInterrupted);

            SetStatus(
                "Running isolated test profile " + expectedSession.Substring(0, 8) +
                ". Exact temp root: " + EditorGameTestModeBootstrap.ActiveSaveRoot);
        }

        private static void CompleteActiveSession()
        {
            if (!IsSessionActive)
            {
                return;
            }

            string expectedSession = SessionState.GetString(
                GameTestModeSessionKeys.SessionId,
                string.Empty);
            bool planRead = TryReadPlan(
                fullDomainReload: true,
                fullSceneReload: true,
                out EditorGameTestModePlan plan,
                out string planMessage);

            EditorGameTestModeBootstrap.TryUpdateDurableRecoveryStage(
                expectedSession,
                EditorGameTestModeRecoveryStage.Recovery,
                out _);

            EditorGameTestModeBootstrap.Disarm();
            bool startSceneRestored = RestorePreviousStartScene(
                expectedSession,
                out string restoreMessage);

            string finalStatus;
            if (!planRead)
            {
                finalStatus = "Isolation stopped, but its plan could not be reconstructed: " + planMessage;
            }
            else if (!Directory.Exists(plan.IsolatedSaveRoot))
            {
                finalStatus = "Isolated session stopped; its GUID-owned temp root was already absent.";
            }
            else if (EditorGameTestModeBootstrap.TryDeleteOwnedRoot(plan, out _, out string cleanupMessage))
            {
                finalStatus = "Isolated session completed and its GUID-owned temp profile was removed.";
            }
            else
            {
                SessionState.SetString(GameTestModeSessionKeys.LastRoot, plan.IsolatedSaveRoot);
                finalStatus = cleanupMessage + " Retained evidence: " + plan.IsolatedSaveRoot;
            }


            if (!startSceneRestored)
            {
                finalStatus += " " + restoreMessage;
            }

            bool retainRecoveryPlan = !planRead || Directory.Exists(plan.IsolatedSaveRoot);
            ClearActiveSession(retainRecoveryPlan, expectedSession);
            SetStatus(finalStatus);
        }

        private static void CompletePendingRecovery()
        {
            if (!IsRecoveryPending || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!TryReadPlan(
                    fullDomainReload: true,
                    fullSceneReload: true,
                    out EditorGameTestModePlan plan,
                    out string planMessage))
            {
                SetStatus("Retained isolation metadata is invalid; no path was deleted: " + planMessage);
                return;
            }

            EditorGameTestModeBootstrap.Disarm();
            bool startSceneRestored = RestorePreviousStartScene(
                plan.SessionId,
                out string restoreMessage);
            if (!Directory.Exists(plan.IsolatedSaveRoot))
            {
                ClearActiveSession(
                    retainRecoveryPlan: false,
                    expectedSessionId: plan.SessionId);
                SetStatus(
                    "The stale isolated record had no remaining temp root. " +
                    (startSceneRestored ? "The prior Play Mode start scene was restored." : restoreMessage));
                return;
            }

            if (EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                    plan,
                    out _,
                    out string cleanupMessage))
            {
                ClearActiveSession(
                    retainRecoveryPlan: false,
                    expectedSessionId: plan.SessionId);
                SetStatus(
                    "The retained GUID-owned isolated profile was safely removed. " +
                    (startSceneRestored ? string.Empty : restoreMessage));
                return;
            }

            SessionState.SetString(GameTestModeSessionKeys.LastRoot, plan.IsolatedSaveRoot);
            SetStatus(cleanupMessage + " Retained evidence: " + plan.IsolatedSaveRoot);
        }

        private static bool RestorePreviousStartScene(
            string expectedSession,
            out string message)
        {
            if (!string.Equals(
                    expectedSession,
                    SessionState.GetString(GameTestModeSessionKeys.SessionId, string.Empty),
                    StringComparison.Ordinal))
            {
                message = "The prior Play Mode start-scene record did not match this session; the start scene was cleared for safety.";
                EditorSceneManager.playModeStartScene = null;
                return false;
            }

            if (SessionState.GetBool(GameTestModeSessionKeys.PreviousStartSceneWasNull, false))
            {
                EditorSceneManager.playModeStartScene = null;
                message = string.Empty;
                return true;
            }

            string priorGuid = SessionState.GetString(
                GameTestModeSessionKeys.PreviousStartSceneGuid,
                string.Empty);
            string priorPath = string.IsNullOrEmpty(priorGuid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(priorGuid);
            SceneAsset priorScene = string.IsNullOrEmpty(priorPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(priorPath);
            if (priorScene == null)
            {
                string recordedPath = SessionState.GetString(
                    GameTestModeSessionKeys.PreviousStartScenePath,
                    string.Empty);
                EditorSceneManager.playModeStartScene = null;
                message =
                    "The previous Play Mode start scene no longer resolves by GUID; the setting was cleared instead of leaving Boot assigned. Recorded path: " +
                    recordedPath;
                Debug.LogError("[AL-ISOLATED-TEST-RESTORE-BLOCKED] " + message);
                return false;
            }

            EditorSceneManager.playModeStartScene = priorScene;
            message = string.Empty;
            return true;
        }

        private static bool TryReadPlan(
            bool fullDomainReload,
            bool fullSceneReload,
            out EditorGameTestModePlan plan,
            out string message)
        {
            return EditorGameTestModeBootstrap.TryCreatePlan(
                SessionState.GetString(GameTestModeSessionKeys.SessionId, string.Empty),
                SessionState.GetString(GameTestModeSessionKeys.TemporaryRoot, string.Empty),
                SessionState.GetString(GameTestModeSessionKeys.PersistentRoot, string.Empty),
                SessionState.GetString(GameTestModeSessionKeys.IsolatedRoot, string.Empty),
                SessionState.GetString(GameTestModeSessionKeys.BootScenePath, string.Empty),
                SessionState.GetString(GameTestModeSessionKeys.BootSceneGuid, string.Empty),
                fullDomainReload,
                fullSceneReload,
                out plan,
                out _,
                out message);
        }

        private static void WriteActiveSession(
            EditorGameTestModePlan plan,
            string previousStartScenePath,
            string previousStartSceneGuid,
            bool previousStartSceneWasNull)
        {
            SessionState.SetString(GameTestModeSessionKeys.SessionId, plan.SessionId);
            SessionState.SetString(GameTestModeSessionKeys.TemporaryRoot, plan.SystemTemporaryRoot);
            SessionState.SetString(GameTestModeSessionKeys.PersistentRoot, plan.PersistentDataRoot);
            SessionState.SetString(GameTestModeSessionKeys.IsolatedRoot, plan.IsolatedSaveRoot);
            SessionState.SetString(GameTestModeSessionKeys.BootScenePath, plan.BootScenePath);
            SessionState.SetString(GameTestModeSessionKeys.BootSceneGuid, plan.BootSceneGuid);
            SessionState.SetString(
                GameTestModeSessionKeys.PreviousStartScenePath,
                previousStartScenePath ?? string.Empty);
            SessionState.SetString(
                GameTestModeSessionKeys.PreviousStartSceneGuid,
                previousStartSceneGuid ?? string.Empty);
            SessionState.SetBool(
                GameTestModeSessionKeys.PreviousStartSceneWasNull,
                previousStartSceneWasNull);
            SessionState.SetString(GameTestModeSessionKeys.LastRoot, plan.IsolatedSaveRoot);
            SessionState.SetBool(GameTestModeSessionKeys.Active, true);
            SessionState.SetBool(GameTestModeSessionKeys.FullDomainReload, true);
            SessionState.SetBool(GameTestModeSessionKeys.FullSceneReload, true);
            SessionState.EraseBool(GameTestModeSessionKeys.RecoveryPending);
        }

        private static void WriteRecoveryPlan(
            EditorGameTestModePlan plan,
            string previousStartSceneGuid,
            bool previousStartSceneWasNull)
        {
            SessionState.EraseBool(GameTestModeSessionKeys.Active);
            SessionState.SetBool(GameTestModeSessionKeys.RecoveryPending, true);
            SessionState.SetString(GameTestModeSessionKeys.SessionId, plan.SessionId);
            SessionState.SetString(GameTestModeSessionKeys.TemporaryRoot, plan.SystemTemporaryRoot);
            SessionState.SetString(GameTestModeSessionKeys.PersistentRoot, plan.PersistentDataRoot);
            SessionState.SetString(GameTestModeSessionKeys.IsolatedRoot, plan.IsolatedSaveRoot);
            SessionState.SetString(GameTestModeSessionKeys.BootScenePath, plan.BootScenePath);
            SessionState.SetString(GameTestModeSessionKeys.BootSceneGuid, plan.BootSceneGuid);
            SessionState.SetString(
                GameTestModeSessionKeys.PreviousStartSceneGuid,
                previousStartSceneGuid ?? string.Empty);
            SessionState.SetString(
                GameTestModeSessionKeys.PreviousStartScenePath,
                previousStartSceneWasNull || string.IsNullOrEmpty(previousStartSceneGuid)
                    ? string.Empty
                    : AssetDatabase.GUIDToAssetPath(previousStartSceneGuid));
            SessionState.SetBool(
                GameTestModeSessionKeys.PreviousStartSceneWasNull,
                previousStartSceneWasNull);
            EditorGameTestModeBootstrap.TryWriteDurableRecoveryRecord(
                new EditorGameTestModeRecoveryRecord(
                    plan,
                    EditorGameTestModeRecoveryStage.Recovery,
                    previousStartSceneGuid,
                    previousStartSceneWasNull),
                out _,
                out _);
        }

        private static void ClearActiveSession(
            bool retainRecoveryPlan = false,
            string expectedSessionId = "")
        {
            SessionState.EraseBool(GameTestModeSessionKeys.Active);
            if (retainRecoveryPlan)
            {
                SessionState.SetBool(GameTestModeSessionKeys.RecoveryPending, true);
            }
            else
            {
                SessionState.EraseBool(GameTestModeSessionKeys.RecoveryPending);
                SessionState.EraseString(GameTestModeSessionKeys.SessionId);
                SessionState.EraseString(GameTestModeSessionKeys.TemporaryRoot);
                SessionState.EraseString(GameTestModeSessionKeys.PersistentRoot);
                SessionState.EraseString(GameTestModeSessionKeys.IsolatedRoot);
                SessionState.EraseString(GameTestModeSessionKeys.BootScenePath);
                SessionState.EraseString(GameTestModeSessionKeys.BootSceneGuid);
                if (!string.IsNullOrWhiteSpace(expectedSessionId))
                {
                    EditorGameTestModeBootstrap.TryClearDurableRecoveryRecord(
                        expectedSessionId,
                        out _);
                }
            }

            SessionState.EraseBool(GameTestModeSessionKeys.FullDomainReload);
            SessionState.EraseBool(GameTestModeSessionKeys.FullSceneReload);
            SessionState.EraseBool(GameTestModeSessionKeys.TransitioningToPlay);
            SessionState.EraseBool(GameTestModeSessionKeys.PreAwakeRearmCompleted);
            SessionState.EraseBool(GameTestModeSessionKeys.Stopping);
            SessionState.EraseBool(GameTestModeSessionKeys.ReloadInterrupted);
            if (!retainRecoveryPlan)
            {
                SessionState.EraseString(GameTestModeSessionKeys.PreviousStartScenePath);
                SessionState.EraseString(GameTestModeSessionKeys.PreviousStartSceneGuid);
                SessionState.EraseBool(GameTestModeSessionKeys.PreviousStartSceneWasNull);
            }
        }

        private static void GetReloadPolicy(out bool fullDomainReload, out bool fullSceneReload)
        {
            if (!EditorSettings.enterPlayModeOptionsEnabled)
            {
                fullDomainReload = true;
                fullSceneReload = true;
                return;
            }

            EnterPlayModeOptions options = EditorSettings.enterPlayModeOptions;
            fullDomainReload = (options & EnterPlayModeOptions.DisableDomainReload) == 0;
            fullSceneReload = (options & EnterPlayModeOptions.DisableSceneReload) == 0;
        }

        private static void GetReloadPolicy(
            EnterPlayModeOptions transitionOptions,
            out bool fullDomainReload,
            out bool fullSceneReload)
        {
            fullDomainReload = (transitionOptions & EnterPlayModeOptions.DisableDomainReload) == 0;
            fullSceneReload = (transitionOptions & EnterPlayModeOptions.DisableSceneReload) == 0;
        }

        private static void AbortTransition(string message)
        {
            SetStatus(message);
            Debug.LogError("[AL-ISOLATED-TEST-BLOCKED] " + message);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
            }

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.isPlaying = false;
                }
                else if (IsSessionActive)
                {
                    CompleteActiveSession();
                }
            };
        }

        private static void FailClosedAndAbort(string sessionId, string message)
        {
            EditorGameTestModeBootstrap.TryUpdateDurableRecoveryStage(
                sessionId,
                EditorGameTestModeRecoveryStage.Recovery,
                out _);
            try
            {
                EditorGameTestModeBootstrap.EnterFailClosedState(sessionId, message);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AL-ISOLATED-TEST-BLOCKED] Failed to render fail-closed diagnostics after the save guard was requested: " +
                    ex.Message);
            }

            try
            {
                AbortTransition(message);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AL-ISOLATED-TEST-BLOCKED] Play Mode abort raised an exception after the save guard was requested: " +
                    ex.Message);
            }
        }

        private static void SetStatus(string message)
        {
            SessionState.SetString(
                GameTestModeSessionKeys.LastStatus,
                string.IsNullOrWhiteSpace(message) ? "No status available." : message);
            GameTestModeWindow.RepaintOpenWindow();
        }
    }

    internal sealed class GameTestModeWindow : EditorWindow
    {
        private const string WindowTitle = "AL Test Mode";
        private static GameTestModeWindow _openWindow;
        private readonly GameTestModeControlPanelCommandGate _commandGate =
            new GameTestModeControlPanelCommandGate();
        private string _lastPrimaryControl = string.Empty;
        private bool _initialFocusPending = true;

        [MenuItem("Another Life/Test Mode/Control Panel", priority = 1)]
        private static void Open()
        {
            _openWindow = GetWindow<GameTestModeWindow>(utility: false, title: WindowTitle, focus: true);
            _openWindow.minSize = new Vector2(500f, 480f);
        }

        internal static void RepaintOpenWindow()
        {
            if (_openWindow != null)
            {
                _openWindow.Repaint();
            }
        }

        private void OnEnable()
        {
            _openWindow = this;
            _initialFocusPending = true;
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            if (ReferenceEquals(_openWindow, this))
            {
                _openWindow = null;
            }
        }

        private void OnGUI()
        {
            bool invalidRecovery = GameTestModeEditorCoordinator.HasInvalidDurableRecoveryRecord;
            bool sessionActive = GameTestModeEditorCoordinator.IsSessionActive;
            bool recoveryPending = GameTestModeEditorCoordinator.IsRecoveryPending;
            bool playModeActive = EditorApplication.isPlayingOrWillChangePlaymode;
            bool canStart = GameTestModeEditorCoordinator.CanStart(out string rawBlocker);
            GameTestModeControlPanelView view = GameTestModeControlPanelPresentation.Build(
                sessionActive,
                recoveryPending,
                invalidRecovery,
                playModeActive,
                canStart,
                GameTestModeEditorCoordinator.CurrentStatus,
                rawBlocker);

            if (!sessionActive && !recoveryPending)
            {
                _commandGate.Reset();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Another Life — First User Experience", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Runs a temporary, isolated Editor playtest. It never opens, replaces, or deletes your normal Another Life save.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "DEVELOPMENT PLAYTEST — NOT PRODUCTION — SESSION ONLY",
                MessageType.Warning);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Journey", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                GameTestModeControlPanelPresentation.JourneyChecklist,
                MessageType.None);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Current state", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(view.CurrentState, MessageType.Info);
            if (!string.IsNullOrEmpty(view.Blocker))
            {
                EditorGUILayout.HelpBox(view.Blocker, MessageType.Warning);
            }

            GUILayout.FlexibleSpace();
            string primaryControl;
            if (invalidRecovery)
            {
                primaryControl = GameTestModeControlPanelPresentation.ForgetControlName;
                GUI.SetNextControlName(primaryControl);
                GUI.enabled = view.PrimaryActionEnabled;
                if (GUILayout.Button(
                        GameTestModeControlPanelPresentation.ForgetAction,
                        GUILayout.Height(52f)))
                {
                    _commandGate.TryCleanUp(
                        GameTestModeEditorCoordinator
                            .ForgetInvalidRecoveryRecordWithoutDeletingFiles);
                }

                GUI.enabled = true;
            }
            else if (sessionActive || recoveryPending)
            {
                bool running = sessionActive;
                primaryControl = running
                    ? GameTestModeControlPanelPresentation.StopControlName
                    : GameTestModeControlPanelPresentation.CleanupControlName;
                GUI.SetNextControlName(primaryControl);
                GUI.enabled = view.PrimaryActionEnabled;
                string action = running
                    ? GameTestModeControlPanelPresentation.StopAction
                    : GameTestModeControlPanelPresentation.CleanupAction;
                if (GUILayout.Button(action, GUILayout.Height(52f)))
                {
                    _commandGate.TryCleanUp(GameTestModeEditorCoordinator.StopActiveSession);
                }

                GUI.enabled = true;
            }
            else
            {
                primaryControl = GameTestModeControlPanelPresentation.StartControlName;
                GUI.SetNextControlName(primaryControl);
                GUI.enabled = view.PrimaryActionEnabled;
                if (GUILayout.Button(
                        GameTestModeControlPanelPresentation.StartAction,
                        GUILayout.Height(58f)))
                {
                    string message = string.Empty;
                    if (!_commandGate.TryStart(() =>
                            GameTestModeEditorCoordinator.TryStartFreshFirstUser(out message)) &&
                        !string.IsNullOrWhiteSpace(message))
                    {
                        EditorUtility.DisplayDialog(
                            "Another Life Test Mode",
                            GameTestModeControlPanelPresentation.Build(
                                false,
                                false,
                                false,
                                false,
                                false,
                                string.Empty,
                                message).Blocker,
                            "Close");
                    }
                }

                GUI.enabled = true;
            }

            ApplyInitialFocus(primaryControl);

            EditorGUILayout.Space(8f);
        }

        private void ApplyInitialFocus(string primaryControl)
        {
            if (!string.Equals(
                    _lastPrimaryControl,
                    primaryControl,
                    StringComparison.Ordinal))
            {
                _lastPrimaryControl = primaryControl;
                _initialFocusPending = true;
            }

            if (_initialFocusPending && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(primaryControl);
                _initialFocusPending = false;
            }
        }
    }
}
#endif
