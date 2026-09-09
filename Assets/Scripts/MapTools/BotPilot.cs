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
    /// 앞을 세로로 훑은 막힘 표와 자유공간 프로브를 보고 이번 틱에 날갯짓할지 정한다. 물리도
    /// 씬도 모른다 — 표도 프로브도 부르는 쪽이 실제 콜라이더로 재서 넘긴다.
    ///
    /// <para>겨냥은 지연도 오차도 없이 완벽하다. 이 봇이 답하려는 질문이 "사람이 아주 잘하면
    /// 통과할 수 있는가"이기 때문이다. 반응 지연을 넣는 것은 난이도를 재는 다른 질문이다.
    /// 그렇다고 "물리가 허락하는 것보다 겁 많은" 봇이어서도 안 된다 — 그러면 "이 맵은 어렵다"가
    /// "우리 봇이 겁쟁이다"로 뒤바뀌어 증명이 무의미해진다.</para>
    ///
    /// <para><b>언제 눌러야 안전한가</b>도 이 클래스가 정한다
    /// (<see cref="FlappyGapAiming"/>은 "틈이 어디 있나"만 찾아 준다). 규칙은 셋이다:
    /// ① 바닥 쪽에서만 몸 반지름만큼 여유를 둔다 — 천장 쪽은 두지 않는다. 막힘 표 자체가
    ///    이미 몸(실제 반지름)으로 캡슐 검사를 한 결과라, 천장 쪽에 반지름을 또 빼면 몸
    ///    하나를 두 번 세는 꼴이라서다.
    /// ② 지금 누르면 새가 그리는 아치를 <b>정점까지 틱마다 따라가며</b>, 그 자리마다 몸이
    ///    들어가는지 자유공간 프로브에 직접 묻는다. 도착 높이 몇 개만 재면 "가는 길"을 안 보게
    ///    된다 — 천장 슬래브 <i>위</i>의 빈 하늘이 도착점으로는 뚫려 있어도, 올라가는 도중에 그
    ///    슬래브에 박는다. 올라가는 구간에서 한 자리라도 막혀 있으면 누르지 않는다 — 한 번
    ///    뚫으면 되돌릴 수 없지만, 안 눌러 낮아지는 건 다음 틱에 다시 판단할 수 있다.
    /// ③ 한 틱이 아니라 근거리 열까지 남은 틱을 실제 중력으로 굴려 봐서 바닥 쪽을 판단한다.
    /// 몸이 다 들어가지 못할 만큼 좁은 자리라도 근거 없이 무조건 날갯짓하지 않는다 — 그
    /// 자리에도 같은 천장 가드를 건다.</para>
    /// </summary>
    public static class BotPilot
    {
        //  아치를 끝까지 따라가는 코드(FlapArc와 Decide의 훑기)는 둘 다 "세로 속도가 0으로
        //  떨어지면 끝"에 기대어 돈다. 중력이나 한 틱의 길이가 0 이하면 속도가 영영 안 줄어
        //  무한 루프가 된다 — 에디터가 조용히 멎는 것보다 바로 터지는 게 낫다.
        static void RequireArcEnds(float gravity, float tickSeconds)
        {
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
        }

        /// <summary>날갯짓 한 번으로 오르는 높이(자연 정점까지 전부). 세로 속도가 0이 될
        /// 때까지 더한 값이다.</summary>
        public static float FlapArc(float flapImpulse, float gravity, float tickSeconds)
        {
            RequireArcEnds(gravity, tickSeconds);

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

        /// <param name="blockedNear">근거리 열의 막힘 표 — <b>바닥 규칙</b>이 쓴다(어느 높이로
        /// 겨냥할지). 천장 판단은 이제 이 표가 아니라 <paramref name="isFree"/>가 한다.</param>
        /// <param name="isFree">발밑이 (x, y)일 때 몸이 들어가는가. 코스를 실제 콜라이더로 재는
        /// 프로브를 호출부가 넘긴다.</param>
        public static BotDecision Decide(
            IReadOnlyList<bool> blockedNear, float bottomY, float step,
            float currentX, float currentY, float verticalSpeed, float bodyRadius,
            float flapImpulse, float gravity, float maxFallSpeed,
            float forwardSpeed, int ticksToNear, float tickSeconds,
            FreeSpaceProbe isFree)
        {
            RequireArcEnds(gravity, tickSeconds);

            //  highNear(틈의 위 끝)는 안 쓴다 — 천장 가드는 아치를 직접 훑으므로 "어느 틈을
            //  골랐나"의 위 끝은 이 자리에서 의미가 없다. lowNear(바닥 규칙용)만 남긴다.
            bool hasNear = FlappyGapAiming.TryFindGap(blockedNear, bottomY, step, currentY, bodyRadius,
                                                      out float lowNear, out _);
            if (hasNear == false)
            {
                //  몸이 통째로 들어갈 만큼 넓은 자리를 못 찾았다고 근거 없이 무조건 누르지
                //  않는다 — 반지름 조건을 0으로 풀어 "그나마 가장 가까운 뚫린 자리"만 다시
                //  찾는다(TryFindGap 자체는 안 고치고 최소폭 인자만 0으로 줘서 문턱을 없앤다).
                //  그 자리에도 아래의 같은 천장 가드를 그대로 건다 — 여기서 무조건 눌러 버리면
                //  가장 좁은 통로에서 정확히 옛날 버그(가드 없는 무조건 날갯짓)가 재발한다.
                hasNear = FlappyGapAiming.TryFindGap(blockedNear, bottomY, step, currentY, 0f,
                                                     out lowNear, out _);
                if (hasNear == false)
                {
                    //  그마저도 없다 — 근처에 뚫린 자리가 전혀 없다. 판단할 근거가 정말 없을
                    //  때만 뜨는 쪽을 고른다(떨어지면 확실히 바닥에 부딪힌다).
                    return new BotDecision(flap: true, gapFound: false);
                }
            }

            //  바닥 쪽에서만 몸 반지름만큼 여유를 둔다. blockedNear 자체가 이미 몸(실제
            //  반지름)으로 캡슐 검사를 한 결과라 이 틈은 이미 "몸이 딱 맞게 들어가는" 자리다.
            //  거기서 반지름을 또 빼면 몸 하나를 두 번 세는 꼴이라, 천장 쪽엔 마진을 더하지 않는다.
            float safeFloor = lowNear + bodyRadius;

            //  "한 틱 뒤"가 아니라 근거리 열까지 남은 틱을 실제 중력으로 굴려 본 자리로
            //  바닥 쪽을 판단한다 — 한 틱만 보면 중력이 매 틱 더 세지는 걸 놓쳐 늦는다.
            float predictedY = FlappyGapAiming.PredictHeight(currentY, verticalSpeed, ticksToNear,
                                                              tickSeconds, gravity, maxFallSpeed);
            bool wantsFlap = predictedY < safeFloor;

            //  누르면 새가 그리는 아치를 정점까지 틱마다 따라가며, 그 자리마다 몸이 들어가는지 묻는다.
            //  정점에서 멈추는 이유: 올라가는 동안은 "누르고 가만히 있는" 이 경로가 도달 가능한 가장 낮은
            //  경로라 막혀 있으면 정말 못 피한다. 정점을 지나면 새는 내려가기 시작하고, 거기서 한 번 더
            //  누르면 다시 오르므로 그 아래가 막혔다는 것이 지금 누르지 말아야 할 이유가 되지 않는다.
            //  도착 높이만 보면 "가는 길"을 안 보게 된다 — 천장 슬래브 위의 빈 하늘이 도착점으로
            //  뚫려 있어도, 올라가는 도중에 그 슬래브에 박는다.
            //  마진은 어디에도 더하지 않는다 — 훑기는 몸이 실제로 지나는 자리만 묻는다.
            bool ceilingSafe = true;
            float x = currentX;
            float y = currentY;
            //  누른 그 틱은 중력 감쇠 없이 임펄스 그대로 — 실제 커널(Step)이 그 틱의 감쇠를
            //  덮어써 버리므로, FlapRiseAfter와 같은 순서다. 종료 조건도 FlapArc와 같은
            //  while (speed > 0f)라, "정점이 몇 틱째냐"가 숫자로 박히지 않고 물리에서 나온다.
            float speed = flapImpulse;
            while (speed > 0f)
            {
                float nextX = x + forwardSpeed * tickSeconds;
                float nextY = y + speed * tickSeconds;
                //  한 틱 사이를 선분으로 훑는다 — 끝점만 보면 그 사이에 낀 얇은 판을 통과한다.
                //  이 한 호출이 양 끝점까지 전부 본다(SegmentIsFree가 i=0..samples를 돌아
                //  두 끝을 포함한다), 그래서 끝점을 따로 묻지 않는다.
                if (CleanRunSearch.SegmentIsFree(isFree, x, y, nextX, nextY, step) == false)
                {
                    ceilingSafe = false;
                    break;
                }
                x = nextX;
                y = nextY;
                speed -= gravity * tickSeconds;
            }

            bool flap = wantsFlap && ceilingSafe;
            return new BotDecision(flap, gapFound: true);
        }
    }
}
