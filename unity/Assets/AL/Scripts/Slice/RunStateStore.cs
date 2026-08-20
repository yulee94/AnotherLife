using System;
using System.IO;
using UnityEngine;

namespace AL.Slice
{
    public enum RunStateSaveStatus
    {
        Saved = 0,
        NoState = 1
    }

    public enum RunStateLoadStatus
    {
        Loaded = 0,
        RecoveredFromMemory = 1,
        NotFound = 2,
        Corrupt = 3
    }

    public readonly struct RunStateSaveResult
    {
        public RunStateSaveResult(RunStateSaveStatus status, bool persistedToDisk, string filePath, string message)
        {
            Status = status;
            PersistedToDisk = persistedToDisk;
            FilePath = filePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public RunStateSaveStatus Status { get; }
        public bool PersistedToDisk { get; }
        public string FilePath { get; }
        public string Message { get; }
        public bool Succeeded => Status == RunStateSaveStatus.Saved;
    }

    public readonly struct RunStateLoadResult
    {
        public RunStateLoadResult(RunStateLoadStatus status, RunState state, string filePath, string message)
        {
            Status = status;
            State = state;
            FilePath = filePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public RunStateLoadStatus Status { get; }
        public RunState State { get; }
        public string FilePath { get; }
        public string Message { get; }
        public bool Succeeded =>
            State != null &&
            (Status == RunStateLoadStatus.Loaded || Status == RunStateLoadStatus.RecoveredFromMemory);
    }

    /// <summary>
    /// Local slice save/reload: a single JSON file with an in-memory fallback. Independent of the
    /// catalog/save/determinism authority — it never touches ISaveGameService, SaveAuthority, or the
    /// offline service stack. Save always succeeds in memory; the disk write is best-effort and its
    /// outcome is reported honestly via <see cref="RunStateSaveResult.PersistedToDisk"/>.
    /// </summary>
    public static class RunStateStore
    {
        public const string DefaultFileName = "slice_run_state.json";

        private static RunState _memory;

        /// <summary>Returns a defensive clone of the last saved/loaded in-memory snapshot, or null.</summary>
        public static RunState InMemory => _memory != null ? _memory.Clone() : null;

        public static bool HasInMemoryState => _memory != null;

        public static string DefaultFilePath => Path.Combine(DefaultDirectory, DefaultFileName);

        public static string DefaultDirectory
        {
            get
            {
                try
                {
                    return Application.persistentDataPath;
                }
                catch
                {
                    return Path.GetTempPath();
                }
            }
        }

        public static RunStateSaveResult Save(RunState state, string directory = null)
        {
            if (state == null)
            {
                return new RunStateSaveResult(RunStateSaveStatus.NoState, false, null, "No run state was supplied to save.");
            }

            // The in-memory copy is always taken first: it is the fallback that guarantees the run is
            // resumable this session even when the disk is unavailable.
            _memory = state.Clone();

            string dir = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
            string path = Path.Combine(dir, DefaultFileName);

            try
            {
                Directory.CreateDirectory(dir);
                string json = state.ToJson(prettyPrint: true);
                string tempPath = path + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
                return new RunStateSaveResult(RunStateSaveStatus.Saved, true, path, string.Empty);
            }
            catch (Exception ex)
            {
                return new RunStateSaveResult(
                    RunStateSaveStatus.Saved,
                    false,
                    path,
                    $"Disk write failed; state held in memory only. {ex.Message}");
            }
        }

        public static RunStateLoadResult Load(string directory = null)
        {
            string dir = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
            string path = Path.Combine(dir, DefaultFileName);

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    RunState state = RunState.FromJson(json);
                    if (state != null)
                    {
                        _memory = state.Clone();
                        return new RunStateLoadResult(RunStateLoadStatus.Loaded, state, path, string.Empty);
                    }

                    return new RunStateLoadResult(RunStateLoadStatus.Corrupt, null, path, "Snapshot file was unreadable or empty.");
                }
                catch (Exception ex)
                {
                    return new RunStateLoadResult(RunStateLoadStatus.Corrupt, null, path, $"Snapshot file could not be read: {ex.Message}");
                }
            }

            if (_memory != null)
            {
                return new RunStateLoadResult(RunStateLoadStatus.RecoveredFromMemory, _memory.Clone(), path, "No snapshot file found; recovered from in-memory state.");
            }

            return new RunStateLoadResult(RunStateLoadStatus.NotFound, null, path, "No snapshot file or in-memory state available.");
        }

        public static bool Delete(string directory = null)
        {
            string dir = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
            string path = Path.Combine(dir, DefaultFileName);
            _memory = null;
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearMemory()
        {
            _memory = null;
        }
    }
}
