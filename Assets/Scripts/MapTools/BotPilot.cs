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
        /// false이고, 그때는 <see cref="AimY"/>도 뜻이 없다.</summary>
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
    /// 통과할 수 있는가"이기 때문이다. 반응 지연을 넣는 것은 난이도를 재는 다른 질문이다.
    /// 그렇다고 "물리가 허락하는 것보다 겁 많은" 봇이어서도 안 된다 — 그러면 "이 맵은 어렵다"가
    /// "우리 봇이 겁쟁이다"로 뒤바뀌어 증명이 무의미해진다.</para>
    ///
    /// <para><b>틈 안 어디를 겨냥하고 언제 눌러야 안전한가</b>도 이 클래스가 정한다
    /// (<see cref="FlappyGapAiming"/>은 "틈이 어디 있나"만 찾아 주고, 그 안 어디를 밟을지는
    /// 다른 목적으로 바닥에 못박는다 — 여기서는 쓰지 않는다). 규칙:
    /// ① 바닥 쪽에서만 몸 반지름만큼 여유를 둔다 — 천장 쪽은 두지 않는다. 막힘 표 자체가
    ///    이미 몸(실제 반지름)으로 캡슐 검사를 한 결과라, 천장 쪽에 반지름을 또 빼면 몸
    ///    하나를 두 번 세는 꼴이라서다.
    /// ② 지금 눌렀을 때 "그 열에 도달하는 시점"의 높이를 <b>그 열 자신의 천장</b>과 비교한다
    ///    (근거리·원거리 열 각각) — 아치의 정점이 아니다. 정점은 그 열을 이미 지나친 자리에서
    ///    일어나므로, 이미 지나친 기준으로 재면 아직 뚫려 있는 하늘을 막힌 것으로 오판한다.
    ///    넘기면 절대 누르지 않는다 — 한 번 뚫으면 되돌릴 수 없지만, 안 눌러 낮아지는 건
    ///    다음 틱에 다시 판단할 수 있다.
    /// ③ 한 틱이 아니라 근거리 열까지 남은 틱을 실제 중력으로 굴려 봐서 바닥 쪽을 판단한다.
    /// 그리고 지금 틈만 보지 않고 <b>다음 틈도 함께</b> 본다 — 겹치면 두 틈을 동시에 만족하는
    /// 자리를, 안 겹치면(높은 틈 뒤에 낮은 틈처럼) 다음 틈 쪽으로 미리, 하지만 한 번에 다
    /// 옮기지 않고 근거리/원거리 비율만큼만 옮긴 자리를 겨냥한다.
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
            IReadOnlyList<bool> blockedNear, IReadOnlyList<bool> blockedFar,
            float bottomY, float step,
            float currentY, float verticalSpeed, float bodyRadius,
            float flapImpulse, float gravity, float maxFallSpeed,
            int ticksToNear, int ticksToFar, float tickSeconds)
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
                    return new BotDecision(flap: true, gapFound: false, aimY: currentY);
                }
            }

            float low = lowNear, high = highNear;
            bool hasFar = FlappyGapAiming.TryFindGap(blockedFar, bottomY, step, currentY, bodyRadius,
                                                     out float lowFar, out float highFar);
            if (hasFar == false)
            {
                hasFar = FlappyGapAiming.TryFindGap(blockedFar, bottomY, step, currentY, 0f,
                                                    out lowFar, out highFar);
            }
            if (hasFar)
            {
                //  다음 장애물의 틈도 미리 봐 둔다. 지금 틈만 보고 가운데 근처를 지키면, 그
                //  틈을 다 지나기도 전에 다음 틈이 낮아져 있을 때 이미 너무 높은 채로 도착해
                //  박는다(실측된 실패 패턴 — 높은 틈 다음에 낮은 틈).
                if (FlappyGapAiming.TryIntersect(low, high, lowFar, highFar, out float bothLow, out float bothHigh))
                {
                    //  겹친다 — 두 틈을 동시에 만족하는 자리가 있다. FlappyGapAiming.TryIntersect는
                    //  원래 "여러 기둥을 차례로 다 통과하는 자리"를 구하려고 있던 함수라 그대로 쓴다.
                    low = bothLow;
                    high = bothHigh;
                }
                else
                {
                    //  안 겹친다 — 지금 틈과 다음 틈이 이어지지 않는다(예: 지금은 높고 넓은데
                    //  다음은 낮고 좁다). 어느 한쪽 가장자리만 다음 틈 쪽으로 당기면 반대쪽은
                    //  그대로 남아, 정작 필요한 방향(내려가야/올라가야 함)을 오히려 막는다 —
                    //  그래서 밴드 전체(위·아래 가장자리 다)를 다음 틈 쪽으로 옮기되, 한 번에
                    //  다음 틈 자리로 스냅하지 않고 근거리/원거리 열 비율만큼만(가까운 열일수록
                    //  적게, 먼 열에 가까워질수록 크게) 옮긴다 — 근거리·원거리 열의 실제 자리
                    //  자체가 매 틱 앞으로 흘러가며 자연히 갱신되므로, 이 비율이 고정이어도
                    //  결과는 갑자기 튀지 않고 매끄럽게 이어진다.
                    float weight = ticksToFar > 0 ? Math.Min(1f, (float)ticksToNear / ticksToFar) : 1f;
                    float pulledLow = low + (lowFar - low) * weight;
                    float pulledHigh = high + (highFar - high) * weight;
                    //  당기다 폭이 너무 좁아지면(둘이 정반대 방향으로 당겨져 역전되는 등)
                    //  가운데를 축으로 최소폭만큼은 남긴다 — 완전히 한 점으로 무너뜨리지 않는다.
                    float minBand = bodyRadius * 2f;
                    if (pulledHigh - pulledLow < minBand)
                    {
                        float mid = (pulledLow + pulledHigh) * 0.5f;
                        pulledLow = mid - minBand * 0.5f;
                        pulledHigh = mid + minBand * 0.5f;
                    }
                    low = pulledLow;
                    high = pulledHigh;
                }
            }

            //  바닥 쪽에서만 몸 반지름만큼 여유를 둔다. blockedNear/Far 자체가 이미 몸(실제
            //  반지름)으로 캡슐 검사를 한 결과라 high는 이미 "몸이 딱 맞게 들어가는" 자리다.
            //  거기서 반지름을 또 빼면 몸 하나를 두 번 세는 꼴이라, 천장 쪽엔 마진을 더하지 않는다.
            float safeFloor = low + bodyRadius;

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
            bool ceilingSafeFar = true;
            if (hasFar)
            {
                float riseAtFar = FlapRiseAfter(flapImpulse, gravity, tickSeconds, ticksToFar);
                ceilingSafeFar = currentY + riseAtFar <= highFar;
            }

            bool flap = wantsFlap && ceilingSafeNear && ceilingSafeFar;
            float flapArc = FlapArc(flapImpulse, gravity, tickSeconds);
            float aim = FlappyGapAiming.AimHeight(low, high, flapArc);
            return new BotDecision(flap, gapFound: true, aimY: aim);
        }
    }
}
