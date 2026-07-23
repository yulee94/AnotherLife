using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AL.Data.Catalogs;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public enum SaveFileReadDisposition
    {
        Missing = 0,
        Read = 1,
        Oversize = 2,
        IoFailure = 3,
        ChangedDuringRead = 4
    }

    public sealed class SaveCandidateLoadSummary
    {
        private const int MaximumDiagnosticCodes = 64;

        public SaveCandidateLoadSummary(
            SaveCandidateSourceGeneration source,
            SaveFileReadDisposition readDisposition,
            long observedByteCount,
            bool hasSemanticOutcome,
            SaveSemanticCandidateOutcome semanticOutcome,
            SaveSemanticDomain disabledDomains,
            SaveSemanticDomain normalizedDomains,
            SaveSemanticDomain preservedUnknownDomains,
            IEnumerable<string> diagnosticCodes)
        {
            Source = source;
            ReadDisposition = readDisposition;
            ObservedByteCount = Math.Max(0L, observedByteCount);
            HasSemanticOutcome = hasSemanticOutcome;
            SemanticOutcome = semanticOutcome;
            DisabledDomains = disabledDomains;
            NormalizedDomains = normalizedDomains;
            PreservedUnknownDomains = preservedUnknownDomains;
            DiagnosticCodes = CopyDiagnosticCodes(diagnosticCodes);
        }

        public SaveCandidateSourceGeneration Source { get; }
        public SaveFileReadDisposition ReadDisposition { get; }
        public long ObservedByteCount { get; }
        public bool HasSemanticOutcome { get; }
        public SaveSemanticCandidateOutcome SemanticOutcome { get; }
        public SaveSemanticDomain DisabledDomains { get; }
        public SaveSemanticDomain NormalizedDomains { get; }
        public SaveSemanticDomain PreservedUnknownDomains { get; }
        public IReadOnlyList<string> DiagnosticCodes { get; }

        private static IReadOnlyList<string> CopyDiagnosticCodes(IEnumerable<string> codes)
        {
            if (codes == null)
            {
                return Array.AsReadOnly(Array.Empty<string>());
            }

            var copy = new List<string>(MaximumDiagnosticCodes);
            foreach (string code in codes)
            {
                if (copy.Count == MaximumDiagnosticCodes)
                {
                    break;
                }

                copy.Add(code ?? string.Empty);
            }

            return new ReadOnlyCollection<string>(copy);
        }
    }

    public sealed class SaveLoadDisposition
    {
        private const int MaximumCandidateSummaries = 4;

        public SaveLoadDisposition(
            IEnumerable<SaveCandidateLoadSummary> candidateSummaries,
            SaveCandidateSourceGeneration selectedSource,
            string selectorReason,
            bool isWritable,
            bool isRuntimeUsable,
            bool offlineProgressApplied,
            bool diskChanged,
            bool rawEvidencePreserved)
        {
            CandidateSummaries = CopyCandidateSummaries(candidateSummaries);
            SelectedSource = selectedSource;
            SelectorReason = selectorReason ?? string.Empty;
            IsWritable = isWritable;
            IsRuntimeUsable = isRuntimeUsable;
            OfflineProgressApplied = offlineProgressApplied;
            DiskChanged = diskChanged;
            RawEvidencePreserved = rawEvidencePreserved;
        }

        public IReadOnlyList<SaveCandidateLoadSummary> CandidateSummaries { get; }
        public SaveCandidateSourceGeneration SelectedSource { get; }
        public string SelectorReason { get; }
        public bool IsWritable { get; }
        public bool IsRuntimeUsable { get; }
        public bool OfflineProgressApplied { get; }
        public bool DiskChanged { get; }
        public bool RawEvidencePreserved { get; }

        private static IReadOnlyList<SaveCandidateLoadSummary> CopyCandidateSummaries(
            IEnumerable<SaveCandidateLoadSummary> summaries)
        {
            if (summaries == null)
            {
                throw new ArgumentNullException(nameof(summaries));
            }

            var copy = new List<SaveCandidateLoadSummary>(MaximumCandidateSummaries);
            foreach (SaveCandidateLoadSummary summary in summaries)
            {
                if (copy.Count == MaximumCandidateSummaries)
                {
                    break;
                }

                if (summary == null)
                {
                    throw new ArgumentException(
                        "Candidate summaries cannot contain null.",
                        nameof(summaries));
                }

                copy.Add(summary);
            }

            return new ReadOnlyCollection<SaveCandidateLoadSummary>(copy);
        }
    }

    public interface ISaveLoadDispositionProvider
    {
        SaveLoadDisposition LastLoadDisposition { get; }
        SaveGameData ReadOnlyCandidateSnapshot { get; }
    }

    public sealed class SaveOperationDisposition
    {
        private const int MaximumDiagnosticCodes = 16;
        private const int MaximumDiagnosticCodeLength = 128;

        public SaveOperationDisposition(
            SaveOperationStatus status,
            bool mayHaveMutated,
            bool candidatePrimaryVerified,
            bool requiredBackupVerified,
            bool previousAuthorityVerified,
            bool cleanupVerified,
            bool rollbackAttempted,
            bool rollbackVerified,
            IEnumerable<string> diagnosticCodes)
        {
            Status = status;
            MayHaveMutated = mayHaveMutated;
            CandidatePrimaryVerified = candidatePrimaryVerified;
            RequiredBackupVerified = requiredBackupVerified;
            PreviousAuthorityVerified = previousAuthorityVerified;
            CleanupVerified = cleanupVerified;
            RollbackAttempted = rollbackAttempted;
            RollbackVerified = rollbackVerified;
            DiagnosticCodes = CopyDiagnosticCodes(diagnosticCodes);
        }

        public SaveOperationStatus Status { get; }
        public bool MayHaveMutated { get; }
        public bool CandidatePrimaryVerified { get; }
        public bool RequiredBackupVerified { get; }
        public bool PreviousAuthorityVerified { get; }
        public bool CleanupVerified { get; }
        public bool RollbackAttempted { get; }
        public bool RollbackVerified { get; }
        public IReadOnlyList<string> DiagnosticCodes { get; }

        private static IReadOnlyList<string> CopyDiagnosticCodes(
            IEnumerable<string> diagnosticCodes)
        {
            if (diagnosticCodes == null)
            {
                return Array.AsReadOnly(Array.Empty<string>());
            }

            var copy = new List<string>(MaximumDiagnosticCodes);
            foreach (string diagnosticCode in diagnosticCodes)
            {
                if (copy.Count == MaximumDiagnosticCodes)
                {
                    break;
                }

                string value = diagnosticCode ?? string.Empty;
                copy.Add(value.Length <= MaximumDiagnosticCodeLength
                    ? value
                    : value.Substring(0, MaximumDiagnosticCodeLength));
            }

            return new ReadOnlyCollection<string>(copy);
        }
    }

    public interface ISaveOperationDispositionProvider
    {
        SaveOperationDisposition LastSaveDisposition { get; }
    }
}
