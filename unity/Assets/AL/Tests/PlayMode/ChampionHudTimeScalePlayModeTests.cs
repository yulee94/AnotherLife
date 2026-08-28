using System.Collections;
using AL.ChampionMode.Skills;
using AL.ChampionMode.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode
{
    public sealed class ChampionHudTimeScalePlayModeTests
    {
        [UnityTest]
        public IEnumerator MenuPauseOutlivesHitPauseAndRestoresExactBaseline()
        {
            float originalTimeScale = Time.timeScale;
            float originalFixedDeltaTime = Time.fixedDeltaTime;
            GameObject hudRoot = null;
            try
            {
                Time.timeScale = 0.65f;
                Time.fixedDeltaTime = 0.013f;
                RuntimeCombatFeedback.RequestHitPause(0.04f, 0.04f);

                hudRoot = new GameObject(
                    "TimeScaleOwnershipHud",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler));
                ChampionHudSession session = ChampionHudSession.Attach(hudRoot.transform);
                session.OpenMenu();

                yield return new WaitForSecondsRealtime(0.08f);

                Assert.That(Time.timeScale, Is.EqualTo(0f));
                session.CloseMenu();
                Assert.That(Time.timeScale, Is.EqualTo(0.65f).Within(0.0001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.013f).Within(0.0001f));
            }
            finally
            {
                if (hudRoot != null)
                {
                    Object.DestroyImmediate(hudRoot);
                }
                ChampionHudCameraGate.Reset();
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator DestroyingHitPauseHostReleasesItsTimeScaleOwnership()
        {
            float originalTimeScale = Time.timeScale;
            float originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 0.75f;
                Time.fixedDeltaTime = 0.015f;
                RuntimeCombatFeedback.RequestHitPause(0.12f, 0.04f);
                GameObject host = GameObject.Find("ChampionRuntimeCombatFeedback");
                Assert.That(host, Is.Not.Null);

                Object.DestroyImmediate(host);
                yield return null;

                Assert.That(Time.timeScale, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.015f).Within(0.0001f));
            }
            finally
            {
                ChampionHudCameraGate.Reset();
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator DeactivatingHitPauseHostReleasesItsTimeScaleOwnership()
        {
            float originalTimeScale = Time.timeScale;
            float originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 0.72f;
                Time.fixedDeltaTime = 0.0144f;
                RuntimeCombatFeedback.RequestHitPause(0.12f, 0.04f);
                GameObject host = GameObject.Find("ChampionRuntimeCombatFeedback");
                Assert.That(host, Is.Not.Null);

                host.SetActive(false);
                yield return null;

                Assert.That(Time.timeScale, Is.EqualTo(0.72f).Within(0.0001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.0144f).Within(0.0001f));
            }
            finally
            {
                GameObject host = GameObject.Find("ChampionRuntimeCombatFeedback");
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }
                ChampionHudCameraGate.Reset();
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator DisabledStaleHitPauseHostCannotReleaseReplacementPause()
        {
            float originalTimeScale = Time.timeScale;
            float originalFixedDeltaTime = Time.fixedDeltaTime;
            try
            {
                Time.timeScale = 0.75f;
                Time.fixedDeltaTime = 0.015f;
                RuntimeCombatFeedback.RequestHitPause(0.04f, 0.04f);
                GameObject staleHost = GameObject.Find("ChampionRuntimeCombatFeedback");
                Assert.That(staleHost, Is.Not.Null);
                Behaviour staleComponent = FindRuntimeFeedbackHost(staleHost);
                Assert.That(staleComponent, Is.Not.Null);

                staleComponent.enabled = false;
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(0.75f).Within(0.0001f));

                RuntimeCombatFeedback.RequestHitPause(0.12f, 0.1f);
                yield return new WaitForSecondsRealtime(0.07f);

                Assert.That(Time.timeScale, Is.EqualTo(0.1f).Within(0.0001f));
                yield return new WaitForSecondsRealtime(0.07f);
                Assert.That(Time.timeScale, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.015f).Within(0.0001f));
            }
            finally
            {
                GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < objects.Length; i++)
                {
                    if (objects[i].name == "ChampionRuntimeCombatFeedback")
                    {
                        Object.DestroyImmediate(objects[i]);
                    }
                }
                ChampionHudCameraGate.Reset();
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDeltaTime;
            }
        }

        private static Behaviour FindRuntimeFeedbackHost(GameObject host)
        {
            Behaviour[] behaviours = host.GetComponents<Behaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i].GetType().Name == "RuntimeFeedbackHost")
                {
                    return behaviours[i];
                }
            }

            return null;
        }
    }
}
