using System;

namespace LOP.MapTools
{
    /// <summary>굴려 보기가 세계에 묻는 것 전부. 이 다섯 가지만 있으면 "이번 틱에 다르게 하면
    /// 어떻게 되나"를 물을 수 있다 — 물리도 씬도 여기서는 모른다.
    ///
    /// <para><b>왜 인터페이스인가.</b> 굴려 보기는 <b>실제 비행과 똑같은 기반 정책·똑같은 이동
    /// 커널</b>로 돌아야 한다. 시뮬레이터를 하나 더 만들어 두 개가 조금이라도 갈라지면, 이 도구가
    /// 내는 숫자 전체가 조용히 무효가 된다. 그래서 굴려 보기는 자기 물리를 갖지 않고 <b>부르는
    /// 쪽이 쓰는 그 함수들</b>을 그대로 받아 쓴다. (테스트는 일부러 가짜 커널을 넣어 굴려 보기
    /// 자체의 판단만 따로 본다.)</para></summary>
    /// <typeparam name="TState">한 틱의 상태. 굴려 보기는 내용을 모른다 — 복사해서 굴릴 뿐이다.</typeparam>
    public interface IRolloutWorld<TState>
    {
        /// <summary>기반 정책이 이 상태에서 무엇을 하려 하는가.</summary>
        BotDecision Decide(in TState state);
        /// <summary>한 틱 굴린다(실제 게임 커널).</summary>
        TState Advance(in TState state, bool flap);
        /// <summary>무언가에 닿았는가 — 굴려 보기가 말하는 "죽음"이다.</summary>
        bool Touched(in TState state);
        /// <summary>결승선을 넘었는가. 넘었으면 더 굴릴 것이 없고, 이보다 나은 결과도 없다.</summary>
        bool Finished(in TState state);
        /// <summary>얼마나 멀리 갔는가(앞으로 간 거리).</summary>
        float ForwardX(in TState state);
    }

    /// <summary>굴려 보고 내린 이번 틱의 결론.</summary>
    public readonly struct RolloutChoice
    {
        public readonly bool Flap;
        /// <summary>실제로 굴려서 정했는가. false면 후보가 하나뿐이라(천장 가드가 누르는 쪽을
        /// 막았다) 굴리지 않고 기반 정책 그대로 간 것이다.</summary>
        public readonly bool RolledOut;
        /// <summary>기반 정책과 <b>다른</b> 선택을 했는가. 이 수가 0이면 전방탐색이 아무것도
        /// 안 하고 있다는 뜻이라, 결과가 안 변한 이유를 바로 읽을 수 있다.</summary>
        public readonly bool Deviated;

        public RolloutChoice(bool flap, bool rolledOut, bool deviated)
        {
            Flap = flap;
            RolledOut = rolledOut;
            Deviated = deviated;
        }
    }

    /// <summary>
    /// 이번 틱의 두 갈래를 <b>직접 굴려 보고</b> 더 나은 쪽을 고른다 — 업계에서 말하는
    /// <i>rollout 정책</i>(= 1단계 전방탐색: 첫 수만 바꿔 보고 그 뒤는 기반 정책으로 끝까지 굴려
    /// 견주는 것)이다.
    ///
    /// <para><b>왜 한 틱짜리 규칙 대신 이것인가.</b> 플래피의 날갯짓은 <b>크기가 하나뿐</b>이다
    /// (세로 속도를 임펄스로 덮어쓴다 — 살짝 치는 날갯짓이 없다). 그래서 진짜 문제는 "지금 무엇을
    /// 할까"가 아니라 <b>"어느 높이 통로를 탈까"</b> 인데, 그건 한 틱을 보는 규칙으로는 표현이
    /// 안 된다. 실제로 탐욕 규칙을 세 번 손봤고(도착 높이 → 경로 전체 → 원거리 바닥) 세 번 다
    /// 같은 벽에 부딪혔다. 마지막 것은 역효과였다 — 봇이 <i>가장 가까운 틈</i>을 겨냥해 코스
    /// 초반에 올라가 막다른 높은 통로에 조기 진입했다. 틈 찾기는 "가장 가까운 틈"만 줄 뿐
    /// <b>그게 지나갈 수 있는 길인지 모르기</b> 때문이다. 굴려 보기는 그걸 직접 물어본다.</para>
    ///
    /// <para><b>후보 A가 없을 때는 굴리지 않는다.</b> 천장 가드가 "지금 누르면 올라가다 박는다"고
    /// 하면 누르는 쪽은 애초에 후보가 아니다 — 고를 게 하나뿐이니 굴려 볼 이유도 없다. 이 가지치기가
    /// 비용의 대부분을 없앤다. <b>굴려 보기가 천장 가드를 약화시키지 않는다</b>는 뜻이기도 하다:
    /// 가드가 막은 자리에서는 어떤 경우에도 누르지 않는다.</para>
    /// </summary>
    public static class BotRollout
    {
        /// <summary>이번 틱에 누를지 정한다.</summary>
        /// <param name="baseDecision">이 상태에서 기반 정책이 낸 판단. 부르는 쪽이 이미 갖고
        /// 있으므로 다시 계산하지 않고 받는다 — 같은 상태에 같은 답이라 재계산은 낭비다.</param>
        /// <param name="horizon">첫 수를 바꾼 뒤 기반 정책으로 몇 틱을 굴려 볼 것인가.</param>
        /// <param name="sameReachEpsilon">"사실상 같은 거리"의 폭. 이보다 작은 차이는 부동소수
        /// 잡음이거나 한 틱 어긋난 것이라 우열로 세지 않는다.</param>
        public static RolloutChoice Choose<TState>(IRolloutWorld<TState> world, in TState state,
                                                   in BotDecision baseDecision, int horizon,
                                                   float sameReachEpsilon)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            if (horizon < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(horizon), horizon,
                    "horizon must be at least 1 — otherwise nothing is rolled out and the choice is meaningless.");
            }

            //  후보 A(누른다)가 아예 없다 — 천장 가드가 막았다. 고를 게 없으니 굴리지 않는다.
            if (baseDecision.CeilingSafe == false)
            {
                return new RolloutChoice(flap: false, rolledOut: false, deviated: false);
            }

            Outcome flapped = Roll(world, state, firstFlap: true, horizon);
            Outcome coasted = Roll(world, state, firstFlap: false, horizon);

            //  ── "더 낫다"의 기준 (순서가 곧 우선순위다) ──
            //  ① 더 오래 산다. 굴리는 동안 안 닿는 쪽이 이긴다 — 이 도구가 답하려는 질문이
            //     "지나갈 수 있는가"라서, 살아남는 것이 다른 무엇보다 앞선다.
            //  ② 둘 다 같은 만큼 살면 더 멀리 간 쪽. 결국 재는 것이 도달 거리다.
            //  ③ 사실상 같으면(몸 지름 이내) 기반 정책의 선택을 따른다. 동점에서 흔들리면
            //     매 틱 이유 없이 판단이 바뀌어 궤적이 잡음이 된다.
            bool flap;
            if (flapped.AliveTicks != coasted.AliveTicks)
            {
                flap = flapped.AliveTicks > coasted.AliveTicks;
            }
            else if (Math.Abs(flapped.ReachX - coasted.ReachX) > sameReachEpsilon)
            {
                flap = flapped.ReachX > coasted.ReachX;
            }
            else
            {
                flap = baseDecision.Flap;
            }

            return new RolloutChoice(flap, rolledOut: true, deviated: flap != baseDecision.Flap);
        }

        /// <summary>굴려 본 결과 — 몇 틱을 살았고 어디까지 갔는가.</summary>
        private readonly struct Outcome
        {
            public readonly int AliveTicks;
            public readonly float ReachX;

            public Outcome(int aliveTicks, float reachX)
            {
                AliveTicks = aliveTicks;
                ReachX = reachX;
            }
        }

        //  첫 틱만 지정한 대로 하고, 그 뒤는 <b>기반 정책 그대로</b> 굴린다. "다르게 눌렀으면"을
        //  묻는 것이지 "다른 봇이었으면"을 묻는 것이 아니다 — 그래서 둘째 틱부터는 손대지 않는다.
        private static Outcome Roll<TState>(IRolloutWorld<TState> world, in TState state,
                                            bool firstFlap, int horizon)
        {
            TState s = state;
            int alive = 0;
            for (int t = 0; t < horizon; t++)
            {
                bool flap = t == 0 ? firstFlap : world.Decide(s).Flap;
                s = world.Advance(s, flap);
                if (world.Touched(s))
                {
                    //  닿은 틱은 산 틱으로 세지 않는다. 자리는 닿은 그 자리를 쓴다 — 벽까지는
                    //  실제로 갔기 때문이다(둘 다 닿았을 때 ②가 그 차이로 우열을 가른다).
                    return new Outcome(alive, world.ForwardX(s));
                }
                alive = t + 1;
                if (world.Finished(s))
                {
                    //  끝까지 갔다. 더 굴릴 것이 없으니 "창을 다 살았다"로 세어, 창을 다 산
                    //  다른 후보와 ②(도달 거리)로 겨루게 한다.
                    return new Outcome(horizon, world.ForwardX(s));
                }
            }
            return new Outcome(alive, world.ForwardX(s));
        }
    }
}
