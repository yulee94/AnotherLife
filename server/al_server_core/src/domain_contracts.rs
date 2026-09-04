//! Provider-neutral ports for domain and infrastructure boundaries.
//!
//! These executable interfaces contain no provider SDK types or implementations.
//! Their opaque handles carry already-normalized payloads; authorization and all
//! gameplay/domain decisions remain with the calling domain service.

use std::num::NonZeroU64;

use crate::provider_contracts::{
    AdapterError, AdapterFailureClass, AdapterRequestContext, OperationId,
};

macro_rules! opaque_handle {
    ($name:ident, $description:literal) => {
        #[doc = $description]
        #[derive(Clone, Copy, Debug, Eq, Hash, PartialEq)]
        pub struct $name(NonZeroU64);

        impl $name {
            /// Creates a handle; zero is reserved as invalid.
            #[must_use]
            pub const fn new(value: u64) -> Option<Self> {
                match NonZeroU64::new(value) {
                    Some(value) => Some(Self(value)),
                    None => None,
                }
            }

            /// Returns the opaque numeric value.
            #[must_use]
            pub const fn get(self) -> u64 {
                self.0.get()
            }
        }
    };
}

macro_rules! operation_receipt {
    ($name:ident, $description:literal) => {
        #[doc = $description]
        #[derive(Clone, Copy, Debug, Eq, PartialEq)]
        pub struct $name(OperationId);

        impl $name {
            /// Creates a receipt for the original idempotent operation.
            #[must_use]
            pub const fn new(operation_id: OperationId) -> Self {
                Self(operation_id)
            }

            /// Returns the operation identity used for reconciliation.
            #[must_use]
            pub const fn operation_id(self) -> OperationId {
                self.0
            }
        }
    };
}

opaque_handle!(
    PersistenceMutationHandle,
    "Opaque normalized persistence mutation payload."
);
opaque_handle!(
    SimulationOutcomeHandle,
    "Opaque normalized authoritative-simulation outcome payload."
);
opaque_handle!(
    SocialMutationHandle,
    "Opaque normalized social mutation payload."
);
opaque_handle!(
    EconomyMutationHandle,
    "Opaque normalized economy mutation payload."
);
opaque_handle!(
    SecurityAbuseActionHandle,
    "Opaque normalized security or abuse action payload."
);
opaque_handle!(
    ObservationPayloadHandle,
    "Opaque sanitized observability payload."
);

operation_receipt!(
    PersistenceReceipt,
    "Receipt for an idempotent persistence operation."
);
operation_receipt!(
    SimulationReceipt,
    "Receipt for an idempotent simulation-outcome operation."
);
operation_receipt!(SocialReceipt, "Receipt for an idempotent social operation.");
operation_receipt!(
    EconomyReceipt,
    "Receipt for an idempotent economy operation."
);
operation_receipt!(
    SecurityAbuseReceipt,
    "Receipt for an idempotent security or abuse operation."
);
operation_receipt!(
    ObservabilityReceipt,
    "Receipt for an idempotent observability operation."
);

/// Reconciled state of a previously submitted provider-neutral operation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ReconciliationStatus<R> {
    /// The original operation remains incomplete.
    Pending,
    /// The original operation completed and returns its stable receipt.
    Succeeded(R),
    /// The original operation failed with a stable class.
    Failed(AdapterFailureClass),
    /// No result is known; callers must fail closed rather than repeat a mutation.
    Unknown,
}

/// Region-local persistence port; implementations own no gameplay decisions.
pub trait PersistencePort {
    /// Submits one idempotent mutation; duplicate operation and payload return one receipt.
    fn submit_mutation(
        &mut self,
        context: AdapterRequestContext,
        mutation: PersistenceMutationHandle,
    ) -> Result<PersistenceReceipt, AdapterError>;

    /// Reconciles the original operation identity before any ambiguous retry.
    fn reconcile_mutation(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
    ) -> Result<ReconciliationStatus<PersistenceReceipt>, AdapterError>;
}

/// Authoritative-simulation outcome port; it does not define gameplay rules.
pub trait SimulationPort {
    /// Submits one idempotent already-authorized outcome for durable handling.
    fn submit_outcome(
        &mut self,
        context: AdapterRequestContext,
        outcome: SimulationOutcomeHandle,
    ) -> Result<SimulationReceipt, AdapterError>;

    /// Reconciles the original outcome operation before any ambiguous retry.
    fn reconcile_outcome(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
    ) -> Result<ReconciliationStatus<SimulationReceipt>, AdapterError>;
}

/// Region-local social port; membership and moderation authority stay in the domain.
pub trait SocialPort {
    /// Submits one idempotent already-authorized social mutation.
    fn submit_social_mutation(
        &mut self,
        context: AdapterRequestContext,
        mutation: SocialMutationHandle,
    ) -> Result<SocialReceipt, AdapterError>;

    /// Reconciles the original social operation before any ambiguous retry.
    fn reconcile_social_mutation(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
    ) -> Result<ReconciliationStatus<SocialReceipt>, AdapterError>;
}

/// Region-local economy port; ledger settlement authority stays in the domain.
pub trait EconomyPort {
    /// Submits one idempotent already-authorized value mutation.
    fn submit_value_mutation(
        &mut self,
        context: AdapterRequestContext,
        mutation: EconomyMutationHandle,
    ) -> Result<EconomyReceipt, AdapterError>;

    /// Reconciles the original economy operation before any ambiguous retry.
    fn reconcile_value_mutation(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
    ) -> Result<ReconciliationStatus<EconomyReceipt>, AdapterError>;
}

/// Security and abuse port; authorization and policy decisions precede invocation.
pub trait SecurityAbusePort {
    /// Submits one idempotent already-authorized security or abuse action.
    fn submit_security_action(
        &mut self,
        context: AdapterRequestContext,
        action: SecurityAbuseActionHandle,
    ) -> Result<SecurityAbuseReceipt, AdapterError>;

    /// Reconciles the original security operation before any ambiguous retry.
    fn reconcile_security_action(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
    ) -> Result<ReconciliationStatus<SecurityAbuseReceipt>, AdapterError>;
}

/// Sanitized observability port; telemetry has no gameplay or release authority.
pub trait ObservabilityPort {
    /// Submits one idempotent sanitized observation payload.
    fn submit_observation(
        &mut self,
        context: AdapterRequestContext,
        observation: ObservationPayloadHandle,
    ) -> Result<ObservabilityReceipt, AdapterError>;

    /// Reconciles the original observation operation before an ambiguous retry.
    fn reconcile_observation(
        &mut self,
        context: AdapterRequestContext,
        operation_id: OperationId,
    ) -> Result<ReconciliationStatus<ObservabilityReceipt>, AdapterError>;
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn domain_receipts_preserve_reconciliation_operation_identity() {
        let operation_id = OperationId::new(1).expect("nonzero operation");
        let receipts = [
            PersistenceReceipt::new(operation_id).operation_id(),
            SimulationReceipt::new(operation_id).operation_id(),
            SocialReceipt::new(operation_id).operation_id(),
            EconomyReceipt::new(operation_id).operation_id(),
            SecurityAbuseReceipt::new(operation_id).operation_id(),
            ObservabilityReceipt::new(operation_id).operation_id(),
        ];

        assert!(receipts.iter().all(|receipt| *receipt == operation_id));
        assert_eq!(
            ReconciliationStatus::<PersistenceReceipt>::Failed(AdapterFailureClass::Conflict),
            ReconciliationStatus::Failed(AdapterFailureClass::Conflict)
        );
    }
}
