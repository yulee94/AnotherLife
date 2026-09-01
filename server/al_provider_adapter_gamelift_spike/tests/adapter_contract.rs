use al_provider_adapter_gamelift_spike::{
    GameLiftApi, GameLiftApiError, GameLiftApiFailure, GameLiftCapacitySnapshot,
    GameLiftLifecycleAction, GameLiftLifecycleSnapshot, GameLiftPlacementSnapshot,
    GameLiftPlacementState, GameLiftSpikeAdapter, GameLiftSpikeConfig, GameLiftStartPlacement,
};
use al_provider_adapter_stub::{StubProviderAdapter, StubProviderScope};
use al_server_core::provider_contracts::{
    AccountId, ActorId, AdapterFailureClass, AdapterObservation, AdapterObserver,
    AdapterRequestContext, AllocationId, ArtifactFingerprint, AuthorizationContextId,
    CompatibilityFingerprint, ContractId, CorrelationId, DeploymentAdapter, LifecycleStatus,
    OperationId, OperationsAdapter, PlacementAdapter, PlacementRequest, PlacementStatus,
    PolicyVersion, ProcessLifecycleRequest, RealmId, RegionId, SchemaVersion, ServiceId, SessionId,
};

#[derive(Default)]
struct RecordingObserver(Vec<AdapterObservation>);

impl AdapterObserver for RecordingObserver {
    fn record(&mut self, observation: AdapterObservation) {
        self.0.push(observation);
    }
}

#[derive(Debug, Default)]
struct RecordingApi {
    starts: Vec<GameLiftStartPlacement>,
    describes: usize,
    stops: usize,
    lifecycle_actions: Vec<GameLiftLifecycleAction>,
    lifecycle_describes: usize,
    start_failure: Option<GameLiftApiError>,
    stop_failure: Option<GameLiftApiError>,
    lifecycle_failure: Option<GameLiftApiError>,
    lifecycle_snapshot: Option<GameLiftLifecycleSnapshot>,
    lifecycle_describe_snapshot: Option<GameLiftLifecycleSnapshot>,
    start_snapshot: Option<GameLiftPlacementSnapshot>,
    describe_snapshots: Vec<GameLiftPlacementSnapshot>,
    describe_snapshot: Option<GameLiftPlacementSnapshot>,
    stop_snapshot: Option<GameLiftPlacementSnapshot>,
    capacity_snapshot: Option<GameLiftCapacitySnapshot>,
    lose_start_response_after_accept: bool,
}

impl GameLiftApi for RecordingApi {
    fn start_game_session_placement(
        &mut self,
        request: GameLiftStartPlacement,
    ) -> Result<GameLiftPlacementSnapshot, GameLiftApiError> {
        self.starts.push(request);
        if let Some(error) = self.start_failure {
            return Err(error);
        }
        if self.lose_start_response_after_accept {
            return Err(GameLiftApiError::new(
                GameLiftApiFailure::AmbiguousCompletion,
            ));
        }
        if let Some(snapshot) = self.start_snapshot {
            return Ok(snapshot);
        }
        Ok(GameLiftPlacementSnapshot::new(
            request.placement_token(),
            request.placement_token(),
            request.region_id(),
            request.realm_id(),
            request.artifact(),
            request.compatibility(),
            GameLiftPlacementState::Pending,
        ))
    }

    fn describe_game_session_placement(
        &mut self,
        placement_token: u64,
    ) -> Result<GameLiftPlacementSnapshot, GameLiftApiError> {
        self.describes += 1;
        if !self.describe_snapshots.is_empty() {
            return Ok(self.describe_snapshots.remove(0));
        }
        if let Some(snapshot) = self.describe_snapshot {
            return Ok(snapshot);
        }
        Ok(GameLiftPlacementSnapshot::new(
            placement_token,
            placement_token,
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Ready {
                lease_epoch: 1,
                endpoint_token: placement_token + 100,
            },
        ))
    }

    fn stop_game_session_placement(
        &mut self,
        placement_token: u64,
    ) -> Result<GameLiftPlacementSnapshot, GameLiftApiError> {
        self.stops += 1;
        if let Some(error) = self.stop_failure {
            return Err(error);
        }
        if let Some(snapshot) = self.stop_snapshot {
            return Ok(snapshot);
        }
        Ok(GameLiftPlacementSnapshot::new(
            placement_token,
            placement_token,
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Cancelled,
        ))
    }

    fn apply_lifecycle(
        &mut self,
        action: GameLiftLifecycleAction,
    ) -> Result<GameLiftLifecycleSnapshot, GameLiftApiError> {
        self.lifecycle_actions.push(action);
        if let Some(error) = self.lifecycle_failure {
            return Err(error);
        }
        if let Some(snapshot) = self.lifecycle_snapshot {
            return Ok(snapshot);
        }
        Ok(GameLiftLifecycleSnapshot::new(
            action.fleet_token(),
            action.request(),
            LifecycleStatus::Complete,
        ))
    }

    fn describe_lifecycle(
        &mut self,
        action: GameLiftLifecycleAction,
    ) -> Result<GameLiftLifecycleSnapshot, GameLiftApiError> {
        self.lifecycle_describes += 1;
        if let Some(snapshot) = self.lifecycle_describe_snapshot {
            return Ok(snapshot);
        }
        if let Some(snapshot) = self.lifecycle_snapshot {
            return Ok(snapshot);
        }
        Ok(GameLiftLifecycleSnapshot::new(
            action.fleet_token(),
            action.request(),
            LifecycleStatus::Complete,
        ))
    }

    fn describe_capacity(&mut self) -> Result<GameLiftCapacitySnapshot, GameLiftApiError> {
        if let Some(snapshot) = self.capacity_snapshot {
            return Ok(snapshot);
        }
        Ok(GameLiftCapacitySnapshot::new(
            required(3, RegionId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            1,
            0,
            0,
        ))
    }
}

fn required<T>(value: u64, constructor: fn(u64) -> Option<T>) -> T {
    constructor(value).expect("test identifier must be nonzero")
}

fn config() -> GameLiftSpikeConfig {
    GameLiftSpikeConfig::new(
        required(3, RegionId::new),
        required(5, ArtifactFingerprint::new),
        required(19, CompatibilityFingerprint::new),
        7001,
        8001,
    )
}

fn context(operation_id: u64, attempt: u32) -> AdapterRequestContext {
    AdapterRequestContext::new(
        ContractId::Placement,
        required(operation_id, OperationId::new),
        required(12, CorrelationId::new),
        required(13, ActorId::new),
        required(14, ServiceId::new),
        required(15, AuthorizationContextId::new),
        required(16, PolicyVersion::new),
        required(3, RegionId::new),
        Some(required(4, RealmId::new)),
        required(17, SchemaVersion::new),
        required(5, ArtifactFingerprint::new),
        required(19, CompatibilityFingerprint::new),
        attempt,
    )
}

fn deployment_context(operation_id: u64, attempt: u32) -> AdapterRequestContext {
    AdapterRequestContext::new(
        ContractId::Deployment,
        required(operation_id, OperationId::new),
        required(12, CorrelationId::new),
        required(13, ActorId::new),
        required(14, ServiceId::new),
        required(15, AuthorizationContextId::new),
        required(16, PolicyVersion::new),
        required(3, RegionId::new),
        None,
        required(17, SchemaVersion::new),
        required(5, ArtifactFingerprint::new),
        required(19, CompatibilityFingerprint::new),
        attempt,
    )
}

fn capacity_context(operation_id: u64, attempt: u32) -> AdapterRequestContext {
    AdapterRequestContext::new(
        ContractId::Capacity,
        required(operation_id, OperationId::new),
        required(12, CorrelationId::new),
        required(13, ActorId::new),
        required(14, ServiceId::new),
        required(15, AuthorizationContextId::new),
        required(16, PolicyVersion::new),
        required(3, RegionId::new),
        None,
        required(17, SchemaVersion::new),
        required(5, ArtifactFingerprint::new),
        required(19, CompatibilityFingerprint::new),
        attempt,
    )
}

fn request(realm: u64) -> PlacementRequest {
    PlacementRequest::new(
        required(1, AccountId::new),
        required(2, SessionId::new),
        required(3, RegionId::new),
        required(realm, RealmId::new),
        required(5, ArtifactFingerprint::new),
    )
}

#[test]
fn placement_keeps_preassigned_region_and_realm_and_deduplicates() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();

    let first = adapter
        .submit(context(101, 0), request(4), &mut observer)
        .expect("first placement must be accepted");
    let duplicate = adapter
        .submit(context(101, 1), request(4), &mut observer)
        .expect("same operation retry must deduplicate");

    assert_eq!(first, duplicate);
    assert_eq!(adapter.api().starts.len(), 1);
    assert_eq!(
        adapter.api().starts[0].region_id(),
        required(3, RegionId::new)
    );
    assert_eq!(
        adapter.api().starts[0].realm_id(),
        required(4, RealmId::new)
    );
}

#[test]
fn payload_or_scope_drift_conflicts_before_second_provider_call() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    adapter
        .submit(context(102, 0), request(4), &mut observer)
        .expect("first placement must be accepted");

    let error = adapter
        .submit(context(102, 1), request(9), &mut observer)
        .expect_err("changed realm under one operation must conflict");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
    assert_eq!(adapter.api().starts.len(), 1);
}

#[test]
fn cross_region_request_fails_closed_before_provider_call() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    let cross_region = PlacementRequest::new(
        required(1, AccountId::new),
        required(2, SessionId::new),
        required(30, RegionId::new),
        required(4, RealmId::new),
        required(5, ArtifactFingerprint::new),
    );

    let error = adapter
        .submit(context(103, 0), cross_region, &mut observer)
        .expect_err("provider must not choose another region");

    assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
    assert!(adapter.api().starts.is_empty());
}

#[test]
fn provider_throttle_maps_to_stable_failure_class() {
    let api = RecordingApi {
        start_failure: Some(GameLiftApiError::new(GameLiftApiFailure::Throttled)),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();

    let error = adapter
        .submit(context(104, 0), request(4), &mut observer)
        .expect_err("provider throttle must remain explicit");

    assert_eq!(error.class(), AdapterFailureClass::Throttled);
}

#[test]
fn invalid_provider_start_snapshot_fails_closed() {
    let api = RecordingApi {
        start_snapshot: Some(GameLiftPlacementSnapshot::new(
            110,
            110,
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Ready {
                lease_epoch: 0,
                endpoint_token: 0,
            },
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();

    let error = adapter
        .submit(context(110, 0), request(4), &mut observer)
        .expect_err("invalid provider snapshot must not create a receipt");

    assert_eq!(error.class(), AdapterFailureClass::Internal);
}

#[test]
fn provider_start_snapshot_scope_mismatch_fails_closed() {
    let api = RecordingApi {
        start_snapshot: Some(GameLiftPlacementSnapshot::new(
            111,
            111,
            required(3, RegionId::new),
            required(9, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Pending,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();

    let error = adapter
        .submit(context(111, 0), request(4), &mut observer)
        .expect_err("provider snapshot scope must match the authorized request");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
}

#[test]
fn ambiguous_start_requires_describe_reconciliation_before_retry() {
    let api = RecordingApi {
        lose_start_response_after_accept: true,
        describe_snapshot: Some(GameLiftPlacementSnapshot::new(
            105,
            999,
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Ready {
                lease_epoch: 1,
                endpoint_token: 205,
            },
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();

    let error = adapter
        .submit(context(105, 0), request(4), &mut observer)
        .expect_err("lost response must be ambiguous");
    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);

    let status = adapter
        .reconcile_operation(
            context(105, 1),
            required(105, OperationId::new),
            &mut observer,
        )
        .expect("reconciliation must query the original placement identity");
    assert!(matches!(status, PlacementStatus::Ready { .. }));
    let receipt = adapter
        .submit(context(105, 2), request(4), &mut observer)
        .expect("retry after reconciliation returns the resolved allocation identity");
    assert_eq!(receipt.allocation_id(), required(999, AllocationId::new));
    assert_eq!(adapter.api().starts.len(), 1);
    assert_eq!(adapter.api().describes, 1);
}

#[test]
fn placement_status_uses_its_own_operation_identity() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    let receipt = adapter
        .submit(context(106, 0), request(4), &mut observer)
        .expect("placement must be accepted");

    let status = adapter
        .status(context(107, 0), receipt, &mut observer)
        .expect("status request has its own operation identity");

    assert!(matches!(status, PlacementStatus::Ready { .. }));
    assert_eq!(adapter.api().describes, 1);
}

#[test]
fn placement_status_rejects_mismatched_provider_snapshot() {
    let api = RecordingApi {
        describe_snapshot: Some(GameLiftPlacementSnapshot::new(
            999,
            999,
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Pending,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();
    let receipt = adapter
        .submit(context(108, 0), request(4), &mut observer)
        .expect("placement must be accepted");

    let error = adapter
        .status(context(109, 0), receipt, &mut observer)
        .expect_err("provider snapshot identity must match the requested placement");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
}

#[test]
fn lifecycle_payload_drift_conflicts_before_second_provider_call() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    let ensure_ready = ProcessLifecycleRequest::EnsureReady {
        region_id: required(3, RegionId::new),
        artifact: required(5, ArtifactFingerprint::new),
    };
    adapter
        .submit_lifecycle(deployment_context(201, 0), ensure_ready, &mut observer)
        .expect("first lifecycle operation must complete");
    let drifted = ProcessLifecycleRequest::Drain {
        region_id: required(3, RegionId::new),
        artifact: required(5, ArtifactFingerprint::new),
    };

    let error = adapter
        .submit_lifecycle(deployment_context(201, 1), drifted, &mut observer)
        .expect_err("changed lifecycle payload must conflict");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
    assert_eq!(adapter.api().lifecycle_actions.len(), 1);
}

#[test]
fn lifecycle_request_scope_mismatch_fails_before_provider_call() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    let cross_region = ProcessLifecycleRequest::Drain {
        region_id: required(30, RegionId::new),
        artifact: required(5, ArtifactFingerprint::new),
    };

    let error = adapter
        .submit_lifecycle(deployment_context(202, 0), cross_region, &mut observer)
        .expect_err("lifecycle payload scope must match its authorized context");

    assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
    assert!(adapter.api().lifecycle_actions.is_empty());
}

#[test]
fn lifecycle_rejects_provider_response_for_another_request() {
    let requested = ProcessLifecycleRequest::EnsureReady {
        region_id: required(3, RegionId::new),
        artifact: required(5, ArtifactFingerprint::new),
    };
    let api = RecordingApi {
        lifecycle_snapshot: Some(GameLiftLifecycleSnapshot::new(
            8001,
            ProcessLifecycleRequest::Drain {
                region_id: required(3, RegionId::new),
                artifact: required(5, ArtifactFingerprint::new),
            },
            LifecycleStatus::Complete,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();

    let error = adapter
        .submit_lifecycle(deployment_context(205, 0), requested, &mut observer)
        .expect_err("provider lifecycle response must match the submitted request");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
}

#[test]
fn ambiguous_lifecycle_retry_reconciles_without_reissuing_provider_operation() {
    let api = RecordingApi {
        lifecycle_failure: Some(GameLiftApiError::new(
            GameLiftApiFailure::AmbiguousCompletion,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();
    let request = ProcessLifecycleRequest::EnsureReady {
        region_id: required(3, RegionId::new),
        artifact: required(5, ArtifactFingerprint::new),
    };

    let first = adapter
        .submit_lifecycle(deployment_context(203, 0), request, &mut observer)
        .expect_err("lost lifecycle response must be ambiguous");
    assert_eq!(first.class(), AdapterFailureClass::AmbiguousCompletion);
    let receipt = adapter
        .submit_lifecycle(deployment_context(203, 1), request, &mut observer)
        .expect("retry must reconcile to the recorded operation without replay");
    let status = adapter
        .lifecycle_status(deployment_context(204, 0), receipt, &mut observer)
        .expect("ambiguous lifecycle state must remain inspectable");

    assert_eq!(status, LifecycleStatus::Complete);
    assert_eq!(adapter.api().lifecycle_actions.len(), 1);
    assert_eq!(adapter.api().lifecycle_describes, 1);
}

#[test]
fn pending_lifecycle_status_refreshes_from_provider() {
    let request = ProcessLifecycleRequest::EnsureReady {
        region_id: required(3, RegionId::new),
        artifact: required(5, ArtifactFingerprint::new),
    };
    let api = RecordingApi {
        lifecycle_snapshot: Some(GameLiftLifecycleSnapshot::new(
            8001,
            request,
            LifecycleStatus::Pending,
        )),
        lifecycle_describe_snapshot: Some(GameLiftLifecycleSnapshot::new(
            8001,
            request,
            LifecycleStatus::Complete,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();
    let receipt = adapter
        .submit_lifecycle(deployment_context(206, 0), request, &mut observer)
        .expect("pending lifecycle operation must return a receipt");

    let status = adapter
        .lifecycle_status(deployment_context(207, 0), receipt, &mut observer)
        .expect("status must refresh pending provider state");

    assert_eq!(status, LifecycleStatus::Complete);
    assert_eq!(adapter.api().lifecycle_describes, 1);
}

#[test]
fn cancellation_uses_its_own_idempotent_operation_identity() {
    let mut adapter = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    let receipt = adapter
        .submit(context(301, 0), request(4), &mut observer)
        .expect("placement must be accepted");

    let first = adapter
        .cancel(context(302, 0), receipt, &mut observer)
        .expect("cancellation uses a separate operation identity");
    let duplicate = adapter
        .cancel(context(302, 1), receipt, &mut observer)
        .expect("cancellation retry must deduplicate");

    assert_eq!(first, PlacementStatus::Cancelled);
    assert_eq!(duplicate, PlacementStatus::Cancelled);
    assert_eq!(adapter.api().stops, 1);
}

#[test]
fn ambiguous_cancellation_retry_reconciles_without_reissuing_provider_operation() {
    let api = RecordingApi {
        stop_failure: Some(GameLiftApiError::new(
            GameLiftApiFailure::AmbiguousCompletion,
        )),
        describe_snapshots: vec![
            GameLiftPlacementSnapshot::new(
                303,
                303,
                required(3, RegionId::new),
                required(4, RealmId::new),
                required(5, ArtifactFingerprint::new),
                required(19, CompatibilityFingerprint::new),
                GameLiftPlacementState::Pending,
            ),
            GameLiftPlacementSnapshot::new(
                303,
                303,
                required(3, RegionId::new),
                required(4, RealmId::new),
                required(5, ArtifactFingerprint::new),
                required(19, CompatibilityFingerprint::new),
                GameLiftPlacementState::Cancelled,
            ),
        ],
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();
    let receipt = adapter
        .submit(context(303, 0), request(4), &mut observer)
        .expect("placement must be accepted");

    let first = adapter
        .cancel(context(304, 0), receipt, &mut observer)
        .expect_err("lost cancellation response must be ambiguous");
    assert_eq!(first.class(), AdapterFailureClass::AmbiguousCompletion);
    let pending = adapter
        .cancel(context(304, 1), receipt, &mut observer)
        .expect("first retry must reconcile pending provider state without replay");
    let status = adapter
        .cancel(context(304, 2), receipt, &mut observer)
        .expect("later retry must observe eventual provider cancellation");

    assert_eq!(pending, PlacementStatus::Pending);
    assert_eq!(status, PlacementStatus::Cancelled);
    assert_eq!(adapter.api().stops, 1);
    assert_eq!(adapter.api().describes, 2);
}

#[test]
fn cancellation_rejects_provider_response_that_is_not_cancelled() {
    let api = RecordingApi {
        stop_snapshot: Some(GameLiftPlacementSnapshot::new(
            305,
            305,
            required(3, RegionId::new),
            required(4, RealmId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            GameLiftPlacementState::Pending,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();
    let receipt = adapter
        .submit(context(305, 0), request(4), &mut observer)
        .expect("placement must be accepted");

    let error = adapter
        .cancel(context(306, 0), receipt, &mut observer)
        .expect_err("stop response must confirm cancellation");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
}

#[test]
fn capacity_rejects_provider_response_for_another_scope() {
    let api = RecordingApi {
        capacity_snapshot: Some(GameLiftCapacitySnapshot::new(
            required(30, RegionId::new),
            required(5, ArtifactFingerprint::new),
            required(19, CompatibilityFingerprint::new),
            1,
            0,
            0,
        )),
        ..RecordingApi::default()
    };
    let mut adapter = GameLiftSpikeAdapter::new(config(), api);
    let mut observer = RecordingObserver::default();

    let error = adapter
        .observe_capacity(
            capacity_context(401, 0),
            required(3, RegionId::new),
            required(5, ArtifactFingerprint::new),
            &mut observer,
        )
        .expect_err("provider capacity scope must match the authorized request");

    assert_eq!(error.class(), AdapterFailureClass::Conflict);
}

#[test]
fn disabling_candidate_restores_neutral_adapter_without_state_rewrite() {
    let state_hash_before = [0x5a_u8; 32];
    let mut candidate = GameLiftSpikeAdapter::new(config(), RecordingApi::default());
    let mut observer = RecordingObserver::default();
    candidate
        .submit(context(106, 0), request(4), &mut observer)
        .expect("candidate placement must be accepted");
    drop(candidate);

    let mut neutral = StubProviderAdapter::new(StubProviderScope::new(
        required(3, RegionId::new),
        required(5, ArtifactFingerprint::new),
        required(19, CompatibilityFingerprint::new),
    ));
    let neutral_receipt = neutral
        .submit(context(107, 0), request(4), &mut observer)
        .expect("neutral path must operate after candidate removal");

    assert_eq!(
        neutral_receipt.allocation_id(),
        required(107, AllocationId::new)
    );
    assert_eq!(state_hash_before, [0x5a_u8; 32]);
}
