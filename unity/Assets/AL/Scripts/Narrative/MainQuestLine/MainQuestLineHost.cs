using System;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.Narrative.MainQuestLine
{
    [DefaultExecutionOrder(-5000)]
    [DisallowMultipleComponent]
    public sealed class MainQuestLineHost : MonoBehaviour
    {
        public const string HostObjectName = "AL Main Quest Line Host";
        public const string OverlayName = "AL-NARRATIVE-OVERLAY";

        private Text _statusText;
        private MainQuestLineCatalog _catalog;
        private MainQuestLineDiagnostic _diagnostic;
        private MainQuestLineProgress _progress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (MainQuestLineSmokeRunner.IsRequested(Environment.GetCommandLineArgs()))
            {
                MainQuestLineSmokeRunner.EnsureRunning();
                return;
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(sceneName, "Boot", StringComparison.Ordinal) ||
                string.Equals(sceneName, "RealmSelection", StringComparison.Ordinal) ||
                string.Equals(sceneName, "CharacterCreation", StringComparison.Ordinal) ||
                string.Equals(sceneName, "ChampionArena", StringComparison.Ordinal) ||
                string.Equals(sceneName, "Kingdom", StringComparison.Ordinal))
            {
                AttachIfNeeded();
            }
        }

        public static MainQuestLineHost AttachIfNeeded()
        {
            MainQuestLineHost existing = FindObjectOfType<MainQuestLineHost>();
            if (existing != null)
            {
                existing.Refresh();
                return existing;
            }

            var host = new GameObject(HostObjectName);
            DontDestroyOnLoad(host);
            return host.AddComponent<MainQuestLineHost>();
        }

        public MainQuestLineCatalog Catalog => _catalog;
        public MainQuestLineDiagnostic Diagnostic => _diagnostic;
        public MainQuestLineProgress Progress => _progress;
        public string OverlayText => _statusText != null ? _statusText.text : string.Empty;

        private void Awake()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (!MainQuestLineCatalogLoader.TryLoadCanonical(out _catalog, out _diagnostic))
            {
                RenderOverlay(true);
                Debug.LogError(
                    MainQuestLineContract.MissingMarker + " " +
                    (_diagnostic != null ? _diagnostic.ToString() : "catalog missing"));
                return;
            }

            Nvs01ProgressData nvs01 = null;
            FirstWorldProgressData firstWorld = null;
            ISaveGameService save;
            if (ServiceLocator.TryGet(out save) && save != null && save.CurrentSave != null)
            {
                nvs01 = save.CurrentSave.Nvs01Progress;
                firstWorld = save.CurrentSave.FirstWorldProgress;
            }

            if (!MainQuestLineResolver.TryResolve(_catalog, nvs01, firstWorld, out _progress, out _diagnostic))
            {
                RenderOverlay(true);
                Debug.LogError(
                    MainQuestLineContract.FailedMarker + " " +
                    (_diagnostic != null ? _diagnostic.ToString() : "resolve failed"));
                return;
            }

            Debug.Log(
                MainQuestLineContract.ActiveMarker +
                " catalog=" + _catalog.CanonicalSha256 +
                " packet=" + _catalog.PacketVersion +
                " chapter=" + _progress.ChapterId +
                " quest=" + _progress.QuestId +
                " scene=" + SceneManager.GetActiveScene().name);
            RenderOverlay(false);
        }

        public void ShowMissingForTests(MainQuestLineDiagnostic diagnostic)
        {
            _catalog = null;
            _progress = null;
            _diagnostic = diagnostic ?? new MainQuestLineDiagnostic(
                MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING",
                "Test missing catalog.",
                MainQuestLineContract.RelativePath,
                "missing");
            RenderOverlay(true);
        }

        private void RenderOverlay(bool failed)
        {
            EnsureOverlay();
            if (_statusText == null)
            {
                return;
            }

            if (failed)
            {
                string code = _diagnostic != null ? _diagnostic.Code : MainQuestLineContract.DiagnosticPrefix + "CATALOG-MISSING";
                _statusText.color = new Color(0.95f, 0.2f, 0.2f, 1f);
                _statusText.text = "NARRATIVE UNAVAILABLE\n" + code;
                return;
            }

            _statusText.color = Color.white;
            string title = _progress != null ? _progress.Chapter.TitleText : string.Empty;
            string quest = _progress != null ? _progress.QuestId : string.Empty;
            string state = _progress != null ? _progress.QuestStateId : string.Empty;
            _statusText.text = "MAIN QUEST\n" + title + "\n" + quest + " / " + state;
        }

        private void EnsureOverlay()
        {
            if (_statusText != null)
            {
                return;
            }

            var canvasObject = new GameObject(OverlayName);
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var textObject = new GameObject("Status");
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(720f, 160f);
            _statusText = textObject.AddComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_statusText.font == null)
            {
                _statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            _statusText.fontSize = 22;
            _statusText.alignment = TextAnchor.UpperLeft;
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
