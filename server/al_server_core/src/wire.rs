//! Strict version-one inter-cell wire framing.
//!
//! The format is explicitly little-endian and is decoded field-by-field. It
//! does not rely on Rust structure layout, alignment, pointer casts, or unsafe
//! code. A frame contains exactly one header and one payload; trailing bytes are
//! rejected so callers cannot accidentally accept concatenated or smuggled data.

use crate::ownership::{CellId, Tick};
use std::cmp::Ordering;
use std::fmt;
use std::num::NonZeroU64;

/// Four-byte marker at the start of every inter-cell frame.
pub const FRAME_MAGIC: [u8; 4] = *b"ALIC";
/// Only protocol version currently accepted.
pub const PROTOCOL_VERSION_V1: u16 = 1;
/// Exact byte length of the version-one header.
pub const HEADER_LEN_V1: usize = 80;
/// Hard safety ceiling independent of a caller's tighter runtime budget.
pub const ABSOLUTE_MAX_PAYLOAD_LEN: usize = 1024 * 1024;
/// Default per-frame payload budget for the prototype.
pub const DEFAULT_MAX_PAYLOAD_LEN: usize = 64 * 1024;

const OFFSET_VERSION: usize = 4;
const OFFSET_HEADER_LEN: usize = 6;
const OFFSET_KIND: usize = 8;
const OFFSET_FLAGS: usize = 10;
const OFFSET_PAYLOAD_LEN: usize = 12;
const OFFSET_WORLD_ID: usize = 16;
const OFFSET_INSTANCE_ID: usize = 24;
const OFFSET_SOURCE_CELL: usize = 32;
const OFFSET_DESTINATION_CELL: usize = 40;
const OFFSET_ROUTE_EPOCH: usize = 48;
const OFFSET_SOURCE_TICK: usize = 56;
const OFFSET_DEADLINE_TICK: usize = 64;
const OFFSET_SEQUENCE: usize = 72;

/// Stable nonzero identity of one authored world topology.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct WorldId(NonZeroU64);

impl WorldId {
    /// Creates a world identity; zero is reserved as invalid.
    #[must_use]
    pub const fn new(value: u64) -> Option<Self> {
        match NonZeroU64::new(value) {
            Some(value) => Some(Self(value)),
            None => None,
        }
    }

    /// Returns the numeric world identity.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0.get()
    }
}

/// Stable nonzero identity of one running world instance.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct InstanceId(NonZeroU64);

impl InstanceId {
    /// Creates an instance identity; zero is reserved as invalid.
    #[must_use]
    pub const fn new(value: u64) -> Option<Self> {
        match NonZeroU64::new(value) {
            Some(value) => Some(Self(value)),
            None => None,
        }
    }

    /// Returns the numeric instance identity.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0.get()
    }
}

/// Semantic payload category carried by a frame.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
#[repr(u16)]
pub enum MessageKind {
    /// Read-only border or interest replica state.
    GhostSnapshot = 1,
    /// Prepare, ready, commit, cancel, or recovery handoff control.
    HandoffControl = 2,
    /// Immutable simulation event routed between authoritative cells.
    CellEvent = 3,
    /// Liveness and route-generation observation.
    Heartbeat = 4,
}

/// Monotonic generation for one directed inter-cell route.
///
/// This is deliberately distinct from an entity's ownership epoch. One frame
/// may batch several entities, each of which retains its own payload-level
/// ownership metadata.
#[derive(Clone, Copy, Debug, Eq, Hash, Ord, PartialEq, PartialOrd)]
pub struct RouteEpoch(NonZeroU64);

impl RouteEpoch {
    /// Creates a route generation; zero is reserved as invalid.
    #[must_use]
    pub const fn new(value: u64) -> Option<Self> {
        match NonZeroU64::new(value) {
            Some(value) => Some(Self(value)),
            None => None,
        }
    }

    /// Returns the numeric route generation.
    #[must_use]
    pub const fn get(self) -> u64 {
        self.0.get()
    }
}

/// Complete authenticated scope of one directed inter-cell route.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct RouteScope {
    world_id: WorldId,
    instance_id: InstanceId,
    source_cell: CellId,
    destination_cell: CellId,
    route_epoch: RouteEpoch,
}

impl RouteScope {
    /// Creates an exact route scope from validated nonzero identities.
    #[must_use]
    pub const fn new(
        world_id: WorldId,
        instance_id: InstanceId,
        source_cell: CellId,
        destination_cell: CellId,
        route_epoch: RouteEpoch,
    ) -> Self {
        Self {
            world_id,
            instance_id,
            source_cell,
            destination_cell,
            route_epoch,
        }
    }

    /// Returns the authored world identity.
    #[must_use]
    pub const fn world_id(self) -> WorldId {
        self.world_id
    }

    /// Returns the running world-instance identity.
    #[must_use]
    pub const fn instance_id(self) -> InstanceId {
        self.instance_id
    }

    /// Returns the authenticated source cell.
    #[must_use]
    pub const fn source_cell(self) -> CellId {
        self.source_cell
    }

    /// Returns the intended destination cell.
    #[must_use]
    pub const fn destination_cell(self) -> CellId {
        self.destination_cell
    }

    /// Returns the directed route generation.
    #[must_use]
    pub const fn route_epoch(self) -> RouteEpoch {
        self.route_epoch
    }
}

/// Source tick and inclusive usefulness deadline of one hot frame.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct FrameWindow {
    source_tick: Tick,
    deadline_tick: Tick,
}

impl FrameWindow {
    /// Creates a window when the deadline is not earlier than the source tick.
    #[must_use]
    pub const fn new(source_tick: Tick, deadline_tick: Tick) -> Option<Self> {
        if deadline_tick.get() < source_tick.get() {
            None
        } else {
            Some(Self {
                source_tick,
                deadline_tick,
            })
        }
    }

    /// Returns the frame's source simulation tick.
    #[must_use]
    pub const fn source_tick(self) -> Tick {
        self.source_tick
    }

    /// Returns the inclusive last tick at which the frame may be accepted.
    #[must_use]
    pub const fn deadline_tick(self) -> Tick {
        self.deadline_tick
    }
}

/// Authenticated route and receiver clock required to accept a frame.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct ReceiveContext {
    route: RouteScope,
    current_tick: Tick,
}

impl ReceiveContext {
    /// Creates the exact route expected from the authenticated transport peer.
    #[must_use]
    pub const fn new(route: RouteScope, current_tick: Tick) -> Self {
        Self {
            route,
            current_tick,
        }
    }

    /// Returns the only route this decode operation will accept.
    #[must_use]
    pub const fn route(self) -> RouteScope {
        self.route
    }

    /// Returns the authoritative receiver tick used for expiry.
    #[must_use]
    pub const fn current_tick(self) -> Tick {
        self.current_tick
    }
}

impl MessageKind {
    fn from_wire(value: u16) -> Option<Self> {
        match value {
            1 => Some(Self::GhostSnapshot),
            2 => Some(Self::HandoffControl),
            3 => Some(Self::CellEvent),
            4 => Some(Self::Heartbeat),
            _ => None,
        }
    }
}

/// Validated version-one frame flags.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub struct FrameFlags(u16);

impl FrameFlags {
    /// No optional flag is set.
    pub const NONE: Self = Self(0);
    /// Payload is safe for idempotent replay by its domain handler.
    pub const IDEMPOTENT: Self = Self(1 << 0);
    /// Ghost payload is a self-contained keyframe rather than a delta.
    pub const KEYFRAME: Self = Self(1 << 1);
    const KNOWN_MASK: u16 = Self::IDEMPOTENT.0 | Self::KEYFRAME.0;

    /// Combines two validated flag sets.
    #[must_use]
    pub const fn union(self, other: Self) -> Self {
        Self(self.0 | other.0)
    }

    /// Returns the encoded flag bits.
    #[must_use]
    pub const fn bits(self) -> u16 {
        self.0
    }

    fn from_wire(bits: u16) -> Option<Self> {
        if bits & !Self::KNOWN_MASK == 0 {
            Some(Self(bits))
        } else {
            None
        }
    }
}

/// Runtime payload limit applied before payload access or output mutation.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct FrameLimits {
    max_payload_len: usize,
}

impl FrameLimits {
    /// Creates a limit no greater than [`ABSOLUTE_MAX_PAYLOAD_LEN`].
    #[must_use]
    pub const fn new(max_payload_len: usize) -> Option<Self> {
        if max_payload_len <= ABSOLUTE_MAX_PAYLOAD_LEN {
            Some(Self { max_payload_len })
        } else {
            None
        }
    }

    /// Returns the configured inclusive payload byte ceiling.
    #[must_use]
    pub const fn max_payload_len(self) -> usize {
        self.max_payload_len
    }
}

impl Default for FrameLimits {
    fn default() -> Self {
        Self {
            max_payload_len: DEFAULT_MAX_PAYLOAD_LEN,
        }
    }
}

/// Version-one header fields independent of payload storage.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct FrameHeader {
    kind: MessageKind,
    flags: FrameFlags,
    route: RouteScope,
    window: FrameWindow,
    sequence: u64,
}

impl FrameHeader {
    /// Creates a validated header. Payload length is derived by the encoder.
    #[must_use]
    pub const fn new(
        kind: MessageKind,
        flags: FrameFlags,
        route: RouteScope,
        window: FrameWindow,
        sequence: u64,
    ) -> Self {
        Self {
            kind,
            flags,
            route,
            window,
            sequence,
        }
    }

    /// Returns the payload category.
    #[must_use]
    pub const fn kind(self) -> MessageKind {
        self.kind
    }

    /// Returns validated optional flags.
    #[must_use]
    pub const fn flags(self) -> FrameFlags {
        self.flags
    }

    /// Returns the exact world, instance, endpoints, and route generation.
    #[must_use]
    pub const fn route(self) -> RouteScope {
        self.route
    }

    /// Returns the authored world identity.
    #[must_use]
    pub const fn world_id(self) -> WorldId {
        self.route.world_id
    }

    /// Returns the running world-instance identity.
    #[must_use]
    pub const fn instance_id(self) -> InstanceId {
        self.route.instance_id
    }

    /// Returns the authenticated source cell.
    #[must_use]
    pub const fn source_cell(self) -> CellId {
        self.route.source_cell
    }

    /// Returns the intended destination cell.
    #[must_use]
    pub const fn destination_cell(self) -> CellId {
        self.route.destination_cell
    }

    /// Returns the directed route generation.
    #[must_use]
    pub const fn route_epoch(self) -> RouteEpoch {
        self.route.route_epoch
    }

    /// Returns the source tick and inclusive usefulness deadline.
    #[must_use]
    pub const fn window(self) -> FrameWindow {
        self.window
    }

    /// Returns the source simulation tick.
    #[must_use]
    pub const fn source_tick(self) -> Tick {
        self.window.source_tick
    }

    /// Returns the inclusive last useful tick.
    #[must_use]
    pub const fn deadline_tick(self) -> Tick {
        self.window.deadline_tick
    }

    /// Returns the per-route monotonic sequence supplied by the caller.
    #[must_use]
    pub const fn sequence(self) -> u64 {
        self.sequence
    }
}

/// A decoded frame borrowing its payload from the input buffer.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct DecodedFrame<'a> {
    header: FrameHeader,
    payload: &'a [u8],
}

impl<'a> DecodedFrame<'a> {
    /// Returns the validated header.
    #[must_use]
    pub const fn header(self) -> FrameHeader {
        self.header
    }

    /// Returns the zero-copy borrowed payload.
    #[must_use]
    pub const fn payload(self) -> &'a [u8] {
        self.payload
    }
}

/// Failure while encoding a version-one frame.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum EncodeError {
    /// The payload exceeds the selected limit.
    PayloadTooLarge {
        /// Actual payload bytes.
        actual: usize,
        /// Configured maximum bytes.
        max: usize,
    },
    /// The caller-owned output buffer cannot hold the complete frame.
    OutputTooSmall {
        /// Required total frame bytes.
        required: usize,
        /// Available output bytes.
        available: usize,
    },
}

impl fmt::Display for EncodeError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for EncodeError {}

/// Failure while decoding a version-one frame.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum DecodeError {
    /// Fewer than [`HEADER_LEN_V1`] bytes were supplied.
    TruncatedHeader {
        /// Required header bytes.
        required: usize,
        /// Available bytes.
        available: usize,
    },
    /// The frame marker is not [`FRAME_MAGIC`].
    InvalidMagic,
    /// The protocol version has no decoder in this build.
    UnsupportedVersion {
        /// Received version number.
        received: u16,
    },
    /// Version one requires the exact published header length.
    InvalidHeaderLength {
        /// Received header length.
        received: u16,
    },
    /// The message kind is not defined by version one.
    UnknownMessageKind {
        /// Received numeric kind.
        received: u16,
    },
    /// At least one unassigned flag bit was set.
    UnknownFlags {
        /// Received raw flag bits.
        received: u16,
    },
    /// The declared payload exceeds the selected limit.
    PayloadTooLarge {
        /// Declared payload bytes.
        declared: usize,
        /// Configured maximum bytes.
        max: usize,
    },
    /// The world identity uses reserved value zero.
    InvalidWorldId,
    /// The running instance identity uses reserved value zero.
    InvalidInstanceId,
    /// The source cell uses reserved identity zero.
    InvalidSourceCell,
    /// The destination cell uses reserved identity zero.
    InvalidDestinationCell,
    /// The source route uses reserved generation zero.
    InvalidRouteEpoch,
    /// The frame declares an expiry earlier than its source tick.
    DeadlineBeforeSource {
        /// Declared source tick.
        source_tick: Tick,
        /// Earlier declared deadline.
        deadline_tick: Tick,
    },
    /// Authenticated receiver world does not match the frame.
    WorldMismatch {
        /// Authenticated expected world.
        expected: WorldId,
        /// World declared by the frame.
        received: WorldId,
    },
    /// Authenticated receiver instance does not match the frame.
    InstanceMismatch {
        /// Authenticated expected instance.
        expected: InstanceId,
        /// Instance declared by the frame.
        received: InstanceId,
    },
    /// Authenticated source route does not match the frame.
    SourceCellMismatch {
        /// Authenticated expected source cell.
        expected: CellId,
        /// Source cell declared by the frame.
        received: CellId,
    },
    /// Intended destination does not match the receiving route.
    DestinationCellMismatch {
        /// Expected local destination cell.
        expected: CellId,
        /// Destination cell declared by the frame.
        received: CellId,
    },
    /// Frame route generation has already been fenced.
    StaleRouteEpoch {
        /// Current authenticated route generation.
        expected: RouteEpoch,
        /// Older generation declared by the frame.
        received: RouteEpoch,
    },
    /// Frame route generation is ahead of the receiver's authenticated route.
    FutureRouteEpoch {
        /// Current authenticated route generation.
        expected: RouteEpoch,
        /// Newer generation declared by the frame.
        received: RouteEpoch,
    },
    /// Receiver time is later than the frame's inclusive deadline.
    Expired {
        /// Inclusive deadline declared by the frame.
        deadline_tick: Tick,
        /// Authoritative receiver tick.
        current_tick: Tick,
    },
    /// The input ended before the declared payload ended.
    TruncatedPayload {
        /// Declared payload bytes.
        declared: usize,
        /// Payload bytes actually available.
        available: usize,
    },
    /// Bytes remain after the one declared frame.
    TrailingBytes {
        /// Unexpected trailing byte count.
        count: usize,
    },
}

impl fmt::Display for DecodeError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self:?}")
    }
}

impl std::error::Error for DecodeError {}

/// Encodes one exact version-one frame into caller-owned memory.
///
/// The function validates all sizes before touching `output`. On failure,
/// `output` is unchanged.
pub fn encode_v1(
    header: FrameHeader,
    payload: &[u8],
    output: &mut [u8],
    limits: FrameLimits,
) -> Result<usize, EncodeError> {
    if payload.len() > limits.max_payload_len {
        return Err(EncodeError::PayloadTooLarge {
            actual: payload.len(),
            max: limits.max_payload_len,
        });
    }

    let required = HEADER_LEN_V1 + payload.len();
    if output.len() < required {
        return Err(EncodeError::OutputTooSmall {
            required,
            available: output.len(),
        });
    }

    output[..HEADER_LEN_V1].fill(0);
    output[..4].copy_from_slice(&FRAME_MAGIC);
    write_u16(output, OFFSET_VERSION, PROTOCOL_VERSION_V1);
    write_u16(output, OFFSET_HEADER_LEN, HEADER_LEN_V1 as u16);
    write_u16(output, OFFSET_KIND, header.kind as u16);
    write_u16(output, OFFSET_FLAGS, header.flags.bits());
    write_u32(output, OFFSET_PAYLOAD_LEN, payload.len() as u32);
    write_u64(output, OFFSET_WORLD_ID, header.route.world_id.get());
    write_u64(output, OFFSET_INSTANCE_ID, header.route.instance_id.get());
    write_u64(output, OFFSET_SOURCE_CELL, header.route.source_cell.get());
    write_u64(
        output,
        OFFSET_DESTINATION_CELL,
        header.route.destination_cell.get(),
    );
    write_u64(output, OFFSET_ROUTE_EPOCH, header.route.route_epoch.get());
    write_u64(output, OFFSET_SOURCE_TICK, header.window.source_tick.get());
    write_u64(
        output,
        OFFSET_DEADLINE_TICK,
        header.window.deadline_tick.get(),
    );
    write_u64(output, OFFSET_SEQUENCE, header.sequence);
    output[HEADER_LEN_V1..required].copy_from_slice(payload);
    Ok(required)
}

/// Decodes one frame only when its authenticated route and deadline are valid.
///
/// There is intentionally no public unscoped decoder. A caller must obtain the
/// expected directed route from its authenticated peer/directory context and
/// provide authoritative receiver time before payload bytes become accessible.
pub fn decode_v1(
    input: &[u8],
    limits: FrameLimits,
    context: ReceiveContext,
) -> Result<DecodedFrame<'_>, DecodeError> {
    if input.len() < HEADER_LEN_V1 {
        return Err(DecodeError::TruncatedHeader {
            required: HEADER_LEN_V1,
            available: input.len(),
        });
    }
    if input[..4] != FRAME_MAGIC {
        return Err(DecodeError::InvalidMagic);
    }

    let version = read_u16(input, OFFSET_VERSION);
    if version != PROTOCOL_VERSION_V1 {
        return Err(DecodeError::UnsupportedVersion { received: version });
    }

    let header_len = read_u16(input, OFFSET_HEADER_LEN);
    if usize::from(header_len) != HEADER_LEN_V1 {
        return Err(DecodeError::InvalidHeaderLength {
            received: header_len,
        });
    }

    let raw_kind = read_u16(input, OFFSET_KIND);
    let kind = MessageKind::from_wire(raw_kind)
        .ok_or(DecodeError::UnknownMessageKind { received: raw_kind })?;
    let raw_flags = read_u16(input, OFFSET_FLAGS);
    let flags = FrameFlags::from_wire(raw_flags).ok_or(DecodeError::UnknownFlags {
        received: raw_flags,
    })?;
    let payload_len = read_u32(input, OFFSET_PAYLOAD_LEN) as usize;
    if payload_len > limits.max_payload_len {
        return Err(DecodeError::PayloadTooLarge {
            declared: payload_len,
            max: limits.max_payload_len,
        });
    }

    let world_id =
        WorldId::new(read_u64(input, OFFSET_WORLD_ID)).ok_or(DecodeError::InvalidWorldId)?;
    let instance_id = InstanceId::new(read_u64(input, OFFSET_INSTANCE_ID))
        .ok_or(DecodeError::InvalidInstanceId)?;
    let source_cell =
        CellId::new(read_u64(input, OFFSET_SOURCE_CELL)).ok_or(DecodeError::InvalidSourceCell)?;
    let destination_cell = CellId::new(read_u64(input, OFFSET_DESTINATION_CELL))
        .ok_or(DecodeError::InvalidDestinationCell)?;
    let route_epoch = RouteEpoch::new(read_u64(input, OFFSET_ROUTE_EPOCH))
        .ok_or(DecodeError::InvalidRouteEpoch)?;
    let source_tick = Tick::new(read_u64(input, OFFSET_SOURCE_TICK));
    let deadline_tick = Tick::new(read_u64(input, OFFSET_DEADLINE_TICK));
    let window =
        FrameWindow::new(source_tick, deadline_tick).ok_or(DecodeError::DeadlineBeforeSource {
            source_tick,
            deadline_tick,
        })?;
    let sequence = read_u64(input, OFFSET_SEQUENCE);

    let route = RouteScope::new(
        world_id,
        instance_id,
        source_cell,
        destination_cell,
        route_epoch,
    );
    validate_receive_context(route, window, context)?;

    let available_payload = input.len() - HEADER_LEN_V1;
    if available_payload < payload_len {
        return Err(DecodeError::TruncatedPayload {
            declared: payload_len,
            available: available_payload,
        });
    }
    if available_payload > payload_len {
        return Err(DecodeError::TrailingBytes {
            count: available_payload - payload_len,
        });
    }

    Ok(DecodedFrame {
        header: FrameHeader::new(kind, flags, route, window, sequence),
        payload: &input[HEADER_LEN_V1..],
    })
}

fn validate_receive_context(
    received: RouteScope,
    window: FrameWindow,
    context: ReceiveContext,
) -> Result<(), DecodeError> {
    let expected = context.route;
    if received.world_id != expected.world_id {
        return Err(DecodeError::WorldMismatch {
            expected: expected.world_id,
            received: received.world_id,
        });
    }
    if received.instance_id != expected.instance_id {
        return Err(DecodeError::InstanceMismatch {
            expected: expected.instance_id,
            received: received.instance_id,
        });
    }
    if received.source_cell != expected.source_cell {
        return Err(DecodeError::SourceCellMismatch {
            expected: expected.source_cell,
            received: received.source_cell,
        });
    }
    if received.destination_cell != expected.destination_cell {
        return Err(DecodeError::DestinationCellMismatch {
            expected: expected.destination_cell,
            received: received.destination_cell,
        });
    }
    match received.route_epoch.cmp(&expected.route_epoch) {
        Ordering::Less => {
            return Err(DecodeError::StaleRouteEpoch {
                expected: expected.route_epoch,
                received: received.route_epoch,
            })
        }
        Ordering::Greater => {
            return Err(DecodeError::FutureRouteEpoch {
                expected: expected.route_epoch,
                received: received.route_epoch,
            })
        }
        Ordering::Equal => {}
    }
    if context.current_tick > window.deadline_tick {
        return Err(DecodeError::Expired {
            deadline_tick: window.deadline_tick,
            current_tick: context.current_tick,
        });
    }
    Ok(())
}

fn write_u16(output: &mut [u8], offset: usize, value: u16) {
    output[offset..offset + 2].copy_from_slice(&value.to_le_bytes());
}

fn write_u32(output: &mut [u8], offset: usize, value: u32) {
    output[offset..offset + 4].copy_from_slice(&value.to_le_bytes());
}

fn write_u64(output: &mut [u8], offset: usize, value: u64) {
    output[offset..offset + 8].copy_from_slice(&value.to_le_bytes());
}

fn read_u16(input: &[u8], offset: usize) -> u16 {
    u16::from_le_bytes([input[offset], input[offset + 1]])
}

fn read_u32(input: &[u8], offset: usize) -> u32 {
    u32::from_le_bytes([
        input[offset],
        input[offset + 1],
        input[offset + 2],
        input[offset + 3],
    ])
}

fn read_u64(input: &[u8], offset: usize) -> u64 {
    u64::from_le_bytes([
        input[offset],
        input[offset + 1],
        input[offset + 2],
        input[offset + 3],
        input[offset + 4],
        input[offset + 5],
        input[offset + 6],
        input[offset + 7],
    ])
}

#[cfg(test)]
mod tests {
    use super::*;

    fn world(value: u64) -> WorldId {
        WorldId::new(value).expect("test world IDs are nonzero")
    }

    fn instance(value: u64) -> InstanceId {
        InstanceId::new(value).expect("test instance IDs are nonzero")
    }

    fn cell(value: u64) -> CellId {
        CellId::new(value).expect("test cell IDs are nonzero")
    }

    fn route_epoch(value: u64) -> RouteEpoch {
        RouteEpoch::new(value).expect("test route epochs are nonzero")
    }

    fn route() -> RouteScope {
        RouteScope::new(world(1), instance(2), cell(3), cell(4), route_epoch(5))
    }

    fn window() -> FrameWindow {
        FrameWindow::new(Tick::new(6), Tick::new(10)).expect("valid test frame window")
    }

    fn header() -> FrameHeader {
        FrameHeader::new(
            MessageKind::GhostSnapshot,
            FrameFlags::IDEMPOTENT.union(FrameFlags::KEYFRAME),
            route(),
            window(),
            8,
        )
    }

    fn receive_at(current_tick: u64) -> ReceiveContext {
        ReceiveContext::new(route(), Tick::new(current_tick))
    }

    fn decode_test(input: &[u8], limits: FrameLimits) -> Result<DecodedFrame<'_>, DecodeError> {
        decode_v1(input, limits, receive_at(7))
    }

    fn encoded(payload: &[u8]) -> Vec<u8> {
        let mut output = vec![0_u8; HEADER_LEN_V1 + payload.len()];
        let written = encode_v1(header(), payload, &mut output, FrameLimits::default())
            .expect("test frame encodes");
        output.truncate(written);
        output
    }

    #[test]
    fn golden_frame_is_explicit_little_endian() {
        let actual = encoded(&[0xaa, 0xbb, 0xcc]);
        let expected = [
            b'A', b'L', b'I', b'C', 1, 0, 80, 0, 1, 0, 3, 0, 3, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2,
            0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 5, 0, 0, 0, 0, 0,
            0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0xaa,
            0xbb, 0xcc,
        ];
        assert_eq!(actual, expected);
    }

    #[test]
    fn round_trip_borrows_exact_payload() {
        let bytes = encoded(b"border-state");
        let decoded = decode_test(&bytes, FrameLimits::default()).expect("valid frame decodes");

        assert_eq!(decoded.header(), header());
        assert_eq!(decoded.payload(), b"border-state");
        assert_eq!(decoded.payload().as_ptr(), bytes[HEADER_LEN_V1..].as_ptr());
    }

    #[test]
    fn encode_failure_leaves_output_unchanged() {
        let limits = FrameLimits::new(2).expect("valid test limit");
        let mut too_small = [0x5a_u8; HEADER_LEN_V1];
        let original = too_small;
        assert_eq!(
            encode_v1(header(), b"abc", &mut too_small, limits),
            Err(EncodeError::PayloadTooLarge { actual: 3, max: 2 })
        );
        assert_eq!(too_small, original);

        let limits = FrameLimits::default();
        assert_eq!(
            encode_v1(header(), b"a", &mut too_small, limits),
            Err(EncodeError::OutputTooSmall {
                required: HEADER_LEN_V1 + 1,
                available: HEADER_LEN_V1,
            })
        );
        assert_eq!(too_small, original);
    }

    #[test]
    fn rejects_every_truncated_prefix() {
        let bytes = encoded(b"payload");
        for prefix_len in 0..bytes.len() {
            assert!(decode_test(&bytes[..prefix_len], FrameLimits::default()).is_err());
        }
        assert!(decode_test(&bytes, FrameLimits::default()).is_ok());
    }

    #[test]
    fn rejects_header_version_kind_flags_and_reserved_zeroes() {
        let mutations: &[(usize, &[u8], DecodeError)] = &[
            (0, b"NOPE", DecodeError::InvalidMagic),
            (
                OFFSET_VERSION,
                &2_u16.to_le_bytes(),
                DecodeError::UnsupportedVersion { received: 2 },
            ),
            (
                OFFSET_HEADER_LEN,
                &55_u16.to_le_bytes(),
                DecodeError::InvalidHeaderLength { received: 55 },
            ),
            (
                OFFSET_KIND,
                &99_u16.to_le_bytes(),
                DecodeError::UnknownMessageKind { received: 99 },
            ),
            (
                OFFSET_FLAGS,
                &0x8000_u16.to_le_bytes(),
                DecodeError::UnknownFlags { received: 0x8000 },
            ),
            (
                OFFSET_WORLD_ID,
                &0_u64.to_le_bytes(),
                DecodeError::InvalidWorldId,
            ),
            (
                OFFSET_INSTANCE_ID,
                &0_u64.to_le_bytes(),
                DecodeError::InvalidInstanceId,
            ),
            (
                OFFSET_SOURCE_CELL,
                &0_u64.to_le_bytes(),
                DecodeError::InvalidSourceCell,
            ),
            (
                OFFSET_DESTINATION_CELL,
                &0_u64.to_le_bytes(),
                DecodeError::InvalidDestinationCell,
            ),
            (
                OFFSET_ROUTE_EPOCH,
                &0_u64.to_le_bytes(),
                DecodeError::InvalidRouteEpoch,
            ),
        ];

        for (offset, replacement, expected) in mutations {
            let mut bytes = encoded(b"x");
            bytes[*offset..*offset + replacement.len()].copy_from_slice(replacement);
            assert_eq!(decode_test(&bytes, FrameLimits::default()), Err(*expected));
        }
    }

    #[test]
    fn rejects_cross_scope_route_mismatch_and_expiry() {
        let bytes = encoded(b"x");
        let mismatches = [
            (
                RouteScope::new(world(9), instance(2), cell(3), cell(4), route_epoch(5)),
                DecodeError::WorldMismatch {
                    expected: world(9),
                    received: world(1),
                },
            ),
            (
                RouteScope::new(world(1), instance(9), cell(3), cell(4), route_epoch(5)),
                DecodeError::InstanceMismatch {
                    expected: instance(9),
                    received: instance(2),
                },
            ),
            (
                RouteScope::new(world(1), instance(2), cell(9), cell(4), route_epoch(5)),
                DecodeError::SourceCellMismatch {
                    expected: cell(9),
                    received: cell(3),
                },
            ),
            (
                RouteScope::new(world(1), instance(2), cell(3), cell(9), route_epoch(5)),
                DecodeError::DestinationCellMismatch {
                    expected: cell(9),
                    received: cell(4),
                },
            ),
            (
                RouteScope::new(world(1), instance(2), cell(3), cell(4), route_epoch(6)),
                DecodeError::StaleRouteEpoch {
                    expected: route_epoch(6),
                    received: route_epoch(5),
                },
            ),
            (
                RouteScope::new(world(1), instance(2), cell(3), cell(4), route_epoch(4)),
                DecodeError::FutureRouteEpoch {
                    expected: route_epoch(4),
                    received: route_epoch(5),
                },
            ),
        ];

        for (expected_route, expected_error) in mismatches {
            assert_eq!(
                decode_v1(
                    &bytes,
                    FrameLimits::default(),
                    ReceiveContext::new(expected_route, Tick::new(7)),
                ),
                Err(expected_error)
            );
        }

        assert!(decode_v1(&bytes, FrameLimits::default(), receive_at(10)).is_ok());
        assert_eq!(
            decode_v1(&bytes, FrameLimits::default(), receive_at(11)),
            Err(DecodeError::Expired {
                deadline_tick: Tick::new(10),
                current_tick: Tick::new(11),
            })
        );
    }

    #[test]
    fn rejects_deadline_before_source_tick() {
        assert!(FrameWindow::new(Tick::new(6), Tick::new(5)).is_none());

        let mut bytes = encoded(b"x");
        bytes[OFFSET_DEADLINE_TICK..OFFSET_DEADLINE_TICK + 8].copy_from_slice(&5_u64.to_le_bytes());
        assert_eq!(
            decode_test(&bytes, FrameLimits::default()),
            Err(DecodeError::DeadlineBeforeSource {
                source_tick: Tick::new(6),
                deadline_tick: Tick::new(5),
            })
        );
    }

    #[test]
    fn rejects_payload_limit_truncation_and_trailing_bytes() {
        let bytes = encoded(b"abc");
        assert_eq!(
            decode_test(&bytes, FrameLimits::new(2).expect("valid test limit")),
            Err(DecodeError::PayloadTooLarge {
                declared: 3,
                max: 2,
            })
        );

        let mut declared_longer = bytes.clone();
        declared_longer[OFFSET_PAYLOAD_LEN..OFFSET_PAYLOAD_LEN + 4]
            .copy_from_slice(&4_u32.to_le_bytes());
        assert_eq!(
            decode_test(&declared_longer, FrameLimits::default()),
            Err(DecodeError::TruncatedPayload {
                declared: 4,
                available: 3,
            })
        );

        let mut trailing = bytes;
        trailing.push(0);
        assert_eq!(
            decode_test(&trailing, FrameLimits::default()),
            Err(DecodeError::TrailingBytes { count: 1 })
        );
    }

    #[test]
    fn arbitrary_inputs_never_panic() {
        let limits = FrameLimits::default();
        let mut state = 0x9e37_79b9_7f4a_7c15_u64;

        for length in 0..=512 {
            let mut bytes = vec![0_u8; length];
            for byte in &mut bytes {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                *byte = state as u8;
            }

            let result = std::panic::catch_unwind(|| decode_v1(&bytes, limits, receive_at(7)));
            assert!(result.is_ok(), "decoder panicked for input length {length}");
        }
    }

    #[test]
    fn deterministic_generated_frames_round_trip() {
        let limits = FrameLimits::default();
        let kinds = [
            MessageKind::GhostSnapshot,
            MessageKind::HandoffControl,
            MessageKind::CellEvent,
            MessageKind::Heartbeat,
        ];
        let flags = [
            FrameFlags::NONE,
            FrameFlags::IDEMPOTENT,
            FrameFlags::KEYFRAME,
            FrameFlags::IDEMPOTENT.union(FrameFlags::KEYFRAME),
        ];
        let mut state = 0xd1b5_4a32_d192_ed03_u64;

        for case in 0..2_048_usize {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            let payload_len = state as usize % 257;
            let mut payload = vec![0_u8; payload_len];
            for byte in &mut payload {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                *byte = state as u8;
            }

            let generated_route = RouteScope::new(
                world(state | 1),
                instance(state.rotate_right(7) | 1),
                cell(state.rotate_left(13) | 1),
                cell(state.rotate_left(17) | 1),
                route_epoch(state.rotate_right(11) | 1),
            );
            let source_tick = Tick::new(state & 0x0000_ffff_ffff_ffff);
            let deadline_tick = source_tick
                .checked_add(32)
                .expect("masked tick has headroom");
            let generated_window =
                FrameWindow::new(source_tick, deadline_tick).expect("ordered generated window");
            let expected_header = FrameHeader::new(
                kinds[case % kinds.len()],
                flags[(case / kinds.len()) % flags.len()],
                generated_route,
                generated_window,
                state,
            );
            let mut output = vec![0_u8; HEADER_LEN_V1 + payload_len];
            let written = encode_v1(expected_header, &payload, &mut output, limits)
                .expect("bounded generated frame encodes");
            let decoded = decode_v1(
                &output[..written],
                limits,
                ReceiveContext::new(generated_route, deadline_tick),
            )
            .expect("bounded generated frame decodes");

            assert_eq!(decoded.header(), expected_header, "case {case}");
            assert_eq!(decoded.payload(), payload, "case {case}");
        }
    }

    #[test]
    fn absolute_limit_cannot_be_relaxed() {
        assert!(FrameLimits::new(ABSOLUTE_MAX_PAYLOAD_LEN).is_some());
        assert!(FrameLimits::new(ABSOLUTE_MAX_PAYLOAD_LEN + 1).is_none());
    }
}
