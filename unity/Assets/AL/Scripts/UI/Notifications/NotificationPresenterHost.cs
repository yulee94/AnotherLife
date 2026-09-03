using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Notifications;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.UI.Notifications
{
    [DefaultExecutionOrder(230)]
    [DisallowMultipleComponent]
    public sealed class NotificationPresenterHost : MonoBehaviour,
        INotificationPresenter,
        INotificationQueueObserver
    {
        public const string RuntimeHostName = "AL_NotificationPresenterHost";
        public const string RuntimePresenterId = "al_notification_ui_presenter";

        private static readonly string[] SupportedSceneNames =
        {
            "Boot",
            "RealmSelection",
            "CharacterCreation",
            "ChampionArena",
            "Kingdom"
        };

        private readonly List<NotificationPresentationPlan> pendingPlans =
            new List<NotificationPresentationPlan>();
        private INotificationService service;
        private INotificationPresentationContentResolver contentResolver;
        private INotificationAccessibilityAnnouncer announcer;
        private NotificationPresenterRegistrationToken presenterToken;
        private NotificationQueueObserverRegistrationToken observerToken;
        private NotificationPresentationPlan activePlan;
        private float nextRefreshAt;

        public string PresenterId => RuntimePresenterId;
        public NotificationPresenterOverlay Overlay { get; private set; }
        public string ActiveNotificationInstanceId =>
            activePlan?.Record.NotificationInstanceId;
        public bool IsRegistered => presenterToken != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        public static NotificationPresenterHost EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || !IsSupportedScene(scene.name) ||
                !ServiceLocator.TryGet(out INotificationService notificationService) ||
                !ServiceLocator.TryGet(
                    out INotificationPresentationContentResolver presentationResolver))
            {
                return null;
            }

            NotificationPresenterHost existing = ElectSceneHost(scene);
            if (existing == null)
            {
                var root = new GameObject(RuntimeHostName);
                SceneManager.MoveGameObjectToScene(root, scene);
                existing = root.AddComponent<NotificationPresenterHost>();
            }

            if (!existing.gameObject.activeSelf)
            {
                existing.gameObject.SetActive(true);
            }

            existing.enabled = true;

            ServiceLocator.TryGet(out INotificationAccessibilityAnnouncer accessibilityAnnouncer);
            existing.Bind(
                notificationService,
                presentationResolver,
                accessibilityAnnouncer);
            return existing;
        }

        public void Bind(
            INotificationService notificationService,
            INotificationPresentationContentResolver presentationResolver,
            INotificationAccessibilityAnnouncer accessibilityAnnouncer = null)
        {
            if (notificationService == null)
            {
                throw new ArgumentNullException(nameof(notificationService));
            }

            if (presentationResolver == null)
            {
                throw new ArgumentNullException(nameof(presentationResolver));
            }

            if (ReferenceEquals(service, notificationService) &&
                ReferenceEquals(contentResolver, presentationResolver) &&
                ReferenceEquals(announcer, accessibilityAnnouncer) &&
                IsRegistered)
            {
                return;
            }

            Unregister(clearDependencies: false);
            service = notificationService;
            contentResolver = presentationResolver;
            announcer = accessibilityAnnouncer;
            EnsureOverlay();

            NotificationQueueObserverRegistrationResult observerRegistration =
                service.RegisterObserver(this);
            if (observerRegistration == null ||
                observerRegistration.Status !=
                NotificationQueueObserverRegistrationStatus.Registered)
            {
                return;
            }

            observerToken = observerRegistration.Token;
            NotificationPresenterRegistrationResult presenterRegistration =
                service.RegisterPresenter(
                    this,
                    new NotificationPresenterCapabilities(new[]
                    {
                        NotificationChannel.Toast,
                        NotificationChannel.Banner,
                        NotificationChannel.Acknowledgement
                    }));
            if (presenterRegistration == null ||
                presenterRegistration.Status !=
                NotificationPresenterRegistrationStatus.Registered)
            {
                service.UnregisterObserver(observerToken);
                observerToken = null;
                return;
            }

            presenterToken = presenterRegistration.Token;
        }

        public NotificationPresenterOfferResult Offer(NotificationPresentationOffer offer)
        {
            if (pendingPlans.Count >= 3)
            {
                return new NotificationPresenterOfferResult(
                    NotificationPresenterOfferStatus.RejectedUnavailable,
                    NotificationPresentationPlanner.InvalidOfferDiagnostic);
            }

            if (!NotificationPresentationPlanner.TryCreate(
                    offer,
                    contentResolver,
                    out NotificationPresentationPlan plan,
                    out string failureCode) ||
                IsTracked(plan?.Record.NotificationInstanceId))
            {
                return new NotificationPresenterOfferResult(
                    NotificationPresenterOfferStatus.RejectedUnsupported,
                    failureCode ?? NotificationPresentationPlanner.InvalidOfferDiagnostic);
            }

            pendingPlans.Add(plan);
            if (plan.BlocksBackground && activePlan != null &&
                !activePlan.BlocksBackground)
            {
                ClearActivePresentation();
            }

            return new NotificationPresenterOfferResult(
                NotificationPresenterOfferStatus.AcceptedPendingPresentation,
                null);
        }

        public void OnQueueChanged(NotificationQueueSnapshot snapshot)
        {
            if (snapshot?.Records == null)
            {
                return;
            }

            pendingPlans.RemoveAll(plan => IsComplete(snapshot, plan.Record.NotificationInstanceId));
            if (activePlan != null &&
                IsComplete(snapshot, activePlan.Record.NotificationInstanceId))
            {
                ClearActivePresentation();
            }
        }

        public void TickForTests()
        {
            ProcessPendingPresentation(refreshQueue: false);
        }

        private void Awake()
        {
            EnsureOverlay();
        }

        private void OnEnable()
        {
            if (!IsRegistered && service != null && contentResolver != null)
            {
                Bind(service, contentResolver, announcer);
            }
        }

        private void Update()
        {
            bool refresh = Time.unscaledTime >= nextRefreshAt;
            if (refresh)
            {
                nextRefreshAt = Time.unscaledTime + 0.25f;
            }

            ProcessPendingPresentation(refresh);
        }

        private void OnDisable()
        {
            Unregister(clearDependencies: false);
        }

        private void OnDestroy()
        {
            Unregister(clearDependencies: true);
        }

        private void ProcessPendingPresentation(bool refreshQueue)
        {
            if (!IsRegistered)
            {
                return;
            }

            if (refreshQueue)
            {
                service.Refresh();
            }

            if (activePlan != null || pendingPlans.Count == 0)
            {
                return;
            }

            NotificationPresentationPlan next = pendingPlans
                .OrderByDescending(plan => plan.BlocksBackground)
                .ThenByDescending(plan => plan.Record.Definition.Severity)
                .ThenByDescending(plan => plan.Record.Definition.Priority)
                .ThenBy(plan => plan.Record.SessionSequence)
                .First();
            pendingPlans.Remove(next);
            activePlan = next;
            Overlay.Show(next, HandleAction);

            NotificationReceiptUpdateResult confirmation = service.ConfirmPresented(
                presenterToken,
                next.Record.NotificationInstanceId);
            if (confirmation == null ||
                (confirmation.Status != NotificationReceiptUpdateStatus.Applied &&
                 confirmation.Status != NotificationReceiptUpdateStatus.NoChange))
            {
                ClearActivePresentation();
                service.ReportDeliveryFailure(
                    presenterToken,
                    next.Record.NotificationInstanceId,
                    NotificationPresentationPlanner.InvalidOfferDiagnostic);
                return;
            }

            if (announcer != null)
            {
                try
                {
                    announcer.Announce(next.Content.AccessibilityAnnouncement);
                }
                catch
                {
                    // The visual presentation remains valid when the platform has no announcer.
                }
            }
        }

        private void HandleAction()
        {
            NotificationPresentationPlan completed = activePlan;
            if (completed == null || presenterToken == null)
            {
                return;
            }

            ClearActivePresentation();
            NotificationReceiptUpdateResult result;
            if (completed.Action == NotificationPresentationAction.Acknowledge)
            {
                result = service.Acknowledge(
                    presenterToken,
                    completed.Record.NotificationInstanceId);
            }
            else if (completed.Action == NotificationPresentationAction.Dismiss)
            {
                result = service.Dismiss(
                    presenterToken,
                    completed.Record.NotificationInstanceId);
            }
            else
            {
                return;
            }

            if (result == null ||
                (result.Status != NotificationReceiptUpdateStatus.Applied &&
                 result.Status != NotificationReceiptUpdateStatus.NoChange))
            {
                pendingPlans.Add(completed);
            }
        }

        private void EnsureOverlay()
        {
            if (Overlay == null)
            {
                Overlay = NotificationPresenterOverlay.Mount(transform);
            }
        }

        private void ClearActivePresentation()
        {
            activePlan = null;
            Overlay?.Hide();
        }

        private bool IsTracked(string notificationInstanceId)
        {
            if (string.Equals(
                    ActiveNotificationInstanceId,
                    notificationInstanceId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            return pendingPlans.Any(plan => string.Equals(
                plan.Record.NotificationInstanceId,
                notificationInstanceId,
                StringComparison.Ordinal));
        }

        private void Unregister(bool clearDependencies)
        {
            if (service != null && presenterToken != null)
            {
                service.UnregisterPresenter(presenterToken);
            }

            presenterToken = null;
            if (service != null && observerToken != null)
            {
                service.UnregisterObserver(observerToken);
            }

            observerToken = null;
            pendingPlans.Clear();
            ClearActivePresentation();
            if (clearDependencies)
            {
                service = null;
                contentResolver = null;
                announcer = null;
            }
        }

        private static bool IsComplete(
            NotificationQueueSnapshot snapshot,
            string notificationInstanceId)
        {
            NotificationQueueRecordSnapshot record = snapshot.Records.FirstOrDefault(item =>
                string.Equals(
                    item.NotificationInstanceId,
                    notificationInstanceId,
                    StringComparison.Ordinal));
            if (record == null)
            {
                return true;
            }

            NotificationDeliveryState state = record.Receipt.State;
            return state == NotificationDeliveryState.Acknowledged ||
                   state == NotificationDeliveryState.Dismissed ||
                   state == NotificationDeliveryState.Expired ||
                   state == NotificationDeliveryState.Superseded;
        }

        private static bool IsSupportedScene(string sceneName)
        {
            return SupportedSceneNames.Contains(sceneName, StringComparer.Ordinal);
        }

        private static NotificationPresenterHost ElectSceneHost(Scene scene)
        {
            NotificationPresenterHost winner = null;
            NotificationPresenterHost[] hosts =
                Resources.FindObjectsOfTypeAll<NotificationPresenterHost>();
            for (int index = 0; index < hosts.Length; index++)
            {
                NotificationPresenterHost candidate = hosts[index];
                if (candidate == null || candidate.gameObject.scene != scene)
                {
                    continue;
                }

                if (winner == null)
                {
                    winner = candidate;
                }
            }

            return winner;
        }
    }
}
