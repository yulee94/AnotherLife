using System;
using System.Collections;
using System.Collections.Generic;
using AL.Core;

namespace AL.Core.Interfaces
{
    public enum EconomyCurrencyKind
    {
        Unknown = 0,
        Resource = 1,
        WarzoneCredits = 2
    }

    public enum EconomyMutationStatus
    {
        Unknown = 0,
        Applied = 1,
        NoChange = 2,
        RejectedNoCurrentSave = 3,
        RejectedProfileNotWritable = 4,
        RejectedUnsupportedCurrency = 5,
        RejectedInvalidAmount = 6,
        RejectedMalformedState = 7,
        RejectedInsufficientBalance = 8,
        RejectedOverflow = 9,
        RejectedDependencyUnavailable = 10
    }

    public enum EconomyBalanceReadStatus
    {
        Unknown = 0,
        Available = 1,
        AvailableReadOnly = 2,
        CompatibleMissingOptional = 3,
        UnavailableNoCurrentSave = 4,
        UnavailableUnsupportedCurrency = 5,
        UnavailableMalformedState = 6
    }

    public enum EconomyProductionSourceStatus
    {
        Unknown = 0,
        Available = 1,
        Unavailable = 2
    }

    public static class EconomyDiagnosticCodes
    {
        public const string NoCurrentSave = "AL-ECO-NO-CURRENT-SAVE";
        public const string ProfileReadOnly = "AL-ECO-PROFILE-READ-ONLY";
        public const string UnsupportedResource = "AL-ECO-UNSUPPORTED-RESOURCE";
        public const string InvalidAmount = "AL-ECO-INVALID-AMOUNT";
        public const string MalformedWallet = "AL-ECO-MALFORMED-WALLET";
        public const string MissingCoreResource = "AL-ECO-MISSING-CORE-RESOURCE";
        public const string MissingOptionalResource = "AL-ECO-MISSING-OPTIONAL-RESOURCE";
        public const string DuplicateResource = "AL-ECO-DUPLICATE-RESOURCE";
        public const string NegativeBalance = "AL-ECO-NEGATIVE-BALANCE";
        public const string PreservedUnknownResource = "AL-ECO-PRESERVED-UNKNOWN-RESOURCE";
        public const string Overflow = "AL-ECO-OVERFLOW";
        public const string InsufficientBalance = "AL-ECO-INSUFFICIENT-BALANCE";
        public const string InvalidCredits = "AL-ECO-INVALID-CREDITS";
        public const string ProductionInvalidDelta = "AL-ECO-PRODUCTION-INVALID-DELTA";
        public const string ProductionInvalidContribution = "AL-ECO-PRODUCTION-INVALID-CONTRIBUTION";
        public const string ProductionInvalidRemainder = "AL-ECO-PRODUCTION-INVALID-REMAINDER";
        public const string ProductionDependency = "AL-ECO-PRODUCTION-DEPENDENCY";
        public const string ProductionCatalog = "AL-ECO-PRODUCTION-CATALOG";
        public const string ProductionProfile = "AL-ECO-PRODUCTION-PROFILE";
        public const string ProductionRealm = "AL-ECO-PRODUCTION-REALM";
        public const string ProductionOathmark = "AL-ECO-PRODUCTION-OATHMARK";
        public const string ProductionDrift = "AL-ECO-PRODUCTION-DRIFT";
        public const string ProductionElapsed = "AL-ECO-PRODUCTION-ELAPSED";
        public const string EventHandler = "AL-ECO-EVENT-HANDLER";
        public const string DiagnosticsTruncated = "AL-ECO-DIAGNOSTICS-TRUNCATED";
    }

    public readonly struct EconomyDiagnostic
    {
        private const int MaxCodeLength = 96;
        private const int MaxRecordPathLength = 256;

        private readonly string _code;
        private readonly string _recordPath;

        public EconomyDiagnostic(string code, string recordPath)
        {
            _code = Bound(code, MaxCodeLength);
            _recordPath = Bound(recordPath, MaxRecordPathLength);
        }

        public string Code => _code ?? string.Empty;
        public string RecordPath => _recordPath ?? string.Empty;

        private static string Bound(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maximumLength
                ? value
                : value.Substring(0, maximumLength);
        }
    }

    public readonly struct EconomyBalanceReadResult
    {
        private readonly IReadOnlyList<EconomyDiagnostic> _diagnostics;

        internal EconomyBalanceReadResult(
            EconomyBalanceReadStatus status,
            EconomyCurrencyKind currencyKind,
            ResourceType? resourceType,
            long? balance,
            IReadOnlyList<EconomyDiagnostic> diagnostics)
        {
            Status = status;
            CurrencyKind = currencyKind;
            ResourceType = resourceType;
            Balance = balance;
            _diagnostics = EconomyContractCollections.FreezeDiagnostics(diagnostics);
        }

        public EconomyBalanceReadStatus Status { get; }
        public EconomyCurrencyKind CurrencyKind { get; }
        public ResourceType? ResourceType { get; }
        public long? Balance { get; }
        public IReadOnlyList<EconomyDiagnostic> Diagnostics =>
            _diagnostics ?? EconomyContractCollections.EmptyDiagnostics;
        public string DiagnosticCode => Diagnostics.Count == 0 ? string.Empty : Diagnostics[0].Code;
        public bool HasBalance => Balance.HasValue;
        public bool IsAvailable =>
            Status == EconomyBalanceReadStatus.Available ||
            Status == EconomyBalanceReadStatus.AvailableReadOnly ||
            Status == EconomyBalanceReadStatus.CompatibleMissingOptional;
    }

    public readonly struct EconomyMutationResult
    {
        private readonly IReadOnlyList<EconomyDiagnostic> _diagnostics;

        internal EconomyMutationResult(
            EconomyMutationStatus status,
            EconomyCurrencyKind currencyKind,
            ResourceType? resourceType,
            long requestedAmount,
            long? previousBalance,
            long? currentBalance,
            IReadOnlyList<EconomyDiagnostic> diagnostics)
        {
            Status = status;
            CurrencyKind = currencyKind;
            ResourceType = resourceType;
            RequestedAmount = requestedAmount;
            PreviousBalance = previousBalance;
            CurrentBalance = currentBalance;
            _diagnostics = EconomyContractCollections.FreezeDiagnostics(diagnostics);
        }

        public EconomyMutationStatus Status { get; }
        public EconomyCurrencyKind CurrencyKind { get; }
        public ResourceType? ResourceType { get; }
        public long RequestedAmount { get; }
        public long? PreviousBalance { get; }
        public long? CurrentBalance { get; }
        public IReadOnlyList<EconomyDiagnostic> Diagnostics =>
            _diagnostics ?? EconomyContractCollections.EmptyDiagnostics;
        public string DiagnosticCode => Diagnostics.Count == 0 ? string.Empty : Diagnostics[0].Code;
        public bool Changed => Status == EconomyMutationStatus.Applied;
    }

    public readonly struct EconomyProductionContribution
    {
        public EconomyProductionContribution(ResourceType resourceType, double amount)
        {
            ResourceType = resourceType;
            Amount = amount;
        }

        public ResourceType ResourceType { get; }
        public double Amount { get; }
    }

    public sealed class EconomyProductionContributionSnapshot
    {
        private const int MaxContributions = 256;

        public EconomyProductionContributionSnapshot(
            EconomyProductionSourceStatus status,
            string profileIdentity,
            string sourceRevision,
            IEnumerable<EconomyProductionContribution> contributions,
            IEnumerable<EconomyDiagnostic> diagnostics)
        {
            if (profileIdentity != null && profileIdentity.Length > 128)
            {
                throw new ArgumentException("Production profile identity exceeds the bounded contract.", nameof(profileIdentity));
            }

            if (status == EconomyProductionSourceStatus.Available && !IsSafeProfileIdentity(profileIdentity))
            {
                throw new ArgumentException("Available production source requires a stable safe profile identity.", nameof(profileIdentity));
            }

            if (sourceRevision != null && sourceRevision.Length > 256)
            {
                throw new ArgumentException("Production source revision exceeds the bounded contract.", nameof(sourceRevision));
            }

            Status = status;
            ProfileIdentity = profileIdentity ?? string.Empty;
            SourceRevision = sourceRevision ?? string.Empty;
            Contributions = EconomyContractCollections.FreezeContributions(contributions, MaxContributions);
            Diagnostics = EconomyContractCollections.FreezeDiagnostics(diagnostics);
        }

        public EconomyProductionSourceStatus Status { get; }
        public string ProfileIdentity { get; }
        public string SourceRevision { get; }
        public IReadOnlyList<EconomyProductionContribution> Contributions { get; }
        public IReadOnlyList<EconomyDiagnostic> Diagnostics { get; }

        private static bool IsSafeProfileIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!char.IsLetterOrDigit(character) &&
                    character != '-' &&
                    character != '_' &&
                    character != '.')
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct EconomyProductionTickResult
    {
        private readonly IReadOnlyList<EconomyMutationResult> _balanceChanges;
        private readonly IReadOnlyList<EconomyDiagnostic> _diagnostics;

        internal EconomyProductionTickResult(
            EconomyMutationStatus status,
            double requestedDeltaSeconds,
            IReadOnlyList<EconomyMutationResult> balanceChanges,
            IReadOnlyList<EconomyDiagnostic> diagnostics)
        {
            Status = status;
            RequestedDeltaSeconds = requestedDeltaSeconds;
            _balanceChanges = EconomyContractCollections.FreezeMutations(balanceChanges);
            _diagnostics = EconomyContractCollections.FreezeDiagnostics(diagnostics);
        }

        public EconomyMutationStatus Status { get; }
        public double RequestedDeltaSeconds { get; }
        public IReadOnlyList<EconomyMutationResult> BalanceChanges =>
            _balanceChanges ?? EconomyContractCollections.EmptyMutations;
        public IReadOnlyList<EconomyDiagnostic> Diagnostics =>
            _diagnostics ?? EconomyContractCollections.EmptyDiagnostics;
        public string DiagnosticCode => Diagnostics.Count == 0 ? string.Empty : Diagnostics[0].Code;
        public bool Changed => Status == EconomyMutationStatus.Applied;
    }

    public interface IEconomyProductionContributionProvider
    {
        EconomyProductionContributionSnapshot BuildContributions(double deltaSeconds);
    }

    internal static class EconomyContractCollections
    {
        private const int MaxDiagnostics = 32;

        internal static readonly IReadOnlyList<EconomyDiagnostic> EmptyDiagnostics =
            new FrozenReadOnlyList<EconomyDiagnostic>(Array.Empty<EconomyDiagnostic>());

        internal static readonly IReadOnlyList<EconomyMutationResult> EmptyMutations =
            new FrozenReadOnlyList<EconomyMutationResult>(Array.Empty<EconomyMutationResult>());

        internal static IReadOnlyList<EconomyDiagnostic> FreezeDiagnostics(
            IEnumerable<EconomyDiagnostic> diagnostics)
        {
            if (diagnostics == null || ReferenceEquals(diagnostics, EmptyDiagnostics))
            {
                return EmptyDiagnostics;
            }

            if (diagnostics is FrozenReadOnlyList<EconomyDiagnostic> frozenDiagnostics)
            {
                return frozenDiagnostics;
            }

            if (diagnostics is IReadOnlyCollection<EconomyDiagnostic> emptyCollection && emptyCollection.Count == 0)
            {
                return EmptyDiagnostics;
            }

            int capacity = diagnostics is IReadOnlyCollection<EconomyDiagnostic> readOnlyCollection
                ? Math.Min(readOnlyCollection.Count, MaxDiagnostics)
                : diagnostics is ICollection<EconomyDiagnostic> mutableCollection
                    ? Math.Min(mutableCollection.Count, MaxDiagnostics)
                    : 0;
            var frozen = new List<EconomyDiagnostic>(capacity);
            foreach (EconomyDiagnostic diagnostic in diagnostics)
            {
                if (frozen.Count >= MaxDiagnostics)
                {
                    frozen[MaxDiagnostics - 1] = new EconomyDiagnostic(
                        EconomyDiagnosticCodes.DiagnosticsTruncated,
                        string.Empty);
                    break;
                }

                frozen.Add(diagnostic);
            }

            return frozen.Count == 0
                ? EmptyDiagnostics
                : new FrozenReadOnlyList<EconomyDiagnostic>(frozen.ToArray());
        }

        internal static IReadOnlyList<EconomyProductionContribution> FreezeContributions(
            IEnumerable<EconomyProductionContribution> contributions,
            int maximum)
        {
            if (contributions == null)
            {
                return new FrozenReadOnlyList<EconomyProductionContribution>(Array.Empty<EconomyProductionContribution>());
            }

            int capacity = contributions is IReadOnlyCollection<EconomyProductionContribution> readOnlyCollection
                ? Math.Min(readOnlyCollection.Count, maximum)
                : contributions is ICollection<EconomyProductionContribution> mutableCollection
                    ? Math.Min(mutableCollection.Count, maximum)
                    : 0;
            var frozen = new List<EconomyProductionContribution>(Math.Max(0, capacity));
            foreach (EconomyProductionContribution contribution in contributions)
            {
                if (frozen.Count >= maximum)
                {
                    throw new ArgumentException("Production contribution count exceeds the bounded contract.", nameof(contributions));
                }

                frozen.Add(contribution);
            }

            return new FrozenReadOnlyList<EconomyProductionContribution>(frozen.ToArray());
        }

        internal static IReadOnlyList<EconomyMutationResult> FreezeMutations(
            IReadOnlyList<EconomyMutationResult> mutations)
        {
            if (mutations == null || mutations.Count == 0 || ReferenceEquals(mutations, EmptyMutations))
            {
                return EmptyMutations;
            }

            if (mutations is FrozenReadOnlyList<EconomyMutationResult> frozenMutations)
            {
                return frozenMutations;
            }

            if (mutations.Count > ResourceRules.WalletResources.Count)
            {
                throw new ArgumentException("Production mutation count exceeds the bounded wallet contract.", nameof(mutations));
            }

            var copy = new EconomyMutationResult[mutations.Count];
            for (int index = 0; index < mutations.Count; index++)
            {
                copy[index] = mutations[index];
            }
            return new FrozenReadOnlyList<EconomyMutationResult>(copy);
        }

        private sealed class FrozenReadOnlyList<T> : IReadOnlyList<T>
        {
            private readonly T[] _items;

            internal FrozenReadOnlyList(T[] items)
            {
                _items = items ?? Array.Empty<T>();
            }

            public int Count => _items.Length;
            public T this[int index] => _items[index];
            public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
        }
    }
}
