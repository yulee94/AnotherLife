namespace AL.Core.Interfaces
{
    public interface IWarzoneCreditService
    {
        int GetCredits();
        void AddCredits(int amount);
        bool SpendCredits(int amount);
    }

    public interface IWarzoneCreditIntegrityService : IWarzoneCreditService
    {
        EconomyBalanceReadResult ReadCredits();
        EconomyMutationResult TryAddCredits(int amount);
        EconomyMutationResult TrySpendCredits(int amount);
    }
}
