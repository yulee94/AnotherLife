using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface IWarmasterService
    {
        WarmasterState GetState();
        void UnlockSet(string setId);
        void EquipSet(string setId);
    }
}
