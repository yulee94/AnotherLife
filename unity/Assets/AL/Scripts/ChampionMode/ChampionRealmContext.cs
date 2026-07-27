using System;
using AL.Core;
using AL.Core.Interfaces;
using AL.RealmSelection;

namespace AL.ChampionMode
{
    public enum ChampionRealmContextStatus
    {
        Available = 0,
        ServiceUnavailable = 1,
        IdentityUnavailable = 2
    }

    public readonly struct ChampionRealmContextResult
    {
        public ChampionRealmContextResult(
            ChampionRealmContextStatus status,
            RealmId realmId,
            RealmIdentityStatus identityStatus,
            string technicalCode)
        {
            Status = status;
            RealmId = realmId;
            IdentityStatus = identityStatus;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public ChampionRealmContextStatus Status { get; }
        public RealmId RealmId { get; }
        public RealmIdentityStatus IdentityStatus { get; }
        public string TechnicalCode { get; }
        public bool IsAvailable => Status == ChampionRealmContextStatus.Available;
    }

    public static class ChampionRealmContext
    {
        public const string ReadyCode = "AL-CHAMPION-REALM-CONTEXT-READY";
        public const string ServiceUnavailableCode = "AL-CHAMPION-REALM-SERVICE-UNAVAILABLE";
        public const string IdentityUnavailableCode = "AL-CHAMPION-REALM-IDENTITY-UNAVAILABLE";

        public static ChampionRealmContextResult ResolveRegistered()
        {
            return ServiceLocator.TryGet(out IRealmService realmService)
                ? Resolve(realmService)
                : ServiceUnavailable();
        }

        public static ChampionRealmContextResult Resolve(IRealmService realmService)
        {
            if (realmService == null)
            {
                return ServiceUnavailable();
            }

            RealmIdentitySnapshot identity;
            try
            {
                identity = realmService.Identity;
            }
            catch (Exception)
            {
                return ServiceUnavailable();
            }

            if (!identity.IsCommittedValid)
            {
                return new ChampionRealmContextResult(
                    ChampionRealmContextStatus.IdentityUnavailable,
                    RealmId.None,
                    identity.Status,
                    IdentityUnavailableCode);
            }

            return new ChampionRealmContextResult(
                ChampionRealmContextStatus.Available,
                identity.RealmId,
                identity.Status,
                ReadyCode);
        }

        public static RealmId Normalize(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Stonehold:
                case RealmId.Eldergrove:
                case RealmId.Crownlands:
                case RealmId.Umbral:
                    return realmId;
                default:
                    return RealmId.None;
            }
        }

        private static ChampionRealmContextResult ServiceUnavailable()
        {
            return new ChampionRealmContextResult(
                ChampionRealmContextStatus.ServiceUnavailable,
                RealmId.None,
                RealmIdentityStatus.ProfileUnavailable,
                ServiceUnavailableCode);
        }
    }
}
