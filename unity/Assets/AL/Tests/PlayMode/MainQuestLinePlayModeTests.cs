using System.Collections;
using AL.Narrative.MainQuestLine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class MainQuestLinePlayModeTests
    {
        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
                _host = null;
            }

            MainQuestLineHost existing = Object.FindObjectOfType<MainQuestLineHost>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator StartupShowsCatalogChapterAndMissingCatalogFailsVisibly()
        {
            _host = new GameObject("MainQuestLinePlayModeHost");
            MainQuestLineHost host = _host.AddComponent<MainQuestLineHost>();
            yield return null;
            Assert.IsNotNull(host.Catalog);
            Assert.That(host.OverlayText, Does.Contain("The First Signal"));
            Assert.That(host.OverlayText, Does.Contain("OMEN_1"));

            host.ShowMissingForTests(null);
            yield return null;
            Assert.That(host.OverlayText, Does.Contain("NARRATIVE UNAVAILABLE"));
            Assert.That(host.OverlayText, Does.Contain("AL-NARRATIVE-CATALOG-MISSING"));
        }

        [UnityTest]
        public IEnumerator RepresentativePathPersistsAcrossEncodedResume()
        {
            MainQuestLineExecutionResult result = MainQuestLineRuntime.ExecuteRepresentativePath();
            Assert.IsTrue(result.Succeeded, result.Diagnostic != null ? result.Diagnostic.ToString() : "failed");
            Assert.AreEqual("TALK_TO_VALERIUS", result.ProgressedStateId);
            Assert.AreEqual(result.ProgressedStateId, result.ResumedStateId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionKingdomSceneRemainsReachable()
        {
            Assert.That(
                Application.CanStreamedLevelBeLoaded("Kingdom"),
                Is.True,
                "The representative narrative entry scene must remain in the five-scene player shell.");
            yield return null;
        }
    }
}
