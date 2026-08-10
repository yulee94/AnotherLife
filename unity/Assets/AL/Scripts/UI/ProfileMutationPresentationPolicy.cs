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
            string technicalCode)
        {
            AuthorityStatus = authorityStatus;
            OrdinaryMutationCommandsEnabled =
                ordinaryMutationCommandsEnabled;
            DisplayText = displayText ?? string.Empty;
            TechnicalCode = technicalCode ?? string.Empty;
        }

        public ProfileWriteAuthorityStatus AuthorityStatus { get; }
        public bool OrdinaryMutationCommandsEnabled { get; }
        public bool IsReadOnly => !OrdinaryMutationCommandsEnabled;
        public string DisplayText { get; }
        public string TechnicalCode { get; }
    }

    /// <summary>
    /// Captures one validated profile-authority snapshot and maps it to a
    /// bounded presentation state. The production entry point always consumes
    /// the hard mutation-containment latch; it performs no polling, reflection,
    /// LINQ, I/O, or frame-loop work.
    /// </summary>
    public static class ProfileMutationPresentationPolicy
    {
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
                            "profile-writes-authorized")
                        : State(
                            status,
                            false,
                            "COMMAND DECK READ-ONLY — PROFILE WRITES NOT ACTIVATED",
                            "profile-writes-not-activated");

                case ProfileWriteAuthorityStatus.MissingProfile:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE MISSING",
                        "profile-missing");

                case ProfileWriteAuthorityStatus.MigrationRequired:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE MIGRATION REQUIRED",
                        "profile-migration-required");

                case ProfileWriteAuthorityStatus.ForwardSchemaReadOnly:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — NEWER PROFILE VERSION",
                        "profile-forward-schema");

                case ProfileWriteAuthorityStatus.DegradedReadOnly:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE DATA DEGRADED",
                        "profile-data-degraded");

                case ProfileWriteAuthorityStatus.RecoveryRequired:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE RECOVERY REQUIRED",
                        "profile-recovery-required");

                case ProfileWriteAuthorityStatus.CommitUncertain:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — SAVE COMMIT UNRESOLVED",
                        "profile-commit-unresolved");

                case ProfileWriteAuthorityStatus.Deleted:
                    return State(
                        status,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE DELETED",
                        "profile-deleted");

                case ProfileWriteAuthorityStatus.Unavailable:
                default:
                    return State(
                        ProfileWriteAuthorityStatus.Unavailable,
                        false,
                        "COMMAND DECK READ-ONLY — PROFILE AUTHORITY UNAVAILABLE",
                        "profile-authority-unavailable");
            }
        }

        private static ProfileMutationPresentationState State(
            ProfileWriteAuthorityStatus authorityStatus,
            bool ordinaryMutationCommandsEnabled,
            string displayText,
            string technicalCode) =>
            new ProfileMutationPresentationState(
                authorityStatus,
                ordinaryMutationCommandsEnabled,
                displayText,
                technicalCode);
    }
}
