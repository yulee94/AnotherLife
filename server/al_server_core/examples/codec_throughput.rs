//! Local smoke benchmark for the bounded inter-cell frame codec.
//!
//! This is not a production capacity claim. Use a dedicated benchmark harness,
//! representative payload distributions, and target hardware before selecting a
//! transport or setting service-level objectives.

use al_server_core::ownership::{CellId, Tick};
use al_server_core::wire::{
    decode_v1, encode_v1, FrameFlags, FrameHeader, FrameLimits, FrameWindow, InstanceId,
    MessageKind, ReceiveContext, RouteEpoch, RouteScope, WorldId,
};
use std::hint::black_box;
use std::time::Instant;

fn main() {
    let iterations = std::env::args()
        .nth(1)
        .and_then(|value| value.parse::<u64>().ok())
        .unwrap_or(1_000_000);
    let payload = [0x5a_u8; 256];
    let route = RouteScope::new(
        WorldId::new(1).expect("constant is nonzero"),
        InstanceId::new(1).expect("constant is nonzero"),
        CellId::new(1).expect("constant is nonzero"),
        CellId::new(2).expect("constant is nonzero"),
        RouteEpoch::new(1).expect("constant is nonzero"),
    );
    let window = FrameWindow::new(Tick::new(42), Tick::new(50))
        .expect("constant deadline follows source tick");
    let header = FrameHeader::new(
        MessageKind::GhostSnapshot,
        FrameFlags::IDEMPOTENT,
        route,
        window,
        7,
    );
    let receive_context = ReceiveContext::new(route, Tick::new(45));
    let limits = FrameLimits::default();
    let mut encoded = [0_u8; 512];

    let started = Instant::now();
    let mut checksum = 0_u64;
    for _ in 0..iterations {
        let length = encode_v1(header, black_box(&payload), &mut encoded, limits)
            .expect("fixed benchmark frame encodes");
        let frame = decode_v1(black_box(&encoded[..length]), limits, receive_context)
            .expect("fixed benchmark frame decodes");
        checksum = checksum.wrapping_add(frame.header().sequence());
        checksum = checksum.wrapping_add(u64::from(frame.payload()[0]));
    }
    let elapsed = started.elapsed();
    let frames_per_second = iterations as f64 / elapsed.as_secs_f64();

    println!(
        "frames={iterations} elapsed_ms={} frames_per_second={frames_per_second:.0} checksum={}",
        elapsed.as_millis(),
        black_box(checksum)
    );
}
