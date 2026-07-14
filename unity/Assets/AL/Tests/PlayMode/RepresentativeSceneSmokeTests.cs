using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public class RepresentativeSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator RepresentativeTestSceneLoadsAndRunsWithoutUnexpectedErrors()
        {
            LogAssert.ignoreFailingMessages = false;

            AsyncOperation load = SceneManager.LoadSceneAsync("Test", LoadSceneMode.Single);
            Assert.NotNull(load, "Expected Assets/Test.unity to be included in build settings.");

            while (!load.isDone)
            {
                yield return null;
            }

            Assert.AreEqual("Test", SceneManager.GetActiveScene().name);

            // Give bootstrapping, service registration, and initial scene behaviours time to run.
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.5f);

            LogAssert.NoUnexpectedReceived();
        }
    }
}
