using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 서버가 보낸 삼색 그랩 상태(클라). 와이어를 Shared 타입(카드·시계·갈고리)으로 바꿔 두어,
    /// 그림이 서버 판정과 같은 식에 렌더 틱을 넣어 그리게 한다. 점수·잡음은 여기서만 읽는다(클라는 판정하지 않는다).
    /// </summary>
    public class TriGrabClientState
    {
        private readonly TriGrabCard?[] cards = new TriGrabCard?[TriGrabConfig.Slots];
        private readonly long[] emptyUntil = new long[TriGrabConfig.Slots];
        private readonly List<TriGrabHook> hooks = new List<TriGrabHook>();
        private readonly List<string> playerIds = new List<string>();
        private readonly Dictionary<string, TriGrabPlayerScore> players = new Dictionary<string, TriGrabPlayerScore>();

        public int Version { get; private set; }
        public bool Received => Version > 0;
        public TriGrabPhase Phase { get; private set; }
        public long PhaseEndTick { get; private set; }
        public int Round { get; private set; }
        public TriGrabRule Rule { get; private set; }
        public TriGrabRoundClock Clock { get; private set; }
        public string RoundWinnerId { get; private set; } = "";
        public string MatchWinnerId { get; private set; } = "";
        public int TargetScore { get; private set; } = TriGrabConfig.TargetScore;

        public IReadOnlyList<TriGrabHook> Hooks => hooks;
        /// <summary>서버 등록 순서. 좌석 색이 이 순서를 따른다.</summary>
        public IReadOnlyList<string> PlayerIds => playerIds;

        public bool Apply(TriGrabStateToC message)
        {
            if (message.Version <= Version)
            {
                return false;
            }
            Version = message.Version;
            Phase = (TriGrabPhase)message.Phase;
            PhaseEndTick = message.PhaseEndTick;
            Round = message.Round;
            Rule = new TriGrabRule((TriGrabRuleType)message.RuleType, message.RuleColor);
            Clock = new TriGrabRoundClock
            {
                BaseAngle0 = message.BaseAngle0,
                Omega = message.Omega,
                LiveStartTick = message.LiveStartTick,
                FreezeTick = message.FreezeTick,
            };
            RoundWinnerId = message.RoundWinnerId ?? "";
            MatchWinnerId = message.MatchWinnerId ?? "";
            TargetScore = message.TargetScore > 0 ? message.TargetScore : TriGrabConfig.TargetScore;

            for (int i = 0; i < cards.Length; i++)
            {
                var w = i < message.Slots.Count ? message.Slots[i] : null;
                cards[i] = w?.Card != null ? ToCard(w.Card) : (TriGrabCard?)null;
                emptyUntil[i] = w?.EmptyUntilTick ?? 0;
            }

            hooks.Clear();
            foreach (var w in message.Hooks)
            {
                hooks.Add(new TriGrabHook
                {
                    OwnerId = w.OwnerId,
                    FireTick = w.FireTick,
                    Origin = new Vector2(w.OriginX, w.OriginZ),
                    Dir = new Vector2(w.DirX, w.DirZ),
                    TurnTick = w.TurnTick,
                    TurnLength = w.TurnLength,
                    Carried = w.Carried != null ? ToCard(w.Carried) : (TriGrabCard?)null,
                    Result = (TriGrabHookResult)w.Result,
                    EndTick = w.EndTick,
                });
            }

            playerIds.Clear();
            players.Clear();
            foreach (var p in message.Players)
            {
                playerIds.Add(p.EntityId);
                players[p.EntityId] = new TriGrabPlayerScore
                {
                    Score = p.Score,
                    Mistakes = p.Mistakes,
                    ReloadUntilTick = p.ReloadUntilTick,
                    BestRtMs = p.BestRtMs,
                };
            }
            return true;
        }

        public static TriGrabCard ToCard(TriGrabCardWire w) => new TriGrabCard
        {
            Id = w.Id, Word = w.Word, Ink = w.Ink, Cycles = w.Cycles, PeriodTicks = w.PeriodTicks, T0Tick = w.T0Tick,
        };

        public TriGrabCard? CardAt(int slot) => slot >= 0 && slot < cards.Length ? cards[slot] : null;

        public long EmptyUntilTick(int slot) => slot >= 0 && slot < emptyUntil.Length ? emptyUntil[slot] : 0;

        public bool TryGetPlayer(string entityId, out TriGrabPlayerScore score)
        {
            score = null;
            return entityId != null && players.TryGetValue(entityId, out score);
        }

        /// <summary>좌석 번호(등록 순서). 모르는 사람이면 -1.</summary>
        public int SeatOf(string entityId) => entityId == null ? -1 : playerIds.IndexOf(entityId);

        public TriGrabHook HookOf(string entityId)
        {
            if (entityId == null) return null;
            foreach (var h in hooks)
            {
                if (h.OwnerId == entityId) return h;
            }
            return null;
        }

        /// <summary>
        /// 그 틱의 단계. 준비 끝 틱이 지났으면 서버 소식이 오기 전에도 Live로 본다 — 준비 끝은 미리 알려진 틱이다.
        /// 라운드 끝 쉼이 지난 뒤는 다음 라운드인지 판 끝인지 모르니 Resolved로 둔다.
        /// </summary>
        public TriGrabPhase PhaseAt(long tick)
        {
            if (Phase == TriGrabPhase.Prep && tick >= PhaseEndTick) return TriGrabPhase.Live;
            return Phase;
        }

        /// <summary>소수 틱의 회전판 각. 이웃한 두 정수 틱 사이를 이어 화면에서 끊기지 않게 한다.</summary>
        public static float BaseAt(in TriGrabRoundClock clock, double tick)
        {
            long t0 = (long)System.Math.Floor(tick);
            float frac = (float)(tick - t0);
            return Mathf.LerpUnclamped(clock.BaseAt(t0), clock.BaseAt(t0 + 1), frac);
        }

        /// <summary>소수 틱의 갈고리 끝 자리(같은 방식으로 이어 준다).</summary>
        public static Vector2 TipAt(TriGrabHook hook, double tick)
        {
            long t0 = (long)System.Math.Floor(tick);
            float frac = (float)(tick - t0);
            return Vector2.LerpUnclamped(hook.TipAt(t0), hook.TipAt(t0 + 1), frac);
        }
    }
}
