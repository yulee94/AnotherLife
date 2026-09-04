# Authoritative Multiplayer Backend and Security Architecture

Status: proposed production boundary. The playable client remains the current
delivery priority; this contract prevents that client from growing into the
eventual multiplayer authority.

## First principles

1. The client is a presentation and prediction participant, never a source of
   truth for combat, objectives, inventory, currency, progression, position, or
   time.
2. The active simulation process owns hot world state. Databases do not sit in
   the per-tick movement or combat loop.
3. One durable transactional system is the authority. CloudNativePG/PostgreSQL
   is the initial default; TiDB is a measured replacement, never a second source
   of truth. A cache or object store cannot silently become character truth.
4. Every mutation is authenticated, authorized, sequence checked, bounded by
   server-known rules, idempotent where retries are possible, and observable.
5. Protocol, storage, and simulation schemas evolve independently. Network
   messages are not database rows.
6. Native client code can raise reverse-engineering cost; it cannot make an
   untrusted machine authoritative or keep a long-lived secret.
7. Scale decisions are earned with representative profiles and load tests. The
   system begins as a modular deployable, not a fleet of speculative services.

## Target topology

```text
Unity client (C#)
  presentation, input, interpolation, owned-character prediction
       |
       | TLS 1.3 / QUIC transport abstraction
       | - reliable streams: session, commands, inventory, chat, control
       | - QUIC DATAGRAM when supported: transient input/snapshot traffic
       | - versioned Protobuf envelopes; measured compact snapshot codec later
       v
Rust edge/session gateway
  authentication, admission, rate/replay controls, routing, protocol versions
       |
       v
Rust authoritative world cell
  fixed-tick simulation, movement, combat, AI, RvR objectives, interest sets
       |
       +---------------------> Valkey or Dragonfly
       |                       presence, routing, leases, disposable hot cache
       |
       +---------------------> PostgreSQL on CloudNativePG
       |                       accounts, characters, inventory, economy,
       |                       progression, durable objective outcomes, outbox
       |
       +---------------------> S3-compatible object API / SeaweedFS
                               replays, large snapshots, generated artifacts,
                               crash bundles, patch/content objects
```

The first server implementation should remain one Rust deployable with internal
gateway, simulation, and persistence modules. Split a module into an independent
service only when isolation, ownership, scaling behavior, or failure containment
requires it.

## Production deployment planes

The eventual production topology is three cooperating planes, not one giant
Kubernetes cluster and not one synchronous request chain:

```text
Internet players
      |
      v
global anycast DDoS protection (optional, provider-neutral)
      | clean L3/L4 traffic; two independent tunnels/interconnects per region
      v
regional edge/data plane on dedicated game nodes
  BGP/UDP VIP -> bounded XDP filter -> connection-aware QUIC gateways
      |                                  |
      |                                  +-- shared snapshot fan-out groups
      v
authoritative Rust simulation fabric
  battle coordinator -> macro cells -> owned micro-shards + border ghosts
      |
      +-- asynchronous persistence/outbox intents
      +-- asynchronous replay/telemetry events

regional persistence plane
  CloudNativePG/PostgreSQL OR TiDB/TiKV -- durable player/world truth
  Valkey OR Dragonfly                   -- disposable cache/presence/routes
  independent S3 API/SeaweedFS          -- blobs, replays, backups/artifacts

control and operations plane
  identity/PKI, catalog and release signing, fleet placement, admission,
  capacity forecasting, observability, incident response, backup/restore
```

The game data plane should use dedicated, tainted bare-metal node pools with
local NUMA/NIC affinity and explicit CPU/memory reservations. Databases, CI,
telemetry indexing, and ordinary web workloads must not contend with an active
battle tick. Kubernetes can schedule and replace processes; it is not the
per-packet router, ownership oracle, or cross-cell transaction protocol.

Cloudflare Magic Transit is one possible external L3/L4 protection layer. It
announces protected prefixes and delivers clean traffic through GRE, IPsec, or a
private interconnect; it does not replace the game's QUIC connection endpoint.
Use provider-independent BGP and tunnel contracts, validate MTU after tunnel and
QUIC overhead, provision two origin paths per region, and retain a tested bypass
or alternate mitigation path. Measure direct-server-return versus symmetric
egress because asymmetric paths can affect latency, observability, and incident
operations.

Cilium is suitable for east/west policy and ordinary Kubernetes services. Its
eBPF kube-proxy replacement, DSR, Maglev selection, and native XDP acceleration
are candidates for the north/south path only after connection continuity is
proven. Five-tuple affinity alone is insufficient for mobile QUIC migration; the
front door must understand the server-issued connection-ID routing contract or
forward to the gateway that owns the connection.

References:

- Magic Transit reference architecture: https://developers.cloudflare.com/reference-architecture/architectures/magic-transit/
- Magic Transit tunnel and MTU behavior: https://developers.cloudflare.com/magic-transit/reference/gre-ipsec-tunnels/
- Cilium kube-proxy-free, DSR, Maglev, and XDP options: https://docs.cilium.io/en/latest/network/kubernetes/kubeproxy-free/

## Client and simulation responsibilities

### Unity client

- Samples input and sends input commands with session, entity, sequence, and
  client-time metadata.
- Predicts only the locally owned champion and reconciles against authoritative
  snapshots.
- Interpolates remote entities and renders presentation-only effects.
- May perform cheap local plausibility checks for responsiveness, but never
  awards damage, loot, currency, objective ownership, or progression.
- Treats all server payloads as untrusted input and enforces bounds before
  allocating or instantiating content.

### Rust authoritative world cell

- Uses a deterministic fixed-step simulation contract and a single-writer owner
  for each entity and RvR objective.
- Validates movement envelopes, cooldowns, resources, line of sight, range,
  targetability, inventory ownership, objective eligibility, and rate limits.
- Calculates hits, damage, deaths, rewards, loot, objective transitions, and
  persistence intents.
- Computes per-connection interest sets and bounded delta snapshots.
- Produces idempotent persistence commands and an auditable security event trail.

Cross-cell transfers use an explicit handoff state machine. An entity cannot be
writable in two cells at once. Handoff failure leaves one recoverable owner, not
two optimistic owners.

## Protocol boundary

Use Protobuf for versioned command, event, control, and service messages. Follow
these compatibility rules:

- Never reuse a field number. Reserve removed numbers and names.
- Add fields compatibly and tolerate unknown fields.
- Keep RPC messages separate from persistence records.
- Negotiate protocol capabilities during session admission.
- Maintain golden-wire fixtures and old-client/new-server compatibility tests.
- Bound message bytes, collection counts, string lengths, recursion, decompressed
  size, and per-tick work before decoding into simulation state.

QUIC provides encrypted connections, multiplexed streams, fast establishment,
and connection migration. Reliable streams are appropriate for commands that
must arrive. High-frequency state should not be forced through one ordered
stream; use independent streams or QUIC DATAGRAM where the supported Unity and
server libraries prove stable on every target platform. Keep the transport
behind an interface so QUIC availability cannot block the playable slice.

Protobuf is not automatically the final high-frequency snapshot format. Profile
bandwidth and CPU with RvR-sized interest sets; introduce a schema-versioned,
bit-packed delta codec only if measurements justify the added complexity.

FlatBuffers can avoid a separate unpacking object graph when readers access a
validated buffer in place, but it is not synonymous with zero-copy end to end.
The NIC, QUIC implementation, decryption, reassembly, bounds verification,
ownership/lifetime transfer, Unity native staging, and GPU upload still impose
work and sometimes copies. Generic tables may also be larger than a purpose-built
quantized snapshot. Benchmark Protobuf, FlatBuffers, and the bounded bit-packed
codec against identical messages and hostile inputs; use the simplest format
that meets bytes, decode CPU, allocations, compatibility, and safety budgets.

References:

- QUIC: https://www.rfc-editor.org/rfc/rfc9000
- QUIC DATAGRAM: https://www.rfc-editor.org/rfc/rfc9221
- Protobuf compatibility practices: https://protobuf.dev/best-practices/dos-donts/
- FlatBuffers untrusted-buffer verification: https://flatbuffers.dev/languages/cpp/#access-of-untrusted-buffers

### Internal hot-path transport and binary framing

Do not make `gRPC is slow` or `raw bytes are zero cost` an architectural law.
gRPC over HTTP/2 adds request metadata, a five-byte length prefix per message,
flow-control/runtime machinery, and TCP head-of-line behavior after loss. Those
costs make it a poor default for expiring cross-cell tick traffic. On a long-lived
stream the request headers are amortized, however, so it is misleading to claim
that every 24-byte update carries a fresh set of HTTP headers. gRPC remains useful
for low-frequency administration, fleet control, persistence workers, and tools
where generated schemas, deadlines, observability, and interoperability are worth
more than the last microsecond.

Use the narrowest transport that matches the placement and delivery semantics:

| Placement | Initial transport | Payload and delivery contract |
| --- | --- | --- |
| Same Rust process | Direct phase buffers and bounded SPSC/MPSC owner queues | Typed records or immutable pages; no serialization or OS IPC |
| Same host, separate processes | Benchmarked shared-memory pool plus generation-checked handles; Unix-domain socket/event control path | Bounded lifetime and backpressure; benchmark a simple ring against iceoryx2 |
| Different hosts, expiring state | QUIC/UDP datagrams behind a transport abstraction | Versioned, explicitly encoded ghost/snapshot deltas; newer state supersedes old state |
| Different hosts, authoritative transition | Reliable QUIC stream or bounded request/ack state machine | Idempotency ID, ownership epoch, tick/deadline, retry and duplicate semantics |
| Control and operations | gRPC/Protobuf or HTTP | Not called synchronously from the simulation tick |

Prefer one NUMA-aware, multi-threaded simulation process per host before splitting
every cell into a process and paying IPC. Separate processes only when crash
containment, independent deployment, security boundaries, or measured placement
needs justify them. Shared memory does not make remote NUMA access free, and a
pointer from one process is not a valid ownership protocol in another. Middleware
must exchange handles and preserve buffer lifetime, generation, access mode, and
reclamation after a process dies.

iceoryx2 is the leading same-host zero-copy candidate because it explicitly lends
shared-memory samples and maintains their lifetime across publishers/subscribers.
Zenoh is a broader pub/sub and routing system whose shared-memory path can fall
back to copies outside a shared-memory domain; its current Rust allocation API is
marked unstable and its garbage collection can add jitter. Benchmark both only
against the actual payload sizes and fan-out. For small fixed tick records, a
bounded in-process queue or purpose-built shared ring can beat a general
middleware while being easier to reason about.

Lock-free is not synonymous with wait-free, bounded, or correct. Select SPSC,
MPSC, or immutable fan-out pages from the real producer/consumer topology; fix
capacity; define overwrite, drop, and backpressure behavior; pad hot atomics to
avoid false sharing; and test wraparound, stalled consumers, crash recovery, and
memory-ordering assumptions. No producer may block a simulation tick waiting for
a telemetry or stale-ghost consumer.

Never cast untrusted packet bytes directly to `#[repr(C, packed)]` records. The
suggested 17-byte movement struct has no protocol version, length, world/cell,
ownership epoch, source tick, sequence, expiry, coordinate origin/range, or
integrity context. It also makes multi-byte fields unaligned; Rust explicitly
forbids references to unaligned packed fields, and even `read_unaligned` remains
unsafe unless the bytes are a valid initialized value. Native endianness and raw
`f32` bit patterns are not a cross-platform wire contract.

The initial hot codec writes fields explicitly into a caller-owned byte slice and
reads them only after bounds checks. Multi-byte integers use specified little-
endian encoding. Positions are range-checked and quantized relative to a declared
cell origin. A common envelope contains at least:

```text
magic | protocol version | message kind | header length | payload length
world/instance | source cell | owner epoch | source tick | sequence | deadline
```

Transport authentication protects the peer/channel; the decoder still rejects
unknown required versions, impossible lengths, stale epochs, non-finite or
out-of-range values, and excess per-tick work. `bytemuck`, `zerocopy`,
FlatBuffers, or an unsafe fast path may be considered only after a safe reference
codec exists, hostile-buffer fuzzing passes, and an end-to-end profile proves a
material gain. Those libraries do not supply authorization, freshness, or
application integrity by themselves.

References:

- gRPC over HTTP/2 message framing: https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md
- HTTP/2 retains TCP head-of-line blocking: https://www.rfc-editor.org/rfc/rfc9113
- Rust packed-field alignment rules: https://doc.rust-lang.org/stable/reference/type-layout.html
- Rust `read_unaligned` safety requirements: https://doc.rust-lang.org/std/ptr/fn.read_unaligned.html
- iceoryx2 zero-copy ownership model: https://iceoryx.io/
- Zenoh shared-memory design and limitations: https://github.com/eclipse-zenoh/roadmap/blob/main/rfcs/ALL/SHM.md

## Real-time network data plane

```text
Anycast / regional UDP VIP
       |
       v
minimal XDP/eBPF filter + queue steering
       |
       v
QUIC gateway pool (connection/TLS/congestion authority)
       |
       +--> reliable streams: admission, inventory, chat, control, handoff
       +--> DATAGRAM: redundant input windows, snapshots, transient events
       |
       v
session-to-cell router --> authoritative battle/world cells
```

### QUIC gateway cluster

A QUIC connection contains endpoint state and cannot be sprayed round-robin after
the handshake. Route by server-selected, non-zero Destination Connection IDs or a
stateful mapping so every packet reaches an instance that owns or can retrieve
the connection. Connection-ID routing material should be opaque to outside
observers, versioned, integrity protected, key rotated, and independent of the
gameplay cell ID.

The gateway owns the external QUIC connection for its lifetime. A gameplay cell
handoff updates an internal session route; it does not force the client to create
a new Internet connection. This separates mobile NAT/address migration from
world-cell ownership migration.

- Use address-validation Retry tokens and QUIC anti-amplification rules before
  spending material CPU or egress on an unverified address.
- Gate 0-RTT to replay-safe operations. Never allow a 0-RTT purchase, reward,
  character mutation, or ownership transfer.
- Send redundant recent input samples in DATAGRAM frames with sequence/tick IDs;
  stale input is discarded rather than retransmitted after its deadline.
- Snapshots carry baseline acknowledgements and can supersede older state.
- Reliable streams are independent by domain so one large control payload does
  not block unrelated control traffic.
- Every message has byte, count, decode-work, frequency, and deadline budgets.
- QUIC provides congestion control for DATAGRAM traffic but not retransmission;
  the application still needs pacing, prioritization, expiry, and loss recovery.

References:

- QUIC deployment and connection migration: https://www.rfc-editor.org/rfc/rfc9308
- QUIC wire image and connection-ID load balancing: https://www.rfc-editor.org/rfc/rfc9312

### XDP/eBPF and AF_XDP boundary

XDP/eBPF is an optional measured optimization at the Linux edge, not a gameplay
framework or anti-cheat system. A minimal program can reject malformed IP/UDP,
obvious floods, disallowed destinations, and expired route generations before
socket allocation; it can also steer packets by NIC queue/CPU or a bounded
connection-ID routing map.

Keep cryptography, QUIC state, protocol decoding, account policy, and gameplay in
auditable user space. QUIC encrypts most transport state, and XDP must not depend
on parsing protected payloads or on a brittle assumption that every UDP packet on
a port has one fixed QUIC layout.

Deployment gates:

- first prove ordinary kernel UDP with `SO_REUSEPORT` is insufficient;
- keep the verifier-approved program small, bounded, versioned, canaried, and
  instantly bypassable;
- use per-CPU maps and bounded cardinality; do not create attacker-controlled
  permanent map entries;
- rate limits account for carrier-grade NAT and IPv6 prefixes rather than banning
  thousands of legitimate users behind one address;
- expose pass/drop/redirect reasons and map pressure without high-cardinality
  logging on the packet path;
- test driver support, generic/copy fallback, queue ownership, rolling upgrades,
  and failure behavior before enabling AF_XDP zero-copy;
- benchmark packets per second, CPU per packet, cache misses, drops, handshake
  completion, p99 latency, and DDoS behavior with and without the program.

AF_XDP can redirect selected frames into user-space UMEM rings, but it also makes
the application responsible for queue/ring ownership and buffer lifecycle. Adopt
it only if measurements show packet I/O, rather than QUIC crypto, snapshot work,
or simulation, is the limiting cost.

References:

- Linux AF_XDP: https://docs.kernel.org/networking/af_xdp.html
- Linux XDP redirect: https://docs.kernel.org/bpf/redirect.html

## Durable SQL: PostgreSQL on CloudNativePG

PostgreSQL stores transactional player and world outcomes. CloudNativePG is the
recommended Kubernetes operator boundary because it manages primary/replica
routing, failover, rolling maintenance, metrics, connection pooling, backups,
and recovery without changing PostgreSQL semantics.

Initial production shape:

- Three PostgreSQL instances across independent failure domains: one primary and
  two replicas.
- Separate read/write and read-only services; gameplay mutations always target
  the primary endpoint.
- PgBouncer with explicit connection budgets for gateways, world cells, workers,
  and operator access.
- Tables partitioned by measured growth characteristics, not by speculative
  microservice ownership.
- Transactional outbox for external side effects. Commit the domain mutation and
  outbox record in one transaction, then deliver asynchronously with idempotent
  consumers.
- Unique operation IDs for purchases, rewards, transfers, mail attachments, and
  other retryable value mutations.
- Online base backups plus WAL archiving and point-in-time recovery to an
  independently operated object store.
- Scheduled restore drills that prove recovery time and recovery point objectives.

Synchronous replication can reduce data-loss exposure, but adds write latency
and can reduce availability. Choose it per mutation class and measured region
latency; do not put per-tick simulation state into synchronous SQL writes.

References:

- CloudNativePG overview and HA model: https://cloudnative-pg.io/docs/current/
- Backup and recovery: https://cloudnative-pg.io/docs/current/backup_recovery/
- Recovery and PITR: https://cloudnative-pg.io/docs/current/recovery/

## Distributed SQL alternative: TiDB

TiDB is a credible replacement for the CloudNativePG/PostgreSQL authority tier
when measured durable write scale requires automatic horizontal distribution.
Its stateless SQL layer speaks the MySQL protocol and executes over TiKV regions
managed by Placement Driver nodes. It provides distributed ACID transactions and
replicated storage, but every cross-region transaction adds consensus and network
work that does not exist inside one PostgreSQL primary.

Do not deploy TiDB alongside PostgreSQL as a second source of player truth. Make
one explicit system-of-record decision:

| Requirement | Prefer CloudNativePG/PostgreSQL | Prefer TiDB |
| --- | --- | --- |
| First production region and modest team | Fewer components and familiar PostgreSQL operations | Additional TiDB, PD, and TiKV operational surface is justified only by measured need |
| Durable write scaling | One vertically scaled/partitioned write primary is sufficient | Writes must scale horizontally beyond one primary |
| Data and query model | Rich relational constraints, PostgreSQL extensions, reporting, or spatial types matter | MySQL-compatible subset fits and distribution-friendly keys/queries are designed deliberately |
| Transactions | Most value mutations are local to a small relational aggregate | Transactions must span a distributed keyspace and their p99 cost is acceptable |
| Resharding | Application-owned partitioning is manageable | Automatic region split, placement, and rebalancing materially reduce risk |
| Failure behavior | Primary failover plus PITR meets the objective | Region replication and stateless SQL scale-out meet tested objectives better |

TiDB should replace both CloudNativePG and any proposed standalone TiKV tier. The
ephemeral cache and object store remain separate. Per-tick RvR simulation remains
in Rust process memory; moving it into distributed SQL would make latency and
availability worse regardless of database brand.

Decision benchmark before replacement:

- production-shaped login hydration, character save, inventory transfer, reward,
  guild/realm, auction, and RvR outcome transactions;
- p50/p95/p99 latency and throughput at normal load, burst load, hot-key load,
  replica loss, leader movement, rebalance, backup, and schema change;
- application retry behavior with pessimistic conflicts, ambiguous commits, and
  connection loss;
- constraint/compatibility review, especially TiDB's unsupported MySQL features
  and lack of spatial types/indexes;
- backup restore, point-in-time objectives, observability, upgrades, operator
  staffing, and full infrastructure cost.

If selected, require TLS rather than merely enabling optional TLS, isolate TiDB,
PD, and TiKV from public networks, rotate service identities, enable encryption
at rest and log redaction, and test restore/failover. Database encryption does not
replace application authorization or server-side economy invariants.

References:

- TiDB architecture: https://docs.pingcap.com/tidb/stable/tidb-architecture/
- TiDB transaction behavior: https://docs.pingcap.com/tidb/stable/transaction-overview/
- TiDB MySQL compatibility limits: https://docs.pingcap.com/tidb/stable/mysql-compatibility/
- TiDB client TLS: https://docs.pingcap.com/tidb/stable/enable-tls-between-clients-and-servers/

## Disposable hot state: Valkey or Dragonfly

Use one cache-facing interface and benchmark both candidates under AnotherLife
traffic. The initial default should favor the option the team can operate and
recover confidently, not a synthetic headline result.

Eligible cache data:

- session presence and short-lived admission state;
- world-cell routing and server discovery;
- bounded rate-limit counters;
- short-lived social/presence projections;
- derived leaderboard or query caches;
- best-effort fan-out that can be reconstructed.

Forbidden cache authority:

- currency, purchased entitlements, canonical inventory, progression, character
  identity, or the sole copy of an RvR outcome;
- correctness-critical distributed locks with no database invariant;
- any record that cannot be rebuilt after total cache loss.

Assume asynchronous replication can lose the most recent writes during failure.
Every caller must behave correctly after eviction, failover, duplication, delay,
or complete cache loss.

References:

- Valkey capabilities: https://valkey.io/topics/introduction/
- Valkey persistence: https://valkey.io/topics/persistence/
- Valkey cluster operations: https://valkey.io/topics/cluster-tutorial/
- Dragonfly documentation: https://www.dragonflydb.io/docs

## Optional durable distributed KV: TiKV

TiKV is a different tier from Valkey/Dragonfly. It is a durable distributed KV
store built on RocksDB, Raft replication, and a Placement Driver; its RawKV API
offers single-key atomicity and its transactional API supports multi-key
transactions. That strength also adds network consensus, storage, scheduling,
backup, client, and operational cost.

Do not place TiKV in the initial critical path merely because it is written in
Rust or has strong benchmark results. Adopt it only after a representative load
test proves PostgreSQL partitioning/read replicas plus the hot cache cannot meet
a concrete durable-KV access pattern.

Candidate future uses:

- high-cardinality durable entity or world-cell snapshots addressed by stable
  keys, when relational queries are not needed;
- durable sparse state for very large numbers of dormant world entities;
- a globally distributed keyspace whose sharding/rebalancing requirements have
  outgrown an application-owned PostgreSQL partitioning scheme;
- compare-and-set or transactional KV workflows with deliberately designed key
  locality.

Poor uses:

- per-tick live simulation state, which belongs in the authoritative process;
- ephemeral presence or rate limits, which belong in the cache;
- relational inventory/economy workflows that depend on joins, constraints,
  reporting, and mature SQL tooling;
- blob data, which belongs in object storage;
- a second canonical copy of records also owned by PostgreSQL.

If adopted, choose RawKV versus transactional APIs explicitly, design prefixes to
avoid hot regions, cap values, use batch operations carefully, provision local
NVMe, spread replicas with topology labels, enable mutual TLS, encryption at rest,
log redaction, least-privilege client identities, backups, and restore drills. Do
not expose TiKV or PD to game clients. Benchmark p50/p95/p99 latency during node
loss, leader movement, compaction, backup, and rebalancing—not only steady-state
throughput.

References:

- TiKV architecture and API semantics: https://tikv.org/docs/dev/reference/architecture/overview/
- TiKV performance methodology: https://tikv.org/docs/dev/deploy/performance/overview/
- TiKV transport security: https://tikv.org/docs/dev/deploy/configure/security/

## Object storage: SeaweedFS

SeaweedFS is a viable self-hosted S3-compatible tier for large, non-transactional
objects. Use it for replays, snapshots, build/content artifacts, telemetry
bundles, and other blobs whose searchable metadata remains in PostgreSQL.

Production shape when self-hosting is justified:

- three master nodes forming a Raft quorum;
- multiple volume servers spread across failure domains;
- redundant filers and independently scalable stateless S3 gateways;
- hot-object replication and scheduled erasure coding for colder data;
- checksums, lifecycle policy, capacity alarms, scrub/repair jobs, and restore
  exercises;
- bucket-level credentials and network policy; game clients never receive broad
  storage credentials;
- signed, short-lived upload/download URLs issued by an authorized service.

Do not create a circular disaster-recovery dependency. If SeaweedFS filer
metadata is stored in the same CloudNativePG cluster whose WAL archives are
written only to that SeaweedFS cluster, loss of the SQL failure domain can make
its own backup target unrecoverable. PostgreSQL backups require an independent
object-store failure domain or separately recoverable SeaweedFS metadata.

SeaweedFS is not the character database and is not a low-latency simulation bus.
For a small deployment, a managed S3-compatible service may be operationally
safer and cheaper than owning masters, filers, volume repair, erasure coding, and
capacity management.

References:

- SeaweedFS architecture: https://github.com/seaweedfs/seaweedfs/wiki/SeaweedFS_Architecture.pdf
- Production topology: https://github.com/seaweedfs/seaweedfs/blob/master/note/slides/seaweedfs-production-setup.md

## Optional durable event backbone: Apache Pulsar

Pulsar is a credible asynchronous backbone when AnotherLife needs many durable
consumers, long backlogs, independent replay, and regional event replication. It
is not required to make the battle simulation scale, and it never carries a
synchronous dependency in the authoritative tick.

```text
player input -> authoritative Rust cell memory -> paced client snapshots
                         |
                         +-> disposable presence/route projection -> cache
                         |
                         +-> durable value command -> SQL transaction + outbox
                         |                                  |
                         |                                  v
                         |                               Pulsar
                         |                         -> projections/analytics/mail
                         |
                         +-> explicitly lossy replay/telemetry stream -> Pulsar
```

Good Pulsar workloads:

- committed transactional outbox events for achievements, mail, social/guild
  projections, economy audit, moderation, and notifications;
- replay segments, security events, telemetry, analytics, and data-lake feeds;
- asynchronous world-control and fleet-capacity observations;
- region-to-region propagation of non-tick-critical public events.

Bad Pulsar workloads:

- movement commands, collision candidates, per-tick snapshots, border ghosts,
  ownership handoff barriers, or the live cell routing table;
- a replacement for SQL inventory/economy invariants;
- a promise of exactly-once gameplay without idempotent domain operations;
- synchronous cross-region coordination of one battle.

The reliable domain path is `SQL transaction -> outbox row -> relay -> Pulsar`.
Consumers store/idempotently recognize the operation/event ID before side
effects. Direct dual-writes from gameplay to SQL and Pulsar create ambiguity when
only one succeeds. High-volume non-transactional telemetry can publish through a
separate bounded path with an explicit loss/backpressure policy.

If selected, operate one local Pulsar cluster per region. Brokers are separate
from BookKeeper bookies and the metadata quorum; isolate their CPU, memory, disk,
and failure domains from battle nodes and the SQL authority. Geo-replication is
asynchronous and conflict semantics stay application-owned. Partition keys
preserve the smallest required ordering domain, such as character, guild, or
world objective; a global ordered topic would become a bottleneck. Bound message
size, retention, backlog, redelivery, poison messages, schema evolution, tenant
quotas, and producer/consumer permissions.

Pulsar tiered storage can offload sealed backlog segments to an S3-compatible
target. If SeaweedFS is tested for that role, keep the active BookKeeper quorum
and independent disaster-recovery copies; do not create another circular failure
domain. Encrypt transport, use workload identities and namespace ACLs, and redact
private/security fields before broad analytics subscriptions.

Adopt Pulsar only after a production-shaped comparison with a simpler Kafka/
Redpanda or NATS JetStream deployment. Test end-to-end p99, sustained and burst
throughput, backlog catch-up impact, key ordering, duplication, consumer failure,
broker/bookie/metadata loss, disk pressure, geo-link loss, schema rollback,
upgrade, restore, operator hours, and total cost. The first regional vertical
slice can use the SQL outbox directly and add a bus when independent consumers or
retention make it worthwhile.

Avoid the false comparison that Kafka is merely a monolithic compute-plus-disk
broker while Pulsar brokers scale instantly. Current Kafka uses KRaft rather than
requiring ZooKeeper and supports a local/remote tiered-storage model; Pulsar
separates stateless message brokers from BookKeeper storage but still has topic
ownership, connections, caches, bookie capacity, metadata coordination, and
rebalance/warm-up behavior. Benchmark the complete operated system, not the box
drawing. Pulsar's separation is valuable when independent broker/storage scaling,
many topics, long backlogs, or geo-replication match the real workload.

References:

- Pulsar architecture and BookKeeper separation: https://pulsar.apache.org/docs/4.1.x/concepts-architecture-overview/
- Pulsar concepts and geo-replication capabilities: https://pulsar.apache.org/docs/4.1.x/concepts-overview/
- Pulsar schema boundary: https://pulsar.apache.org/docs/4.1.x/schema-overview/
- Pulsar tiered storage: https://pulsar.apache.org/docs/4.1.x/tiered-storage-overview/
- Kafka KRaft deployment roles: https://kafka.apache.org/41/operations/kraft/
- Kafka local/remote tiered-storage boundary: https://kafka.apache.org/41/operations/tiered-storage/

## Layered anti-cheat and anti-tamper

### 1. Server authority

The primary anti-cheat is architectural. The server derives outcomes from
validated input and server-owned state. It rejects impossible motion, timing,
cooldown, targeting, inventory, economy, and objective transitions.

### 2. Protocol and session security

- TLS 1.3 through QUIC, short-lived authenticated sessions, key rotation, and
  least-privilege service identities.
- Monotonic command sequences, bounded time windows, nonces where replay matters,
  and explicit reconnect/resume rules.
- Per-account, per-device, per-address, and per-command budgets at the edge.
- Fuzzed decoders and fail-closed version negotiation.

### 3. Simulation invariants

- Independent server time and random sources.
- Server-owned collision/nav queries for gameplay-critical movement and line of
  sight.
- Conservation and uniqueness constraints for value-bearing transactions.
- Idempotency and database constraints beneath application checks.

### 4. Signed content and build integrity

- Sign catalogs, remote content manifests, native libraries, and release builds.
- Verify content hashes before activation and maintain rollback-safe manifests.
- Keep signing keys outside source control and build workers except during the
  minimum signing operation.

### 5. Client hardening and telemetry

A Rust native plug-in may host performance-critical deterministic kernels,
bounded parsing, hashing, compression, or platform integrity signals. Obfuscation,
symbol stripping, LTO, value masking, and anti-debug checks only increase attack
cost. They must never contain permanent authority or a secret whose disclosure
breaks the economy. Treat every signal as probabilistic telemetry and combine it
with server-side behavioral evidence before enforcement.

### 6. Operations and enforcement

- Structured security events with privacy-aware retention.
- Shadow scoring and human-reviewable evidence for high-impact enforcement.
- Ban and appeal workflows resistant to account churn and false positives.
- Dependency scanning, SBOMs, secret scanning, patch SLAs, and incident drills.

## Rust and FFI boundary

Rust is preferred for the dedicated simulation server and for proven hot kernels.
C# remains appropriate for Unity presentation, input, editor tools, and engine
lifecycle integration.

A native client FFI must:

- expose a versioned C ABI with fixed-width types and explicit status codes;
- use caller-owned buffers or documented allocation/free pairs;
- validate pointers, lengths, enum values, and integer overflow;
- contain panics before they cross the ABI;
- avoid callbacks from arbitrary worker threads into Unity;
- avoid global mutable gameplay authority;
- ship per-platform binaries with ABI and symbol tests;
- provide a correct managed fallback until platform coverage is complete.

Do not move ordinary C# gameplay into Rust merely to hide it. Move deterministic,
profiled, portable computation when the measured benefit exceeds FFI marshalling,
debugging, deployment, and certification costs.

## Ten-thousand-player battle architecture

### The non-negotiable constraint

Ten thousand visible players cannot all be full-fidelity peers in one mutual
interaction set. A naive snapshot containing only 24 bytes per entity at 10 Hz is
2.4 MB/s (19.2 Mbit/s) per client before packet, reliability, event, voice, and
security overhead. Sending that to ten thousand clients is 24 GB/s (192 Gbit/s)
of battlefield egress. Full animation, equipment, combat, buffs, projectiles, and
physics would be much larger. All-to-all gameplay queries trend toward one hundred
million pair relationships per tick.

The production promise can be: every participant is perceptually represented
inside render distance, wall-clock battle time never slows, and every material
interaction remains authoritative. It cannot mean every client receives every
individual at full simulation, animation, identity, and update frequency.

### Evidence boundary and target vocabulary

There is no public production evidence for ten thousand human players in one
seamless, mutually interactive, full-fidelity real-time battle without time
dilation or aggressive fidelity reduction. EVE Online's publicly documented
6,739-player M2-XFE system peak experienced failed entry and state-recovery
problems. Its 6,557-character FWST-8 production battle remained at maximum time
dilation for the full 12-hour event, and EVE deliberately uses a one-hertz
simulation plus time dilation for overload. Aether Wars reached a 10,412 peak
including AI, but only 2,379 were peak concurrent humans (3,852 human participants
over the run), and CCP called it a technology demo. Improbable's disclosed human
density test reached 4,144 users; its ten-thousand tests used simulated clients
and bounded high-frequency focus sets.

AnotherLife therefore records four separate numbers instead of one marketing
count:

- **connected** — sessions admitted to the regional service;
- **represented** — identities perceptually present to a viewer;
- **individually replicated** — actors currently receiving individual state;
- **causally interactive** — actors able to affect one another at the tested
  authoritative fidelity and latency.

The credible first ten-thousand goal is represented and potentially relevant,
with bounded engaged/awareness sets and deterministic promotion before material
interaction. It must not be described as ten thousand full-fidelity combatants
until real-client, real-network, rendering, simulation, failure, and soak gates
prove that exact claim.

References:

- CCP M2-XFE failure analysis: https://www.eveonline.com/news/view/the-second-timer-in-m2-xfe
- CCP FWST-8 production battle and maximum time dilation: https://www.eveonline.com/news/view/fury-at-fwst-8-battle-report
- CCP server tick and simulation deep dive: https://www.eveonline.com/news/view/paint-your-ship-red-and-make-it-faster
- CCP time dilation design: https://www.eveonline.com/news/view/introducing-time-dilation-tidi
- CCP Aether Wars results: https://www.eveonline.com/fr/news/view/eve-aether-wars-round-one
- Improbable density tests and fidelity tiers: https://www.improbable.io/news/intimacy-at-scale-building-an-architecture-for-density

### Hierarchical fidelity without time dilation

Use three independently budgeted representations:

1. **Engaged set** — nearby, targeted, damaging, healing, colliding, grouped, or
   objective-relevant entities. Full authoritative state, responsive snapshots,
   individual animation, identity, equipment, and prediction/interpolation.
2. **Awareness set** — individually visible nearby forces that are not currently
   interacting with the player. Lower-frequency quantized transforms, coarse
   animation state, reduced equipment/material variants, and no expensive local
   prediction.
3. **Mass set** — distant forces represented as formation/sector aggregates plus
   deterministic GPU instances or impostors. The server sends aggregate bounds,
   density, faction, motion vector, combat intensity, and notable events; the
   client reconstructs stable visual members from versioned seeds. An individual
   is promoted before it can materially interact with the viewer.

Illustrative bandwidth envelope, to be replaced by measured codecs:

| Tier | Representation | Example budget |
| --- | --- | --- |
| Engaged | 128 individual entities × 20 Hz × 32-byte delta | 81.9 KB/s |
| Awareness | 512 individual entities × 5 Hz × 16-byte delta | 41.0 KB/s |
| Mass | 156 aggregates covering the remaining force × 2 Hz × 24 bytes | 7.5 KB/s |

The illustrative payload total is about 130 KB/s before overhead, versus 2.4
MB/s for the deliberately unrealistic minimal all-individual snapshot. Fidelity
changes with relevance and capacity; simulation time does not.

### Spatial indexing and causal relevance

A uniform spatial hash changes a radius query from scanning every entity to
examining the viewer's bucket and a bounded neighborhood. With a sensible cell
size and distributed occupancy, work approaches `O(N + candidates + results)`
per rebuild/query phase. It is not a proof against `O(N²)`: ten thousand players
in one bucket or an unbounded global effect recreates the dense interaction
graph.

Each cell therefore owns several purpose-specific indexes rather than one magic
grid:

- a fixed or adaptive 2D ground grid for players, NPCs, objectives, and common
  range queries;
- altitude bands or a sparse 3D grid for flyers and vertically layered spaces;
- swept broad-phase buckets for fast projectiles, followed by narrow-phase tests;
- static BVHs/terrain queries for world collision, line of sight, and occlusion;
- explicit caps, overflow buckets, density alarms, and hot-bucket subdivision.

Indexes rebuild or incrementally update in deterministic phases. Stable entity
IDs break ties; iteration order is never hash-map accident. Profile cell size
against actual ability radii, movement speed, terrain topology, and cache lines.

`rstar` is a reasonable benchmark candidate for relatively stable, sparse, or
irregular geometry and nearest-neighbor queries. Its own documentation notes that
R-trees suit many queries with relatively few insertions; each moving entity
update costs tree mutation work, iterator forms may allocate, and fully
overlapping elements degrade queries toward `O(N)`. A rebuilt uniform grid often
wins for thousands of similarly sized, fast-moving ground actors. Benchmark
grid, sweep-and-prune, `rstar`, and a custom/refitted BVH on real movement and
hotspot traces rather than standardizing on a crate name.

Distance is only one input to network relevance. A relevance graph must promote
an entity for causal reasons even when it is farther away: current target or
attacker, party/raid member, projectile source, objective contributor, combat
event, voice/social membership, or an entity about to cross a visibility or
interaction boundary. Visibility, occlusion, faction, stealth, detection, and
authorization filters run before serialization. Promotion is immediate where
gameplay requires it; demotion uses hysteresis and grace time.

Compute relevance once per spatial/causal cohort, compile shared public snapshot
pages once, and append a small private overlay for owned prediction state,
secrets, quest state, and authorization-specific facts. Never leak hidden or
enemy-only data merely to make shared batching easier.

### Tick-rate, packet, and codec budgets

Network LOD schedules replication, not authoritative simulation correctness.
The server still resolves material movement and combat at the fixed battle tick;
only what a given observer receives changes by relevance tier, motion, recent
change, bandwidth estimate, loss, and deadline.

- Inputs use compact sequence-numbered windows containing the newest command and
  a few recent commands; duplicates are harmless and late inputs expire.
- State deltas are quantized relative to a cell/sector origin and baseline ID.
  Bit widths, coordinate ranges, and overflow behavior are versioned and tested.
- Batch several entity deltas into a shared page, but keep most Internet UDP
  datagrams within a conservative path payload near 1200 bytes until path-MTU
  discovery proves a larger value. Never rely on IP fragmentation.
- Split large snapshot work across paced datagrams with independent deadlines.
  Prioritize owned corrections and engaged events over awareness and mass data.
- Compression is opt-in for sufficiently large payloads after profiling. Tiny
  real-time packets often become slower or larger. Bound compressed and expanded
  sizes, CPU time, dictionary identity, and nesting; do not mix secrets with
  attacker-controlled reflection in the same compression context.
- Acknowledgements identify usable baselines and highest processed input, not
  every transient packet. Missing deltas trigger a newer baseline or full state,
  never an unbounded reliable backlog.

Dead reckoning sends position, velocity, facing, movement mode, source tick, and
a bounded validity horizon for predictable remote motion. Update frequency can
fall toward 2 Hz for stable distant actors, but distance thresholds such as 50 m
are profile/game-design inputs. Abrupt acceleration, attack/cast starts,
projectiles, target/party relevance, boundary approach, error growth, or a camera
focus promotes updates immediately. The client clamps extrapolation and blends
or snaps according to an explicit correction/error budget; it never predicts
damage, death, objective ownership, or hidden state.

Record bytes before/after coding, datagrams, fragmentation, codec CPU, allocator
activity, expiry, retransmission/supersession, and visual error per tier. Packet
batching is successful only if it lowers total CPU/bandwidth without increasing
input latency or correction age beyond the playability budget.

### Authoritative battle-cell cluster

```text
Battle coordinator
  objective clock, cell directory, transfer epochs, global outcome reduction
       |
       +-- Cell A authoritative entities + read-only border ghosts
       +-- Cell B authoritative entities + read-only border ghosts
       +-- Cell C authoritative entities + read-only border ghosts
       +-- ...
       |
       +-- snapshot/aggregate compilers --> edge fan-out groups --> clients
```

- Partition by spatial interaction locality, not arbitrary player IDs. Each
  entity has exactly one writable cell and a monotonic ownership epoch.
- Neighboring cells receive read-only ghost borders. Cross-border attacks use a
  bounded command/acknowledgement protocol; they never create two writers.
- Use a data-oriented Rust simulation with structure-of-arrays storage, fixed
  steps, integer or deliberately controlled deterministic math, spatial hashes or
  BVHs, bounded job queues, and no general-purpose scene physics.
- Keep broad-phase work near O(N). Target selection, collision, aura, perception,
  projectile, and area-effect queries operate on local buckets and strict
  gameplay radii, not the whole army.
- Compile shared deltas once per interest bucket. Add small per-client private
  overlays instead of serializing ten thousand almost-identical snapshots.
- Separate movement, combat, AI, objective, persistence, and replication budgets.
  One saturated subsystem cannot consume the entire tick.
- Distant combat may be computed in formation batches only where the game design
  makes the aggregate result equivalent to allowed individual outcomes. A player
  entering or targeting that area promotes the relevant entities before precise
  resolution.
- Very large area effects use distributed spatial reduction with explicit caps
  and deterministic ordering. No spell scans all ten thousand entities every
  frame.

### Continuous spatial mesh and seamless ownership

"Seamless" is a player-experience invariant, not a claim that the backend is one
process. Under ordinary traversal the client keeps one gateway connection, one
continuous coordinate space, stable entity identities, and enough visual and
collision data to cross a server boundary without a loading screen. Internal
ownership may change invisibly behind that contract.

Prefer an immutable fine-cell lattice with stable IDs and catalog boundaries.
Build quadtrees, BVHs, or navigation/terrain topology indexes over those cells,
then dynamically assign connected groups of fine cells to macro-cell workers.
This is safer than moving arbitrary Voronoi polygons through live entities:
stable fine cells make catalog addressing, checkpoints, routing, tests, and
rollback tractable while still allowing elastic worker regions.

The partition controller minimizes a weighted interaction graph, not merely
player count. Its cost includes measured simulation microseconds, interacting
pairs, projectiles, ability/aura work, outgoing recipients/bytes, border traffic,
mailbox depth, and checkpoint size. It uses forecasts, minimum residence time,
hysteresis, spare capacity, and migration budgets. It prefers boundaries along
terrain occlusion, walls, rivers, empty ground, and objective-front topology,
then co-locates or merges microcells whose cross-boundary interaction cut becomes
expensive. Repartitioning faster than it can amortize migration is thrashing.

Elastic boundary collapse is therefore a useful option, not an escape from a
dense graph. A coordinator can group a combat island onto one large worker or
co-locate neighboring owners on one NUMA-aware host, reducing network crossings.
If that island exceeds one host and all members materially interact, another
partition cannot remove the underlying work; gameplay locality, fidelity tiers,
or admission must constrain it.

A dynamic quadtree can identify that one courtyard is hot; it does not make the
hot courtyard fit on one core. Ten thousand mutually interacting actors can leave
the candidate edge count near fifty million even after spatial indexing. Do not
assign one operating-system thread to every cell, and do not promise that a
high-density leaf is isolated to one dedicated core. A worker owns a connected
group of stable fine cells while deterministic tick phases run bounded parallel
jobs over SoA ranges: input application, broad phase, narrow phase, ability/event
resolution, state commit, relevance, and snapshot compilation. Cross-range
writes become sorted immutable intents that a deterministic reduction commits.
When the causal graph is too dense for that host, encounter affinity, ability
caps, aggregate-equivalent systems, or admission are correctness-preserving
tools; recursively drawing smaller rectangles is not.

Seamless handoff is a pre-copy plus tick-barrier protocol:

1. source announces entity ID, current owner/epoch, destination, transfer tick,
   catalog/simulation versions, and a hash of bounded state;
2. destination warms static data and receives a full snapshot plus subsequent
   deltas while the source remains sole writer;
3. gateway temporarily duplicates relevant commands to the transfer coordinator,
   but only the current epoch can commit them;
4. at the agreed tick barrier, the coordinator advances route and ownership
   epoch; destination starts writing from the exact accepted state;
5. source retains a time-bounded read-only ghost and forwards/rejects late old-
   epoch commands exactly once;
6. audits compare handoff hashes, command sequences, and conservation invariants
   before old state is retired.

The ghost-component pattern stores only the data each neighboring system is
allowed to read, with owner, epoch, authoritative tick, version, expiry, and
staleness class. Separate ghost components for transform/visibility, collision
bounds, combat targetability, and presentation prevent a convenient replica from
quietly becoming writable authority. Spatial auditing continuously checks unique
ownership, monotonic epochs, bounded staleness, orphan/duplicate IDs, transfer
hashes, and impossible cross-border outcomes.

Distributed combat should avoid a general distributed transaction per hit. Keep
projectiles and short-lived combat islands under one owner when feasible. A
cross-owner cast uses an idempotent protocol: the attacker owner reserves and
validates cast-side resources at a tick, the target owner validates target-side
state and commits damage once, and acknowledgements finalize or release the
reservation. Kill/reward/objective reducers consume committed events. A ghost may
support targeting preview or conservative broad phase, but it cannot author
health, death, loot, or ownership.

### Actor ownership, dynamic micro-shards, and ghosts

Use actors as an ownership and messaging model, not one asynchronous runtime task
per entity. A macro-cell actor owns an epoch and a data-oriented collection of
entities; batched jobs process movement, combat, replication, and spatial queries
inside that ownership boundary.

Each macro cell contains stable micro-shard coordinates and continuously records
measured cost: entities, interacting pairs, collision candidates, ability work,
snapshot recipients, bytes, mailbox depth, and tick time. A controller may split,
merge, or reassign micro-shards only at explicit tick/ownership epochs with
hysteresis and headroom. Population alone is not enough: five hundred idle
players and five hundred mutually fighting players have different costs.

Micro-shard handoff state machine:

1. source remains the sole writer and emits a versioned transfer snapshot;
2. destination validates catalog/simulation versions and prepares capacity;
3. coordinator advances the ownership epoch and routing table atomically;
4. destination becomes writer; source retains a bounded read-only ghost;
5. late commands carrying the old epoch are forwarded once or rejected; they are
   never applied by both owners;
6. acknowledgement and timeout rules retire source state or roll back to the
   previous single owner.

Every ghost records entity ID, owner cell, ownership epoch, authoritative tick,
state version, and an explicit maximum staleness budget. Ghosts are read-only.
Cross-border hits and transfers are commands to the owner, while speculative
border queries may use ghosts only where their staleness cannot change the
outcome. Economy, rewards, death, objective capture, and ownership never resolve
from a ghost copy.

Dynamic splitting cannot solve a fully connected hotspot: if every entity in one
micro-shard materially interacts with every other entity, moving half elsewhere
adds cross-shard traffic without reducing the interaction graph. Terrain lanes,
bounded effects, formation semantics, target limits, occlusion, and engaged-set
promotion are required to preserve locality.

If all ten thousand players intentionally occupy the same tiny collision volume
and can target, collide with, buff, and damage every other player at once, spatial
partitioning no longer helps. The game design must preserve locality through
collision rules, target/raid limits, objective fronts, terrain lanes, formation
mechanics, occlusion, and bounded effect radii. That is a correctness constraint,
not merely an optimization.

### Distributed Rust simulation process

The "mesh" is a fleet of single-writer simulation owners with explicit routes;
it is not a transparent service mesh making arbitrary synchronous calls between
entities. Keep the latency-sensitive execution path small:

```text
NIC queues
  -> bounded gateway decode/pacing workers (async I/O)
  -> per-cell tick inboxes
  -> fixed-step cell scheduler on pinned worker threads
  -> immutable public state pages + private overlays
  -> gateway fan-out/pacing workers (async I/O)

                         +-> persistence intent queues
                         +-> replay/telemetry queues
```

Tokio is appropriate for sockets, timers, admission/control APIs, and bounded
inter-process channels. Do not express every player, projectile, or status effect
as an independently scheduled Tokio task. The simulation core uses generational
entity handles, structure-of-arrays/archetype-like storage, deterministic command
buffers, preallocated scratch arenas, and coarse parallel jobs with explicit
barriers.

One tick has profiled, deterministic phases:

1. drain only commands admitted for this tick; sequence-check and canonicalize;
2. apply completed ownership handoffs and entity lifecycle commands;
3. integrate movement and update spatial indexes;
4. run broad-phase candidate generation and bounded narrow-phase queries;
5. resolve abilities, projectiles, auras, AI, deaths, and objectives in stable
   order, partitioning only independent work;
6. commit one new authoritative tick state and append audit/persistence intents;
7. build shared interest cohorts, deltas, aggregates, and private overlays;
8. publish immutable output pages to paced gateway queues and checkpoint jobs.

Every phase has a time, item, byte, and allocation budget. Inboxes and outboxes
are bounded; senders observe backpressure or explicit expiry instead of growing
memory. Cross-cell messages carry source/destination cell, ownership epoch,
simulation version, source tick, deadline, and idempotency key. A slow or failed
neighbor cannot block the local tick indefinitely.

Kernel bypass remains an ingress optimization. XDP rejects or steers before
socket work; AF_XDP may place selected frames into user-space rings. The QUIC
gateway still performs cryptography, connection recovery, congestion control,
protocol parsing, and admission. Benchmarks must compare ordinary UDP,
`SO_REUSEPORT`, io_uring where supported, Cilium native service acceleration,
custom XDP, and AF_XDP on the exact NIC/driver/NUMA topology before accepting the
operational burden.

#### SIMD and collision kernels

The performance target is not "evaluate 50 million collision permutations
faster." It is "do not generate 50 million irrelevant pairs." A linear spatial-
index update and bounded bucket queries should reduce each entity to a small,
measured candidate set. Static terrain/structures use prebuilt acceleration
data; moving capsules, projectiles, and areas use purpose-specific broad phases.

Within the remaining narrow phase, lay out homogeneous fields contiguously and
process batches large enough for LLVM auto-vectorization. Keep branches and
pointer chasing outside the inner loop, reuse aligned scratch storage, partition
work by cache/NUMA locality, and merge results in stable ID order. Inspect emitted
assembly and hardware counters; "written in Rust" does not itself prove SIMD.

Use a scalar reference implementation in differential/property/fuzz tests.
Where auto-vectorization is insufficient, add architecture-specific kernels
behind runtime feature detection and the same safe API. Ship explicit CPU tiers
and never execute unsupported instructions. Authoritative math must meet the
replay contract across every permitted tier; avoid unsafe fast-math assumptions,
unordered floating reductions, and architecture-dependent tie breaking.

Headless Unity remains a valid parity/prototyping or low-density server option,
not a universal trap. AnotherLife should select a custom Rust authoritative core
for the high-density target only after a loopback vertical slice proves gameplay
parity and profiles show the required control over memory, scheduling, network,
and deterministic replay. Keep one set of catalog/protocol vectors so two
implementations cannot silently invent different rules.

### Real-time and transactional state separation

Classify state by correctness and deadline rather than putting every record in
the fastest-looking product:

| State class | Owner while active | Durable path |
| --- | --- | --- |
| movement, cooldown clocks, projectiles, short buffs | authoritative cell memory | periodic/versioned recovery checkpoint where needed |
| combat and objective outcome pending commit | authoritative cell plus idempotent intent | transactional worker to SQL authority/outbox |
| inventory, currency, entitlement, progression | SQL authority | ACID transaction with invariants and operation ID |
| session presence, route hints, rate counters | gateway/cache projection | reconstruct or expire |
| replay segments, large checkpoints, crash data | producer buffer | asynchronous object storage with hash and metadata row |

Gameplay that transfers durable value uses a reservation/commit state machine or
waits for the database result; it never tells the client a permanent reward was
committed when only a cache or unflushed queue knows about it. Conversely, the
movement tick never waits synchronously for SQL, cache replication, object
storage, analytics, or a global control plane. Persistence workers use bounded
queues, idempotency keys, transactional outboxes, retries with jitter, dead-letter
review, and explicit admission/load shedding before their backlog threatens
recovery objectives.

### Unity rendering path

The current GameObject/Built-In pipeline is not the ten-thousand-character render
solution. Subject to the user's render-pipeline decision, build a dedicated crowd
renderer on URP using Entities Graphics/BatchRendererGroup or an equivalent
measured GPU-driven path:

- a small high-detail ring with normal skinned meshes, full materials, shadows,
  attachments, and readable silhouettes;
- a medium ring with aggressive mesh/bone/material LOD, GPU animation sampling,
  shared atlases, and restricted shadows;
- a large mass ring using indirect/instanced low-poly meshes or impostors, shared
  faction materials, deterministic variation, and no individual nameplates;
- frustum, distance, portal/terrain occlusion, and Hi-Z-style culling before draw
  submission;
- capped transparent VFX, lights, decals, projectiles, combat text, audio voices,
  and UI markers independent of player count;
- stable promotion/demotion with hysteresis so LOD changes do not flicker or
  expose network corrections.

Every target hardware tier needs its own measured caps. The low tier can show the
same battle truth through coarser representations without pretending to render
ten thousand AAA skinned characters.

Unity references:

- BatchRendererGroup operation: https://docs.unity3d.com/Manual/batch-renderer-group-how.html
- Entities Graphics performance and DOTS instancing: https://docs.unity.cn/Packages/com.unity.entities.graphics@1.0/manual/entities-graphics-performance.html
- Netcode relevancy and distance importance patterns: https://docs.unity.cn/Packages/com.unity.netcode@1.0/manual/optimizations.html

### Hybrid Unity DOTS client pipeline

DOTS is not currently installed in AnotherLife, and the project currently uses
the Built-In Render Pipeline. Do not add Entities, Burst, Entities Graphics, and
a render-pipeline migration to the playable-MVP critical path. First ship and
profile the correct GameObject terrain, champion, camera, physics, and combat
loop. Then build one isolated crowd benchmark and let the user make the visual
and render-pipeline decision with measured evidence.

The target is a hybrid boundary, not a dogmatic all-ECS rewrite:

```text
managed presentation/authoring                     data-oriented crowd world
hero, camera, input, UI, menus,         bridge     remote transforms, relevance,
quests, high-detail animation, VFX  <----------->  interpolation, culling, LOD,
editor and accessibility tools                    animation samples, draw data
                                                        |
                                                        v
                                      Burst jobs + Entities Graphics/BRG
```

Keep the locally controlled champion, camera, UI, designer-facing authoring, and
the nearest readable combatants on the most productive Unity path. Convert large
homogeneous remote populations and measured CPU hot loops into unmanaged ECS
components and Burst jobs. This preserves engine integration while removing
per-object managed overhead where population makes it material.

#### Structural data organization

- Components contain only the fields used together: quantized/current and prior
  transform samples, velocity, faction, relevance tier, animation state/time,
  render variant, health display state, owner/epoch, and interpolation flags.
- Do not put strings, managed object references, variable object graphs, or one
  giant component containing every gameplay field in a hot archetype.
- Use blobs/shared components only for immutable catalog/render definitions, and
  watch shared-component cardinality because every unique value can fragment
  chunks and batches.
- Separate frequently changing data from stable metadata. Group write patterns
  so one system does not dirty unrelated cache lines or upload unchanged render
  properties.
- Pre-size native collections and reuse command/snapshot/decode buffers. Dispose
  ownership explicitly; run leak detection and domain-reload tests.
- Stable network entity IDs map to ephemeral ECS entities through a bounded
  lookup. Network IDs are never array indexes and stale generations cannot
  mutate a recycled entity.

Archetype chunks are contiguous for matching component sets, but performance is
not automatic. Measure chunk occupancy, structural changes, cache misses, job
dependencies, main-thread sync points, upload bytes, and entity churn.

#### Client systems and interpolation

Process a received snapshot through explicit stages with no per-entity managed
allocation:

1. a bounded decoder writes validated native staging records;
2. identity/lifecycle applies spawn, despawn, ownership epoch, and catalog keys
   through an end-of-phase structural-change queue;
3. sample history records authoritative tick, position, rotation, velocity, and
   discontinuity flags in fixed-capacity buffers;
4. Burst jobs select interpolation samples and calculate render transforms in
   parallel using a presentation clock behind the server tick;
5. owned-character reconciliation remains separate from remote interpolation;
6. relevance and visibility jobs assign LOD/render tiers with hysteresis;
7. render extraction uploads only changed instance properties and batch commands.

Interpolation must understand teleports, cell handoffs, missing samples, clock
adjustment, and maximum extrapolation. It cannot blend across a discontinuity or
continue extrapolating indefinitely. Burst AOT-compiles eligible high-performance
C# to native code in player builds, but it does not eliminate all C# or replace
IL2CPP/Mono; managed presentation code remains valid outside the hot path.

The zero-GC objective applies to steady-state receive, interpolation, interest,
and crowd-render loops after warm-up. Prove it with profiler allocation counters
and long soaks. Avoid LINQ, closures, boxing, interpolated logging, temporary
arrays, managed events per entity, and `GetComponent` walks in those loops; do
not contort low-frequency tools or menus without evidence. Zero managed
allocations also cannot guarantee flat frame times: job fences, structural
changes, shader compilation, GPU saturation, upload/decompression, thermal
throttling, and operating-system scheduling remain separate measured budgets.

Unity references:

- Burst compilation and high-performance C# limits: https://docs.unity3d.com/Manual/script-compilation-burst.html
- NativeContainer and job safety: https://docs.unity3d.com/Manual/job-system-thread-safe-types.html
- Released Entities Graphics package information: https://docs.unity3d.com/Manual/com.unity.entities.graphics.html

### Blender-to-Unity VAT crowd pipeline

Vertex Animation Textures move repeated-character deformation from CPU skinning
to shader texture samples. They are appropriate for medium/far awareness and mass
tiers, not for every character at every distance. Nearby heroes retain skeletal
animation for high-quality blending, IK, ragdolls, facial motion, weapon sockets,
cloth, and arbitrary equipment.

The Blender bake contract is deterministic and catalog-driven:

- freeze an approved LOD mesh topology, vertex order, transforms, coordinate
  convention, bounds, root-motion convention, and clip list before baking;
- bake position and, only where the quality tier needs it, normal/tangent data
  for each sampled frame into importer-tested textures or texture arrays;
- export clip ID, frame count, sample rate, looping/event markers, texture
  dimensions/format, quantization scale/bias, bounds, source skeleton/version,
  mesh hash, and bake-tool version in a schema-validated manifest;
- keep root motion, gameplay hit timing, sockets, and authority outside the VAT
  pixels; VAT is presentation data;
- validate topology/hash compatibility, first/last loop continuity, bounds over
  all frames, NaN/range errors, shader decode parity, and deterministic rebakes;
- generate LOD-specific bakes rather than sampling a hero-resolution mesh at
  mass distance.

Memory is a first-class budget. An illustrative position-only clip containing
5,000 vertices, 120 frames, three 16-bit components consumes about 3.6 MB before
texture padding, mip policy, normals, or compression. Normals can roughly double
that, and many clips/variants multiply it. Reduce far-LOD vertices and sample
rates, share clips across compatible silhouettes, stream clip pages, and profile
real GPU formats on each target instead of baking every animation blindly.

One draw call is possible only for instances compatible with the same render
batch: mesh/submesh, shader pass, material/VAT set, texture layout, shadow state,
and platform limits. Per-instance structured data can select animation, phase,
palette, faction, and bounded variation without breaking the batch. Unique
materials, arbitrary armor meshes, transparent passes, decals, and attachments
create additional batches; design customization as instanced equipment/palette
sets and enforce a draw/instance budget per tier.

`Graphics.RenderMeshIndirect` submits one or more indirect draw commands in one
Unity API call for one mesh; it does not guarantee one hardware draw for an
entire heterogeneous army. Unity requires compute-shader support, and the
provided world bounds are used to cull and sort the submitted group as one
entity. AnotherLife must spatially bucket commands and measure false-positive
visibility, command count, material/pass splits, shadows, and platform fallback
rather than using "single draw call" as an acceptance claim.

The GPU shader samples two adjacent VAT frames, interpolates them, applies the
instance transform, and consumes per-instance clip/time/variation properties.
Use Entities Graphics, BatchRendererGroup, or a proven indirect path after the
render-pipeline decision. Frustum/occlusion/distance culling happens before draw
submission, and off-screen instances do not advance expensive presentation
work. Visual events are promoted to skeletal/high-detail actors before their
exact pose, socket, or interaction can matter.

Acceptance for the crowd lane is a reproducible hardware-tier matrix, not a
marketing screenshot: visible count, draw calls, batches, main/render-thread
milliseconds, GPU milliseconds, upload bytes, resident VAT memory, frame-time
p95/p99, thermal behavior, and perceptual defects at 128, 512, 2,000, 5,000, and
10,000 representatives.

Unity indirect-rendering reference:
https://docs.unity3d.com/ScriptReference/Graphics.RenderMeshIndirect.html

### Asynchronous world and identity streaming

The visual world uses stable catalogued chunks and several independently
budgeted residency rings. Ring distances are profiles, not laws: a 500 m active
physics radius and a 1.5 km detailed streaming radius would be excessive on many
mobile devices and insufficient for a fast mount or aerial camera. Compute the
look-ahead horizon from velocity, route, camera, download/decode percentiles,
storage speed, memory pressure, and platform quality.

| Residency tier | Required content and behavior |
| --- | --- |
| continuity core | shipped low-detail terrain/horizon, collision fallback, shared faction characters, UI, and recovery content needed even offline |
| interaction ring | current/next fine terrain, structures, gameplay collision, nav/query data, high-priority actors, audio/VFX needed for imminent play |
| prefetch ring | likely route-adjacent chunk bundles and medium LODs downloaded/decoded under byte, CPU, memory, and cancellation budgets |
| horizon ring | coarse terrain silhouettes, landmark HLODs/impostors, sky/weather, and aggregate armies with no fine object graph |

Use additive scenes and Addressables after its production loader is installed and
tested. Publish immutable, content-addressed, platform-specific bundles plus a
small signed/versioned catalog. SeaweedFS can be the S3-compatible origin and a
CDN can cache public immutable objects, but clients never browse the filer or
receive storage credentials. Catalog activation verifies signature/hash,
compatibility, disk budget, and rollback; an interrupted update retains the last
known-good catalog.

Do not write bespoke `UnityWebRequest` calls throughout gameplay. A streaming
service owns request coalescing, priorities, retry/backoff, cancellation, cache
leases, dependency handles, memory admission, telemetry, and symmetric unload.
Downloading is asynchronous, but Unity object integration is not magically free:
`Object.InstantiateAsync` still performs its final integration and `Awake` calls
on the main thread. Bound activation count/time per frame, use Unity's async mesh/
texture upload time slice, avoid activation during combat-critical frames, and
test decompression and shader warm-up spikes on cold low-end hardware.

"No loading screen" means normal walking never intentionally zones. It cannot
guarantee that an uncached client on a failed network can outrun missing bytes.
The continuity core must always render and collide safely; quality stays coarse,
prefetch distance grows, travel speed is conservatively bounded, or entry into an
unavailable chunk is denied with an in-world recovery state rather than exposing
void, falling through terrain, or blocking the main thread.

Player appearance is catalog composition, not ten thousand unique texture
downloads. A prioritizer ranks identity by gameplay causality, group/guild,
target/attacker, screen coverage/frustum, distance, recent interaction, and
download cost. It guarantees fairness/identity minimums, then spends a per-frame
and per-second budget:

1. nearby/engaged players use cached modular body, armor, weapon, dye/palette,
   heraldry, and attachment sets;
2. awareness players use reduced shared equipment/material combinations;
3. mass players use stable faction/role silhouettes with VAT or impostors;
4. promotion preserves entity ID, transform, action phase, faction, and selected
   silhouette while swapping presentation only.

Custom colors and heraldry should normally be small validated parameters or
atlas references. User-generated images, if ever allowed, need moderation,
sanitization, transcoding, strict dimensions/formats, privacy controls, and a
separate cache; they never enter a shader or decoder as arbitrary source bytes.

World-streaming acceptance includes cold/warm/offline traversal, rapid reversal,
teleport/failure recovery, corrupt/hostile bundles, version mismatch, CDN/origin
loss, disk-full, low-memory eviction, cancellation races, and repeated
load/unload leak soaks. Record time-to-safe-collision, proxy-to-final visual
latency, frame spikes, resident/allocated memory, GPU upload time, cache hit rate,
download bytes, and orphaned handles.

Unity references:

- Addressables remote bundle caching: https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/remote-content-assetbundle-cache.html
- Addressables content-update workflow: https://docs.unity3d.com/Packages/com.unity.addressables@1.21/manual/content-update-builds-overview.html
- Unity 6 asynchronous instantiation boundary: https://docs.unity3d.com/ScriptReference/Object.InstantiateAsync.html
- Per-frame asynchronous upload budget: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/QualitySettings-asyncUploadTimeSlice.html

### Capacity degradation order

No time dilation means the authoritative tick never intentionally stretches to
hide overload. When a budget is threatened, degrade or shed work in this order:

1. drop expired packets and distant cosmetic events;
2. lower mass/awareness update frequency and increase aggregate size;
3. reduce client animation, shadow, VFX, audio, and UI fidelity;
4. defer non-critical persistence, analytics, social, and replay work through
   bounded durable queues;
5. stop new arrivals, preserve reconnect reservations, or route them to another
   front before the battle cell exceeds its tested envelope;
6. fail closed on value mutations and competitive outcomes.

Never let an unbounded queue accumulate tick debt. A missed real-time deadline is
an observed failure with load-shedding action, not a reason to slow game time.

## Millions of concurrent active players

Millions online is a fleet/control-plane problem layered above the battle-cell
problem:

- regional QUIC edge gateways terminate sessions, mitigate abuse, negotiate
  protocol versions, and route players to authoritative cells;
- a global directory maps account, character, realm, and active ownership epoch
  without becoming the gameplay data path;
- many independently scalable world-cell clusters own active spatial regions;
- warm capacity and admission control absorb bursts; Agones or an equivalent
  fleet manager may provision processes, but it does not make one hot simulation
  scale automatically;
- durable player authority is partitioned by an explicit home/owner. Avoid global
  consensus for routine per-region play even if TiDB is selected;
- social, telemetry, leaderboard, mail, and analytics projections are asynchronous
  and cannot block combat;
- caches, databases, event workers, object storage, and observability scale on
  their own measured workloads;
- regional failure has tested reconnect, evacuation, checkpoint, and recovery
  behavior rather than silently duplicating live ownership.

Use aggregate capacity math, not a single headline CCU number. Record active
connections per gateway, players/entities per cell, battle cells per host, bytes
per client, messages per second, authoritative tick cost, persistence operations,
and failure-domain headroom. One million players spread over two thousand cells
is a different problem from one million players in one interaction set.

Agones can maintain warm fleets and allocate/autoscale game-server processes; its
scope is orchestration, not distributed simulation correctness:

- Fleet allocation: https://agones.dev/site/docs/integration-patterns/allocation-from-fleet/
- Fleet autoscaling: https://agones.dev/site/docs/reference/fleetautoscaler/

### Scale verification gates

- A deterministic headless bot harness drives representative movement, combat,
  skills, objectives, reconnects, and adversarial inputs—not idle sockets.
- The ten-thousand-participant battle soak holds the chosen real-time tick budget
  for at least a full battle while reporting p50/p95/p99/max per subsystem.
- Tests inject packet loss, jitter, duplication, reordering, slow clients, cell
  loss, gateway loss, cache loss, persistence delay, leader movement, and region
  evacuation.
- Each client tier meets its frame, memory, thermal, bandwidth, and visual-density
  envelope with ten thousand perceptual participants.
- Promotion from aggregate to individual state is identity-stable, authoritative,
  cheat-resistant, and visually continuous.
- Economy and progression remain exactly-once at the domain level during retries,
  transfers, failover, and replay.
- Capacity tests prove the overload degradation order and that time never slows.

## Delivery sequence

1. Finish a stable offline playable slice with real terrain, collision, champion
   movement, MMO camera, combat loop, and deterministic receipts.
2. Run the authoritative Rust simulation locally with a loopback transport; keep
   Unity prediction and reconciliation observable.
3. Add one regional environment with gateway, one world cell, CNPG, and optional
   cache. Exercise reconnects, duplicate commands, rollback, and old clients.
4. Add load generation, failure injection, database restore drills, protocol
   fuzzing, and RvR-sized interest-management benchmarks.
5. Introduce SeaweedFS only when object volume or sovereignty requirements justify
   operating it. Otherwise use a managed S3-compatible target behind the same API.
6. Split world cells, persistence workers, or social services only from measured
   scaling and fault-isolation needs.

## Required verification gates

- Deterministic simulation replay produces the same authoritative result.
- Golden protocol fixtures pass across supported old/new version pairs.
- Malformed, oversized, duplicated, delayed, reordered, and replayed traffic is
  bounded and rejected safely.
- Client prediction converges after correction under representative latency,
  jitter, loss, and reconnects.
- No cache outage can mint, lose, or duplicate durable value.
- PostgreSQL failover and point-in-time restore meet recorded objectives.
- Object-store node loss, metadata recovery, and blob restore are rehearsed.
- Native ABI tests run for every target architecture; managed fallback remains
  behaviorally equivalent.
- RvR load tests report tick time, queue depth, bandwidth, memory, database
  pressure, cache behavior, and degraded-mode correctness.
