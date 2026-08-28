#!/usr/bin/env python3
"""Test deterministic assembly and fail-closed world-asset validation."""

from __future__ import annotations

import copy
import sys
from pathlib import Path
from typing import Any, Callable

import world_asset_inventory as inventory


REPO_ROOT = Path(__file__).resolve().parents[3]


def expect_failure(
    catalog: dict[str, Any],
    mutate: Callable[[dict[str, Any]], None],
    category: str,
) -> None:
    changed = copy.deepcopy(catalog)
    mutate(changed)
    try:
        inventory.validate_catalog(REPO_ROOT, changed)
    except inventory.InventoryValidationError as error:
        if category not in str(error):
            raise AssertionError(
                f"expected {category!r}, got {error!s}"
            ) from error
        return
    raise AssertionError(f"negative fixture unexpectedly passed: {category}")


def make_aggregate_budget_overrun(catalog: dict[str, Any]) -> None:
    records_by_id = {record["assetId"]: record for record in catalog["records"]}
    records = [
        records_by_id["wa_crownlands_architecture_building_town_hall_base_v001"],
        records_by_id["wa_crownlands_architecture_building_workshop_base_v001"],
    ]
    for index, record in enumerate(records):
        ceilings = inventory.BUDGET_CEILINGS[record["budgetClassId"]]
        record["budgetMeasurements"].update(
            {
                "measurementState": "measured",
                "baseVariants": 1,
                "stateDerivatives": 1,
                "lod0Triangles": 100,
                "mobileNormalTriangles": 50,
                "lod0MaterialSlots": 1,
                "mobileDraws": 1,
                "textureLongEdgePixels": 1024,
                "textureFormat": "astc_6x6",
                "colliderPrimitives": 1,
                "colliderProxyTriangles": 0,
                "navSourceTriangles": 0,
                "navLinkPairs": 0,
                "navDataBytes": 0,
                "activeVfxSources": 0,
                "liveParticles": 0,
                "transparentDraws": 0,
                "dynamicLights": 0,
                "activationP95Ms": 0.1,
                "loadReadyP95Ms": 100,
                "artifacts": [
                    {
                        "sha256": f"{index + 1}" * 64,
                        "compressedDeliveryBytes": (ceilings[7] - 1) * inventory.MIB,
                        "installedBytes": (ceilings[8] - 1) * inventory.MIB,
                        "residentBytes": (ceilings[6] - 1) * inventory.MIB,
                        "loadIoBytes": 40 * inventory.MIB,
                    }
                ],
                "placements": [
                    {
                        "realmId": "crownlands",
                        "sceneId": "aggregate_budget_scene",
                        "cellId": "aggregate_budget_cell",
                        "ring": "interaction",
                        "visibleInstances": 1,
                    }
                ],
            }
        )


def main() -> int:
    assert inventory.canonical_source_bytes(
        inventory.TAXONOMY_PATH,
        b"alpha\r\nbeta\r",
    ) == b"alpha\nbeta\n"
    assert inventory.canonical_source_bytes(
        inventory.BUILDING_CATALOG_PATH,
        b'{"lineEnding":"preserved"}\r\n',
    ) == b'{"lineEnding":"preserved"}\r\n'

    first = inventory.build_catalog(REPO_ROOT)
    second = inventory.build_catalog(REPO_ROOT)
    first_raw = inventory.canonical_json(first)
    second_raw = inventory.canonical_json(second)
    assert first_raw == second_raw

    evidence = inventory.validate_catalog(REPO_ROOT, first)
    assert len(first["familyRecords"]) == 242
    assert len(first["records"]) == 8
    assert len(first["aliases"]) == 16
    assert evidence["familyCoverage"]["covered"] == 242
    assert evidence["realmCoverage"]["cells"] == 968
    assert evidence["duplicateAndAliasReport"]["duplicateCount"] == 0
    assert evidence["ownerAuthorityCoverage"]["complete"] == 250
    assert evidence["bindingCoverage"]["verifiedPrefabTuples"] == 8
    assert evidence["budgetRollup"]["assignedFamilies"] == 242
    assert evidence["budgetRollup"]["classCount"] == 26
    assert evidence["approvalState"]["generationState"] == "held"
    assert evidence["approvalState"]["activationState"] == "held"

    family = first["familyRecords"][0]
    asset = first["records"][0]
    expect_failure(
        first,
        lambda value: value["familyRecords"].append(
            copy.deepcopy(value["familyRecords"][0])
        ),
        "DuplicateId",
    )
    expect_failure(
        first,
        lambda value: value["familyRecords"][0].__setitem__(
            "familyId", "bad-family-id"
        ),
        "MalformedCatalog",
    )
    expect_failure(
        first,
        lambda value: value["familyRecords"][0]["realmApplicability"].pop(),
        "CategoryRealmGap",
    )
    expect_failure(
        first,
        lambda value: value["familyRecords"][0].pop("ownerAuthority"),
        "OwnerAuthorityMissing",
    )
    expect_failure(
        first,
        lambda value: value["familyRecords"][0]["provenance"].__setitem__(
            "sourceReferences", []
        ),
        "ProvenanceBlocked",
    )
    expect_failure(
        first,
        lambda value: value["familyRecords"][0]["standards"].__setitem__(
            "coordinateProfileId", "missing_profile"
        ),
        "ProfileMissing",
    )
    expect_failure(
        first,
        lambda value: value["familyRecords"][0].__setitem__(
            "budgetClassId", "budget_unassigned"
        ),
        "BudgetUnassigned",
    )
    expect_failure(
        first,
        lambda value: value["records"][0]["binding"]["prefab"].update(
            {"guid": "0" * 32}
        ),
        "BrokenPrefabBinding",
    )
    expect_failure(
        first,
        lambda value: value["records"][0]["binding"]["prefab"].update(
            {"path": "Assets/../../outside.prefab"}
        ),
        "UnsafeBindingPath",
    )
    expect_failure(
        first,
        lambda value: value["gatePolicy"].__setitem__(
            "generationState", "eligible"
        ),
        "GateConflict",
    )
    expect_failure(
        first,
        lambda value: inventory.make_measured_budget_overrun(
            value["records"][0]
        ),
        "BudgetOverrun",
    )
    expect_failure(
        first,
        make_aggregate_budget_overrun,
        "AggregateBudgetOverrun",
    )

    assert family["familyId"].startswith("waf_")
    assert asset["assetId"].startswith("wa_")
    print("PASS: two independent generations are byte-identical")
    print("PASS: 242 families, 8 preserved bindings, and 16 aliases validate")
    print("PASS: 12 adversarial inventory mutations fail closed")
    print(f"PASS: catalog raw SHA-256 {inventory.sha256(first_raw)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
