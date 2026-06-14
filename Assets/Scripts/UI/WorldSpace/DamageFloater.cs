using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// World Space 데미지 플로터(데미지 숫자). 히트 지점에 스폰되어 월드 공간에서 위로 떠오르며
    /// 카메라를 향해 billboard, 거리비례 스케일로 화면상 크기 일정, 수명 동안 페이드아웃한다.
    /// DamageFloaterEmitter가 풀링해 재사용한다. World Space 패턴은 M3a(CharacterNameplate)와 동일.
    /// 실행순서 3100(CameraController 3000 뒤)로 카메라 이동 후 위치/billboard 계산 → 떨림 방지.
    /// </summary>
    [DefaultExecutionOrder(3100)]
    public class DamageFloater : MonoBehaviour
    {
        // 거리 비례 스케일 → 화면상 크기 일정(World Space 원근 상쇄). 튜닝값.
        private const float ScreenSizeFactor = 0.1f;
        private const float Lifetime = 1.0f;
        private const float RiseSpeed = 1.5f; // 월드 유닛/초

        private Camera _camera;
        private VisualElement _root;
        private Label _label;
        private float _timer;
        private bool _active;
        private Vector3 _worldPosition;
        private Color _baseColor = Color.white;

        public bool IsActive => _active;

        /// <summary>DamageFloaterEmitter가 UIDocument 빌드 후 root/label을 주입하고 숨긴다.</summary>
        public void Initialize(VisualElement root, Label label)
        {
            _root = root;
            _label = label;
            Hide();
        }

        public void Show(Vector3 worldPosition, string text, Color color)
        {
            _worldPosition = worldPosition;
            _baseColor = color;
            _timer = Lifetime;
            _active = true;

            if (_label != null)
            {
                _label.text = text;
                _label.style.color = color;
            }

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
            }
        }

        public void Hide()
        {
            _active = false;

            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }
        }

        private void LateUpdate()
        {
            if (!_active)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera == null)
            {
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Hide();
                return;
            }

            // 월드 공간에서 위로 상승.
            _worldPosition += Vector3.up * (RiseSpeed * Time.deltaTime);

            transform.position = _worldPosition;
            // 스크린 정렬 빌보드: 카메라 회전 복사(항상 수평·정면).
            transform.rotation = _camera.transform.rotation;
            // 화면상 크기 일정.
            float distance = Vector3.Distance(_worldPosition, _camera.transform.position);
            transform.localScale = Vector3.one * (distance * ScreenSizeFactor);

            // 페이드아웃.
            if (_label != null)
            {
                Color color = _baseColor;
                color.a = Mathf.Clamp01(_timer / Lifetime);
                _label.style.color = color;
            }
        }
    }
}
