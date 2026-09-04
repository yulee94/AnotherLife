using System;
using System.Collections.Generic;
using System.Linq;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.RealmWar.Territories.Contracts;

namespace AL.RealmWar.Territories
{
    internal sealed class SaveBackedTerritoryCandidate : ITerritoryCandidateApplyTarget
    {
        private readonly SaveGameData _save;
        private readonly ISaveGameService _saveGameService;
        private readonly TerritoryPhaseBCatalog _catalog;
        private readonly bool _persistOnCommit;
        private readonly List<TerritoryData> _territoriesBefore;
        private readonly TerritoryCaptureLedgerData _ledgerBefore;
        private TerritoryCaptureTransactionPlan _stagedPlan;
        private TerritoryCaptureReceipt _stagedReceipt;
        private TerritoryCaptureCommittedEvent _stagedEvent;
        private bool _ownershipApplied;

        public SaveBackedTerritoryCandidate(
            SaveGameData save,
            ISaveGameService saveGameService,
            TerritoryPhaseBCatalog catalog)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _saveGameService = saveGameService ??
                throw new ArgumentNullException(nameof(saveGameService));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _persistOnCommit = true;
            _territoriesBefore = CloneTerritories(save.Territories);
            _ledgerBefore = CloneLedger(save.TerritoryCaptureLedger);
        }

        internal SaveBackedTerritoryCandidate(
            SaveGameData save,
            TerritoryPhaseBCatalog catalog)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _persistOnCommit = false;
            _territoriesBefore = CloneTerritories(save.Territories);
            _ledgerBefore = CloneLedger(save.TerritoryCaptureLedger);
        }

        public TerritoryApplyStepStatus ApplyOwnership(TerritoryCaptureTransactionPlan plan)
        {
            if (plan?.CapturePlan == null)
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            TerritoryData territory = FindTerritory(plan.CapturePlan.TerritoryId);
            if (territory == null)
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            long currentRevision = TerritoryCaptureTransactionService.ReadRevision(
                _save.TerritoryCaptureLedger,
                territory.Id);
            if (territory.OwnerRealm != plan.CapturePlan.PreviousOwner ||
                currentRevision != plan.CapturePlan.PreviousRevision)
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            if (HasCommittedOperation(plan.CapturePlan.OperationId, plan.ResultId))
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            territory.OwnerRealm = plan.CapturePlan.NewOwner;
            UpsertRevision(territory.Id, plan.CapturePlan.NewRevision);
            _stagedPlan = plan;
            _ownershipApplied = true;
            return TerritoryApplyStepStatus.Applied;
        }

        public TerritoryApplyStepStatus ApplyReceipt(TerritoryCaptureReceipt receipt)
        {
            if (!_ownershipApplied ||
                receipt == null ||
                _stagedPlan == null ||
                !string.Equals(
                    receipt.OperationId,
                    _stagedPlan.CapturePlan.OperationId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receipt.ResultId,
                    _stagedPlan.ResultId,
                    StringComparison.Ordinal))
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            EnsureLedger();
            _save.TerritoryCaptureLedger.Receipts.Add(
                TerritoryCaptureReceiptRecord.FromReceipt(receipt));
            _stagedReceipt = receipt;
            return TerritoryApplyStepStatus.Applied;
        }

        public TerritoryApplyStepStatus ApplyOutbox(TerritoryCaptureCommittedEvent committedEvent)
        {
            if (!_ownershipApplied ||
                committedEvent == null ||
                _stagedPlan == null ||
                !string.Equals(
                    committedEvent.CaptureOperationId,
                    _stagedPlan.CapturePlan.OperationId,
                    StringComparison.Ordinal))
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            EnsureLedger();
            _save.TerritoryCaptureLedger.Outbox.Add(
                TerritoryCaptureOutboxRecord.FromEvent(committedEvent));
            _stagedEvent = committedEvent;
            return TerritoryApplyStepStatus.Applied;
        }

        public TerritoryCommitStatus Commit(TerritoryCaptureTransactionPlan plan)
        {
            if (!ReferenceEquals(plan, _stagedPlan) ||
                _stagedReceipt == null ||
                _stagedEvent == null)
            {
                return TerritoryCommitStatus.Rejected;
            }

            if (!_persistOnCommit)
            {
                return TerritoryCommitStatus.Committed;
            }

            try
            {
                _saveGameService.Save();
            }
            catch (Exception)
            {
                MarkStagedReceiptCommitUncertain();
                return TerritoryCommitStatus.Uncertain;
            }

            SaveOperationStatus status = _saveGameService.LastSaveStatus;
            if (status == SaveOperationStatus.CommitUncertain)
            {
                MarkStagedReceiptCommitUncertain();
                return TerritoryCommitStatus.Uncertain;
            }

            if (status == SaveOperationStatus.SavedPrimary)
            {
                return TerritoryCommitStatus.Committed;
            }

            return TerritoryCommitStatus.Rejected;
        }

        private void MarkStagedReceiptCommitUncertain()
        {
            TerritoryCaptureReceiptRecord row =
                _save.TerritoryCaptureLedger?.Receipts?.LastOrDefault(item =>
                    item != null &&
                    string.Equals(
                        item.ReceiptId,
                        _stagedReceipt?.ReceiptId,
                        StringComparison.Ordinal));
            if (row != null)
            {
                row.Durability = (int)TerritoryOperationDurability.CommitUncertain;
            }
        }

        public bool Rollback(TerritoryCaptureTransactionPlan plan)
        {
            _save.Territories = CloneTerritories(_territoriesBefore);
            _save.TerritoryCaptureLedger = CloneLedger(_ledgerBefore);
            _stagedPlan = null;
            _stagedReceipt = null;
            _stagedEvent = null;
            _ownershipApplied = false;
            return true;
        }

        private bool HasCommittedOperation(string operationId, string resultId)
        {
            List<TerritoryCaptureReceiptRecord> receipts =
                _save.TerritoryCaptureLedger?.Receipts;
            if (receipts == null)
            {
                return false;
            }

            return receipts.Any(item =>
                item != null &&
                (string.Equals(item.OperationId, operationId, StringComparison.Ordinal) ||
                 string.Equals(item.ResultId, resultId, StringComparison.Ordinal)));
        }

        private TerritoryData FindTerritory(string territoryId)
        {
            return _save.Territories?.FirstOrDefault(item =>
                item != null &&
                string.Equals(item.Id, territoryId, StringComparison.Ordinal));
        }

        private void UpsertRevision(string territoryId, long revision)
        {
            EnsureLedger();
            TerritoryOwnershipRevisionData row =
                _save.TerritoryCaptureLedger.Revisions.FirstOrDefault(item =>
                    item != null &&
                    string.Equals(item.TerritoryId, territoryId, StringComparison.Ordinal));
            if (row == null)
            {
                _save.TerritoryCaptureLedger.Revisions.Add(
                    new TerritoryOwnershipRevisionData
                    {
                        TerritoryId = territoryId,
                        Revision = revision
                    });
                return;
            }

            row.Revision = revision;
        }

        private void EnsureLedger()
        {
            if (_save.TerritoryCaptureLedger == null)
            {
                _save.TerritoryCaptureLedger = new TerritoryCaptureLedgerData
                {
                    Version = TerritoryCaptureLedgerData.CurrentVersion,
                    CatalogId = _catalog.Identity?.CatalogId ?? string.Empty,
                    CatalogRawSha256 = _catalog.Identity?.RawSha256 ?? string.Empty,
                    ProfileSessionId =
                        TerritoryCaptureTransactionService.LocalProfileSessionId
                };
            }

            _save.TerritoryCaptureLedger.Receipts ??=
                new List<TerritoryCaptureReceiptRecord>();
            _save.TerritoryCaptureLedger.Outbox ??=
                new List<TerritoryCaptureOutboxRecord>();
            _save.TerritoryCaptureLedger.Revisions ??=
                new List<TerritoryOwnershipRevisionData>();
            if (string.IsNullOrWhiteSpace(_save.TerritoryCaptureLedger.ProfileSessionId))
            {
                _save.TerritoryCaptureLedger.ProfileSessionId =
                    TerritoryCaptureTransactionService.LocalProfileSessionId;
            }
        }

        private static List<TerritoryData> CloneTerritories(List<TerritoryData> source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new List<TerritoryData>(source.Count);
            foreach (TerritoryData territory in source)
            {
                if (territory == null)
                {
                    clone.Add(null);
                    continue;
                }

                clone.Add(new TerritoryData
                {
                    Id = territory.Id,
                    Name = territory.Name,
                    OwnerRealm = territory.OwnerRealm,
                    BonusType = territory.BonusType,
                    BonusAmount = territory.BonusAmount,
                    IsFortress = territory.IsFortress
                });
            }

            return clone;
        }

        private static TerritoryCaptureLedgerData CloneLedger(TerritoryCaptureLedgerData source)
        {
            if (source == null)
            {
                return null;
            }

            return new TerritoryCaptureLedgerData
            {
                Version = source.Version,
                CatalogId = source.CatalogId,
                CatalogRawSha256 = source.CatalogRawSha256,
                StateRevisionHash = source.StateRevisionHash,
                ProfileSessionId = source.ProfileSessionId,
                Revisions = source.Revisions == null
                    ? new List<TerritoryOwnershipRevisionData>()
                    : source.Revisions.Select(CloneRevision).ToList(),
                Receipts = source.Receipts == null
                    ? new List<TerritoryCaptureReceiptRecord>()
                    : source.Receipts.Select(CloneReceipt).ToList(),
                Outbox = source.Outbox == null
                    ? new List<TerritoryCaptureOutboxRecord>()
                    : source.Outbox.Select(CloneOutbox).ToList()
            };
        }

        private static TerritoryOwnershipRevisionData CloneRevision(
            TerritoryOwnershipRevisionData source)
        {
            if (source == null)
            {
                return null;
            }

            return new TerritoryOwnershipRevisionData
            {
                TerritoryId = source.TerritoryId,
                Revision = source.Revision
            };
        }

        private static TerritoryCaptureReceiptRecord CloneReceipt(
            TerritoryCaptureReceiptRecord source)
        {
            if (source == null)
            {
                return null;
            }

            return new TerritoryCaptureReceiptRecord
            {
                ReceiptId = source.ReceiptId,
                OperationId = source.OperationId,
                SemanticHash = source.SemanticHash,
                Durability = source.Durability,
                ResultId = source.ResultId,
                EventId = source.EventId,
                TerritoryId = source.TerritoryId,
                PreviousOwner = source.PreviousOwner,
                NewOwner = source.NewOwner,
                PreviousRevision = source.PreviousRevision,
                NewRevision = source.NewRevision,
                WarzoneCreditsDelta = source.WarzoneCreditsDelta,
                QuestProgressDelta = source.QuestProgressDelta,
                CatalogId = source.CatalogId,
                CatalogSchemaVersion = source.CatalogSchemaVersion,
                CatalogContentVersion = source.CatalogContentVersion,
                CatalogSourceRevision = source.CatalogSourceRevision,
                CatalogRawSha256 = source.CatalogRawSha256,
                StateRevisionHash = source.StateRevisionHash,
                ProfileSessionId = source.ProfileSessionId,
                AuthorizationId = source.AuthorizationId,
                AuthorizationSourceResultId = source.AuthorizationSourceResultId,
                AuthorizationSourceResultHash = source.AuthorizationSourceResultHash
            };
        }

        private static TerritoryCaptureOutboxRecord CloneOutbox(
            TerritoryCaptureOutboxRecord source)
        {
            if (source == null)
            {
                return null;
            }

            return new TerritoryCaptureOutboxRecord
            {
                EventId = source.EventId,
                CaptureOperationId = source.CaptureOperationId,
                TerritoryId = source.TerritoryId,
                PreviousOwner = source.PreviousOwner,
                NewOwner = source.NewOwner,
                PreviousRevision = source.PreviousRevision,
                NewRevision = source.NewRevision,
                CatalogId = source.CatalogId,
                CatalogSchemaVersion = source.CatalogSchemaVersion,
                CatalogContentVersion = source.CatalogContentVersion,
                CatalogSourceRevision = source.CatalogSourceRevision,
                CatalogRawSha256 = source.CatalogRawSha256,
                StateRevisionHash = source.StateRevisionHash,
                ProfileSessionId = source.ProfileSessionId,
                AuthorizationId = source.AuthorizationId,
                AuthorizationSourceResultId = source.AuthorizationSourceResultId,
                AuthorizationSourceResultHash = source.AuthorizationSourceResultHash,
                ReceiptId = source.ReceiptId
            };
        }
    }

    internal sealed class SaveBackedTerritoryEconomy : ITerritoryEconomyApplyTarget
    {
        private readonly SaveGameData _save;
        private readonly int _creditsBefore;
        private bool _applied;

        public SaveBackedTerritoryEconomy(SaveGameData save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _creditsBefore = save.WarzoneCredits;
        }

        public TerritoryApplyStepStatus Apply(TerritoryEconomyCommand command)
        {
            if (command == null || command.WarzoneCreditsDelta < 0)
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            if (command.WarzoneCreditsDelta == 0)
            {
                _applied = true;
                return TerritoryApplyStepStatus.Applied;
            }

            try
            {
                _save.WarzoneCredits = checked(_save.WarzoneCredits + command.WarzoneCreditsDelta);
            }
            catch (OverflowException)
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            _applied = true;
            return TerritoryApplyStepStatus.Applied;
        }

        public bool Rollback(TerritoryEconomyCommand command)
        {
            if (_applied)
            {
                _save.WarzoneCredits = _creditsBefore;
                _applied = false;
            }

            return true;
        }
    }

    internal sealed class SaveBackedTerritoryQuest : ITerritoryQuestApplyTarget
    {
        private readonly SaveGameData _save;
        private readonly List<QuestState> _questsBefore;
        private bool _applied;

        public SaveBackedTerritoryQuest(SaveGameData save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _questsBefore = CloneQuests(save.Quests);
        }

        public TerritoryApplyStepStatus Apply(TerritoryQuestCommand command)
        {
            if (command == null || command.ProgressDelta < 0)
            {
                return TerritoryApplyStepStatus.Rejected;
            }

            if (command.ProgressDelta == 0 ||
                !string.Equals(command.ProgressType, "CaptureTerritory", StringComparison.Ordinal))
            {
                _applied = true;
                return TerritoryApplyStepStatus.Applied;
            }

            _save.Quests ??= new List<QuestState>();
            foreach (QuestState state in _save.Quests)
            {
                if (state == null ||
                    state.IsCompleted ||
                    !string.Equals(state.QuestId, "Q5", StringComparison.Ordinal))
                {
                    continue;
                }

                int next = (int)Math.Min(int.MaxValue, (long)state.CurrentValue + command.ProgressDelta);
                state.CurrentValue = next;
                if (state.CurrentValue >= 1)
                {
                    state.IsCompleted = true;
                }
            }

            _applied = true;
            return TerritoryApplyStepStatus.Applied;
        }

        public bool Rollback(TerritoryQuestCommand command)
        {
            if (_applied)
            {
                _save.Quests = CloneQuests(_questsBefore);
                _applied = false;
            }

            return true;
        }

        private static List<QuestState> CloneQuests(List<QuestState> source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new List<QuestState>(source.Count);
            foreach (QuestState state in source)
            {
                if (state == null)
                {
                    clone.Add(null);
                    continue;
                }

                clone.Add(new QuestState
                {
                    QuestId = state.QuestId,
                    CurrentValue = state.CurrentValue,
                    IsCompleted = state.IsCompleted,
                    IsClaimed = state.IsClaimed
                });
            }

            return clone;
        }
    }
}
