using System.Collections.Generic;
using R3;

namespace LOP.UI
{
    /// <summary>
    /// Flappy 정보 HUD ViewModel(spec §1). 진행 막대·순위·기록 시간을 노출한다.
    /// 값은 매 틱 변하는 월드 값이라 View가 매 프레임 <see cref="Refresh"/>를 부른다(<see cref="FlapPadViewModel"/>와 같은 짝).
    /// 점 목록은 프레임마다 통째로 바뀌므로 R3 대신 목록을 그대로 내준다.
    /// </summary>
    public class RaceHudViewModel
    {
        private readonly IRaceHudSource _source;
        private readonly RaceHudFrame _frame = new RaceHudFrame();
        private readonly RankChangeDebounce _rankGate = new RankChangeDebounce();
        private readonly List<RaceHudDot> _dots = new List<RaceHudDot>();
        private readonly List<RaceHudBand> _bands = new List<RaceHudBand>();

        private readonly ReactiveProperty<string> _clockText = new ReactiveProperty<string>(RaceClock.Format(0d));
        private readonly ReactiveProperty<int> _rank = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> _total = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<float> _myProgress = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<bool> _showProgress = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> _showRank = new ReactiveProperty<bool>(true);
        private readonly Subject<Unit> _rankPopped = new Subject<Unit>();

        /// <summary>왼쪽 위 기록 시간(`m:ss.s`).</summary>
        public ReadOnlyReactiveProperty<string> ClockText => _clockText;

        /// <summary>내 순위(1부터). 0이면 내 새가 없다 — 화면은 순위 판을 숨긴다.</summary>
        public ReadOnlyReactiveProperty<int> Rank => _rank;

        /// <summary>"/ n"의 n — 지금 판에 남은 새 수.</summary>
        public ReadOnlyReactiveProperty<int> Total => _total;

        /// <summary>내 진행 비율(막대의 호박색 채움).</summary>
        public ReadOnlyReactiveProperty<float> MyProgress => _myProgress;

        /// <summary>막대를 보일지. 결승선을 모르는 맵에서 점을 0에 몰아 두느니 숨긴다.</summary>
        public ReadOnlyReactiveProperty<bool> ShowProgress => _showProgress;

        /// <summary>순위판을 보일지. 내가 결승하면 숨긴다 — 결승·결과 화면이 등수를 보여 주고, 관전 화면의
        /// "나가기" 버튼이 이 판과 같은 자리(오른쪽 위)에 겹친다(RaceHud.uss .race-rank-plate vs
        /// RaceSpectateView.uss .spectate-leave).</summary>
        public ReadOnlyReactiveProperty<bool> ShowRank => _showRank;

        /// <summary>순위 숫자를 튕길 때(<see cref="RankChangeDebounce"/>가 허락한 변화).</summary>
        public Observable<Unit> RankPopped => _rankPopped;

        /// <summary>막대의 점들. 남 먼저, 나 마지막 — 그리는 순서가 곧 겹침 순서라 내 점이 위에 온다.</summary>
        public IReadOnlyList<RaceHudDot> Dots => _dots;

        /// <summary>진행 막대 바탕의 구간 띠(막 색). 표식이 없는 맵이면 비어 있다.</summary>
        public IReadOnlyList<RaceHudBand> Bands => _bands;

        public RaceHudViewModel(IRaceHudSource source)
        {
            _source = source;
        }

        public void Refresh()
        {
            _frame.Clear();
            _source.Fill(_frame);

            double elapsed = RaceClock.ElapsedSeconds(_frame.NowSeconds, _frame.StartTick, _frame.SecondsPerTick, _frame.MyFinishedTick);
            _clockText.Value = RaceClock.Format(elapsed);

            RaceStandings.Order(_frame.Birds);
            int rank = RaceStandings.RankOf(_frame.Birds, _frame.MyId);
            bool finished = _frame.MyFinishedTick != FinishState.NotFinished;
            if (_rankGate.Observe(rank, elapsed, finished) != RankChange.None)
            {
                _rankPopped.OnNext(Unit.Default);
            }
            _rank.Value = rank;
            _total.Value = _frame.Birds.Count;
            _showRank.Value = !finished;

            _showProgress.Value = _frame.HasCourse;
            _dots.Clear();
            float mine = 0f;
            bool hasMine = false;
            foreach (var bird in _frame.Birds)
            {
                float ratio = RaceProgress.Ratio(bird.X, _frame.StartX, _frame.FinishX);
                if (bird.Id == _frame.MyId)
                {
                    mine = ratio;
                    hasMine = true;
                    continue;
                }
                //  사용자 결정(10-25): 상대는 칸별 색이 아니라 전원 같은 중립색이다 — 게임이 색을 배정하지 않는다.
                _dots.Add(new RaceHudDot(ratio, false, FlappyRacePalette.OpponentNeutral));
            }
            if (hasMine)
            {
                _dots.Add(new RaceHudDot(mine, true, FlappyRacePalette.Mine));
            }
            _myProgress.Value = mine;
            UpdateBands();
        }

        //  띠는 구간 시작 → 다음 구간 시작(마지막은 결승선). 출발 ~ 첫 구간은 띠가 없다(막대 바탕 그대로) — 시제품도 그랬다.
        private void UpdateBands()
        {
            _bands.Clear();
            if (_frame.HasCourse == false)
            {
                return;
            }
            var sections = _frame.Sections;
            for (int i = 0; i < sections.Count; i++)
            {
                float from = RaceProgress.Ratio(sections[i].X, _frame.StartX, _frame.FinishX);
                float to = i + 1 < sections.Count ? RaceProgress.Ratio(sections[i + 1].X, _frame.StartX, _frame.FinishX) : 1f;
                _bands.Add(new RaceHudBand(from, to, FlappyRacePalette.ActColor(sections[i].Act)));
            }
        }
    }
}
