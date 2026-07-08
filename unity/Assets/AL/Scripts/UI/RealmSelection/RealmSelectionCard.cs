using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AL.Core;
using AL.Data.Definitions;
using System;

namespace AL.UI.RealmSelection
{
    public class RealmSelectionCard : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Button _selectButton;

        private RealmDefinition _definition;
        private Action<RealmId> _onSelected;

        public void Setup(RealmDefinition definition, Action<RealmId> onSelected)
        {
            _definition = definition;
            _onSelected = onSelected;

            _nameText.text = definition.RealmName;
            _descriptionText.text = definition.Description;

            if (definition.Icon != null)
                _iconImage.sprite = definition.Icon;

            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(HandleSelection);
        }

        private void HandleSelection()
        {
            _onSelected?.Invoke(_definition.Id);
        }
    }
}
