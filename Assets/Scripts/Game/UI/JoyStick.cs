using GameFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class JoyStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private Image joyStickArea;
        [SerializeField] private Image joyStickBackground;
        [SerializeField] private Image joyStickHandle;

        [Inject]
        private PlayerInputManager playerInputManager;

        [Inject]
        private CameraController cameraController;

        public Vector2 inputVector { get; private set; }

        private RectTransform rectTransform;
        private float maxRadius;
        private Vector2 initPosition;
        private bool isDragging;

        private void Start()
        {
            rectTransform = GetComponent<RectTransform>();
            maxRadius = (joyStickBackground.rectTransform.sizeDelta.x - joyStickHandle.rectTransform.sizeDelta.x) / 2;
            initPosition = joyStickBackground.rectTransform.anchoredPosition;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                playerInputManager.SetJump(true);
            }

            if (isDragging && inputVector != Vector2.zero)
            {
                float yAngle = cameraController.MainCamera.transform.eulerAngles.y;
                Quaternion cameraRotation = Quaternion.Euler(0, yAngle, 0);

                Vector3 transformedInput = cameraRotation * new Vector3(inputVector.x, 0, inputVector.y);

                playerInputManager.SetHorizontal(transformedInput.x);
                playerInputManager.SetVertical(transformedInput.z);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.pointerId < 0 && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(joyStickArea.rectTransform, eventData.position, eventData.pressEventCamera) == false)
            {
                return;
            }

            isDragging = true;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                joyStickBackground.rectTransform.anchoredPosition = localPoint;

                OnDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDragging == false)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joyStickBackground.rectTransform, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                if (localPoint.magnitude > maxRadius)
                {
                    localPoint = localPoint.normalized * maxRadius;
                }

                joyStickHandle.rectTransform.anchoredPosition = localPoint;

                inputVector = localPoint.normalized;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (isDragging == false)
            {
                return;
            }

            isDragging = false;
            inputVector = Vector2.zero;
            joyStickHandle.rectTransform.anchoredPosition = Vector2.zero;
            joyStickBackground.rectTransform.anchoredPosition = initPosition;
        }
    }
}
