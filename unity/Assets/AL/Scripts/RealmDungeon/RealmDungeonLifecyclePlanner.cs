using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AL.RealmDungeon
{
    public sealed class RealmDungeonLifecyclePlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const long RequiredCooldownSeconds = 604800L;

        private readonly RealmDungeonCatalogSnapshot catalog;

        public RealmDungeonLifecyclePlanner(RealmDungeonCatalogSnapshot catalog)
        {
            this.catalog = catalog;
        }

        public RealmDungeonCatalogSnapshot Catalog => catalog;

        public RealmDungeonPlanningResult Plan(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot)
        {
            if (!IsValidRequest(request) || snapshot == null)
            {
                return Reject(RealmDungeonPlanningStatus.InvalidRequest, RealmDungeonRejectReason.InvalidRequest);
            }

            RealmDungeonPlanningResult catalogGate = ValidateCatalog();
            if (catalogGate != null)
            {
                return catalogGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, catalog.Binding))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.StaleCatalog);
            }

            RealmDungeonPlanningResult aliasGate = RejectAlias(request);
            if (aliasGate != null)
            {
                return aliasGate;
            }

            RealmDungeonDefinition dungeon = FindDungeon(request.DungeonId);
            if (dungeon == null ||
                !string.Equals(dungeon.RaidDragonId, request.RaidDragonId, StringComparison.Ordinal))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.UnknownDungeon);
            }

            if (!string.IsNullOrEmpty(request.EntranceId) &&
                (dungeon.EntranceIds == null || !dungeon.EntranceIds.Contains(request.EntranceId)))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.UnknownDungeon);
            }

            if (request.Operation == RealmDungeonOperation.TraversePortal ||
                request.Operation == RealmDungeonOperation.BeginManifestation ||
                request.Operation == RealmDungeonOperation.CompleteManifestation)
            {
                if (!IsStableId(request.PortalId) ||
                    !string.Equals(dungeon.PortalId, request.PortalId, StringComparison.Ordinal))
                {
                    return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.UnknownDungeon);
                }

                if (request.Traversal == RealmDungeonPortalTraversal.Inward ||
                    request.Traversal == RealmDungeonPortalTraversal.Ambient)
                {
                    return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.InwardPortalTraversal);
                }
            }

            if (request.ExpectedRevision != snapshot.Revision)
            {
                return Reject(RealmDungeonPlanningStatus.InvalidRequest, RealmDungeonRejectReason.InvalidRequest);
            }

            if (!string.Equals(snapshot.DungeonId, request.DungeonId, StringComparison.Ordinal) ||
                !string.Equals(snapshot.RaidDragonId, request.RaidDragonId, StringComparison.Ordinal))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.UnknownDungeon);
            }

            string fingerprint = RequestFingerprint(request);
            if (request.Operation == RealmDungeonOperation.CommitDefeat)
            {
                RealmDungeonReceipt sameOperation = FindReceiptByOperation(snapshot, request.OperationId);
                if (sameOperation != null &&
                    string.Equals(sameOperation.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return new RealmDungeonPlanningResult(
                        RealmDungeonPlanningStatus.AlreadyCommitted,
                        RealmDungeonRejectReason.None,
                        null,
                        sameOperation,
                        false);
                }

                RealmDungeonReceipt sameDefeat = FindReceiptByDefeat(snapshot, request.DefeatIdentity);
                if (sameDefeat != null)
                {
                    return Reject(RealmDungeonPlanningStatus.Conflict, RealmDungeonRejectReason.None);
                }
            }

            if (request.Operation == RealmDungeonOperation.ReportFault)
            {
                return Freeze(request, snapshot, fingerprint);
            }

            if (request.FaultKind == RealmDungeonFaultKind.TrustedTimeUnavailable ||
                request.FaultKind == RealmDungeonFaultKind.SplitBrainOwnership ||
                request.FaultKind == RealmDungeonFaultKind.CorruptState)
            {
                return Freeze(request, snapshot, fingerprint);
            }

            if (request.TrustedClockUnixSeconds < snapshot.LastObservedClockUnixSeconds)
            {
                return Freeze(request, snapshot, fingerprint);
            }

            switch (request.Operation)
            {
                case RealmDungeonOperation.Observe:
                    return Observe(request, snapshot, fingerprint);
                case RealmDungeonOperation.Engage:
                    return Engage(request, snapshot, fingerprint);
                case RealmDungeonOperation.CommitDefeat:
                    return CommitDefeat(request, snapshot, fingerprint);
                case RealmDungeonOperation.RecordNonKill:
                    return RecordNonKill(request, snapshot, fingerprint);
                case RealmDungeonOperation.TraversePortal:
                    return TraversePortal(request, snapshot, fingerprint);
                case RealmDungeonOperation.BeginManifestation:
                    return BeginManifestation(request, snapshot, fingerprint);
                case RealmDungeonOperation.CompleteManifestation:
                    return CompleteManifestation(request, snapshot, fingerprint);
                default:
                    return Reject(RealmDungeonPlanningStatus.InvalidRequest, RealmDungeonRejectReason.InvalidRequest);
            }
        }

        private RealmDungeonPlanningResult Observe(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            RealmDungeonLifeState lifeState = snapshot.LifeState;
            if (lifeState == RealmDungeonLifeState.Cooldown &&
                snapshot.NextEligibleAtUnixSeconds > 0 &&
                request.TrustedClockUnixSeconds >= snapshot.NextEligibleAtUnixSeconds)
            {
                lifeState = RealmDungeonLifeState.RespawnEligible;
            }

            return Prepare(
                request,
                snapshot,
                fingerprint,
                Clone(
                    snapshot,
                    lifeState,
                    snapshot.DefeatCommittedAtUnixSeconds,
                    snapshot.NextEligibleAtUnixSeconds,
                    request.TrustedClockUnixSeconds,
                    snapshot.LeaseId,
                    snapshot.SpawnCycleId,
                    TargetableFor(lifeState),
                    InvulnerableFor(lifeState),
                    snapshot.Receipts,
                    null));
        }

        private RealmDungeonPlanningResult Engage(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            if (snapshot.LifeState != RealmDungeonLifeState.AliveIdle)
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.Unsupported);
            }

            return Prepare(
                request,
                snapshot,
                fingerprint,
                Clone(
                    snapshot,
                    RealmDungeonLifeState.AliveEngaged,
                    snapshot.DefeatCommittedAtUnixSeconds,
                    snapshot.NextEligibleAtUnixSeconds,
                    request.TrustedClockUnixSeconds,
                    snapshot.LeaseId,
                    snapshot.SpawnCycleId,
                    true,
                    false,
                    snapshot.Receipts,
                    null));
        }

        private RealmDungeonPlanningResult CommitDefeat(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            if (!catalog.KillOnly ||
                (snapshot.LifeState != RealmDungeonLifeState.AliveEngaged &&
                 snapshot.LifeState != RealmDungeonLifeState.DefeatCommitPending))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.Unsupported);
            }

            long nextEligible = request.TrustedClockUnixSeconds + catalog.CooldownSeconds;
            RealmDungeonReceipt receipt = new RealmDungeonReceipt(
                request.OperationId,
                fingerprint,
                request.DefeatIdentity,
                snapshot.DungeonId,
                snapshot.RaidDragonId,
                snapshot.InstanceId,
                request.TrustedClockUnixSeconds,
                nextEligible);
            List<RealmDungeonReceipt> receipts = new List<RealmDungeonReceipt>(
                snapshot.Receipts ?? Array.Empty<RealmDungeonReceipt>())
            {
                receipt
            };

            return Prepare(
                request,
                snapshot,
                fingerprint,
                Clone(
                    snapshot,
                    RealmDungeonLifeState.Cooldown,
                    request.TrustedClockUnixSeconds,
                    nextEligible,
                    request.TrustedClockUnixSeconds,
                    string.Empty,
                    string.Empty,
                    false,
                    true,
                    receipts,
                    receipt),
                receipt);
        }

        private RealmDungeonPlanningResult RecordNonKill(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            if (request.NonKillKind == RealmDungeonNonKillKind.None ||
                !Enum.IsDefined(typeof(RealmDungeonNonKillKind), request.NonKillKind))
            {
                return Reject(RealmDungeonPlanningStatus.InvalidRequest, RealmDungeonRejectReason.InvalidRequest);
            }

            return Prepare(
                request,
                snapshot,
                fingerprint,
                Clone(
                    snapshot,
                    snapshot.LifeState,
                    snapshot.DefeatCommittedAtUnixSeconds,
                    snapshot.NextEligibleAtUnixSeconds,
                    request.TrustedClockUnixSeconds,
                    snapshot.LeaseId,
                    snapshot.SpawnCycleId,
                    snapshot.Targetable,
                    snapshot.Invulnerable,
                    snapshot.Receipts,
                    null));
        }

        private RealmDungeonPlanningResult TraversePortal(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            if (request.Traversal != RealmDungeonPortalTraversal.Outward ||
                snapshot.LifeState != RealmDungeonLifeState.Manifesting ||
                !IsStableId(snapshot.LeaseId) || !IsStableId(snapshot.SpawnCycleId) ||
                !string.Equals(snapshot.LeaseId, request.LeaseId, StringComparison.Ordinal) ||
                !string.Equals(snapshot.SpawnCycleId, request.SpawnCycleId, StringComparison.Ordinal))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.Unsupported);
            }

            if (!request.PresentationApproved || !snapshot.PresentationApproved)
            {
                return Reject(RealmDungeonPlanningStatus.Unavailable, RealmDungeonRejectReason.MissingPresentationBundle);
            }

            return Observe(request, snapshot, fingerprint);
        }

        private RealmDungeonPlanningResult BeginManifestation(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            if (!request.PresentationApproved ||
                !snapshot.PresentationApproved ||
                catalog.GenericFallback)
            {
                return new RealmDungeonPlanningResult(
                    RealmDungeonPlanningStatus.Unavailable,
                    RealmDungeonRejectReason.MissingPresentationBundle,
                    null,
                    null,
                    false);
            }

            if (snapshot.LifeState == RealmDungeonLifeState.Manifesting &&
                (!IsStableId(snapshot.LeaseId) || !IsStableId(snapshot.SpawnCycleId) ||
                 !string.Equals(snapshot.LeaseId, request.LeaseId, StringComparison.Ordinal) ||
                 !string.Equals(snapshot.SpawnCycleId, request.SpawnCycleId, StringComparison.Ordinal)))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.DuplicateLease);
            }

            if (snapshot.LifeState != RealmDungeonLifeState.RespawnEligible &&
                snapshot.LifeState != RealmDungeonLifeState.Manifesting)
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.Unsupported);
            }

            if (!IsStableId(request.LeaseId) || !IsStableId(request.SpawnCycleId))
            {
                return Reject(RealmDungeonPlanningStatus.InvalidRequest, RealmDungeonRejectReason.InvalidRequest);
            }

            return Prepare(
                request,
                snapshot,
                fingerprint,
                Clone(
                    snapshot,
                    RealmDungeonLifeState.Manifesting,
                    snapshot.DefeatCommittedAtUnixSeconds,
                    snapshot.NextEligibleAtUnixSeconds,
                    request.TrustedClockUnixSeconds,
                    request.LeaseId,
                    request.SpawnCycleId,
                    false,
                    true,
                    snapshot.Receipts,
                    null));
        }

        private RealmDungeonPlanningResult CompleteManifestation(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            if (!request.PresentationApproved || !snapshot.PresentationApproved)
            {
                return new RealmDungeonPlanningResult(
                    RealmDungeonPlanningStatus.Unavailable,
                    RealmDungeonRejectReason.MissingPresentationBundle,
                    null,
                    null,
                    false);
            }

            if (snapshot.LifeState != RealmDungeonLifeState.Manifesting ||
                !IsStableId(snapshot.LeaseId) || !IsStableId(snapshot.SpawnCycleId) ||
                !string.Equals(snapshot.LeaseId, request.LeaseId, StringComparison.Ordinal) ||
                !string.Equals(snapshot.SpawnCycleId, request.SpawnCycleId, StringComparison.Ordinal))
            {
                return Reject(RealmDungeonPlanningStatus.Rejected, RealmDungeonRejectReason.Unsupported);
            }

            return Prepare(
                request,
                snapshot,
                fingerprint,
                Clone(
                    snapshot,
                    RealmDungeonLifeState.AliveIdle,
                    0,
                    0,
                    request.TrustedClockUnixSeconds,
                    string.Empty,
                    string.Empty,
                    true,
                    false,
                    snapshot.Receipts,
                    null));
        }

        private RealmDungeonPlanningResult Freeze(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot snapshot,
            string fingerprint)
        {
            return new RealmDungeonPlanningResult(
                RealmDungeonPlanningStatus.RecoveryRequired,
                RealmDungeonRejectReason.None,
                new RealmDungeonTransitionPlan(
                    request.Operation,
                    fingerprint,
                    snapshot,
                    Clone(
                        snapshot,
                        RealmDungeonLifeState.RecoveryRequired,
                        snapshot.DefeatCommittedAtUnixSeconds,
                        snapshot.NextEligibleAtUnixSeconds,
                        snapshot.LastObservedClockUnixSeconds,
                        snapshot.LeaseId,
                        snapshot.SpawnCycleId,
                        false,
                        true,
                        snapshot.Receipts,
                        null),
                    null),
                null,
                false);
        }

        private RealmDungeonPlanningResult ValidateCatalog()
        {
            if (catalog == null ||
                catalog.Binding == null ||
                catalog.Dungeons == null ||
                catalog.GuildClosedBossProfileIds == null ||
                catalog.GuardianCatalogDragonIds == null)
            {
                return Reject(RealmDungeonPlanningStatus.Unavailable, RealmDungeonRejectReason.Unsupported);
            }

            if (catalog.Status != RealmDungeonCatalogStatus.Ready ||
                !catalog.IsComplete ||
                !catalog.KillOnly ||
                catalog.CooldownSeconds != RequiredCooldownSeconds ||
                catalog.ProductionEligible ||
                catalog.GenericFallback ||
                catalog.Dungeons.Count != 4)
            {
                return Reject(RealmDungeonPlanningStatus.Unavailable, RealmDungeonRejectReason.Unsupported);
            }

            return null;
        }

        private RealmDungeonPlanningResult RejectAlias(RealmDungeonTransitionRequest request)
        {
            if (StartsWithPrefix(request.DungeonId, catalog.GuildClosedInstanceIdPrefix) ||
                StartsWithPrefix(request.RaidDragonId, catalog.GuildClosedInstanceIdPrefix) ||
                catalog.GuildClosedBossProfileIds.Contains(request.RaidDragonId) ||
                catalog.GuildClosedBossProfileIds.Contains(request.DungeonId))
            {
                return Reject(
                    RealmDungeonPlanningStatus.Rejected,
                    RealmDungeonRejectReason.GuildClosedInstanceAlias);
            }

            if (catalog.GuardianCatalogDragonIds.Contains(request.RaidDragonId) ||
                catalog.GuardianCatalogDragonIds.Contains(request.DungeonId))
            {
                return Reject(
                    RealmDungeonPlanningStatus.Rejected,
                    RealmDungeonRejectReason.GuardianIdentityAlias);
            }

            return null;
        }

        private RealmDungeonDefinition FindDungeon(string dungeonId)
        {
            return catalog.Dungeons.FirstOrDefault(value =>
                value != null && string.Equals(value.DungeonId, dungeonId, StringComparison.Ordinal));
        }

        private static RealmDungeonPlanningResult Prepare(
            RealmDungeonTransitionRequest request,
            RealmDungeonAuthoritySnapshot expected,
            string fingerprint,
            RealmDungeonAuthoritySnapshot candidate,
            RealmDungeonReceipt reward = null)
        {
            return new RealmDungeonPlanningResult(
                RealmDungeonPlanningStatus.Prepared,
                RealmDungeonRejectReason.None,
                new RealmDungeonTransitionPlan(request.Operation, fingerprint, expected, candidate, reward),
                null,
                false);
        }

        private static RealmDungeonPlanningResult Reject(
            RealmDungeonPlanningStatus status,
            RealmDungeonRejectReason reason)
        {
            return new RealmDungeonPlanningResult(status, reason, null, null, false);
        }

        private static RealmDungeonAuthoritySnapshot Clone(
            RealmDungeonAuthoritySnapshot source,
            RealmDungeonLifeState lifeState,
            long defeatCommittedAt,
            long nextEligibleAt,
            long lastObservedClock,
            string leaseId,
            string spawnCycleId,
            bool targetable,
            bool invulnerable,
            IEnumerable<RealmDungeonReceipt> receipts,
            RealmDungeonReceipt extraReceipt)
        {
            RealmDungeonReceipt[] nextReceipts = (receipts ?? Array.Empty<RealmDungeonReceipt>()).ToArray();
            return new RealmDungeonAuthoritySnapshot(
                source.DungeonId,
                source.RaidDragonId,
                source.InstanceId,
                lifeState,
                defeatCommittedAt,
                nextEligibleAt,
                lastObservedClock,
                leaseId,
                spawnCycleId,
                targetable,
                invulnerable,
                source.PresentationApproved,
                source.ProductionEligible,
                source.Revision + 1,
                nextReceipts);
        }

        private static bool TargetableFor(RealmDungeonLifeState lifeState)
        {
            return lifeState == RealmDungeonLifeState.AliveIdle ||
                   lifeState == RealmDungeonLifeState.AliveEngaged ||
                   lifeState == RealmDungeonLifeState.DefeatCommitPending;
        }

        private static bool InvulnerableFor(RealmDungeonLifeState lifeState)
        {
            return !TargetableFor(lifeState);
        }

        private static RealmDungeonReceipt FindReceiptByOperation(
            RealmDungeonAuthoritySnapshot snapshot,
            string operationId)
        {
            return snapshot.Receipts == null
                ? null
                : snapshot.Receipts.FirstOrDefault(value =>
                    value != null &&
                    string.Equals(value.OperationId, operationId, StringComparison.Ordinal));
        }

        private static RealmDungeonReceipt FindReceiptByDefeat(
            RealmDungeonAuthoritySnapshot snapshot,
            string defeatIdentity)
        {
            return snapshot.Receipts == null
                ? null
                : snapshot.Receipts.FirstOrDefault(value =>
                    value != null &&
                    string.Equals(value.DefeatIdentity, defeatIdentity, StringComparison.Ordinal));
        }

        private static string RequestFingerprint(RealmDungeonTransitionRequest request)
        {
            return string.Join(
                "|",
                new[]
                {
                    request.Operation.ToString(),
                    request.OperationId,
                    request.DungeonId,
                    request.RaidDragonId,
                    request.DefeatIdentity
                });
        }

        private static bool BindingEquals(
            RealmDungeonCatalogBinding left,
            RealmDungeonCatalogBinding right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(left.CatalogId, right.CatalogId, StringComparison.Ordinal) &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CatalogHash, right.CatalogHash, StringComparison.Ordinal);
        }

        private static bool StartsWithPrefix(string value, string prefix)
        {
            return !string.IsNullOrEmpty(prefix) &&
                   !string.IsNullOrEmpty(value) &&
                   value.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static bool IsValidRequest(RealmDungeonTransitionRequest request)
        {
            return request != null &&
                   Enum.IsDefined(typeof(RealmDungeonOperation), request.Operation) &&
                   IsStableId(request.OperationId) &&
                   IsStableId(request.DungeonId) &&
                   IsStableId(request.RaidDragonId) &&
                   (string.IsNullOrEmpty(request.EntranceId) || IsStableId(request.EntranceId)) &&
                   (string.IsNullOrEmpty(request.PortalId) || IsStableId(request.PortalId)) &&
                   (string.IsNullOrEmpty(request.DefeatIdentity) || IsStableId(request.DefeatIdentity)) &&
                   (string.IsNullOrEmpty(request.LeaseId) || IsStableId(request.LeaseId)) &&
                   (string.IsNullOrEmpty(request.SpawnCycleId) || IsStableId(request.SpawnCycleId)) &&
                   request.TrustedClockUnixSeconds >= 0 &&
                   request.ExpectedRevision >= 0 &&
                   Enum.IsDefined(typeof(RealmDungeonNonKillKind), request.NonKillKind) &&
                   Enum.IsDefined(typeof(RealmDungeonFaultKind), request.FaultKind) &&
                   Enum.IsDefined(typeof(RealmDungeonPortalTraversal), request.Traversal);
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) > MaximumIdentityUtf8Bytes)
            {
                return false;
            }

            if (value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool ok = (character >= 'a' && character <= 'z') ||
                          (character >= '0' && character <= '9') ||
                          character == '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
