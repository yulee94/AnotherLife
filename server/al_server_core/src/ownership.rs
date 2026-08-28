//! Single-writer ownership and read-only ghost freshness rules.

use std::cmp::Ordering;
use std::fmt;
use std::num::NonZeroU64;

macro_rules! nonzero_id {
    ($(#[$meta:meta])* $name:ident, $description:literal) => {
        $(#[$meta])*
        #[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
        pub struct $name(NonZeroU64);

        impl $name {
            #[doc = concat!("Creates ", $description, "; zero is reserved as invalid.")]
            #[must_use]
            pub const fn new(value: u64) -> Option<Self> {
                match NonZeroU64::new(value) {
                    Some(value) => Some(Self(value)),
                    None => None,
                }
            }

            #[doc = concat!("Returns the numeric value of ", $description, ".")]
            #[must_use]
            pub const fn get(self) -> u64 {
                self.0.get()
            }
        }

        impl fmt::Display for $name {
            fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                self.get().fmt(formatter)
            }
        }
    };
}

nonzero_id!(
    /// Stable identity of one authoritative spatial microcell.
    CellId,
    "a cell identifier"
);
nonzero_id!(
    /// Stable identity of an entity whose authority can move between cells.
    EntityId,
    "an entity identifier"
);
nonzero_id!(
    /// Monotonically increasing fencing generation for one entity's owner.
    OwnershipEpoch,
    "an ownership epoch"
);

impl OwnershipEpoch {
    /// Returns the next epoch, or `None` when the epoch space is exhausted.
    #[must_use]
    pub const fn checked_next(self) -> Option<Self> {
        match self.get().checked_add(1) {
            Some(value) => Self::new(value),
            None => None,
        }
    }
}

/// Monotonic fixed-step simulation tick.
#[derive(Clone, Copy, Debug, Default, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct Tick(u64);

impl Tick {
    /// Creates a tick from its numeric value.
    #[must_use]
    pub const fn new(value: u64) -> Self {
        Self(value)
    }

    /// Returns the numeric tick value.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0
    }

    /// Adds a bounded tick duration, returning `None` on overflow.
    #[must_use]
    pub const fn checked_add(self, ticks: u64) -> Option<Self> {
        match self.0.checked_add(ticks) {
            Some(value) => Some(Self(value)),
            None => None,
        }
    }
}

impl fmt::Display for Tick {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.get().fmt(formatter)
    }
}

/// The sole cell permitted to mutate one entity at one ownership epoch.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct OwnershipLease {
    entity: EntityId,
    owner: CellId,
    epoch: OwnershipEpoch,
}

impl OwnershipLease {
    /// Creates an ownership lease from already-validated nonzero identities.
    #[must_use]
    pub const fn new(entity: EntityId, owner: CellId, epoch: OwnershipEpoch) -> Self {
        Self {
            entity,
            owner,
            epoch,
        }
    }

    /// Returns the owned entity.
    #[must_use]
    pub const fn entity(self) -> EntityId {
        self.entity
    }

    /// Returns the sole writer cell.
    #[must_use]
    pub const fn owner(self) -> CellId {
        self.owner
    }

    /// Returns the fencing epoch.
    #[must_use]
    pub const fn epoch(self) -> OwnershipEpoch {
        self.epoch
    }

    /// Classifies a claimed writer without mutating authority.
    #[must_use]
    pub fn validate_claim(self, claim: Self) -> WriteClaimVerdict {
        if claim.entity != self.entity {
            return WriteClaimVerdict::WrongEntity {
                expected: self.entity,
                claimed: claim.entity,
            };
        }

        match claim.epoch.cmp(&self.epoch) {
            Ordering::Less => WriteClaimVerdict::StaleEpoch {
                expected: self.epoch,
                claimed: claim.epoch,
            },
            Ordering::Greater => WriteClaimVerdict::FutureEpoch {
                expected: self.epoch,
                claimed: claim.epoch,
            },
            Ordering::Equal if claim.owner != self.owner => WriteClaimVerdict::WrongOwner {
                expected: self.owner,
                claimed: claim.owner,
            },
            Ordering::Equal => WriteClaimVerdict::Accepted,
        }
    }

    /// Produces the next fenced lease for a different owner.
    ///
    /// Calling this does not itself perform a handoff. The caller must publish
    /// the result only at a deterministic authority cutover.
    pub fn next_owner(self, destination: CellId) -> Result<Self, OwnershipTransferError> {
        if destination == self.owner {
            return Err(OwnershipTransferError::SameOwner(destination));
        }

        let epoch = self
            .epoch
            .checked_next()
            .ok_or(OwnershipTransferError::EpochExhausted(self.epoch))?;
        Ok(Self::new(self.entity, destination, epoch))
    }
}

/// Result of checking whether a cell may write an entity.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum WriteClaimVerdict {
    /// Entity, owner, and epoch match current authority.
    Accepted,
    /// The claim addresses another entity.
    WrongEntity {
        /// Authoritative entity.
        expected: EntityId,
        /// Entity in the claim.
        claimed: EntityId,
    },
    /// The claim was issued by a cell that is not the current owner.
    WrongOwner {
        /// Authoritative owner.
        expected: CellId,
        /// Owner in the claim.
        claimed: CellId,
    },
    /// The claim is fenced by a newer authoritative epoch.
    StaleEpoch {
        /// Authoritative epoch.
        expected: OwnershipEpoch,
        /// Older claimed epoch.
        claimed: OwnershipEpoch,
    },
    /// The claim is ahead of the receiver's route and cannot be trusted yet.
    FutureEpoch {
        /// Authoritative epoch known by the receiver.
        expected: OwnershipEpoch,
        /// Newer claimed epoch.
        claimed: OwnershipEpoch,
    },
}

/// Failure to derive the next owner lease.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum OwnershipTransferError {
    /// A transfer must change owner cells.
    SameOwner(CellId),
    /// No higher fencing epoch can be represented.
    EpochExhausted(OwnershipEpoch),
}

impl fmt::Display for OwnershipTransferError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for OwnershipTransferError {}

/// Authority metadata carried by a read-only ghost snapshot.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GhostStamp {
    entity: EntityId,
    owner: CellId,
    epoch: OwnershipEpoch,
    source_tick: Tick,
}

impl GhostStamp {
    /// Creates ghost authority metadata.
    #[must_use]
    pub const fn new(
        entity: EntityId,
        owner: CellId,
        epoch: OwnershipEpoch,
        source_tick: Tick,
    ) -> Self {
        Self {
            entity,
            owner,
            epoch,
            source_tick,
        }
    }

    /// Returns the represented entity.
    #[must_use]
    pub const fn entity(self) -> EntityId {
        self.entity
    }

    /// Returns the authoritative source cell declared by the ghost.
    #[must_use]
    pub const fn owner(self) -> CellId {
        self.owner
    }

    /// Returns the declared authority epoch.
    #[must_use]
    pub const fn epoch(self) -> OwnershipEpoch {
        self.epoch
    }

    /// Returns the source simulation tick.
    #[must_use]
    pub const fn source_tick(self) -> Tick {
        self.source_tick
    }
}

/// Result of validating a ghost before replacing a read-only replica.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum GhostVerdict {
    /// The ghost matches current authority, is recent enough, and advances time.
    Fresh {
        /// Difference between local and source ticks.
        age_ticks: u64,
    },
    /// The ghost describes a different entity.
    WrongEntity {
        /// Authoritative entity.
        expected: EntityId,
        /// Entity in the ghost.
        received: EntityId,
    },
    /// The ghost claims the wrong source owner at the current epoch.
    WrongOwner {
        /// Authoritative owner.
        expected: CellId,
        /// Owner in the ghost.
        received: CellId,
    },
    /// The ghost belongs to an epoch already fenced by authority.
    StaleEpoch {
        /// Authoritative epoch.
        expected: OwnershipEpoch,
        /// Older received epoch.
        received: OwnershipEpoch,
    },
    /// The ghost is ahead of the receiver's known authority route.
    FutureEpoch {
        /// Authoritative epoch.
        expected: OwnershipEpoch,
        /// Newer received epoch.
        received: OwnershipEpoch,
    },
    /// The source tick is ahead of the local simulation clock.
    FutureTick {
        /// Local tick.
        local: Tick,
        /// Source tick from the ghost.
        received: Tick,
    },
    /// The ghost exceeds the configured age budget.
    Expired {
        /// Observed age.
        age_ticks: u64,
        /// Inclusive maximum accepted age.
        max_age_ticks: u64,
    },
    /// The same source tick was already accepted.
    Duplicate {
        /// Repeated source tick.
        source_tick: Tick,
    },
    /// The ghost arrived older than the last accepted snapshot.
    Reordered {
        /// Most recent accepted source tick.
        last_accepted: Tick,
        /// Older received source tick.
        received: Tick,
    },
}

/// Validates an incoming ghost against current authority and local time.
///
/// `max_age_ticks` is inclusive: a ghost exactly that old remains usable. Pass
/// the last accepted stamp, when present, to reject duplicates and reordering.
#[must_use]
pub fn evaluate_ghost(
    incoming: GhostStamp,
    authority: OwnershipLease,
    local_tick: Tick,
    max_age_ticks: u64,
    last_accepted: Option<GhostStamp>,
) -> GhostVerdict {
    if incoming.entity != authority.entity {
        return GhostVerdict::WrongEntity {
            expected: authority.entity,
            received: incoming.entity,
        };
    }

    match incoming.epoch.cmp(&authority.epoch) {
        Ordering::Less => {
            return GhostVerdict::StaleEpoch {
                expected: authority.epoch,
                received: incoming.epoch,
            }
        }
        Ordering::Greater => {
            return GhostVerdict::FutureEpoch {
                expected: authority.epoch,
                received: incoming.epoch,
            }
        }
        Ordering::Equal => {}
    }

    if incoming.owner != authority.owner {
        return GhostVerdict::WrongOwner {
            expected: authority.owner,
            received: incoming.owner,
        };
    }

    if incoming.source_tick > local_tick {
        return GhostVerdict::FutureTick {
            local: local_tick,
            received: incoming.source_tick,
        };
    }

    let age_ticks = local_tick.get() - incoming.source_tick.get();
    if age_ticks > max_age_ticks {
        return GhostVerdict::Expired {
            age_ticks,
            max_age_ticks,
        };
    }

    if let Some(previous) = last_accepted.filter(|previous| {
        previous.entity == incoming.entity
            && previous.owner == incoming.owner
            && previous.epoch == incoming.epoch
    }) {
        match incoming.source_tick.cmp(&previous.source_tick) {
            Ordering::Less => {
                return GhostVerdict::Reordered {
                    last_accepted: previous.source_tick,
                    received: incoming.source_tick,
                }
            }
            Ordering::Equal => {
                return GhostVerdict::Duplicate {
                    source_tick: incoming.source_tick,
                }
            }
            Ordering::Greater => {}
        }
    }

    GhostVerdict::Fresh { age_ticks }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn cell(value: u64) -> CellId {
        CellId::new(value).expect("test cell IDs are nonzero")
    }

    fn entity(value: u64) -> EntityId {
        EntityId::new(value).expect("test entity IDs are nonzero")
    }

    fn epoch(value: u64) -> OwnershipEpoch {
        OwnershipEpoch::new(value).expect("test epochs are nonzero")
    }

    #[test]
    fn identifiers_reserve_zero() {
        assert_eq!(CellId::new(0), None);
        assert_eq!(EntityId::new(0), None);
        assert_eq!(OwnershipEpoch::new(0), None);
    }

    #[test]
    fn transfer_increments_epoch_and_changes_only_owner() {
        let initial = OwnershipLease::new(entity(9), cell(1), epoch(7));
        let transferred = initial.next_owner(cell(2)).expect("valid transfer");

        assert_eq!(transferred.entity(), initial.entity());
        assert_eq!(transferred.owner(), cell(2));
        assert_eq!(transferred.epoch(), epoch(8));
        assert_eq!(
            initial.validate_claim(transferred),
            WriteClaimVerdict::FutureEpoch {
                expected: epoch(7),
                claimed: epoch(8),
            }
        );
        assert_eq!(
            transferred.validate_claim(initial),
            WriteClaimVerdict::StaleEpoch {
                expected: epoch(8),
                claimed: epoch(7),
            }
        );
    }

    #[test]
    fn transfer_rejects_same_owner_and_epoch_exhaustion() {
        let initial = OwnershipLease::new(entity(9), cell(1), epoch(7));
        assert_eq!(
            initial.next_owner(cell(1)),
            Err(OwnershipTransferError::SameOwner(cell(1)))
        );

        let exhausted = OwnershipLease::new(entity(9), cell(1), epoch(u64::MAX));
        assert_eq!(
            exhausted.next_owner(cell(2)),
            Err(OwnershipTransferError::EpochExhausted(epoch(u64::MAX)))
        );
    }

    #[test]
    fn writer_claim_requires_entity_owner_and_epoch() {
        let authority = OwnershipLease::new(entity(9), cell(2), epoch(8));
        assert_eq!(
            authority.validate_claim(authority),
            WriteClaimVerdict::Accepted
        );
        assert!(matches!(
            authority.validate_claim(OwnershipLease::new(entity(10), cell(2), epoch(8))),
            WriteClaimVerdict::WrongEntity { .. }
        ));
        assert!(matches!(
            authority.validate_claim(OwnershipLease::new(entity(9), cell(3), epoch(8))),
            WriteClaimVerdict::WrongOwner { .. }
        ));
    }

    #[test]
    fn ghost_age_budget_is_inclusive() {
        let authority = OwnershipLease::new(entity(9), cell(2), epoch(8));
        let boundary = GhostStamp::new(entity(9), cell(2), epoch(8), Tick::new(90));

        assert_eq!(
            evaluate_ghost(boundary, authority, Tick::new(100), 10, None),
            GhostVerdict::Fresh { age_ticks: 10 }
        );
        assert_eq!(
            evaluate_ghost(boundary, authority, Tick::new(101), 10, None),
            GhostVerdict::Expired {
                age_ticks: 11,
                max_age_ticks: 10,
            }
        );
    }

    #[test]
    fn ghost_rejects_authority_time_and_order_mismatches() {
        let authority = OwnershipLease::new(entity(9), cell(2), epoch(8));
        let previous = GhostStamp::new(entity(9), cell(2), epoch(8), Tick::new(90));

        let cases = [
            (
                GhostStamp::new(entity(10), cell(2), epoch(8), Tick::new(91)),
                GhostVerdict::WrongEntity {
                    expected: entity(9),
                    received: entity(10),
                },
            ),
            (
                GhostStamp::new(entity(9), cell(3), epoch(8), Tick::new(91)),
                GhostVerdict::WrongOwner {
                    expected: cell(2),
                    received: cell(3),
                },
            ),
            (
                GhostStamp::new(entity(9), cell(1), epoch(7), Tick::new(91)),
                GhostVerdict::StaleEpoch {
                    expected: epoch(8),
                    received: epoch(7),
                },
            ),
            (
                GhostStamp::new(entity(9), cell(2), epoch(9), Tick::new(91)),
                GhostVerdict::FutureEpoch {
                    expected: epoch(8),
                    received: epoch(9),
                },
            ),
            (
                GhostStamp::new(entity(9), cell(2), epoch(8), Tick::new(101)),
                GhostVerdict::FutureTick {
                    local: Tick::new(100),
                    received: Tick::new(101),
                },
            ),
            (
                previous,
                GhostVerdict::Duplicate {
                    source_tick: Tick::new(90),
                },
            ),
            (
                GhostStamp::new(entity(9), cell(2), epoch(8), Tick::new(89)),
                GhostVerdict::Reordered {
                    last_accepted: Tick::new(90),
                    received: Tick::new(89),
                },
            ),
        ];

        for (incoming, expected) in cases {
            assert_eq!(
                evaluate_ghost(incoming, authority, Tick::new(100), 20, Some(previous)),
                expected
            );
        }
    }

    #[test]
    fn prior_epoch_does_not_block_first_snapshot_after_transfer() {
        let authority = OwnershipLease::new(entity(9), cell(3), epoch(9));
        let previous = GhostStamp::new(entity(9), cell(2), epoch(8), Tick::new(100));
        let incoming = GhostStamp::new(entity(9), cell(3), epoch(9), Tick::new(95));

        assert_eq!(
            evaluate_ghost(incoming, authority, Tick::new(100), 10, Some(previous)),
            GhostVerdict::Fresh { age_ticks: 5 }
        );
    }
}
