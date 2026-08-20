using System;
using AL.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Slice
{
    /// <summary>
    /// Character-creation ENTRY POINT for the legacy-runtime vertical slice. This is a deliberate stub:
    /// it proves the boot flow reaches character creation with the committed realm in
    /// <see cref="GreyboxRunState"/>, and raises <see cref="OnCharacterConfirmed"/> so the boot
    /// orchestrator can advance to the combat arena.
    ///
    /// The character-creation workstream replaces this stub with the real "create / confirm one
    /// champion" UI; the integration pass wires that UI to <see cref="OnCharacterConfirmed"/>.
    /// </summary>
    public class GreyboxCharacterCreationEntryController : MonoBehaviour
    {
        private const string AdvisorBark =
            "NVS-01 // ADVISOR: \"Your realm is sworn. Now, lord, name your champion.\"";

        /// <summary>Raised when the greybox champion is confirmed.</summary>
        public event Action OnCharacterConfirmed;

        private GameObject _canvasRoot;

        public void Present()
        {
            if (_canvasRoot != null)
            {
                return;
            }

            GreyboxUi.EnsureEventSystem();
            BuildUi();
        }

        private void BuildUi()
        {
            _canvasRoot = GreyboxUi.CreateCanvas("GreyboxCharacterCreationCanvas", 200);
            Font font = GreyboxUi.LoadFont();

            GreyboxUi.CreateBackdrop(_canvasRoot.transform, "Backdrop", new Color(0.006f, 0.010f, 0.016f, 0.96f));

            Text title = GreyboxUi.CreateText(
                _canvasRoot.transform, "Title", font, 40, new Color(1f, 0.88f, 0.62f), TextAnchor.MiddleCenter);
            GreyboxUi.PlaceTopCentered(title.rectTransform, 120f, 1200f, 64f);
            title.text = "CHARACTER CREATION // ENTRY POINT";

            string realmLabel = GreyboxRunState.HasRealm
                ? "REALM: " + GreyboxRunState.SelectedRealmId.ToString().ToUpperInvariant()
                : "REALM: (none)";
            Text realmText = GreyboxUi.CreateText(
                _canvasRoot.transform, "RealmLabel", font, 24, new Color(0.78f, 0.86f, 0.94f), TextAnchor.MiddleCenter);
            GreyboxUi.PlaceTopCentered(realmText.rectTransform, 200f, 1200f, 40f);
            realmText.text = realmLabel;

            Text bark = GreyboxUi.CreateText(
                _canvasRoot.transform, "Nvs01AdvisorBark", font, 20, new Color(0.55f, 0.85f, 0.95f), TextAnchor.MiddleCenter);
            GreyboxUi.PlaceTopCentered(bark.rectTransform, 268f, 1500f, 44f);
            bark.text = AdvisorBark;

            GreyboxUi.CreateButton(
                _canvasRoot.transform,
                "ConfirmChampion",
                "CONFIRM CHAMPION (GREYBOX STUB)",
                font,
                350f,
                620f,
                64f,
                Confirm);
        }

        private void Confirm()
        {
            Debug.Log($"[GREYBOX-SLICE] Character creation entry point confirmed for realm {GreyboxRunState.SelectedRealmId}.");

            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                Destroy(_canvasRoot);
                _canvasRoot = null;
            }

            OnCharacterConfirmed?.Invoke();
            Destroy(this);
        }
    }
}
