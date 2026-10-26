using System;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 머리 위 "+N"의 주인(spec §7) — 경기가 시작될 때 월드 공간 패널 하나를 만들어 <see cref="FlapStreakFloater"/>에
    /// 연속 날갯짓(<see cref="FlappyFlapStreak"/>)과 내 새 그림을 물린다. 판정과 무관한 화면 연출이다.
    /// <para>패널 설정은 이름표·데미지 숫자와 같은 <c>UI/WorldSpaceNameplatePanelSettings</c>를 쓴다.</para>
    /// </summary>
    public sealed class FlappyFlapStreakFx : IStartable, IDisposable
    {
        private const string PanelSettingsResource = "UI/WorldSpaceNameplatePanelSettings";
        private const string UxmlResource = "UI/FlapStreak";

        private readonly FlappyFlapStreak _streak;
        private readonly ActorRegistry _actorRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly FlappyConfig _config;
        private GameObject _go;

        public FlappyFlapStreakFx(FlappyFlapStreak streak, ActorRegistry actorRegistry, IGameDataStore gameDataStore, FlappyConfig config)
        {
            _streak = streak;
            _actorRegistry = actorRegistry;
            _gameDataStore = gameDataStore;
            _config = config;
        }

        public void Start()
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panelSettings == null || uxml == null)
            {
                Debug.LogError($"[FlappyFlapStreakFx] 리소스 로드 실패: {PanelSettingsResource} / {UxmlResource}");
                return;
            }
            //  패널 값이 세팅된 뒤 OnEnable이 패널을 빌드하도록 꺼 둔 채 구성 → 켠다(DamageFloaterEmitter와 같은 순서).
            _go = new GameObject("FlapStreakFloater");
            _go.SetActive(false);
            var document = _go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            document.worldSpaceSize = new Vector2(160f, 80f);
            var floater = _go.AddComponent<FlapStreakFloater>();
            _go.SetActive(true);
            floater.Bind(document.rootVisualElement, document.rootVisualElement.Q<Label>("streak-text"),
                         _streak.Frame, MyVisual, _config.BodyRadius);
        }

        internal Transform MyVisual()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = _gameDataStore.userEntityId;
            var actor = string.IsNullOrEmpty(id) ? null : _actorRegistry.Get(id);
            if (actor == null)
            {
                return null;
            }
            var visual = actor.visualGameObject;
            return visual != null ? visual.transform : null;
        }

        public void Dispose()
        {
            if (_go != null)
            {
                if (UnityEngine.Application.isPlaying) { UnityEngine.Object.Destroy(_go); }
                else { UnityEngine.Object.DestroyImmediate(_go); }
            }
            _go = null;
        }
    }
}
