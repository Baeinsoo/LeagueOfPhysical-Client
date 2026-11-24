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
    /// 오면 서버 것으로 바꾼다. 일정 틱 안에 안 오면 서버가 받지 않은 것으로 보고 버린다.
    /// 조작판(이동 0·쏘기 막기)과 손 그림이 이 하나를 같이 본다.
    /// </summary>
    public class TriGrabMyHook
    {
        /// <summary>서버 확인을 기다리는 최대 틱. 사거리 끝까지 갔다 오기(약 46틱) + 왕복 여유.</summary>
        public const long ConfirmWindowTicks = 60;
        /// <summary>서버가 정한 쏜 틱이 내가 붙인 입력 틱과 이만큼 어긋나도 같은 발사로 본다(입력 유실·지터).</summary>
        public const long FireTickSlack = 3;

        private readonly TriGrabClientState state;
        private TG.TriGrabHook predicted;
        private int roundAtFire;

        public TriGrabMyHook(TriGrabClientState state)
        {
            this.state = state;
        }

        /// <summary>아직 서버가 확인하지 않은 미리 그린 손.</summary>
        public TG.TriGrabHook Predicted => predicted;

        /// <summary>마지막으로 본 내 손이 돌아오는 틱. 서버 쉼(돌아온 뒤 8틱)을 소식 전에 지키는 데 쓴다.</summary>
        public long LastEndTick { get; private set; } = long.MinValue;

        /// <summary>쏜 순간. 사거리 끝까지 가는 손으로 미리 그린다(잡힘·막힘은 서버 손이 오면 안다).</summary>
        public TG.TriGrabHook Fire(string ownerId, Vector2 origin, float yawDegrees, long fireTick)
        {
            roundAtFire = state.Received ? state.Round : -1;
            predicted = TG.TriGrabHook.Fire(ownerId, new System.Numerics.Vector2(origin.x, origin.y), yawDegrees, fireTick);
            LastEndTick = TriGrabHookPath.EndTick(predicted);
            return predicted;
        }

        /// <summary>
        /// 지금 그릴 내 손. 서버가 확인했으면 서버 것, 기다리는 중이면 미리 그린 것, 아니면 서버에 남은 것(없으면 null).
        /// </summary>
        public TG.TriGrabHook Current(string myId, long tick)
        {
            var server = state.HookOf(myId);
            if (predicted != null)
            {
                if (server != null && server.FireTick >= predicted.FireTick - FireTickSlack)
                {
                    predicted = null;
                }
                else if (RejectedByState() || tick - predicted.FireTick > ConfirmWindowTicks)
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
                LastEndTick = TriGrabHookPath.EndTick(server);
            }
            return server;
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
