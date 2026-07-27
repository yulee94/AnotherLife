using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    public readonly struct RealmSelectionGridSpec
    {
        public RealmSelectionGridSpec(int columnCount, Vector2 cellSize, Vector2 spacing)
        {
            ColumnCount = columnCount;
            CellSize = cellSize;
            Spacing = spacing;
        }

        public int ColumnCount { get; }
        public Vector2 CellSize { get; }
        public Vector2 Spacing { get; }
    }

    public static class RealmSelectionViewportLayout
    {
        private const float PortraitMaximumCardWidth = 900f;
        private const float PortraitCardHeight = 220f;
        private const float PortraitRowSpacing = 24f;
        private const float LandscapeMaximumCardWidth = 790f;
        private const float LandscapeCardHeight = 148f;
        private const float LandscapeColumnSpacing = 28f;
        private const float LandscapeRowSpacing = 36f;

        public static void NormalizeSafeArea(
            Rect safeArea,
            Vector2 screenSize,
            out Vector2 anchorMin,
            out Vector2 anchorMax)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                anchorMin = Vector2.zero;
                anchorMax = Vector2.one;
                return;
            }

            float xMin = Mathf.Clamp(safeArea.xMin, 0f, screenSize.x);
            float yMin = Mathf.Clamp(safeArea.yMin, 0f, screenSize.y);
            float xMax = Mathf.Clamp(safeArea.xMax, xMin, screenSize.x);
            float yMax = Mathf.Clamp(safeArea.yMax, yMin, screenSize.y);
            anchorMin = new Vector2(xMin / screenSize.x, yMin / screenSize.y);
            anchorMax = new Vector2(xMax / screenSize.x, yMax / screenSize.y);
        }

        public static RealmSelectionGridSpec CalculateGrid(float availableWidth, bool portrait)
        {
            float width = Mathf.Max(0f, availableWidth);
            if (portrait)
            {
                return new RealmSelectionGridSpec(
                    1,
                    new Vector2(Mathf.Min(PortraitMaximumCardWidth, width), PortraitCardHeight),
                    new Vector2(0f, PortraitRowSpacing));
            }

            float cellWidth = Mathf.Max(0f, (width - LandscapeColumnSpacing) * 0.5f);
            return new RealmSelectionGridSpec(
                2,
                new Vector2(Mathf.Min(LandscapeMaximumCardWidth, cellWidth), LandscapeCardHeight),
                new Vector2(LandscapeColumnSpacing, LandscapeRowSpacing));
        }
    }

    internal sealed class RealmSelectionSafeAreaDriver : MonoBehaviour
    {
        private RectTransform _safeAreaRoot;
        private RectTransform _gridRoot;
        private GridLayoutGroup _grid;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        public void Bind(RectTransform safeAreaRoot, RectTransform gridRoot, GridLayoutGroup grid)
        {
            _safeAreaRoot = safeAreaRoot;
            _gridRoot = gridRoot;
            _grid = grid;
            ApplyLayout(force: true);
        }

        private void LateUpdate()
        {
            ApplyLayout(force: false);
        }

        private void ApplyLayout(bool force)
        {
            if (_safeAreaRoot == null || _gridRoot == null || _grid == null)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            if (!force &&
                safeArea == _lastSafeArea &&
                screenWidth == _lastScreenWidth &&
                screenHeight == _lastScreenHeight)
            {
                return;
            }

            _lastSafeArea = safeArea;
            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;

            RealmSelectionViewportLayout.NormalizeSafeArea(
                safeArea,
                new Vector2(screenWidth, screenHeight),
                out Vector2 anchorMin,
                out Vector2 anchorMax);
            _safeAreaRoot.anchorMin = anchorMin;
            _safeAreaRoot.anchorMax = anchorMax;
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;

            Canvas.ForceUpdateCanvases();
            bool portrait = screenHeight >= screenWidth;
            RealmSelectionGridSpec spec = RealmSelectionViewportLayout.CalculateGrid(
                Mathf.Abs(_gridRoot.rect.width),
                portrait);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = spec.ColumnCount;
            _grid.cellSize = spec.CellSize;
            _grid.spacing = spec.Spacing;
        }
    }
}
