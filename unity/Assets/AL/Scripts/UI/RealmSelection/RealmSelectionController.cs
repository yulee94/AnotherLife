using System.Collections;
using System.Collections.Generic;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.RealmSelection;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.RealmSelection
{
    public class RealmSelectionController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private RealmSelectionCard _cardPrefab;
        [SerializeField] private Transform _container;
        [SerializeField] private GameObject _screenPrefab;
        [SerializeField] private string _nextScene = "CharacterCreation";

        [Header("Realm Heraldry")]
        [SerializeField] private Sprite _stoneholdEmblem;
        [SerializeField] private Sprite _eldergroveEmblem;
        [SerializeField] private Sprite _crownlandsEmblem;
        [SerializeField] private Sprite _umbralEmblem;

        private bool _selectionInProgress;
        private RealmSelectionCommitOverlay _commitOverlay;
        private RealmSelectionProductionScreen _productionScreen;

        public RealmId PendingRealmId =>
            _commitOverlay != null ? _commitOverlay.PendingRealmId : RealmId.None;

        public bool IsCommitOverlayVisible =>
            _commitOverlay != null && _commitOverlay.IsVisible;

        private void Start()
        {
            EnsurePresentationCamera();
            Bootloader.InitializeIfMissing();
            if (RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Loading)
            {
                StartCoroutine(PopulateWhenCatalogReady());
                return;
            }

            PopulateRealms();
        }

        private IEnumerator PopulateWhenCatalogReady()
        {
            float catalogWait = 0f;
            while (RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Loading && catalogWait < 10f)
            {
                catalogWait += Time.unscaledDeltaTime;
                yield return null;
            }

            PopulateRealms();
        }

        private static Camera EnsurePresentationCamera()
        {
            Camera main = Camera.main;
            if (IsDisplayPresentationCamera(main))
            {
                return main;
            }

            Camera[] activeCameras = Camera.allCameras;
            for (int i = 0; i < activeCameras.Length; i++)
            {
                if (IsDisplayPresentationCamera(activeCameras[i]))
                {
                    return activeCameras[i];
                }
            }

            var cameraObject = new GameObject("RealmSelectionCamera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PresentationChrome.StoneVoid;
            camera.cullingMask = 0;
            camera.depth = -100f;
            camera.orthographic = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            camera.useOcclusionCulling = false;
            return camera;
        }

        private static bool IsDisplayPresentationCamera(Camera camera)
        {
            return camera != null &&
                   camera.isActiveAndEnabled &&
                   camera.targetTexture == null &&
                   camera.targetDisplay == 0;
        }

        private void PopulateRealms()
        {
            var dataService = ServiceLocator.Get<IGameDataService>();
            IEnumerable<RealmDefinition> realms = OrderRealms(dataService.GetAllRealms());

            if (_screenPrefab != null)
            {
                GameObject instance = Instantiate(_screenPrefab);
                instance.name = RealmSelectionProductionLayout.CanvasName;
                PresentationChrome.BindFonts(instance.transform, RealmSelectionIdentity.ResolvePresentationFont());
                _commitOverlay = instance.GetComponentInChildren<RealmSelectionCommitOverlay>(true);
                BindCommitOverlay();
                BindAuthoredViewport(instance.transform);
                BindAuthoredCards(instance.transform, realms);
                return;
            }

            if (_cardPrefab != null && _container != null)
            {
                foreach (RealmDefinition realm in realms)
                {
                    RealmSelectionCard card = Instantiate(_cardPrefab, _container);
                    card.Setup(realm, PresentCandidate, RealmCatalogRuntime.Current);
                }

                EnsureRuntimeCommitOverlay();
                return;
            }

            Font font = RealmSelectionIdentity.ResolvePresentationFont();
            _productionScreen = RealmSelectionProductionLayout.Build(
                realms,
                GetRealmEmblem,
                PresentCandidate,
                font);
            _commitOverlay = _productionScreen.Commit;
            BindCommitOverlay();
        }

        private void BindAuthoredCards(Transform root, IEnumerable<RealmDefinition> realms)
        {
            foreach (RealmDefinition realm in realms)
            {
                if (realm == null)
                {
                    continue;
                }

                Transform slot = FindNamed(root, realm.RealmName) ??
                                 FindNamed(root, realm.Id.ToString());
                if (slot == null)
                {
                    continue;
                }

                var card = slot.GetComponent<RealmSelectionCard>();
                if (card != null)
                {
                    card.Setup(realm, PresentCandidate, RealmCatalogRuntime.Current);
                    continue;
                }

                var button = slot.GetComponent<Button>();
                if (button != null)
                {
                    RealmIdentityPresentation identity = ResolveIdentity(realm.Id);
                    BindAuthoredLabel(slot, realm.Id + "_People", identity.PeopleName);
                    BindAuthoredLabel(slot, realm.Id + "_Name", identity.RealmName.ToUpperInvariant());
                    BindAuthoredLabel(
                        slot,
                        realm.Id + "_Structure",
                        identity.MarkName + "  ·  " + identity.SilhouetteLanguage);
                    BindAuthoredLabel(slot, realm.Id + "_Material", identity.MaterialLanguage);
                    Transform emblem = FindNamed(slot, "RealmEmblem");
                    if (emblem != null && emblem.TryGetComponent(out Image emblemImage))
                    {
                        emblemImage.sprite = GetRealmEmblem(realm.Id);
                        emblemImage.preserveAspect = true;
                    }

                    RealmId captured = realm.Id;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => PresentCandidate(captured));
                }
            }
        }

        private static void BindAuthoredLabel(Transform root, string name, string value)
        {
            Transform label = FindNamed(root, name);
            if (label != null && label.TryGetComponent(out Text text))
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void BindAuthoredViewport(Transform root)
        {
            RectTransform safeArea = FindNamed(root, "SafeArea") as RectTransform;
            RectTransform cardsRoot = FindNamed(root, "RealmCards") as RectTransform;
            GridLayoutGroup grid = cardsRoot != null ? cardsRoot.GetComponent<GridLayoutGroup>() : null;
            if (safeArea == null || cardsRoot == null || grid == null)
            {
                return;
            }

            RealmSelectionSafeAreaDriver driver = root.GetComponent<RealmSelectionSafeAreaDriver>();
            if (driver == null)
            {
                driver = root.gameObject.AddComponent<RealmSelectionSafeAreaDriver>();
            }

            driver.Bind(safeArea, cardsRoot, grid);
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static IEnumerable<RealmDefinition> OrderRealms(IEnumerable<RealmDefinition> realms)
        {
            RealmCatalogSnapshot catalog = RealmCatalogRuntime.Current;
            if (catalog == null || catalog.Realms == null || catalog.Realms.Count == 0)
            {
                return realms;
            }

            var byId = new Dictionary<RealmId, RealmDefinition>();
            foreach (RealmDefinition realm in realms)
            {
                if (realm != null && !byId.ContainsKey(realm.Id))
                {
                    byId.Add(realm.Id, realm);
                }
            }

            var ordered = new List<RealmDefinition>(catalog.Realms.Count);
            for (int i = 0; i < catalog.Realms.Count; i++)
            {
                if (byId.TryGetValue(catalog.Realms[i].RuntimeId, out RealmDefinition realm))
                {
                    ordered.Add(realm);
                    byId.Remove(catalog.Realms[i].RuntimeId);
                }
            }

            foreach (RealmDefinition leftover in byId.Values)
            {
                ordered.Add(leftover);
            }

            return ordered;
        }

        public void PresentCandidate(RealmId id)
        {
            if (_selectionInProgress || id == RealmId.None)
            {
                return;
            }

            EnsureRuntimeCommitOverlay();
            _commitOverlay.Present(ResolveIdentity(id), GetRealmEmblem(id));
        }

        public bool ConfirmPendingSelection()
        {
            if (_commitOverlay == null || !_commitOverlay.IsVisible || _selectionInProgress)
            {
                return false;
            }

            StartCoroutine(PersistAndAdvance(_commitOverlay.PendingRealmId));
            return true;
        }

        public void WithdrawPendingSelection()
        {
            if (_selectionInProgress)
            {
                return;
            }

            _commitOverlay?.Hide();
        }

        private void BindCommitOverlay()
        {
            if (_commitOverlay == null)
            {
                return;
            }

            _commitOverlay.Bind(() => { ConfirmPendingSelection(); }, WithdrawPendingSelection);
        }

        private void EnsureRuntimeCommitOverlay()
        {
            if (_commitOverlay != null)
            {
                return;
            }

            var host = new GameObject("RealmSelectionCommitCanvas");
            var canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            PresentationChrome.ApplyCanvasScaler(host.AddComponent<CanvasScaler>());
            host.AddComponent<GraphicRaycaster>();
            _commitOverlay = RealmSelectionCommitOverlay.Create(
                host.transform,
                RealmSelectionIdentity.ResolvePresentationFont());
            BindCommitOverlay();
        }

        private IEnumerator PersistAndAdvance(RealmId id)
        {
            _selectionInProgress = true;
            float catalogWait = 0f;
            while (RealmCatalogRuntime.Status == RealmCatalogRuntimeStatus.Loading && catalogWait < 10f)
            {
                catalogWait += Time.unscaledDeltaTime;
                yield return null;
            }

            var realmService = ServiceLocator.Get<IRealmService>();
            RealmSelectionResult result = realmService.TrySelectRealm(
                new RealmSelectionRequest(System.Guid.NewGuid().ToString("N"), id));
            RealmSelectionFeedbackPresentation feedback = RealmSelectionFeedback.FromResult(
                result,
                RealmCatalogRuntime.Current);
            if (_commitOverlay != null)
            {
                _commitOverlay.PresentOutcome(
                    feedback.IsSuccess ? "REALM BOUND" : "REALM LOCKED",
                    feedback.Text);
            }

            if (!result.AllowsNavigation)
            {
                _selectionInProgress = false;
                Debug.LogError(result.TechnicalCode);
                yield break;
            }

            SceneManager.LoadScene(_nextScene);
        }

        public Sprite GetRealmEmblem(RealmId id)
        {
            return id switch
            {
                RealmId.Stonehold => _stoneholdEmblem,
                RealmId.Eldergrove => _eldergroveEmblem,
                RealmId.Crownlands => _crownlandsEmblem,
                RealmId.Umbral => _umbralEmblem,
                _ => null
            };
        }

        private static RealmIdentityPresentation ResolveIdentity(RealmId id)
        {
            RealmCatalogSnapshot catalog = RealmCatalogRuntime.Current;
            if (catalog != null && catalog.TryGet(id, out RealmCatalogEntry entry))
            {
                return new RealmIdentityPresentation(
                    id,
                    entry.Id,
                    entry.DisplayName,
                    entry.PeopleName,
                    entry.MarkName,
                    entry.SilhouetteLanguage,
                    entry.MaterialLanguage,
                    RealmSelectionIdentity.FrameKindFor(id));
            }

            return new RealmIdentityPresentation(
                id,
                string.Empty,
                id == RealmId.None ? "Unclaimed Realm" : id.ToString(),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                RealmSelectionIdentity.FrameKindFor(id));
        }
    }
}
