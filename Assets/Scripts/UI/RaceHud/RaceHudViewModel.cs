using System.Collections.Generic;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// Flappy 정보 HUD ViewModel(spec §1·§5). 진행 막대·순위·기록 시간에 더해 구간 띠·구간 배너·LAST STRETCH!를 정하고,
    /// 그 순간의 소리(구간·팡파르·음악 올리기)를 낸다.
    /// 값은 매 틱 변하는 월드 값이라 View가 매 프레임 <see cref="Refresh"/>를 부른다(<see cref="FlapPadViewModel"/>와 같은 짝).
    /// 점·띠 목록과 배너 값은 프레임마다 바뀌므로 R3 대신 그대로 내준다.
    /// <para>배너 시간은 HUD 한 장의 <see cref="RaceHudFrame.NowSeconds"/>(러너 시계)로 잰다 — 기록 시간과 같은 시계라 시험이 장만으로 돈다.</para>
    /// </summary>
    public class RaceHudViewModel
    {
        private readonly IRaceHudSource _source;
        private readonly IFlappyAudio _audio;
        private readonly RaceHudFrame _frame = new RaceHudFrame();
        private readonly RankChangeDebounce _rankGate = new RankChangeDebounce();
        private readonly CourseSectionTracker _sectionTracker = new CourseSectionTracker();
        private readonly List<RaceHudDot> _dots = new List<RaceHudDot>();
        private readonly List<RaceHudBand> _bands = new List<RaceHudBand>();

        private readonly ReactiveProperty<string> _clockText = new ReactiveProperty<string>(RaceClock.Format(0d));
        private readonly ReactiveProperty<int> _rank = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> _total = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<float> _myProgress = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<bool> _showProgress = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> _showRank = new ReactiveProperty<bool>(true);
        private readonly Subject<Unit> _rankPopped = new Subject<Unit>();

        private CourseSection _bannerSection;
        private double _bannerStart;
        private bool _hasBanner;
        private bool _finalStretchOn;

        private RankChange _delta;
        private double _deltaStart;
        private bool _hasDelta;

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

        /// <summary>지금 뜬 큰 글자 띠. 결승했거나 내 새가 없으면 늘 None.</summary>
        public HudBannerKind BannerKind { get; private set; }

        /// <summary>구간 이름(구간 배너).</summary>
        public string BannerTitle { get; private set; } = string.Empty;

        /// <summary>"n막"(구간 배너).</summary>
        public string BannerActText { get; private set; } = string.Empty;

        /// <summary>막 색(구간 배너 아래 띠).</summary>
        public Color BannerColor { get; private set; }

        public float BannerAlpha { get; private set; }

        /// <summary>LAST STRETCH! 크기(1.5배 → 1배). 구간 배너는 늘 1.</summary>
        public float BannerScale { get; private set; } = 1f;

        public const string RankUpText = "+1";

        //  화면 글꼴(블랙한산스·주아)에 진짜 빼기(U+2212)가 없고 대체 글꼴도 없다 — 시제품의 "−1" 대신 하이픈.
        public const string RankDownText = "-1";

        /// <summary>순위 숫자 옆 "+1"/"-1"(spec §5 추월). 없으면 빈 문자열.</summary>
        public string DeltaText { get; private set; } = string.Empty;

        public Color DeltaColor { get; private set; }

        public float DeltaAlpha { get; private set; }

        /// <summary>떠오른 높이(px, 위로).</summary>
        public float DeltaRise { get; private set; }

        public RaceHudViewModel(IRaceHudSource source, IFlappyAudio audio)
        {
            _source = source;
            _audio = audio;
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
            //  튕김·±1·소리가 같은 판정 하나를 쓴다 — 출발 2.5초·0.8초 거르기, 결승 뒤 변화는 추월이 아님(RankChangeDebounce).
            RankChange change = _rankGate.Observe(rank, elapsed, finished);
            if (change != RankChange.None)
            {
                _rankPopped.OnNext(Unit.Default);
                _delta = change;
                _deltaStart = _frame.NowSeconds;
                _hasDelta = true;
                _audio.Play(change == RankChange.Up ? FlappySfx.RankUp : FlappySfx.RankDown);
            }
            UpdateDelta(finished);
            _rank.Value = rank;
            _total.Value = _frame.Birds.Count;
            _showRank.Value = !finished;

            _showProgress.Value = _frame.HasCourse;
            _dots.Clear();
            float mine = 0f;
            float myX = 0f;
            bool hasMine = false;
            foreach (var bird in _frame.Birds)
            {
                float ratio = RaceProgress.Ratio(bird.X, _frame.StartX, _frame.FinishX);
                if (bird.Id == _frame.MyId)
                {
                    mine = ratio;
                    myX = bird.X;
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
            UpdateBanner(hasMine, myX, elapsed, finished);
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

        private void UpdateBanner(bool hasMine, float myX, double elapsed, bool finished)
        {
            if (hasMine)
            {
                //  출발(기록 시간이 0보다 커진 뒤)부터 결승 전까지만 알린다 — 줄 서기 중·결승 뒤엔 자리만 따라간다.
                bool racing = elapsed > 0d && finished == false;
                int entered = _sectionTracker.Observe(_frame.Sections, myX, racing);
                if (entered != CourseSectionTracker.None)
                {
                    Enter(_frame.Sections[entered]);
                }
            }

            BannerKind = HudBannerKind.None;
            BannerAlpha = 0f;
            BannerScale = 1f;
            //  결승·탈락 뒤엔 거둔다 — 결승 도장(36%·56%)·관전 화면과 겹치지 않게.
            if (_hasBanner == false || hasMine == false || finished)
            {
                return;
            }
            float t = (float)(_frame.NowSeconds - _bannerStart);
            if (_bannerSection.Final)
            {
                if (t < HudBannerTimeline.StretchTotal)
                {
                    BannerKind = HudBannerKind.LastStretch;
                    BannerAlpha = HudBannerTimeline.StretchAlpha(t);
                    BannerScale = HudBannerTimeline.StretchScale(t);
                }
            }
            else if (t < HudBannerTimeline.SectionTotal)
            {
                BannerKind = HudBannerKind.Section;
                BannerAlpha = HudBannerTimeline.SectionAlpha(t);
            }
        }

        //  들어선 순간 한 번: 배너 시작 시각을 적고 소리를 낸다. 마지막 구간은 구간 소리 대신 팡파르 + 음악 올리기(시제품 그대로).
        //  음악은 결승 때 FlappyRaceCues가 StopMusic으로 끄고, 그게 속도를 1로 되돌린다(조각 2).
        private void Enter(CourseSection section)
        {
            _bannerSection = section;
            _bannerStart = _frame.NowSeconds;
            _hasBanner = true;
            BannerTitle = section.Title;
            BannerActText = $"{section.Act}막";
            BannerColor = FlappyRacePalette.ActColor(section.Act);

            if (section.Final)
            {
                _audio.Play(FlappySfx.LastStretch);
                if (_finalStretchOn == false)
                {
                    _finalStretchOn = true;
                    _audio.SetFinalStretch(true);
                }
                return;
            }
            _audio.Play(FlappySfx.Section);
        }

        //  1.2초 동안 투명도 1 → 0, 초당 20 px 떠오름(시제품 rankDelta). 결승하면 지운다 — 순위 판도 숨는다.
        private void UpdateDelta(bool finished)
        {
            float t = _hasDelta ? (float)(_frame.NowSeconds - _deltaStart) : HudBannerTimeline.DeltaTotal;
            if (finished || t >= HudBannerTimeline.DeltaTotal)
            {
                _hasDelta = false;
                DeltaText = string.Empty;
                DeltaAlpha = 0f;
                DeltaRise = 0f;
                return;
            }
            bool up = _delta == RankChange.Up;
            DeltaText = up ? RankUpText : RankDownText;
            DeltaColor = up ? FlappyRacePalette.RankUp : FlappyRacePalette.RankDown;
            DeltaAlpha = HudBannerTimeline.DeltaAlpha(t);
            DeltaRise = HudBannerTimeline.DeltaRise(t);
        }
    }
}
