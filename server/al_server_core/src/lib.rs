//! Engine-free primitives for AnotherLife's authoritative simulation servers.
//!
//! This crate intentionally contains no networking runtime, database client,
//! orchestration SDK, Unity integration, or process-wide mutable state. Its
//! types are small enough to exercise with deterministic unit tests and fuzzing.

#![forbid(unsafe_code)]
#![deny(missing_docs)]
#![deny(rustdoc::broken_intra_doc_links)]

pub mod handoff;
pub mod microcell;
pub mod ownership;
pub mod wire;
