using System;
using System.Collections.Generic;
using FlappyRace;

namespace LOP.MapTools
{
    /// <summary>봇이 이번 틱에 내린 판단.</summary>
    public readonly struct BotDecision
    {
        public readonly bool Flap;
        /// <summary>앞을 훑어 지나갈 만한 자리를 찾았는가(몸이 다 들어가지 못하는 좁은 틈도
        /// 포함 — 그런 자리에도 천장 가드는 그대로 걸린다). 아예 아무 자리도 못 찾았을 때만
        /// false다.</summary>
        public readonly bool GapFound;

        public BotDecision(bool flap, bool gapFound)
        {
            Flap = flap;
            GapFound = gapFound;
        }
    }

    /// <summary>
    /// 앞을 세로로 훑은 막힘 표를 보고 이번 틱에 날갯짓할지 정한다. 물리도 씬도 모른다 —
    /// 표는 부르는 쪽이 실제 콜라이더로 재서 넘긴다.
    ///
    /// <para>겨냥은 지연도 오차도 없이 완벽하다. 이 봇이 답하려는 질문이 "사람이 아주 잘하면
    /// 통과할 수 있는가"이기 때문이다. 반응 지연을 넣는 것은 난이도를 재는 다른 질문이다.
    /// 그렇다고 "물리가 허락하는 것보다 겁 많은" 봇이어서도 안 된다 — 그러면 "이 맵은 어렵다"가
    /// "우리 봇이 겁쟁이다"로 뒤바뀌어 증명이 무의미해진다.</para>
    ///
    /// <para><b>언제 눌러야 안전한가</b>도 이 클래스가 정한다
    /// (<see cref="FlappyGapAiming"/>은 "틈이 어디 있나"만 찾아 준다). 규칙은 근거리·정점·원거리
    /// 세 열 각각에 <b>같은 모양의 천장 가드를 걸고, 셋을 AND로 묶는다</b> — 세 열 중
    /// 하나라도 막히면 누르지 않는다:
    /// ① 바닥 쪽에서만 몸 반지름만큼 여유를 둔다 — 천장 쪽은 두지 않는다. 막힘 표 자체가
    ///    이미 몸(실제 반지름)으로 캡슐 검사를 한 결과라, 천장 쪽에 반지름을 또 빼면 몸
    ///    하나를 두 번 세는 꼴이라서다.
    /// ② 지금 눌렀을 때 "그 열에 도달하는 시점"의 높이를 <b>그 열 자신의 천장</b>과 비교한다
    ///    (근거리·정점·원거리 열 각각) — 아치의 정점이 아니다. 정점은 대개 그 세 열 중
    ///    하나(정점 열)에서 일어나지만, 규칙 자체는 "그 열에 실제로 도달하는 순간의 높이"이지
    ///    "아치 전체의 최고점"이 아니다. 넘기면 절대 누르지 않는다 — 한 번 뚫으면 되돌릴 수
    ///    없지만, 안 눌러 낮아지는 건 다음 틱에 다시 판단할 수 있다.
    /// ③ 한 틱이 아니라 근거리 열까지 남은 틱을 실제 중력으로 굴려 봐서 바닥 쪽을 판단한다.
    /// 몸이 다 들어가지 못할 만큼 좁은 자리라도 근거 없이 무조건 날갯짓하지 않는다 — 그
    /// 자리에도 같은 천장 가드를 건다.</para>
    /// </summary>
    public static class BotPilot
    {
        /// <summary>날갯짓 한 번으로 오르는 높이(자연 정점까지 전부). 세로 속도가 0이 될
        /// 때까지 더한 값이다.</summary>
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

        /// <summary>
        /// 지금 누르면 <paramref name="ticks"/>틱 뒤 도달하는 높이(자연 정점까지 다 오른 게
        /// 아니라 그 시점까지만). 실제 게임 커널의 순서와 같다 — 누른 그 틱은 중력 감쇠 없이
        /// 임펄스 그대로 움직이고(실제 Step()이 그 틱의 중력 감쇠 계산을 덮어써 버리므로),
        /// 그다음 틱부터 중력이 깎는다. <paramref name="ticks"/>가 자연 정점 틱수보다 크면
        /// 도중에 속도가 0 밑으로 내려가 자연히 <see cref="FlapArc"/>와 같은 값에서 멈춘다.
        /// </summary>
        public static float FlapRiseAfter(float flapImpulse, float gravity, float tickSeconds, int ticks)
        {
            float rise = 0f;
            float speed = flapImpulse;
            for (int i = 0; i < ticks && speed > 0f; i++)
            {
                rise += speed * tickSeconds;
                speed -= gravity * tickSeconds;
            }
            return rise;
        }

        public static BotDecision Decide(
            IReadOnlyList<bool> blockedNear, IReadOnlyList<bool> blockedApex, IReadOnlyList<bool> blockedFar,
            float bottomY, float step,
            float currentY, float verticalSpeed, float bodyRadius,
            float flapImpulse, float gravity, float maxFallSpeed,
            int ticksToNear, int ticksToApex, int ticksToFar, float tickSeconds)
        {
            bool hasNear = FlappyGapAiming.TryFindGap(blockedNear, bottomY, step, currentY, bodyRadius,
                                                      out float lowNear, out float highNear);
            if (hasNear == false)
            {
                //  몸이 통째로 들어갈 만큼 넓은 자리를 못 찾았다고 근거 없이 무조건 누르지
                //  않는다 — 반지름 조건을 0으로 풀어 "그나마 가장 가까운 뚫린 자리"만 다시
                //  찾는다(TryFindGap 자체는 안 고치고 최소폭 인자만 0으로 줘서 문턱을 없앤다).
                //  그 자리에도 아래의 같은 천장 가드를 그대로 건다 — 여기서 무조건 눌러 버리면
                //  가장 좁은 통로에서 정확히 옛날 버그(가드 없는 무조건 날갯짓)가 재발한다.
                hasNear = FlappyGapAiming.TryFindGap(blockedNear, bottomY, step, currentY, 0f,
                                                     out lowNear, out highNear);
                if (hasNear == false)
                {
                    //  그마저도 없다 — 근처에 뚫린 자리가 전혀 없다. 판단할 근거가 정말 없을
                    //  때만 뜨는 쪽을 고른다(떨어지면 확실히 바닥에 부딪힌다).
                    return new BotDecision(flap: true, gapFound: false);
                }
            }

            //  바닥 쪽에서만 몸 반지름만큼 여유를 둔다. blockedNear 자체가 이미 몸(실제
            //  반지름)으로 캡슐 검사를 한 결과라 highNear는 이미 "몸이 딱 맞게 들어가는"
            //  자리다. 거기서 반지름을 또 빼면 몸 하나를 두 번 세는 꼴이라, 천장 쪽엔 마진을
            //  더하지 않는다.
            float safeFloor = lowNear + bodyRadius;

            //  "한 틱 뒤"가 아니라 근거리 열까지 남은 틱을 실제 중력으로 굴려 본 자리로
            //  바닥 쪽을 판단한다 — 한 틱만 보면 중력이 매 틱 더 세지는 걸 놓쳐 늦는다.
            float predictedY = FlappyGapAiming.PredictHeight(currentY, verticalSpeed, ticksToNear,
                                                              tickSeconds, gravity, maxFallSpeed);
            bool wantsFlap = predictedY < safeFloor;

            //  지금 눌렀을 때 "각 열에 도달하는 시점"의 높이를 그 열 자신의 천장과 비교한다 —
            //  아치의 정점이 아니라 그 열에 실제로 도달하는 순간의 높이다. 정점은 그 열을
            //  이미 지나친 곳에서 일어나므로, 정점을 아무 열의 천장과 비교하면 이미 지나친
            //  기준으로 지금 판단하는 꼴이 되어 아직 뚫려 있는 하늘까지 막힌 것으로 오판한다.
            float riseAtNear = FlapRiseAfter(flapImpulse, gravity, tickSeconds, ticksToNear);
            bool ceilingSafeNear = currentY + riseAtNear <= highNear;

            //  정점 열 — 날갯짓 아치가 가장 높이 오르는 자리(세로 속도가 0이 되는 순간)다.
            //  근거리·원거리 열 사이에 숨은, 두 열 모두보다 좁은 위쪽 기둥은 그 두 가드를
            //  통과해 버리므로 이 열을 따로 봐야 한다. 규칙은 근거리·원거리와 완전히 같다.
            bool ceilingSafeApex = true;
            bool hasApex = FlappyGapAiming.TryFindGap(blockedApex, bottomY, step, currentY, bodyRadius,
                                                      out _, out float highApex);
            if (hasApex == false)
            {
                hasApex = FlappyGapAiming.TryFindGap(blockedApex, bottomY, step, currentY, 0f,
                                                     out _, out highApex);
            }
            if (hasApex)
            {
                float riseAtApex = FlapRiseAfter(flapImpulse, gravity, tickSeconds, ticksToApex);
                ceilingSafeApex = currentY + riseAtApex <= highApex;
            }

            bool ceilingSafeFar = true;
            bool hasFar = FlappyGapAiming.TryFindGap(blockedFar, bottomY, step, currentY, bodyRadius,
                                                     out _, out float highFar);
            if (hasFar == false)
            {
                hasFar = FlappyGapAiming.TryFindGap(blockedFar, bottomY, step, currentY, 0f,
                                                    out _, out highFar);
            }
            if (hasFar)
            {
                float riseAtFar = FlapRiseAfter(flapImpulse, gravity, tickSeconds, ticksToFar);
                ceilingSafeFar = currentY + riseAtFar <= highFar;
            }

            bool flap = wantsFlap && ceilingSafeNear && ceilingSafeApex && ceilingSafeFar;
            return new BotDecision(flap, gapFound: true);
        }
    }
}
