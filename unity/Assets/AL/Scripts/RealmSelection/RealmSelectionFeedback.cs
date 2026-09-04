using System;
using AL.Core;

namespace AL.RealmSelection
{
    public readonly struct RealmSelectionFeedbackPresentation
    {
        public RealmSelectionFeedbackPresentation(string localizationKey, string text, bool isSuccess)
        {
            LocalizationKey = localizationKey ?? string.Empty;
            Text = text ?? string.Empty;
            IsSuccess = isSuccess;
        }

        public string LocalizationKey { get; }
        public string Text { get; }
        public bool IsSuccess { get; }
    }

    public static class RealmSelectionFeedback
    {
        public const string LockWarningKey = "realm.lock.warning";

        public static bool TryResolveLockWarning(RealmCatalogSnapshot catalog, out string text)
        {
            if (catalog != null && catalog.TryGetLocalization(LockWarningKey, out text) &&
                !string.IsNullOrEmpty(text))
            {
                return true;
            }

            text = AL.UI.RealmSelection.RealmSelectionIdentity.LockWarningFallback;
            return catalog != null;
        }

        public static RealmSelectionFeedbackPresentation FromResult(
            RealmSelectionResult result,
            RealmCatalogSnapshot catalog)
        {
            if (result.AllowsNavigation)
            {
                string key = SelectionLineKey(result.CommittedRealmId != RealmId.None
                    ? result.CommittedRealmId
                    : result.RequestedRealmId);
                string text;
                if (catalog != null && catalog.TryGetLocalization(key, out text) && !string.IsNullOrEmpty(text))
                {
                    return new RealmSelectionFeedbackPresentation(key, text, true);
                }

                return new RealmSelectionFeedbackPresentation(
                    key,
                    "This account is bound to the chosen realm.",
                    true);
            }

            if (result.Status == RealmSelectionStatus.RejectedDifferentRealm)
            {
                string lockText;
                TryResolveLockWarning(catalog, out lockText);
                return new RealmSelectionFeedbackPresentation(
                    LockWarningKey,
                    lockText + " Changing realm requires a verified profile reset.",
                    false);
            }

            return new RealmSelectionFeedbackPresentation(
                string.Empty,
                result.TechnicalCode,
                false);
        }

        private static string SelectionLineKey(RealmId realmId)
        {
            switch (realmId)
            {
                case RealmId.Crownlands:
                    return "realm.crownlands.selection.line";
                case RealmId.Stonehold:
                    return "realm.stonehold.selection.line";
                case RealmId.Eldergrove:
                    return "realm.eldergrove.selection.line";
                case RealmId.Umbral:
                    return "realm.umbral.selection.line";
                default:
                    return string.Empty;
            }
        }
    }
}
