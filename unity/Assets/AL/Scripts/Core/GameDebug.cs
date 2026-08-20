using UnityEngine;

namespace AL.Core
{
    /// <summary>
    /// Lightweight gate for verbose runtime logging. Debug.isDebugBuild is true in the
    /// Unity Editor and in development builds, and false in release builds — so routing
    /// combat/feedback spam through here keeps normal release play free of log flooding
    /// while preserving diagnostics for developers.
    /// </summary>
    public static class GameDebug
    {
        public static bool Enabled => Debug.isDebugBuild || Application.isEditor;

        public static void Log(object message)
        {
            if (Enabled)
            {
                Debug.Log(message);
            }
        }

        public static void LogWarning(object message)
        {
            if (Enabled)
            {
                Debug.LogWarning(message);
            }
        }
    }
}
