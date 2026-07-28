using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    /// <summary>
    /// Bounded Eldergrove operational activity. Structural roots remain still;
    /// only the cultivation core, water cue, and one protected leaf respond.
    /// </summary>
    public sealed class EldergroveAtelierStableActivity :
        MonoBehaviour,
        IArchitectureConstructionActivity
    {
        private const float PulseStart = 9.1f;
        private const float PulseDuration = 2.4f;
        private const float LeafStart = 11.9f;
        private const float LeafDuration = 1.15f;

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer[] sapRenderers = new Renderer[0];
        [SerializeField] private Transform interiorFitout;
        [SerializeField] private float fitoutVisibleAt = 6.2f;
        [SerializeField] private Transform temporaryGuideFrame;
        [SerializeField] private float guideVisibleAt = 1.55f;
        [SerializeField] private float guideHiddenAt = 4.65f;
        [SerializeField] private Transform waterRipple;
        [SerializeField] private Transform protectedLeaf;
        [SerializeField] private Light cultivationLight;
        [SerializeField] private Color restingEmission =
            new Color(0.025f, 0.08f, 0.018f);
        [SerializeField] private Color activeEmission =
            new Color(0.28f, 1.35f, 0.14f);

        private Vector3 rippleScale;
        private Vector3 leafScale;
        private MaterialPropertyBlock propertyBlock;
        private bool cacheReady;

        public int SapRendererCount => sapRenderers?.Length ?? 0;
        public bool HasWaterRipple => waterRipple != null;
        public bool HasProtectedLeaf => protectedLeaf != null;

        public void Configure(
            Renderer[] authoredSapRenderers,
            Transform fitoutTransform,
            float fitoutStageStart,
            Transform guideFrameTransform,
            float guideFrameStart,
            float guideFrameEnd,
            Transform rippleTransform,
            Transform leafTransform,
            Light localizedLight)
        {
            sapRenderers = authoredSapRenderers ?? new Renderer[0];
            interiorFitout = fitoutTransform;
            fitoutVisibleAt = Mathf.Max(0f, fitoutStageStart);
            temporaryGuideFrame = guideFrameTransform;
            guideVisibleAt = Mathf.Max(0f, guideFrameStart);
            guideHiddenAt = Mathf.Max(guideVisibleAt, guideFrameEnd);
            waterRipple = rippleTransform;
            protectedLeaf = leafTransform;
            cultivationLight = localizedLight;
            cacheReady = false;
            EnsureCache();
        }

        public void EvaluateActivity(float presentationTime, bool reducedMotion)
        {
            EnsureCache();

            if (interiorFitout != null)
            {
                bool shouldShowFitout = presentationTime >= fitoutVisibleAt;
                if (interiorFitout.gameObject.activeSelf != shouldShowFitout)
                {
                    interiorFitout.gameObject.SetActive(shouldShowFitout);
                }
            }

            if (temporaryGuideFrame != null)
            {
                bool shouldShowGuide =
                    presentationTime >= guideVisibleAt &&
                    presentationTime < guideHiddenAt;
                if (temporaryGuideFrame.gameObject.activeSelf != shouldShowGuide)
                {
                    temporaryGuideFrame.gameObject.SetActive(shouldShowGuide);
                }
            }

            float pulseProgress = WindowProgress(
                presentationTime,
                PulseStart,
                PulseDuration);
            bool pulseActive =
                presentationTime >= PulseStart &&
                presentationTime <= PulseStart + PulseDuration;
            float pulseStrength = pulseActive
                ? Mathf.Sin(pulseProgress * Mathf.PI)
                : 0f;
            if (reducedMotion)
            {
                pulseStrength = presentationTime >= PulseStart ? 0.28f : 0f;
            }

            ApplyEmission(Color.Lerp(
                restingEmission,
                activeEmission,
                pulseStrength));

            if (cultivationLight != null)
            {
                cultivationLight.intensity =
                    pulseStrength * (reducedMotion ? 0.35f : 0.8f);
                cultivationLight.enabled = pulseStrength > 0.01f;
            }

            if (waterRipple != null)
            {
                float expansion = reducedMotion ? 0f : pulseStrength * 0.28f;
                waterRipple.localScale =
                    rippleScale * (1f + expansion);
                waterRipple.localRotation = reducedMotion
                    ? Quaternion.identity
                    : Quaternion.Euler(0f, pulseProgress * 90f, 0f);
            }

            if (protectedLeaf != null)
            {
                float leafProgress = WindowProgress(
                    presentationTime,
                    LeafStart,
                    LeafDuration);
                float unfold = reducedMotion
                    ? (presentationTime >= LeafStart ? 1f : 0f)
                    : SmoothStep(leafProgress);
                protectedLeaf.localScale = Vector3.Lerp(
                    leafScale * 0.35f,
                    leafScale,
                    unfold);
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

            rippleScale =
                waterRipple == null ? Vector3.one : waterRipple.localScale;
            leafScale =
                protectedLeaf == null ? Vector3.one : protectedLeaf.localScale;
            propertyBlock ??= new MaterialPropertyBlock();
            cacheReady = true;
        }

        private void ApplyEmission(Color emission)
        {
            foreach (Renderer targetRenderer in sapRenderers)
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

        private static float SmoothStep(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
