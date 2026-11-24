using System.Collections.Generic;
using UnityEngine;
using TG = LOP.TriGrab;

namespace LOP.UI
{
    /// <summary>
    /// 삼색 그랩 HUD — 위 가운데 "라운드 N / 10"·남은 정답 수·남은 시간 막대·주문 띠(목표 글은 미끼 색), 오른쪽 점수판(점수순),
    /// 라운드 끝 띠, 판 끝 우승 띠와 순위 줄. 값은 매 프레임 서버 상태에서 읽는다(점수는 서버만 정한다).
    /// 원판 위 주문·연출 글(+1, 땡!)은 월드 쪽 <see cref="TriGrabOverlayView"/>가 그린다.
    /// </summary>
    public class TriGrabHudViewModel : System.IDisposable
    {
        /// <summary>남은 시간이 이 비율 아래면 막대가 빨개진다.</summary>
        public const float TimeLowFraction = 0.25f;

        public readonly struct ScoreRow
        {
            public readonly string Name;
            public readonly Color Seat;
            public readonly string Score;
            /// <summary>지금 순위("1위"). 같은 점수는 같은 순위.</summary>
            public readonly string Place;
            public readonly bool Mine;
            public ScoreRow(string name, Color seat, string score, string place, bool mine)
            {
                Name = name; Seat = seat; Score = score; Place = place; Mine = mine;
            }
        }

        private readonly TriGrabClientState state;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly EntityRenderClock renderClock;
        private readonly List<ScoreRow> rows = new List<ScoreRow>();

        public TriGrabHudViewModel(TriGrabClientState state, IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry, EntityRenderClock renderClock)
        {
            this.state = state;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.renderClock = renderClock;
        }

        public bool Received => state.Received;
        public string RuleText { get; private set; } = "";
        public string RoundText { get; private set; } = "";
        /// <summary>준비 중 가운데 큰 글("라운드 N / 10"). 아니면 빈 글.</summary>
        public string PrepTitle { get; private set; } = "";
        public string ToastText { get; private set; } = "";
        /// <summary>판 끝 순위 줄. 아니면 빈 글.</summary>
        public string ToastSub { get; private set; } = "";
        public Color ToastColor { get; private set; } = Color.white;
        public IReadOnlyList<ScoreRow> Rows => rows;
        /// <summary>이번 라운드 남은 시간 비율(0~1). 음수면 막대를 숨긴다.</summary>
        public float TimeFraction { get; private set; } = -1f;

        public void Tick()
        {
            if (!state.Received) return;
            double renderTick = TriGrabRenderTicks.Remote(renderClock);
            if (double.IsNaN(renderTick)) renderTick = 0d;
            var phase = state.PhaseAt((long)System.Math.Floor(renderTick));

            RuleText = RuleTextFor(state);
            RoundText = RoundLine(state.Round, state.TotalRounds, phase, state.RemainingOk);
            PrepTitle = phase == TG.TriGrabPhase.Prep ? RoundLabel(state.Round, state.TotalRounds) : "";
            TimeFraction = TimeLeft(phase, renderTick, state.PhaseEndTick);

            ToastText = Toast(phase, state.Round, TopNames());
            ToastSub = phase == TG.TriGrabPhase.Over ? FinalLine(Standings()) : "";
            ToastColor = phase == TG.TriGrabPhase.Over ? TriGrabPalette.Correct : Color.white;

            rows.Clear();
            string me = playerContext.entityId;
            foreach (int i in RankOrder(state))
            {
                string id = state.PlayerIds[i];
                state.TryGetPlayer(id, out var score);
                int place = state.PlaceOf(id);
                rows.Add(new ScoreRow(NameOf(id), TriGrabPalette.Seat(i), $"{score?.Score ?? 0}",
                                      place > 0 ? $"{place}위" : "", id == me));
            }
        }

        private List<(string name, int place, int score)> Standings()
        {
            var list = new List<(string, int, int)>();
            foreach (int i in RankOrder(state))
            {
                string id = state.PlayerIds[i];
                state.TryGetPlayer(id, out var score);
                list.Add((NameOf(id), state.PlaceOf(id), score?.Score ?? 0));
            }
            return list;
        }

        private List<string> TopNames()
        {
            var names = new List<string>();
            foreach (var id in state.PlayerIds)
            {
                if (state.PlaceOf(id) == 1) names.Add(NameOf(id));
            }
            return names;
        }

        /// <summary>점수판 순서 — 점수 높은 순, 같으면 좌석(등록) 순. 좌석 번호 목록을 돌려준다.</summary>
        public static List<int> RankOrder(TriGrabClientState state)
        {
            var order = new List<int>();
            for (int i = 0; i < state.PlayerIds.Count; i++) order.Add(i);
            int ScoreOf(int i) => state.TryGetPlayer(state.PlayerIds[i], out var p) ? p.Score : 0;
            //  List.Sort는 안정 정렬이 아니라 좌석 번호로 끝까지 가른다.
            order.Sort((a, b) => ScoreOf(a) != ScoreOf(b) ? ScoreOf(b).CompareTo(ScoreOf(a)) : a.CompareTo(b));
            return order;
        }

        public static string RoundLabel(int round, int totalRounds) => round > 0 ? $"라운드 {round} / {totalRounds}" : "";

        /// <summary>위 띠 첫 줄. 잡기 중엔 남은 정답 수를 붙인다.</summary>
        public static string RoundLine(int round, int totalRounds, TG.TriGrabPhase phase, int remainingOk)
        {
            string label = RoundLabel(round, totalRounds);
            if (label.Length == 0) return "";
            return phase == TG.TriGrabPhase.Live ? $"{label} · 정답 {remainingOk}장 남음" : label;
        }

        /// <summary>남은 시간 비율. 준비 중은 가득, 잡기 중엔 시간 상한(8초)까지 줄고, 그 밖엔 숨긴다(-1).</summary>
        public static float TimeLeft(TG.TriGrabPhase phase, double renderTick, long phaseEndTick)
        {
            if (phase == TG.TriGrabPhase.Prep) return 1f;
            if (phase != TG.TriGrabPhase.Live) return -1f;
            double left = (phaseEndTick - renderTick) / TG.TriGrabConfig.LiveLimitTicks;
            return Mathf.Clamp01((float)left);
        }

        private string NameOf(string id) => PlayerNames.For(entityRegistry, id, $"{state.SeatOf(id) + 1}P");

        /// <summary>1라운드 전엔 서버가 아직 주문을 안 뽑았다(기본값이 가짜로 보이지 않게 빈 글).</summary>
        public static string RuleTextFor(TriGrabClientState state) => state.Round >= 1 ? RuleRichText(state.Rule) : "";

        /// <summary>주문 글. 목표 글(색 이름)은 미끼 색으로 칠한다 — 원판 가운데 주문과 같은 속임.</summary>
        public static string RuleRichText(TG.TriGrabRule rule)
        {
            string target = TriGrabOverlayView.RuleTarget(rule);
            string tinted = rule.Type == TG.TriGrabRuleType.Match
                ? target
                : $"<color={TriGrabPalette.InkHtml(rule.Lure)}>{target}</color>";
            string suffix = TriGrabOverlayView.RuleSuffix(rule);
            //  "인 카드…"는 목표 글에 붙이고, "카드를 잡아라"는 띄어 쓴다.
            return rule.Type == TG.TriGrabRuleType.Match
                ? $"{TriGrabOverlayView.RuleKind(rule)} {tinted} {suffix}"
                : $"{TriGrabOverlayView.RuleKind(rule)} {tinted}{suffix}";
        }

        /// <summary>가운데 띠. 판 끝 = 우승(공동이면 이름 여럿), 라운드 끝 = "라운드 N 끝", 그 밖엔 빈 글.</summary>
        public static string Toast(TG.TriGrabPhase phase, int round, IReadOnlyList<string> winners)
        {
            if (phase == TG.TriGrabPhase.Over)
            {
                return winners == null || winners.Count == 0 ? "경기 종료" : $"{string.Join(" · ", winners)} 우승!";
            }
            return phase == TG.TriGrabPhase.Resolved ? $"라운드 {round} 끝" : "";
        }

        /// <summary>판 끝 순위 줄("1위 가 5 · 2위 나 3").</summary>
        public static string FinalLine(IReadOnlyList<(string name, int place, int score)> standings)
        {
            var parts = new List<string>();
            foreach (var s in standings) parts.Add($"{s.place}위 {s.name} {s.score}");
            return string.Join(" · ", parts);
        }

        public void Dispose() { }
    }
}
