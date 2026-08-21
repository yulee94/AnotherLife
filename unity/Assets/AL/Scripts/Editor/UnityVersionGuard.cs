#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace AL.EditorTools
{
    /// <summary>
    /// Fails fast when this project is opened with any Unity Editor version other than the
    /// project-pinned 6000.3.22f1 LTS.
    ///
    /// Background: opening the project with Unity 6000.5.3f1 (installed side-by-side) corrupted
    /// Library/ShaderCache and left every UI element unrendered — the realm-select and kingdom
    /// screens looked empty and the console spammed "Couldn't open include file HLSLSupport.cginc".
    /// Application.unityVersion is the authoritative running version, so we block on any mismatch
    /// before the wrong editor can do further damage. 6000.5.x remains blocked.
    ///
    /// Honest limit: no editor-script hook runs before the very first asset import (scripts must
    /// compile before they can execute), so this fires at the earliest point Unity exposes — during
    /// domain reload — and then exits the editor so it never produces a version-mismatched cache.
    /// This is the strongest in-repo guard Unity supports; the environment-side mitigation
    /// (removing/renaming the side-by-side Unity 6 install) is handled separately.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityVersionGuard
    {
        /// <summary>The only Unity Editor version this project may be opened with.</summary>
        public const string RequiredUnityVersion = "6000.3.22f1";

        /// <summary>
        /// Environment variable that deliberately bypasses the guard. Set it to any non-empty value
        /// other than "0" or "false" for a sanctioned one-off open with a different version; the
        /// bypass is still logged as a warning so it can never happen silently.
        /// </summary>
        public const string AllowAnyVersionVariable = "AL_ALLOW_ANY_UNITY_VERSION";

        private static string _mismatchMessage;

        static UnityVersionGuard()
        {
            // Earliest script-level hook. Log before scheduling the blocking dialog/exit so the
            // wrong editor is never used silently even if the delayCall is somehow preempted.
            if (string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
            {
                return;
            }

            _mismatchMessage = BuildMismatchMessage(Application.unityVersion);
            Debug.LogError(_mismatchMessage);

            if (IsOverrideEnabled())
            {
                Debug.LogWarning(
                    "[UnityVersionGuard] Version mismatch tolerated because '" +
                    AllowAnyVersionVariable + "' is set. Shader-cache corruption risk is now yours.");
                return;
            }

            EditorApplication.delayCall += BlockAndExit;
        }

        private static void BlockAndExit()
        {
            // Modal dialog blocks interaction in the editor; worker import threads may already be
            // running, so exit rather than let the wrong editor continue importing.
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Wrong Unity version",
                    _mismatchMessage + "\n\nThe editor will now close.",
                    "Close");
            }

            EditorApplication.Exit(1);
        }

        private static string BuildMismatchMessage(string actualVersion)
        {
            return
                "AnotherLife must be opened with Unity " + RequiredUnityVersion + ".\n" +
                "Detected Unity " + actualVersion + ".\n" +
                "A version mismatch corrupts Library/ShaderCache and breaks all UI rendering\n" +
                "(the realm-select and kingdom screens appear empty).\n\n" +
                "Close the editor and reopen this project with Unity " + RequiredUnityVersion + ".";
        }

        private static bool IsOverrideEnabled()
        {
            string value = Environment.GetEnvironmentVariable(AllowAnyVersionVariable);
            return !string.IsNullOrWhiteSpace(value) &&
                   !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
