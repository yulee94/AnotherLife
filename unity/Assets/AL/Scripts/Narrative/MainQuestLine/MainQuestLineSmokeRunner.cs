using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AL.Narrative.MainQuestLine
{
    [DefaultExecutionOrder(-31000)]
    [DisallowMultipleComponent]
    public sealed class MainQuestLineSmokeRunner : MonoBehaviour
    {
        public const string HostObjectName = "AL Narrative Smoke Runner";

        private static MainQuestLineSmokeRunner _instance;
        private bool _running;

        public static bool IsRequested(IEnumerable<string> arguments)
        {
            if (arguments == null)
            {
                return false;
            }

            foreach (string argument in arguments)
            {
                if (string.Equals(argument, MainQuestLineContract.SmokeEnableArgument, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static MainQuestLineSmokeRunner EnsureRunning()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var host = new GameObject(HostObjectName);
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<MainQuestLineSmokeRunner>();
            return _instance;
        }

        public static string ResolveOutputPath(IEnumerable<string> arguments)
        {
            if (arguments == null)
            {
                return string.Empty;
            }

            string previous = null;
            foreach (string argument in arguments)
            {
                if (string.Equals(previous, MainQuestLineContract.SmokeOutputArgument, StringComparison.Ordinal))
                {
                    return argument ?? string.Empty;
                }

                previous = argument;
            }

            return string.Empty;
        }

        private void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            Application.runInBackground = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string outputPath = ResolveOutputPath(arguments);
            var sceneSequence = new List<string> { SceneManager.GetActiveScene().name };

            MainQuestLineCatalog catalog;
            MainQuestLineDiagnostic diagnostic;
            if (!MainQuestLineCatalogLoader.TryLoadCanonical(out catalog, out diagnostic))
            {
                yield return Finish(false, outputPath, catalog, sceneSequence, string.Empty, string.Empty, diagnostic);
                yield break;
            }

            Debug.Log(
                MainQuestLineContract.ActiveMarker +
                " catalog=" + catalog.CanonicalSha256 +
                " packet=" + catalog.PacketVersion +
                " chapter=" + catalog.EntryChapterId +
                " quest=" + catalog.EntryQuestId +
                " scene=" + SceneManager.GetActiveScene().name);

            MainQuestLineExecutionResult execution = MainQuestLineRuntime.ExecuteRepresentativePath(catalog);
            if (!execution.Succeeded)
            {
                yield return Finish(
                    false,
                    outputPath,
                    catalog,
                    sceneSequence,
                    execution.ProgressedStateId,
                    execution.ResumedStateId,
                    execution.Diagnostic);
                yield break;
            }

            string kingdomName = MainQuestLineContract.EntryScene;
            if (!string.Equals(SceneManager.GetActiveScene().name, kingdomName, StringComparison.Ordinal))
            {
                if (!Application.CanStreamedLevelBeLoaded(kingdomName))
                {
                    yield return Finish(
                        false,
                        outputPath,
                        catalog,
                        sceneSequence,
                        execution.ProgressedStateId,
                        execution.ResumedStateId,
                        new MainQuestLineDiagnostic(
                            MainQuestLineContract.DiagnosticPrefix + "DEPENDENCY-MISSING",
                            "Kingdom scene is not in the packaged player shell.",
                            kingdomName,
                            "unloaded"));
                    yield break;
                }

                AsyncOperation load = SceneManager.LoadSceneAsync(kingdomName, LoadSceneMode.Single);
                if (load == null)
                {
                    yield return Finish(
                        false,
                        outputPath,
                        catalog,
                        sceneSequence,
                        execution.ProgressedStateId,
                        execution.ResumedStateId,
                        new MainQuestLineDiagnostic(
                            MainQuestLineContract.DiagnosticPrefix + "DEPENDENCY-MISSING",
                            "Kingdom scene load did not start.",
                            kingdomName,
                            "null"));
                    yield break;
                }

                while (!load.isDone)
                {
                    yield return null;
                }
            }

            sceneSequence.Add(SceneManager.GetActiveScene().name);
            MainQuestLineHost host = MainQuestLineHost.AttachIfNeeded();
            host.Refresh();
            yield return null;
            yield return Finish(
                host.Catalog != null && host.Progress != null,
                outputPath,
                catalog,
                sceneSequence,
                execution.ProgressedStateId,
                execution.ResumedStateId,
                host.Diagnostic);
        }

        private IEnumerator Finish(
            bool succeeded,
            string outputPath,
            MainQuestLineCatalog catalog,
            List<string> sceneSequence,
            string progressedStateId,
            string resumedStateId,
            MainQuestLineDiagnostic diagnostic)
        {
            string gameData = MainQuestLineCatalogLoader.ResolveGameDataDirectory();
            var evidence = new MainQuestLineEvidenceFile
            {
                schemaVersion = 1,
                status = succeeded ? MainQuestLineContract.PassStatus : "failed",
                reasonCode = succeeded
                    ? MainQuestLineContract.PassReason
                    : diagnostic != null
                        ? diagnostic.Code
                        : MainQuestLineContract.DiagnosticPrefix + "FAILED",
                applicationIsEditor = Application.isEditor,
                unityVersion = Application.unityVersion,
                buildGuid = Application.isEditor ? "editor" : Application.buildGUID,
                enabledSceneManifestSha256 = MainQuestLineCatalogLoader.HashFileOrEmpty(
                    Path.Combine(gameData, MainQuestLineContract.EnabledSceneManifestFileName)),
                generatedSceneManifestSha256 = MainQuestLineCatalogLoader.HashFileOrEmpty(
                    Path.Combine(gameData, MainQuestLineContract.GeneratedSceneManifestFileName)),
                narrativeCatalogSha256 = catalog != null
                    ? catalog.CanonicalSha256
                    : string.Empty,
                narrativePacketVersion = catalog != null
                    ? catalog.PacketVersion
                    : string.Empty,
                entryChapterId = MainQuestLineContract.EntryChapterId,
                entryQuestId = MainQuestLineContract.EntryQuestId,
                progressedQuestStateId = progressedStateId,
                resumedQuestStateId = resumedStateId,
                sceneSequence = sceneSequence.ToArray(),
                isolatedSaveClaimed = true
            };

            string json = JsonUtility.ToJson(evidence, true);
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                try
                {
                    string directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(outputPath, json + "\n", new UTF8Encoding(false));
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        MainQuestLineContract.FailedMarker +
                        " evidence write failed: " + exception.GetType().Name);
                    succeeded = false;
                }
            }

            if (!Application.isEditor)
            {
                Application.Quit(succeeded ? 0 : 1);
            }

            yield break;
        }

        [Serializable]
        private sealed class MainQuestLineEvidenceFile
        {
            public int schemaVersion;
            public string status;
            public string reasonCode;
            public bool applicationIsEditor;
            public string unityVersion;
            public string buildGuid;
            public string enabledSceneManifestSha256;
            public string generatedSceneManifestSha256;
            public string narrativeCatalogSha256;
            public string narrativePacketVersion;
            public string entryChapterId;
            public string entryQuestId;
            public string progressedQuestStateId;
            public string resumedQuestStateId;
            public string[] sceneSequence;
            public bool isolatedSaveClaimed;
        }
    }
}
