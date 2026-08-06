using System;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalResourceService : IResourceIntegrityService
    {
        private const int MaxWalletRecords = 256;

        private static readonly IReadOnlyList<EconomyDiagnostic> MissingProductionProviderDiagnostics =
            EconomyContractCollections.FreezeDiagnostics(new[]
            {
                new EconomyDiagnostic(EconomyDiagnosticCodes.ProductionDependency, "Production.Source")
            });

        private static readonly IReadOnlyList<EconomyDiagnostic>
            ProfileReadOnlyDiagnostics =
                EconomyContractCollections.FreezeDiagnostics(new[]
                {
                    new EconomyDiagnostic(
                        EconomyDiagnosticCodes.ProfileReadOnly,
                        "Resources")
                });

        private readonly ISaveGameService _saveGameService;
        private readonly EconomyWriteAuthorityGate _writeAuthorityGate;
        private readonly IEconomyProductionContributionProvider _injectedProductionProvider;

        private readonly ResourceData[] _walletEntries;
        private readonly bool[] _walletSeen;
        private readonly long[] _currentBalances;
        private readonly bool[] _currentEntryPresence;
        private readonly double[] _productionRemainders;
        private readonly double[] _emptyProductionRemainders;
        private readonly double[] _stagedContributions;
        private readonly double[] _stagedRemainders;
        private readonly long[] _stagedBalances;
        private readonly bool[] _stagedInsertions;
        private readonly bool[] _stagedBalanceChanges;
        private readonly List<EconomyDiagnostic> _walletErrorDiagnostics = new List<EconomyDiagnostic>(16);
        private readonly List<EconomyDiagnostic> _walletUnknownDiagnostics = new List<EconomyDiagnostic>(8);

        private string _productionProfileIdentity = string.Empty;
        private string _lastProductionDiagnosticCode = string.Empty;

        public event Action<ResourceType, long> OnResourceChanged;

        public LocalResourceService(ISaveGameService saveGameService)
            : this(
                saveGameService,
                EconomyWriteAuthorityGate.FromSaveService(saveGameService),
                null)
        {
        }

        private LocalResourceService(
            ISaveGameService saveGameService,
            EconomyWriteAuthorityGate writeAuthorityGate,
            IEconomyProductionContributionProvider productionProvider)
        {
            _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
            _writeAuthorityGate = writeAuthorityGate ??
                throw new ArgumentNullException(nameof(writeAuthorityGate));
            _injectedProductionProvider = productionProvider;

            int resourceCount = ResourceRules.WalletResources.Count;
            _walletEntries = new ResourceData[resourceCount];
            _walletSeen = new bool[resourceCount];
            _currentBalances = new long[resourceCount];
            _currentEntryPresence = new bool[resourceCount];
            _productionRemainders = new double[resourceCount];
            _emptyProductionRemainders = new double[resourceCount];
            _stagedContributions = new double[resourceCount];
            _stagedRemainders = new double[resourceCount];
            _stagedBalances = new long[resourceCount];
            _stagedInsertions = new bool[resourceCount];
            _stagedBalanceChanges = new bool[resourceCount];
        }

        public EconomyBalanceReadResult ReadResource(ResourceType type)
        {
            if (!ResourceRules.TryGetWalletIndex(type, out int resourceIndex))
            {
                return ReadFailure(
                    type,
                    EconomyBalanceReadStatus.UnavailableUnsupportedCurrency,
                    EconomyDiagnosticCodes.UnsupportedResource,
                    $"Resources[{type}]");
            }

            if (!TryGetCurrentSave(out SaveGameData save))
            {
                return ReadFailure(
                    type,
                    EconomyBalanceReadStatus.UnavailableNoCurrentSave,
                    EconomyDiagnosticCodes.NoCurrentSave,
                    "Resources");
            }

            if (!TryBuildWalletSnapshot(save, out _, out IReadOnlyList<EconomyDiagnostic> diagnostics))
            {
                return new EconomyBalanceReadResult(
                    EconomyBalanceReadStatus.UnavailableMalformedState,
                    EconomyCurrencyKind.Resource,
                    type,
                    null,
                    diagnostics);
            }

            ResourceData entry = _walletEntries[resourceIndex];
            bool writable = IsProfileWritableFor(save);
            if (entry == null)
            {
                IReadOnlyList<EconomyDiagnostic> missingDiagnostics = AppendDiagnostic(
                    diagnostics,
                    new EconomyDiagnostic(
                        EconomyDiagnosticCodes.MissingOptionalResource,
                        $"Resources[{type}]"));
                return new EconomyBalanceReadResult(
                    writable
                        ? EconomyBalanceReadStatus.CompatibleMissingOptional
                        : EconomyBalanceReadStatus.AvailableReadOnly,
                    EconomyCurrencyKind.Resource,
                    type,
                    0,
                    missingDiagnostics);
            }

            return new EconomyBalanceReadResult(
                writable
                    ? EconomyBalanceReadStatus.Available
                    : EconomyBalanceReadStatus.AvailableReadOnly,
                EconomyCurrencyKind.Resource,
                type,
                entry.Amount,
                diagnostics);
        }

        public EconomyMutationResult TryAddResource(ResourceType type, long amount)
        {
            if (!ResourceRules.TryGetWalletIndex(type, out int resourceIndex))
            {
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedUnsupportedCurrency,
                    EconomyDiagnosticCodes.UnsupportedResource,
                    $"Resources[{type}]");
            }

            if (amount == 0)
            {
                return Mutation(type, amount, EconomyMutationStatus.NoChange, null, null);
            }

            if (amount < 0)
            {
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedInvalidAmount,
                    EconomyDiagnosticCodes.InvalidAmount,
                    $"Resources[{type}]");
            }

            if (!TryGetWritableWallet(type, amount, out List<ResourceData> wallet, out IReadOnlyList<EconomyDiagnostic> diagnostics, out EconomyMutationResult failure))
            {
                return failure;
            }

            ResourceData entry = _walletEntries[resourceIndex];
            long previous = entry?.Amount ?? 0;
            long current;
            try
            {
                current = checked(previous + amount);
            }
            catch (OverflowException)
            {
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedOverflow,
                    EconomyDiagnosticCodes.Overflow,
                    $"Resources[{type}]",
                    previous,
                    previous);
            }

            if (entry == null)
            {
                if (wallet.Count >= MaxWalletRecords)
                {
                    return MutationFailure(
                        type,
                        amount,
                        EconomyMutationStatus.RejectedMalformedState,
                        EconomyDiagnosticCodes.MalformedWallet,
                        "Resources");
                }

                entry = new ResourceData { Type = type, Amount = current };
                wallet.Add(entry);
            }
            else
            {
                entry.Amount = current;
            }

            var result = new EconomyMutationResult(
                EconomyMutationStatus.Applied,
                EconomyCurrencyKind.Resource,
                type,
                amount,
                previous,
                current,
                diagnostics);
            NotifyResourceChanged(type, current);
            return result;
        }

        public EconomyMutationResult TryConsumeResource(ResourceType type, long amount)
        {
            if (!ResourceRules.TryGetWalletIndex(type, out int resourceIndex))
            {
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedUnsupportedCurrency,
                    EconomyDiagnosticCodes.UnsupportedResource,
                    $"Resources[{type}]");
            }

            if (amount <= 0)
            {
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedInvalidAmount,
                    EconomyDiagnosticCodes.InvalidAmount,
                    $"Resources[{type}]");
            }

            if (!TryGetWritableWallet(type, amount, out _, out IReadOnlyList<EconomyDiagnostic> diagnostics, out EconomyMutationResult failure))
            {
                return failure;
            }

            ResourceData entry = _walletEntries[resourceIndex];
            if (entry == null || entry.Amount < amount)
            {
                long previous = entry?.Amount ?? 0;
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedInsufficientBalance,
                    EconomyDiagnosticCodes.InsufficientBalance,
                    $"Resources[{type}]",
                    previous,
                    previous);
            }

            long current;
            try
            {
                current = checked(entry.Amount - amount);
            }
            catch (OverflowException)
            {
                return MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedOverflow,
                    EconomyDiagnosticCodes.Overflow,
                    $"Resources[{type}]",
                    entry.Amount,
                    entry.Amount);
            }

            long previousBalance = entry.Amount;
            entry.Amount = current;
            var result = new EconomyMutationResult(
                EconomyMutationStatus.Applied,
                EconomyCurrencyKind.Resource,
                type,
                amount,
                previousBalance,
                current,
                diagnostics);
            NotifyResourceChanged(type, current);
            return result;
        }

        public EconomyProductionTickResult TryTickProduction(double deltaSeconds)
        {
            if (double.IsNaN(deltaSeconds) ||
                double.IsInfinity(deltaSeconds) ||
                deltaSeconds <= 0d ||
                deltaSeconds > 1d)
            {
                return ProductionFailure(
                    EconomyMutationStatus.RejectedInvalidAmount,
                    deltaSeconds,
                    EconomyDiagnosticCodes.ProductionInvalidDelta,
                    "Production.DeltaSeconds");
            }

            if (!TryGetCurrentSave(out SaveGameData save))
            {
                return ProductionFailure(
                    EconomyMutationStatus.RejectedNoCurrentSave,
                    deltaSeconds,
                    EconomyDiagnosticCodes.NoCurrentSave,
                    "Resources");
            }

            if (!IsProfileWritableFor(save))
            {
                return ProductionReadOnly(deltaSeconds);
            }

            IEconomyProductionContributionProvider provider = ResolveProductionProvider();
            if (provider == null)
            {
                return new EconomyProductionTickResult(
                    EconomyMutationStatus.RejectedDependencyUnavailable,
                    deltaSeconds,
                    EconomyContractCollections.EmptyMutations,
                    MissingProductionProviderDiagnostics);
            }

            if (!TryBuildWalletSnapshot(save, out List<ResourceData> wallet, out IReadOnlyList<EconomyDiagnostic> walletDiagnostics))
            {
                return new EconomyProductionTickResult(
                    EconomyMutationStatus.RejectedMalformedState,
                    deltaSeconds,
                    EconomyContractCollections.EmptyMutations,
                    walletDiagnostics);
            }

            EconomyProductionContributionSnapshot source;
            try
            {
                source = provider.BuildContributions(deltaSeconds);
            }
            catch (Exception)
            {
                return ProductionFailure(
                    EconomyMutationStatus.RejectedDependencyUnavailable,
                    deltaSeconds,
                    EconomyDiagnosticCodes.ProductionDependency,
                    "Production.Source");
            }

            if (source == null ||
                source.Status != EconomyProductionSourceStatus.Available ||
                string.IsNullOrWhiteSpace(source.SourceRevision))
            {
                return new EconomyProductionTickResult(
                    EconomyMutationStatus.RejectedDependencyUnavailable,
                    deltaSeconds,
                    EconomyContractCollections.EmptyMutations,
                    MissingProductionProviderDiagnostics);
            }

            PrepareCurrentWalletArrays();
            bool profileChanged = !string.Equals(
                _productionProfileIdentity,
                source.ProfileIdentity,
                StringComparison.Ordinal);
            double[] currentRemainders = profileChanged
                ? _emptyProductionRemainders
                : _productionRemainders;
            EconomyMutationStatus planStatus = EconomyProductionBatchPlanner.Plan(
                source.Contributions,
                currentRemainders,
                _currentBalances,
                _currentEntryPresence,
                _stagedContributions,
                _stagedRemainders,
                _stagedBalances,
                _stagedInsertions,
                _stagedBalanceChanges,
                out EconomyDiagnostic planDiagnostic);

            if (planStatus != EconomyMutationStatus.Applied &&
                planStatus != EconomyMutationStatus.NoChange)
            {
                return new EconomyProductionTickResult(
                    planStatus,
                    deltaSeconds,
                    EconomyContractCollections.EmptyMutations,
                    OneDiagnostic(planDiagnostic.Code, planDiagnostic.RecordPath));
            }

            if (!IsProfileWritableFor(save))
            {
                return ProductionReadOnly(deltaSeconds);
            }

            if (planStatus == EconomyMutationStatus.NoChange)
            {
                if (profileChanged)
                {
                    Array.Clear(_productionRemainders, 0, _productionRemainders.Length);
                    _productionProfileIdentity = source.ProfileIdentity;
                }

                return new EconomyProductionTickResult(
                    EconomyMutationStatus.NoChange,
                    deltaSeconds,
                    EconomyContractCollections.EmptyMutations,
                    walletDiagnostics);
            }

            int insertionCount = 0;
            for (int index = 0; index < _stagedInsertions.Length; index++)
            {
                if (_stagedInsertions[index])
                {
                    insertionCount++;
                }
            }

            if (wallet.Count + insertionCount > MaxWalletRecords)
            {
                return ProductionFailure(
                    EconomyMutationStatus.RejectedMalformedState,
                    deltaSeconds,
                    EconomyDiagnosticCodes.MalformedWallet,
                    "Resources");
            }

            if (insertionCount > 0 && wallet.Capacity < wallet.Count + insertionCount)
            {
                wallet.Capacity = wallet.Count + insertionCount;
            }

            if (!IsProfileWritableFor(save))
            {
                return ProductionReadOnly(deltaSeconds);
            }

            _productionProfileIdentity = source.ProfileIdentity;

            List<EconomyMutationResult> balanceChanges = null;
            for (int resourceIndex = 0; resourceIndex < ResourceRules.WalletResources.Count; resourceIndex++)
            {
                ResourceType resourceType = ResourceRules.WalletResources[resourceIndex];
                if (_stagedInsertions[resourceIndex])
                {
                    var inserted = new ResourceData
                    {
                        Type = resourceType,
                        Amount = _stagedBalances[resourceIndex]
                    };
                    wallet.Add(inserted);
                    _walletEntries[resourceIndex] = inserted;
                }
                else if (_stagedBalanceChanges[resourceIndex])
                {
                    _walletEntries[resourceIndex].Amount = _stagedBalances[resourceIndex];
                }

                _productionRemainders[resourceIndex] = _stagedRemainders[resourceIndex];
                if (!_stagedBalanceChanges[resourceIndex])
                {
                    continue;
                }

                long previous = _currentEntryPresence[resourceIndex]
                    ? _currentBalances[resourceIndex]
                    : 0;
                long current = _stagedBalances[resourceIndex];
                if (balanceChanges == null)
                {
                    balanceChanges = new List<EconomyMutationResult>(ResourceRules.WalletResources.Count);
                }

                balanceChanges.Add(new EconomyMutationResult(
                    EconomyMutationStatus.Applied,
                    EconomyCurrencyKind.Resource,
                    resourceType,
                    current - previous,
                    previous,
                    current,
                    walletDiagnostics));
            }

            IReadOnlyList<EconomyMutationResult> frozenChanges =
                EconomyContractCollections.FreezeMutations(balanceChanges);
            for (int index = 0; index < frozenChanges.Count; index++)
            {
                EconomyMutationResult change = frozenChanges[index];
                NotifyResourceChanged(change.ResourceType.Value, change.CurrentBalance.Value);
            }

            return new EconomyProductionTickResult(
                EconomyMutationStatus.Applied,
                deltaSeconds,
                frozenChanges,
                walletDiagnostics);
        }

        public long GetResourceCount(ResourceType type)
        {
            EconomyBalanceReadResult result = ReadResource(type);
            return result.IsAvailable && result.Balance.HasValue ? result.Balance.Value : 0;
        }

        public void AddResource(ResourceType type, long amount)
        {
            EconomyMutationResult result = TryAddResource(type, amount);
            LogCompatibilityRejection("AddResource", result);
        }

        public bool ConsumeResource(ResourceType type, long amount)
        {
            EconomyMutationResult result = TryConsumeResource(type, amount);
            LogCompatibilityRejection("ConsumeResource", result);
            return result.Status == EconomyMutationStatus.Applied;
        }

        public bool HasEnough(ResourceType type, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            EconomyBalanceReadResult result = ReadResource(type);
            return result.Status != EconomyBalanceReadStatus.AvailableReadOnly &&
                   result.IsAvailable &&
                   result.Balance.HasValue &&
                   result.Balance.Value >= amount;
        }

        public void TickProduction(double deltaSeconds)
        {
            EconomyProductionTickResult result = TryTickProduction(deltaSeconds);
            if (result.Status == EconomyMutationStatus.Applied ||
                result.Status == EconomyMutationStatus.NoChange)
            {
                _lastProductionDiagnosticCode = string.Empty;
                return;
            }

            if (string.Equals(_lastProductionDiagnosticCode, result.DiagnosticCode, StringComparison.Ordinal))
            {
                return;
            }

            _lastProductionDiagnosticCode = result.DiagnosticCode;
            Debug.LogWarning($"[{result.DiagnosticCode}] TickProduction rejected with status {result.Status}.");
        }

        private bool TryGetWritableWallet(
            ResourceType type,
            long amount,
            out List<ResourceData> wallet,
            out IReadOnlyList<EconomyDiagnostic> diagnostics,
            out EconomyMutationResult failure)
        {
            if (!TryGetCurrentSave(out SaveGameData save))
            {
                wallet = null;
                diagnostics = EconomyContractCollections.EmptyDiagnostics;
                failure = MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedNoCurrentSave,
                    EconomyDiagnosticCodes.NoCurrentSave,
                    "Resources");
                return false;
            }

            if (!IsProfileWritableFor(save))
            {
                wallet = null;
                diagnostics = EconomyContractCollections.EmptyDiagnostics;
                failure = MutationFailure(
                    type,
                    amount,
                    EconomyMutationStatus.RejectedProfileNotWritable,
                    EconomyDiagnosticCodes.ProfileReadOnly,
                    "Resources");
                return false;
            }

            if (!TryBuildWalletSnapshot(save, out wallet, out diagnostics))
            {
                failure = new EconomyMutationResult(
                    EconomyMutationStatus.RejectedMalformedState,
                    EconomyCurrencyKind.Resource,
                    type,
                    amount,
                    null,
                    null,
                    diagnostics);
                return false;
            }

            failure = default;
            return true;
        }

        private bool TryBuildWalletSnapshot(
            SaveGameData save,
            out List<ResourceData> wallet,
            out IReadOnlyList<EconomyDiagnostic> diagnostics)
        {
            Array.Clear(_walletEntries, 0, _walletEntries.Length);
            Array.Clear(_walletSeen, 0, _walletSeen.Length);
            _walletErrorDiagnostics.Clear();
            _walletUnknownDiagnostics.Clear();

            wallet = save?.Resources;
            if (wallet == null)
            {
                _walletErrorDiagnostics.Add(new EconomyDiagnostic(
                    EconomyDiagnosticCodes.MalformedWallet,
                    "Resources"));
                diagnostics = EconomyContractCollections.FreezeDiagnostics(_walletErrorDiagnostics);
                return false;
            }

            if (wallet.Count > MaxWalletRecords)
            {
                _walletErrorDiagnostics.Add(new EconomyDiagnostic(
                    EconomyDiagnosticCodes.MalformedWallet,
                    "Resources"));
                diagnostics = EconomyContractCollections.FreezeDiagnostics(_walletErrorDiagnostics);
                return false;
            }

            for (int recordIndex = 0; recordIndex < wallet.Count; recordIndex++)
            {
                ResourceData entry = wallet[recordIndex];
                if (entry == null)
                {
                    _walletErrorDiagnostics.Add(new EconomyDiagnostic(
                        EconomyDiagnosticCodes.MalformedWallet,
                        $"Resources[{recordIndex}]"));
                    continue;
                }

                if (!ResourceRules.TryGetWalletIndex(entry.Type, out int resourceIndex))
                {
                    _walletUnknownDiagnostics.Add(new EconomyDiagnostic(
                        EconomyDiagnosticCodes.PreservedUnknownResource,
                        $"Resources[{recordIndex}]"));
                    continue;
                }

                if (_walletSeen[resourceIndex])
                {
                    _walletErrorDiagnostics.Add(new EconomyDiagnostic(
                        EconomyDiagnosticCodes.DuplicateResource,
                        $"Resources[{recordIndex}]"));
                    continue;
                }

                _walletSeen[resourceIndex] = true;
                _walletEntries[resourceIndex] = entry;
                if (entry.Amount < 0)
                {
                    _walletErrorDiagnostics.Add(new EconomyDiagnostic(
                        EconomyDiagnosticCodes.NegativeBalance,
                        $"Resources[{recordIndex}]"));
                }
            }

            for (int resourceIndex = 0; resourceIndex < ResourceRules.WalletResources.Count; resourceIndex++)
            {
                ResourceType type = ResourceRules.WalletResources[resourceIndex];
                if (ResourceRules.IsCoreResource(type) && !_walletSeen[resourceIndex])
                {
                    _walletErrorDiagnostics.Add(new EconomyDiagnostic(
                        EconomyDiagnosticCodes.MissingCoreResource,
                        $"Resources[{type}]"));
                }
            }

            if (_walletErrorDiagnostics.Count == 0 && _walletUnknownDiagnostics.Count == 0)
            {
                diagnostics = EconomyContractCollections.EmptyDiagnostics;
                return true;
            }

            var orderedDiagnostics = new List<EconomyDiagnostic>(_walletErrorDiagnostics.Count + _walletUnknownDiagnostics.Count);
            orderedDiagnostics.AddRange(_walletErrorDiagnostics);
            orderedDiagnostics.AddRange(_walletUnknownDiagnostics);
            diagnostics = EconomyContractCollections.FreezeDiagnostics(orderedDiagnostics);
            return _walletErrorDiagnostics.Count == 0;
        }

        private void PrepareCurrentWalletArrays()
        {
            for (int index = 0; index < ResourceRules.WalletResources.Count; index++)
            {
                ResourceData entry = _walletEntries[index];
                _currentEntryPresence[index] = entry != null;
                _currentBalances[index] = entry?.Amount ?? 0;
            }
        }

        private IEconomyProductionContributionProvider ResolveProductionProvider()
        {
            if (_injectedProductionProvider != null)
            {
                return _injectedProductionProvider;
            }

            try
            {
                return ServiceLocator.TryGet(out IEconomyProductionContributionProvider provider)
                    ? provider
                    : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool TryGetCurrentSave(out SaveGameData save)
        {
            try
            {
                save = _saveGameService.CurrentSave;
                return save != null;
            }
            catch (Exception)
            {
                save = null;
                return false;
            }
        }

        private bool IsProfileWritableFor(SaveGameData expectedPublishedSave)
        {
            return _writeAuthorityGate.IsWritableFor(expectedPublishedSave);
        }

        private void NotifyResourceChanged(ResourceType type, long balance)
        {
            Delegate[] handlers = OnResourceChanged?.GetInvocationList();
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers)
            {
                try
                {
                    ((Action<ResourceType, long>)handler)(type, balance);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{EconomyDiagnosticCodes.EventHandler}] Resource event handler {ex.GetType().Name} was isolated after commit.");
                }
            }
        }

        private static EconomyBalanceReadResult ReadFailure(
            ResourceType type,
            EconomyBalanceReadStatus status,
            string diagnosticCode,
            string path)
        {
            return new EconomyBalanceReadResult(
                status,
                EconomyCurrencyKind.Resource,
                type,
                null,
                OneDiagnostic(diagnosticCode, path));
        }

        private static EconomyMutationResult Mutation(
            ResourceType type,
            long amount,
            EconomyMutationStatus status,
            long? previous,
            long? current)
        {
            return new EconomyMutationResult(
                status,
                EconomyCurrencyKind.Resource,
                type,
                amount,
                previous,
                current,
                EconomyContractCollections.EmptyDiagnostics);
        }

        private static EconomyMutationResult MutationFailure(
            ResourceType type,
            long amount,
            EconomyMutationStatus status,
            string diagnosticCode,
            string path,
            long? previous = null,
            long? current = null)
        {
            return new EconomyMutationResult(
                status,
                EconomyCurrencyKind.Resource,
                type,
                amount,
                previous,
                current,
                OneDiagnostic(diagnosticCode, path));
        }

        private static EconomyProductionTickResult ProductionFailure(
            EconomyMutationStatus status,
            double deltaSeconds,
            string diagnosticCode,
            string path)
        {
            return new EconomyProductionTickResult(
                status,
                deltaSeconds,
                EconomyContractCollections.EmptyMutations,
                OneDiagnostic(diagnosticCode, path));
        }

        private static EconomyProductionTickResult ProductionReadOnly(
            double deltaSeconds) =>
            new EconomyProductionTickResult(
                EconomyMutationStatus.RejectedProfileNotWritable,
                deltaSeconds,
                EconomyContractCollections.EmptyMutations,
                ProfileReadOnlyDiagnostics);

        private static IReadOnlyList<EconomyDiagnostic> OneDiagnostic(string code, string path) =>
            Array.AsReadOnly(new[] { new EconomyDiagnostic(code, path) });

        private static IReadOnlyList<EconomyDiagnostic> AppendDiagnostic(
            IReadOnlyList<EconomyDiagnostic> existing,
            EconomyDiagnostic diagnostic)
        {
            var combined = new List<EconomyDiagnostic>((existing?.Count ?? 0) + 1);
            if (existing != null)
            {
                for (int index = 0; index < existing.Count; index++)
                {
                    combined.Add(existing[index]);
                }
            }

            combined.Add(diagnostic);
            return EconomyContractCollections.FreezeDiagnostics(combined);
        }

        private static void LogCompatibilityRejection(string operation, EconomyMutationResult result)
        {
            if (result.Status == EconomyMutationStatus.Applied ||
                result.Status == EconomyMutationStatus.NoChange ||
                result.Status == EconomyMutationStatus.RejectedInsufficientBalance)
            {
                return;
            }

            string code = string.IsNullOrWhiteSpace(result.DiagnosticCode)
                ? EconomyDiagnosticCodes.MalformedWallet
                : result.DiagnosticCode;
            Debug.LogWarning($"[{code}] {operation} rejected with status {result.Status}.");
        }
    }
}
