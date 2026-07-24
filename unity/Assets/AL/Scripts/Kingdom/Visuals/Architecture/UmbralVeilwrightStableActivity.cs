using System;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    /// <summary>
    /// Umbral-only operational confirmation attached beside the reusable
    /// construction-state controller. The effect follows four authored anchors,
    /// closes at one grounded core, confirms at one chimney point, then sleeps.
    /// </summary>
    public sealed class UmbralVeilwrightStableActivity :
        MonoBehaviour,
        IArchitectureConstructionActivity
    {
        private const float ActivityStart = 9.1f;
        private const float AnchorWakeDuration = 0.95f;
        private const float InwardFoldDuration = 1.25f;
        private const float EclipseDuration = 0.70f;
        private const float ChimneyConfirmDuration = 0.80f;
        private const float AnchorStagger = 0.14f;

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");

        [Header("Authored convergence route")]
        [SerializeField] private Transform[] anchorPoints =
            Array.Empty<Transform>();
        [SerializeField] private Transform corePoint;
        [SerializeField] private Transform chimneyPoint;
        [SerializeField] private Transform convergenceOrb;
        [SerializeField] private Transform eclipseRing;

        [Header("Bounded value response")]
        [SerializeField] private Renderer[] anchorRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private Renderer[] routeRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private Renderer[] coreRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private Renderer[] chimneyRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private Light convergenceLight;
        [SerializeField] private Color restingEmission =
            new Color(0.01f, 0.005f, 0.025f);
        [SerializeField] private Color activeEmission =
            new Color(0.18f, 0.04f, 1.35f);

        private Quaternion eclipseRingPose;
        private Vector3 eclipseRingScale;
        private MaterialPropertyBlock propertyBlock;
        private bool cacheReady;

        public int AnchorCount => anchorPoints?.Length ?? 0;
        public bool SupportsReducedMotion => true;
        public float EventStart => ActivityStart;
        public float EventEnd =>
            ActivityStart +
            AnchorWakeDuration +
            InwardFoldDuration +
            EclipseDuration +
            ChimneyConfirmDuration;

        public void Configure(
            Transform[] authoredAnchors,
            Transform authoredCorePoint,
            Transform authoredChimneyPoint,
            Transform movingConvergenceOrb,
            Transform rotatingEclipseRing,
            Renderer[] authoredAnchorRenderers,
            Renderer[] authoredRouteRenderers,
            Renderer[] authoredCoreRenderers,
            Renderer[] authoredChimneyRenderers,
            Light localizedConvergenceLight)
        {
            anchorPoints = authoredAnchors ?? Array.Empty<Transform>();
            corePoint = authoredCorePoint;
            chimneyPoint = authoredChimneyPoint;
            convergenceOrb = movingConvergenceOrb;
            eclipseRing = rotatingEclipseRing;
            anchorRenderers =
                authoredAnchorRenderers ?? Array.Empty<Renderer>();
            routeRenderers =
                authoredRouteRenderers ?? Array.Empty<Renderer>();
            coreRenderers =
                authoredCoreRenderers ?? Array.Empty<Renderer>();
            chimneyRenderers =
                authoredChimneyRenderers ?? Array.Empty<Renderer>();
            convergenceLight = localizedConvergenceLight;
            cacheReady = false;
            EnsureCache();
        }

        public void EvaluateActivity(float presentationTime, bool reducedMotion)
        {
            EnsureCache();

            float eventTime = presentationTime - ActivityStart;
            ResetTransientGeometry();

            if (reducedMotion)
            {
                EvaluateReducedMotion(eventTime);
                return;
            }

            float foldStart = AnchorWakeDuration;
            float eclipseStart = foldStart + InwardFoldDuration;
            float chimneyStart = eclipseStart + EclipseDuration;
            float eventEnd = chimneyStart + ChimneyConfirmDuration;

            ApplyAnchorWake(eventTime);

            float routeStrength = WindowPulse(
                eventTime,
                foldStart,
                eclipseStart);
            float eclipseStrength = WindowPulse(
                eventTime,
                eclipseStart,
                chimneyStart);
            float chimneyStrength = WindowPulse(
                eventTime,
                chimneyStart,
                eventEnd);

            ApplyEmission(
                routeRenderers,
                EmissionAt(routeStrength * 0.85f));
            ApplyEmission(
                coreRenderers,
                EmissionAt(Mathf.Max(
                    routeStrength * 0.35f,
                    eclipseStrength)));
            ApplyEmission(
                chimneyRenderers,
                EmissionAt(chimneyStrength));

            ApplyEclipseRing(eventTime, eclipseStart, chimneyStart);
            ApplyConvergenceOrb(
                eventTime,
                foldStart,
                eclipseStart,
                chimneyStart,
                eventEnd);
            ApplyLight(
                Mathf.Max(
                    routeStrength * 0.55f,
                    Mathf.Max(eclipseStrength, chimneyStrength * 0.65f)),
                1.25f);
        }

        private void Awake()
        {
            EnsureCache();
        }

        private void EvaluateReducedMotion(float eventTime)
        {
            float reducedStart = AnchorWakeDuration;
            float reducedEnd =
                AnchorWakeDuration +
                InwardFoldDuration +
                EclipseDuration +
                ChimneyConfirmDuration;
            bool confirming =
                eventTime >= reducedStart &&
                eventTime <= reducedEnd;
            float confirmationStrength = confirming ? 0.28f : 0f;

            ApplyEmission(
                anchorRenderers,
                EmissionAt(confirmationStrength * 0.55f));
            ApplyEmission(
                routeRenderers,
                restingEmission);
            ApplyEmission(
                coreRenderers,
                EmissionAt(confirmationStrength));
            ApplyEmission(
                chimneyRenderers,
                EmissionAt(confirmationStrength * 0.75f));
            ApplyLight(confirmationStrength, 0.35f);
        }

        private void ApplyAnchorWake(float eventTime)
        {
            for (int index = 0; index < anchorRenderers.Length; index++)
            {
                float wakeStart = index * AnchorStagger;
                float wakePeak = wakeStart + 0.26f;
                float wakeEnd = AnchorWakeDuration + index * 0.04f;
                float strength = eventTime <= wakePeak
                    ? Mathf.InverseLerp(wakeStart, wakePeak, eventTime)
                    : 1f - Mathf.InverseLerp(wakePeak, wakeEnd, eventTime);
                ApplyEmission(
                    anchorRenderers[index],
                    EmissionAt(Mathf.Clamp01(strength)));
            }
        }

        private void ApplyEclipseRing(
            float eventTime,
            float eclipseStart,
            float chimneyStart)
        {
            if (eclipseRing == null)
            {
                return;
            }

            float progress = Mathf.InverseLerp(
                eclipseStart,
                chimneyStart,
                eventTime);
            if (eventTime < eclipseStart || eventTime > chimneyStart)
            {
                return;
            }

            float closure = Mathf.Sin(progress * Mathf.PI);
            eclipseRing.localRotation =
                eclipseRingPose *
                Quaternion.Euler(0f, -110f * closure, 0f);
            eclipseRing.localScale = Vector3.Lerp(
                eclipseRingScale,
                eclipseRingScale * 0.82f,
                closure);
        }

        private void ApplyConvergenceOrb(
            float eventTime,
            float foldStart,
            float eclipseStart,
            float chimneyStart,
            float eventEnd)
        {
            if (convergenceOrb == null ||
                corePoint == null)
            {
                return;
            }

            bool foldActive =
                eventTime >= foldStart &&
                eventTime <= eclipseStart &&
                anchorPoints.Length > 0;
            bool eclipseActive =
                eventTime > eclipseStart &&
                eventTime <= chimneyStart;
            bool chimneyActive =
                eventTime > chimneyStart &&
                eventTime <= eventEnd &&
                chimneyPoint != null;
            convergenceOrb.gameObject.SetActive(
                foldActive || eclipseActive || chimneyActive);

            if (foldActive)
            {
                float progress = Mathf.InverseLerp(
                    foldStart,
                    eclipseStart,
                    eventTime);
                float routePosition = progress * anchorPoints.Length;
                int anchorIndex = Mathf.Min(
                    Mathf.FloorToInt(routePosition),
                    anchorPoints.Length - 1);
                Transform anchor = anchorPoints[anchorIndex];
                if (anchor != null)
                {
                    float segmentProgress = routePosition - anchorIndex;
                    convergenceOrb.position = Vector3.Lerp(
                        anchor.position,
                        corePoint.position,
                        EaseInOut(segmentProgress));
                }

                return;
            }

            if (eclipseActive)
            {
                convergenceOrb.position = corePoint.position;
                return;
            }

            if (chimneyActive)
            {
                convergenceOrb.position = Vector3.Lerp(
                    corePoint.position,
                    chimneyPoint.position,
                    EaseInOut(Mathf.InverseLerp(
                        chimneyStart,
                        eventEnd,
                        eventTime)));
            }
        }

        private void ApplyLight(float strength, float intensityMultiplier)
        {
            if (convergenceLight == null)
            {
                return;
            }

            convergenceLight.intensity =
                Mathf.Clamp01(strength) * intensityMultiplier;
            convergenceLight.enabled = strength > 0.01f;
        }

        private void ResetTransientGeometry()
        {
            if (convergenceOrb != null)
            {
                convergenceOrb.gameObject.SetActive(false);
            }

            if (eclipseRing != null)
            {
                eclipseRing.localRotation = eclipseRingPose;
                eclipseRing.localScale = eclipseRingScale;
            }

            ApplyLight(0f, 0f);
        }

        private void EnsureCache()
        {
            if (cacheReady)
            {
                return;
            }

            eclipseRingPose = eclipseRing == null
                ? Quaternion.identity
                : eclipseRing.localRotation;
            eclipseRingScale = eclipseRing == null
                ? Vector3.one
                : eclipseRing.localScale;
            propertyBlock ??= new MaterialPropertyBlock();
            cacheReady = true;
        }

        private Color EmissionAt(float strength)
        {
            return Color.Lerp(
                restingEmission,
                activeEmission,
                Mathf.Clamp01(strength));
        }

        private void ApplyEmission(
            Renderer[] renderers,
            Color emission)
        {
            foreach (Renderer targetRenderer in renderers)
            {
                ApplyEmission(targetRenderer, emission);
            }
        }

        private void ApplyEmission(
            Renderer targetRenderer,
            Color emission)
        {
            if (targetRenderer == null)
            {
                return;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, emission);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private static float WindowPulse(
            float value,
            float start,
            float end)
        {
            if (value < start || value > end)
            {
                return 0f;
            }

            return Mathf.Sin(
                Mathf.InverseLerp(start, end, value) *
                Mathf.PI);
        }

        private static float EaseInOut(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
