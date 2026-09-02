using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AL.Benchmarks.GoldenScenes;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace AL.EditorTools
{
    public sealed class GoldenSceneBenchmarkBuildIdentityProcessor : BuildPlayerProcessor
    {
        public override int callbackOrder => -2100;

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            if (buildPlayerContext == null)
                throw new ArgumentNullException(nameof(buildPlayerContext));
            EnsureRepositoryClean(ResolveRepositoryStatus());
            string sourceCommit = ResolveSourceCommit();
            GoldenSceneBuildIdentityMetadata metadata = CreateMetadataForBuild(
                Application.dataPath,
                sourceCommit,
                EditorUserBuildSettings.activeBuildTarget.ToString(),
                Application.unityVersion);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(
                projectRoot,
                "Library", "AL", "GoldenSceneBuildIdentity",
                GoldenSceneBuildIdentityContract.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);
            File.WriteAllText(path, metadata.ToJson());
            buildPlayerContext.AddAdditionalPathToStreamingAssets(
                path,
                GoldenSceneBuildIdentityContract.RelativePath);
        }

        internal static GoldenSceneBuildIdentityMetadata CreateMetadataForBuild(
            string assetsRoot,
            string sourceCommit,
            string buildTarget,
            string unityVersion)
        {
            return CreateMetadataForBuildAtTimestamp(
                assetsRoot,
                sourceCommit,
                buildTarget,
                unityVersion,
                ResolveSourceCommitTimestamp(sourceCommit));
        }

        internal static GoldenSceneBuildIdentityMetadata CreateMetadataForBuildAtTimestamp(
            string assetsRoot,
            string sourceCommit,
            string buildTarget,
            string unityVersion,
            string generatedAtUtc)
        {
            if (string.IsNullOrWhiteSpace(assetsRoot))
                throw new BuildFailedException("AL-GS-BUILD-ASSETS-ROOT-MISSING");
            if (!IsLowerHex(sourceCommit, 40))
                throw new BuildFailedException("AL-GS-BUILD-COMMIT-INVALID");
            if (string.IsNullOrWhiteSpace(buildTarget))
                throw new BuildFailedException("AL-GS-BUILD-TARGET-MISSING");
            if (string.IsNullOrWhiteSpace(unityVersion))
                throw new BuildFailedException("AL-GS-BUILD-UNITY-VERSION-MISSING");
            if (!DateTime.TryParse(
                    generatedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTime generatedAt))
                throw new BuildFailedException("AL-GS-BUILD-TIME-INVALID");

            string catalogPath = Path.Combine(
                assetsRoot,
                "AL", "StreamingAssets", "GameData",
                GoldenSceneCatalogContract.FileName);
            if (!File.Exists(catalogPath))
                throw new BuildFailedException("AL-GS-BUILD-CATALOG-MISSING: " + catalogPath);

            GoldenSceneCatalogLoadResult catalog;
            try
            {
                catalog = GoldenSceneCatalogLoader.Validate(File.ReadAllBytes(catalogPath));
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(
                    "AL-GS-BUILD-CATALOG-INVALID: " + exception.Message);
            }
            if (!catalog.IsAccepted)
                throw new BuildFailedException("AL-GS-BUILD-CATALOG-REJECTED");

            string normalizedTarget = buildTarget.Trim();
            string buildId = "al-gs-" +
                             generatedAt.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture) +
                             "-" + sourceCommit.Substring(0, 12) +
                             "-" + normalizedTarget;
            return new GoldenSceneBuildIdentityMetadata(
                buildId,
                sourceCommit,
                catalog.CatalogFingerprint,
                unityVersion.Trim(),
                normalizedTarget,
                GoldenSceneBuildIdentityContract.RenderPipeline,
                generatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        internal static string ResolveSourceCommit()
        {
            string output = RunGit("rev-parse HEAD", "AL-GS-BUILD-COMMIT-UNAVAILABLE");
            if (!IsLowerHex(output, 40))
                throw new BuildFailedException("AL-GS-BUILD-COMMIT-INVALID");
            return output;
        }

        private static string ResolveSourceCommitTimestamp(string sourceCommit)
        {
            if (!IsLowerHex(sourceCommit, 40))
                throw new BuildFailedException("AL-GS-BUILD-COMMIT-INVALID");
            string output = RunGit(
                "show -s --format=%cI " + sourceCommit,
                "AL-GS-BUILD-COMMIT-TIME-UNAVAILABLE");
            if (!DateTimeOffset.TryParse(
                    output,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset timestamp))
                throw new BuildFailedException("AL-GS-BUILD-COMMIT-TIME-INVALID");
            return timestamp.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        }

        private static string ResolveRepositoryStatus()
        {
            return RunGit(
                "status --porcelain --untracked-files=all",
                "AL-GS-BUILD-REPOSITORY-STATUS-UNAVAILABLE");
        }

        internal static void EnsureRepositoryClean(string porcelainStatus)
        {
            if (!string.IsNullOrWhiteSpace(porcelainStatus))
                throw new BuildFailedException(
                    "AL-GS-BUILD-REPOSITORY-DIRTY: commit or remove all tracked and untracked content before building.");
        }

        private static string RunGit(string arguments, string failureCode)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    throw new BuildFailedException("AL-GS-BUILD-COMMIT-PROCESS-FAILED");
                string output = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd().Trim();
                if (!process.WaitForExit(10000) || process.ExitCode != 0)
                    throw new BuildFailedException(
                        failureCode + ": " + error);
                return output;
            }
        }

        private static bool IsLowerHex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'a' && character <= 'f')))
                    return false;
            }
            return true;
        }
    }
}
