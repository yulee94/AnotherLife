using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class Nvs01CatalogTests
    {
        private const string ValidatorTypeName = "AL.Narrative.Nvs01.Nvs01CatalogValidator";
        private const string LoaderTypeName = "AL.Narrative.Nvs01.Nvs01CatalogLoader";
        private const string ContractTypeName = "AL.Narrative.Nvs01.Contracts.Nvs01CatalogContract";
        private const string ExporterTypeName = "AL.Editor.Narrative.ExportNvs01Catalog";
        private const string DiagnosticCodePrefix = "AL-NVS01-";
        private const string SourceRelativePath = "Docs/Narrative/NVS_01/OMEN_1_A1.packet.json";
        private const string ArtifactAssetRelativePath = "StreamingAssets/AL/Narrative/OMEN_1.catalog.json";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Test]
        public void CanonicalSourceArtifactAndContractIdentityMatch()
        {
            byte[] source = File.ReadAllBytes(SourceAbsolutePath());
            byte[] artifact = CanonicalArtifactBytes();
            object diagnostic;
            byte[] canonical = Canonicalize(source, out diagnostic);

            Assert.NotNull(canonical, DiagnosticSummary(diagnostic));
            Assert.IsNull(diagnostic);
            CollectionAssert.AreEqual(artifact, canonical, "The runtime artifact must be the normalized A1 source bytes, not a separately-authored copy.");

            Assert.AreEqual(8317, ContractField<int>("CanonicalByteLength"));
            Assert.AreEqual(ContractField<int>("CanonicalByteLength"), artifact.Length);
            Assert.AreEqual(
                ContractField<string>("CanonicalSha256"),
                ComputeSha256(artifact));
            Assert.AreEqual("omen1-a1-2026-07-22-v002", ContractField<string>("PacketVersion"));
            Assert.AreEqual("AL/Narrative/OMEN_1.catalog.json", ContractField<string>("StreamingAssetsRelativePath"));

            object result = Validate(artifact, true);
            AssertAccepted(result);
            object verified = GetProperty(result, "VerifiedCatalog");
            Assert.AreEqual(artifact.Length, GetProperty(verified, "CanonicalByteLength"));
            Assert.AreEqual(ComputeSha256(artifact), GetProperty(verified, "CanonicalSha256"));
        }

        [Test]
        public void CrLfSourceNormalizesToCanonicalArtifact()
        {
            byte[] canonical = CanonicalArtifactBytes();
            string canonicalText = StrictUtf8.GetString(canonical);
            byte[] crlfSource = StrictUtf8.GetBytes(canonicalText.Replace("\n", "\r\n"));

            object diagnostic;
            byte[] normalized = Canonicalize(crlfSource, out diagnostic);

            Assert.NotNull(normalized, DiagnosticSummary(diagnostic));
            Assert.IsNull(diagnostic);
            CollectionAssert.AreEqual(canonical, normalized);
            Assert.False(normalized.Contains((byte)'\r'));
        }

        [Test]
        public void CanonicalRuntimeRejectsEveryNonCanonicalByteForm()
        {
            byte[] canonical = CanonicalArtifactBytes();
            string json = StrictUtf8.GetString(canonical);

            AssertRejected(StrictUtf8.GetBytes(json.Replace("\n", "\r\n")), true, "CATALOG-MALFORMED");
            AssertRejected(Prepend(new byte[] { 0xef, 0xbb, 0xbf }, canonical), true, "CATALOG-MALFORMED");

            byte[] bareCr = (byte[])canonical.Clone();
            bareCr[Array.IndexOf(bareCr, (byte)'\n')] = (byte)'\r';
            AssertRejected(bareCr, true, "CATALOG-MALFORMED");

            byte[] missingFinalLf = new byte[canonical.Length - 1];
            Buffer.BlockCopy(canonical, 0, missingFinalLf, 0, missingFinalLf.Length);
            AssertRejected(missingFinalLf, true, "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("\"questId\": \"OMEN_1\"", "\"questId\": \"OMEN_X\""),
                true,
                "HASH-DRIFT");

            byte[] malformedUtf8 = (byte[])canonical.Clone();
            malformedUtf8[1] = 0xff;
            AssertRejected(malformedUtf8, true, "CATALOG-MALFORMED");

            int maximum = ContractField<int>("MaximumByteLength");
            var oversize = Enumerable.Repeat((byte)' ', maximum + 1).ToArray();
            oversize[oversize.Length - 1] = (byte)'\n';
            AssertRejected(oversize, true, "CATALOG-MALFORMED");
        }

        [Test]
        public void StrictSchemaRejectsDuplicateUnknownWrongCaseAndSourceTextProperties()
        {
            AssertRejected(
                Mutate(
                    "  \"schemaVersion\": 1,\n",
                    "  \"schemaVersion\": 1,\n  \"schemaVersion\": 1,\n"),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate(
                    "{\n  \"schemaVersion\": 1,",
                    "{\n  \"unexpected\": true,\n  \"schemaVersion\": 1,"),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("  \"questId\": \"OMEN_1\",", "  \"QuestId\": \"OMEN_1\","),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate(
                    "\"id\":\"OBJ_OMEN_1_REPORT\",\"textKey\":\"objective.omen1.report\",",
                    "\"id\":\"OBJ_OMEN_1_REPORT\",\"textKey\":\"objective.omen1.report\",\"sourceText\":\"duplicate authority\","),
                false,
                "CATALOG-MALFORMED");
        }

        [Test]
        public void StrictSchemaRejectsMissingWrongTypeOverflowAndMalformedJson()
        {
            AssertRejected(
                Mutate("  \"descriptionKey\": \"quest.omen1.description\",\n", string.Empty),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("  \"schemaVersion\": 1,", "  \"schemaVersion\": \"1\","),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("  \"schemaVersion\": 1,", "  \"schemaVersion\": 2147483648,"),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("  \"schemaVersion\": 1,", "  \"schemaVersion\": 1"),
                false,
                "CATALOG-MALFORMED");
        }

        [Test]
        public void UnsupportedSchemaAndContentVersionsFailClosed()
        {
            AssertRejected(
                Mutate("  \"schemaVersion\": 1,", "  \"schemaVersion\": 2,"),
                false,
                "VERSION-UNSUPPORTED");

            AssertRejected(
                Mutate(
                    "  \"packetVersion\": \"omen1-a1-2026-07-22-v002\",",
                    "  \"packetVersion\": \"omen1-a1-2026-07-22-v999\","),
                false,
                "VERSION-UNSUPPORTED");
        }

        [Test]
        public void SchemaAndStateSemanticsRejectBlankIdWrongCaseObjectiveAndInvalidTerminal()
        {
            AssertRejected(
                Mutate("\"id\":\"OBJ_OMEN_1_TALK\",\"textKey\"", "\"id\":\" \",\"textKey\""),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("\"id\":\"OBJ_OMEN_1_TALK\",\"textKey\"", "\"id\":\"OBJ_OMEN_1_TALK\",\"TextKey\""),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                MutateOccurrence("\"terminal\":false", "\"terminal\":true", 0, 5),
                false,
                "TRANSITION-INVALID");
        }

        [Test]
        public void DialogueAndStateReferencesMustResolve()
        {
            AssertRejected(
                Mutate("\"target\":\"DLG_OMEN_1_START\"", "\"target\":\"DLG_UNKNOWN\""),
                false,
                "REFERENCE-MISSING");

            AssertRejected(
                Mutate("\"activatesIn\":\"OFFERED\"", "\"activatesIn\":\"STATE_UNKNOWN\""),
                false,
                "REFERENCE-MISSING");
        }

        [Test]
        public void UnknownLocationHookAndResultCapabilitiesFailClosed()
        {
            AssertRejected(
                Mutate(
                    "{\"id\":\"LOCATION_SKY_CASTLE_MARKER\",\"status\":\"requested\"}",
                    "{\"id\":\"LOCATION_UNKNOWN\",\"status\":\"requested\"}"),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate(
                    "{\"id\":\"HOOK_SKY_CASTLE_ARENA\",\"status\":\"requested\"}",
                    "{\"id\":\"HOOK_UNKNOWN\",\"status\":\"requested\"}"),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate(
                    "{\"id\":\"EVENT_SKY_CASTLE_ARENA_UNAVAILABLE\",\"status\":\"requested\"}",
                    "{\"id\":\"EVENT_UNKNOWN\",\"status\":\"requested\"}"),
                false,
                "CATALOG-MALFORMED");
        }

        [Test]
        public void ArtifactConsequenceTargetMustResolve()
        {
            AssertRejected(
                Mutate("\"target\":\"ARTIFACT_CELESTIAL_TEAR\"", "\"target\":\"ARTIFACT_UNKNOWN\""),
                false,
                "REFERENCE-MISSING");
        }

        [Test]
        public void SemanticValidationRejectsDuplicateIdsBrokenReferencesAndUnreachableStates()
        {
            AssertRejected(
                Mutate("\"id\":\"TALK_TO_VALERIUS\",\"resume\"", "\"id\":\"OFFERED\",\"resume\""),
                false,
                "ID-DUPLICATE");

            AssertRejected(
                Mutate("\"objective\":\"OBJ_OMEN_1_TALK\"", "\"objective\":\"OBJ_DOES_NOT_EXIST\""),
                false,
                "REFERENCE-MISSING");

            AssertRejected(
                Mutate(
                    "\n  ],\n  \"objectives\": [",
                    ",\n    {\"id\":\"ORPHAN\",\"resume\":\"x\",\"terminal\":false}\n  ],\n  \"objectives\": ["),
                false,
                "STATE-UNREACHABLE");
        }

        [Test]
        public void SemanticValidationRejectsLocalizationCapabilityAndConsequenceDrift()
        {
            AssertRejected(
                Mutate("\"titleKey\": \"quest.omen1.title\"", "\"titleKey\": \"quest.omen1.missing\""),
                false,
                "REFERENCE-MISSING");

            AssertRejected(
                Mutate(
                    "{\"id\":\"LOCATION_SKY_CASTLE_MARKER\",\"status\":\"requested\"}",
                    "{\"id\":\"LOCATION_SKY_CASTLE_MARKER\",\"status\":\"available\"}"),
                false,
                "CATALOG-MALFORMED");

            AssertRejected(
                Mutate("\"repeatability\":\"once\",\"amount\":500", "\"repeatability\":\"once\",\"amount\":501"),
                false,
                "CATALOG-MALFORMED");
        }

        [Test]
        public void FailedValidationNeverPublishesPartialCatalog()
        {
            object rejected = Validate(
                Mutate(
                    "\"id\":\"DLG_OMEN_1_FAILURE\",\"speakerId\":\"NPC_VALERIUS\"",
                    "\"id\":\"DLG_OMEN_1_FAILURE\",\"speakerId\":\"NPC_MISSING\""),
                false);

            Assert.False((bool)GetProperty(rejected, "IsAccepted"));
            Assert.AreEqual("Rejected", GetProperty(rejected, "Status").ToString());
            Assert.IsNull(GetProperty(rejected, "VerifiedCatalog"));
            Assert.AreEqual(ExpectedDiagnosticCode("REFERENCE-MISSING"), DiagnosticCodes(rejected).Single());
            AssertReadOnlyList(GetProperty(rejected, "Diagnostics"));
        }

        [Test]
        public void AcceptedCatalogPreservesCountsOrderQueriesAndImmutability()
        {
            object result = Validate(CanonicalArtifactBytes(), true);
            AssertAccepted(result);
            object verified = GetProperty(result, "VerifiedCatalog");
            object catalog = GetProperty(verified, "Catalog");

            Assert.AreEqual(6, Items(GetProperty(catalog, "States")).Length);
            Assert.AreEqual(3, Items(GetProperty(catalog, "Objectives")).Length);
            Assert.AreEqual(8, Items(GetProperty(catalog, "Dialogue")).Length);
            Assert.AreEqual(8, Items(GetProperty(catalog, "Transitions")).Length);
            Assert.AreEqual(10, Items(GetProperty(catalog, "ExternalCapabilities")).Length);
            Assert.AreEqual(5, Items(GetProperty(catalog, "Consequences")).Length);
            Assert.AreEqual(28, ((IDictionary)GetProperty(catalog, "Localization")).Count);

            CollectionAssert.AreEqual(
                new[] { "OFFERED", "TALK_TO_VALERIUS", "INVESTIGATE_SKY_CASTLE", "FAILED", "REPORT_TO_VALERIUS", "COMPLETED" },
                Ids(catalog, "States"));
            CollectionAssert.AreEqual(
                new[] { "OBJ_OMEN_1_TALK", "OBJ_OMEN_1_ARENA", "OBJ_OMEN_1_REPORT" },
                Ids(catalog, "Objectives"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "DLG_OMEN_1_OFFER", "DLG_OMEN_1_START", "DLG_OMEN_1_LORE", "DLG_OMEN_1_GO",
                    "DLG_OMEN_1_ARENA_START", "DLG_OMEN_1_FAILURE", "DLG_OMEN_1_REPORT", "DLG_OMEN_1_REPORT_CONCLUSION"
                },
                Ids(catalog, "Dialogue"));
            CollectionAssert.AreEqual(
                new[]
                {
                    "OFFERED/QUEST_ACCEPTED", "TALK_TO_VALERIUS/REQUEST_SKY_CASTLE_ARENA",
                    "INVESTIGATE_SKY_CASTLE/EVENT_SKY_CASTLE_ARENA_FAILURE", "FAILED/RETRY_SKY_CASTLE_ARENA",
                    "INVESTIGATE_SKY_CASTLE/EVENT_SKY_CASTLE_ARENA_CANCELLED", "INVESTIGATE_SKY_CASTLE/EVENT_SKY_CASTLE_ARENA_SUCCESS",
                    "REPORT_TO_VALERIUS/SELECT_VALERIUS", "REPORT_TO_VALERIUS/DLG_OMEN_1_REPORT_CONCLUSION"
                },
                Items(GetProperty(catalog, "Transitions"))
                    .Select(item => GetProperty(item, "From") + "/" + GetProperty(item, "EventId"))
                    .ToArray());

            object value;
            Assert.True(TryQuery(catalog, "TryGetState", out value, "OFFERED"));
            Assert.AreEqual("OFFERED", GetProperty(value, "Id"));
            Assert.True(TryQuery(catalog, "TryGetObjective", out value, "OBJ_OMEN_1_REPORT"));
            Assert.AreEqual("objective.omen1.report", GetProperty(value, "TextKey"));
            Assert.True(TryQuery(catalog, "TryGetDialogue", out value, "DLG_OMEN_1_REPORT"));
            Assert.True(TryQuery(catalog, "TryGetExternalCapability", out value, "HOOK_SKY_CASTLE_ARENA"));
            Assert.True(TryQuery(catalog, "TryGetConsequence", out value, "GRANT_GOLD_500"));
            Assert.AreEqual(500L, GetProperty(value, "Amount"));
            Assert.True(TryQuery(catalog, "TryGetTransition", out value, "OFFERED", "QUEST_ACCEPTED"));
            Assert.AreEqual("TALK_TO_VALERIUS", GetProperty(value, "To"));
            Assert.True(TryQuery(catalog, "TryGetLocalization", out value, "quest.omen1.title"));
            Assert.AreEqual(((IDictionary)GetProperty(catalog, "Localization"))["quest.omen1.title"], value);
            Assert.False(string.IsNullOrWhiteSpace((string)value));
            Assert.False(TryQuery(catalog, "TryGetState", out value, "offered"), "Queries must remain ordinal and case-sensitive.");
            Assert.IsNull(value);

            AssertReadOnlyList(GetProperty(catalog, "States"));
            AssertReadOnlyList(GetProperty(catalog, "Objectives"));
            AssertReadOnlyList(GetProperty(catalog, "Dialogue"));
            AssertReadOnlyList(GetProperty(Items(GetProperty(catalog, "Dialogue"))[0], "Choices"));
            AssertReadOnlyDictionary(GetProperty(catalog, "Localization"));
            AssertReadOnlyDictionary(GetProperty(catalog, "StatesById"));
            AssertReadOnlyDictionary(GetProperty(catalog, "TransitionsByKey"));
            AssertReadOnlyList(GetProperty(result, "Diagnostics"));

            foreach (object record in new[]
                     {
                         catalog,
                         Items(GetProperty(catalog, "States"))[0],
                         Items(GetProperty(catalog, "Objectives"))[0],
                         Items(GetProperty(catalog, "Dialogue"))[0],
                         Items(GetProperty(catalog, "Transitions"))[0]
                     })
            {
                Assert.False(
                    record.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Any(property => property.CanWrite),
                    record.GetType().FullName + " must not expose mutable public properties.");
            }
        }

        [Test]
        public void LoaderPublishesOnlyFirstResultAndReturnsSameCachedInstance()
        {
            Type loaderType = RuntimeType(LoaderTypeName);
            object loader = Activator.CreateInstance(loaderType, true);
            MethodInfo loadForTests = loaderType.GetMethod(
                "LoadBytesOnceForTests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(loadForTests, "The loader must retain its internal deterministic byte seam.");

            object first = loadForTests.Invoke(loader, new object[] { CanonicalArtifactBytes() });
            object second = loadForTests.Invoke(loader, new object[] { StrictUtf8.GetBytes("not json\n") });

            Assert.True((bool)GetProperty(first, "IsSuccess"));
            Assert.AreSame(first, second, "Load-once must never replace a published catalog result.");
            Assert.AreSame(first, GetProperty(loader, "CachedResult"));
            Assert.True((bool)GetProperty(loader, "HasResult"));
            Assert.False((bool)GetProperty(loader, "IsLoading"));
            Assert.AreSame(
                GetProperty(first, "VerifiedCatalog"),
                GetProperty(second, "VerifiedCatalog"));
        }

        [Test]
        public void LoaderRejectsMissingAndMalformedBytesAndCachesTheFirstFailure()
        {
            Type loaderType = RuntimeType(LoaderTypeName);
            MethodInfo loadForTests = loaderType.GetMethod(
                "LoadBytesOnceForTests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(loadForTests, "The loader must retain its internal deterministic byte seam.");

            object missingLoader = Activator.CreateInstance(loaderType, true);
            object missing = loadForTests.Invoke(missingLoader, new object[] { null });
            AssertLoadRejected(missing, "CATALOG-MISSING");
            object missingThenValid = loadForTests.Invoke(missingLoader, new object[] { CanonicalArtifactBytes() });
            Assert.AreSame(missing, missingThenValid, "A missing first load must remain the one published fail-closed result.");
            Assert.AreSame(missing, GetProperty(missingLoader, "CachedResult"));

            object malformedLoader = Activator.CreateInstance(loaderType, true);
            object malformed = loadForTests.Invoke(
                malformedLoader,
                new object[] { Prepend(new byte[] { 0xef, 0xbb, 0xbf }, CanonicalArtifactBytes()) });
            AssertLoadRejected(malformed, "CATALOG-MALFORMED");
            object malformedThenValid = loadForTests.Invoke(malformedLoader, new object[] { CanonicalArtifactBytes() });
            Assert.AreSame(malformed, malformedThenValid, "A malformed first load must not be replaced by later bytes.");
            Assert.AreSame(malformed, GetProperty(malformedLoader, "CachedResult"));
        }

        [Test]
        public void LoaderTransportFailuresAreVisibleAndNeverPublishCatalogs()
        {
            Type loaderType = RuntimeType(LoaderTypeName);
            MethodInfo failure = loaderType.GetMethod("Failure", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(failure, "The loader transport failure path must remain inspectable.");
            Type statusType = failure.GetParameters()[0].ParameterType;

            foreach (string statusName in new[] { "NotFound", "TransportFailed" })
            {
                object status = Enum.Parse(statusType, statusName);
                object result = failure.Invoke(
                    null,
                    new[]
                    {
                        status,
                        (object)"CATALOG-MISSING",
                        "Packaged catalog transport failed.",
                        "readable packaged catalog",
                        statusName
                    });

                Assert.AreEqual(statusName, GetProperty(result, "Status").ToString());
                Assert.False((bool)GetProperty(result, "IsSuccess"));
                Assert.IsNull(GetProperty(result, "VerifiedCatalog"));
                CollectionAssert.AreEqual(
                    new[] { ExpectedDiagnosticCode("CATALOG-MISSING") },
                    DiagnosticCodes(result));
                AssertReadOnlyList(GetProperty(result, "Diagnostics"));
            }
        }

        [Test]
        public void ExporterIsIdempotentAndVerifyGuardsTheCanonicalArtifact()
        {
            Type exporter = RuntimeType(ExporterTypeName, "Assembly-CSharp-Editor");
            byte[] before = CanonicalArtifactBytes();

            bool firstChanged = (bool)InvokeStatic(exporter, "ExportOrThrow");
            byte[] afterFirst = CanonicalArtifactBytes();
            bool secondChanged = (bool)InvokeStatic(exporter, "ExportOrThrow");
            byte[] afterSecond = CanonicalArtifactBytes();
            InvokeStatic(exporter, "VerifyOrThrow");

            Assert.False(firstChanged, "An artifact committed from the canonical source must already be current.");
            Assert.False(secondChanged, "Repeated export must remain an unchanged no-op.");
            CollectionAssert.AreEqual(before, afterFirst);
            CollectionAssert.AreEqual(before, afterSecond);
        }

        [Test]
        public void ExactlyOneOmenCatalogArtifactExistsUnderTheCorrectStreamingAssetsRoot()
        {
            string expected = Path.GetFullPath(ArtifactAbsolutePath());
            string[] matches = Directory
                .GetFiles(Application.dataPath, "OMEN_1.catalog.json", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            CollectionAssert.AreEqual(new[] { expected }, matches);
            StringAssert.StartsWith(
                Path.GetFullPath(Path.Combine(Application.dataPath, "StreamingAssets")) + Path.DirectorySeparatorChar,
                expected);
            Assert.False(
                expected.IndexOf(
                    Path.DirectorySeparatorChar + "AL" + Path.DirectorySeparatorChar + "StreamingAssets" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) >= 0,
                "The artifact must not be placed under Assets/AL/StreamingAssets.");
        }

        private static byte[] CanonicalArtifactBytes()
        {
            string path = ArtifactAbsolutePath();
            Assert.True(File.Exists(path), "Missing generated runtime artifact: " + path);
            return File.ReadAllBytes(path);
        }

        private static string ProjectRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string SourceAbsolutePath() =>
            Path.GetFullPath(Path.Combine(ProjectRoot(), SourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static string ArtifactAbsolutePath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ArtifactAssetRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        private static byte[] Canonicalize(byte[] source, out object diagnostic)
        {
            object[] arguments = { source, null, null };
            bool succeeded = (bool)InvokeStatic(ValidatorType(), "TryCanonicalizeSource", arguments);
            diagnostic = arguments[2];
            return succeeded ? (byte[])arguments[1] : null;
        }

        private static object Validate(byte[] bytes, bool requireCanonicalIdentity) =>
            InvokeStatic(
                ValidatorType(),
                requireCanonicalIdentity ? "ValidateCanonicalArtifact" : "ValidateDocument",
                bytes);

        private static void AssertAccepted(object result)
        {
            string detail = string.Join("\n", DiagnosticDetails(result));
            Assert.True((bool)GetProperty(result, "IsAccepted"), detail);
            Assert.AreEqual("Accepted", GetProperty(result, "Status").ToString());
            Assert.NotNull(GetProperty(result, "VerifiedCatalog"));
            CollectionAssert.IsEmpty(DiagnosticCodes(result));
        }

        private static void AssertRejected(byte[] bytes, bool requireCanonicalIdentity, string expectedCode)
        {
            object result = Validate(bytes, requireCanonicalIdentity);
            Assert.False((bool)GetProperty(result, "IsAccepted"), expectedCode);
            Assert.AreEqual("Rejected", GetProperty(result, "Status").ToString());
            Assert.IsNull(GetProperty(result, "VerifiedCatalog"), "Rejected input must never publish a partial catalog.");
            CollectionAssert.AreEqual(
                new[] { ExpectedDiagnosticCode(expectedCode) },
                DiagnosticCodes(result),
                string.Join("\n", DiagnosticDetails(result)));
        }

        private static byte[] Mutate(string expected, string replacement)
        {
            string source = StrictUtf8.GetString(CanonicalArtifactBytes());
            Assert.AreEqual(
                1,
                CountOccurrences(source, expected),
                "Mutation anchor must occur exactly once: " + expected);
            string mutated = source.Replace(expected, replacement);
            Assert.True(mutated.EndsWith("\n", StringComparison.Ordinal));
            return StrictUtf8.GetBytes(mutated);
        }

        private static byte[] MutateOccurrence(
            string expected,
            string replacement,
            int zeroBasedOccurrence,
            int expectedCount)
        {
            string source = StrictUtf8.GetString(CanonicalArtifactBytes());
            Assert.AreEqual(
                expectedCount,
                CountOccurrences(source, expected),
                "Mutation anchor count changed: " + expected);
            Assert.That(zeroBasedOccurrence, Is.GreaterThanOrEqualTo(0).And.LessThan(expectedCount));

            int offset = 0;
            for (int occurrence = 0; occurrence <= zeroBasedOccurrence; occurrence++)
            {
                offset = source.IndexOf(expected, offset, StringComparison.Ordinal);
                Assert.That(offset, Is.GreaterThanOrEqualTo(0));
                if (occurrence < zeroBasedOccurrence) offset += expected.Length;
            }

            string mutated = source.Substring(0, offset) + replacement + source.Substring(offset + expected.Length);
            Assert.True(mutated.EndsWith("\n", StringComparison.Ordinal));
            return StrictUtf8.GetBytes(mutated);
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static byte[] Prepend(byte[] prefix, byte[] value)
        {
            var result = new byte[prefix.Length + value.Length];
            Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
            Buffer.BlockCopy(value, 0, result, prefix.Length, value.Length);
            return result;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
            }
        }

        private static T ContractField<T>(string name)
        {
            FieldInfo field = RuntimeType(ContractTypeName).GetField(name, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field, ContractTypeName + "." + name);
            return (T)field.GetValue(null);
        }

        private static string[] Ids(object catalog, string propertyName) =>
            Items(GetProperty(catalog, propertyName))
                .Select(item => (string)GetProperty(item, "Id"))
                .ToArray();

        private static object[] Items(object collection) =>
            ((IEnumerable)collection).Cast<object>().ToArray();

        private static string[] DiagnosticCodes(object result)
        {
            string[] codes = Items(GetProperty(result, "Diagnostics"))
                .Select(diagnostic => (string)GetProperty(diagnostic, "Code"))
                .ToArray();

            foreach (string code in codes)
            {
                StringAssert.StartsWith(
                    DiagnosticCodePrefix,
                    code,
                    "Every emitted NVS-01 diagnostic code must use the stable G1 prefix.");
            }

            return codes;
        }

        private static string ExpectedDiagnosticCode(string code) =>
            code.StartsWith(DiagnosticCodePrefix, StringComparison.Ordinal) ? code : DiagnosticCodePrefix + code;

        private static string[] DiagnosticDetails(object result) =>
            Items(GetProperty(result, "Diagnostics"))
                .Select(DiagnosticSummary)
                .ToArray();

        private static string DiagnosticSummary(object diagnostic)
        {
            if (diagnostic == null) return string.Empty;
            return GetProperty(diagnostic, "Code") + " at " + GetProperty(diagnostic, "Path") +
                   ": " + GetProperty(diagnostic, "Message") +
                   " expected=" + GetProperty(diagnostic, "Expected") +
                   " actual=" + GetProperty(diagnostic, "Actual");
        }

        private static void AssertLoadRejected(object result, string expectedCode)
        {
            Assert.AreEqual("Rejected", GetProperty(result, "Status").ToString());
            Assert.False((bool)GetProperty(result, "IsSuccess"));
            Assert.IsNull(GetProperty(result, "VerifiedCatalog"));
            CollectionAssert.AreEqual(
                new[] { ExpectedDiagnosticCode(expectedCode) },
                DiagnosticCodes(result));
            AssertReadOnlyList(GetProperty(result, "Diagnostics"));
        }

        private static bool TryQuery(object target, string methodName, out object value, params object[] keys)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(method, target.GetType().FullName + "." + methodName);
            var arguments = new object[keys.Length + 1];
            Array.Copy(keys, arguments, keys.Length);
            bool found = (bool)method.Invoke(target, arguments);
            value = arguments[arguments.Length - 1];
            return found;
        }

        private static void AssertReadOnlyList(object value)
        {
            var list = value as IList;
            Assert.NotNull(list, value.GetType().FullName + " must implement IList for immutable inspection.");
            Assert.True(list.IsReadOnly);
            object item = list.Count == 0 ? new object() : list[0];
            Assert.Throws<NotSupportedException>(() => list.Add(item));
        }

        private static void AssertReadOnlyDictionary(object value)
        {
            var dictionary = value as IDictionary;
            Assert.NotNull(dictionary, value.GetType().FullName + " must implement IDictionary for immutable inspection.");
            Assert.True(dictionary.IsReadOnly);
            IDictionaryEnumerator enumerator = dictionary.GetEnumerator();
            Assert.True(enumerator.MoveNext());
            DictionaryEntry item = enumerator.Entry;
            Assert.Throws<NotSupportedException>(() => dictionary.Add(item.Key, item.Value));
        }

        private static Type ValidatorType() => RuntimeType(ValidatorTypeName);

        private static Type RuntimeType(string fullName, string assemblyName = null)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly =>
                    !assembly.IsDynamic &&
                    (assemblyName == null ||
                     string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal)))
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(candidate => candidate != null);
            string location = assemblyName == null ? "loaded assemblies" : assemblyName;
            Assert.NotNull(type, "Expected runtime type " + fullName + " in " + location + ".");
            return type;
        }

        private static object GetProperty(object target, string propertyName)
        {
            Assert.NotNull(target, "Cannot inspect " + propertyName + " on a null target.");
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property, target.GetType().FullName + "." + propertyName);
            return property.GetValue(target);
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(method, type.FullName + "." + methodName);
            return method.Invoke(null, arguments);
        }
    }
}
