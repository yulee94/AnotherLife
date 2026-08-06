using System;
using System.Collections.Generic;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.Services.Local
{
    public class LocalWarzoneCreditService : IWarzoneCreditIntegrityService
    {
        private const string CreditsPath = "WarzoneCredits";

        private readonly ISaveGameService _saveGameService;
        private readonly EconomyWriteAuthorityGate _writeAuthorityGate;

        public LocalWarzoneCreditService(ISaveGameService saveGameService)
            : this(
                saveGameService,
                EconomyWriteAuthorityGate.FromSaveService(saveGameService))
        {
        }

        private LocalWarzoneCreditService(
            ISaveGameService saveGameService,
            EconomyWriteAuthorityGate writeAuthorityGate)
        {
            _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
            _writeAuthorityGate = writeAuthorityGate ??
                throw new ArgumentNullException(nameof(writeAuthorityGate));
        }

        public EconomyBalanceReadResult ReadCredits()
        {
            if (!TryGetCurrentSave(out SaveGameData save))
            {
                return ReadFailure(
                    EconomyBalanceReadStatus.UnavailableNoCurrentSave,
                    EconomyDiagnosticCodes.NoCurrentSave);
            }

            if (save.WarzoneCredits < 0)
            {
                return ReadFailure(
                    EconomyBalanceReadStatus.UnavailableMalformedState,
                    EconomyDiagnosticCodes.InvalidCredits);
            }

            return new EconomyBalanceReadResult(
                IsProfileWritableFor(save)
                    ? EconomyBalanceReadStatus.Available
                    : EconomyBalanceReadStatus.AvailableReadOnly,
                EconomyCurrencyKind.WarzoneCredits,
                null,
                save.WarzoneCredits,
                EconomyContractCollections.EmptyDiagnostics);
        }

        public EconomyMutationResult TryAddCredits(int amount)
        {
            if (amount == 0)
            {
                return Mutation(EconomyMutationStatus.NoChange, amount, null, null);
            }

            if (amount < 0)
            {
                return MutationFailure(
                    EconomyMutationStatus.RejectedInvalidAmount,
                    amount,
                    EconomyDiagnosticCodes.InvalidAmount);
            }

            if (!TryGetWritableSave(amount, out SaveGameData save, out EconomyMutationResult failure))
            {
                return failure;
            }

            int previous = save.WarzoneCredits;
            int current;
            try
            {
                current = checked(previous + amount);
            }
            catch (OverflowException)
            {
                return MutationFailure(
                    EconomyMutationStatus.RejectedOverflow,
                    amount,
                    EconomyDiagnosticCodes.Overflow,
                    previous,
                    previous);
            }

            save.WarzoneCredits = current;
            return Mutation(EconomyMutationStatus.Applied, amount, previous, current);
        }

        public EconomyMutationResult TrySpendCredits(int amount)
        {
            if (amount <= 0)
            {
                return MutationFailure(
                    EconomyMutationStatus.RejectedInvalidAmount,
                    amount,
                    EconomyDiagnosticCodes.InvalidAmount);
            }

            if (!TryGetWritableSave(amount, out SaveGameData save, out EconomyMutationResult failure))
            {
                return failure;
            }

            int previous = save.WarzoneCredits;
            if (previous < amount)
            {
                return MutationFailure(
                    EconomyMutationStatus.RejectedInsufficientBalance,
                    amount,
                    EconomyDiagnosticCodes.InsufficientBalance,
                    previous,
                    previous);
            }

            int current;
            try
            {
                current = checked(previous - amount);
            }
            catch (OverflowException)
            {
                return MutationFailure(
                    EconomyMutationStatus.RejectedOverflow,
                    amount,
                    EconomyDiagnosticCodes.Overflow,
                    previous,
                    previous);
            }

            save.WarzoneCredits = current;
            return Mutation(EconomyMutationStatus.Applied, amount, previous, current);
        }

        public int GetCredits()
        {
            EconomyBalanceReadResult result = ReadCredits();
            return result.IsAvailable && result.Balance.HasValue
                ? checked((int)result.Balance.Value)
                : 0;
        }

        public void AddCredits(int amount)
        {
            EconomyMutationResult result = TryAddCredits(amount);
            if (result.Status == EconomyMutationStatus.Applied)
            {
                _saveGameService.Save();
                return;
            }

            LogCompatibilityRejection("AddCredits", result);
        }

        public bool SpendCredits(int amount)
        {
            EconomyMutationResult result = TrySpendCredits(amount);
            if (result.Status != EconomyMutationStatus.Applied)
            {
                LogCompatibilityRejection("SpendCredits", result);
                return false;
            }

            _saveGameService.Save();
            return true;
        }

        private bool TryGetWritableSave(
            int amount,
            out SaveGameData save,
            out EconomyMutationResult failure)
        {
            if (!TryGetCurrentSave(out save))
            {
                failure = MutationFailure(
                    EconomyMutationStatus.RejectedNoCurrentSave,
                    amount,
                    EconomyDiagnosticCodes.NoCurrentSave);
                return false;
            }

            if (!IsProfileWritableFor(save))
            {
                failure = MutationFailure(
                    EconomyMutationStatus.RejectedProfileNotWritable,
                    amount,
                    EconomyDiagnosticCodes.ProfileReadOnly,
                    save.WarzoneCredits >= 0 ? save.WarzoneCredits : (long?)null,
                    save.WarzoneCredits >= 0 ? save.WarzoneCredits : (long?)null);
                return false;
            }

            if (save.WarzoneCredits < 0)
            {
                failure = MutationFailure(
                    EconomyMutationStatus.RejectedMalformedState,
                    amount,
                    EconomyDiagnosticCodes.InvalidCredits);
                return false;
            }

            failure = default;
            return true;
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

        private static EconomyBalanceReadResult ReadFailure(
            EconomyBalanceReadStatus status,
            string diagnosticCode)
        {
            return new EconomyBalanceReadResult(
                status,
                EconomyCurrencyKind.WarzoneCredits,
                null,
                null,
                OneDiagnostic(diagnosticCode, CreditsPath));
        }

        private static EconomyMutationResult Mutation(
            EconomyMutationStatus status,
            int amount,
            long? previous,
            long? current)
        {
            return new EconomyMutationResult(
                status,
                EconomyCurrencyKind.WarzoneCredits,
                null,
                amount,
                previous,
                current,
                EconomyContractCollections.EmptyDiagnostics);
        }

        private static EconomyMutationResult MutationFailure(
            EconomyMutationStatus status,
            int amount,
            string diagnosticCode,
            long? previous = null,
            long? current = null)
        {
            return new EconomyMutationResult(
                status,
                EconomyCurrencyKind.WarzoneCredits,
                null,
                amount,
                previous,
                current,
                OneDiagnostic(diagnosticCode, CreditsPath));
        }

        private static IReadOnlyList<EconomyDiagnostic> OneDiagnostic(string code, string path) =>
            Array.AsReadOnly(new[] { new EconomyDiagnostic(code, path) });

        private static void LogCompatibilityRejection(string operation, EconomyMutationResult result)
        {
            if (result.Status == EconomyMutationStatus.NoChange ||
                result.Status == EconomyMutationStatus.RejectedInsufficientBalance)
            {
                return;
            }

            string code = string.IsNullOrWhiteSpace(result.DiagnosticCode)
                ? EconomyDiagnosticCodes.InvalidCredits
                : result.DiagnosticCode;
            Debug.LogWarning($"[{code}] {operation} rejected with status {result.Status}.");
        }
    }
}
