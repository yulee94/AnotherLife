using AL.ChampionMode.Control;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AL.ChampionMode.UI
{
    /// <summary>
    /// Hold-to-block touch control. Mirrors the keyboard/gamepad "Block" action for
    /// touch devices by driving <see cref="ChampionController.SetBlocking"/> on press
    /// and release.
    /// </summary>
    public class ChampionBlockButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private ChampionController _controller;

        public void Setup(ChampionController controller)
        {
            _controller = controller;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _controller?.SetBlocking(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _controller?.SetBlocking(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _controller?.SetBlocking(false);
        }
    }
}
