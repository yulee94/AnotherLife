using System;
using AL.Core;
using AL.Data.Definitions;
using AL.RealmSelection;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    public enum RealmStructuralFrameKind
    {
        None = 0,
        OrthogonalPlate = 1,
        LivingOrbit = 2,
        CelestialMeridian = 3,
        SeveredEclipse = 4
    }

    public readonly struct RealmIdentityPresentation
    {
        public RealmIdentityPresentation(
            RealmId runtimeId,
            string catalogId,
            string realmName,
            string peopleName,
            string markName,
            string silhouetteLanguage,
            string materialLanguage,
            RealmStructuralFrameKind frameKind)
        {
            RuntimeId = runtimeId;
            CatalogId = catalogId ?? string.Empty;
            RealmName = realmName ?? string.Empty;
            PeopleName = peopleName ?? string.Empty;
            MarkName = markName ?? string.Empty;
            SilhouetteLanguage = silhouetteLanguage ?? string.Empty;
            MaterialLanguage = materialLanguage ?? string.Empty;
            FrameKind = frameKind;
        }

        public RealmId RuntimeId { get; }
        public string CatalogId { get; }
        public string RealmName { get; }
        public string PeopleName { get; }
        public string MarkName { get; }
        public string SilhouetteLanguage { get; }
        public string MaterialLanguage { get; }
        public RealmStructuralFrameKind FrameKind { get; }

        public string GreyscaleKey =>
            CatalogId + "|" + MarkName + "|" + SilhouetteLanguage + "|" + FrameKind;

        public bool HasStructuralIdentity =>
            PeopleName.Length > 0 &&
            MarkName.Length > 0 &&
            SilhouetteLanguage.Length > 0 &&
            MaterialLanguage.Length > 0 &&
            FrameKind != RealmStructuralFrameKind.None;
    }

    public static class RealmSelectionIdentity
    {
        public const string LockWarningFallback =
            "This account will be bound to the chosen realm. Future characters on this account must belong to the same realm.";

        public static RealmIdentityPresentation Resolve(
            RealmDefinition definition,
            RealmCatalogSnapshot catalog)
        {
            RealmId runtimeId = definition != null ? definition.Id : RealmId.None;
            RealmCatalogEntry entry = null;
            if (catalog != null)
            {
                catalog.TryGet(runtimeId, out entry);
            }

            string realmName = !string.IsNullOrWhiteSpace(entry?.DisplayName)
                ? entry.DisplayName
                : definition != null ? definition.RealmName : string.Empty;
            string peopleName = !string.IsNullOrWhiteSpace(entry?.PeopleName)
                ? entry.PeopleName
                : realmName;
            return new RealmIdentityPresentation(
                runtimeId,
                entry != null ? entry.Id : string.Empty,
                realmName,
                peopleName,
                entry != null ? entry.MarkName : string.Empty,
                entry != null ? entry.SilhouetteLanguage : string.Empty,
                entry != null ? entry.MaterialLanguage : string.Empty,
                FrameKindFor(runtimeId));
        }

        public static RealmStructuralFrameKind FrameKindFor(RealmId id)
        {
            switch (id)
            {
                case RealmId.Stonehold:
                    return RealmStructuralFrameKind.OrthogonalPlate;
                case RealmId.Eldergrove:
                    return RealmStructuralFrameKind.LivingOrbit;
                case RealmId.Crownlands:
                    return RealmStructuralFrameKind.CelestialMeridian;
                case RealmId.Umbral:
                    return RealmStructuralFrameKind.SeveredEclipse;
                default:
                    return RealmStructuralFrameKind.None;
            }
        }

        public static Font ResolvePresentationFont(int size = 22)
        {
            return AL.UI.Presentation.PresentationChrome.ResolveFont(size);
        }

        public static void BuildStructuralFrame(Transform parent, RealmStructuralFrameKind kind)
        {
            Color plate = new Color(0.10f, 0.11f, 0.12f, 0.96f);
            Color edge = new Color(0.78f, 0.76f, 0.70f, 0.42f);
            Color voidColor = new Color(0.04f, 0.04f, 0.045f, 0.94f);

            switch (kind)
            {
                case RealmStructuralFrameKind.OrthogonalPlate:
                    CreatePlate(parent, "Frame_Outer", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    CreatePlate(parent, "Frame_TopBar", edge, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(-18f, 6f));
                    CreatePlate(parent, "Frame_BottomBar", edge, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(-18f, 6f));
                    CreatePlate(parent, "Frame_LeftBar", edge, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, -18f));
                    CreatePlate(parent, "Frame_RightBar", edge, new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(6f, -18f));
                    CreateAnchored(parent, "Frame_CornerNW", edge, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(22f, 22f));
                    CreateAnchored(parent, "Frame_CornerNE", edge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(22f, 22f));
                    CreateAnchored(parent, "Frame_CornerSW", edge, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(22f, 22f));
                    CreateAnchored(parent, "Frame_CornerSE", edge, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-18f, 18f), new Vector2(22f, 22f));
                    CreateAnchored(parent, "Frame_AxisH", edge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 4f));
                    CreateAnchored(parent, "Frame_AxisV", edge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 72f));
                    break;

                case RealmStructuralFrameKind.LivingOrbit:
                    CreatePlate(parent, "Frame_Outer", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    CreateAnchored(parent, "Frame_Orbit", edge, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(128f, 128f));
                    CreateAnchored(parent, "Frame_Seed", voidColor, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(36f, 48f));
                    CreateAnchored(parent, "Frame_ArcA", edge, new Vector2(0.28f, 0.86f), new Vector2(0.28f, 0.86f), Vector2.zero, new Vector2(34f, 14f));
                    CreateAnchored(parent, "Frame_ArcB", edge, new Vector2(0.72f, 0.86f), new Vector2(0.72f, 0.86f), Vector2.zero, new Vector2(34f, 14f));
                    CreateAnchored(parent, "Frame_ArcC", edge, new Vector2(0.50f, 0.54f), new Vector2(0.50f, 0.54f), Vector2.zero, new Vector2(54f, 12f));
                    break;

                case RealmStructuralFrameKind.CelestialMeridian:
                    CreatePlate(parent, "Frame_Outer", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    CreateAnchored(parent, "Frame_MeridianV", edge, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(6f, 150f));
                    CreateAnchored(parent, "Frame_MeridianH", edge, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(150f, 6f));
                    CreateAnchored(parent, "Frame_Diamond", edge, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(28f, 28f));
                    CreateAnchored(parent, "Frame_OrbitL", edge, new Vector2(0.22f, 0.72f), new Vector2(0.22f, 0.72f), Vector2.zero, new Vector2(10f, 64f));
                    CreateAnchored(parent, "Frame_OrbitR", edge, new Vector2(0.78f, 0.72f), new Vector2(0.78f, 0.72f), Vector2.zero, new Vector2(10f, 64f));
                    break;

                case RealmStructuralFrameKind.SeveredEclipse:
                    CreatePlate(parent, "Frame_Outer", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    CreateAnchored(parent, "Frame_Eclipse", edge, new Vector2(0.46f, 0.72f), new Vector2(0.46f, 0.72f), Vector2.zero, new Vector2(120f, 120f));
                    CreateAnchored(parent, "Frame_OffsetVoid", voidColor, new Vector2(0.62f, 0.72f), new Vector2(0.62f, 0.72f), Vector2.zero, new Vector2(78f, 78f));
                    CreateAnchored(parent, "Frame_Diagonal", edge, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f), Vector2.zero, new Vector2(148f, 10f))
                        .rectTransform.localRotation = Quaternion.Euler(0f, 0f, -38f);
                    CreateAnchored(parent, "Frame_BrokenArcA", edge, new Vector2(0.22f, 0.58f), new Vector2(0.22f, 0.58f), Vector2.zero, new Vector2(36f, 10f));
                    CreateAnchored(parent, "Frame_BrokenArcB", edge, new Vector2(0.78f, 0.86f), new Vector2(0.78f, 0.86f), Vector2.zero, new Vector2(36f, 10f));
                    break;

                default:
                    CreatePlate(parent, "Frame_Outer", plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    break;
            }
        }

        private static Image CreatePlate(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 sizeDelta)
        {
            var image = CreateImage(parent, name, color);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = sizeDelta.x < 0f || sizeDelta.y < 0f ? sizeDelta : Vector2.zero;
            if (anchorMin == anchorMax)
            {
                rect.sizeDelta = sizeDelta;
            }
            else if (sizeDelta != Vector2.zero && offsetMin == Vector2.zero)
            {
                rect.sizeDelta = sizeDelta;
            }

            return image;
        }

        private static Image CreateAnchored(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var image = CreateImage(parent, name, color);
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return image;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
