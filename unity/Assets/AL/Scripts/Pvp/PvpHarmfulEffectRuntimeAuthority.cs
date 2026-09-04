using System;
using System.Collections.Generic;
using System.Linq;

namespace AL.Pvp
{
    public sealed class PvpHarmfulEffectRuntimeAuthority
    {
        private readonly PvpHarmfulEffectGatePlanner planner;
        private readonly PvpZonePresenceCatalog zones;

        public PvpHarmfulEffectRuntimeAuthority(
            PvpHarmfulEffectGatePlanner planner,
            PvpZonePresenceCatalog zones)
        {
            this.planner = planner;
            this.zones = zones;
        }

        public PvpHarmfulEffectApplicationReceipt Apply(
            PvpHarmfulEffectApplicationRequest request,
            IPvpHarmfulHealthMutator mutator)
        {
            PvpHarmfulEffectApplicationReceipt receipt = Evaluate(request);
            mutator?.TryMutate(receipt);
            return receipt;
        }

        public IReadOnlyList<PvpHarmfulEffectApplicationReceipt> ApplyEach(
            IEnumerable<PvpHarmfulEffectApplicationRequest> requests,
            IPvpHarmfulHealthMutator mutator)
        {
            return (requests ?? Enumerable.Empty<PvpHarmfulEffectApplicationRequest>())
                .Select(request => Apply(request, mutator))
                .ToArray();
        }

        public PvpHostileTargetAuthorization AuthorizeHostileTarget(
            PvpHarmfulEffectApplicationRequest request)
        {
            PvpHarmfulEffectApplicationReceipt receipt = Evaluate(request);
            return new PvpHostileTargetAuthorization(
                receipt.Applied,
                receipt.Gate,
                receipt.Presentation);
        }

        internal static PvpHarmfulEffectApplicationReceipt Unbound(
            PvpHarmfulEffectKind kind,
            IPvpHarmfulHealthMutator mutator)
        {
            PvpHarmfulEffectGateDecision gate = UnknownDecision(
                kind,
                "AL-PVP-RUNTIME-UNBOUND",
                string.Empty);
            PvpHarmfulEffectApplicationReceipt receipt = ReceiptFrom(gate);
            mutator?.TryMutate(receipt);
            return receipt;
        }

        private PvpHarmfulEffectApplicationReceipt Evaluate(
            PvpHarmfulEffectApplicationRequest request)
        {
            PvpHarmfulEffectKind kind = request != null &&
                                        request.Provenance != null &&
                                        Enum.IsDefined(typeof(PvpHarmfulEffectKind), request.Provenance.EffectKind)
                ? request.Provenance.EffectKind
                : PvpHarmfulEffectKind.DirectHit;

            if (planner == null || zones == null || request == null ||
                request.Source == null || request.Target == null || request.Provenance == null)
            {
                return ReceiptFrom(UnknownDecision(kind, "AL-PVP-RUNTIME-UNBOUND", string.Empty));
            }

            if (!zones.TryResolve(request.Source.ZoneId, out PvpZonePolicyKind sourceKind) ||
                !zones.TryResolve(request.Target.ZoneId, out PvpZonePolicyKind targetKind))
            {
                return ReceiptFrom(
                    UnknownDecision(
                        kind,
                        "AL-PVP-RUNTIME-ZONE-UNKNOWN",
                        request.Target.ZoneId ?? string.Empty));
            }

            PvpHarmfulEffectQuery query = new PvpHarmfulEffectQuery(
                WithZoneKind(request.Source, sourceKind),
                WithZoneKind(request.Target, targetKind),
                request.Provenance,
                request.Guilds,
                request.Alliances,
                request.ExpectedGuildAuthorityRevision,
                request.ExpectedAllianceAuthorityRevision,
                request.ExpectedSourceToggleRevision,
                request.ExpectedTargetToggleRevision,
                request.ExpectedSourceZoneRevision,
                request.ExpectedTargetZoneRevision,
                request.ExpectedSourceActorRevision,
                request.ExpectedTargetActorRevision,
                request.ExpectedCatalogBinding,
                request.ClockUnixSeconds);
            return ReceiptFrom(planner.Evaluate(query));
        }

        private static PvpHarmfulEffectApplicationReceipt ReceiptFrom(
            PvpHarmfulEffectGateDecision gate)
        {
            bool applied = gate != null && gate.Eligible;
            return new PvpHarmfulEffectApplicationReceipt(
                gate,
                new PvpHostilePresentationView(
                    gate == null ? PvpPresentationKind.Unknown : gate.Presentation),
                applied,
                applied);
        }

        private static PvpActorSnapshot WithZoneKind(
            PvpActorSnapshot actor,
            PvpZonePolicyKind kind)
        {
            return new PvpActorSnapshot(
                actor.AccountId,
                actor.CharacterId,
                actor.SessionGeneration,
                actor.ImmutableRealmId,
                actor.LifeState,
                kind,
                actor.ZoneId,
                actor.ZonePolicyRevision,
                actor.PvpToggleEnabled,
                actor.PvpToggleRevision,
                actor.ActorRevision);
        }

        private static PvpHarmfulEffectGateDecision UnknownDecision(
            PvpHarmfulEffectKind kind,
            string code,
            string subjectId)
        {
            return new PvpHarmfulEffectGateDecision(
                PvpGateStatus.Indeterminate,
                PvpGateRejectReason.UnknownAuthority,
                PvpPresentationKind.Unknown,
                kind,
                false,
                code,
                subjectId);
        }
    }

    public static class PvpHarmfulEffectRuntimeGate
    {
        private static PvpHarmfulEffectRuntimeAuthority authority;
        private static IPvpHarmfulEffectSession session;

        public static void Bind(PvpHarmfulEffectRuntimeAuthority value)
        {
            authority = value;
            session = null;
        }

        public static void Bind(
            PvpHarmfulEffectRuntimeAuthority value,
            IPvpHarmfulEffectSession boundSession)
        {
            authority = value;
            session = boundSession;
        }

        public static void Reset()
        {
            authority = null;
            session = null;
        }

        public static PvpHarmfulEffectApplicationReceipt Apply(
            PvpHarmfulEffectApplicationRequest request,
            IPvpHarmfulHealthMutator mutator)
        {
            if (authority == null)
            {
                PvpHarmfulEffectKind kind = request != null && request.Provenance != null
                    ? request.Provenance.EffectKind
                    : PvpHarmfulEffectKind.DirectHit;
                return PvpHarmfulEffectRuntimeAuthority.Unbound(kind, mutator);
            }

            return authority.Apply(request, mutator);
        }

        public static PvpHarmfulEffectApplicationReceipt ApplyOverlap(
            PvpHarmfulEffectKind kind,
            IPvpHarmfulHealthMutator mutator)
        {
            if (authority == null ||
                session == null ||
                !session.TryCreate(kind, out PvpHarmfulEffectApplicationRequest request) ||
                request == null)
            {
                return PvpHarmfulEffectRuntimeAuthority.Unbound(kind, mutator);
            }

            return authority.Apply(request, mutator);
        }
    }
}
