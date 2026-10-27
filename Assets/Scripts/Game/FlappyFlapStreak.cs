using System;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 연속 날갯짓(spec §7, 사용자 결정 10-26) — 기절 없이 이어진 먹힌 날갯짓 수를 세고, 이번 판 최고를 기록지에 적는다. 판정과 무관하다.
    /// <para>세기는 누른 순간(예측) <see cref="FlappyFlapSound.LiveFlap"/>으로 — 시뮬이 받는 조건과 같다. 기절은 예측 <see cref="FlappyStun"/>으로
    /// 본다(서버 교정도 같은 칸을 고친다). 교정이 늦게 만든 기절은 교정이 온 장에 0, 서버가 지운 예측 기절은 되살리지 않는다(spec §10, 받아들임).</para>
    /// <para>그리는 쪽(<see cref="FlappyFlapComboFx"/>)은 <see cref="Flapped"/>·<see cref="Crashed"/>·<see cref="Racing"/>만 본다.</para>
    /// </summary>
    public sealed class FlappyFlapStreak : ITickable, IDisposable
    {
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly IGameDataStore _gameDataStore;
        private readonly GameFramework.Runner.IRunner _runner;
        private readonly FlappyFlapSound _flapSound;
        private readonly MatchRecordSheet _sheet;
        private readonly FlapStreak _streak = new FlapStreak();
        private bool _racing;

        /// <summary>센 날갯짓 하나 — 그 뒤의 연속 수.</summary>
        public event Action<int> Flapped;

        /// <summary>내 새가 기절에 들어가 연속이 0이 됐다.</summary>
        public event Action Crashed;

        internal int Count => _streak.Count;
        internal int Best => _streak.Best;

        /// <summary>내 새가 달리는 중(기절·결승·몸 없음이 아님). 꼬리는 이때만 그린다.</summary>
        public bool Racing => _racing;

        public FlappyFlapStreak(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
                                GameFramework.Runner.IRunner runner, FlappyFlapSound flapSound, MatchRecordSheet sheet)
        {
            _entityRegistry = entityRegistry;
            _gameDataStore = gameDataStore;
            _runner = runner;
            _flapSound = flapSound;
            _sheet = sheet;
            _flapSound.LiveFlap += OnLiveFlap;
        }

        private void OnLiveFlap()
        {
            var ticker = _runner.tickUpdater;
            if (ticker == null)
            {
                return;
            }
            //  먹힌 날갯짓이면 달리는 중이다 — 다음 Tick을 기다리지 않고 바로.
            _racing = true;
            if (_streak.Flap(ticker.tick) == false)
            {
                return;
            }
            if (_streak.Count == _streak.Best)
            {
                _sheet.Set(FlappyRecordMetrics.FlapStreak, _streak.Best);
            }
            Flapped?.Invoke(_streak.Count);
        }

        public void Tick()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = _gameDataStore.userEntityId;
            var mine = string.IsNullOrEmpty(id) ? null : _entityRegistry.Get(id);
            if (mine == null)
            {
                _racing = false;
                return;
            }
            bool stunned = (mine.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f;
            if (_streak.ObserveStun(stunned))
            {
                Crashed?.Invoke();
            }
            _racing = stunned == false && mine.Get<FinishState>()?.Finished != true;
        }

        public void Dispose() => _flapSound.LiveFlap -= OnLiveFlap;
    }
}
