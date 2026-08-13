using System;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmSelection;

namespace AL.UI
{
    public enum LaunchReadinessState
    {
        WaitingForBootLoad,
        WaitingForRequiredCatalogs,
        WaitingForMediaPresentation,
        WaitingForDestination,
        AwaitingExplicitContinue,
        Failed,
        Transitioning
    }

    public enum LaunchReadinessFailure
    {
        None,
        BootLoadUnavailable,
        RequiredCatalogUnavailable,
        DestinationUnavailable,
        EvidenceStale,
        RetryLimitReached
    }

    public enum LaunchMediaPresentation
    {
        None,
        StaticFallbackEstablished,
        LoopingVideoEstablished
    }

    public readonly struct LaunchBootLoadEvidence
    {
        public LaunchBootLoadEvidence(
            int attemptGeneration,
            string stackRegistrationId,
            int stackVersion,
            SaveLoadStatus loadStatus,
            int saveSchemaVersion,
            int profileInitializationVersion)
        {
            AttemptGeneration = attemptGeneration;
            StackRegistrationId = stackRegistrationId ?? string.Empty;
            StackVersion = stackVersion;
            LoadStatus = loadStatus;
            SaveSchemaVersion = saveSchemaVersion;
            ProfileInitializationVersion = profileInitializationVersion;
        }

        public int AttemptGeneration { get; }
        public string StackRegistrationId { get; }
        public int StackVersion { get; }
        public SaveLoadStatus LoadStatus { get; }
        public int SaveSchemaVersion { get; }
        public int ProfileInitializationVersion { get; }

        public bool IsWellFormed =>
            AttemptGeneration > 0 &&
            !string.IsNullOrWhiteSpace(StackRegistrationId) &&
            StackVersion > 0 &&
            IsApprovedLoadStatus(LoadStatus) &&
            SaveSchemaVersion == SaveGameData.CurrentSaveSchemaVersion &&
            ProfileInitializationVersion == SaveGameData.CurrentProfileInitializationVersion;

        internal static bool IsApprovedLoadStatus(SaveLoadStatus status)
        {
            return status == SaveLoadStatus.LoadedPrimary ||
                status == SaveLoadStatus.RecoveredFromBackup ||
                status == SaveLoadStatus.CreatedNew ||
                status == SaveLoadStatus.CreatedNewAfterUnrecoverableCorruption;
        }
    }

    public readonly struct LaunchCatalogEvidence
    {
        public LaunchCatalogEvidence(
            int attemptGeneration,
            int catalogGeneration,
            string catalogVersion,
            int realmCount)
        {
            AttemptGeneration = attemptGeneration;
            CatalogGeneration = catalogGeneration;
            CatalogVersion = catalogVersion ?? string.Empty;
            RealmCount = realmCount;
        }

        public int AttemptGeneration { get; }
        public int CatalogGeneration { get; }
        public string CatalogVersion { get; }
        public int RealmCount { get; }

        public bool IsWellFormed =>
            AttemptGeneration > 0 &&
            CatalogGeneration > 0 &&
            string.Equals(
                CatalogVersion,
                RealmCatalogRuntime.SupportedVersion,
                StringComparison.Ordinal) &&
            RealmCount == 4;
    }

    public readonly struct LaunchDestinationEvidence
    {
        public LaunchDestinationEvidence(int attemptGeneration, string sceneName)
        {
            AttemptGeneration = attemptGeneration;
            SceneName = sceneName ?? string.Empty;
        }

        public int AttemptGeneration { get; }
        public string SceneName { get; }

        public bool IsWellFormed =>
            AttemptGeneration > 0 &&
            !string.IsNullOrWhiteSpace(SceneName);
    }

    public readonly struct LaunchReadinessSnapshot
    {
        internal LaunchReadinessSnapshot(
            int attemptGeneration,
            int attemptNumber,
            LaunchReadinessState state,
            LaunchReadinessFailure failure,
            bool retryAllowed)
        {
            AttemptGeneration = attemptGeneration;
            AttemptNumber = attemptNumber;
            State = state;
            Failure = failure;
            RetryAllowed = retryAllowed;
        }

        public int AttemptGeneration { get; }
        public int AttemptNumber { get; }
        public LaunchReadinessState State { get; }
        public LaunchReadinessFailure Failure { get; }
        public bool RetryAllowed { get; }
        public bool CanContinue => State == LaunchReadinessState.AwaitingExplicitContinue;
    }

    /// <summary>
    /// Pure, process-local launch gate. It owns no save, catalog, media, input, or scene work;
    /// adapters can only publish evidence bound to the current attempt generation.
    /// </summary>
    public sealed class LaunchReadinessCoordinator
    {
        public const int MaximumAttempts = 3;

        private int _attemptGeneration = 1;
        private int _attemptNumber = 1;
        private LaunchBootLoadEvidence? _bootLoad;
        private LaunchCatalogEvidence? _catalog;
        private LaunchDestinationEvidence? _destination;
        private LaunchMediaPresentation _media;
        private LaunchReadinessFailure _failure;
        private bool _retryAllowed;
        private bool _transitioning;

        public int AttemptGeneration => _attemptGeneration;

        public LaunchReadinessSnapshot Snapshot
        {
            get
            {
                LaunchReadinessState state;
                if (_transitioning)
                {
                    state = LaunchReadinessState.Transitioning;
                }
                else if (_failure != LaunchReadinessFailure.None)
                {
                    state = LaunchReadinessState.Failed;
                }
                else if (!_bootLoad.HasValue)
                {
                    state = LaunchReadinessState.WaitingForBootLoad;
                }
                else if (!_catalog.HasValue)
                {
                    state = LaunchReadinessState.WaitingForRequiredCatalogs;
                }
                else if (_media == LaunchMediaPresentation.None)
                {
                    state = LaunchReadinessState.WaitingForMediaPresentation;
                }
                else if (!_destination.HasValue)
                {
                    state = LaunchReadinessState.WaitingForDestination;
                }
                else
                {
                    state = LaunchReadinessState.AwaitingExplicitContinue;
                }

                return new LaunchReadinessSnapshot(
                    _attemptGeneration,
                    _attemptNumber,
                    state,
                    _failure,
                    _retryAllowed);
            }
        }

        public bool TryPublishBootLoad(LaunchBootLoadEvidence evidence)
        {
            if (!CanAccept(evidence.AttemptGeneration) || !evidence.IsWellFormed)
            {
                return false;
            }

            _bootLoad = evidence;
            return true;
        }

        public bool TryPublishCatalog(LaunchCatalogEvidence evidence)
        {
            if (!CanAccept(evidence.AttemptGeneration) || !evidence.IsWellFormed)
            {
                return false;
            }

            _catalog = evidence;
            return true;
        }

        public bool TryEstablishMedia(int attemptGeneration, LaunchMediaPresentation presentation)
        {
            if (!CanAccept(attemptGeneration) || presentation == LaunchMediaPresentation.None)
            {
                return false;
            }

            _media = presentation;
            return true;
        }

        public bool TryPublishDestination(LaunchDestinationEvidence evidence)
        {
            if (!CanAccept(evidence.AttemptGeneration) || !evidence.IsWellFormed)
            {
                return false;
            }

            _destination = evidence;
            return true;
        }

        public bool TryFail(
            int attemptGeneration,
            LaunchReadinessFailure failure,
            bool retryAllowed)
        {
            if (!CanAccept(attemptGeneration) || failure == LaunchReadinessFailure.None)
            {
                return false;
            }

            if (retryAllowed && _attemptNumber >= MaximumAttempts)
            {
                _failure = LaunchReadinessFailure.RetryLimitReached;
                _retryAllowed = false;
            }
            else
            {
                _failure = failure;
                _retryAllowed = retryAllowed;
            }
            return true;
        }

        public bool TryBeginRetry()
        {
            if (_transitioning ||
                _failure == LaunchReadinessFailure.None ||
                !_retryAllowed)
            {
                return false;
            }

            if (_attemptNumber >= MaximumAttempts)
            {
                _failure = LaunchReadinessFailure.RetryLimitReached;
                _retryAllowed = false;
                return false;
            }

            _attemptNumber++;
            _attemptGeneration++;
            _bootLoad = null;
            _catalog = null;
            _destination = null;
            _media = LaunchMediaPresentation.None;
            _failure = LaunchReadinessFailure.None;
            _retryAllowed = false;
            return true;
        }

        public bool TryBeginTransition(int attemptGeneration)
        {
            if (attemptGeneration != _attemptGeneration ||
                _transitioning ||
                !Snapshot.CanContinue)
            {
                return false;
            }

            _transitioning = true;
            return true;
        }

        public bool TryFailTransition(
            int attemptGeneration,
            LaunchReadinessFailure failure,
            bool retryAllowed)
        {
            if (attemptGeneration != _attemptGeneration ||
                !_transitioning ||
                failure == LaunchReadinessFailure.None)
            {
                return false;
            }

            _transitioning = false;
            _failure = failure;
            _retryAllowed = retryAllowed && _attemptNumber < MaximumAttempts;
            return true;
        }

        private bool CanAccept(int attemptGeneration)
        {
            return attemptGeneration == _attemptGeneration &&
                !_transitioning &&
                _failure == LaunchReadinessFailure.None;
        }
    }

    internal enum BootLoadReadinessProbeStatus
    {
        Pending,
        Ready,
        Unavailable
    }

    internal sealed class CurrentBootLoadReceipt
    {
        internal CurrentBootLoadReceipt(
            LaunchBootLoadEvidence evidence,
            IOfflineServiceStackMarker marker,
            ISaveGameService saveService,
            SaveGameData save)
        {
            Evidence = evidence;
            Marker = marker;
            SaveService = saveService;
            Save = save;
        }

        internal LaunchBootLoadEvidence Evidence { get; }
        internal IOfflineServiceStackMarker Marker { get; }
        internal ISaveGameService SaveService { get; }
        internal SaveGameData Save { get; }
    }

    internal static class BootLoadReadinessProbe
    {
        internal static BootLoadReadinessProbeStatus TryCapture(
            int attemptGeneration,
            out CurrentBootLoadReceipt receipt)
        {
            receipt = null;
            if (!ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker))
            {
                return BootLoadReadinessProbeStatus.Unavailable;
            }

            if (marker.LoadState == OfflineStackLoadState.NotStarted ||
                marker.LoadState == OfflineStackLoadState.InProgress)
            {
                return BootLoadReadinessProbeStatus.Pending;
            }

            if (marker.LoadState != OfflineStackLoadState.Succeeded ||
                !marker.TryGetExpected<ISaveGameService>(out var saveService) ||
                !ServiceLocator.TryGet<ISaveGameService>(out var registeredSave) ||
                !ReferenceEquals(saveService, registeredSave) ||
                !ReferenceEquals(marker.SaveRoot, saveService) ||
                saveService.CurrentSave == null ||
                !LaunchBootLoadEvidence.IsApprovedLoadStatus(saveService.LastLoadStatus) ||
                !IsCurrentSaveSchema(saveService.CurrentSave))
            {
                return BootLoadReadinessProbeStatus.Unavailable;
            }

            var evidence = new LaunchBootLoadEvidence(
                attemptGeneration,
                marker.RegistrationId,
                marker.StackVersion,
                saveService.LastLoadStatus,
                saveService.CurrentSave.SaveSchemaVersion,
                saveService.CurrentSave.ProfileInitializationVersion);
            if (!evidence.IsWellFormed)
            {
                return BootLoadReadinessProbeStatus.Unavailable;
            }

            receipt = new CurrentBootLoadReceipt(
                evidence,
                marker,
                saveService,
                saveService.CurrentSave);
            return BootLoadReadinessProbeStatus.Ready;
        }

        internal static bool IsCurrent(CurrentBootLoadReceipt receipt)
        {
            if (receipt == null ||
                !ServiceLocator.TryGet<IOfflineServiceStackMarker>(out var marker) ||
                !ServiceLocator.TryGet<ISaveGameService>(out var saveService))
            {
                return false;
            }

            return ReferenceEquals(marker, receipt.Marker) &&
                ReferenceEquals(saveService, receipt.SaveService) &&
                ReferenceEquals(saveService.CurrentSave, receipt.Save) &&
                ReferenceEquals(marker.SaveRoot, saveService) &&
                marker.LoadState == OfflineStackLoadState.Succeeded &&
                string.Equals(
                    marker.RegistrationId,
                    receipt.Evidence.StackRegistrationId,
                    StringComparison.Ordinal) &&
                marker.StackVersion == receipt.Evidence.StackVersion &&
                saveService.LastLoadStatus == receipt.Evidence.LoadStatus &&
                IsCurrentSaveSchema(saveService.CurrentSave);
        }

        private static bool IsCurrentSaveSchema(SaveGameData save)
        {
            return save != null &&
                string.Equals(
                    save.SaveFormatId,
                    SaveGameData.CurrentSaveFormatId,
                    StringComparison.Ordinal) &&
                save.SaveSchemaVersion == SaveGameData.CurrentSaveSchemaVersion &&
                save.ProfileInitializationVersion ==
                    SaveGameData.CurrentProfileInitializationVersion;
        }
    }

    internal sealed class CurrentRealmCatalogReceipt
    {
        internal CurrentRealmCatalogReceipt(
            LaunchCatalogEvidence evidence,
            RealmCatalogSnapshot snapshot)
        {
            Evidence = evidence;
            Snapshot = snapshot;
        }

        internal LaunchCatalogEvidence Evidence { get; }
        internal RealmCatalogSnapshot Snapshot { get; }
    }

    internal static class RealmCatalogReadinessProbe
    {
        internal static bool TryCapture(
            int attemptGeneration,
            out CurrentRealmCatalogReceipt receipt)
        {
            receipt = null;
            RealmCatalogSnapshot snapshot = RealmCatalogRuntime.Current;
            if (RealmCatalogRuntime.Status != RealmCatalogRuntimeStatus.Ready ||
                snapshot == null)
            {
                return false;
            }

            var evidence = new LaunchCatalogEvidence(
                attemptGeneration,
                RealmCatalogRuntime.CurrentGeneration,
                snapshot.Version,
                snapshot.Realms.Count);
            if (!evidence.IsWellFormed)
            {
                return false;
            }

            receipt = new CurrentRealmCatalogReceipt(evidence, snapshot);
            return true;
        }

        internal static bool IsCurrent(CurrentRealmCatalogReceipt receipt)
        {
            return receipt != null &&
                RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Ready &&
                ReferenceEquals(RealmCatalogRuntime.Current, receipt.Snapshot) &&
                RealmCatalogRuntime.CurrentGeneration ==
                    receipt.Evidence.CatalogGeneration &&
                string.Equals(
                    RealmCatalogRuntime.Current.Version,
                    receipt.Evidence.CatalogVersion,
                    StringComparison.Ordinal);
        }
    }
}
