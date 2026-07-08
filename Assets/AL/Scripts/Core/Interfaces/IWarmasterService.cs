using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface IWarmasterService
    {
        WarmasterState GetState();
        void UnlockSet(string setId);
        void EquipSet(string setId);
        bool PurchasePiece(string pieceId, int warzoneCreditCost);
        int GetPurchasedPieceCount();
        int GetRequiredPieceCount();
        bool IsTrueWarmaster();
    }
}
