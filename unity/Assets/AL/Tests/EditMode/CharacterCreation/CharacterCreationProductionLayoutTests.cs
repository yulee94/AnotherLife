using AL.Core;
using AL.Data.Catalogs;
using AL.Data.Runtime;
using AL.UI.CharacterCreation;
using AL.UI.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Tests.EditMode.CharacterCreation
{
    public sealed class CharacterCreationProductionLayoutTests
    {
        [SetUp]
        public void SetUp()
        {
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            CharacterCreationIdentity.ResetClaims();
            SliceRunState.Reset();
        }

        [Test]
        public void CreatorFramingFitsEnabledRendererBoundsAfterVisualReplacement()
        {
            GameObject preview = new GameObject("CreatorPreview");
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject cameraObject = new GameObject("CreatorPreviewCamera");
            try
            {
                preview.transform.position = new Vector3(0.85f, 0f, 3.4f);
                visual.transform.SetParent(preview.transform, false);
                visual.transform.localPosition = Vector3.up * 0.9f;
                visual.transform.localScale = new Vector3(0.9f, 1.8f, 0.55f);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.aspect = 16f / 9f;
                cameraObject.transform.position = new Vector3(0.85f, 1.05f, 1.05f);
                cameraObject.transform.LookAt(preview.transform.position + Vector3.up * 0.72f);

                Assert.That(
                    CharacterCreationPreviewPresentation.TryFrame(camera, preview.transform),
                    Is.True);

                Bounds bounds = visual.GetComponent<Renderer>().bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x < 2; x++)
                {
                    for (int y = 0; y < 2; y++)
                    {
                        for (int z = 0; z < 2; z++)
                        {
                            Vector3 viewport = camera.WorldToViewportPoint(new Vector3(
                                x == 0 ? min.x : max.x,
                                y == 0 ? min.y : max.y,
                                z == 0 ? min.z : max.z));
                            Assert.That(viewport.x, Is.InRange(0.12f, 0.88f));
                            Assert.That(viewport.y, Is.InRange(0.10f, 0.90f));
                            Assert.That(viewport.z, Is.GreaterThan(0f));
                        }
                    }
                }

                Assert.That(camera.fieldOfView, Is.EqualTo(30f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(preview);
            }
        }

        [Test]
        public void CreatorLightingIsOwnedDeterministicAndUnaffectedByOtherSceneLights()
        {
            GameObject unrelatedObject = new GameObject("UnrelatedSceneLight");
            GameObject owner = new GameObject("CharacterCreationPreviewPresentationTestOwner");
            try
            {
                Light unrelated = unrelatedObject.AddComponent<Light>();
                System.Reflection.MethodInfo ensureOwned =
                    typeof(CharacterCreationPreviewPresentation).GetMethod(
                        "EnsureOwnedLights",
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.Static,
                        binder: null,
                        types: new[] { typeof(Transform) },
                        modifiers: null);
                Assert.That(ensureOwned, Is.Not.Null);

                Light key = (Light)ensureOwned.Invoke(
                    null,
                    new object[] { owner.transform });
                ensureOwned.Invoke(null, new object[] { owner.transform });

                Transform keyTransform = owner.transform.Find("CreatorKeyLight");
                Transform fillTransform = owner.transform.Find("CreatorFillLight");
                Assert.That(keyTransform, Is.Not.Null);
                Assert.That(fillTransform, Is.Not.Null);
                Assert.That(key, Is.SameAs(keyTransform.GetComponent<Light>()));
                Assert.That(owner.transform.childCount, Is.EqualTo(2));
                AssertCreatorLight(
                    keyTransform.GetComponent<Light>(),
                    1.35f,
                    new Color(1f, 0.94f, 0.86f, 1f),
                    new Vector3(32f, 332f, 0f));
                AssertCreatorLight(
                    fillTransform.GetComponent<Light>(),
                    0.45f,
                    new Color(0.62f, 0.72f, 1f, 1f),
                    new Vector3(18f, 35f, 0f));
                Assert.That(unrelatedObject.GetComponent<Light>(), Is.SameAs(unrelated));
            }
            finally
            {
                Object.DestroyImmediate(unrelatedObject);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProductionSurfaceUsesSharedChromeNotLegacyRuntime()
        {
            string controller = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationController.cs"));
            string layout = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationProductionLayout.cs"));

            Assert.That(controller, Does.Contain("PresentationChrome"));
            Assert.That(controller, Does.Contain("CharacterCreationProductionLayout"));
            Assert.That(controller, Does.Contain("ChampionCustomizationController"));
            Assert.That(controller, Does.Contain("CreatorPreview"));
            Assert.That(controller, Does.Contain("ChampionArena"));
            Assert.That(controller, Does.Not.Contain("LegacyRuntime"));
            Assert.That(controller, Does.Not.Contain("PrimitiveType.Capsule"));
            Assert.That(controller, Does.Not.Contain("CreatePrimitive"));
            Assert.That(controller, Does.Not.Contain("Kingdom"));
            Assert.That(layout, Does.Contain("PresentationChrome"));
            Assert.That(layout, Does.Contain("ValidationBanner"));
            Assert.That(layout, Does.Contain("CommittedHeraldry"));
            Assert.That(layout, Does.Not.Contain("LegacyRuntime"));
        }

        [Test]
        public void UsernameValidationIsPresentedOnTheBannerNotLogged()
        {
            string controller = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "AL/Scripts/UI/CharacterCreation/CharacterCreationController.cs"));
            Assert.That(controller, Does.Contain("PresentValidation"));
            Assert.That(controller, Does.Contain("usernameError"));
            Assert.That(controller, Does.Not.Contain("Debug.Log"));
        }

        [Test]
        public void PeopleCopyIsLockedAndHeraldryResolvesForEachRealm()
        {
            Assert.IsTrue(CharacterCreationLook.TryPeopleLabel(RealmId.Stonehold, out string people));
            Assert.AreEqual("Dwarven people", people);
            Assert.That(people, Does.Not.Contain("Dwarves"));
            Assert.That(
                CharacterCreationProductionLayout.ResolveHeraldryCopy(RealmId.Crownlands),
                Does.Contain("Celestial Meridian"));
            Assert.That(
                CharacterCreationProductionLayout.ResolveHeraldryCopy(RealmId.Umbral),
                Does.Contain("Severed Eclipse"));
        }

        [Test]
        public void TwoSameRealmLooksStayDifferentOnTheSurface()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft first, out _));
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Crownlands, out CharacterCreationDraft second, out _));
            first.TrySelectClassFamily(ClassFamily.Warrior, out _);
            second.TrySelectClassFamily(ClassFamily.Warrior, out _);
            second.CycleArmorTint();
            second.CycleHairStyle();
            second.ToggleHelmet();

            string left = CharacterCreationProductionLayout.FormatLookSummary(first.Customization);
            string right = CharacterCreationProductionLayout.FormatLookSummary(second.Customization);
            Assert.AreNotEqual(left, right);
            Assert.IsTrue(CharacterCreationLook.LooksDifferent(first.Customization, second.Customization));
        }

        [Test]
        public void ProductionCreatorExposesAndSummarizesBodyBaseSelection()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(
                RealmId.Crownlands,
                out CharacterCreationDraft draft,
                out _));
            Font font = PresentationChrome.ResolveFont();
            CharacterCreationProductionScreen screen = CharacterCreationProductionLayout.Build(
                draft,
                font,
                _ => { },
                () => { },
                () => { },
                _ => { },
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { },
                () => { },
                () => { });

            Assert.IsNotNull(screen.CanvasObject.transform.Find("BodyBase"));
            Assert.That(
                CharacterCreationProductionLayout.FormatLookSummary(draft.Customization),
                Does.Contain("base male"));
            draft.CycleBodyBase();
            Assert.That(
                CharacterCreationProductionLayout.FormatLookSummary(draft.Customization),
                Does.Contain("base female"));

            Object.DestroyImmediate(screen.CanvasObject);
        }

        [Test]
        public void BuildPresentsClassLookUsernameAndValidationBanner()
        {
            Assert.IsTrue(CharacterCreationDraft.TryCreate(RealmId.Stonehold, out CharacterCreationDraft draft, out _));
            Font font = PresentationChrome.ResolveFont();
            CharacterCreationProductionScreen screen = CharacterCreationProductionLayout.Build(
                draft,
                font,
                _ => { },
                () => { },
                () => { },
                _ => { },
                () => { },
                _ => { },
                _ => { },
                () => { },
                () => { },
                () => { },
                () => { });

            Assert.That(screen.CanvasObject.name, Is.EqualTo(CharacterCreationProductionLayout.CanvasName));
            Assert.AreEqual(4, screen.ClassCards.Count);
            Assert.IsNotNull(screen.Username);
            Assert.IsNotNull(screen.Confirm);
            Assert.IsNotNull(screen.SkinTone);
            Assert.IsNotNull(screen.HairColor);
            Assert.IsNotNull(screen.EyeColor);
            Assert.IsNotNull(screen.SkinSwatch);
            Assert.IsNotNull(screen.HairSwatch);
            Assert.IsNotNull(screen.EyeSwatch);
            Assert.IsNotNull(screen.StatusPlate);
            Assert.AreEqual(CharacterCreationProductionLayout.ValidationBannerName, screen.StatusPlate.gameObject.name);
            Assert.IsFalse(screen.StatusPlate.gameObject.activeSelf);
            Assert.That(screen.People.text, Does.Contain("Dwarven people"));
            Assert.That(screen.People.text, Does.Not.Contain("enum"));

            CharacterCreationProductionLayout.PresentValidation(screen, "Enter a username.");
            Assert.IsTrue(screen.StatusPlate.gameObject.activeSelf);
            Assert.AreEqual("Enter a username.", screen.Status.text);

            CharacterCreationIdentity.TryClaim("TakenName", string.Empty, out _, out _);
            Assert.IsFalse(CharacterCreationIdentity.TryClaim("takenname", string.Empty, out _, out string duplicate));
            CharacterCreationProductionLayout.PresentValidation(screen, duplicate);
            Assert.AreEqual("That username is already taken.", screen.Status.text);

            Object.DestroyImmediate(screen.CanvasObject);
        }

        [Test]
        public void EmblemPathMatchesApprovedArcaneAxisAsset()
        {
            Assert.IsTrue(GameDataRealmReferences.TryGetByLegacyIdentity(
                "Stonehold",
                (int)RealmId.Stonehold,
                out GameDataRealmReference reference));
            Assert.That(reference.AssetReference, Does.Contain("S_ArcaneAxis_Stonehold_Flat_256_v001.png"));
            string path = System.IO.Path.Combine(
                Application.dataPath,
                reference.AssetReference.Replace("Assets/", string.Empty).Replace('/', System.IO.Path.DirectorySeparatorChar));
            Assert.IsTrue(System.IO.File.Exists(path), path);
        }

        private static void AssertCreatorLight(
            Light light,
            float intensity,
            Color color,
            Vector3 euler)
        {
            Assert.That(light, Is.Not.Null);
            Assert.That(light.type, Is.EqualTo(LightType.Directional));
            Assert.That(light.intensity, Is.EqualTo(intensity).Within(0.001f));
            Assert.That(light.color, Is.EqualTo(color));
            Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(light.renderMode, Is.EqualTo(LightRenderMode.ForceVertex));
            Assert.That(
                Quaternion.Angle(light.transform.rotation, Quaternion.Euler(euler)),
                Is.LessThan(0.1f));
        }
    }
}
