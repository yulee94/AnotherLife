using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace AL.Narrative.MainQuestLine
{
    public sealed class MainQuestLineCatalog
    {
        internal MainQuestLineCatalog(
            MainQuestLineCatalogFile file,
            IReadOnlyList<MainQuestLineChapter> chapters,
            int canonicalByteLength,
            string canonicalSha256)
        {
            File = file;
            Chapters = chapters;
            CanonicalByteLength = canonicalByteLength;
            CanonicalSha256 = canonicalSha256;
        }

        internal MainQuestLineCatalogFile File { get; }
        public IReadOnlyList<MainQuestLineChapter> Chapters { get; }
        public int CanonicalByteLength { get; }
        public string CanonicalSha256 { get; }
        public string PacketVersion => File.packetVersion;
        public string EntryChapterId => File.entryChapterId;
        public string EntryQuestId => File.entryQuestId;
        public string EntryScene => File.entryScene;
        public string ProgressedStateId => File.progressedStateId;
        public string AcceptChoiceKey => File.acceptChoiceKey;
        public string[] CriticalPath => File.criticalPath ?? Array.Empty<string>();

        public bool TryGetChapter(string chapterId, out MainQuestLineChapter chapter)
        {
            if (!string.IsNullOrEmpty(chapterId))
            {
                for (int i = 0; i < Chapters.Count; i++)
                {
                    if (string.Equals(Chapters[i].Id, chapterId, StringComparison.Ordinal))
                    {
                        chapter = Chapters[i];
                        return true;
                    }
                }
            }

            chapter = null;
            return false;
        }

        public bool TryGetChapterByQuest(string questId, out MainQuestLineChapter chapter)
        {
            if (!string.IsNullOrEmpty(questId))
            {
                for (int i = 0; i < Chapters.Count; i++)
                {
                    if (string.Equals(Chapters[i].MainQuestId, questId, StringComparison.Ordinal))
                    {
                        chapter = Chapters[i];
                        return true;
                    }
                }
            }

            chapter = null;
            return false;
        }

        public static bool TryParse(
            byte[] bytes,
            out MainQuestLineCatalog catalog,
            out MainQuestLineDiagnostic diagnostic)
        {
            catalog = null;
            diagnostic = null;
            if (bytes == null)
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                    "Runtime catalog bytes are missing.",
                    MainQuestLineContract.RelativePath,
                    "null");
                return false;
            }

            if (bytes.Length == 0 || bytes.Length > MainQuestLineContract.MaximumByteLength)
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MALFORMED",
                    "Runtime catalog size is outside the accepted bound.",
                    MainQuestLineContract.CanonicalByteLength.ToString(),
                    bytes.Length.ToString());
                return false;
            }

            string sha256 = ComputeSha256(bytes);
            if (bytes.Length != MainQuestLineContract.CanonicalByteLength ||
                !string.Equals(sha256, MainQuestLineContract.CanonicalSha256, StringComparison.Ordinal))
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-IDENTITY",
                    "Runtime catalog identity does not match the committed shipping artifact.",
                    MainQuestLineContract.CanonicalSha256,
                    sha256);
                return false;
            }

            MainQuestLineCatalogFile file;
            try
            {
                file = JsonUtility.FromJson<MainQuestLineCatalogFile>(
                    Encoding.UTF8.GetString(bytes));
            }
            catch (Exception exception)
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MALFORMED",
                    "Runtime catalog JSON could not be parsed.",
                    "object",
                    exception.GetType().Name);
                return false;
            }

            if (file == null ||
                file.schemaVersion != MainQuestLineContract.SchemaVersion ||
                !string.Equals(file.catalogId, MainQuestLineContract.CatalogId, StringComparison.Ordinal) ||
                !string.Equals(file.packetId, MainQuestLineContract.PacketId, StringComparison.Ordinal) ||
                !string.Equals(file.packetVersion, MainQuestLineContract.PacketVersion, StringComparison.Ordinal) ||
                !string.Equals(file.sourceStatus, MainQuestLineContract.SourceStatus, StringComparison.Ordinal) ||
                !string.Equals(file.deliveryMechanism, MainQuestLineContract.DeliveryMechanism, StringComparison.Ordinal) ||
                !string.Equals(file.relativePath, MainQuestLineContract.RelativePath, StringComparison.Ordinal) ||
                !string.Equals(file.entryChapterId, MainQuestLineContract.EntryChapterId, StringComparison.Ordinal) ||
                !string.Equals(file.entryQuestId, MainQuestLineContract.EntryQuestId, StringComparison.Ordinal) ||
                !string.Equals(file.entryScene, MainQuestLineContract.EntryScene, StringComparison.Ordinal) ||
                !string.Equals(file.progressEvent, MainQuestLineContract.ProgressEvent, StringComparison.Ordinal) ||
                !string.Equals(file.progressedStateId, MainQuestLineContract.ProgressedStateId, StringComparison.Ordinal) ||
                !string.Equals(file.acceptChoiceKey, MainQuestLineContract.AcceptChoiceKey, StringComparison.Ordinal) ||
                file.chapters == null ||
                file.chapters.Length != MainQuestLineContract.ChapterCount ||
                file.criticalPath == null ||
                file.criticalPath.Length != MainQuestLineContract.ChapterCount)
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MALFORMED",
                    "Runtime catalog fields do not match the shipping contract.",
                    MainQuestLineContract.CatalogId,
                    file == null ? "null" : file.catalogId ?? string.Empty);
                return false;
            }

            var chapters = new MainQuestLineChapter[file.chapters.Length];
            for (int i = 0; i < file.chapters.Length; i++)
            {
                MainQuestLineChapterRecord record = file.chapters[i];
                if (record == null ||
                    string.IsNullOrWhiteSpace(record.id) ||
                    record.order != i ||
                    string.IsNullOrWhiteSpace(record.mainQuestId) ||
                    !string.Equals(record.mainQuestId, file.criticalPath[i], StringComparison.Ordinal))
                {
                    diagnostic = new MainQuestLineDiagnostic(
                        MainQuestLineContract.DiagnosticPrefix + "CATALOG-MALFORMED",
                        "Runtime catalog chapter identity or order drifted.",
                        "contiguous critical path",
                        record == null ? "null" : record.id ?? string.Empty);
                    return false;
                }

                chapters[i] = new MainQuestLineChapter(record);
            }

            if (!string.Equals(chapters[0].MainQuestId, MainQuestLineContract.EntryQuestId, StringComparison.Ordinal) ||
                !string.Equals(chapters[1].MainQuestId, MainQuestLineContract.ProofQuestId, StringComparison.Ordinal) ||
                !string.Equals(chapters[0].RuntimeBinding, "nvs01_omen_1", StringComparison.Ordinal) ||
                !string.Equals(chapters[1].RuntimeBinding, "proof_of_worth", StringComparison.Ordinal))
            {
                diagnostic = new MainQuestLineDiagnostic(
                    MainQuestLineContract.DiagnosticPrefix + "CATALOG-MALFORMED",
                    "Representative runtime bindings drifted.",
                    MainQuestLineContract.EntryQuestId,
                    chapters[0].MainQuestId);
                return false;
            }

            catalog = new MainQuestLineCatalog(file, Array.AsReadOnly(chapters), bytes.Length, sha256);
            return true;
        }

        internal static string ComputeSha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
