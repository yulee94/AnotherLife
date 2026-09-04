using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Art
{
    public sealed class StoneholdMasterGruffNpcFoundationTests
    {
        private static string RepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [Test]
        public void FoundationDccFilesExist()
        {
            string blend = Path.Combine(
                RepoRoot,
                "ArtSource",
                "NPCs",
                "rct_stonehold_npc_service_v001",
                "rct_stonehold_npc_service_humanoid_v001.blend");
            string fbx = Path.Combine(
                RepoRoot,
                "ArtSource",
                "NPCs",
                "rct_stonehold_npc_service_v001",
                "Exports",
                "rct_stonehold_npc_service_humanoid_v001.fbx");
            Assert.That(File.Exists(blend), Is.True, blend);
            Assert.That(File.Exists(fbx), Is.True, fbx);
        }

        [Test]
        public void ManifestRecordsIncompleteModularity()
        {
            string manifest = Path.Combine(
                RepoRoot,
                "Docs",
                "AssetLibrary",
                "StoneholdMasterGruffNpc3DSourceV001",
                "npc_3d_foundation_manifest_v001.json");
            Assert.That(File.Exists(manifest), Is.True, manifest);
            string json = File.ReadAllText(manifest);
            Assert.That(json, Does.Contain("\"productionReady\": false"));
            Assert.That(json, Does.Contain("incomplete_vertex_groups_only"));
            Assert.That(json, Does.Contain("rct_stonehold_npc_service_v001"));
        }
    }
}
