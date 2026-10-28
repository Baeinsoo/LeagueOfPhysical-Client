using System;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 연속 날갯짓 연출 "집중형"의 주인(spec §7, 사용자 결정 10-26). 시뮬이 받은 날갯짓마다 흐름(<see cref="FlapComboFx"/>)에 걸음을 넣고,
    /// 맵 고비(spec 2026-10-27)엔 조용한 화음(<see cref="FlappySfx.Milestone"/>)을 한 번 낸다. 내 새가 기절에 들어가면 꼬리를 끈다.
    /// <para>날갯짓 소리는 <see cref="FlappyFlapSound"/>의 원래 소리 그대로다 — 콤보마다 음을 올리지 않는다(사용자 결정).</para>
    /// <para>경기가 시작될 때 화면 공간 판 하나를 만들어 <see cref="FlapComboView"/>에 흐름과 내 새 그림을 물린다. 판정과 무관한 화면 연출이다.</para>
    /// </summary>
    public sealed class FlappyFlapComboFx : IStartable, IDisposable
    {
        private const string PanelSettingsResource = "UI/FlapComboPanelSettings";
        private const string UxmlResource = "UI/FlapCombo";

        private readonly FlappyFlapStreak _streak;
        private readonly ActorRegistry _actorRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly FlappyConfig _config;
        private readonly IFlappyAudio _audio;
        private GameObject _go;

        internal FlapComboFx Fx { get; } = new FlapComboFx(new System.Random());

        public FlappyFlapComboFx(FlappyFlapStreak streak, ActorRegistry actorRegistry, IGameDataStore gameDataStore,
                                 FlappyConfig config, IFlappyAudio audio)
        {
            _streak = streak;
            _actorRegistry = actorRegistry;
            _gameDataStore = gameDataStore;
            _config = config;
            _audio = audio;
            _streak.Flapped += OnFlapped;
            _streak.Crashed += OnCrashed;
        }

        private void OnFlapped(FlapComboBeat beat)
        {
            Fx.Queue(beat.Count, beat.Milestone, beat.DashGain);
            if (beat.Milestone >= 0)
            {
                _audio.Play(FlappySfx.Milestone);
            }
        }

        private void OnCrashed() => Fx.Crash();

        public void Start()
        {
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            var uxml = Resources.Load<VisualTreeAsset>(UxmlResource);
            if (panelSettings == null || uxml == null)
            {
                Debug.LogError($"[FlappyFlapComboFx] 리소스 로드 실패: {PanelSettingsResource} / {UxmlResource}");
                return;
            }
            //  패널 값이 세팅된 뒤 OnEnable이 패널을 빌드하도록 꺼 둔 채 구성 → 켠다(DamageFloaterEmitter와 같은 순서).
            _go = new GameObject("FlapComboView");
            _go.SetActive(false);
            var document = _go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = uxml;
            var view = _go.AddComponent<FlapComboView>();
            _go.SetActive(true);
            document.rootVisualElement.pickingMode = PickingMode.Ignore;
            view.Bind(document.rootVisualElement.Q("combo-root"), Fx, MyVisual, () => _streak.Racing, _config.BodyRadius);
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
            _streak.Flapped -= OnFlapped;
            _streak.Crashed -= OnCrashed;
            if (_go != null)
            {
                if (UnityEngine.Application.isPlaying) { UnityEngine.Object.Destroy(_go); }
                else { UnityEngine.Object.DestroyImmediate(_go); }
            }
            _go = null;
        }
    }
}
