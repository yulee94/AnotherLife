using System;
using System.Collections;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.FirstUserIdentity;
using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public class ChampionCustomizationController : MonoBehaviour
    {
        private Renderer[] _renderers;
        private CharacterCustomizationCatalogData _catalog;
        private bool _catalogLoadStarted;
        private bool _useExternalPresentation;
        private ChampionCustomizationState _externalPresentation;

        private static readonly string[] BodyPresets =
        {
            "average", "slim", "broad", "tall", "stout", "duelist", "statuesque", "massive", "compact"
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
            "none", "scar", "warpaint", "realm_mark", "rune", "tattoo", "beard", "duelist_scar", "ash_mask"
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
            if (_useExternalPresentation)
            {
                ApplyState(_externalPresentation);
            }
            else
            {
                ApplySavedCustomization();
            }

            if (_catalog == null && !_catalogLoadStarted)
            {
                StartCoroutine(ApplySharedCatalogAsync());
            }
        }

        public void ApplySavedCustomization()
        {
            if (_useExternalPresentation)
            {
                ApplyState(_externalPresentation);
                return;
            }

            ApplyState(GetPresentationSnapshot());
        }

        public void ApplyPresentation(ChampionCustomizationState state)
        {
            if (state == null)
            {
                return;
            }

            _useExternalPresentation = true;
            _externalPresentation = state;
            ApplyState(state);
        }

        private void ApplyState(ChampionCustomizationState state)
        {
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
            SetPartActive("Cape_Rune", state.CapeEnabled && ShouldShowCapeRunes(state.ArmorStyleId));
            SetPartActive("Helmet", state.HelmetEnabled);
            GetComponent<ProceduralChampionMotion>()?.Rebind();
            GetComponent<ProceduralChampionSurfaceResponse>()?.Rebind();
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

        public bool ApplyAppearancePreset(string presetId)
        {
            var state = GetState();
            if (state == null || string.IsNullOrWhiteSpace(presetId))
            {
                return false;
            }

            if (!TryApplyCatalogForgePreset(state, presetId))
            {
                switch (presetId)
                {
                    case "vanguard":
                        ApplyVanguardPreset(state);
                        break;
                    case "arcanist":
                        ApplyArcanistPreset(state);
                        break;
                    case "nightblade":
                        ApplyNightbladePreset(state);
                        break;
                    case "dreadknight":
                        ApplyDreadknightPreset(state);
                        break;
                    case "oracle":
                        ApplyOraclePreset(state);
                        break;
                    case "duelist":
                        ApplyDuelistPreset(state);
                        break;
                    case "inquisitor":
                        ApplyInquisitorPreset(state);
                        break;
                    case "warden":
                        ApplyWardenPreset(state);
                        break;
                    case "spellblade":
                        ApplySpellbladePreset(state);
                        break;
                    default:
                        return false;
                }
            }

            SaveAndApply();
            return true;
        }

        public string GetAppearanceSummary()
        {
            var state = GetPresentationSnapshot();
            if (state == null)
            {
                return "Appearance unavailable";
            }

            NormalizeState(state);
            return
                $"{GetProfileLabel(state)} | {FormatId(state.BodyPresetId)}\n" +
                $"{FormatId(state.ArmorStyleId)} / {FormatId(state.HairStyleId)} / {FormatId(state.FaceMarkId)}\n" +
                $"{FormatId(state.WeaponStyleId)} + {FormatId(state.OffhandStyleId)} / C:{(state.CapeEnabled ? "On" : "Off")} H:{(state.HelmetEnabled ? "On" : "Off")}";
        }

        public Color GetPrimaryColor()
        {
            var state = GetPresentationSnapshot();
            return state == null ? PrimaryPalette[0] : new Color(state.PrimaryR, state.PrimaryG, state.PrimaryB);
        }

        public Color GetHairColor()
        {
            var state = GetPresentationSnapshot();
            return state == null ? HairPalette[0] : new Color(state.HairR, state.HairG, state.HairB);
        }

        public Color GetSkinColor()
        {
            var state = GetPresentationSnapshot();
            return state == null ? SkinPalette[0] : new Color(state.SkinR, state.SkinG, state.SkinB);
        }

        public Color GetEyeColor()
        {
            var state = GetPresentationSnapshot();
            return state == null ? EyePalette[0] : new Color(state.EyeR, state.EyeG, state.EyeB);
        }

        public Color GetAccentColor()
        {
            var state = GetPresentationSnapshot();
            return state == null ? AccentPalette[0] : new Color(state.AccentR, state.AccentG, state.AccentB);
        }

        private static void ApplyVanguardPreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "broad";
            state.HairStyleId = "short";
            state.ArmorStyleId = "warmaster_plate";
            state.FaceMarkId = "scar";
            state.WeaponStyleId = "sword";
            state.OffhandStyleId = "shield";
            state.CapeEnabled = true;
            state.HelmetEnabled = true;
            ApplyPresetColors(
                state,
                new Color(0.22f, 0.27f, 0.34f),
                new Color(0.07f, 0.055f, 0.045f),
                new Color(0.64f, 0.48f, 0.36f),
                new Color(0.86f, 0.62f, 0.24f),
                new Color(0.92f, 0.64f, 0.20f));
        }

        private static void ApplyArcanistPreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "tall";
            state.HairStyleId = "long";
            state.ArmorStyleId = "arcane_robes";
            state.FaceMarkId = "rune";
            state.WeaponStyleId = "staff";
            state.OffhandStyleId = "tome";
            state.CapeEnabled = true;
            state.HelmetEnabled = false;
            ApplyPresetColors(
                state,
                new Color(0.08f, 0.14f, 0.32f),
                new Color(0.72f, 0.74f, 0.82f),
                new Color(0.68f, 0.52f, 0.44f),
                new Color(0.40f, 0.82f, 1.00f),
                new Color(0.26f, 0.78f, 1.00f));
        }

        private static void ApplyNightbladePreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "slim";
            state.HairStyleId = "topknot";
            state.ArmorStyleId = "assassin_leathers";
            state.FaceMarkId = "tattoo";
            state.WeaponStyleId = "bow";
            state.OffhandStyleId = "dagger";
            state.CapeEnabled = false;
            state.HelmetEnabled = false;
            ApplyPresetColors(
                state,
                new Color(0.10f, 0.095f, 0.12f),
                new Color(0.18f, 0.035f, 0.055f),
                new Color(0.50f, 0.38f, 0.34f),
                new Color(0.84f, 0.18f, 0.14f),
                new Color(0.78f, 0.12f, 0.18f));
        }

        private static void ApplyDreadknightPreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "massive";
            state.HairStyleId = "mohawk";
            state.ArmorStyleId = "warmaster_plate";
            state.FaceMarkId = "ash_mask";
            state.WeaponStyleId = "hammer";
            state.OffhandStyleId = "shield";
            state.CapeEnabled = true;
            state.HelmetEnabled = true;
            ApplyPresetColors(
                state,
                new Color(0.055f, 0.060f, 0.070f),
                new Color(0.16f, 0.16f, 0.18f),
                new Color(0.46f, 0.34f, 0.32f),
                new Color(0.95f, 0.18f, 0.08f),
                new Color(0.94f, 0.12f, 0.08f));
        }

        private static void ApplyOraclePreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "statuesque";
            state.HairStyleId = "braid";
            state.ArmorStyleId = "arcane_robes";
            state.FaceMarkId = "realm_mark";
            state.WeaponStyleId = "staff";
            state.OffhandStyleId = "orb";
            state.CapeEnabled = true;
            state.HelmetEnabled = false;
            ApplyPresetColors(
                state,
                new Color(0.82f, 0.78f, 0.66f),
                new Color(0.88f, 0.84f, 0.72f),
                new Color(0.78f, 0.62f, 0.52f),
                new Color(0.58f, 1.00f, 0.82f),
                new Color(0.38f, 1.00f, 0.74f));
        }

        private static void ApplyDuelistPreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "duelist";
            state.HairStyleId = "short";
            state.ArmorStyleId = "light_scout";
            state.FaceMarkId = "duelist_scar";
            state.WeaponStyleId = "sword";
            state.OffhandStyleId = "dagger";
            state.CapeEnabled = false;
            state.HelmetEnabled = false;
            ApplyPresetColors(
                state,
                new Color(0.18f, 0.21f, 0.24f),
                new Color(0.42f, 0.24f, 0.11f),
                new Color(0.66f, 0.48f, 0.36f),
                new Color(0.95f, 0.64f, 0.20f),
                new Color(0.92f, 0.54f, 0.16f));
        }

        private static void ApplyInquisitorPreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "tall";
            state.HairStyleId = "short";
            state.ArmorStyleId = "heavy_plate";
            state.FaceMarkId = "realm_mark";
            state.WeaponStyleId = "sword";
            state.OffhandStyleId = "tome";
            state.CapeEnabled = true;
            state.HelmetEnabled = true;
            ApplyPresetColors(
                state,
                new Color(0.12f, 0.13f, 0.14f),
                new Color(0.08f, 0.06f, 0.04f),
                new Color(0.72f, 0.56f, 0.42f),
                new Color(0.95f, 0.64f, 0.20f),
                new Color(0.92f, 0.54f, 0.16f));
        }

        private static void ApplyWardenPreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "broad";
            state.HairStyleId = "braid";
            state.ArmorStyleId = "heavy_plate";
            state.FaceMarkId = "warpaint";
            state.WeaponStyleId = "axe";
            state.OffhandStyleId = "shield";
            state.CapeEnabled = true;
            state.HelmetEnabled = false;
            ApplyPresetColors(
                state,
                new Color(0.12f, 0.26f, 0.18f),
                new Color(0.55f, 0.36f, 0.16f),
                new Color(0.55f, 0.38f, 0.26f),
                new Color(0.58f, 1.00f, 0.82f),
                new Color(0.38f, 1.00f, 0.74f));
        }

        private static void ApplySpellbladePreset(ChampionCustomizationState state)
        {
            state.BodyPresetId = "duelist";
            state.HairStyleId = "long";
            state.ArmorStyleId = "arcane_robes";
            state.FaceMarkId = "rune";
            state.WeaponStyleId = "sword";
            state.OffhandStyleId = "orb";
            state.CapeEnabled = true;
            state.HelmetEnabled = false;
            ApplyPresetColors(
                state,
                new Color(0.08f, 0.12f, 0.22f),
                new Color(0.80f, 0.82f, 0.90f),
                new Color(0.64f, 0.50f, 0.46f),
                new Color(0.40f, 0.82f, 1.00f),
                new Color(0.68f, 0.28f, 0.96f));
        }

        private static void ApplyPresetColors(
            ChampionCustomizationState state,
            Color primary,
            Color hair,
            Color skin,
            Color eye,
            Color accent)
        {
            state.PrimaryR = primary.r;
            state.PrimaryG = primary.g;
            state.PrimaryB = primary.b;
            state.HairR = hair.r;
            state.HairG = hair.g;
            state.HairB = hair.b;
            state.SkinR = skin.r;
            state.SkinG = skin.g;
            state.SkinB = skin.b;
            state.EyeR = eye.r;
            state.EyeG = eye.g;
            state.EyeB = eye.b;
            state.AccentR = accent.r;
            state.AccentG = accent.g;
            state.AccentB = accent.b;
        }

        private bool TryApplyCatalogForgePreset(ChampionCustomizationState state, string presetId)
        {
            var presets = _catalog?.forgePresets;
            if (presets == null)
            {
                return false;
            }

            foreach (var preset in presets)
            {
                if (preset == null || preset.id != presetId)
                {
                    continue;
                }

                state.BodyPresetId = preset.bodyPresetId;
                state.HairStyleId = preset.hairStyleId;
                state.ArmorStyleId = preset.armorStyleId;
                state.FaceMarkId = preset.faceMarkId;
                state.WeaponStyleId = preset.weaponStyleId;
                state.OffhandStyleId = preset.offhandStyleId;
                state.CapeEnabled = preset.capeEnabled;
                state.HelmetEnabled = preset.helmetEnabled;
                ApplyPresetColors(
                    state,
                    ReadPresetColor(preset.primaryColor, PrimaryPalette[0]),
                    ReadPresetColor(preset.hairColor, HairPalette[0]),
                    ReadPresetColor(preset.skinColor, SkinPalette[0]),
                    ReadPresetColor(preset.eyeColor, EyePalette[0]),
                    ReadPresetColor(preset.accentColor, AccentPalette[0]));
                return true;
            }

            return false;
        }

        private static Color ReadPresetColor(float[] rgb, Color fallback)
        {
            if (rgb == null || rgb.Length < 3)
            {
                return fallback;
            }

            return new Color(
                Mathf.Clamp01(rgb[0]),
                Mathf.Clamp01(rgb[1]),
                Mathf.Clamp01(rgb[2]));
        }

        private static string GetProfileLabel(ChampionCustomizationState state)
        {
            if (state.FaceMarkId == "ash_mask" && state.WeaponStyleId == "hammer")
            {
                return "Dreadknight";
            }

            if (state.WeaponStyleId == "staff" && state.OffhandStyleId == "orb")
            {
                return "Oracle";
            }

            if (state.BodyPresetId == "duelist" || (state.WeaponStyleId == "sword" && state.OffhandStyleId == "dagger"))
            {
                return "Duelist";
            }

            if (state.ArmorStyleId == "arcane_robes" || state.WeaponStyleId == "staff" || state.OffhandStyleId == "tome" || state.OffhandStyleId == "orb")
            {
                return "Arcanist";
            }

            if (state.ArmorStyleId == "assassin_leathers" || state.OffhandStyleId == "dagger" || state.WeaponStyleId == "bow")
            {
                return "Nightblade";
            }

            if (state.ArmorStyleId == "warmaster_plate" || state.ArmorStyleId == "heavy_plate" || state.OffhandStyleId == "shield")
            {
                return "Vanguard";
            }

            return "Custom";
        }

        private void SaveAndApply()
        {
            ServiceLocator.Get<ISaveGameService>().Save();
            PersistConfirmedIdentity();
            ApplySavedCustomization();
        }

        private static void PersistConfirmedIdentity()
        {
            if (!ServiceLocator.TryGet(out ISaveGameService saveGameService) ||
                saveGameService.CurrentSave == null)
            {
                return;
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(saveGameService.CurrentSave);
            if (!snapshot.ClassFamily.HasValue ||
                !FirstUserIdentityDerivation.IsSupportedRealm(snapshot.Realm))
            {
                return;
            }

            MvpLoopSaveAuthority.TryCommit(
                saveGameService,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    snapshot.Realm,
                    snapshot.ClassFamily.Value,
                    true,
                    snapshot.LastResultId,
                    snapshot.LastBuildId,
                    snapshot.LastBuildLevel));
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
            if (!TryGetMutableSave(
                    out _,
                    out SaveGameData save))
            {
                return null;
            }

            return save?.ChampionCustomization;
        }

        private static bool TryGetMutableSave(
            out ISaveGameService saveGameService,
            out SaveGameData save)
        {
            saveGameService = null;
            save = null;
            try
            {
                saveGameService = ServiceLocator.Get<ISaveGameService>();
            }
            catch
            {
                return false;
            }

            return AL.Services.Local.ProfileMutationContainment
                .TryGetMutableSave(
                    saveGameService,
                    AL.Services.Local.ProfileMutationSurfaceIds
                        .ChampionCustomization,
                    out save);
        }

        private static ChampionCustomizationState GetPresentationSnapshot()
        {
            ChampionCustomizationState source;
            try
            {
                source = ServiceLocator.Get<ISaveGameService>()
                    .CurrentSave?.ChampionCustomization;
            }
            catch
            {
                return null;
            }

            if (source == null)
            {
                return null;
            }

            return new ChampionCustomizationState
            {
                BodyPresetId = source.BodyPresetId,
                HairStyleId = source.HairStyleId,
                ArmorStyleId = source.ArmorStyleId,
                FaceMarkId = source.FaceMarkId,
                WeaponStyleId = source.WeaponStyleId,
                OffhandStyleId = source.OffhandStyleId,
                PrimaryR = source.PrimaryR,
                PrimaryG = source.PrimaryG,
                PrimaryB = source.PrimaryB,
                HairR = source.HairR,
                HairG = source.HairG,
                HairB = source.HairB,
                SkinR = source.SkinR,
                SkinG = source.SkinG,
                SkinB = source.SkinB,
                EyeR = source.EyeR,
                EyeG = source.EyeG,
                EyeB = source.EyeB,
                AccentR = source.AccentR,
                AccentG = source.AccentG,
                AccentB = source.AccentB,
                CapeEnabled = source.CapeEnabled,
                HelmetEnabled = source.HelmetEnabled,
                ClassFamilyId = source.ClassFamilyId,
                IdentityConfirmed = source.IdentityConfirmed,
                LastResultId = source.LastResultId
            };
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
                "duelist" => new Vector3(0.94f, 1.08f, 0.92f),
                "statuesque" => new Vector3(1.02f, 1.24f, 0.98f),
                "massive" => new Vector3(1.24f, 1.04f, 1.14f),
                "compact" => new Vector3(1.02f, 0.86f, 1.02f),
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
            SetExactPartActive("Hair_Short_Fade_L", isShort);
            SetExactPartActive("Hair_Short_Fade_R", isShort);
            SetExactPartActive("Hair_Long", isLong);
            SetExactPartActive("Hair_Long_Side_L", isLong);
            SetExactPartActive("Hair_Long_Side_R", isLong);
            SetExactPartActive("Hair_Long_Fold_L", isLong);
            SetExactPartActive("Hair_Long_Fold_R", isLong);
            SetExactPartActive("Hair_Braid", isBraid);
            SetExactPartActive("Hair_Braid_Band", isBraid);
            SetPartActive("Hair_Braid_Segment", isBraid);
            SetExactPartActive("Hair_Mohawk", isMohawk);
            SetExactPartActive("Hair_Mohawk_Tip", isMohawk);
            SetExactPartActive("Hair_Mohawk_Ridge", isMohawk);
            SetExactPartActive("Hair_Topknot", isTopknot);
            SetExactPartActive("Hair_Topknot_Tail", isTopknot);
            SetExactPartActive("Hair_Topknot_Band", isTopknot);
            SetExactPartActive("Hair_Topknot_Pin", isTopknot);
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
            SetPartActive("Armor_Bevel", !isRobe);
            SetPartActive("Armor_SidePlate", !isRobe);
            SetExactPartActive("Armor_BackPlate", !isRobe);
            SetExactPartActive("Armor_BackSpine", !isRobe);
            SetExactPartActive("Armor_Undersuit_Seam", true);
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
            SetPartActive("Armor_Rib", !isRobe);
            SetPartActive("Armor_SternumPlate", !isRobe);
            SetPartActive("Armor_Etching", !isRobe);
            SetPartActive("Armor_Rivet", isHeavy || isWarmaster);
            SetPartActive("Armor_ForearmBlade", isAssassin || isWarmaster);
            SetExactPartActive("Armor_CenterGem", true);
            SetPartActive("RobeTrim", isRobe);
            SetExactPartActive("Arcane_FocusHalo", isRobe);
            SetExactPartActive("Assassin_Mask", isAssassin);
            SetPartActive("Belt", true);
            SetPartActive("PlateSkirt", !isLight || isWarmaster);
            SetPartActive("Knee", !isRobe);
            SetPartActive("Shoulder", !isLight);
            SetPartActive("Shoulder_Layer", !isLight);
            SetPartActive("Shoulder_Edge", isHeavy || isWarmaster);
            SetPartActive("Shoulder_Ridge", isHeavy || isWarmaster);
            SetPartActive("ShoulderSpike", isWarmaster);
            SetPartActive("BackAttachment", isWarmaster);
            SetPartActive("Belt_Pouch", !isRobe);
            SetExactPartActive("Belt_CommandSeal", true);
            SetPartActive("Glove_Knuckle", true);
            SetPartActive("Glove_Thumb", true);
            SetPartActive("Knee_Ridge", !isRobe);
            SetPartActive("Boot_Heel", true);
            SetPartActive("Boot_Tread", true);
            SetPartActive("PlateSkirt_Side", !isLight || isWarmaster);
            SetPartActive("ThighStrap", !isRobe);
            SetPartActive("BootStrap", !isRobe);
            SetPartActive("Cape_Rune", isRobe || isWarmaster);
            SetPartActive("Prestige_Mantle", isHeavy || isWarmaster);
            SetPartActive("Prestige_Sash", !isRobe);
            SetPartActive("Prestige_FieldMedal", !isRobe && !isAssassin);
            SetPartActive("Prestige_BattleChain", !isRobe);
            SetPartActive("Prestige_WaistWrap", true);
            SetPartActive("Back_Harness", true);

            SetPartScale("ChestArmor", isRobe ? new Vector3(0.78f, 0.82f, 0.28f) : isAssassin ? new Vector3(0.84f, 0.64f, 0.26f) : isHeavy ? new Vector3(1.05f, 0.82f, 0.38f) : new Vector3(0.92f, 0.74f, 0.32f));
            SetPartScale("Armor_Bevel_Top", isHeavy ? new Vector3(0.58f, 0.048f, 0.040f) : new Vector3(0.50f, 0.040f, 0.036f));
            SetPartScale("Armor_Pectoral_L", isHeavy ? new Vector3(0.34f, 0.32f, 0.09f) : new Vector3(0.30f, 0.28f, 0.08f));
            SetPartScale("Armor_Pectoral_R", isHeavy ? new Vector3(0.34f, 0.32f, 0.09f) : new Vector3(0.30f, 0.28f, 0.08f));
            SetPartScale("Shoulder_L", isHeavy ? new Vector3(0.34f, 0.26f, 0.34f) : new Vector3(0.26f, 0.20f, 0.28f));
            SetPartScale("Shoulder_R", isHeavy ? new Vector3(0.34f, 0.26f, 0.34f) : new Vector3(0.26f, 0.20f, 0.28f));
            SetPartScale("Shoulder_Layer_L", isWarmaster ? new Vector3(0.42f, 0.085f, 0.28f) : new Vector3(0.34f, 0.075f, 0.24f));
            SetPartScale("Shoulder_Layer_R", isWarmaster ? new Vector3(0.42f, 0.085f, 0.28f) : new Vector3(0.34f, 0.075f, 0.24f));
            SetPartScale("Shoulder_Ridge_L", isWarmaster ? new Vector3(0.40f, 0.070f, 0.14f) : new Vector3(0.32f, 0.055f, 0.12f));
            SetPartScale("Shoulder_Ridge_R", isWarmaster ? new Vector3(0.40f, 0.070f, 0.14f) : new Vector3(0.32f, 0.055f, 0.12f));
            SetPartScale("Glove_L", isAssassin ? new Vector3(0.14f, 0.30f, 0.14f) : new Vector3(0.18f, 0.24f, 0.18f));
            SetPartScale("Glove_R", isAssassin ? new Vector3(0.14f, 0.30f, 0.14f) : new Vector3(0.18f, 0.24f, 0.18f));
            SetPartScale("PlateSkirt_Front", isWarmaster ? new Vector3(0.52f, 0.50f, 0.07f) : new Vector3(0.42f, 0.44f, 0.06f));
        }

        private static bool ShouldShowCapeRunes(string armorStyleId)
        {
            return armorStyleId == "arcane_robes" || armorStyleId == "warmaster_plate";
        }

        private void ApplyFaceMark(string faceMarkId)
        {
            SetExactPartActive("FaceMark", false);
            SetExactPartActive("FaceMark_Secondary", false);
            SetExactPartActive("FaceMark_Tertiary", false);
            SetPartActive("FacialHair", false);

            if (faceMarkId == "none")
            {
                return;
            }

            switch (faceMarkId)
            {
                case "scar":
                    SetExactPartActive("FaceMark", true);
                    SetPartTransform("FaceMark", new Vector3(-0.08f, 0.62f, 0.49f), new Vector3(0.035f, 0.28f, 0.025f), new Vector3(0f, 0f, 24f));
                    break;
                case "realm_mark":
                    SetExactPartActive("FaceMark", true);
                    SetPartTransform("FaceMark", new Vector3(0f, 0.68f, 0.49f), new Vector3(0.13f, 0.13f, 0.025f), new Vector3(0f, 0f, 45f));
                    break;
                case "rune":
                    SetExactPartActive("FaceMark", true);
                    SetPartTransform("FaceMark", new Vector3(0.08f, 0.66f, 0.49f), new Vector3(0.12f, 0.12f, 0.025f), new Vector3(0f, 0f, 0f));
                    break;
                case "tattoo":
                    SetExactPartActive("FaceMark", true);
                    SetPartTransform("FaceMark", new Vector3(0f, 0.58f, 0.49f), new Vector3(0.30f, 0.030f, 0.025f), new Vector3(0f, 0f, -18f));
                    break;
                case "beard":
                    SetPartActive("FacialHair", true);
                    SetPartTransform("FacialHair_Mustache", new Vector3(0f, 0.57f, 0.50f), new Vector3(0.28f, 0.040f, 0.030f), Vector3.zero);
                    SetPartTransform("FacialHair_Chin", new Vector3(0f, 0.45f, 0.42f), new Vector3(0.24f, 0.11f, 0.055f), Vector3.zero);
                    SetPartTransform("FacialHair_Jaw_L", new Vector3(-0.18f, 0.50f, 0.42f), new Vector3(0.075f, 0.18f, 0.050f), new Vector3(0f, 0f, -10f));
                    SetPartTransform("FacialHair_Jaw_R", new Vector3(0.18f, 0.50f, 0.42f), new Vector3(0.075f, 0.18f, 0.050f), new Vector3(0f, 0f, 10f));
                    break;
                case "duelist_scar":
                    SetExactPartActive("FaceMark", true);
                    SetExactPartActive("FaceMark_Secondary", true);
                    SetPartTransform("FaceMark", new Vector3(-0.10f, 0.68f, 0.494f), new Vector3(0.030f, 0.24f, 0.023f), new Vector3(0f, 0f, -20f));
                    SetPartTransform("FaceMark_Secondary", new Vector3(0.14f, 0.61f, 0.494f), new Vector3(0.028f, 0.18f, 0.023f), new Vector3(0f, 0f, 24f));
                    break;
                case "ash_mask":
                    SetExactPartActive("FaceMark", true);
                    SetExactPartActive("FaceMark_Secondary", true);
                    SetExactPartActive("FaceMark_Tertiary", true);
                    SetPartTransform("FaceMark", new Vector3(0f, 0.59f, 0.496f), new Vector3(0.36f, 0.050f, 0.024f), Vector3.zero);
                    SetPartTransform("FaceMark_Secondary", new Vector3(-0.13f, 0.71f, 0.496f), new Vector3(0.14f, 0.024f, 0.024f), new Vector3(0f, 0f, -8f));
                    SetPartTransform("FaceMark_Tertiary", new Vector3(0.13f, 0.71f, 0.496f), new Vector3(0.14f, 0.024f, 0.024f), new Vector3(0f, 0f, 8f));
                    break;
                default:
                    SetExactPartActive("FaceMark", true);
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
            SetExactPartActive("Sword_Edge_L", isSword);
            SetExactPartActive("Sword_Edge_R", isSword);
            SetExactPartActive("Sword_Fuller", isSword);
            SetExactPartActive("Sword_Gem", isSword);
            SetExactPartActive("Sword_CoreLine", isSword);
            SetExactPartActive("Weapon_Head", isAxe);
            SetExactPartActive("Axe_Blade_L", isAxe);
            SetExactPartActive("Axe_Blade_R", isAxe);
            SetExactPartActive("Axe_Edge_L", isAxe);
            SetExactPartActive("Axe_Edge_R", isAxe);
            SetExactPartActive("Axe_Rivet_L", isAxe);
            SetExactPartActive("Axe_Rivet_R", isAxe);
            SetExactPartActive("Hammer_Face", isHammer);
            SetExactPartActive("Hammer_Rune", isHammer);
            SetExactPartActive("Hammer_SideCap_L", isHammer);
            SetExactPartActive("Hammer_SideCap_R", isHammer);
            SetExactPartActive("Hammer_ImpactCore", isHammer);
            SetExactPartActive("Staff_Crystal", isStaff);
            SetExactPartActive("Staff_Ring", isStaff);
            SetExactPartActive("Staff_RuneBand", isStaff);
            SetExactPartActive("Staff_OrbitStone_L", isStaff);
            SetExactPartActive("Staff_OrbitStone_R", isStaff);
            SetPartActive("Bow_Limb", isBow);
            SetExactPartActive("Bow_GripWrap", isBow);
            SetExactPartActive("Bow_String", isBow);
            SetExactPartActive("Bow_ArrowNock", isBow);
            SetExactPartActive("Bow_ArrowShaft", isBow);
            SetExactPartActive("Bow_ArrowHead", isBow);
            SetExactPartActive("Bow_Fletching", isBow);
            SetPartActive("Back_Scabbard", isSword);
            SetPartActive("Back_Quiver", isBow);
            SetPartActive("Back_QuiverArrow", isBow);
            SetPartActive("Back_Relic", isStaff);
            SetPartActive("Back_HammerHook", isAxe || isHammer);

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
            SetExactPartActive("Shield_Rim_Top", offhandStyleId == "shield");
            SetExactPartActive("Shield_Rim_Bottom", offhandStyleId == "shield");
            SetExactPartActive("Shield_Rivet_Top", offhandStyleId == "shield");
            SetExactPartActive("Shield_Rivet_Bottom", offhandStyleId == "shield");
            SetExactPartActive("Shield_Boss", offhandStyleId == "shield");
            SetExactPartActive("Shield_Scar", offhandStyleId == "shield");
            SetExactPartActive("Orb_Off", offhandStyleId == "orb");
            SetExactPartActive("Orb_Ring", offhandStyleId == "orb");
            SetExactPartActive("Orb_Core", offhandStyleId == "orb");
            SetExactPartActive("Weapon_Off", offhandStyleId == "dagger");
            SetExactPartActive("Dagger_Blade", offhandStyleId == "dagger");
            SetExactPartActive("Dagger_Guard", offhandStyleId == "dagger");
            SetExactPartActive("Dagger_Edge", offhandStyleId == "dagger");
            SetExactPartActive("Tome_Off", offhandStyleId == "tome");
            SetExactPartActive("Tome_Page", offhandStyleId == "tome");
            SetExactPartActive("Tome_Clasp", offhandStyleId == "tome");
            SetExactPartActive("Tome_Rune", offhandStyleId == "tome");
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
                bool isSkinShadow = objectName.Contains("skin_shadow");
                bool isArmorShadow = objectName.Contains("armor_shadow") || objectName.Contains("battlewear");
                bool isLip = objectName.Contains("lowerlip");
                bool isHair = objectName.Contains("hair") || objectName.Contains("brow");
                bool isSkin = objectName.Contains("skin") || objectName.Contains("ear");
                bool isEye = objectName.Contains("eye");
                bool isBrightMetal = objectName.Contains("blade") ||
                                     objectName.Contains("edge") ||
                                     objectName.Contains("arrowhead") ||
                                     objectName.Contains("arrowshaft") ||
                                     objectName.Contains("quiverarrow");
                bool isEyeGlint = objectName.Contains("eye_glint");
                bool isParchment = objectName.Contains("tome_page");
                bool isAccent = objectName.Contains("facemark") ||
                                objectName.Contains("backattachment") ||
                                objectName.Contains("arcane_focus") ||
                                objectName.Contains("orb") ||
                                objectName.Contains("backspine") ||
                                objectName.Contains("commandseal") ||
                                objectName.Contains("coreline") ||
                                objectName.Contains("impactcore") ||
                                objectName.Contains("reliccore") ||
                                objectName.Contains("orbitstone") ||
                                objectName.Contains("fletching") ||
                                objectName.Contains("chain") ||
                                objectName.Contains("pin") ||
                                objectName.Contains("sashplate") ||
                                objectName.Contains("fieldmedal") ||
                                objectName.Contains("harnessring") ||
                                objectName.Contains("scabbard_rune") ||
                                objectName.Contains("quiver_rim") ||
                                objectName.Contains("hammerhook_rivet") ||
                                objectName.Contains("mantle_trim") ||
                                objectName.Contains("knuckle") ||
                                objectName.Contains("trim") ||
                                objectName.Contains("etching") ||
                                objectName.Contains("gem") ||
                                objectName.Contains("rune") ||
                                objectName.Contains("ridge") ||
                                objectName.Contains("rim") ||
                                objectName.Contains("rivet") ||
                                objectName.Contains("tome_clasp") ||
                                objectName.Contains("belt_buckle") ||
                                objectName.Contains("clasp") ||
                                objectName.Contains("crystal") ||
                                objectName.Contains("crest") ||
                                objectName.Contains("spike") ||
                                objectName.Contains("guard");
                bool isFabric = objectName.Contains("cape") ||
                                objectName.Contains("cloth") ||
                                objectName.Contains("mask") ||
                                objectName.Contains("robe") ||
                                objectName.Contains("mantle") ||
                                objectName.Contains("sash") ||
                                objectName.Contains("waistwrap") ||
                                objectName.Contains("tassel") ||
                                objectName.Contains("undersuit") ||
                                objectName.Contains("hood") ||
                                objectName.Contains("tome");
                bool isLeather = objectName.Contains("belt") ||
                                  objectName.Contains("boot") ||
                                  objectName.Contains("pouch") ||
                                  objectName.Contains("harness") ||
                                  objectName.Contains("scabbard") ||
                                  objectName.Contains("quiver") ||
                                  objectName.Contains("tread") ||
                                  objectName.Contains("heel") ||
                                  objectName.Contains("strap") ||
                                  objectName.Contains("grip") ||
                                  objectName.Contains("bow_limb");
                bool isMetal = objectName.Contains("helmet") ||
                               objectName.Contains("armor") ||
                               objectName.Contains("shoulder") ||
                               objectName.Contains("sternum") ||
                               objectName.Contains("glove") ||
                               objectName.Contains("thumb") ||
                               objectName.Contains("forearm") ||
                               objectName.Contains("boot") ||
                               objectName.Contains("weapon") ||
                               objectName.Contains("shield") ||
                               objectName.Contains("sidecap") ||
                               objectName.Contains("relicframe") ||
                               objectName.Contains("hammerhook") ||
                               objectName.Contains("boss") ||
                               objectName.Contains("scar") ||
                               objectName.Contains("knee") ||
                               objectName.Contains("belt");

                Color targetColor = isEyeGlint
                    ? Color.Lerp(Color.white, eye, 0.18f)
                    : isSkinShadow
                    ? Color.Lerp(skin, Color.black, 0.38f)
                    : isArmorShadow
                        ? Color.Lerp(primary, Color.black, 0.46f)
                        : isLip
                            ? Color.Lerp(skin, new Color(0.58f, 0.32f, 0.30f), 0.24f)
                            : isParchment
                                ? Color.Lerp(new Color(0.82f, 0.76f, 0.58f), accent, 0.12f)
                                : isBrightMetal
                                    ? Color.Lerp(Color.white, primary, 0.16f)
                                    : isHair
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

                float metallic = isArmorShadow ? 0.30f : isBrightMetal ? 0.58f : isMetal ? 0.46f : isAccent ? 0.26f : isLeather ? 0.06f : 0f;
                float smoothness = isArmorShadow ? 0.38f : isBrightMetal ? 0.78f : isEyeGlint ? 0.88f : isEye || isAccent ? 0.72f : isMetal ? 0.60f : isSkin ? 0.30f : isHair ? 0.36f : 0.46f;
                float emissionStrength = isEye
                    ? isEyeGlint ? 0.42f : 0.22f
                    : objectName.Contains("orb") || objectName.Contains("crystal") || objectName.Contains("backattachment_core") || objectName.Contains("reliccore") || objectName.Contains("gem") || objectName.Contains("rune") || objectName.Contains("coreline") || objectName.Contains("impactcore") || objectName.Contains("orbitstone") || objectName.Contains("commandseal")
                        ? 0.46f
                        : objectName.Contains("arcane_focus")
                            ? 0.34f
                        : objectName.Contains("facemark") || objectName.Contains("trim") || objectName.Contains("crest") || objectName.Contains("etching")
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
