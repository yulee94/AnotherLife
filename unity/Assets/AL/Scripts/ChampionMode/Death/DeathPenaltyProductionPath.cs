using AL.ChampionMode.Control;
using AL.Core.Interfaces;

namespace AL.ChampionMode.Death
{
    /// <summary>
    /// Production death seam: persist the profile-bound penalty first, then
    /// stand up only when that commit (or exact replay) authorizes revival.
    /// </summary>
    public static class DeathPenaltyProductionPath
    {
        public static bool TryCommitPenaltyThenApply(
            ISaveGameService saveGameService,
            DeathPenaltyCommitRequest request,
            InnerRealmDeathRespawnPlan plan,
            ChampionCombat combat,
            ChampionController controller,
            out DeathPenaltyCommitResult penalty)
        {
            penalty = AL.Services.Local.DeathPenaltySaveAuthority.TryCommit(
                saveGameService,
                request);
            if (penalty == null || !penalty.AllowsRevive)
            {
                return false;
            }

            return InnerRealmDeathRespawnApplier.TryApply(plan, combat, controller);
        }
    }
}
