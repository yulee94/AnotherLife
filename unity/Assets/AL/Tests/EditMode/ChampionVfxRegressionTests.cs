using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.EditMode
{
    public class ChampionVfxRegressionTests
    {
        [Test]
        public void BurstParticleReuseStopsBeforeDurationReconfiguration()
        {
            var effect = new GameObject("VFX_Test_PlayingBurst");
            try
            {
                var particles = effect.AddComponent<ParticleSystem>();
                particles.Play(true);
                Assert.True(particles.isPlaying);

                MethodInfo resetMethod = GetRuntimeType("AL.ChampionMode.Skills.SkillEffectFactory").GetMethod(
                    "ResetBurstParticlesForReuse",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(resetMethod, "Expected burst particle reset helper.");

                resetMethod.Invoke(null, new object[] { particles });

                Assert.False(particles.isPlaying);
                Assert.AreEqual(0, particles.particleCount);

                var main = particles.main;
                main.duration = 0.75f;
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(effect);
            }
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);
            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }
    }
}
