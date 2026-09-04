using System;
using System.Collections.Generic;
using System.Linq;
using AL.Alliances;
using AL.Guilds;

namespace AL.Pvp
{
    public sealed class PvpHarmfulEffectGatePlanner
    {
        private const int MaximumIdentityUtf8Bytes = 128;
        private const long SecondsPerHour = 3600L;

        private readonly PvpHarmfulEffectGatePolicySnapshot policy;

        public PvpHarmfulEffectGatePlanner(PvpHarmfulEffectGatePolicySnapshot policy)
        {
            this.policy = policy;
        }

        public PvpHarmfulEffectGateDecision Evaluate(PvpHarmfulEffectQuery query)
        {
            if (query == null ||
                query.Source == null ||
                query.Target == null ||
                query.Provenance == null)
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    PvpHarmfulEffectKind.DirectHit,
                    false,
                    "AL-PVP-GATE-UNKNOWN",
                    string.Empty);
            }

            PvpHarmfulEffectKind kind = query.Provenance.EffectKind;
            if (!Enum.IsDefined(typeof(PvpHarmfulEffectKind), kind))
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    PvpHarmfulEffectKind.DirectHit,
                    false,
                    "AL-PVP-GATE-EFFECT-UNKNOWN",
                    query.Provenance.HitIdentity);
            }

            PvpHarmfulEffectGateDecision policyGate = ValidatePolicy(kind, query.Provenance.HitIdentity);
            if (policyGate != null)
            {
                return policyGate;
            }

            if (!BindingEquals(query.ExpectedCatalogBinding, policy.Binding))
            {
                return Reject(
                    PvpGateRejectReason.StaleRevision,
                    kind,
                    "AL-PVP-GATE-CATALOG-STALE",
                    query.Provenance.HitIdentity);
            }

            if (!IsValidActor(query.Source) || !IsValidActor(query.Target))
            {
                return Reject(
                    PvpGateRejectReason.InvalidRequest,
                    kind,
                    "AL-PVP-GATE-ACTOR-INVALID",
                    query.Provenance.HitIdentity);
            }

            if (query.ClockUnixSeconds < 0)
            {
                return Reject(
                    PvpGateRejectReason.InvalidRequest,
                    kind,
                    "AL-PVP-GATE-CLOCK-INVALID",
                    query.Provenance.HitIdentity);
            }

            if (!IsStableId(query.Provenance.OwnerAccountId) ||
                !IsStableId(query.Provenance.OwnerCharacterId) ||
                !IsStableId(query.Provenance.SourceActionId) ||
                !IsStableId(query.Provenance.HitIdentity))
            {
                return Reject(
                    PvpGateRejectReason.InvalidRequest,
                    kind,
                    "AL-PVP-GATE-PROVENANCE-INVALID",
                    query.Provenance.HitIdentity);
            }

            if (!string.Equals(
                    query.Provenance.OwnerAccountId, query.Source.AccountId, StringComparison.Ordinal) ||
                !string.Equals(
                    query.Provenance.OwnerCharacterId, query.Source.CharacterId, StringComparison.Ordinal))
            {
                return Reject(
                    PvpGateRejectReason.ProvenanceMismatch,
                    kind,
                    "AL-PVP-GATE-PROVENANCE-MISMATCH",
                    query.Provenance.HitIdentity);
            }

            if (IsStale(query))
            {
                return Reject(
                    PvpGateRejectReason.StaleRevision,
                    kind,
                    "AL-PVP-GATE-REVISION-STALE",
                    query.Provenance.HitIdentity);
            }

            if (string.Equals(
                    query.Source.CharacterId, query.Target.CharacterId, StringComparison.Ordinal) ||
                string.Equals(
                    query.Source.AccountId, query.Target.AccountId, StringComparison.Ordinal))
            {
                return Reject(
                    PvpGateRejectReason.SameActor,
                    kind,
                    "AL-PVP-GATE-SAME-ACTOR",
                    query.Source.CharacterId);
            }

            if (query.Source.LifeState != PvpActorLifeState.Alive ||
                query.Target.LifeState != PvpActorLifeState.Alive)
            {
                return Reject(
                    PvpGateRejectReason.ActorNotLive,
                    kind,
                    "AL-PVP-GATE-ACTOR-NOT-LIVE",
                    query.Target.CharacterId);
            }

            if (!IsStableId(query.Source.ImmutableRealmId) ||
                !IsStableId(query.Target.ImmutableRealmId) ||
                !string.Equals(
                    query.Source.ImmutableRealmId,
                    query.Target.ImmutableRealmId,
                    StringComparison.Ordinal))
            {
                return Reject(
                    PvpGateRejectReason.CrossRealm,
                    kind,
                    "AL-PVP-GATE-CROSS-REALM",
                    query.Target.ImmutableRealmId);
            }

            if (query.Source.ZoneKind == PvpZonePolicyKind.Unknown ||
                query.Target.ZoneKind == PvpZonePolicyKind.Unknown ||
                !Enum.IsDefined(typeof(PvpZonePolicyKind), query.Source.ZoneKind) ||
                !Enum.IsDefined(typeof(PvpZonePolicyKind), query.Target.ZoneKind))
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-ZONE-UNKNOWN",
                    query.Target.ZoneId);
            }

            if (policy.ForcedSafeZones.Contains(query.Source.ZoneKind) ||
                policy.ForcedSafeZones.Contains(query.Target.ZoneKind))
            {
                return Decide(
                    PvpGateStatus.Rejected,
                    PvpGateRejectReason.ForcedSafeZone,
                    PvpPresentationKind.Protected,
                    kind,
                    false,
                    "AL-PVP-GATE-FORCED-SAFE",
                    query.Target.ZoneId);
            }

            PvpHarmfulEffectGateDecision social = EvaluateSocialRelation(query, kind);
            if (social != null)
            {
                return social;
            }

            bool forcedByWar = HasOpposingActiveWar(query);
            if ((query.Source.PvpToggleEnabled && query.Target.PvpToggleEnabled) || forcedByWar)
            {
                return Decide(
                    PvpGateStatus.Eligible,
                    PvpGateRejectReason.None,
                    forcedByWar ? PvpPresentationKind.WarHostile : PvpPresentationKind.Hostile,
                    kind,
                    forcedByWar,
                    forcedByWar ? "AL-PVP-GATE-WAR-HOSTILE" : "AL-PVP-GATE-TOGGLE-HOSTILE",
                    query.Target.CharacterId);
            }

            return Decide(
                PvpGateStatus.Rejected,
                PvpGateRejectReason.ToggleOff,
                PvpPresentationKind.Neutral,
                kind,
                false,
                "AL-PVP-GATE-TOGGLE-OFF",
                query.Target.CharacterId);
        }

        public IReadOnlyList<PvpHarmfulEffectGateDecision> EvaluateEach(
            IEnumerable<PvpHarmfulEffectQuery> queries)
        {
            return (queries ?? Enumerable.Empty<PvpHarmfulEffectQuery>())
                .Select(Evaluate)
                .ToArray();
        }

        private PvpHarmfulEffectGateDecision EvaluateSocialRelation(
            PvpHarmfulEffectQuery query,
            PvpHarmfulEffectKind kind)
        {
            if (!IsUsableGuildAuthority(query.Guilds) || !IsUsableAllianceAuthority(query.Alliances))
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-SOCIAL-UNKNOWN",
                    query.Provenance.HitIdentity);
            }

            GuildSnapshot sourceGuild;
            GuildSnapshot targetGuild;
            if (!TryResolveGuild(query.Guilds, query.Source.AccountId, out sourceGuild) ||
                !TryResolveGuild(query.Guilds, query.Target.AccountId, out targetGuild))
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-GUILD-AMBIGUOUS",
                    query.Provenance.HitIdentity);
            }

            if (sourceGuild != null &&
                targetGuild != null &&
                string.Equals(sourceGuild.GuildId, targetGuild.GuildId, StringComparison.Ordinal))
            {
                return Decide(
                    PvpGateStatus.Rejected,
                    PvpGateRejectReason.SameGuild,
                    PvpPresentationKind.Protected,
                    kind,
                    false,
                    "AL-PVP-GATE-SAME-GUILD",
                    sourceGuild.GuildId);
            }

            AllianceSnapshot sourceAlliance;
            AllianceSnapshot targetAlliance;
            if (!TryResolveAlliance(query.Alliances, sourceGuild, out sourceAlliance) ||
                !TryResolveAlliance(query.Alliances, targetGuild, out targetAlliance))
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-ALLIANCE-AMBIGUOUS",
                    query.Provenance.HitIdentity);
            }

            if (sourceAlliance != null &&
                targetAlliance != null &&
                string.Equals(
                    sourceAlliance.AllianceId, targetAlliance.AllianceId, StringComparison.Ordinal))
            {
                return Decide(
                    PvpGateStatus.Rejected,
                    PvpGateRejectReason.SameAlliance,
                    PvpPresentationKind.Protected,
                    kind,
                    false,
                    "AL-PVP-GATE-SAME-ALLIANCE",
                    sourceAlliance.AllianceId);
            }

            return null;
        }

        private bool HasOpposingActiveWar(PvpHarmfulEffectQuery query)
        {
            if (!IsUsableGuildAuthority(query.Guilds) || !IsUsableAllianceAuthority(query.Alliances))
            {
                return false;
            }

            GuildSnapshot sourceGuild;
            GuildSnapshot targetGuild;
            if (!TryResolveGuild(query.Guilds, query.Source.AccountId, out sourceGuild) ||
                !TryResolveGuild(query.Guilds, query.Target.AccountId, out targetGuild) ||
                sourceGuild == null ||
                targetGuild == null)
            {
                return false;
            }

            AllianceSnapshot sourceAlliance;
            AllianceSnapshot targetAlliance;
            if (!TryResolveAlliance(query.Alliances, sourceGuild, out sourceAlliance) ||
                !TryResolveAlliance(query.Alliances, targetGuild, out targetAlliance) ||
                sourceAlliance == null ||
                targetAlliance == null)
            {
                return false;
            }

            AllianceWarSnapshot war = FindWarBetween(
                query.Alliances, sourceAlliance.AllianceId, targetAlliance.AllianceId);
            return EffectiveWarState(war, query.ClockUnixSeconds) == policy.ForceHostilityWarState &&
                   policy.ForceHostilityWarState == AllianceWarState.Active;
        }

        private PvpHarmfulEffectGateDecision ValidatePolicy(
            PvpHarmfulEffectKind kind,
            string subjectId)
        {
            if (policy == null ||
                policy.Binding == null ||
                policy.RevalidatedEffectKinds == null ||
                policy.ForcedSafeZones == null)
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-POLICY-UNKNOWN",
                    subjectId);
            }

            if (policy.Status == PvpCatalogStatus.Unavailable ||
                policy.Status == PvpCatalogStatus.Incomplete ||
                !policy.IsComplete)
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-POLICY-UNAVAILABLE",
                    subjectId);
            }

            if (policy.Status == PvpCatalogStatus.UnsupportedVersion)
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.Unsupported,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-POLICY-UNSUPPORTED",
                    subjectId);
            }

            if (policy.Status != PvpCatalogStatus.Ready ||
                policy.ClientAuthority ||
                policy.HealthMutation ||
                policy.PresentationIsAuthoritative ||
                policy.WarNoticeHours != 24 ||
                policy.WarActiveHours != 168 ||
                policy.ForceHostilityWarState != AllianceWarState.Active)
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.UnknownAuthority,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-POLICY-MALFORMED",
                    subjectId);
            }

            if (!policy.RevalidatedEffectKinds.Contains(kind))
            {
                return Decide(
                    PvpGateStatus.Indeterminate,
                    PvpGateRejectReason.Unsupported,
                    PvpPresentationKind.Unknown,
                    kind,
                    false,
                    "AL-PVP-GATE-EFFECT-NOT-COVERED",
                    subjectId);
            }

            return null;
        }

        private static bool IsStale(PvpHarmfulEffectQuery query)
        {
            return query.Guilds == null ||
                   query.Alliances == null ||
                   query.ExpectedGuildAuthorityRevision != query.Guilds.Revision ||
                   query.ExpectedAllianceAuthorityRevision != query.Alliances.Revision ||
                   query.ExpectedSourceToggleRevision != query.Source.PvpToggleRevision ||
                   query.ExpectedTargetToggleRevision != query.Target.PvpToggleRevision ||
                   query.ExpectedSourceZoneRevision != query.Source.ZonePolicyRevision ||
                   query.ExpectedTargetZoneRevision != query.Target.ZonePolicyRevision ||
                   query.ExpectedSourceActorRevision != query.Source.ActorRevision ||
                   query.ExpectedTargetActorRevision != query.Target.ActorRevision;
        }

        private static bool IsValidActor(PvpActorSnapshot actor)
        {
            return actor != null &&
                   IsStableId(actor.AccountId) &&
                   IsStableId(actor.CharacterId) &&
                   actor.SessionGeneration > 0 &&
                   actor.ZonePolicyRevision >= 0 &&
                   actor.PvpToggleRevision >= 0 &&
                   actor.ActorRevision >= 0 &&
                   Enum.IsDefined(typeof(PvpActorLifeState), actor.LifeState);
        }

        private static bool IsUsableGuildAuthority(GuildAuthoritySnapshot guilds)
        {
            return guilds != null &&
                   guilds.Status == GuildAuthorityStatus.Available &&
                   guilds.IsComplete &&
                   guilds.Guilds != null;
        }

        private static bool IsUsableAllianceAuthority(AllianceAuthoritySnapshot alliances)
        {
            return alliances != null &&
                   alliances.Status == AllianceAuthorityStatus.Available &&
                   alliances.IsComplete &&
                   alliances.Alliances != null &&
                   alliances.Wars != null;
        }

        private static bool TryResolveGuild(
            GuildAuthoritySnapshot guilds,
            string accountId,
            out GuildSnapshot guild)
        {
            GuildSnapshot[] matches = guilds.Guilds
                .Where(row =>
                    row != null &&
                    row.Status == GuildStatus.Active &&
                    row.Members != null &&
                    row.Members.Any(member =>
                        member != null &&
                        member.State == GuildMembershipState.Active &&
                        string.Equals(member.AccountId, accountId, StringComparison.Ordinal)))
                .ToArray();
            if (matches.Length > 1)
            {
                guild = null;
                return false;
            }

            guild = matches.Length == 1 ? matches[0] : null;
            return true;
        }

        private static bool TryResolveAlliance(
            AllianceAuthoritySnapshot alliances,
            GuildSnapshot guild,
            out AllianceSnapshot alliance)
        {
            alliance = null;
            if (guild == null)
            {
                return true;
            }

            AllianceSnapshot[] matches = alliances.Alliances
                .Where(row =>
                    row != null &&
                    row.Relation == AllianceRelationState.Active &&
                    row.MemberGuilds != null &&
                    row.MemberGuilds.Any(member =>
                        member != null &&
                        string.Equals(member.GuildId, guild.GuildId, StringComparison.Ordinal)))
                .ToArray();
            if (matches.Length > 1)
            {
                return false;
            }

            alliance = matches.Length == 1 ? matches[0] : null;
            return true;
        }

        private static AllianceWarSnapshot FindWarBetween(
            AllianceAuthoritySnapshot snapshot,
            string leftAllianceId,
            string rightAllianceId)
        {
            if (string.IsNullOrEmpty(leftAllianceId) || string.IsNullOrEmpty(rightAllianceId))
            {
                return null;
            }

            AllianceWarSnapshot[] matches = snapshot.Wars.Where(row =>
                    row != null &&
                    WarMatches(row, leftAllianceId, rightAllianceId) &&
                    row.CommittedState != AllianceWarState.None)
                .ToArray();
            AllianceWarSnapshot[] open = matches.Where(row =>
                    row.CommittedState == AllianceWarState.Declared ||
                    row.CommittedState == AllianceWarState.Active)
                .ToArray();
            if (open.Length == 1)
            {
                return open[0];
            }

            return open.Length == 0 && matches.Length == 1 ? matches[0] : null;
        }

        private static bool WarMatches(
            AllianceWarSnapshot war,
            string leftAllianceId,
            string rightAllianceId)
        {
            return (string.Equals(war.AttackerAllianceId, leftAllianceId, StringComparison.Ordinal) &&
                    string.Equals(war.DefenderAllianceId, rightAllianceId, StringComparison.Ordinal)) ||
                   (string.Equals(war.AttackerAllianceId, rightAllianceId, StringComparison.Ordinal) &&
                    string.Equals(war.DefenderAllianceId, leftAllianceId, StringComparison.Ordinal));
        }

        private AllianceWarState EffectiveWarState(AllianceWarSnapshot war, long clockUnixSeconds)
        {
            if (war == null || war.CommittedState == AllianceWarState.None)
            {
                return AllianceWarState.None;
            }

            if (war.CommittedState == AllianceWarState.Cooling ||
                war.CommittedState == AllianceWarState.ReconciliationPending)
            {
                return war.CommittedState;
            }

            long noticeEnd = checked(war.DeclaredAtUnixSeconds + (policy.WarNoticeHours * SecondsPerHour));
            long activeEnd = checked(noticeEnd + (policy.WarActiveHours * SecondsPerHour));
            if (clockUnixSeconds < noticeEnd)
            {
                return AllianceWarState.Declared;
            }

            if (clockUnixSeconds < activeEnd)
            {
                return AllianceWarState.Active;
            }

            return AllianceWarState.Cooling;
        }

        private static bool BindingEquals(GuildCatalogBinding left, GuildCatalogBinding right)
        {
            return left != null &&
                   right != null &&
                   left.SchemaVersion == right.SchemaVersion &&
                   string.Equals(left.ContentVersion, right.ContentVersion, StringComparison.Ordinal) &&
                   string.Equals(left.SourceRevision, right.SourceRevision, StringComparison.Ordinal) &&
                   string.Equals(left.CatalogHash, right.CatalogHash, StringComparison.Ordinal);
        }

        private static bool IsStableId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaximumIdentityUtf8Bytes)
            {
                return false;
            }

            if (value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static PvpHarmfulEffectGateDecision Reject(
            PvpGateRejectReason reason,
            PvpHarmfulEffectKind kind,
            string code,
            string subjectId)
        {
            PvpPresentationKind presentation = reason == PvpGateRejectReason.ForcedSafeZone ||
                                               reason == PvpGateRejectReason.SameGuild ||
                                               reason == PvpGateRejectReason.SameAlliance
                ? PvpPresentationKind.Protected
                : PvpPresentationKind.Neutral;
            return Decide(
                PvpGateStatus.Rejected,
                reason,
                presentation,
                kind,
                false,
                code,
                subjectId);
        }

        private static PvpHarmfulEffectGateDecision Decide(
            PvpGateStatus status,
            PvpGateRejectReason reason,
            PvpPresentationKind presentation,
            PvpHarmfulEffectKind kind,
            bool forcedByActiveWar,
            string code,
            string subjectId)
        {
            return new PvpHarmfulEffectGateDecision(
                status,
                reason,
                presentation,
                kind,
                forcedByActiveWar,
                code,
                subjectId);
        }
    }
}
