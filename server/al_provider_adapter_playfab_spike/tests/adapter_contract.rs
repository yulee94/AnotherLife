use al_provider_adapter_playfab_spike::{
    map_playfab_failure, PlayFabAdapter, PlayFabApi, PlayFabApiError, PlayFabCapacity,
    PlayFabScope, PlayFabServer, PlayFabServerDetails, PlayFabServerState,
    SyntheticSandboxAuthorization,
};
use al_server_core::provider_contracts::{
    AccountId, ActorId, AdapterError, AdapterFailureClass, AdapterObservation, AdapterObserver,
    AdapterRequestContext, AllocationId, ArtifactFingerprint, AuthorizationContextId,
    CompatibilityFingerprint, ContractId, CorrelationId, DeploymentAdapter, LifecycleStatus,
    OperationId, OperationsAdapter, PlacementAdapter, PlacementRequest, PlacementStatus,
    PolicyVersion, RealmId, RegionId, SchemaVersion, ServiceId, SessionId,
};
use std::cell::RefCell;
use std::rc::Rc;
use std::sync::Once;

#[derive(Default)]
struct Observer(Vec<AdapterObservation>);

impl AdapterObserver for Observer {
    fn record(&mut self, observation: AdapterObservation) {
        self.0.push(observation);
    }
}

#[derive(Default)]
struct CallCounts {
    request_calls: usize,
    status_calls: usize,
    shutdown_calls: usize,
    build_calls: usize,
    capacity_calls: usize,
    title_ids: Vec<String>,
    shutdown_regions: Vec<String>,
}

#[derive(Default)]
struct FakeApi {
    calls: Rc<RefCell<CallCounts>>,
    server: Option<PlayFabServer>,
    detail_servers: Vec<PlayFabServer>,
    build_states: Vec<bool>,
    request_error: Option<PlayFabApiError>,
    details_error: Option<PlayFabApiError>,
    absent_details_remaining: usize,
    shutdown_error: Option<PlayFabApiError>,
}

impl PlayFabApi for FakeApi {
    fn request_server(
        &mut self,
        title_id: &str,
        _build_id: &str,
        session_id: &str,
        region: &str,
    ) -> Result<PlayFabServer, PlayFabApiError> {
        let mut calls = self.calls.borrow_mut();
        calls.request_calls += 1;
        calls.title_ids.push(title_id.to_owned());
        drop(calls);
        if let Some(error) = self.request_error.take() {
            return Err(error);
        }
        Ok(self.server.clone().unwrap_or_else(|| PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: session_id.to_owned(),
            region: region.to_owned(),
            state: PlayFabServerState::Active,
        }))
    }

    fn server_details(
        &mut self,
        title_id: &str,
        _build_id: &str,
        session_id: &str,
        region: &str,
    ) -> Result<PlayFabServerDetails, PlayFabApiError> {
        let mut calls = self.calls.borrow_mut();
        calls.status_calls += 1;
        calls.title_ids.push(title_id.to_owned());
        drop(calls);
        if let Some(error) = self.details_error.take() {
            return Err(error);
        }
        if self.absent_details_remaining > 0 {
            self.absent_details_remaining -= 1;
            return Ok(PlayFabServerDetails::Absent);
        }
        if !self.detail_servers.is_empty() {
            return Ok(PlayFabServerDetails::Found(self.detail_servers.remove(0)));
        }
        Ok(PlayFabServerDetails::Found(
            self.server.clone().unwrap_or_else(|| PlayFabServer {
                server_id: "provider-private-server".to_owned(),
                session_id: session_id.to_owned(),
                region: region.to_owned(),
                state: PlayFabServerState::Active,
            }),
        ))
    }

    fn shutdown_server(
        &mut self,
        title_id: &str,
        _build_id: &str,
        _session_id: &str,
        region: &str,
    ) -> Result<(), PlayFabApiError> {
        let mut calls = self.calls.borrow_mut();
        calls.shutdown_calls += 1;
        calls.title_ids.push(title_id.to_owned());
        calls.shutdown_regions.push(region.to_owned());
        if let Some(error) = self.shutdown_error.take() {
            return Err(error);
        }
        Ok(())
    }

    fn build_ready(
        &mut self,
        title_id: &str,
        _build_id: &str,
        _region: &str,
    ) -> Result<bool, PlayFabApiError> {
        let mut calls = self.calls.borrow_mut();
        calls.build_calls += 1;
        calls.title_ids.push(title_id.to_owned());
        drop(calls);
        if self.build_states.is_empty() {
            Ok(true)
        } else {
            Ok(self.build_states.remove(0))
        }
    }

    fn capacity(
        &mut self,
        title_id: &str,
        _build_id: &str,
        _region: &str,
    ) -> Result<PlayFabCapacity, PlayFabApiError> {
        let mut calls = self.calls.borrow_mut();
        calls.capacity_calls += 1;
        calls.title_ids.push(title_id.to_owned());
        Ok(PlayFabCapacity {
            ready_processes: 2,
            allocated_processes: 1,
            pending_operations: 0,
        })
    }
}

fn required<T>(value: u64, constructor: fn(u64) -> Option<T>) -> T {
    constructor(value).expect("fixture identity is nonzero")
}

fn context(
    contract: ContractId,
    operation: u64,
    region: u64,
    realm: Option<u64>,
) -> AdapterRequestContext {
    context_with_authorization(contract, operation, region, realm, 12)
}

fn context_with_authorization(
    contract: ContractId,
    operation: u64,
    region: u64,
    realm: Option<u64>,
    authorization: u64,
) -> AdapterRequestContext {
    AdapterRequestContext::new(
        contract,
        required(operation, OperationId::new),
        required(operation + 1000, CorrelationId::new),
        required(10, ActorId::new),
        required(11, ServiceId::new),
        required(authorization, AuthorizationContextId::new),
        required(13, PolicyVersion::new),
        required(region, RegionId::new),
        realm.map(|value| required(value, RealmId::new)),
        required(14, SchemaVersion::new),
        required(15, ArtifactFingerprint::new),
        required(16, CompatibilityFingerprint::new),
        0,
    )
}

fn placement_request(region: u64, realm: u64) -> PlacementRequest {
    PlacementRequest::new(
        required(100, AccountId::new),
        required(101, SessionId::new),
        required(region, RegionId::new),
        required(realm, RealmId::new),
        required(15, ArtifactFingerprint::new),
    )
}

fn scope() -> PlayFabScope {
    PlayFabScope::new(
        "title-opaque",
        "build-opaque",
        required(20, RegionId::new),
        required(12, AuthorizationContextId::new),
        "KoreaCentral",
        "WestUs3",
        required(15, ArtifactFingerprint::new),
        required(16, CompatibilityFingerprint::new),
    )
    .expect("valid scope")
}

fn authorization(scope: &PlayFabScope) -> SyntheticSandboxAuthorization {
    static INITIALIZE: Once = Once::new();
    INITIALIZE.call_once(|| {
        std::env::set_var("PLAYFAB_SPIKE_LIVE_AUTHORIZED", "1");
        std::env::set_var("PLAYFAB_TITLE_ID", "title-opaque");
        std::env::set_var("PLAYFAB_BUILD_ID", "build-opaque");
        std::env::set_var("PLAYFAB_HOME_REGION", "KoreaCentral");
        std::env::set_var("PLAYFAB_FORBIDDEN_REGION", "WestUs3");
        std::env::set_var("PLAYFAB_SECRET_KEY", "x");
    });
    SyntheticSandboxAuthorization::from_environment(scope).expect("authorized test scope")
}

fn adapter(api: FakeApi) -> PlayFabAdapter<FakeApi> {
    let scope = scope();
    let authorization = authorization(&scope);
    PlayFabAdapter::new(scope, api, authorization).expect("authorization is bound to scope")
}

#[test]
fn scope_rejects_invalid_or_ambiguous_region_configuration() {
    assert!(PlayFabScope::new(
        "bad title",
        "build",
        required(20, RegionId::new),
        required(12, AuthorizationContextId::new),
        "KoreaCentral",
        "WestUs3",
        required(15, ArtifactFingerprint::new),
        required(16, CompatibilityFingerprint::new),
    )
    .is_err());
    assert!(PlayFabScope::new(
        "ABCDE",
        "build",
        required(20, RegionId::new),
        required(12, AuthorizationContextId::new),
        "KoreaCentral",
        "koreacentral",
        required(15, ArtifactFingerprint::new),
        required(16, CompatibilityFingerprint::new),
    )
    .is_err());
}

#[test]
fn adapter_requires_explicit_synthetic_authorization() {
    let authorized_scope = scope();
    let _ = authorization(&authorized_scope);
    let unauthorized_scope = PlayFabScope::new(
        "other-title",
        "build-opaque",
        required(20, RegionId::new),
        required(12, AuthorizationContextId::new),
        "KoreaCentral",
        "WestUs3",
        required(15, ArtifactFingerprint::new),
        required(16, CompatibilityFingerprint::new),
    )
    .expect("valid alternate scope");
    assert!(SyntheticSandboxAuthorization::from_environment(&unauthorized_scope).is_err());
}

#[test]
fn authorization_token_cannot_be_reused_for_another_scope() {
    let authorized_scope = scope();
    let authorization = authorization(&authorized_scope);
    let other_scope = PlayFabScope::new(
        "other-title",
        "build-opaque",
        required(20, RegionId::new),
        required(12, AuthorizationContextId::new),
        "KoreaCentral",
        "WestUs3",
        required(15, ArtifactFingerprint::new),
        required(16, CompatibilityFingerprint::new),
    )
    .expect("valid alternate scope");

    assert!(PlayFabAdapter::new(other_scope, FakeApi::default(), authorization).is_err());
}

#[test]
fn duplicate_placement_reuses_one_provider_call_and_payload_drift_conflicts() {
    let api = FakeApi::default();
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let original_context = context(ContractId::Placement, 1, 20, Some(30));
    let request = placement_request(20, 30);

    let first = adapter
        .submit(original_context, request, &mut observer)
        .expect("submit");
    let duplicate = adapter
        .submit(
            original_context.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect("duplicate");
    assert_eq!(first, duplicate);
    assert_eq!(calls.borrow().request_calls, 1);
    assert_eq!(calls.borrow().title_ids, vec!["title-opaque"]);

    let changed = placement_request(20, 31);
    let error = adapter
        .submit(
            original_context.next_attempt().expect("retry"),
            changed,
            &mut observer,
        )
        .expect_err("payload drift must fail");
    assert_eq!(error.class(), AdapterFailureClass::Conflict);
    assert_eq!(calls.borrow().request_calls, 1);
}

#[test]
fn cross_region_placement_fails_before_provider_call() {
    let api = FakeApi::default();
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let error = adapter
        .submit(
            context(ContractId::Placement, 2, 21, Some(30)),
            placement_request(21, 30),
            &mut observer,
        )
        .expect_err("forbidden region must fail closed");
    assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
    assert_eq!(calls.borrow().request_calls, 0);
}

#[test]
fn status_rejects_provider_region_drift_and_never_exposes_resource_identity() {
    let api = FakeApi {
        server: Some(PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-3".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Active,
        }),
        detail_servers: vec![
            PlayFabServer {
                server_id: "provider-private-server".to_owned(),
                session_id: "al-bakeoff-op-3".to_owned(),
                region: "KoreaCentral".to_owned(),
                state: PlayFabServerState::Active,
            },
            PlayFabServer {
                server_id: "provider-private-server".to_owned(),
                session_id: "al-bakeoff-op-3".to_owned(),
                region: "WestUs3".to_owned(),
                state: PlayFabServerState::Active,
            },
            PlayFabServer {
                server_id: "provider-private-server".to_owned(),
                session_id: "al-bakeoff-op-3".to_owned(),
                region: "WestUs3".to_owned(),
                state: PlayFabServerState::Terminated,
            },
        ],
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let submit_context = context(ContractId::Placement, 3, 20, Some(30));
    let receipt = adapter
        .submit(submit_context, placement_request(20, 30), &mut observer)
        .expect("submit");
    let status = adapter
        .status(
            context(ContractId::Placement, 4, 20, Some(30)),
            receipt,
            &mut observer,
        )
        .expect("status");
    let allocation = match status {
        PlacementStatus::Ready { allocation_id, .. } => allocation_id,
        other => panic!("unexpected status: {other:?}"),
    };
    assert_ne!(allocation, required(100, AllocationId::new));
    assert_ne!(allocation.get(), 30);

    let error = adapter
        .status(
            context(ContractId::Placement, 5, 20, Some(30)),
            receipt,
            &mut observer,
        )
        .expect_err("provider region drift must fail closed");
    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);
    assert_eq!(calls.borrow().shutdown_calls, 1);
    assert_eq!(calls.borrow().shutdown_regions, vec!["WestUs3"]);
    let duplicate = adapter
        .submit(
            submit_context.next_attempt().expect("retry"),
            placement_request(20, 30),
            &mut observer,
        )
        .expect_err("cleaned drifted placement cannot return a live receipt");
    assert_eq!(duplicate.class(), AdapterFailureClass::Unauthorized);
}

#[test]
fn drift_cleanup_never_targets_a_provider_reported_unapproved_region() {
    let api = FakeApi {
        server: Some(PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-28".to_owned(),
            region: "NorthEurope".to_owned(),
            state: PlayFabServerState::Active,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();

    let error = adapter
        .submit(
            context(ContractId::Placement, 28, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect_err("unapproved provider region must fail closed");

    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);
    assert_eq!(calls.borrow().shutdown_calls, 0);
    assert!(calls.borrow().shutdown_regions.is_empty());
}

#[test]
fn initial_drift_cleanup_is_journaled_and_reconciled_after_restart() {
    let first_api = FakeApi {
        server: Some(PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-29".to_owned(),
            region: "WestUs3".to_owned(),
            state: PlayFabServerState::Active,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&first_api.calls);
    let mut first = adapter(first_api);
    let mut observer = Observer::default();
    let original = context(ContractId::Placement, 29, 20, Some(30));
    let request = placement_request(20, 30);

    let error = first
        .submit(original, request, &mut observer)
        .expect_err("drift cleanup remains ambiguous until termination is observed");
    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);
    assert_eq!(calls.borrow().shutdown_calls, 1);
    let journal = first.into_journal();

    let restarted_api = FakeApi {
        calls: Rc::clone(&calls),
        detail_servers: vec![PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-29".to_owned(),
            region: "WestUs3".to_owned(),
            state: PlayFabServerState::Terminated,
        }],
        ..FakeApi::default()
    };
    let restarted_scope = scope();
    let restarted_authorization = authorization(&restarted_scope);
    let mut restarted = PlayFabAdapter::resume(
        restarted_scope,
        restarted_api,
        restarted_authorization,
        journal,
    )
    .expect("resume journaled cleanup");

    let retry = restarted
        .submit(
            original.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect_err("cleaned drifted placement must never become accepted");
    assert_eq!(retry.class(), AdapterFailureClass::Unauthorized);
    assert_eq!(calls.borrow().request_calls, 1);
    assert_eq!(calls.borrow().shutdown_calls, 1);
    assert_eq!(calls.borrow().status_calls, 1);
}

#[test]
fn placement_receipt_cannot_be_queried_from_another_realm() {
    let api = FakeApi {
        server: Some(PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-8".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Active,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit(
            context(ContractId::Placement, 8, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect("submit");

    let error = adapter
        .status(
            context(ContractId::Placement, 9, 20, Some(31)),
            receipt,
            &mut observer,
        )
        .expect_err("cross-realm receipt access must fail closed");

    assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
    assert_eq!(calls.borrow().status_calls, 0);
}

#[test]
fn placement_receipt_cannot_be_cancelled_from_another_realm() {
    let api = FakeApi::default();
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit(
            context(ContractId::Placement, 10, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect("submit");

    let error = adapter
        .cancel(
            context(ContractId::Placement, 11, 20, Some(31)),
            receipt,
            &mut observer,
        )
        .expect_err("cross-realm receipt cancellation must fail closed");

    assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
    assert_eq!(calls.borrow().shutdown_calls, 0);
}

#[test]
fn duplicate_cancellation_is_idempotent() {
    let api = FakeApi {
        detail_servers: vec![PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-14".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Terminated,
        }],
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit(
            context(ContractId::Placement, 14, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect("submit");
    let cancellation = context(ContractId::Placement, 15, 20, Some(30));

    let first = adapter
        .cancel(cancellation, receipt, &mut observer)
        .expect("cancel");
    let duplicate = adapter
        .cancel(
            cancellation.next_attempt().expect("retry"),
            receipt,
            &mut observer,
        )
        .expect("duplicate cancel");

    assert_eq!(first, PlacementStatus::Pending);
    assert_eq!(duplicate, PlacementStatus::Cancelled);
    assert_eq!(calls.borrow().shutdown_calls, 1);
    assert_eq!(calls.borrow().status_calls, 1);
}

#[test]
fn successful_shutdown_is_polled_until_termination_before_terminal_acceptance() {
    let api = FakeApi {
        absent_details_remaining: 1,
        detail_servers: vec![
            PlayFabServer {
                server_id: "provider-private-server".to_owned(),
                session_id: "al-bakeoff-op-30".to_owned(),
                region: "KoreaCentral".to_owned(),
                state: PlayFabServerState::Terminating,
            },
            PlayFabServer {
                server_id: "provider-private-server".to_owned(),
                session_id: "al-bakeoff-op-30".to_owned(),
                region: "KoreaCentral".to_owned(),
                state: PlayFabServerState::Terminated,
            },
        ],
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit(
            context(ContractId::Placement, 30, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect("placement");
    let cancellation = context(ContractId::Placement, 31, 20, Some(30));

    assert_eq!(
        adapter
            .cancel(cancellation, receipt, &mut observer)
            .expect("shutdown accepted"),
        PlacementStatus::Pending
    );
    assert_eq!(
        adapter
            .cancel(
                cancellation.next_attempt().expect("first poll"),
                receipt,
                &mut observer,
            )
            .expect("termination still pending"),
        PlacementStatus::Pending
    );
    assert_eq!(
        adapter
            .cancel(
                cancellation
                    .next_attempt()
                    .and_then(|value| value.next_attempt())
                    .expect("second poll"),
                receipt,
                &mut observer,
            )
            .expect("provider termination still in progress"),
        PlacementStatus::Pending
    );
    assert_eq!(
        adapter
            .cancel(
                cancellation
                    .next_attempt()
                    .and_then(|value| value.next_attempt())
                    .and_then(|value| value.next_attempt())
                    .expect("third poll"),
                receipt,
                &mut observer,
            )
            .expect("termination confirmed"),
        PlacementStatus::Cancelled
    );
    assert_eq!(calls.borrow().shutdown_calls, 1);
    assert_eq!(calls.borrow().status_calls, 3);
}

#[test]
fn ambiguous_cancellation_is_reconciled_before_retrying_shutdown() {
    let api = FakeApi {
        detail_servers: vec![PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-26".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Terminated,
        }],
        shutdown_error: Some(PlayFabApiError {
            http_status: 503,
            playfab_error_code: None,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit(
            context(ContractId::Placement, 26, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect("placement");
    let cancellation = context(ContractId::Placement, 27, 20, Some(30));
    let error = adapter
        .cancel(cancellation, receipt, &mut observer)
        .expect_err("lost shutdown response is ambiguous");
    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);

    let status = adapter
        .cancel(
            cancellation.next_attempt().expect("retry"),
            receipt,
            &mut observer,
        )
        .expect("retry reconciles termination");
    assert_eq!(status, PlacementStatus::Cancelled);
    assert_eq!(calls.borrow().shutdown_calls, 1);
    assert_eq!(calls.borrow().status_calls, 1);
}

#[test]
fn lifecycle_receipt_cannot_be_queried_with_another_authorization() {
    let mut adapter = adapter(FakeApi::default());
    let mut observer = Observer::default();
    let receipt = adapter
        .submit_lifecycle(
            context(ContractId::Deployment, 12, 20, None),
            al_server_core::provider_contracts::ProcessLifecycleRequest::EnsureReady {
                region_id: required(20, RegionId::new),
                artifact: required(15, ArtifactFingerprint::new),
            },
            &mut observer,
        )
        .expect("lifecycle submit");

    let error = adapter
        .lifecycle_status(
            context_with_authorization(ContractId::Deployment, 13, 20, None, 99),
            receipt,
            &mut observer,
        )
        .expect_err("lifecycle receipt authorization drift must fail closed");

    assert_eq!(error.class(), AdapterFailureClass::Unauthorized);
}

#[test]
fn pending_lifecycle_is_polled_until_ready() {
    let api = FakeApi {
        build_states: vec![false, true],
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit_lifecycle(
            context(ContractId::Deployment, 16, 20, None),
            al_server_core::provider_contracts::ProcessLifecycleRequest::EnsureReady {
                region_id: required(20, RegionId::new),
                artifact: required(15, ArtifactFingerprint::new),
            },
            &mut observer,
        )
        .expect("lifecycle submit");

    let status = adapter
        .lifecycle_status(
            context(ContractId::Deployment, 17, 20, None),
            receipt,
            &mut observer,
        )
        .expect("poll lifecycle");

    assert_eq!(status, LifecycleStatus::Complete);
    assert_eq!(calls.borrow().build_calls, 2);
}

#[test]
fn ambiguous_placement_is_reconciled_before_retrying_mutation() {
    let api = FakeApi {
        server: Some(PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-18".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Active,
        }),
        request_error: Some(PlayFabApiError {
            http_status: 0,
            playfab_error_code: None,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let original = context(ContractId::Placement, 18, 20, Some(30));
    let request = placement_request(20, 30);

    let error = adapter
        .submit(original, request, &mut observer)
        .expect_err("lost response must be ambiguous");
    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);

    let receipt = adapter
        .submit(
            original.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect("retry reconciles first");

    assert_eq!(receipt.operation_id(), original.operation_id());
    assert_eq!(calls.borrow().request_calls, 1);
    assert_eq!(calls.borrow().status_calls, 1);
}

#[test]
fn absent_ambiguous_placement_is_retried_only_after_not_found_reconciliation() {
    let api = FakeApi {
        request_error: Some(PlayFabApiError {
            http_status: 0,
            playfab_error_code: None,
        }),
        absent_details_remaining: 1,
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let original = context(ContractId::Placement, 25, 20, Some(30));
    let request = placement_request(20, 30);
    let first = adapter
        .submit(original, request, &mut observer)
        .expect_err("lost response must be ambiguous");
    assert_eq!(first.class(), AdapterFailureClass::AmbiguousCompletion);

    let receipt = adapter
        .submit(
            original.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect("not-found reconciliation permits one safe retry");
    assert_eq!(receipt.operation_id(), original.operation_id());
    assert_eq!(calls.borrow().status_calls, 1);
    assert_eq!(calls.borrow().request_calls, 2);
}

#[test]
fn ambiguous_placement_reconciliation_does_not_treat_other_bad_requests_as_not_found() {
    let api = FakeApi {
        request_error: Some(PlayFabApiError {
            http_status: 503,
            playfab_error_code: None,
        }),
        details_error: Some(PlayFabApiError {
            http_status: 404,
            playfab_error_code: None,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let original = context(ContractId::Placement, 32, 20, Some(30));
    let request = placement_request(20, 30);

    let first = adapter
        .submit(original, request, &mut observer)
        .expect_err("transient mutating response is ambiguous");
    assert_eq!(first.class(), AdapterFailureClass::AmbiguousCompletion);
    let retry = adapter
        .submit(
            original.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect_err("bad request is not proof of absence");
    assert_eq!(retry.class(), AdapterFailureClass::InvalidRequest);
    assert_eq!(calls.borrow().request_calls, 1);
    assert_eq!(calls.borrow().status_calls, 1);
}

#[test]
fn transient_mutating_http_failures_are_ambiguous_and_reconciled() {
    let api = FakeApi {
        request_error: Some(PlayFabApiError {
            http_status: 503,
            playfab_error_code: None,
        }),
        server: Some(PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-33".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Active,
        }),
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let original = context(ContractId::Placement, 33, 20, Some(30));
    let request = placement_request(20, 30);

    let error = adapter
        .submit(original, request, &mut observer)
        .expect_err("transient mutation response cannot prove rejection");
    assert_eq!(error.class(), AdapterFailureClass::AmbiguousCompletion);
    let receipt = adapter
        .submit(
            original.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect("retry reconciles before mutation");
    assert_eq!(receipt.operation_id(), original.operation_id());
    assert_eq!(calls.borrow().request_calls, 1);
    assert_eq!(calls.borrow().status_calls, 1);
}

#[test]
fn deployment_and_capacity_use_neutral_contracts_without_gameplay_authority() {
    let mut adapter = adapter(FakeApi::default());
    let mut observer = Observer::default();
    let lifecycle = adapter
        .submit_lifecycle(
            context(ContractId::Deployment, 6, 20, None),
            al_server_core::provider_contracts::ProcessLifecycleRequest::EnsureReady {
                region_id: required(20, RegionId::new),
                artifact: required(15, ArtifactFingerprint::new),
            },
            &mut observer,
        )
        .expect("lifecycle submit");
    assert_eq!(
        adapter
            .lifecycle_status(
                context(ContractId::Deployment, 7, 20, None),
                lifecycle,
                &mut observer,
            )
            .expect("lifecycle status"),
        LifecycleStatus::Complete
    );
    let capacity = adapter
        .observe_capacity(
            context(ContractId::Capacity, 8, 20, None),
            required(20, RegionId::new),
            required(15, ArtifactFingerprint::new),
            &mut observer,
        )
        .expect("capacity");
    assert_eq!(capacity.ready_processes(), 2);
    assert_eq!(capacity.allocated_processes(), 1);
}

#[test]
fn disabled_adapter_rejects_new_work_but_allows_existing_cleanup() {
    let api = FakeApi {
        detail_servers: vec![PlayFabServer {
            server_id: "provider-private-server".to_owned(),
            session_id: "al-bakeoff-op-21".to_owned(),
            region: "KoreaCentral".to_owned(),
            state: PlayFabServerState::Terminated,
        }],
        ..FakeApi::default()
    };
    let calls = Rc::clone(&api.calls);
    let mut adapter = adapter(api);
    let mut observer = Observer::default();
    let receipt = adapter
        .submit(
            context(ContractId::Placement, 21, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect("submit before disable");
    adapter.disable();

    let error = adapter
        .submit(
            context(ContractId::Placement, 22, 20, Some(30)),
            placement_request(20, 30),
            &mut observer,
        )
        .expect_err("disabled adapter must reject new work");
    assert_eq!(error.class(), AdapterFailureClass::Unavailable);

    let cleanup = context(ContractId::Placement, 23, 20, Some(30));
    let pending = adapter
        .cancel(cleanup, receipt, &mut observer)
        .expect("cleanup must remain available after disable");
    assert_eq!(pending, PlacementStatus::Pending);
    let status = adapter
        .cancel(
            cleanup.next_attempt().expect("poll"),
            receipt,
            &mut observer,
        )
        .expect("cleanup termination must be confirmed after disable");
    assert_eq!(status, PlacementStatus::Cancelled);
    assert_eq!(calls.borrow().shutdown_calls, 1);
}

#[test]
fn retained_journal_reconciles_duplicate_after_adapter_restart() {
    let first_api = FakeApi::default();
    let calls = Rc::clone(&first_api.calls);
    let mut first_adapter = adapter(first_api);
    let mut observer = Observer::default();
    let original = context(ContractId::Placement, 24, 20, Some(30));
    let request = placement_request(20, 30);
    let receipt = first_adapter
        .submit(original, request, &mut observer)
        .expect("initial placement");
    let journal = first_adapter.into_journal();
    let restarted_api = FakeApi {
        calls: Rc::clone(&calls),
        ..FakeApi::default()
    };
    let restarted_scope = scope();
    let restarted_authorization = authorization(&restarted_scope);
    let mut restarted = PlayFabAdapter::resume(
        restarted_scope,
        restarted_api,
        restarted_authorization,
        journal,
    )
    .expect("resume authorized adapter");

    let duplicate = restarted
        .submit(
            original.next_attempt().expect("retry"),
            request,
            &mut observer,
        )
        .expect("duplicate after restart");

    assert_eq!(duplicate, receipt);
    assert_eq!(calls.borrow().request_calls, 1);
}

#[test]
fn playfab_failure_translation_is_stable_and_fail_closed() {
    let cases = [
        (429, None, AdapterFailureClass::Throttled),
        (401, None, AdapterFailureClass::Unauthorized),
        (403, None, AdapterFailureClass::Unauthorized),
        (408, None, AdapterFailureClass::Unavailable),
        (409, None, AdapterFailureClass::Conflict),
        (502, None, AdapterFailureClass::Unavailable),
        (400, Some(1199), AdapterFailureClass::Throttled),
        (400, Some(1609), AdapterFailureClass::Unsupported),
        (400, None, AdapterFailureClass::InvalidRequest),
        (599, None, AdapterFailureClass::Internal),
    ];
    for (status, code, expected) in cases {
        let error: AdapterError = map_playfab_failure(status, code);
        assert_eq!(error.class(), expected);
    }
}
