using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface IBattleSimulator
    {
        BattleReport Simulate(BattleRequest request);
    }
}
