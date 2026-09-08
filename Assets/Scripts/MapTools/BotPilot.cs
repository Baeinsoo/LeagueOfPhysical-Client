using System;
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
    ///
    /// <para><b>틈 안 어디를 겨냥하고 언제 눌러야 안전한가</b>도 이 클래스가 정한다
    /// (<see cref="FlappyGapAiming"/>은 "틈이 어디 있나"만 찾아 주고, 그 안 어디를 밟을지는
    /// 다른 목적으로 바닥에 못박는다 — 여기서는 쓰지 않는다). 규칙은 세 가지:
    /// ① 틈의 위·아래 가장자리 모두에서 몸 반지름만큼 떨어져서 지나간다.
    /// ② 지금 눌렀을 때 다다르는 정점(아치 전체)이 천장 마진을 넘으면 <b>절대 누르지 않는다</b> —
    ///    한 번 뚫으면 되돌릴 수 없지만, 안 눌러 낮아지는 건 다음 틱에 다시 판단할 수 있다.
    /// ③ 한 틱이 아니라 몇 틱 앞을 실제 중력으로 굴려 봐서 판단한다 — 중력은 매 틱 더 세게
    ///    당기므로, 한 틱 뒤 값만 보면 안전해 보였다가 그다음 틱엔 이미 늦어 있다.
    /// 그리고 지금 틈만 보지 않고 <b>다음 틈도 함께</b> 본다 — 높은 틈 뒤에 낮은 틈이 오면,
    /// 지금 틈의 넉넉한 천장만 믿고 오르다 다음 틈에 못 미친 채 도착해 박기 때문이다.</para>
    /// </summary>
    public static class BotPilot
    {
        /// <summary>날갯짓 한 번으로 오르는 높이. 세로 속도가 0이 될 때까지 더한 값이다.</summary>
        public static float FlapArc(float flapImpulse, float gravity, float tickSeconds)
        {
            //  gravity(중력)나 tickSeconds(한 틱의 길이)가 0 이하면 아래 루프에서 speed가 절대
            //  0 밑으로 안 내려간다 — 무한 루프. 조용히 걸리는 것보다 바로 터지는 게 낫다.
            if (gravity <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity,
                    "gravity must be positive — otherwise speed never drops to 0 and the loop never terminates.");
            }
            if (tickSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds,
                    "tickSeconds must be positive — otherwise speed never drops to 0 and the loop never terminates.");
            }

            float rise = 0f;
            float speed = flapImpulse;
            while (speed > 0f)
            {
                rise += speed * tickSeconds;
                speed -= gravity * tickSeconds;
            }
            return rise;
        }

        //  장애물까지 남은 틱을 실제 중력으로 굴려 본다. 1틱만 보면 중력이 매 틱 더 세지는 걸
        //  놓쳐 "아직 안전"이라고 오판한 바로 다음 틱에 이미 마진 아래인 경우가 생긴다.
        //  프로토타입급 봇들이 "장애물까지 몇 틱, 그만큼 미리 굴려 본다"고 쓰는 방식을
        //  고정 상수로 흉내낸다(가까움에 따라 동적으로 바꾸지 않음 — 스캔 거리 자체가
        //  고정이라 "장애물까지 남은 틱 수"도 사실상 고정이다).
        private const int ProjectionTicks = 3;

        public static BotDecision Decide(
            IReadOnlyList<bool> blockedNear, IReadOnlyList<bool> blockedFar,
            float bottomY, float step,
            float currentY, float verticalSpeed, float bodyRadius,
            float flapArc, float gravity, float maxFallSpeed, float tickSeconds)
        {
            if (FlappyGapAiming.TryFindGap(blockedNear, bottomY, step, currentY, bodyRadius,
                                           out float low, out float high) == false)
            {
                //  근거가 없을 땐 떠 있는 쪽을 고른다 — 떨어지게 두면 바닥에 부딪히고,
                //  이 검사는 "통과할 수 있는가"를 묻지 "얼마나 잘 나는가"를 묻지 않는다.
                return new BotDecision(flap: true, gapFound: false, aimY: currentY);
            }

            //  다음 장애물의 틈도 미리 봐 둔다. 지금 틈만 보고 가운데 근처를 지키면, 그 틈을
            //  다 지나기도 전에 다음 틈이 낮아져 있을 때 이미 너무 높은 채로 도착해 박는다
            //  (실측된 실패 패턴 — 높은 틈 다음에 낮은 틈). FlappyGapAiming.TryIntersect는
            //  원래 "여러 기둥을 차례로 다 통과하는 자리"를 구하려고 있던 함수라 그대로 쓴다.
            if (FlappyGapAiming.TryFindGap(blockedFar, bottomY, step, currentY, bodyRadius,
                                           out float lowFar, out float highFar))
            {
                if (FlappyGapAiming.TryIntersect(low, high, lowFar, highFar, out float bothLow, out float bothHigh))
                {
                    low = bothLow;
                    high = bothHigh;
                }
                else
                {
                    //  두 틈이 안 겹친다 — 이번 틈을 다 지나면 다음 틈엔 못 미친다. 다음 틈에
                    //  가까운 가장자리 쪽으로 지금부터 기울여 둔다(과도하게 미루지 않고
                    //  미리 정렬 — 갑자기 바뀌지 않고 다음 틈이 오는 쪽을 지금부터 반영).
                    float nearCenter = (low + high) * 0.5f;
                    float farCenter = (lowFar + highFar) * 0.5f;
                    if (farCenter < nearCenter)
                    {
                        high = Math.Max(low, Math.Min(high, farCenter));
                    }
                    else if (farCenter > nearCenter)
                    {
                        low = Math.Min(high, Math.Max(low, farCenter));
                    }
                }
            }

            //  틈의 위·아래 가장자리 모두에서 몸 반지름만큼은 떨어져서 지나간다. blockedNear/Far
            //  자체가 이미 몸 반지름만큼 겹치지 않는 자리만 "안 막힘"으로 표시하므로, 이 마진은
            //  그 위에 얹는 추가 여유다(프로토타입도 자기 기본 반지름과 같은 크기의 마진을 썼다).
            float margin = bodyRadius;
            float safeFloor = low + margin;
            float safeCeil = high - margin;
            if (safeFloor > safeCeil)
            {
                //  마진을 다 채우면 역전될 만큼 좁다 — 가운데 한 점으로 좁혀 둔다. 이러면 아래
                //  두 조건이 사실상 항상 걸려 날갯짓이 나가지 않는다: 바닥 쪽으로 처지는 것보다
                //  천장을 뚫는 게 항상 더 나쁘다 — 한 번 뚫으면 그 자리에서 못 지나가지만,
                //  안 눌러 낮아지는 건 다음 틱에 다시 판단할 수 있다.
                safeFloor = safeCeil = (low + high) * 0.5f;
            }

            //  "한 틱 뒤"가 아니라 장애물까지 남은 몇 틱을 실제 중력으로 굴려 본 자리로 판단한다.
            float predictedY = FlappyGapAiming.PredictHeight(currentY, verticalSpeed, ProjectionTicks,
                                                              tickSeconds, gravity, maxFallSpeed);
            bool wantsFlap = predictedY < safeFloor;

            //  누르면 오르는 아치 전체(flapArc)를 더한 자리가 천장 마진을 넘는가. 넘기면
            //  절대 누르지 않는다 — 둘 다 나빠 보여도 되돌릴 수 없는 쪽(천장을 뚫는 것)을 피한다.
            bool ceilingSafe = currentY + flapArc <= safeCeil;

            bool flap = wantsFlap && ceilingSafe;
            float aim = FlappyGapAiming.AimHeight(low, high, flapArc);
            return new BotDecision(flap, gapFound: true, aimY: aim);
        }
    }
}
