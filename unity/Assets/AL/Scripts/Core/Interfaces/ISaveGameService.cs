using AL.Core;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public interface ISaveGameService
    {
        SaveGameData CurrentSave { get; }
        void Save();
        void Load();
        bool HasSave();
        void CreateNewSave(RealmId realmId);
    }
}
