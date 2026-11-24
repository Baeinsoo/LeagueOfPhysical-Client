using System.Collections.Generic;
using UnityEngine;
using TG = LOP.TriGrab;

namespace LOP
{
    /// <summary>
    /// 손끝 자리를 틱으로 묻는 식(그림용). 서버 손(<see cref="TG.TriGrabHook"/>)의 쏜 틱·돌아선 틱·돌아선 길이에서 나온다.
    /// 나갈 땐 쏜 틱부터 36 m/s로 곧게, 돌아올 땐 돌아선 틱부터 26 m/s로 몸(쏜 자리 — 손이 나가 있는 동안은 못 움직인다) 쪽으로.
    /// 아직 돌아서지 않은 손(나가는 중)은 사거리 끝에서 돌아선다고 본다.
    /// </summary>
    public static class TriGrabHookPath
    {
        private const float OutStep = TG.TriGrabConfig.HookOutSpeed * TG.TriGrabConfig.TickSeconds;
        private const float BackStep = TG.TriGrabConfig.HookBackSpeed * TG.TriGrabConfig.TickSeconds;
        /// <summary>서버 StepBack이 "도착"으로 보는 거리(몸 반지름 × 0.6).</summary>
        private const float Arrive = TG.TriGrabConfig.BodyRadius * 0.6f;

        public static Vector2 ToUnity(System.Numerics.Vector2 v) => new Vector2(v.X, v.Y);

        /// <summary>돌아선 틱과 그때 길이. 나가는 중이면 사거리 끝에 닿을 틱.</summary>
        public static void Turn(TG.TriGrabHook h, out long turnTick, out float turnLength)
        {
            if (h.State != TG.TriGrabHookState.Out)
            {
                turnTick = h.TurnTick;
                turnLength = h.TurnLength;
                return;
            }
            //  서버는 쏜 틱에도 한 걸음 나간다 — 틱 t의 길이 = 36·dt·(t − 쏜 틱 + 1).
            turnTick = h.FireTick + Mathf.CeilToInt(TG.TriGrabConfig.HookRange / OutStep - 1e-4f) - 1;
            turnLength = TG.TriGrabConfig.HookRange;
        }

        /// <summary>손이 몸에 돌아오는 틱(그 틱 끝에 서버가 손을 지운다).</summary>
        public static long EndTick(TG.TriGrabHook h)
        {
            Turn(h, out long turnTick, out float turnLength);
            //  돌아선 다음 틱부터 매 틱, 움직이기 전 거리가 한 걸음 + 도착 거리 안이면 도착.
            int k = Mathf.Max(1, Mathf.CeilToInt((turnLength - BackStep - Arrive) / BackStep - 1e-4f) + 1);
            return turnTick + k;
        }

        /// <summary>정수 틱의 쏜 자리에서 손끝까지 거리.</summary>
        public static float LengthAt(TG.TriGrabHook h, long tick)
        {
            if (tick < h.FireTick) return 0f;
            Turn(h, out long turnTick, out float turnLength);
            if (tick <= turnTick) return Mathf.Min(OutStep * (tick - h.FireTick + 1), turnLength);
            return Mathf.Max(0f, turnLength - BackStep * (tick - turnTick));
        }

        /// <summary>소수 틱의 손끝 자리 — 이웃한 두 틱 사이를 잇는다.</summary>
        public static Vector2 TipAt(TG.TriGrabHook h, double tick)
        {
            long t0 = (long)System.Math.Floor(tick);
            float frac = (float)(tick - t0);
            float len = Mathf.LerpUnclamped(LengthAt(h, t0), LengthAt(h, t0 + 1), frac);
            return ToUnity(h.Origin) + ToUnity(h.Dir) * len;
        }

        /// <summary>그 틱에 나가 있나(쏜 틱부터 돌아온 틱까지).</summary>
        public static bool IsOut(TG.TriGrabHook h, double tick) => h != null && tick >= h.FireTick && tick <= EndTick(h);

        /// <summary>뻗는 중인가(집게를 벌린다).</summary>
        public static bool Extending(TG.TriGrabHook h, long tick)
        {
            Turn(h, out long turnTick, out _);
            return tick <= turnTick;
        }
    }

    /// <summary>
    /// 내 손. 쏜 순간 클라가 같은 식으로 미리 만든 손(나가는 중)을 들고 있다가, 서버 상태에 내 손(같은 쏜 틱 근처)이
    /// 돌아서서 오면 서버 것으로 바꾼다. 일정 틱 안에 안 오면 서버가 받지 않은 것으로 보고 버린다.
    /// 미리 그린 손은 화면에 보이는 카드(원격 시각 자리)·두더지에 서버와 같은 첫 닿음 규칙(<see cref="TG.TriGrabHook.StepOut"/>)을
    /// 써서 거기서 돌아선다 — 그림만이다. 잡힘·빈손·기절은 서버가 정하고, 다르면 서버 것으로 바꾼다.
    /// 조작판(이동 0·쏘기 막기)과 손 그림이 이 하나를 같이 본다.
    /// </summary>
    public class TriGrabMyHook
    {
        /// <summary>서버 확인을 기다리는 최대 틱. 사거리 끝까지 갔다 오기(약 46틱) + 왕복 여유.</summary>
        public const long ConfirmWindowTicks = 60;
        /// <summary>서버가 정한 쏜 틱이 내가 붙인 입력 틱과 이만큼 어긋나도 같은 발사로 본다(입력 유실·지터).</summary>
        public const long FireTickSlack = 3;
        /// <summary>한 번에 미리 따라가는 최대 틱(사거리 끝까지 약 19틱).</summary>
        private const int MaxSweepTicks = 64;

        private readonly TriGrabClientState state;
        private readonly TriGrabCardTrack track;
        private readonly System.Func<double> remoteTick;
        private TG.TriGrabHook predicted;
        private int roundAtFire;
        private long sweptTick;
        private long confirmedEndTick = long.MinValue;

        private long stunUntil;
        private long stunDeliverTick;
        private int stunRound;

        private readonly List<TG.TriGrabCard> seenCards = new List<TG.TriGrabCard>();
        private readonly List<TG.TriGrabMole> seenMoles = new List<TG.TriGrabMole>();
        private readonly List<TG.TriGrabCard> cardCopies = new List<TG.TriGrabCard>();
        private readonly List<TG.TriGrabMole> moleCopies = new List<TG.TriGrabMole>();

        public TriGrabMyHook(TriGrabClientState state) : this(state, null, (System.Func<double>)null)
        {
        }

        [VContainer.Inject]
        public TriGrabMyHook(TriGrabClientState state, TriGrabCardTrack track, EntityRenderClock renderClock)
            : this(state, track, () => TriGrabRenderTicks.Remote(renderClock))
        {
        }

        public TriGrabMyHook(TriGrabClientState state, TriGrabCardTrack track, System.Func<double> remoteTick)
        {
            this.state = state;
            this.track = track;
            this.remoteTick = remoteTick;
        }

        /// <summary>아직 서버가 확인하지 않은 미리 그린 손.</summary>
        public TG.TriGrabHook Predicted => predicted;

        /// <summary>
        /// 마지막으로 본 내 손이 돌아오는 틱. 서버 쉼(돌아온 뒤 8틱)을 소식 전에 지키는 데 쓴다.
        /// 미리 그린 손은 기다리는 동안만 센다 — 서버가 받지 않아 버린 손이 쉼을 늘리지 않게.
        /// </summary>
        public long LastEndTick => predicted != null
            ? System.Math.Max(confirmedEndTick, TriGrabHookPath.EndTick(predicted))
            : confirmedEndTick;

        /// <summary>쏜 순간. 나가는 손으로 미리 그리고, 그리는 동안 화면 속 카드·두더지에 닿으면 돌아선다.</summary>
        public TG.TriGrabHook Fire(string ownerId, Vector2 origin, float yawDegrees, long fireTick)
        {
            roundAtFire = state.Received ? state.Round : -1;
            predicted = TG.TriGrabHook.Fire(ownerId, new System.Numerics.Vector2(origin.x, origin.y), yawDegrees, fireTick);
            sweptTick = fireTick - 1;
            return predicted;
        }

        /// <summary>
        /// 지금 그릴 내 손. 서버가 돌아선 손을 보냈으면 서버 것, 기다리는 중이면 미리 그린 것, 아니면 서버에 남은 것(없으면 null).
        /// </summary>
        public TG.TriGrabHook Current(string myId, long tick)
        {
            var server = state.HookOf(myId);
            ObserveStun(server);
            if (predicted != null)
            {
                Sweep(tick + 1);
                bool same = server != null && server.FireTick >= predicted.FireTick - FireTickSlack;
                //  서버가 아직 나가는 중이면 끝을 모른다 — 내가 본 첫 닿음으로 그린 손을 계속 쓴다.
                if (same && server.State != TG.TriGrabHookState.Out)
                {
                    predicted = null;
                }
                else if (!same && (RejectedByState() || tick - predicted.FireTick > ConfirmWindowTicks))
                {
                    predicted = null;
                }
                else
                {
                    return predicted;
                }
            }
            if (server != null)
            {
                confirmedEndTick = TriGrabHookPath.EndTick(server);
            }
            return server;
        }

        //  미리 그린 손을 그 틱까지 서버와 같은 걸음으로 따라간다. 닿는 것은 지금 화면에 그려진 것(원격 시각) —
        //  카드는 그 시각 자리, 나타나는 정도·두더지 높이도 그 시각 값이 되게 틱을 옮긴 사본을 쓴다.
        private void Sweep(long throughTick)
        {
            if (predicted.State != TG.TriGrabHookState.Out || track == null || remoteTick == null) return;
            if (throughTick <= sweptTick) return;
            double remote = remoteTick();
            if (double.IsNaN(remote)) return;
            long seen = (long)System.Math.Floor(remote);
            CollectSeen(remote);
            for (int i = 0; i < MaxSweepTicks && sweptTick < throughTick && predicted.State == TG.TriGrabHookState.Out; i++)
            {
                long t = ++sweptTick;
                long shift = t - seen;
                for (int c = 0; c < seenCards.Count; c++)
                {
                    cardCopies[c].Id = seenCards[c].Id;
                    cardCopies[c].Pos = seenCards[c].Pos;
                    cardCopies[c].BornTick = seenCards[c].BornTick + shift;
                    cardCopies[c].CarriedBy = null;
                }
                for (int m = 0; m < seenMoles.Count; m++)
                {
                    var src = seenMoles[m];
                    var dst = moleCopies[m];
                    dst.Id = src.Id;
                    dst.Pos = src.Pos;
                    dst.SpawnTick = src.SpawnTick + shift;
                    dst.StandTicks = src.StandTicks;
                    dst.BonkTick = src.BonkTick >= 0 ? src.BonkTick + shift : -1;
                }
                TG.TriGrabHook.StepOut(predicted, cardCopies, moleCopies, t, TG.TriGrabConfig.TickSeconds);
            }
        }

        private void CollectSeen(double remote)
        {
            seenCards.Clear();
            foreach (var c in state.Cards)
            {
                //  끌려가는 카드는 서버도 판 위로 보지 않는다.
                if (c.Gone || c.CarriedBy != null || remote < c.BornTick) continue;
                if (!track.TryGet(c.Id, remote, out var p)) continue;
                seenCards.Add(new TG.TriGrabCard { Id = c.Id, BornTick = c.BornTick, Pos = new System.Numerics.Vector2(p.x, p.y) });
            }
            seenMoles.Clear();
            seenMoles.AddRange(state.Moles);
            while (cardCopies.Count < seenCards.Count) cardCopies.Add(new TG.TriGrabCard());
            if (cardCopies.Count > seenCards.Count) cardCopies.RemoveRange(seenCards.Count, cardCopies.Count - seenCards.Count);
            while (moleCopies.Count < seenMoles.Count) moleCopies.Add(new TG.TriGrabMole());
            if (moleCopies.Count > seenMoles.Count) moleCopies.RemoveRange(seenMoles.Count, moleCopies.Count - seenMoles.Count);
        }

        //  서버 규칙: 오답 카드를 끈 손이 몸에 닿는 틱 E에 기절이 E + StunTicks까지. 손 길과 카드 정답 여부는 이미 안다.
        private void ObserveStun(TG.TriGrabHook server)
        {
            if (server == null || server.State != TG.TriGrabHookState.Back || server.CardId < 0) return;
            if (!state.TryGetCard(server.CardId, out var card) || card.Ok) return;
            long end = TriGrabHookPath.EndTick(server);
            if (end + TG.TriGrabConfig.StunTicks <= stunUntil) return;
            stunDeliverTick = end;
            stunUntil = end + TG.TriGrabConfig.StunTicks;
            stunRound = state.Round;
        }

        /// <summary>
        /// 서버 소식 전에 아는 내 기절 끝 틱(없으면 0). 라운드가 손이 닿기 전에 끝났으면 서버는 손을 점수 없이 지우므로 기절도 없다.
        /// </summary>
        public long StunUntil(string myId)
        {
            ObserveStun(state.HookOf(myId));
            if (stunUntil == 0 || state.Round != stunRound) return 0;
            long liveEnd;
            if (state.Phase == TG.TriGrabPhase.Live) liveEnd = state.PhaseEndTick;
            else if (state.Phase == TG.TriGrabPhase.Resolved) liveEnd = state.PhaseEndTick - TG.TriGrabConfig.ResolvedTicks;
            else return 0;
            return stunDeliverTick <= liveEnd ? stunUntil : 0;
        }

        //  서버가 그 쏘기를 받지 않았다고 확정할 수 있는 경우만 버린다: 새 라운드로 갔다 / Live가 끝났다.
        private bool RejectedByState()
        {
            if (!state.Received) return false;
            if (roundAtFire >= 0 && state.Round != roundAtFire) return true;
            return state.Phase == TG.TriGrabPhase.Resolved || state.Phase == TG.TriGrabPhase.Over;
        }

        /// <summary>내 손이 나가 있나(쏜 틱부터 돌아온 틱까지). 서버 확인을 기다리는 동안도 나가 있는 것으로 본다.</summary>
        public bool IsOut(string myId, long tick) => TriGrabHookPath.IsOut(Current(myId, tick), tick);

        public void Clear() => predicted = null;
    }

    /// <summary>끄는 동안의 조준선. 조작판(UI)이 쓰고 손 그림(월드)이 읽는다.</summary>
    public class TriGrabAim
    {
        public bool Active;
        public Vector2 Origin, Dir;

        public void Clear() => Active = false;
    }
}
