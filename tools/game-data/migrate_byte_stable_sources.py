#!/usr/bin/env python3
"""Check or migrate exact CRLF checkout variants of byte-stable sources.

Fresh checkouts are protected by .gitattributes. Existing Windows worktrees
can retain CRLF bytes after those attributes are introduced because Git does
not rewrite an unchanged path during a fast-forward. ``--write`` repairs only
files whose CRLF-to-LF result has the exact reviewed SHA-256. Any BOM, lone CR,
content mutation, missing file, or other byte drift fails before anything is
written. The all-target preflight is no-write, while replacement is atomic per
file rather than transactional across the group. If I/O interruption leaves an
exact-LF prefix and a legacy-CRLF suffix, a safe rerun completes the remainder.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import stat
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Optional, Sequence
from unittest import mock


ERROR_PREFIX = "AnotherLife byte-identity migration failed"
MIGRATION_HINT = (
    "Re-run this tool with --write using the same Python interpreter."
)
WORLD_ATLAS_RELATIVE_PATH = (
    "unity/Assets/AL/StreamingAssets/GameData/"
    "al_world_atlas_narrative_catalog.json"
)
WORLD_ATLAS_SHA256 = (
    "9034e8fb8e4c6b611c7b9285e456338e719edd9a5a4ff76a5fd05a196d3c9c8a"
)
WORLD_ATLAS_CANONICAL_LENGTH = 34_835


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
    Target(WORLD_ATLAS_RELATIVE_PATH, WORLD_ATLAS_SHA256),
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
        if path.is_symlink():
            assessments.append(
                Assessment(
                    target,
                    "invalid",
                    None,
                    "target leaf is a symbolic link",
                )
            )
            continue
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
        if path.is_symlink():
            raise MigrationError(
                f"target leaf became a symbolic link: {item.target.relative_path}"
            )

        original_mode = stat.S_IMODE(
            os.stat(path, follow_symlinks=False).st_mode
        )
        descriptor = -1
        temporary: Optional[Path] = None
        try:
            descriptor, temporary_name = tempfile.mkstemp(
                prefix=f".{path.name}.byte-identity-",
                suffix=".tmp",
                dir=path.parent,
            )
            temporary = Path(temporary_name)
            os.chmod(temporary, original_mode)
            stream = os.fdopen(descriptor, "wb")
            descriptor = -1
            with stream:
                stream.write(canonical)
                stream.flush()
                os.fsync(stream.fileno())

            if path.is_symlink():
                raise MigrationError(
                    "target leaf became a symbolic link before replacement: "
                    f"{item.target.relative_path}"
                )
            os.replace(temporary, path)
            temporary = None
        finally:
            try:
                if descriptor >= 0:
                    os.close(descriptor)
            finally:
                if temporary is not None and (
                    temporary.exists() or temporary.is_symlink()
                ):
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
            + "\n"
            + MIGRATION_HINT
        )


def assert_self_test(condition: bool, message: str) -> None:
    if not condition:
        raise MigrationError("self-test failed: " + message)


def run_self_test(repository_root: Path) -> bool:
    atlas_targets = tuple(
        target
        for target in TARGETS
        if target.relative_path == WORLD_ATLAS_RELATIVE_PATH
    )
    assert_self_test(
        len(atlas_targets) == 1,
        "one production world-atlas target",
    )
    atlas_target = atlas_targets[0]
    assert_self_test(
        atlas_target.sha256 == WORLD_ATLAS_SHA256,
        "production world-atlas reviewed identity",
    )
    atlas_assessment = inspect_targets(repository_root, atlas_targets)[0]
    assert_self_test(
        atlas_assessment.state in ("exact", "legacy-crlf"),
        "production world-atlas exact-or-reviewed-CRLF acceptance: "
        f"{atlas_assessment.detail}",
    )
    atlas_canonical = atlas_assessment.canonical_bytes
    assert_self_test(
        atlas_canonical is not None
        and len(atlas_canonical) == WORLD_ATLAS_CANONICAL_LENGTH
        and sha256(atlas_canonical) == WORLD_ATLAS_SHA256,
        "production world-atlas canonical byte identity",
    )

    canonical = b'{\n  "value": "realm"\n}\n'
    target = Target("fixture with spaces.json", sha256(canonical))
    legacy = canonical.replace(b"\n", b"\r\n")

    assert_self_test(assess_bytes(target, canonical).state == "exact", "exact")
    legacy_assessment = assess_bytes(target, legacy)
    assert_self_test(legacy_assessment.state == "legacy-crlf", "legacy CRLF")
    assert_self_test(
        legacy_assessment.canonical_bytes == canonical,
        "legacy canonical recovery",
    )
    try:
        require_exact((legacy_assessment,))
    except MigrationError as error:
        failure = str(error)
        assert_self_test(
            failure.endswith(MIGRATION_HINT),
            "interpreter-neutral actionable migration hint",
        )
        assert_self_test(
            "python3" not in failure and "Run:" not in failure,
            "migration hint does not require interpreter or path quoting",
        )
    else:
        raise MigrationError("self-test failed: legacy exact check did not fail")
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

        atlas_path = root / atlas_target.relative_path
        atlas_path.parent.mkdir(parents=True)
        atlas_legacy = atlas_canonical.replace(b"\n", b"\r\n")
        atlas_path.write_bytes(atlas_legacy)
        isolated_atlas = inspect_targets(root, (atlas_target,))
        assert_self_test(
            isolated_atlas[0].state == "legacy-crlf",
            "isolated production world-atlas CRLF scan",
        )
        assert_self_test(
            write_migrations(root, isolated_atlas) == 1,
            "isolated production world-atlas migration",
        )
        assert_self_test(
            atlas_path.read_bytes() == atlas_canonical,
            "isolated production world-atlas exact migration result",
        )
        exact_atlas = inspect_targets(root, (atlas_target,))
        assert_self_test(
            exact_atlas[0].state == "exact",
            "isolated production world-atlas exact rescan",
        )
        before_idempotence = atlas_path.read_bytes()
        assert_self_test(
            write_migrations(root, exact_atlas) == 0,
            "isolated production world-atlas idempotent migration count",
        )
        assert_self_test(
            atlas_path.read_bytes() == before_idempotence,
            "isolated production world-atlas idempotent bytes",
        )

        path = root / target.relative_path
        path.write_bytes(legacy)
        predictable = path.with_name(path.name + ".byte-identity.tmp")
        sentinel = b"pre-existing-untracked-sentinel"
        predictable.write_bytes(sentinel)
        assessments = inspect_targets(root, (target,))
        assert_self_test(assessments[0].state == "legacy-crlf", "fixture scan")
        assert_self_test(write_migrations(root, assessments) == 1, "fixture write")
        assert_self_test(path.read_bytes() == canonical, "fixture exact result")
        assert_self_test(
            predictable.read_bytes() == sentinel,
            "pre-existing predictable sibling preservation",
        )
        assert_self_test(
            not list(root.glob(f".{path.name}.byte-identity-*.tmp")),
            "owned random temp cleanup",
        )

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
        assert_self_test(path.read_bytes() == before, "preflight no-write failure")

        symlink_supported = False
        symlink_backing = root / "symlink-backing.json"
        symlink_backing.write_bytes(legacy)
        symlink_target = Target("symlink-fixture.json", target.sha256)
        symlink_path = root / symlink_target.relative_path
        try:
            os.symlink(symlink_backing, symlink_path)
        except (NotImplementedError, OSError):
            pass
        else:
            symlink_supported = True
            symlink_assessment = inspect_targets(root, (symlink_target,))[0]
            assert_self_test(
                symlink_assessment.state == "invalid" and
                "symbolic link" in symlink_assessment.detail,
                "target symlink rejection",
            )
            try:
                write_migrations(root, (symlink_assessment,))
            except MigrationError:
                pass
            else:
                raise MigrationError(
                    "self-test failed: target symlink migration was not blocked"
                )
            assert_self_test(symlink_path.is_symlink(), "symlink leaf preserved")
            assert_self_test(
                symlink_backing.read_bytes() == legacy,
                "symlink backing bytes preserved",
            )

        first_target = Target("partial-first.json", target.sha256)
        second_target = Target("partial-second.json", target.sha256)
        first_path = root / first_target.relative_path
        second_path = root / second_target.relative_path
        first_path.write_bytes(legacy)
        second_path.write_bytes(legacy)
        partial = inspect_targets(root, (first_target, second_target))
        real_replace = os.replace
        replace_count = 0

        def fail_second_replace(source: Path, destination: Path) -> None:
            nonlocal replace_count
            replace_count += 1
            if replace_count == 2:
                raise OSError("injected second replacement failure")
            real_replace(source, destination)

        with mock.patch.object(os, "replace", side_effect=fail_second_replace):
            try:
                write_migrations(root, partial)
            except OSError as error:
                assert_self_test(
                    "injected second replacement failure" in str(error),
                    "injected replacement failure identity",
                )
            else:
                raise MigrationError(
                    "self-test failed: injected replacement failure did not stop"
                )

        assert_self_test(
            first_path.read_bytes() == canonical,
            "completed first migration remains exact after interruption",
        )
        assert_self_test(
            second_path.read_bytes() == legacy,
            "unreplaced second migration remains legacy CRLF",
        )
        assert_self_test(
            not list(root.glob(".*.byte-identity-*.tmp")),
            "interrupted owned temp cleanup",
        )
        retry = inspect_targets(root, (first_target, second_target))
        assert_self_test(write_migrations(root, retry) == 1, "partial retry count")
        assert_self_test(
            first_path.read_bytes() == canonical and
            second_path.read_bytes() == canonical,
            "idempotent retry reaches exact bytes",
        )

        return symlink_supported


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group()
    action.add_argument(
        "--write",
        action="store_true",
        help=(
            "rewrite only exact reviewed CRLF variants to canonical LF bytes; "
            "preflight is all-target/no-write and replacement is atomic per file, "
            "with safe retry after partial I/O interruption"
        ),
    )
    action.add_argument(
        "--self-test",
        action="store_true",
        help=(
            "run exact/CRLF/drift/BOM/lone-CR/preflight/temp-ownership/"
            "partial-retry fixtures plus the production world-atlas target"
        ),
    )
    args = parser.parse_args()

    try:
        repository_root = Path(__file__).resolve().parents[2]
        if args.self_test:
            symlink_supported = run_self_test(repository_root)
            print("PASS: byte-identity migration self-test")
            if symlink_supported:
                print("PASS: target symlink rejection fixture")
            else:
                print(
                    "SKIP: target symlink rejection fixture; symlink creation "
                    "is unavailable in this environment"
                )
            return 0

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
