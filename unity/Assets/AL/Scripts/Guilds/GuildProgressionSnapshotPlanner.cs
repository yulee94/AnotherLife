using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AL.Guilds
{
    public sealed class GuildProgressionSnapshotPlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const int MaximumReceipts = 4096;

        private readonly GuildProgressionPolicySnapshot policy;

        public GuildProgressionSnapshotPlanner(GuildProgressionPolicySnapshot policy)
        {
            this.policy = policy;
        }

        public GuildProgressionPlanningResult ResolveMemberPerkProvenance(
            GuildProgressionStateSnapshot progression,
            GuildAuthoritySnapshot membership)
        {
            GuildProgressionPlanningResult policyGate = ValidatePolicy();
            if (policyGate != null)
            {
                return policyGate;
            }

            GuildProgressionPlanningResult membershipGate = ValidateMembership(membership);
            if (membershipGate != null)
            {
                return membershipGate;
            }

            GuildProgressionPlanningResult progressionGate = ValidateProgression(progression);
            if (progressionGate != null)
            {
                return progressionGate;
            }

            GuildMemberPerkProvenance[] provenance = ResolveProvenance(progression, membership);
            var plan = new GuildProgressionTransitionPlan(
                GuildProgressionOperation.AdvanceLevel,
                new string('0', 64),
                progression,
                progression,
                null,
                new string('0', 64),
                provenance);
            return new GuildProgressionPlanningResult(
                GuildPlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<GuildDiagnostic>());
        }

        public GuildProgressionPlanningResult Plan(
            GuildProgressionTransitionRequest request,
            GuildProgressionStateSnapshot progression,
            GuildAuthoritySnapshot membership)
        {
            if (!IsValidRequest(request))
            {
                return Reject(
                    GuildPlanningStatus.InvalidRequest,
                    "AL-GUILD-PROGRESSION-REQUEST-INVALID",
                    request?.OperationId,
                    "Guild progression identity, fields, or revisions are invalid.");
            }

            GuildProgressionPlanningResult policyGate = ValidatePolicy();
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(request.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    GuildPlanningStatus.StaleCatalog,
                    "AL-GUILD-CATALOG-STALE",
                    request.OperationId,
                    "The request is not fenced to the accepted Guild progression catalog.");
            }

            GuildProgressionPlanningResult membershipGate = ValidateMembership(membership);
            if (membershipGate != null)
            {
                return membershipGate;
            }

            GuildProgressionPlanningResult progressionGate = ValidateProgression(progression);
            if (progressionGate != null)
            {
                return progressionGate;
            }

            if (!string.Equals(progression.GuildId, request.GuildId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Conflict,
                    "AL-GUILD-PROGRESSION-GUILD-MISMATCH",
                    request.GuildId,
                    "Progression snapshot does not match the requested Guild.");
            }

            string requestFingerprint = RequestFingerprint(request);
            GuildProgressionPlanningResult replay = ClassifyReplay(
                request,
                requestFingerprint,
                progression.Receipts);
            if (replay != null)
            {
                return replay;
            }

            if (progression.Revision != request.ExpectedProgressionRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleAuthority,
                    "AL-GUILD-PROGRESSION-REVISION-STALE",
                    request.OperationId,
                    "Expected Guild progression revision is stale.");
            }

            if (progression.Revision == long.MaxValue)
            {
                return Reject(
                    GuildPlanningStatus.Overflow,
                    "AL-GUILD-PROGRESSION-REVISION-OVERFLOW",
                    request.OperationId,
                    "Guild progression revision cannot advance.");
            }

            if (progression.Receipts.Count >= MaximumReceipts)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PROGRESSION-RECEIPT-CAPACITY",
                    request.OperationId,
                    "Guild progression receipt history cannot safely accept another row.");
            }

            GuildSnapshot guild = FindGuild(membership, request.GuildId);
            if (guild == null || guild.Status != GuildStatus.Active)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-GUILD-ACTIVE-GUILD-NOT-FOUND",
                    request.GuildId,
                    "The requested active Guild does not exist.");
            }

            if (guild.Revision != request.ExpectedGuildRevision)
            {
                return Reject(
                    GuildPlanningStatus.StaleGuild,
                    "AL-GUILD-REVISION-STALE",
                    request.GuildId,
                    "Expected Guild revision is stale.");
            }

            if (!IsActiveMaster(guild, request.ActorAccountId))
            {
                return Reject(
                    GuildPlanningStatus.Unauthorized,
                    "AL-GUILD-ACTOR-UNAUTHORIZED",
                    request.ActorAccountId,
                    "Actor role does not authorize this Guild progression transition.");
            }

            if (request.Operation == GuildProgressionOperation.AdvanceLevel)
            {
                return PlanAdvanceLevel(request, requestFingerprint, progression, membership, guild);
            }

            return PlanCompleteResearch(request, requestFingerprint, progression, membership, guild);
        }

        private GuildProgressionPlanningResult PlanAdvanceLevel(
            GuildProgressionTransitionRequest request,
            string requestFingerprint,
            GuildProgressionStateSnapshot progression,
            GuildAuthoritySnapshot membership,
            GuildSnapshot guild)
        {
            GuildProgressionLevelDefinition level = policy.Levels.SingleOrDefault(row =>
                string.Equals(row.LevelId, request.TargetLevelId, StringComparison.Ordinal));
            if (level == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-GUILD-PROGRESSION-LEVEL-NOT-FOUND",
                    request.TargetLevelId,
                    "The requested Guild level is not in the catalog.");
            }

            if (string.Equals(progression.CurrentLevelId, request.TargetLevelId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.NoChange,
                    "AL-GUILD-PROGRESSION-LEVEL-UNCHANGED",
                    request.TargetLevelId,
                    "Guild already has the requested structural level.");
            }

            if (!string.IsNullOrEmpty(progression.CurrentLevelId))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-PROGRESSION-LEVEL-ALREADY-SET",
                    progression.CurrentLevelId,
                    "Additional Guild levels are not selected in this catalog.");
            }

            return Commit(
                request,
                requestFingerprint,
                progression,
                membership,
                guild,
                request.TargetLevelId,
                progression.CompletedResearchIds);
        }

        private GuildProgressionPlanningResult PlanCompleteResearch(
            GuildProgressionTransitionRequest request,
            string requestFingerprint,
            GuildProgressionStateSnapshot progression,
            GuildAuthoritySnapshot membership,
            GuildSnapshot guild)
        {
            GuildResearchDefinition research = policy.Research.SingleOrDefault(row =>
                string.Equals(row.ResearchId, request.TargetResearchId, StringComparison.Ordinal));
            if (research == null)
            {
                return Reject(
                    GuildPlanningStatus.NotFound,
                    "AL-GUILD-PROGRESSION-RESEARCH-NOT-FOUND",
                    request.TargetResearchId,
                    "The requested Guild research is not in the catalog.");
            }

            if (!string.Equals(progression.CurrentLevelId, research.RequiredLevelId, StringComparison.Ordinal))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-PROGRESSION-RESEARCH-LEVEL",
                    request.TargetResearchId,
                    "Guild research requires its catalog level prerequisite.");
            }

            if (research.RequiredResearchIds.Any(required =>
                    !progression.CompletedResearchIds.Contains(required)))
            {
                return Reject(
                    GuildPlanningStatus.Ineligible,
                    "AL-GUILD-PROGRESSION-RESEARCH-PREREQ",
                    request.TargetResearchId,
                    "Guild research requires its catalog research prerequisites.");
            }

            if (progression.CompletedResearchIds.Contains(request.TargetResearchId))
            {
                return Reject(
                    GuildPlanningStatus.NoChange,
                    "AL-GUILD-PROGRESSION-RESEARCH-UNCHANGED",
                    request.TargetResearchId,
                    "Guild already completed the requested research.");
            }

            string[] completed = progression.CompletedResearchIds
                .Concat(new[] { request.TargetResearchId })
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return Commit(
                request,
                requestFingerprint,
                progression,
                membership,
                guild,
                progression.CurrentLevelId,
                completed);
        }

        private GuildProgressionPlanningResult Commit(
            GuildProgressionTransitionRequest request,
            string requestFingerprint,
            GuildProgressionStateSnapshot progression,
            GuildAuthoritySnapshot membership,
            GuildSnapshot guild,
            string currentLevelId,
            IReadOnlyList<string> completedResearch)
        {
            long nextRevision = progression.Revision + 1;
            var receipt = new GuildProgressionReceipt(
                request.OperationId,
                request.Operation,
                requestFingerprint,
                request.GuildId,
                request.ActorAccountId,
                request.TargetLevelId,
                request.TargetResearchId,
                nextRevision,
                string.Empty,
                true);
            var candidate = new GuildProgressionStateSnapshot(
                GuildAuthorityStatus.Available,
                progression.GuildId,
                nextRevision,
                currentLevelId,
                completedResearch,
                progression.Receipts.Concat(new[] { receipt }).ToArray(),
                true);
            GuildMemberPerkProvenance[] provenance = ResolveProvenance(candidate, membership);
            string planHash = HashParts(
                "guild_progression_plan_v1",
                requestFingerprint,
                nextRevision.ToString(CultureInfo.InvariantCulture),
                currentLevelId ?? string.Empty,
                string.Join(",", completedResearch ?? Array.Empty<string>()),
                guild.Revision.ToString(CultureInfo.InvariantCulture));
            receipt = new GuildProgressionReceipt(
                receipt.OperationId,
                receipt.Operation,
                receipt.RequestFingerprint,
                receipt.GuildId,
                receipt.ActorAccountId,
                receipt.TargetLevelId,
                receipt.TargetResearchId,
                receipt.ResultingRevision,
                planHash,
                true);
            candidate = new GuildProgressionStateSnapshot(
                candidate.Status,
                candidate.GuildId,
                candidate.Revision,
                candidate.CurrentLevelId,
                candidate.CompletedResearchIds,
                progression.Receipts.Concat(new[] { receipt }).ToArray(),
                true);
            var plan = new GuildProgressionTransitionPlan(
                request.Operation,
                requestFingerprint,
                progression,
                candidate,
                receipt,
                planHash,
                provenance);
            return new GuildProgressionPlanningResult(
                GuildPlanningStatus.Prepared,
                plan,
                null,
                Array.Empty<GuildDiagnostic>());
        }

        private GuildMemberPerkProvenance[] ResolveProvenance(
            GuildProgressionStateSnapshot progression,
            GuildAuthoritySnapshot membership)
        {
            GuildSnapshot guild = FindGuild(membership, progression.GuildId);
            if (guild == null || guild.Status != GuildStatus.Active || guild.Members == null)
            {
                return Array.Empty<GuildMemberPerkProvenance>();
            }

            return guild.Members
                .Where(member => member != null && member.State == GuildMembershipState.Active)
                .SelectMany(member => policy.Perks
                    .Where(perk => PerkPrerequisitesMet(perk, progression))
                    .Select(perk => ToProvenance(member.AccountId, perk)))
                .OrderBy(row => row.AccountId, StringComparer.Ordinal)
                .ThenBy(row => row.RuleId, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool PerkPrerequisitesMet(
            GuildPerkDefinition perk,
            GuildProgressionStateSnapshot progression)
        {
            return string.Equals(progression.CurrentLevelId, perk.RequiredLevelId, StringComparison.Ordinal) &&
                   progression.CompletedResearchIds.Contains(perk.RequiredResearchId);
        }

        private static GuildMemberPerkProvenance ToProvenance(string accountId, GuildPerkDefinition perk)
        {
            return new GuildMemberPerkProvenance(
                accountId,
                perk.SourceId,
                perk.ProfileId,
                perk.RuleId,
                perk.RequiredLevelId,
                perk.RequiredResearchId,
                perk.Scope,
                perk.Cap == null ? string.Empty : perk.Cap.Kind,
                perk.Stacking == null ? string.Empty : perk.Stacking.Group,
                perk.Stacking == null ? 0 : perk.Stacking.Order,
                perk.Stacking == null ? string.Empty : perk.Stacking.Rule,
                perk.SourceVersion,
                perk.SourceHash,
                perk.StatBreakdownToken,
                perk.ProductionEligible);
        }

        private GuildProgressionPlanningResult ValidatePolicy()
        {
            if (policy == null ||
                policy.Status == GuildCatalogStatus.Unavailable ||
                policy.Binding == null)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-GUILD-PROGRESSION-CATALOG-UNAVAILABLE",
                    string.Empty,
                    "Guild progression catalog is unavailable.");
            }

            if (policy.Status != GuildCatalogStatus.Ready ||
                !policy.IsComplete ||
                policy.Levels == null ||
                policy.Research == null ||
                policy.Perks == null ||
                !IsValidBinding(policy.Binding))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PROGRESSION-CATALOG-MALFORMED",
                    string.Empty,
                    "Guild progression catalog is incomplete or contradictory.");
            }

            if (!policy.HiddenGlobalMultipliersForbidden ||
                policy.Perks.Any(perk => perk != null && perk.HiddenGlobalMultiplier))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PERK-HIDDEN-MULTIPLIER",
                    string.Empty,
                    "Hidden global Guild perk multipliers are forbidden.");
            }

            if (policy.NumericPerkTuningProductionEligible ||
                policy.Perks.Any(perk => perk != null && perk.ProductionEligible) ||
                policy.Perks.Any(perk => perk != null && perk.Cap != null && perk.Cap.ProductionEligible) ||
                policy.Levels.Any(level => level != null && level.ProductionEligible) ||
                policy.Research.Any(row => row != null && row.ProductionEligible))
            {
                return Reject(
                    GuildPlanningStatus.Unsupported,
                    "AL-GUILD-PERK-TUNING-PRODUCTION-INELIGIBLE",
                    string.Empty,
                    "Numeric Guild perk tuning is production-ineligible in this slice.");
            }

            if (policy.Perks.Any(perk => perk != null && perk.AppliesCombatMutation))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PERK-COMBAT-MUTATION",
                    string.Empty,
                    "Guild progression must not apply combat mutation.");
            }

            if (policy.Perks.Any(perk => !IsValidPerk(perk)))
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PERK-PROVENANCE-INCOMPLETE",
                    string.Empty,
                    "Every Guild perk must carry typed provenance.");
            }

            return null;
        }

        private static GuildProgressionPlanningResult ValidateMembership(GuildAuthoritySnapshot membership)
        {
            if (membership == null || membership.Status == GuildAuthorityStatus.Unavailable)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-GUILD-AUTHORITY-UNAVAILABLE",
                    string.Empty,
                    "Guild membership authority is unavailable.");
            }

            if (membership.Status != GuildAuthorityStatus.Available ||
                !membership.IsComplete ||
                membership.Guilds == null ||
                membership.PendingRequests == null ||
                membership.Receipts == null)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-AUTHORITY-MALFORMED",
                    string.Empty,
                    "Guild membership authority snapshot is incomplete or contradictory.");
            }

            return null;
        }

        private static GuildProgressionPlanningResult ValidateProgression(
            GuildProgressionStateSnapshot progression)
        {
            if (progression == null || progression.Status == GuildAuthorityStatus.Unavailable)
            {
                return Reject(
                    GuildPlanningStatus.Unavailable,
                    "AL-GUILD-PROGRESSION-UNAVAILABLE",
                    string.Empty,
                    "Guild progression authority is unavailable.");
            }

            if (progression.Status != GuildAuthorityStatus.Available ||
                !progression.IsComplete ||
                !IsStableId(progression.GuildId) ||
                progression.Revision < 0 ||
                progression.CompletedResearchIds == null ||
                progression.Receipts == null)
            {
                return Reject(
                    GuildPlanningStatus.Malformed,
                    "AL-GUILD-PROGRESSION-MALFORMED",
                    string.Empty,
                    "Guild progression snapshot is incomplete or contradictory.");
            }

            return null;
        }

        private static bool IsValidPerk(GuildPerkDefinition perk)
        {
            return perk != null &&
                   string.Equals(perk.SourceId, "guild_progression", StringComparison.Ordinal) &&
                   string.Equals(perk.ProfileId, "guild_member_character_stats", StringComparison.Ordinal) &&
                   IsStableId(perk.RuleId) &&
                   IsStableId(perk.RequiredLevelId) &&
                   IsStableId(perk.RequiredResearchId) &&
                   perk.Scope == GuildPerkScope.MemberCharacterStats &&
                   perk.Cap != null &&
                   string.Equals(perk.Cap.Kind, "unselected", StringComparison.Ordinal) &&
                   perk.Stacking != null &&
                   IsStableId(perk.Stacking.Group) &&
                   perk.Stacking.Order >= 0 &&
                   string.Equals(perk.Stacking.Rule, "explicit_visible_only", StringComparison.Ordinal) &&
                   IsOpaqueId(perk.SourceVersion) &&
                   IsSha256(perk.SourceHash) &&
                   IsBreakdownToken(perk.StatBreakdownToken);
        }

        private static GuildProgressionPlanningResult ClassifyReplay(
            GuildProgressionTransitionRequest request,
            string requestFingerprint,
            IReadOnlyList<GuildProgressionReceipt> receipts)
        {
            GuildProgressionReceipt existing = receipts.SingleOrDefault(row =>
                string.Equals(row.OperationId, request.OperationId, StringComparison.Ordinal));
            if (existing == null)
            {
                return null;
            }

            if (string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal) &&
                existing.Operation == request.Operation &&
                existing.IsSupported)
            {
                return new GuildProgressionPlanningResult(
                    GuildPlanningStatus.AlreadyCommitted,
                    null,
                    existing,
                    Array.Empty<GuildDiagnostic>());
            }

            return Reject(
                GuildPlanningStatus.Conflict,
                "AL-GUILD-PROGRESSION-OPERATION-CONFLICT",
                request.OperationId,
                "Guild progression operation identity collided with a different fingerprint.");
        }

        private static GuildSnapshot FindGuild(GuildAuthoritySnapshot membership, string guildId)
        {
            return membership.Guilds.SingleOrDefault(row =>
                string.Equals(row.GuildId, guildId, StringComparison.Ordinal));
        }

        private static bool IsActiveMaster(GuildSnapshot guild, string accountId)
        {
            return guild.Members != null &&
                   guild.Members.Any(row =>
                       string.Equals(row.AccountId, accountId, StringComparison.Ordinal) &&
                       row.State == GuildMembershipState.Active &&
                       row.Role == GuildRole.Master);
        }

        private static bool IsValidRequest(GuildProgressionTransitionRequest request)
        {
            if (request == null ||
                !Enum.IsDefined(typeof(GuildProgressionOperation), request.Operation) ||
                !IsOpaqueId(request.OperationId) ||
                !IsOpaqueId(request.ActorAccountId) ||
                !IsStableId(request.GuildId) ||
                request.ExpectedProgressionRevision < 0 ||
                request.ExpectedGuildRevision < 0 ||
                !IsValidBinding(request.ExpectedCatalogBinding))
            {
                return false;
            }

            switch (request.Operation)
            {
                case GuildProgressionOperation.AdvanceLevel:
                    return IsStableId(request.TargetLevelId) &&
                           string.IsNullOrEmpty(request.TargetResearchId);
                case GuildProgressionOperation.CompleteResearch:
                    return IsStableId(request.TargetResearchId) &&
                           string.IsNullOrEmpty(request.TargetLevelId);
                default:
                    return false;
            }
        }

        private static string RequestFingerprint(GuildProgressionTransitionRequest request)
        {
            return HashParts(
                "guild_progression_request_v1",
                ((int)request.Operation).ToString(CultureInfo.InvariantCulture),
                request.OperationId,
                request.ActorAccountId,
                request.GuildId,
                request.TargetLevelId,
                request.TargetResearchId,
                request.ExpectedProgressionRevision.ToString(CultureInfo.InvariantCulture),
                request.ExpectedGuildRevision.ToString(CultureInfo.InvariantCulture),
                BindingHash(request.ExpectedCatalogBinding));
        }

        private static string BindingHash(GuildCatalogBinding binding)
        {
            if (binding == null)
            {
                return string.Empty;
            }

            return HashParts(
                "guild_catalog_binding_v1",
                binding.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                binding.ContentVersion,
                binding.SourceRevision,
                binding.CatalogHash);
        }

        private static bool IsValidBinding(GuildCatalogBinding binding)
        {
            return binding != null &&
                   binding.SchemaVersion > 0 &&
                   IsOpaqueId(binding.ContentVersion) &&
                   IsOpaqueId(binding.SourceRevision) &&
                   IsSha256(binding.CatalogHash);
        }

        private static bool BindingEquals(GuildCatalogBinding left, GuildCatalogBinding right)
        {
            return left != null && right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   string.Equals(left.ContentVersion, right.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CatalogHash, right.CatalogHash, StringComparison.Ordinal);
        }

        private static bool IsOpaqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                Encoding.UTF8.GetByteCount(value) > MaximumIdentityUtf8Bytes)
            {
                return false;
            }

            return value.All(character => !char.IsControl(character) && !char.IsWhiteSpace(character));
        }

        private static bool IsStableId(string value)
        {
            if (!IsOpaqueId(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool previousUnderscore = false;
            for (var index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed = (character >= 'a' && character <= 'z') ||
                               (character >= '0' && character <= '9') ||
                               character == '_';
                if (!allowed || (character == '_' && previousUnderscore))
                {
                    return false;
                }

                previousUnderscore = character == '_';
            }

            return value[value.Length - 1] != '_';
        }

        private static bool IsBreakdownToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128 || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            return value.All(character =>
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '_' ||
                character == '.');
        }

        private static bool IsSha256(string value)
        {
            return value != null && value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
        }

        private static string HashParts(params string[] parts)
        {
            var canonical = new StringBuilder();
            foreach (string part in parts)
            {
                string value = part ?? string.Empty;
                canonical.Append(
                    Encoding.UTF8.GetByteCount(value).ToString(CultureInfo.InvariantCulture));
                canonical.Append(':');
                canonical.Append(value);
            }

            using (SHA256 sha = SHA256.Create())
            {
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))
                    .Select(value => value.ToString("x2")));
            }
        }

        private static GuildProgressionPlanningResult Reject(
            GuildPlanningStatus status,
            string code,
            string subjectId,
            string message)
        {
            return new GuildProgressionPlanningResult(
                status,
                null,
                null,
                new[] { new GuildDiagnostic(code, subjectId ?? string.Empty, message) });
        }
    }
}
