using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 내 갈고리. 쏜 순간 클라가 같은 식으로 미리 만든 갈고리를 들고 있다가, 서버 상태에 내 갈고리(같은 쏜 틱 근처)가
    /// 오면 서버 것으로 바꾼다. 일정 틱 안에 안 오면 서버가 거절한 것으로 보고 버린다(스펙 §6).
    /// 조작판(이동 0·쏘기 막기)과 갈고리 그림이 이 하나를 같이 본다.
    /// </summary>
    public class TriGrabMyHook
    {
        /// <summary>서버 확인을 기다리는 최대 틱. 한 바퀴(뻗고 돌아오기 약 32틱) + 왕복 여유.</summary>
        public const long ConfirmWindowTicks = 60;
        /// <summary>서버가 정한 쏜 틱이 내가 붙인 입력 틱과 이만큼 어긋나도 같은 발사로 본다(입력 유실·지터).</summary>
        public const long FireTickSlack = 3;

        private readonly TriGrabClientState state;
        private TriGrabHook predicted;

        public TriGrabMyHook(TriGrabClientState state)
        {
            this.state = state;
        }

        /// <summary>아직 서버가 확인하지 않은 미리 그린 갈고리.</summary>
        public TriGrabHook Predicted => predicted;

        /// <summary>쏜 순간. 서버와 같은 식으로 빗나감을 가정한 갈고리를 만든다.</summary>
        public TriGrabHook Fire(string ownerId, Vector2 origin, float yawDegrees, long fireTick)
        {
            long turn = fireTick + TriGrabConfig.ReachTicks;
            predicted = new TriGrabHook
            {
                OwnerId = ownerId,
                FireTick = fireTick,
                Origin = origin,
                Dir = TriGrabMath.DirFromYaw(yawDegrees),
                TurnTick = turn,
                TurnLength = TriGrabConfig.HookLength,
                Result = TriGrabHookResult.None,
                EndTick = TriGrabHook.ComputeEndTick(turn, TriGrabConfig.HookLength, false),
            };
            return predicted;
        }

        /// <summary>
        /// 지금 그릴 내 갈고리. 서버가 확인했으면 서버 것, 기다리는 중이면 미리 그린 것, 아니면 서버에 남은 것(없으면 null).
        /// 확인되거나 기다림이 끝난 미리 그린 갈고리는 여기서 버린다.
        /// </summary>
        public TriGrabHook Current(string myId, long tick)
        {
            var server = state.HookOf(myId);
            if (predicted != null)
            {
                if (server != null && server.FireTick >= predicted.FireTick - FireTickSlack)
                {
                    predicted = null;
                    return server;
                }
                if (tick - predicted.FireTick > ConfirmWindowTicks)
                {
                    predicted = null;
                }
                else
                {
                    return predicted;
                }
            }
            return server;
        }

        /// <summary>내 갈고리가 나가 있나(쏜 틱부터 돌아온 틱까지). 서버 확인을 기다리는 동안도 나가 있는 것으로 본다.</summary>
        public bool IsOut(string myId, long tick)
        {
            var hook = Current(myId, tick);
            if (hook == null) return false;
            if (ReferenceEquals(hook, predicted)) return true;
            return tick >= hook.FireTick && tick <= hook.EndTick;
        }

        public void Clear() => predicted = null;
    }

    /// <summary>끄는 동안의 조준선. 조작판(UI)이 쓰고 갈고리 그림(월드)이 읽는다.</summary>
    public class TriGrabAim
    {
        public bool Active;
        public Vector2 Origin, Dir;

        public void Clear() => Active = false;
    }
}
