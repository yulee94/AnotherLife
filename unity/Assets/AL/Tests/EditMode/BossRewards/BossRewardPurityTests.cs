using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AL.Core.BossRewards;
using NUnit.Framework;

namespace AL.Tests.EditMode.BossRewards
{
    public class BossRewardPurityTests
    {
        private static readonly string[] ProhibitedEntropyTokens =
        {
            "DateTime.Now",
            "DateTime.UtcNow",
            "DateTimeOffset.Now",
            "DateTimeOffset.UtcNow",
            "Environment.TickCount",
            "Time.time",
            "UnityEngine.Random",
            "System.Random",
            ".GetHashCode("
        };

        [Test]
        public void PureAssemblyHasNoUnityEngineReference()
        {
            Assembly assembly = typeof(BossRewardComputation).Assembly;
            string[] references = assembly.GetReferencedAssemblies()
                .Select(item => item.Name)
                .ToArray();

            Assert.IsFalse(references.Any(item =>
                item.StartsWith("UnityEngine", StringComparison.Ordinal)));
        }

        [Test]
        public void PublicContractsExposeNoSetters()
        {
            Type[] contractTypes =
            {
                typeof(BossRewardBinding),
                typeof(BossRewardProfile),
                typeof(BossRewardEntry),
                typeof(BossEquipmentDefinitionSnapshot),
                typeof(BossRewardComputationRequest),
                typeof(BossRewardComputedValue),
                typeof(BossRewardComputedDrop),
                typeof(OwnedEquipmentSnapshot),
                typeof(BossRewardApplicationRequest),
                typeof(BossRewardApplicationPlan)
            };

            foreach (Type type in contractTypes)
            {
                PropertyInfo writable = type.GetProperties(BindingFlags.Public |
                                                           BindingFlags.Instance)
                    .FirstOrDefault(property => property.SetMethod != null);
                Assert.IsNull(writable, type.FullName + " exposes a public setter.");
            }
        }

        [Test]
        public void ComputationRequestContainsNoCallerValueOrDisplayAuthority()
        {
            string[] propertyNames = typeof(BossRewardComputationRequest)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToArray();

            CollectionAssert.DoesNotContain(propertyNames, "WarzoneCredits");
            CollectionAssert.DoesNotContain(propertyNames, "LootTable");
            CollectionAssert.DoesNotContain(propertyNames, "PlayerDisplayName");
            CollectionAssert.DoesNotContain(propertyNames, "BossName");
            CollectionAssert.DoesNotContain(propertyNames, "Timestamp");
            CollectionAssert.DoesNotContain(propertyNames, "RandomSeed");
        }

        [Test]
        public void BossRewardSourcesContainNoProhibitedEntropy()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string sourceRoot = Path.Combine(
                projectRoot,
                "Assets",
                "AL",
                "Scripts",
                "Core",
                "BossRewards");
            Assert.IsTrue(Directory.Exists(sourceRoot), sourceRoot);

            string combined = string.Join(
                "\n",
                Directory.GetFiles(sourceRoot, "*.cs", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
            foreach (string token in ProhibitedEntropyTokens)
                StringAssert.DoesNotContain(token, combined, token);
        }

        [Test]
        public void BoundedCopyStopsAtTechnicalCeiling()
        {
            Assert.Throws<ArgumentException>(() =>
                new BossRewardCatalogSnapshot(
                    BossRewardTestFixtures.GameId,
                    BossRewardTestFixtures.CatalogSetId,
                    BossRewardTestFixtures.SchemaVersion,
                    "catalog_revision",
                    TooManyBindings(),
                    Array.Empty<BossRewardProfile>(),
                    Array.Empty<BossEquipmentDefinitionSnapshot>(),
                    Array.Empty<string>()));
        }

        [Test]
        public void DiagnosticsHaveDeterministicSeverityCodeRecordFieldOrder()
        {
            var diagnostics = new[]
            {
                Diagnostic(
                    "AL-BOSS-REWARD-REQUEST-Z",
                    BossRewardDiagnosticSeverity.Warning,
                    "record_b",
                    "field_b"),
                Diagnostic(
                    "AL-BOSS-REWARD-REQUEST-A",
                    BossRewardDiagnosticSeverity.Error,
                    "record_a",
                    "field_a"),
                Diagnostic(
                    "AL-BOSS-REWARD-REQUEST-A",
                    BossRewardDiagnosticSeverity.Warning,
                    "record_a",
                    "field_b")
            };

            IReadOnlyList<BossRewardDiagnostic> ordered =
                BossRewardDiagnosticOrdering.Order(diagnostics);

            Assert.AreEqual(BossRewardDiagnosticSeverity.Warning, ordered[0].Severity);
            Assert.AreEqual("AL-BOSS-REWARD-REQUEST-A", ordered[0].Code);
            Assert.AreEqual("AL-BOSS-REWARD-REQUEST-Z", ordered[1].Code);
            Assert.AreEqual(BossRewardDiagnosticSeverity.Error, ordered[2].Severity);
        }

        [Test]
        public void DiagnosticSelectionPrecedesCapAndIgnoresInputPermutation()
        {
            BossRewardDiagnostic[] forward = Enumerable.Range(0, 200)
                .Select(index => Diagnostic(
                    "AL-BOSS-REWARD-REQUEST-BOUND",
                    BossRewardDiagnosticSeverity.Error,
                    "record_" +
                    index.ToString("D3", CultureInfo.InvariantCulture),
                    "field"))
                .ToArray();
            BossRewardDiagnostic[] reverse = forward
                .Reverse()
                .ToArray();

            IReadOnlyList<BossRewardDiagnostic> first =
                BossRewardDiagnosticOrdering.Order(forward);
            IReadOnlyList<BossRewardDiagnostic> second =
                BossRewardDiagnosticOrdering.Order(reverse);

            Assert.AreEqual(
                BossRewardTechnicalLimits.MaximumDiagnostics,
                first.Count);
            Assert.AreEqual(
                1,
                first.Count(item =>
                    item.Code ==
                    "AL-BOSS-REWARD-TRANSACTION-DIAGNOSTIC-LIMIT"));
            CollectionAssert.AreEqual(
                first.Select(DiagnosticIdentity).ToArray(),
                second.Select(DiagnosticIdentity).ToArray());
            Assert.IsTrue(first.Any(item => item.RecordId == "record_000"));
            Assert.IsFalse(first.Any(item => item.RecordId == "record_199"));
        }

        private static IEnumerable<BossRewardBinding> TooManyBindings()
        {
            for (int index = 0;
                 index <= BossRewardTechnicalLimits.MaximumCatalogEntries;
                 index++)
            {
                yield return new BossRewardBinding(
                    "boss_" + index,
                    "v1",
                    "profile_" + index,
                    "v1");
            }
        }

        private static BossRewardDiagnostic Diagnostic(
            string code,
            BossRewardDiagnosticSeverity severity,
            string recordId,
            string fieldPath)
        {
            return new BossRewardDiagnostic(
                code,
                severity,
                BossRewardDiagnosticDomain.Request,
                fieldPath,
                true,
                "safe",
                "operation",
                recordId);
        }

        private static string DiagnosticIdentity(
            BossRewardDiagnostic diagnostic)
        {
            return string.Join(
                "|",
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.RecordId,
                diagnostic.FieldPath,
                diagnostic.Domain,
                diagnostic.OperationId,
                diagnostic.BlocksOperation,
                diagnostic.SchemaVersion,
                diagnostic.ContentVersion,
                diagnostic.SafeDeveloperMessage);
        }
    }
}
