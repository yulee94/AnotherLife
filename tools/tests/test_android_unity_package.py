import importlib.util
import json
import struct
import tempfile
import unittest
import zipfile
from io import BytesIO
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
        classes = BytesIO()
        with zipfile.ZipFile(classes, "w") as jar:
            jar.writestr("com/unity3d/player/UnityPlayer.class", b"class")
        classes.seek(0)
        aar.writestr("classes.jar", classes.read())
        for name, data in extra:
            aar.writestr(name, data)


def dex_with_classes(*descriptors: str) -> bytes:
    header_size = 112
    string_ids_offset = header_size
    type_ids_offset = string_ids_offset + (4 * len(descriptors))
    class_defs_offset = type_ids_offset + (4 * len(descriptors))
    data_offset = class_defs_offset + (32 * len(descriptors))

    string_payloads = []
    next_string_offset = data_offset
    string_offsets = []
    for descriptor in descriptors:
        encoded = descriptor.encode("ascii")
        if len(encoded) >= 0x80:
            raise ValueError("test DEX helper only supports one-byte ULEB128 lengths")
        payload = bytes((len(encoded),)) + encoded + b"\0"
        string_offsets.append(next_string_offset)
        string_payloads.append(payload)
        next_string_offset += len(payload)

    data = bytearray(next_string_offset)
    data[:8] = b"dex\n035\0"
    struct.pack_into("<I", data, 32, len(data))
    struct.pack_into("<I", data, 36, header_size)
    struct.pack_into("<I", data, 40, 0x12345678)
    struct.pack_into("<II", data, 56, len(descriptors), string_ids_offset)
    struct.pack_into("<II", data, 64, len(descriptors), type_ids_offset)
    struct.pack_into("<II", data, 96, len(descriptors), class_defs_offset)
    struct.pack_into("<II", data, 104, len(data) - data_offset, data_offset)

    for index, string_offset in enumerate(string_offsets):
        struct.pack_into("<I", data, string_ids_offset + (index * 4), string_offset)
        struct.pack_into("<I", data, type_ids_offset + (index * 4), index)
        struct.pack_into("<I", data, class_defs_offset + (index * 32), index)
    cursor = data_offset
    for payload in string_payloads:
        data[cursor:cursor + len(payload)] = payload
        cursor += len(payload)
    return bytes(data)


def write_apk(path: Path, extra=(), omit=(), unity_asset=b"player-data", dex=None):
    required = {
        "AndroidManifest.xml": b"binary-manifest",
        "assets/bin/Data/globalgamemanagers": unity_asset,
        "lib/arm64-v8a/libmain.so": elf_aarch64(),
        "lib/arm64-v8a/libunity.so": elf_aarch64(),
        "lib/arm64-v8a/libil2cpp.so": elf_aarch64(),
        "classes.dex": dex or dex_with_classes("Lcom/unity3d/player/UnityPlayer;"),
    }
    with zipfile.ZipFile(path, "w") as apk:
        for name, data in required.items():
            if name not in omit:
                apk.writestr(name, data)
        for name, data in extra:
            apk.writestr(name, data)


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

    def test_valid_arm64_apk_is_bound_to_the_staged_aar(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "unityLibrary-debug.aar"
            write_aar(source)
            module.verify_and_stage(
                source, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
            )
            apk = root / "app-debug.apk"
            write_apk(apk)

            report = module.verify_packaged_apk(
                apk,
                root / "artifacts",
                "debug",
                "repo-sha",
                "2022.3.62f3",
            )

            self.assertEqual(report["schemaVersion"], 1)
            self.assertEqual(report["variant"], "debug")
            self.assertEqual(report["abis"], ["arm64-v8a"])
            self.assertEqual(
                report["unityPlayerClass"], "Lcom/unity3d/player/UnityPlayer;"
            )
            self.assertEqual(report["sourceAar"]["sha256"], module.sha256_file(source))
            self.assertEqual(report["apk"]["sha256"], module.sha256_file(apk))

    def test_apk_rejects_missing_unity_class_wrong_abi_and_non_aarch64_elf(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "unityLibrary-debug.aar"
            write_aar(source)
            module.verify_and_stage(
                source, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
            )

            missing_class = root / "missing-class.apk"
            write_apk(missing_class, dex=dex_with_classes("Lexample/Other;"))
            with self.assertRaisesRegex(module.PackageError, "UnityPlayer"):
                module.verify_packaged_apk(
                    missing_class, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
                )

            wrong_abi = root / "wrong-abi.apk"
            write_apk(wrong_abi, extra=(("lib/x86_64/libunity.so", elf_aarch64()),))
            with self.assertRaisesRegex(module.PackageError, "unsupported ABI"):
                module.verify_packaged_apk(
                    wrong_abi, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
                )

            wrong_elf = root / "wrong-elf.apk"
            data = bytearray(elf_aarch64())
            struct.pack_into("<H", data, 18, 62)
            write_apk(
                wrong_elf,
                extra=(("lib/arm64-v8a/libil2cpp.so", bytes(data)),),
                omit={"lib/arm64-v8a/libil2cpp.so"},
            )
            with self.assertRaisesRegex(module.PackageError, "AArch64"):
                module.verify_packaged_apk(
                    wrong_elf, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
                )

    def test_apk_rejects_unity_player_data_that_does_not_match_the_staged_aar(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "unityLibrary-release.aar"
            write_aar(source)
            module.verify_and_stage(
                source, root / "artifacts", "release", "repo-sha", "2022.3.62f3"
            )
            apk = root / "app-release.apk"
            write_apk(apk, unity_asset=b"different-player-data")

            with self.assertRaisesRegex(module.PackageError, "globalgamemanagers"):
                module.verify_packaged_apk(
                    apk, root / "artifacts", "release", "repo-sha", "2022.3.62f3"
                )

    def test_apk_rejects_native_library_that_does_not_match_the_staged_aar(self):
        module = load_module()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "unityLibrary-debug.aar"
            write_aar(source)
            module.verify_and_stage(
                source, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
            )
            changed_library = bytearray(elf_aarch64())
            changed_library[-1] = 1
            apk = root / "app-debug.apk"
            write_apk(
                apk,
                extra=(("lib/arm64-v8a/libunity.so", bytes(changed_library)),),
                omit={"lib/arm64-v8a/libunity.so"},
            )

            with self.assertRaisesRegex(module.PackageError, "libunity.so"):
                module.verify_packaged_apk(
                    apk, root / "artifacts", "debug", "repo-sha", "2022.3.62f3"
                )

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

    def test_host_exposes_variant_specific_final_apk_verification_tasks(self):
        gradle = (SCRIPT.parents[1] / "app/build.gradle.kts").read_text(encoding="utf-8")
        self.assertIn("verifyUnityDebugApk", gradle)
        self.assertIn("verifyUnityReleaseApk", gradle)
        self.assertIn('dependsOn("assembleDebug")', gradle)
        self.assertIn('dependsOn("assembleRelease")', gradle)
        self.assertIn('"--verify-apk"', gradle)
        self.assertIn('"verifyUnityDebugApk requires -PwithUnity=true."', gradle)
        self.assertIn('"verifyUnityReleaseApk requires -PwithUnity=true."', gradle)

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
