using AL.Core;
using AL.Data.Definitions;

namespace AL.Core.Interfaces
{
    public interface IRealmService
    {
        RealmId CurrentRealmId { get; }
        RealmDefinition CurrentRealm { get; }
        void SelectRealm(RealmId id);
    }
}
