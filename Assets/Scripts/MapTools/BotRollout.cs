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
        /// <summary>세로 속도 — 양수면 올라가는 중, 음수면 떨어지는 중이다.</summary>
        float VerticalSpeed(in TState state);
    }

    /// <summary>한 갈래를 굴려 본 결과 — 몇 틱을 살았고 어디까지 갔는가.
    /// <para>고르는 데는 <see cref="BotRollout.Choose{TState}(IRolloutWorld{TState}, in TState, in BotDecision, int, float)"/>가
    /// 이 값을 안에서 쓰고 버린다. 밖으로 내는 것은 <b>진단용</b>이다 — "왜 그 갈래를 골랐나"를
    /// 물으려면 두 갈래가 각각 몇 점이었는지 보여야 한다.</para></summary>
    public readonly struct RolloutBranch
    {
        public readonly int AliveTicks;
        public readonly float ReachX;
        /// <summary>굴리기가 <b>멈춘 자리</b>의 세로 속도(지평 끝, 또는 도중에 닿았으면 그 자리).
        /// 양수면 올라가는 중이다.</summary>
        public readonly float EndVerticalSpeed;

        public RolloutBranch(int aliveTicks, float reachX, float endVerticalSpeed)
        {
            AliveTicks = aliveTicks;
            ReachX = reachX;
            EndVerticalSpeed = endVerticalSpeed;
        }
    }

    /// <summary>이번 틱에 굴려 본 두 갈래. <see cref="Rolled"/>가 false면 굴리지 않은 것이라
    /// (천장 가드가 누르는 쪽을 후보에서 뺐다) 두 갈래 값은 뜻이 없다.</summary>
    public readonly struct RolloutBranches
    {
        public readonly bool Rolled;
        public readonly RolloutBranch Flapped;
        public readonly RolloutBranch Coasted;

        public RolloutBranches(bool rolled, RolloutBranch flapped, RolloutBranch coasted)
        {
            Rolled = rolled;
            Flapped = flapped;
            Coasted = coasted;
        }
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
        /// <summary>세로 속도가 "사실상 같다"고 볼 폭(m/s). 도달 거리의 폭과 달리 상수인 이유는
        /// 이 값이 몸 크기에서 나오는 것이 아니라 <b>관문에서 실제로 갈린 폭</b>(+17 대 −15,
        /// 약 32m/s)의 눈금에서 고른 것이라서다 — 그 3%가 잡음과 뜻 있는 차이의 경계다.
        /// 적중률 59.8%도 이 폭으로 잰 값이므로, 이 수를 바꾸면 그 근거가 같이 흔들린다.
        /// <para>측정 도구가 같은 신호를 채점할 때도 <b>이 상수</b>를 쓴다 — 두 벌로 두면
        /// 조용히 갈라져 "봇이 쓰는 기준의 적중률"이 봇의 것이 아니게 된다.</para></summary>
        public const float SameSpeedEpsilon = 0.5f;

        /// <summary>이번 틱에 누를지 정한다.</summary>
        /// <param name="baseDecision">이 상태에서 기반 정책이 낸 판단. 부르는 쪽이 이미 갖고
        /// 있으므로 다시 계산하지 않고 받는다 — 같은 상태에 같은 답이라 재계산은 낭비다.</param>
        /// <param name="horizon">첫 수를 바꾼 뒤 기반 정책으로 몇 틱을 굴려 볼 것인가.</param>
        /// <param name="sameReachEpsilon">"사실상 같은 거리"의 폭. 이보다 작은 차이는 부동소수
        /// 잡음이거나 한 틱 어긋난 것이라 우열로 세지 않는다.</param>
        public static RolloutChoice Choose<TState>(IRolloutWorld<TState> world, in TState state,
                                                   in BotDecision baseDecision, int horizon,
                                                   float sameReachEpsilon)
            => Choose(world, state, baseDecision, horizon, sameReachEpsilon, out _);

        /// <summary>위와 <b>똑같이</b> 정하면서, 두 갈래가 각각 몇 점이었는지도 함께 낸다.
        /// 판단에는 아무 영향이 없다 — 안에서 이미 계산해 버리던 값을 밖으로 낼 뿐이라
        /// 굴려 보는 횟수도 그대로다(그래서 이 진단이 공짜다).</summary>
        public static RolloutChoice Choose<TState>(IRolloutWorld<TState> world, in TState state,
                                                   in BotDecision baseDecision, int horizon,
                                                   float sameReachEpsilon, out RolloutBranches branches)
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
                branches = default;
                return new RolloutChoice(flap: false, rolledOut: false, deviated: false);
            }

            RolloutBranch flapped = Roll(world, state, firstFlap: true, horizon);
            RolloutBranch coasted = Roll(world, state, firstFlap: false, horizon);
            branches = new RolloutBranches(rolled: true, flapped, coasted);

            //  ③ 두 갈래가 사실상 같으면(Prefer가 0) 기반 정책의 선택을 따른다. 동점에서
            //     흔들리면 매 틱 이유 없이 판단이 바뀌어 궤적이 잡음이 된다.
            int prefer = Prefer(flapped, coasted, sameReachEpsilon);
            bool flap = prefer != 0 ? prefer > 0 : baseDecision.Flap;

            return new RolloutChoice(flap, rolledOut: true, deviated: flap != baseDecision.Flap);
        }

        /// <summary>두 갈래 중 어느 쪽이 나은가 — <b>지금 쓰는 기준 그 자체</b>다.
        /// +1이면 누르는 쪽, −1이면 안 누르는 쪽, <b>0이면 이 기준으로는 둘을 구별하지 못한다</b>
        /// (그때 <see cref="Choose{TState}(IRolloutWorld{TState}, in TState, in BotDecision, int, float, out RolloutBranches)"/>가
        /// 기반 정책으로 떨어진다).
        ///
        /// <para><b>왜 밖으로 냈나.</b> 이 기준이 좋은 갈래를 실제로 얼마나 자주 맞히는지
        /// 재려면(<see cref="BranchSignals"/>) 채점기가 같은 규칙을 물어봐야 하는데, 채점기가
        /// 규칙을 <i>베껴</i> 갖고 있으면 둘이 조용히 갈라져 "지금 기준의 적중률"이 지금 기준의
        /// 것이 아니게 된다. 그래서 규칙은 이 한 군데에만 둔다.</para>
        ///
        /// <para>순서가 곧 우선순위다:
        /// ① <b>더 오래 산다</b> — 굴리는 동안 안 닿는 쪽이 이긴다. 이 도구가 답하려는 질문이
        ///    "지나갈 수 있는가"라서 살아남는 것이 다른 무엇보다 앞선다.
        /// ② 둘 다 같은 만큼 살면 <b>더 멀리 간 쪽</b>. 결국 재는 것이 도달 거리다.
        ///    (전진 속도가 상수라 실제로는 ①과 같은 값이지만, 전진이 상수가 아닌 세계에서도
        ///    이 순서가 맞으므로 남겨 둔다.)
        /// ③ 그것도 같으면 <b>끝에서 더 올라가는 중인 쪽</b>.</para>
        ///
        /// <para><b>왜 ③을 넣었나 — 재 보고 넣었다.</b> ①②만으로는 굴려 본 521틱 중
        /// <b>463틱(88.9%)이 동점</b>이라, 전방탐색이 열 번 중 아홉 번은 아무 말도 못 하고
        /// 기반 정책에 떠넘기고 있었다. 전진 속도가 상수여서 "더 오래 산다"와 "더 멀리 간다"가
        /// <i>같은 수</i>인 것이 원인이다 — 기준이 둘로 보이지만 사실은 하나였다. 되돌리기가
        /// 정답을 알려 준 264틱에서 <b>지금 기준의 적중률은 0.0%</b>(전부 동점)였고, 같은
        /// 자리에서 지평 끝 세로 속도는 <b>59.8%</b>를 맞혔다. 그래서 동점을 깨는 자리에만
        /// 이 한 신호를 넣는다 — ①②가 말을 할 때는 아무것도 바뀌지 않는다.</para>
        ///
        /// <para>후보로 재 본 다른 신호는 일부러 <b>안</b> 넣었다: "가드가 안 막은 틱수"는
        /// 67.7%가 거꾸로였고(= 넣으면 나빠진다), "세로 여유"는 45.1%로 동전 던지기였다.</para>
        ///
        /// <para><b>③을 "관문에서 재는 값"으로 바꿔 보고 되돌렸다 (2026-09-14).</b> 관문을 가른
        /// 것은 지평 끝 자세가 아니라 <i>관문에 도착하는 순간</i>의 낙하 속도였다(통과 −9.2~−5.0
        /// 대 실패 −17.6, 겹침 없음). 그래서 "재는 지점을 관문으로 옮긴" 후보를 같은 자로 채점했는데
        /// <b>전부 동전 던지기보다 나빴다</b>: 가장 좁았던 자리의 세로 속도 22.7% · 굴리는 동안의
        /// 최저 세로 속도 20.5% · 급강하 틱수 15.9%(셋 다 "거꾸로"가 60~70%). 같은 채점에서
        /// <b>겉으로 이긴 것은 "가드가 안 막은 틱수" 79.5%</b>였지만(지평 끝 세로 속도 45.5%,
        /// ①②③ 전체 61.4%), <b>실제로 넣어 보니 훨씬 나빠졌다</b> — 네 스폰이 x=366.5에서
        /// 199.5~280.0으로 후퇴했고 18줄 훑기 완주가 2 → 1이 됐다. 그래서 되돌렸다.</para>
        ///
        /// <para><b>거기서 배운 것 — 채점 점수는 닫힌 고리에서 그대로 재현되지 않는다.</b> 채점은
        /// <i>지금 규칙으로 난 궤적</i> 위에서 매겨진다. 규칙을 바꾸면 궤적 자체가 달라지므로
        /// 그 점수는 더 이상 같은 자리를 가리키지 않는다. 점수는 후보를 <b>추리는</b> 데 쓰고,
        /// 채택 여부는 반드시 맵 검사(훑기 완주 수)로 정한다.</para>
        ///
        /// <para><b>그리고 신호는 여기서 문제가 아니었다.</b> 같은 측정이 말하기를, 관문 앞
        /// <b>마지막 30틱은 천장 가드가 100% 막는다</b>(네 비행 모두 30/30. 180틱에서도 93%,
        /// 비행 전체로도 70%). 막힌 틱에는 후보가 하나뿐이라 <i>어떤 신호를 넣어도 고를 것이
        /// 없다.</i> 관문 앞에서 답을 바꾸려면 훨씬 앞에서 높이를 벌어야 하고, 그건 동점 깨기가
        /// 아니라 <b>기반 정책</b>(<see cref="BotPilot"/>) 쪽 문제다.</para></summary>
        public static int Prefer(in RolloutBranch flapped, in RolloutBranch coasted, float sameReachEpsilon)
        {
            if (flapped.AliveTicks != coasted.AliveTicks)
            {
                return flapped.AliveTicks > coasted.AliveTicks ? 1 : -1;
            }
            if (Math.Abs(flapped.ReachX - coasted.ReachX) > sameReachEpsilon)
            {
                return flapped.ReachX > coasted.ReachX ? 1 : -1;
            }
            if (Math.Abs(flapped.EndVerticalSpeed - coasted.EndVerticalSpeed) > SameSpeedEpsilon)
            {
                return flapped.EndVerticalSpeed > coasted.EndVerticalSpeed ? 1 : -1;
            }
            return 0;
        }

        //  첫 틱만 지정한 대로 하고, 그 뒤는 <b>기반 정책 그대로</b> 굴린다. "다르게 눌렀으면"을
        //  묻는 것이지 "다른 봇이었으면"을 묻는 것이 아니다 — 그래서 둘째 틱부터는 손대지 않는다.
        private static RolloutBranch Roll<TState>(IRolloutWorld<TState> world, in TState state,
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
                    return new RolloutBranch(alive, world.ForwardX(s), world.VerticalSpeed(s));
                }
                alive = t + 1;
                if (world.Finished(s))
                {
                    //  끝까지 갔다. 더 굴릴 것이 없으니 "창을 다 살았다"로 세어, 창을 다 산
                    //  다른 후보와 ②(도달 거리)로 겨루게 한다.
                    return new RolloutBranch(horizon, world.ForwardX(s), world.VerticalSpeed(s));
                }
            }
            return new RolloutBranch(alive, world.ForwardX(s), world.VerticalSpeed(s));
        }
    }
}
