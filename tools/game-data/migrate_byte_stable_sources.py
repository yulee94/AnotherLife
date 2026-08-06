#!/usr/bin/env python3
"""Check or migrate exact CRLF checkout variants of byte-stable sources.

Fresh checkouts are protected by .gitattributes. Existing Windows worktrees
can retain CRLF bytes after those attributes are introduced because Git does
not rewrite an unchanged path during a fast-forward. ``--write`` repairs only
files whose CRLF-to-LF result has the exact reviewed SHA-256. Any BOM, lone CR,
content mutation, missing file, or other byte drift fails before anything is
written.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional, Sequence


ERROR_PREFIX = "AnotherLife byte-identity migration failed"


@dataclass(frozen=True)
class Target:
    relative_path: str
    sha256: str


@dataclass(frozen=True)
class Assessment:
    target: Target
    state: str
    canonical_bytes: Optional[bytes]
    detail: str


TARGETS: tuple[Target, ...] = (
    Target(
        "unity/Docs/GameDataCatalog/PhaseC/"
        "phase-c-six-family-technical-source.json",
        "5ed847c448d39c4a87ab53e6230621c0bd931e9deb27f43e35b57fdfbfcefa3b",
    ),
    Target(
        "unity/Docs/GameDataCatalog/PhaseC/"
        "phase-c-six-family-technical-source-v002.json",
        "60498d1a071ea79eb37c1b8889a1faaa5c7aee69679c1043256535ef4d3c1685",
    ),
    Target(
        "unity/Docs/GameDataCatalog/PhaseC/"
        "phase-c-six-family-technical-source-v003.json",
        "984ff58bcea68e67258152ff2056d7ce430fe0e91658764bcca3abaa3d66c439",
    ),
    Target(
        "unity/Docs/GameDataCatalog/PhaseC/Shadow/"
        "realm-family-shadow-v001.json",
        "265160f0c20b10293a69572fbcc4703ad81add498b20dfb727c353e050b0eccb",
    ),
    Target(
        "unity/Docs/GameDataCatalog/PhaseC/Shadow/"
        "realm-family-shadow-v001.evidence.json",
        "9aca84e7d937fffcaf26fa3f018d66fef251d6c9a84eeef90b8b251e7d121b83",
    ),
    Target(
        "unity/Docs/GameDataCatalog/PhaseC/"
        "Phase_C4A_Building_Authority_Convergence.md",
        "b94895911e46cfd03dfb08b15e3c4ccf860a028ffe62d922c95e564fd2e5e039",
    ),
    Target(
        "unity/Docs/Narrative/GameData/phase-c-six-family-content-map.json",
        "8377a47d659a2e7dd238e35f373dbefa711e4ca16bf95e280e2dc36029327353",
    ),
    Target(
        "unity/Assets/AL/StreamingAssets/GameData/al_realm_catalog.json",
        "33321936662b98f9c18edf4122ad163053d1aff3017b06556cad694420e9e8d8",
    ),
    Target(
        "unity/Assets/AL/StreamingAssets/GameData/"
        "al_notification_content_catalog.json",
        "3c32ba4faa8293897fa8c6ecf3518993aa17778c5848ea47bc48ce697ae1c1c3",
    ),
    Target(
        "unity/Assets/AL/Scripts/Services/Local/LocalGameDataService.cs",
        "7be267f64de24718090170af779ce57b5ffd88eb50a55e9d4e5ff011443276f9",
    ),
    Target(
        "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
        "GameDataWalletResourceReferences.cs",
        "07ef09c4bca55278a7db6dd09c9740352829bb677eb1ea4c817b8646ac02c699",
    ),
    Target(
        "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
        "GameDataRealmReferences.cs",
        "4bb8457c9831756a8cf6c2ddf3f14a5fd5c51866370c870cb074a53313bbdf4f",
    ),
    Target(
        "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
        "GameDataRealmCapabilityProfiles.cs",
        "8413f45a32cad1bf71107c0c6cea18e4c8e86b7f8191a19ff0bcc0875e89b427",
    ),
    Target(
        "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
        "GameDataBuildingProgressionRegistry.cs",
        "319cb9f97cff850c3e0f79c30ae877c2876ecab6cf70d9fa681a672be4b430c4",
    ),
    Target(
        "unity/Assets/AL/Scripts/Data/Catalogs/SixFamily/"
        "GameDataSixFamilySchemas.cs",
        "3c759d9ea2f1b2d6aca53d1e5f213bf0edb057eb0751bf3c9bfe9ae94b15d9bb",
    ),
    Target(
        "unity/Assets/AL/Tests/EditMode/GameDataCatalog/"
        "GameDataBuildingProgressionRegistryTests.cs",
        "8e911d59f0884c1d4ef7201f35579c6f2b257008d1c853345bea68d28d50ab29",
    ),
    Target(
        "tools/game-data/generate_phase_c_realm_shadow_artifact.py",
        "71daae1ceef84d28417a4d25e0862d14df101b2df5eb253e4197349038889141",
    ),
)


class MigrationError(RuntimeError):
    """Raised when a target is missing, drifted, or needs explicit migration."""


def sha256(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def assess_bytes(target: Target, raw: bytes) -> Assessment:
    actual_sha256 = sha256(raw)
    if actual_sha256 == target.sha256:
        return Assessment(target, "exact", raw, "canonical LF bytes")

    if raw.startswith(b"\xef\xbb\xbf"):
        return Assessment(target, "invalid", None, "UTF-8 BOM is not allowed")

    canonical = raw.replace(b"\r\n", b"\n")
    if canonical == raw:
        return Assessment(
            target,
            "invalid",
            None,
            f"SHA-256 {actual_sha256} is not the reviewed identity",
        )
    if b"\r" in canonical:
        return Assessment(
            target,
            "invalid",
            None,
            "contains a lone or mixed carriage return",
        )
    if sha256(canonical) != target.sha256:
        return Assessment(
            target,
            "invalid",
            None,
            "CRLF normalization does not recover the reviewed identity",
        )

    return Assessment(
        target,
        "legacy-crlf",
        canonical,
        "exact reviewed bytes after CRLF-to-LF checkout migration",
    )


def inspect_targets(
    repository_root: Path,
    targets: Sequence[Target],
) -> list[Assessment]:
    assessments: list[Assessment] = []
    for target in targets:
        path = repository_root / target.relative_path
        if not path.is_file():
            assessments.append(
                Assessment(target, "invalid", None, "file is missing")
            )
            continue
        assessments.append(assess_bytes(target, path.read_bytes()))
    return assessments


def validate_assessments(assessments: Iterable[Assessment]) -> None:
    invalid = [item for item in assessments if item.state == "invalid"]
    if invalid:
        details = "\n".join(
            f"- {item.target.relative_path}: {item.detail}" for item in invalid
        )
        raise MigrationError(
            "unreviewed byte drift was found; no files were changed:\n" + details
        )


def write_migrations(
    repository_root: Path,
    assessments: Sequence[Assessment],
) -> int:
    validate_assessments(assessments)
    migrations = [item for item in assessments if item.state == "legacy-crlf"]
    for item in migrations:
        path = repository_root / item.target.relative_path
        canonical = item.canonical_bytes
        if canonical is None:
            raise MigrationError(
                f"internal error: {item.target.relative_path} has no canonical bytes"
            )
        temporary = path.with_name(path.name + ".byte-identity.tmp")
        try:
            temporary.write_bytes(canonical)
            os.replace(temporary, path)
        finally:
            if temporary.exists():
                temporary.unlink()

    final = inspect_targets(repository_root, tuple(item.target for item in assessments))
    validate_assessments(final)
    not_exact = [item for item in final if item.state != "exact"]
    if not_exact:
        raise MigrationError("post-migration verification did not reach exact bytes")
    return len(migrations)


def require_exact(assessments: Sequence[Assessment]) -> None:
    validate_assessments(assessments)
    migrations = [item for item in assessments if item.state == "legacy-crlf"]
    if migrations:
        details = "\n".join(
            f"- {item.target.relative_path}" for item in migrations
        )
        raise MigrationError(
            "legacy CRLF checkout bytes require the explicit safe migration; "
            "no files were changed:\n"
            + details
            + "\nRun: python3 tools/game-data/"
            "migrate_byte_stable_sources.py --write"
        )


def assert_self_test(condition: bool, message: str) -> None:
    if not condition:
        raise MigrationError("self-test failed: " + message)


def run_self_test() -> None:
    canonical = b'{\n  "value": "realm"\n}\n'
    target = Target("fixture.json", sha256(canonical))
    legacy = canonical.replace(b"\n", b"\r\n")

    assert_self_test(assess_bytes(target, canonical).state == "exact", "exact")
    legacy_assessment = assess_bytes(target, legacy)
    assert_self_test(legacy_assessment.state == "legacy-crlf", "legacy CRLF")
    assert_self_test(
        legacy_assessment.canonical_bytes == canonical,
        "legacy canonical recovery",
    )
    assert_self_test(
        assess_bytes(target, canonical + b" ").state == "invalid",
        "mutated content rejection",
    )
    assert_self_test(
        assess_bytes(target, b"\xef\xbb\xbf" + canonical).state == "invalid",
        "BOM rejection",
    )
    assert_self_test(
        assess_bytes(target, canonical.replace(b"\n", b"\r", 1)).state
        == "invalid",
        "lone CR rejection",
    )
    assert_self_test(
        assess_bytes(target, legacy.replace(b"realm", b"other")).state
        == "invalid",
        "CRLF plus mutation rejection",
    )

    with tempfile.TemporaryDirectory(prefix="anotherlife-byte-identity-") as temp:
        root = Path(temp)
        path = root / target.relative_path
        path.write_bytes(legacy)
        assessments = inspect_targets(root, (target,))
        assert_self_test(assessments[0].state == "legacy-crlf", "fixture scan")
        assert_self_test(write_migrations(root, assessments) == 1, "fixture write")
        assert_self_test(path.read_bytes() == canonical, "fixture exact result")

        path.write_bytes(legacy)
        drift_target = Target("drift.json", sha256(b"expected\n"))
        drift_path = root / drift_target.relative_path
        drift_path.write_bytes(b"unexpected\r\n")
        before = path.read_bytes()
        blocked = inspect_targets(root, (target, drift_target))
        try:
            write_migrations(root, blocked)
        except MigrationError:
            pass
        else:
            raise MigrationError("self-test failed: drift did not block migration")
        assert_self_test(path.read_bytes() == before, "two-phase no-write failure")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group()
    action.add_argument(
        "--write",
        action="store_true",
        help="rewrite only exact reviewed CRLF variants to canonical LF bytes",
    )
    action.add_argument(
        "--self-test",
        action="store_true",
        help="run exact/CRLF/drift/BOM/lone-CR/atomicity fixtures",
    )
    args = parser.parse_args()

    try:
        if args.self_test:
            run_self_test()
            print("PASS: byte-identity migration self-test")
            return 0

        repository_root = Path(__file__).resolve().parents[2]
        assessments = inspect_targets(repository_root, TARGETS)
        if args.write:
            migrated = write_migrations(repository_root, assessments)
            print(
                "PASS: byte-stable sources are exact; "
                f"migrated {migrated} legacy CRLF path(s)"
            )
        else:
            require_exact(assessments)
            print(
                "PASS: all byte-stable sources match their exact reviewed "
                "SHA-256 identities"
            )
        return 0
    except (MigrationError, OSError) as error:
        print(f"{ERROR_PREFIX}: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
