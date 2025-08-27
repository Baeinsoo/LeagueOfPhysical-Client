using TMPro;
using UnityEngine;

namespace LOP
{
    public class DamageUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;
        [SerializeField] private float moveSpeed = 75.0f;
        [SerializeField] private float lifetime = 1.0f;
        [SerializeField] private AnimationCurve scaleCurve = new AnimationCurve(
            new Keyframe(0f, 0.7f, 0f, 2f),
            new Keyframe(0.1f, 1.2f, 0f, 0f),
            new Keyframe(1f, 1f, -0.5f, -0.5f));

        private bool _isActive = false;
        public bool isActive
        { 
            get => _isActive;
            set
            {
                _isActive = value;
                gameObject.SetActive(value);
            }
        }
        public int index { get; set; }
        public Vector3 offset { get; set; }

        private LOPEntity entity;
        private Camera mainCamera;
        private RectTransform rectTransform;
        private Canvas parentCanvas;
        private float lifeTimer;
        private Vector3 worldPositionOffset = Vector3.up * 1.5f;
        private float timeElapsed = 0f;

        private void Awake()
        {
            mainCamera = Camera.main;
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnDestroy()
        {
            Debug.Log($"DamageUI for entity index {index} is being destroyed.");
        }

        public void Clear()
        {
            index = 0;
            offset = Vector3.zero;
            isActive = false;
        }

        public void ShowDamage(LOPEntity entity, string text, Vector3 offset, Canvas parentCanvas)
        {
            this.isActive = true;
            this.entity = entity;
            this.text.text = text;
            this.lifeTimer = lifetime;
            this.timeElapsed = 0f;
            this.offset = offset;
            this.parentCanvas = parentCanvas;

            transform.SetParent(parentCanvas.transform, false);
        }

        private void LateUpdate()
        {
            if (entity == null)
            {
                return;
            }

            lifeTimer -= Time.smoothDeltaTime;
            timeElapsed += Time.smoothDeltaTime;

            if (lifeTimer <= 0)
            {
                isActive = false;
                return;
            }

            //  LateUpdate가 visualGameObject의 외형 LateUpdate가 이후에 실행 보장이 되어야 함.
            LOPEntityView entityView = entity.transform.parent.GetComponentInChildren<LOPEntityView>();
            if (entityView == null || entityView.visualGameObject == null)
            {
                Debug.LogWarning("EntityView or visualGameObject is null in DamageUI.");
                return;
            }

            Vector3 screenPoint = mainCamera.WorldToScreenPoint(entityView.visualGameObject.transform.position + worldPositionOffset);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvas.GetComponent<RectTransform>(),
                screenPoint,
                null,
                out var localPoint))
            {
                localPoint.y += moveSpeed * timeElapsed;
                rectTransform.anchoredPosition = (Vector2)offset + localPoint;
            }

            Color color = text.color;
            color.a = lifeTimer / lifetime;
            text.color = color;

            float scale = scaleCurve.Evaluate(timeElapsed / lifetime);
            rectTransform.localScale = new Vector3(scale, scale, 1f);

            //  ease out
        }
    }
}
