using AL.ChampionMode.Control;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AL.ChampionMode.UI
{
    public class ChampionMoveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private ChampionController _controller;
        [SerializeField] private Vector2 _moveInput;

        public void Setup(ChampionController controller, Vector2 moveInput)
        {
            _controller = controller;
            _moveInput = moveInput;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _controller?.SetExternalMoveInput(_moveInput);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _controller?.SetExternalMoveInput(Vector2.zero);
        }
    }
}

