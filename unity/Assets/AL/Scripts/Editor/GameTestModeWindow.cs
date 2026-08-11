#if UNITY_EDITOR
using System;
using System.IO;
using AL.Core.Scenes;
using AL.Development;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
        internal const string LastStatus = "AL.GameTestMode.LastStatus";
        internal const string LastRoot = "AL.GameTestMode.LastRoot";
    }

    [InitializeOnLoad]
    internal static class GameTestModeEditorCoordinator
    {
        private const string MenuRoot = "Another Life/Test Mode/";

        static GameTestModeEditorCoordinator()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.delayCall += RecoverOrVerifyAfterReload;
        }

        internal static bool IsSessionActive =>
            SessionState.GetBool(GameTestModeSessionKeys.Active, false);

        internal static bool IsRecoveryPending =>
            SessionState.GetBool(GameTestModeSessionKeys.RecoveryPending, false);

        internal static string CurrentStatus =>
            SessionState.GetString(
                GameTestModeSessionKeys.LastStatus,
                "Ready for an isolated fresh-profile run.");

        internal static string LastRoot =>
            SessionState.GetString(GameTestModeSessionKeys.LastRoot, string.Empty);

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
                }
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
            bool rootCreated = false;

            try
            {
                if (!EditorGameTestModeBootstrap.TryCreateOwnedRoot(
                        plan,
                        out _,
                        out message))
                {
                    throw new InvalidOperationException(message);
                }

                rootCreated = true;

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
                EditorApplication.isPlaying = true;
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                EditorGameTestModeBootstrap.Disarm();
                EditorSceneManager.playModeStartScene = priorStartScene;

                if (rootCreated &&
                    !EditorGameTestModeBootstrap.TryDeleteOwnedRoot(plan, out _, out string cleanupMessage))
                {
                    WriteRecoveryPlan(plan);
                    SessionState.SetString(GameTestModeSessionKeys.LastRoot, plan.IsolatedSaveRoot);
                    message = ex.Message + " " + cleanupMessage;
                }
                else
                {
                    ClearActiveSession(retainRecoveryPlan: false);
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

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!IsSessionActive)
            {
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
                    SetStatus("Stopping isolated game-test session…");
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    CompleteActiveSession();
                    break;
            }
        }

        private static void RecoverOrVerifyAfterReload()
        {
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
                AbortTransition("Runtime isolation verification failed: " + message);
                return;
            }

            SetStatus(
                "Running isolated test profile " + expectedSession.Substring(0, 8) +
                ". Real developer saves are untouched.");
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

            EditorGameTestModeBootstrap.Disarm();
            bool startSceneRestored = RestorePreviousStartScene(
                expectedSession,
                out string restoreMessage);

            string finalStatus;
            if (!planRead)
            {
                finalStatus = "Isolation stopped, but its plan could not be reconstructed: " + planMessage;
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

            bool retainRecoveryPlan = planRead && Directory.Exists(plan.IsolatedSaveRoot);
            ClearActiveSession(retainRecoveryPlan);
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
            if (EditorGameTestModeBootstrap.TryDeleteOwnedRoot(
                    plan,
                    out _,
                    out string cleanupMessage))
            {
                ClearActiveSession(retainRecoveryPlan: false);
                SetStatus("The retained GUID-owned isolated profile was safely removed.");
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

        private static void WriteRecoveryPlan(EditorGameTestModePlan plan)
        {
            SessionState.EraseBool(GameTestModeSessionKeys.Active);
            SessionState.SetBool(GameTestModeSessionKeys.RecoveryPending, true);
            SessionState.SetString(GameTestModeSessionKeys.SessionId, plan.SessionId);
            SessionState.SetString(GameTestModeSessionKeys.TemporaryRoot, plan.SystemTemporaryRoot);
            SessionState.SetString(GameTestModeSessionKeys.PersistentRoot, plan.PersistentDataRoot);
            SessionState.SetString(GameTestModeSessionKeys.IsolatedRoot, plan.IsolatedSaveRoot);
            SessionState.SetString(GameTestModeSessionKeys.BootScenePath, plan.BootScenePath);
            SessionState.SetString(GameTestModeSessionKeys.BootSceneGuid, plan.BootSceneGuid);
        }

        private static void ClearActiveSession(bool retainRecoveryPlan = false)
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
            }

            SessionState.EraseBool(GameTestModeSessionKeys.FullDomainReload);
            SessionState.EraseBool(GameTestModeSessionKeys.FullSceneReload);
            SessionState.EraseString(GameTestModeSessionKeys.PreviousStartScenePath);
            SessionState.EraseString(GameTestModeSessionKeys.PreviousStartSceneGuid);
            SessionState.EraseBool(GameTestModeSessionKeys.PreviousStartSceneWasNull);
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

        [MenuItem("Another Life/Test Mode/Control Panel", priority = 1)]
        private static void Open()
        {
            _openWindow = GetWindow<GameTestModeWindow>(utility: false, title: WindowTitle, focus: true);
            _openWindow.minSize = new Vector2(440f, 300f);
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
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Another Life — Isolated Game Test Mode", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Starts the production Boot scene with a new GUID-owned profile under the system temp folder. It never moves, replaces, or opens your normal Another Life save root.",
                MessageType.Info);
            EditorGUILayout.HelpBox(
                "This exercises what is currently built. It is not release, cinematic, device, balance, or visual-approval evidence.",
                MessageType.Warning);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(
                GameTestModeEditorCoordinator.CurrentStatus,
                EditorStyles.textArea,
                GUILayout.MinHeight(58f));

            string lastRoot = GameTestModeEditorCoordinator.LastRoot;
            if (!string.IsNullOrWhiteSpace(lastRoot))
            {
                EditorGUILayout.LabelField("Last isolated root", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(lastRoot, EditorStyles.textField, GUILayout.Height(20f));
            }

            GUILayout.FlexibleSpace();
            if (GameTestModeEditorCoordinator.IsSessionActive ||
                GameTestModeEditorCoordinator.IsRecoveryPending)
            {
                string action = EditorApplication.isPlayingOrWillChangePlaymode
                    ? "Stop Isolated Test"
                    : "Recover / Clean Up Isolated Session";
                if (GUILayout.Button(action, GUILayout.Height(40f)))
                {
                    GameTestModeEditorCoordinator.StopActiveSession();
                }
            }
            else
            {
                bool canStart = GameTestModeEditorCoordinator.CanStart(out string blocker);
                GUI.enabled = canStart;
                if (GUILayout.Button("Fresh First User (Isolated)", GUILayout.Height(44f)))
                {
                    if (!GameTestModeEditorCoordinator.TryStartFreshFirstUser(out string message))
                    {
                        EditorUtility.DisplayDialog("Another Life Test Mode", message, "Close");
                    }
                }

                GUI.enabled = true;
                if (!canStart)
                {
                    EditorGUILayout.HelpBox(blocker, MessageType.Error);
                }
            }

            EditorGUILayout.Space(8f);
        }
    }
}
#endif
