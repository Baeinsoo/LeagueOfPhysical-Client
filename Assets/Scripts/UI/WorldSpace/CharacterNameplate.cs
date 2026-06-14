using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 엔티티 머리 위 World Space HP바 네임플레이트. 엔티티별로 생성되어 visualGameObject(머리)를 따라가며
    /// 카메라를 향해 billboard하고, EntityDamage 이벤트로 HP바를 갱신한다.
    /// World Space UI Toolkit(PanelSettings RenderMode=WorldSpace) 첫 실사용. 스크린스페이스 윈도우
    /// 매니저 밖이며(가이드라인 — 월드 추적 UI), UIDocument를 직접 소유한다.
    ///
    /// 실행순서를 CameraController(3000) 뒤(3100)로 두어, 카메라가 LateUpdate에서 이동한 뒤 위치/billboard를
    /// 계산한다(그렇지 않으면 카메라 추적이 한 프레임 어긋나 떨린다).
    /// HP는 EntityDamage(presentation fan-out, DamageFloaterEmitter와 동일 신호)로 갱신 — 발화가 보장된 경로.
    /// maxHP/초기값은 스폰 시 초기화된 HealthComponent에서 읽는다.
    /// </summary>
    [DefaultExecutionOrder(3100)]
    public class CharacterNameplate : MonoEntityView<LOPEntity, LOPEntityController>
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/Nameplate";
        private static readonly Vector3 HeadOffset = new Vector3(0f, 2.0f, 0f);

        // 거리 비례 스케일 → 화면상 크기 일정(World Space 원근 확대/축소 상쇄). 튜닝값.
        private const float ScreenSizeFactor = 0.1f;

        private Camera _camera;
        private GameObject _panelGameObject;
        private VisualElement _hpFill;
        private LOPEntityView _entityView;
        private int _maxHp;
        private int _currentHp;

        protected void Start()
        {
            _entityView = transform.parent.GetComponentInChildren<LOPEntityView>();

            HealthComponent health = entity.GetEntityComponent<HealthComponent>();
            _maxHp = health != null ? health.maxHP : 1;
            _currentHp = health != null ? health.currentHP : _maxHp;

            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panelSettings == null || uxml == null)
            {
                Debug.LogError($"[Nameplate] 리소스 로드 실패: {PanelSettingsResource} / {UxmlResource}");
                return;
            }

            // 패널 값이 세팅된 뒤 OnEnable이 패널을 빌드하도록 inactive로 구성 → 활성화.
            _panelGameObject = new GameObject($"Nameplate_{entity.entityId}");
            _panelGameObject.SetActive(false);

            var document = _panelGameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.worldSpaceSize = new Vector2(200f, 30f);

            _panelGameObject.SetActive(true);

            _hpFill = document.rootVisualElement.Q<VisualElement>("hp-bar-fill");
            UpdateHpBar();

            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
        }

        public override void Cleanup()
        {
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);

            if (_panelGameObject != null)
            {
                Destroy(_panelGameObject);
                _panelGameObject = null;
            }

            base.Cleanup();
        }

        private void OnEntityDamage(EntityDamage entityDamage)
        {
            _currentHp = (int)entityDamage.remainingHP;
            UpdateHpBar();
        }

        private void UpdateHpBar()
        {
            if (_hpFill == null || _maxHp <= 0)
            {
                return;
            }

            float percent = Mathf.Clamp01((float)_currentHp / _maxHp) * 100f;
            _hpFill.style.width = Length.Percent(percent);
        }

        private void LateUpdate()
        {
            if (_panelGameObject == null)
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

            Vector3 basePosition = (_entityView != null && _entityView.visualGameObject != null)
                ? _entityView.visualGameObject.transform.position
                : entity.position;

            Vector3 worldPosition = basePosition + HeadOffset;
            _panelGameObject.transform.position = worldPosition;

            // 스크린 정렬 빌보드: 카메라 회전을 그대로 복사 → 패널이 화면 평면과 평행이라 어느 위치/각도서든
            // 항상 수평이 맞고 카메라를 정면으로 향한다.
            _panelGameObject.transform.rotation = _camera.transform.rotation;

            // 화면상 크기를 거리와 무관하게 일정하게 유지.
            float distance = Vector3.Distance(worldPosition, _camera.transform.position);
            _panelGameObject.transform.localScale = Vector3.one * (distance * ScreenSizeFactor);
        }
    }
}
