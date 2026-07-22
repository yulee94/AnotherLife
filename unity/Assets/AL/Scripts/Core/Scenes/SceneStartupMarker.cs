using System.Collections.Generic;
using UnityEngine;

namespace AL.Core.Scenes
{
    /// <summary>
    /// Narrow, non-mutating production scene startup marker (spec section 9, #223 approved seam D4).
    ///
    /// On Start it validates the live scene path/name against its serialized identity and the shared
    /// <see cref="ProductionSceneDescriptor"/>, then emits exactly one stable technical log:
    ///   [AL-SCENE-ACTIVE] id=&lt;sceneId&gt; name=&lt;sceneName&gt; path=&lt;assetPath&gt; role=&lt;role&gt; version=&lt;sourceVersion&gt;
    /// A mismatch logs an Error (distinct prefix) so external launch validation cannot be fooled into a
    /// false pass. It performs no save/service/gameplay/navigation/UI mutation, uses no DontDestroyOnLoad,
    /// and carries no player-facing copy. Log-subscriber failure cannot change scene behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneStartupMarker : MonoBehaviour
    {
        [SerializeField] private string _sceneId;
        [SerializeField] private string _expectedAssetPath;
        [SerializeField] private string _role;
        [SerializeField] private string _sourceVersion;

        private bool _hasEmitted;

        public string SceneId => _sceneId;
        public string ExpectedAssetPath => _expectedAssetPath;
        public string Role => _role;
        public string SourceVersion => _sourceVersion;

        private void Start()
        {
            Emit(gameObject.scene.path, gameObject.scene.name);
        }

        /// <summary>
        /// Emits the marker log exactly once. Subsequent calls are no-ops. Returns the typed report so a
        /// test can drive activation with a controlled path/name without a fully imported scene.
        /// </summary>
        internal SceneStartupMarkerReport Emit(string activeScenePath, string activeSceneName)
        {
            if (_hasEmitted)
            {
                return SceneStartupMarkerReport.AlreadyEmitted();
            }

            _hasEmitted = true;

            SceneStartupMarkerReport report = Evaluate(activeScenePath, activeSceneName);
            if (report.IsValid)
            {
                Debug.Log(report.Message);
            }
            else
            {
                Debug.LogError(report.Message);
            }

            return report;
        }

        /// <summary>
        /// Pure validation: no logging, no state change, safe to call repeatedly. Compares the serialized
        /// marker identity and the live path/name against the descriptor record for this scene id.
        /// </summary>
        internal SceneStartupMarkerReport Evaluate(string activeScenePath, string activeSceneName)
        {
            var failures = new List<string>();

            if (!ProductionSceneDescriptor.TryGetById(_sceneId, out ProductionSceneRecord record))
            {
                failures.Add($"scene id '{_sceneId}' is not present in the production scene descriptor");
            }
            else
            {
                if (!record.IsProductionScene)
                {
                    failures.Add($"scene id '{_sceneId}' is not a production scene and must not carry a startup marker");
                }

                if (!string.Equals(record.AssetPath, _expectedAssetPath, System.StringComparison.Ordinal))
                {
                    failures.Add($"expectedAssetPath '{_expectedAssetPath}' does not match descriptor path '{record.AssetPath}'");
                }

                if (!string.Equals(record.Role, _role, System.StringComparison.Ordinal))
                {
                    failures.Add($"role '{_role}' does not match descriptor role '{record.Role}'");
                }

                if (!string.Equals(record.SceneName, activeSceneName, System.StringComparison.Ordinal))
                {
                    failures.Add($"active scene name '{activeSceneName}' does not match descriptor name '{record.SceneName}'");
                }
            }

            if (!string.Equals(_expectedAssetPath, activeScenePath, System.StringComparison.Ordinal))
            {
                failures.Add($"active scene path '{activeScenePath}' does not match expectedAssetPath '{_expectedAssetPath}'");
            }

            if (!string.Equals(_sourceVersion, ProductionSceneDescriptor.SourceVersion, System.StringComparison.Ordinal))
            {
                failures.Add($"sourceVersion '{_sourceVersion}' does not match descriptor version '{ProductionSceneDescriptor.SourceVersion}'");
            }

            string activationLog =
                $"[AL-SCENE-ACTIVE] id={_sceneId} name={activeSceneName} path={activeScenePath} role={_role} version={_sourceVersion}";

            if (failures.Count == 0)
            {
                return SceneStartupMarkerReport.Valid(activationLog);
            }

            string mismatchLog =
                $"[AL-SCENE-ACTIVE-MISMATCH] id={_sceneId} path={activeScenePath} name={activeSceneName}: {string.Join("; ", failures)}";
            return SceneStartupMarkerReport.Invalid(mismatchLog, failures);
        }
    }

    /// <summary>Typed result of a startup marker evaluation/emission.</summary>
    internal sealed class SceneStartupMarkerReport
    {
        private static readonly IReadOnlyList<string> NoFailures = new string[0];

        private SceneStartupMarkerReport(bool isValid, bool emitted, string message, IReadOnlyList<string> failures)
        {
            IsValid = isValid;
            Emitted = emitted;
            Message = message ?? string.Empty;
            Failures = failures ?? NoFailures;
        }

        public bool IsValid { get; }
        public bool Emitted { get; }
        public string Message { get; }
        public IReadOnlyList<string> Failures { get; }

        public static SceneStartupMarkerReport Valid(string message)
        {
            return new SceneStartupMarkerReport(true, true, message, NoFailures);
        }

        public static SceneStartupMarkerReport Invalid(string message, IReadOnlyList<string> failures)
        {
            return new SceneStartupMarkerReport(false, true, message, failures);
        }

        public static SceneStartupMarkerReport AlreadyEmitted()
        {
            return new SceneStartupMarkerReport(true, false, string.Empty, NoFailures);
        }
    }
}
