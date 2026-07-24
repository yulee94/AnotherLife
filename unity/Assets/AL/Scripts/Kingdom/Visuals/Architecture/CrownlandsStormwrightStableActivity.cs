using System;
using UnityEngine;

namespace AL.Kingdom.Visuals.Architecture
{
    /// <summary>
    /// Crownlands-only operational activity attached beside the reusable
    /// construction-state controller.
    /// </summary>
    public sealed class CrownlandsStormwrightStableActivity :
        MonoBehaviour,
        IArchitectureConstructionActivity
    {
        private const float PulseStart = 9.1f;
        private const float PulseDuration = 2.05f;
        private const float InstrumentStart = 11.75f;
        private const float InstrumentDuration = 1.2f;

        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform instrumentRing;
        [SerializeField] private Transform pulseOrb;
        [SerializeField] private Transform[] pulseRoute = Array.Empty<Transform>();
        [SerializeField] private Renderer[] calibrationRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private Light calibrationLight;
        [SerializeField] private Color restingEmission =
            new Color(0.05f, 0.08f, 0.24f);
        [SerializeField] private Color activeEmission =
            new Color(0.10f, 0.22f, 1.75f);

        private Quaternion instrumentRingPose;
        private MaterialPropertyBlock propertyBlock;
        private bool cacheReady;

        public int PulseRouteNodeCount => pulseRoute?.Length ?? 0;

        public void Configure(
            Transform rotatingInstrumentRing,
            Transform movingPulseOrb,
            Transform[] authoredPulseRoute,
            Renderer[] pulseRenderers,
            Light pulseLight)
        {
            instrumentRing = rotatingInstrumentRing;
            pulseOrb = movingPulseOrb;
            pulseRoute = authoredPulseRoute ?? Array.Empty<Transform>();
            calibrationRenderers = pulseRenderers ?? Array.Empty<Renderer>();
            calibrationLight = pulseLight;
            cacheReady = false;
            EnsureCache();
        }

        public void EvaluateActivity(float presentationTime, bool reducedMotion)
        {
            EnsureCache();

            if (instrumentRing != null)
            {
                float instrumentProgress = Mathf.Clamp01(
                    (presentationTime - InstrumentStart) / InstrumentDuration);
                float rotation =
                    presentationTime >= InstrumentStart &&
                    presentationTime <= InstrumentStart + InstrumentDuration
                        ? EaseInOut(instrumentProgress) * 360f
                        : 0f;
                instrumentRing.localRotation =
                    instrumentRingPose * Quaternion.Euler(0f, rotation, 0f);
            }

            float pulseProgress = Mathf.Clamp01(
                (presentationTime - PulseStart) / PulseDuration);
            bool pulseActive =
                presentationTime >= PulseStart &&
                presentationTime <= PulseStart + PulseDuration;
            float pulseStrength = pulseActive
                ? Mathf.Sin(pulseProgress * Mathf.PI)
                : 0f;

            if (reducedMotion)
            {
                pulseStrength *= 0.35f;
            }

            ApplyEmission(Color.Lerp(
                restingEmission,
                activeEmission,
                pulseStrength));

            if (calibrationLight != null)
            {
                calibrationLight.intensity =
                    pulseStrength * (reducedMotion ? 0.50f : 1.45f);
                calibrationLight.enabled =
                    pulseActive && pulseStrength > 0.01f;
            }

            if (pulseOrb == null)
            {
                return;
            }

            bool showOrb =
                pulseActive &&
                !reducedMotion &&
                pulseRoute.Length >= 2;
            pulseOrb.gameObject.SetActive(showOrb);
            if (!showOrb)
            {
                return;
            }

            float routePosition = pulseProgress * (pulseRoute.Length - 1);
            int startNodeIndex = Mathf.Min(
                Mathf.FloorToInt(routePosition),
                pulseRoute.Length - 2);
            float segmentProgress = routePosition - startNodeIndex;
            Transform startNode = pulseRoute[startNodeIndex];
            Transform endNode = pulseRoute[startNodeIndex + 1];
            if (startNode != null && endNode != null)
            {
                pulseOrb.position = Vector3.Lerp(
                    startNode.position,
                    endNode.position,
                    EaseInOut(segmentProgress));
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

            instrumentRingPose = instrumentRing == null
                ? Quaternion.identity
                : instrumentRing.localRotation;
            propertyBlock ??= new MaterialPropertyBlock();
            cacheReady = true;
        }

        private void ApplyEmission(Color emission)
        {
            foreach (Renderer targetRenderer in calibrationRenderers)
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

        private static float EaseInOut(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
