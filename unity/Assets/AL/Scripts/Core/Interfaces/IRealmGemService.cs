using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;
using AL.RealmGems;

namespace AL.Core.Interfaces
{
    public interface IRealmGemService
    {
        IEnumerable<RealmGemState> GetRealmGems();
        WishgateState GetWishgateState();
        bool PickUpGem(string gemId, string carrierId);
        RealmGemMutationResult PickUpGem(RealmGemMutationRequest request);
        void DropGem(string gemId);
        RealmGemMutationResult DropGem(RealmGemMutationRequest request);
        void ReturnGemHome(string gemId);
        RealmGemMutationResult ReturnGemHome(RealmGemMutationRequest request);
        void MarkWishgateEarned(string reason);
        void ChooseWishReward(string rewardId);
        WishgateRewardResult ApplyWishgateReward(WishgateRewardRequest request);
    }
}

