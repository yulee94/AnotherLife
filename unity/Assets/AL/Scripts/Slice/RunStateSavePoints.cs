using System;
using UnityEngine;

namespace AL.Slice
{
    /// <summary>
    /// The two concrete slice integration points:
    ///  - <see cref="SaveAfterKingdomBuild"/> is the save point that runs once the kingdom build action
    ///    has completed (or at any natural break in the loop). It stamps the phase and persists a
    ///    snapshot of the live <see cref="RunStateSession"/>.
    ///  - <see cref="LoadFromMainOrPause"/> is the reload entry used at main menu / pause to restore a
    ///    previous run so the player can continue or replay the loop.
    /// Both operate only on <see cref="RunStateSession"/> and <see cref="RunStateStore"/>; neither
    /// touches the catalog/save/determinism authority.
    /// </summary>
    public static class RunStateSavePoints
    {
        public static RunStateSaveResult SaveAfterKingdomBuild(string directory = null)
        {
            RunState current = RunStateSession.Current;
            if (current == null)
            {
                return new RunStateSaveResult(RunStateSaveStatus.NoState, false, null, "No live run state; nothing to save.");
            }

            current.phase = current.kingdom.buildPerformed ? SlicePhase.Complete : SlicePhase.KingdomBuild;
            current.savedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            RunStateSession.Set(current);

            return RunStateStore.Save(current, directory);
        }

        public static RunStateLoadResult LoadFromMainOrPause(string directory = null)
        {
            RunStateLoadResult result = RunStateStore.Load(directory);
            if (result.Succeeded)
            {
                RunStateSession.Set(result.State);
            }

            return result;
        }
    }

    /// <summary>
    /// Optional drop-in component for the pause/quit save path. Attach it to a persistent GameObject
    /// (for example the slice boot root) and it will persist the live run state whenever the app
    /// backgrounds or exits, mirroring the "reload from the pause path" behaviour.
    /// </summary>
    public sealed class SliceRunStateAutosave : MonoBehaviour
    {
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                RunStateSavePoints.SaveAfterKingdomBuild();
            }
        }

        private void OnApplicationQuit()
        {
            RunStateSavePoints.SaveAfterKingdomBuild();
        }
    }
}
