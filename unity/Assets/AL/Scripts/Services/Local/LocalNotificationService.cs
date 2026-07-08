using AL.Core;
using AL.Core.Interfaces;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalNotificationService : INotificationService
    {
        public void ShowMessage(string message)
        {
            Debug.Log($"[Notification] {message}");
        }

        public void ShowError(string error)
        {
            Debug.LogWarning($"[Notification] {error}");
        }

        public void ShowResourceGain(ResourceType type, long amount)
        {
            Debug.Log($"[Notification] +{amount} {type}");
        }
    }
}

