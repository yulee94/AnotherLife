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
            ApplyColors(new Color(state.PrimaryR, state.PrimaryG, state.PrimaryB), new Color(state.HairR, state.HairG, state.HairB));
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

        private void ApplyColors(Color primary, Color hair)
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
                bool isMetal = objectName.Contains("helmet") ||
                               objectName.Contains("armor") ||
                               objectName.Contains("shoulder") ||
                               objectName.Contains("glove") ||
                               objectName.Contains("boot") ||
                               objectName.Contains("weapon");
                renderer.material.color = isHair
                    ? hair
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
