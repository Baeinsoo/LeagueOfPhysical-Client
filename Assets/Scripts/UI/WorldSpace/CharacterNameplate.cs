using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 머리 위 World Space 이름표. 이름·칭호·배너·레벨(<see cref="PlayerLook"/> +
    /// <see cref="CosmeticCatalog"/>, <see cref="NameplateLook"/>로 풀이)과 HP바를 함께 띄운다.
    /// 엔티티별로 생성되어 visualGameObject(머리)를 따라가며 카메라를 향해 billboard한다.
    /// World Space UI Toolkit(PanelSettings RenderMode=WorldSpace) 첫 실사용. 스크린스페이스 윈도우
    /// 매니저 밖이며(가이드라인 — 월드 추적 UI), UIDocument를 직접 소유한다.
    ///
    /// 이름·칭호·배너는 판 시작 때 생성 데이터로 한 번 온 뒤 안 바뀐다(룩은 그 판 스냅샷) — 그래서
    /// Start에서 한 번만 그린다. HP만 EntityHealthChanged로 계속 갱신한다.
    ///
    /// 실행순서를 CameraController(3000) 뒤(3100)로 두어, 카메라가 LateUpdate에서 이동한 뒤 위치/billboard를
    /// 계산한다(그렇지 않으면 카메라 추적이 한 프레임 어긋나 떨린다).
    /// HP는 EntityHealthChanged(스냅샷 적용 시 변경분 발행 — 로컬=UserEntitySnap, 원격=EntitySnap)로 갱신 — 권위 상태 기반.
    /// maxHP/초기값은 World.Entity의 World.Health(순수 C# 코어)에서 읽는다(Model A — 뷰가 코어를 pull).
    /// </summary>
    [DefaultExecutionOrder(3100)]
    public class CharacterNameplate : MonoBehaviour, ICleanup
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private CosmeticCatalog cosmeticCatalog;

        [Inject]
        private CharacterDecorationSettings decorations;

        public LOPActor actor { get; private set; }

        public void SetEntity(LOPActor actor)
        {
            this.actor = actor;
        }

        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/Nameplate";
        private static readonly Vector3 HeadOffset = new Vector3(0f, 2.0f, 0f);

        private const string BannerPlainClass = "banner--plain";
        private const string BannerStripeClass = "banner--stripe";

        // 거리 비례 스케일 → 화면상 크기 일정(World Space 원근 확대/축소 상쇄). 튜닝값.
        private const float ScreenSizeFactor = 0.1f;

        private Camera _camera;
        private GameObject _panelGameObject;
        private VisualElement _hpFill;
        private int _maxHp;
        private int _currentHp;
        private System.IDisposable _subscription;

        protected void Start()
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(actor.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            _maxHp = health != null ? health.Max : 1;
            _currentHp = health != null ? health.Current : _maxHp;

            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panelSettings == null || uxml == null)
            {
                Debug.LogError($"[Nameplate] 리소스 로드 실패: {PanelSettingsResource} / {UxmlResource}");
                return;
            }

            // 패널 값이 세팅된 뒤 OnEnable이 패널을 빌드하도록 inactive로 구성 → 활성화.
            _panelGameObject = new GameObject($"Nameplate_{actor.entityId}");
            _panelGameObject.SetActive(false);

            var document = _panelGameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.worldSpaceSize = new Vector2(240f, 44f);

            _panelGameObject.SetActive(true);

            VisualElement root = document.rootVisualElement;
            _hpFill = root.Q<VisualElement>("hp-bar-fill");

            VisualElement hpBarBg = root.Q<VisualElement>("hp-bar-bg");
            if (hpBarBg != null)
            {
                hpBarBg.style.display = decorations.ShowHealthBar ? DisplayStyle.Flex : DisplayStyle.None;
            }

            BindNameBlock(root, worldEntity);
            UpdateHpBar();

            _subscription = GlobalMessagePipe.GetSubscriber<string, EntityHealthChanged>().Subscribe(actor.entityId, OnEntityHealthChanged);
        }

        // PlayerLook이 없는 몸(몬스터·심판)에는 이름 블록을 안 그린다 — NameplateLook.Resolve(null)이 주는
        // "플레이어"/"plain" 기본값은 테스트용이지, 화면에 띄우라는 뜻이 아니다.
        private void BindNameBlock(VisualElement root, GameFramework.World.Entity worldEntity)
        {
            VisualElement banner = root.Q<VisualElement>("banner");
            if (banner == null)
            {
                return;
            }

            PlayerLook look = worldEntity?.Get<PlayerLook>();
            if (look == null)
            {
                banner.style.display = DisplayStyle.None;
                return;
            }

            NameplateText text = NameplateLook.Resolve(look, cosmeticCatalog);

            Label nameLabel = root.Q<Label>("name-label");
            if (nameLabel != null)
            {
                nameLabel.text = text.Name;
            }

            Label levelLabel = root.Q<Label>("level-label");
            if (levelLabel != null)
            {
                levelLabel.text = $"Lv {text.Level}";
            }

            Label titleLabel = root.Q<Label>("title-label");
            if (titleLabel != null)
            {
                bool hasTitle = string.IsNullOrEmpty(text.Title) == false;
                titleLabel.style.display = hasTitle ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasTitle)
                {
                    titleLabel.text = text.Title;
                }
            }

            banner.RemoveFromClassList(BannerPlainClass);
            banner.RemoveFromClassList(BannerStripeClass);
            banner.AddToClassList(text.BannerStyle == "stripe" ? BannerStripeClass : BannerPlainClass);
        }

        public void Cleanup()
        {
            _subscription?.Dispose();

            if (_panelGameObject != null)
            {
                Destroy(_panelGameObject);
                _panelGameObject = null;
            }

            actor = null;
        }

        private void OnEntityHealthChanged(EntityHealthChanged e)
        {
            _currentHp = e.current;
            _maxHp = e.max;
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

            var worldEntity = entityRegistry.Get(actor.entityId);
            Vector3 basePosition = (actor.visualGameObject != null)
                ? actor.visualGameObject.transform.position
                : worldEntity != null ? GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity) : Vector3.zero;

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
