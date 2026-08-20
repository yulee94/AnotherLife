using System.Collections.Generic;
using AL.ChampionMode.Customization;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using UnityEngine;

namespace AL.ChampionMode.Presentation
{
    /// <summary>
    /// Binds catalog champion identity onto the first-session 3D body.
    /// Root is never a visible capsule; remaining primitives are labelled TEMPORARY.
    /// </summary>
    public static class ChampionPresentationBinder
    {
        public static GameObject CreateChampionRoot(Vector3 position)
        {
            var player = new GameObject(FirstSessionChampionStart.PlayerObjectName);
            player.tag = "Player";
            player.transform.position = position;
            var collider = player.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.45f;
            collider.center = Vector3.zero;
            return player;
        }

        public static bool RootLooksLikeCapsule(GameObject root)
        {
            if (root == null)
            {
                return true;
            }

            var renderer = root.GetComponent<MeshRenderer>();
            var filter = root.GetComponent<MeshFilter>();
            if (renderer != null && renderer.enabled)
            {
                return true;
            }

            if (filter != null && filter.sharedMesh != null &&
                string.Equals(filter.sharedMesh.name, "Capsule", System.StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        public static GameObject CreateAndBind(Vector3 position, RealmId realm, out ChampionPresentationSpec spec)
        {
            spec = null;
            IReadOnlyList<ChampionPresentationCatalogEntry> catalog = CollectCatalog();
            ClassFamily? requestedClass = null;
            string requestedChampionId = string.Empty;
            if (SliceRunState.HasConfirmedChampion &&
                SliceRunState.Champion != null &&
                SliceRunState.Champion.Realm == realm)
            {
                requestedClass = SliceRunState.Champion.Family;
                requestedChampionId = SliceRunState.Champion.Id;
            }
            else
            {
                requestedClass = TryReadSavedClassFamily();
            }

            if (!ChampionPresentation.TryResolveFromSession(
                    realm,
                    requestedClass,
                    requestedChampionId,
                    catalog,
                    out spec,
                    out string diagnostic))
            {
                Debug.LogError(diagnostic);
                return null;
            }

            GameObject player = CreateChampionRoot(position);
            ProceduralChampionModelBuilder.EnsureModel(player);
            var customization = player.GetComponent<ChampionCustomizationController>() ??
                                player.AddComponent<ChampionCustomizationController>();
            customization.BindPresentation(spec);
            AttachIdentityTokens(player, spec);
            AttachTemporaryPlaque(player, spec);
            return player;
        }

        public static ChampionPresentationCatalogEntry FromDefinition(ChampionDefinition definition)
        {
            if (definition == null)
            {
                return default;
            }

            return new ChampionPresentationCatalogEntry(
                definition.Id,
                definition.DisplayName,
                definition.Realm,
                definition.Family,
                definition.WeaponStyleId,
                definition.OffhandStyleId,
                string.Empty,
                string.Empty);
        }

        private static IReadOnlyList<ChampionPresentationCatalogEntry> CollectCatalog()
        {
            var list = new List<ChampionPresentationCatalogEntry>();
            if (!ServiceLocator.TryGet(out IGameDataService data) || data == null)
            {
                return list;
            }

            foreach (ChampionDefinition champion in data.GetAllChampions())
            {
                if (champion == null || string.IsNullOrWhiteSpace(champion.Id))
                {
                    continue;
                }

                list.Add(FromDefinition(champion));
            }

            return list;
        }

        private static ClassFamily? TryReadSavedClassFamily()
        {
            if (!ServiceLocator.TryGet(out ISaveGameService save) || save?.CurrentSave == null)
            {
                return null;
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save.CurrentSave);
            return snapshot != null && snapshot.ClassFamily.HasValue ? snapshot.ClassFamily : null;
        }

        private static void AttachIdentityTokens(GameObject player, ChampionPresentationSpec spec)
        {
            EnsureToken(player.transform, spec.ClassFamilyTokenName);
            EnsureToken(player.transform, spec.PeopleTokenName);
        }

        private static void EnsureToken(Transform parent, string name)
        {
            if (parent.Find(name) != null)
            {
                return;
            }

            var token = new GameObject(name);
            token.transform.SetParent(parent, false);
        }

        private static void AttachTemporaryPlaque(GameObject player, ChampionPresentationSpec spec)
        {
            Transform existing = player.transform.Find(FirstSessionChampionStart.PresentationPlaqueName);
            GameObject plaque = existing != null
                ? existing.gameObject
                : new GameObject(FirstSessionChampionStart.PresentationPlaqueName);
            plaque.transform.SetParent(player.transform, false);
            plaque.transform.localPosition = new Vector3(0f, 1.55f, 0.2f);
            var text = plaque.GetComponent<TextMesh>() ?? plaque.AddComponent<TextMesh>();
            text.text = FirstSessionChampionStart.PresentationPlaqueCopy +
                        "\n" + spec.PeopleName + " · " + spec.ClassFamily;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.045f;
            text.fontSize = 24;
            text.color = new Color(0.78f, 0.70f, 0.52f, 0.78f);
        }
    }
}
