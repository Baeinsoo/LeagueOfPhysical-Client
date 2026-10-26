using System;
using VContainer.Unity;

namespace LOP
{
    /// <summary>머리 위 "+N" 한 장(spec §7). 그리는 쪽(<see cref="FlapStreakFloater"/>)은 이것만 본다.</summary>
    public readonly struct FlapStreakFrame
    {
        public static readonly FlapStreakFrame Hidden = new FlapStreakFrame(false, string.Empty, 0f, 1f);

        public readonly bool Visible;
        public readonly string Text;
        public readonly float Alpha;
        public readonly float Scale;

        public FlapStreakFrame(bool visible, string text, float alpha, float scale)
        {
            Visible = visible;
            Text = text;
            Alpha = alpha;
            Scale = scale;
        }
    }

    /// <summary>
    /// 연속 날갯짓(spec §7, 사용자 결정 10-26) — 기절 없이 이어진 먹힌 날갯짓 수를 세고, 이번 판 최고를 기록지에 적는다. 판정과 무관하다.
    /// <para>세기는 누른 순간(예측) <see cref="FlappyFlapSound.LiveFlap"/>으로 — 시뮬이 받는 조건과 같다. 기절은 예측 <see cref="FlappyStun"/>으로
    /// 본다(서버 교정도 같은 칸을 고친다). 교정이 늦게 만든 기절은 교정이 온 장에 0, 서버가 지운 예측 기절은 되살리지 않는다(spec §10, 받아들임).</para>
    /// <para>시각은 화면 시계(<c>Time.unscaledTimeAsDouble</c>) — "+N"의 팝·사라짐이 틱 걸음(0.02초)으로 끊겨 보이지 않게.</para>
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

        //  시험 이음매 — 실제로는 화면 시계.
        internal Func<double> now = () => UnityEngine.Time.unscaledTimeAsDouble;

        internal int Count => _streak.Count;
        internal int Best => _streak.Best;

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
            //  먹힌 날갯짓이면 달리는 중이다 — 다음 Tick을 기다리지 않고 바로 보이게.
            _racing = true;
            if (_streak.Flap(ticker.tick, now()) && _streak.Count == _streak.Best)
            {
                _sheet.Set(FlappyRecordMetrics.FlapStreak, _streak.Best);
            }
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
            _streak.ObserveStun(stunned);
            _racing = stunned == false && mine.Get<FinishState>()?.Finished != true;
        }

        /// <summary>지금 "+N"을 어떻게 그릴지. 기절·결승·0이면 숨김.</summary>
        public FlapStreakFrame Frame()
        {
            if (_racing == false || _streak.Count <= 0)
            {
                return FlapStreakFrame.Hidden;
            }
            float since = (float)(now() - _streak.LastFlapSeconds);
            float alpha = FlapStreakLook.Alpha(since);
            if (alpha <= 0f)
            {
                return FlapStreakFrame.Hidden;
            }
            return new FlapStreakFrame(true, FlapStreakLook.Text(_streak.Count), alpha, FlapStreakLook.Scale(since));
        }

        public void Dispose() => _flapSound.LiveFlap -= OnLiveFlap;
    }
}
