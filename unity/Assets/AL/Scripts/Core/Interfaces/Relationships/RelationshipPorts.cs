namespace AL.Core.Interfaces.Relationships
{
    public interface IRelationshipIdentityResolver
    {
        RelationshipCatalogAvailability Availability { get; }

        string IdentityCatalogRevision { get; }

        RelationshipIdentityCatalogValidationResult CatalogValidation { get; }

        RelationshipIdentityResolution ResolveNpc(string npcId);

        RelationshipIdentityResolution ResolveFaction(string factionId);
    }

    public interface IRelationshipPolicyResolver
    {
        RelationshipCatalogAvailability Availability { get; }

        RelationshipPolicyValidationResult PolicyValidation { get; }

        RelationshipPolicySnapshot ResolvePolicy();
    }

    public interface IRelationshipMutationTarget
    {
        RelationshipRawState CurrentRawState { get; }

        RelationshipSnapshot GetCurrentSnapshot(
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies);

        RelationshipApplyResult Apply(
            RelationshipPreparedPlan plan,
            IRelationshipIdentityResolver identities,
            IRelationshipPolicyResolver policies,
            IRelationshipOperationLedger ledger);
    }

    public interface IRelationshipOperationLedger
    {
        bool TryGet(string operationId, out RelationshipPreparedPlan appliedPlan);

        bool TryFindCorrelation(string correlationId, out string operationId);

        bool TryRecord(RelationshipPreparedPlan plan, out string conflictCorrelationId);
    }
}
