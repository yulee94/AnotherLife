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
        self.assertEqual(policy["launchSmoke"]["isolationMode"], "current_authenticated_user")
        self.assertFalse(policy["launchSmoke"]["isolatedProfileClaimed"])
        self.assertEqual(policy["launchSmoke"]["continueControl"], "keyboard_enter")
        self.assertEqual(policy["launchSmoke"]["orderedEvidence"], [
            "[AL-SCENE-ACTIVE] id=al_scene_boot name=Boot path=Assets/AL/Scenes/Boot.unity role=production_entry version=223.2",
            "AL Boot Sequence Started...",
            "[AL-SCENE-ACTIVE] id=al_scene_realm_selection name=RealmSelection path=Assets/AL/Scenes/RealmSelection.unity role=onboarding_selection version=223.2",
        ])
        android = policy["deferredAndroid"]
        self.assertEqual(android["task"], "t_7b530af7")
        self.assertEqual(android["status"], "deferred_pc_first")
        self.assertEqual(android["approvedEditor"], "6000.3.22f1")
        self.assertEqual(android["legacyExporterEditor"], "2022.3.62f3")
        self.assertFalse(android["legacyCrossVersionProjectAuthorized"])
        self.assertFalse(android["androidModuleAvailable"])
        self.assertTrue(
            {
                "tools/qa/run_deterministic_qa.py",
                "tools/qa/deterministic_qa_policy.json",
                "tools/qa/manual_results.v1.json",
                "unity/SharedContracts/integrated-qa-evidence.schema.json",
            }.issubset(policy["sourceInputs"]["explicitFiles"])
        )

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

    def test_unity6_builtin_package_pins_match_the_resolved_lock(self):
        manifest = json.loads((REPO_ROOT / "unity/Packages/manifest.json").read_text(encoding="utf-8"))
        lock = json.loads((REPO_ROOT / "unity/Packages/packages-lock.json").read_text(encoding="utf-8"))
        expected = {
            "com.unity.test-framework": "1.6.0",
            "com.unity.textmeshpro": "5.0.0",
            "com.unity.ugui": "2.0.0",
        }

        for package, version in expected.items():
            self.assertEqual(manifest["dependencies"][package], version)
            self.assertEqual(lock["dependencies"][package]["version"], version)

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

    def test_windows_boot_connection_guid_is_exactly_normalized_with_raw_hashes_preserved(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifests = []
            for name, connection_guid in (("first", "123"), ("second", "987654321")):
                player = root / name
                data = player / "AnotherLifeUnity_Data"
                data.mkdir(parents=True)
                (player / "AnotherLifeUnity.exe").write_bytes(b"MZ" + b"\0" * 62)
                (data / "globalgamemanagers").write_bytes(b"manager")
                (data / "boot.config").write_text(
                    "player-connection-mode=Listen\n"
                    f"player-connection-guid={connection_guid}\n"
                    "player-connection-ip=192.0.2.10\n",
                    encoding="utf-8",
                )
                artifacts = module.inspect_artifacts(player, "windows64-development")
                artifacts["root"] = "player"
                manifests.append({
                    "target": "windows64-development",
                    "artifacts": artifacts,
                    "run": {"host": name},
                })

            first_boot = next(
                item for item in manifests[0]["artifacts"]["files"]
                if item["path"] == "AnotherLifeUnity_Data/boot.config"
            )
            second_boot = next(
                item for item in manifests[1]["artifacts"]["files"]
                if item["path"] == "AnotherLifeUnity_Data/boot.config"
            )
            self.assertNotEqual(first_boot["sha256"], second_boot["sha256"])
            self.assertEqual(first_boot["reproducibleSha256"], second_boot["reproducibleSha256"])
            self.assertEqual(first_boot["normalization"], ["player-connection-guid"])

            comparison = module.compare_manifests(*manifests)
            self.assertEqual(comparison["status"], "normalized_equivalent")
            self.assertIn(
                "AnotherLifeUnity_Data/boot.config:player-connection-guid",
                comparison["normalization"],
            )

            manifests[1]["artifacts"]["files"][2]["reproducibleSha256"] = "f" * 64
            comparison = module.compare_manifests(*manifests)
            self.assertEqual(comparison["status"], "stop_ship")

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

    def test_launch_smoke_accepts_current_user_and_refuses_isolation_inference(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        launch = policy["launchSmoke"]
        isolation = {
            "method": "current_authenticated_user",
            "isolatedProfileClaimed": False,
            "developerIdentity": "desktop\\developer",
            "launchIdentity": "desktop\\developer",
            "developerLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchPersistentDataPath": "C:/Users/Developer/AppData/LocalLow/DefaultCompany/AnotherLifeUnity",
            "freshProfile": False,
            "profileChainHasNoReparsePoints": True,
        }
        log = "\n".join(launch["orderedEvidence"])

        passed = module.evaluate_windows_launch_smoke(log, isolation, launch)
        self.assertEqual(passed["status"], "passed")
        self.assertEqual(passed["reasonCode"], "boot_to_realm_selection")
        self.assertEqual(passed["observedEvidence"], launch["orderedEvidence"])
        self.assertFalse(passed["isolatedProfileClaimed"])

        claimed = dict(isolation, isolatedProfileClaimed=True)
        rejected = module.evaluate_windows_launch_smoke(log, claimed, launch)
        self.assertEqual(rejected["status"], "stop_ship")
        self.assertEqual(rejected["reasonCode"], "isolated_profile_not_claimed")

        other_user = dict(isolation, launchIdentity="desktop\\smoke")
        rejected = module.evaluate_windows_launch_smoke(log, other_user, launch)
        self.assertEqual(rejected["status"], "stop_ship")
        self.assertEqual(rejected["reasonCode"], "launch_identity_not_current_user")

        wrong_order = "\n".join([launch["orderedEvidence"][1], launch["orderedEvidence"][0]])
        rejected = module.evaluate_windows_launch_smoke(wrong_order, isolation, launch)
        self.assertEqual(rejected["status"], "stop_ship")
        self.assertEqual(rejected["reasonCode"], "launch_evidence_out_of_order")

    def test_launch_smoke_arms_continue_only_after_boot_sequence_started(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        launch = policy["launchSmoke"]
        isolation = {
            "method": "current_authenticated_user",
            "isolatedProfileClaimed": False,
            "developerIdentity": "desktop\\developer",
            "launchIdentity": "desktop\\developer",
            "developerLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchPersistentDataPath": "C:/Users/Developer/AppData/LocalLow/DefaultCompany/AnotherLifeUnity",
            "freshProfile": False,
            "profileChainHasNoReparsePoints": True,
        }
        boot_only = module.evaluate_windows_launch_smoke(launch["orderedEvidence"][0], isolation, launch)
        self.assertFalse(module.should_send_explicit_continue(boot_only, launch))

        boot_ready = module.evaluate_windows_launch_smoke(
            "\n".join(launch["orderedEvidence"][:2]),
            isolation,
            launch,
        )
        self.assertTrue(module.should_send_explicit_continue(boot_ready, launch))

        complete = module.evaluate_windows_launch_smoke("\n".join(launch["orderedEvidence"]), isolation, launch)
        self.assertFalse(module.should_send_explicit_continue(complete, launch))

    def test_launch_smoke_does_not_stop_on_current_user_recovery_required(self):
        module = load_module()
        policy = module.load_policy(POLICY)
        launch = policy["launchSmoke"]
        isolation = {
            "method": "current_authenticated_user",
            "isolatedProfileClaimed": False,
            "developerIdentity": "desktop\\developer",
            "launchIdentity": "desktop\\developer",
            "developerLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchLocalLow": "C:/Users/Developer/AppData/LocalLow",
            "launchPersistentDataPath": "C:/Users/Developer/AppData/LocalLow/DefaultCompany/AnotherLifeUnity",
            "freshProfile": False,
            "profileChainHasNoReparsePoints": True,
        }
        log = "\n".join([
            "AL-SAVE-RECOVERY-REQUIRED: Existing generations were preserved because none can be activated without an explicit recovery decision.",
            "[BOOT_STACK_LOAD_FAILED] Bootloader save load failed: Load status RecoveryRequired; current save present: False.",
            launch["orderedEvidence"][0],
            launch["orderedEvidence"][1],
        ])

        result = module.evaluate_windows_launch_smoke(log, isolation, launch)

        self.assertNotEqual(result["reasonCode"], "launch_failure_token")
        self.assertEqual(result["status"], "running")
        self.assertEqual(result["observedEvidence"], launch["orderedEvidence"][:2])
        self.assertTrue(module.should_send_explicit_continue(result, launch))

    def test_persistent_overlay_restores_user_saves(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            live = Path(temporary) / "AnotherLifeUnity"
            live.mkdir()
            (live / "save.tmp.json").write_text("keep-me", encoding="utf-8")
            nested = live / "quarantine" / "save.json"
            nested.parent.mkdir()
            nested.write_text("nested", encoding="utf-8")

            with module.temporary_empty_persistent_data(live) as overlay:
                self.assertTrue(overlay["freshProfile"])
                self.assertTrue(overlay["userSavePreserved"])
                self.assertFalse((live / "save.tmp.json").exists())
                (live / "smoke-only.txt").write_text("transient", encoding="utf-8")

            self.assertEqual((live / "save.tmp.json").read_text(encoding="utf-8"), "keep-me")
            self.assertEqual(nested.read_text(encoding="utf-8"), "nested")
            self.assertFalse((live / "smoke-only.txt").exists())
            self.assertFalse((live.parent / "AnotherLifeUnity.pre-smoke").exists())

    def test_launch_smoke_runner_accepts_current_user_and_does_not_claim_isolation(self):
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
            launches = []

            class FakeProcess:
                pid = 4242
                returncode = 1

                def poll(self):
                    return 1

                def terminate(self):
                    return None

                def kill(self):
                    return None

                def wait(self, timeout=None):
                    return 1

            original_popen = module.subprocess.Popen

            def fake_popen(command, cwd=None, **kwargs):
                if command and str(command[0]).endswith("AnotherLifeUnity.exe"):
                    launches.append({"command": command, "cwd": cwd})
                    return FakeProcess()
                return original_popen(command, cwd=cwd, **kwargs)

            module.subprocess.Popen = fake_popen
            try:
                result = module.run_windows_launch_smoke(
                    policy,
                    manifest_path,
                    output,
                    developer_identity=current_identity,
                    developer_local_low=current_local_low,
                    overlay_persistent_data=False,
                )
            finally:
                module.subprocess.Popen = original_popen

            self.assertEqual(result["isolation"]["method"], "current_authenticated_user")
            self.assertFalse(result["isolation"]["isolatedProfileClaimed"])
            self.assertEqual(result["isolation"]["developerIdentity"], current_identity)
            self.assertEqual(result["isolation"]["launchIdentity"], current_identity)
            self.assertNotEqual(result["launchResult"]["reasonCode"], "launch_identity_not_isolated")
            self.assertEqual(len(launches), 1)
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

    def test_repository_hygiene_runs_integrated_qa_contract(self):
        workflow = (REPO_ROOT / ".github/workflows/quality-gates.yml").read_text(encoding="utf-8")
        self.assertIn(
            "python tools/qa/test_run_deterministic_qa.py",
            workflow,
        )
        self.assertIn(
            "python tools/qa/run_deterministic_qa.py --repo-root . --profile contract",
            workflow,
        )
        self.assertIn("artifacts/deterministic-qa/report.json", workflow)
        self.assertIn("python tools/qa/test_realm_slice_evidence.py", workflow)


if __name__ == "__main__":
    unittest.main()
