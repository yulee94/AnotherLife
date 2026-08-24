using System;
using System.Collections.Generic;
using System.IO;
using AL.Core;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.RealmSelection;
using AL.UI.Presentation;
using AL.UI.RealmSelection;
using UnityEngine;
using UnityEngine.UI;

namespace AL.UI.CharacterCreation
{
    public sealed class CharacterCreationProductionScreen
    {
        public GameObject CanvasObject;
        public Text People;
        public Text Heraldry;
        public Text Look;
        public Text Status;
        public Image StatusPlate;
        public InputField Username;
        public Button Confirm;
        public Text ConfirmLabel;
        public Image Emblem;
        public readonly Dictionary<ClassFamily, Image> ClassCards = new Dictionary<ClassFamily, Image>();
    }

    /// <summary>
    /// Production creator chrome. Same stone/metal/type tokens as realm-select.
    /// Class, look, and username sit on one surface; people is copy, not a picker.
    /// </summary>
    public static class CharacterCreationProductionLayout
    {
        public const string CanvasName = "CharacterCreationCanvas";
        public const string ValidationBannerName = "ValidationBanner";
        public const string ConfirmName = "ConfirmChampion";
        public const string UsernameName = "Username";

        public static CharacterCreationProductionScreen BuildError(string error, Font font)
        {
            CharacterCreationProductionScreen screen = BuildShell(
                font,
                RealmId.None,
                "CHARACTER CREATION",
                string.Empty,
                string.Empty,
                null);
            PresentValidation(screen, error);
            return screen;
        }

        public static CharacterCreationProductionScreen Build(
            CharacterCreationDraft draft,
            Font font,
            Action<ClassFamily> onClass,
            Action onBodyBase,
            Action onArmorTint,
            Action onBodyTint,
            Action onHairStyle,
            Action onHairColor,
            Action onBodyPreset,
            Action onHelmet,
            Action onCape,
            Action onConfirm)
        {
            CharacterCreationLook.TryRealmLabel(draft.Realm, out string realmLabel);
            CharacterCreationLook.TryPeopleLabel(draft.Realm, out string peopleLabel);
            string peopleCopy = realmLabel + "  ·  " + peopleLabel + "  —  people are locked to this realm.";
            string heraldryCopy = ResolveHeraldryCopy(draft.Realm);

            CharacterCreationProductionScreen screen = BuildShell(
                font,
                draft.Realm,
                "SHAPE YOUR CHAMPION",
                peopleCopy,
                heraldryCopy,
                TryLoadEmblem(draft.Realm));

            PresentationChrome.CreateLabel(
                screen.CanvasObject.transform,
                "ClassHeading",
                font,
                "CLASS PATH",
                PresentationChrome.CaptionSize,
                PresentationChrome.MetalEdge,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -168f),
                new Vector2(400f, 20f));

            float classX = 48f;
            foreach (ClassFamily family in draft.AvailableFamilies)
            {
                CharacterCreationLook.TryClassLabel(family, out string label);
                Button button = CreateAction(
                    screen.CanvasObject.transform,
                    font,
                    "Class_" + family,
                    label.ToUpperInvariant(),
                    new Vector2(classX, -196f),
                    new Vector2(176f, PresentationChrome.MinHit),
                    () => onClass?.Invoke(family));
                screen.ClassCards[family] = button.GetComponent<Image>();
                classX += 188f;
            }

            PresentationChrome.CreateLabel(
                screen.CanvasObject.transform,
                "LookHeading",
                font,
                "APPEARANCE",
                PresentationChrome.CaptionSize,
                PresentationChrome.MetalEdge,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -268f),
                new Vector2(400f, 20f));

            CreateAction(screen.CanvasObject.transform, font, "BodyBase", "BODY BASE",
                new Vector2(48f, -296f), new Vector2(176f, PresentationChrome.MinHit), onBodyBase);
            CreateAction(screen.CanvasObject.transform, font, "ArmorTint", "ARMOR TINT",
                new Vector2(236f, -296f), new Vector2(176f, PresentationChrome.MinHit), onArmorTint);
            CreateAction(screen.CanvasObject.transform, font, "BodyTint", "SKIN TONE",
                new Vector2(424f, -296f), new Vector2(176f, PresentationChrome.MinHit), onBodyTint);
            CreateAction(screen.CanvasObject.transform, font, "HairStyle", "HAIR",
                new Vector2(48f, -364f), new Vector2(176f, PresentationChrome.MinHit), onHairStyle);
            CreateAction(screen.CanvasObject.transform, font, "HairColor", "HAIR COLOR",
                new Vector2(236f, -364f), new Vector2(176f, PresentationChrome.MinHit), onHairColor);
            CreateAction(screen.CanvasObject.transform, font, "BodyPreset", "BUILD",
                new Vector2(424f, -364f), new Vector2(176f, PresentationChrome.MinHit), onBodyPreset);
            CreateAction(screen.CanvasObject.transform, font, "Helmet", "HELMET",
                new Vector2(48f, -432f), new Vector2(176f, PresentationChrome.MinHit), onHelmet);
            CreateAction(screen.CanvasObject.transform, font, "Cape", "CAPE",
                new Vector2(236f, -432f), new Vector2(176f, PresentationChrome.MinHit), onCape);

            screen.Look = PresentationChrome.CreateLabel(
                screen.CanvasObject.transform,
                "LookSummary",
                font,
                string.Empty,
                PresentationChrome.BodySize,
                PresentationChrome.InkMuted,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -504f),
                new Vector2(780f, 48f));

            PresentationChrome.CreateLabel(
                screen.CanvasObject.transform,
                "NameHeading",
                font,
                "USERNAME",
                PresentationChrome.CaptionSize,
                PresentationChrome.MetalEdge,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -560f),
                new Vector2(400f, 20f));

            screen.Username = CreateUsernameField(
                screen.CanvasObject.transform,
                font,
                new Vector2(48f, -588f),
                new Vector2(420f, PresentationChrome.MinHit));

            screen.Confirm = CreateAction(
                screen.CanvasObject.transform,
                font,
                ConfirmName,
                "ENTER THE REALM",
                new Vector2(48f, -664f),
                new Vector2(420f, 64f),
                onConfirm);
            screen.Confirm.GetComponent<Image>().color = PresentationChrome.MetalEdge;
            screen.ConfirmLabel = screen.Confirm.GetComponentInChildren<Text>();
            if (screen.ConfirmLabel != null)
            {
                screen.ConfirmLabel.color = PresentationChrome.StoneVoid;
                screen.ConfirmLabel.fontSize = PresentationChrome.ActionSize;
            }

            return screen;
        }

        public static void PresentValidation(CharacterCreationProductionScreen screen, string message)
        {
            if (screen == null)
            {
                return;
            }

            bool visible = !string.IsNullOrWhiteSpace(message);
            if (screen.StatusPlate != null)
            {
                screen.StatusPlate.gameObject.SetActive(visible);
            }

            if (screen.Status != null)
            {
                screen.Status.text = visible ? message : string.Empty;
            }
        }

        public static void PaintClassSelection(CharacterCreationProductionScreen screen, ClassFamily? selected)
        {
            if (screen == null)
            {
                return;
            }

            foreach (KeyValuePair<ClassFamily, Image> pair in screen.ClassCards)
            {
                bool isSelected = selected.HasValue && selected.Value == pair.Key;
                pair.Value.color = isSelected
                    ? PresentationChrome.MetalEdge
                    : PresentationChrome.StoneInset;
                Text label = pair.Value.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = isSelected ? PresentationChrome.StoneVoid : PresentationChrome.Ink;
                }
            }
        }

        public static string FormatLookSummary(ChampionCustomizationState look)
        {
            if (look == null)
            {
                return string.Empty;
            }

            return "base " + CharacterCreationLook.NormalizeBodyBaseId(look.BodyBaseId) +
                   "  ·  hair " + look.HairStyleId +
                   "  ·  body " + look.BodyPresetId +
                   "  ·  armor " + look.ArmorStyleId +
                   "  ·  helm " + (look.HelmetEnabled ? "on" : "off") +
                   "  ·  cape " + (look.CapeEnabled ? "on" : "off");
        }

        public static Sprite TryLoadEmblem(RealmId realm)
        {
            if (!GameDataRealmReferences.TryGetByLegacyIdentity(
                    realm.ToString(),
                    (int)realm,
                    out GameDataRealmReference reference) ||
                string.IsNullOrEmpty(reference.AssetReference))
            {
                return null;
            }

            string relative = reference.AssetReference.Replace("Assets/", string.Empty).Replace('/', Path.DirectorySeparatorChar);
            string path = Path.Combine(Application.dataPath, relative);
            if (!File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return null;
            }

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        public static string ResolveHeraldryCopy(RealmId realm)
        {
            RealmCatalogSnapshot catalog = RealmCatalogRuntime.Current;
            if (catalog != null && catalog.TryGet(realm, out RealmCatalogEntry entry))
            {
                return entry.MarkName + "  ·  " + entry.SilhouetteLanguage + "  ·  " + entry.MaterialLanguage;
            }

            switch (realm)
            {
                case RealmId.Crownlands:
                    return "Celestial Meridian  ·  four-point meridian  ·  aged silver, pale stone";
                case RealmId.Stonehold:
                    return "Faultline Plate  ·  orthogonal plate  ·  basalt, worked iron";
                case RealmId.Eldergrove:
                    return "Living Orbit  ·  seed-and-orbit  ·  living wood, sap-gold";
                case RealmId.Umbral:
                    return "Severed Eclipse  ·  broken eclipse  ·  ash veil, dark crystal";
                default:
                    return string.Empty;
            }
        }

        private static CharacterCreationProductionScreen BuildShell(
            Font font,
            RealmId realm,
            string title,
            string peopleCopy,
            string heraldryCopy,
            Sprite emblem)
        {
            var screen = new CharacterCreationProductionScreen();
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
                new Vector2(0.46f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            PresentationChrome.CreatePlate(
                canvasObject.transform,
                "UpperGloom",
                new Color(0.02f, 0.022f, 0.028f, 0.55f),
                new Vector2(0f, 0.62f),
                new Vector2(0.46f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                Vector2.zero);

            var safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(canvasObject.transform, false);
            var safeRect = safe.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = new Vector2(0.46f, 1f);
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            PresentationChrome.CreatePlate(
                safe.transform,
                "TopMetalRail",
                PresentationChrome.MetalEdge,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -18f),
                new Vector2(-48f, 3f));
            PresentationChrome.CreatePlate(
                safe.transform,
                "BottomMetalRail",
                PresentationChrome.MetalDim,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 36f),
                new Vector2(-64f, 2f));

            PresentationChrome.CreateLabel(
                canvasObject.transform,
                "Title",
                font,
                title,
                PresentationChrome.DisplaySize,
                PresentationChrome.Ink,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(48f, -28f),
                new Vector2(760f, 52f));

            var heraldryHost = new GameObject("CommittedHeraldry", typeof(RectTransform));
            heraldryHost.transform.SetParent(canvasObject.transform, false);
            var heraldryRect = heraldryHost.GetComponent<RectTransform>();
            heraldryRect.anchorMin = new Vector2(0f, 1f);
            heraldryRect.anchorMax = new Vector2(0f, 1f);
            heraldryRect.pivot = new Vector2(0f, 1f);
            heraldryRect.anchoredPosition = new Vector2(48f, -88f);
            heraldryRect.sizeDelta = new Vector2(64f, 64f);
            RealmSelectionIdentity.BuildStructuralFrame(
                heraldryHost.transform,
                RealmSelectionIdentity.FrameKindFor(realm));

            screen.Emblem = PresentationChrome.CreatePlate(
                heraldryHost.transform,
                "RealmEmblem",
                Color.white,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(10f, 0f),
                new Vector2(52f, 52f));
            screen.Emblem.preserveAspect = true;
            if (emblem != null)
            {
                screen.Emblem.sprite = emblem;
                screen.Emblem.enabled = true;
            }
            else
            {
                screen.Emblem.enabled = false;
            }

            screen.People = PresentationChrome.CreateLabel(
                canvasObject.transform,
                "People",
                font,
                peopleCopy,
                PresentationChrome.PeopleSize,
                PresentationChrome.InkMuted,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(116f, -92f),
                new Vector2(680f, 24f));
            screen.Heraldry = PresentationChrome.CreateLabel(
                canvasObject.transform,
                "Heraldry",
                font,
                heraldryCopy,
                PresentationChrome.CaptionSize,
                PresentationChrome.InkFaint,
                TextAnchor.UpperLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(116f, -118f),
                new Vector2(680f, 22f));

            screen.StatusPlate = PresentationChrome.CreatePlate(
                canvasObject.transform,
                ValidationBannerName,
                new Color(0.18f, 0.08f, 0.07f, 0.94f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(48f, 56f),
                new Vector2(760f, 56f),
                raycastTarget: false);
            screen.Status = PresentationChrome.CreateLabel(
                screen.StatusPlate.transform,
                "ValidationCopy",
                font,
                string.Empty,
                PresentationChrome.BodySize,
                new Color(0.94f, 0.78f, 0.62f, 1f),
                TextAnchor.MiddleLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            var statusRect = screen.Status.rectTransform;
            statusRect.offsetMin = new Vector2(16f, 4f);
            statusRect.offsetMax = new Vector2(-16f, -4f);
            screen.StatusPlate.gameObject.SetActive(false);

            PresentationChrome.BindFonts(canvasObject.transform, font);
            return screen;
        }

        private static Button CreateAction(
            Transform parent,
            Font font,
            string name,
            string label,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Action onClick)
        {
            Button button = PresentationChrome.CreateHit(
                parent,
                name,
                PresentationChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                sizeDelta);
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            PresentationChrome.CreateLabel(
                button.transform,
                "Label",
                font,
                label,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            return button;
        }

        private static InputField CreateUsernameField(Transform parent, Font font, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            Image plate = PresentationChrome.CreatePlate(
                parent,
                UsernameName,
                PresentationChrome.StoneInset,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                anchoredPosition,
                sizeDelta,
                raycastTarget: true);

            Text text = PresentationChrome.CreateLabel(
                plate.transform,
                "Text",
                font,
                string.Empty,
                PresentationChrome.ActionSize,
                PresentationChrome.Ink,
                TextAnchor.MiddleLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            text.supportRichText = false;
            text.raycastTarget = true;
            text.rectTransform.offsetMin = new Vector2(16f, 4f);
            text.rectTransform.offsetMax = new Vector2(-16f, -4f);

            Text placeholder = PresentationChrome.CreateLabel(
                plate.transform,
                "Placeholder",
                font,
                "NAME THIS CHAMPION",
                PresentationChrome.BodySize,
                PresentationChrome.InkFaint,
                TextAnchor.MiddleLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, 0.5f),
                Vector2.zero,
                Vector2.zero);
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.rectTransform.offsetMin = new Vector2(16f, 4f);
            placeholder.rectTransform.offsetMax = new Vector2(-16f, -4f);

            var field = plate.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = CharacterCreationIdentity.MaxLength;
            field.contentType = InputField.ContentType.Standard;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }
    }
}
