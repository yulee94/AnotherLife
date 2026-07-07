using AL.Core;

namespace AL.Core.Interfaces
{
    public interface INotificationService
    {
        void ShowMessage(string message);
        void ShowError(string error);
        void ShowResourceGain(ResourceType type, long amount);
    }
}
