#if !UNITY_EDITOR
#error The isolated first-user runtime policy is Editor-only.
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Development;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Editor.Development.FirstUserGameTest
{
    /// <summary>
    /// Prevents the production Bootloader update loop from attempting economy or construction
    /// mutations while the exact isolated profile intentionally remains non-writable. The policy
    /// never changes profile authority and never edits the production Bootloader contract.
    /// </summary>
    internal sealed class FirstUserIsolatedRuntimePolicy
    {
        private static FirstUserIsolatedRuntimePolicy _active;
        private static readonly FieldInfo RuntimeActiveField = typeof(Bootloader).GetField(
            "_runtimeActive",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo StandbyForOwnershipField = typeof(Bootloader).GetField(
            "_standbyForOwnership",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly string _sessionId;
        private readonly string _isolatedRoot;
        private readonly ISaveGameService _saveService;
        private readonly Bootloader _bootloader;
        private readonly int _bootloaderInstanceId;
        private readonly int _sceneHandle;
        private bool _awaitingOwnershipHandoff;

        private FirstUserIsolatedRuntimePolicy(
            string sessionId,
            string isolatedRoot,
            ISaveGameService saveService,
            Bootloader bootloader,
            Scene scene,
            bool awaitingOwnershipHandoff)
        {
            _sessionId = sessionId;
            _isolatedRoot = isolatedRoot;
            _saveService = saveService;
            _bootloader = bootloader;
            _bootloaderInstanceId = bootloader.GetInstanceID();
            _sceneHandle = scene.handle;
            _awaitingOwnershipHandoff = awaitingOwnershipHandoff;
        }

        internal static bool IsInstalled => _active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayModeEntry()
        {
            // SubsystemRegistration runs even when domain reload is disabled. Never retain an
            // Editor-only Bootloader owner across Play Mode sessions.
            SceneManager.sceneLoaded -= HandleSceneLoadedBeforeFirstUpdate;
            SceneManager.sceneLoaded += HandleSceneLoadedBeforeFirstUpdate;
            SceneManager.sceneUnloaded -= HandleOwnedSceneUnloaded;
            SceneManager.sceneUnloaded += HandleOwnedSceneUnloaded;
            _active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallBeforeFirstUpdate()
        {
            TrySecureArmedSceneBeforeFirstUpdate(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoadedBeforeFirstUpdate(
            Scene scene,
            LoadSceneMode mode)
        {
            TrySecureArmedSceneBeforeFirstUpdate(scene);
        }

        private static void TrySecureArmedSceneBeforeFirstUpdate(Scene scene)
        {
            if (!EditorGameTestModeBootstrap.IsArmed ||
                !SessionState.GetBool(EditorGameTestModeBootstrap.SessionActiveKey, false))
            {
                return;
            }

            if (!TrySecureScene(scene, out string message))
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    string.IsNullOrEmpty(message)
                        ? "The isolated production-tick boundary was unavailable"
                        : message);
            }
        }

        internal static bool TrySecureScene(Scene scene, out string message)
        {
            message = string.Empty;
            if (!scene.IsValid() || !scene.isLoaded ||
                !EditorGameTestModeBootstrap.TryVerifyActiveRuntime(out _, out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The exact isolated runtime was unavailable before production ticking.";
                }

                return false;
            }

            string sessionId = EditorGameTestModeBootstrap.ActiveSessionId;
            string isolatedRoot;
            try
            {
                isolatedRoot = Path.GetFullPath(EditorGameTestModeBootstrap.ActiveSaveRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                message = "The isolated runtime root was invalid before production ticking.";
                return false;
            }

            if (!FirstUserCoreGameplayPlanner.IsCanonicalSessionId(sessionId) ||
                string.IsNullOrEmpty(isolatedRoot) ||
                !ServiceLocator.TryGet<ISaveGameService>(out ISaveGameService saveService) ||
                saveService == null ||
                saveService.LastLoadStatus != SaveLoadStatus.CreatedNew ||
                saveService.CurrentSave == null ||
                saveService.CurrentSave.SelectedRealm != RealmId.None ||
                !(saveService is IProfileWriteAuthorityProvider authorityProvider) ||
                ProfileWriteAuthorityProviderGuard.IsCurrentWritable(authorityProvider))
            {
                message =
                    "The isolated profile did not retain its exact fresh, non-writable authority boundary.";
                return false;
            }

            Bootloader[] bootloaders = FindBootloaders(scene);
            if (bootloaders.Length != 1 || bootloaders[0] == null)
            {
                message = "The isolated scene did not expose exactly one Bootloader tick owner.";
                return false;
            }

            Bootloader bootloader = bootloaders[0];
            if (_active != null && _active.Matches(
                    sessionId,
                    isolatedRoot,
                    saveService,
                    bootloader,
                    scene))
            {
                return _active.TryAdvanceOwnedPolicy(out _, out message);
            }

            if (_active != null &&
                !TryForgetDestroyedSceneOwner(out message))
            {
                return false;
            }

            if (!TryReadBootloaderOwnershipState(
                    bootloader,
                    out bool runtimeActive,
                    out bool awaitingOwnership,
                    out message))
            {
                return false;
            }

            if (awaitingOwnership)
            {
                if (runtimeActive || !bootloader.enabled)
                {
                    message = "The isolated Bootloader ownership handoff state was inconsistent.";
                    return false;
                }
            }
            else
            {
                if (!runtimeActive || !bootloader.enabled)
                {
                    message = "The isolated Bootloader was not an active tick owner.";
                    return false;
                }

                bootloader.enabled = false;
                if (bootloader.enabled)
                {
                    message =
                        "The isolated scene Bootloader production tick could not be suspended.";
                    return false;
                }
            }

            _active = new FirstUserIsolatedRuntimePolicy(
                sessionId,
                isolatedRoot,
                saveService,
                bootloader,
                scene,
                awaitingOwnership);
            return _active.TryAdvanceOwnedPolicy(out _, out message);
        }

        internal static bool TryVerifyActive(out string message)
        {
            if (_active == null)
            {
                message = "The isolated production-tick policy is not installed.";
                return false;
            }

            if (!_active.TryAdvanceOwnedPolicy(out bool ready, out message))
            {
                return false;
            }

            if (!ready)
            {
                message = "The isolated Bootloader ownership handoff is still pending.";
                return false;
            }

            return true;
        }

        internal static bool TryAdvanceAndVerify(out bool ready, out string message)
        {
            if (_active == null)
            {
                ready = false;
                message = "The isolated production-tick policy is not installed.";
                return false;
            }

            return _active.TryAdvanceOwnedPolicy(out ready, out message);
        }

        /// <summary>
        /// Memory-only verification for the host-driver tick. Full filesystem, marker, and
        /// private ownership-state validation remains mandatory at install, resume, replay,
        /// and every other authority boundary through TryAdvanceAndVerify.
        /// </summary>
        internal static bool TryAdvanceTickBoundary(out bool ready, out string message)
        {
            if (_active == null)
            {
                ready = false;
                message = "The isolated production-tick policy is not installed.";
                return false;
            }

            return _active.TryAdvanceMemoryOnlyTickBoundary(out ready, out message);
        }

        internal static bool TryForgetDestroyedSceneOwner(out string message)
        {
            message = string.Empty;
            if (_active == null)
            {
                return true;
            }

            if (_active._bootloader != null)
            {
                Scene ownerScene = _active._bootloader.gameObject.scene;
                if (ownerScene.IsValid() && ownerScene.isLoaded)
                {
                    message =
                        "The isolated Bootloader owner still exists; its disabled policy cannot be forgotten.";
                    return false;
                }
            }

            _active = null;
            return true;
        }

        private static void HandleOwnedSceneUnloaded(Scene scene)
        {
            if (_active != null && scene.IsValid() &&
                scene.handle == _active._sceneHandle)
            {
                _active = null;
            }
        }

        private bool TryAdvanceOwnedPolicy(out bool ready, out string message)
        {
            ready = false;
            message = string.Empty;
            if (!EditorGameTestModeBootstrap.TryVerifyActiveRuntime(out _, out message) ||
                !string.Equals(
                    EditorGameTestModeBootstrap.ActiveSessionId,
                    _sessionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string activeRoot;
            try
            {
                activeRoot = Path.GetFullPath(EditorGameTestModeBootstrap.ActiveSaveRoot);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                message = "The isolated runtime root became invalid.";
                return false;
            }

            if (!PathsEqual(activeRoot, _isolatedRoot))
            {
                message = "The isolated profile root drifted.";
                return false;
            }

            if (!TryVerifyNonWritableProfileBoundary(out message))
            {
                return false;
            }

            if (_bootloader == null ||
                _bootloader.GetInstanceID() != _bootloaderInstanceId ||
                _bootloader.gameObject.scene.handle != _sceneHandle ||
                !TryReadBootloaderOwnershipState(
                    _bootloader,
                    out bool runtimeActive,
                    out bool awaitingOwnership,
                    out message))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = "The exact isolated Bootloader production-tick policy drifted.";
                }

                return false;
            }

            if (_awaitingOwnershipHandoff)
            {
                if (awaitingOwnership)
                {
                    if (runtimeActive || !_bootloader.enabled)
                    {
                        message =
                            "The isolated Bootloader ownership handoff became inconsistent.";
                        return false;
                    }

                    return true;
                }

                if (!runtimeActive || !_bootloader.enabled)
                {
                    message = "The isolated Bootloader failed to claim runtime ownership safely.";
                    return false;
                }

                // The standby owner's only preceding Update attempts ownership and returns before
                // production ticking. The earlier host-driver execution order now disables it before
                // its next Update can reach TickProduction.
                _bootloader.enabled = false;
                _awaitingOwnershipHandoff = false;
            }

            if (awaitingOwnership || !runtimeActive || _bootloader.enabled)
            {
                message = "The exact isolated Bootloader production-tick policy drifted.";
                return false;
            }

            ready = true;
            return true;
        }

        private bool TryAdvanceMemoryOnlyTickBoundary(
            out bool ready,
            out string message)
        {
            if (_awaitingOwnershipHandoff)
            {
                // The one-time handoff requires exact private ownership state. Once secured,
                // every later frame uses only the bounded memory checks below.
                return TryAdvanceOwnedPolicy(out ready, out message);
            }

            ready = false;
            message = string.Empty;
            if (!EditorGameTestModeBootstrap.IsArmed ||
                !SessionState.GetBool(
                    EditorGameTestModeBootstrap.SessionActiveKey,
                    false) ||
                !string.Equals(
                    EditorGameTestModeBootstrap.ActiveSessionId,
                    _sessionId,
                    StringComparison.Ordinal) ||
                !PathsEqual(
                    EditorGameTestModeBootstrap.ActiveSaveRoot,
                    _isolatedRoot))
            {
                message = "The isolated session or root tick boundary drifted.";
                return false;
            }

            if (!TryVerifyNonWritableProfileBoundary(out message))
            {
                return false;
            }

            if (_bootloader == null ||
                _bootloader.GetInstanceID() != _bootloaderInstanceId ||
                _bootloader.enabled ||
                !_bootloader.gameObject.scene.IsValid() ||
                !_bootloader.gameObject.scene.isLoaded ||
                _bootloader.gameObject.scene.handle != _sceneHandle)
            {
                message = "The exact isolated Bootloader tick owner drifted.";
                return false;
            }

            ready = true;
            return true;
        }

        private bool TryVerifyNonWritableProfileBoundary(out string message)
        {
            message = string.Empty;
            if (!ServiceLocator.TryGet<ISaveGameService>(
                    out ISaveGameService currentSave) ||
                currentSave == null)
            {
                message = "The isolated save service was unavailable.";
                return false;
            }

            if (!ReferenceEquals(currentSave, _saveService))
            {
                message = "The exact isolated save-service instance drifted.";
                return false;
            }

            if (currentSave.LastLoadStatus != SaveLoadStatus.CreatedNew)
            {
                message =
                    "The isolated save load status drifted to " +
                    currentSave.LastLoadStatus + ".";
                return false;
            }

            if (currentSave.CurrentSave == null)
            {
                message = "The isolated current-save snapshot became null.";
                return false;
            }

            if (currentSave.CurrentSave.SelectedRealm != RealmId.None)
            {
                message =
                    "The isolated development realm boundary drifted to " +
                    currentSave.CurrentSave.SelectedRealm + ".";
                return false;
            }

            if (!(currentSave is IProfileWriteAuthorityProvider authorityProvider))
            {
                message = "The isolated save lost its write-authority provider boundary.";
                return false;
            }

            if (ProfileWriteAuthorityProviderGuard.IsCurrentWritable(authorityProvider))
            {
                message = "The isolated save unexpectedly became production-writable.";
                return false;
            }

            return true;
        }

        private bool Matches(
            string sessionId,
            string isolatedRoot,
            ISaveGameService saveService,
            Bootloader bootloader,
            Scene scene)
        {
            return string.Equals(_sessionId, sessionId, StringComparison.Ordinal) &&
                   PathsEqual(_isolatedRoot, isolatedRoot) &&
                   ReferenceEquals(_saveService, saveService) &&
                   ReferenceEquals(_bootloader, bootloader) &&
                   _sceneHandle == scene.handle;
        }

        private static Bootloader[] FindBootloaders(Scene scene)
        {
            var found = new List<Bootloader>(capacity: 2);
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                Bootloader[] candidates = roots[index].GetComponentsInChildren<Bootloader>(true);
                for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
                {
                    found.Add(candidates[candidateIndex]);
                }
            }

            return found.ToArray();
        }

        private static bool TryReadBootloaderOwnershipState(
            Bootloader bootloader,
            out bool runtimeActive,
            out bool awaitingOwnership,
            out string message)
        {
            runtimeActive = false;
            awaitingOwnership = false;
            message = string.Empty;
            if (bootloader == null || RuntimeActiveField == null ||
                StandbyForOwnershipField == null ||
                RuntimeActiveField.FieldType != typeof(bool) ||
                StandbyForOwnershipField.FieldType != typeof(bool))
            {
                message = "The exact Bootloader ownership state contract was unavailable.";
                return false;
            }

            try
            {
                runtimeActive = (bool)RuntimeActiveField.GetValue(bootloader);
                awaitingOwnership = (bool)StandbyForOwnershipField.GetValue(bootloader);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is FieldAccessException ||
                exception is TargetException)
            {
                message = "The exact Bootloader ownership state could not be inspected.";
                return false;
            }
        }

        private static bool PathsEqual(string first, string second)
        {
            StringComparison comparison =
                Application.platform == RuntimePlatform.WindowsEditor
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            return string.Equals(first, second, comparison);
        }
    }
}
