using System;
using System.Text;
using AL.Core.Interfaces.Notifications;

namespace AL.UI.Notifications
{
    public enum NotificationPresentationContentStatus
    {
        Resolved = 0,
        MissingContentKey = 1,
        InvalidPlaceholderSchema = 2,
        UnsafeRenderedContent = 3,
        UnsupportedLocale = 4,
        ContentCatalogUnavailable = 5
    }

    public enum NotificationPresentationAction
    {
        None = 0,
        Acknowledge = 1,
        Dismiss = 2
    }

    public enum NotificationAccessibilityAnnouncementStatus
    {
        Announced = 0,
        Unsupported = 1,
        Failed = 2
    }

    public sealed class NotificationPresentationContent
    {
        public NotificationPresentationContent(
            string title,
            string body,
            string acknowledgementLabel,
            string dismissLabel,
            string accessibilityAnnouncement)
        {
            Title = title;
            Body = body;
            AcknowledgementLabel = acknowledgementLabel;
            DismissLabel = dismissLabel;
            AccessibilityAnnouncement = accessibilityAnnouncement;
        }

        public string Title { get; }
        public string Body { get; }
        public string AcknowledgementLabel { get; }
        public string DismissLabel { get; }
        public string AccessibilityAnnouncement { get; }
    }

    public sealed class NotificationPresentationContentResolution
    {
        public NotificationPresentationContentResolution(
            NotificationPresentationContentStatus status,
            NotificationPresentationContent content,
            string diagnosticCode)
        {
            Status = status;
            Content = content;
            DiagnosticCode = diagnosticCode;
        }

        public NotificationPresentationContentStatus Status { get; }
        public NotificationPresentationContent Content { get; }
        public string DiagnosticCode { get; }
    }

    public interface INotificationPresentationContentResolver
    {
        NotificationPresentationContentResolution ResolveContent(
            NotificationQueueRecordSnapshot record);
    }

    public interface INotificationPresentationLocalizationResolver
    {
        bool IsAvailable { get; }
        bool TryResolve(string localizationReference, out string text);
    }

    public interface INotificationAccessibilityAnnouncer
    {
        NotificationAccessibilityAnnouncementStatus Announce(string announcement);
    }

    public sealed class NotificationPresentationPlan
    {
        internal NotificationPresentationPlan(
            NotificationQueueRecordSnapshot record,
            NotificationPresentationContent content,
            NotificationPresentationAction action,
            string actionLabel,
            string severityMarker,
            bool blocksBackground,
            bool movesFocus)
        {
            Record = record;
            Content = content;
            Action = action;
            ActionLabel = actionLabel;
            SeverityMarker = severityMarker;
            BlocksBackground = blocksBackground;
            MovesFocus = movesFocus;
        }

        public NotificationQueueRecordSnapshot Record { get; }
        public NotificationPresentationContent Content { get; }
        public NotificationPresentationAction Action { get; }
        public string ActionLabel { get; }
        public string SeverityMarker { get; }
        public bool BlocksBackground { get; }
        public bool MovesFocus { get; }
    }

    public static class NotificationPresentationPlanner
    {
        public const string InvalidOfferDiagnostic = "AL-NTF-PRESENTER-OFFER";
        public const string ContentDiagnostic = "AL-NTF-CONTENT-MISSING";
        public const string LifetimeDiagnostic = "AL-NTF-PRESENTER-LIFETIME";

        private const int MaximumTitleUtf8Bytes = 256;
        private const int MaximumBodyUtf8Bytes = 2048;
        private const int MaximumActionUtf8Bytes = 128;
        private const int MaximumAnnouncementUtf8Bytes = 2304;

        public static bool TryCreate(
            NotificationPresentationOffer offer,
            INotificationPresentationContentResolver contentResolver,
            out NotificationPresentationPlan plan,
            out string failureCode)
        {
            plan = null;
            failureCode = InvalidOfferDiagnostic;
            NotificationQueueRecordSnapshot record = offer?.Record;
            if (record?.Definition == null || record.Request == null || record.Receipt == null ||
                record.Channel == NotificationChannel.HistoryOnly ||
                (record.Receipt.State != NotificationDeliveryState.PendingPresenter &&
                 record.Receipt.State != NotificationDeliveryState.DeliveryFailed))
            {
                return false;
            }

            NotificationPresentationContentResolution resolution;
            try
            {
                resolution = contentResolver?.ResolveContent(record);
            }
            catch
            {
                resolution = null;
            }

            if (resolution == null ||
                resolution.Status != NotificationPresentationContentStatus.Resolved ||
                resolution.Content == null)
            {
                failureCode = SafeDiagnostic(resolution?.DiagnosticCode, ContentDiagnostic);
                return false;
            }

            NotificationPresentationContent content = resolution.Content;
            if (!IsSafeText(content.Title, MaximumTitleUtf8Bytes, allowLineBreaks: false) ||
                !IsSafeText(content.Body, MaximumBodyUtf8Bytes, allowLineBreaks: true) ||
                !IsSafeText(
                    content.AccessibilityAnnouncement,
                    MaximumAnnouncementUtf8Bytes,
                    allowLineBreaks: true))
            {
                failureCode = ContentDiagnostic;
                return false;
            }

            NotificationPresentationAction action = NotificationPresentationAction.None;
            string actionLabel = string.Empty;
            bool blocksBackground = false;
            bool movesFocus = false;
            switch (record.Definition.AcknowledgementPolicy)
            {
                case NotificationAcknowledgementPolicy.Required:
                    if (record.Channel != NotificationChannel.Acknowledgement ||
                        !IsSafeText(
                            content.AcknowledgementLabel,
                            MaximumActionUtf8Bytes,
                            allowLineBreaks: false))
                    {
                        return false;
                    }

                    action = NotificationPresentationAction.Acknowledge;
                    actionLabel = content.AcknowledgementLabel;
                    blocksBackground = true;
                    movesFocus = true;
                    break;
                case NotificationAcknowledgementPolicy.Dismissible:
                    if (!IsSafeText(
                            content.DismissLabel,
                            MaximumActionUtf8Bytes,
                            allowLineBreaks: false))
                    {
                        return false;
                    }

                    action = NotificationPresentationAction.Dismiss;
                    actionLabel = content.DismissLabel;
                    break;
                case NotificationAcknowledgementPolicy.None:
                    if (record.Definition.ExpiryPolicy == null ||
                        record.Definition.ExpiryPolicy.Mode == NotificationExpiryMode.None)
                    {
                        failureCode = LifetimeDiagnostic;
                        return false;
                    }

                    break;
                default:
                    return false;
            }

            plan = new NotificationPresentationPlan(
                record,
                content,
                action,
                actionLabel,
                SeverityMarker(record.Definition.Severity),
                blocksBackground,
                movesFocus);
            failureCode = null;
            return true;
        }

        private static string SeverityMarker(NotificationSeverity severity)
        {
            switch (severity)
            {
                case NotificationSeverity.Information:
                    return "[i]";
                case NotificationSeverity.Success:
                    return "[+]";
                case NotificationSeverity.Warning:
                    return "[!]";
                case NotificationSeverity.RecoverableError:
                    return "[x]";
                case NotificationSeverity.BlockingError:
                    return "[!!]";
                default:
                    return "[?]";
            }
        }

        private static bool IsSafeText(string value, int maximumUtf8Bytes, bool allowLineBreaks)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !value.IsNormalized(NormalizationForm.FormC) ||
                Encoding.UTF8.GetByteCount(value) > maximumUtf8Bytes ||
                value.IndexOf('<') >= 0 || value.IndexOf('>') >= 0)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsControl(character))
                {
                    continue;
                }

                if (!allowLineBreaks || character != '\n' && character != '\r')
                {
                    return false;
                }
            }

            return true;
        }

        private static string SafeDiagnostic(string candidate, string fallback)
        {
            if (string.IsNullOrWhiteSpace(candidate) ||
                !candidate.StartsWith("AL-", StringComparison.Ordinal) ||
                candidate.Length > NotificationTechnicalLimits.MaximumDiagnosticCodeUtf8Bytes)
            {
                return fallback;
            }

            for (int index = 0; index < candidate.Length; index++)
            {
                char character = candidate[index];
                if (!char.IsUpper(character) && !char.IsDigit(character) && character != '-')
                {
                    return fallback;
                }
            }

            return candidate;
        }
    }
}
