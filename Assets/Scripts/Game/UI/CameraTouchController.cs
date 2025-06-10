using UnityEngine;
using UnityEngine.EventSystems;

namespace LOP
{
    public class CameraTouchController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private CameraController cameraController;

        private bool isDragging;
        private Vector2 lastTouchPosition;
   
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.pointerId < 0 && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            isDragging = true;
            lastTouchPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging == false)
            {
                return;
            }

            Vector2 deltaPosition = eventData.position - lastTouchPosition;
            lastTouchPosition = eventData.position;

            if (cameraController != null)
            {
                cameraController.ProcessTouchInput(deltaPosition);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDragging == false)
            {
                return;
            }

            isDragging = false;
        }
    }
}
