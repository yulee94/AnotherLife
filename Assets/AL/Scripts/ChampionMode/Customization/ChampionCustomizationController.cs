using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.ChampionMode.Customization
{
    public class ChampionCustomizationController : MonoBehaviour
    {
        private Renderer[] _renderers;

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
            _renderers = GetComponentsInChildren<Renderer>(true);
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

        private void ApplyColors(Color primary, Color hair)
        {
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                bool isHair = renderer.gameObject.name.ToLowerInvariant().Contains("hair");
                renderer.material.color = isHair ? hair : primary;
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
    }
}

