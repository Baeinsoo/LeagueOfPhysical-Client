using System.Collections.Generic;
using TG = LOP.TriGrab;

namespace LOP
{
    /// <summary>서버가 알린 한 번짜리 연출(정답·오답·두더지·빗나감).</summary>
    public readonly struct TriGrabFxEvent
    {
        public readonly TG.TriGrabFxKind Kind;
        public readonly UnityEngine.Vector2 Pos;
        public readonly string Owner;
        public readonly int Line;
        public readonly long Tick;

        public TriGrabFxEvent(TG.TriGrabFxKind kind, UnityEngine.Vector2 pos, string owner, int line, long tick)
        {
            Kind = kind; Pos = pos; Owner = owner; Line = line; Tick = tick;
        }
    }

    /// <summary>
    /// 서버가 보낸 원형 판 상태(클라). 와이어를 Shared 타입(카드 정체·손·두더지·점수)으로 바꿔 둔다.
    /// 카드 자리는 여기 없다 — <see cref="TriGrabCardTrack"/>이 따로 받는다. 클라는 판정하지 않고 읽기만 한다.
    /// </summary>
    public class TriGrabClientState
    {
        /// <summary>이만큼 오래된 연출 키는 잊는다(같은 연출이 이렇게 늦게 다시 오는 일은 없다).</summary>
        public const long FxMemoryTicks = 500;

        private Dictionary<int, TG.TriGrabCard> cards = new Dictionary<int, TG.TriGrabCard>();
        private Dictionary<int, TG.TriGrabCard> previousCards = new Dictionary<int, TG.TriGrabCard>();
        private readonly List<TG.TriGrabHook> previousHooks = new List<TG.TriGrabHook>();
        private readonly Dictionary<int, long> goneTicks = new Dictionary<int, long>();
        private readonly Dictionary<int, TG.TriGrabHook> lastCarriers = new Dictionary<int, TG.TriGrabHook>();
        private readonly List<int> staleIds = new List<int>();
        private readonly List<(TG.TriGrabHook hook, long untilTick)> leftHooks = new List<(TG.TriGrabHook, long)>();
        private readonly List<TG.TriGrabCard> cardList = new List<TG.TriGrabCard>();
        private readonly List<TG.TriGrabHook> hooks = new List<TG.TriGrabHook>();
        private readonly List<TG.TriGrabMole> moles = new List<TG.TriGrabMole>();
        private readonly List<string> playerIds = new List<string>();
        private readonly Dictionary<string, TG.TriGrabPlayerScore> players = new Dictionary<string, TG.TriGrabPlayerScore>();
        private readonly Dictionary<string, int> places = new Dictionary<string, int>();
        private readonly Queue<TriGrabFxEvent> fx = new Queue<TriGrabFxEvent>();
        private readonly Dictionary<(long tick, int kind, string owner, float x, float z, int line), long> seenFx =
            new Dictionary<(long, int, string, float, float, int), long>();
        private readonly List<(long, int, string, float, float, int)> forget = new List<(long, int, string, float, float, int)>();

        public int Version { get; private set; }
        public bool Received => Version > 0;
        public TG.TriGrabPhase Phase { get; private set; }
        public long PhaseEndTick { get; private set; }
        public int Round { get; private set; }
        public int TotalRounds { get; private set; } = TG.TriGrabConfig.TotalRounds;
        public TG.TriGrabRule Rule { get; private set; }
        public long LiveStartTick { get; private set; }
        public int Quota { get; private set; }
        public int OkSpawned { get; private set; }

        /// <summary>이번 라운드 카드(사라진 것 포함), 서버 순서.</summary>
        public IReadOnlyList<TG.TriGrabCard> Cards => cardList;
        public IReadOnlyList<TG.TriGrabHook> Hooks => hooks;
        public IReadOnlyList<TG.TriGrabMole> Moles => moles;
        /// <summary>서버 등록 순서. 좌석 색이 이 순서를 따른다.</summary>
        public IReadOnlyList<string> PlayerIds => playerIds;

        public bool Apply(TriGrabRingStateToC message)
        {
            if (message.Version <= Version)
            {
                return false;
            }
            Version = message.Version;
            var previousPhase = Phase;
            int previousRound = Round;
            Phase = (TG.TriGrabPhase)message.Phase;
            PhaseEndTick = message.PhaseEndTick;
            Round = message.Round;
            TotalRounds = message.TotalRounds > 0 ? message.TotalRounds : TG.TriGrabConfig.TotalRounds;
            Rule = new TG.TriGrabRule((TG.TriGrabRuleType)message.RuleType, message.RuleValue, message.RuleLure);
            LiveStartTick = message.LiveStartTick;
            Quota = message.Quota;
            OkSpawned = message.OkSpawned;

            (previousCards, cards) = (cards, previousCards);
            cards.Clear();
            cardList.Clear();
            previousHooks.Clear();
            previousHooks.AddRange(hooks);
            foreach (var w in message.Cards)
            {
                var c = new TG.TriGrabCard
                {
                    Id = w.Id, Word = w.Word, Ink = w.Ink, Fill = w.Fill, Ok = w.Ok, BornTick = w.BornTick,
                    CarriedBy = string.IsNullOrEmpty(w.CarriedBy) ? null : w.CarriedBy, Gone = w.Gone,
                };
                cards[c.Id] = c;
                cardList.Add(c);
            }

            hooks.Clear();
            foreach (var w in message.Hooks)
            {
                hooks.Add(new TG.TriGrabHook
                {
                    Owner = w.OwnerId,
                    FireTick = w.FireTick,
                    Origin = new System.Numerics.Vector2(w.OriginX, w.OriginZ),
                    Dir = new System.Numerics.Vector2(w.DirX, w.DirZ),
                    State = (TG.TriGrabHookState)w.State,
                    End = (TG.TriGrabHookEnd)w.End,
                    CardId = w.CardId,
                    TurnTick = w.TurnTick,
                    TurnLength = w.TurnLength,
                    Tip = new System.Numerics.Vector2(w.TipX, w.TipZ),
                });
            }

            moles.Clear();
            foreach (var w in message.Moles)
            {
                moles.Add(new TG.TriGrabMole
                {
                    Id = w.Id,
                    Pos = new System.Numerics.Vector2(w.X, w.Z),
                    SpawnTick = w.SpawnTick,
                    StandTicks = w.StandTicks,
                    BonkTick = w.BonkTick,
                    SayIndex = w.Say,
                    BonkLineIndex = w.BonkLine,
                });
            }

            playerIds.Clear();
            players.Clear();
            places.Clear();
            foreach (var p in message.Players)
            {
                playerIds.Add(p.EntityId);
                players[p.EntityId] = new TG.TriGrabPlayerScore
                {
                    Score = p.Score, Correct = p.Correct, Wrong = p.Wrong,
                    StunUntilTick = p.StunUntilTick, CooldownUntilTick = p.CooldownUntilTick,
                };
                places[p.EntityId] = p.Place;
            }

            long newest = long.MinValue;
            foreach (var f in message.Fx)
            {
                if (f.Tick > newest) newest = f.Tick;
                //  같은 연출(틱·종류·사람·자리·대사)은 한 번만 — 상태가 다시 와도 두 번 띄우지 않는다.
                var key = (f.Tick, f.Kind, f.OwnerId ?? "", f.X, f.Z, f.Line);
                if (seenFx.ContainsKey(key)) continue;
                seenFx[key] = f.Tick;
                fx.Enqueue(new TriGrabFxEvent((TG.TriGrabFxKind)f.Kind, new UnityEngine.Vector2(f.X, f.Z), f.OwnerId ?? "", f.Line, f.Tick));
            }
            if (newest != long.MinValue) ForgetFxBefore(newest - FxMemoryTicks);
            RecordGoneTicks(message, previousPhase);
            RecordLeftHooks(previousPhase, previousRound);
            return true;
        }

        /// <summary>
        /// 서버에서 지워진 손. 손은 신뢰 소식으로 바로 지워지지만 남의 손은 원격 시각(조금 과거)으로 그리므로,
        /// 돌아오는 틱까지 계속 그려야 끌려오던 카드가 사슬 없이 날지 않는다. 돌아오는 틱은 카드 사라진 틱과 같은 추정을 쓴다.
        /// </summary>
        private void RecordLeftHooks(TG.TriGrabPhase previousPhase, int previousRound)
        {
            if (Round != previousRound)
            {
                leftHooks.Clear();
            }
            //  같은 사람이 새로 쏜 손이 상태에 있으면 옛 손은 이미 돌아왔다.
            leftHooks.RemoveAll(l => HookOf(l.hook.Owner) != null);
            foreach (var h in previousHooks)
            {
                bool still = false;
                foreach (var now in hooks)
                {
                    if (now.Owner == h.Owner && now.FireTick == h.FireTick) still = true;
                }
                if (still) continue;
                long until;
                if (h.CardId >= 0 && goneTicks.TryGetValue(h.CardId, out var gone) && gone != long.MinValue) until = gone;
                else if (Phase == TG.TriGrabPhase.Resolved && previousPhase == TG.TriGrabPhase.Live) until = PhaseEndTick - TG.TriGrabConfig.ResolvedTicks;
                else until = TriGrabHookPath.EndTick(h) + 1;
                leftHooks.RemoveAll(l => l.hook.Owner == h.Owner);
                leftHooks.Add((h, until));
            }
        }

        /// <summary>서버에서 지워졌지만 그 틱 전까지는 그려야 할 손과 그 틱(이 틱부터 안 그린다).</summary>
        public IReadOnlyList<(TG.TriGrabHook hook, long untilTick)> LeftHooks => leftHooks;

        /// <summary>
        /// 카드가 사라진 틱 추정. 사라짐은 신뢰 소식으로 바로 오지만 카드는 원격 시각(조금 과거)으로 그리므로,
        /// 그 틱까지는 계속 그려야 손끝에서 갑자기 꺼지지 않는다. 끌던 사람의 정답·오답 연출 틱 → 라운드 끝 틱 → 끌던 손이 돌아오는 틱 순.
        /// </summary>
        private void RecordGoneTicks(TriGrabRingStateToC message, TG.TriGrabPhase previousPhase)
        {
            foreach (var c in cardList)
            {
                if (!c.Gone || goneTicks.ContainsKey(c.Id)) continue;
                long gone = long.MinValue;
                TG.TriGrabHook carrier = null;
                if (previousCards.TryGetValue(c.Id, out var before) && !before.Gone && before.CarriedBy != null)
                {
                    foreach (var h in previousHooks)
                    {
                        if (h.Owner == before.CarriedBy && h.CardId == c.Id) carrier = h;
                    }
                    foreach (var f in message.Fx)
                    {
                        var kind = (TG.TriGrabFxKind)f.Kind;
                        if ((kind == TG.TriGrabFxKind.Correct || kind == TG.TriGrabFxKind.Wrong) && f.OwnerId == before.CarriedBy)
                        {
                            gone = System.Math.Max(gone, f.Tick);
                        }
                    }
                    if (gone == long.MinValue && Phase == TG.TriGrabPhase.Resolved && previousPhase == TG.TriGrabPhase.Live)
                    {
                        gone = PhaseEndTick - TG.TriGrabConfig.ResolvedTicks;
                    }
                    if (gone == long.MinValue && carrier != null)
                    {
                        gone = TriGrabHookPath.EndTick(carrier);
                    }
                }
                goneTicks[c.Id] = gone;
                if (carrier != null) lastCarriers[c.Id] = carrier;
            }

            //  지난 라운드 카드는 잊는다(카드 번호는 판 안에서 겹치지 않는다).
            staleIds.Clear();
            foreach (var id in goneTicks.Keys)
            {
                if (!cards.ContainsKey(id)) staleIds.Add(id);
            }
            foreach (var id in staleIds)
            {
                goneTicks.Remove(id);
                lastCarriers.Remove(id);
            }
        }

        /// <summary>카드가 사라진 틱. 그 틱부터 안 그린다. 안 사라졌으면 long.MaxValue, 언제인지 모르면 long.MinValue.</summary>
        public long GoneTickOf(int id)
        {
            if (!cards.TryGetValue(id, out var c) || !c.Gone) return long.MaxValue;
            return goneTicks.TryGetValue(id, out var t) ? t : long.MinValue;
        }

        /// <summary>사라진 카드를 마지막에 끌던 손(사라진 틱까지 그 손끝에 그린다). 없으면 null.</summary>
        public TG.TriGrabHook LastCarrierOf(int id) => lastCarriers.TryGetValue(id, out var h) ? h : null;

        private void ForgetFxBefore(long tick)
        {
            forget.Clear();
            foreach (var pair in seenFx)
            {
                if (pair.Value < tick) forget.Add(pair.Key);
            }
            foreach (var k in forget) seenFx.Remove(k);
        }

        /// <summary>아직 안 띄운 연출을 하나 꺼낸다.</summary>
        public bool TryDequeueFx(out TriGrabFxEvent e)
        {
            if (fx.Count > 0)
            {
                e = fx.Dequeue();
                return true;
            }
            e = default;
            return false;
        }

        public int PendingFxCount => fx.Count;

        public bool TryGetCard(int id, out TG.TriGrabCard card) => cards.TryGetValue(id, out card);

        public bool TryGetPlayer(string entityId, out TG.TriGrabPlayerScore score)
        {
            score = null;
            return entityId != null && players.TryGetValue(entityId, out score);
        }

        /// <summary>지금 누적 점수 순위(같은 점수는 공동). 모르면 0.</summary>
        public int PlaceOf(string entityId) => entityId != null && places.TryGetValue(entityId, out var p) ? p : 0;

        /// <summary>좌석 번호(등록 순서). 모르는 사람이면 -1.</summary>
        public int SeatOf(string entityId) => entityId == null ? -1 : playerIds.IndexOf(entityId);

        public TG.TriGrabHook HookOf(string entityId)
        {
            if (entityId == null) return null;
            foreach (var h in hooks)
            {
                if (h.Owner == entityId) return h;
            }
            return null;
        }

        /// <summary>남은 정답 카드 수 = 아직 안 낸 할당량 + 판 위(끌려가는 것 포함)의 정답 카드.</summary>
        public int RemainingOk
        {
            get
            {
                int onDisc = 0;
                foreach (var c in cardList)
                {
                    if (c.Ok && !c.Gone) onDisc++;
                }
                return System.Math.Max(0, Quota - OkSpawned + onDisc);
            }
        }

        /// <summary>
        /// 그 틱의 단계. 준비 끝 틱이 지났으면 소식 전에도 Live로, Live의 시간 상한 틱이 지났으면 Resolved로 본다 —
        /// 둘 다 미리 알려진 틱이다. 할당량을 다 잡아 일찍 끝나는 것은 서버 소식으로만 안다.
        /// </summary>
        public TG.TriGrabPhase PhaseAt(long tick)
        {
            var phase = Phase;
            if (phase == TG.TriGrabPhase.Prep && tick >= PhaseEndTick) return TG.TriGrabPhase.Live;
            if (phase == TG.TriGrabPhase.Live && tick >= PhaseEndTick) return TG.TriGrabPhase.Resolved;
            return phase;
        }
    }
}
