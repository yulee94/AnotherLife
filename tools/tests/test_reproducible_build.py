import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "reproducible_build.py"
REPO_ROOT = SCRIPT.parents[1]
POLICY = REPO_ROOT / "tools/builds/reproducible_build_policy.json"


def load_module():
    spec = importlib.util.spec_from_file_location("reproducible_build", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ReproducibleBuildTests(unittest.TestCase):
    def test_policy_pins_every_supported_toolchain_and_build_input(self):
        module = load_module()
        policy = module.load_policy(POLICY)

        self.assertEqual(policy["schemaVersion"], 1)
        self.assertEqual(policy["projectEditor"], {
            "version": "6000.3.22f1",
            "revision": "1c726e1fb402",
        })
        windows = policy["targets"]["windows64-development"]
        self.assertEqual(windows["unityVersion"], "6000.3.22f1")
        self.assertEqual(windows["scriptingBackend"], "Mono2x")
        self.assertEqual(windows["architecture"], "x86_64")
        self.assertEqual(set(policy["targets"]), {"windows64-development"})
        android = policy["deferredAndroid"]
        self.assertEqual(android["task"], "t_7b530af7")
        self.assertEqual(android["status"], "deferred_pc_first")
        self.assertEqual(android["approvedEditor"], "6000.3.22f1")
        self.assertEqual(android["legacyExporterEditor"], "2022.3.62f3")
        self.assertFalse(android["legacyCrossVersionProjectAuthorized"])
        self.assertFalse(android["androidModuleAvailable"])

    def test_legacy_android_editor_cannot_open_the_unity6_project(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        project_editor = module.read_project_editor(REPO_ROOT / "unity")

        result = module.evaluate_android_compatibility(policy, project_editor)

        self.assertEqual(result["status"], "deferred")
        self.assertEqual(result["reasonCode"], "mobile_deferred_pc_first")
        self.assertEqual(result["approvedFutureEditor"], "6000.3.22f1")
        self.assertIn("Never open", result["remediation"])
        self.assertFalse(result["mayLaunchExporter"])

    def test_android_packaging_uses_variant_scoped_artifact_root(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        target = policy["deferredAndroid"]["retainedPackageProfiles"]["release"]

        command = module.create_android_package_command(REPO_ROOT, target, "2022.3.62f3")

        artifacts_index = command.index("--artifacts-dir") + 1
        self.assertEqual(
            Path(command[artifacts_index]),
            REPO_ROOT / "unity/Builds/AndroidArtifacts",
        )

    def test_source_inventory_is_traceable_and_content_order_is_deterministic(self):
        module = load_module()
        policy = module.load_policy(POLICY)

        first = module.collect_source_inventory(REPO_ROOT, policy)
        second = module.collect_source_inventory(REPO_ROOT, policy)

        self.assertEqual(first, second)
        self.assertRegex(first["sourceRevision"], r"^[0-9a-f]{40}$")
        self.assertRegex(first["sourceTreeSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(first["contentTreeSha256"], r"^[0-9a-f]{64}$")
        self.assertEqual([scene["path"] for scene in first["scenes"]], [
            "Assets/AL/Scenes/Boot.unity",
            "Assets/AL/Scenes/RealmSelection.unity",
            "Assets/AL/Scenes/CharacterCreation.unity",
            "Assets/AL/Scenes/ChampionArena.unity",
            "Assets/AL/Scenes/Kingdom.unity",
        ])
        self.assertTrue(all(scene["enabled"] for scene in first["scenes"]))
        self.assertIn("unity/Packages/packages-lock.json", first["inputFiles"])
        self.assertIn("unity/ProjectSettings/ProjectSettings.asset", first["inputFiles"])
        self.assertIn("tools/reproducible_build.py", first["inputFiles"])
        self.assertIn("tools/builds/reproducible_build_policy.json", first["inputFiles"])
        self.assertIn("gradle/wrapper/gradle-wrapper.properties", first["inputFiles"])

    def test_artifact_hash_and_smoke_are_stable_and_fail_closed(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "AnotherLifeUnity_Data").mkdir()
            (root / "AnotherLifeUnity.exe").write_bytes(b"MZ" + b"\0" * 62)
            (root / "AnotherLifeUnity_Data/globalgamemanagers").write_bytes(b"player-data")

            first = module.inspect_artifacts(root, "windows64-development")
            second = module.inspect_artifacts(root, "windows64-development")
            self.assertEqual(first, second)
            self.assertEqual(first["smoke"]["status"], "passed")
            self.assertRegex(first["treeSha256"], r"^[0-9a-f]{64}$")

            (root / "AnotherLifeUnity.exe").write_bytes(b"not-pe")
            failed = module.inspect_artifacts(root, "windows64-development")
            self.assertEqual(failed["smoke"]["status"], "failed")
            self.assertIn("PE", " ".join(failed["smoke"]["failures"]))

    def test_target_cleanup_is_scoped_and_preserves_sibling_variant(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            selected = root / "build/artifacts/release"
            sibling = root / "build/artifacts/debug"
            export = root / "build/export"
            for directory in (selected, sibling, export):
                directory.mkdir(parents=True)
                (directory / "stale.bin").write_bytes(b"stale")

            module.clean_target_outputs(
                root,
                {"cleanInputs": ["build/export", "build/artifacts/release"]},
            )

            self.assertFalse(selected.exists())
            self.assertFalse(export.exists())
            self.assertTrue((sibling / "stale.bin").is_file())

    def test_target_cleanup_rejects_directory_symlink(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "real-output"
            target.mkdir()
            (target / "keep.bin").write_bytes(b"keep")
            link = root / "linked-output"
            try:
                link.symlink_to(target, target_is_directory=True)
            except OSError:
                subprocess.run(
                    ["cmd.exe", "/c", "mklink", "/J", str(link), str(target)],
                    check=True,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                )

            with self.assertRaisesRegex(module.BuildContractError, "symlink"):
                module.clean_target_outputs(root, {"cleanInputs": ["linked-output"]})

            self.assertTrue((target / "keep.bin").is_file())

    def test_manifest_is_canonical_signed_ready_and_comparable(self):
        module = load_module()
        base = {
            "schemaVersion": 1,
            "target": "windows64-development",
            "source": {"sourceRevision": "a" * 40, "sourceTreeSha256": "b" * 64},
            "toolchain": {"unityVersion": "6000.3.22f1"},
            "settings": {"scriptingBackend": "Mono2x", "architecture": "x86_64"},
            "scenes": [{"path": "Assets/AL/Scenes/Boot.unity", "enabled": True}],
            "content": {"treeSha256": "c" * 64},
            "artifacts": {"treeSha256": "d" * 64, "smoke": {"status": "passed"}},
            "run": {"startedAtUtc": "first", "endedAtUtc": "first", "host": "one"},
        }
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / "manifest.json"
            written = module.write_signed_ready_manifest(base, destination)
            payload = json.loads(destination.read_text(encoding="utf-8"))
            self.assertEqual(payload["manifestSha256"], written["manifestSha256"])
            self.assertRegex(payload["manifestSha256"], r"^[0-9a-f]{64}$")
            self.assertEqual(
                destination.with_suffix(".json.sha256").read_text(encoding="ascii"),
                f'{payload["manifestSha256"]}  manifest.json\n',
            )

            equivalent = json.loads(json.dumps(payload))
            equivalent["run"] = {"startedAtUtc": "second", "endedAtUtc": "second", "host": "two"}
            comparison = module.compare_manifests(payload, equivalent)
            self.assertEqual(comparison["status"], "normalized_equivalent")
            self.assertEqual(comparison["normalization"], ["run", "manifestSha256"])

            equivalent["artifacts"]["treeSha256"] = "e" * 64
            comparison = module.compare_manifests(payload, equivalent)
            self.assertEqual(comparison["status"], "stop_ship")
            self.assertIn("artifacts.treeSha256", comparison["differences"])

    def test_manifest_source_summary_includes_actual_editor_settings_and_input_hashes(self):
        module = load_module()
        source = {
            "sourceRevision": "a" * 40,
            "sourceTreeSha256": "b" * 64,
            "trackedInputsDirty": False,
            "projectEditor": {"version": "6000.3.22f1", "revision": "1c726e1fb402"},
            "projectSettings": {"bundleVersion": "1.0"},
            "inputFiles": {"unity/Packages/manifest.json": {"bytes": 1, "sha256": "c" * 64}},
        }

        summary = module.manifest_source_summary(source)

        self.assertEqual(summary["projectEditor"], source["projectEditor"])
        self.assertEqual(summary["projectSettings"], source["projectSettings"])
        self.assertEqual(summary["inputFiles"], source["inputFiles"])

    def test_launch_smoke_requires_distinct_profile_and_exact_ordered_markers(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        launch = policy["launchSmoke"]
        isolation = {
            "developerIdentity": "desktop\\developer",
            "launchIdentity": "desktop\\smoke",
            "developerLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchLocalLow": "C:/Users/Smoke/AppData/LocalLow",
            "launchPersistentDataPath": "C:/Users/Smoke/AppData/LocalLow/DefaultCompany/AnotherLifeUnity",
            "freshProfile": True,
            "profileChainHasNoReparsePoints": True,
        }
        log = "\n".join([
            launch["orderedEvidence"][0],
            launch["orderedEvidence"][1],
            launch["orderedEvidence"][2],
            launch["orderedEvidence"][3],
        ])

        passed = module.evaluate_windows_launch_smoke(log, isolation, launch)
        self.assertEqual(passed["status"], "passed")
        self.assertEqual(passed["observedEvidence"], launch["orderedEvidence"])

        same_profile = dict(isolation, launchIdentity="desktop\\developer")
        rejected = module.evaluate_windows_launch_smoke(log, same_profile, launch)
        self.assertEqual(rejected["status"], "stop_ship")
        self.assertEqual(rejected["reasonCode"], "launch_identity_not_isolated")

        wrong_order = "\n".join([launch["orderedEvidence"][1], launch["orderedEvidence"][0]])
        rejected = module.evaluate_windows_launch_smoke(wrong_order, isolation, launch)
        self.assertEqual(rejected["status"], "stop_ship")
        self.assertEqual(rejected["reasonCode"], "launch_evidence_out_of_order")

    def test_launch_smoke_runner_refuses_developer_identity_before_starting_player(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            artifact_root = root / "player"
            artifact_root.mkdir()
            executable = artifact_root / "AnotherLifeUnity.exe"
            executable.write_bytes(b"MZ" + b"\0" * 62)
            manifest_path = root / "build.json"
            module.write_signed_ready_manifest({
                "schemaVersion": 1,
                "target": "windows64-development",
                "status": "succeeded",
                "source": {"sourceRevision": "a" * 40, "sourceTreeSha256": "b" * 64},
                "artifacts": {
                    "root": artifact_root.as_posix(),
                    "treeSha256": "c" * 64,
                    "files": [{
                        "path": "AnotherLifeUnity.exe",
                        "bytes": executable.stat().st_size,
                        "sha256": module.sha256_file(executable),
                    }],
                },
            }, manifest_path)
            current_identity = module.current_windows_identity()
            current_local_low = module.current_windows_local_low()
            output = root / "launch.json"

            result = module.run_windows_launch_smoke(
                policy,
                manifest_path,
                output,
                developer_identity=current_identity,
                developer_local_low=current_local_low,
            )

            self.assertEqual(result["status"], "stop_ship")
            self.assertEqual(result["launchResult"]["reasonCode"], "launch_identity_not_isolated")
            self.assertTrue(output.is_file())

    def test_toolchain_inventory_captures_embedded_android_sdk_ndk_and_jdk(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            unity_exe = root / "Unity/Editor/Unity.exe"
            android = root / "Unity/Editor/Data/PlaybackEngines/AndroidPlayer"
            unity_exe.parent.mkdir(parents=True)
            unity_exe.write_bytes(b"MZ")
            sources = {
                android / "NDK/source.properties": "Pkg.Revision = 23.1.7779620\n",
                android / "SDK/build-tools/34.0.0/source.properties": "Pkg.Revision=34.0.0\n",
                android / "SDK/platforms/android-34/source.properties": "AndroidVersion.ApiLevel=34\n",
                android / "SDK/platform-tools/source.properties": "Pkg.Revision=32.0.0\n",
                android / "OpenJDK/release": 'JAVA_VERSION="11.0.14.1"\nIMPLEMENTOR="Eclipse Adoptium"\n',
            }
            for path, content in sources.items():
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8")
            repo = root / "repo"
            config = {
                "gradle/wrapper/gradle-wrapper.properties": "distributionUrl=https://example/gradle-9.4.1-bin.zip\n",
                "gradle/libs.versions.toml": 'agp = "9.2.1"\n',
                "unity/Packages/packages-lock.json": "{}\n",
            }
            for relative, content in config.items():
                path = repo / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8")

            inventory = module.collect_toolchain(repo, unity_exe, "2022.3.62f3")

        self.assertEqual(inventory["embeddedAndroid"]["ndkRevision"], "23.1.7779620")
        self.assertEqual(inventory["embeddedAndroid"]["buildTools"], ["34.0.0"])
        self.assertEqual(inventory["embeddedAndroid"]["platforms"], ["android-34"])
        self.assertEqual(inventory["embeddedAndroid"]["platformToolsRevision"], "32.0.0")
        self.assertEqual(inventory["embeddedAndroid"]["jdkVersion"], "11.0.14.1")
        self.assertEqual(inventory["hostGradle"]["wrapperVersion"], "9.4.1")
        self.assertEqual(inventory["hostGradle"]["androidGradlePluginVersion"], "9.2.1")

    def test_wrong_editor_version_fails_before_process_launch(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        calls = []

        result = module.preflight_build(
            REPO_ROOT,
            policy,
            "windows64-development",
            actual_unity_version="6000.5.3f1",
            process_launcher=lambda command: calls.append(command),
        )

        self.assertEqual(result["status"], "stop_ship")
        self.assertEqual(result["reasonCode"], "unity_version_mismatch")
        self.assertEqual(calls, [])

    def test_reproducibility_run_rejects_untracked_explicit_build_input(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            subprocess.run(["git", "init", "--quiet"], cwd=root, check=True)
            (root / "build.py").write_text("print('untracked')\n", encoding="utf-8")
            policy = {"sourceInputs": {"trackedRoots": [], "explicitFiles": ["build.py"]}}

            with self.assertRaisesRegex(module.BuildContractError, "dirty"):
                module._assert_clean_tracked_inputs(root, policy)

    def test_stopped_build_manifest_still_captures_traceability_inputs(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        module.probe_unity_version = lambda unused: "6000.5.3f1"
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / "stopped.json"
            payload = module.run_build(
                REPO_ROOT,
                policy,
                "windows64-development",
                Path("unused-unity.exe"),
                destination,
                clean_library=False,
            )

        self.assertEqual(payload["status"], "stop_ship")
        self.assertEqual(payload["toolchain"]["unityVersion"], "6000.5.3f1")
        self.assertEqual(payload["settings"]["scriptingBackend"], "Mono2x")
        self.assertEqual(len(payload["scenes"]), 5)
        self.assertRegex(payload["content"]["treeSha256"], r"^[0-9a-f]{64}$")
        self.assertRegex(payload["source"]["sourceRevision"], r"^[0-9a-f]{40}$")

    def test_repository_hygiene_runs_reproducibility_contract(self):
        workflow = (REPO_ROOT / ".github/workflows/quality-gates.yml").read_text(encoding="utf-8")
        self.assertIn(
            "python -m unittest tools.tests.test_reproducible_build tools.tests.test_android_unity_package",
            workflow,
        )
        self.assertIn("reproducible_build.py --repo-root . inventory", workflow)


if __name__ == "__main__":
    unittest.main()
