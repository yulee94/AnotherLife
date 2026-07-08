namespace AL.Core.Interfaces
{
    public interface IWarzoneCreditService
    {
        int GetCredits();
        void AddCredits(int amount);
        bool SpendCredits(int amount);
    }
}
