#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.Scenes;
using AL.Services.Local;
using UnityEditor;
using UnityEngine;

namespace AL.Development
{
    public enum EditorGameTestModeFailure
    {
        None,
        InvalidSessionId,
        FullDomainReloadRequired,
        FullSceneReloadRequired,
        InvalidTemporaryRoot,
        InvalidPersistentDataRoot,
        EnvironmentBindingMismatch,
        InvalidBootScene,
        IsolatedRootMismatch,
        PersistentDataOverlap,
        IsolatedRootAlreadyExists,
        IsolatedRootCreationFailed,
        IsolatedRootMissing,
        OwnershipMarkerMissing,
        OwnershipMarkerMismatch,
        ReparsePointRejected,
        IsolatedRootNotFresh,
        ForeignSaveFactoryPresent,
        DifferentSessionAlreadyArmed,
        PreExistingSaveState,
        NotArmed,
        FactoryNotOwned,
        SaveServiceNotCreated,
        RegisteredSaveServiceMismatch,
        OfflineStackMarkerMissing,
        OfflineStackMarkerMismatch,
        OfflineStackLoadIncomplete,
        FreshProfileLoadInvalid,
        CurrentSaveMissing,
        DurableRecoveryRecordMissing,
        DurableRecoveryRecordInvalid,
        CleanupWhileArmed,
        CleanupInventoryTooLarge,
        CleanupInventoryChanged,
        CleanupFailed
    }

    public enum EditorGameTestModeRecoveryStage
    {
        Starting,
        Running,
        Recovery
    }

    public readonly struct EditorGameTestModeRecoveryRecord
    {
        public EditorGameTestModeRecoveryRecord(
            EditorGameTestModePlan plan,
            EditorGameTestModeRecoveryStage stage,
            string previousStartSceneGuid,
            bool previousStartSceneWasNull)
        {
            Plan = plan;
            Stage = stage;
            PreviousStartSceneGuid = previousStartSceneGuid ?? string.Empty;
            PreviousStartSceneWasNull = previousStartSceneWasNull;
        }

        public EditorGameTestModePlan Plan { get; }
        public EditorGameTestModeRecoveryStage Stage { get; }
        public string PreviousStartSceneGuid { get; }
        public bool PreviousStartSceneWasNull { get; }
    }

    public readonly struct EditorGameTestModePlan
    {
        public EditorGameTestModePlan(
            string sessionId,
            string systemTemporaryRoot,
            string persistentDataRoot,
            string isolatedSaveRoot,
            string bootScenePath,
            string bootSceneGuid)
        {
            SessionId = sessionId ?? string.Empty;
            SystemTemporaryRoot = systemTemporaryRoot ?? string.Empty;
            PersistentDataRoot = persistentDataRoot ?? string.Empty;
            IsolatedSaveRoot = isolatedSaveRoot ?? string.Empty;
            BootScenePath = bootScenePath ?? string.Empty;
            BootSceneGuid = bootSceneGuid ?? string.Empty;
        }

        public string SessionId { get; }
        public string SystemTemporaryRoot { get; }
        public string PersistentDataRoot { get; }
        public string IsolatedSaveRoot { get; }
        public string BootScenePath { get; }
        public string BootSceneGuid { get; }
    }

    /// <summary>
    /// Editor-only bridge compiled into AL.Runtime so it can use the existing internal save factory
    /// seam without widening runtime authority or editing Bootloader. Player builds contain no type
    /// from this file because the entire implementation is guarded by UNITY_EDITOR.
    /// </summary>
    public static class EditorGameTestModeBootstrap
    {
        public const string ContractVersion = "al.editor.game-test-mode.v1";
        public const string RecoveryContractVersion = "al.editor.game-test-mode.recovery.v1";
        public const string MarkerFileName = ".anotherlife-isolated-game-test";
        public const string TemporaryProductFolder = "AnotherLife";
        public const string TemporaryModeFolder = "GameTestMode";
        public const string ExpectedBootScenePath = "Assets/AL/Scenes/Boot.unity";
        public const string ExpectedBootSceneGuid = "14733c561820e497a96a3f2467d247cc";
        public const string SessionActiveKey = "AL.GameTestMode.Active";
        public const string SessionIdKey = "AL.GameTestMode.SessionId";
        public const string SessionTemporaryRootKey = "AL.GameTestMode.TemporaryRoot";
        public const string SessionPersistentRootKey = "AL.GameTestMode.PersistentRoot";
        public const string SessionIsolatedRootKey = "AL.GameTestMode.IsolatedRoot";
        public const string SessionBootScenePathKey = "AL.GameTestMode.BootScenePath";
        public const string SessionBootSceneGuidKey = "AL.GameTestMode.BootSceneGuid";
        public const string SessionFullDomainReloadKey = "AL.GameTestMode.FullDomainReload";
        public const string SessionFullSceneReloadKey = "AL.GameTestMode.FullSceneReload";

        private const string RecoveryHeader = "ANOTHER_LIFE_ISOLATED_GAME_TEST_RECOVERY";
        private const string RecoveryPreferencePrefix = "AL.GameTestMode.DurableRecovery.";

        private const int MaximumCleanupEntries = 256;
        private static readonly object Sync = new object();
        private static readonly StringComparison PathComparison =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        private static readonly string ProjectRecoveryPreferenceKey =
            RecoveryPreferencePrefix + ComputeProjectIdentityToken();

        private static EditorGameTestModePlan _activePlan;
        private static bool _armed;
        private static Func<object> _ownedSaveFactory;
        private static Func<object> _displacedSaveFactory;
        private static ISaveGameService _createdSaveService;
        private static GameObject _bannerObject;
        private static string _lastFailure = string.Empty;
        private static string _recoveryPreferenceKeyOverrideForTests = string.Empty;

        public static bool IsArmed
        {
            get
            {
                lock (Sync)
                {
                    return _armed;
                }
            }
        }

        public static string ActiveSessionId
        {
            get
            {
                lock (Sync)
                {
                    return _armed ? _activePlan.SessionId : string.Empty;
                }
            }
        }

        public static string ActiveSaveRoot
        {
            get
            {
                lock (Sync)
                {
                    return _armed ? _activePlan.IsolatedSaveRoot : string.Empty;
                }
            }
        }

        public static string LastFailure
        {
            get
            {
                lock (Sync)
                {
                    return _lastFailure;
                }
            }
        }

        public static string DurableRecoveryPreferenceKey =>
            string.IsNullOrEmpty(_recoveryPreferenceKeyOverrideForTests)
                ? ProjectRecoveryPreferenceKey
                : _recoveryPreferenceKeyOverrideForTests;

        public static bool HasDurableRecoveryRecord =>
            EditorPrefs.HasKey(DurableRecoveryPreferenceKey);

        internal static void SetDurableRecoveryPreferenceKeyOverrideForTests(string key)
        {
            const string requiredPrefix = "AL.GameTestMode.Tests.";
            if (string.IsNullOrWhiteSpace(key) ||
                !key.StartsWith(requiredPrefix, StringComparison.Ordinal) ||
                key.Length > 128)
            {
                throw new ArgumentException(
                    "The test recovery preference key must use the bounded AL.GameTestMode.Tests namespace.",
                    nameof(key));
            }

            _recoveryPreferenceKeyOverrideForTests = key;
        }

        internal static void ClearDurableRecoveryPreferenceKeyOverrideForTests()
        {
            _recoveryPreferenceKeyOverrideForTests = string.Empty;
        }

        public static string BuildMarkerContents(string sessionId)
        {
            return "ANOTHER_LIFE_ISOLATED_GAME_TEST\n" +
                ContractVersion + "\n" +
                (sessionId ?? string.Empty) + "\n";
        }

        public static bool TryWriteDurableRecoveryRecord(
            EditorGameTestModeRecoveryRecord record,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            EditorGameTestModePlan source = record.Plan;
            if (!TryCreatePlan(
                    source.SessionId,
                    source.SystemTemporaryRoot,
                    source.PersistentDataRoot,
                    source.IsolatedSaveRoot,
                    source.BootScenePath,
                    source.BootSceneGuid,
                    fullDomainReload: true,
                    fullSceneReload: true,
                    out EditorGameTestModePlan rebound,
                    out failure,
                    out message))
            {
                return false;
            }

            if (!string.Equals(rebound.SessionId, source.SessionId, StringComparison.Ordinal) ||
                !string.Equals(rebound.IsolatedSaveRoot, source.IsolatedSaveRoot, PathComparison))
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The recovery record is not bound to the current project environment.",
                    out failure,
                    out message);
            }

            if (record.PreviousStartSceneWasNull)
            {
                if (!string.IsNullOrEmpty(record.PreviousStartSceneGuid))
                {
                    return Fail(
                        EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                        "A null prior start scene cannot carry an asset GUID.",
                        out failure,
                        out message);
                }
            }
            else if (!IsCanonicalAssetGuid(record.PreviousStartSceneGuid))
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The prior Play Mode start-scene GUID is invalid.",
                    out failure,
                    out message);
            }

            if (EditorPrefs.HasKey(DurableRecoveryPreferenceKey))
            {
                if (!TryReadDurableRecoveryRecord(
                        out EditorGameTestModeRecoveryRecord existing,
                        out _,
                        out string existingMessage))
                {
                    return Fail(
                        EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                        "An invalid durable recovery record already exists and was not overwritten: " +
                        existingMessage,
                        out failure,
                        out message);
                }

                if (!string.Equals(
                        existing.Plan.SessionId,
                        record.Plan.SessionId,
                        StringComparison.Ordinal))
                {
                    return Fail(
                        EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                        "A different isolated session already owns the durable recovery record.",
                        out failure,
                        out message);
                }
            }

            try
            {
                EditorPrefs.SetString(
                    DurableRecoveryPreferenceKey,
                    BuildDurableRecoveryContents(record));
                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The project-scoped recovery record could not be written: " + ex.Message,
                    out failure,
                    out message);
            }
        }

        public static bool TryReadDurableRecoveryRecord(
            out EditorGameTestModeRecoveryRecord record,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            record = default;
            string preferenceKey = DurableRecoveryPreferenceKey;
            if (!EditorPrefs.HasKey(preferenceKey))
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordMissing,
                    "No project-scoped isolated test recovery record exists.",
                    out failure,
                    out message);
            }

            string encoded;
            try
            {
                encoded = EditorPrefs.GetString(preferenceKey, string.Empty);
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The project-scoped recovery record could not be read: " + ex.Message,
                    out failure,
                    out message);
            }

            if (string.IsNullOrEmpty(encoded) || encoded.Length > 512 || encoded.IndexOf('\r') >= 0)
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The project-scoped recovery record has an invalid byte shape.",
                    out failure,
                    out message);
            }

            string[] lines = encoded.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length != 8 ||
                !string.Equals(lines[0], RecoveryHeader, StringComparison.Ordinal) ||
                !string.Equals(lines[1], RecoveryContractVersion, StringComparison.Ordinal) ||
                !string.IsNullOrEmpty(lines[7]) ||
                !IsCanonicalSessionId(lines[2]))
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The project-scoped recovery record is malformed or unsupported.",
                    out failure,
                    out message);
            }

            EditorGameTestModeRecoveryStage stage;
            switch (lines[3])
            {
                case "starting":
                    stage = EditorGameTestModeRecoveryStage.Starting;
                    break;
                case "running":
                    stage = EditorGameTestModeRecoveryStage.Running;
                    break;
                case "recovery":
                    stage = EditorGameTestModeRecoveryStage.Recovery;
                    break;
                default:
                    return Fail(
                        EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                        "The project-scoped recovery record has an invalid lifecycle stage.",
                        out failure,
                        out message);
            }

            bool previousWasNull;
            if (string.Equals(lines[4], "null", StringComparison.Ordinal))
            {
                previousWasNull = true;
                if (!string.IsNullOrEmpty(lines[5]))
                {
                    return Fail(
                        EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                        "The project-scoped recovery record has contradictory start-scene data.",
                        out failure,
                        out message);
                }
            }
            else if (string.Equals(lines[4], "guid", StringComparison.Ordinal) &&
                     IsCanonicalAssetGuid(lines[5]))
            {
                previousWasNull = false;
            }
            else
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The project-scoped recovery record has invalid start-scene data.",
                    out failure,
                    out message);
            }

            string temporaryRoot = lines[6];
            if (string.IsNullOrWhiteSpace(temporaryRoot))
            {
                return Fail(
                    EditorGameTestModeFailure.DurableRecoveryRecordInvalid,
                    "The project-scoped recovery record does not include a temporary root.",
                    out failure,
                    out message);
            }

            string isolatedRoot = BuildExpectedIsolatedRoot(temporaryRoot, lines[2]);
            if (!TryCreatePlan(
                    lines[2],
                    temporaryRoot,
                    Application.persistentDataPath,
                    isolatedRoot,
                    ExpectedBootScenePath,
                    ExpectedBootSceneGuid,
                    fullDomainReload: true,
                    fullSceneReload: true,
                    out EditorGameTestModePlan plan,
                    out failure,
                    out message))
            {
                return false;
            }

            record = new EditorGameTestModeRecoveryRecord(
                plan,
                stage,
                lines[5],
                previousWasNull);
            failure = EditorGameTestModeFailure.None;
            message = string.Empty;
            return true;
        }

        public static bool TryUpdateDurableRecoveryStage(
            string expectedSessionId,
            EditorGameTestModeRecoveryStage stage,
            out string message)
        {
            if (!TryReadDurableRecoveryRecord(
                    out EditorGameTestModeRecoveryRecord record,
                    out _,
                    out message) ||
                !string.Equals(record.Plan.SessionId, expectedSessionId, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "The recovery record did not belong to the active isolated session.";
                }

                return false;
            }

            return TryWriteDurableRecoveryRecord(
                new EditorGameTestModeRecoveryRecord(
                    record.Plan,
                    stage,
                    record.PreviousStartSceneGuid,
                    record.PreviousStartSceneWasNull),
                out _,
                out message);
        }

        public static bool TryClearDurableRecoveryRecord(
            string expectedSessionId,
            out string message)
        {
            message = string.Empty;
            if (!EditorPrefs.HasKey(DurableRecoveryPreferenceKey))
            {
                return true;
            }

            if (!TryReadDurableRecoveryRecord(
                    out EditorGameTestModeRecoveryRecord record,
                    out _,
                    out message) ||
                !string.Equals(record.Plan.SessionId, expectedSessionId, StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "The durable record was retained because its session did not match.";
                }

                return false;
            }

            EditorPrefs.DeleteKey(DurableRecoveryPreferenceKey);
            return true;
        }

        public static void ForgetInvalidDurableRecoveryRecordWithoutDeletingFiles()
        {
            EditorPrefs.DeleteKey(DurableRecoveryPreferenceKey);
        }

        public static string BuildExpectedIsolatedRoot(string systemTemporaryRoot, string sessionId)
        {
            if (!TryNormalizeDirectory(systemTemporaryRoot, out string normalizedTemporaryRoot) ||
                !IsCanonicalSessionId(sessionId))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(
                    normalizedTemporaryRoot,
                    TemporaryProductFolder,
                    TemporaryModeFolder,
                    sessionId));
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool TryCreatePlan(
            string sessionId,
            string systemTemporaryRoot,
            string persistentDataRoot,
            string isolatedSaveRoot,
            string bootScenePath,
            string bootSceneGuid,
            bool fullDomainReload,
            bool fullSceneReload,
            out EditorGameTestModePlan plan,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            plan = default;
            failure = EditorGameTestModeFailure.None;
            message = string.Empty;

            if (!IsCanonicalSessionId(sessionId))
            {
                return Fail(
                    EditorGameTestModeFailure.InvalidSessionId,
                    "The isolated test session ID must be exactly 32 lowercase hexadecimal characters.",
                    out failure,
                    out message);
            }

            if (!fullDomainReload)
            {
                return Fail(
                    EditorGameTestModeFailure.FullDomainReloadRequired,
                    "Disable Domain Reload is not supported by isolated game test mode.",
                    out failure,
                    out message);
            }

            if (!fullSceneReload)
            {
                return Fail(
                    EditorGameTestModeFailure.FullSceneReloadRequired,
                    "Disable Scene Reload is not supported by isolated game test mode.",
                    out failure,
                    out message);
            }

            if (!TryNormalizeDirectory(systemTemporaryRoot, out string normalizedTemporaryRoot))
            {
                return Fail(
                    EditorGameTestModeFailure.InvalidTemporaryRoot,
                    "The system temporary root could not be resolved.",
                    out failure,
                    out message);
            }

            string liveTemporaryPath;
            try
            {
                liveTemporaryPath = Path.GetTempPath();
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.EnvironmentBindingMismatch,
                    "The current process temporary root could not be read: " + ex.Message,
                    out failure,
                    out message);
            }

            if (!TryNormalizeDirectory(liveTemporaryPath, out string liveTemporaryRoot) ||
                !string.Equals(normalizedTemporaryRoot, liveTemporaryRoot, PathComparison))
            {
                return Fail(
                    EditorGameTestModeFailure.EnvironmentBindingMismatch,
                    "The supplied temporary root does not match the current process temporary root.",
                    out failure,
                    out message);
            }

            if (!TryNormalizeDirectory(persistentDataRoot, out string normalizedPersistentRoot))
            {
                return Fail(
                    EditorGameTestModeFailure.InvalidPersistentDataRoot,
                    "The developer profile root could not be resolved for isolation comparison.",
                    out failure,
                    out message);
            }


            string livePersistentPath;
            try
            {
                livePersistentPath = Application.persistentDataPath;
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.EnvironmentBindingMismatch,
                    "The current Unity persistent-data root could not be read: " + ex.Message,
                    out failure,
                    out message);
            }

            if (!TryNormalizeDirectory(livePersistentPath, out string livePersistentRoot) ||
                !string.Equals(normalizedPersistentRoot, livePersistentRoot, PathComparison))
            {
                return Fail(
                    EditorGameTestModeFailure.EnvironmentBindingMismatch,
                    "The supplied persistent-data root does not match the current Unity process.",
                    out failure,
                    out message);
            }

            if (!string.Equals(bootScenePath, ExpectedBootScenePath, StringComparison.Ordinal) ||
                !string.Equals(bootSceneGuid, ExpectedBootSceneGuid, StringComparison.Ordinal))
            {
                return Fail(
                    EditorGameTestModeFailure.InvalidBootScene,
                    "The exact production Boot scene path and GUID are required.",
                    out failure,
                    out message);
            }

            string expectedRoot = BuildExpectedIsolatedRoot(normalizedTemporaryRoot, sessionId);
            if (!TryNormalizeDirectory(isolatedSaveRoot, out string normalizedIsolatedRoot) ||
                !string.Equals(expectedRoot, normalizedIsolatedRoot, PathComparison))
            {
                return Fail(
                    EditorGameTestModeFailure.IsolatedRootMismatch,
                    "The isolated save root is not the exact GUID-owned system-temp path.",
                    out failure,
                    out message);
            }

            if (PathsOverlap(normalizedIsolatedRoot, normalizedPersistentRoot))
            {
                return Fail(
                    EditorGameTestModeFailure.PersistentDataOverlap,
                    "The isolated save root overlaps the developer profile root.",
                    out failure,
                    out message);
            }


            if (!TryValidateExistingPathChain(
                    normalizedTemporaryRoot,
                    normalizedIsolatedRoot,
                    requireLeaf: false,
                    out failure,
                    out message))
            {
                return false;
            }

            plan = new EditorGameTestModePlan(
                sessionId,
                normalizedTemporaryRoot,
                normalizedPersistentRoot,
                normalizedIsolatedRoot,
                bootScenePath,
                bootSceneGuid);
            return true;
        }

        public static bool TryCreateOwnedRoot(
            EditorGameTestModePlan plan,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            if (!TryCreatePlan(
                    plan.SessionId,
                    plan.SystemTemporaryRoot,
                    plan.PersistentDataRoot,
                    plan.IsolatedSaveRoot,
                    plan.BootScenePath,
                    plan.BootSceneGuid,
                    fullDomainReload: true,
                    fullSceneReload: true,
                    out _,
                    out failure,
                    out message))
            {
                return false;
            }

            if (Directory.Exists(plan.IsolatedSaveRoot) || File.Exists(plan.IsolatedSaveRoot))
            {
                return Fail(
                    EditorGameTestModeFailure.IsolatedRootAlreadyExists,
                    "The newly allocated isolated root already exists; no files were changed.",
                    out failure,
                    out message);
            }

            try
            {
                string productRoot = Path.Combine(
                    plan.SystemTemporaryRoot,
                    TemporaryProductFolder);
                string modeRoot = Path.Combine(productRoot, TemporaryModeFolder);
                if (!TryCreateOrValidateDirectory(productRoot, out failure, out message) ||
                    !TryCreateOrValidateDirectory(modeRoot, out failure, out message) ||
                    !TryCreateOrValidateDirectory(plan.IsolatedSaveRoot, out failure, out message))
                {
                    return false;
                }

                byte[] markerBytes = Encoding.UTF8.GetBytes(BuildMarkerContents(plan.SessionId));
                string markerPath = Path.Combine(plan.IsolatedSaveRoot, MarkerFileName);
                using (var stream = new FileStream(
                           markerPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 4096,
                           options: FileOptions.WriteThrough))
                {
                    stream.Write(markerBytes, 0, markerBytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                return TryValidateOwnedRoot(
                    plan,
                    requireFreshRoot: true,
                    out failure,
                    out message);
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.IsolatedRootCreationFailed,
                    "The isolated root could not be created safely: " + ex.Message,
                    out failure,
                    out message);
            }
        }

        public static bool TryValidateOwnedRoot(
            EditorGameTestModePlan plan,
            bool requireFreshRoot,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            failure = EditorGameTestModeFailure.None;
            message = string.Empty;

            if (!TryCreatePlan(
                    plan.SessionId,
                    plan.SystemTemporaryRoot,
                    plan.PersistentDataRoot,
                    plan.IsolatedSaveRoot,
                    plan.BootScenePath,
                    plan.BootSceneGuid,
                    fullDomainReload: true,
                    fullSceneReload: true,
                    out _,
                    out failure,
                    out message))
            {
                return false;
            }


            if (!TryValidateExistingPathChain(
                    plan.SystemTemporaryRoot,
                    plan.IsolatedSaveRoot,
                    requireLeaf: true,
                    out failure,
                    out message))
            {
                return false;
            }

            if (!Directory.Exists(plan.IsolatedSaveRoot))
            {
                return Fail(
                    EditorGameTestModeFailure.IsolatedRootMissing,
                    "The isolated save root does not exist.",
                    out failure,
                    out message);
            }

            try
            {
                var rootInfo = new DirectoryInfo(plan.IsolatedSaveRoot);
                if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return Fail(
                        EditorGameTestModeFailure.ReparsePointRejected,
                        "The isolated save root is a reparse point.",
                        out failure,
                        out message);
                }
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.IsolatedRootMissing,
                    "The isolated save root could not be inspected: " + ex.Message,
                    out failure,
                    out message);
            }

            string markerPath = Path.Combine(plan.IsolatedSaveRoot, MarkerFileName);
            if (!File.Exists(markerPath))
            {
                return Fail(
                    EditorGameTestModeFailure.OwnershipMarkerMissing,
                    "The isolated save ownership marker is missing.",
                    out failure,
                    out message);
            }

            byte[] markerBytes;
            byte[] expectedMarkerBytes = Encoding.UTF8.GetBytes(BuildMarkerContents(plan.SessionId));
            try
            {
                var markerInfo = new FileInfo(markerPath);
                if ((markerInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return Fail(
                        EditorGameTestModeFailure.ReparsePointRejected,
                        "The isolated save ownership marker is a reparse point.",
                        out failure,
                        out message);
                }

                if (markerInfo.Length != expectedMarkerBytes.Length)
                {
                    return Fail(
                        EditorGameTestModeFailure.OwnershipMarkerMismatch,
                        "The isolated save ownership marker has an unexpected byte length.",
                        out failure,
                        out message);
                }

                markerBytes = File.ReadAllBytes(markerPath);
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.OwnershipMarkerMismatch,
                    "The isolated save ownership marker could not be read: " + ex.Message,
                    out failure,
                    out message);
            }

            if (!BytesEqual(markerBytes, expectedMarkerBytes))
            {
                return Fail(
                    EditorGameTestModeFailure.OwnershipMarkerMismatch,
                    "The isolated save ownership marker does not match this session.",
                    out failure,
                    out message);
            }

            if (requireFreshRoot)
            {
                int entryCount = 0;
                bool markerOnly = true;
                try
                {
                    foreach (string entry in Directory.EnumerateFileSystemEntries(plan.IsolatedSaveRoot))
                    {
                        entryCount++;
                        if (entryCount > 1 ||
                            !string.Equals(Path.GetFullPath(entry), Path.GetFullPath(markerPath), PathComparison))
                        {
                            markerOnly = false;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return Fail(
                        EditorGameTestModeFailure.IsolatedRootNotFresh,
                        "The isolated root inventory could not be verified: " + ex.Message,
                        out failure,
                        out message);
                }

                if (entryCount != 1 || !markerOnly)
                {
                    return Fail(
                        EditorGameTestModeFailure.IsolatedRootNotFresh,
                        "Fresh first-user mode requires an isolated root containing only its ownership marker.",
                        out failure,
                        out message);
                }
            }

            return true;
        }

        public static bool TryArm(
            EditorGameTestModePlan plan,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            if (!TryValidateOwnedRoot(plan, requireFreshRoot: true, out failure, out message))
            {
                RememberFailure(message);
                return false;
            }

            lock (Sync)
            {
                if (_armed)
                {
                    if (string.Equals(_activePlan.SessionId, plan.SessionId, StringComparison.Ordinal) &&
                        string.Equals(_activePlan.IsolatedSaveRoot, plan.IsolatedSaveRoot, PathComparison) &&
                        ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedSaveFactory))
                    {
                        failure = EditorGameTestModeFailure.None;
                        message = string.Empty;
                        return true;
                    }

                    return FailAndRemember(
                        EditorGameTestModeFailure.DifferentSessionAlreadyArmed,
                        "A different isolated game-test session is already armed.",
                        out failure,
                        out message);
                }

                if (OfflineServiceStack.SaveGameFactoryOverride != null)
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.ForeignSaveFactoryPresent,
                        "Another save factory override is active; isolated mode refused to replace it.",
                        out failure,
                        out message);
                }

                _activePlan = plan;
                _createdSaveService = null;
                _displacedSaveFactory = null;
                _ownedSaveFactory = CreateGuardedSaveService;
                OfflineServiceStack.SaveGameFactoryOverride = _ownedSaveFactory;
                _lastFailure = string.Empty;
                _armed = true;
                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
        }

        public static bool TryVerifyActiveRuntime(
            out EditorGameTestModeFailure failure,
            out string message)
        {
            lock (Sync)
            {
                if (!_armed)
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.NotArmed,
                        "Isolated game-test mode is not armed.",
                        out failure,
                        out message);
                }

                if (!ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedSaveFactory))
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.FactoryNotOwned,
                        "The active save factory is not owned by this isolated session.",
                        out failure,
                        out message);
                }

                if (!TryValidateOwnedRoot(_activePlan, requireFreshRoot: false, out failure, out message))
                {
                    _lastFailure = message;
                    return false;
                }

                if (_createdSaveService == null)
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.SaveServiceNotCreated,
                        "Bootloader has not created the isolated save service.",
                        out failure,
                        out message);
                }

                if (!ServiceLocator.TryGet<ISaveGameService>(out var registeredSave) ||
                    !ReferenceEquals(registeredSave, _createdSaveService))
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.RegisteredSaveServiceMismatch,
                        "The registered save service is not the exact isolated instance.",
                        out failure,
                        out message);
                }

                if (!ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker))
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.OfflineStackMarkerMissing,
                        "The offline service-stack marker is missing.",
                        out failure,
                        out message);
                }

                if (!marker.TryGetExpected<ISaveGameService>(out var expectedSave) ||
                    !ReferenceEquals(expectedSave, _createdSaveService))
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.OfflineStackMarkerMismatch,
                        "The offline stack marker does not bind the exact isolated save instance.",
                        out failure,
                        out message);
                }

                if (marker.LoadState != OfflineStackLoadState.Succeeded)
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.OfflineStackLoadIncomplete,
                        "Production Boot has not completed the isolated save load.",
                        out failure,
                        out message);
                }

                if (_createdSaveService.LastLoadStatus != SaveLoadStatus.CreatedNew)
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.FreshProfileLoadInvalid,
                        "The isolated fresh root did not produce an exact CreatedNew load.",
                        out failure,
                        out message);
                }

                var currentSave = _createdSaveService.CurrentSave;
                if (currentSave == null || currentSave.SelectedRealm != RealmId.None)
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.CurrentSaveMissing,
                        "The isolated load did not publish a fresh realm-unselected current save.",
                        out failure,
                        out message);
                }

                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
        }

        public static bool TryVerifyPreAwakeClean(
            out EditorGameTestModeFailure failure,
            out string message)
        {
            lock (Sync)
            {
                if (ServiceLocator.TryGet<ISaveGameService>(out _) ||
                    ServiceLocator.TryGet<IOfflineServiceStackMarker>(out _))
                {
                    return FailAndRemember(
                        EditorGameTestModeFailure.PreExistingSaveState,
                        "A save service or offline-stack marker was already registered before Boot Awake.",
                        out failure,
                        out message);
                }

                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
        }

        public static void Disarm()
        {
            lock (Sync)
            {
                if (ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedSaveFactory))
                {
                    OfflineServiceStack.SaveGameFactoryOverride = _displacedSaveFactory;
                }

                _armed = false;
                _activePlan = default;
                _ownedSaveFactory = null;
                _displacedSaveFactory = null;
                _createdSaveService = null;

                if (_bannerObject != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(_bannerObject);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(_bannerObject);
                    }

                    _bannerObject = null;
                }
            }
        }

        public static bool TryDeleteOwnedRoot(
            EditorGameTestModePlan plan,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            lock (Sync)
            {
                if (_armed)
                {
                    return Fail(
                        EditorGameTestModeFailure.CleanupWhileArmed,
                        "An armed isolated session must be disarmed before cleanup.",
                        out failure,
                        out message);
                }
            }

            if (!TryValidateOwnedRoot(plan, requireFreshRoot: false, out failure, out message))
            {
                return false;
            }

            try
            {
                if (!TryCollectCleanupInventory(
                        plan,
                        out List<string> firstFiles,
                        out List<string> firstDirectories,
                        out failure,
                        out message))
                {
                    return false;
                }

                if (!TryValidateOwnedRoot(plan, requireFreshRoot: false, out failure, out message) ||
                    !TryCollectCleanupInventory(
                        plan,
                        out List<string> secondFiles,
                        out List<string> secondDirectories,
                        out failure,
                        out message))
                {
                    return false;
                }

                if (!SameOrdinalPaths(firstFiles, secondFiles) ||
                    !SameOrdinalPaths(firstDirectories, secondDirectories))
                {
                    return Fail(
                        EditorGameTestModeFailure.CleanupInventoryChanged,
                        "Cleanup retained the isolated root because its inventory changed during validation.",
                        out failure,
                        out message);
                }

                lock (Sync)
                {
                    if (_armed)
                    {
                        return Fail(
                            EditorGameTestModeFailure.CleanupWhileArmed,
                            "The isolated session became armed during cleanup validation.",
                            out failure,
                            out message);
                    }
                }

                string markerPath = Path.GetFullPath(Path.Combine(plan.IsolatedSaveRoot, MarkerFileName));
                foreach (string filePath in secondFiles)
                {
                    if (string.Equals(filePath, markerPath, PathComparison))
                    {
                        continue;
                    }

                    if (!TryValidateDeletionEntry(plan.IsolatedSaveRoot, filePath, expectDirectory: false))
                    {
                        return Fail(
                            EditorGameTestModeFailure.CleanupInventoryChanged,
                            "Cleanup retained the isolated root because a file changed after validation.",
                            out failure,
                            out message);
                    }

                    File.Delete(filePath);
                }

                secondDirectories.Sort((left, right) => right.Length.CompareTo(left.Length));
                foreach (string directoryPath in secondDirectories)
                {
                    if (!TryValidateDeletionEntry(plan.IsolatedSaveRoot, directoryPath, expectDirectory: true))
                    {
                        return Fail(
                            EditorGameTestModeFailure.CleanupInventoryChanged,
                            "Cleanup retained the isolated root because a directory changed after validation.",
                            out failure,
                            out message);
                    }

                    Directory.Delete(directoryPath, recursive: false);
                }

                if (!TryValidateOwnedRoot(plan, requireFreshRoot: false, out failure, out message))
                {
                    return false;
                }

                File.Delete(markerPath);
                if (!TryValidateExistingPathChain(
                        plan.SystemTemporaryRoot,
                        plan.IsolatedSaveRoot,
                        requireLeaf: true,
                        out failure,
                        out message))
                {
                    TryRestoreOwnershipMarker(plan);
                    return false;
                }

                Directory.Delete(plan.IsolatedSaveRoot, recursive: false);
                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                TryRestoreOwnershipMarker(plan);
                return Fail(
                    EditorGameTestModeFailure.CleanupFailed,
                    "Cleanup retained the isolated root: " + ex.Message,
                    out failure,
                    out message);
            }
        }

        public static void EnterFailClosedState(string sessionId, string diagnostic)
        {
            lock (Sync)
            {
                _activePlan = new EditorGameTestModePlan(
                    IsCanonicalSessionId(sessionId) ? sessionId : string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    ExpectedBootScenePath,
                    ExpectedBootSceneGuid);
                _armed = true;
                InstallThrowingFactoryNoLock(
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "Isolation could not be proven before scene Awake."
                        : diagnostic);
                EnsureBannerNoLock(blocked: true);
            }
        }

        public static void FailClosedForLifecycleBoundary(string trigger)
        {
            string sessionId;
            string diagnostic;
            lock (Sync)
            {
                if (!_armed)
                {
                    return;
                }

                sessionId = _activePlan.SessionId;
                bool runtimeValid = TryVerifyActiveRuntime(out _, out string validationMessage);
                diagnostic = (string.IsNullOrWhiteSpace(trigger)
                    ? "An editor lifecycle boundary"
                    : trigger) + " stopped the isolated session before it could continue in the background.";
                if (!runtimeValid && !string.IsNullOrWhiteSpace(validationMessage))
                {
                    diagnostic += " Isolation validation: " + validationMessage;
                }

                InstallThrowingFactoryNoLock(diagnostic);
                BlockBootloadersBeforeAwakeNoLock();
                EnsureBannerNoLock(blocked: true);
            }

            if (IsCanonicalSessionId(sessionId))
            {
                TryUpdateDurableRecoveryStage(
                    sessionId,
                    EditorGameTestModeRecoveryStage.Recovery,
                    out _);
            }

            Debug.LogError("[AL-ISOLATED-TEST-BLOCKED] " + diagnostic);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void VerifyBeforeFirstSceneAwake()
        {
            try
            {
                VerifyBeforeFirstSceneAwakeCore();
            }
            catch (Exception ex)
            {
                string diagnostic =
                    "The pre-Awake isolation boundary threw unexpectedly and was blocked: " +
                    ex.GetType().Name + ": " + ex.Message;
                lock (Sync)
                {
                    _armed = true;
                    InstallThrowingFactoryNoLock(diagnostic);
                    try
                    {
                        BlockBootloadersBeforeAwakeNoLock();
                    }
                    catch
                    {
                        // The throwing save factory is already installed; never weaken it for diagnostics.
                    }

                    try
                    {
                        EnsureBannerNoLock(blocked: true);
                    }
                    catch
                    {
                        // The throwing save factory is the authoritative fail-closed boundary.
                    }
                }

                Debug.LogError("[AL-ISOLATED-TEST-BLOCKED] " + diagnostic);
            }
        }

        private static void VerifyBeforeFirstSceneAwakeCore()
        {
            if (!IsArmed && SessionState.GetBool(SessionActiveKey, false))
            {
                bool fullDomainReload = SessionState.GetBool(SessionFullDomainReloadKey, false);
                bool fullSceneReload = SessionState.GetBool(SessionFullSceneReloadKey, false);
                bool recovered = TryCreatePlan(
                        SessionState.GetString(SessionIdKey, string.Empty),
                        SessionState.GetString(SessionTemporaryRootKey, string.Empty),
                        SessionState.GetString(SessionPersistentRootKey, string.Empty),
                        SessionState.GetString(SessionIsolatedRootKey, string.Empty),
                        SessionState.GetString(SessionBootScenePathKey, string.Empty),
                        SessionState.GetString(SessionBootSceneGuidKey, string.Empty),
                        fullDomainReload,
                        fullSceneReload,
                        out EditorGameTestModePlan recoveredPlan,
                        out _,
                        out string recoveryMessage) &&
                    TryArm(recoveredPlan, out _, out recoveryMessage);
                if (!recovered)
                {
                    lock (Sync)
                    {
                        _activePlan = recoveredPlan;
                        _armed = true;
                        InstallThrowingFactoryNoLock(
                            string.IsNullOrWhiteSpace(recoveryMessage)
                                ? "SessionState isolation metadata could not be recovered before scene Awake."
                                : recoveryMessage);
                        Debug.LogError("[AL-ISOLATED-TEST-BLOCKED] " + _lastFailure);
                        BlockBootloadersBeforeAwakeNoLock();
                        EnsureBannerNoLock(blocked: true);
                    }

                    return;
                }
            }

            lock (Sync)
            {
                if (!_armed)
                {
                    return;
                }

                string validationMessage = string.Empty;
                if (!TryVerifyPreAwakeClean(out _, out validationMessage) ||
                    !ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedSaveFactory) ||
                    !TryValidateOwnedRoot(
                        _activePlan,
                        requireFreshRoot: true,
                        out _,
                        out validationMessage))
                {
                    string diagnostic = string.IsNullOrWhiteSpace(validationMessage)
                        ? "The isolated save factory was replaced before scene load."
                        : validationMessage;
                    InstallThrowingFactoryNoLock(diagnostic);
                    Debug.LogError("[AL-ISOLATED-TEST-BLOCKED] " + diagnostic);
                    BlockBootloadersBeforeAwakeNoLock();
                    EnsureBannerNoLock(blocked: true);
                    return;
                }

                EnsureBannerNoLock(blocked: false);
            }
        }

        private static object CreateGuardedSaveService()
        {
            lock (Sync)
            {
                string message = string.Empty;
                if (!_armed ||
                    !TryValidateOwnedRoot(
                        _activePlan,
                        requireFreshRoot: _createdSaveService == null,
                        out _,
                        out message))
                {
                    throw new InvalidOperationException(
                        "AL-ISOLATED-TEST-FACTORY-BLOCKED: " +
                        (string.IsNullOrWhiteSpace(message)
                            ? "The isolated session is not safely armed."
                            : message));
                }

                if (_createdSaveService == null)
                {
                    _createdSaveService = new LocalSaveGameService(_activePlan.IsolatedSaveRoot);
                }

                return _createdSaveService;
            }
        }

        private static void InstallThrowingFactoryNoLock(string message)
        {
            _lastFailure = message ?? string.Empty;
            _createdSaveService = null;
            if (!ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedSaveFactory))
            {
                _displacedSaveFactory = OfflineServiceStack.SaveGameFactoryOverride;
            }

            _ownedSaveFactory = () => throw new InvalidOperationException(
                "AL-ISOLATED-TEST-FAIL-CLOSED: " + _lastFailure);
            OfflineServiceStack.SaveGameFactoryOverride = _ownedSaveFactory;
        }

        private static bool TryValidateExistingPathChain(
            string systemTemporaryRoot,
            string isolatedSaveRoot,
            bool requireLeaf,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            try
            {
                string expectedRoot = BuildExpectedIsolatedRoot(systemTemporaryRoot, GetLeafName(isolatedSaveRoot));
                if (string.IsNullOrEmpty(expectedRoot) ||
                    !string.Equals(expectedRoot, isolatedSaveRoot, PathComparison))
                {
                    return Fail(
                        EditorGameTestModeFailure.EnvironmentBindingMismatch,
                        "The isolated path chain is not bound to the current temporary root.",
                        out failure,
                        out message);
                }

                string[] paths =
                {
                    systemTemporaryRoot,
                    Path.Combine(systemTemporaryRoot, TemporaryProductFolder),
                    Path.Combine(systemTemporaryRoot, TemporaryProductFolder, TemporaryModeFolder),
                    isolatedSaveRoot
                };

                for (int index = 0; index < paths.Length; index++)
                {
                    string path = Path.GetFullPath(paths[index]);
                    bool exists = Directory.Exists(path);
                    if (!exists)
                    {
                        if (index == paths.Length - 1 && requireLeaf)
                        {
                            return Fail(
                                EditorGameTestModeFailure.IsolatedRootMissing,
                                "The isolated save root does not exist.",
                                out failure,
                                out message);
                        }

                        if (File.Exists(path))
                        {
                            return Fail(
                                EditorGameTestModeFailure.ReparsePointRejected,
                                "A required isolated-path directory is occupied by a file.",
                                out failure,
                                out message);
                        }

                        continue;
                    }

                    var directory = new DirectoryInfo(path);
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return Fail(
                            EditorGameTestModeFailure.ReparsePointRejected,
                            "The isolated path chain contains a reparse point: " + path,
                            out failure,
                            out message);
                    }
                }

                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.EnvironmentBindingMismatch,
                    "The isolated path chain could not be inspected safely: " + ex.Message,
                    out failure,
                    out message);
            }
        }

        private static bool TryCreateOrValidateDirectory(
            string path,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            try
            {
                if (File.Exists(path))
                {
                    return Fail(
                        EditorGameTestModeFailure.IsolatedRootCreationFailed,
                        "A required isolated-path directory is occupied by a file.",
                        out failure,
                        out message);
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var directory = new DirectoryInfo(path);
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return Fail(
                        EditorGameTestModeFailure.ReparsePointRejected,
                        "The isolated path chain contains a reparse point: " + path,
                        out failure,
                        out message);
                }

                failure = EditorGameTestModeFailure.None;
                message = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                return Fail(
                    EditorGameTestModeFailure.IsolatedRootCreationFailed,
                    "A required isolated-path directory could not be created: " + ex.Message,
                    out failure,
                    out message);
            }
        }

        private static bool TryCollectCleanupInventory(
            EditorGameTestModePlan plan,
            out List<string> files,
            out List<string> directories,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            files = new List<string>();
            directories = new List<string>();
            var pending = new Queue<DirectoryInfo>();
            pending.Enqueue(new DirectoryInfo(plan.IsolatedSaveRoot));
            int entryCount = 0;

            while (pending.Count > 0)
            {
                DirectoryInfo directory = pending.Dequeue();
                foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos(
                             "*",
                             SearchOption.TopDirectoryOnly))
                {
                    entryCount++;
                    if (entryCount > MaximumCleanupEntries)
                    {
                        return Fail(
                            EditorGameTestModeFailure.CleanupInventoryTooLarge,
                            "The isolated root contains too many entries for bounded cleanup.",
                            out failure,
                            out message);
                    }

                    string fullPath = Path.GetFullPath(entry.FullName);
                    if (!IsSameOrNested(fullPath, plan.IsolatedSaveRoot) ||
                        string.Equals(fullPath, plan.IsolatedSaveRoot, PathComparison) ||
                        (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return Fail(
                            EditorGameTestModeFailure.ReparsePointRejected,
                            "Cleanup retained the isolated root because an entry is outside the owned no-follow boundary.",
                            out failure,
                            out message);
                    }

                    if (entry is DirectoryInfo childDirectory)
                    {
                        directories.Add(fullPath);
                        pending.Enqueue(childDirectory);
                    }
                    else
                    {
                        files.Add(fullPath);
                    }
                }
            }

            files.Sort(StringComparerForPaths);
            directories.Sort(StringComparerForPaths);
            failure = EditorGameTestModeFailure.None;
            message = string.Empty;
            return true;
        }

        private static bool TryValidateDeletionEntry(
            string isolatedRoot,
            string entryPath,
            bool expectDirectory)
        {
            try
            {
                string fullPath = Path.GetFullPath(entryPath);
                if (!IsSameOrNested(fullPath, isolatedRoot) ||
                    string.Equals(fullPath, isolatedRoot, PathComparison))
                {
                    return false;
                }

                FileSystemInfo entry = expectDirectory
                    ? (FileSystemInfo)new DirectoryInfo(fullPath)
                    : new FileInfo(fullPath);
                entry.Refresh();
                return entry.Exists && (entry.Attributes & FileAttributes.ReparsePoint) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool SameOrdinalPaths(List<string> first, List<string> second)
        {
            if (first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                if (!string.Equals(first[index], second[index], PathComparison))
                {
                    return false;
                }
            }

            return true;
        }

        private static void TryRestoreOwnershipMarker(EditorGameTestModePlan plan)
        {
            try
            {
                if (!Directory.Exists(plan.IsolatedSaveRoot) ||
                    !TryValidateExistingPathChain(
                        plan.SystemTemporaryRoot,
                        plan.IsolatedSaveRoot,
                        requireLeaf: true,
                        out _,
                        out _))
                {
                    return;
                }

                string markerPath = Path.Combine(plan.IsolatedSaveRoot, MarkerFileName);
                if (!File.Exists(markerPath))
                {
                    File.WriteAllBytes(
                        markerPath,
                        Encoding.UTF8.GetBytes(BuildMarkerContents(plan.SessionId)));
                }
            }
            catch
            {
                // Cleanup remains fail-closed; the retained path is reported to the developer.
            }
        }

        private static string GetLeafName(string path)
        {
            if (!TryNormalizeDirectory(path, out string normalized))
            {
                return string.Empty;
            }

            return Path.GetFileName(normalized);
        }

        private static IComparer<string> StringComparerForPaths =>
            PathComparison == StringComparison.OrdinalIgnoreCase
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;

        private static void EnsureBannerNoLock(bool blocked)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_bannerObject != null)
            {
                var existing = _bannerObject.GetComponent<EditorGameTestModeBanner>();
                if (existing != null)
                {
                    existing.Configure(_activePlan.SessionId, blocked, _lastFailure);
                }

                return;
            }

            _bannerObject = new GameObject("[AL] Isolated Game Test Mode");
            UnityEngine.Object.DontDestroyOnLoad(_bannerObject);
            var banner = _bannerObject.AddComponent<EditorGameTestModeBanner>();
            banner.Configure(_activePlan.SessionId, blocked, _lastFailure);
        }

        private static void BlockBootloadersBeforeAwakeNoLock()
        {
            foreach (Bootloader bootloader in UnityEngine.Object.FindObjectsOfType<Bootloader>(
                         includeInactive: true))
            {
                if (bootloader != null && bootloader.gameObject.activeSelf)
                {
                    bootloader.gameObject.SetActive(false);
                }
            }
        }

        private static bool IsCanonicalSessionId(string sessionId)
        {
            return !string.IsNullOrWhiteSpace(sessionId) &&
                sessionId.Length == 32 &&
                string.Equals(sessionId, sessionId.ToLowerInvariant(), StringComparison.Ordinal) &&
                Guid.TryParseExact(sessionId, "N", out Guid parsed) &&
                parsed != Guid.Empty;
        }

        private static bool IsCanonicalAssetGuid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            int nonZero = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool decimalDigit = character >= '0' && character <= '9';
                bool lowerHex = character >= 'a' && character <= 'f';
                if (!decimalDigit && !lowerHex)
                {
                    return false;
                }

                nonZero |= character == '0' ? 0 : 1;
            }

            return nonZero != 0;
        }

        private static string BuildDurableRecoveryContents(
            EditorGameTestModeRecoveryRecord record)
        {
            string stage;
            switch (record.Stage)
            {
                case EditorGameTestModeRecoveryStage.Starting:
                    stage = "starting";
                    break;
                case EditorGameTestModeRecoveryStage.Running:
                    stage = "running";
                    break;
                case EditorGameTestModeRecoveryStage.Recovery:
                    stage = "recovery";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(record));
            }

            return RecoveryHeader + "\n" +
                RecoveryContractVersion + "\n" +
                record.Plan.SessionId + "\n" +
                stage + "\n" +
                (record.PreviousStartSceneWasNull ? "null" : "guid") + "\n" +
                record.PreviousStartSceneGuid + "\n" +
                record.Plan.SystemTemporaryRoot + "\n";
        }

        private static string ComputeProjectIdentityToken()
        {
            string projectPath;
            try
            {
                projectPath = Path.GetFullPath(Application.dataPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            catch
            {
                projectPath = Application.dataPath ?? string.Empty;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                projectPath = projectPath.ToUpperInvariant();
            }

            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(projectPath));
            }

            var builder = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private static bool BytesEqual(byte[] first, byte[] second)
        {
            if (first == null || second == null || first.Length != second.Length)
            {
                return false;
            }

            int difference = 0;
            for (int i = 0; i < first.Length; i++)
            {
                difference |= first[i] ^ second[i];
            }

            return difference == 0;
        }

        private static bool TryNormalizeDirectory(string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                normalized = Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return !string.IsNullOrWhiteSpace(normalized);
            }
            catch
            {
                return false;
            }
        }

        private static bool PathsOverlap(string first, string second)
        {
            return IsSameOrNested(first, second) || IsSameOrNested(second, first);
        }

        private static bool IsSameOrNested(string candidate, string parent)
        {
            if (string.Equals(candidate, parent, PathComparison))
            {
                return true;
            }

            string parentPrefix = parent + Path.DirectorySeparatorChar;
            return candidate.StartsWith(parentPrefix, PathComparison);
        }

        private static bool Fail(
            EditorGameTestModeFailure failureValue,
            string failureMessage,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            failure = failureValue;
            message = failureMessage ?? string.Empty;
            return false;
        }

        private static bool FailAndRemember(
            EditorGameTestModeFailure failureValue,
            string failureMessage,
            out EditorGameTestModeFailure failure,
            out string message)
        {
            _lastFailure = failureMessage ?? string.Empty;
            return Fail(failureValue, failureMessage, out failure, out message);
        }

        private static void RememberFailure(string message)
        {
            lock (Sync)
            {
                _lastFailure = message ?? string.Empty;
            }
        }
    }

    [DefaultExecutionOrder(-31990)]
    [DisallowMultipleComponent]
    public sealed class EditorGameTestModeHostDriver : MonoBehaviour
    {
        public event Action Tick;
        public event Action Destroyed;

        public Coroutine RunCoroutine(IEnumerator routine)
        {
            if (routine == null)
            {
                throw new ArgumentNullException(nameof(routine));
            }

            return StartCoroutine(routine);
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            Tick?.Invoke();
        }

        private void OnDestroy()
        {
            Destroyed?.Invoke();
            Tick = null;
            Destroyed = null;
        }
    }

    [DefaultExecutionOrder(-32000)]
    internal sealed class EditorGameTestModeBanner : MonoBehaviour
    {
        private GUIStyle _style;
        private string _label = string.Empty;
        private bool _blocked;

        internal void Configure(string sessionId, bool blocked, string diagnostic)
        {
            _blocked = blocked;
            string shortSession = string.IsNullOrWhiteSpace(sessionId)
                ? "unknown"
                : sessionId.Substring(0, Math.Min(8, sessionId.Length));
            _label = blocked
                ? "ISOLATED TEST MODE BLOCKED • REAL SAVES NOT USED • " + diagnostic
                : "ISOLATED GAME TEST MODE • TEMP PROFILE • NOT RELEASE EVIDENCE • " + shortSession;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && !_blocked)
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "Editor focus loss");
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && !_blocked)
            {
                EditorGameTestModeBootstrap.FailClosedForLifecycleBoundary(
                    "Application pause");
            }
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                _style.normal.textColor = Color.white;
            }

            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = _blocked
                ? new Color(0.72f, 0.06f, 0.06f, 0.96f)
                : new Color(0.72f, 0.34f, 0.02f, 0.94f);
            float width = Mathf.Max(200f, Mathf.Min(Screen.width - 24f, 760f));
            GUI.Box(new Rect(12f, 12f, width, 42f), _label, _style);
            GUI.backgroundColor = previousColor;
        }
    }
}
#endif
