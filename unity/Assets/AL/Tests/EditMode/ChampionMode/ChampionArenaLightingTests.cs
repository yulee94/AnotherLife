using System;
using System.Collections.Generic;
using System.Reflection;
using AL.ChampionMode;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.ChampionMode
{
    public sealed class ChampionArenaLightingTests
    {
        private const string KeyLightName = "Key Light - Moonforge";

        private readonly List<GameObject> _created = new List<GameObject>();
        private GameObject _controllerHost;

        [SetUp]
        public void SetUp()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            DestroyExistingKeyLights();
        }

        [TearDown]
        public void TearDown()
        {
            FirstSessionChampionStart.ResetToFirstSessionLanding();
            for (int i = 0; i < _created.Count; i++)
            {
                DestroyImmediateIfPresent(_created[i]);
            }

            _created.Clear();
            DestroyImmediateIfPresent(_controllerHost);
            _controllerHost = null;
            DestroyExistingKeyLights();
        }

        [Test]
        public void ConfigureArenaLighting_EmptyNamedKeyLight_AddsLightWithoutThrowing()
        {
            GameObject empty = Track(new GameObject(KeyLightName));
            ChampionArenaSceneController controller = CreateController();

            Assert.DoesNotThrow(() => InvokeLighting(controller));

            Light light = empty.GetComponent<Light>();
            Assert.IsTrue(light != null, "Named empty Key Light must receive a Light component.");
            Assert.AreEqual(LightType.Directional, light.type);
            Assert.AreEqual(KeyLightName, empty.name);
            Assert.AreEqual(1, CountNamedKeyLights());
        }

        [Test]
        public void ConfigureArenaLighting_DestroyedKeyLight_AddsLightWithoutThrowing()
        {
            GameObject named = Track(new GameObject(KeyLightName));
            Light destroyed = named.AddComponent<Light>();
            UnityEngine.Object.DestroyImmediate(destroyed);
            ChampionArenaSceneController controller = CreateController();

            Assert.DoesNotThrow(() => InvokeLighting(controller));

            Light light = named.GetComponent<Light>();
            Assert.IsTrue(light != null, "Destroyed Key Light must be replaced, not SetName'd.");
            Assert.AreEqual(LightType.Directional, light.type);
            Assert.AreEqual(KeyLightName, named.name);
        }

        [Test]
        public void ConfigureArenaLighting_DoesNotHijackUnrelatedLight()
        {
            GameObject sun = Track(new GameObject("Sun"));
            Light sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            ChampionArenaSceneController controller = CreateController();

            Assert.DoesNotThrow(() => InvokeLighting(controller));

            Assert.AreEqual("Sun", sun.name);
            GameObject key = GameObject.Find(KeyLightName);
            Assert.IsTrue(key != null);
            Assert.IsTrue(key.GetComponent<Light>() != null);
            Assert.AreNotSame(sun, key);
        }

        private ChampionArenaSceneController CreateController()
        {
            _controllerHost = Track(new GameObject("ChampionArenaLightingHost"));
            return _controllerHost.AddComponent<ChampionArenaSceneController>();
        }

        private GameObject Track(GameObject value)
        {
            _created.Add(value);
            return value;
        }

        private static void InvokeLighting(ChampionArenaSceneController controller)
        {
            MethodInfo method = typeof(ChampionArenaSceneController).GetMethod(
                "ConfigureArenaLighting",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "ConfigureArenaLighting");
            try
            {
                method.Invoke(controller, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static int CountNamedKeyLights()
        {
            GameObject[] all = UnityEngine.Object.FindObjectsOfType<GameObject>();
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == KeyLightName)
                {
                    count++;
                }
            }

            return count;
        }

        private static void DestroyExistingKeyLights()
        {
            GameObject[] all = UnityEngine.Object.FindObjectsOfType<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i];
                if (candidate != null && candidate.name == KeyLightName)
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                }
            }
        }

        private static void DestroyImmediateIfPresent(UnityEngine.Object value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
