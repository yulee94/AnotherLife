using System;
using System.Collections.Generic;
using AL.ChampionMode.Customization;
using AL.ChampionMode.Presentation;
using AL.Core;
using AL.Core.Interfaces;
using AL.Data.Definitions;
using AL.Data.Runtime;
using AL.Services.Local;
using AL.UI.FirstUserIdentity;
using AL.UI.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AL.UI.CharacterCreation
{
    /// <summary>
    /// Production creator after realm commit: class family, appearance, username,
    /// committed-realm heraldry, and an adult procedural preview. Hands off to ChampionArena.
    /// </summary>
    public class CharacterCreationController : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] private string _combatSceneName = "ChampionArena";

        private readonly List<ChampionDefinition> _champions = new List<ChampionDefinition>();
        private CharacterCreationDraft _draft;
        private CharacterCreationProductionScreen _screen;
        private GameObject _previewPresentationRoot;
        private ChampionCustomizationController _preview;
        private CharacterCreationPreviewMotion _previewMotion;
        private Camera _previewCamera;
        private bool _committing;
        private bool _alreadyConfirmed;

        private void Start()
        {
            Bootloader.InitializeIfMissing();
            EnsureSaveLoaded();
            EnsureEventSystem();
            LoadChampions();
            if (!TryBuildDraft())
            {
                return;
            }

            BuildPreview();
            BuildUi();
            RefreshPreview();
        }

        private void EnsureSaveLoaded()
        {
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            if (save != null && save.CurrentSave == null)
            {
                save.Load();
            }

            if (save?.CurrentSave != null)
            {
                MvpLoopSaveCodec.RestoreSessionIdentity(save.CurrentSave);
            }
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void LoadChampions()
        {
            _champions.Clear();
            IGameDataService data = ServiceLocator.Get<IGameDataService>();
            if (data == null)
            {
                return;
            }

            foreach (ChampionDefinition champion in data.GetAllChampions())
            {
                if (champion != null)
                {
                    _champions.Add(champion);
                }
            }
        }

        private bool TryBuildDraft()
        {
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            SaveGameData current = save?.CurrentSave;
            RealmId realm = current != null ? current.SelectedRealm : RealmId.None;
            if (!CharacterCreationDraft.TryCreate(realm, out _draft, out string error))
            {
                Font font = PresentationChrome.ResolveFont();
                _screen = CharacterCreationProductionLayout.BuildError(error, font);
                return false;
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(current);
            _alreadyConfirmed = snapshot.HasConfirmedChampion || SliceRunState.HasConfirmedChampion;
            if (snapshot.ClassFamily.HasValue)
            {
                _draft.TrySelectClassFamily(snapshot.ClassFamily.Value, out _);
            }

            if (current?.ChampionCustomization != null && snapshot.HasConfirmedChampion)
            {
                CharacterCreationLook.CopyInto(_draft.Customization, current.ChampionCustomization);
            }

            return true;
        }

        private void BuildPreview()
        {
            ReleasePreviewPresentation();
            _previewPresentationRoot =
                new GameObject("CharacterCreationPreviewPresentation");
            _previewPresentationRoot.transform.SetParent(transform, false);
            Light keyLight = CharacterCreationPreviewPresentation.EnsureOwnedLights(
                _previewPresentationRoot.transform);

            var previewObject = new GameObject("CreatorPreview");
            previewObject.transform.SetParent(_previewPresentationRoot.transform, false);
            previewObject.transform.position = new Vector3(0.85f, 0f, 3.4f);
            previewObject.transform.rotation = Quaternion.Euler(0f, 168f, 0f);
            _preview = previewObject.AddComponent<ChampionCustomizationController>();
            _preview.ApplyPresentation(_draft.Customization);

            var cameraObject = new GameObject("CreatorPreviewCamera");
            cameraObject.transform.SetParent(_previewPresentationRoot.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            _previewCamera = camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PresentationChrome.StoneVoid;
            camera.fieldOfView = 28f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 24f;
            camera.depth = 8f;
            camera.rect = new Rect(0.46f, 0.04f, 0.52f, 0.92f);
            cameraObject.transform.position = new Vector3(0.85f, 1.05f, 1.05f);
            cameraObject.transform.LookAt(new Vector3(0.85f, 0.72f, 3.4f));
            _previewMotion = cameraObject.AddComponent<CharacterCreationPreviewMotion>();
            _previewMotion.Configure(previewObject.transform, keyLight, camera);
        }

        private void OnDestroy()
        {
            ReleasePreviewPresentation();
        }

        private void ReleasePreviewPresentation()
        {
            GameObject ownedRoot = _previewPresentationRoot;
            _previewPresentationRoot = null;
            _preview = null;
            _previewMotion = null;
            _previewCamera = null;
            if (ownedRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(ownedRoot);
            }
            else
            {
                DestroyImmediate(ownedRoot);
            }
        }

        private void BuildUi()
        {
            Font font = PresentationChrome.ResolveFont();
            _screen = CharacterCreationProductionLayout.Build(
                _draft,
                font,
                SelectClass,
                () => MutateLook(_draft.CycleBodyBase),
                () => MutateLook(_draft.CycleArmorTint),
                value => MutateLook(() => _draft.SetSkinToneIndex(value)),
                () => MutateLook(_draft.CycleHairStyle),
                value => MutateLook(() => _draft.SetHairColorIndex(value)),
                value => MutateLook(() => _draft.SetEyeColorIndex(value)),
                () => MutateLook(_draft.CycleBodyPreset),
                () => MutateLook(_draft.ToggleHelmet),
                () => MutateLook(_draft.ToggleCape),
                ConfirmChampion);

            if (_alreadyConfirmed &&
                !string.IsNullOrWhiteSpace(SliceRunState.Champion.Username) &&
                _screen.Username != null)
            {
                _screen.Username.text = SliceRunState.Champion.Username;
            }

            RefreshSelection();
        }

        private void SelectClass(ClassFamily family)
        {
            if (_committing)
            {
                return;
            }

            if (!_draft.TrySelectClassFamily(family, out string error))
            {
                PresentValidation(error);
                return;
            }

            PresentValidation(string.Empty);
            RefreshSelection();
            RefreshPreview();
        }

        private void MutateLook(Action mutation)
        {
            if (_committing)
            {
                return;
            }

            mutation();
            PresentValidation(string.Empty);
            RefreshSelection();
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            _preview?.ApplyPresentation(_draft.Customization);
            if (_preview != null &&
                !FirstSessionAuthoredVisualBinder.TryBindChampion(
                    _preview.gameObject,
                    _draft.Realm,
                    _draft.Customization,
                    out _))
            {
                PresentValidation("Champion preview unavailable. Your choices are preserved.");
            }

            if (_preview != null &&
                !CharacterCreationPreviewPresentation.TryFrame(
                    _previewCamera,
                    _preview.transform))
            {
                PresentValidation("Champion preview unavailable. Your choices are preserved.");
            }
        }

        private void RefreshSelection()
        {
            CharacterCreationProductionLayout.PaintClassSelection(_screen, _draft.ClassFamily);
            if (_screen?.Look != null)
            {
                _screen.Look.text = CharacterCreationProductionLayout.FormatLookSummary(_draft.Customization);
            }

            CharacterCreationProductionLayout.PaintColorControls(_screen, _draft.Customization);

            if (_screen?.Confirm != null)
            {
                _screen.Confirm.interactable = _draft.ClassFamily.HasValue && !_committing;
            }

            if (_screen?.ConfirmLabel != null)
            {
                _screen.ConfirmLabel.text = _alreadyConfirmed ? "CONTINUE TO ARENA" : "ENTER THE REALM";
            }
        }

        private void ConfirmChampion()
        {
            if (_committing || _draft == null || !_draft.ClassFamily.HasValue)
            {
                PresentValidation("Choose a class path before entering the realm.");
                return;
            }

            _committing = true;
            string alreadyOwned = _alreadyConfirmed ? SliceRunState.Champion.Username : string.Empty;
            if (!CharacterCreationIdentity.TryClaim(
                    _screen?.Username != null ? _screen.Username.text : string.Empty,
                    alreadyOwned,
                    out string username,
                    out string usernameError))
            {
                _committing = false;
                PresentValidation(usernameError);
                return;
            }

            ClassFamily family = _draft.ClassFamily.Value;
            ChampionDefinition bound = CharacterCreationDraft.BindChampion(_champions, _draft.Realm, family);
            ChampionState state = bound != null
                ? BuildChampionState(bound)
                : new ChampionState { Id = "champion_unbound", DisplayName = "Champion", Realm = _draft.Realm };
            state.Family = family;
            state.Realm = _draft.Realm;
            state.Username = username;
            if (!TryPersistChampion(state, username, _draft.Customization, out string persistError))
            {
                _committing = false;
                PresentValidation(persistError);
                return;
            }

            SliceRunState.ConfirmChampion(state);

            CharacterCreationLook.TryClassLabel(family, out string classLabel);
            PresentValidation(string.Empty);
            if (_screen?.Look != null)
            {
                _screen.Look.text = username + "  ·  " + classLabel + "  —  entering the inner realm.";
            }

            AdvanceToCombat();
        }

        private static bool TryPersistChampion(
            ChampionState state,
            string username,
            ChampionCustomizationState appearance,
            out string error)
        {
            error = string.Empty;
            ISaveGameService save = ServiceLocator.Get<ISaveGameService>();
            if (save == null)
            {
                error = "Could not persist identity. Stay on create.";
                return false;
            }

            if (save.CurrentSave == null)
            {
                save.Load();
            }

            if (save.CurrentSave == null ||
                !FirstUserIdentityDerivation.IsSupportedRealm(save.CurrentSave.SelectedRealm))
            {
                error = "Could not persist identity. Stay on create.";
                return false;
            }

            MvpLoopSnapshot snapshot = MvpLoopSaveCodec.Read(save.CurrentSave);
            MvpLoopCommitResult commit = MvpLoopSaveAuthority.TryCommit(
                save,
                new MvpLoopCommitRequest(
                    Guid.NewGuid().ToString("N"),
                    save.CurrentSave.SelectedRealm,
                    state.Family,
                    true,
                    snapshot.LastResultId,
                    snapshot.LastBuildId,
                    snapshot.LastBuildLevel,
                    username,
                    appearance));
            if (!commit.Accepted)
            {
                error = "Could not persist identity. Stay on create.";
                return false;
            }

            return true;
        }

        private void AdvanceToCombat()
        {
            if (string.IsNullOrWhiteSpace(_combatSceneName))
            {
                _committing = false;
                PresentValidation("The inner realm is not configured.");
                return;
            }

            try
            {
                SceneManager.LoadScene(_combatSceneName);
            }
            catch (Exception)
            {
                _committing = false;
                PresentValidation("The inner realm could not be opened.");
            }
        }

        private static ChampionState BuildChampionState(ChampionDefinition champion)
        {
            var state = new ChampionState
            {
                Id = champion.Id,
                DisplayName = champion.DisplayName,
                Family = champion.Family,
                Subclass = champion.Subclass,
                Realm = champion.Realm,
                MaxHealth = champion.BaseStats.MaxHealth,
                MaxMana = champion.BaseStats.MaxMana,
                Attack = champion.BaseStats.Attack,
                Defense = champion.BaseStats.Defense,
                Speed = champion.BaseStats.Speed,
                CritRate = champion.BaseStats.CritRate,
                WeaponStyleId = champion.WeaponStyleId,
                OffhandStyleId = champion.OffhandStyleId
            };

            if (champion.BaseSkills != null)
            {
                foreach (SkillDefinition skill in champion.BaseSkills)
                {
                    if (skill != null && !string.IsNullOrWhiteSpace(skill.Id))
                    {
                        state.SkillIds.Add(skill.Id);
                    }
                }
            }

            return state;
        }

        private void PresentValidation(string message)
        {
            CharacterCreationProductionLayout.PresentValidation(_screen, message);
        }
    }
}
