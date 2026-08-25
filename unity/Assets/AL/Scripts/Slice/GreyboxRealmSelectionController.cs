using System;
using System.Collections.Generic;
using AL.Core;
using AL.Data.Definitions;
using AL.Services.Local;
using UnityEngine;
using UnityEngine.UI;

namespace AL.Slice
{
    /// <summary>
    /// Greybox realm-selection screen for the legacy-runtime vertical slice. It reads the four realms
    /// directly from the hardcoded <see cref="LocalGameDataService"/> records (no ServiceLocator, no
    /// catalog/save/determinism authority) and, on selection, commits the choice to
    /// <see cref="GreyboxRunState"/> and raises <see cref="OnRealmCommitted"/> so the boot orchestrator
    /// can advance to the character-creation entry point.
    /// </summary>
    public class GreyboxRealmSelectionController : MonoBehaviour
    {
        private const string AdvisorBark =
            "NVS-01 // ADVISOR: \"Choose the realm that will define your command, my lord.\"";

        /// <summary>Raised after the realm is committed to <see cref="GreyboxRunState"/>.</summary>
        public event Action<RealmId> OnRealmCommitted;

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
            _canvasRoot = GreyboxUi.CreateCanvas("GreyboxRealmSelectionCanvas", 200);
            Font font = GreyboxUi.LoadFont();

            GreyboxUi.CreateBackdrop(_canvasRoot.transform, "Backdrop", new Color(0.006f, 0.010f, 0.016f, 0.96f));

            Text title = GreyboxUi.CreateText(
                _canvasRoot.transform, "Title", font, 44, new Color(1f, 0.88f, 0.62f), TextAnchor.MiddleCenter);
            GreyboxUi.PlaceTopCentered(title.rectTransform, 64f, 1200f, 70f);
            title.text = "ANOTHER LIFE";

            Text subtitle = GreyboxUi.CreateText(
                _canvasRoot.transform, "Subtitle", font, 22, new Color(0.78f, 0.86f, 0.94f), TextAnchor.MiddleCenter);
            GreyboxUi.PlaceTopCentered(subtitle.rectTransform, 136f, 1200f, 36f);
            subtitle.text = "GREYBOX VERTICAL SLICE // REALM SELECTION";

            Text bark = GreyboxUi.CreateText(
                _canvasRoot.transform, "Nvs01AdvisorBark", font, 20, new Color(0.55f, 0.85f, 0.95f), TextAnchor.MiddleCenter);
            GreyboxUi.PlaceTopCentered(bark.rectTransform, 196f, 1400f, 44f);
            bark.text = AdvisorBark;

            List<RealmDefinition> realms = new List<RealmDefinition>(new LocalGameDataService().GetAllRealms());
            float yOffset = 268f;
            foreach (RealmDefinition realm in realms)
            {
                CreateRealmButton(_canvasRoot.transform, realm, font, yOffset);
                yOffset += 78f;
            }
        }

        private void CreateRealmButton(Transform parent, RealmDefinition realm, Font font, float yOffset)
        {
            RealmId realmId = realm.Id;
            string label = string.IsNullOrWhiteSpace(realm.RealmName)
                ? realmId.ToString()
                : realm.RealmName.ToUpperInvariant();

            GreyboxUi.CreateButton(
                parent,
                "Realm_" + realmId,
                label,
                font,
                yOffset,
                760f,
                62f,
                () => Commit(realmId));
        }

        private void Commit(RealmId realmId)
        {
            GreyboxRunState.CommitRealm(realmId);
            Debug.Log($"[GREYBOX-SLICE] Realm committed to local run state: {realmId}");

            if (_canvasRoot != null)
            {
                _canvasRoot.SetActive(false);
                Destroy(_canvasRoot);
                _canvasRoot = null;
            }

            OnRealmCommitted?.Invoke(realmId);
            Destroy(this);
        }
    }
}
