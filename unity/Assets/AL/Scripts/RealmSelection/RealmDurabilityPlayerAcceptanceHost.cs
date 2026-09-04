using System;
using UnityEngine;

namespace AL.RealmSelection
{
    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class RealmDurabilityPlayerAcceptanceHost : MonoBehaviour
    {
        public const string HostObjectName = "AL Realm Durability Player Acceptance";

        private static RealmDurabilityPlayerAcceptanceHost _instance;
        private bool _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MaybeStart()
        {
            string root;
            string phase;
            string outputPath;
            if (!RealmDurabilityPlayerAcceptance.TryParseCommandLine(
                    Environment.GetCommandLineArgs(),
                    out root,
                    out phase,
                    out outputPath))
            {
                return;
            }

            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(HostObjectName);
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<RealmDurabilityPlayerAcceptanceHost>();
            _instance._root = root;
            _instance._phase = phase;
            _instance._outputPath = outputPath;
        }

        private string _root;
        private string _phase;
        private string _outputPath;

        private void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;
            Application.runInBackground = true;
            RealmDurabilityAcceptanceResult result = RealmDurabilityPlayerAcceptance.Run(_root, _phase);
            RealmDurabilityPlayerAcceptance.WriteOutput(_outputPath, result);
            if (!Application.isEditor)
            {
                Application.Quit(result.Passed ? 0 : 1);
            }
        }
    }
}
