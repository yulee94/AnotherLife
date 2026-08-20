using System;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.FirstUserIdentity;

namespace AL.ChampionMode.Quests
{
    /// <summary>
    /// Lordship is the persisted covenant mark. t_c8823dae consumes this flag.
    /// Slot is existing ChampionCustomization.LastResultId — no new SaveGameData field.
    /// Old saves without a ch01_&lt;realm&gt; mark stay locked.
    /// </summary>
    public static class ProofOfWorthLordship
    {
        public static bool IsGranted(string lastResultId)
        {
            return ProofOfWorthIds.IsRealmVariantId(lastResultId);
        }

        public static bool IsGranted(MvpLoopSnapshot snapshot)
        {
            return snapshot != null && IsGranted(snapshot.LastResultId);
        }

        public static bool IsGranted(SaveGameData save)
        {
            return IsGranted(MvpLoopSaveCodec.Read(save));
        }

        public static string ResolveMarkId(RealmId realm)
        {
            return ProofOfWorthIds.ResolveRealmVariantId(realm);
        }

        public static bool TryWriteMark(SaveGameData save, string markId)
        {
            if (save == null || !ProofOfWorthIds.IsRealmVariantId(markId))
            {
                return false;
            }

            save.ChampionCustomization ??= new ChampionCustomizationState();
            if (string.Equals(save.ChampionCustomization.LastResultId, markId, StringComparison.Ordinal))
            {
                return true;
            }

            save.ChampionCustomization.LastResultId = markId;
            return true;
        }

        public static MvpLoopCommitResult TryPersist(
            ISaveGameService saveGameService,
            RealmId realm)
        {
            string markId = ResolveMarkId(realm);
            if (saveGameService == null || !ProofOfWorthIds.IsRealmVariantId(markId))
            {
                return new MvpLoopCommitResult(false, false, "AL-C1-LORDSHIP-REALM-MISSING");
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(saveGameService.CurrentSave);
            if (!snapshot.ClassFamily.HasValue ||
                !FirstUserIdentityDerivation.IsSupportedRealm(realm))
            {
                return new MvpLoopCommitResult(false, false, "AL-C1-LORDSHIP-IDENTITY-MISSING");
            }

            MvpLoopCommitResult commit = MvpLoopSaveAuthority.TryCommit(
                saveGameService,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    realm,
                    snapshot.ClassFamily.Value,
                    snapshot.IdentityConfirmed,
                    markId,
                    snapshot.LastBuildId,
                    snapshot.LastBuildLevel));
            return commit ?? new MvpLoopCommitResult(false, false, "AL-C1-LORDSHIP-PERSIST-FAILED");
        }
    }
}
