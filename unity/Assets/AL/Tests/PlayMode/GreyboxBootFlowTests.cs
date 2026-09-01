using System.Collections;
using AL.Core;
using AL.Slice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AL.Tests.PlayMode
{
    // Greybox vertical-slice opening: realm selection commits to local run state and advances to the
    // character-creation entry point. Drives the slice controllers directly (no UI raycasting, no
    // profile isolation) so it is stable under headless batch-mode runs.
    public sealed class GreyboxBootFlowTests
    {
        [UnityTest]
        public IEnumerator RealmSelectionPresentsCommitsAndAdvancesToCharacterCreation()
        {
            GreyboxRunState.Reset();
            var host = new GameObject("GreyboxBootFlowHost");

            RealmId committed = RealmId.None;
            bool advanced = false;

            var selection = host.AddComponent<GreyboxRealmSelectionController>();
            selection.OnRealmCommitted += id => committed = id;
            selection.Present();

            yield return null;

            GameObject selectionCanvas = GameObject.Find("GreyboxRealmSelectionCanvas");
            Assert.That(selectionCanvas, Is.Not.Null, "Realm selection canvas should be presented on boot.");

            Button[] realmButtons = selectionCanvas.GetComponentsInChildren<Button>(true);
            Assert.That(realmButtons.Length, Is.EqualTo(4), "Four hardcoded realm buttons should be presented.");

            // Simulate the player selecting the first realm.
            realmButtons[0].onClick.Invoke();
            yield return null;

            Assert.That(GreyboxRunState.HasRealm, Is.True, "Selecting a realm should store the choice in local run state.");
            Assert.That(GreyboxRunState.SelectedRealmId, Is.Not.EqualTo(RealmId.None));
            Assert.That(committed, Is.EqualTo(GreyboxRunState.SelectedRealmId), "OnRealmCommitted should report the committed realm.");

            // The realm selection advances to the character-creation entry point.
            var entry = host.AddComponent<GreyboxCharacterCreationEntryController>();
            entry.OnCharacterConfirmed += () => advanced = true;
            entry.Present();

            yield return null;

            GameObject entryCanvas = GameObject.Find("GreyboxCharacterCreationCanvas");
            Assert.That(entryCanvas, Is.Not.Null, "Character creation entry point should be presented after realm selection.");

            Button confirmButton = entryCanvas.GetComponentInChildren<Button>(true);
            Assert.That(confirmButton, Is.Not.Null, "Character creation entry point should expose a confirm action.");
            confirmButton.onClick.Invoke();
            yield return null;

            Assert.That(advanced, Is.True, "Confirming the character creation entry should fire OnCharacterConfirmed.");

            Object.Destroy(host);
            GreyboxRunState.Reset();
        }
    }
}
