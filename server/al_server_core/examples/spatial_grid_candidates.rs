//! Spread-versus-hotspot candidate-count benchmark for the reference grid.
//!
//! This exposes broadphase degeneration; it is not a concurrency, server tick,
//! networking, rendering, or 10,000-player capacity claim.

use al_server_core::microcell::{
    EntityState, FixedTickMicrocell, GridSpec, MicrocellConfig, Position2, RadiusQueryBuffer,
    Velocity2, MAX_ENTITY_CAPACITY,
};
use al_server_core::ownership::{EntityId, Tick};
use std::error::Error;
use std::hint::black_box;
use std::time::Instant;

fn main() -> Result<(), Box<dyn Error>> {
    let entity_count = std::env::args()
        .nth(1)
        .map(|value| value.parse::<usize>())
        .transpose()?
        .unwrap_or(5_000);
    let iterations = std::env::args()
        .nth(2)
        .map(|value| value.parse::<usize>())
        .transpose()?
        .unwrap_or(1_000);
    if entity_count == 0 || iterations == 0 {
        return Err("entity count and iterations must both be nonzero".into());
    }
    if entity_count > MAX_ENTITY_CAPACITY {
        return Err("entity count exceeds the reference microcell ceiling".into());
    }

    let side = ceil_sqrt(entity_count);
    let grid = GridSpec::new(Position2::new(0, 0), 100, side + 2, side + 2)?;
    let config = MicrocellConfig::new(entity_count, 0, grid)?;
    let mut spread = FixedTickMicrocell::new(config, Tick::new(0));
    let mut hotspot = FixedTickMicrocell::new(config, Tick::new(0));

    for index in 0..entity_count {
        let id = EntityId::new(u64::try_from(index)? + 1).ok_or("entity ID overflow")?;
        let x_cell = u32::try_from(index)? % side;
        let y_cell = u32::try_from(index)? / side;
        let spread_position = Position2::new(
            i32::try_from(x_cell * 100 + 50)?,
            i32::try_from(y_cell * 100 + 50)?,
        );
        spread.spawn(EntityState::new(id, spread_position, Velocity2::default()))?;
        hotspot.spawn(EntityState::new(
            id,
            Position2::new(50, 50),
            Velocity2::default(),
        ))?;
    }

    let spread_center_coordinate = i32::try_from((side / 2) * 100 + 50)?;
    run_case(
        "spread",
        &mut spread,
        Position2::new(spread_center_coordinate, spread_center_coordinate),
        150,
        entity_count,
        iterations,
    )?;
    run_case(
        "single_hotspot",
        &mut hotspot,
        Position2::new(50, 50),
        150,
        entity_count,
        iterations,
    )?;
    Ok(())
}

fn ceil_sqrt(value: usize) -> u32 {
    let mut side = 1_u32;
    while u64::from(side) * u64::from(side) < u64::try_from(value).unwrap_or(u64::MAX) {
        side += 1;
    }
    side
}

fn run_case(
    label: &str,
    cell: &mut FixedTickMicrocell,
    center: Position2,
    radius: u32,
    result_capacity: usize,
    iterations: usize,
) -> Result<(), Box<dyn Error>> {
    let mut output = RadiusQueryBuffer::new(result_capacity)?;
    let mut candidates = 0_u128;
    let mut matches = 0_u128;
    let started = Instant::now();
    for _ in 0..iterations {
        let stats = cell.query_radius(black_box(center), radius, &mut output)?;
        candidates += stats.candidates_examined() as u128;
        matches += stats.matches() as u128;
        black_box(output.ids());
    }
    let elapsed = started.elapsed();
    println!(
        "reference_only=true distribution={label} entities={result_capacity} queries={iterations} candidates_per_query={} matches_per_query={} elapsed_ms={}",
        candidates / iterations as u128,
        matches / iterations as u128,
        elapsed.as_millis(),
    );
    Ok(())
}
