using System;
using System.IO;
using AL.Narrative.Nvs01;
using AL.Narrative.Nvs01.Contracts;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AL.Editor.Narrative
{
    public sealed class ExportNvs01Catalog : IPreprocessBuildWithReport
    {
        private const string SourceProjectPath = "Docs/Narrative/NVS_01/OMEN_1_A1.packet.json";
        private const string ArtifactAssetPath = "Assets/StreamingAssets/AL/Narrative/OMEN_1.catalog.json";

        public int callbackOrder => -500;

        [MenuItem("AL/Narrative/Export NVS-01 Catalog")]
        public static void ExportMenu()
        {
            bool changed = ExportOrThrow();
            Debug.Log(changed
                ? "[NVS-01] Exported canonical OMEN_1 runtime catalog."
                : "[NVS-01] OMEN_1 runtime catalog is already canonical.");
        }

        [MenuItem("AL/Narrative/Verify NVS-01 Catalog")]
        public static void VerifyMenu()
        {
            VerifyOrThrow();
            Debug.Log("[NVS-01] OMEN_1 runtime catalog matches the approved canonical source.");
        }

        public static bool ExportOrThrow()
        {
            byte[] canonical = ReadCanonicalSourceOrThrow();
            string artifactPath = ArtifactAbsolutePath();
            bool changed = !File.Exists(artifactPath) || !BytesEqual(File.ReadAllBytes(artifactPath), canonical);
            if (!changed)
            {
                ValidateArtifactOrThrow(canonical);
                return false;
            }

            string directory = Path.GetDirectoryName(artifactPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("NVS-01 artifact directory could not be resolved.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(artifactPath, canonical);
            ValidateArtifactOrThrow(File.ReadAllBytes(artifactPath));
            AssetDatabase.ImportAsset(ArtifactAssetPath, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        public static void VerifyOrThrow()
        {
            byte[] canonical = ReadCanonicalSourceOrThrow();
            string artifactPath = ArtifactAbsolutePath();
            if (!File.Exists(artifactPath))
            {
                throw new BuildFailedException("NVS-01 runtime catalog is missing. Run AL/Narrative/Export NVS-01 Catalog.");
            }

            byte[] artifact = File.ReadAllBytes(artifactPath);
            if (!BytesEqual(artifact, canonical))
            {
                throw new BuildFailedException("NVS-01 runtime catalog has byte drift from the approved canonical A1 source.");
            }

            ValidateArtifactOrThrow(artifact);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            VerifyOrThrow();
        }

        private static byte[] ReadCanonicalSourceOrThrow()
        {
            string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SourceProjectPath));
            if (!File.Exists(sourcePath))
            {
                throw new InvalidOperationException("NVS-01 authoritative A1 packet is missing.");
            }

            byte[] canonical;
            Nvs01CatalogDiagnostic diagnostic;
            if (!Nvs01CatalogValidator.TryCanonicalizeSource(
                    File.ReadAllBytes(sourcePath),
                    out canonical,
                    out diagnostic))
            {
                throw new InvalidOperationException(FormatDiagnostic(diagnostic));
            }

            ValidateArtifactOrThrow(canonical);
            return canonical;
        }

        private static void ValidateArtifactOrThrow(byte[] bytes)
        {
            Nvs01CatalogValidationResult result = Nvs01CatalogValidator.ValidateCanonicalArtifact(bytes);
            if (result.IsAccepted)
            {
                return;
            }

            string detail = result.Diagnostics.Count > 0
                ? FormatDiagnostic(result.Diagnostics[0])
                : "AL-NVS01-VALIDATION: catalog rejected without a diagnostic.";
            throw new InvalidOperationException(detail);
        }

        private static string FormatDiagnostic(Nvs01CatalogDiagnostic diagnostic)
        {
            if (diagnostic == null) return "AL-NVS01-VALIDATION: missing diagnostic.";
            return diagnostic.Code + " at " + diagnostic.Path + ": " + diagnostic.Message +
                   " Expected=" + diagnostic.Expected + " Actual=" + diagnostic.Actual;
        }

        private static string ArtifactAbsolutePath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "StreamingAssets", "AL", "Narrative", "OMEN_1.catalog.json"));
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }

            return true;
        }
    }
}
