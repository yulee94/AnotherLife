using AL.ChampionMode.Death;
using AL.Core;
using AL.Core.Interfaces;

namespace AL.Services.Local
{
    public static class DeathPenaltySaveAuthority
    {
        public static bool CanCommit(ISaveGameService saveGameService) =>
            saveGameService?.CurrentSave != null &&
            saveGameService is IProfileBoundDeathPenaltyCandidateStore;

        public static DeathPenaltyCommitRequest CreateInnerRealmRequest(
            string operationId,
            string deathEventId,
            RealmId realmId)
        {
            return new DeathPenaltyCommitRequest(
                operationId,
                deathEventId,
                DeathPenaltyIds.InnerCombatSessionId,
                DeathPenaltyIds.InnerEncounterAttemptId,
                DeathPenaltyIds.InstanceId(realmId.ToString()));
        }

        public static DeathPenaltyCommitResult TryCommit(
            ISaveGameService saveGameService,
            DeathPenaltyCommitRequest request)
        {
            if (saveGameService?.CurrentSave == null ||
                !(saveGameService is IProfileBoundDeathPenaltyCandidateStore store))
            {
                return DeathPenaltyTransaction.Reject(
                    DeathPenaltyCommitStatus.RejectedReadOnly,
                    saveGameService?.CurrentSave,
                    DeathPenaltyCommitCodes.ReadOnly);
            }

            return store.TryCommitProfileBoundDeathPenalty(request);
        }
    }
}
