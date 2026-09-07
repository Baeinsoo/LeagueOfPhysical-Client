using System.Collections.Generic;
using FlappyRace;

namespace LOP.MapTools
{
    /// <summary>봇이 이번 틱에 내린 판단.</summary>
    public readonly struct BotDecision
    {
        public readonly bool Flap;
        /// <summary>앞을 훑어 지나갈 만한 틈을 찾았는가. 못 찾았으면 <see cref="AimY"/>는 뜻이 없다.</summary>
        public readonly bool GapFound;
        public readonly float AimY;

        public BotDecision(bool flap, bool gapFound, float aimY)
        {
            Flap = flap;
            GapFound = gapFound;
            AimY = aimY;
        }
    }

    /// <summary>
    /// 앞을 세로로 훑은 막힘 표를 보고 이번 틱에 날갯짓할지 정한다. 물리도 씬도 모른다 —
    /// 표는 부르는 쪽이 실제 콜라이더로 재서 넘긴다.
    ///
    /// <para>겨냥은 지연도 오차도 없이 완벽하다. 이 봇이 답하려는 질문이 "사람이 아주 잘하면
    /// 통과할 수 있는가"이기 때문이다. 반응 지연을 넣는 것은 난이도를 재는 다른 질문이다.</para>
    /// </summary>
    public static class BotPilot
    {
        /// <summary>날갯짓 한 번으로 오르는 높이. 세로 속도가 0이 될 때까지 더한 값이다.</summary>
        public static float FlapArc(float flapImpulse, float gravity, float tickSeconds)
        {
            float rise = 0f;
            float speed = flapImpulse;
            while (speed > 0f)
            {
                rise += speed * tickSeconds;
                speed -= gravity * tickSeconds;
            }
            return rise;
        }

        public static BotDecision Decide(IReadOnlyList<bool> blockedAhead, float bottomY, float step,
                                         float currentY, float verticalSpeed, float bodyRadius,
                                         float flapArc)
        {
            if (FlappyGapAiming.TryFindGap(blockedAhead, bottomY, step, currentY, bodyRadius,
                                           out float low, out float high) == false)
            {
                //  근거가 없을 땐 떠 있는 쪽을 고른다 — 떨어지게 두면 바닥에 부딪히고,
                //  이 검사는 "통과할 수 있는가"를 묻지 "얼마나 잘 나는가"를 묻지 않는다.
                return new BotDecision(flap: true, gapFound: false, aimY: currentY);
            }

            float aim = FlappyGapAiming.AimHeight(low, high, flapArc);
            //  한 틱 뒤를 본다. 날갯짓은 세로 속도를 덮어쓰므로, 안 누르면 내려갈 참일 때 지금 눌러야
            //  늦지 않는다. "지금 목표보다 낮은가"로 보면 이미 지나간 뒤다.
            float nextY = currentY + verticalSpeed * TickForLookahead;
            return new BotDecision(flap: nextY < aim, gapFound: true, aimY: aim);
        }

        //  한 틱 앞을 본다. 커널의 틱과 같은 값이지만, 이 클래스는 물리를 모르므로 상수로 둔다 —
        //  값이 갈리면 봇이 늦거나 이르게 누를 뿐 결과가 조용히 틀리지는 않는다.
        const float TickForLookahead = 0.02f;
    }
}
