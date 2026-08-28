//! Deterministic multi-cell battle workload for bounded local validation.
//!
//! This executable measures reference primitives in one process. It is not a
//! network test, complete combat simulation, production server, CCU claim, or
//! evidence that 10,000 mutually interacting players fit a real-time budget.

#![forbid(unsafe_code)]

use al_server_core::handoff::{
    Handoff, HandoffId, HandoffLimits, HandoffPhase, HandoffTransition, ReadyOutcome,
};
use al_server_core::microcell::{
    EntityState, FixedTickMicrocell, GridSpec, IntentSourceId, MicrocellConfig, Position2,
    RadiusQueryBuffer, RadiusQueryError, Velocity2, VelocityIntent,
};
use al_server_core::ownership::{CellId, EntityId, OwnershipEpoch, OwnershipLease, Tick};
use al_server_core::wire::{
    decode_v1, encode_v1, FrameFlags, FrameHeader, FrameLimits, FrameWindow, InstanceId,
    MessageKind, ReceiveContext, RouteEpoch, RouteScope, WorldId,
};
use std::collections::VecDeque;
use std::error::Error;
use std::fmt;
use std::hint::black_box;
use std::time::Instant;

const OUTPUT_SCHEMA_VERSION: u32 = 1;
const COMPACT_PAGE_HEADER_LEN: usize = 16;
const ENGAGED_RECORD_LEN: usize = 16;
const AWARENESS_RECORD_LEN: usize = 12;
const MASS_RECORD_LEN: usize = 12;
const GHOST_PAYLOAD_HEADER_LEN: usize = 8;
const GHOST_RECORD_LEN: usize = 32;
const HANDOFF_PAYLOAD_LEN: usize = 40;
const FRAME_PAYLOAD_BUDGET: usize = 1_120;
const COHORTS_PER_CELL_SIDE: u32 = 4;

fn main() -> Result<(), Box<dyn Error>> {
    let config = BattleConfig::production_reference();
    println!("{}", config.metadata_json());
    let result = run_battle(config)?;
    println!("{}", result.to_json(config));
    Ok(())
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct BattleConfig {
    entity_count: usize,
    cell_columns: u32,
    cell_rows: u32,
    grid_cell_size: u32,
    grid_cells_per_cell: u32,
    measured_ticks: u64,
    tick_rate_hz: u32,
    replication_period_ticks: u64,
    engaged_radius: u32,
    awareness_radius: u32,
    client_snapshot_budget: usize,
    max_datagram_bytes: usize,
    ghost_margin: u32,
    handoff_first_tick: u64,
    handoff_interval_ticks: u64,
    handoff_pairs_per_boundary: usize,
    queue_max_frames: usize,
    queue_max_bytes: usize,
    queue_drain_frames_per_tick: usize,
    seed: u64,
}

impl BattleConfig {
    const fn production_reference() -> Self {
        Self {
            entity_count: 10_000,
            cell_columns: 4,
            cell_rows: 4,
            grid_cell_size: 64,
            grid_cells_per_cell: 16,
            measured_ticks: 120,
            tick_rate_hz: 30,
            replication_period_ticks: 6,
            engaged_radius: 96,
            awareness_radius: 384,
            client_snapshot_budget: 3_072,
            max_datagram_bytes: 1_200,
            ghost_margin: 64,
            handoff_first_tick: 10,
            handoff_interval_ticks: 30,
            handoff_pairs_per_boundary: 4,
            queue_max_frames: 96,
            queue_max_bytes: 96 * 1_200,
            queue_drain_frames_per_tick: 80,
            seed: 0x41_4c_5f_42_41_54_54_4c,
        }
    }

    fn validate(self) -> Result<(), HarnessError> {
        require(self.entity_count > 1, "entity count must exceed one")?;
        require(
            self.cell_columns > 1 && self.cell_rows > 0,
            "multi-cell dimensions are invalid",
        )?;
        require(
            self.cell_columns % 2 == 0,
            "handoff pairing requires an even column count",
        )?;
        require(
            self.grid_cell_size > 0 && self.grid_cells_per_cell > 0,
            "grid dimensions must be nonzero",
        )?;
        require(self.measured_ticks > 0, "measured ticks must be nonzero")?;
        require(self.tick_rate_hz > 0, "tick rate must be nonzero")?;
        require(
            self.replication_period_ticks > 0,
            "replication period must be nonzero",
        )?;
        require(
            self.engaged_radius < self.awareness_radius,
            "engaged radius must be smaller than awareness radius",
        )?;
        require(
            self.max_datagram_bytes > COMPACT_PAGE_HEADER_LEN + ENGAGED_RECORD_LEN
                && self.max_datagram_bytes <= 1_200,
            "datagram budget must fit records and remain at most 1200 bytes",
        )?;
        require(
            self.client_snapshot_budget >= self.max_datagram_bytes,
            "client snapshot budget must fit one datagram",
        )?;
        let cell_extent = self.cell_extent()?;
        require(
            cell_extent % COHORTS_PER_CELL_SIDE == 0,
            "cell extent must divide evenly into observer cohorts",
        )?;
        require(
            self.ghost_margin > 0 && self.ghost_margin < cell_extent / 2,
            "ghost margin must be within a cell",
        )?;
        require(
            self.handoff_interval_ticks > 2,
            "handoff interval must exceed prepare duration",
        )?;
        require(
            self.handoff_pairs_per_boundary > 0,
            "handoff pairs must be nonzero",
        )?;
        require(
            self.queue_max_frames > 0
                && self.queue_max_bytes >= self.max_datagram_bytes
                && self.queue_drain_frames_per_tick > 0,
            "queue bounds are invalid",
        )?;
        let _ = self.cell_count()?;
        Ok(())
    }

    fn cell_count(self) -> Result<usize, HarnessError> {
        usize::try_from(
            u64::from(self.cell_columns)
                .checked_mul(u64::from(self.cell_rows))
                .ok_or_else(|| HarnessError::new("cell count overflow"))?,
        )
        .map_err(|_| HarnessError::new("cell count exceeds usize"))
    }

    fn cell_extent(self) -> Result<u32, HarnessError> {
        self.grid_cell_size
            .checked_mul(self.grid_cells_per_cell)
            .ok_or_else(|| HarnessError::new("cell extent overflow"))
    }

    fn tick_budget_ns(self) -> u128 {
        1_000_000_000_u128 / u128::from(self.tick_rate_hz)
    }

    fn cell_capacity(self) -> Result<usize, HarnessError> {
        let cells = self.cell_count()?;
        let base = self.entity_count.div_ceil(cells);
        base.checked_add(
            self.handoff_pairs_per_boundary
                .checked_mul(4)
                .ok_or_else(|| HarnessError::new("handoff headroom overflow"))?,
        )
        .ok_or_else(|| HarnessError::new("cell capacity overflow"))
    }

    fn metadata_json(self) -> String {
        format!(
            concat!(
                "{{\"record\":\"metadata\",\"schema_version\":{},",
                "\"reference_only\":true,\"workload\":\"multicell_frontline\",",
                "\"interest_strategy\":\"hierarchical_cohort_v2\",",
                "\"target_arch\":\"{}\",\"target_os\":\"{}\",",
                "\"debug_assertions\":{},\"seed\":{},\"active_entities\":{},",
                "\"cell_columns\":{},\"cell_rows\":{},\"measured_ticks\":{},",
                "\"tick_rate_hz\":{},\"tick_budget_ns\":{},",
                "\"replication_period_ticks\":{},\"engaged_radius\":{},",
                "\"awareness_radius\":{},\"client_snapshot_budget\":{},",
                "\"max_datagram_bytes\":{},\"ghost_margin\":{},",
                "\"compact_page_header_bytes\":{},\"engaged_record_bytes\":{},",
                "\"awareness_record_bytes\":{},\"mass_record_bytes\":{},",
                "\"queue_max_frames\":{},\"queue_max_bytes\":{},",
                "\"queue_drain_frames_per_tick\":{}}}"
            ),
            OUTPUT_SCHEMA_VERSION,
            std::env::consts::ARCH,
            std::env::consts::OS,
            cfg!(debug_assertions),
            self.seed,
            self.entity_count,
            self.cell_columns,
            self.cell_rows,
            self.measured_ticks,
            self.tick_rate_hz,
            self.tick_budget_ns(),
            self.replication_period_ticks,
            self.engaged_radius,
            self.awareness_radius,
            self.client_snapshot_budget,
            self.max_datagram_bytes,
            self.ghost_margin,
            COMPACT_PAGE_HEADER_LEN,
            ENGAGED_RECORD_LEN,
            AWARENESS_RECORD_LEN,
            MASS_RECORD_LEN,
            self.queue_max_frames,
            self.queue_max_bytes,
            self.queue_drain_frames_per_tick,
        )
    }
}

#[derive(Debug, Eq, PartialEq)]
struct HarnessError(String);

impl HarnessError {
    fn new(message: impl Into<String>) -> Self {
        Self(message.into())
    }
}

impl fmt::Display for HarnessError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        self.0.fmt(formatter)
    }
}

impl Error for HarnessError {}

fn require(condition: bool, message: &'static str) -> Result<(), HarnessError> {
    if condition {
        Ok(())
    } else {
        Err(HarnessError::new(message))
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct TimingSummary {
    samples: usize,
    total_ns: u128,
    mean_ns: u128,
    p50_ns: u128,
    p95_ns: u128,
    p99_ns: u128,
    max_ns: u128,
}

impl TimingSummary {
    fn from_samples(samples: &[u128]) -> Result<Self, HarnessError> {
        require(!samples.is_empty(), "timing samples must be nonempty")?;
        let mut sorted = samples.to_vec();
        sorted.sort_unstable();
        let total_ns = sorted.iter().sum();
        let count = sorted.len();
        Ok(Self {
            samples: count,
            total_ns,
            mean_ns: total_ns / count as u128,
            p50_ns: percentile(&sorted, 50),
            p95_ns: percentile(&sorted, 95),
            p99_ns: percentile(&sorted, 99),
            max_ns: sorted[count - 1],
        })
    }
}

fn percentile(sorted: &[u128], percentile: usize) -> u128 {
    let rank = sorted.len().saturating_mul(percentile).saturating_add(99) / 100;
    sorted[rank.saturating_sub(1).min(sorted.len() - 1)]
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct DeterministicMetrics {
    observer_evaluations: u64,
    unique_observers: usize,
    interest_cell_queries: u64,
    interest_candidates: u64,
    engaged_records: u64,
    awareness_desired_records: u64,
    awareness_sent_records: u64,
    awareness_shed_records: u64,
    mass_aggregate_records: u64,
    mass_represented_entities: u64,
    causal_promotions: u64,
    degraded_observers: u64,
    engaged_over_budget_observers: u64,
    client_desired_bytes: u64,
    client_sent_bytes: u64,
    client_datagrams: u64,
    ghost_records_generated: u64,
    ghost_frames_generated: u64,
    ghost_frame_bytes_generated: u64,
    handoffs_started: u64,
    handoffs_committed: u64,
    handoffs_aborted: u64,
    ownership_oracle_checks: u64,
    ghost_oracle_checks: u64,
    compact_codec_pages: u64,
    compact_codec_bytes: u64,
    workload_checksum: u64,
}

#[derive(Debug)]
struct BattleResult {
    deterministic: DeterministicMetrics,
    tick_timing: TimingSummary,
    simulation_timing: TimingSummary,
    interest_timing: TimingSummary,
    ghost_wire_timing: TimingSummary,
    queue_timing: TimingSummary,
    tick_deadline_misses: usize,
    client_snapshot_bytes: TimingSummary,
    client_snapshot_bytes_min: u128,
    client_snapshot_bytes_max: u128,
    queue: QueueMetrics,
    final_queue_frames: usize,
    final_queue_bytes: usize,
}

impl BattleResult {
    fn to_json(&self, config: BattleConfig) -> String {
        let d = self.deterministic;
        let projected_client_bytes_per_second = d.client_sent_bytes as u128
            * u128::from(config.tick_rate_hz)
            / u128::from(config.measured_ticks);
        let projected_intercell_bytes_per_second = self.queue.delivered_bytes as u128
            * u128::from(config.tick_rate_hz)
            / u128::from(config.measured_ticks);
        let observers = d.observer_evaluations as f64;
        let projected_client_bytes_per_second_per_client =
            projected_client_bytes_per_second / config.entity_count as u128;
        format!(
            concat!(
                "{{\"record\":\"scenario\",\"schema_version\":{},",
                "\"reference_only\":true,\"workload\":\"multicell_frontline\",",
                "\"active_entities\":{},\"cells\":{},\"ticks\":{},",
                "\"tick_budget_ns\":{},\"tick_total_ns\":{},",
                "\"tick_mean_ns\":{},\"tick_p50_ns\":{},\"tick_p95_ns\":{},",
                "\"tick_p99_ns\":{},\"tick_max_ns\":{},",
                "\"tick_deadline_misses\":{},",
                "\"simulation_mean_ns\":{},\"simulation_p99_ns\":{},",
                "\"interest_mean_ns\":{},\"interest_p99_ns\":{},",
                "\"ghost_wire_mean_ns\":{},\"ghost_wire_p99_ns\":{},",
                "\"queue_mean_ns\":{},\"queue_p99_ns\":{},",
                "\"observer_evaluations\":{},\"unique_observers\":{},",
                "\"interest_cell_queries\":{},\"interest_candidates\":{},",
                "\"engaged_records\":{},",
                "\"engaged_mean_per_observer\":{:.3},",
                "\"awareness_desired_records\":{},\"awareness_sent_records\":{},",
                "\"awareness_desired_mean_per_observer\":{:.3},",
                "\"awareness_sent_mean_per_observer\":{:.3},",
                "\"awareness_shed_records\":{},\"mass_aggregate_records\":{},",
                "\"mass_represented_entities\":{},\"causal_promotions\":{},",
                "\"mass_entities_mean_per_observer\":{:.3},",
                "\"individually_sent_mean_per_observer\":{:.3},",
                "\"degraded_observers\":{},\"engaged_over_budget_observers\":{},",
                "\"client_desired_bytes\":{},\"client_sent_bytes\":{},",
                "\"client_datagrams\":{},\"client_snapshot_bytes_mean\":{},",
                "\"client_snapshot_bytes_p95\":{},\"client_snapshot_bytes_p99\":{},",
                "\"client_snapshot_bytes_min\":{},\"client_snapshot_bytes_max\":{},",
                "\"projected_client_bytes_per_second\":{},",
                "\"projected_client_bytes_per_second_per_client\":{},",
                "\"ghost_records_generated\":{},\"ghost_frames_generated\":{},",
                "\"ghost_frame_bytes_generated\":{},",
                "\"handoffs_started\":{},\"handoffs_committed\":{},",
                "\"handoffs_aborted\":{},\"ownership_oracle_checks\":{},",
                "\"ghost_oracle_checks\":{},\"queue_frames_high_water\":{},",
                "\"queue_bytes_high_water\":{},\"ghost_frames_dropped\":{},",
                "\"ghost_frames_evicted_for_control\":{},",
                "\"expired_frames\":{},\"rejected_control_frames\":{},",
                "\"delivered_ghost_frames\":{},\"delivered_control_frames\":{},",
                "\"delivered_intercell_bytes\":{},",
                "\"projected_intercell_bytes_per_second\":{},",
                "\"final_queue_frames\":{},\"final_queue_bytes\":{},",
                "\"compact_codec_pages\":{},\"compact_codec_bytes\":{},",
                "\"workload_checksum\":\"{:016x}\"}}"
            ),
            OUTPUT_SCHEMA_VERSION,
            config.entity_count,
            config.cell_count().unwrap_or(0),
            config.measured_ticks,
            config.tick_budget_ns(),
            self.tick_timing.total_ns,
            self.tick_timing.mean_ns,
            self.tick_timing.p50_ns,
            self.tick_timing.p95_ns,
            self.tick_timing.p99_ns,
            self.tick_timing.max_ns,
            self.tick_deadline_misses,
            self.simulation_timing.mean_ns,
            self.simulation_timing.p99_ns,
            self.interest_timing.mean_ns,
            self.interest_timing.p99_ns,
            self.ghost_wire_timing.mean_ns,
            self.ghost_wire_timing.p99_ns,
            self.queue_timing.mean_ns,
            self.queue_timing.p99_ns,
            d.observer_evaluations,
            d.unique_observers,
            d.interest_cell_queries,
            d.interest_candidates,
            d.engaged_records,
            d.engaged_records as f64 / observers,
            d.awareness_desired_records,
            d.awareness_sent_records,
            d.awareness_desired_records as f64 / observers,
            d.awareness_sent_records as f64 / observers,
            d.awareness_shed_records,
            d.mass_aggregate_records,
            d.mass_represented_entities,
            d.causal_promotions,
            d.mass_represented_entities as f64 / observers,
            (d.engaged_records + d.awareness_sent_records) as f64 / observers,
            d.degraded_observers,
            d.engaged_over_budget_observers,
            d.client_desired_bytes,
            d.client_sent_bytes,
            d.client_datagrams,
            self.client_snapshot_bytes.mean_ns,
            self.client_snapshot_bytes.p95_ns,
            self.client_snapshot_bytes.p99_ns,
            self.client_snapshot_bytes_min,
            self.client_snapshot_bytes_max,
            projected_client_bytes_per_second,
            projected_client_bytes_per_second_per_client,
            d.ghost_records_generated,
            d.ghost_frames_generated,
            d.ghost_frame_bytes_generated,
            d.handoffs_started,
            d.handoffs_committed,
            d.handoffs_aborted,
            d.ownership_oracle_checks,
            d.ghost_oracle_checks,
            self.queue.frames_high_water,
            self.queue.bytes_high_water,
            self.queue.ghost_dropped,
            self.queue.ghost_evicted_for_control,
            self.queue.expired,
            self.queue.control_rejected,
            self.queue.ghost_delivered,
            self.queue.control_delivered,
            self.queue.delivered_bytes,
            projected_intercell_bytes_per_second,
            self.final_queue_frames,
            self.final_queue_bytes,
            d.compact_codec_pages,
            d.compact_codec_bytes,
            d.workload_checksum,
        )
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct CellGeometry {
    index: usize,
    column: u32,
    row: u32,
    origin: Position2,
    extent: u32,
}

impl CellGeometry {
    fn new(index: usize, config: BattleConfig) -> Result<Self, HarnessError> {
        let index_u32 =
            u32::try_from(index).map_err(|_| HarnessError::new("cell index overflow"))?;
        let column = index_u32 % config.cell_columns;
        let row = index_u32 / config.cell_columns;
        let extent = config.cell_extent()?;
        let origin_x = column
            .checked_mul(extent)
            .ok_or_else(|| HarnessError::new("cell origin x overflow"))?;
        let origin_y = row
            .checked_mul(extent)
            .ok_or_else(|| HarnessError::new("cell origin y overflow"))?;
        Ok(Self {
            index,
            column,
            row,
            origin: Position2::new(
                i32::try_from(origin_x).map_err(|_| HarnessError::new("origin x exceeds i32"))?,
                i32::try_from(origin_y).map_err(|_| HarnessError::new("origin y exceeds i32"))?,
            ),
            extent,
        })
    }

    fn cell_id(self) -> CellId {
        CellId::new(self.index as u64 + 1).expect("bounded cell index is nonzero")
    }

    fn contains(self, position: Position2) -> bool {
        let max_x = i64::from(self.origin.x()) + i64::from(self.extent);
        let max_y = i64::from(self.origin.y()) + i64::from(self.extent);
        i64::from(position.x()) >= i64::from(self.origin.x())
            && i64::from(position.x()) < max_x
            && i64::from(position.y()) >= i64::from(self.origin.y())
            && i64::from(position.y()) < max_y
    }

    fn relative(self, position: Position2) -> (i32, i32) {
        (
            position.x() - self.origin.x(),
            position.y() - self.origin.y(),
        )
    }
}

fn build_cohort_plans(
    config: BattleConfig,
    geometry: &[CellGeometry],
) -> Result<Vec<CohortPlan>, HarnessError> {
    let cell_extent = config.cell_extent()?;
    let cohort_extent = cell_extent / COHORTS_PER_CELL_SIDE;
    let cohort_columns = config
        .cell_columns
        .checked_mul(COHORTS_PER_CELL_SIDE)
        .ok_or_else(|| HarnessError::new("cohort column count overflow"))?;
    let cohort_rows = config
        .cell_rows
        .checked_mul(COHORTS_PER_CELL_SIDE)
        .ok_or_else(|| HarnessError::new("cohort row count overflow"))?;
    let cohort_count = usize::try_from(
        u64::from(cohort_columns)
            .checked_mul(u64::from(cohort_rows))
            .ok_or_else(|| HarnessError::new("cohort count overflow"))?,
    )
    .map_err(|_| HarnessError::new("cohort count exceeds usize"))?;
    let mut plans = Vec::with_capacity(cohort_count);
    let radius = i64::from(config.awareness_radius);

    for row in 0..cohort_rows {
        for column in 0..cohort_columns {
            let minimum_x = column
                .checked_mul(cohort_extent)
                .ok_or_else(|| HarnessError::new("cohort x origin overflow"))?;
            let minimum_y = row
                .checked_mul(cohort_extent)
                .ok_or_else(|| HarnessError::new("cohort y origin overflow"))?;
            let maximum_x = minimum_x
                .checked_add(cohort_extent)
                .ok_or_else(|| HarnessError::new("cohort x extent overflow"))?;
            let maximum_y = minimum_y
                .checked_add(cohort_extent)
                .ok_or_else(|| HarnessError::new("cohort y extent overflow"))?;
            let expanded_minimum_x = i64::from(minimum_x) - radius;
            let expanded_minimum_y = i64::from(minimum_y) - radius;
            let expanded_maximum_x = i64::from(maximum_x) + radius;
            let expanded_maximum_y = i64::from(maximum_y) + radius;
            let mut cell_indices = Vec::new();

            for cell in geometry {
                let cell_minimum_x = i64::from(cell.origin.x());
                let cell_minimum_y = i64::from(cell.origin.y());
                let cell_maximum_x = cell_minimum_x + i64::from(cell.extent);
                let cell_maximum_y = cell_minimum_y + i64::from(cell.extent);
                if cell_maximum_x > expanded_minimum_x
                    && cell_minimum_x < expanded_maximum_x
                    && cell_maximum_y > expanded_minimum_y
                    && cell_minimum_y < expanded_maximum_y
                {
                    cell_indices.push(cell.index);
                }
            }
            require(
                !cell_indices.is_empty(),
                "observer cohort had no candidate authoritative cell",
            )?;
            plans.push(CohortPlan { cell_indices });
        }
    }
    Ok(plans)
}

#[derive(Clone, Copy, Debug)]
struct ActiveHandoff {
    machine: Handoff,
    entity: EntityId,
    source_index: usize,
    destination_index: usize,
    ready_tick: Tick,
}

#[derive(Debug)]
struct CohortPlan {
    cell_indices: Vec<usize>,
}

#[derive(Debug)]
struct InterestScratch {
    engaged: Vec<EntityId>,
    awareness: Vec<EntityId>,
    mass: Vec<MassRecord>,
    engaged_faction_a: Vec<usize>,
    engaged_faction_b: Vec<usize>,
    awareness_faction_a: Vec<usize>,
    awareness_faction_b: Vec<usize>,
    sent_faction_a: Vec<usize>,
    sent_faction_b: Vec<usize>,
    page_buffer: Vec<u8>,
}

impl InterestScratch {
    fn new(entity_count: usize, cell_count: usize, max_datagram_bytes: usize) -> Self {
        Self {
            engaged: Vec::with_capacity(entity_count),
            awareness: Vec::with_capacity(entity_count),
            mass: Vec::with_capacity(cell_count),
            engaged_faction_a: vec![0; cell_count],
            engaged_faction_b: vec![0; cell_count],
            awareness_faction_a: vec![0; cell_count],
            awareness_faction_b: vec![0; cell_count],
            sent_faction_a: vec![0; cell_count],
            sent_faction_b: vec![0; cell_count],
            page_buffer: vec![0; max_datagram_bytes],
        }
    }

    fn clear(&mut self) {
        self.engaged.clear();
        self.awareness.clear();
        self.mass.clear();
        self.engaged_faction_a.fill(0);
        self.engaged_faction_b.fill(0);
        self.awareness_faction_a.fill(0);
        self.awareness_faction_b.fill(0);
        self.sent_faction_a.fill(0);
        self.sent_faction_b.fill(0);
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct ObserverInterestOutcome {
    observer_index: usize,
    cell_queries: usize,
    candidates: usize,
    engaged: usize,
    awareness_desired: usize,
    awareness_sent: usize,
    awareness_shed: usize,
    mass_aggregates: usize,
    mass_entities: usize,
    causal_promotions: usize,
    engaged_over_budget: bool,
    desired_bytes: usize,
    encoded: CompactEncodeStats,
}

#[derive(Debug)]
struct BattleWorld {
    config: BattleConfig,
    geometry: Vec<CellGeometry>,
    cells: Vec<FixedTickMicrocell>,
    query_buffers: Vec<RadiusQueryBuffer>,
    intent_buffers: Vec<Vec<VelocityIntent>>,
    cohort_plans: Vec<CohortPlan>,
    interest_worklist: Vec<(usize, usize)>,
    observer_outcomes: Vec<ObserverInterestOutcome>,
    interest_scratch: InterestScratch,
    positions: Vec<Position2>,
    velocities: Vec<Velocity2>,
    forward_velocities: Vec<Velocity2>,
    leases: Vec<OwnershipLease>,
    resident_counts: Vec<usize>,
    resident_faction_a: Vec<usize>,
    resident_faction_b: Vec<usize>,
    route_ghost_records: Vec<Vec<GhostRecord>>,
    active_handoffs: Vec<ActiveHandoff>,
    next_handoff_id: u64,
    next_sequence: u64,
    queue: BoundedFrameQueue,
    metrics: DeterministicMetrics,
    checksum: Checksum,
    client_snapshot_samples: Vec<u128>,
    observer_seen: Vec<bool>,
}

impl BattleWorld {
    fn new(config: BattleConfig) -> Result<Self, HarnessError> {
        config.validate()?;
        let cell_count = config.cell_count()?;
        let cell_capacity = config.cell_capacity()?;
        let mut geometry = Vec::with_capacity(cell_count);
        let mut cells = Vec::with_capacity(cell_count);
        let mut query_buffers = Vec::with_capacity(cell_count);
        let mut intent_buffers = Vec::with_capacity(cell_count);
        for index in 0..cell_count {
            let cell_geometry = CellGeometry::new(index, config)?;
            let grid = GridSpec::new(
                cell_geometry.origin,
                config.grid_cell_size,
                config.grid_cells_per_cell,
                config.grid_cells_per_cell,
            )
            .map_err(|error| HarnessError::new(error.to_string()))?;
            let microcell_config = MicrocellConfig::new(cell_capacity, cell_capacity, grid)
                .map_err(|error| HarnessError::new(error.to_string()))?;
            geometry.push(cell_geometry);
            cells.push(FixedTickMicrocell::new(microcell_config, Tick::new(0)));
            query_buffers.push(
                RadiusQueryBuffer::new(cell_capacity)
                    .map_err(|error| HarnessError::new(error.to_string()))?,
            );
            intent_buffers.push(Vec::with_capacity(cell_capacity));
        }
        let cohort_plans = build_cohort_plans(config, &geometry)?;
        let observers_per_shard = config.entity_count.div_ceil(
            usize::try_from(config.replication_period_ticks)
                .map_err(|_| HarnessError::new("replication period exceeds usize"))?,
        );

        let mut world = Self {
            config,
            geometry,
            cells,
            query_buffers,
            intent_buffers,
            cohort_plans,
            interest_worklist: Vec::with_capacity(observers_per_shard),
            observer_outcomes: Vec::with_capacity(observers_per_shard),
            interest_scratch: InterestScratch::new(
                config.entity_count,
                cell_count,
                config.max_datagram_bytes,
            ),
            positions: vec![Position2::default(); config.entity_count],
            velocities: vec![Velocity2::default(); config.entity_count],
            forward_velocities: vec![Velocity2::default(); config.entity_count],
            leases: Vec::with_capacity(config.entity_count),
            resident_counts: vec![0; cell_count],
            resident_faction_a: vec![0; cell_count],
            resident_faction_b: vec![0; cell_count],
            route_ghost_records: (0..cell_count * cell_count).map(|_| Vec::new()).collect(),
            active_handoffs: Vec::new(),
            next_handoff_id: 1,
            next_sequence: 1,
            queue: BoundedFrameQueue::new(config.queue_max_frames, config.queue_max_bytes),
            metrics: DeterministicMetrics::default(),
            checksum: Checksum::new(config.seed),
            client_snapshot_samples: Vec::with_capacity(config.entity_count),
            observer_seen: vec![false; config.entity_count],
        };
        world.spawn_initial_entities()?;
        world.gather_and_validate_ownership()?;
        Ok(world)
    }

    fn spawn_initial_entities(&mut self) -> Result<(), HarnessError> {
        let cell_count = self.config.cell_count()?;
        let ownership_epoch = OwnershipEpoch::new(1).expect("constant epoch is nonzero");
        for index in 0..self.config.entity_count {
            let id = entity_id(index)?;
            let cell_index = index % cell_count;
            let geometry = self.geometry[cell_index];
            let random = splitmix64(self.config.seed.wrapping_add(id.get()));
            let edge_padding = self.config.ghost_margin / 4 + 2;
            let usable = geometry
                .extent
                .checked_sub(edge_padding * 2)
                .ok_or_else(|| HarnessError::new("cell placement extent underflow"))?;
            let relative_x = edge_padding + random as u32 % usable;
            let relative_y = edge_padding + random.rotate_left(29) as u32 % usable;
            let position = Position2::new(
                geometry.origin.x()
                    + i32::try_from(relative_x)
                        .map_err(|_| HarnessError::new("relative x exceeds i32"))?,
                geometry.origin.y()
                    + i32::try_from(relative_y)
                        .map_err(|_| HarnessError::new("relative y exceeds i32"))?,
            );
            let forward = match random.rotate_left(11) & 3 {
                0 => Velocity2::new(1, 0),
                1 => Velocity2::new(-1, 0),
                2 => Velocity2::new(0, 1),
                _ => Velocity2::new(0, -1),
            };
            self.forward_velocities[index] = forward;
            self.positions[index] = position;
            self.cells[cell_index]
                .spawn(EntityState::new(id, position, Velocity2::default()))
                .map_err(|error| HarnessError::new(error.to_string()))?;
            self.leases
                .push(OwnershipLease::new(id, geometry.cell_id(), ownership_epoch));
        }
        Ok(())
    }

    fn gather_and_validate_ownership(&mut self) -> Result<(), HarnessError> {
        let mut seen = vec![false; self.config.entity_count];
        self.resident_counts.fill(0);
        self.resident_faction_a.fill(0);
        self.resident_faction_b.fill(0);
        for (cell_index, cell) in self.cells.iter().enumerate() {
            for state in cell.entities() {
                let index = entity_index(state.id(), self.config.entity_count)?;
                require(!seen[index], "entity was resident in two microcells")?;
                seen[index] = true;
                require(
                    self.leases[index].entity() == state.id(),
                    "lease entity mismatched resident entity",
                )?;
                require(
                    self.leases[index].owner() == self.geometry[cell_index].cell_id(),
                    "resident cell mismatched sole-writer lease",
                )?;
                require(
                    self.geometry[cell_index].contains(state.position()),
                    "resident position escaped owner geometry",
                )?;
                self.positions[index] = state.position();
                self.velocities[index] = state.velocity();
                self.resident_counts[cell_index] += 1;
                if state.id().get() & 1 == 0 {
                    self.resident_faction_a[cell_index] += 1;
                } else {
                    self.resident_faction_b[cell_index] += 1;
                }
            }
        }
        require(
            seen.into_iter().all(|value| value),
            "entity disappeared from all cells",
        )?;
        require(
            self.resident_counts.iter().sum::<usize>() == self.config.entity_count,
            "resident entity conservation failed",
        )?;
        self.metrics.ownership_oracle_checks += 1;
        Ok(())
    }

    fn step_simulation(&mut self, tick: Tick) -> Result<(), HarnessError> {
        let source = IntentSourceId::new(1).expect("constant source is nonzero");
        for intents in &mut self.intent_buffers {
            intents.clear();
        }
        for (cell_index, cell) in self.cells.iter().enumerate() {
            for state in cell.entities() {
                let index = entity_index(state.id(), self.config.entity_count)?;
                let forward = self.forward_velocities[index];
                let velocity = if tick.get() & 1 == 1 {
                    forward
                } else {
                    Velocity2::new(-forward.x(), -forward.y())
                };
                self.intent_buffers[cell_index].push(VelocityIntent::new(
                    state.id(),
                    1,
                    source,
                    tick.get() ^ state.id().get(),
                    velocity,
                ));
            }
            shuffle_intents(
                &mut self.intent_buffers[cell_index],
                self.config.seed ^ tick.get() ^ cell_index as u64,
            );
        }
        let mut stepped = 0_usize;
        for (cell, intents) in self.cells.iter_mut().zip(&self.intent_buffers) {
            let report = cell
                .step(tick, black_box(intents))
                .map_err(|error| HarnessError::new(error.to_string()))?;
            stepped += report.entity_count();
            require(
                report.intents_received() == report.entity_count()
                    && report.intents_applied() == report.entity_count(),
                "one deterministic intent was not applied per resident",
            )?;
        }
        require(
            stepped == self.config.entity_count,
            "fixed tick lost or duplicated active entities",
        )?;
        self.gather_and_validate_ownership()?;
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum QueueClass {
    Ghost,
    Control,
}

#[derive(Debug)]
struct QueuedFrame {
    bytes: Vec<u8>,
    route: RouteScope,
    deadline: Tick,
    class: QueueClass,
    kind: MessageKind,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct QueueMetrics {
    frames_high_water: usize,
    bytes_high_water: usize,
    ghost_dropped: u64,
    ghost_evicted_for_control: u64,
    control_rejected: u64,
    expired: u64,
    ghost_delivered: u64,
    control_delivered: u64,
    delivered_bytes: u64,
}

#[derive(Debug)]
struct BoundedFrameQueue {
    frames: VecDeque<QueuedFrame>,
    bytes: usize,
    max_frames: usize,
    max_bytes: usize,
    metrics: QueueMetrics,
}

impl BoundedFrameQueue {
    fn new(max_frames: usize, max_bytes: usize) -> Self {
        Self {
            frames: VecDeque::with_capacity(max_frames),
            bytes: 0,
            max_frames,
            max_bytes,
            metrics: QueueMetrics::default(),
        }
    }

    fn enqueue(&mut self, frame: QueuedFrame) {
        match frame.class {
            QueueClass::Ghost => {
                if self.would_exceed(frame.bytes.len()) {
                    self.metrics.ghost_dropped += 1;
                    return;
                }
            }
            QueueClass::Control => {
                while self.would_exceed(frame.bytes.len()) {
                    let Some(position) = self
                        .frames
                        .iter()
                        .position(|queued| queued.class == QueueClass::Ghost)
                    else {
                        self.metrics.control_rejected += 1;
                        return;
                    };
                    if let Some(evicted) = self.frames.remove(position) {
                        self.bytes -= evicted.bytes.len();
                        self.metrics.ghost_evicted_for_control += 1;
                    }
                }
            }
        }
        self.bytes += frame.bytes.len();
        self.frames.push_back(frame);
        self.metrics.frames_high_water = self.metrics.frames_high_water.max(self.frames.len());
        self.metrics.bytes_high_water = self.metrics.bytes_high_water.max(self.bytes);
        debug_assert!(self.frames.len() <= self.max_frames);
        debug_assert!(self.bytes <= self.max_bytes);
    }

    fn would_exceed(&self, additional_bytes: usize) -> bool {
        self.frames.len() >= self.max_frames
            || self
                .bytes
                .checked_add(additional_bytes)
                .map_or(true, |bytes| bytes > self.max_bytes)
    }

    fn drain(&mut self, current_tick: Tick, limit: usize) -> Result<u64, HarnessError> {
        let mut index = 0_usize;
        while index < self.frames.len() {
            if self.frames[index].deadline < current_tick {
                if let Some(expired) = self.frames.remove(index) {
                    self.bytes -= expired.bytes.len();
                    self.metrics.expired += 1;
                }
            } else {
                index += 1;
            }
        }

        let mut checksum = Checksum::new(0x51_55_45_55_45 ^ current_tick.get());
        for _ in 0..limit {
            if self.frames.is_empty() {
                break;
            }
            let position = self
                .frames
                .iter()
                .position(|frame| frame.class == QueueClass::Control)
                .unwrap_or(0);
            let frame = self
                .frames
                .remove(position)
                .expect("selected queue frame exists");
            self.bytes -= frame.bytes.len();
            let limits = FrameLimits::new(FRAME_PAYLOAD_BUDGET)
                .expect("payload budget is below absolute ceiling");
            let decoded = decode_v1(
                &frame.bytes,
                limits,
                ReceiveContext::new(frame.route, current_tick),
            )
            .map_err(|error| HarnessError::new(error.to_string()))?;
            require(
                decoded.header().kind() == frame.kind,
                "queued kind changed during decode",
            )?;
            require(
                decoded.header().deadline_tick() == frame.deadline,
                "queued deadline changed during decode",
            )?;
            let payload_checksum = match frame.kind {
                MessageKind::GhostSnapshot => decode_ghost_payload(decoded.payload())?.1,
                MessageKind::HandoffControl => decode_handoff_payload(decoded.payload())?,
                MessageKind::CellEvent | MessageKind::Heartbeat => {
                    return Err(HarnessError::new("unexpected queue message kind"))
                }
            };
            checksum.mix(decoded.header().sequence());
            checksum.mix(payload_checksum);
            self.metrics.delivered_bytes += frame.bytes.len() as u64;
            match frame.class {
                QueueClass::Ghost => self.metrics.ghost_delivered += 1,
                QueueClass::Control => self.metrics.control_delivered += 1,
            }
        }
        Ok(checksum.finish())
    }

    fn len(&self) -> usize {
        self.frames.len()
    }

    fn is_empty(&self) -> bool {
        self.frames.is_empty()
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
#[repr(u8)]
enum CompactTier {
    Engaged = 1,
    Awareness = 2,
    Mass = 3,
}

impl CompactTier {
    fn from_wire(value: u8) -> Option<Self> {
        match value {
            1 => Some(Self::Engaged),
            2 => Some(Self::Awareness),
            3 => Some(Self::Mass),
            _ => None,
        }
    }

    const fn record_len(self) -> usize {
        match self {
            Self::Engaged => ENGAGED_RECORD_LEN,
            Self::Awareness => AWARENESS_RECORD_LEN,
            Self::Mass => MASS_RECORD_LEN,
        }
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct MassRecord {
    cell_index: u16,
    faction_a: u16,
    faction_b: u16,
    relative_x: i16,
    relative_y: i16,
    entity_count: u16,
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct CompactEncodeStats {
    pages: usize,
    bytes: usize,
    checksum: u64,
}

impl CompactEncodeStats {
    fn merge(&mut self, other: Self) {
        self.pages += other.pages;
        self.bytes += other.bytes;
        self.checksum = self.checksum.rotate_left(9).wrapping_add(other.checksum);
    }
}

struct CompactEntityPageInput<'a> {
    tier: CompactTier,
    observer: EntityId,
    observer_position: Position2,
    tick: Tick,
    ids: &'a [EntityId],
    positions: &'a [Position2],
    velocities: &'a [Velocity2],
    max_datagram_bytes: usize,
}

fn encode_entity_pages(
    input: CompactEntityPageInput<'_>,
    page_buffer: &mut [u8],
) -> Result<CompactEncodeStats, HarnessError> {
    let CompactEntityPageInput {
        tier,
        observer,
        observer_position,
        tick,
        ids,
        positions,
        velocities,
        max_datagram_bytes,
    } = input;
    require(
        matches!(tier, CompactTier::Engaged | CompactTier::Awareness),
        "entity codec received mass tier",
    )?;
    if ids.is_empty() {
        return Ok(CompactEncodeStats::default());
    }
    let records_per_page = (max_datagram_bytes - COMPACT_PAGE_HEADER_LEN) / tier.record_len();
    require(
        records_per_page > 0,
        "compact page cannot fit one entity record",
    )?;
    require(
        page_buffer.len() >= max_datagram_bytes,
        "reusable compact page buffer was below the datagram budget",
    )?;
    let mut stats = CompactEncodeStats::default();
    for chunk in ids.chunks(records_per_page) {
        let page_len = COMPACT_PAGE_HEADER_LEN + chunk.len() * tier.record_len();
        let page = page_buffer
            .get_mut(..page_len)
            .ok_or_else(|| HarnessError::new("reusable compact page buffer was too small"))?;
        page.fill(0);
        encode_compact_header(page, tier, chunk.len(), tick, observer)?;
        for (record_index, id) in chunk.iter().copied().enumerate() {
            let index = entity_index(id, positions.len())?;
            let offset = COMPACT_PAGE_HEADER_LEN + record_index * tier.record_len();
            let handle = u32::try_from(id.get())
                .map_err(|_| HarnessError::new("compact entity handle exceeds u32"))?;
            let relative_x = checked_i16(
                positions[index].x() - observer_position.x(),
                "entity relative x",
            )?;
            let relative_y = checked_i16(
                positions[index].y() - observer_position.y(),
                "entity relative y",
            )?;
            write_u32(page, offset, handle)?;
            write_i16(page, offset + 4, relative_x)?;
            write_i16(page, offset + 6, relative_y)?;
            match tier {
                CompactTier::Engaged => {
                    page[offset + 8] = checked_i8(velocities[index].x(), "velocity x")? as u8;
                    page[offset + 9] = checked_i8(velocities[index].y(), "velocity y")? as u8;
                    write_u16(page, offset + 10, (handle as u16 & 1) | 0x10)?;
                    write_u16(page, offset + 12, tick.get() as u16)?;
                    write_u16(page, offset + 14, 0)?;
                }
                CompactTier::Awareness => {
                    page[offset + 8] = heading_code(velocities[index]);
                    page[offset + 9] =
                        u8::from(velocities[index].x() != 0 || velocities[index].y() != 0);
                    write_u16(page, offset + 10, 0)?;
                }
                CompactTier::Mass => unreachable!("mass tier rejected above"),
            }
        }
        let (decoded_tier, decoded_count, checksum) = decode_compact_page(page)?;
        require(decoded_tier == tier, "compact tier changed during decode")?;
        require(
            decoded_count == chunk.len(),
            "compact record count changed during decode",
        )?;
        stats.pages += 1;
        stats.bytes += page.len();
        stats.checksum = stats.checksum.rotate_left(7).wrapping_add(checksum);
    }
    Ok(stats)
}

fn encode_mass_pages(
    observer: EntityId,
    tick: Tick,
    records: &[MassRecord],
    max_datagram_bytes: usize,
    page_buffer: &mut [u8],
) -> Result<CompactEncodeStats, HarnessError> {
    if records.is_empty() {
        return Ok(CompactEncodeStats::default());
    }
    let records_per_page = (max_datagram_bytes - COMPACT_PAGE_HEADER_LEN) / MASS_RECORD_LEN;
    require(
        records_per_page > 0,
        "compact page cannot fit one mass record",
    )?;
    require(
        page_buffer.len() >= max_datagram_bytes,
        "reusable compact page buffer was below the datagram budget",
    )?;
    let mut stats = CompactEncodeStats::default();
    for chunk in records.chunks(records_per_page) {
        let page_len = COMPACT_PAGE_HEADER_LEN + chunk.len() * MASS_RECORD_LEN;
        let page = page_buffer
            .get_mut(..page_len)
            .ok_or_else(|| HarnessError::new("reusable compact page buffer was too small"))?;
        page.fill(0);
        encode_compact_header(page, CompactTier::Mass, chunk.len(), tick, observer)?;
        for (record_index, record) in chunk.iter().enumerate() {
            let offset = COMPACT_PAGE_HEADER_LEN + record_index * MASS_RECORD_LEN;
            write_u16(page, offset, record.cell_index)?;
            write_u16(page, offset + 2, record.faction_a)?;
            write_u16(page, offset + 4, record.faction_b)?;
            write_i16(page, offset + 6, record.relative_x)?;
            write_i16(page, offset + 8, record.relative_y)?;
            write_u16(page, offset + 10, record.entity_count)?;
        }
        let (decoded_tier, decoded_count, checksum) = decode_compact_page(page)?;
        require(
            decoded_tier == CompactTier::Mass,
            "mass tier changed during decode",
        )?;
        require(
            decoded_count == chunk.len(),
            "mass count changed during decode",
        )?;
        stats.pages += 1;
        stats.bytes += page.len();
        stats.checksum = stats.checksum.rotate_left(7).wrapping_add(checksum);
    }
    Ok(stats)
}

fn encode_compact_header(
    output: &mut [u8],
    tier: CompactTier,
    record_count: usize,
    tick: Tick,
    observer: EntityId,
) -> Result<(), HarnessError> {
    require(
        output.len() >= COMPACT_PAGE_HEADER_LEN,
        "compact output lacked header",
    )?;
    output[0..4].copy_from_slice(b"ALCP");
    output[4] = 1;
    output[5] = tier as u8;
    write_u16(
        output,
        6,
        u16::try_from(record_count)
            .map_err(|_| HarnessError::new("compact record count exceeds u16"))?,
    )?;
    write_u32(
        output,
        8,
        u32::try_from(tick.get()).map_err(|_| HarnessError::new("compact tick exceeds u32"))?,
    )?;
    write_u32(
        output,
        12,
        u32::try_from(observer.get())
            .map_err(|_| HarnessError::new("observer handle exceeds u32"))?,
    )
}

fn decode_compact_page(input: &[u8]) -> Result<(CompactTier, usize, u64), HarnessError> {
    require(
        input.len() >= COMPACT_PAGE_HEADER_LEN,
        "compact page was truncated",
    )?;
    require(&input[0..4] == b"ALCP", "compact page magic was invalid")?;
    require(input[4] == 1, "compact page version was invalid")?;
    let tier = CompactTier::from_wire(input[5])
        .ok_or_else(|| HarnessError::new("compact page tier was invalid"))?;
    let count = usize::from(read_u16(input, 6)?);
    let expected = COMPACT_PAGE_HEADER_LEN
        .checked_add(
            count
                .checked_mul(tier.record_len())
                .ok_or_else(|| HarnessError::new("compact page length overflow"))?,
        )
        .ok_or_else(|| HarnessError::new("compact page length overflow"))?;
    require(
        input.len() == expected,
        "compact page had truncation or trailing bytes",
    )?;
    let mut checksum = Checksum::new(u64::from(read_u32(input, 8)?));
    checksum.mix(u64::from(read_u32(input, 12)?));
    checksum.mix(tier as u64);
    checksum.mix(count as u64);
    for record_index in 0..count {
        let offset = COMPACT_PAGE_HEADER_LEN + record_index * tier.record_len();
        match tier {
            CompactTier::Engaged => {
                checksum.mix(u64::from(read_u32(input, offset)?));
                checksum.mix(read_i16(input, offset + 4)? as u16 as u64);
                checksum.mix(read_i16(input, offset + 6)? as u16 as u64);
                checksum.mix(u64::from(input[offset + 8]));
                checksum.mix(u64::from(input[offset + 9]));
                checksum.mix(u64::from(read_u16(input, offset + 10)?));
                checksum.mix(u64::from(read_u16(input, offset + 12)?));
                checksum.mix(u64::from(read_u16(input, offset + 14)?));
            }
            CompactTier::Awareness => {
                checksum.mix(u64::from(read_u32(input, offset)?));
                checksum.mix(read_i16(input, offset + 4)? as u16 as u64);
                checksum.mix(read_i16(input, offset + 6)? as u16 as u64);
                checksum.mix(u64::from(input[offset + 8]));
                checksum.mix(u64::from(input[offset + 9]));
                checksum.mix(u64::from(read_u16(input, offset + 10)?));
            }
            CompactTier::Mass => {
                for field_offset in [0, 2, 4, 6, 8, 10] {
                    checksum.mix(u64::from(read_u16(input, offset + field_offset)?));
                }
            }
        }
    }
    Ok((tier, count, checksum.finish()))
}

fn encoded_pages_len(
    record_count: usize,
    record_len: usize,
    max_datagram_bytes: usize,
) -> Result<usize, HarnessError> {
    if record_count == 0 {
        return Ok(0);
    }
    let records_per_page = max_datagram_bytes
        .checked_sub(COMPACT_PAGE_HEADER_LEN)
        .ok_or_else(|| HarnessError::new("datagram budget below compact header"))?
        / record_len;
    require(records_per_page > 0, "datagram cannot fit a compact record")?;
    let pages = record_count.div_ceil(records_per_page);
    record_count
        .checked_mul(record_len)
        .and_then(|records| {
            pages
                .checked_mul(COMPACT_PAGE_HEADER_LEN)?
                .checked_add(records)
        })
        .ok_or_else(|| HarnessError::new("encoded page byte count overflow"))
}

fn max_records_within_budget(
    desired: usize,
    record_len: usize,
    max_datagram_bytes: usize,
    budget: usize,
) -> Result<usize, HarnessError> {
    let mut low = 0_usize;
    let mut high = desired;
    while low < high {
        let candidate = low + (high - low).div_ceil(2);
        if encoded_pages_len(candidate, record_len, max_datagram_bytes)? <= budget {
            low = candidate;
        } else {
            high = candidate - 1;
        }
    }
    Ok(low)
}

fn encode_ghost_payload(records: &[GhostRecord]) -> Result<Vec<u8>, HarnessError> {
    let length = GHOST_PAYLOAD_HEADER_LEN
        .checked_add(
            records
                .len()
                .checked_mul(GHOST_RECORD_LEN)
                .ok_or_else(|| HarnessError::new("ghost payload length overflow"))?,
        )
        .ok_or_else(|| HarnessError::new("ghost payload length overflow"))?;
    require(
        length <= FRAME_PAYLOAD_BUDGET,
        "ghost payload exceeded frame budget",
    )?;
    let mut output = vec![0_u8; length];
    output[0..4].copy_from_slice(b"ALGS");
    output[4] = 1;
    output[5] = GHOST_RECORD_LEN as u8;
    write_u16(
        &mut output,
        6,
        u16::try_from(records.len())
            .map_err(|_| HarnessError::new("ghost record count exceeds u16"))?,
    )?;
    for (index, record) in records.iter().enumerate() {
        let offset = GHOST_PAYLOAD_HEADER_LEN + index * GHOST_RECORD_LEN;
        write_u64(&mut output, offset, record.entity.get())?;
        write_u64(&mut output, offset + 8, record.epoch.get())?;
        write_i32(&mut output, offset + 16, record.position.x())?;
        write_i32(&mut output, offset + 20, record.position.y())?;
        write_i16(
            &mut output,
            offset + 24,
            checked_i16(record.velocity.x(), "ghost velocity x")?,
        )?;
        write_i16(
            &mut output,
            offset + 26,
            checked_i16(record.velocity.y(), "ghost velocity y")?,
        )?;
        write_u16(&mut output, offset + 28, 1)?;
        write_u16(&mut output, offset + 30, 0)?;
    }
    Ok(output)
}

fn decode_ghost_payload(input: &[u8]) -> Result<(usize, u64), HarnessError> {
    require(
        input.len() >= GHOST_PAYLOAD_HEADER_LEN,
        "ghost payload was truncated",
    )?;
    require(&input[0..4] == b"ALGS", "ghost payload magic was invalid")?;
    require(input[4] == 1, "ghost payload version was invalid")?;
    require(
        usize::from(input[5]) == GHOST_RECORD_LEN,
        "ghost record size was invalid",
    )?;
    let count = usize::from(read_u16(input, 6)?);
    let expected = GHOST_PAYLOAD_HEADER_LEN
        .checked_add(
            count
                .checked_mul(GHOST_RECORD_LEN)
                .ok_or_else(|| HarnessError::new("ghost decoded length overflow"))?,
        )
        .ok_or_else(|| HarnessError::new("ghost decoded length overflow"))?;
    require(
        input.len() == expected,
        "ghost payload had truncation or trailing bytes",
    )?;
    let mut checksum = Checksum::new(count as u64);
    for index in 0..count {
        let offset = GHOST_PAYLOAD_HEADER_LEN + index * GHOST_RECORD_LEN;
        let entity = read_u64(input, offset)?;
        let epoch = read_u64(input, offset + 8)?;
        require(
            entity != 0 && epoch != 0,
            "ghost identity or epoch was zero",
        )?;
        checksum.mix(entity);
        checksum.mix(epoch);
        checksum.mix(read_i32(input, offset + 16)? as u32 as u64);
        checksum.mix(read_i32(input, offset + 20)? as u32 as u64);
        checksum.mix(read_i16(input, offset + 24)? as u16 as u64);
        checksum.mix(read_i16(input, offset + 26)? as u16 as u64);
        checksum.mix(u64::from(read_u16(input, offset + 28)?));
        require(
            read_u16(input, offset + 30)? == 0,
            "ghost reserved field was nonzero",
        )?;
    }
    Ok((count, checksum.finish()))
}

fn encode_handoff_payload(event: ControlEvent) -> Result<Vec<u8>, HarnessError> {
    let mut output = vec![0_u8; HANDOFF_PAYLOAD_LEN];
    output[0..4].copy_from_slice(b"ALHC");
    output[4] = 1;
    output[5] = event.opcode;
    write_u16(&mut output, 6, 0)?;
    write_u64(&mut output, 8, event.handoff_id.get())?;
    write_u64(&mut output, 16, event.entity.get())?;
    write_u64(&mut output, 24, event.epoch.get())?;
    write_u64(&mut output, 32, event.cutover_tick.get())?;
    Ok(output)
}

fn decode_handoff_payload(input: &[u8]) -> Result<u64, HarnessError> {
    require(
        input.len() == HANDOFF_PAYLOAD_LEN,
        "handoff payload length was invalid",
    )?;
    require(&input[0..4] == b"ALHC", "handoff payload magic was invalid")?;
    require(input[4] == 1, "handoff payload version was invalid")?;
    require((1..=3).contains(&input[5]), "handoff opcode was invalid")?;
    require(
        read_u16(input, 6)? == 0,
        "handoff reserved field was nonzero",
    )?;
    let handoff = read_u64(input, 8)?;
    let entity = read_u64(input, 16)?;
    let epoch = read_u64(input, 24)?;
    let cutover = read_u64(input, 32)?;
    require(
        handoff != 0 && entity != 0 && epoch != 0,
        "handoff identity was zero",
    )?;
    let mut checksum = Checksum::new(u64::from(input[5]));
    checksum.mix(handoff);
    checksum.mix(entity);
    checksum.mix(epoch);
    checksum.mix(cutover);
    Ok(checksum.finish())
}

fn run_battle(config: BattleConfig) -> Result<BattleResult, HarnessError> {
    config.validate()?;
    let mut world = BattleWorld::new(config)?;
    let tick_count = usize::try_from(config.measured_ticks)
        .map_err(|_| HarnessError::new("measured ticks exceed usize"))?;
    let mut tick_samples = Vec::with_capacity(tick_count);
    let mut simulation_samples = Vec::with_capacity(tick_count);
    let mut interest_samples = Vec::with_capacity(tick_count);
    let mut ghost_wire_samples = Vec::with_capacity(tick_count);
    let mut queue_samples = Vec::with_capacity(tick_count);
    let mut deadline_misses = 0_usize;

    for tick_value in 1..=config.measured_ticks {
        let tick = Tick::new(tick_value);
        let tick_started = Instant::now();

        let simulation_started = Instant::now();
        world.process_handoffs(tick)?;
        world.step_simulation(tick)?;
        simulation_samples.push(simulation_started.elapsed().as_nanos());

        let ghost_started = Instant::now();
        world.generate_ghost_frames(tick)?;
        ghost_wire_samples.push(ghost_started.elapsed().as_nanos());

        let interest_started = Instant::now();
        world.compile_interest_shard(tick)?;
        interest_samples.push(interest_started.elapsed().as_nanos());

        let queue_started = Instant::now();
        let queue_checksum = world
            .queue
            .drain(tick, config.queue_drain_frames_per_tick)?;
        world.checksum.mix(queue_checksum);
        queue_samples.push(queue_started.elapsed().as_nanos());

        let elapsed = tick_started.elapsed().as_nanos();
        if elapsed > config.tick_budget_ns() {
            deadline_misses += 1;
        }
        tick_samples.push(elapsed);
        world.checksum.mix(tick.get());
        world.checksum.mix(world.queue.len() as u64);
    }

    require(
        world.active_handoffs.is_empty(),
        "handoff remained active after workload",
    )?;
    require(
        world.metrics.handoffs_aborted == 0,
        "reference handoff unexpectedly aborted",
    )?;
    require(
        world.metrics.handoffs_started == world.metrics.handoffs_committed,
        "not every started handoff committed",
    )?;
    world.gather_and_validate_ownership()?;

    let mut drain_tick = Tick::new(config.measured_ticks);
    let mut final_drain_steps = 0_usize;
    while !world.queue.is_empty() {
        drain_tick = drain_tick
            .checked_add(1)
            .ok_or_else(|| HarnessError::new("final drain tick overflow"))?;
        let queue_checksum = world
            .queue
            .drain(drain_tick, config.queue_drain_frames_per_tick)?;
        world.checksum.mix(queue_checksum);
        final_drain_steps += 1;
        require(final_drain_steps <= 16, "bounded queue failed to drain")?;
    }

    world.metrics.unique_observers = world.observer_seen.iter().filter(|seen| **seen).count();
    require(
        world.metrics.unique_observers == config.entity_count,
        "not every active entity was evaluated as an observer",
    )?;
    require(
        world.queue.metrics.control_rejected == 0,
        "bounded queue rejected a handoff control frame",
    )?;
    require(
        world.queue.metrics.ghost_dropped + world.queue.metrics.ghost_evicted_for_control > 0,
        "overload workload did not exercise bounded ghost shedding",
    )?;

    for index in 0..config.entity_count {
        world.checksum.mix(entity_id(index)?.get());
        world.checksum.mix(world.leases[index].owner().get());
        world.checksum.mix(world.leases[index].epoch().get());
        world.checksum.mix(world.positions[index].x() as u32 as u64);
        world.checksum.mix(world.positions[index].y() as u32 as u64);
    }
    let queue_metrics = world.queue.metrics;
    for value in [
        queue_metrics.ghost_dropped,
        queue_metrics.ghost_evicted_for_control,
        queue_metrics.control_rejected,
        queue_metrics.expired,
        queue_metrics.ghost_delivered,
        queue_metrics.control_delivered,
        queue_metrics.delivered_bytes,
    ] {
        world.checksum.mix(value);
    }
    world.metrics.workload_checksum = world.checksum.finish();

    let client_snapshot_bytes = TimingSummary::from_samples(&world.client_snapshot_samples)?;
    let client_snapshot_bytes_min = *world
        .client_snapshot_samples
        .iter()
        .min()
        .ok_or_else(|| HarnessError::new("client snapshot samples were empty"))?;
    let client_snapshot_bytes_max = *world
        .client_snapshot_samples
        .iter()
        .max()
        .ok_or_else(|| HarnessError::new("client snapshot samples were empty"))?;
    Ok(BattleResult {
        deterministic: world.metrics,
        tick_timing: TimingSummary::from_samples(&tick_samples)?,
        simulation_timing: TimingSummary::from_samples(&simulation_samples)?,
        interest_timing: TimingSummary::from_samples(&interest_samples)?,
        ghost_wire_timing: TimingSummary::from_samples(&ghost_wire_samples)?,
        queue_timing: TimingSummary::from_samples(&queue_samples)?,
        tick_deadline_misses: deadline_misses,
        client_snapshot_bytes,
        client_snapshot_bytes_min,
        client_snapshot_bytes_max,
        queue: queue_metrics,
        final_queue_frames: world.queue.len(),
        final_queue_bytes: world.queue.bytes,
    })
}

fn direct_ghost_neighbors(
    source: CellGeometry,
    position: Position2,
    config: BattleConfig,
) -> Result<Vec<usize>, HarnessError> {
    require(
        source.contains(position),
        "ghost source did not contain position",
    )?;
    let (relative_x, relative_y) = source.relative(position);
    let margin = i32::try_from(config.ghost_margin)
        .map_err(|_| HarnessError::new("ghost margin exceeds i32"))?;
    let extent =
        i32::try_from(source.extent).map_err(|_| HarnessError::new("cell extent exceeds i32"))?;
    let mut destinations = Vec::with_capacity(4);
    if relative_x < margin && source.column > 0 {
        destinations.push(source.index - 1);
    }
    if relative_x >= extent - margin && source.column + 1 < config.cell_columns {
        destinations.push(source.index + 1);
    }
    if relative_y < margin && source.row > 0 {
        destinations.push(source.index - config.cell_columns as usize);
    }
    if relative_y >= extent - margin && source.row + 1 < config.cell_rows {
        destinations.push(source.index + config.cell_columns as usize);
    }
    Ok(destinations)
}

fn brute_cells_share_ghost_border(
    source: CellGeometry,
    destination: CellGeometry,
    position: Position2,
    margin: u32,
) -> bool {
    let (relative_x, relative_y) = source.relative(position);
    let margin = i32::try_from(margin).unwrap_or(i32::MAX);
    let extent = i32::try_from(source.extent).unwrap_or(i32::MAX);
    (source.row == destination.row
        && destination.column + 1 == source.column
        && relative_x < margin)
        || (source.row == destination.row
            && source.column + 1 == destination.column
            && relative_x >= extent - margin)
        || (source.column == destination.column
            && destination.row + 1 == source.row
            && relative_y < margin)
        || (source.column == destination.column
            && source.row + 1 == destination.row
            && relative_y >= extent - margin)
}

fn squared_distance(left: Position2, right: Position2) -> i128 {
    let x = i128::from(left.x()) - i128::from(right.x());
    let y = i128::from(left.y()) - i128::from(right.y());
    x * x + y * y
}

fn entity_id(index: usize) -> Result<EntityId, HarnessError> {
    let value = u64::try_from(index)
        .map_err(|_| HarnessError::new("entity index exceeds u64"))?
        .checked_add(1)
        .ok_or_else(|| HarnessError::new("entity ID overflow"))?;
    EntityId::new(value).ok_or_else(|| HarnessError::new("entity ID became zero"))
}

fn entity_index(id: EntityId, entity_count: usize) -> Result<usize, HarnessError> {
    let index =
        usize::try_from(id.get() - 1).map_err(|_| HarnessError::new("entity ID exceeds usize"))?;
    if index < entity_count {
        Ok(index)
    } else {
        Err(HarnessError::new("entity ID exceeded active range"))
    }
}

fn cell_index(id: CellId, cell_count: usize) -> Result<usize, HarnessError> {
    let index =
        usize::try_from(id.get() - 1).map_err(|_| HarnessError::new("cell ID exceeds usize"))?;
    if index < cell_count {
        Ok(index)
    } else {
        Err(HarnessError::new("cell ID exceeded topology range"))
    }
}

fn splitmix64(mut value: u64) -> u64 {
    value = value.wrapping_add(0x9e37_79b9_7f4a_7c15);
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^ (value >> 31)
}

fn shuffle_intents(intents: &mut [VelocityIntent], mut state: u64) {
    for upper in (1..intents.len()).rev() {
        state = splitmix64(state);
        let destination = usize::try_from(state % (upper as u64 + 1)).unwrap_or(0);
        intents.swap(upper, destination);
    }
}

fn heading_code(velocity: Velocity2) -> u8 {
    match (velocity.x().signum(), velocity.y().signum()) {
        (1, 0) => 1,
        (-1, 0) => 2,
        (0, 1) => 3,
        (0, -1) => 4,
        _ => 0,
    }
}

fn checked_i16(value: i32, label: &'static str) -> Result<i16, HarnessError> {
    i16::try_from(value).map_err(|_| HarnessError::new(format!("{label} exceeded i16")))
}

fn checked_i8(value: i32, label: &'static str) -> Result<i8, HarnessError> {
    i8::try_from(value).map_err(|_| HarnessError::new(format!("{label} exceeded i8")))
}

fn write_array<const N: usize>(
    output: &mut [u8],
    offset: usize,
    value: [u8; N],
) -> Result<(), HarnessError> {
    let destination = output
        .get_mut(offset..offset + N)
        .ok_or_else(|| HarnessError::new("codec output was too small"))?;
    destination.copy_from_slice(&value);
    Ok(())
}

fn read_array<const N: usize>(input: &[u8], offset: usize) -> Result<[u8; N], HarnessError> {
    let source = input
        .get(offset..offset + N)
        .ok_or_else(|| HarnessError::new("codec input was truncated"))?;
    let mut value = [0_u8; N];
    value.copy_from_slice(source);
    Ok(value)
}

fn write_u16(output: &mut [u8], offset: usize, value: u16) -> Result<(), HarnessError> {
    write_array(output, offset, value.to_le_bytes())
}

fn write_i16(output: &mut [u8], offset: usize, value: i16) -> Result<(), HarnessError> {
    write_array(output, offset, value.to_le_bytes())
}

fn write_u32(output: &mut [u8], offset: usize, value: u32) -> Result<(), HarnessError> {
    write_array(output, offset, value.to_le_bytes())
}

fn write_i32(output: &mut [u8], offset: usize, value: i32) -> Result<(), HarnessError> {
    write_array(output, offset, value.to_le_bytes())
}

fn write_u64(output: &mut [u8], offset: usize, value: u64) -> Result<(), HarnessError> {
    write_array(output, offset, value.to_le_bytes())
}

fn read_u16(input: &[u8], offset: usize) -> Result<u16, HarnessError> {
    Ok(u16::from_le_bytes(read_array(input, offset)?))
}

fn read_i16(input: &[u8], offset: usize) -> Result<i16, HarnessError> {
    Ok(i16::from_le_bytes(read_array(input, offset)?))
}

fn read_u32(input: &[u8], offset: usize) -> Result<u32, HarnessError> {
    Ok(u32::from_le_bytes(read_array(input, offset)?))
}

fn read_i32(input: &[u8], offset: usize) -> Result<i32, HarnessError> {
    Ok(i32::from_le_bytes(read_array(input, offset)?))
}

fn read_u64(input: &[u8], offset: usize) -> Result<u64, HarnessError> {
    Ok(u64::from_le_bytes(read_array(input, offset)?))
}

#[derive(Clone, Copy, Debug)]
struct Checksum(u64);

impl Checksum {
    const fn new(seed: u64) -> Self {
        Self(0xcbf2_9ce4_8422_2325 ^ seed)
    }

    fn mix(&mut self, value: u64) {
        for byte in value.to_le_bytes() {
            self.0 ^= u64::from(byte);
            self.0 = self.0.wrapping_mul(0x0000_0100_0000_01b3);
        }
    }

    const fn finish(self) -> u64 {
        self.0
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn small_config() -> BattleConfig {
        BattleConfig {
            entity_count: 128,
            cell_columns: 2,
            cell_rows: 2,
            grid_cell_size: 32,
            grid_cells_per_cell: 8,
            measured_ticks: 12,
            tick_rate_hz: 30,
            replication_period_ticks: 3,
            engaged_radius: 32,
            awareness_radius: 96,
            client_snapshot_budget: 1_200,
            max_datagram_bytes: 1_200,
            ghost_margin: 32,
            handoff_first_tick: 4,
            handoff_interval_ticks: 6,
            handoff_pairs_per_boundary: 1,
            queue_max_frames: 8,
            queue_max_bytes: 8 * 1_200,
            queue_drain_frames_per_tick: 8,
            seed: 0x12_34_56_78_9a_bc_de_f0,
        }
    }

    #[test]
    fn explicit_payload_codecs_round_trip_and_reject_shape_changes() {
        let ghost = GhostRecord {
            entity: EntityId::new(7).expect("nonzero entity"),
            epoch: OwnershipEpoch::new(3).expect("nonzero epoch"),
            position: Position2::new(-12, 400),
            velocity: Velocity2::new(1, -1),
        };
        let encoded_ghost = encode_ghost_payload(&[ghost]).expect("ghost encodes");
        assert_eq!(
            decode_ghost_payload(&encoded_ghost).map(|value| value.0),
            Ok(1)
        );
        assert!(decode_ghost_payload(&encoded_ghost[..encoded_ghost.len() - 1]).is_err());
        let mut trailing_ghost = encoded_ghost.clone();
        trailing_ghost.push(0);
        assert!(decode_ghost_payload(&trailing_ghost).is_err());

        let event = ControlEvent {
            opcode: 1,
            handoff_id: HandoffId::new(9).expect("nonzero handoff"),
            entity: ghost.entity,
            epoch: ghost.epoch,
            source_index: 0,
            destination_index: 1,
            cutover_tick: Tick::new(44),
        };
        let encoded_handoff = encode_handoff_payload(event).expect("handoff encodes");
        assert!(decode_handoff_payload(&encoded_handoff).is_ok());
        let mut invalid_handoff = encoded_handoff;
        invalid_handoff[5] = 0xff;
        assert!(decode_handoff_payload(&invalid_handoff).is_err());

        let mut compact = vec![0_u8; COMPACT_PAGE_HEADER_LEN + ENGAGED_RECORD_LEN];
        encode_compact_header(
            &mut compact,
            CompactTier::Engaged,
            1,
            Tick::new(4),
            ghost.entity,
        )
        .expect("header encodes");
        write_u32(&mut compact, COMPACT_PAGE_HEADER_LEN, 8).expect("record handle writes");
        assert_eq!(
            decode_compact_page(&compact).map(|value| (value.0, value.1)),
            Ok((CompactTier::Engaged, 1))
        );
        let mut invalid_version = compact.clone();
        invalid_version[4] = 2;
        assert!(decode_compact_page(&invalid_version).is_err());
        assert!(decode_compact_page(&compact[..compact.len() - 1]).is_err());
        compact.push(0);
        assert!(decode_compact_page(&compact).is_err());
    }

    #[test]
    fn bounded_queue_evicts_ghost_before_rejecting_control() {
        let route = RouteScope::new(
            WorldId::new(1).expect("nonzero world"),
            InstanceId::new(1).expect("nonzero instance"),
            CellId::new(1).expect("nonzero source"),
            CellId::new(2).expect("nonzero destination"),
            RouteEpoch::new(1).expect("nonzero route epoch"),
        );
        let frame = |class, kind| QueuedFrame {
            bytes: vec![0_u8; 100],
            route,
            deadline: Tick::new(2),
            class,
            kind,
        };
        let mut queue = BoundedFrameQueue::new(2, 200);
        queue.enqueue(frame(QueueClass::Ghost, MessageKind::GhostSnapshot));
        queue.enqueue(frame(QueueClass::Ghost, MessageKind::GhostSnapshot));
        queue.enqueue(frame(QueueClass::Control, MessageKind::HandoffControl));
        assert_eq!(queue.len(), 2);
        assert_eq!(queue.bytes, 200);
        assert_eq!(queue.metrics.ghost_evicted_for_control, 1);
        assert_eq!(queue.metrics.control_rejected, 0);
        assert_eq!(
            queue
                .frames
                .iter()
                .filter(|frame| frame.class == QueueClass::Control)
                .count(),
            1
        );
    }

    #[test]
    fn direct_corner_ghost_routing_matches_all_cell_oracle() {
        let config = small_config();
        let source = CellGeometry::new(0, config).expect("geometry builds");
        let position = Position2::new(
            source.origin.x() + i32::try_from(source.extent).expect("extent fits") - 1,
            source.origin.y() + i32::try_from(source.extent).expect("extent fits") - 1,
        );
        let direct = direct_ghost_neighbors(source, position, config).expect("routing succeeds");
        assert_eq!(direct, vec![1, 2]);
        let oracle: Vec<usize> = (0..config.cell_count().expect("cell count"))
            .filter(|destination| {
                *destination != source.index
                    && brute_cells_share_ghost_border(
                        source,
                        CellGeometry::new(*destination, config).expect("geometry builds"),
                        position,
                        config.ghost_margin,
                    )
            })
            .collect();
        assert_eq!(direct, oracle);
    }

    #[test]
    fn merged_multicell_interest_matches_global_brute_force_oracle() {
        let config = small_config();
        let mut world = BattleWorld::new(config).expect("world builds");
        let observer = entity_id(0).expect("observer ID");
        let center = world.positions[0];
        let mut actual = Vec::new();
        let cohort_index = world
            .observer_cohort_index(center)
            .expect("observer maps to a cohort");
        for plan_index in 0..world.cohort_plans[cohort_index].cell_indices.len() {
            let cell_index = world.cohort_plans[cohort_index].cell_indices[plan_index];
            world.cells[cell_index]
                .query_radius(
                    center,
                    config.awareness_radius,
                    &mut world.query_buffers[cell_index],
                )
                .expect("bounded query succeeds");
            actual.extend(
                world.query_buffers[cell_index]
                    .ids()
                    .iter()
                    .copied()
                    .filter(|id| *id != observer),
            );
        }
        actual.sort_unstable();
        let radius_squared =
            i128::from(config.awareness_radius) * i128::from(config.awareness_radius);
        let expected: Vec<EntityId> = world
            .positions
            .iter()
            .enumerate()
            .filter(|(index, position)| {
                *index != 0 && squared_distance(center, **position) <= radius_squared
            })
            .map(|(index, _)| entity_id(index).expect("entity ID"))
            .collect();
        assert_eq!(actual, expected);
    }

    #[test]
    fn cohort_batching_matches_flat_cell_baseline_for_every_observer() {
        let config = small_config();
        let mut baseline = BattleWorld::new(config).expect("baseline world builds");
        let mut optimized = BattleWorld::new(config).expect("optimized world builds");
        let tick = Tick::new(1);

        for observer_index in 0..config.entity_count {
            baseline
                .compile_observer_interest_baseline(observer_index, tick)
                .expect("flat baseline compiles");
            let cohort_index = optimized
                .observer_cohort_index(optimized.positions[observer_index])
                .expect("observer maps to a cohort");
            let outcome = optimized
                .compile_observer_interest(observer_index, cohort_index, tick)
                .expect("cohort interest compiles");
            optimized
                .record_observer_interest(outcome, tick)
                .expect("cohort outcome records");
        }

        baseline.metrics.interest_cell_queries = optimized.metrics.interest_cell_queries;
        assert_eq!(baseline.metrics, optimized.metrics);
        assert_eq!(baseline.checksum.finish(), optimized.checksum.finish());
        assert_eq!(
            baseline.client_snapshot_samples,
            optimized.client_snapshot_samples
        );
    }

    #[test]
    fn compact_budget_is_monotonic_and_bounded() {
        let budget = 1_200;
        let selected = max_records_within_budget(1_000, AWARENESS_RECORD_LEN, 1_200, budget)
            .expect("budget calculation succeeds");
        assert!(
            encoded_pages_len(selected, AWARENESS_RECORD_LEN, 1_200).expect("length") <= budget
        );
        assert!(
            encoded_pages_len(selected + 1, AWARENESS_RECORD_LEN, 1_200).expect("length") > budget
        );
    }

    #[test]
    fn small_multicell_battle_replays_exactly_and_conserves_authority() {
        let config = small_config();
        let left = run_battle(config).expect("left workload succeeds");
        let right = run_battle(config).expect("right workload succeeds");
        assert_eq!(left.deterministic, right.deterministic);
        assert_eq!(left.queue, right.queue);
        assert_eq!(left.deterministic.unique_observers, config.entity_count);
        assert_eq!(left.deterministic.handoffs_started, 8);
        assert_eq!(left.deterministic.handoffs_committed, 8);
        assert_eq!(left.deterministic.handoffs_aborted, 0);
        assert!(left.queue.ghost_dropped + left.queue.ghost_evicted_for_control > 0);
        assert_eq!(left.queue.control_rejected, 0);
        assert_eq!(left.final_queue_frames, 0);
        assert_eq!(left.final_queue_bytes, 0);
    }

    #[test]
    fn json_records_are_single_line_and_explicitly_reference_only() {
        let config = small_config();
        let result = run_battle(config).expect("workload succeeds");
        let metadata = config.metadata_json();
        let scenario = result.to_json(config);
        assert!(metadata.contains("\"reference_only\":true"));
        assert!(scenario.contains("\"reference_only\":true"));
        assert!(scenario.contains("\"active_entities\":128"));
        assert!(!metadata.contains('\n'));
        assert!(!scenario.contains('\n'));
    }
}

#[derive(Clone, Copy, Debug)]
struct ControlEvent {
    opcode: u8,
    handoff_id: HandoffId,
    entity: EntityId,
    epoch: OwnershipEpoch,
    source_index: usize,
    destination_index: usize,
    cutover_tick: Tick,
}

#[derive(Clone, Copy, Debug)]
struct CommitOperation {
    entity: EntityId,
    source_index: usize,
    destination_index: usize,
    previous: OwnershipLease,
    current: OwnershipLease,
    handoff_id: HandoffId,
    cutover_tick: Tick,
}

impl BattleWorld {
    fn process_handoffs(&mut self, tick: Tick) -> Result<(), HarnessError> {
        let mut controls = Vec::new();
        let mut commits = Vec::new();
        for entry in &mut self.active_handoffs {
            if tick == entry.ready_tick {
                let outcome = entry
                    .machine
                    .destination_ready(entry.machine.id(), tick)
                    .map_err(|error| HarnessError::new(error.to_string()))?;
                require(
                    outcome == ReadyOutcome::Accepted,
                    "first deterministic ready acknowledgement was not accepted",
                )?;
                controls.push(ControlEvent {
                    opcode: 2,
                    handoff_id: entry.machine.id(),
                    entity: entry.entity,
                    epoch: entry.machine.writer().epoch(),
                    source_index: entry.destination_index,
                    destination_index: entry.source_index,
                    cutover_tick: entry.machine.cutover_tick(),
                });
            }

            match entry
                .machine
                .advance_to(tick)
                .map_err(|error| HarnessError::new(error.to_string()))?
            {
                HandoffTransition::None => {}
                HandoffTransition::Committed {
                    previous,
                    current,
                    at_tick,
                } => commits.push(CommitOperation {
                    entity: entry.entity,
                    source_index: entry.source_index,
                    destination_index: entry.destination_index,
                    previous,
                    current,
                    handoff_id: entry.machine.id(),
                    cutover_tick: at_tick,
                }),
                HandoffTransition::Aborted { .. } => self.metrics.handoffs_aborted += 1,
            }
        }

        for control in controls {
            self.emit_control_frame(control, tick)?;
        }
        for operation in commits {
            self.commit_migration(operation)?;
            self.metrics.handoffs_committed += 1;
            self.emit_control_frame(
                ControlEvent {
                    opcode: 3,
                    handoff_id: operation.handoff_id,
                    entity: operation.entity,
                    epoch: operation.current.epoch(),
                    source_index: operation.source_index,
                    destination_index: operation.destination_index,
                    cutover_tick: operation.cutover_tick,
                },
                tick,
            )?;
        }
        self.active_handoffs.retain(|entry| {
            matches!(
                entry.machine.phase(),
                HandoffPhase::Preparing | HandoffPhase::Prepared
            )
        });

        if tick.get() >= self.config.handoff_first_tick
            && (tick.get() - self.config.handoff_first_tick) % self.config.handoff_interval_ticks
                == 0
        {
            self.schedule_balanced_handoffs(tick)?;
        }
        Ok(())
    }

    fn schedule_balanced_handoffs(&mut self, tick: Tick) -> Result<(), HarnessError> {
        require(
            self.active_handoffs.is_empty(),
            "handoff schedule overlapped an active transfer",
        )?;
        let mut specifications = Vec::new();
        for row in 0..self.config.cell_rows {
            for left_column in (0..self.config.cell_columns).step_by(2) {
                let left_index = usize::try_from(row * self.config.cell_columns + left_column)
                    .map_err(|_| HarnessError::new("left cell index overflow"))?;
                let right_index = left_index + 1;
                let left_entities: Vec<EntityId> = self.cells[left_index]
                    .entities()
                    .map(EntityState::id)
                    .collect();
                let right_entities: Vec<EntityId> = self.cells[right_index]
                    .entities()
                    .map(EntityState::id)
                    .collect();
                require(
                    left_entities.len() >= self.config.handoff_pairs_per_boundary
                        && right_entities.len() >= self.config.handoff_pairs_per_boundary,
                    "not enough residents for balanced handoff",
                )?;
                let left_offset = (tick.get() as usize + left_index * 17) % left_entities.len();
                let right_offset = (tick.get() as usize + right_index * 17) % right_entities.len();
                for pair in 0..self.config.handoff_pairs_per_boundary {
                    specifications.push((
                        left_entities[(left_offset + pair) % left_entities.len()],
                        left_index,
                        right_index,
                    ));
                    specifications.push((
                        right_entities[(right_offset + pair) % right_entities.len()],
                        right_index,
                        left_index,
                    ));
                }
            }
        }

        let ready_tick = tick
            .checked_add(1)
            .ok_or_else(|| HarnessError::new("ready tick overflow"))?;
        let cutover_tick = tick
            .checked_add(2)
            .ok_or_else(|| HarnessError::new("cutover tick overflow"))?;
        let limits = HandoffLimits::new(4).expect("constant handoff span is nonzero");
        for (entity, source_index, destination_index) in specifications {
            let index = entity_index(entity, self.config.entity_count)?;
            let lease = self.leases[index];
            require(
                lease.owner() == self.geometry[source_index].cell_id(),
                "handoff source did not own entity",
            )?;
            let handoff_id = HandoffId::new(self.next_handoff_id)
                .ok_or_else(|| HarnessError::new("handoff ID became zero"))?;
            self.next_handoff_id = self
                .next_handoff_id
                .checked_add(1)
                .ok_or_else(|| HarnessError::new("handoff ID overflow"))?;
            let machine = Handoff::begin(
                handoff_id,
                lease,
                self.geometry[destination_index].cell_id(),
                tick,
                ready_tick,
                cutover_tick,
                limits,
            )
            .map_err(|error| HarnessError::new(error.to_string()))?;
            self.active_handoffs.push(ActiveHandoff {
                machine,
                entity,
                source_index,
                destination_index,
                ready_tick,
            });
            self.metrics.handoffs_started += 1;
            self.emit_control_frame(
                ControlEvent {
                    opcode: 1,
                    handoff_id,
                    entity,
                    epoch: lease.epoch(),
                    source_index,
                    destination_index,
                    cutover_tick,
                },
                tick,
            )?;
        }
        Ok(())
    }

    fn commit_migration(&mut self, operation: CommitOperation) -> Result<(), HarnessError> {
        let index = entity_index(operation.entity, self.config.entity_count)?;
        require(
            self.leases[index] == operation.previous,
            "handoff previous lease mismatched authority table",
        )?;
        let snapshot = self.cells[operation.source_index]
            .despawn(operation.entity)
            .ok_or_else(|| HarnessError::new("handoff source resident was missing"))?;
        let source = self.geometry[operation.source_index];
        let destination = self.geometry[operation.destination_index];
        require(
            source.row == destination.row,
            "reference handoff crossed a non-horizontal edge",
        )?;
        let (_, relative_y) = source.relative(snapshot.position());
        let maximum_relative = i32::try_from(destination.extent)
            .map_err(|_| HarnessError::new("destination extent exceeds i32"))?
            - 3;
        let destination_y = destination.origin.y() + relative_y.clamp(2, maximum_relative);
        let destination_x = if destination.column > source.column {
            destination.origin.x() + 2
        } else {
            destination.origin.x()
                + i32::try_from(destination.extent)
                    .map_err(|_| HarnessError::new("destination extent exceeds i32"))?
                - 3
        };
        let moved = EntityState::new(
            snapshot.id(),
            Position2::new(destination_x, destination_y),
            snapshot.velocity(),
        );
        self.cells[operation.destination_index]
            .spawn(moved)
            .map_err(|error| HarnessError::new(error.to_string()))?;
        self.leases[index] = operation.current;
        require(
            operation.current.owner() == destination.cell_id()
                && operation.current.epoch().get() == operation.previous.epoch().get() + 1,
            "handoff did not publish the destination with one higher epoch",
        )?;
        require(
            self.cells[operation.source_index]
                .entity(operation.entity)
                .is_none()
                && self.cells[operation.destination_index]
                    .entity(operation.entity)
                    .is_some(),
            "handoff exposed zero or two resident writers",
        )?;
        Ok(())
    }

    fn emit_control_frame(
        &mut self,
        event: ControlEvent,
        source_tick: Tick,
    ) -> Result<(), HarnessError> {
        let payload = encode_handoff_payload(event)?;
        let deadline = source_tick
            .checked_add(4)
            .ok_or_else(|| HarnessError::new("control deadline overflow"))?;
        self.enqueue_wire_frame(WireFrameRequest {
            kind: MessageKind::HandoffControl,
            flags: FrameFlags::IDEMPOTENT,
            source_index: event.source_index,
            destination_index: event.destination_index,
            source_tick,
            deadline_tick: deadline,
            payload: &payload,
            class: QueueClass::Control,
        })?;
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct GhostRecord {
    entity: EntityId,
    epoch: OwnershipEpoch,
    position: Position2,
    velocity: Velocity2,
}

struct WireFrameRequest<'a> {
    kind: MessageKind,
    flags: FrameFlags,
    source_index: usize,
    destination_index: usize,
    source_tick: Tick,
    deadline_tick: Tick,
    payload: &'a [u8],
    class: QueueClass,
}

impl BattleWorld {
    fn generate_ghost_frames(&mut self, tick: Tick) -> Result<(), HarnessError> {
        for records in &mut self.route_ghost_records {
            records.clear();
        }
        let cell_count = self.config.cell_count()?;
        for source_index in 0..cell_count {
            let geometry = self.geometry[source_index];
            for state in self.cells[source_index].entities() {
                let entity_index = entity_index(state.id(), self.config.entity_count)?;
                let record = GhostRecord {
                    entity: state.id(),
                    epoch: self.leases[entity_index].epoch(),
                    position: state.position(),
                    velocity: state.velocity(),
                };
                for destination in direct_ghost_neighbors(geometry, state.position(), self.config)?
                {
                    let route_index = source_index * cell_count + destination;
                    self.route_ghost_records[route_index].push(record);
                    self.metrics.ghost_records_generated += 1;
                }
            }
        }

        if tick.get() == 1 || tick.get() == self.config.measured_ticks {
            self.verify_ghost_routes_against_oracle()?;
        }

        let max_records = (FRAME_PAYLOAD_BUDGET - GHOST_PAYLOAD_HEADER_LEN) / GHOST_RECORD_LEN;
        require(max_records > 0, "ghost frame cannot fit one record")?;
        for source_index in 0..cell_count {
            for destination_index in 0..cell_count {
                if source_index == destination_index {
                    continue;
                }
                let route_index = source_index * cell_count + destination_index;
                let records = self.route_ghost_records[route_index].clone();
                for chunk in records.chunks(max_records) {
                    let payload = encode_ghost_payload(chunk)?;
                    let deadline = tick
                        .checked_add(1)
                        .ok_or_else(|| HarnessError::new("ghost deadline overflow"))?;
                    let frame_len = self.enqueue_wire_frame(WireFrameRequest {
                        kind: MessageKind::GhostSnapshot,
                        flags: FrameFlags::IDEMPOTENT.union(FrameFlags::KEYFRAME),
                        source_index,
                        destination_index,
                        source_tick: tick,
                        deadline_tick: deadline,
                        payload: &payload,
                        class: QueueClass::Ghost,
                    })?;
                    self.metrics.ghost_frames_generated += 1;
                    self.metrics.ghost_frame_bytes_generated += frame_len as u64;
                }
            }
        }
        Ok(())
    }

    fn verify_ghost_routes_against_oracle(&mut self) -> Result<(), HarnessError> {
        let cell_count = self.config.cell_count()?;
        let mut expected = vec![0_usize; cell_count * cell_count];
        for source_index in 0..cell_count {
            for state in self.cells[source_index].entities() {
                for destination_index in 0..cell_count {
                    if source_index != destination_index
                        && brute_cells_share_ghost_border(
                            self.geometry[source_index],
                            self.geometry[destination_index],
                            state.position(),
                            self.config.ghost_margin,
                        )
                    {
                        expected[source_index * cell_count + destination_index] += 1;
                    }
                }
            }
        }
        for (route, records) in self.route_ghost_records.iter().enumerate() {
            require(
                records.len() == expected[route],
                "direct ghost routing disagreed with all-cell border oracle",
            )?;
        }
        self.metrics.ghost_oracle_checks += 1;
        Ok(())
    }

    fn enqueue_wire_frame(&mut self, request: WireFrameRequest<'_>) -> Result<usize, HarnessError> {
        let WireFrameRequest {
            kind,
            flags,
            source_index,
            destination_index,
            source_tick,
            deadline_tick,
            payload,
            class,
        } = request;
        let route = RouteScope::new(
            WorldId::new(1).expect("constant world is nonzero"),
            InstanceId::new(1).expect("constant instance is nonzero"),
            self.geometry[source_index].cell_id(),
            self.geometry[destination_index].cell_id(),
            RouteEpoch::new(1).expect("constant route epoch is nonzero"),
        );
        let window = FrameWindow::new(source_tick, deadline_tick)
            .ok_or_else(|| HarnessError::new("wire deadline preceded source tick"))?;
        let header = FrameHeader::new(kind, flags, route, window, self.next_sequence);
        self.next_sequence = self
            .next_sequence
            .checked_add(1)
            .ok_or_else(|| HarnessError::new("wire sequence overflow"))?;
        let limits = FrameLimits::new(FRAME_PAYLOAD_BUDGET)
            .expect("payload budget is below absolute ceiling");
        let mut encoded = [0_u8; 1_200];
        let length = encode_v1(header, payload, &mut encoded, limits)
            .map_err(|error| HarnessError::new(error.to_string()))?;
        require(
            length <= self.config.max_datagram_bytes,
            "wire frame exceeded MTU budget",
        )?;
        self.queue.enqueue(QueuedFrame {
            bytes: encoded[..length].to_vec(),
            route,
            deadline: deadline_tick,
            class,
            kind,
        });
        Ok(length)
    }

    fn compile_interest_shard(&mut self, tick: Tick) -> Result<(), HarnessError> {
        let period = usize::try_from(self.config.replication_period_ticks)
            .map_err(|_| HarnessError::new("replication period exceeds usize"))?;
        let shard = usize::try_from(tick.get() % self.config.replication_period_ticks)
            .map_err(|_| HarnessError::new("replication shard exceeds usize"))?;

        self.interest_worklist.clear();
        for observer_index in (shard..self.config.entity_count).step_by(period) {
            let cohort_index = self.observer_cohort_index(self.positions[observer_index])?;
            self.interest_worklist.push((cohort_index, observer_index));
        }
        self.interest_worklist.sort_unstable();

        self.observer_outcomes.clear();
        for work_index in 0..self.interest_worklist.len() {
            let (cohort_index, observer_index) = self.interest_worklist[work_index];
            let outcome = self.compile_observer_interest(observer_index, cohort_index, tick)?;
            self.observer_outcomes.push(outcome);
        }
        self.observer_outcomes
            .sort_unstable_by_key(|outcome| outcome.observer_index);
        for outcome_index in 0..self.observer_outcomes.len() {
            let outcome = self.observer_outcomes[outcome_index];
            self.record_observer_interest(outcome, tick)?;
        }
        Ok(())
    }

    fn observer_cohort_index(&self, position: Position2) -> Result<usize, HarnessError> {
        let x = u32::try_from(position.x())
            .map_err(|_| HarnessError::new("observer x was below the world origin"))?;
        let y = u32::try_from(position.y())
            .map_err(|_| HarnessError::new("observer y was below the world origin"))?;
        let cell_extent = self.config.cell_extent()?;
        let world_width = self
            .config
            .cell_columns
            .checked_mul(cell_extent)
            .ok_or_else(|| HarnessError::new("world width overflow"))?;
        let world_height = self
            .config
            .cell_rows
            .checked_mul(cell_extent)
            .ok_or_else(|| HarnessError::new("world height overflow"))?;
        require(
            x < world_width && y < world_height,
            "observer escaped the cohort topology",
        )?;
        let cohort_extent = cell_extent / COHORTS_PER_CELL_SIDE;
        let cohort_columns = self
            .config
            .cell_columns
            .checked_mul(COHORTS_PER_CELL_SIDE)
            .ok_or_else(|| HarnessError::new("cohort column count overflow"))?;
        let column = x / cohort_extent;
        let row = y / cohort_extent;
        let index = u64::from(row)
            .checked_mul(u64::from(cohort_columns))
            .and_then(|row_offset| row_offset.checked_add(u64::from(column)))
            .ok_or_else(|| HarnessError::new("observer cohort index overflow"))?;
        let index = usize::try_from(index)
            .map_err(|_| HarnessError::new("observer cohort index exceeds usize"))?;
        require(
            index < self.cohort_plans.len(),
            "observer cohort index exceeded plan count",
        )?;
        Ok(index)
    }

    fn compile_observer_interest(
        &mut self,
        observer_index: usize,
        cohort_index: usize,
        tick: Tick,
    ) -> Result<ObserverInterestOutcome, HarnessError> {
        let observer = entity_id(observer_index)?;
        let observer_position = self.positions[observer_index];
        let awareness_radius_squared =
            i128::from(self.config.awareness_radius) * i128::from(self.config.awareness_radius);
        let engaged_radius_squared =
            i128::from(self.config.engaged_radius) * i128::from(self.config.engaged_radius);
        self.interest_scratch.clear();
        let mut candidates = 0_usize;
        let cell_queries = self.cohort_plans[cohort_index].cell_indices.len();
        for plan_index in 0..cell_queries {
            let cell_index = self.cohort_plans[cohort_index].cell_indices[plan_index];
            let stats = self.cells[cell_index]
                .query_radius(
                    observer_position,
                    self.config.awareness_radius,
                    &mut self.query_buffers[cell_index],
                )
                .map_err(|error| match error {
                    RadiusQueryError::ResultCapacityExceeded { required, .. } => HarnessError::new(
                        format!("interest query buffer too small; required {required}"),
                    ),
                    other => HarnessError::new(other.to_string()),
                })?;
            candidates += stats.candidates_examined();
            for id in self.query_buffers[cell_index].ids() {
                if *id == observer {
                    continue;
                }
                let target_index = entity_index(*id, self.config.entity_count)?;
                let distance = squared_distance(observer_position, self.positions[target_index]);
                require(
                    distance <= awareness_radius_squared,
                    "spatial query returned an out-of-radius entity",
                )?;
                if distance <= engaged_radius_squared {
                    self.interest_scratch.engaged.push(*id);
                } else {
                    self.interest_scratch.awareness.push(*id);
                }
            }
        }
        self.interest_scratch.engaged.sort_unstable();
        self.interest_scratch.awareness.sort_unstable();

        let causal_index =
            (observer_index + self.config.entity_count / 2) % self.config.entity_count;
        let mut causal_promotions = 0_usize;
        if causal_index != observer_index {
            let causal = entity_id(causal_index)?;
            match self.interest_scratch.awareness.binary_search(&causal) {
                Ok(index) => {
                    self.interest_scratch.awareness.remove(index);
                    self.interest_scratch.engaged.push(causal);
                    causal_promotions += 1;
                }
                Err(_)
                    if self
                        .interest_scratch
                        .engaged
                        .binary_search(&causal)
                        .is_err() =>
                {
                    self.interest_scratch.engaged.push(causal);
                    causal_promotions += 1;
                }
                Err(_) => {}
            }
            self.interest_scratch.engaged.sort_unstable();
        }

        for id in &self.interest_scratch.engaged {
            let index = entity_index(*id, self.config.entity_count)?;
            let owner_index = cell_index(self.leases[index].owner(), self.cells.len())?;
            if id.get() & 1 == 0 {
                self.interest_scratch.engaged_faction_a[owner_index] += 1;
            } else {
                self.interest_scratch.engaged_faction_b[owner_index] += 1;
            }
        }
        for id in &self.interest_scratch.awareness {
            let index = entity_index(*id, self.config.entity_count)?;
            let owner_index = cell_index(self.leases[index].owner(), self.cells.len())?;
            if id.get() & 1 == 0 {
                self.interest_scratch.awareness_faction_a[owner_index] += 1;
            } else {
                self.interest_scratch.awareness_faction_b[owner_index] += 1;
            }
        }

        let observer_cell = cell_index(self.leases[observer_index].owner(), self.cells.len())?;
        let observer_is_faction_a = observer.get() & 1 == 0;
        let mut desired_mass_count = 0_usize;
        let mut base_mass_count = 0_usize;
        for cell_index in 0..self.cells.len() {
            let observer_a = usize::from(cell_index == observer_cell && observer_is_faction_a);
            let observer_b = usize::from(cell_index == observer_cell && !observer_is_faction_a);
            let base_a = self.resident_faction_a[cell_index]
                .checked_sub(observer_a + self.interest_scratch.engaged_faction_a[cell_index])
                .ok_or_else(|| HarnessError::new("engaged faction A count exceeded residents"))?;
            let base_b = self.resident_faction_b[cell_index]
                .checked_sub(observer_b + self.interest_scratch.engaged_faction_b[cell_index])
                .ok_or_else(|| HarnessError::new("engaged faction B count exceeded residents"))?;
            let desired_a = base_a
                .checked_sub(self.interest_scratch.awareness_faction_a[cell_index])
                .ok_or_else(|| HarnessError::new("awareness faction A count exceeded residents"))?;
            let desired_b = base_b
                .checked_sub(self.interest_scratch.awareness_faction_b[cell_index])
                .ok_or_else(|| HarnessError::new("awareness faction B count exceeded residents"))?;
            let engaged_count = self.interest_scratch.engaged_faction_a[cell_index]
                + self.interest_scratch.engaged_faction_b[cell_index];
            let awareness_count = self.interest_scratch.awareness_faction_a[cell_index]
                + self.interest_scratch.awareness_faction_b[cell_index];
            require(
                desired_a
                    + desired_b
                    + engaged_count
                    + awareness_count
                    + usize::from(cell_index == observer_cell)
                    == self.resident_counts[cell_index],
                "desired mass aggregation did not conserve cell residents",
            )?;
            base_mass_count += usize::from(base_a + base_b > 0);
            desired_mass_count += usize::from(desired_a + desired_b > 0);
        }

        let engaged_count = self.interest_scratch.engaged.len();
        let awareness_count = self.interest_scratch.awareness.len();
        let desired_bytes = encoded_pages_len(
            engaged_count,
            ENGAGED_RECORD_LEN,
            self.config.max_datagram_bytes,
        )? + encoded_pages_len(
            awareness_count,
            AWARENESS_RECORD_LEN,
            self.config.max_datagram_bytes,
        )? + encoded_pages_len(
            desired_mass_count,
            MASS_RECORD_LEN,
            self.config.max_datagram_bytes,
        )?;
        let base_bytes = encoded_pages_len(
            engaged_count,
            ENGAGED_RECORD_LEN,
            self.config.max_datagram_bytes,
        )? + encoded_pages_len(
            base_mass_count,
            MASS_RECORD_LEN,
            self.config.max_datagram_bytes,
        )?;
        let sent_awareness_count = if base_bytes >= self.config.client_snapshot_budget {
            0
        } else {
            max_records_within_budget(
                awareness_count,
                AWARENESS_RECORD_LEN,
                self.config.max_datagram_bytes,
                self.config.client_snapshot_budget - base_bytes,
            )?
        };
        for id in &self.interest_scratch.awareness[..sent_awareness_count] {
            let index = entity_index(*id, self.config.entity_count)?;
            let owner_index = cell_index(self.leases[index].owner(), self.cells.len())?;
            if id.get() & 1 == 0 {
                self.interest_scratch.sent_faction_a[owner_index] += 1;
            } else {
                self.interest_scratch.sent_faction_b[owner_index] += 1;
            }
        }

        for cell_index in 0..self.cells.len() {
            let observer_a = usize::from(cell_index == observer_cell && observer_is_faction_a);
            let observer_b = usize::from(cell_index == observer_cell && !observer_is_faction_a);
            let faction_a = self.resident_faction_a[cell_index]
                .checked_sub(
                    observer_a
                        + self.interest_scratch.engaged_faction_a[cell_index]
                        + self.interest_scratch.sent_faction_a[cell_index],
                )
                .ok_or_else(|| HarnessError::new("sent faction A count exceeded residents"))?;
            let faction_b = self.resident_faction_b[cell_index]
                .checked_sub(
                    observer_b
                        + self.interest_scratch.engaged_faction_b[cell_index]
                        + self.interest_scratch.sent_faction_b[cell_index],
                )
                .ok_or_else(|| HarnessError::new("sent faction B count exceeded residents"))?;
            let entity_count = faction_a + faction_b;
            let individual_count = self.interest_scratch.engaged_faction_a[cell_index]
                + self.interest_scratch.engaged_faction_b[cell_index]
                + self.interest_scratch.sent_faction_a[cell_index]
                + self.interest_scratch.sent_faction_b[cell_index];
            require(
                entity_count + individual_count + usize::from(cell_index == observer_cell)
                    == self.resident_counts[cell_index],
                "sent mass aggregation did not conserve cell residents",
            )?;
            if entity_count == 0 {
                continue;
            }
            let geometry = self.geometry[cell_index];
            let center = Position2::new(
                geometry.origin.x() + i32::try_from(geometry.extent / 2).unwrap_or(0),
                geometry.origin.y() + i32::try_from(geometry.extent / 2).unwrap_or(0),
            );
            self.interest_scratch.mass.push(MassRecord {
                cell_index: u16::try_from(cell_index)
                    .map_err(|_| HarnessError::new("mass cell index exceeds u16"))?,
                faction_a: u16::try_from(faction_a)
                    .map_err(|_| HarnessError::new("faction A aggregate exceeds u16"))?,
                faction_b: u16::try_from(faction_b)
                    .map_err(|_| HarnessError::new("faction B aggregate exceeds u16"))?,
                relative_x: checked_i16(center.x() - observer_position.x(), "mass relative x")?,
                relative_y: checked_i16(center.y() - observer_position.y(), "mass relative y")?,
                entity_count: u16::try_from(entity_count)
                    .map_err(|_| HarnessError::new("mass entity count exceeds u16"))?,
            });
        }

        let mut encoded = CompactEncodeStats::default();
        let InterestScratch {
            engaged,
            awareness,
            mass,
            page_buffer,
            ..
        } = &mut self.interest_scratch;
        encoded.merge(encode_entity_pages(
            CompactEntityPageInput {
                tier: CompactTier::Engaged,
                observer,
                observer_position,
                tick,
                ids: engaged,
                positions: &self.positions,
                velocities: &self.velocities,
                max_datagram_bytes: self.config.max_datagram_bytes,
            },
            page_buffer,
        )?);
        encoded.merge(encode_entity_pages(
            CompactEntityPageInput {
                tier: CompactTier::Awareness,
                observer,
                observer_position,
                tick,
                ids: &awareness[..sent_awareness_count],
                positions: &self.positions,
                velocities: &self.velocities,
                max_datagram_bytes: self.config.max_datagram_bytes,
            },
            page_buffer,
        )?);
        encoded.merge(encode_mass_pages(
            observer,
            tick,
            mass,
            self.config.max_datagram_bytes,
            page_buffer,
        )?);
        let expected_sent_bytes =
            encoded_pages_len(
                engaged_count,
                ENGAGED_RECORD_LEN,
                self.config.max_datagram_bytes,
            )? + encoded_pages_len(
                sent_awareness_count,
                AWARENESS_RECORD_LEN,
                self.config.max_datagram_bytes,
            )? + encoded_pages_len(mass.len(), MASS_RECORD_LEN, self.config.max_datagram_bytes)?;
        require(
            encoded.bytes == expected_sent_bytes,
            "compact codec byte count disagreed with budget formula",
        )?;
        if base_bytes <= self.config.client_snapshot_budget {
            require(
                encoded.bytes <= self.config.client_snapshot_budget,
                "awareness shedding failed to enforce snapshot budget",
            )?;
        }

        let awareness_shed = awareness_count - sent_awareness_count;
        let mass_entities = mass
            .iter()
            .map(|record| usize::from(record.entity_count))
            .sum::<usize>();
        require(
            engaged_count + sent_awareness_count + mass_entities == self.config.entity_count - 1,
            "fidelity tiers did not conserve non-observer entities",
        )?;
        Ok(ObserverInterestOutcome {
            observer_index,
            cell_queries,
            candidates,
            engaged: engaged_count,
            awareness_desired: awareness_count,
            awareness_sent: sent_awareness_count,
            awareness_shed,
            mass_aggregates: mass.len(),
            mass_entities,
            causal_promotions,
            engaged_over_budget: base_bytes >= self.config.client_snapshot_budget,
            desired_bytes,
            encoded,
        })
    }

    fn record_observer_interest(
        &mut self,
        outcome: ObserverInterestOutcome,
        tick: Tick,
    ) -> Result<(), HarnessError> {
        let observer = entity_id(outcome.observer_index)?;
        self.metrics.observer_evaluations += 1;
        self.observer_seen[outcome.observer_index] = true;
        self.metrics.interest_cell_queries += outcome.cell_queries as u64;
        self.metrics.interest_candidates += outcome.candidates as u64;
        self.metrics.engaged_records += outcome.engaged as u64;
        self.metrics.awareness_desired_records += outcome.awareness_desired as u64;
        self.metrics.awareness_sent_records += outcome.awareness_sent as u64;
        self.metrics.awareness_shed_records += outcome.awareness_shed as u64;
        self.metrics.mass_aggregate_records += outcome.mass_aggregates as u64;
        self.metrics.mass_represented_entities += outcome.mass_entities as u64;
        self.metrics.causal_promotions += outcome.causal_promotions as u64;
        self.metrics.degraded_observers += u64::from(outcome.awareness_shed > 0);
        self.metrics.engaged_over_budget_observers += u64::from(outcome.engaged_over_budget);
        self.metrics.client_desired_bytes += outcome.desired_bytes as u64;
        self.metrics.client_sent_bytes += outcome.encoded.bytes as u64;
        self.metrics.client_datagrams += outcome.encoded.pages as u64;
        self.metrics.compact_codec_pages += outcome.encoded.pages as u64;
        self.metrics.compact_codec_bytes += outcome.encoded.bytes as u64;
        self.client_snapshot_samples
            .push(outcome.encoded.bytes as u128);
        self.checksum.mix(observer.get());
        self.checksum.mix(tick.get());
        self.checksum.mix(outcome.candidates as u64);
        self.checksum.mix(outcome.engaged as u64);
        self.checksum.mix(outcome.awareness_sent as u64);
        self.checksum.mix(outcome.mass_entities as u64);
        self.checksum.mix(outcome.encoded.checksum);
        Ok(())
    }

    #[cfg(test)]
    fn compile_observer_interest_baseline(
        &mut self,
        observer_index: usize,
        tick: Tick,
    ) -> Result<(), HarnessError> {
        let observer = entity_id(observer_index)?;
        let observer_position = self.positions[observer_index];
        let awareness_radius_squared =
            i128::from(self.config.awareness_radius) * i128::from(self.config.awareness_radius);
        let engaged_radius_squared =
            i128::from(self.config.engaged_radius) * i128::from(self.config.engaged_radius);
        let mut engaged = Vec::new();
        let mut awareness = Vec::new();
        let mut candidates = 0_usize;
        for cell_index in 0..self.cells.len() {
            let stats = self.cells[cell_index]
                .query_radius(
                    observer_position,
                    self.config.awareness_radius,
                    &mut self.query_buffers[cell_index],
                )
                .map_err(|error| match error {
                    RadiusQueryError::ResultCapacityExceeded { required, .. } => HarnessError::new(
                        format!("interest query buffer too small; required {required}"),
                    ),
                    other => HarnessError::new(other.to_string()),
                })?;
            candidates += stats.candidates_examined();
            for id in self.query_buffers[cell_index].ids() {
                if *id == observer {
                    continue;
                }
                let target_index = entity_index(*id, self.config.entity_count)?;
                let distance = squared_distance(observer_position, self.positions[target_index]);
                require(
                    distance <= awareness_radius_squared,
                    "spatial query returned an out-of-radius entity",
                )?;
                if distance <= engaged_radius_squared {
                    engaged.push(*id);
                } else {
                    awareness.push(*id);
                }
            }
        }
        engaged.sort_unstable();
        awareness.sort_unstable();
        let causal_index =
            (observer_index + self.config.entity_count / 2) % self.config.entity_count;
        if causal_index != observer_index {
            let causal = entity_id(causal_index)?;
            match awareness.binary_search(&causal) {
                Ok(index) => {
                    awareness.remove(index);
                    engaged.push(causal);
                    self.metrics.causal_promotions += 1;
                }
                Err(_) if engaged.binary_search(&causal).is_err() => {
                    engaged.push(causal);
                    self.metrics.causal_promotions += 1;
                }
                Err(_) => {}
            }
            engaged.sort_unstable();
        }

        let desired_mass =
            self.build_mass_records(observer_index, observer_position, &engaged, &awareness)?;
        let desired_bytes = encoded_pages_len(
            engaged.len(),
            ENGAGED_RECORD_LEN,
            self.config.max_datagram_bytes,
        )? + encoded_pages_len(
            awareness.len(),
            AWARENESS_RECORD_LEN,
            self.config.max_datagram_bytes,
        )? + encoded_pages_len(
            desired_mass.len(),
            MASS_RECORD_LEN,
            self.config.max_datagram_bytes,
        )?;

        let base_mass =
            self.build_mass_records(observer_index, observer_position, &engaged, &[])?;
        let base_bytes = encoded_pages_len(
            engaged.len(),
            ENGAGED_RECORD_LEN,
            self.config.max_datagram_bytes,
        )? + encoded_pages_len(
            base_mass.len(),
            MASS_RECORD_LEN,
            self.config.max_datagram_bytes,
        )?;
        let sent_awareness_count = if base_bytes >= self.config.client_snapshot_budget {
            self.metrics.engaged_over_budget_observers += 1;
            0
        } else {
            max_records_within_budget(
                awareness.len(),
                AWARENESS_RECORD_LEN,
                self.config.max_datagram_bytes,
                self.config.client_snapshot_budget - base_bytes,
            )?
        };
        let sent_awareness = &awareness[..sent_awareness_count];
        let mass =
            self.build_mass_records(observer_index, observer_position, &engaged, sent_awareness)?;

        let mut encoded = CompactEncodeStats::default();
        let mut page_buffer = vec![0_u8; self.config.max_datagram_bytes];
        encoded.merge(encode_entity_pages(
            CompactEntityPageInput {
                tier: CompactTier::Engaged,
                observer,
                observer_position,
                tick,
                ids: &engaged,
                positions: &self.positions,
                velocities: &self.velocities,
                max_datagram_bytes: self.config.max_datagram_bytes,
            },
            &mut page_buffer,
        )?);
        encoded.merge(encode_entity_pages(
            CompactEntityPageInput {
                tier: CompactTier::Awareness,
                observer,
                observer_position,
                tick,
                ids: sent_awareness,
                positions: &self.positions,
                velocities: &self.velocities,
                max_datagram_bytes: self.config.max_datagram_bytes,
            },
            &mut page_buffer,
        )?);
        encoded.merge(encode_mass_pages(
            observer,
            tick,
            &mass,
            self.config.max_datagram_bytes,
            &mut page_buffer,
        )?);
        let expected_sent_bytes =
            encoded_pages_len(
                engaged.len(),
                ENGAGED_RECORD_LEN,
                self.config.max_datagram_bytes,
            )? + encoded_pages_len(
                sent_awareness.len(),
                AWARENESS_RECORD_LEN,
                self.config.max_datagram_bytes,
            )? + encoded_pages_len(mass.len(), MASS_RECORD_LEN, self.config.max_datagram_bytes)?;
        require(
            encoded.bytes == expected_sent_bytes,
            "compact codec byte count disagreed with budget formula",
        )?;
        if base_bytes <= self.config.client_snapshot_budget {
            require(
                encoded.bytes <= self.config.client_snapshot_budget,
                "awareness shedding failed to enforce snapshot budget",
            )?;
        }

        let awareness_shed = awareness.len() - sent_awareness.len();
        let mass_entities = mass
            .iter()
            .map(|record| usize::from(record.entity_count))
            .sum::<usize>();
        require(
            engaged.len() + sent_awareness.len() + mass_entities == self.config.entity_count - 1,
            "fidelity tiers did not conserve non-observer entities",
        )?;
        self.metrics.observer_evaluations += 1;
        self.observer_seen[observer_index] = true;
        self.metrics.interest_candidates += candidates as u64;
        self.metrics.engaged_records += engaged.len() as u64;
        self.metrics.awareness_desired_records += awareness.len() as u64;
        self.metrics.awareness_sent_records += sent_awareness.len() as u64;
        self.metrics.awareness_shed_records += awareness_shed as u64;
        self.metrics.mass_aggregate_records += mass.len() as u64;
        self.metrics.mass_represented_entities += mass_entities as u64;
        if awareness_shed > 0 {
            self.metrics.degraded_observers += 1;
        }
        self.metrics.client_desired_bytes += desired_bytes as u64;
        self.metrics.client_sent_bytes += encoded.bytes as u64;
        self.metrics.client_datagrams += encoded.pages as u64;
        self.metrics.compact_codec_pages += encoded.pages as u64;
        self.metrics.compact_codec_bytes += encoded.bytes as u64;
        self.client_snapshot_samples.push(encoded.bytes as u128);
        self.checksum.mix(observer.get());
        self.checksum.mix(tick.get());
        self.checksum.mix(candidates as u64);
        self.checksum.mix(engaged.len() as u64);
        self.checksum.mix(sent_awareness.len() as u64);
        self.checksum.mix(mass_entities as u64);
        self.checksum.mix(encoded.checksum);
        Ok(())
    }

    #[cfg(test)]
    fn build_mass_records(
        &self,
        observer_index: usize,
        observer_position: Position2,
        engaged: &[EntityId],
        awareness: &[EntityId],
    ) -> Result<Vec<MassRecord>, HarnessError> {
        let mut individual_per_cell = vec![0_usize; self.cells.len()];
        let mut faction_a_per_cell = self.resident_faction_a.clone();
        let mut faction_b_per_cell = self.resident_faction_b.clone();
        for id in engaged.iter().chain(awareness) {
            let index = entity_index(*id, self.config.entity_count)?;
            let owner_index = cell_index(self.leases[index].owner(), self.cells.len())?;
            individual_per_cell[owner_index] += 1;
        }
        for id in engaged.iter().chain(awareness) {
            let index = entity_index(*id, self.config.entity_count)?;
            let owner_index = cell_index(self.leases[index].owner(), self.cells.len())?;
            if id.get() & 1 == 0 {
                faction_a_per_cell[owner_index] -= 1;
            } else {
                faction_b_per_cell[owner_index] -= 1;
            }
        }
        let observer_cell = cell_index(self.leases[observer_index].owner(), self.cells.len())?;
        if (observer_index + 1) as u64 & 1 == 0 {
            faction_a_per_cell[observer_cell] -= 1;
        } else {
            faction_b_per_cell[observer_cell] -= 1;
        }

        let mut records = Vec::with_capacity(self.cells.len());
        for cell_index in 0..self.cells.len() {
            let entity_count = faction_a_per_cell[cell_index] + faction_b_per_cell[cell_index];
            require(
                entity_count
                    + individual_per_cell[cell_index]
                    + usize::from(cell_index == observer_cell)
                    == self.resident_counts[cell_index],
                "mass aggregation did not conserve cell residents",
            )?;
            if entity_count == 0 {
                continue;
            }
            let geometry = self.geometry[cell_index];
            let center = Position2::new(
                geometry.origin.x() + i32::try_from(geometry.extent / 2).unwrap_or(0),
                geometry.origin.y() + i32::try_from(geometry.extent / 2).unwrap_or(0),
            );
            records.push(MassRecord {
                cell_index: u16::try_from(cell_index)
                    .map_err(|_| HarnessError::new("mass cell index exceeds u16"))?,
                faction_a: u16::try_from(faction_a_per_cell[cell_index])
                    .map_err(|_| HarnessError::new("faction A aggregate exceeds u16"))?,
                faction_b: u16::try_from(faction_b_per_cell[cell_index])
                    .map_err(|_| HarnessError::new("faction B aggregate exceeds u16"))?,
                relative_x: checked_i16(center.x() - observer_position.x(), "mass relative x")?,
                relative_y: checked_i16(center.y() - observer_position.y(), "mass relative y")?,
                entity_count: u16::try_from(entity_count)
                    .map_err(|_| HarnessError::new("mass entity count exceeds u16"))?,
            });
        }
        Ok(records)
    }
}
