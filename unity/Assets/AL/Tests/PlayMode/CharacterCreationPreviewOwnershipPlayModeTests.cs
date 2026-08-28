using System.Collections;
using System.Reflection;
using AL.Core;
using AL.UI.CharacterCreation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class CharacterCreationPreviewOwnershipPlayModeTests
    {
        [UnityTest]
        public IEnumerator CreatorPreviewPresentationHasSingleOwnedRootAndExactTeardown()
        {
            var host = new GameObject("CharacterCreationPreviewOwnershipHost");
            host.SetActive(false);
            try
            {
                CharacterCreationController controller =
                    host.AddComponent<CharacterCreationController>();
                BindingFlags privateInstance =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo rootField = typeof(CharacterCreationController).GetField(
                    "_previewPresentationRoot",
                    privateInstance);
                Assert.That(rootField, Is.Not.Null);
                Assert.That(
                    CharacterCreationDraft.TryCreate(
                        RealmId.Crownlands,
                        out CharacterCreationDraft draft,
                        out string error),
                    Is.True,
                    error);
                typeof(CharacterCreationController).GetField("_draft", privateInstance)
                    ?.SetValue(controller, draft);
                MethodInfo buildPreview = typeof(CharacterCreationController).GetMethod(
                    "BuildPreview",
                    privateInstance);
                MethodInfo releasePreview = typeof(CharacterCreationController).GetMethod(
                    "ReleasePreviewPresentation",
                    privateInstance);
                Assert.That(buildPreview, Is.Not.Null);
                Assert.That(releasePreview, Is.Not.Null);

                buildPreview.Invoke(controller, null);
                GameObject firstRoot = (GameObject)rootField.GetValue(controller);
                Assert.That(firstRoot, Is.Not.Null);
                Assert.That(firstRoot.transform.parent, Is.SameAs(host.transform));
                Assert.That(firstRoot.transform.Find("CreatorKeyLight"), Is.Not.Null);
                Assert.That(firstRoot.transform.Find("CreatorFillLight"), Is.Not.Null);
                Assert.That(firstRoot.transform.Find("CreatorPreview"), Is.Not.Null);
                Assert.That(firstRoot.transform.Find("CreatorPreviewCamera"), Is.Not.Null);

                buildPreview.Invoke(controller, null);
                GameObject secondRoot = (GameObject)rootField.GetValue(controller);
                yield return null;
                Assert.That(firstRoot == null, Is.True);
                Assert.That(secondRoot, Is.Not.Null);
                Assert.That(
                    CountDirectChildren(
                        host.transform,
                        "CharacterCreationPreviewPresentation"),
                    Is.EqualTo(1));

                releasePreview.Invoke(controller, null);
                yield return null;
                Assert.That(secondRoot == null, Is.True);
                Assert.That(rootField.GetValue(controller), Is.Null);
                Assert.That(
                    CountDirectChildren(
                        host.transform,
                        "CharacterCreationPreviewPresentation"),
                    Is.Zero);
            }
            finally
            {
                Object.Destroy(host);
            }

            yield return null;
        }

        private static int CountDirectChildren(Transform owner, string objectName)
        {
            int count = 0;
            for (int index = 0; index < owner.childCount; index++)
            {
                if (owner.GetChild(index).name == objectName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
