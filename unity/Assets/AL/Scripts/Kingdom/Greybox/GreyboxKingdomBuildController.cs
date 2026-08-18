using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AL.Core;
using AL.Data.Definitions;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AL.Kingdom.Greybox
{
    /// <summary>
    /// Minimal greybox kingdom-build scene for the vertical slice.
    ///
    /// Presented after the combat encounter. It offers one meaningful build action
    /// ("Construct / Upgrade a structure") backed by the hardcoded
    /// <see cref="LocalGameDataService"/> building options and greybox placeholder
    /// primitives. The action spends a fixed slice budget (combat loot) and writes the
    /// visible result into <see cref="GreyboxKingdomRunState"/>, which is the local run
    /// state for this slice and can be saved / reloaded or exited back to the loop.
    ///
    /// This component is intentionally self-contained: it reads structure definitions
    /// straight from <see cref="LocalGameDataService"/> and keeps its own run state,
    /// so it does not depend on the production save/catalog authority stack.
    /// </summary>
    public class GreyboxKingdomBuildController : MonoBehaviour
    {
        private const string SaveFileName = "greybox_runstate.json";

        private const int RowHeight = 56;
        private const int ListWidth = 620;

        // Hardcoded building options from LocalGameDataService (legacy PascalCase IDs).
        private static readonly string[] BuildableStructureIds =
        {
            "TownHall", "Farm", "LumberMill", "Quarry", "GoldMine",
            "Barracks", "Academy", "Market", "Storehouse", "Forge",
            "Stable", "Workshop", "Embassy", "Wall", "Watchtower"
        };

        /// <summary>Raised when the player chooses to leave the build scene (loop exit).</summary>
        public event Action OnLoopExit;

        private LocalGameDataService _gameData;
        private GreyboxKingdomRunState _runState;
        private string _savePath;

        private Canvas _canvas;
        private Text _budgetText;
        private Text _statusText;
        private Transform _listContent;
        private GameObject _structureWorldRoot;
        private Material _structureMaterial;

        public GreyboxKingdomRunState RunState => _runState;

        /// <summary>Ensures an instance exists and shows the build scene.</summary>
        public static GreyboxKingdomBuildController Open()
        {
            GreyboxKingdomBuildController existing = FindObjectOfType<GreyboxKingdomBuildController>();
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                existing.Refresh();
                return existing;
            }

            var host = new GameObject("GreyboxKingdomBuildController");
            return host.AddComponent<GreyboxKingdomBuildController>();
        }

        /// <summary>Show the build scene if hidden, hide it if shown.</summary>
        public static void Toggle()
        {
            GreyboxKingdomBuildController existing = FindObjectOfType<GreyboxKingdomBuildController>();
            if (existing != null)
            {
                bool nextActive = !existing.gameObject.activeSelf;
                existing.gameObject.SetActive(nextActive);
                if (nextActive)
                {
                    existing.Refresh();
                }

                return;
            }

            Open();
        }

        private void Awake()
        {
            _savePath = Path.Combine(Application.persistentDataPath, SaveFileName);
            _gameData = new LocalGameDataService();
            LoadRunState();
            EnsureEnvironment();
            BuildUi();
            Refresh();
        }

        private void LoadRunState()
        {
            _runState = null;
            if (File.Exists(_savePath))
            {
                try
                {
                    string json = File.ReadAllText(_savePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        _runState = JsonUtility.FromJson<GreyboxKingdomRunState>(json);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GreyboxKingdomBuild] Failed to read run state, starting fresh: {ex.Message}");
                }
            }

            if (_runState == null)
            {
                _runState = new GreyboxKingdomRunState();
            }

            if (_runState.Structures == null)
            {
                _runState.Structures = new List<GreyboxStructureState>();
            }

            if (_runState.Budget == null)
            {
                _runState.Budget = new List<GreyboxResourceAmount>();
            }
        }

        private void SaveRunState()
        {
            try
            {
                _runState.Version = GreyboxKingdomRunState.CurrentVersion;
                string json = JsonUtility.ToJson(_runState, prettyPrint: true);
                File.WriteAllText(_savePath, json);
                SetStatus("Run state saved to " + _savePath);
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message);
            }
        }

        /// <summary>Grant the fixed combat-loot slice budget if it has not been granted yet.</summary>
        public void GrantSliceBudgetIfMissing()
        {
            if (_runState.SliceBudgetSeeded)
            {
                return;
            }

            // Fixed "combat loot" budget for the greybox slice. Rare resources are
            // intentionally omitted; structure costs only reference core resources.
            GrantResource(ResourceType.Food, 400);
            GrantResource(ResourceType.Wood, 800);
            GrantResource(ResourceType.Stone, 800);
            GrantResource(ResourceType.Gold, 600);
            GrantResource(ResourceType.ManaStone, 200);
            GrantResource(ResourceType.Ore, 200);
            _runState.SliceBudgetSeeded = true;
            SaveRunState();
            SetStatus("Combat loot granted: fixed slice budget is now available for building.");
        }

        /// <summary>Add resources to the run-state budget (e.g. actual combat rewards).</summary>
        public void GrantResource(ResourceType resourceType, long amount)
        {
            long current = _runState.GetBudget(resourceType);
            _runState.SetBudget(resourceType, current + Math.Max(0L, amount));
        }

        /// <summary>Perform the single meaningful build action: construct/upgrade one structure.</summary>
        public void BuildStructure(string buildingId)
        {
            BuildingDefinition definition = _gameData.GetBuilding(buildingId);
            if (definition == null)
            {
                SetStatus($"Unknown structure: {buildingId}");
                return;
            }

            GreyboxStructureState structure = _runState.FindStructure(buildingId);
            int currentLevel = structure?.Level ?? 0;
            if (currentLevel >= definition.MaxLevel)
            {
                SetStatus($"{definition.DisplayName} is already at max level ({definition.MaxLevel}).");
                return;
            }

            BuildingConstructionLevelDefinition nextLevel = GetLevelDefinition(definition, currentLevel + 1);
            if (nextLevel == null || nextLevel.Costs == null || nextLevel.Costs.Count == 0)
            {
                SetStatus($"{definition.DisplayName} has no valid next construction level.");
                return;
            }

            // Validate affordability up front so a partial spend can never occur.
            foreach (BuildingConstructionCostDefinition cost in nextLevel.Costs)
            {
                if (cost == null || cost.Amount <= 0)
                {
                    continue;
                }

                if (_runState.GetBudget(cost.ResourceType) < cost.Amount)
                {
                    SetStatus(
                        $"Not enough {cost.ResourceType} to build {definition.DisplayName} " +
                        $"(need {cost.Amount}, have {_runState.GetBudget(cost.ResourceType)}).");
                    return;
                }
            }

            // Spend the budget and advance the structure level.
            foreach (BuildingConstructionCostDefinition cost in nextLevel.Costs)
            {
                if (cost == null || cost.Amount <= 0)
                {
                    continue;
                }

                _runState.SetBudget(
                    cost.ResourceType,
                    _runState.GetBudget(cost.ResourceType) - cost.Amount);
            }

            if (structure == null)
            {
                structure = new GreyboxStructureState { BuildingId = buildingId, Level = 0 };
                _runState.Structures.Add(structure);
            }

            structure.Level = currentLevel + 1;
            _runState.BuildActionCount++;
            SaveRunState();

            SetStatus(
                $"{definition.DisplayName} built to level {structure.Level}. " +
                $"({_runState.BuildActionCount} build action(s) this run)");
            Refresh();
        }

        private static BuildingConstructionLevelDefinition GetLevelDefinition(
            BuildingDefinition definition,
            int targetLevel)
        {
            List<BuildingConstructionLevelDefinition> levels = definition?.ConstructionLevels;
            if (levels == null)
            {
                return null;
            }

            for (int i = 0; i < levels.Count; i++)
            {
                BuildingConstructionLevelDefinition level = levels[i];
                if (level != null && level.TargetLevel == targetLevel)
                {
                    return level;
                }
            }

            return null;
        }

        private void EnsureEnvironment()
        {
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("GreyboxMainCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.045f, 0.052f, 0.062f);
                camera.transform.position = new Vector3(0f, 9f, -11f);
                camera.transform.LookAt(Vector3.zero);
                cameraObject.tag = "MainCamera";
            }

            if (FindObjectOfType<Light>() == null)
            {
                var lightObject = new GameObject("GreyboxDirectionalLight");
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        private void BuildUi()
        {
            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            var canvasObject = new GameObject("GreyboxKingdomBuildCanvas", typeof(RectTransform));
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.transform.SetParent(transform, false);

            // Left command panel.
            CreatePanel(_canvas.transform, "Backdrop", new Vector2(0f, 0f), new Vector2(680f, 1080f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Color(0.006f, 0.010f, 0.016f, 0.82f));

            Text title = CreateText(_canvas.transform, "Title", new Vector2(24f, -20f), new Vector2(620f, 40f), 26, new Color(1f, 0.88f, 0.62f));
            title.text = "KINGDOM BUILD // GREYBOX SLICE";

            Text realm = CreateText(_canvas.transform, "Realm", new Vector2(24f, -66f), new Vector2(620f, 26f), 15, new Color(0.72f, 0.82f, 0.92f));
            realm.text = "Realm: " + _runState.Realm + "  |  Build action: construct / upgrade a structure";

            _budgetText = CreateText(_canvas.transform, "Budget", new Vector2(24f, -100f), new Vector2(620f, 150f), 15, new Color(0.86f, 0.91f, 0.96f));
            _budgetText.text = "TREASURY (combat loot)";

            // Scrollable structure list viewport backdrop.
            CreatePanel(_canvas.transform, "ListViewportBackdrop", new Vector2(24f, -258f), new Vector2(ListWidth + 16f, 540f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Color(0.02f, 0.04f, 0.06f, 0.6f));

            ScrollRect scroll = BuildScrollList(_canvas.transform, "StructureList", new Vector2(32f, -266f), new Vector2(ListWidth, 532f));
            _listContent = scroll.content;

            // Footer actions.
            CreateButton(_canvas.transform, "SAVE & CONTINUE", new Vector2(24f, -826f), new Vector2(190f, 48f), () =>
            {
                SaveRunState();
                SetStatus("Progress saved. You can reload from the main path or keep building.");
            });
            CreateButton(_canvas.transform, "RELOAD", new Vector2(238f, -826f), new Vector2(190f, 48f), () =>
            {
                LoadRunState();
                GrantSliceBudgetIfMissing();
                Refresh();
                SetStatus("Run state reloaded from disk.");
            });
            CreateButton(_canvas.transform, "EXIT LOOP", new Vector2(452f, -826f), new Vector2(190f, 48f), ExitLoop);

            _statusText = CreateText(_canvas.transform, "Status", new Vector2(24f, -888f), new Vector2(620f, 150f), 16, Color.white);
            _statusText.text = "Choose a structure to construct or upgrade.";
        }

        private ScrollRect BuildScrollList(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var scrollObject = new GameObject(name, typeof(RectTransform));
            scrollObject.transform.SetParent(parent, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 1f);
            scrollRect.anchorMax = new Vector2(0f, 1f);
            scrollRect.pivot = new Vector2(0f, 1f);
            scrollRect.anchoredPosition = anchoredPosition;
            scrollRect.sizeDelta = size;

            var viewportObject = new GameObject("Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewportObject.AddComponent<RectMask2D>();

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, BuildableStructureIds.Length * RowHeight);

            ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return scroll;
        }

        private void Refresh()
        {
            GrantSliceBudgetIfMissing();
            RefreshBudget();
            RefreshList();
            RefreshPlaceholders();
        }

        private void RefreshBudget()
        {
            if (_budgetText == null)
            {
                return;
            }

            var lines = new List<string> { "TREASURY (combat loot)" };
            ResourceType[] wallet =
            {
                ResourceType.Food, ResourceType.Wood, ResourceType.Stone,
                ResourceType.Gold, ResourceType.ManaStone, ResourceType.Ore
            };
            foreach (ResourceType type in wallet)
            {
                lines.Add($"{type,-9} {_runState.GetBudget(type),8}");
            }

            _budgetText.text = string.Join("\n", lines);
        }

        private void RefreshList()
        {
            if (_listContent == null)
            {
                return;
            }

            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                Destroy(_listContent.GetChild(i).gameObject);
            }

            for (int index = 0; index < BuildableStructureIds.Length; index++)
            {
                CreateStructureRow(_listContent, BuildableStructureIds[index], index);
            }
        }

        private void CreateStructureRow(Transform parent, string buildingId, int index)
        {
            BuildingDefinition definition = _gameData.GetBuilding(buildingId);
            if (definition == null)
            {
                return;
            }

            GreyboxStructureState structure = _runState.FindStructure(buildingId);
            int level = structure?.Level ?? 0;
            BuildingConstructionLevelDefinition next = GetLevelDefinition(definition, level + 1);
            string costText = level >= definition.MaxLevel
                ? "MAX LEVEL"
                : FormatCost(next);

            var row = new GameObject("Row_" + buildingId, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRect.sizeDelta = new Vector2(0f, RowHeight - 4f);

            Text name = CreateText(row.transform, "Name", new Vector2(8f, -6f), new Vector2(160f, RowHeight - 8f), 15, new Color(0.92f, 0.88f, 0.78f));
            name.text = $"{definition.DisplayName}  Lv{level}";
            name.alignment = TextAnchor.MiddleLeft;

            Text cost = CreateText(row.transform, "Cost", new Vector2(176f, -6f), new Vector2(280f, RowHeight - 8f), 13, new Color(0.62f, 0.72f, 0.82f));
            cost.text = costText;
            cost.alignment = TextAnchor.MiddleLeft;

            string buildingIdClosure = buildingId;
            CreateButton(row.transform, "BUILD", new Vector2(464f, -6f), new Vector2(120f, 40f), () => BuildStructure(buildingIdClosure));
        }

        private static string FormatCost(BuildingConstructionLevelDefinition level)
        {
            if (level == null || level.Costs == null || level.Costs.Count == 0)
            {
                return "—";
            }

            return string.Join(
                "  ",
                level.Costs
                    .Where(cost => cost != null && cost.Amount > 0)
                    .Select(cost => $"{cost.Amount} {cost.ResourceType}"));
        }

        private void RefreshPlaceholders()
        {
            if (_structureWorldRoot == null)
            {
                _structureWorldRoot = new GameObject("GreyboxKingdomStructures");
            }

            for (int i = _structureWorldRoot.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(_structureWorldRoot.transform.GetChild(i).gameObject);
            }

            if (_structureMaterial == null)
            {
                Shader shader = Shader.Find("Standard");
                _structureMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"));
                _structureMaterial.color = new Color(0.22f, 0.30f, 0.40f);
            }

            int index = 0;
            foreach (string buildingId in BuildableStructureIds)
            {
                GreyboxStructureState structure = _runState.FindStructure(buildingId);
                int level = structure?.Level ?? 0;
                if (level <= 0)
                {
                    continue;
                }

                int row = index / 5;
                int column = index % 5;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Structure_" + buildingId + "_Lv" + level;
                cube.transform.SetParent(_structureWorldRoot.transform, false);
                cube.transform.position = new Vector3(column * 2.2f - 4.4f, 0.5f + level * 0.4f, row * 2.2f + 1f);
                cube.transform.localScale = new Vector3(1.6f, 1f + level * 0.8f, 1.6f);
                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = _structureMaterial;
                }

                index++;
            }
        }

        private void ExitLoop()
        {
            SaveRunState();
            string summary = SummarizeRunState();
            SetStatus("Loop exit requested. Run state summary:\n" + summary);
            if (OnLoopExit != null)
            {
                OnLoopExit.Invoke();
            }

            if (Application.isEditor)
            {
                Debug.Log("[GreyboxKingdomBuild] Loop exit (editor): " + summary);
            }
            else
            {
                Application.Quit();
            }
        }

        private string SummarizeRunState()
        {
            string structures = _runState.Structures.Count == 0
                ? "none"
                : string.Join(", ", _runState.Structures
                    .Where(s => s != null)
                    .Select(s => $"{s.BuildingId}={s.Level}"));
            return $"realm={_runState.Realm}, buildActions={_runState.BuildActionCount}, structures=[{structures}]";
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
            }

            Debug.Log("[GreyboxKingdomBuild] " + message);
        }

        private void CreateButton(Transform parent, string label, Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.32f, 0.92f);
            Button button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(action);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Text labelText = CreateText(buttonObject.transform, label + "_Text", Vector2.zero, size, 18, Color.white);
            labelText.text = label;
            labelText.alignment = TextAnchor.MiddleCenter;
            RectTransform labelRect = labelText.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return text;
        }

        private Image CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform));
            panelObject.transform.SetParent(parent, false);
            Image image = panelObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }
    }
}
