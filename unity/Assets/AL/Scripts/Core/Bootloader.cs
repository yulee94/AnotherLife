using UnityEngine;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AL.Core
{
    public class Bootloader : MonoBehaviour
    {
        private const int OfflineStackVersion = 1;
        internal static Func<BootloaderInitializationResult> PostInstallValidationOverride;
        internal static Func<Type, object, bool> PublishBeforeRegisterOverride;

        [SerializeField] private bool _autoLoadOnStart = true;

        private readonly string _runtimeOwnerId = Guid.NewGuid().ToString("N");
        private BootloaderInitializationResult _initializationResult = BootloaderInitializationResult.NotStarted();
        private bool _runtimeActive;
        private bool _runtimeDriftReported;
        private bool _standbyForOwnership;
        private IOfflineServiceStackMarker _ownedMarker;

        private void Awake()
        {
            _initializationResult = InitializeIfMissing();
            _runtimeActive = _initializationResult.Succeeded;

            if (!_runtimeActive)
            {
                enabled = false;
                return;
            }

            var markerValidation = BootloaderInitializationResult.NotStarted();
            if (!TryGetValidatedMarker(out var marker, out markerValidation))
            {
                if (markerValidation.State != BootloaderInitializationState.NotStarted)
                {
                    _initializationResult = markerValidation;
                    _runtimeActive = false;
                    enabled = false;
                    LogInitializationResult(_initializationResult);
                }

                return;
            }

            if (!marker.TryClaimRuntimeOwner(_runtimeOwnerId))
            {
                // Reverse-ordering handoff: an incoming Bootloader can awaken before the outgoing
                // owner's OnDestroy releases the claim. Do not disable; stand by and reclaim ownership
                // from Update once the previous owner releases it. Claim-first-wins stays intact and no
                // liveness/takeover heuristic is used.
                _initializationResult = BootloaderInitializationResult.RuntimeOwnerRejected(marker.RegistrationId);
                _standbyForOwnership = true;
                _runtimeActive = false;
                LogInitializationResult(_initializationResult);
                return;
            }

            ActivateAsRuntimeOwner(marker);
        }

        private void ActivateAsRuntimeOwner(IOfflineServiceStackMarker marker)
        {
            _ownedMarker = marker;
            _runtimeActive = true;

            if (_autoLoadOnStart)
            {
                RunAutoLoad(marker);
            }
        }

        private void TryActivateFromStandby()
        {
            if (!TryGetValidatedMarker(out var marker, out _))
            {
                // No consistent complete stack to own yet; keep standing by silently so a transient
                // or inconsistent registry does not spam drift diagnostics.
                return;
            }

            if (!marker.TryClaimRuntimeOwner(_runtimeOwnerId))
            {
                // The previous owner is still alive; keep standing by without re-logging the rejection.
                return;
            }

            _standbyForOwnership = false;
            _initializationResult = BootloaderInitializationResult.ReusedCompleteStack(marker.RegistrationId);
            ActivateAsRuntimeOwner(marker);
        }

        private void RunAutoLoad(IOfflineServiceStackMarker marker)
        {
            if (marker.LoadState == OfflineStackLoadState.Succeeded)
            {
                // A prior owner in this session already loaded offline progress for this stack;
                // reuse it without loading again so repeated startup cannot double-apply progress.
                return;
            }

            if (!marker.TryBeginLoad(_runtimeOwnerId))
            {
                if (marker.LoadState == OfflineStackLoadState.InProgress)
                {
                    _initializationResult = BootloaderInitializationResult.LoadInProgressConflict(marker.RegistrationId);
                    _runtimeActive = false;
                    enabled = false;
                    ReleaseRuntimeOwnership();
                    LogInitializationResult(_initializationResult);
                }

                return;
            }

            if (!marker.TryGetExpected<ISaveGameService>(out var saveGameService))
            {
                marker.MarkLoadFailed(_runtimeOwnerId);
                _initializationResult = BootloaderInitializationResult.FailedLoad("Expected save service was not available for load.");
                _runtimeActive = false;
                enabled = false;
                ReleaseRuntimeOwnership();
                LogInitializationResult(_initializationResult);
                return;
            }

            try
            {
                saveGameService.Load();
                bool approvedLoad = IsApprovedLoadSuccess(saveGameService);
                bool markerStillValid = TryGetValidatedMarker(out var postLoadMarker, out var postLoadValidation);
                if (!approvedLoad ||
                    !markerStillValid ||
                    !ReferenceEquals(marker, postLoadMarker) ||
                    !postLoadMarker.TryGetExpected<ISaveGameService>(out var postLoadSave) ||
                    !ReferenceEquals(saveGameService, postLoadSave))
                {
                    marker.MarkLoadFailed(_runtimeOwnerId);
                    string message = !postLoadValidation.Succeeded && postLoadValidation.State != BootloaderInitializationState.NotStarted
                        ? postLoadValidation.Message
                        : $"Load status {saveGameService.LastLoadStatus}; current save present: {saveGameService.CurrentSave != null}.";
                    _initializationResult = BootloaderInitializationResult.FailedLoad(message);
                    _runtimeActive = false;
                    enabled = false;
                    ReleaseRuntimeOwnership();
                    LogInitializationResult(_initializationResult);
                    return;
                }

                ReconcileCompletedConstruction(postLoadMarker);
                marker.MarkLoadSucceeded(_runtimeOwnerId);
                OfflineKingdomProductionCatchUp.TryApplyAfterLoad(saveGameService);
            }
            catch (Exception ex)
            {
                marker.MarkLoadFailed(_runtimeOwnerId);
                _initializationResult = BootloaderInitializationResult.FailedLoad(ex.Message);
                _runtimeActive = false;
                enabled = false;
                ReleaseRuntimeOwnership();
                LogInitializationResult(_initializationResult);
            }
        }

        private void OnDestroy()
        {
            // Release the single-owner claim so a later Bootloader (for example after a scene
            // transition destroys this owner) can claim ownership and continue runtime work.
            ReleaseRuntimeOwnership();
        }

        private void ReleaseRuntimeOwnership()
        {
            if (_ownedMarker == null)
            {
                return;
            }

            _ownedMarker.TryReleaseRuntimeOwner(_runtimeOwnerId);
            _ownedMarker = null;
        }

        public static BootloaderInitializationResult InitializeIfMissing()
        {
            var markerResult = TryValidateExistingMarker();
            if (markerResult.State == BootloaderInitializationState.ReusedCompleteStack ||
                markerResult.State == BootloaderInitializationState.FailedInconsistentMarker)
            {
                LogInitializationResult(markerResult);
                return markerResult;
            }

            if (ServiceLocator.ContainsAny(OfflineServiceStack.RequiredServiceTypes))
            {
                var partialResult = BootloaderInitializationResult.FailedPartialRegistry(
                    GetPresentRequiredTypes(),
                    GetMissingRequiredTypes());
                LogInitializationResult(partialResult);
                return partialResult;
            }

            OfflineServiceStack stack;
            try
            {
                stack = OfflineServiceStack.Create(OfflineStackVersion);
            }
            catch (Exception ex)
            {
                var constructionResult = BootloaderInitializationResult.FailedConstruction(ex.Message);
                LogInitializationResult(constructionResult);
                return constructionResult;
            }

            var publicationResult = ServiceLocator.PublishBatch(stack.Registrations, PublishBeforeRegisterOverride);
            if (!publicationResult.Succeeded)
            {
                var failedPublicationResult = BootloaderInitializationResult.FailedPublication(
                    publicationResult.ServiceType,
                    publicationResult.Message);
                LogInitializationResult(failedPublicationResult);
                return failedPublicationResult;
            }

            var installedResult = PostInstallValidationOverride != null
                ? PostInstallValidationOverride()
                : TryValidateExistingMarker();
            var result = installedResult.State == BootloaderInitializationState.ReusedCompleteStack
                ? BootloaderInitializationResult.CreatedCompleteStack(stack.Marker.RegistrationId)
                : installedResult;

            if (!result.Succeeded)
            {
                publicationResult.Transaction?.Rollback();
                result = BootloaderInitializationResult.FailedPublication(
                    typeof(IOfflineServiceStackMarker),
                    $"Post-install verification failed and the attempted stack was rolled back. {installedResult.Message}");
            }
            else
            {
                publicationResult.Transaction?.Commit();
            }

            LogInitializationResult(result);
            return result;
        }

        private static BootloaderInitializationResult TryValidateExistingMarker()
        {
            if (!ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker))
            {
                return BootloaderInitializationResult.NotStarted();
            }

            if (marker.StackVersion != OfflineStackVersion)
            {
                return BootloaderInitializationResult.FailedInconsistentMarker(
                    marker.RegistrationId,
                    missingTypes: Array.Empty<Type>(),
                    mismatchedTypes: Array.Empty<Type>(),
                    unexpectedTypes: Array.Empty<Type>(),
                    invalidTypes: new[] { typeof(IOfflineServiceStackMarker) },
                    message: $"Unsupported offline stack marker version {marker.StackVersion}.");
            }

            var missing = new List<Type>();
            var mismatched = new List<Type>();
            var unexpected = new List<Type>();
            var invalid = new List<Type>();

            if (string.IsNullOrWhiteSpace(marker.RegistrationId))
            {
                invalid.Add(typeof(IOfflineServiceStackMarker));
            }

            if (marker.ExpectedInstances == null)
            {
                invalid.Add(typeof(IOfflineServiceStackMarker));
                return BootloaderInitializationResult.FailedInconsistentMarker(
                    marker.RegistrationId,
                    missingTypes: missing.ToArray(),
                    mismatchedTypes: mismatched.ToArray(),
                    unexpectedTypes: unexpected.ToArray(),
                    invalidTypes: invalid.ToArray(),
                    message: "Offline service stack marker has no expected-instance inventory.");
            }

            foreach (var key in marker.ExpectedInstances.Keys.Where(type => type != null))
            {
                if (!OfflineServiceStack.RequiredServiceTypes.Contains(key))
                {
                    unexpected.Add(key);
                }
            }

            foreach (var serviceType in OfflineServiceStack.RequiredServiceTypes)
            {
                if (!ServiceLocator.TryGet(serviceType, out var current))
                {
                    missing.Add(serviceType);
                    continue;
                }

                if (!marker.ExpectedInstances.TryGetValue(serviceType, out var expected) ||
                    expected == null ||
                    !serviceType.IsInstanceOfType(expected) ||
                    !ReferenceEquals(current, expected))
                {
                    mismatched.Add(serviceType);
                }
            }

            if (!marker.ExpectedInstances.TryGetValue(typeof(ISaveGameService), out var expectedSave) ||
                !marker.ExpectedInstances.TryGetValue(typeof(IGameDataService), out var expectedGameData) ||
                !ReferenceEquals(marker.SaveRoot, expectedSave) ||
                !ReferenceEquals(marker.GameDataRoot, expectedGameData))
            {
                invalid.Add(typeof(IOfflineServiceStackMarker));
            }

            if (missing.Count > 0 || mismatched.Count > 0 || unexpected.Count > 0 || invalid.Count > 0)
            {
                return BootloaderInitializationResult.FailedInconsistentMarker(
                    marker.RegistrationId,
                    missingTypes: missing.ToArray(),
                    mismatchedTypes: mismatched.ToArray(),
                    unexpectedTypes: unexpected.ToArray(),
                    invalidTypes: invalid.ToArray(),
                    message: "Offline service stack marker no longer matches registered services.");
            }

            return BootloaderInitializationResult.ReusedCompleteStack(marker.RegistrationId);
        }

        private static bool TryGetValidatedMarker(
            out IOfflineServiceStackMarker marker,
            out BootloaderInitializationResult validation)
        {
            validation = TryValidateExistingMarker();
            if (!validation.Succeeded ||
                !ServiceLocator.TryGet<IOfflineServiceStackMarker>(out marker))
            {
                marker = null;
                return false;
            }

            return true;
        }

        private void Update()
        {
            if (_standbyForOwnership)
            {
                // Not the runtime owner yet: attempt to reclaim before any tick/drift/save work.
                TryActivateFromStandby();
                return;
            }

            if (!_runtimeActive)
            {
                return;
            }

            if (!TryGetValidatedMarker(out var marker, out var validation))
            {
                if (!_runtimeDriftReported)
                {
                    _runtimeDriftReported = true;
                    Debug.LogError(BootloaderInitializationResult.RuntimeDrift(validation.Message).Message);
                }

                // The runtime claim is deliberately NOT released here: an inconsistent or replaced
                // marker already blocks any other Bootloader from validating and reusing this stack,
                // and the claim is released on OnDestroy (reviewer m3 asymmetry note).
                _runtimeActive = false;
                enabled = false;
                return;
            }

            if (!marker.IsRuntimeOwner(_runtimeOwnerId))
            {
                _runtimeActive = false;
                enabled = false;
                return;
            }

            if (marker.TryGetExpected<IResourceService>(out var resourceService))
            {
                resourceService.TickProduction(Time.deltaTime);
            }

            if (marker.TryGetExpected<IBuildingService>(out var buildingService))
            {
                buildingService.ReconcileCompletedConstructions(
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
        }

        private static void ReconcileCompletedConstruction(
            IOfflineServiceStackMarker marker)
        {
            if (marker == null ||
                !marker.TryGetExpected<IBuildingService>(out var buildingService))
            {
                return;
            }

            BuildingConstructionReconcileResult result =
                buildingService.ReconcileCompletedConstructions(
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (result.Status == BuildingConstructionStatus.CommitUncertain ||
                result.Status == BuildingConstructionStatus.SaveFailedRolledBack)
            {
                Debug.LogWarning(result.DiagnosticCode);
            }
        }

        private void OnApplicationQuit()
        {
            SaveIfReady();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveIfReady();
            }
        }

        private void SaveIfReady()
        {
            var validation = BootloaderInitializationResult.NotStarted();
            if (!_runtimeActive ||
                !TryGetValidatedMarker(out var marker, out validation) ||
                !marker.TryGetExpected<ISaveGameService>(out var saveGameService))
            {
                if (_runtimeActive && !validation.Succeeded && validation.State != BootloaderInitializationState.NotStarted)
                {
                    // As in Update, the claim is deliberately NOT released on drift here; the
                    // inconsistent marker blocks reuse and OnDestroy performs the release
                    // (reviewer m3 asymmetry note).
                    _runtimeActive = false;
                    _runtimeDriftReported = true;
                    enabled = false;
                    Debug.LogError(BootloaderInitializationResult.RuntimeDrift(validation.Message).Message);
                }

                return;
            }

            if (!marker.IsRuntimeOwner(_runtimeOwnerId) ||
                !ProfileMutationContainment.CanInvokeLifecycleSave(
                    saveGameService))
            {
                return;
            }

            if (saveGameService.CurrentSave == null)
            {
                Debug.LogError("[BOOT_STACK_SAVE_FAILED] Bootloader save skipped because this owner has no valid current save.");
                return;
            }

            try
            {
                ProfileMutationContainment.InvokeLifecycleSave(saveGameService);
                if (saveGameService.LastSaveStatus != SaveOperationStatus.SavedPrimary)
                {
                    Debug.LogError($"[BOOT_STACK_SAVE_FAILED] Bootloader save did not complete successfully: {saveGameService.LastSaveStatus} {saveGameService.LastSaveMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BOOT_STACK_SAVE_FAILED] Bootloader save failed: {ex.Message}");
            }
        }

        private static bool IsApprovedLoadSuccess(ISaveGameService saveGameService)
        {
            if (saveGameService == null || saveGameService.CurrentSave == null)
            {
                return false;
            }

            return saveGameService.LastLoadStatus == SaveLoadStatus.LoadedPrimary ||
                saveGameService.LastLoadStatus == SaveLoadStatus.RecoveredFromBackup ||
                saveGameService.LastLoadStatus == SaveLoadStatus.CreatedNew ||
                saveGameService.LastLoadStatus == SaveLoadStatus.CreatedNewAfterUnrecoverableCorruption ||
                saveGameService.LastLoadStatus == SaveLoadStatus.MigratedSchemaOne;
        }

        private static Type[] GetPresentRequiredTypes()
        {
            return OfflineServiceStack.RequiredServiceTypes
                .Where(type => ServiceLocator.TryGet(type, out _))
                .ToArray();
        }

        private static Type[] GetMissingRequiredTypes()
        {
            return OfflineServiceStack.RequiredServiceTypes
                .Where(type => !ServiceLocator.TryGet(type, out _))
                .ToArray();
        }

        private static void LogInitializationResult(BootloaderInitializationResult result)
        {
            if (!result.Succeeded && result.State != BootloaderInitializationState.NotStarted)
            {
                Debug.LogError(result.Message);
            }
        }
    }

    public enum BootloaderInitializationState
    {
        NotStarted,
        ReusedCompleteStack,
        CreatedCompleteStack,
        FailedPartialRegistry,
        FailedInconsistentMarker,
        FailedConstruction,
        FailedPublication,
        FailedLoad,
        FailedLoadInProgress,
        RuntimeDrift,
        RuntimeOwnerRejected
    }

    public sealed class BootloaderInitializationResult
    {
        private BootloaderInitializationResult(
            BootloaderInitializationState state,
            string code,
            string message,
            string registrationId = "",
            Type serviceType = null,
            IReadOnlyList<Type> presentTypes = null,
            IReadOnlyList<Type> missingTypes = null,
            IReadOnlyList<Type> mismatchedTypes = null,
            IReadOnlyList<Type> unexpectedTypes = null,
            IReadOnlyList<Type> invalidTypes = null)
        {
            State = state;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            RegistrationId = registrationId ?? string.Empty;
            ServiceType = serviceType;
            PresentTypes = CopyTypes(presentTypes);
            MissingTypes = CopyTypes(missingTypes);
            MismatchedTypes = CopyTypes(mismatchedTypes);
            UnexpectedTypes = CopyTypes(unexpectedTypes);
            InvalidTypes = CopyTypes(invalidTypes);
        }

        public BootloaderInitializationState State { get; }
        public string Code { get; }
        public string Message { get; }
        public string RegistrationId { get; }
        public Type ServiceType { get; }
        public IReadOnlyList<Type> PresentTypes { get; }
        public IReadOnlyList<Type> MissingTypes { get; }
        public IReadOnlyList<Type> MismatchedTypes { get; }
        public IReadOnlyList<Type> UnexpectedTypes { get; }
        public IReadOnlyList<Type> InvalidTypes { get; }
        public bool Succeeded => State == BootloaderInitializationState.ReusedCompleteStack ||
            State == BootloaderInitializationState.CreatedCompleteStack;

        public static BootloaderInitializationResult NotStarted()
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.NotStarted,
                "BOOT_STACK_NOT_STARTED",
                "Bootloader service-stack initialization has not started.");
        }

        public static BootloaderInitializationResult ReusedCompleteStack(string registrationId)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.ReusedCompleteStack,
                "BOOT_STACK_REUSED",
                $"[BOOT_STACK_REUSED] Reused offline service stack {registrationId}.",
                registrationId);
        }

        public static BootloaderInitializationResult CreatedCompleteStack(string registrationId)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.CreatedCompleteStack,
                "BOOT_STACK_CREATED",
                $"[BOOT_STACK_CREATED] Created offline service stack {registrationId}.",
                registrationId);
        }

        public static BootloaderInitializationResult FailedPartialRegistry(Type[] presentTypes, Type[] missingTypes)
        {
            string present = string.Join(", ", presentTypes.Select(type => type.Name));
            string missing = string.Join(", ", missingTypes.Select(type => type.Name));
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedPartialRegistry,
                "BOOT_STACK_PARTIAL_REGISTRY",
                $"[BOOT_STACK_PARTIAL_REGISTRY] Required services are partially registered. Present: {present}. Missing: {missing}.",
                presentTypes: presentTypes,
                missingTypes: missingTypes);
        }

        public static BootloaderInitializationResult FailedInconsistentMarker(
            string registrationId,
            Type[] mismatchedTypes,
            string message)
        {
            return FailedInconsistentMarker(
                registrationId,
                missingTypes: Array.Empty<Type>(),
                mismatchedTypes: mismatchedTypes,
                unexpectedTypes: Array.Empty<Type>(),
                invalidTypes: Array.Empty<Type>(),
                message);
        }

        public static BootloaderInitializationResult FailedInconsistentMarker(
            string registrationId,
            Type[] missingTypes,
            Type[] mismatchedTypes,
            Type[] unexpectedTypes,
            Type[] invalidTypes,
            string message)
        {
            string missing = string.Join(", ", missingTypes.Select(type => type.Name));
            string mismatched = string.Join(", ", mismatchedTypes.Select(type => type.Name));
            string unexpected = string.Join(", ", unexpectedTypes.Select(type => type.Name));
            string invalid = string.Join(", ", invalidTypes.Select(type => type.Name));
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedInconsistentMarker,
                "BOOT_STACK_MARKER_INCONSISTENT",
                $"[BOOT_STACK_MARKER_INCONSISTENT] {message} Registration: {registrationId}. Missing: {missing}. Mismatched: {mismatched}. Unexpected: {unexpected}. Invalid: {invalid}.",
                registrationId,
                missingTypes: missingTypes,
                mismatchedTypes: mismatchedTypes,
                unexpectedTypes: unexpectedTypes,
                invalidTypes: invalidTypes);
        }

        public static BootloaderInitializationResult FailedConstruction(string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedConstruction,
                "BOOT_STACK_CONSTRUCTION_FAILED",
                $"[BOOT_STACK_CONSTRUCTION_FAILED] Could not construct offline service stack: {message}");
        }

        public static BootloaderInitializationResult FailedPublication(Type serviceType, string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedPublication,
                "BOOT_STACK_PUBLICATION_FAILED",
                $"[BOOT_STACK_PUBLICATION_FAILED] Could not publish offline service stack: {message}",
                serviceType: serviceType);
        }

        public static BootloaderInitializationResult FailedLoad(string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedLoad,
                "BOOT_STACK_LOAD_FAILED",
                $"[BOOT_STACK_LOAD_FAILED] Bootloader save load failed: {message}");
        }

        public static BootloaderInitializationResult LoadInProgressConflict(string registrationId)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.FailedLoadInProgress,
                "BOOT_STACK_LOAD_IN_PROGRESS",
                $"[BOOT_STACK_LOAD_IN_PROGRESS] Offline stack load is already in progress under another owner for stack {registrationId}.",
                registrationId);
        }

        public static BootloaderInitializationResult RuntimeDrift(string message)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.RuntimeDrift,
                "BOOT_STACK_RUNTIME_DRIFT",
                $"[BOOT_STACK_RUNTIME_DRIFT] {message}");
        }

        public static BootloaderInitializationResult RuntimeOwnerRejected(string registrationId)
        {
            return new BootloaderInitializationResult(
                BootloaderInitializationState.RuntimeOwnerRejected,
                "BOOT_STACK_RUNTIME_OWNER_REJECTED",
                $"[BOOT_STACK_RUNTIME_OWNER_REJECTED] Another Bootloader already owns runtime work for stack {registrationId}.",
                registrationId);
        }

        private static IReadOnlyList<Type> CopyTypes(IReadOnlyList<Type> types)
        {
            return Array.AsReadOnly((types ?? Array.Empty<Type>()).ToArray());
        }
    }

    internal interface IOfflineServiceStackMarker
    {
        int StackVersion { get; }
        string RegistrationId { get; }
        IReadOnlyDictionary<Type, object> ExpectedInstances { get; }
        object SaveRoot { get; }
        object GameDataRoot { get; }
        string RuntimeOwnerId { get; }
        OfflineStackLoadState LoadState { get; }
        bool TryClaimRuntimeOwner(string ownerId);
        bool IsRuntimeOwner(string ownerId);
        bool TryReleaseRuntimeOwner(string ownerId);
        bool TryBeginLoad(string ownerId);
        void MarkLoadSucceeded(string ownerId);
        void MarkLoadFailed(string ownerId);
        bool TryGetExpected<T>(out T service);
    }

    internal enum OfflineStackLoadState
    {
        NotStarted,
        InProgress,
        Succeeded,
        Failed
    }

    internal sealed class LocalOfflineServiceStackMarker : IOfflineServiceStackMarker
    {
        private bool _loadInProgress;
        private bool _loadSucceeded;
        private string _runtimeOwnerId = string.Empty;

        public LocalOfflineServiceStackMarker(
            int stackVersion,
            string registrationId,
            IReadOnlyDictionary<Type, object> expectedInstances,
            object saveRoot,
            object gameDataRoot)
        {
            StackVersion = stackVersion;
            RegistrationId = registrationId;
            ExpectedInstances = new ReadOnlyDictionary<Type, object>(
                new Dictionary<Type, object>(expectedInstances));
            SaveRoot = saveRoot;
            GameDataRoot = gameDataRoot;
        }

        public int StackVersion { get; }
        public string RegistrationId { get; }
        public IReadOnlyDictionary<Type, object> ExpectedInstances { get; }
        public object SaveRoot { get; }
        public object GameDataRoot { get; }
        public string RuntimeOwnerId => _runtimeOwnerId;
        public OfflineStackLoadState LoadState { get; private set; }

        public bool TryClaimRuntimeOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return false;
            }

            if (string.IsNullOrEmpty(_runtimeOwnerId))
            {
                _runtimeOwnerId = ownerId;
                return true;
            }

            return string.Equals(_runtimeOwnerId, ownerId, StringComparison.Ordinal);
        }

        public bool IsRuntimeOwner(string ownerId)
        {
            return !string.IsNullOrWhiteSpace(ownerId) &&
                string.Equals(_runtimeOwnerId, ownerId, StringComparison.Ordinal);
        }

        public bool TryReleaseRuntimeOwner(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId))
            {
                return false;
            }

            // A destroyed or disabled owner surrenders the claim so the next Bootloader can take over.
            // If it died mid-load, fail the load state and clear the in-progress flag so the next
            // owner can retry; an already-succeeded load stays succeeded so it is not re-applied.
            if (_loadInProgress)
            {
                _loadInProgress = false;
                LoadState = OfflineStackLoadState.Failed;
            }

            _runtimeOwnerId = string.Empty;
            return true;
        }

        public bool TryBeginLoad(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId) || _loadInProgress || _loadSucceeded)
            {
                return false;
            }

            _loadInProgress = true;
            LoadState = OfflineStackLoadState.InProgress;
            return true;
        }

        public void MarkLoadSucceeded(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId))
            {
                return;
            }

            _loadInProgress = false;
            _loadSucceeded = true;
            LoadState = OfflineStackLoadState.Succeeded;
        }

        public void MarkLoadFailed(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId))
            {
                return;
            }

            _loadInProgress = false;
            LoadState = OfflineStackLoadState.Failed;
        }

        public bool TryGetExpected<T>(out T service)
        {
            if (ExpectedInstances.TryGetValue(typeof(T), out var existing) && existing is T typed)
            {
                service = typed;
                return true;
            }

            service = default;
            return false;
        }
    }

    internal sealed class OfflineServiceStack
    {
        private OfflineServiceStack(
            IReadOnlyDictionary<Type, object> requiredInstances,
            LocalOfflineServiceStackMarker marker,
            IReadOnlyList<ServiceRegistrationEntry> registrations)
        {
            RequiredInstances = requiredInstances;
            Marker = marker;
            Registrations = registrations;
        }

        private static readonly Type[] RequiredServiceTypeArray =
        {
            typeof(IGameDataService),
            typeof(ISaveGameService),
            typeof(IRealmService),
            typeof(IResourceService),
            typeof(IResearchService),
            typeof(IBuildingService),
            typeof(ITrainingService),
            typeof(IBattleSimulator),
            typeof(IWarzoneCreditService),
            typeof(IWarmasterService),
            typeof(ITerritoryService),
            typeof(IWorldStateService),
            typeof(IReputationService),
            typeof(IFactionService),
            typeof(IPersonaService),
            typeof(IQuestService),
            typeof(IStoryService),
            typeof(INotificationService),
            typeof(IRealmGemService),
            typeof(IWorldAtlasService),
            typeof(IBossLootService)
        };

        public static readonly IReadOnlyList<Type> RequiredServiceTypes = Array.AsReadOnly(RequiredServiceTypeArray);
        public IReadOnlyDictionary<Type, object> RequiredInstances { get; }
        public LocalOfflineServiceStackMarker Marker { get; }
        public IReadOnlyList<ServiceRegistrationEntry> Registrations { get; }

        // Internal construction fault-injection seams (mirroring Bootloader.PostInstallValidationOverride).
        // Each override, when non-null, substitutes or fails construction of the named service so tests
        // can exercise the spec section-12 construction faults. They are reset in test TearDown; production
        // always leaves them null and uses the current local implementations.
        internal static Func<object> GameDataFactoryOverride;
        internal static Func<object> SaveGameFactoryOverride;
        internal static Func<object, object> ResourceFactoryOverride;
        internal static Func<object> NotificationFactoryOverride;
        internal static Func<object, object, object, object> BossLootFactoryOverride;

        public static OfflineServiceStack Create(int stackVersion)
        {
            IGameDataService gameData = GameDataFactoryOverride != null
                ? (IGameDataService)GameDataFactoryOverride()
                : new LocalGameDataService();
            ISaveGameService saveGame = SaveGameFactoryOverride != null
                ? (ISaveGameService)SaveGameFactoryOverride()
                : new LocalSaveGameService();

            var realmService = new LocalRealmService(saveGame, gameData);
            KingdomProductionContributionProvider productionProvider = null;
            IResourceService resourceService;
            if (ResourceFactoryOverride != null)
            {
                resourceService = (IResourceService)ResourceFactoryOverride(saveGame);
            }
            else
            {
                productionProvider = KingdomProductionConsumer.CreateLive(saveGame);
                resourceService = new LocalResourceService(saveGame, productionProvider);
            }

            var researchService = new LocalResearchService(saveGame, resourceService);
            var buildingService = new LocalBuildingService(saveGame, resourceService, gameData);
            KingdomProductionConsumer.BindBuildingService(productionProvider, buildingService);
            var trainingService = new LocalTrainingService(saveGame, resourceService);

            var battleSim = new AL.Battle.Simulator.FixedPointBattleSimulator();
            var warzoneCredits = new LocalWarzoneCreditService(saveGame);
            var warmaster = new LocalWarmasterService(saveGame, warzoneCredits);
            var territoryService = new AL.RealmWar.Warzone.WarzoneService(saveGame);
            var questService = new LocalQuestService(saveGame, resourceService, warzoneCredits);
            var storyService = new LocalStoryService(saveGame, gameData);
            var reputationService = new ReputationService(saveGame);
            var factionService = new FactionService(saveGame);
            var personaService = new PersonaService(saveGame);
            INotificationService notificationService = NotificationFactoryOverride != null
                ? (INotificationService)NotificationFactoryOverride()
                : new LocalNotificationService();
            var realmGemService = new LocalRealmGemService(saveGame);
            var worldStateService = new WorldStateService(saveGame, notificationService);
            var worldAtlasService = new AL.RealmWar.World.LocalWorldAtlasService(storyService);
            IBossLootService bossLootService = BossLootFactoryOverride != null
                ? (IBossLootService)BossLootFactoryOverride(saveGame, warzoneCredits, notificationService)
                : new LocalBossLootService(saveGame, warzoneCredits, notificationService);

            var requiredInstances = new Dictionary<Type, object>
            {
                [typeof(IGameDataService)] = gameData,
                [typeof(ISaveGameService)] = saveGame,
                [typeof(IRealmService)] = realmService,
                [typeof(IResourceService)] = resourceService,
                [typeof(IResearchService)] = researchService,
                [typeof(IBuildingService)] = buildingService,
                [typeof(ITrainingService)] = trainingService,
                [typeof(IBattleSimulator)] = battleSim,
                [typeof(IWarzoneCreditService)] = warzoneCredits,
                [typeof(IWarmasterService)] = warmaster,
                [typeof(ITerritoryService)] = territoryService,
                [typeof(IWorldStateService)] = worldStateService,
                [typeof(IReputationService)] = reputationService,
                [typeof(IFactionService)] = factionService,
                [typeof(IPersonaService)] = personaService,
                [typeof(IQuestService)] = questService,
                [typeof(IStoryService)] = storyService,
                [typeof(INotificationService)] = notificationService,
                [typeof(IRealmGemService)] = realmGemService,
                [typeof(IWorldAtlasService)] = worldAtlasService,
                [typeof(IBossLootService)] = bossLootService
            };

            ProfileMutationCatalogValidationResult mutationCoverage =
                ProfileMutationSurfaceCatalog.ValidateProductionCoverage(
                    requiredInstances.Keys);
            if (!mutationCoverage.IsValid)
            {
                throw new InvalidOperationException(
                    "Profile mutation surface coverage is incomplete: " +
                    string.Join(",", mutationCoverage.Diagnostics));
            }

            if (RequiredServiceTypes.Any(type => !requiredInstances.ContainsKey(type) || requiredInstances[type] == null))
            {
                throw new InvalidOperationException("Offline service stack required-instance map is incomplete.");
            }

            var immutableRequiredInstances = new ReadOnlyDictionary<Type, object>(
                new Dictionary<Type, object>(requiredInstances));

            var marker = new LocalOfflineServiceStackMarker(
                stackVersion,
                Guid.NewGuid().ToString("N"),
                immutableRequiredInstances,
                saveGame,
                gameData);

            var registrations = RequiredServiceTypes
                .Select(type => new ServiceRegistrationEntry(type, immutableRequiredInstances[type]))
                .ToList();
            registrations.Add(new ServiceRegistrationEntry(typeof(IOfflineServiceStackMarker), marker));

            return new OfflineServiceStack(
                immutableRequiredInstances,
                marker,
                new ReadOnlyCollection<ServiceRegistrationEntry>(registrations));
        }
    }

    // The following internal types exist only to support the EditMode integrity tests. The test
    // assembly (AL.EditMode.Tests) cannot reference the predefined Assembly-CSharp, so it cannot
    // implement the internal marker interface or the service interfaces directly; these controllable
    // doubles live in the production assembly and are driven by reflection. They are never referenced
    // by production code paths and carry no static state beyond per-instance test configuration.
    internal sealed class ControllableSaveGameService :
        ISaveGameService,
        ILegacyMvpLoopCandidateStore,
        ILegacyFirstWorldProgressCandidateStore
    {
        private SaveGameData _currentSave;

        internal int LoadCount;
        internal int SaveCount;
        internal int CurrentSaveReadCount;
        internal int FailLoadTimes = 0;
        internal bool ThrowOnSave = false;
        internal bool ProvideCurrentSaveOnLoad = true;
        internal SaveLoadStatus LoadStatusToReport = SaveLoadStatus.CreatedNew;
        internal SaveOperationStatus SaveStatusToReport = SaveOperationStatus.SavedPrimary;
        internal Action LoadCallback = null;

        public SaveGameData CurrentSave
        {
            get
            {
                CurrentSaveReadCount++;
                return _currentSave;
            }
        }
        public SaveLoadStatus LastLoadStatus { get; private set; }
        public string LastLoadMessage { get; private set; } = string.Empty;
        public SaveOperationStatus LastSaveStatus { get; private set; }
        public string LastSaveMessage { get; private set; } = string.Empty;

        internal void SeedCurrentSave()
        {
            _currentSave = new SaveGameData();
        }

        public void Load()
        {
            LoadCount++;
            LoadCallback?.Invoke();

            if (LoadCount <= FailLoadTimes)
            {
                throw new InvalidOperationException("Controlled offline stack load failure.");
            }

            LastLoadStatus = LoadStatusToReport;
            LastLoadMessage = "Controlled load.";
            _currentSave = ProvideCurrentSaveOnLoad ? (_currentSave ?? new SaveGameData()) : null;
        }

        public void Save()
        {
            SaveCount++;

            if (ThrowOnSave)
            {
                throw new InvalidOperationException("Controlled save failure.");
            }

            LastSaveStatus = SaveStatusToReport;
            LastSaveMessage = "Controlled save.";
        }

        public bool HasSave()
        {
            return _currentSave != null;
        }

        public void CreateNewSave(RealmId realmId)
        {
            _currentSave = new SaveGameData { SelectedRealm = realmId };
        }

        public void DeleteSave()
        {
            _currentSave = null;
            LastLoadStatus = SaveLoadStatus.None;
            LastSaveStatus = SaveOperationStatus.None;
        }

        SaveCandidateCommitResult
            ILegacyMvpLoopCandidateStore.TryCommitLegacyMvpLoop(
                MvpLoopCommitRequest request)
        {
            if (!TryCloneCurrentSave(out SaveGameData candidate))
            {
                return RejectedCandidate("AL-MVP-LOOP-PROFILE-READ-ONLY");
            }

            MvpLoopPrepareDisposition disposition =
                MvpLoopSaveCodec.PrepareCandidate(
                    candidate,
                    request,
                    out string message);
            switch (disposition)
            {
                case MvpLoopPrepareDisposition.Duplicate:
                    return new SaveCandidateCommitResult(
                        SaveCandidateCommitOutcome.Duplicate,
                        _currentSave,
                        message);
                case MvpLoopPrepareDisposition.Prepared:
                    return PublishCandidate(candidate, message);
                default:
                    return RejectedCandidate(message);
            }
        }

        SaveCandidateCommitResult
            ILegacyFirstWorldProgressCandidateStore
                .TryCommitLegacyFirstWorldProgress(
                    FirstWorldProgressCommitRequest request)
        {
            if (!TryCloneCurrentSave(out SaveGameData candidate))
            {
                return RejectedCandidate(
                    "AL-FIRST-WORLD-PROFILE-READ-ONLY");
            }

            FirstWorldProgressPrepareDisposition disposition =
                FirstWorldProgressSaveCodec.PrepareCandidate(
                    candidate,
                    request,
                    out _,
                    out string message);
            switch (disposition)
            {
                case FirstWorldProgressPrepareDisposition.Duplicate:
                    return new SaveCandidateCommitResult(
                        SaveCandidateCommitOutcome.Duplicate,
                        _currentSave,
                        message);
                case FirstWorldProgressPrepareDisposition.Prepared:
                    return PublishCandidate(candidate, message);
                default:
                    return RejectedCandidate(message);
            }
        }

        private bool TryCloneCurrentSave(out SaveGameData candidate)
        {
            candidate = null;
            if (_currentSave == null)
            {
                return false;
            }

            string json = JsonUtility.ToJson(_currentSave);
            candidate = JsonUtility.FromJson<SaveGameData>(json);
            return candidate != null;
        }

        private SaveCandidateCommitResult PublishCandidate(
            SaveGameData candidate,
            string message)
        {
            SaveGameData previous = _currentSave;
            _currentSave = candidate;
            try
            {
                Save();
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.Committed,
                    _currentSave,
                    message);
            }
            catch (Exception exception)
            {
                _currentSave = previous;
                return new SaveCandidateCommitResult(
                    SaveCandidateCommitOutcome.PreviousPreserved,
                    _currentSave,
                    exception.Message);
            }
        }

        private SaveCandidateCommitResult RejectedCandidate(string message)
        {
            return new SaveCandidateCommitResult(
                SaveCandidateCommitOutcome.Rejected,
                _currentSave,
                string.IsNullOrWhiteSpace(message)
                    ? "AL-TEST-SAVE-CANDIDATE-REJECTED"
                    : message);
        }
    }

    internal sealed class CountingResourceService : IResourceService
    {
        internal int TickCount;

        public event Action<ResourceType, long> OnResourceChanged;

        public long GetResourceCount(ResourceType type)
        {
            return 0;
        }

        public void AddResource(ResourceType type, long amount)
        {
        }

        public bool ConsumeResource(ResourceType type, long amount)
        {
            return false;
        }

        public bool HasEnough(ResourceType type, long amount)
        {
            return false;
        }

        public void TickProduction(double deltaSeconds)
        {
            TickCount++;
            OnResourceChanged?.Invoke(ResourceType.Gold, 0);
        }
    }

    internal sealed class ConfigurableOfflineServiceStackMarker : IOfflineServiceStackMarker
    {
        private string _runtimeOwnerId = string.Empty;
        private bool _loadInProgress;
        private bool _loadSucceeded;

        public int StackVersion { get; set; } = 1;
        public string RegistrationId { get; set; } = "configurable-marker";
        public IReadOnlyDictionary<Type, object> ExpectedInstances { get; set; }
        public object SaveRoot { get; set; }
        public object GameDataRoot { get; set; }
        public string RuntimeOwnerId => _runtimeOwnerId;
        public OfflineStackLoadState LoadState { get; private set; }

        public bool TryClaimRuntimeOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return false;
            }

            if (string.IsNullOrEmpty(_runtimeOwnerId))
            {
                _runtimeOwnerId = ownerId;
                return true;
            }

            return string.Equals(_runtimeOwnerId, ownerId, StringComparison.Ordinal);
        }

        public bool IsRuntimeOwner(string ownerId)
        {
            return !string.IsNullOrWhiteSpace(ownerId) &&
                string.Equals(_runtimeOwnerId, ownerId, StringComparison.Ordinal);
        }

        public bool TryReleaseRuntimeOwner(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId))
            {
                return false;
            }

            if (_loadInProgress)
            {
                _loadInProgress = false;
                LoadState = OfflineStackLoadState.Failed;
            }

            _runtimeOwnerId = string.Empty;
            return true;
        }

        public bool TryBeginLoad(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId) || _loadInProgress || _loadSucceeded)
            {
                return false;
            }

            _loadInProgress = true;
            LoadState = OfflineStackLoadState.InProgress;
            return true;
        }

        public void MarkLoadSucceeded(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId))
            {
                return;
            }

            _loadInProgress = false;
            _loadSucceeded = true;
            LoadState = OfflineStackLoadState.Succeeded;
        }

        public void MarkLoadFailed(string ownerId)
        {
            if (!IsRuntimeOwner(ownerId))
            {
                return;
            }

            _loadInProgress = false;
            LoadState = OfflineStackLoadState.Failed;
        }

        public bool TryGetExpected<T>(out T service)
        {
            if (ExpectedInstances != null &&
                ExpectedInstances.TryGetValue(typeof(T), out var existing) &&
                existing is T typed)
            {
                service = typed;
                return true;
            }

            service = default;
            return false;
        }
    }
}
