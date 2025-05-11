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
        [SerializeField] private RectTransform joyStickRoot;
        [SerializeField] private Image joyStickBackground;
        [SerializeField] private Image joyStickHandle;

        [Inject]
        private PlayerInputManager playerInputManager;

        public Vector2 inputVector { get; private set; }

        private float maxRadius;
        private Vector2 initPosition;

        private void Start()
        {
            maxRadius = (joyStickBackground.rectTransform.sizeDelta.x - joyStickHandle.rectTransform.sizeDelta.x) / 2;
            initPosition = joyStickBackground.rectTransform.anchoredPosition;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                playerInputManager.SetJump(true);
            }

            if (inputVector != Vector2.zero)
            {
                playerInputManager.SetHorizontal(inputVector.x);
                playerInputManager.SetVertical(inputVector.y);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(joyStickRoot, eventData.position, eventData.pressEventCamera, out var localPoint))
            {
                joyStickBackground.rectTransform.anchoredPosition = localPoint;

                OnDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
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
            inputVector = Vector2.zero;
            joyStickHandle.rectTransform.anchoredPosition = Vector2.zero;
            joyStickBackground.rectTransform.anchoredPosition = initPosition;
        }
    }
}
