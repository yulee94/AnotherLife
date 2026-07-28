using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    /// <summary>
    /// Bounded Stonehold operational activity. The completed structure remains
    /// still while the forge, bellows, and hammer provide short functional cues.
    /// </summary>
    public sealed class StoneholdWorkshopStableActivity :
        MonoBehaviour,
        IArchitectureConstructionActivity
    {
        private const float ForgeStart = 9.0f;
        private const float BellowsStart = 10.2f;
        private const float BellowsDuration = 0.9f;
        private const float HammerStart = 12.0f;
        private const float HammerDuration = 0.75f;

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform bellows;
        [SerializeField] private Transform hammer;
        [SerializeField] private Renderer[] forgeRenderers = new Renderer[0];
        [SerializeField] private Light forgeLight;
        [SerializeField] private Color restingEmission =
            new Color(0.18f, 0.035f, 0.01f);
        [SerializeField] private Color activeEmission =
            new Color(2.1f, 0.38f, 0.045f);

        private Vector3 bellowsScale;
        private Quaternion hammerRotation;
        private MaterialPropertyBlock propertyBlock;
        private bool cacheReady;

        public bool HasBellows => bellows != null;
        public bool HasHammer => hammer != null;
        public int ForgeRendererCount => forgeRenderers?.Length ?? 0;

        public void Configure(
            Transform bellowsTransform,
            Transform hammerTransform,
            Renderer[] emissionRenderers,
            Light localizedForgeLight)
        {
            bellows = bellowsTransform;
            hammer = hammerTransform;
            forgeRenderers = emissionRenderers ?? new Renderer[0];
            forgeLight = localizedForgeLight;
            cacheReady = false;
            EnsureCache();
        }

        public void EvaluateActivity(float presentationTime, bool reducedMotion)
        {
            EnsureCache();

            float forgeStrength = presentationTime < ForgeStart
                ? 0f
                : 0.55f + Mathf.Sin(presentationTime * 1.7f) * 0.08f;
            if (reducedMotion)
            {
                forgeStrength = presentationTime < ForgeStart ? 0f : 0.45f;
            }

            ApplyEmission(Color.Lerp(restingEmission, activeEmission, forgeStrength));
            if (forgeLight != null)
            {
                forgeLight.intensity = forgeStrength * (reducedMotion ? 0.55f : 1.05f);
                forgeLight.enabled = forgeStrength > 0.01f;
            }

            float bellowsProgress = WindowProgress(
                presentationTime,
                BellowsStart,
                BellowsDuration);
            if (bellows != null)
            {
                float compression = reducedMotion
                    ? 0f
                    : Mathf.Sin(bellowsProgress * Mathf.PI) * 0.22f;
                bellows.localScale = new Vector3(
                    bellowsScale.x,
                    bellowsScale.y,
                    bellowsScale.z * (1f - compression));
            }

            float hammerProgress = WindowProgress(
                presentationTime,
                HammerStart,
                HammerDuration);
            if (hammer != null)
            {
                float strike = reducedMotion
                    ? 0f
                    : Mathf.Sin(hammerProgress * Mathf.PI) * 48f;
                hammer.localRotation =
                    hammerRotation * Quaternion.Euler(strike, 0f, 0f);
            }
        }

        private void Awake()
        {
            EnsureCache();
        }

        private void EnsureCache()
        {
            if (cacheReady)
            {
                return;
            }

            bellowsScale = bellows == null ? Vector3.one : bellows.localScale;
            hammerRotation = hammer == null ? Quaternion.identity : hammer.localRotation;
            propertyBlock ??= new MaterialPropertyBlock();
            cacheReady = true;
        }

        private void ApplyEmission(Color emission)
        {
            foreach (Renderer targetRenderer in forgeRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissionColorId, emission);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static float WindowProgress(float time, float start, float duration)
        {
            if (time < start || time > start + duration)
            {
                return 0f;
            }

            return Mathf.Clamp01((time - start) / duration);
        }
    }
}
