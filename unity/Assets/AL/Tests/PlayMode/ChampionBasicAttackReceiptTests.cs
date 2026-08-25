using System.Collections;
using AL.ChampionMode.Control;
using AL.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class ChampionBasicAttackReceiptTests
    {
        [UnityTest]
        public IEnumerator OnlyAnAcceptedBasicAttackPublishesAReceipt()
        {
            var champion = new GameObject("AcceptedAttackReceiptChampion");
            ChampionController controller =
                champion.AddComponent<ChampionController>();
            controller.ConfigureRealmContext(RealmId.Crownlands);
            var observedCount = 0;
            ChampionBasicAttackReceipt observed = default;
            controller.BasicAttackAccepted += receipt =>
            {
                observedCount++;
                observed = receipt;
            };

            Assert.That(controller.LastBasicAttackReceipt.Sequence, Is.Zero);
            Assert.That(controller.RequestBasicAttack(), Is.True);
            Assert.That(observedCount, Is.EqualTo(1));
            Assert.That(observed.Sequence, Is.EqualTo(1));
            Assert.That(
                controller.LastBasicAttackReceipt.Sequence,
                Is.EqualTo(observed.Sequence));

            Assert.That(
                controller.RequestBasicAttack(),
                Is.False,
                "An overlapping command is rejected and must not look accepted to tutorial progression.");
            Assert.That(observedCount, Is.EqualTo(1));
            Assert.That(controller.LastBasicAttackReceipt.Sequence, Is.EqualTo(1));

            Object.Destroy(champion);
            yield return null;
        }
    }
}
