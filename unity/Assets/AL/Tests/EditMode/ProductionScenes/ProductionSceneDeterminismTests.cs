using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using R = AL.Tests.EditMode.ProductionScenes.ProductionSceneTestReflection;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Determinism contract (#223 "Determinism and source control"). Declared comparison for this PR:
    /// semantic YAML equivalence via the validator is AUTHORITATIVE (Unity local file IDs may legitimately
    /// differ across environments); byte-level scene/.meta stability is reported as SUPPORTING evidence.
    /// Two validation/generation cycles must produce no unreviewed scene/.meta change.
    /// </summary>
    public sealed class ProductionSceneDeterminismTests
    {
        [Test]
        public void TwoValidationRunsAreSemanticallyEquivalent()
        {
            Dictionary<string, string> first = SemanticFingerprint();
            Dictionary<string, string> second = SemanticFingerprint();

            Assert.That(second.Keys, Is.EquivalentTo(first.Keys));
            foreach (var pair in first)
            {
                Assert.AreEqual(pair.Value, second[pair.Key], $"Semantic drift across runs for {pair.Key}");
            }
        }

        [Test]
        public void GenerateThenValidateProducesNoByteDiff_SupportingEvidence()
        {
            Dictionary<string, byte[]> before = SceneFileBytes();

            R.StaticMethod(R.GeneratorType, "GenerateMissingProductionScenes");
            R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");

            Dictionary<string, byte[]> after = SceneFileBytes();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            foreach (var pair in before)
            {
                Assert.AreEqual(pair.Value, after[pair.Key], $"Byte diff across generate/validate cycle for {pair.Key}");
            }
        }

        [Test]
        public void ValidationIsAuthoritativeAndReportsValidInventory()
        {
            object report = R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");
            Assert.IsTrue(R.PropBool(report, "IsValid"), R.Invoke(report, "Summarize").ToString());
            Assert.IsEmpty(R.AsStrings(R.Prop(report, "InventoryFailures")));
            Assert.AreEqual(5, R.AsObjects(R.Prop(report, "Scenes")).Count);
        }

        private static Dictionary<string, string> SemanticFingerprint()
        {
            object report = R.StaticMethod(R.GeneratorType, "ValidateProductionScenes");
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (object scene in R.AsObjects(R.Prop(report, "Scenes")))
            {
                string id = R.PropString(scene, "SceneId");
                var transitions = (System.Collections.IDictionary)R.Prop(scene, "TransitionValues");
                var transitionText = string.Join(",", transitions.Keys.Cast<object>()
                    .OrderBy(k => k.ToString(), StringComparer.Ordinal)
                    .Select(k => $"{k}={transitions[k]}"));

                map[id] = string.Join("|",
                    "status=" + R.Prop(scene, "Status"),
                    "name=" + R.PropString(scene, "SceneName"),
                    "guid=" + R.PropString(scene, "ActualAssetGuid"),
                    "controllers=" + R.PropInt(scene, "ControllerCount"),
                    "eventSystems=" + R.PropInt(scene, "EventSystemCount"),
                    "markers=" + R.PropInt(scene, "MarkerCount"),
                    "bootloaders=" + R.PropInt(scene, "BootloaderCount"),
                    "missingScripts=" + R.PropInt(scene, "MissingScriptCount"),
                    "transitions=" + transitionText);
            }

            return map;
        }

        private static Dictionary<string, byte[]> SceneFileBytes()
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (object record in R.DescriptorProduction())
            {
                string path = R.PropString(record, "AssetPath");
                if (File.Exists(path))
                {
                    map[path] = File.ReadAllBytes(path);
                }

                if (File.Exists(path + ".meta"))
                {
                    map[path + ".meta"] = File.ReadAllBytes(path + ".meta");
                }
            }

            return map;
        }
    }
}
