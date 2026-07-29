using AL.Core;
using AL.Core.Interfaces.Notifications;

namespace AL.Core.Interfaces
{
    public interface INotificationService
    {
        NotificationEnqueueResult Enqueue(NotificationRequest request);

        NotificationPresenterRegistrationResult RegisterPresenter(
            INotificationPresenter presenter,
            NotificationPresenterCapabilities capabilities);

        NotificationPresenterUnregistrationStatus UnregisterPresenter(
            NotificationPresenterRegistrationToken token);

        NotificationReceiptUpdateResult ConfirmPresented(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId);

        NotificationReceiptUpdateResult ReportDeliveryFailure(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            string failureCode);

        NotificationReceiptUpdateResult Acknowledge(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId);

        NotificationReceiptUpdateResult Dismiss(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId);

        NotificationActionResult InvokeAction(
            NotificationPresenterRegistrationToken token,
            string notificationInstanceId,
            NotificationActionInvocation invocation);

        NotificationQueueObserverRegistrationResult RegisterObserver(
            INotificationQueueObserver observer);

        NotificationQueueObserverUnregistrationStatus UnregisterObserver(
            NotificationQueueObserverRegistrationToken token);

        NotificationQueueSnapshot GetSnapshot();
        NotificationDeliveryReceipt GetReceipt(string notificationInstanceId);
        void Refresh();

        [System.Obsolete("Compatibility-only raw notification wrapper. Use Enqueue(NotificationRequest).")]
        void ShowMessage(string message);

        [System.Obsolete("Compatibility-only raw notification wrapper. Use Enqueue(NotificationRequest).")]
        void ShowError(string error);

        [System.Obsolete("Compatibility-only raw notification wrapper. Use Enqueue(NotificationRequest).")]
        void ShowResourceGain(ResourceType type, long amount);
    }
}
