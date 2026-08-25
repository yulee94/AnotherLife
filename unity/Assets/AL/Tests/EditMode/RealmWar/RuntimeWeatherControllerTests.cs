using System.Collections.Generic;
using AL.Core;
using AL.RealmWar.Warzone;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode.RealmWar
{
    public sealed class RuntimeWeatherControllerTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                DestroyImmediateIfPresent(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void ConfigureForRealm_AddsParticleSystemsWithoutThrowing()
        {
            GameObject host = Track(new GameObject("WeatherHost"));
            RuntimeWeatherController weather = host.AddComponent<RuntimeWeatherController>();

            Assert.DoesNotThrow(() => weather.ConfigureForRealm(RealmId.Stonehold));

            Assert.IsTrue(host.GetComponent<ParticleSystem>() != null);
            Assert.IsTrue(host.transform.Find("Weather_GroundMist") != null);
            Assert.IsTrue(host.transform.Find("Weather_GroundMist").GetComponent<ParticleSystem>() != null);
        }

        [Test]
        public void ConfigureForRealm_DestroyedParticleSystem_ReplacesWithoutThrowing()
        {
            GameObject host = Track(new GameObject("WeatherHostDestroyedPs"));
            ParticleSystem destroyed = host.AddComponent<ParticleSystem>();
            Object.DestroyImmediate(destroyed);
            RuntimeWeatherController weather = host.AddComponent<RuntimeWeatherController>();

            Assert.DoesNotThrow(() => weather.ConfigureForRealm(RealmId.Crownlands));

            Assert.IsTrue(host.GetComponent<ParticleSystem>() != null, "Destroyed ParticleSystem must be replaced, not configured via ??.");
            ParticleSystem particles = host.GetComponent<ParticleSystem>();
            Assert.DoesNotThrow(() =>
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = true;
            });
        }

        [Test]
        public void ConfigureForRealm_DestroyedAtmosphereLight_ReplacesWithoutThrowing()
        {
            GameObject host = Track(new GameObject("WeatherHostDestroyedLight"));
            GameObject lightObject = Track(new GameObject("Weather_RealmAtmosphereLight"));
            lightObject.transform.SetParent(host.transform, false);
            Light destroyed = lightObject.AddComponent<Light>();
            Object.DestroyImmediate(destroyed);
            RuntimeWeatherController weather = host.AddComponent<RuntimeWeatherController>();

            Assert.DoesNotThrow(() => weather.ConfigureForRealm(RealmId.Eldergrove));

            Assert.IsTrue(lightObject.GetComponent<Light>() != null, "Destroyed weather Light must be replaced, not SetName'd.");
        }

        [Test]
        public void ConfigureForRealm_DestroyedChildParticleSystem_ReplacesWithoutThrowing()
        {
            GameObject host = Track(new GameObject("WeatherHostDestroyedChildPs"));
            GameObject mist = Track(new GameObject("Weather_GroundMist"));
            mist.transform.SetParent(host.transform, false);
            ParticleSystem destroyed = mist.AddComponent<ParticleSystem>();
            Object.DestroyImmediate(destroyed);
            RuntimeWeatherController weather = host.AddComponent<RuntimeWeatherController>();

            Assert.DoesNotThrow(() => weather.ConfigureForRealm(RealmId.Umbral));

            Assert.IsTrue(mist.GetComponent<ParticleSystem>() != null);
        }

        private GameObject Track(GameObject value)
        {
            _created.Add(value);
            return value;
        }

        private static void DestroyImmediateIfPresent(Object value)
        {
            if (value != null)
            {
                Object.DestroyImmediate(value);
            }
        }
    }
}
