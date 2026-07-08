using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public class ChampionCustomizationController : MonoBehaviour
    {
        private Renderer[] _renderers;

        private static readonly string[] BodyPresets =
        {
            "average", "slim", "broad", "tall", "stout"
        };

        private static readonly string[] HairStyles =
        {
            "short", "long", "braid"
        };

        private static readonly string[] ArmorStyles =
        {
            "realm_basic", "light_scout", "heavy_plate", "warmaster_plate"
        };

        private static readonly string[] FaceMarks =
        {
            "none", "scar", "warpaint", "realm_mark"
        };

        private static readonly string[] WeaponStyles =
        {
            "sword", "axe", "staff", "bow"
        };

        private static readonly string[] OffhandStyles =
        {
            "shield", "orb", "dagger", "none"
        };

        private static readonly Color[] PrimaryPalette =
        {
            new Color(0.20f, 0.40f, 1.00f),
            new Color(0.45f, 0.38f, 0.30f),
            new Color(0.18f, 0.58f, 0.32f),
            new Color(0.85f, 0.62f, 0.18f),
            new Color(0.22f, 0.08f, 0.28f)
        };

        private static readonly Color[] HairPalette =
        {
            new Color(0.08f, 0.06f, 0.04f),
            new Color(0.55f, 0.36f, 0.16f),
            new Color(0.85f, 0.78f, 0.55f),
            new Color(0.80f, 0.82f, 0.90f),
            new Color(0.25f, 0.05f, 0.08f)
        };

        private static readonly Color[] SkinPalette =
        {
            new Color(0.72f, 0.56f, 0.42f),
            new Color(0.55f, 0.38f, 0.26f),
            new Color(0.86f, 0.70f, 0.54f),
            new Color(0.64f, 0.50f, 0.46f),
            new Color(0.42f, 0.34f, 0.40f)
        };

        private static readonly Color[] EyePalette =
        {
            new Color(0.25f, 0.58f, 0.92f),
            new Color(0.28f, 0.72f, 0.42f),
            new Color(0.70f, 0.42f, 0.18f),
            new Color(0.78f, 0.72f, 0.88f),
            new Color(0.90f, 0.18f, 0.12f)
        };

        private static readonly Color[] AccentPalette =
        {
            new Color(0.85f, 0.62f, 0.18f),
            new Color(0.30f, 0.75f, 1.00f),
            new Color(0.42f, 1.00f, 0.48f),
            new Color(0.90f, 0.12f, 0.16f),
            new Color(0.68f, 0.28f, 0.96f)
        };

        private void Awake()
        {
            ProceduralChampionModelBuilder.EnsureModel(gameObject);
            RefreshRendererCache();
        }

        private void Start()
        {
            ApplySavedCustomization();
        }

        public void ApplySavedCustomization()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            ProceduralChampionModelBuilder.EnsureModel(gameObject);
            RefreshRendererCache();
            NormalizeState(state);
            ApplyBodyPreset(state.BodyPresetId);
            ApplyHairStyle(state.HairStyleId);
            ApplyArmorStyle(state.ArmorStyleId);
            ApplyFaceMark(state.FaceMarkId);
            ApplyWeaponStyle(state.WeaponStyleId);
            ApplyOffhandStyle(state.OffhandStyleId);
            ApplyColors(
                new Color(state.PrimaryR, state.PrimaryG, state.PrimaryB),
                new Color(state.HairR, state.HairG, state.HairB),
                new Color(state.SkinR, state.SkinG, state.SkinB),
                new Color(state.EyeR, state.EyeG, state.EyeB),
                new Color(state.AccentR, state.AccentG, state.AccentB));
            SetPartActive("Cape", state.CapeEnabled);
            SetPartActive("Helmet", state.HelmetEnabled);
        }

        public void CyclePrimaryColor()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            Color current = new Color(state.PrimaryR, state.PrimaryG, state.PrimaryB);
            Color next = NextColor(current, PrimaryPalette);
            state.PrimaryR = next.r;
            state.PrimaryG = next.g;
            state.PrimaryB = next.b;
            SaveAndApply();
        }

        public void CycleHairColor()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            Color current = new Color(state.HairR, state.HairG, state.HairB);
            Color next = NextColor(current, HairPalette);
            state.HairR = next.r;
            state.HairG = next.g;
            state.HairB = next.b;
            SaveAndApply();
        }

        public void CycleSkinColor()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            Color current = new Color(state.SkinR, state.SkinG, state.SkinB);
            Color next = NextColor(current, SkinPalette);
            state.SkinR = next.r;
            state.SkinG = next.g;
            state.SkinB = next.b;
            SaveAndApply();
        }

        public void CycleEyeColor()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            Color current = new Color(state.EyeR, state.EyeG, state.EyeB);
            Color next = NextColor(current, EyePalette);
            state.EyeR = next.r;
            state.EyeG = next.g;
            state.EyeB = next.b;
            SaveAndApply();
        }

        public void CycleAccentColor()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            Color current = new Color(state.AccentR, state.AccentG, state.AccentB);
            Color next = NextColor(current, AccentPalette);
            state.AccentR = next.r;
            state.AccentG = next.g;
            state.AccentB = next.b;
            SaveAndApply();
        }

        public void CycleBodyPreset()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.BodyPresetId = NextId(state.BodyPresetId, BodyPresets, "average");
            SaveAndApply();
        }

        public void CycleHairStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.HairStyleId = NextId(state.HairStyleId, HairStyles, "short");
            SaveAndApply();
        }

        public void CycleArmorStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.ArmorStyleId = NextId(state.ArmorStyleId, ArmorStyles, "realm_basic");
            SaveAndApply();
        }

        public void CycleFaceMark()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.FaceMarkId = NextId(state.FaceMarkId, FaceMarks, "none");
            SaveAndApply();
        }

        public void CycleWeaponStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.WeaponStyleId = NextId(state.WeaponStyleId, WeaponStyles, "sword");
            SaveAndApply();
        }

        public void CycleOffhandStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.OffhandStyleId = NextId(state.OffhandStyleId, OffhandStyles, "shield");
            SaveAndApply();
        }

        public void ToggleCape()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.CapeEnabled = !state.CapeEnabled;
            SaveAndApply();
        }

        public void ToggleHelmet()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.HelmetEnabled = !state.HelmetEnabled;
            SaveAndApply();
        }

        private void SaveAndApply()
        {
            ServiceLocator.Get<ISaveGameService>().Save();
            ApplySavedCustomization();
        }

        private ChampionCustomizationState GetState()
        {
            var save = ServiceLocator.Get<ISaveGameService>().CurrentSave;
            return save?.ChampionCustomization;
        }

        private void NormalizeState(ChampionCustomizationState state)
        {
            if (!ContainsId(state.BodyPresetId, BodyPresets))
            {
                state.BodyPresetId = "average";
            }

            if (!ContainsId(state.HairStyleId, HairStyles))
            {
                state.HairStyleId = "short";
            }

            if (!ContainsId(state.ArmorStyleId, ArmorStyles))
            {
                state.ArmorStyleId = "realm_basic";
            }

            if (!ContainsId(state.FaceMarkId, FaceMarks))
            {
                state.FaceMarkId = "none";
            }

            if (!ContainsId(state.WeaponStyleId, WeaponStyles))
            {
                state.WeaponStyleId = "sword";
            }

            if (!ContainsId(state.OffhandStyleId, OffhandStyles))
            {
                state.OffhandStyleId = "shield";
            }
        }

        private void ApplyBodyPreset(string presetId)
        {
            transform.localScale = presetId switch
            {
                "slim" => new Vector3(0.86f, 1.06f, 0.86f),
                "broad" => new Vector3(1.16f, 1.00f, 1.06f),
                "tall" => new Vector3(0.96f, 1.18f, 0.96f),
                "stout" => new Vector3(1.08f, 0.92f, 1.08f),
                _ => Vector3.one
            };
        }

        private void ApplyHairStyle(string hairStyleId)
        {
            SetExactPartActive("Hair_Short", hairStyleId == "short");
            SetExactPartActive("Hair_Long", hairStyleId == "long");
            SetExactPartActive("Hair_Braid", hairStyleId == "braid");
        }

        private void ApplyArmorStyle(string armorStyleId)
        {
            bool isLight = armorStyleId == "light_scout";
            bool isHeavy = armorStyleId == "heavy_plate" || armorStyleId == "warmaster_plate";
            bool isWarmaster = armorStyleId == "warmaster_plate";

            SetPartActive("ChestArmor", true);
            SetPartActive("Glove", true);
            SetPartActive("Boot", true);
            SetPartActive("Weapon_Main", true);
            SetPartActive("Shoulder", !isLight);
            SetPartActive("BackAttachment", isWarmaster);

            SetPartScale("ChestArmor", isHeavy ? new Vector3(1.05f, 0.82f, 0.38f) : new Vector3(0.92f, 0.74f, 0.32f));
            SetPartScale("Shoulder_L", isHeavy ? new Vector3(0.34f, 0.26f, 0.34f) : new Vector3(0.26f, 0.20f, 0.28f));
            SetPartScale("Shoulder_R", isHeavy ? new Vector3(0.34f, 0.26f, 0.34f) : new Vector3(0.26f, 0.20f, 0.28f));
        }

        private void ApplyFaceMark(string faceMarkId)
        {
            bool isVisible = faceMarkId != "none";
            SetExactPartActive("FaceMark", isVisible);
            if (!isVisible)
            {
                return;
            }

            switch (faceMarkId)
            {
                case "scar":
                    SetPartTransform("FaceMark", new Vector3(-0.08f, 0.62f, 0.49f), new Vector3(0.035f, 0.28f, 0.025f), new Vector3(0f, 0f, 24f));
                    break;
                case "realm_mark":
                    SetPartTransform("FaceMark", new Vector3(0f, 0.68f, 0.49f), new Vector3(0.13f, 0.13f, 0.025f), new Vector3(0f, 0f, 45f));
                    break;
                default:
                    SetPartTransform("FaceMark", new Vector3(0f, 0.61f, 0.49f), new Vector3(0.24f, 0.035f, 0.025f), Vector3.zero);
                    break;
            }
        }

        private void ApplyWeaponStyle(string weaponStyleId)
        {
            switch (weaponStyleId)
            {
                case "axe":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.08f, 0.56f, 0.08f), new Vector3(0f, 0f, 18f));
                    break;
                case "staff":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.055f, 0.88f, 0.055f), new Vector3(0f, 0f, 8f));
                    break;
                case "bow":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.10f, 0.16f), new Vector3(0.04f, 0.82f, 0.04f), new Vector3(0f, 0f, 78f));
                    break;
                default:
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.00f, 0.16f), new Vector3(0.06f, 0.70f, 0.06f), new Vector3(0f, 0f, 34f));
                    break;
            }
        }

        private void ApplyOffhandStyle(string offhandStyleId)
        {
            SetExactPartActive("Shield_Off", offhandStyleId == "shield");
            SetExactPartActive("Orb_Off", offhandStyleId == "orb");
            SetExactPartActive("Weapon_Off", offhandStyleId == "dagger");
        }

        private void ApplyColors(Color primary, Color hair, Color skin, Color eye, Color accent)
        {
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name.ToLowerInvariant();
                bool isHair = objectName.Contains("hair");
                bool isSkin = objectName.Contains("skin");
                bool isEye = objectName.Contains("eye");
                bool isAccent = objectName.Contains("facemark") ||
                                objectName.Contains("cape") ||
                                objectName.Contains("backattachment") ||
                                objectName.Contains("orb");
                bool isMetal = objectName.Contains("helmet") ||
                               objectName.Contains("armor") ||
                               objectName.Contains("shoulder") ||
                               objectName.Contains("glove") ||
                               objectName.Contains("boot") ||
                               objectName.Contains("weapon") ||
                               objectName.Contains("shield");
                renderer.material.color = isHair
                    ? hair
                    : isSkin
                        ? skin
                        : isEye
                            ? eye
                            : isAccent
                                ? accent
                                : isMetal
                                    ? Color.Lerp(primary, Color.white, 0.22f)
                                    : primary;
            }
        }

        private void SetPartActive(string partName, bool isActive)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name.ToLowerInvariant().Contains(partName.ToLowerInvariant()))
                {
                    child.gameObject.SetActive(isActive);
                }
            }
        }

        private void SetExactPartActive(string partName, bool isActive)
        {
            Transform part = FindPart(partName);
            if (part != null)
            {
                part.gameObject.SetActive(isActive);
            }
        }

        private void SetPartScale(string partName, Vector3 scale)
        {
            Transform part = FindPart(partName);
            if (part != null)
            {
                part.localScale = scale;
            }
        }

        private void SetPartTransform(string partName, Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles)
        {
            Transform part = FindPart(partName);
            if (part != null)
            {
                part.localPosition = localPosition;
                part.localScale = localScale;
                part.localRotation = Quaternion.Euler(localEulerAngles);
            }
        }

        private Transform FindPart(string partName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == partName)
                {
                    return child;
                }
            }

            return null;
        }

        private void RefreshRendererCache()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
        }

        private static Color NextColor(Color current, Color[] palette)
        {
            int currentIndex = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                float distance = Mathf.Abs(current.r - palette[i].r) + Mathf.Abs(current.g - palette[i].g) + Mathf.Abs(current.b - palette[i].b);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    currentIndex = i;
                }
            }

            return palette[(currentIndex + 1) % palette.Length];
        }

        private static string NextId(string current, string[] ids, string fallback)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return fallback;
            }

            int index = -1;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == current)
                {
                    index = i;
                    break;
                }
            }

            return ids[(index + 1) % ids.Length];
        }

        private static bool ContainsId(string current, string[] ids)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return false;
            }

            foreach (string id in ids)
            {
                if (id == current)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
