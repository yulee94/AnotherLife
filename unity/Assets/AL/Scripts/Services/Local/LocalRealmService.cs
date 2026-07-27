using System;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.RealmSelection;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalRealmService : IRealmService
    {
        private readonly ISaveGameService _saveGameService;
        private readonly IGameDataService _gameDataService;
        private readonly RealmCatalogSnapshot _catalog;
        private RealmCatalogSnapshot Catalog => _catalog ?? RealmCatalogRuntime.Current;

        public RealmId CurrentRealmId => Identity.IsCommittedValid ? Identity.RealmId : RealmId.None;
        public RealmDefinition CurrentRealm => Identity.IsCommittedValid ? _gameDataService.GetRealm(Identity.RealmId) : null;

        public RealmIdentitySnapshot Identity
        {
            get
            {
                if (_saveGameService == null || _saveGameService.CurrentSave == null)
                    return Snapshot(RealmIdentityStatus.ProfileUnavailable, RealmId.None, "AL-REALM-PROFILE-UNAVAILABLE");
                RealmId selected = _saveGameService.CurrentSave.SelectedRealm;
                if (selected == RealmId.None)
                    return Snapshot(RealmIdentityStatus.Uncommitted, RealmId.None, "AL-REALM-UNCOMMITTED");
                if (!IsDefinedPlayable(selected))
                    return Snapshot(RealmIdentityStatus.InvalidPersistedIdentity, selected, "AL-REALM-PERSISTED-ID-INVALID");
                RealmCatalogEntry ignored;
                if (Catalog == null || !Catalog.TryGet(selected, out ignored))
                    return Snapshot(RealmIdentityStatus.CatalogUnavailable, selected, "AL-REALM-DEFINITION-UNAVAILABLE");
                if (!HasRuntimeDefinition(selected))
                    return Snapshot(RealmIdentityStatus.CatalogUnavailable, selected, "AL-REALM-DEFINITION-UNAVAILABLE");
                return Snapshot(RealmIdentityStatus.CommittedValid, selected, "AL-REALM-COMMITTED-VALID");
            }
        }

        public LocalRealmService(ISaveGameService saveGameService, IGameDataService gameDataService)
            : this(saveGameService, gameDataService, RealmCatalogRuntime.Current) { }

        public LocalRealmService(ISaveGameService saveGameService, IGameDataService gameDataService, RealmCatalogSnapshot catalog)
        {
            _saveGameService = saveGameService;
            _gameDataService = gameDataService;
            _catalog = catalog;
        }

        public void SelectRealm(RealmId id)
        {
            RealmSelectionResult result = TrySelectRealm(new RealmSelectionRequest(Guid.NewGuid().ToString("N"), id));
            if (!result.AllowsNavigation) Debug.LogWarning(result.TechnicalCode);
        }

        public RealmSelectionResult TrySelectRealm(RealmSelectionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
                return Result(RealmSelectionStatus.InvalidTransaction, request.RequestedRealmId, false, false, "AL-REALM-TRANSACTION-INVALID");
            if (!IsDefinedPlayable(request.RequestedRealmId))
                return Result(RealmSelectionStatus.InvalidRealm, request.RequestedRealmId, false, false, "AL-REALM-REQUEST-INVALID");
            RealmCatalogEntry ignored;
            if (Catalog == null || !Catalog.TryGet(request.RequestedRealmId, out ignored))
                return Result(RealmSelectionStatus.RealmDefinitionUnavailable, request.RequestedRealmId, false, false, "AL-REALM-DEFINITION-UNAVAILABLE");
            if (!HasRuntimeDefinition(request.RequestedRealmId))
                return Result(RealmSelectionStatus.RealmDefinitionUnavailable, request.RequestedRealmId, false, false, "AL-REALM-DEFINITION-UNAVAILABLE");
            if (_saveGameService == null)
                return Result(RealmSelectionStatus.ProfileUnavailable, request.RequestedRealmId, false, false, "AL-REALM-PROFILE-UNAVAILABLE");

            RealmId existing = _saveGameService.CurrentSave == null ? RealmId.None : _saveGameService.CurrentSave.SelectedRealm;
            if (existing != RealmId.None)
            {
                if (!IsDefinedPlayable(existing) || !Catalog.TryGet(existing, out ignored))
                    return Result(RealmSelectionStatus.ProfileUnavailable, request.RequestedRealmId, false, false, "AL-REALM-PERSISTED-ID-INVALID");
                return existing == request.RequestedRealmId
                    ? Result(RealmSelectionStatus.AlreadyCommittedSameRealm, request.RequestedRealmId, false, true, "AL-REALM-ALREADY-COMMITTED")
                    : Result(RealmSelectionStatus.RejectedDifferentRealm, request.RequestedRealmId, false, true, "AL-REALM-DIFFERENT-REALM-REJECTED");
            }

            if (_saveGameService.CurrentSave == null) _saveGameService.CreateNewSave(request.RequestedRealmId);
            else
            {
                _saveGameService.CurrentSave.SelectedRealm = request.RequestedRealmId;
                _saveGameService.Save();
            }

            bool persisted = _saveGameService.LastSaveStatus == SaveOperationStatus.SavedPrimary &&
                             _saveGameService.CurrentSave != null &&
                             _saveGameService.CurrentSave.SelectedRealm == request.RequestedRealmId;
            if (!persisted)
            {
                if (_saveGameService.CurrentSave != null) _saveGameService.CurrentSave.SelectedRealm = RealmId.None;
                return Result(RealmSelectionStatus.SaveFailedPreviousPreserved, request.RequestedRealmId, false, false, "AL-REALM-SAVE-FAILED");
            }

            Debug.Log($"Realm committed: {request.RequestedRealmId}");
            return Result(RealmSelectionStatus.Committed, request.RequestedRealmId, true, true, "AL-REALM-COMMITTED");
        }

        private RealmIdentitySnapshot Snapshot(RealmIdentityStatus status, RealmId id, string code)
        {
            return new RealmIdentitySnapshot(status, id, Catalog == null ? string.Empty : Catalog.Version, code);
        }

        private RealmSelectionResult Result(RealmSelectionStatus status, RealmId requested, bool mutated, bool persisted, string code)
        {
            RealmId committed = _saveGameService == null || _saveGameService.CurrentSave == null ? RealmId.None : _saveGameService.CurrentSave.SelectedRealm;
            return new RealmSelectionResult(status, requested, committed, mutated, persisted, code);
        }

        private static bool IsDefinedPlayable(RealmId id)
        {
            return id != RealmId.None && Enum.IsDefined(typeof(RealmId), id);
        }

        private bool HasRuntimeDefinition(RealmId id)
        {
            return _gameDataService != null && _gameDataService.GetRealm(id) != null;
        }
    }
}
