//! Deterministic local degeneration harness for the fixed-tick microcell.
//!
//! Standard output is JSON Lines so results can be archived and compared. The
//! workload is deterministic; wall-clock measurements are inherently specific
//! to the machine, build, power state, and competing processes. This executable
//! does not exercise networking, combat, persistence, parallelism, rendering,
//! or a complete game-server process and therefore cannot establish player or
//! battle capacity.

use al_server_core::microcell::{
    EntityState, FixedTickMicrocell, GridSpec, IntentSourceId, MicrocellConfig, Position2,
    RadiusQueryBuffer, RadiusQueryError, Velocity2, VelocityIntent, MAX_ENTITY_CAPACITY,
};
use al_server_core::ownership::{EntityId, Tick};
use std::error::Error;
use std::fmt;
use std::hint::black_box;
use std::time::{Duration, Instant};

const SCHEMA_VERSION: u32 = 1;
const CELL_SIZE: u32 = 64;
const GRID_SIDE: u32 = 256;
const DEFAULT_ENTITY_COUNT: usize = 10_000;
const DEFAULT_WARMUP_TICKS: usize = 30;
const DEFAULT_MEASURED_TICKS: usize = 120;
const DEFAULT_QUERIES_PER_TICK: usize = 8;
const DEFAULT_QUERY_RADIUS: u32 = 128;
const DEFAULT_RESULT_CAPACITY: usize = 256;
const DEFAULT_SEED: u64 = 0x41_4c_5f_43_41_50_5f_31;
const MAX_TOTAL_TICKS: usize = 1_000_000;
const MAX_TOTAL_QUERIES: usize = 10_000_000;

fn main() -> Result<(), Box<dyn Error>> {
    let config = match HarnessConfig::from_args(std::env::args().skip(1)) {
        Ok(config) => config,
        Err(HarnessError::HelpRequested) => {
            println!("{}", HarnessError::HelpRequested);
            return Ok(());
        }
        Err(error) => return Err(error.into()),
    };
    println!("{}", config.metadata_json());
    for distribution in Distribution::ALL {
        let result = run_scenario(config, distribution)?;
        println!("{}", result.to_json(config));
    }
    Ok(())
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct HarnessConfig {
    entity_count: usize,
    warmup_ticks: usize,
    measured_ticks: usize,
    queries_per_tick: usize,
    query_radius: u32,
    result_capacity: usize,
    seed: u64,
}

impl Default for HarnessConfig {
    fn default() -> Self {
        Self {
            entity_count: DEFAULT_ENTITY_COUNT,
            warmup_ticks: DEFAULT_WARMUP_TICKS,
            measured_ticks: DEFAULT_MEASURED_TICKS,
            queries_per_tick: DEFAULT_QUERIES_PER_TICK,
            query_radius: DEFAULT_QUERY_RADIUS,
            result_capacity: DEFAULT_RESULT_CAPACITY,
            seed: DEFAULT_SEED,
        }
    }
}

impl HarnessConfig {
    fn from_args(arguments: impl Iterator<Item = String>) -> Result<Self, HarnessError> {
        let mut config = Self::default();
        let mut arguments = arguments.peekable();
        while let Some(argument) = arguments.next() {
            if argument == "--help" || argument == "-h" {
                return Err(HarnessError::HelpRequested);
            }
            let value = arguments
                .next()
                .ok_or_else(|| HarnessError::MissingValue(argument.clone()))?;
            match argument.as_str() {
                "--entities" => config.entity_count = parse_value(&argument, &value)?,
                "--warmup-ticks" => config.warmup_ticks = parse_value(&argument, &value)?,
                "--measured-ticks" => config.measured_ticks = parse_value(&argument, &value)?,
                "--queries-per-tick" => config.queries_per_tick = parse_value(&argument, &value)?,
                "--query-radius" => config.query_radius = parse_value(&argument, &value)?,
                "--result-capacity" => config.result_capacity = parse_value(&argument, &value)?,
                "--seed" => config.seed = parse_u64(&argument, &value)?,
                _ => return Err(HarnessError::UnknownArgument(argument)),
            }
        }
        config.validate()?;
        Ok(config)
    }

    fn validate(self) -> Result<(), HarnessError> {
        if self.entity_count == 0 {
            return Err(HarnessError::InvalidConfig("entities must be nonzero"));
        }
        if self.entity_count > MAX_ENTITY_CAPACITY {
            return Err(HarnessError::InvalidConfig(
                "entities exceed the reference microcell ceiling",
            ));
        }
        if self.measured_ticks == 0 {
            return Err(HarnessError::InvalidConfig(
                "measured ticks must be nonzero",
            ));
        }
        if self.queries_per_tick == 0 {
            return Err(HarnessError::InvalidConfig(
                "queries per tick must be nonzero",
            ));
        }
        if self.result_capacity > MAX_ENTITY_CAPACITY {
            return Err(HarnessError::InvalidConfig(
                "result capacity exceeds the reference microcell ceiling",
            ));
        }
        let tick_count = self
            .warmup_ticks
            .checked_add(self.measured_ticks)
            .ok_or(HarnessError::InvalidConfig("tick count overflow"))?;
        if tick_count > MAX_TOTAL_TICKS {
            return Err(HarnessError::InvalidConfig(
                "total ticks exceed the harness safety ceiling",
            ));
        }
        u64::try_from(tick_count)
            .map_err(|_| HarnessError::InvalidConfig("tick count exceeds u64"))?;
        let total_queries = tick_count
            .checked_mul(self.queries_per_tick)
            .ok_or(HarnessError::InvalidConfig("query count overflow"))?;
        if total_queries > MAX_TOTAL_QUERIES {
            return Err(HarnessError::InvalidConfig(
                "total queries exceed the harness safety ceiling",
            ));
        }
        Ok(())
    }

    fn metadata_json(self) -> String {
        format!(
            concat!(
                "{{\"record\":\"metadata\",\"schema_version\":{},",
                "\"reference_only\":true,\"target_arch\":\"{}\",",
                "\"target_os\":\"{}\",\"debug_assertions\":{},",
                "\"seed\":{},\"entities\":{},\"warmup_ticks\":{},",
                "\"measured_ticks\":{},\"queries_per_tick\":{},",
                "\"query_radius\":{},\"result_capacity\":{},",
                "\"cell_size\":{},\"grid_width\":{},\"grid_height\":{},",
                "\"intents_per_tick\":{},",
                "\"intent_input_order\":\"deterministic_shuffle\"}}"
            ),
            SCHEMA_VERSION,
            std::env::consts::ARCH,
            std::env::consts::OS,
            cfg!(debug_assertions),
            self.seed,
            self.entity_count,
            self.warmup_ticks,
            self.measured_ticks,
            self.queries_per_tick,
            self.query_radius,
            self.result_capacity,
            CELL_SIZE,
            GRID_SIDE,
            GRID_SIDE,
            self.entity_count,
        )
    }
}

fn parse_value<T>(argument: &str, value: &str) -> Result<T, HarnessError>
where
    T: std::str::FromStr,
{
    value.parse().map_err(|_| HarnessError::InvalidValue {
        argument: argument.to_owned(),
        value: value.to_owned(),
    })
}

fn parse_u64(argument: &str, value: &str) -> Result<u64, HarnessError> {
    if let Some(hexadecimal) = value
        .strip_prefix("0x")
        .or_else(|| value.strip_prefix("0X"))
    {
        u64::from_str_radix(hexadecimal, 16).map_err(|_| HarnessError::InvalidValue {
            argument: argument.to_owned(),
            value: value.to_owned(),
        })
    } else {
        parse_value(argument, value)
    }
}

#[derive(Debug, Eq, PartialEq)]
enum HarnessError {
    HelpRequested,
    MissingValue(String),
    UnknownArgument(String),
    InvalidValue { argument: String, value: String },
    InvalidConfig(&'static str),
    Invariant(String),
}

impl fmt::Display for HarnessError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::HelpRequested => write!(
                formatter,
                concat!(
                    "usage: authoritative_capacity_harness ",
                    "[--entities N] [--warmup-ticks N] [--measured-ticks N] ",
                    "[--queries-per-tick N] [--query-radius N] ",
                    "[--result-capacity N] [--seed N_OR_0xHEX]"
                )
            ),
            Self::MissingValue(argument) => write!(formatter, "missing value for {argument}"),
            Self::UnknownArgument(argument) => write!(formatter, "unknown argument {argument}"),
            Self::InvalidValue { argument, value } => {
                write!(formatter, "invalid value {value:?} for {argument}")
            }
            Self::InvalidConfig(message) => write!(formatter, "invalid configuration: {message}"),
            Self::Invariant(message) => write!(formatter, "harness invariant failed: {message}"),
        }
    }
}

impl Error for HarnessError {}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum Distribution {
    Spread,
    BoundaryDense,
    SingleHotspot,
}

impl Distribution {
    const ALL: [Self; 3] = [Self::Spread, Self::BoundaryDense, Self::SingleHotspot];

    const fn label(self) -> &'static str {
        match self {
            Self::Spread => "spread",
            Self::BoundaryDense => "boundary_dense",
            Self::SingleHotspot => "single_hotspot",
        }
    }

    const fn seed_tag(self) -> u64 {
        match self {
            Self::Spread => 0x53_50_52_45_41_44,
            Self::BoundaryDense => 0x42_4f_55_4e_44_41_52_59,
            Self::SingleHotspot => 0x48_4f_54_53_50_4f_54,
        }
    }
}

#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
struct QueryObservation {
    candidates: usize,
    matches: usize,
    overflow_required: Option<usize>,
}

#[derive(Debug, Default)]
struct QueryMetrics {
    durations_ns: Vec<u128>,
    candidates_total: u128,
    candidates_max: usize,
    matches_total: u128,
    matches_max: usize,
    overflow_queries: usize,
    overflow_required_max: usize,
}

impl QueryMetrics {
    fn record(&mut self, elapsed: Duration, observation: QueryObservation) {
        self.durations_ns.push(elapsed.as_nanos());
        self.candidates_total += observation.candidates as u128;
        self.candidates_max = self.candidates_max.max(observation.candidates);
        self.matches_total += observation.matches as u128;
        self.matches_max = self.matches_max.max(observation.matches);
        if let Some(required) = observation.overflow_required {
            self.overflow_queries += 1;
            self.overflow_required_max = self.overflow_required_max.max(required);
        }
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
    fn from_samples(samples: &[u128]) -> Self {
        assert!(!samples.is_empty(), "validated harness has samples");
        let mut sorted = samples.to_vec();
        sorted.sort_unstable();
        let total_ns = sorted.iter().sum();
        let count = sorted.len();
        Self {
            samples: count,
            total_ns,
            mean_ns: total_ns / count as u128,
            p50_ns: percentile(&sorted, 50),
            p95_ns: percentile(&sorted, 95),
            p99_ns: percentile(&sorted, 99),
            max_ns: sorted[count - 1],
        }
    }
}

fn percentile(sorted: &[u128], percentile: usize) -> u128 {
    let rank = sorted.len().saturating_mul(percentile).saturating_add(99) / 100;
    sorted[rank.saturating_sub(1).min(sorted.len() - 1)]
}

#[derive(Debug)]
struct ScenarioResult {
    distribution: Distribution,
    state_timing: TimingSummary,
    query_timing: TimingSummary,
    candidates_total: u128,
    candidates_max: usize,
    matches_total: u128,
    matches_max: usize,
    overflow_queries: usize,
    overflow_required_max: usize,
    oracle_checks: usize,
    workload_checksum: u64,
}

impl ScenarioResult {
    fn to_json(&self, config: HarnessConfig) -> String {
        let query_count = self.query_timing.samples;
        format!(
            concat!(
                "{{\"record\":\"scenario\",\"schema_version\":{},",
                "\"reference_only\":true,\"distribution\":\"{}\",",
                "\"entities\":{},\"measured_ticks\":{},",
                "\"intents_per_tick\":{},\"query_count\":{},",
                "\"state_update_total_ns\":{},\"state_update_mean_ns\":{},",
                "\"state_update_p50_ns\":{},\"state_update_p95_ns\":{},",
                "\"state_update_p99_ns\":{},\"state_update_max_ns\":{},",
                "\"query_total_ns\":{},\"query_mean_ns\":{},",
                "\"query_p50_ns\":{},\"query_p95_ns\":{},",
                "\"query_p99_ns\":{},\"query_max_ns\":{},",
                "\"candidates_total\":{},\"candidates_mean\":{:.3},",
                "\"candidates_max\":{},\"matches_total\":{},",
                "\"matches_mean\":{:.3},\"matches_max\":{},",
                "\"result_capacity\":{},\"overflow_queries\":{},",
                "\"overflow_required_max\":{},\"overflow_output_cleared\":true,",
                "\"oracle_checks\":{},\"workload_checksum\":\"{:016x}\"}}"
            ),
            SCHEMA_VERSION,
            self.distribution.label(),
            config.entity_count,
            config.measured_ticks,
            config.entity_count,
            query_count,
            self.state_timing.total_ns,
            self.state_timing.mean_ns,
            self.state_timing.p50_ns,
            self.state_timing.p95_ns,
            self.state_timing.p99_ns,
            self.state_timing.max_ns,
            self.query_timing.total_ns,
            self.query_timing.mean_ns,
            self.query_timing.p50_ns,
            self.query_timing.p95_ns,
            self.query_timing.p99_ns,
            self.query_timing.max_ns,
            self.candidates_total,
            mean(self.candidates_total, query_count),
            self.candidates_max,
            self.matches_total,
            mean(self.matches_total, query_count),
            self.matches_max,
            config.result_capacity,
            self.overflow_queries,
            self.overflow_required_max,
            self.oracle_checks,
            self.workload_checksum,
        )
    }
}

fn mean(total: u128, count: usize) -> f64 {
    total as f64 / count as f64
}

struct Workload {
    cell: FixedTickMicrocell,
    forward_intents: Vec<VelocityIntent>,
    reverse_intents: Vec<VelocityIntent>,
    query_centers: Vec<Position2>,
}

fn run_scenario(
    config: HarnessConfig,
    distribution: Distribution,
) -> Result<ScenarioResult, HarnessError> {
    config.validate()?;
    let mut workload = build_workload(config, distribution)?;
    let mut bounded_output = RadiusQueryBuffer::new(config.result_capacity)
        .map_err(|error| HarnessError::Invariant(error.to_string()))?;
    let mut oracle_checks = 0_usize;
    verify_query(
        &mut workload.cell,
        workload.query_centers[0],
        config.query_radius,
        &mut bounded_output,
    )?;
    oracle_checks += 1;

    for _ in 0..config.warmup_ticks {
        step_once(&mut workload)?;
        for query_index in 0..config.queries_per_tick {
            let center = workload.query_centers[query_index % workload.query_centers.len()];
            observe_query(
                &mut workload.cell,
                center,
                config.query_radius,
                &mut bounded_output,
            )?;
        }
    }

    let mut state_durations_ns = Vec::with_capacity(config.measured_ticks);
    let query_count = config
        .measured_ticks
        .checked_mul(config.queries_per_tick)
        .ok_or(HarnessError::InvalidConfig("query count overflow"))?;
    let mut query_metrics = QueryMetrics {
        durations_ns: Vec::with_capacity(query_count),
        ..QueryMetrics::default()
    };
    let mut checksum = Checksum::new(config.seed ^ distribution.seed_tag());

    for measured_tick in 0..config.measured_ticks {
        let started = Instant::now();
        let report = step_once(&mut workload)?;
        let elapsed = started.elapsed();
        state_durations_ns.push(elapsed.as_nanos());
        if report.entity_count() != config.entity_count
            || report.intents_received() != config.entity_count
            || report.intents_applied() != config.entity_count
        {
            return Err(HarnessError::Invariant(
                "step did not retain and apply exactly one intent per entity".to_owned(),
            ));
        }
        checksum.mix(report.tick().get());

        for query_offset in 0..config.queries_per_tick {
            let query_index = measured_tick
                .wrapping_mul(config.queries_per_tick)
                .wrapping_add(query_offset)
                % workload.query_centers.len();
            let center = workload.query_centers[query_index];
            let started = Instant::now();
            let observation = observe_query(
                &mut workload.cell,
                black_box(center),
                config.query_radius,
                &mut bounded_output,
            )?;
            let elapsed = started.elapsed();
            checksum.mix(observation.candidates as u64);
            checksum.mix(observation.matches as u64);
            checksum.mix(observation.overflow_required.unwrap_or(0) as u64);
            for id in bounded_output.ids() {
                checksum.mix(id.get());
            }
            black_box(bounded_output.ids());
            query_metrics.record(elapsed, observation);
        }
    }

    let final_center = workload.query_centers[workload.query_centers.len() - 1];
    verify_query(
        &mut workload.cell,
        final_center,
        config.query_radius,
        &mut bounded_output,
    )?;
    oracle_checks += 1;
    for entity in workload.cell.entities() {
        checksum.mix(entity.id().get());
        checksum.mix(entity.position().x() as u32 as u64);
        checksum.mix(entity.position().y() as u32 as u64);
        checksum.mix(entity.velocity().x() as u32 as u64);
        checksum.mix(entity.velocity().y() as u32 as u64);
    }

    Ok(ScenarioResult {
        distribution,
        state_timing: TimingSummary::from_samples(&state_durations_ns),
        query_timing: TimingSummary::from_samples(&query_metrics.durations_ns),
        candidates_total: query_metrics.candidates_total,
        candidates_max: query_metrics.candidates_max,
        matches_total: query_metrics.matches_total,
        matches_max: query_metrics.matches_max,
        overflow_queries: query_metrics.overflow_queries,
        overflow_required_max: query_metrics.overflow_required_max,
        oracle_checks,
        workload_checksum: checksum.finish(),
    })
}

fn build_workload(
    config: HarnessConfig,
    distribution: Distribution,
) -> Result<Workload, HarnessError> {
    let grid = GridSpec::new(Position2::new(0, 0), CELL_SIZE, GRID_SIDE, GRID_SIDE)
        .map_err(|error| HarnessError::Invariant(error.to_string()))?;
    let microcell_config = MicrocellConfig::new(config.entity_count, config.entity_count, grid)
        .map_err(|error| HarnessError::Invariant(error.to_string()))?;
    let mut cell = FixedTickMicrocell::new(microcell_config, Tick::new(0));
    let source = IntentSourceId::new(1).expect("constant source ID is nonzero");
    let mut forward_intents = Vec::with_capacity(config.entity_count);
    let mut reverse_intents = Vec::with_capacity(config.entity_count);
    let scenario_seed = config.seed ^ distribution.seed_tag();

    for index in 0..config.entity_count {
        let id_value = u64::try_from(index)
            .map_err(|_| HarnessError::Invariant("entity index exceeds u64".to_owned()))?
            .checked_add(1)
            .ok_or_else(|| HarnessError::Invariant("entity ID overflow".to_owned()))?;
        let id = EntityId::new(id_value)
            .ok_or_else(|| HarnessError::Invariant("entity ID became zero".to_owned()))?;
        let random = splitmix64(scenario_seed.wrapping_add(id_value));
        let (position, forward_velocity) = placement(distribution, index, random);
        cell.spawn(EntityState::new(id, position, Velocity2::default()))
            .map_err(|error| HarnessError::Invariant(error.to_string()))?;
        forward_intents.push(VelocityIntent::new(
            id,
            1,
            source,
            id_value,
            forward_velocity,
        ));
        reverse_intents.push(VelocityIntent::new(
            id,
            1,
            source,
            id_value,
            Velocity2::new(-forward_velocity.x(), -forward_velocity.y()),
        ));
    }
    shuffle_intents(
        &mut forward_intents,
        &mut reverse_intents,
        scenario_seed ^ 0x49_4e_54_45_4e_54_53,
    );

    let query_centers = (0..config.queries_per_tick.max(16))
        .map(|index| query_center(distribution, scenario_seed, index))
        .collect();
    Ok(Workload {
        cell,
        forward_intents,
        reverse_intents,
        query_centers,
    })
}

fn placement(distribution: Distribution, index: usize, random: u64) -> (Position2, Velocity2) {
    match distribution {
        Distribution::Spread => {
            const MARGIN_CELLS: u32 = 4;
            let usable_side = GRID_SIDE - MARGIN_CELLS * 2;
            let x_cell = MARGIN_CELLS + (random as u32 % usable_side);
            let y_cell = MARGIN_CELLS + (random.rotate_left(31) as u32 % usable_side);
            let x = coordinate(x_cell, 16 + (random.rotate_left(7) as u32 % 32));
            let y = coordinate(y_cell, 16 + (random.rotate_left(43) as u32 % 32));
            let velocity = match random.rotate_left(17) & 3 {
                0 => Velocity2::new(1, 0),
                1 => Velocity2::new(-1, 0),
                2 => Velocity2::new(0, 1),
                _ => Velocity2::new(0, -1),
            };
            (Position2::new(x, y), velocity)
        }
        Distribution::BoundaryDense => {
            const CLUSTER_SIDE: usize = 16;
            const CLUSTER_ORIGIN: u32 = GRID_SIDE / 2 - CLUSTER_SIDE as u32 / 2;
            let node = index % (CLUSTER_SIDE * CLUSTER_SIDE);
            let x_boundary = CLUSTER_ORIGIN + u32::try_from(node % CLUSTER_SIDE).unwrap_or(0);
            let y_boundary = CLUSTER_ORIGIN + u32::try_from(node / CLUSTER_SIDE).unwrap_or(0);
            let x_edge = coordinate(x_boundary, 0);
            let y_edge = coordinate(y_boundary, 0);
            let offset = i32::try_from(random % 9).unwrap_or(0) - 4;
            match (index / (CLUSTER_SIDE * CLUSTER_SIDE)) & 3 {
                0 => (
                    Position2::new(x_edge - 1, y_edge + offset),
                    Velocity2::new(1, 0),
                ),
                1 => (
                    Position2::new(x_edge, y_edge + offset),
                    Velocity2::new(-1, 0),
                ),
                2 => (
                    Position2::new(x_edge + offset, y_edge - 1),
                    Velocity2::new(0, 1),
                ),
                _ => (
                    Position2::new(x_edge + offset, y_edge),
                    Velocity2::new(0, -1),
                ),
            }
        }
        Distribution::SingleHotspot => {
            let hotspot_cell = GRID_SIDE / 2;
            let x = coordinate(hotspot_cell, 24 + (random as u32 % 8));
            let y = coordinate(hotspot_cell, 24 + (random.rotate_left(29) as u32 % 8));
            let velocity = match index & 3 {
                0 => Velocity2::new(1, 0),
                1 => Velocity2::new(-1, 0),
                2 => Velocity2::new(0, 1),
                _ => Velocity2::new(0, -1),
            };
            (Position2::new(x, y), velocity)
        }
    }
}

fn query_center(distribution: Distribution, seed: u64, index: usize) -> Position2 {
    let random = splitmix64(
        seed.wrapping_add(index as u64)
            .wrapping_add(0x51_55_45_52_59),
    );
    match distribution {
        Distribution::Spread => {
            const MARGIN_CELLS: u32 = 4;
            let usable_side = GRID_SIDE - MARGIN_CELLS * 2;
            Position2::new(
                coordinate(MARGIN_CELLS + random as u32 % usable_side, CELL_SIZE / 2),
                coordinate(
                    MARGIN_CELLS + random.rotate_left(31) as u32 % usable_side,
                    CELL_SIZE / 2,
                ),
            )
        }
        Distribution::BoundaryDense => {
            const CLUSTER_SIDE: u32 = 16;
            const CLUSTER_ORIGIN: u32 = GRID_SIDE / 2 - CLUSTER_SIDE / 2;
            Position2::new(
                coordinate(CLUSTER_ORIGIN + random as u32 % CLUSTER_SIDE, 0),
                coordinate(
                    CLUSTER_ORIGIN + random.rotate_left(31) as u32 % CLUSTER_SIDE,
                    0,
                ),
            )
        }
        Distribution::SingleHotspot => Position2::new(
            coordinate(GRID_SIDE / 2, CELL_SIZE / 2),
            coordinate(GRID_SIDE / 2, CELL_SIZE / 2),
        ),
    }
}

fn coordinate(cell: u32, offset: u32) -> i32 {
    i32::try_from(cell * CELL_SIZE + offset).expect("fixed grid coordinates fit i32")
}

fn splitmix64(mut value: u64) -> u64 {
    value = value.wrapping_add(0x9e37_79b9_7f4a_7c15);
    value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
    value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
    value ^ (value >> 31)
}

fn shuffle_intents(forward: &mut [VelocityIntent], reverse: &mut [VelocityIntent], mut state: u64) {
    debug_assert_eq!(forward.len(), reverse.len());
    for upper in (1..forward.len()).rev() {
        state = splitmix64(state);
        let destination = usize::try_from(state % (upper as u64 + 1)).unwrap_or(0);
        forward.swap(upper, destination);
        reverse.swap(upper, destination);
    }
}

fn step_once(
    workload: &mut Workload,
) -> Result<al_server_core::microcell::StepReport, HarnessError> {
    let target = workload
        .cell
        .tick()
        .checked_add(1)
        .ok_or_else(|| HarnessError::Invariant("tick overflow".to_owned()))?;
    let intents = if target.get() & 1 == 1 {
        &workload.forward_intents
    } else {
        &workload.reverse_intents
    };
    workload
        .cell
        .step(target, black_box(intents))
        .map_err(|error| HarnessError::Invariant(error.to_string()))
}

fn observe_query(
    cell: &mut FixedTickMicrocell,
    center: Position2,
    radius: u32,
    output: &mut RadiusQueryBuffer,
) -> Result<QueryObservation, HarnessError> {
    match cell.query_radius(center, radius, output) {
        Ok(stats) => Ok(QueryObservation {
            candidates: stats.candidates_examined(),
            matches: stats.matches(),
            overflow_required: None,
        }),
        Err(RadiusQueryError::ResultCapacityExceeded {
            required,
            capacity,
            candidates_examined,
        }) => {
            if capacity != output.capacity() || !output.ids().is_empty() {
                return Err(HarnessError::Invariant(
                    "overflow exposed partial output or wrong capacity".to_owned(),
                ));
            }
            Ok(QueryObservation {
                candidates: candidates_examined,
                matches: required,
                overflow_required: Some(required),
            })
        }
        Err(error) => Err(HarnessError::Invariant(error.to_string())),
    }
}

fn verify_query(
    cell: &mut FixedTickMicrocell,
    center: Position2,
    radius: u32,
    bounded_output: &mut RadiusQueryBuffer,
) -> Result<(), HarnessError> {
    let radius_squared = i128::from(radius) * i128::from(radius);
    let expected: Vec<EntityId> = cell
        .entities()
        .filter(|entity| {
            let dx = i128::from(entity.position().x()) - i128::from(center.x());
            let dy = i128::from(entity.position().y()) - i128::from(center.y());
            dx * dx + dy * dy <= radius_squared
        })
        .map(EntityState::id)
        .collect();
    let mut complete_output = RadiusQueryBuffer::new(cell.entity_count())
        .map_err(|error| HarnessError::Invariant(error.to_string()))?;
    let complete = cell
        .query_radius(center, radius, &mut complete_output)
        .map_err(|error| HarnessError::Invariant(error.to_string()))?;
    if complete_output.ids() != expected || complete.matches() != expected.len() {
        return Err(HarnessError::Invariant(
            "grid query disagreed with brute-force oracle".to_owned(),
        ));
    }

    let bounded = observe_query(cell, center, radius, bounded_output)?;
    if bounded.matches != expected.len() {
        return Err(HarnessError::Invariant(
            "bounded query reported the wrong required result count".to_owned(),
        ));
    }
    if expected.len() <= bounded_output.capacity() && bounded_output.ids() != expected {
        return Err(HarnessError::Invariant(
            "bounded successful query disagreed with oracle".to_owned(),
        ));
    }
    if expected.len() > bounded_output.capacity()
        && bounded.overflow_required != Some(expected.len())
    {
        return Err(HarnessError::Invariant(
            "bounded overflow did not report exact required capacity".to_owned(),
        ));
    }
    Ok(())
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

    fn small_config() -> HarnessConfig {
        HarnessConfig {
            entity_count: 512,
            warmup_ticks: 2,
            measured_ticks: 4,
            queries_per_tick: 3,
            query_radius: 64,
            result_capacity: 32,
            seed: 0x1234_5678_9abc_def0,
        }
    }

    #[test]
    fn workload_counts_and_checksums_are_deterministic() {
        for distribution in Distribution::ALL {
            let left = run_scenario(small_config(), distribution).expect("left run succeeds");
            let right = run_scenario(small_config(), distribution).expect("right run succeeds");
            assert_eq!(left.workload_checksum, right.workload_checksum);
            assert_eq!(left.candidates_total, right.candidates_total);
            assert_eq!(left.matches_total, right.matches_total);
            assert_eq!(left.overflow_queries, right.overflow_queries);
            assert_eq!(left.state_timing.samples, 4);
            assert_eq!(left.query_timing.samples, 12);
            assert_eq!(left.oracle_checks, 2);
        }
    }

    #[test]
    fn single_hotspot_overflow_is_exact_and_bounded() {
        let config = small_config();
        let result = run_scenario(config, Distribution::SingleHotspot).expect("run succeeds");
        assert_eq!(result.candidates_total, 512 * 12);
        assert_eq!(result.matches_total, 512 * 12);
        assert_eq!(result.candidates_max, 512);
        assert_eq!(result.matches_max, 512);
        assert_eq!(result.overflow_queries, 12);
        assert_eq!(result.overflow_required_max, 512);
    }

    #[test]
    fn argument_parser_accepts_hex_seed_and_rejects_unbounded_inputs() {
        let parsed = HarnessConfig::from_args(
            ["--entities", "42", "--seed", "0x10"]
                .into_iter()
                .map(str::to_owned),
        )
        .expect("arguments parse");
        assert_eq!(parsed.entity_count, 42);
        assert_eq!(parsed.seed, 16);
        assert!(matches!(
            HarnessConfig::from_args(["--entities", "0"].into_iter().map(str::to_owned)),
            Err(HarnessError::InvalidConfig(_))
        ));
        assert!(matches!(
            HarnessConfig::from_args(["--entities", "1000001"].into_iter().map(str::to_owned)),
            Err(HarnessError::InvalidConfig(_))
        ));
        assert!(matches!(
            HarnessConfig::from_args(
                ["--measured-ticks", "1000001"]
                    .into_iter()
                    .map(str::to_owned)
            ),
            Err(HarnessError::InvalidConfig(_))
        ));
        assert!(matches!(
            HarnessConfig::from_args(
                ["--measured-ticks", "1000000", "--queries-per-tick", "11"]
                    .into_iter()
                    .map(str::to_owned)
            ),
            Err(HarnessError::InvalidConfig(_))
        ));
    }

    #[test]
    fn output_is_one_machine_readable_record_per_line() {
        let config = small_config();
        let result = run_scenario(config, Distribution::Spread).expect("run succeeds");
        let metadata = config.metadata_json();
        let scenario = result.to_json(config);
        assert!(metadata.starts_with("{\"record\":\"metadata\""));
        assert!(metadata.ends_with('}'));
        assert!(!metadata.contains('\n'));
        assert!(scenario.starts_with("{\"record\":\"scenario\""));
        assert!(scenario.contains("\"distribution\":\"spread\""));
        assert!(scenario.contains("\"overflow_output_cleared\":true"));
        assert!(scenario.ends_with('}'));
        assert!(!scenario.contains('\n'));
    }
}
