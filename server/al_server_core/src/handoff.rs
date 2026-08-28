//! Deterministic, bounded, single-writer ownership handoff.

use crate::ownership::{CellId, OwnershipLease, OwnershipTransferError, Tick};
use std::fmt;
use std::num::NonZeroU64;

/// Stable nonzero identity used to deduplicate handoff control messages.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct HandoffId(NonZeroU64);

impl HandoffId {
    /// Creates a handoff identity; zero is reserved as invalid.
    #[must_use]
    pub const fn new(value: u64) -> Option<Self> {
        match NonZeroU64::new(value) {
            Some(value) => Some(Self(value)),
            None => None,
        }
    }

    /// Returns the numeric identity.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0.get()
    }
}

impl fmt::Display for HandoffId {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.get().fmt(formatter)
    }
}

/// Upper bound on how far a handoff may schedule its cutover.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct HandoffLimits {
    max_span_ticks: NonZeroU64,
}

impl HandoffLimits {
    /// Creates a nonzero scheduling span.
    #[must_use]
    pub const fn new(max_span_ticks: u64) -> Option<Self> {
        match NonZeroU64::new(max_span_ticks) {
            Some(max_span_ticks) => Some(Self { max_span_ticks }),
            None => None,
        }
    }

    /// Returns the maximum ticks from start through cutover.
    #[must_use]
    pub const fn max_span_ticks(self) -> u64 {
        self.max_span_ticks.get()
    }
}

/// Observable handoff lifecycle phase.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum HandoffPhase {
    /// Source owns authority while destination prepares a read-only shadow.
    Preparing,
    /// Destination acknowledged preparation but remains read-only until cutover.
    Prepared,
    /// Destination became the sole writer at the scheduled tick.
    Committed,
    /// Transfer ended without changing the source writer.
    Aborted,
}

/// Reason a handoff preserved source ownership rather than committing.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum HandoffAbortReason {
    /// Destination did not acknowledge by the inclusive ready deadline.
    ReadyDeadlineExceeded,
    /// An authenticated control-plane request cancelled the transfer.
    Cancelled,
}

/// State transition produced by advancing or cancelling a handoff.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum HandoffTransition {
    /// No phase boundary was crossed.
    None,
    /// Cutover published a new sole-writer lease.
    Committed {
        /// Previous authoritative lease.
        previous: OwnershipLease,
        /// New authoritative lease with incremented epoch.
        current: OwnershipLease,
        /// Exact cutover tick.
        at_tick: Tick,
    },
    /// Transfer ended and source ownership remained unchanged.
    Aborted {
        /// Preserved source lease.
        authority: OwnershipLease,
        /// Abort reason.
        reason: HandoffAbortReason,
        /// First tick at which the abort was observed.
        at_tick: Tick,
    },
}

/// Result of processing a destination-ready acknowledgement.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ReadyOutcome {
    /// First valid acknowledgement moved the state to prepared.
    Accepted,
    /// A duplicate acknowledgement was harmlessly deduplicated.
    AlreadyPrepared,
    /// A late duplicate arrived after this same handoff committed.
    AlreadyCommitted,
}

/// Rejected handoff operation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum HandoffError {
    /// Destination equals the source owner.
    SameCell(CellId),
    /// No new fencing epoch can be allocated.
    EpochExhausted,
    /// Ready deadline must not be earlier than the start tick.
    ReadyDeadlineBeforeStart,
    /// Cutover must be strictly later than the ready deadline.
    CutoverNotAfterReadyDeadline,
    /// Cutover exceeds the configured bounded span.
    SpanTooLarge {
        /// Requested duration.
        requested_ticks: u64,
        /// Configured duration ceiling.
        max_ticks: u64,
    },
    /// A command supplied a different handoff identity.
    WrongHandoffId {
        /// Expected active identity.
        expected: HandoffId,
        /// Received identity.
        received: HandoffId,
    },
    /// Simulation time attempted to move backward.
    TickRegression {
        /// Last observed tick.
        previous: Tick,
        /// Regressing requested tick.
        received: Tick,
    },
    /// Operation is not valid in the current phase.
    InvalidPhase(HandoffPhase),
}

impl fmt::Display for HandoffError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for HandoffError {}

/// One bounded ownership transfer. It always exposes exactly one writer lease.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct Handoff {
    id: HandoffId,
    source: OwnershipLease,
    destination: OwnershipLease,
    ready_deadline: Tick,
    cutover_tick: Tick,
    observed_tick: Tick,
    phase: HandoffPhase,
    abort_reason: Option<HandoffAbortReason>,
}

impl Handoff {
    /// Begins a bounded transfer while preserving source write authority.
    pub fn begin(
        id: HandoffId,
        source: OwnershipLease,
        destination_cell: CellId,
        start_tick: Tick,
        ready_deadline: Tick,
        cutover_tick: Tick,
        limits: HandoffLimits,
    ) -> Result<Self, HandoffError> {
        let destination = match source.next_owner(destination_cell) {
            Ok(destination) => destination,
            Err(OwnershipTransferError::SameOwner(cell)) => {
                return Err(HandoffError::SameCell(cell))
            }
            Err(OwnershipTransferError::EpochExhausted(_)) => {
                return Err(HandoffError::EpochExhausted)
            }
        };

        if ready_deadline < start_tick {
            return Err(HandoffError::ReadyDeadlineBeforeStart);
        }
        if cutover_tick <= ready_deadline {
            return Err(HandoffError::CutoverNotAfterReadyDeadline);
        }
        let requested_ticks = cutover_tick.get() - start_tick.get();
        if requested_ticks > limits.max_span_ticks() {
            return Err(HandoffError::SpanTooLarge {
                requested_ticks,
                max_ticks: limits.max_span_ticks(),
            });
        }

        Ok(Self {
            id,
            source,
            destination,
            ready_deadline,
            cutover_tick,
            observed_tick: start_tick,
            phase: HandoffPhase::Preparing,
            abort_reason: None,
        })
    }

    /// Returns the handoff identity.
    #[must_use]
    pub const fn id(self) -> HandoffId {
        self.id
    }

    /// Returns the current phase.
    #[must_use]
    pub const fn phase(self) -> HandoffPhase {
        self.phase
    }

    /// Returns the latest monotonic tick observed by this state machine.
    #[must_use]
    pub const fn observed_tick(self) -> Tick {
        self.observed_tick
    }

    /// Returns the inclusive destination-ready deadline.
    #[must_use]
    pub const fn ready_deadline(self) -> Tick {
        self.ready_deadline
    }

    /// Returns the scheduled authority cutover tick.
    #[must_use]
    pub const fn cutover_tick(self) -> Tick {
        self.cutover_tick
    }

    /// Returns the only lease allowed to write in the current phase.
    #[must_use]
    pub const fn writer(self) -> OwnershipLease {
        match self.phase {
            HandoffPhase::Preparing | HandoffPhase::Prepared | HandoffPhase::Aborted => self.source,
            HandoffPhase::Committed => self.destination,
        }
    }

    /// Returns the destination's future lease while it is read-only.
    ///
    /// The returned lease is routing metadata, not permission to write. Only
    /// [`Self::writer`] identifies current authority.
    #[must_use]
    pub const fn prepared_destination(self) -> Option<OwnershipLease> {
        match self.phase {
            HandoffPhase::Prepared => Some(self.destination),
            _ => None,
        }
    }

    /// Returns the terminal abort reason, when any.
    #[must_use]
    pub const fn abort_reason(self) -> Option<HandoffAbortReason> {
        self.abort_reason
    }

    /// Processes an idempotent destination-ready acknowledgement.
    ///
    /// Acknowledgements at the ready deadline are accepted. The fixed-tick
    /// driver must process messages for a tick before advancing beyond it.
    pub fn destination_ready(
        &mut self,
        id: HandoffId,
        at_tick: Tick,
    ) -> Result<ReadyOutcome, HandoffError> {
        self.require_id(id)?;
        let transition = self.advance_to(at_tick)?;
        if matches!(transition, HandoffTransition::Committed { .. }) {
            return Ok(ReadyOutcome::AlreadyCommitted);
        }
        if matches!(transition, HandoffTransition::Aborted { .. }) {
            return Err(HandoffError::InvalidPhase(self.phase));
        }

        match self.phase {
            HandoffPhase::Preparing => {
                self.phase = HandoffPhase::Prepared;
                Ok(ReadyOutcome::Accepted)
            }
            HandoffPhase::Prepared => Ok(ReadyOutcome::AlreadyPrepared),
            HandoffPhase::Committed => Ok(ReadyOutcome::AlreadyCommitted),
            HandoffPhase::Aborted => Err(HandoffError::InvalidPhase(self.phase)),
        }
    }

    /// Advances monotonic simulation time and applies due terminal transitions.
    pub fn advance_to(&mut self, tick: Tick) -> Result<HandoffTransition, HandoffError> {
        if tick < self.observed_tick {
            return Err(HandoffError::TickRegression {
                previous: self.observed_tick,
                received: tick,
            });
        }
        self.observed_tick = tick;

        match self.phase {
            HandoffPhase::Preparing if tick > self.ready_deadline => {
                self.phase = HandoffPhase::Aborted;
                self.abort_reason = Some(HandoffAbortReason::ReadyDeadlineExceeded);
                Ok(HandoffTransition::Aborted {
                    authority: self.source,
                    reason: HandoffAbortReason::ReadyDeadlineExceeded,
                    at_tick: tick,
                })
            }
            HandoffPhase::Prepared if tick >= self.cutover_tick => {
                self.phase = HandoffPhase::Committed;
                Ok(HandoffTransition::Committed {
                    previous: self.source,
                    current: self.destination,
                    at_tick: self.cutover_tick,
                })
            }
            _ => Ok(HandoffTransition::None),
        }
    }

    /// Cancels an active transfer before cutover, preserving source authority.
    pub fn cancel(
        &mut self,
        id: HandoffId,
        at_tick: Tick,
    ) -> Result<HandoffTransition, HandoffError> {
        self.require_id(id)?;
        let due = self.advance_to(at_tick)?;
        if !matches!(due, HandoffTransition::None) {
            return Ok(due);
        }

        match self.phase {
            HandoffPhase::Preparing | HandoffPhase::Prepared => {
                self.phase = HandoffPhase::Aborted;
                self.abort_reason = Some(HandoffAbortReason::Cancelled);
                Ok(HandoffTransition::Aborted {
                    authority: self.source,
                    reason: HandoffAbortReason::Cancelled,
                    at_tick,
                })
            }
            HandoffPhase::Committed | HandoffPhase::Aborted => {
                Err(HandoffError::InvalidPhase(self.phase))
            }
        }
    }

    fn require_id(&self, received: HandoffId) -> Result<(), HandoffError> {
        if received == self.id {
            Ok(())
        } else {
            Err(HandoffError::WrongHandoffId {
                expected: self.id,
                received,
            })
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ownership::{EntityId, OwnershipEpoch, WriteClaimVerdict};

    fn cell(value: u64) -> CellId {
        CellId::new(value).expect("test cell IDs are nonzero")
    }

    fn lease(epoch: u64) -> OwnershipLease {
        OwnershipLease::new(
            EntityId::new(9).expect("nonzero entity"),
            cell(1),
            OwnershipEpoch::new(epoch).expect("nonzero epoch"),
        )
    }

    fn id(value: u64) -> HandoffId {
        HandoffId::new(value).expect("test handoff IDs are nonzero")
    }

    fn limits() -> HandoffLimits {
        HandoffLimits::new(20).expect("nonzero span")
    }

    fn handoff() -> Handoff {
        Handoff::begin(
            id(77),
            lease(4),
            cell(2),
            Tick::new(10),
            Tick::new(15),
            Tick::new(20),
            limits(),
        )
        .expect("valid handoff")
    }

    #[test]
    fn happy_path_changes_writer_only_at_cutover() {
        let mut transfer = handoff();
        let source = transfer.writer();

        assert_eq!(
            transfer.destination_ready(id(77), Tick::new(14)),
            Ok(ReadyOutcome::Accepted)
        );
        assert_eq!(transfer.phase(), HandoffPhase::Prepared);
        assert_eq!(transfer.writer(), source);
        assert_eq!(
            transfer.destination_ready(id(77), Tick::new(14)),
            Ok(ReadyOutcome::AlreadyPrepared)
        );
        assert_eq!(
            transfer.advance_to(Tick::new(19)),
            Ok(HandoffTransition::None)
        );
        assert_eq!(transfer.writer(), source);

        let transition = transfer
            .advance_to(Tick::new(20))
            .expect("cutover succeeds");
        let destination = transfer.writer();
        assert_eq!(transfer.phase(), HandoffPhase::Committed);
        assert_eq!(destination.owner(), cell(2));
        assert_eq!(destination.epoch().get(), source.epoch().get() + 1);
        assert_eq!(
            destination.validate_claim(source),
            WriteClaimVerdict::StaleEpoch {
                expected: destination.epoch(),
                claimed: source.epoch(),
            }
        );
        assert_eq!(
            transition,
            HandoffTransition::Committed {
                previous: source,
                current: destination,
                at_tick: Tick::new(20),
            }
        );
        assert_eq!(
            transfer.destination_ready(id(77), Tick::new(21)),
            Ok(ReadyOutcome::AlreadyCommitted)
        );
    }

    #[test]
    fn missing_ready_deadline_aborts_without_changing_writer() {
        let mut transfer = handoff();
        let source = transfer.writer();

        assert_eq!(
            transfer.advance_to(Tick::new(16)),
            Ok(HandoffTransition::Aborted {
                authority: source,
                reason: HandoffAbortReason::ReadyDeadlineExceeded,
                at_tick: Tick::new(16),
            })
        );
        assert_eq!(transfer.phase(), HandoffPhase::Aborted);
        assert_eq!(transfer.writer(), source);
        assert_eq!(
            transfer.destination_ready(id(77), Tick::new(16)),
            Err(HandoffError::InvalidPhase(HandoffPhase::Aborted))
        );
    }

    #[test]
    fn ready_at_inclusive_deadline_can_commit() {
        let mut transfer = handoff();
        assert_eq!(
            transfer.destination_ready(id(77), Tick::new(15)),
            Ok(ReadyOutcome::Accepted)
        );
        assert!(matches!(
            transfer.advance_to(Tick::new(20)),
            Ok(HandoffTransition::Committed { .. })
        ));
    }

    #[test]
    fn cancellation_and_wrong_ids_are_safe() {
        let mut transfer = handoff();
        let source = transfer.writer();
        assert!(matches!(
            transfer.destination_ready(id(78), Tick::new(12)),
            Err(HandoffError::WrongHandoffId { .. })
        ));
        assert_eq!(transfer.observed_tick(), Tick::new(10));
        assert_eq!(
            transfer.cancel(id(77), Tick::new(12)),
            Ok(HandoffTransition::Aborted {
                authority: source,
                reason: HandoffAbortReason::Cancelled,
                at_tick: Tick::new(12),
            })
        );
        assert_eq!(transfer.writer(), source);
    }

    #[test]
    fn begin_enforces_schedule_and_epoch_bounds() {
        let base = lease(4);
        assert_eq!(
            Handoff::begin(
                id(1),
                base,
                cell(1),
                Tick::new(10),
                Tick::new(15),
                Tick::new(20),
                limits()
            ),
            Err(HandoffError::SameCell(cell(1)))
        );
        assert_eq!(
            Handoff::begin(
                id(1),
                base,
                cell(2),
                Tick::new(10),
                Tick::new(9),
                Tick::new(20),
                limits()
            ),
            Err(HandoffError::ReadyDeadlineBeforeStart)
        );
        assert_eq!(
            Handoff::begin(
                id(1),
                base,
                cell(2),
                Tick::new(10),
                Tick::new(15),
                Tick::new(15),
                limits()
            ),
            Err(HandoffError::CutoverNotAfterReadyDeadline)
        );
        assert_eq!(
            Handoff::begin(
                id(1),
                base,
                cell(2),
                Tick::new(10),
                Tick::new(15),
                Tick::new(31),
                limits(),
            ),
            Err(HandoffError::SpanTooLarge {
                requested_ticks: 21,
                max_ticks: 20,
            })
        );
        assert_eq!(
            Handoff::begin(
                id(1),
                lease(u64::MAX),
                cell(2),
                Tick::new(10),
                Tick::new(15),
                Tick::new(20),
                limits(),
            ),
            Err(HandoffError::EpochExhausted)
        );
    }

    #[test]
    fn time_never_regresses() {
        let mut transfer = handoff();
        transfer
            .advance_to(Tick::new(12))
            .expect("advance succeeds");
        assert_eq!(
            transfer.advance_to(Tick::new(11)),
            Err(HandoffError::TickRegression {
                previous: Tick::new(12),
                received: Tick::new(11),
            })
        );
        assert_eq!(transfer.observed_tick(), Tick::new(12));
    }

    #[test]
    fn exhaustive_ready_timing_preserves_exactly_one_writer() {
        for ready_tick in 10..=22 {
            let mut transfer = handoff();
            let source = transfer.writer();
            let result = transfer.destination_ready(id(77), Tick::new(ready_tick));

            for tick in ready_tick..=25 {
                let _ = transfer.advance_to(Tick::new(tick));
                let writer = transfer.writer();
                let is_source = writer == source;
                let is_destination =
                    writer.owner() == cell(2) && writer.epoch().get() == source.epoch().get() + 1;
                assert_ne!(is_source, is_destination, "tick={tick}, ready={ready_tick}");
            }

            if ready_tick <= 15 {
                assert!(result.is_ok());
                assert_eq!(transfer.phase(), HandoffPhase::Committed);
            } else {
                assert!(result.is_err());
                assert_eq!(transfer.phase(), HandoffPhase::Aborted);
            }
        }
    }
}
