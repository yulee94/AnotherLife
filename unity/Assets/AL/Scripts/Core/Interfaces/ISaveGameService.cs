using AL.Core;
using AL.Data.Runtime;

namespace AL.Core.Interfaces
{
    public enum SaveLoadStatus
    {
        None = 0,
        LoadedPrimary = 1,
        RecoveredFromBackup = 2,
        CreatedNew = 3,
        CreatedNewAfterUnrecoverableCorruption = 4,
        RecoveryFailed = 5,
        LoadedPrimaryNormalized = 6,
        LoadedPrimaryWithPreservedUnknown = 7,
        LoadedPrimaryDegraded = 8,
        LoadedForwardSchemaReadOnly = 9,
        RecoveryRequired = 10
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
