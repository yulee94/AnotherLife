//! Deterministic fixed-tick microcell simulation reference.
//!
//! The implementation is deliberately single-loop and engine-free. Entity
//! components live in separate contiguous arrays, intents are immutable inputs
//! reduced in canonical order, and the derived spatial index uses bounded CSR
//! arrays rather than one allocation per entity or grid cell. This is a
//! correctness and profiling reference, not a claim of production battle scale.

use crate::ownership::{EntityId, Tick};
use std::cmp::Ordering;
use std::fmt;
use std::num::NonZeroU64;

/// Hard safety ceiling for entities in one reference microcell.
pub const MAX_ENTITY_CAPACITY: usize = 1_000_000;
/// Hard safety ceiling for immutable intents reduced in one tick.
pub const MAX_INTENT_CAPACITY: usize = 4_000_000;
/// Hard safety ceiling for cells in one uniform grid.
pub const MAX_GRID_CELLS: usize = 1_048_576;

/// Two-dimensional integer simulation position.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct Position2 {
    x: i32,
    y: i32,
}

impl Position2 {
    /// Creates an integer position in caller-defined world units.
    #[must_use]
    pub const fn new(x: i32, y: i32) -> Self {
        Self { x, y }
    }

    /// Returns the horizontal coordinate.
    #[must_use]
    pub const fn x(self) -> i32 {
        self.x
    }

    /// Returns the vertical coordinate.
    #[must_use]
    pub const fn y(self) -> i32 {
        self.y
    }
}

/// Per-tick integer displacement.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct Velocity2 {
    x: i32,
    y: i32,
}

impl Velocity2 {
    /// Creates a per-tick displacement.
    #[must_use]
    pub const fn new(x: i32, y: i32) -> Self {
        Self { x, y }
    }

    /// Returns horizontal displacement per tick.
    #[must_use]
    pub const fn x(self) -> i32 {
        self.x
    }

    /// Returns vertical displacement per tick.
    #[must_use]
    pub const fn y(self) -> i32 {
        self.y
    }
}

/// Complete state needed to spawn one reference entity.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct EntityState {
    id: EntityId,
    position: Position2,
    velocity: Velocity2,
}

impl EntityState {
    /// Creates one entity state.
    #[must_use]
    pub const fn new(id: EntityId, position: Position2, velocity: Velocity2) -> Self {
        Self {
            id,
            position,
            velocity,
        }
    }

    /// Returns the stable entity identity.
    #[must_use]
    pub const fn id(self) -> EntityId {
        self.id
    }

    /// Returns the position.
    #[must_use]
    pub const fn position(self) -> Position2 {
        self.position
    }

    /// Returns the velocity.
    #[must_use]
    pub const fn velocity(self) -> Velocity2 {
        self.velocity
    }
}

/// Immutable entity view returned by the simulation.
pub type EntitySnapshot = EntityState;

/// Stable nonzero identity of an intent-producing system or authority.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct IntentSourceId(NonZeroU64);

impl IntentSourceId {
    /// Creates a source identity; zero is reserved as invalid.
    #[must_use]
    pub const fn new(value: u64) -> Option<Self> {
        match NonZeroU64::new(value) {
            Some(value) => Some(Self(value)),
            None => None,
        }
    }

    /// Returns the numeric source identity.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0.get()
    }
}

/// Immutable request to replace one entity's velocity before integration.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct VelocityIntent {
    target: EntityId,
    priority: u16,
    source: IntentSourceId,
    sequence: u64,
    velocity: Velocity2,
}

impl VelocityIntent {
    /// Creates a velocity intent.
    ///
    /// Higher priority wins. Equal priorities are ordered by source ID and then
    /// sequence, both ascending. Duplicate arbitration keys with different
    /// values reject the complete tick as ambiguous.
    #[must_use]
    pub const fn new(
        target: EntityId,
        priority: u16,
        source: IntentSourceId,
        sequence: u64,
        velocity: Velocity2,
    ) -> Self {
        Self {
            target,
            priority,
            source,
            sequence,
            velocity,
        }
    }

    /// Returns the target entity.
    #[must_use]
    pub const fn target(self) -> EntityId {
        self.target
    }

    fn arbitration_cmp(&self, other: &Self) -> Ordering {
        self.target
            .cmp(&other.target)
            .then_with(|| other.priority.cmp(&self.priority))
            .then_with(|| self.source.cmp(&other.source))
            .then_with(|| self.sequence.cmp(&other.sequence))
    }

    fn same_arbitration_key(self, other: Self) -> bool {
        self.target == other.target
            && self.priority == other.priority
            && self.source == other.source
            && self.sequence == other.sequence
    }
}

/// Fixed uniform-grid geometry.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct GridSpec {
    origin: Position2,
    cell_size: u32,
    width: u32,
    height: u32,
    cell_count: usize,
    max_x_exclusive: i64,
    max_y_exclusive: i64,
}

impl GridSpec {
    /// Creates bounded grid geometry.
    pub fn new(
        origin: Position2,
        cell_size: u32,
        width: u32,
        height: u32,
    ) -> Result<Self, GridSpecError> {
        if cell_size == 0 {
            return Err(GridSpecError::ZeroCellSize);
        }
        if width == 0 || height == 0 {
            return Err(GridSpecError::ZeroDimension);
        }

        let cell_count_u64 = u64::from(width)
            .checked_mul(u64::from(height))
            .ok_or(GridSpecError::CellCountOverflow)?;
        let cell_count =
            usize::try_from(cell_count_u64).map_err(|_| GridSpecError::CellCountOverflow)?;
        if cell_count > MAX_GRID_CELLS {
            return Err(GridSpecError::TooManyCells {
                requested: cell_count,
                max: MAX_GRID_CELLS,
            });
        }

        let span_x = i64::from(cell_size)
            .checked_mul(i64::from(width))
            .ok_or(GridSpecError::ExtentOverflow)?;
        let span_y = i64::from(cell_size)
            .checked_mul(i64::from(height))
            .ok_or(GridSpecError::ExtentOverflow)?;
        let max_x_exclusive = i64::from(origin.x)
            .checked_add(span_x)
            .ok_or(GridSpecError::ExtentOverflow)?;
        let max_y_exclusive = i64::from(origin.y)
            .checked_add(span_y)
            .ok_or(GridSpecError::ExtentOverflow)?;
        let coordinate_ceiling = i64::from(i32::MAX) + 1;
        if max_x_exclusive > coordinate_ceiling || max_y_exclusive > coordinate_ceiling {
            return Err(GridSpecError::ExtentOutsideCoordinateDomain);
        }

        Ok(Self {
            origin,
            cell_size,
            width,
            height,
            cell_count,
            max_x_exclusive,
            max_y_exclusive,
        })
    }

    /// Returns the inclusive minimum coordinate.
    #[must_use]
    pub const fn origin(self) -> Position2 {
        self.origin
    }

    /// Returns one square cell's edge length.
    #[must_use]
    pub const fn cell_size(self) -> u32 {
        self.cell_size
    }

    /// Returns horizontal cell count.
    #[must_use]
    pub const fn width(self) -> u32 {
        self.width
    }

    /// Returns vertical cell count.
    #[must_use]
    pub const fn height(self) -> u32 {
        self.height
    }

    /// Returns total grid cells.
    #[must_use]
    pub const fn cell_count(self) -> usize {
        self.cell_count
    }

    /// Reports whether a position lies inside the half-open grid bounds.
    #[must_use]
    pub fn contains(self, position: Position2) -> bool {
        let x = i64::from(position.x);
        let y = i64::from(position.y);
        x >= i64::from(self.origin.x)
            && x < self.max_x_exclusive
            && y >= i64::from(self.origin.y)
            && y < self.max_y_exclusive
    }

    fn cell_index(self, position: Position2) -> Option<usize> {
        if !self.contains(position) {
            return None;
        }
        let x = (i64::from(position.x) - i64::from(self.origin.x)) / i64::from(self.cell_size);
        let y = (i64::from(position.y) - i64::from(self.origin.y)) / i64::from(self.cell_size);
        let index = y * i64::from(self.width) + x;
        usize::try_from(index).ok()
    }

    fn query_cells(self, center: Position2, radius: u32) -> Option<CellRange> {
        let radius = i64::from(radius);
        let query_min_x = i64::from(center.x) - radius;
        let query_max_x = i64::from(center.x) + radius;
        let query_min_y = i64::from(center.y) - radius;
        let query_max_y = i64::from(center.y) + radius;
        let grid_min_x = i64::from(self.origin.x);
        let grid_min_y = i64::from(self.origin.y);
        let grid_max_x = self.max_x_exclusive - 1;
        let grid_max_y = self.max_y_exclusive - 1;

        if query_max_x < grid_min_x
            || query_min_x > grid_max_x
            || query_max_y < grid_min_y
            || query_min_y > grid_max_y
        {
            return None;
        }

        let min_x = query_min_x.max(grid_min_x);
        let max_x = query_max_x.min(grid_max_x);
        let min_y = query_min_y.max(grid_min_y);
        let max_y = query_max_y.min(grid_max_y);
        let size = i64::from(self.cell_size);
        Some(CellRange {
            min_x: u32::try_from((min_x - grid_min_x) / size).ok()?,
            max_x: u32::try_from((max_x - grid_min_x) / size).ok()?,
            min_y: u32::try_from((min_y - grid_min_y) / size).ok()?,
            max_y: u32::try_from((max_y - grid_min_y) / size).ok()?,
        })
    }
}

/// Invalid grid geometry.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum GridSpecError {
    /// Cell edge length must be nonzero.
    ZeroCellSize,
    /// Width and height must both be nonzero.
    ZeroDimension,
    /// Width multiplied by height cannot be represented.
    CellCountOverflow,
    /// The grid exceeds its absolute cell-count ceiling.
    TooManyCells {
        /// Requested grid cells.
        requested: usize,
        /// Absolute supported maximum.
        max: usize,
    },
    /// World-space extent arithmetic overflowed.
    ExtentOverflow,
    /// Maximum coordinate cannot be represented by [`Position2`].
    ExtentOutsideCoordinateDomain,
}

impl fmt::Display for GridSpecError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for GridSpecError {}

/// Bounded allocation policy for one microcell.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct MicrocellConfig {
    entity_capacity: usize,
    intent_capacity: usize,
    grid: GridSpec,
}

impl MicrocellConfig {
    /// Creates a bounded configuration below absolute safety ceilings.
    pub fn new(
        entity_capacity: usize,
        intent_capacity: usize,
        grid: GridSpec,
    ) -> Result<Self, MicrocellConfigError> {
        if entity_capacity > MAX_ENTITY_CAPACITY {
            return Err(MicrocellConfigError::EntityCapacityTooLarge {
                requested: entity_capacity,
                max: MAX_ENTITY_CAPACITY,
            });
        }
        if intent_capacity > MAX_INTENT_CAPACITY {
            return Err(MicrocellConfigError::IntentCapacityTooLarge {
                requested: intent_capacity,
                max: MAX_INTENT_CAPACITY,
            });
        }
        Ok(Self {
            entity_capacity,
            intent_capacity,
            grid,
        })
    }

    /// Returns maximum resident entities.
    #[must_use]
    pub const fn entity_capacity(self) -> usize {
        self.entity_capacity
    }

    /// Returns maximum immutable intents accepted in one tick.
    #[must_use]
    pub const fn intent_capacity(self) -> usize {
        self.intent_capacity
    }

    /// Returns spatial grid geometry.
    #[must_use]
    pub const fn grid(self) -> GridSpec {
        self.grid
    }
}

/// Invalid microcell allocation policy.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum MicrocellConfigError {
    /// Requested resident capacity exceeds the hard ceiling.
    EntityCapacityTooLarge {
        /// Requested capacity.
        requested: usize,
        /// Absolute maximum.
        max: usize,
    },
    /// Requested per-tick intent capacity exceeds the hard ceiling.
    IntentCapacityTooLarge {
        /// Requested capacity.
        requested: usize,
        /// Absolute maximum.
        max: usize,
    },
}

impl fmt::Display for MicrocellConfigError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for MicrocellConfigError {}

/// Failure to spawn a resident entity.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum SpawnError {
    /// The entity ID already exists.
    DuplicateEntity(EntityId),
    /// Resident capacity is full and no partial insert occurred.
    CapacityExceeded {
        /// Configured resident limit.
        capacity: usize,
    },
    /// Initial position lies outside the microcell grid.
    PositionOutsideGrid {
        /// Rejected entity.
        entity: EntityId,
        /// Rejected position.
        position: Position2,
    },
}

impl fmt::Display for SpawnError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for SpawnError {}

/// Failure to advance one atomic fixed tick.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum StepError {
    /// The requested target is not exactly the next fixed tick.
    UnexpectedTargetTick {
        /// Required next tick.
        expected: Tick,
        /// Requested tick.
        received: Tick,
    },
    /// No subsequent tick can be represented.
    TickExhausted,
    /// Immutable intent count exceeds configured scratch capacity.
    IntentCapacityExceeded {
        /// Received intents.
        received: usize,
        /// Configured maximum.
        capacity: usize,
    },
    /// An intent targets no resident entity.
    UnknownEntity(EntityId),
    /// One arbitration identity supplied conflicting values.
    AmbiguousIntent {
        /// Conflicted target.
        target: EntityId,
        /// Conflicted source.
        source: IntentSourceId,
        /// Conflicted source sequence.
        sequence: u64,
    },
    /// Position integration overflowed its integer coordinate.
    PositionOverflow {
        /// Entity that overflowed.
        entity: EntityId,
        /// Previous position.
        position: Position2,
        /// Applied velocity.
        velocity: Velocity2,
    },
    /// Integrated position would leave the owned grid.
    PositionOutsideGrid {
        /// Entity that would leave.
        entity: EntityId,
        /// Rejected next position.
        position: Position2,
    },
    /// Derived index construction observed inconsistent component state.
    SpatialIndexInvariant {
        /// Entity with an invalid derived position.
        entity: EntityId,
        /// Invalid derived position.
        position: Position2,
    },
}

impl fmt::Display for StepError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for StepError {}

/// Successful fixed-tick reduction and integration report.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct StepReport {
    tick: Tick,
    entity_count: usize,
    intents_received: usize,
    intents_applied: usize,
}

impl StepReport {
    /// Returns the committed simulation tick.
    #[must_use]
    pub const fn tick(self) -> Tick {
        self.tick
    }

    /// Returns resident entity count.
    #[must_use]
    pub const fn entity_count(self) -> usize {
        self.entity_count
    }

    /// Returns immutable intents submitted for reduction.
    #[must_use]
    pub const fn intents_received(self) -> usize {
        self.intents_received
    }

    /// Returns winning per-entity intents applied.
    #[must_use]
    pub const fn intents_applied(self) -> usize {
        self.intents_applied
    }
}

/// Caller-owned bounded result storage for radius queries.
#[derive(Debug, Eq, PartialEq)]
pub struct RadiusQueryBuffer {
    ids: Vec<EntityId>,
    capacity: usize,
}

impl RadiusQueryBuffer {
    /// Allocates result storage with an explicit logical capacity.
    pub fn new(capacity: usize) -> Result<Self, RadiusQueryBufferError> {
        if capacity > MAX_ENTITY_CAPACITY {
            return Err(RadiusQueryBufferError::CapacityTooLarge {
                requested: capacity,
                max: MAX_ENTITY_CAPACITY,
            });
        }
        Ok(Self {
            ids: Vec::with_capacity(capacity),
            capacity,
        })
    }

    /// Returns canonical entity-ID-ordered query results.
    #[must_use]
    pub fn ids(&self) -> &[EntityId] {
        &self.ids
    }

    /// Returns the logical result ceiling.
    #[must_use]
    pub const fn capacity(&self) -> usize {
        self.capacity
    }
}

/// Invalid caller-owned query result capacity.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RadiusQueryBufferError {
    /// Requested results exceed the absolute entity ceiling.
    CapacityTooLarge {
        /// Requested result slots.
        requested: usize,
        /// Absolute maximum result slots.
        max: usize,
    },
}

impl fmt::Display for RadiusQueryBufferError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for RadiusQueryBufferError {}

/// Work and match counts for one successful spatial query.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct RadiusQueryStats {
    candidates_examined: usize,
    matches: usize,
}

impl RadiusQueryStats {
    /// Returns entities examined after grid broadphase selection.
    #[must_use]
    pub const fn candidates_examined(self) -> usize {
        self.candidates_examined
    }

    /// Returns entities inside the exact circular radius.
    #[must_use]
    pub const fn matches(self) -> usize {
        self.matches
    }
}

/// Failure to produce a complete radius-query result.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RadiusQueryError {
    /// Complete canonical results exceed caller-owned capacity.
    ResultCapacityExceeded {
        /// Required complete result slots.
        required: usize,
        /// Available logical slots.
        capacity: usize,
        /// Candidates examined before exact filtering.
        candidates_examined: usize,
    },
    /// A resident component violated grid invariants.
    SpatialIndexInvariant {
        /// Invalid resident entity.
        entity: EntityId,
        /// Invalid resident position.
        position: Position2,
    },
    /// Derived row-major cell index cannot be represented on this target.
    GridIndexOverflow,
}

impl fmt::Display for RadiusQueryError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for RadiusQueryError {}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct CellRange {
    min_x: u32,
    max_x: u32,
    min_y: u32,
    max_y: u32,
}

#[derive(Debug)]
struct EntityStore {
    capacity: usize,
    ids: Vec<EntityId>,
    position_x: Vec<i32>,
    position_y: Vec<i32>,
    velocity_x: Vec<i32>,
    velocity_y: Vec<i32>,
}

impl EntityStore {
    fn new(capacity: usize) -> Self {
        Self {
            capacity,
            ids: Vec::with_capacity(capacity),
            position_x: Vec::with_capacity(capacity),
            position_y: Vec::with_capacity(capacity),
            velocity_x: Vec::with_capacity(capacity),
            velocity_y: Vec::with_capacity(capacity),
        }
    }

    fn len(&self) -> usize {
        self.ids.len()
    }

    fn position(&self, index: usize) -> Position2 {
        Position2::new(self.position_x[index], self.position_y[index])
    }

    fn velocity(&self, index: usize) -> Velocity2 {
        Velocity2::new(self.velocity_x[index], self.velocity_y[index])
    }

    fn snapshot(&self, index: usize) -> EntitySnapshot {
        EntityState::new(self.ids[index], self.position(index), self.velocity(index))
    }

    fn index_of(&self, id: EntityId) -> Result<usize, usize> {
        self.ids.binary_search(&id)
    }

    fn insert(&mut self, state: EntityState) -> Result<(), SpawnError> {
        let index = match self.index_of(state.id) {
            Ok(_) => return Err(SpawnError::DuplicateEntity(state.id)),
            Err(index) => index,
        };
        if self.len() >= self.capacity {
            return Err(SpawnError::CapacityExceeded {
                capacity: self.capacity,
            });
        }
        self.ids.insert(index, state.id);
        self.position_x.insert(index, state.position.x);
        self.position_y.insert(index, state.position.y);
        self.velocity_x.insert(index, state.velocity.x);
        self.velocity_y.insert(index, state.velocity.y);
        Ok(())
    }

    fn remove(&mut self, id: EntityId) -> Option<EntitySnapshot> {
        let index = self.index_of(id).ok()?;
        let snapshot = self.snapshot(index);
        self.ids.remove(index);
        self.position_x.remove(index);
        self.position_y.remove(index);
        self.velocity_x.remove(index);
        self.velocity_y.remove(index);
        Some(snapshot)
    }

    fn commit_components(
        &mut self,
        position_x: &[i32],
        position_y: &[i32],
        velocity_x: &[i32],
        velocity_y: &[i32],
    ) {
        self.position_x.copy_from_slice(position_x);
        self.position_y.copy_from_slice(position_y);
        self.velocity_x.copy_from_slice(velocity_x);
        self.velocity_y.copy_from_slice(velocity_y);
    }
}

#[derive(Debug)]
struct UniformGrid {
    spec: GridSpec,
    counts: Vec<usize>,
    offsets: Vec<usize>,
    cursors: Vec<usize>,
    members: Vec<usize>,
}

impl UniformGrid {
    fn new(spec: GridSpec, entity_capacity: usize) -> Self {
        Self {
            spec,
            counts: vec![0; spec.cell_count],
            offsets: vec![0; spec.cell_count + 1],
            cursors: vec![0; spec.cell_count],
            members: Vec::with_capacity(entity_capacity),
        }
    }

    fn rebuild(
        &mut self,
        ids: &[EntityId],
        position_x: &[i32],
        position_y: &[i32],
    ) -> Result<(), (EntityId, Position2)> {
        self.counts.fill(0);
        for (index, id) in ids.iter().copied().enumerate() {
            let position = Position2::new(position_x[index], position_y[index]);
            let cell = self.spec.cell_index(position).ok_or((id, position))?;
            self.counts[cell] += 1;
        }

        self.offsets[0] = 0;
        for cell in 0..self.counts.len() {
            self.offsets[cell + 1] = self.offsets[cell] + self.counts[cell];
            self.cursors[cell] = self.offsets[cell];
        }
        self.members.clear();
        self.members.resize(ids.len(), 0);
        for index in 0..ids.len() {
            let position = Position2::new(position_x[index], position_y[index]);
            let cell = self
                .spec
                .cell_index(position)
                .ok_or((ids[index], position))?;
            let destination = self.cursors[cell];
            self.members[destination] = index;
            self.cursors[cell] += 1;
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct ReducedVelocity {
    entity_index: usize,
    velocity: Velocity2,
}

/// Bounded deterministic reference simulation for one spatial microcell.
#[derive(Debug)]
pub struct FixedTickMicrocell {
    config: MicrocellConfig,
    tick: Tick,
    entities: EntityStore,
    grid: UniformGrid,
    next_grid: UniformGrid,
    grid_dirty: bool,
    intent_scratch: Vec<VelocityIntent>,
    reduced: Vec<ReducedVelocity>,
    next_position_x: Vec<i32>,
    next_position_y: Vec<i32>,
    next_velocity_x: Vec<i32>,
    next_velocity_y: Vec<i32>,
}

impl FixedTickMicrocell {
    /// Allocates one bounded microcell with empty resident state.
    #[must_use]
    pub fn new(config: MicrocellConfig, initial_tick: Tick) -> Self {
        Self {
            config,
            tick: initial_tick,
            entities: EntityStore::new(config.entity_capacity),
            grid: UniformGrid::new(config.grid, config.entity_capacity),
            next_grid: UniformGrid::new(config.grid, config.entity_capacity),
            grid_dirty: false,
            intent_scratch: Vec::with_capacity(config.intent_capacity),
            reduced: Vec::with_capacity(config.entity_capacity),
            next_position_x: Vec::with_capacity(config.entity_capacity),
            next_position_y: Vec::with_capacity(config.entity_capacity),
            next_velocity_x: Vec::with_capacity(config.entity_capacity),
            next_velocity_y: Vec::with_capacity(config.entity_capacity),
        }
    }

    /// Returns the committed fixed tick.
    #[must_use]
    pub const fn tick(&self) -> Tick {
        self.tick
    }

    /// Returns resident count.
    #[must_use]
    pub fn entity_count(&self) -> usize {
        self.entities.len()
    }

    /// Returns one entity by stable ID.
    #[must_use]
    pub fn entity(&self, id: EntityId) -> Option<EntitySnapshot> {
        self.entities
            .index_of(id)
            .ok()
            .map(|index| self.entities.snapshot(index))
    }

    /// Iterates immutable snapshots in ascending entity-ID order.
    pub fn entities(&self) -> impl ExactSizeIterator<Item = EntitySnapshot> + '_ {
        (0..self.entities.len()).map(|index| self.entities.snapshot(index))
    }

    /// Adds one resident without reallocating past configured capacity.
    ///
    /// Spatial index rebuilding is deferred until the next query or tick, so a
    /// setup batch does not perform one grid rebuild per entity.
    pub fn spawn(&mut self, state: EntityState) -> Result<(), SpawnError> {
        if !self.config.grid.contains(state.position) {
            return Err(SpawnError::PositionOutsideGrid {
                entity: state.id,
                position: state.position,
            });
        }
        self.entities.insert(state)?;
        self.grid_dirty = true;
        Ok(())
    }

    /// Removes and returns one resident by stable ID.
    ///
    /// Component arrays remain in canonical entity-ID order. The derived grid
    /// is rebuilt lazily before the next query, or atomically as part of the
    /// next successful tick. A missing ID leaves all state unchanged.
    pub fn despawn(&mut self, id: EntityId) -> Option<EntitySnapshot> {
        let snapshot = self.entities.remove(id)?;
        self.grid_dirty = true;
        Some(snapshot)
    }

    /// Reduces immutable intents and atomically advances exactly one tick.
    ///
    /// The function creates no entity tasks or threads. It canonicalizes a
    /// bounded copy of the immutable input slice, validates the complete next
    /// state and spatial index, then commits all SoA component arrays together.
    pub fn step(
        &mut self,
        target_tick: Tick,
        intents: &[VelocityIntent],
    ) -> Result<StepReport, StepError> {
        let expected_tick = self.tick.checked_add(1).ok_or(StepError::TickExhausted)?;
        if target_tick != expected_tick {
            return Err(StepError::UnexpectedTargetTick {
                expected: expected_tick,
                received: target_tick,
            });
        }
        if intents.len() > self.config.intent_capacity {
            return Err(StepError::IntentCapacityExceeded {
                received: intents.len(),
                capacity: self.config.intent_capacity,
            });
        }

        self.intent_scratch.clear();
        self.intent_scratch.extend_from_slice(intents);
        self.intent_scratch
            .sort_unstable_by(VelocityIntent::arbitration_cmp);
        self.reduce_intents()?;
        self.prepare_next_components()?;
        self.next_grid
            .rebuild(
                &self.entities.ids,
                &self.next_position_x,
                &self.next_position_y,
            )
            .map_err(|(entity, position)| StepError::SpatialIndexInvariant { entity, position })?;

        self.entities.commit_components(
            &self.next_position_x,
            &self.next_position_y,
            &self.next_velocity_x,
            &self.next_velocity_y,
        );
        std::mem::swap(&mut self.grid, &mut self.next_grid);
        self.grid_dirty = false;
        self.tick = target_tick;
        Ok(StepReport {
            tick: target_tick,
            entity_count: self.entities.len(),
            intents_received: intents.len(),
            intents_applied: self.reduced.len(),
        })
    }

    /// Executes a bounded exact-radius query with canonical result ordering.
    ///
    /// Grid cells are visited in row-major order and candidate members are
    /// stable by entity ID. Successful output is sorted globally by entity ID.
    /// On capacity overflow the buffer is cleared; no partial result escapes.
    pub fn query_radius(
        &mut self,
        center: Position2,
        radius: u32,
        output: &mut RadiusQueryBuffer,
    ) -> Result<RadiusQueryStats, RadiusQueryError> {
        self.ensure_grid()?;
        output.ids.clear();
        let Some(range) = self.config.grid.query_cells(center, radius) else {
            return Ok(RadiusQueryStats {
                candidates_examined: 0,
                matches: 0,
            });
        };

        let radius_squared = i128::from(radius) * i128::from(radius);
        let mut candidates_examined = 0_usize;
        let mut matches = 0_usize;
        for y in range.min_y..=range.max_y {
            for x in range.min_x..=range.max_x {
                let cell = usize::try_from(
                    u64::from(y) * u64::from(self.config.grid.width) + u64::from(x),
                )
                .map_err(|_| RadiusQueryError::GridIndexOverflow)?;
                let start = self.grid.offsets[cell];
                let end = self.grid.offsets[cell + 1];
                for member in &self.grid.members[start..end] {
                    candidates_examined += 1;
                    let position = self.entities.position(*member);
                    if squared_distance(position, center) <= radius_squared {
                        matches += 1;
                        if output.ids.len() < output.capacity {
                            output.ids.push(self.entities.ids[*member]);
                        }
                    }
                }
            }
        }

        if matches > output.capacity {
            output.ids.clear();
            return Err(RadiusQueryError::ResultCapacityExceeded {
                required: matches,
                capacity: output.capacity,
                candidates_examined,
            });
        }
        output.ids.sort_unstable();
        Ok(RadiusQueryStats {
            candidates_examined,
            matches,
        })
    }

    fn ensure_grid(&mut self) -> Result<(), RadiusQueryError> {
        if !self.grid_dirty {
            return Ok(());
        }
        self.grid
            .rebuild(
                &self.entities.ids,
                &self.entities.position_x,
                &self.entities.position_y,
            )
            .map_err(
                |(entity, position)| RadiusQueryError::SpatialIndexInvariant { entity, position },
            )?;
        self.grid_dirty = false;
        Ok(())
    }

    fn reduce_intents(&mut self) -> Result<(), StepError> {
        self.reduced.clear();
        for pair in self.intent_scratch.windows(2) {
            if pair[0].same_arbitration_key(pair[1]) && pair[0].velocity != pair[1].velocity {
                return Err(StepError::AmbiguousIntent {
                    target: pair[0].target,
                    source: pair[0].source,
                    sequence: pair[0].sequence,
                });
            }
        }

        let mut cursor = 0_usize;
        while cursor < self.intent_scratch.len() {
            let winner = self.intent_scratch[cursor];
            let entity_index = self
                .entities
                .index_of(winner.target)
                .map_err(|_| StepError::UnknownEntity(winner.target))?;
            self.reduced.push(ReducedVelocity {
                entity_index,
                velocity: winner.velocity,
            });
            cursor += 1;
            while cursor < self.intent_scratch.len()
                && self.intent_scratch[cursor].target == winner.target
            {
                cursor += 1;
            }
        }
        Ok(())
    }

    fn prepare_next_components(&mut self) -> Result<(), StepError> {
        self.next_position_x.clear();
        self.next_position_y.clear();
        self.next_velocity_x.clear();
        self.next_velocity_y.clear();

        let mut reduced_cursor = 0_usize;
        for index in 0..self.entities.len() {
            let velocity = if reduced_cursor < self.reduced.len()
                && self.reduced[reduced_cursor].entity_index == index
            {
                let velocity = self.reduced[reduced_cursor].velocity;
                reduced_cursor += 1;
                velocity
            } else {
                self.entities.velocity(index)
            };
            let position = self.entities.position(index);
            let next_x = position.x.checked_add(velocity.x);
            let next_y = position.y.checked_add(velocity.y);
            let next_position = match (next_x, next_y) {
                (Some(x), Some(y)) => Position2::new(x, y),
                _ => {
                    return Err(StepError::PositionOverflow {
                        entity: self.entities.ids[index],
                        position,
                        velocity,
                    })
                }
            };
            if !self.config.grid.contains(next_position) {
                return Err(StepError::PositionOutsideGrid {
                    entity: self.entities.ids[index],
                    position: next_position,
                });
            }
            self.next_position_x.push(next_position.x);
            self.next_position_y.push(next_position.y);
            self.next_velocity_x.push(velocity.x);
            self.next_velocity_y.push(velocity.y);
        }
        Ok(())
    }
}

fn squared_distance(left: Position2, right: Position2) -> i128 {
    let dx = i128::from(left.x) - i128::from(right.x);
    let dy = i128::from(left.y) - i128::from(right.y);
    dx * dx + dy * dy
}

#[cfg(test)]
mod tests {
    use super::*;

    fn entity(value: u64) -> EntityId {
        EntityId::new(value).expect("test entity IDs are nonzero")
    }

    fn source(value: u64) -> IntentSourceId {
        IntentSourceId::new(value).expect("test source IDs are nonzero")
    }

    fn grid() -> GridSpec {
        GridSpec::new(Position2::new(0, 0), 10, 20, 20).expect("valid test grid")
    }

    fn config(entity_capacity: usize, intent_capacity: usize) -> MicrocellConfig {
        MicrocellConfig::new(entity_capacity, intent_capacity, grid()).expect("valid test config")
    }

    fn state(id: u64, x: i32, y: i32) -> EntityState {
        EntityState::new(entity(id), Position2::new(x, y), Velocity2::default())
    }

    fn brute_force(cell: &FixedTickMicrocell, center: Position2, radius: u32) -> Vec<EntityId> {
        let radius_squared = i128::from(radius) * i128::from(radius);
        cell.entities()
            .filter(|snapshot| {
                let position = snapshot.position();
                let dx = i128::from(position.x()) - i128::from(center.x());
                let dy = i128::from(position.y()) - i128::from(center.y());
                dx * dx + dy * dy <= radius_squared
            })
            .map(EntityState::id)
            .collect()
    }

    fn assert_query_matches_oracle(
        cell: &mut FixedTickMicrocell,
        center: Position2,
        radius: u32,
    ) -> RadiusQueryStats {
        let expected = brute_force(cell, center, radius);
        let mut output =
            RadiusQueryBuffer::new(cell.entity_count()).expect("bounded query buffer capacity");
        let stats = cell
            .query_radius(center, radius, &mut output)
            .expect("full-capacity query succeeds");
        assert_eq!(output.ids(), expected);
        assert_eq!(stats.matches(), expected.len());
        stats
    }

    #[test]
    fn grid_and_config_bounds_are_explicit() {
        assert_eq!(
            GridSpec::new(Position2::new(0, 0), 0, 1, 1),
            Err(GridSpecError::ZeroCellSize)
        );
        assert_eq!(
            GridSpec::new(Position2::new(0, 0), 1, 0, 1),
            Err(GridSpecError::ZeroDimension)
        );
        assert!(matches!(
            GridSpec::new(Position2::new(0, 0), 1, 1_025, 1_025),
            Err(GridSpecError::TooManyCells { .. })
        ));
        assert!(matches!(
            MicrocellConfig::new(MAX_ENTITY_CAPACITY + 1, 0, grid()),
            Err(MicrocellConfigError::EntityCapacityTooLarge { .. })
        ));
        assert!(matches!(
            RadiusQueryBuffer::new(MAX_ENTITY_CAPACITY + 1),
            Err(RadiusQueryBufferError::CapacityTooLarge { .. })
        ));
    }

    #[test]
    fn spawn_is_sorted_and_capacity_failure_is_atomic() {
        let mut cell = FixedTickMicrocell::new(config(3, 0), Tick::new(0));
        cell.spawn(state(3, 30, 30)).expect("spawn succeeds");
        cell.spawn(state(1, 10, 10)).expect("spawn succeeds");
        cell.spawn(state(2, 20, 20)).expect("spawn succeeds");
        assert_eq!(
            cell.entities().map(EntityState::id).collect::<Vec<_>>(),
            vec![entity(1), entity(2), entity(3)]
        );
        assert_eq!(
            cell.spawn(state(4, 40, 40)),
            Err(SpawnError::CapacityExceeded { capacity: 3 })
        );
        assert_eq!(cell.entity_count(), 3);
        assert_eq!(
            cell.spawn(state(2, 40, 40)),
            Err(SpawnError::DuplicateEntity(entity(2)))
        );
    }

    #[test]
    fn despawn_preserves_order_and_rebuilds_queries_without_partial_state() {
        let mut cell = FixedTickMicrocell::new(config(4, 0), Tick::new(0));
        cell.spawn(state(4, 40, 40)).expect("spawn succeeds");
        cell.spawn(state(2, 20, 20)).expect("spawn succeeds");
        cell.spawn(state(1, 10, 10)).expect("spawn succeeds");
        cell.spawn(state(3, 30, 30)).expect("spawn succeeds");
        assert_query_matches_oracle(&mut cell, Position2::new(25, 25), 100);

        assert_eq!(cell.despawn(entity(9)), None);
        assert_eq!(cell.entity_count(), 4);
        assert_eq!(
            cell.despawn(entity(2)),
            Some(EntityState::new(
                entity(2),
                Position2::new(20, 20),
                Velocity2::default(),
            ))
        );
        assert_eq!(
            cell.entities().map(EntityState::id).collect::<Vec<_>>(),
            vec![entity(1), entity(3), entity(4)]
        );
        assert_eq!(cell.entity_count(), 3);
        let stats = assert_query_matches_oracle(&mut cell, Position2::new(25, 25), 100);
        assert_eq!(stats.matches(), 3);
        assert_eq!(cell.tick(), Tick::new(0));
    }

    #[test]
    fn immutable_intent_reduction_is_order_independent() {
        let mut left = FixedTickMicrocell::new(config(2, 8), Tick::new(10));
        let mut right = FixedTickMicrocell::new(config(2, 8), Tick::new(10));
        for cell in [&mut left, &mut right] {
            cell.spawn(state(1, 50, 50)).expect("spawn succeeds");
            cell.spawn(state(2, 70, 70)).expect("spawn succeeds");
        }
        let intents = [
            VelocityIntent::new(entity(1), 1, source(2), 1, Velocity2::new(1, 0)),
            VelocityIntent::new(entity(1), 2, source(9), 1, Velocity2::new(3, 0)),
            VelocityIntent::new(entity(2), 1, source(1), 2, Velocity2::new(0, -2)),
        ];
        let reversed = [intents[2], intents[1], intents[0]];

        let left_report = left.step(Tick::new(11), &intents).expect("step succeeds");
        let right_report = right.step(Tick::new(11), &reversed).expect("step succeeds");
        assert_eq!(left_report, right_report);
        assert_eq!(
            left.entities().collect::<Vec<_>>(),
            right.entities().collect::<Vec<_>>()
        );
        assert_eq!(
            left.entity(entity(1)).expect("entity exists").position(),
            Position2::new(53, 50)
        );
        assert_eq!(left_report.intents_applied(), 2);
    }

    #[test]
    fn ambiguous_or_oversized_intents_reject_whole_tick() {
        let mut cell = FixedTickMicrocell::new(config(1, 2), Tick::new(4));
        cell.spawn(state(1, 50, 50)).expect("spawn succeeds");
        let original = cell.entity(entity(1));
        let ambiguous = [
            VelocityIntent::new(entity(1), 1, source(1), 7, Velocity2::new(1, 0)),
            VelocityIntent::new(entity(1), 1, source(1), 7, Velocity2::new(2, 0)),
        ];
        assert!(matches!(
            cell.step(Tick::new(5), &ambiguous),
            Err(StepError::AmbiguousIntent { .. })
        ));
        assert_eq!(cell.tick(), Tick::new(4));
        assert_eq!(cell.entity(entity(1)), original);

        let oversized = [ambiguous[0], ambiguous[0], ambiguous[0]];
        assert_eq!(
            cell.step(Tick::new(5), &oversized),
            Err(StepError::IntentCapacityExceeded {
                received: 3,
                capacity: 2,
            })
        );
        assert_eq!(cell.entity(entity(1)), original);
    }

    #[test]
    fn invalid_integration_and_tick_are_atomic() {
        let mut cell = FixedTickMicrocell::new(config(1, 1), Tick::new(4));
        cell.spawn(state(1, 199, 50)).expect("spawn succeeds");
        let original = cell.entity(entity(1));
        assert!(matches!(
            cell.step(
                Tick::new(5),
                &[VelocityIntent::new(
                    entity(1),
                    1,
                    source(1),
                    1,
                    Velocity2::new(1, 0),
                )],
            ),
            Err(StepError::PositionOutsideGrid { .. })
        ));
        assert_eq!(cell.entity(entity(1)), original);
        assert_eq!(cell.tick(), Tick::new(4));
        assert!(matches!(
            cell.step(Tick::new(6), &[]),
            Err(StepError::UnexpectedTargetTick { .. })
        ));
        assert_eq!(
            cell.step(
                Tick::new(5),
                &[VelocityIntent::new(
                    entity(2),
                    1,
                    source(1),
                    2,
                    Velocity2::default(),
                )],
            ),
            Err(StepError::UnknownEntity(entity(2)))
        );
        assert_eq!(cell.entity(entity(1)), original);
    }

    #[test]
    fn coordinate_and_tick_overflow_are_explicit_and_atomic() {
        let edge_grid = GridSpec::new(Position2::new(i32::MAX - 9, 0), 10, 1, 1)
            .expect("maximum coordinate remains representable");
        let edge_config = MicrocellConfig::new(1, 0, edge_grid).expect("valid edge configuration");
        let mut edge = FixedTickMicrocell::new(edge_config, Tick::new(0));
        edge.spawn(EntityState::new(
            entity(1),
            Position2::new(i32::MAX, 0),
            Velocity2::new(1, 0),
        ))
        .expect("edge spawn succeeds");
        let original = edge.entity(entity(1));
        assert!(matches!(
            edge.step(Tick::new(1), &[]),
            Err(StepError::PositionOverflow { .. })
        ));
        assert_eq!(edge.entity(entity(1)), original);
        assert_eq!(edge.tick(), Tick::new(0));

        let exhausted = MicrocellConfig::new(0, 0, grid()).expect("valid empty config");
        let mut exhausted = FixedTickMicrocell::new(exhausted, Tick::new(u64::MAX));
        assert_eq!(
            exhausted.step(Tick::new(u64::MAX), &[]),
            Err(StepError::TickExhausted)
        );
    }

    #[test]
    fn random_queries_match_brute_force_oracle() {
        let mut cell = FixedTickMicrocell::new(config(1_000, 0), Tick::new(0));
        let mut random = 0x8f3d_9a71_b4c2_e605_u64;
        for id in 1..=1_000_u64 {
            random ^= random << 13;
            random ^= random >> 7;
            random ^= random << 17;
            let x = i32::try_from(random % 200).expect("bounded random coordinate");
            let y = i32::try_from(random.rotate_left(19) % 200).expect("bounded random coordinate");
            cell.spawn(state(id, x, y)).expect("spawn succeeds");
        }

        for _ in 0..200 {
            random ^= random << 13;
            random ^= random >> 7;
            random ^= random << 17;
            let center = Position2::new(
                i32::try_from(random % 260).expect("bounded query") - 30,
                i32::try_from(random.rotate_left(23) % 260).expect("bounded query") - 30,
            );
            let radius = u32::try_from(random.rotate_right(9) % 40).expect("bounded radius");
            let stats = assert_query_matches_oracle(&mut cell, center, radius);
            assert!(stats.candidates_examined() <= cell.entity_count());
        }
    }

    #[test]
    fn moving_boundary_queries_match_oracle_each_tick() {
        let mut cell = FixedTickMicrocell::new(config(8, 8), Tick::new(0));
        let positions = [9, 10, 19, 20, 29, 30, 39, 40];
        for (index, x) in positions.into_iter().enumerate() {
            cell.spawn(EntityState::new(
                entity(u64::try_from(index + 1).expect("small ID")),
                Position2::new(x, 50),
                Velocity2::new(if index % 2 == 0 { 1 } else { -1 }, 0),
            ))
            .expect("spawn succeeds");
        }

        for tick in 0..8_u64 {
            assert_query_matches_oracle(&mut cell, Position2::new(25, 50), 17);
            cell.step(Tick::new(tick + 1), &[]).expect("step succeeds");
        }
    }

    #[test]
    fn single_hotspot_reports_full_candidate_set_and_bounded_overflow() {
        let mut cell = FixedTickMicrocell::new(config(512, 0), Tick::new(0));
        for id in (1..=512_u64).rev() {
            cell.spawn(state(id, 55, 55)).expect("spawn succeeds");
        }
        let stats = assert_query_matches_oracle(&mut cell, Position2::new(55, 55), 0);
        assert_eq!(stats.candidates_examined(), 512);
        assert_eq!(stats.matches(), 512);

        let mut bounded = RadiusQueryBuffer::new(10).expect("bounded query buffer capacity");
        assert_eq!(
            cell.query_radius(Position2::new(55, 55), 0, &mut bounded),
            Err(RadiusQueryError::ResultCapacityExceeded {
                required: 512,
                capacity: 10,
                candidates_examined: 512,
            })
        );
        assert!(bounded.ids().is_empty());
    }
}
