using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 삼색 그랩 HUD — 위 가운데 "라운드 N / 5"·남은 시간 막대·주문 띠, 오른쪽 점수판(누적 점수 + 이번 라운드 등수),
    /// 준비 중 큰 "라운드 N", 정답마다 "○○ 1등 +10" 띠, 라운드 끝 "라운드 결과", 내 오답 "오답 −1".
    /// 값은 매 프레임 서버 상태에서 읽는다(점수·잡음은 서버만 정한다).
    /// </summary>
    public class TriGrabHudViewModel : System.IDisposable
    {
        /// <summary>내 오답 글이 머리 위에 떠 있는 시간(초).</summary>
        public const float WrongSeconds = 0.9f;
        /// <summary>"○○ 1등 +10" 띠가 떠 있는 시간(초).</summary>
        public const float CorrectToastSeconds = 1.2f;
        /// <summary>남은 시간이 이 비율 아래면 막대가 빨개진다.</summary>
        public const float TimeLowFraction = 0.25f;

        public readonly struct ScoreRow
        {
            public readonly string Name;
            public readonly Color Seat;
            /// <summary>누적 점수.</summary>
            public readonly string Score;
            /// <summary>이번 라운드 등수 표시("✓1 +10"). 아직이면 빈 글.</summary>
            public readonly string Place;
            public readonly bool Mine;
            public ScoreRow(string name, Color seat, string score, string place, bool mine)
            {
                Name = name; Seat = seat; Score = score; Place = place; Mine = mine;
            }
        }

        /// <summary>
        /// 정답이 날 때마다 "○○ 1등 +10"을 잠깐 띄운다. 라운드가 바뀌면 처음부터 센다.
        /// 처음 받은 상태에 이미 있던 정답(다시 접속 등)은 띄우지 않는다.
        /// </summary>
        public sealed class CorrectAnnouncer
        {
            private readonly HashSet<string> announced = new HashSet<string>();
            private int round = -1;
            private string text = "";
            private float shownAt = -100f;

            /// <summary>지금 띄운(또는 마지막으로 띄운) 정답을 낸 사람.</summary>
            public string LastId { get; private set; } = "";

            public string Update(int currentRound, IReadOnlyList<(string id, string name, int place, int points)> players, float now)
            {
                bool first = round < 0;
                if (currentRound != round)
                {
                    round = currentRound;
                    announced.Clear();
                    text = "";
                }
                int bestPlace = 0;
                foreach (var p in players)
                {
                    if (p.place <= 0 || !announced.Add(p.id) || first) continue;
                    //  한 번에 여럿이 들어오면 가장 늦은 등수를 보인다(가장 최근 정답).
                    if (p.place > bestPlace)
                    {
                        bestPlace = p.place;
                        text = CorrectToast(p.name, p.place, p.points);
                        LastId = p.id;
                        shownAt = now;
                    }
                }
                return now - shownAt < CorrectToastSeconds ? text : "";
            }
        }

        private readonly TriGrabClientState state;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly List<ScoreRow> rows = new List<ScoreRow>();
        private readonly List<(string id, string name, int place, int points)> places = new List<(string, string, int, int)>();
        private readonly CorrectAnnouncer announcer = new CorrectAnnouncer();

        private int seenMistakes = -1;
        private float wrongAt = -100f;

        public TriGrabHudViewModel(TriGrabClientState state, IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry, GameFramework.Runner.IRunner runner)
        {
            this.state = state;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.runner = runner;
        }

        public bool Received => state.Received;
        public string RuleText { get; private set; } = "";
        public string RoundText { get; private set; } = "";
        /// <summary>준비 중 가운데 큰 글("라운드 N / 5"). 아니면 빈 글.</summary>
        public string PrepTitle { get; private set; } = "";
        /// <summary>정답·라운드 결과·판 끝 띠. 아니면 빈 글.</summary>
        public string ToastText { get; private set; } = "";
        /// <summary>라운드를 끝낸 정답처럼 결과 띠와 겹친 "○○ N등 +점". 결과 제목 위에 작게. 아니면 빈 글.</summary>
        public string ToastLead { get; private set; } = "";
        /// <summary>라운드 결과 아래 줄("1등 ○○ +10 · 2등 …"). 아니면 빈 글.</summary>
        public string ToastSub { get; private set; } = "";
        public Color ToastColor { get; private set; } = Color.white;
        public IReadOnlyList<ScoreRow> Rows => rows;
        /// <summary>내 오답 글을 지금 보이나와 그 투명도(0~1).</summary>
        public float WrongAlpha { get; private set; }
        /// <summary>이번 라운드 남은 시간 비율(0~1). 음수면 막대를 숨긴다.</summary>
        public float TimeFraction { get; private set; } = -1f;

        public void Tick()
        {
            if (!state.Received) return;
            var t = runner?.tickUpdater;
            double renderTick = t == null || t.interval <= 0d ? 0d : (t.elapsedTime - t.interval) / t.interval;
            long tick = (long)System.Math.Floor(renderTick);
            var phase = state.PhaseAt(tick);

            RuleText = RuleTextFor(state);
            RoundText = RoundLabel(state.Round, state.TotalRounds);
            PrepTitle = phase == TriGrabPhase.Prep ? RoundText : "";
            TimeFraction = TimeLeft(phase, renderTick, state.Clock.LiveStartTick, state.RoundLimitTick);

            places.Clear();
            foreach (var id in state.PlayerIds)
            {
                state.TryGetPlayer(id, out var p);
                places.Add((id, NameOf(id), p?.RoundPlace ?? 0, p?.RoundPoints ?? 0));
            }
            string correct = announcer.Update(state.Round, places, Time.unscaledTime);

            var (text, sub, lead) = ComposeToast(phase, state.Round, places, correct, Toast(phase, state.MatchWinnerId, NameOf(state.MatchWinnerId)));
            ToastText = text; ToastSub = sub; ToastLead = lead;
            ToastColor = phase == TriGrabPhase.Over ? TriGrabPalette.Seat(state.SeatOf(state.MatchWinnerId))
                : phase == TriGrabPhase.Resolved ? Color.white
                : TriGrabPalette.Seat(state.SeatOf(announcer.LastId));

            rows.Clear();
            string me = playerContext.entityId;
            foreach (int i in RankOrder(state))
            {
                string id = state.PlayerIds[i];
                state.TryGetPlayer(id, out var score);
                rows.Add(new ScoreRow(NameOf(id), TriGrabPalette.Seat(i), $"{score?.Score ?? 0}",
                    PlaceBadge(score?.RoundPlace ?? 0, score?.RoundPoints ?? 0), id == me));
            }

            //  실수 수가 오른 순간이 내 오답이다(서버가 −1을 정한 뒤라 조금 늦지만 되돌릴 일은 없다).
            int mistakes = state.TryGetPlayer(me, out var mine) ? mine.Mistakes : 0;
            if (seenMistakes >= 0 && mistakes > seenMistakes) wrongAt = Time.unscaledTime;
            seenMistakes = mistakes;
            WrongAlpha = FlashAlpha(Time.unscaledTime - wrongAt, WrongSeconds);
        }

        /// <summary>점수판 순서 — 누적 점수 높은 순, 같으면 좌석(등록) 순. 좌석 번호 목록을 돌려준다.</summary>
        public static List<int> RankOrder(TriGrabClientState state)
        {
            var order = new List<int>();
            for (int i = 0; i < state.PlayerIds.Count; i++) order.Add(i);
            int ScoreOf(int i) => state.TryGetPlayer(state.PlayerIds[i], out var p) ? p.Score : 0;
            //  List.Sort는 안정 정렬이 아니라 좌석 번호로 끝까지 가른다.
            order.Sort((a, b) => ScoreOf(a) != ScoreOf(b) ? ScoreOf(b).CompareTo(ScoreOf(a)) : a.CompareTo(b));
            return order;
        }

        /// <summary>
        /// 가운데 띠 셋(큰 글, 아래 줄, 위 작은 글). 판 끝 = 우승 띠, 라운드 끝 = "라운드 N 결과" + 결과 줄
        /// (라운드를 끝낸 정답의 "○○ N등 +점"은 그 위에 작게 — 결과 띠에 덮이지 않게), 그 밖 = 방금 난 정답 띠.
        /// </summary>
        public static (string text, string sub, string lead) ComposeToast(TriGrabPhase phase, int round,
            IReadOnlyList<(string id, string name, int place, int points)> places, string correctToast, string overToast)
        {
            if (phase == TriGrabPhase.Over) return (overToast, "", "");
            if (phase == TriGrabPhase.Resolved) return (RoundResultTitle(round), RoundResultLine(places), correctToast ?? "");
            return (correctToast ?? "", "", "");
        }

        public static string RoundLabel(int round, int totalRounds) => round > 0 ? $"라운드 {round} / {totalRounds}" : "";

        /// <summary>이번 라운드 등수 표시. 아직 정답 전이면 빈 글.</summary>
        public static string PlaceBadge(int place, int points) => place > 0 ? $"✓{place} +{points}" : "";

        public static string CorrectToast(string name, int place, int points) => $"{name} {place}등 +{points}";

        public static string RoundResultTitle(int round) => $"라운드 {round} 결과";

        /// <summary>라운드 결과 아래 줄. 정답 낸 사람만 등수 순으로.</summary>
        public static string RoundResultLine(IReadOnlyList<(string id, string name, int place, int points)> players)
        {
            var done = new List<(string id, string name, int place, int points)>();
            foreach (var p in players) if (p.place > 0) done.Add(p);
            if (done.Count == 0) return "아무도 못 찾았다";
            done.Sort((a, b) => a.place.CompareTo(b.place));
            var parts = new List<string>();
            foreach (var p in done) parts.Add($"{p.place}등 {p.name} +{p.points}");
            return string.Join(" · ", parts);
        }

        /// <summary>남은 시간 비율. 준비 중은 가득, Live는 상한까지 줄고, 그 밖엔 숨긴다(-1).</summary>
        public static float TimeLeft(TriGrabPhase phase, double renderTick, long liveStartTick, long roundLimitTick)
        {
            if (roundLimitTick <= liveStartTick) return -1f;
            if (phase == TriGrabPhase.Prep) return 1f;
            if (phase != TriGrabPhase.Live) return -1f;
            double left = (roundLimitTick - renderTick) / (roundLimitTick - liveStartTick);
            return Mathf.Clamp01((float)left);
        }

        private string NameOf(string id) => PlayerNames.For(entityRegistry, id, $"{state.SeatOf(id) + 1}P");

        /// <summary>1라운드 전엔 서버가 아직 주문을 안 뽑았다(기본값이 가짜로 보이지 않게 빈 글).</summary>
        public static string RuleTextFor(TriGrabClientState state) => state.Round >= 1 ? RuleRichText(state.Rule) : "";

        /// <summary>주문 글. 주문에 든 색 이름은 그 색으로 칠한다.</summary>
        public static string RuleRichText(in TriGrabRule rule)
        {
            if (rule.Type == TriGrabRuleType.Match) return rule.KoreanText;
            string name = TriGrabRule.ColorName(rule.Color);
            string tinted = $"<color={TriGrabPalette.InkHtml(rule.Color)}>{name}</color>";
            return rule.KoreanText.Replace(name, tinted);
        }

        /// <summary>판 끝 띠. 누적 1위가 혼자면 "○○ 우승!", 같은 점수면 "경기 종료". 판 끝이 아니면 빈 글.</summary>
        public static string Toast(TriGrabPhase phase, string winnerId, string winnerName)
        {
            if (phase != TriGrabPhase.Over) return "";
            return string.IsNullOrEmpty(winnerId) ? "경기 종료" : $"{winnerName} 우승!";
        }

        /// <summary>떠오른 뒤 지난 초에 따른 투명도. 처음 60%는 그대로, 남은 동안 사라진다.</summary>
        public static float FlashAlpha(float elapsed, float duration)
        {
            if (elapsed < 0f || elapsed >= duration) return 0f;
            float fadeFrom = duration * 0.6f;
            return elapsed <= fadeFrom ? 1f : 1f - (elapsed - fadeFrom) / (duration - fadeFrom);
        }

        /// <summary>내 몸의 머리 위 자리(월드). 몸이 없으면 false.</summary>
        public bool TryGetMyHead(ActorRegistry actors, out Vector3 position)
        {
            position = default;
            if (actors == null || !actors.TryGet(playerContext.entityId, out var actor) || actor?.visualGameObject == null) return false;
            position = actor.visualGameObject.transform.position + Vector3.up * 2.6f;
            return true;
        }

        public void Dispose() { }
    }
}
