using System;
using System.Linq;
using AL.Benchmarks.GoldenScenes;
using UnityEditor;
using UnityEngine;

namespace AL.EditorTools
{
    [InitializeOnLoad]
    internal static class GoldenSceneBenchmarkEditorBootstrap
    {
        static GoldenSceneBenchmarkEditorBootstrap()
        {
            EditorApplication.delayCall += TryEnterPlayMode;
        }

        internal static bool ShouldEnterPlayMode(
            string[] arguments,
            bool isBatchMode,
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            return isBatchMode &&
                   !isPlaying &&
                   !isPlayingOrWillChangePlaymode &&
                   arguments != null &&
                   arguments.Any(argument => string.Equals(
                       argument,
                       GoldenSceneBenchmarkRequestParser.EnableArgument,
                       StringComparison.Ordinal));
        }

        private static void TryEnterPlayMode()
        {
            if (!ShouldEnterPlayMode(
                    Environment.GetCommandLineArgs(),
                    Application.isBatchMode,
                    EditorApplication.isPlaying,
                    EditorApplication.isPlayingOrWillChangePlaymode))
                return;

            EditorApplication.EnterPlaymode();
        }
    }
}
