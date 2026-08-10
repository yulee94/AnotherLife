using AL.Core.SaveAuthority;
using AL.Services.Local;

namespace AL.UI
{
    public readonly struct ProfileMutationPresentationState
    {
        internal ProfileMutationPresentationState(
            ProfileWriteAuthorityStatus authorityStatus,
            bool ordinaryMutationCommandsEnabled,
            string displayText,
            string reasonText,
            string technicalCode)
        {
            AuthorityStatus = authorityStatus;
            OrdinaryMutationCommandsEnabled =
                ordinaryMutationCommandsEnabled;
            DisplayText = displayText ?? string.Empty;
            ReasonText = reasonText ?? string.Empty;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public ProfileWriteAuthorityStatus AuthorityStatus { get; }
        public bool OrdinaryMutationCommandsEnabled { get; }
        public bool IsReadOnly => !OrdinaryMutationCommandsEnabled;
        public string DisplayText { get; }
        public string ReasonText { get; }
        public string TechnicalCode { get; }
    }

    internal readonly struct ProfileMutationSurfacePresentationState
    {
        internal ProfileMutationSurfacePresentationState(
            bool mutationCommandsEnabled,
            string reasonText)
        {
            MutationCommandsEnabled = mutationCommandsEnabled;
            ReasonText = reasonText ?? string.Empty;
        }

        public bool MutationCommandsEnabled { get; }
        public string ReasonText { get; }
    }

    /// <summary>
    /// Captures one validated profile-authority snapshot and maps it to a
    /// bounded presentation state. The production entry point always consumes
    /// the hard mutation-containment latch; it performs no polling, reflection,
    /// LINQ, I/O, or frame-loop work.
    /// </summary>
    public static class ProfileMutationPresentationPolicy
    {
        internal const string ProfileWritesNotActivatedReason =
            "PROFILE WRITES NOT ACTIVATED";
        private const string ProfileAuthorityUnavailableReason =
            "PROFILE AUTHORITY UNAVAILABLE";

        public static ProfileMutationPresentationState Capture(
            IProfileWriteAuthorityProvider provider) =>
            Capture(
                provider,
                ProfileMutationContainment.ProductionWriteActivationEnabled);

        internal static ProfileMutationPresentationState Capture(
            IProfileWriteAuthorityProvider provider,
            bool productionWriteActivationEnabled)
        {
            ProfileWriteAuthoritySnapshot authority =
                ProfileWriteAuthorityProviderGuard.ReadOrUnavailable(provider);
            ProfileWriteAuthorityStatus status = authority.Status;

            switch (status)
            {
                case ProfileWriteAuthorityStatus.Writable:
                    return productionWriteActivationEnabled
                        ? State(
                            status,
                            true,
                            "COMMAND DECK WRITABLE — PROFILE AUTHORITY VERIFIED",
                            "PROFILE AUTHORITY VERIFIED",
                            "profile-writes-authorized")
                        : State(
                            status,
                            false,
                            "COMMAND DECK READ-ONLY — PROFILE WRITES NOT ACTIVATED",
                            ProfileWritesNotActivatedReason,
                            "profile-writes-not-activated");

                case ProfileWriteAuthorityStatus.MissingProfile:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE MISSING",
                        "PROFILE MISSING",
                        "profile-missing");

                case ProfileWriteAuthorityStatus.MigrationRequired:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE MIGRATION REQUIRED",
                        "PROFILE MIGRATION REQUIRED",
                        "profile-migration-required");

                case ProfileWriteAuthorityStatus.ForwardSchemaReadOnly:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — NEWER PROFILE VERSION",
                        "NEWER PROFILE VERSION",
                        "profile-forward-schema");

                case ProfileWriteAuthorityStatus.DegradedReadOnly:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE DATA DEGRADED",
                        "PROFILE DATA DEGRADED",
                        "profile-data-degraded");

                case ProfileWriteAuthorityStatus.RecoveryRequired:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE RECOVERY REQUIRED",
                        "PROFILE RECOVERY REQUIRED",
                        "profile-recovery-required");

                case ProfileWriteAuthorityStatus.CommitUncertain:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — SAVE COMMIT UNRESOLVED",
                        "SAVE COMMIT UNRESOLVED",
                        "profile-commit-unresolved");

                case ProfileWriteAuthorityStatus.Deleted:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE DELETED",
                        "PROFILE DELETED",
                        "profile-deleted");

                case ProfileWriteAuthorityStatus.Unavailable:
                default:
                    return State(
                        ProfileWriteAuthorityStatus.Unavailable,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE AUTHORITY UNAVAILABLE",
                        ProfileAuthorityUnavailableReason,
                        "profile-authority-unavailable");
            }
        }

        internal static ProfileMutationSurfacePresentationState ResolveSurface(
            ProfileMutationPresentationState profilePresentation,
            bool surfaceActivationEnabled)
        {
            if (!profilePresentation.OrdinaryMutationCommandsEnabled)
            {
                return new ProfileMutationSurfacePresentationState(
                    false,
                    NormalizeReason(profilePresentation.ReasonText));
            }

            if (!surfaceActivationEnabled)
            {
                return new ProfileMutationSurfacePresentationState(
                    false,
                    ProfileWritesNotActivatedReason);
            }

            return new ProfileMutationSurfacePresentationState(
                true,
                NormalizeReason(profilePresentation.ReasonText));
        }

        private static string NormalizeReason(string reasonText) =>
            string.IsNullOrWhiteSpace(reasonText)
                ? ProfileAuthorityUnavailableReason
                : reasonText;

        private static ProfileMutationPresentationState State(
            ProfileWriteAuthorityStatus authorityStatus,
            bool ordinaryMutationCommandsEnabled,
            string displayText,
            string reasonText,
            string technicalCode) =>
            new ProfileMutationPresentationState(
                authorityStatus,
                ordinaryMutationCommandsEnabled,
                displayText,
                reasonText,
                technicalCode);
    }
}
