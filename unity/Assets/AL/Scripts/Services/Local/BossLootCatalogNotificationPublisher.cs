using System;
using AL.Core.Interfaces;
using AL.Core.Interfaces.Notifications;
using AL.Data.Runtime;

namespace AL.Services.Local
{
    public static class BossLootCatalogNotificationPublisher
    {
        public const string CommittedDefinitionId = "al_notify_reward_committed";
        public const string FailedDefinitionId = "al_notify_reward_failed";
        public const string SourceSystemId = "al_source_boss_loot";
        public const string SummaryParameterName = "reward_summary";
        public const string CommittedSummaryReference = "notification.reward.summary.committed";
        public const string FailedSummaryReference = "notification.reward.summary.failed";

        public static NotificationEnqueueResult PublishCommitted(
            INotificationService notifications,
            BossLootRequest request,
            BossLootResult result,
            DateTime occurredAtUtc)
        {
            return Publish(
                notifications,
                CommittedDefinitionId,
                CommittedSummaryReference,
                request,
                result,
                occurredAtUtc);
        }

        public static NotificationEnqueueResult PublishFailed(
            INotificationService notifications,
            BossLootRequest request,
            BossLootResult result,
            DateTime occurredAtUtc)
        {
            return Publish(
                notifications,
                FailedDefinitionId,
                FailedSummaryReference,
                request,
                result,
                occurredAtUtc);
        }

        private static NotificationEnqueueResult Publish(
            INotificationService notifications,
            string definitionId,
            string summaryReference,
            BossLootRequest request,
            BossLootResult result,
            DateTime occurredAtUtc)
        {
            if (notifications == null)
            {
                return null;
            }

            return notifications.Enqueue(
                new NotificationRequest(
                    definitionId,
                    SourceSystemId,
                    BuildCorrelation(request, result),
                    occurredAtUtc,
                    new[]
                    {
                        new NotificationParameter(
                            SummaryParameterName,
                            NotificationParameterValue.FromLocalizationReference(summaryReference))
                    },
                    null,
                    null,
                    null));
        }

        public static string BuildCorrelation(BossLootRequest request, BossLootResult result)
        {
            string encounterId = request != null && !string.IsNullOrWhiteSpace(request.EncounterId)
                ? request.EncounterId
                : result != null ? result.EncounterId : string.Empty;
            string rewardResultId = request != null && !string.IsNullOrWhiteSpace(request.RewardResultId)
                ? request.RewardResultId
                : result != null ? result.RewardResultId : string.Empty;
            return "al_boss_loot:" + (encounterId ?? string.Empty) + ":" + (rewardResultId ?? string.Empty);
        }
    }
}
