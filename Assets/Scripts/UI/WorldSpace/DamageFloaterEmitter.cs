using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP
{
    /// <summary>
    /// 엔티티별 데미지 숫자 에미터/매니저. <see cref="EntityDamage"/>(fan-out)를 받아 World Space
    /// 데미지 플로터(<see cref="DamageFloater"/>)를 풀에서 꺼내 머리 위에 띄운다. *뷰 자체가 아니라*
    /// floater 풀을 소유·재사용하는 에미터다. M3b에서 UGUI(screen-space 투영) → World Space UI Toolkit로 전환.
    /// </summary>
    public class DamageFloaterEmitter : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        private const int MAX_FLOATERS = 4;
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/DamageFloater";
        private static readonly Vector3 HeadOffset = new Vector3(0f, 2.0f, 0f);

        // 동시 타격 시 화면-위로 적층 간격 + 약간의 수평 흔들림(카메라 거리 비례 → 화면상 일정). 튜닝값.
        private const float StackScreenFactor = 0.05f;
        private const float JitterScreenFactor = 0.015f;

        private readonly List<DamageFloater> _floaters = new List<DamageFloater>();
        private LOPEntityView _entityView;
        private Camera _camera;

        private void Awake()
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panelSettings == null || uxml == null)
            {
                Debug.LogError($"[DamageFloaterEmitter] 리소스 로드 실패: {PanelSettingsResource} / {UxmlResource}");
                return;
            }

            for (int i = 0; i < MAX_FLOATERS; i++)
            {
                // 패널 값이 세팅된 뒤 OnEnable이 패널을 빌드하도록 inactive로 구성 → 활성화.
                var go = new GameObject($"DamageFloater_{i}");
                go.SetActive(false);

                var document = go.AddComponent<UIDocument>();
                document.panelSettings = panelSettings;
                document.visualTreeAsset = uxml;
                document.worldSpaceSize = new Vector2(160f, 80f);

                var floater = go.AddComponent<DamageFloater>();
                go.SetActive(true);

                floater.Initialize(document.rootVisualElement, document.rootVisualElement.Q<Label>("damage-text"));
                _floaters.Add(floater);
            }
        }

        protected void Start()
        {
            _entityView = transform.parent.GetComponentInChildren<LOPEntityView>();
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);
        }

        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);

            foreach (var floater in _floaters)
            {
                if (floater != null)
                {
                    Destroy(floater.gameObject);
                }
            }
            _floaters.Clear();

            entity = null;
        }

        private void OnEntityDamage(EntityDamage entityDamage)
        {
            DamageFloater floater = _floaters.Find(f => !f.IsActive);
            if (floater == null && _floaters.Count > 0)
            {
                floater = _floaters[0]; // 모두 사용 중이면 가장 오래된 것 재활용.
            }
            if (floater == null)
            {
                return;
            }

            Vector3 headPosition = (_entityView != null && _entityView.visualGameObject != null)
                ? _entityView.visualGameObject.transform.position
                : entity.position;

            // 활성 플로터 수에 따라 중앙→좌우로 부채꼴 분산(단일은 중앙, 동시 타격은 겹치지 않게).
            int activeBefore = 0;
            foreach (var f in _floaters)
            {
                if (f.IsActive) activeBefore++;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            Vector3 anchor = headPosition + HeadOffset;
            // 원본처럼 동시 타격은 화면 위로 적층(activeBefore만큼) + 미세 수평 흔들림. 카메라 right/up 기준이라
            // 카메라를 돌려도 화면 수직/수평이 유지되고, 거리 비례라 화면상 간격이 일정하다.
            Vector3 right = _camera != null ? _camera.transform.right : Vector3.right;
            Vector3 up = _camera != null ? _camera.transform.up : Vector3.up;
            float distance = _camera != null ? Vector3.Distance(_camera.transform.position, anchor) : 5f;
            float stack = activeBefore * distance * StackScreenFactor;
            float jitter = Random.Range(-1f, 1f) * distance * JitterScreenFactor;
            Vector3 spawnPosition = anchor + up * stack + right * jitter;

            string text = entityDamage.isDodged ? "Dodge"
                : entityDamage.isCritical ? "Critical! " + entityDamage.damage
                : entityDamage.damage.ToString();

            Color color = entityDamage.isDodged ? new Color(0.7f, 0.7f, 0.7f)
                : entityDamage.isCritical ? new Color(1f, 0.55f, 0.2f)
                : new Color(1f, 0.9f, 0.45f);

            floater.Show(spawnPosition, text, color);
        }
    }
}
