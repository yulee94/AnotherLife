using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Customization.Contracts;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Runtime;
using AL.Services.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AL.Tests.PlayMode
{
    public sealed class ChampionCustomizationPreviewPlayModeTests
    {
        [UnityTest]
        public IEnumerator RealPreviewChangesVisualsAndCancelRestoresWithoutSaveAccess()
        {
            IDictionary<Type, object> services = Services();
            bool hadPrior = services.TryGetValue(
                typeof(ISaveGameService),
                out object priorService);
            var save = new TrackingSaveService();
            ServiceLocator.Register<ISaveGameService>(save);
            GameObject owner = new GameObject("CustomizationPreviewPlayModeHost");

            try
            {
                string json = File.ReadAllText(Path.Combine(
                    Application.dataPath,
                    "AL",
                    "StreamingAssets",
                    "GameData",
                    "character_customization.v1.json"));
                Assert.That(
                    CharacterCustomizationCatalog.TryParsePlannerCatalog(
                        json,
                        out CustomizationCatalogSnapshot catalog,
                        out IReadOnlyList<CustomizationDiagnostic> diagnostics),
                    Is.True,
                    string.Join("\n", diagnostics.Select(item =>
                        item.Code + ":" + item.FieldPath)));
                ModelCapabilitySnapshot model =
                    ProceduralChampionPreviewModelCapabilities.Create(catalog);
                RawCustomizationSnapshot raw = RawDefaults(catalog);
                CustomizationCompatibilityResult compatibility =
                    CustomizationCompatibilityPlanner.Classify(
                        raw,
                        CustomizationCatalogAvailability.Ready,
                        catalog);
                CustomizationQueryResult query =
                    CustomizationCompatibilityPlanner.Resolve(
                        raw,
                        CustomizationCatalogAvailability.Ready,
                        catalog,
                        model);
                CustomizationDraft draft = CustomizationDraftPlanner.Create(
                    "draft_playmode_preview_001",
                    query,
                    compatibility);
                ChampionCustomizationController renderer =
                    owner.AddComponent<ChampionCustomizationController>();
                var appearance = new ProceduralChampionAppearanceAdapter(
                    renderer,
                    model,
                    query.EffectivePresentation);
                var preview = new CustomizationPreviewController(
                    draft,
                    catalog,
                    model,
                    appearance);
                Transform shortHair = owner.transform.Find("Hair_Short");
                Transform longHair = owner.transform.Find("Hair_Long");
                Assert.That(shortHair, Is.Not.Null);
                Assert.That(longHair, Is.Not.Null);
                Assert.That(shortHair.gameObject.activeSelf, Is.True);
                Assert.That(longHair.gameObject.activeSelf, Is.False);
                string visualBefore = CaptureVisualFingerprint(owner.transform);

                CustomizationPreviewResult result = preview.Preview(
                    CustomizationEditRequest.SelectOption(
                        CustomizationField.HairStyle,
                        "long"));

                Assert.That(result.Succeeded, Is.True);
                Assert.That(shortHair.gameObject.activeSelf, Is.False);
                Assert.That(longHair.gameObject.activeSelf, Is.True);
                Assert.That(preview.Cancel(),
                    Is.EqualTo(AppearanceRollbackStatus.Restored));
                Assert.That(shortHair.gameObject.activeSelf, Is.True);
                Assert.That(longHair.gameObject.activeSelf, Is.False);
                Assert.That(CaptureVisualFingerprint(owner.transform), Is.EqualTo(visualBefore));
                Assert.That(save.CurrentSaveReadCount, Is.Zero);
                Assert.That(save.SaveCallCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
                if (hadPrior)
                {
                    services[typeof(ISaveGameService)] = priorService;
                }
                else
                {
                    services.Remove(typeof(ISaveGameService));
                }
            }

            yield return null;
        }

        private static RawCustomizationSnapshot RawDefaults(
            CustomizationCatalogSnapshot catalog)
        {
            return new RawCustomizationSnapshot(
                1,
                1L,
                false,
                catalog.Policy.ApprovedDefaults);
        }

        private static IDictionary<Type, object> Services()
        {
            FieldInfo field = typeof(ServiceLocator).GetField(
                "Services",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IDictionary<Type, object>)field.GetValue(null);
        }

        private static string CaptureVisualFingerprint(Transform root)
        {
            var builder = new StringBuilder();
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                builder.Append(item.name).Append('|')
                    .Append(item.gameObject.activeSelf).Append('|')
                    .Append(item.localPosition.ToString("R")).Append('|')
                    .Append(item.localRotation.ToString("R")).Append('|')
                    .Append(item.localScale.ToString("R")).Append(';');

                var renderer = item.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && material.HasProperty("_Color"))
                    {
                        builder.Append(material.color.ToString("R")).Append(';');
                    }
                }
            }

            return builder.ToString();
        }

        private sealed class TrackingSaveService : ISaveGameService
        {
            internal int CurrentSaveReadCount { get; private set; }
            internal int SaveCallCount { get; private set; }

            public SaveGameData CurrentSave
            {
                get
                {
                    CurrentSaveReadCount++;
                    return new SaveGameData();
                }
            }

            public SaveLoadStatus LastLoadStatus => SaveLoadStatus.LoadedPrimary;
            public string LastLoadMessage => string.Empty;
            public SaveOperationStatus LastSaveStatus => SaveOperationStatus.None;
            public string LastSaveMessage => string.Empty;

            public void Save()
            {
                SaveCallCount++;
            }

            public void Load()
            {
            }

            public bool HasSave()
            {
                return true;
            }

            public void CreateNewSave(RealmId realmId)
            {
            }

            public void DeleteSave()
            {
            }
        }
    }
}
