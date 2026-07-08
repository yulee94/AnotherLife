using System;
using System.Collections;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public class ChampionCustomizationController : MonoBehaviour
    {
        private Renderer[] _renderers;
        private CharacterCustomizationCatalogData _catalog;
        private bool _catalogLoadStarted;

        private static readonly string[] BodyPresets =
        {
            "average", "slim", "broad", "tall", "stout"
        };

        private static readonly string[] HairStyles =
        {
            "short", "long", "braid", "mohawk", "topknot"
        };

        private static readonly string[] ArmorStyles =
        {
            "realm_basic", "light_scout", "heavy_plate", "warmaster_plate", "arcane_robes", "assassin_leathers"
        };

        private static readonly string[] FaceMarks =
        {
            "none", "scar", "warpaint", "realm_mark", "rune", "tattoo"
        };

        private static readonly string[] WeaponStyles =
        {
            "sword", "axe", "staff", "bow", "hammer"
        };

        private static readonly string[] OffhandStyles =
        {
            "shield", "orb", "dagger", "tome", "none"
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
            TryApplySharedCatalog();
        }

        private void Start()
        {
            ApplySavedCustomization();
            if (_catalog == null && !_catalogLoadStarted)
            {
                StartCoroutine(ApplySharedCatalogAsync());
            }
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
            Color next = NextColor(current, GetPrimaryPalette());
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
            Color next = NextColor(current, GetHairPalette());
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
            Color next = NextColor(current, GetSkinPalette());
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
            Color next = NextColor(current, GetEyePalette());
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
            Color next = NextColor(current, GetAccentPalette());
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

            state.BodyPresetId = NextId(state.BodyPresetId, GetBodyPresetIds(), "average");
            SaveAndApply();
        }

        public void CycleHairStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.HairStyleId = NextId(state.HairStyleId, GetHairStyleIds(), "short");
            SaveAndApply();
        }

        public void CycleArmorStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.ArmorStyleId = NextId(state.ArmorStyleId, GetArmorStyleIds(), "realm_basic");
            SaveAndApply();
        }

        public void CycleFaceMark()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.FaceMarkId = NextId(state.FaceMarkId, GetFaceMarkIds(), "none");
            SaveAndApply();
        }

        public void CycleWeaponStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.WeaponStyleId = NextId(state.WeaponStyleId, GetWeaponStyleIds(), "sword");
            SaveAndApply();
        }

        public void CycleOffhandStyle()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.OffhandStyleId = NextId(state.OffhandStyleId, GetOffhandStyleIds(), "shield");
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

        public void RandomizeAppearance()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.BodyPresetId = PickRandom(GetBodyPresetIds(), "average");
            state.HairStyleId = PickRandom(GetHairStyleIds(), "short");
            state.ArmorStyleId = PickRandom(GetArmorStyleIds(), "realm_basic");
            state.FaceMarkId = PickRandom(GetFaceMarkIds(), "none");
            state.WeaponStyleId = PickRandom(GetWeaponStyleIds(), "sword");
            state.OffhandStyleId = PickRandom(GetOffhandStyleIds(), "shield");
            ApplyColorToState(PickRandom(GetPrimaryPalette(), PrimaryPalette[0]), (r, g, b) =>
            {
                state.PrimaryR = r;
                state.PrimaryG = g;
                state.PrimaryB = b;
            });
            ApplyColorToState(PickRandom(GetHairPalette(), HairPalette[0]), (r, g, b) =>
            {
                state.HairR = r;
                state.HairG = g;
                state.HairB = b;
            });
            ApplyColorToState(PickRandom(GetSkinPalette(), SkinPalette[0]), (r, g, b) =>
            {
                state.SkinR = r;
                state.SkinG = g;
                state.SkinB = b;
            });
            ApplyColorToState(PickRandom(GetEyePalette(), EyePalette[0]), (r, g, b) =>
            {
                state.EyeR = r;
                state.EyeG = g;
                state.EyeB = b;
            });
            ApplyColorToState(PickRandom(GetAccentPalette(), AccentPalette[0]), (r, g, b) =>
            {
                state.AccentR = r;
                state.AccentG = g;
                state.AccentB = b;
            });
            state.CapeEnabled = UnityEngine.Random.value > 0.18f;
            state.HelmetEnabled = UnityEngine.Random.value > 0.42f;
            SaveAndApply();
        }

        public void ResetAppearance()
        {
            var state = GetState();
            if (state == null)
            {
                return;
            }

            state.BodyPresetId = "average";
            state.HairStyleId = "short";
            state.ArmorStyleId = "realm_basic";
            state.FaceMarkId = "none";
            state.WeaponStyleId = "sword";
            state.OffhandStyleId = "shield";
            state.PrimaryR = 0.20f;
            state.PrimaryG = 0.40f;
            state.PrimaryB = 1.00f;
            state.HairR = 0.08f;
            state.HairG = 0.06f;
            state.HairB = 0.04f;
            state.SkinR = 0.72f;
            state.SkinG = 0.56f;
            state.SkinB = 0.42f;
            state.EyeR = 0.25f;
            state.EyeG = 0.58f;
            state.EyeB = 0.92f;
            state.AccentR = 0.85f;
            state.AccentG = 0.62f;
            state.AccentB = 0.18f;
            state.CapeEnabled = true;
            state.HelmetEnabled = false;
            SaveAndApply();
        }

        public string GetAppearanceSummary()
        {
            var state = GetState();
            if (state == null)
            {
                return "Appearance unavailable";
            }

            NormalizeState(state);
            return
                $"{FormatId(state.BodyPresetId)} body | {FormatId(state.ArmorStyleId)}\n" +
                $"{FormatId(state.HairStyleId)} hair | {FormatId(state.FaceMarkId)} mark\n" +
                $"{FormatId(state.WeaponStyleId)} + {FormatId(state.OffhandStyleId)} | Cape {(state.CapeEnabled ? "On" : "Off")} | Helm {(state.HelmetEnabled ? "On" : "Off")}";
        }

        public Color GetPrimaryColor()
        {
            var state = GetState();
            return state == null ? PrimaryPalette[0] : new Color(state.PrimaryR, state.PrimaryG, state.PrimaryB);
        }

        public Color GetHairColor()
        {
            var state = GetState();
            return state == null ? HairPalette[0] : new Color(state.HairR, state.HairG, state.HairB);
        }

        public Color GetSkinColor()
        {
            var state = GetState();
            return state == null ? SkinPalette[0] : new Color(state.SkinR, state.SkinG, state.SkinB);
        }

        public Color GetEyeColor()
        {
            var state = GetState();
            return state == null ? EyePalette[0] : new Color(state.EyeR, state.EyeG, state.EyeB);
        }

        public Color GetAccentColor()
        {
            var state = GetState();
            return state == null ? AccentPalette[0] : new Color(state.AccentR, state.AccentG, state.AccentB);
        }

        private void SaveAndApply()
        {
            ServiceLocator.Get<ISaveGameService>().Save();
            ApplySavedCustomization();
        }

        private void TryApplySharedCatalog()
        {
            if (CharacterCustomizationCatalog.TryLoad(out var catalog))
            {
                ApplyCatalog(catalog);
            }
        }

        private IEnumerator ApplySharedCatalogAsync()
        {
            _catalogLoadStarted = true;
            bool applied = false;
            yield return CharacterCustomizationCatalog.LoadAsync(catalog =>
            {
                if (catalog == null)
                {
                    return;
                }

                ApplyCatalog(catalog);
                applied = true;
            });

            _catalogLoadStarted = false;
            if (applied)
            {
                ApplySavedCustomization();
                Debug.Log("[ChampionCustomizationController] Applied shared customization catalog from StreamingAssets.");
            }
        }

        private void ApplyCatalog(CharacterCustomizationCatalogData catalog)
        {
            _catalog = catalog;
        }

        private ChampionCustomizationState GetState()
        {
            var save = ServiceLocator.Get<ISaveGameService>().CurrentSave;
            return save?.ChampionCustomization;
        }

        private void NormalizeState(ChampionCustomizationState state)
        {
            state.BodyPresetId = NormalizeId(state.BodyPresetId, GetBodyPresetIds(), "average");
            state.HairStyleId = NormalizeId(state.HairStyleId, GetHairStyleIds(), "short");
            state.ArmorStyleId = NormalizeId(state.ArmorStyleId, GetArmorStyleIds(), "realm_basic");
            state.FaceMarkId = NormalizeId(state.FaceMarkId, GetFaceMarkIds(), "none");
            state.WeaponStyleId = NormalizeId(state.WeaponStyleId, GetWeaponStyleIds(), "sword");
            state.OffhandStyleId = NormalizeId(state.OffhandStyleId, GetOffhandStyleIds(), "shield");
        }

        private void ApplyBodyPreset(string presetId)
        {
            if (TryGetCatalogBodyScale(presetId, out var catalogScale))
            {
                transform.localScale = catalogScale;
                return;
            }

            transform.localScale = presetId switch
            {
                "slim" => new Vector3(0.86f, 1.06f, 0.86f),
                "broad" => new Vector3(1.16f, 1.00f, 1.06f),
                "tall" => new Vector3(0.96f, 1.18f, 0.96f),
                "stout" => new Vector3(1.08f, 0.92f, 1.08f),
                _ => Vector3.one
            };
        }

        private bool TryGetCatalogBodyScale(string presetId, out Vector3 scale)
        {
            scale = Vector3.one;
            var presets = _catalog?.bodyPresets;
            if (presets == null)
            {
                return false;
            }

            foreach (var preset in presets)
            {
                if (preset == null || preset.id != presetId || preset.scale == null || preset.scale.Length < 3)
                {
                    continue;
                }

                scale = new Vector3(
                    Mathf.Max(0.1f, preset.scale[0]),
                    Mathf.Max(0.1f, preset.scale[1]),
                    Mathf.Max(0.1f, preset.scale[2]));
                return true;
            }

            return false;
        }

        private void ApplyHairStyle(string hairStyleId)
        {
            bool isShort = hairStyleId == "short";
            bool isLong = hairStyleId == "long";
            bool isBraid = hairStyleId == "braid";
            bool isMohawk = hairStyleId == "mohawk";
            bool isTopknot = hairStyleId == "topknot";

            SetExactPartActive("Hair_Short", isShort);
            SetExactPartActive("Hair_Short_Front", isShort);
            SetExactPartActive("Hair_Long", isLong);
            SetExactPartActive("Hair_Long_Side_L", isLong);
            SetExactPartActive("Hair_Long_Side_R", isLong);
            SetExactPartActive("Hair_Braid", isBraid);
            SetExactPartActive("Hair_Braid_Band", isBraid);
            SetExactPartActive("Hair_Mohawk", isMohawk);
            SetExactPartActive("Hair_Mohawk_Tip", isMohawk);
            SetExactPartActive("Hair_Topknot", isTopknot);
            SetExactPartActive("Hair_Topknot_Tail", isTopknot);
            SetExactPartActive("Hair_Topknot_Band", isTopknot);
        }

        private void ApplyArmorStyle(string armorStyleId)
        {
            bool isRobe = armorStyleId == "arcane_robes";
            bool isAssassin = armorStyleId == "assassin_leathers";
            bool isLight = armorStyleId == "light_scout" || isAssassin || isRobe;
            bool isHeavy = armorStyleId == "heavy_plate" || armorStyleId == "warmaster_plate";
            bool isWarmaster = armorStyleId == "warmaster_plate";

            SetPartActive("ChestArmor", true);
            SetPartActive("Armor_Pectoral", true);
            SetPartActive("Armor_Collar", true);
            SetPartActive("Armor_AbPlate", true);
            SetPartActive("Armor_HipPlate", !isRobe);
            SetPartActive("Armor_Thigh", !isRobe);
            SetPartActive("Glove", true);
            SetPartActive("Boot", true);
            SetPartActive("Weapon_Main", true);
            SetPartActive("Hood", isRobe);
            SetPartActive("RobePanel", isRobe);
            SetPartActive("RobeBackPanel", isRobe);
            SetPartActive("RobeSleeve", isRobe);
            SetPartActive("ArmorTrim", true);
            SetPartActive("Belt", true);
            SetPartActive("PlateSkirt", !isLight || isWarmaster);
            SetPartActive("Knee", !isRobe);
            SetPartActive("Shoulder", !isLight);
            SetPartActive("ShoulderSpike", isWarmaster);
            SetPartActive("BackAttachment", isWarmaster);

            SetPartScale("ChestArmor", isRobe ? new Vector3(0.78f, 0.82f, 0.28f) : isAssassin ? new Vector3(0.84f, 0.64f, 0.26f) : isHeavy ? new Vector3(1.05f, 0.82f, 0.38f) : new Vector3(0.92f, 0.74f, 0.32f));
            SetPartScale("Armor_Pectoral_L", isHeavy ? new Vector3(0.34f, 0.32f, 0.09f) : new Vector3(0.30f, 0.28f, 0.08f));
            SetPartScale("Armor_Pectoral_R", isHeavy ? new Vector3(0.34f, 0.32f, 0.09f) : new Vector3(0.30f, 0.28f, 0.08f));
            SetPartScale("Shoulder_L", isHeavy ? new Vector3(0.34f, 0.26f, 0.34f) : new Vector3(0.26f, 0.20f, 0.28f));
            SetPartScale("Shoulder_R", isHeavy ? new Vector3(0.34f, 0.26f, 0.34f) : new Vector3(0.26f, 0.20f, 0.28f));
            SetPartScale("Glove_L", isAssassin ? new Vector3(0.14f, 0.30f, 0.14f) : new Vector3(0.18f, 0.24f, 0.18f));
            SetPartScale("Glove_R", isAssassin ? new Vector3(0.14f, 0.30f, 0.14f) : new Vector3(0.18f, 0.24f, 0.18f));
            SetPartScale("PlateSkirt_Front", isWarmaster ? new Vector3(0.52f, 0.50f, 0.07f) : new Vector3(0.42f, 0.44f, 0.06f));
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
                case "rune":
                    SetPartTransform("FaceMark", new Vector3(0.08f, 0.66f, 0.49f), new Vector3(0.12f, 0.12f, 0.025f), new Vector3(0f, 0f, 0f));
                    break;
                case "tattoo":
                    SetPartTransform("FaceMark", new Vector3(0f, 0.58f, 0.49f), new Vector3(0.30f, 0.030f, 0.025f), new Vector3(0f, 0f, -18f));
                    break;
                default:
                    SetPartTransform("FaceMark", new Vector3(0f, 0.61f, 0.49f), new Vector3(0.24f, 0.035f, 0.025f), Vector3.zero);
                    break;
            }
        }

        private void ApplyWeaponStyle(string weaponStyleId)
        {
            bool isSword = weaponStyleId == "sword";
            bool isAxe = weaponStyleId == "axe";
            bool isStaff = weaponStyleId == "staff";
            bool isBow = weaponStyleId == "bow";
            bool isHammer = weaponStyleId == "hammer";

            SetExactPartActive("Sword_Blade", isSword);
            SetExactPartActive("Sword_Guard", isSword);
            SetExactPartActive("Weapon_Head", isAxe);
            SetExactPartActive("Axe_Blade_L", isAxe);
            SetExactPartActive("Axe_Blade_R", isAxe);
            SetExactPartActive("Hammer_Face", isHammer);
            SetExactPartActive("Staff_Crystal", isStaff);
            SetPartActive("Bow_Limb", isBow);
            SetExactPartActive("Bow_String", isBow);

            switch (weaponStyleId)
            {
                case "axe":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.08f, 0.56f, 0.08f), new Vector3(0f, 0f, 18f));
                    SetPartTransform("Weapon_Head", new Vector3(0.80f, 0.48f, 0.18f), new Vector3(0.28f, 0.18f, 0.10f), new Vector3(0f, 0f, 18f));
                    break;
                case "staff":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.055f, 0.88f, 0.055f), new Vector3(0f, 0f, 8f));
                    break;
                case "bow":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.10f, 0.16f), new Vector3(0.04f, 0.82f, 0.04f), new Vector3(0f, 0f, 78f));
                    SetPartTransform("Bow_String", new Vector3(0.58f, 0.10f, 0.16f), new Vector3(0.025f, 0.78f, 0.025f), new Vector3(0f, 0f, 78f));
                    break;
                case "hammer":
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.02f, 0.16f), new Vector3(0.075f, 0.60f, 0.075f), new Vector3(0f, 0f, 20f));
                    SetPartTransform("Hammer_Face", new Vector3(0.84f, 0.50f, 0.18f), new Vector3(0.40f, 0.22f, 0.16f), new Vector3(0f, 0f, 20f));
                    break;
                default:
                    SetPartTransform("Weapon_Main", new Vector3(0.72f, 0.00f, 0.16f), new Vector3(0.06f, 0.70f, 0.06f), new Vector3(0f, 0f, 34f));
                    break;
            }
        }

        private void ApplyOffhandStyle(string offhandStyleId)
        {
            SetExactPartActive("Shield_Off", offhandStyleId == "shield");
            SetExactPartActive("Shield_Crest", offhandStyleId == "shield");
            SetExactPartActive("Orb_Off", offhandStyleId == "orb");
            SetExactPartActive("Orb_Ring", offhandStyleId == "orb");
            SetExactPartActive("Weapon_Off", offhandStyleId == "dagger");
            SetExactPartActive("Dagger_Blade", offhandStyleId == "dagger");
            SetExactPartActive("Tome_Off", offhandStyleId == "tome");
            SetExactPartActive("Tome_Clasp", offhandStyleId == "tome");
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
                bool isHair = objectName.Contains("hair") || objectName.Contains("brow");
                bool isSkin = objectName.Contains("skin") || objectName.Contains("ear");
                bool isEye = objectName.Contains("eye");
                bool isAccent = objectName.Contains("facemark") ||
                                objectName.Contains("backattachment") ||
                                objectName.Contains("orb") ||
                                objectName.Contains("trim") ||
                                objectName.Contains("tome_clasp") ||
                                objectName.Contains("belt_buckle") ||
                                objectName.Contains("clasp") ||
                                objectName.Contains("crystal") ||
                                objectName.Contains("crest") ||
                                objectName.Contains("spike") ||
                                objectName.Contains("guard");
                bool isFabric = objectName.Contains("cape") ||
                                objectName.Contains("robe") ||
                                objectName.Contains("hood") ||
                                objectName.Contains("tome");
                bool isLeather = objectName.Contains("belt") ||
                                 objectName.Contains("boot") ||
                                 objectName.Contains("grip") ||
                                 objectName.Contains("bow_limb");
                bool isMetal = objectName.Contains("helmet") ||
                               objectName.Contains("armor") ||
                               objectName.Contains("shoulder") ||
                               objectName.Contains("glove") ||
                               objectName.Contains("boot") ||
                               objectName.Contains("weapon") ||
                               objectName.Contains("shield") ||
                               objectName.Contains("knee") ||
                               objectName.Contains("belt");

                Color targetColor = isHair
                    ? hair
                    : isSkin
                        ? skin
                        : isEye
                            ? eye
                            : isAccent
                                ? accent
                                : isFabric
                                    ? Color.Lerp(primary, accent, 0.45f)
                                    : isLeather
                                        ? Color.Lerp(primary, new Color(0.16f, 0.10f, 0.06f), 0.64f)
                                        : isMetal
                                            ? Color.Lerp(primary, Color.white, 0.22f)
                                            : primary;

                float metallic = isMetal ? 0.46f : isAccent ? 0.26f : isLeather ? 0.06f : 0f;
                float smoothness = isEye || isAccent ? 0.72f : isMetal ? 0.60f : isSkin ? 0.30f : isHair ? 0.36f : 0.46f;
                float emissionStrength = isEye
                    ? 0.22f
                    : objectName.Contains("orb") || objectName.Contains("crystal") || objectName.Contains("backattachment_core")
                        ? 0.46f
                        : objectName.Contains("facemark") || objectName.Contains("trim") || objectName.Contains("crest")
                            ? 0.10f
                            : 0f;
                ApplyRendererMaterial(renderer, targetColor, metallic, smoothness, emissionStrength);
            }
        }

        private static void ApplyRendererMaterial(Renderer renderer, Color color, float metallic, float smoothness, float emissionStrength)
        {
            if (renderer == null)
            {
                return;
            }

            var material = renderer.material;
            material.color = color;
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));
            }

            if (emissionStrength > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionStrength);
            }
            else if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", Color.black);
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

        private string[] GetBodyPresetIds()
        {
            return ExtractIds(_catalog?.bodyPresets, BodyPresets);
        }

        private string[] GetHairStyleIds()
        {
            return ExtractIds(_catalog?.hairStyles, HairStyles);
        }

        private string[] GetArmorStyleIds()
        {
            return ExtractIds(_catalog?.armorStyles, ArmorStyles);
        }

        private string[] GetFaceMarkIds()
        {
            return ExtractIds(_catalog?.faceMarks, FaceMarks);
        }

        private string[] GetWeaponStyleIds()
        {
            return ExtractIds(_catalog?.weaponStyles, WeaponStyles);
        }

        private string[] GetOffhandStyleIds()
        {
            return ExtractIds(_catalog?.offhandStyles, OffhandStyles);
        }

        private Color[] GetPrimaryPalette()
        {
            return ExtractColors(_catalog?.primaryColors, PrimaryPalette);
        }

        private Color[] GetHairPalette()
        {
            return ExtractColors(_catalog?.hairColors, HairPalette);
        }

        private Color[] GetSkinPalette()
        {
            return ExtractColors(_catalog?.skinColors, SkinPalette);
        }

        private Color[] GetEyePalette()
        {
            return ExtractColors(_catalog?.eyeColors, EyePalette);
        }

        private Color[] GetAccentPalette()
        {
            return ExtractColors(_catalog?.accentColors, AccentPalette);
        }

        private static string NormalizeId(string current, string[] ids, string fallback)
        {
            if (ContainsId(current, ids))
            {
                return current;
            }

            return ContainsId(fallback, ids) ? fallback : FirstIdOrFallback(ids, fallback);
        }

        private static string[] ExtractIds(BodyPresetData[] options, string[] fallback)
        {
            if (options == null || options.Length == 0)
            {
                return fallback;
            }

            var ids = new List<string>(options.Length);
            foreach (var option in options)
            {
                if (option != null && !string.IsNullOrWhiteSpace(option.id))
                {
                    ids.Add(option.id);
                }
            }

            return ids.Count > 0 ? ids.ToArray() : fallback;
        }

        private static string[] ExtractIds(StyleOptionData[] options, string[] fallback)
        {
            if (options == null || options.Length == 0)
            {
                return fallback;
            }

            var ids = new List<string>(options.Length);
            foreach (var option in options)
            {
                if (option != null && !string.IsNullOrWhiteSpace(option.id))
                {
                    ids.Add(option.id);
                }
            }

            return ids.Count > 0 ? ids.ToArray() : fallback;
        }

        private static Color[] ExtractColors(ColorOptionData[] options, Color[] fallback)
        {
            if (options == null || options.Length == 0)
            {
                return fallback;
            }

            var colors = new List<Color>(options.Length);
            foreach (var option in options)
            {
                if (option == null || option.rgb == null || option.rgb.Length < 3)
                {
                    continue;
                }

                colors.Add(new Color(
                    Mathf.Clamp01(option.rgb[0]),
                    Mathf.Clamp01(option.rgb[1]),
                    Mathf.Clamp01(option.rgb[2])));
            }

            return colors.Count > 0 ? colors.ToArray() : fallback;
        }

        private static string PickRandom(string[] ids, string fallback)
        {
            if (ids == null || ids.Length == 0)
            {
                return fallback;
            }

            return ids[UnityEngine.Random.Range(0, ids.Length)];
        }

        private static Color PickRandom(Color[] colors, Color fallback)
        {
            if (colors == null || colors.Length == 0)
            {
                return fallback;
            }

            return colors[UnityEngine.Random.Range(0, colors.Length)];
        }

        private static void ApplyColorToState(Color color, Action<float, float, float> apply)
        {
            apply?.Invoke(color.r, color.g, color.b);
        }

        private static Color NextColor(Color current, Color[] palette)
        {
            if (palette == null || palette.Length == 0)
            {
                return current;
            }

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
            if (ids == null || ids.Length == 0)
            {
                return fallback;
            }

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

        private static string FirstIdOrFallback(string[] ids, string fallback)
        {
            return ids != null && ids.Length > 0 ? ids[0] : fallback;
        }

        private static bool ContainsId(string current, string[] ids)
        {
            if (ids == null || ids.Length == 0 || string.IsNullOrWhiteSpace(current))
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

        private static string FormatId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return "None";
            }

            string[] words = id.Replace('-', '_').Split('_');
            for (int i = 0; i < words.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(words[i]))
                {
                    continue;
                }

                string word = words[i].ToLowerInvariant();
                words[i] = char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word.Substring(1) : string.Empty);
            }

            return string.Join(" ", words);
        }
    }
}
