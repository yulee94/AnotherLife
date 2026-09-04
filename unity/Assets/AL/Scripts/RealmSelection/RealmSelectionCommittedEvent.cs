using System;
using AL.Core;
using UnityEngine;

namespace AL.RealmSelection
{
    public sealed class RealmSelectionCommittedEvent
    {
        public RealmSelectionCommittedEvent(
            string transactionId,
            RealmId previousRealmId,
            RealmId newRealmId,
            string catalogVersion,
            string eventId,
            string profileGeneration)
        {
            TransactionId = transactionId ?? string.Empty;
            PreviousRealmId = previousRealmId;
            NewRealmId = newRealmId;
            CatalogVersion = catalogVersion ?? string.Empty;
            EventId = eventId ?? string.Empty;
            ProfileGeneration = profileGeneration ?? string.Empty;
        }

        public string TransactionId { get; }
        public RealmId PreviousRealmId { get; }
        public RealmId NewRealmId { get; }
        public string CatalogVersion { get; }
        public string EventId { get; }
        public string ProfileGeneration { get; }
    }

    public static class RealmSelectionEventDelivery
    {
        public static event Action<RealmSelectionCommittedEvent> Committed;

        public static void ResetSubscribers()
        {
            Committed = null;
        }

        public static int Publish(RealmSelectionCommittedEvent evt)
        {
            if (evt == null)
            {
                return 0;
            }

            Delegate[] handlers = Committed?.GetInvocationList();
            if (handlers == null || handlers.Length == 0)
            {
                return 0;
            }

            int delivered = 0;
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    ((Action<RealmSelectionCommittedEvent>)handlers[i]).Invoke(evt);
                    delivered++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("AL-REALM-EVENT-HANDLER " + ex.GetType().Name);
                }
            }

            return delivered;
        }
    }
}
