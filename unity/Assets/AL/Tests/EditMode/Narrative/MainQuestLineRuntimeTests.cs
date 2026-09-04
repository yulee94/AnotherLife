using System;
using System.IO;
using AL.Data.Runtime;
using AL.Narrative.MainQuestLine;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.Narrative
{
    public sealed class MainQuestLineRuntimeTests
    {
        [Test]
        public void CanonicalCatalogMatchesCommittedIdentity()
        {
            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            Assert.IsTrue(
                MainQuestLineCatalogLoader.TryLoadCanonical(out catalog, out diagnostic),
                diagnostic != null ? diagnostic.ToString() : "missing");
            Assert.AreEqual(MainQuestLineContract.CanonicalByteLength, catalog.CanonicalByteLength);
            Assert.AreEqual(MainQuestLineContract.CanonicalSha256, catalog.CanonicalSha256);
            Assert.AreEqual(MainQuestLineContract.EntryChapterId, catalog.EntryChapterId);
            Assert.AreEqual(MainQuestLineContract.EntryQuestId, catalog.EntryQuestId);
            Assert.AreEqual(15, catalog.Chapters.Count);
            Assert.AreEqual("nvs01_omen_1", catalog.Chapters[0].RuntimeBinding);
            Assert.AreEqual("proof_of_worth", catalog.Chapters[1].RuntimeBinding);
        }

        [Test]
        public void MissingCatalogFailsVisibly()
        {
            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            Assert.IsFalse(
                MainQuestLineCatalogLoader.TryLoadFromPath(
                    Path.Combine(Path.GetTempPath(), "al-missing-narrative.json"),
                    out catalog,
                    out diagnostic));
            Assert.IsNull(catalog);
            Assert.IsNotNull(diagnostic);
            Assert.AreEqual(
                MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                diagnostic.Code);
        }

        [Test]
        public void TamperedCatalogFailsClosed()
        {
            byte[] bytes = File.ReadAllBytes(MainQuestLineCatalogLoader.ResolveCatalogPath());
            bytes[bytes.Length - 2] ^= 0x01;
            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            Assert.IsFalse(MainQuestLineCatalog.TryParse(bytes, out catalog, out diagnostic));
            Assert.AreEqual(
                MainQuestLineContract.DiagnosticPrefix + "CATALOG-IDENTITY",
                diagnostic.Code);
        }

        [Test]
        public void CleanSaveResolvesChapterZero()
        {
            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            Assert.IsTrue(MainQuestLineCatalogLoader.TryLoadCanonical(out catalog, out diagnostic));
            MainQuestLineProgress progress;
            Assert.IsTrue(
                MainQuestLineResolver.TryResolve(catalog, null, null, out progress, out diagnostic),
                diagnostic != null ? diagnostic.ToString() : "resolve");
            Assert.AreEqual("CH00_FIRST_SIGNAL", progress.ChapterId);
            Assert.AreEqual("OMEN_1", progress.QuestId);
            Assert.AreEqual("OFFERED", progress.QuestStateId);
        }

        [Test]
        public void CompletedOmenResolvesProofOfWorth()
        {
            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            Assert.IsTrue(MainQuestLineCatalogLoader.TryLoadCanonical(out catalog, out diagnostic));
            var nvs = new Nvs01ProgressData
            {
                Version = 1,
                QuestId = "OMEN_1",
                StateId = "COMPLETED"
            };
            MainQuestLineProgress progress;
            Assert.IsTrue(
                MainQuestLineResolver.TryResolve(catalog, nvs, null, out progress, out diagnostic));
            Assert.AreEqual("CH01_PROOF_OF_WORTH", progress.ChapterId);
            Assert.AreEqual("MQ_C1_PROOF_OF_WORTH", progress.QuestId);
        }

        [Test]
        public void RepresentativePathProgressesAndResumes()
        {
            MainQuestLineExecutionResult result = MainQuestLineRuntime.ExecuteRepresentativePath();
            Assert.IsTrue(result.Succeeded, result.Diagnostic != null ? result.Diagnostic.ToString() : "failed");
            Assert.AreEqual("TALK_TO_VALERIUS", result.ProgressedStateId);
            Assert.AreEqual("TALK_TO_VALERIUS", result.ResumedStateId);
        }
    }
}
