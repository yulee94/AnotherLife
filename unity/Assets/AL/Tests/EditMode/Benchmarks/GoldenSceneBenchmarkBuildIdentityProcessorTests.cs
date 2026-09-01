using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Benchmarks.GoldenScenes;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;

namespace AL.Tests.EditMode.Benchmarks
{
    public sealed class GoldenSceneBenchmarkBuildIdentityProcessorTests
    {
        [Test]
        public void BuildProcessorEmbedsValidatedCommitCatalogUnityTargetAndBuiltInIdentity()
        {
            Assert.That(
                GoldenSceneBuildIdentityContract.RelativePath,
                Is.EqualTo("GameData/al_golden_scene_build_identity.json"));
            Type processor = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "AL.EditorTools.GoldenSceneBenchmarkBuildIdentityProcessor",
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
            Assert.That(processor, Is.Not.Null);
            Assert.That(
                typeof(BuildPlayerProcessor).IsAssignableFrom(processor),
                Is.True,
                "Build identity must be generated before shared GameData paths are registered.");
            MethodInfo create = processor.GetMethod(
                "CreateMetadataForBuild",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(create, Is.Not.Null);

            GoldenSceneBuildIdentityMetadata metadata =
                (GoldenSceneBuildIdentityMetadata)create.Invoke(null, new object[]
                {
                    Application.dataPath,
                    "1aedfba024b7c82701494188492876a4b8a7828f",
                    "StandaloneWindows64",
                    "6000.3.22f1",
                    "2026-08-31T03:00:00.0000000Z"
                });
            string catalogPath = Path.Combine(
                Application.dataPath,
                "AL", "StreamingAssets", "GameData",
                GoldenSceneCatalogContract.FileName);
            string expectedFingerprint = GoldenSceneCatalogLoader
                .Validate(File.ReadAllBytes(catalogPath))
                .CatalogFingerprint;

            Assert.That(metadata.SourceCommit,
                Is.EqualTo("1aedfba024b7c82701494188492876a4b8a7828f"));
            Assert.That(metadata.CatalogFingerprint, Is.EqualTo(expectedFingerprint));
            Assert.That(metadata.UnityVersion, Is.EqualTo("6000.3.22f1"));
            Assert.That(metadata.BuildTarget, Is.EqualTo("StandaloneWindows64"));
            Assert.That(metadata.RenderPipeline,
                Is.EqualTo(GoldenSceneBuildIdentityContract.RenderPipeline));
            Assert.That(metadata.BuildId,
                Is.EqualTo("al-gs-20260831T030000Z-1aedfba024b7-StandaloneWindows64"));
            Assert.That(GoldenSceneBuildIdentityMetadata.TryParse(
                metadata.ToJson(),
                out GoldenSceneBuildIdentityMetadata parsed,
                out string diagnostic), Is.True, diagnostic);
            Assert.That(parsed.BuildId, Is.EqualTo(metadata.BuildId));
        }

        [Test]
        public void BuildProcessorRejectsRepositoryContentOutsideHead()
        {
            Type processor = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "AL.EditorTools.GoldenSceneBenchmarkBuildIdentityProcessor",
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
            Assert.That(processor, Is.Not.Null);
            MethodInfo ensureClean = processor.GetMethod(
                "EnsureRepositoryClean",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(ensureClean, Is.Not.Null);
            Assert.That(
                () => ensureClean.Invoke(null, new object[] { string.Empty }),
                Throws.Nothing);
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(
                () => ensureClean.Invoke(null, new object[] { " M Assets/test.cs" }));
            Assert.That(error.InnerException, Is.TypeOf<BuildFailedException>());
            Assert.That(error.InnerException.Message,
                Does.Contain("AL-GS-BUILD-REPOSITORY-DIRTY"));
        }

        [Test]
        public void EditorBootstrapOnlyStartsForIdleBatchRequest()
        {
            Type bootstrap = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "AL.EditorTools.GoldenSceneBenchmarkEditorBootstrap",
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
            Assert.That(bootstrap, Is.Not.Null);
            MethodInfo shouldEnter = bootstrap.GetMethod(
                "ShouldEnterPlayMode",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(shouldEnter, Is.Not.Null);

            Assert.That((bool)shouldEnter.Invoke(null, new object[]
            {
                new[] { "Unity.exe", "--al-gs-run" }, true, false, false
            }), Is.True);
            Assert.That((bool)shouldEnter.Invoke(null, new object[]
            {
                new[] { "Unity.exe" }, true, false, false
            }), Is.False);
            Assert.That((bool)shouldEnter.Invoke(null, new object[]
            {
                new[] { "Unity.exe", "--al-gs-run" }, false, false, false
            }), Is.False);
            Assert.That((bool)shouldEnter.Invoke(null, new object[]
            {
                new[] { "Unity.exe", "--al-gs-run" }, true, true, false
            }), Is.False);
        }
    }
}