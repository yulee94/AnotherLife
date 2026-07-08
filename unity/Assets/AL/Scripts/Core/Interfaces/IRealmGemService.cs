using System.Collections.Generic;
using AL.Core;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface IRealmGemService
    {
        IEnumerable<RealmGemState> GetRealmGems();
        WishgateState GetWishgateState();
        bool PickUpGem(string gemId, string carrierId);
        void DropGem(string gemId);
        void ReturnGemHome(string gemId);
        void MarkWishgateEarned(string reason);
        void ChooseWishReward(string rewardId);
    }
}

