using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.UI.CharacterCreation;
using AL.UI.SharedMenu;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Services.Local
{
    public enum MvpApprovalStartNewDisposition
    {
        Succeeded = 0,
        ReloadBootRequired = 1,
        Failed = 2
    }

    public sealed class MvpApprovalSlotPlan
    {
        public const string ApprovalRootSuffix = ".mvp-approval-slot-v1";
        public const string MarkerFileName = ".anotherlife-mvp-approval-slot";
        public const string MarkerContents = "anotherlife-mvp-approval-slot-v1\n";
        public const string SaveRootGuardFileName = ".anotherlife-mvp-save-root-guard";
        public const string SaveRootGuardContents = "anotherlife-mvp-save-root-guard-v1\n";

        private MvpApprovalSlotPlan(string normalRoot, string approvalRoot)
        {
            NormalRoot = normalRoot;
            ApprovalRoot = approvalRoot;
            SaveRoot = Path.Combine(approvalRoot, "profile");
            MarkerPath = Path.Combine(approvalRoot, MarkerFileName);
            SaveRootGuardPath = Path.Combine(SaveRoot, SaveRootGuardFileName);
        }

        public string NormalRoot { get; }
        public string ApprovalRoot { get; }
        public string SaveRoot { get; }
        public string MarkerPath { get; }
        public string SaveRootGuardPath { get; }

        public static bool TryCreate(
            string normalRoot,
            out MvpApprovalSlotPlan plan,
            out string failure)
        {
            plan = null;
            failure = string.Empty;
            try
            {
                string normalizedNormal = Normalize(normalRoot);
                string volumeRoot = Path.GetPathRoot(normalizedNormal) ?? string.Empty;
                if (normalizedNormal.Length == 0 ||
                    string.Equals(normalizedNormal, volumeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    failure = "The normal profile root is missing or unsafe.";
                    return false;
                }

                string approvalRoot = Normalize(normalizedNormal + ApprovalRootSuffix);
                var candidate = new MvpApprovalSlotPlan(normalizedNormal, approvalRoot);
                if (SameOrDescendant(candidate.ApprovalRoot, candidate.NormalRoot) ||
                    SameOrDescendant(candidate.NormalRoot, candidate.ApprovalRoot) ||
                    SameOrDescendant(candidate.SaveRoot, candidate.NormalRoot))
                {
                    failure = "The approval profile path overlaps the normal profile root.";
                    return false;
                }

                plan = candidate;
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is NotSupportedException ||
                exception is PathTooLongException)
            {
                failure = "The approval profile path is invalid.";
                return false;
            }
        }

        internal static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A path is required.", nameof(path));
            }

            string full = Path.GetFullPath(path);
            string root = Path.GetPathRoot(full) ?? string.Empty;
            return full.Length > root.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }

        internal static bool SameOrDescendant(string candidate, string parent)
        {
            string normalizedCandidate = Normalize(candidate);
            string normalizedParent = Normalize(parent);
            return string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase) ||
                   normalizedCandidate.StartsWith(
                       normalizedParent + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Installs the isolated, durable approval profile before Boot Awake. The normal profile root is
    /// opened only as a retained identity pin and is never mutated. Ownership or path ambiguity
    /// replaces the save factory with a throwing factory so approval Players cannot fall back to
    /// Application.persistentDataPath.
    /// </summary>
    public static class MvpApprovalSlotRuntime
    {
        private static readonly object Sync = new object();
        private static readonly byte[] MarkerBytes =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(MvpApprovalSlotPlan.MarkerContents);
        private static readonly byte[] SaveRootGuardBytes =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(MvpApprovalSlotPlan.SaveRootGuardContents);

        private static readonly Func<object> FailClosedFactory = ThrowFailClosed;
        private static MvpApprovalSlotPlan _activePlan;
        private static MvpApprovalTransactionalSaveGameService _activeService;
        private static Func<object> _ownedFactory;
        private static MvpApprovalVirtualStore _activeStore;
        [ThreadStatic] private static LocalSaveGameService _deleteAuthorizedService;
#if UNITY_INCLUDE_TESTS
        private static bool? _approvalFlavorOverrideForTests;
        [ThreadStatic] internal static Action BeforeAuthorizedDeleteForTests;
        [ThreadStatic] internal static Action BeforeFreshLoadForTests;
#endif

        public static MvpApprovalSlotPlan ActivePlan
        {
            get
            {
                using (AcquireRuntimeLock())
                {
                    return _activePlan;
                }
            }
        }

        public static bool TryReloadBootAfterReset(out string failure)
        {
#if AL_MVP_APPROVAL_SLOT && !UNITY_EDITOR
            try
            {
                SceneManager.LoadScene("Boot", LoadSceneMode.Single);
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[AL-MVP-APPROVAL-BOOT-RELOAD-FAILED] " +
                    exception.GetType().Name);
                failure = "Approval Boot reload failed.";
                return false;
            }
#else
            failure = "Restart the approval Player to begin the cleared journey.";
            return false;
#endif
        }

        public static ISaveGameService ActiveService
        {
            get
            {
                using (AcquireRuntimeLock())
                {
                    return _activeService;
                }
            }
        }

        public static bool IsApprovalFlavor
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (_approvalFlavorOverrideForTests.HasValue)
                {
                    return _approvalFlavorOverrideForTests.Value;
                }
#endif
#if AL_MVP_APPROVAL_SLOT && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeFirstSceneAwake()
        {
            if (!IsApprovalFlavor)
            {
                return;
            }

            if (!TryInstall(true, Application.persistentDataPath, out string failure))
            {
                Debug.LogError("[AL-MVP-APPROVAL-SLOT-FAILED] " + failure);
            }
        }

        public static bool TryInstall(
            bool approvalFlavor,
            string normalRoot,
            out string failure)
        {
            failure = string.Empty;
            if (!approvalFlavor)
            {
                return true;
            }

            try
            {
                using (AcquireRuntimeLock())
                {
                    try
                    {
                        if (!MvpApprovalSlotPlan.TryCreate(
                                normalRoot,
                                out MvpApprovalSlotPlan plan,
                                out failure))
                        {
                            InstallFailClosedFactoryLocked();
                            return false;
                        }

                        if (_activePlan != null)
                        {
                            if (SamePlan(_activePlan, plan) &&
                                ReferenceEquals(
                                    OfflineServiceStack.SaveGameFactoryOverride,
                                    _ownedFactory) &&
                                TryValidateOwnedSlot(_activePlan, out failure))
                            {
                                return true;
                            }

                            failure =
                                "A different approval profile runtime is already active.";
                            InstallFailClosedFactoryLocked();
                            return false;
                        }

                        if (OfflineServiceStack.SaveGameFactoryOverride != null)
                        {
                            failure =
                                "A foreign save factory prevents approval profile ownership.";
                            InstallFailClosedFactoryLocked();
                            return false;
                        }

                        if (!TryPrepareOwnedSlot(
                                plan,
                                out MvpApprovalVirtualStore store,
                                out failure))
                        {
                            InstallFailClosedFactoryLocked();
                            return false;
                        }

                        if (!store.TryValidate(plan, out failure))
                        {
                            store.Revoke();
                            InstallFailClosedFactoryLocked();
                            return false;
                        }

                        _activeStore?.Revoke();
                        _activeStore = store;
                        _activePlan = plan;
                        _activeService = null;
                        _ownedFactory = CreateGuardedSaveService;
                        OfflineServiceStack.SaveGameFactoryOverride = _ownedFactory;
                        return true;
                    }
                    catch (Exception exception)
                    {
                        failure = "The isolated approval runtime failed closed: " +
                                  exception.GetType().Name;
                        InstallFailClosedFactoryLocked();
                        return false;
                    }
                }
            }
            catch (TimeoutException)
            {
                failure = "The isolated approval runtime is busy.";
                return false;
            }
        }

        public static bool CanStartNewJourney(out string failure)
        {
            try
            {
                using (AcquireRuntimeLock())
                {
                    if (_activePlan == null ||
                        _activeService == null ||
                        _activeService.PersistenceFrozen ||
                        !ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedFactory))
                    {
                        failure = "The isolated approval journey is not ready.";
                        return false;
                    }

                    return TryValidateOwnedSlot(_activePlan, out failure);
                }
            }
            catch (TimeoutException)
            {
                failure = "The isolated approval runtime is busy.";
                return false;
            }
        }

        public static MvpApprovalStartNewDisposition TryStartNewJourney(out string failure)
        {
            try
            {
                return TryStartNewJourneyCore(out failure);
            }
            catch (TimeoutException)
            {
                failure = "The isolated approval runtime is busy.";
                return MvpApprovalStartNewDisposition.Failed;
            }
        }

        private static MvpApprovalStartNewDisposition TryStartNewJourneyCore(
            out string failure)
        {
            failure = string.Empty;
            MvpApprovalTransactionalSaveGameService service;
            MvpApprovalSlotPlan plan;
            bool reloadBootRequired = false;
            using (AcquireRuntimeLock())
            {
                if (!CanStartNewJourney(out failure))
                {
                    return MvpApprovalStartNewDisposition.Failed;
                }

                service = _activeService;
                plan = _activePlan;
                if (ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker))
                {
                    if (!marker.TryGetExpected<ISaveGameService>(out var expected) ||
                        !ReferenceEquals(expected, service))
                    {
                        failure = "The isolated approval service is not the active profile owner.";
                        return MvpApprovalStartNewDisposition.Failed;
                    }

                    reloadBootRequired = marker.LoadState == OfflineStackLoadState.Failed;
                    if (!reloadBootRequired && marker.LoadState != OfflineStackLoadState.Succeeded)
                    {
                        failure = "The isolated approval load has not reached a resettable state.";
                        return MvpApprovalStartNewDisposition.Failed;
                    }
                }

            }

            if (!TryValidateOwnedSlot(plan, out failure))
            {
                return MvpApprovalStartNewDisposition.Failed;
            }

            try
            {
                MvpApprovalStartNewDisposition disposition = service.ExecuteReset(inner =>
                {
                    try
                    {
                        _deleteAuthorizedService = inner;
#if UNITY_INCLUDE_TESTS
                        BeforeAuthorizedDeleteForTests?.Invoke();
#endif
                        inner.DeleteSave();
                    }
                    finally
                    {
                        _deleteAuthorizedService = null;
                    }

                    if (inner.HasSave())
                    {
                        throw new InvalidOperationException(
                            "The isolated approval journey retained profile artifacts after reset.");
                    }

                    if (reloadBootRequired)
                    {
                        return MvpApprovalStartNewDisposition.ReloadBootRequired;
                    }

#if UNITY_INCLUDE_TESTS
                    BeforeFreshLoadForTests?.Invoke();
#endif
                    inner.Load();
                    if (inner.LastLoadStatus != SaveLoadStatus.CreatedNew ||
                        inner.CurrentSave == null ||
                        inner.CurrentSave.SelectedRealm != RealmId.None ||
                        MvpLoopSaveCodec.Read(inner.CurrentSave).ShouldSkipCreate)
                    {
                        throw new InvalidOperationException(
                            "The isolated approval journey did not verify as a fresh profile.");
                    }

                    return MvpApprovalStartNewDisposition.Succeeded;
                });

                AL.Data.Runtime.SliceRunState.Reset();
                AL.VerticalSlice.SliceRunState.Reset();
                CharacterCreationIdentity.ResetClaims();
                AL.ChampionMode.FirstSessionChampionStart.ResetToFirstSessionLanding();
                CrossModeSession.Reset();
                return disposition;
            }
            catch (Exception exception)
            {
                failure = "The isolated approval journey could not be reset: " +
                          exception.GetType().Name;
                return MvpApprovalStartNewDisposition.Failed;
            }
        }

        public static bool IsDeleteAuthorized(ISaveGameService saveGameService)
        {
            return saveGameService != null &&
                   ReferenceEquals(saveGameService, _deleteAuthorizedService);
        }

        internal static bool TryDeleteAuthorizedArtifact(
            LocalSaveGameService saveGameService,
            string artifactPath,
            out string failure)
        {
            failure = string.Empty;
            if (!IsDeleteAuthorized(saveGameService))
            {
                failure = "The approval artifact delete is not authorized for this save service.";
                return false;
            }

            return _activeStore != null && _activeStore.TryDelete(
                artifactPath,
                out failure);
        }

        private static object CreateGuardedSaveService()
        {
            using (AcquireRuntimeLock())
            {
                string failure = string.Empty;
                if (_activePlan == null ||
                    !ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedFactory) ||
                    !TryValidateOwnedSlot(_activePlan, out failure))
                {
                    throw new InvalidOperationException(
                        "The isolated approval save factory failed ownership validation: " + failure);
                }

                if (_activeService != null)
                {
                    return _activeService;
                }

                var inner = new LocalSaveGameService(
                    _activePlan.SaveRoot,
                    new MvpApprovalSaveFileOperations(_activePlan.SaveRoot, _activeStore));
                _activeService = new MvpApprovalTransactionalSaveGameService(
                    _activeStore,
                    inner);
                return _activeService;
            }
        }

        private static bool TryPrepareOwnedSlot(
            MvpApprovalSlotPlan plan,
            out MvpApprovalVirtualStore store,
            out string failure)
        {
            store = null;
            if (!TryValidatePlanPaths(plan, out failure))
            {
                return false;
            }

            return MvpApprovalVirtualStore.TryPrepare(plan, out store, out failure);
        }

        private static bool TryValidateOwnedSlot(
            MvpApprovalSlotPlan plan,
            out string failure)
        {
            if (!TryValidatePlanPaths(plan, out failure))
            {
                return false;
            }

            if (_activeStore == null)
            {
                failure = "The approval virtual store is unavailable.";
                return false;
            }

            return _activeStore.TryValidate(plan, out failure);
        }

        private static bool TryValidatePlanPaths(MvpApprovalSlotPlan plan, out string failure)
        {
            failure = string.Empty;
            if (plan == null ||
                MvpApprovalSlotPlan.SameOrDescendant(plan.ApprovalRoot, plan.NormalRoot) ||
                MvpApprovalSlotPlan.SameOrDescendant(plan.NormalRoot, plan.ApprovalRoot) ||
                MvpApprovalSlotPlan.SameOrDescendant(plan.SaveRoot, plan.NormalRoot) ||
                !MvpApprovalSlotPlan.SameOrDescendant(plan.SaveRoot, plan.ApprovalRoot))
            {
                failure = "The approval profile path overlaps the normal profile root.";
                return false;
            }

            return true;
        }

        private static bool TryReadExactMarker(MvpApprovalSlotPlan plan, out string failure)
        {
            failure = string.Empty;
            try
            {
                if (!File.Exists(plan.MarkerPath) || Directory.Exists(plan.MarkerPath))
                {
                    failure = "The approval profile ownership marker is missing.";
                    return false;
                }

                if ((File.GetAttributes(plan.MarkerPath) & FileAttributes.ReparsePoint) != 0)
                {
                    failure = "The approval profile ownership marker crosses a reparse boundary.";
                    return false;
                }

                byte[] observed = File.ReadAllBytes(plan.MarkerPath);
                if (!observed.SequenceEqual(MarkerBytes))
                {
                    failure = "The approval profile ownership marker does not match.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                failure = "The approval profile ownership marker could not be verified: " +
                          exception.GetType().Name;
                return false;
            }
        }

        private static bool TryReadExactSaveRootGuard(
            MvpApprovalSlotPlan plan,
            out string failure)
        {
            failure = string.Empty;
            try
            {
                if (!File.Exists(plan.SaveRootGuardPath) ||
                    Directory.Exists(plan.SaveRootGuardPath))
                {
                    failure = "The approval save-root guard is missing.";
                    return false;
                }

                if ((File.GetAttributes(plan.SaveRootGuardPath) & FileAttributes.ReparsePoint) != 0)
                {
                    failure = "The approval save-root guard crosses a reparse boundary.";
                    return false;
                }

                if (!File.ReadAllBytes(plan.SaveRootGuardPath).SequenceEqual(SaveRootGuardBytes))
                {
                    failure = "The approval save-root guard does not match.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                failure = "The approval save-root guard could not be verified: " +
                          exception.GetType().Name;
                return false;
            }
        }

        private static bool TryRejectReparseBoundary(string path, out string failure)
        {
            failure = string.Empty;
            try
            {
                string cursor = MvpApprovalSlotPlan.Normalize(path);
                string volumeRoot = Path.GetPathRoot(cursor) ?? string.Empty;
                while (!string.IsNullOrEmpty(cursor))
                {
                    if ((File.Exists(cursor) || Directory.Exists(cursor)) &&
                        (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    {
                        failure = "The approval profile path crosses a reparse boundary.";
                        return false;
                    }

                    if (string.Equals(cursor, volumeRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    string parent = Path.GetDirectoryName(cursor);
                    if (string.IsNullOrEmpty(parent) || SamePath(parent, cursor))
                    {
                        break;
                    }
                    cursor = MvpApprovalSlotPlan.Normalize(parent);
                }

                return true;
            }
            catch (Exception exception)
            {
                failure = "The approval profile reparse boundary could not be verified: " +
                          exception.GetType().Name;
                return false;
            }
        }

        private static bool SamePlan(MvpApprovalSlotPlan left, MvpApprovalSlotPlan right) =>
            left != null && right != null &&
            SamePath(left.NormalRoot, right.NormalRoot) &&
            SamePath(left.ApprovalRoot, right.ApprovalRoot) &&
            SamePath(left.SaveRoot, right.SaveRoot);

        private static bool SamePath(string left, string right)
        {
            try
            {
                return string.Equals(
                    MvpApprovalSlotPlan.Normalize(left),
                    MvpApprovalSlotPlan.Normalize(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void InstallFailClosedFactoryLocked()
        {
            if (!Monitor.IsEntered(Sync))
            {
                throw new InvalidOperationException(
                    "Approval fail-closed installation requires runtime-lock ownership.");
            }

            try
            {
                _activeStore?.Revoke();
            }
            catch
            {
                // The throwing factory remains the final authority even if the
                // old store cannot be synchronously revoked.
            }
            finally
            {
                _activeStore = null;
                _activePlan = null;
                _activeService = null;
                _ownedFactory = null;
                OfflineServiceStack.SaveGameFactoryOverride = FailClosedFactory;
            }
        }

        private static object ThrowFailClosed()
        {
            throw new InvalidOperationException(
                "The MVP approval profile failed closed before the offline stack was created.");
        }

        private static IDisposable AcquireRuntimeLock()
        {
            if (!Monitor.TryEnter(Sync, TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException(
                    "Approval runtime synchronization timed out.");
            }

            return new RuntimeLockLease(Sync);
        }

        private sealed class RuntimeLockLease : IDisposable
        {
            private object _sync;

            internal RuntimeLockLease(object sync)
            {
                _sync = sync;
            }

            public void Dispose()
            {
                object sync = Interlocked.Exchange(ref _sync, null);
                if (sync != null)
                {
                    Monitor.Exit(sync);
                }
            }
        }

#if UNITY_INCLUDE_TESTS
        internal static void SetApprovalFlavorForTests(bool? value)
        {
            _approvalFlavorOverrideForTests = value;
        }

        internal static void ResetRuntimePreservingStoreForTests()
        {
            using (AcquireRuntimeLock())
            {
                if (ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedFactory) ||
                    ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, FailClosedFactory))
                {
                    OfflineServiceStack.SaveGameFactoryOverride = null;
                }

                _activeStore?.Revoke();
                _activeStore = null;
                _activePlan = null;
                _activeService = null;
                _ownedFactory = null;
                _deleteAuthorizedService = null;
            }
        }

        internal static void ResetForTests()
        {
            using (AcquireRuntimeLock())
            {
                if (ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, _ownedFactory) ||
                    ReferenceEquals(OfflineServiceStack.SaveGameFactoryOverride, FailClosedFactory))
                {
                    OfflineServiceStack.SaveGameFactoryOverride = null;
                }
                _activePlan = null;
                _activeService = null;
                _activeStore?.DeletePersistentDataForTests();
                _activeStore?.Revoke();
                _activeStore = null;
                _ownedFactory = null;
                _deleteAuthorizedService = null;
                _approvalFlavorOverrideForTests = null;
                BeforeAuthorizedDeleteForTests = null;
                BeforeFreshLoadForTests = null;
            }
        }
#endif
    }


#if UNITY_INCLUDE_TESTS
    // Retained temporarily for historical regression probes only. Approval
    // production code never compiles or calls this path-based implementation.
    internal static class WindowsOwnedArtifactDeletion
    {
        private static readonly byte[] ExpectedGuardBytes =
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(MvpApprovalSlotPlan.SaveRootGuardContents);
        private const uint GenericRead = 0x80000000;
        private const uint DeleteAccess = 0x00010000;
        private const uint FileReadAttributes = 0x00000080;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint ShareDelete = 0x00000004;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileBegin = 0;
        private const uint FileAttributeReparsePoint = 0x00000400;
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;
        private const int FileDispositionInfo = 4;
        private const int FileAttributeTagInfo = 9;
#if UNITY_INCLUDE_TESTS
        [ThreadStatic] internal static Action<string> AfterArtifactValidationForTests;
#endif

        [StructLayout(LayoutKind.Sequential)]
        private struct FileDispositionInformation
        {
            public byte DeleteFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileAttributeTagInformation
        {
            public uint FileAttributes;
            public uint ReparseTag;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public uint CreationTimeLow;
            public uint CreationTimeHigh;
            public uint LastAccessTimeLow;
            public uint LastAccessTimeHigh;
            public uint LastWriteTimeLow;
            public uint LastWriteTimeHigh;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        internal sealed class OwnedSlotLease : IDisposable
        {
            private readonly object _sync = new object();
            private SafeFileHandle _normalRoot;
            private SafeFileHandle _approvalRoot;
            private SafeFileHandle _saveRoot;
            private SafeFileHandle _saveRootGuard;
            private int _referenceCount = 1;

            internal OwnedSlotLease(
                SafeFileHandle normalRoot,
                SafeFileHandle approvalRoot,
                SafeFileHandle saveRoot,
                SafeFileHandle saveRootGuard)
            {
                _normalRoot = normalRoot;
                _approvalRoot = approvalRoot;
                _saveRoot = saveRoot;
                _saveRootGuard = saveRootGuard;
            }

            internal bool TryRetain(out IDisposable reference)
            {
                lock (_sync)
                {
                    if (_referenceCount <= 0)
                    {
                        reference = null;
                        return false;
                    }

                    _referenceCount++;
                    reference = new LeaseReference(this);
                    return true;
                }
            }

            internal bool TryValidateGuard(string expectedGuard, out string failure)
            {
                lock (_sync)
                {
                    if (_referenceCount <= 0 ||
                        _saveRootGuard == null ||
                        _saveRootGuard.IsInvalid)
                    {
                        failure = "The approval save-root guard lease is unavailable.";
                        return false;
                    }

                    string expected;
                    try
                    {
                        expected = Normalize(expectedGuard);
                    }
                    catch (Exception exception)
                    {
                        failure = "Approval save-root guard path could not be normalized: " +
                                  exception.GetType().Name;
                        return false;
                    }

                    if (!TryGetSafeFinalPath(
                            _saveRootGuard,
                            out string finalGuard,
                            out failure) ||
                        !string.Equals(
                            finalGuard,
                            expected,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(failure))
                        {
                            failure =
                                "Approval save-root guard lease did not resolve to its expected path.";
                        }
                        return false;
                    }

                    if (!TryReadExactGuardBytes(_saveRootGuard, out failure))
                    {
                        return false;
                    }

                    return true;
                }
            }

            public void Dispose()
            {
                SafeFileHandle saveRootGuard = null;
                SafeFileHandle saveRoot = null;
                SafeFileHandle approvalRoot = null;
                SafeFileHandle normalRoot = null;
                lock (_sync)
                {
                    if (_referenceCount <= 0 || --_referenceCount > 0)
                    {
                        return;
                    }

                    saveRootGuard = _saveRootGuard;
                    _saveRootGuard = null;
                    saveRoot = _saveRoot;
                    _saveRoot = null;
                    approvalRoot = _approvalRoot;
                    _approvalRoot = null;
                    normalRoot = _normalRoot;
                    _normalRoot = null;
                }

                saveRootGuard?.Dispose();
                saveRoot?.Dispose();
                approvalRoot?.Dispose();
                normalRoot?.Dispose();
            }

            private sealed class LeaseReference : IDisposable
            {
                private OwnedSlotLease _owner;

                internal LeaseReference(OwnedSlotLease owner)
                {
                    _owner = owner;
                }

                public void Dispose()
                {
                    Interlocked.Exchange(ref _owner, null)?.Dispose();
                }
            }
        }

        internal static bool TryAcquireSingleLinkArtifactLease(
            string saveRoot,
            string artifactPath,
            out IDisposable lease,
            out bool missing,
            out string failure)
        {
            lease = null;
            missing = false;
            failure = string.Empty;
            SafeFileHandle handle = null;
            try
            {
                string expectedRoot = Normalize(saveRoot);
                string expectedArtifact = Normalize(artifactPath);
                if (!string.Equals(
                        Normalize(Path.GetDirectoryName(expectedArtifact)),
                        expectedRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    failure = "Approval mutation target is not a direct save-root child.";
                    return false;
                }

                handle = CreateFile(
                    expectedArtifact,
                    0,
                    ShareRead | ShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    FileFlagOpenReparsePoint,
                    IntPtr.Zero);
                if (handle == null || handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    missing = error == ErrorFileNotFound || error == ErrorPathNotFound;
                    failure = "Approval mutation target could not be opened: Win32 " + error;
                    return false;
                }

                if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
                {
                    failure = "Approval mutation target identity could not be read: Win32 " +
                              Marshal.GetLastWin32Error();
                    return false;
                }

                if ((information.FileAttributes & FileAttributeReparsePoint) != 0 ||
                    information.NumberOfLinks != 1)
                {
                    failure =
                        "Approval mutation target must be a non-reparse single-link file.";
                    return false;
                }

                if (!TryGetSafeFinalPath(handle, out string finalArtifact, out failure) ||
                    !string.Equals(
                        finalArtifact,
                        expectedArtifact,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(failure))
                    {
                        failure = "Approval mutation target changed identity during validation.";
                    }
                    return false;
                }

                lease = handle;
                handle = null;
                return true;
            }
            catch (Exception exception)
            {
                failure = "Approval mutation target validation failed: " +
                          exception.GetType().Name;
                return false;
            }
            finally
            {
                handle?.Dispose();
            }
        }

        internal static bool TryAcquireOwnedSlotLease(
            MvpApprovalSlotPlan plan,
            out OwnedSlotLease lease,
            out string failure)
        {
            lease = null;
            failure = string.Empty;
            if (plan == null || Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                failure = "Approval profile ownership leases require Windows directory handles.";
                return false;
            }

            SafeFileHandle normalRoot = null;
            SafeFileHandle approvalRoot = null;
            SafeFileHandle saveRoot = null;
            SafeFileHandle saveRootGuard = null;
            try
            {
                if (!TryOpenPinnedDirectory(plan.NormalRoot, out normalRoot, out failure) ||
                    !TryOpenPinnedDirectory(plan.ApprovalRoot, out approvalRoot, out failure) ||
                    !TryOpenPinnedDirectory(plan.SaveRoot, out saveRoot, out failure) ||
                    !TryOpenPinnedGuard(plan.SaveRootGuardPath, out saveRootGuard, out failure))
                {
                    return false;
                }

                lease = new OwnedSlotLease(
                    normalRoot,
                    approvalRoot,
                    saveRoot,
                    saveRootGuard);
                normalRoot = null;
                approvalRoot = null;
                saveRoot = null;
                saveRootGuard = null;
                return true;
            }
            finally
            {
                saveRootGuard?.Dispose();
                saveRoot?.Dispose();
                approvalRoot?.Dispose();
                normalRoot?.Dispose();
            }
        }

        private static bool TryOpenPinnedDirectory(
            string expectedDirectory,
            out SafeFileHandle handle,
            out string failure)
        {
            failure = string.Empty;
            string expected;
            try
            {
                expected = Normalize(expectedDirectory);
            }
            catch (Exception exception)
            {
                handle = null;
                failure = "Approval directory path could not be normalized: " +
                          exception.GetType().Name;
                return false;
            }

            handle = CreateFile(
                expected,
                FileReadAttributes | DeleteAccess,
                ShareRead | ShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid ||
                !TryGetSafeFinalPath(handle, out string finalDirectory, out failure) ||
                !string.Equals(finalDirectory, expected, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(failure))
                {
                    failure = "Approval directory lease did not resolve to its expected path.";
                }
                handle.Dispose();
                handle = null;
                return false;
            }

            return true;
        }

        private static bool TryOpenPinnedGuard(
            string expectedGuard,
            out SafeFileHandle handle,
            out string failure)
        {
            failure = string.Empty;
            string expected;
            try
            {
                expected = Normalize(expectedGuard);
            }
            catch (Exception exception)
            {
                handle = null;
                failure = "Approval save-root guard path could not be normalized: " +
                          exception.GetType().Name;
                return false;
            }

            handle = CreateFile(
                expected,
                GenericRead,
                ShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid ||
                !TryGetSafeFinalPath(handle, out string finalGuard, out failure) ||
                !string.Equals(finalGuard, expected, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(failure))
                {
                    failure = "Approval save-root guard lease did not resolve to its expected path.";
                }
                handle.Dispose();
                handle = null;
                return false;
            }

            return true;
        }

        internal static bool TryDeleteDirectChild(
            string expectedDirectory,
            string artifactPath,
            out string failure)
        {
            failure = string.Empty;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                failure = "Approval profile reset requires Windows handle-safe deletion.";
                return false;
            }

            string expected;
            string artifact;
            try
            {
                expected = Normalize(expectedDirectory);
                artifact = Normalize(artifactPath);
            }
            catch (Exception exception)
            {
                failure = "Approval artifact paths could not be normalized: " +
                          exception.GetType().Name;
                return false;
            }

            if (!string.Equals(
                    Normalize(Path.GetDirectoryName(artifact)),
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                failure = "Approval reset rejected an artifact outside its direct save root.";
                return false;
            }

            using (SafeFileHandle directoryHandle = CreateFile(
                       expected,
                       FileReadAttributes,
                       ShareRead | ShareWrite | ShareDelete,
                       IntPtr.Zero,
                       OpenExisting,
                       FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                       IntPtr.Zero))
            {
                if (directoryHandle.IsInvalid ||
                    !TryGetSafeFinalPath(directoryHandle, out string finalDirectory, out failure) ||
                    !string.Equals(finalDirectory, expected, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(failure))
                    {
                        failure = "Approval save root handle did not resolve to its owned path.";
                    }
                    return false;
                }

                using (SafeFileHandle artifactHandle = CreateFile(
                           artifact,
                           DeleteAccess | FileReadAttributes,
                           ShareRead | ShareWrite,
                           IntPtr.Zero,
                           OpenExisting,
                           FileFlagOpenReparsePoint,
                           IntPtr.Zero))
                {
                    if (artifactHandle.IsInvalid)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ErrorFileNotFound || error == ErrorPathNotFound)
                        {
                            return true;
                        }

                        failure = "Approval artifact handle could not be opened: Win32 " + error;
                        return false;
                    }

                    if (!TryGetSafeFinalPath(artifactHandle, out string finalArtifact, out failure) ||
                        !string.Equals(
                            Normalize(Path.GetDirectoryName(finalArtifact)),
                            finalDirectory,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(failure))
                        {
                            failure = "Approval artifact handle escaped its owned save root.";
                        }
                        return false;
                    }

#if UNITY_INCLUDE_TESTS
                    AfterArtifactValidationForTests?.Invoke(artifact);
#endif
                    var disposition = new FileDispositionInformation { DeleteFile = 1 };
                    if (!SetFileInformationByHandle(
                            artifactHandle,
                            FileDispositionInfo,
                            ref disposition,
                            (uint)Marshal.SizeOf<FileDispositionInformation>()))
                    {
                        failure = "Approval artifact handle delete failed: Win32 " +
                                  Marshal.GetLastWin32Error();
                        return false;
                    }

                    return true;
                }
            }
        }

        private static bool TryReadExactGuardBytes(
            SafeFileHandle handle,
            out string failure)
        {
            if (!GetFileSizeEx(handle, out long length) ||
                length != ExpectedGuardBytes.Length)
            {
                failure = "Approval save-root guard contents have an unexpected length.";
                return false;
            }

            if (!SetFilePointerEx(handle, 0, out _, FileBegin))
            {
                failure = "Approval save-root guard contents could not be rewound.";
                return false;
            }

            var actual = new byte[ExpectedGuardBytes.Length];
            if (!ReadFile(
                    handle,
                    actual,
                    (uint)actual.Length,
                    out uint bytesRead,
                    IntPtr.Zero) ||
                bytesRead != actual.Length)
            {
                failure = "Approval save-root guard contents could not be read exactly.";
                return false;
            }

            for (int i = 0; i < ExpectedGuardBytes.Length; i++)
            {
                if (actual[i] != ExpectedGuardBytes[i])
                {
                    failure = "Approval save-root guard contents are foreign.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool TryGetSafeFinalPath(
            SafeFileHandle handle,
            out string finalPath,
            out string failure)
        {
            finalPath = string.Empty;
            failure = string.Empty;
            if (!GetFileInformationByHandleEx(
                    handle,
                    FileAttributeTagInfo,
                    out FileAttributeTagInformation attributes,
                    (uint)Marshal.SizeOf<FileAttributeTagInformation>()))
            {
                failure = "Approval handle attributes could not be read: Win32 " +
                          Marshal.GetLastWin32Error();
                return false;
            }

            if ((attributes.FileAttributes & FileAttributeReparsePoint) != 0)
            {
                failure = "Approval handle resolved to a reparse point.";
                return false;
            }

            var buffer = new StringBuilder(1024);
            uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0 || length >= buffer.Capacity)
            {
                failure = "Approval handle final path could not be resolved: Win32 " +
                          Marshal.GetLastWin32Error();
                return false;
            }

            finalPath = NormalizeDevicePath(buffer.ToString());
            return true;
        }

        private static string Normalize(string path) =>
            Path.GetFullPath(path ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static string NormalizeDevicePath(string path)
        {
            const string uncPrefix = @"\\?\UNC\";
            const string devicePrefix = @"\\?\";
            if (path.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = @"\\" + path.Substring(uncPrefix.Length);
            }
            else if (path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(devicePrefix.Length);
            }

            return Normalize(path);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation fileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileSizeEx(
            SafeFileHandle file,
            out long fileSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFilePointerEx(
            SafeFileHandle file,
            long distanceToMove,
            out long newFilePointer,
            uint moveMethod);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadFile(
            SafeFileHandle file,
            [Out] byte[] buffer,
            uint bytesToRead,
            out uint bytesRead,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            int fileInformationClass,
            out FileAttributeTagInformation fileInformation,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(
            SafeFileHandle file,
            int fileInformationClass,
            ref FileDispositionInformation fileInformation,
            uint bufferSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            StringBuilder filePath,
            uint filePathLength,
            uint flags);
    }
#endif
}
