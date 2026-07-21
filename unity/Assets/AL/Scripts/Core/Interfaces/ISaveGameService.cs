using AL.Core;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public enum SaveLoadStatus
    {
        None,
        LoadedPrimary,
        RecoveredFromBackup,
        CreatedNew,
        CreatedNewAfterUnrecoverableCorruption,
        RecoveryFailed
    }

    public enum SaveOperationStatus
    {
        None,
        SavedPrimary,
        SaveFailedPreviousPreserved,
        DeleteFailed
    }

    public interface ISaveGameService
    {
        SaveGameData CurrentSave { get; }
        SaveLoadStatus LastLoadStatus { get; }
        string LastLoadMessage { get; }
        SaveOperationStatus LastSaveStatus { get; }
        string LastSaveMessage { get; }
        void Save();
        void Load();
        bool HasSave();
        void CreateNewSave(RealmId realmId);
        void DeleteSave();
    }
}
