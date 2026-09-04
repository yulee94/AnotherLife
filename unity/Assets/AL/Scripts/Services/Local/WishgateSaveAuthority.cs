using System.IO;
using AL.Core.Interfaces;
using AL.RealmSelection;
using UnityEngine;

namespace AL.Services.Local
{
    public static class WishgateSaveAuthority
    {
        public static bool CanCommit(ISaveGameService saveGameService) =>
            saveGameService?.CurrentSave != null &&
            saveGameService is IProfileBoundWishgateCandidateStore;

        public static WishgateCommitResult TryCommit(
            ISaveGameService saveGameService,
            WishgateCommitRequest request)
        {
            return TryCommit(saveGameService, request, TryCreateProductionDependencies());
        }

        internal static WishgateCommitResult TryCommit(
            ISaveGameService saveGameService,
            WishgateCommitRequest request,
            WishgateDurableDependencies dependencies)
        {
            if (saveGameService?.CurrentSave == null ||
                !(saveGameService is IProfileBoundWishgateCandidateStore store))
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedReadOnly,
                    saveGameService?.CurrentSave,
                    WishgateCommitCodes.ReadOnly);
            }

            if (dependencies == null || !dependencies.IsComplete)
            {
                return WishgateDurableTransaction.Reject(
                    WishgateCommitStatus.RejectedUnavailable,
                    saveGameService.CurrentSave,
                    WishgateCommitCodes.CatalogUnavailable);
            }

            return store.TryCommitProfileBoundWishgate(request, dependencies);
        }

        internal static WishgateDurableDependencies TryCreateProductionDependencies()
        {
            RealmGemCatalogSnapshot gems = TryLoadRealmGemCatalog();
            if (gems == null)
            {
                return null;
            }

            return new WishgateDurableDependencies(
                gems,
                new WishgateSystemClock(),
                new WishgateCatalogTransactionAuthority(),
                new IdentityOnlyWishgateRewardApplicator());
        }

        internal static RealmGemCatalogSnapshot TryLoadRealmGemCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "AL",
                "StreamingAssets",
                "GameData",
                "realm_specialized.v1.json");
            if (!File.Exists(path))
            {
                return null;
            }

            RealmCatalogLoadResult parsed = RealmCatalogRuntime.Parse(File.ReadAllText(path));
            if (!parsed.IsSuccess)
            {
                return null;
            }

            RealmGemCatalogBuildResult built = RealmGemCatalogResolver.Build(parsed.Snapshot);
            return built.IsReady ? built.Snapshot : null;
        }
    }
}
