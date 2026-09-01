import importlib.util
import json
import struct
import tempfile
import unittest
import zipfile
from pathlib import Path

SCRIPT = Path(__file__).parents[1] / "android_unity_package.py"


def load_module():
    spec = importlib.util.spec_from_file_location("android_unity_package", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def elf_aarch64():
    data = bytearray(64)
    data[:4] = b"\x7fELF"
    data[4] = 2
    data[5] = 1
    data[6] = 1
    struct.pack_into("<H", data, 16, 3)
    struct.pack_into("<H", data, 18, 183)
    return bytes(data)


def write_aar(path: Path, extra=(), omit=()):
    required = {
        "AndroidManifest.xml": b"<manifest />",
        "assets/bin/Data/globalgamemanagers": b"player-data",
        "jni/arm64-v8a/libmain.so": elf_aarch64(),
        "jni/arm64-v8a/libunity.so": elf_aarch64(),
        "jni/arm64-v8a/libil2cpp.so": elf_aarch64(),
        "proguard.txt": b"-keep class com.unity3d.** { *; }",
    }
    with zipfile.ZipFile(path, "w") as aar:
        for name, data in required.items():
            if name not in omit:
                aar.writestr(name, data)
        with tempfile.SpooledTemporaryFile() as classes:
            with zipfile.ZipFile(classes, "w") as jar:
                jar.writestr("com/unity3d/player/UnityPlayer.class", b"class")
            classes.seek(0)
            aar.writestr("classes.jar", classes.read())
        for name, data in extra:
            aar.writestr(name, data)


class AndroidUnityPackageTests(unittest.TestCase):
    def test_valid_arm64_aar_is_staged_with_deterministic_inventory(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "unityLibrary-debug.aar"
            write_aar(source)
            staged, inventory = module.verify_and_stage(
                source, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
            )
            self.assertEqual(staged, root / "artifacts/debug/unityLibrary-debug.aar")
            payload = json.loads(inventory.read_text(encoding="utf-8"))
            self.assertEqual(payload["schemaVersion"], 1)
            self.assertEqual(payload["variant"], "debug")
            self.assertEqual(payload["abis"], ["arm64-v8a"])
            self.assertEqual(payload["repositorySha"], "repo-sha")
            self.assertEqual(payload["unityVersion"], "2022.3.62f3")
            self.assertEqual(payload["buildProfile"], {
                "development": True,
                "il2CppCompilerConfiguration": "Debug",
                "managedStrippingLevel": "Minimal",
            })
            self.assertEqual(payload["aar"]["path"], "unityLibrary-debug.aar")
            module.verify_staged(root / "artifacts", "debug", "repo-sha", "2022.3.62f3")
            with self.assertRaisesRegex(module.PackageError, "repository SHA"):
                module.verify_staged(root / "artifacts", "debug", "other-sha", "2022.3.62f3")
            with self.assertRaisesRegex(module.PackageError, "Unity version"):
                module.verify_staged(root / "artifacts", "debug", "repo-sha", "other-version")
            payload["buildProfile"]["development"] = False
            inventory.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(module.PackageError, "build profile"):
                module.verify_staged(root / "artifacts", "debug", "repo-sha", "2022.3.62f3")
            payload["buildProfile"]["development"] = True
            inventory.write_text(json.dumps(payload), encoding="utf-8")
            self.assertEqual(
                sorted(item["path"] for item in payload["requiredEntries"]),
                sorted(module.REQUIRED_ENTRIES),
            )

    def test_missing_il2cpp_fails_closed(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "unityLibrary-release.aar"
            write_aar(source, omit={"jni/arm64-v8a/libil2cpp.so"})
            with self.assertRaisesRegex(module.PackageError, "libil2cpp.so"):
                module.verify_and_stage(source, root / "artifacts", "release", "sha", "2022.3.62f3")

    def test_wrong_abi_and_non_aarch64_elf_fail_closed(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            wrong_abi = root / "wrong-abi.aar"
            write_aar(wrong_abi, extra=(("jni/x86_64/libunity.so", elf_aarch64()),))
            with self.assertRaisesRegex(module.PackageError, "unsupported ABI"):
                module.verify_and_stage(wrong_abi, root / "out", "debug", "sha", "2022.3.62f3")

            wrong_elf = root / "wrong-elf.aar"
            data = bytearray(elf_aarch64())
            struct.pack_into("<H", data, 18, 62)
            write_aar(
                wrong_elf,
                extra=(("jni/arm64-v8a/libil2cpp.so", bytes(data)),),
                omit={"jni/arm64-v8a/libil2cpp.so"},
            )
            with self.assertRaisesRegex(module.PackageError, "AArch64"):
                module.verify_and_stage(wrong_elf, root / "out", "debug", "sha", "2022.3.62f3")

    def test_release_and_debug_are_the_only_variants(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "unityLibrary-profile.aar"
            write_aar(source)
            with self.assertRaisesRegex(module.PackageError, "variant"):
                module.verify_and_stage(source, Path(temporary) / "out", "profile", "sha", "2022.3.62f3")

    def test_host_declares_opt_in_variant_aware_arm64_dependencies(self):
        gradle = (SCRIPT.parents[1] / "app/build.gradle.kts").read_text(encoding="utf-8")
        self.assertIn('providers.gradleProperty("withUnity")', gradle)
        self.assertIn('abiFilters += "arm64-v8a"', gradle)
        self.assertIn('debugImplementation(files(unityDebugAar))', gradle)
        self.assertIn('releaseImplementation(files(unityReleaseAar))', gradle)
        self.assertNotIn("pickFirst", gradle)

    def test_host_build_fails_when_opted_in_artifacts_are_missing_or_stale(self):
        gradle = (SCRIPT.parents[1] / "app/build.gradle.kts").read_text(encoding="utf-8")
        self.assertIn("verifyUnityDebugPackageInput", gradle)
        self.assertIn("verifyUnityReleasePackageInput", gradle)
        self.assertIn("--verify-only", gradle)
        self.assertIn('tasks.matching { it.name == "preDebugBuild" }', gradle)
        self.assertIn('tasks.matching { it.name == "preReleaseBuild" }', gradle)
        self.assertNotIn('tasks.named("preBuild")', gradle)

    def test_exporter_declares_distinct_debug_and_release_profiles(self):
        exporter = (
            SCRIPT.parents[1]
            / "unity/Assets/AL/Scripts/Editor/AndroidUnityLibraryExporter.cs"
        ).read_text(encoding="utf-8")
        self.assertIn("ExportReleaseArm64Il2Cpp", exporter)
        self.assertIn("BuildOptions.Development", exporter)
        self.assertIn("Il2CppCompilerConfiguration.Debug", exporter)
        self.assertIn("Il2CppCompilerConfiguration.Release", exporter)
        self.assertIn("ManagedStrippingLevel.Medium", exporter)


if __name__ == "__main__":
    unittest.main()
