using System;
using AL.Core;

namespace AL.RealmSelection
{
    public enum RealmCharacterEligibility
    {
        Allowed = 0,
        AccountRealmUnavailable = 1,
        InvalidRequestedRealm = 2,
        RejectedDifferentRealm = 3
    }

    public static class RealmCharacterConstraint
    {
        public static RealmCharacterEligibility Evaluate(RealmIdentitySnapshot accountIdentity, RealmId requestedRealmId)
        {
            if (!accountIdentity.IsCommittedValid)
                return RealmCharacterEligibility.AccountRealmUnavailable;
            if (requestedRealmId == RealmId.None || !Enum.IsDefined(typeof(RealmId), requestedRealmId))
                return RealmCharacterEligibility.InvalidRequestedRealm;
            return requestedRealmId == accountIdentity.RealmId
                ? RealmCharacterEligibility.Allowed
                : RealmCharacterEligibility.RejectedDifferentRealm;
        }
    }
}
