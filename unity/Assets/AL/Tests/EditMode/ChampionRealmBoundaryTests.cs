using System.Reflection;
using AL.ChampionMode.AI;
using AL.Core;
using NUnit.Framework;
using UnityEngine;

namespace AL.Tests.EditMode
{
    public sealed class ChampionRealmBoundaryTests
    {
        [Test]
        public void BotSpawnerDoesNotSubstituteOrSpawnForUnavailableRealm()
        {
            var root = new GameObject("ChampionRealmBoundaryTests");

            try
            {
                var spawner = root.AddComponent<RvrBotSpawner>();

                spawner.Configure(null, null, RealmId.None, 10);

                Assert.That(root.transform.childCount, Is.Zero);
                Assert.That(ReadField<RealmId>(spawner, "_playerRealm"), Is.EqualTo(RealmId.None));
                Assert.That(ReadField<bool>(spawner, "_spawned"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static T ReadField<T>(object target, string name)
        {
            return (T)target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }
    }
}
