using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 삼색 그랩 HUD — 위 가운데 주문 띠·라운드, 오른쪽 점수판, 준비 중 큰 "라운드 N", 정답 띠, 내 오답 "오답 −1".
    /// 값은 매 프레임 서버 상태에서 읽는다(점수·잡음은 서버만 정한다).
    /// </summary>
    public class TriGrabHudViewModel : System.IDisposable
    {
        /// <summary>내 오답 글이 머리 위에 떠 있는 시간(초).</summary>
        public const float WrongSeconds = 0.9f;

        public readonly struct ScoreRow
        {
            public readonly string Name;
            public readonly Color Seat;
            public readonly string Score;
            public readonly bool Mine;
            public ScoreRow(string name, Color seat, string score, bool mine) { Name = name; Seat = seat; Score = score; Mine = mine; }
        }

        private readonly TriGrabClientState state;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly List<ScoreRow> rows = new List<ScoreRow>();

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
        /// <summary>준비 중 가운데 큰 글("라운드 N"). 아니면 빈 글.</summary>
        public string PrepTitle { get; private set; } = "";
        /// <summary>라운드 끝·판 끝 띠. 아니면 빈 글.</summary>
        public string ToastText { get; private set; } = "";
        public Color ToastColor { get; private set; } = Color.white;
        public IReadOnlyList<ScoreRow> Rows => rows;
        /// <summary>내 오답 글을 지금 보이나와 그 투명도(0~1).</summary>
        public float WrongAlpha { get; private set; }

        public void Tick()
        {
            if (!state.Received) return;
            var t = runner?.tickUpdater;
            long tick = t == null || t.interval <= 0d ? 0 : (long)System.Math.Floor((t.elapsedTime - t.interval) / t.interval);
            var phase = state.PhaseAt(tick);

            RuleText = RuleTextFor(state);
            RoundText = state.Round > 0 ? $"라운드 {state.Round}" : "";
            PrepTitle = phase == TriGrabPhase.Prep ? RoundText : "";

            string winner = phase == TriGrabPhase.Over ? state.MatchWinnerId : state.RoundWinnerId;
            ToastText = Toast(phase, winner, NameOf(winner));
            ToastColor = TriGrabPalette.Seat(state.SeatOf(winner));

            rows.Clear();
            string me = playerContext.entityId;
            for (int i = 0; i < state.PlayerIds.Count; i++)
            {
                string id = state.PlayerIds[i];
                state.TryGetPlayer(id, out var score);
                rows.Add(new ScoreRow(NameOf(id), TriGrabPalette.Seat(i), $"{score?.Score ?? 0}/{state.TargetScore}", id == me));
            }

            //  실수 수가 오른 순간이 내 오답이다(서버가 −1을 정한 뒤라 조금 늦지만 되돌릴 일은 없다).
            int mistakes = state.TryGetPlayer(me, out var mine) ? mine.Mistakes : 0;
            if (seenMistakes >= 0 && mistakes > seenMistakes) wrongAt = Time.unscaledTime;
            seenMistakes = mistakes;
            WrongAlpha = FlashAlpha(Time.unscaledTime - wrongAt, WrongSeconds);
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

        /// <summary>정답 띠. 라운드가 끝났으면 "○○ 정답!", 판이 끝났으면 "○○ 우승!"(동점이면 "경기 종료").</summary>
        public static string Toast(TriGrabPhase phase, string winnerId, string winnerName)
        {
            bool has = !string.IsNullOrEmpty(winnerId);
            if (phase == TriGrabPhase.Over) return has ? $"{winnerName} 우승!" : "경기 종료";
            if (phase == TriGrabPhase.Resolved && has) return $"{winnerName} 정답!";
            return "";
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
