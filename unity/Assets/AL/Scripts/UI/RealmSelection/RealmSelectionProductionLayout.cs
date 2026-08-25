using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;
using AL.RealmSelection;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    public sealed class RealmSelectionProductionScreen
    {
        public GameObject CanvasObject;
        public RectTransform SafeArea;
        public RectTransform CardsRoot;
        public GridLayoutGroup Grid;
        public RealmSelectionCommitOverlay Commit;
        public readonly List<Button> RealmButtons = new List<Button>(4);
    }

    public static class RealmSelectionProductionLayout
    {
        public const string CanvasName = "RealmSelectionCanvas";
        public const string ScreenPrefabPath = "Assets/AL/Prefabs/UI/RealmSelection/AL_UI_RealmSelection_Screen.prefab";
        public const string CardPrefabPath = "Assets/AL/Prefabs/UI/RealmSelection/AL_UI_RealmSelection_Card.prefab";

        public static RealmSelectionProductionScreen Build(
            IEnumerable<RealmDefinition> realms,
            Func<RealmId, Sprite> emblemFor,
            Action<RealmId> onConsider,
            Font font,
            bool attachRuntimeLayoutDriver = true)
        {
            var screen = new RealmSelectionProductionScreen();
            var canvasObject = new GameObject(CanvasName);
            screen.CanvasObject = canvasObject;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            PresentationChrome.ApplyCanvasScaler(canvasObject.AddComponent<CanvasScaler>());
            canvasObject.AddComponent<GraphicRaycaster>();

            PresentationChrome.CreatePlate(
                canvasObject.transform,
                "StoneField",
                PresentationChrome.StoneVoid,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);

            BuildAtmosphere(canvasObject.transform);

            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            screen.SafeArea = safeAreaObject.GetComponent<RectTransform>();
            screen.SafeArea.anchorMin = Vector2.zero;
            screen.SafeArea.anchorMax = Vector2.one;
            screen.SafeArea.offsetMin = Vector2.zero;
            screen.SafeArea.offsetMax = Vector2.zero;

            PresentationChrome.CreatePlate(
                screen.SafeArea,
                "TopMetalRail",
                PresentationChrome.MetalEdge,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -96f),
                new Vector2(-160f, 3f));
            PresentationChrome.CreatePlate(
                screen.SafeArea,
                "BottomMetalRail",
                PresentationChrome.MetalDim,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 48f),
                new Vector2(-220f, 2f));

            PresentationChrome.CreateLabel(
                screen.SafeArea,
                "Title",
                font,
                "SWEAR YOUR REALM",
                PresentationChrome.DisplaySize,
                PresentationChrome.Ink,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(-48f, 52f));
            PresentationChrome.CreateLabel(
                screen.SafeArea,
                "Subtitle",
                font,
                RealmSelectionIdentity.LockWarningFallback,
                PresentationChrome.BodySize,
                PresentationChrome.InkMuted,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -70f),
                new Vector2(-80f, 40f));

            var cardsObject = new GameObject("RealmCards", typeof(RectTransform));
            cardsObject.transform.SetParent(screen.SafeArea, false);
            screen.CardsRoot = cardsObject.GetComponent<RectTransform>();
            screen.CardsRoot.anchorMin = Vector2.zero;
            screen.CardsRoot.anchorMax = Vector2.one;
            screen.CardsRoot.offsetMin = new Vector2(36f, 72f);
            screen.CardsRoot.offsetMax = new Vector2(-36f, -132f);
            screen.Grid = cardsObject.AddComponent<GridLayoutGroup>();
            screen.Grid.childAlignment = TextAnchor.MiddleCenter;
            screen.Grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            screen.Grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            screen.Grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            RealmSelectionGridSpec referenceGrid = RealmSelectionViewportLayout.CalculateGrid(
                availableWidth: 1608f,
                portrait: false);
            screen.Grid.constraintCount = referenceGrid.ColumnCount;
            screen.Grid.cellSize = referenceGrid.CellSize;
            screen.Grid.spacing = referenceGrid.Spacing;

            foreach (RealmDefinition realm in realms)
            {
                if (realm == null)
                {
                    continue;
                }

                screen.RealmButtons.Add(
                    BuildCard(screen.CardsRoot, font, realm, emblemFor != null ? emblemFor(realm.Id) : null, onConsider));
            }

            if (attachRuntimeLayoutDriver)
            {
                var driver = canvasObject.AddComponent<RealmSelectionSafeAreaDriver>();
                driver.Bind(screen.SafeArea, screen.CardsRoot, screen.Grid);
            }

            screen.Commit = RealmSelectionCommitOverlay.Create(canvasObject.transform, font);
            return screen;
        }

        public static Button BuildCard(
            Transform parent,
            Font font,
            RealmDefinition realm,
            Sprite emblem,
            Action<RealmId> onConsider)
        {
            RealmIdentityPresentation identity = RealmSelectionIdentity.Resolve(realm, RealmCatalogRuntime.Current);
            var buttonObject = new GameObject(string.IsNullOrEmpty(identity.RealmName) ? realm.Id.ToString() : identity.RealmName);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = PresentationChrome.StoneInset;
            image.raycastTarget = true;
            var button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.05f, 1.02f, 1f);
            colors.pressedColor = new Color(0.86f, 0.86f, 0.84f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            RealmId captured = realm.Id;
            button.onClick.AddListener(() => onConsider?.Invoke(captured));

            RealmSelectionIdentity.BuildStructuralFrame(buttonObject.transform, identity.FrameKind);
            if (emblem != null)
            {
                Image mark = PresentationChrome.CreatePlate(
                    buttonObject.transform,
                    "RealmEmblem",
                    Color.white,
                    new Vector2(0.5f, 0.50f),
                    new Vector2(0.5f, 0.50f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    new Vector2(96f, 96f));
                mark.sprite = emblem;
                mark.preserveAspect = true;
            }

            PresentationChrome.CreateLabel(
                buttonObject.transform,
                identity.RealmName + "_People",
                font,
                identity.PeopleName,
                PresentationChrome.PeopleSize,
                PresentationChrome.InkMuted,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -14f),
                new Vector2(-24f, 22f));
            PresentationChrome.CreateLabel(
                buttonObject.transform,
                identity.RealmName + "_Name",
                font,
                identity.RealmName.ToUpperInvariant(),
                PresentationChrome.TitleSize,
                PresentationChrome.Ink,
                TextAnchor.UpperCenter,
                new Vector2(0f, 1f),
                Vector2.one,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -36f),
                new Vector2(-24f, 32f));
            PresentationChrome.CreateLabel(
                buttonObject.transform,
                identity.RealmName + "_Structure",
                font,
                identity.MarkName + "  ·  " + identity.SilhouetteLanguage,
                PresentationChrome.CaptionSize,
                PresentationChrome.InkFaint,
                TextAnchor.LowerCenter,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 52f),
                new Vector2(-28f, 36f));
            PresentationChrome.CreateLabel(
                buttonObject.transform,
                identity.RealmName + "_Material",
                font,
                identity.MaterialLanguage,
                PresentationChrome.CaptionSize,
                PresentationChrome.MetalDim,
                TextAnchor.LowerCenter,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 32f),
                new Vector2(-28f, 22f));
            PresentationChrome.CreateLabel(
                buttonObject.transform,
                identity.RealmName + "_Consider",
                font,
                "CONSIDER",
                PresentationChrome.CaptionSize,
                PresentationChrome.MetalEdge,
                TextAnchor.LowerCenter,
                new Vector2(0f, 0f),
                Vector2.one,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 10f),
                new Vector2(-28f, 20f));

            return button;
        }

        private static void BuildAtmosphere(Transform parent)
        {
            PresentationChrome.CreatePlate(
                parent,
                "UpperGloom",
                new Color(0.02f, 0.022f, 0.028f, 0.55f),
                new Vector2(0f, 0.62f),
                Vector2.one,
                new Vector2(0.5f, 1f),
                Vector2.zero,
                Vector2.zero);
            PresentationChrome.CreatePlate(
                parent,
                "WarTableFalloff",
                new Color(0.09f, 0.08f, 0.06f, 0.16f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.28f),
                new Vector2(0.5f, 0f),
                Vector2.zero,
                Vector2.zero);
            PresentationChrome.CreatePlate(
                parent,
                "DistantRidge",
                new Color(0.02f, 0.022f, 0.026f, 0.94f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 110f),
                new Vector2(1600f, 86f));
        }
    }
}
