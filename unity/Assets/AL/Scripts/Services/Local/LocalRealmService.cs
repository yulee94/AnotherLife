using System;
using System.Security.Cryptography;
using System.Text;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.SaveAuthority;
using AL.Data.Definitions;
using AL.RealmSelection;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalRealmService : IRealmService
    {
        private const string CatalogAuthorityId = "al_realm_catalog";
        private const string CanonicalTransactionPrefix = "rsel_";
        private readonly ISaveGameService _saveGameService;
        private readonly IGameDataService _gameDataService;
        private readonly RealmCatalogSnapshot _catalog;
        private readonly IProfileBoundRealmSelectionStore _committedStore;
        private readonly IProfileWriteAuthorityProvider _authorityProvider;
        private RealmIdentitySnapshot _committedIdentity;
        private RealmCatalogSnapshot Catalog => _catalog ?? RealmCatalogRuntime.Current;

        public RealmId CurrentRealmId => Identity.IsCommittedValid ? Identity.RealmId : RealmId.None;
        public RealmDefinition CurrentRealm => Identity.IsCommittedValid ? _gameDataService.GetRealm(Identity.RealmId) : null;

        public RealmIdentitySnapshot Identity
        {
            get
            {
                RefreshCommittedIdentity();
                RealmIdentitySnapshot identity = _committedIdentity;
                if (!identity.IsCommittedValid)
                {
                    return identity;
                }

                if (!IsDefinedPlayable(identity.RealmId))
                    return Snapshot(RealmIdentityStatus.InvalidPersistedIdentity, identity.RealmId, "AL-REALM-PERSISTED-ID-INVALID");
                RealmCatalogEntry ignored;
                if (Catalog == null || !Catalog.TryGet(identity.RealmId, out ignored))
                    return Snapshot(RealmIdentityStatus.CatalogUnavailable, identity.RealmId, "AL-REALM-DEFINITION-UNAVAILABLE");
                if (!HasRuntimeDefinition(identity.RealmId))
                    return Snapshot(RealmIdentityStatus.CatalogUnavailable, identity.RealmId, "AL-REALM-DEFINITION-UNAVAILABLE");
                return identity;
            }
        }

        public LocalRealmService(ISaveGameService saveGameService, IGameDataService gameDataService)
            : this(saveGameService, gameDataService, RealmCatalogRuntime.Current) { }

        public LocalRealmService(ISaveGameService saveGameService, IGameDataService gameDataService, RealmCatalogSnapshot catalog)
        {
            _saveGameService = saveGameService;
            _gameDataService = gameDataService;
            _catalog = catalog;
            _committedStore = saveGameService as IProfileBoundRealmSelectionStore;
            _authorityProvider = saveGameService as IProfileWriteAuthorityProvider;
            _committedIdentity = _committedStore == null
                ? Snapshot(RealmIdentityStatus.ProfileUnavailable, RealmId.None, "AL-REALM-COMMITTED-STORE-UNAVAILABLE")
                : _committedStore.GetCommittedRealm();
            if (_committedStore != null)
            {
                _committedStore.RealmSelectionCommitted += OnRealmSelectionCommitted;
            }
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

            if (_committedStore == null || _authorityProvider == null)
            {
                return Result(
                    RealmSelectionStatus.ProfileUnavailable,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-PRODUCTION-AUTHORITY-UNAVAILABLE");
            }

            RealmCatalogEntry entry;
            if (!Catalog.TryGet(request.RequestedRealmId, out entry))
            {
                return Result(
                    RealmSelectionStatus.RealmDefinitionUnavailable,
                    request.RequestedRealmId,
                    false,
                    false,
                    "AL-REALM-DEFINITION-UNAVAILABLE");
            }

            RealmSelectionCommitResult committed =
                _committedStore.TryCommitRealmSelection(
                    new RealmSelectionCommand(
                        CanonicalizeTransactionId(request),
                        request.RequestedRealmId,
                        entry.Id,
                        CatalogAuthorityId,
                        Catalog.Version,
                        ProfileAuthorityExpectation.From(
                            _authorityProvider.GetCurrentAuthority())));
            RefreshCommittedIdentity();
            return ToCompatibilityResult(committed, request.RequestedRealmId);
        }

        private RealmIdentitySnapshot Snapshot(RealmIdentityStatus status, RealmId id, string code)
        {
            return new RealmIdentitySnapshot(status, id, Catalog == null ? string.Empty : Catalog.Version, code);
        }

        private RealmSelectionResult Result(RealmSelectionStatus status, RealmId requested, bool mutated, bool persisted, string code)
        {
            RefreshCommittedIdentity();
            RealmId committed = _committedIdentity.IsCommittedValid
                ? _committedIdentity.RealmId
                : RealmId.None;
            return new RealmSelectionResult(status, requested, committed, mutated, persisted, code);
        }

        private void RefreshCommittedIdentity()
        {
            if (_committedStore == null)
            {
                _committedIdentity = Snapshot(
                    RealmIdentityStatus.ProfileUnavailable,
                    RealmId.None,
                    "AL-REALM-COMMITTED-STORE-UNAVAILABLE");
                return;
            }

            _committedIdentity = _committedStore.GetCommittedRealm();
        }

        private void OnRealmSelectionCommitted(RealmSelectionCommittedEvent committedEvent)
        {
            // The event is a durable notification, not an alternate state source.
            // Always re-read the profile-bound authority so duplicate, stale, or
            // out-of-order delivery cannot switch the realm exposed to gameplay.
            RefreshCommittedIdentity();
        }

        private static string CanonicalizeTransactionId(
            RealmSelectionRequest request)
        {
            string supplied = request.TransactionId.Trim();
            if (supplied.Length == 37 &&
                supplied.StartsWith(
                    CanonicalTransactionPrefix,
                    StringComparison.Ordinal) &&
                IsLowerHex(supplied, CanonicalTransactionPrefix.Length))
            {
                return supplied;
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(supplied));
                var canonical = new StringBuilder(37);
                canonical.Append(CanonicalTransactionPrefix);
                for (int i = 0; i < 16; i++)
                {
                    canonical.Append(digest[i].ToString("x2"));
                }
                return canonical.ToString();
            }
        }

        private static bool IsLowerHex(string value, int offset)
        {
            for (int i = offset; i < value.Length; i++)
            {
                char character = value[i];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f'))
                {
                    return false;
                }
            }
            return true;
        }

        private static RealmSelectionResult ToCompatibilityResult(
            RealmSelectionCommitResult committed,
            RealmId requested)
        {
            if (committed == null)
            {
                return new RealmSelectionResult(
                    RealmSelectionStatus.SaveFailedPreviousPreserved,
                    requested,
                    RealmId.None,
                    false,
                    false,
                    "AL-REALM-COMMIT-RESULT-MISSING");
            }

            RealmSelectionStatus status;
            switch (committed.Status)
            {
                case RealmSelectionCommitStatus.Committed:
                    status = RealmSelectionStatus.Committed;
                    break;
                case RealmSelectionCommitStatus.AlreadyCommittedSameRealm:
                case RealmSelectionCommitStatus.DuplicateTransaction:
                    status = RealmSelectionStatus.AlreadyCommittedSameRealm;
                    break;
                case RealmSelectionCommitStatus.RejectedDifferentRealm:
                case RealmSelectionCommitStatus.TransactionMismatch:
                    status = RealmSelectionStatus.RejectedDifferentRealm;
                    break;
                case RealmSelectionCommitStatus.InvalidTransaction:
                    status = RealmSelectionStatus.InvalidTransaction;
                    break;
                case RealmSelectionCommitStatus.InvalidRealm:
                    status = RealmSelectionStatus.InvalidRealm;
                    break;
                case RealmSelectionCommitStatus.RealmDefinitionUnavailable:
                    status = RealmSelectionStatus.RealmDefinitionUnavailable;
                    break;
                case RealmSelectionCommitStatus.ProfileUnavailable:
                case RealmSelectionCommitStatus.ProfileNotWritable:
                case RealmSelectionCommitStatus.ForwardSchemaReadOnly:
                case RealmSelectionCommitStatus.RecoveryRequired:
                    status = RealmSelectionStatus.ProfileUnavailable;
                    break;
                default:
                    status = RealmSelectionStatus.SaveFailedPreviousPreserved;
                    break;
            }

            return new RealmSelectionResult(
                status,
                requested,
                committed.CommittedRealmId,
                committed.MutationOccurred,
                committed.PersistedAndVerified,
                committed.TechnicalCode);
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
