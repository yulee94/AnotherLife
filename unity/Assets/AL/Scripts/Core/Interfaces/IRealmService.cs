using AL.Core;
using AL.Data.Definitions;
using AL.RealmSelection;

namespace AL.Core.Interfaces
{
    public interface IRealmService
    {
        RealmId CurrentRealmId { get; }
        RealmDefinition CurrentRealm { get; }
        RealmIdentitySnapshot Identity { get; }
        RealmSelectionResult TrySelectRealm(RealmSelectionRequest request);
        void SelectRealm(RealmId id);
    }
}
