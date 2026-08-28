using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AL.Core.Interfaces;
using UnityEngine;

namespace AL.Services.Local
{
    /// <summary>
    /// Durable approval-only virtual filesystem. The complete file set is committed
    /// as one flushed HKCU registry value under a cross-process mutex, so approval
    /// persistence never resolves, creates, or mutates normal-save paths.
    /// </summary>
    internal sealed class MvpApprovalVirtualStore
    {
        private const int FormatVersion = 1;
        private const string RegistrySubKeyPath = @"Software\AnotherLife\MvpApprovalVfsV1";
        private const string MutexPrefix = @"Global\AnotherLife.MvpApprovalVfsV1.";
#if UNITY_INCLUDE_TESTS
        private const string TestRegistrySubKeyRoot =
            @"Software\AnotherLife\Tests\MvpApprovalVfsV1";
        internal static string RegistrySubKeyPathOverrideForTests;
        internal static Action BeforePersistForTests;
#endif

        private readonly string _ownerFingerprint;
        private readonly string _registrySubKeyPath;
        private readonly string _registryValueName;
        private readonly string _userSid;
        private readonly string _mutexName;
        private readonly string _saveRoot;
        private volatile bool _revoked;
        private int _transactionOwnerThreadId;
        private VirtualEnvelope _transactionBaselineEnvelope;
        private VirtualEnvelope _transactionEnvelope;
        private bool _transactionDirty;
#if UNITY_INCLUDE_TESTS
        private int _commitCountForTests;
        internal int CommitCountForTests => _commitCountForTests;
#endif

        private MvpApprovalVirtualStore(MvpApprovalSlotPlan plan)
        {
            _registrySubKeyPath = ResolveRegistrySubKeyPath();
            WindowsRegistryValueStore.EnsureWindows();
            _saveRoot = NormalizePath(plan.SaveRoot);
            _ownerFingerprint = Fingerprint(plan.NormalRoot);
            _registryValueName = _ownerFingerprint;
            _userSid = WindowsNamedMutex.GetCurrentUserSid();
            _mutexName = MutexPrefix + _userSid + "." + _ownerFingerprint;
        }

        internal static bool TryPrepare(
            MvpApprovalSlotPlan plan,
            out MvpApprovalVirtualStore store,
            out string failure)
        {
            store = null;
            failure = string.Empty;
            try
            {
                var candidate = new MvpApprovalVirtualStore(plan);
                using IDisposable crossProcess = candidate.AcquireCrossProcessLock();
                if (!candidate.HasEnvelopeLocked())
                {
                    candidate.PersistEnvelopeLocked(candidate.CreateEmptyEnvelope());
                }
                else
                {
                    candidate.LoadLocked();
                }

                store = candidate;
                return true;
            }
            catch (Exception exception) when (IsStoreFailure(exception))
            {
                failure = "The approval virtual store could not be prepared: " +
                          exception.GetType().Name;
                return false;
            }
        }

        internal bool TryValidate(MvpApprovalSlotPlan plan, out string failure)
        {
            try
            {
                using IDisposable crossProcess = AcquireCrossProcessLock();
                if (_revoked)
                {
                    failure = "The approval virtual store has been revoked.";
                    return false;
                }

                if (plan == null ||
                    !string.Equals(_saveRoot, NormalizePath(plan.SaveRoot), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(_ownerFingerprint, Fingerprint(plan.NormalRoot), StringComparison.Ordinal))
                {
                    failure = "The approval virtual store owner does not match.";
                    return false;
                }

                LoadLocked();
                failure = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsStoreFailure(exception))
            {
                failure = exception is IOException &&
                          exception.Message.IndexOf("mutex", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "The approval virtual-store mutex is unavailable."
                    : "The approval virtual store failed validation: " +
                      exception.GetType().Name;
                return false;
            }
        }

        internal bool TryAcquireOperation(out IDisposable lease, out string failure)
        {
            IDisposable crossProcess = null;
            try
            {
                crossProcess = AcquireCrossProcessLock();
                if (_revoked)
                {
                    crossProcess.Dispose();
                    crossProcess = null;
                    lease = null;
                    failure = "The approval virtual-store operation lease is unavailable.";
                    return false;
                }

                lease = new OperationLease(crossProcess);
                crossProcess = null;
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                crossProcess?.Dispose();
                lease = null;
                failure = "The approval virtual-store operation lease failed: " +
                          exception.GetType().Name;
                return false;
            }
        }

        internal bool TryBeginTransaction(
            out ApprovalTransaction transaction,
            out string failure)
        {
            transaction = null;
            failure = string.Empty;
            IDisposable crossProcess = null;
            try
            {
                crossProcess = AcquireCrossProcessLock();
                EnsureActiveLocked();
                if (_transactionEnvelope != null)
                {
                    throw new InvalidOperationException(
                        "An approval save transaction is already active.");
                }

                _transactionOwnerThreadId = Thread.CurrentThread.ManagedThreadId;
                _transactionBaselineEnvelope =
                    CanonicalPersistentEnvelope(LoadPersistedEnvelopeLocked());
                _transactionEnvelope = CreateWorkingEnvelope(_transactionBaselineEnvelope);
                _transactionDirty = false;
                transaction = new ApprovalTransaction(this, crossProcess);
                return true;
            }
            catch (Exception exception)
            {
                crossProcess?.Dispose();
                failure = "The approval save transaction could not begin: " +
                          exception.GetType().Name;
                return false;
            }
        }

        private void CommitTransaction()
        {
            EnsureTransactionOwner();
            if (!_transactionDirty)
            {
                return;
            }

            VirtualEnvelope next = CanonicalPersistentEnvelope(_transactionEnvelope);
            if (CanonicalPrimaryEquals(_transactionBaselineEnvelope, next))
            {
                _transactionDirty = false;
                return;
            }

#if UNITY_INCLUDE_TESTS
            BeforePersistForTests?.Invoke();
#endif
            EnsureActiveLocked();
            PersistEnvelopeLocked(next);
#if UNITY_INCLUDE_TESTS
            _commitCountForTests++;
#endif
            _transactionBaselineEnvelope = CloneEnvelope(next);
            _transactionDirty = false;
        }

        private void RollbackTransaction()
        {
            EnsureTransactionOwner();
            _transactionEnvelope = CreateWorkingEnvelope(
                _transactionBaselineEnvelope);
            _transactionDirty = false;
        }

        private void EndTransaction(IDisposable crossProcess)
        {
            try
            {
                EnsureTransactionOwner();
                _transactionBaselineEnvelope = null;
                _transactionEnvelope = null;
                _transactionDirty = false;
                _transactionOwnerThreadId = 0;
            }
            finally
            {
                crossProcess.Dispose();
            }
        }

        private void EnsureTransactionOwner()
        {
            if (_transactionEnvelope == null ||
                _transactionOwnerThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                throw new InvalidOperationException(
                    "Approval save transaction ownership was lost.");
            }
        }

        internal void Revoke()
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            _revoked = true;
        }

#if UNITY_INCLUDE_TESTS
        internal void DeletePersistentDataForTests()
        {
            if (!IsTestRegistryLeafPathForTests(_registrySubKeyPath))
            {
                return;
            }

            using IDisposable crossProcess = AcquireCrossProcessLock();
            WindowsRegistryValueStore.DeleteAndFlush(
                _registrySubKeyPath,
                _registryValueName);
        }

        internal static bool IsTestRegistryLeafPathForTests(string subKeyPath)
        {
            if (string.IsNullOrWhiteSpace(subKeyPath))
            {
                return false;
            }

            string prefix = TestRegistrySubKeyRoot + @"\";
            if (!subKeyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string leaf = subKeyPath.Substring(prefix.Length);
            return leaf.Length == 32 &&
                   leaf.IndexOf('\\') < 0 &&
                   leaf.IndexOf('/') < 0 &&
                   Guid.TryParseExact(leaf, "N", out _);
        }
#endif

        internal bool FileExists(string name)
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            return Find(LoadLocked(), name) != null;
        }

        internal SaveFileReadResult Read(string name, int maximumBytes)
        {
            if (maximumBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            }

            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            VirtualEntry entry = Find(LoadLocked(), name);
            if (entry == null)
            {
                return new SaveFileReadResult(
                    SaveFileReadDisposition.Missing,
                    null,
                    0,
                    "SAVE_FILE_MISSING");
            }

            byte[] bytes = Decode(entry);
            if (bytes.Length > maximumBytes)
            {
                return new SaveFileReadResult(
                    SaveFileReadDisposition.Oversize,
                    null,
                    bytes.Length,
                    "SAVE_FILE_OVERSIZE");
            }

            return new SaveFileReadResult(
                SaveFileReadDisposition.Read,
                bytes,
                bytes.Length,
                string.Empty);
        }

        internal bool TryCreate(string name, byte[] bytes)
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            VirtualEnvelope envelope = LoadLocked();
            if (Find(envelope, name) != null)
            {
                return false;
            }

            envelope.entries.Add(CreateEntry(name, bytes));
            CommitLocked(envelope);
            return true;
        }

        internal void Copy(string sourceName, string destinationName, bool overwrite)
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            VirtualEnvelope envelope = LoadLocked();
            VirtualEntry source = Find(envelope, sourceName) ??
                                  throw new FileNotFoundException("Approval source is missing.");
            VirtualEntry destination = Find(envelope, destinationName);
            if (destination != null && !overwrite)
            {
                throw new IOException("Approval destination already exists.");
            }

            if (destination == null)
            {
                envelope.entries.Add(new VirtualEntry
                {
                    name = destinationName,
                    contentsBase64 = source.contentsBase64
                });
            }
            else
            {
                destination.contentsBase64 = source.contentsBase64;
            }

            CommitLocked(envelope);
        }

        internal void Move(string sourceName, string destinationName)
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            VirtualEnvelope envelope = LoadLocked();
            VirtualEntry source = Find(envelope, sourceName) ??
                                  throw new FileNotFoundException("Approval source is missing.");
            if (Find(envelope, destinationName) != null)
            {
                throw new IOException("Approval destination already exists.");
            }

            envelope.entries.Remove(source);
            source.name = destinationName;
            envelope.entries.Add(source);
            CommitLocked(envelope);
        }

        internal void Replace(string sourceName, string destinationName, string backupName)
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            VirtualEnvelope envelope = LoadLocked();
            VirtualEntry source = Find(envelope, sourceName) ??
                                  throw new FileNotFoundException("Approval replacement source is missing.");
            VirtualEntry destination = Find(envelope, destinationName) ??
                                       throw new FileNotFoundException("Approval replacement destination is missing.");
            string previousContents = destination.contentsBase64;
            string nextContents = source.contentsBase64;
            envelope.entries.Remove(source);
            destination.contentsBase64 = nextContents;
            VirtualEntry backup = Find(envelope, backupName);
            if (backup == null)
            {
                envelope.entries.Add(new VirtualEntry
                {
                    name = backupName,
                    contentsBase64 = previousContents
                });
            }
            else
            {
                backup.contentsBase64 = previousContents;
            }

            CommitLocked(envelope);
        }

        internal bool TryDelete(string path, out string failure)
        {
            try
            {
                string name = DirectChildName(path);
                using IDisposable crossProcess = AcquireCrossProcessLock();
                EnsureActiveLocked();
                VirtualEnvelope envelope = LoadLocked();
                VirtualEntry entry = Find(envelope, name);
                if (entry != null)
                {
                    envelope.entries.Remove(entry);
                    CommitLocked(envelope);
                }

                failure = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsStoreFailure(exception))
            {
                failure = "Approval virtual artifact deletion failed: " +
                          exception.GetType().Name;
                return false;
            }
        }

        internal string[] EnumerateNames(string pattern)
        {
            using IDisposable crossProcess = AcquireCrossProcessLock();
            EnsureActiveLocked();
            string regexPattern = "^" +
                                  Regex.Escape(pattern ?? string.Empty)
                                      .Replace("\\*", ".*")
                                      .Replace("\\?", ".") +
                                  "$";
            var regex = new Regex(regexPattern, RegexOptions.CultureInvariant);
            return LoadLocked().entries
                .Where(entry => entry != null && regex.IsMatch(entry.name ?? string.Empty))
                .Select(entry => entry.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        internal string DirectChildName(string path)
        {
            string normalized = NormalizePath(path);
            if (!string.Equals(
                    NormalizePath(Path.GetDirectoryName(normalized)),
                    _saveRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Approval persistence path is not a direct virtual-root child.");
            }

            string name = Path.GetFileName(normalized);
            if (!IsLegalVirtualChildName(name))
            {
                throw new IOException("Approval persistence filename is invalid.");
            }

            return name;
        }

        private static bool IsLegalVirtualChildName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                name == "." ||
                name == ".." ||
                name.EndsWith(" ", StringComparison.Ordinal) ||
                name.EndsWith(".", StringComparison.Ordinal) ||
                Path.IsPathRooted(name) ||
                !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal) ||
                name.IndexOf(':') >= 0 ||
                name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return false;
            }

            string stem = name.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL")
            {
                return false;
            }

            return !(stem.Length == 4 &&
                     (stem.StartsWith("COM", StringComparison.Ordinal) ||
                      stem.StartsWith("LPT", StringComparison.Ordinal)) &&
                     stem[3] >= '1' &&
                     stem[3] <= '9');
        }

        internal bool IsExactRoot(string path) =>
            string.Equals(NormalizePath(path), _saveRoot, StringComparison.OrdinalIgnoreCase);

        private VirtualEnvelope LoadLocked()
        {
            if (_transactionEnvelope != null)
            {
                EnsureTransactionOwner();
                return CloneEnvelope(_transactionEnvelope);
            }

            return LoadPersistedEnvelopeLocked();
        }

        private VirtualEnvelope LoadPersistedEnvelopeLocked()
        {
            if (!WindowsRegistryValueStore.TryRead(
                    _registrySubKeyPath,
                    _registryValueName,
                    out string json))
            {
                throw new IOException("Approval virtual-store envelope is missing.");
            }
            VirtualEnvelope envelope = JsonUtility.FromJson<VirtualEnvelope>(json);
            if (envelope == null ||
                envelope.version != FormatVersion ||
                !string.Equals(envelope.ownerFingerprint, _ownerFingerprint, StringComparison.Ordinal) ||
                envelope.entries == null)
            {
                throw new IOException("Approval virtual-store envelope is foreign or corrupt.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (VirtualEntry entry in envelope.entries)
            {
                if (entry == null ||
                    !IsLegalVirtualChildName(entry.name) ||
                    !names.Add(entry.name))
                {
                    throw new IOException("Approval virtual-store inventory is invalid.");
                }

                Decode(entry);
            }

            if (envelope.entries.Count > 1 ||
                (envelope.entries.Count == 1 &&
                 !string.Equals(
                     envelope.entries[0].name,
                     "save.json",
                     StringComparison.Ordinal)))
            {
                throw new IOException(
                    "Approval virtual-store persisted inventory is not canonical.");
            }

            return envelope;
        }

        private VirtualEnvelope CreateEmptyEnvelope() =>
            new VirtualEnvelope
            {
                version = FormatVersion,
                ownerFingerprint = _ownerFingerprint,
                entries = new List<VirtualEntry>()
            };

        private void CommitLocked(VirtualEnvelope envelope)
        {
            EnsureActiveLocked();
            envelope.entries = envelope.entries
                .OrderBy(entry => entry.name, StringComparer.Ordinal)
                .ToList();
            if (_transactionEnvelope != null)
            {
                EnsureTransactionOwner();
                _transactionEnvelope = CloneEnvelope(envelope);
                _transactionDirty = true;
                return;
            }

            throw new InvalidOperationException(
                "Approval virtual-store mutations require a whole-service transaction.");
        }

        private void PersistEnvelopeLocked(VirtualEnvelope envelope)
        {
            WindowsRegistryValueStore.WriteAndFlush(
                _registrySubKeyPath,
                _registryValueName,
                JsonUtility.ToJson(envelope));
            VirtualEnvelope observed = LoadPersistedEnvelopeLocked();
            if (!PersistentEnvelopeEquals(envelope, observed))
            {
                throw new IOException(
                    "Approval virtual-store readback did not match the intended envelope.");
            }
        }

        private static bool PersistentEnvelopeEquals(
            VirtualEnvelope intended,
            VirtualEnvelope observed)
        {
            if (intended == null ||
                observed == null ||
                intended.version != observed.version ||
                !string.Equals(
                    intended.ownerFingerprint,
                    observed.ownerFingerprint,
                    StringComparison.Ordinal) ||
                intended.entries == null ||
                observed.entries == null ||
                intended.entries.Count != observed.entries.Count)
            {
                return false;
            }

            for (int index = 0; index < intended.entries.Count; index++)
            {
                VirtualEntry intendedEntry = intended.entries[index];
                VirtualEntry observedEntry = observed.entries[index];
                if (intendedEntry == null ||
                    observedEntry == null ||
                    !string.Equals(
                        intendedEntry.name,
                        observedEntry.name,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        intendedEntry.contentsBase64,
                        observedEntry.contentsBase64,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static VirtualEnvelope CloneEnvelope(VirtualEnvelope source) =>
            new VirtualEnvelope
            {
                version = source.version,
                ownerFingerprint = source.ownerFingerprint,
                entries = source.entries
                    .Select(entry => new VirtualEntry
                    {
                        name = entry.name,
                        contentsBase64 = entry.contentsBase64
                    })
                    .ToList()
            };

        private VirtualEnvelope CanonicalPersistentEnvelope(VirtualEnvelope source)
        {
            VirtualEntry primary = Find(source, "save.json");
            VirtualEnvelope canonical = CreateEmptyEnvelope();
            if (primary != null)
            {
                canonical.entries.Add(new VirtualEntry
                {
                    name = "save.json",
                    contentsBase64 = primary.contentsBase64
                });
            }

            return canonical;
        }

        private static VirtualEnvelope CreateWorkingEnvelope(VirtualEnvelope canonical)
        {
            VirtualEnvelope working = CloneEnvelope(canonical);
            VirtualEntry primary = Find(working, "save.json");
            if (primary != null)
            {
                working.entries.Add(new VirtualEntry
                {
                    name = "save.backup.json",
                    contentsBase64 = primary.contentsBase64
                });
            }

            return working;
        }

        private static bool CanonicalPrimaryEquals(
            VirtualEnvelope left,
            VirtualEnvelope right)
        {
            VirtualEntry leftPrimary = Find(left, "save.json");
            VirtualEntry rightPrimary = Find(right, "save.json");
            return string.Equals(
                leftPrimary?.contentsBase64,
                rightPrimary?.contentsBase64,
                StringComparison.Ordinal);
        }

        private bool HasEnvelopeLocked()
        {
            return WindowsRegistryValueStore.Exists(
                _registrySubKeyPath,
                _registryValueName);
        }

        private static string ResolveRegistrySubKeyPath()
        {
#if UNITY_INCLUDE_TESTS
            string overridePath = RegistrySubKeyPathOverrideForTests;
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                if (!IsTestRegistryLeafPathForTests(overridePath))
                {
                    throw new ArgumentException(
                        "Approval registry override requires one exact GUID test leaf.",
                        nameof(RegistrySubKeyPathOverrideForTests));
                }

                return overridePath;
            }
#endif
            return RegistrySubKeyPath;
        }

        private IDisposable AcquireCrossProcessLock()
        {
            return WindowsNamedMutex.Acquire(_mutexName, _userSid, 5000);
        }

        private void EnsureActiveLocked()
        {
            if (_revoked)
            {
                throw new InvalidOperationException("The approval virtual store has been revoked.");
            }
        }

        private static VirtualEntry Find(VirtualEnvelope envelope, string name) =>
            envelope.entries.FirstOrDefault(
                entry => entry != null &&
                         string.Equals(entry.name, name, StringComparison.Ordinal));

        private static VirtualEntry CreateEntry(string name, byte[] bytes) =>
            new VirtualEntry
            {
                name = name,
                contentsBase64 = Convert.ToBase64String(bytes ?? Array.Empty<byte>())
            };

        private static byte[] Decode(VirtualEntry entry)
        {
            try
            {
                return Convert.FromBase64String(entry.contentsBase64 ?? string.Empty);
            }
            catch (FormatException exception)
            {
                throw new IOException("Approval virtual-store contents are invalid.", exception);
            }
        }

        private static string Fingerprint(string normalRoot)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                NormalizePath(normalRoot).ToUpperInvariant());
            using SHA256 sha256 = SHA256.Create();
            byte[] digest = sha256.ComputeHash(bytes);
            var result = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                result.Append(value.ToString("x2"));
            }
            return result.ToString();
        }

        private static string NormalizePath(string path) =>
            Path.GetFullPath(path ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        internal static bool IsStoreFailure(Exception exception) =>
            exception is IOException ||
            exception is InvalidOperationException ||
            exception is ArgumentException ||
            exception is UnauthorizedAccessException ||
            exception is System.Security.SecurityException ||
            exception is PlatformNotSupportedException ||
            exception is DllNotFoundException ||
            exception is EntryPointNotFoundException ||
            exception is BadImageFormatException;

        [Serializable]
        private sealed class VirtualEnvelope
        {
            public int version;
            public string ownerFingerprint;
            public List<VirtualEntry> entries;
        }

        [Serializable]
        private sealed class VirtualEntry
        {
            public string name;
            public string contentsBase64;
        }

        internal sealed class ApprovalTransaction : IDisposable
        {
            private MvpApprovalVirtualStore _owner;
            private IDisposable _crossProcess;

            internal ApprovalTransaction(
                MvpApprovalVirtualStore owner,
                IDisposable crossProcess)
            {
                _owner = owner;
                _crossProcess = crossProcess;
            }

            internal void Commit()
            {
                if (_owner == null)
                {
                    throw new ObjectDisposedException(nameof(ApprovalTransaction));
                }

                _owner.CommitTransaction();
            }

            internal void Rollback()
            {
                if (_owner == null)
                {
                    throw new ObjectDisposedException(nameof(ApprovalTransaction));
                }

                _owner.RollbackTransaction();
            }

            public void Dispose()
            {
                MvpApprovalVirtualStore owner = _owner;
                IDisposable crossProcess = _crossProcess;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                _crossProcess = null;
                owner.EndTransaction(crossProcess);
            }
        }

        private sealed class OperationLease : IDisposable
        {
            private IDisposable _crossProcess;
            private bool _disposed;

            internal OperationLease(IDisposable crossProcess)
            {
                _crossProcess = crossProcess;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _crossProcess.Dispose();
                _crossProcess = null;
            }
        }
    }

    internal static class WindowsNamedMutex
    {
        private const uint TokenQuery = 0x0008;
        private const int TokenUser = 1;
        private const uint SecurityDescriptorRevision = 1;
        private const uint MutexAllAccess = 0x001F0001;
        private const uint WaitObject0 = 0x00000000;
        private const uint WaitAbandoned = 0x00000080;
        private const uint WaitTimeout = 0x00000102;
        private const uint WaitFailed = 0xFFFFFFFF;

#if UNITY_INCLUDE_TESTS
        internal static Func<string> CurrentUserSidOverrideForTests;
        internal static Func<IntPtr, uint, uint> WaitOverrideForTests;
        internal static Action<IntPtr> CloseHandleObserverForTests;
#endif

        internal static string GetCurrentUserSid()
        {
#if UNITY_INCLUDE_TESTS
            if (CurrentUserSidOverrideForTests != null)
            {
                return CurrentUserSidOverrideForTests();
            }
#endif
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out IntPtr token))
            {
                throw NativeFailure("open process token");
            }

            try
            {
                GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out uint required);
                if (required == 0)
                {
                    throw NativeFailure("query token user size");
                }

                IntPtr buffer = Marshal.AllocHGlobal(checked((int)required));
                try
                {
                    if (!GetTokenInformation(
                            token,
                            TokenUser,
                            buffer,
                            required,
                            out _))
                    {
                        throw NativeFailure("query token user");
                    }

                    IntPtr sid = Marshal.ReadIntPtr(buffer);
                    if (!ConvertSidToStringSid(sid, out IntPtr sidString))
                    {
                        throw NativeFailure("format token SID");
                    }

                    try
                    {
                        return Marshal.PtrToStringUni(sidString) ??
                               throw new IOException("Approval mutex user SID is empty.");
                    }
                    finally
                    {
                        LocalFree(sidString);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        internal static IDisposable Acquire(
            string name,
            string userSid,
            uint timeoutMilliseconds)
        {
            string sddl = "D:P(A;;GA;;;" + userSid + ")" +
                          "(A;;GA;;;SY)(A;;GA;;;BA)";
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor(
                    sddl,
                    SecurityDescriptorRevision,
                    out IntPtr descriptor,
                    out _))
            {
                throw NativeFailure("build mutex security descriptor");
            }

            IntPtr handle;
            try
            {
                var attributes = new SecurityAttributes
                {
                    Length = checked((uint)Marshal.SizeOf<SecurityAttributes>()),
                    SecurityDescriptor = descriptor,
                    InheritHandle = 0
                };
                handle = CreateMutexEx(
                    ref attributes,
                    name,
                    0,
                    MutexAllAccess);
            }
            finally
            {
                LocalFree(descriptor);
            }

            if (handle == IntPtr.Zero)
            {
                throw NativeFailure("create or open approval mutex");
            }

            uint wait;
            try
            {
#if UNITY_INCLUDE_TESTS
                wait = WaitOverrideForTests != null
                    ? WaitOverrideForTests(handle, timeoutMilliseconds)
                    : WaitForSingleObject(handle, timeoutMilliseconds);
#else
                wait = WaitForSingleObject(handle, timeoutMilliseconds);
#endif
            }
            catch
            {
                CloseMutexHandle(handle);
                throw;
            }
            if (wait == WaitObject0)
            {
                return new Lease(handle);
            }

            if (wait == WaitAbandoned)
            {
                int releaseError = 0;
                try
                {
                    if (!ReleaseMutex(handle))
                    {
                        releaseError = Marshal.GetLastWin32Error();
                    }
                }
                finally
                {
                    CloseMutexHandle(handle);
                }

                throw new IOException(
                    "Approval virtual-store mutex ownership was abandoned; " +
                    "registry durability is uncertain" +
                    (releaseError == 0
                        ? "."
                        : " and release failed with Win32 " + releaseError + "."));
            }

            int error = wait == WaitFailed ? Marshal.GetLastWin32Error() : 0;
            CloseMutexHandle(handle);
            if (wait == WaitTimeout)
            {
                throw new IOException("Approval virtual-store mutex timed out.");
            }

            throw new IOException(
                "Approval virtual-store mutex wait failed with Win32 " + error + ".");
        }

        private static void CloseMutexHandle(IntPtr handle)
        {
#if UNITY_INCLUDE_TESTS
            CloseHandleObserverForTests?.Invoke(handle);
#endif
            CloseHandle(handle);
        }

        private static IOException NativeFailure(string operation) =>
            new IOException(
                "Approval virtual-store mutex " + operation +
                " failed with Win32 " + Marshal.GetLastWin32Error() + ".");

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            internal uint Length;
            internal IntPtr SecurityDescriptor;
            internal int InheritHandle;
        }

        private sealed class Lease : IDisposable
        {
            private IntPtr _handle;

            internal Lease(IntPtr handle)
            {
                _handle = handle;
            }

            public void Dispose()
            {
                IntPtr handle = _handle;
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                _handle = IntPtr.Zero;
                try
                {
                    if (!ReleaseMutex(handle))
                    {
                        throw NativeFailure("release");
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(
            IntPtr process,
            uint desiredAccess,
            out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            IntPtr token,
            int informationClass,
            IntPtr information,
            uint informationLength,
            out uint returnLength);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertSidToStringSid(
            IntPtr sid,
            out IntPtr stringSid);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string securityDescriptor,
            uint stringRevision,
            out IntPtr descriptor,
            out uint descriptorSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateMutexEx(
            ref SecurityAttributes attributes,
            string name,
            uint flags,
            uint desiredAccess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(
            IntPtr handle,
            uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReleaseMutex(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }

    internal static class WindowsRegistryValueStore
    {
        private const string RegistryMutationMutexPrefix =
            "Global\\AnotherLife.MvpApprovalVfsV1.Registry.";
        private const uint RegistryMutationTimeoutMilliseconds = 5000;
        private const int ErrorSuccess = 0;
        private const int ErrorFileNotFound = 2;
        private const int ErrorAccessDenied = 5;
        private const int ErrorMoreData = 234;
        private const int ErrorNoMoreItems = 259;
        private const uint KeyQueryValue = 0x0001;
        private const uint KeySetValue = 0x0002;
        private const uint RegOptionNonVolatile = 0;
        private const uint RegSz = 1;

#if UNITY_INCLUDE_TESTS
        internal static Func<IntPtr, int> FlushOverrideForTests;
        internal static Func<IntPtr, string, int> DeleteKeyOverrideForTests;
#endif
        private const uint MaximumEnvelopeBytes = 32u * 1024u * 1024u;
        private static readonly IntPtr HKeyCurrentUser =
            new IntPtr(unchecked((int)0x80000001));

        internal static bool Exists(string subKeyPath, string valueName) =>
            TryRead(subKeyPath, valueName, out _);

        internal static bool TryRead(
            string subKeyPath,
            string valueName,
            out string value)
        {
            EnsureWindows();
            value = string.Empty;
            int opened = RegOpenKeyEx(
                HKeyCurrentUser,
                subKeyPath,
                0,
                KeyQueryValue,
                out IntPtr key);
            if (opened == ErrorFileNotFound)
            {
                return false;
            }
            ThrowOnError(opened, "open");

            try
            {
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    uint size = 0;
                    int queried = RegQueryValueEx(
                        key,
                        valueName,
                        IntPtr.Zero,
                        out uint valueType,
                        null,
                        ref size);
                    if (queried == ErrorFileNotFound)
                    {
                        return false;
                    }
                    if (queried != ErrorSuccess && queried != ErrorMoreData)
                    {
                        ThrowOnError(queried, "query-size");
                    }
                    if (valueType != RegSz)
                    {
                        throw new IOException("Approval registry envelope type is invalid.");
                    }
                    if (size > MaximumEnvelopeBytes)
                    {
                        throw new IOException("Approval registry envelope is oversized.");
                    }

                    byte[] bytes = new byte[size];
                    queried = RegQueryValueEx(
                        key,
                        valueName,
                        IntPtr.Zero,
                        out valueType,
                        bytes,
                        ref size);
                    if (queried == ErrorMoreData)
                    {
                        continue;
                    }
                    if (queried == ErrorFileNotFound)
                    {
                        return false;
                    }
                    ThrowOnError(queried, "query");
                    int observedSize = checked((int)size);
                    if (valueType != RegSz ||
                        observedSize < 2 ||
                        observedSize > bytes.Length ||
                        (observedSize & 1) != 0 ||
                        bytes[observedSize - 2] != 0 ||
                        bytes[observedSize - 1] != 0)
                    {
                        throw new IOException("Approval registry envelope encoding is invalid.");
                    }

                    for (int index = 0; index < observedSize - 2; index += 2)
                    {
                        if (bytes[index] == 0 && bytes[index + 1] == 0)
                        {
                            throw new IOException(
                                "Approval registry envelope contains an embedded terminator.");
                        }
                    }

                    value = Encoding.Unicode.GetString(
                        bytes,
                        0,
                        observedSize - 2);
                    return true;
                }

                throw new IOException("Approval registry envelope changed during read.");
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        internal static void WriteAndFlush(
            string subKeyPath,
            string valueName,
            string value)
        {
            EnsureWindows();
            using IDisposable mutationLock = AcquireMutationLock();
            int created = RegCreateKeyEx(
                HKeyCurrentUser,
                subKeyPath,
                0,
                null,
                RegOptionNonVolatile,
                KeyQueryValue | KeySetValue,
                IntPtr.Zero,
                out IntPtr key,
                out _);
            ThrowOnError(created, "create");

            try
            {
                byte[] bytes = Encoding.Unicode.GetBytes((value ?? string.Empty) + "\0");
                if (bytes.Length > MaximumEnvelopeBytes)
                {
                    throw new IOException("Approval registry envelope is oversized.");
                }

                ThrowOnError(
                    RegSetValueEx(
                        key,
                        valueName,
                        0,
                        RegSz,
                        bytes,
                        checked((uint)bytes.Length)),
                    "write");
#if UNITY_INCLUDE_TESTS
                int flushResult = FlushOverrideForTests != null
                    ? FlushOverrideForTests(key)
                    : RegFlushKey(key);
#else
                int flushResult = RegFlushKey(key);
#endif
                if (flushResult != ErrorSuccess)
                {
                    bool changed = TryRead(
                        subKeyPath,
                        valueName,
                        out string observed) &&
                        string.Equals(observed, value ?? string.Empty, StringComparison.Ordinal);
                    throw new ApprovalRegistryCommitUncertainException(
                        changed,
                        flushResult);
                }
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        internal static void DeleteAndFlush(string subKeyPath, string valueName)
        {
            EnsureWindows();
            using IDisposable mutationLock = AcquireMutationLock();
            int opened = RegOpenKeyEx(
                HKeyCurrentUser,
                subKeyPath,
                0,
                KeyQueryValue | KeySetValue,
                out IntPtr key);
            if (opened == ErrorFileNotFound)
            {
                return;
            }
            ThrowOnError(opened, "open-delete");

            bool hasRemainingValues;
            try
            {
                int deleted = RegDeleteValue(key, valueName);
                if (deleted != ErrorFileNotFound)
                {
                    ThrowOnError(deleted, "delete");
                    ThrowOnError(RegFlushKey(key), "flush-delete");
                }

                hasRemainingValues = QueryOpenKeyHasValues(key);
            }
            finally
            {
                RegCloseKey(key);
            }

            if (hasRemainingValues)
            {
                return;
            }

            int deletedKey;
#if UNITY_INCLUDE_TESTS
            deletedKey = DeleteKeyOverrideForTests != null
                ? DeleteKeyOverrideForTests(HKeyCurrentUser, subKeyPath)
                : RegDeleteKey(HKeyCurrentUser, subKeyPath);
#else
            deletedKey = RegDeleteKey(HKeyCurrentUser, subKeyPath);
#endif
            if (deletedKey != ErrorSuccess &&
                deletedKey != ErrorFileNotFound)
            {
                if (deletedKey == ErrorAccessDenied &&
                    TryQueryKeyHasValues(subKeyPath, out bool hasValues) &&
                    hasValues)
                {
                    return;
                }

                ThrowOnError(deletedKey, "delete-empty-key");
            }
        }

#if UNITY_INCLUDE_TESTS
        internal static void DeleteTestSubKeyAndFlush(string subKeyPath)
        {
            if (!MvpApprovalVirtualStore.IsTestRegistryLeafPathForTests(subKeyPath))
            {
                throw new ArgumentException(
                    "Approval test cleanup requires one exact unique test leaf.",
                    nameof(subKeyPath));
            }

            EnsureWindows();
            using IDisposable mutationLock = AcquireMutationLock();
            int opened = RegOpenKeyEx(
                HKeyCurrentUser,
                subKeyPath,
                0,
                KeyQueryValue | KeySetValue,
                out IntPtr key);
            if (opened == ErrorFileNotFound)
            {
                return;
            }
            ThrowOnError(opened, "open-test-cleanup");

            try
            {
                for (int count = 0; count < 128; count++)
                {
                    uint nameLength = 1024;
                    var valueName = new StringBuilder(1024);
                    int enumerated = RegEnumValue(
                        key,
                        0,
                        valueName,
                        ref nameLength,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        null,
                        IntPtr.Zero);
                    if (enumerated == ErrorNoMoreItems)
                    {
                        ThrowOnError(RegFlushKey(key), "flush-test-cleanup");
                        break;
                    }
                    ThrowOnError(enumerated, "enumerate-test-cleanup");
                    ThrowOnError(
                        RegDeleteValue(key, valueName.ToString()),
                        "delete-test-value");
                    if (count == 127)
                    {
                        throw new IOException(
                            "Approval test registry cleanup exceeded its value bound.");
                    }
                }
            }
            finally
            {
                RegCloseKey(key);
            }

            int deletedKey = RegDeleteKey(HKeyCurrentUser, subKeyPath);
            if (deletedKey != ErrorSuccess && deletedKey != ErrorFileNotFound)
            {
                ThrowOnError(deletedKey, "delete-test-key");
            }
        }
#endif

        private static IDisposable AcquireMutationLock()
        {
            string userSid = WindowsNamedMutex.GetCurrentUserSid();
            return WindowsNamedMutex.Acquire(
                RegistryMutationMutexPrefix + userSid,
                userSid,
                RegistryMutationTimeoutMilliseconds);
        }

        private static bool QueryOpenKeyHasValues(IntPtr key)
        {
            uint nameLength = 1;
            var valueName = new StringBuilder(1);
            int enumerated = RegEnumValue(
                key,
                0,
                valueName,
                ref nameLength,
                IntPtr.Zero,
                IntPtr.Zero,
                null,
                IntPtr.Zero);
            if (enumerated == ErrorNoMoreItems)
            {
                return false;
            }
            if (enumerated == ErrorSuccess || enumerated == ErrorMoreData)
            {
                return true;
            }

            ThrowOnError(enumerated, "enumerate-before-delete-key");
            return true;
        }

        private static bool TryQueryKeyHasValues(
            string subKeyPath,
            out bool hasValues)
        {
            hasValues = false;
            int opened = RegOpenKeyEx(
                HKeyCurrentUser,
                subKeyPath,
                0,
                KeyQueryValue,
                out IntPtr key);
            if (opened == ErrorFileNotFound)
            {
                return false;
            }
            ThrowOnError(opened, "open-after-delete-key");

            try
            {
                uint nameLength = 1;
                var valueName = new StringBuilder(1);
                int enumerated = RegEnumValue(
                    key,
                    0,
                    valueName,
                    ref nameLength,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    null,
                    IntPtr.Zero);
                if (enumerated == ErrorNoMoreItems)
                {
                    return true;
                }
                if (enumerated == ErrorSuccess || enumerated == ErrorMoreData)
                {
                    hasValues = true;
                    return true;
                }

                ThrowOnError(enumerated, "enumerate-after-delete-key");
                return false;
            }
            finally
            {
                RegCloseKey(key);
            }
        }

        internal static void EnsureWindows()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer)
            {
                throw new PlatformNotSupportedException(
                    "The MVP approval virtual store is Windows-only.");
            }
        }

        private static void ThrowOnError(int error, string operation)
        {
            if (error != ErrorSuccess)
            {
                throw new IOException(
                    "Approval registry " + operation + " failed with Win32 " + error + ".");
            }
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegOpenKeyEx(
            IntPtr key,
            string subKey,
            uint options,
            uint desiredAccess,
            out IntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegCreateKeyEx(
            IntPtr key,
            string subKey,
            uint reserved,
            string valueClass,
            uint options,
            uint desiredAccess,
            IntPtr securityAttributes,
            out IntPtr result,
            out uint disposition);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegQueryValueEx(
            IntPtr key,
            string valueName,
            IntPtr reserved,
            out uint valueType,
            [Out] byte[] data,
            ref uint dataSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegSetValueEx(
            IntPtr key,
            string valueName,
            uint reserved,
            uint valueType,
            byte[] data,
            uint dataSize);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegDeleteValue(IntPtr key, string valueName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegDeleteKey(IntPtr key, string subKey);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int RegEnumValue(
            IntPtr key,
            uint index,
            StringBuilder valueName,
            ref uint valueNameLength,
            IntPtr reserved,
            IntPtr valueType,
            [Out] byte[] data,
            IntPtr dataLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegFlushKey(IntPtr key);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr key);
    }

    internal sealed class ApprovalRegistryCommitUncertainException : IOException
    {
        internal ApprovalRegistryCommitUncertainException(
            bool changed,
            int win32Error)
            : base(
                "Approval registry commit could not be durably flushed; " +
                "changed=" + changed +
                ", win32=" + win32Error + ".")
        {
            Changed = changed;
            Win32Error = win32Error;
        }

        internal bool Changed { get; }
        internal int Win32Error { get; }
    }

    internal sealed class MvpApprovalSaveFileOperations : ISaveFileOperations
    {
        private readonly string _saveRoot;
        private readonly MvpApprovalVirtualStore _store;

        internal MvpApprovalSaveFileOperations(
            string saveRoot,
            MvpApprovalVirtualStore store)
        {
            _saveRoot = Path.GetFullPath(saveRoot ?? throw new ArgumentNullException(nameof(saveRoot)))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool FileExists(string path) =>
            _store.FileExists(_store.DirectChildName(path));

        public void CreateDirectory(string path)
        {
            if (!_store.IsExactRoot(path))
            {
                throw new IOException("Approval persistence may create only its exact virtual root.");
            }
        }

        public SaveFileReadResult ReadAllBytesBounded(string path, int maximumBytes) =>
            _store.Read(_store.DirectChildName(path), maximumBytes);

        public SaveFileWriteResult WriteAllTextDurable(string path, string contents)
        {
            try
            {
                bool created = _store.TryCreate(
                    _store.DirectChildName(path),
                    new UTF8Encoding(false).GetBytes(contents ?? string.Empty));
                return created
                    ? new SaveFileWriteResult(true, true, string.Empty)
                    : new SaveFileWriteResult(false, false, "SAVE_FILE_WRITE_FAILED");
            }
            catch (Exception exception) when (MvpApprovalVirtualStore.IsStoreFailure(exception))
            {
                return new SaveFileWriteResult(false, false, "SAVE_FILE_WRITE_FAILED");
            }
        }

        public void Copy(string sourcePath, string destinationPath, bool overwrite) =>
            _store.Copy(
                _store.DirectChildName(sourcePath),
                _store.DirectChildName(destinationPath),
                overwrite);

        public void Move(string sourcePath, string destinationPath) =>
            _store.Move(
                _store.DirectChildName(sourcePath),
                _store.DirectChildName(destinationPath));

        public void Replace(string sourcePath, string destinationPath, string backupPath) =>
            _store.Replace(
                _store.DirectChildName(sourcePath),
                _store.DirectChildName(destinationPath),
                _store.DirectChildName(backupPath));

        public void Delete(string path)
        {
            if (!_store.TryDelete(path, out string failure))
            {
                throw new IOException(failure);
            }
        }

        public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern)
        {
            if (!_store.IsExactRoot(directoryPath))
            {
                throw new IOException("Approval persistence may enumerate only its exact virtual root.");
            }

            return _store.EnumerateNames(searchPattern)
                .Select(name => Path.Combine(_saveRoot, name))
                .ToArray();
        }

        public DateTime GetCreationTimeUtc(string path)
        {
            _store.DirectChildName(path);
            return DateTime.MinValue;
        }

        public bool IsReparsePoint(string path)
        {
            _store.DirectChildName(path);
            return false;
        }
    }
}
