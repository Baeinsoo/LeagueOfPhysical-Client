using System.Collections.Generic;

namespace FlappyRace
{
    /// <summary>
    /// [진단용 임시] 오토파일럿의 순수 계산 — 세로로 훑은 "막힘/뚫림" 표에서 지나갈 수 있는 틈을
    /// 고르고, 그 틈의 어디를 겨냥할지 정한다. 물리도 시간도 모르므로 테스트로 고정할 수 있다.
    ///
    /// 옛 <c>FlappyCourseScan</c>이 하던 계산과 같은 규칙이다. 다른 것은 "무엇이 막혔나"를 얻는
    /// 경로뿐 — 그쪽은 <c>FlappyObstacle</c>이 붙은 콜라이더 bounds를 읽었는데, 실제 맵에는 그
    /// 컴포넌트가 없어(씬에 있는 MonoBehaviour는 SpawnPoint·FinishLine뿐) 쓸 수 없다.
    /// 그래서 막힘 여부는 부르는 쪽이 물리로 재서 넘긴다.
    /// </summary>
    public static class FlappyGapAiming
    {
        /// <summary>
        /// 아래에서 위로 훑은 막힘 표에서, <paramref name="currentY"/>에 가장 가까우면서 새가
        /// 지나갈 수 있는(2·반지름 이상) 틈을 고른다. 없으면 false.
        /// </summary>
        /// <param name="blocked">아래에서 위로 <paramref name="step"/> 간격의 막힘 여부.</param>
        /// <param name="bottomY">blocked[0]의 y.</param>
        public static bool TryFindGap(IReadOnlyList<bool> blocked, float bottomY, float step,
                                      float currentY, float bodyRadius,
                                      out float low, out float high)
        {
            low = 0f;
            high = 0f;
            float minSpan = bodyRadius * 2f;
            float bestDistance = float.MaxValue;
            bool found = false;

            int runStart = -1;
            for (int i = 0; i <= blocked.Count; i++)
            {
                //  마지막 칸을 지나서도 한 번 더 도는 이유: 표 끝까지 뚫려 있으면 그 구간이 닫히지 않는다.
                bool isBlocked = i == blocked.Count || blocked[i];
                if (isBlocked == false)
                {
                    if (runStart < 0)
                    {
                        runStart = i;
                    }
                    continue;
                }
                if (runStart < 0)
                {
                    continue;
                }

                float runLow = bottomY + runStart * step;
                float runHigh = bottomY + (i - 1) * step;
                runStart = -1;
                if (runHigh - runLow < minSpan)
                {
                    continue;
                }

                //  "가장 가까운"은 틈 중심까지의 거리로 잰다 — 옛 스캔과 같은 기준이다.
                float distance = System.Math.Abs((runLow + runHigh) * 0.5f - currentY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    low = runLow;
                    high = runHigh;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// 날갯짓 한 번이 올려 주는 높이. 이 값이 겨냥 위치를 정한다 — 틈이 이보다 넓으면 가운데를
        /// 노려도 천장에 안 닿고, 좁으면 바닥에 붙어야 아치가 틈 안에 들어온다.
        /// </summary>
        public static float FlapArc(float flapImpulse, float gravity)
            => flapImpulse * flapImpulse / (2f * gravity);

        /// <summary>
        /// 틈 안에서 어느 높이를 목표로 삼을지. 옛 <c>FlappyCourseScan.Target</c>의 <c>pLo</c>와 같은 식.
        /// </summary>
        public static float AimHeight(float low, float high, float flapArc)
        {
            float span = high - low;
            return span >= flapArc ? low + (span - flapArc) * 0.5f : low;
        }

        /// <summary>
        /// 여러 기둥의 틈이 겹치는 구간. 새는 그 기둥들을 <b>차례로 다</b> 지나야 하므로, 앞쪽 하나만
        /// 보고 겨냥하면 그다음 기둥에 박는다. 겹치는 데가 없으면 false.
        /// </summary>
        public static bool TryIntersect(float lowA, float highA, float lowB, float highB,
                                        out float low, out float high)
        {
            low = lowA > lowB ? lowA : lowB;
            high = highA < highB ? highA : highB;
            return high > low;
        }

        /// <summary>
        /// 중력만 받았을 때 <paramref name="ticks"/>틱 뒤의 높이. 시뮬(<c>FlappyMoveSystem</c>)과 같은
        /// 순서로 밟는다 — 속도를 먼저 줄이고 종단속도로 자른 뒤 그 속도로 움직인다. 순서를 바꾸면
        /// 예측이 실제보다 한 틱씩 어긋난다.
        /// </summary>
        public static float PredictHeight(float y, float verticalSpeed, int ticks, float deltaTime,
                                          float gravity, float maxFallSpeed)
        {
            for (int i = 0; i < ticks; i++)
            {
                verticalSpeed -= gravity * deltaTime;
                if (verticalSpeed < -maxFallSpeed)
                {
                    verticalSpeed = -maxFallSpeed;
                }
                y += verticalSpeed * deltaTime;
            }
            return y;
        }

        /// <summary>
        /// 이번 틱에 날갯짓해야 하는가. <b>지금 낮은가가 아니라 "이대로 두면 도착할 때 걸리는가"</b>를 본다 —
        /// 날갯짓은 정점까지 여러 틱이 걸리므로, 눈앞이 낮아진 뒤에 치면 이미 늦는다.
        /// </summary>
        /// <param name="ticks">틈에 도달하기까지 남은 틱 수.</param>
        /// <param name="margin">틈 가장자리에서 이만큼은 떨어져서 지나간다(몸 반지름 몫).</param>
        public static bool ShouldFlap(float y, float verticalSpeed, float low, float high,
                                      int ticks, float deltaTime, float gravity, float maxFallSpeed,
                                      float flapImpulse, float margin)
        {
            float coasting = PredictHeight(y, verticalSpeed, ticks, deltaTime, gravity, maxFallSpeed);
            if (coasting >= low + margin)
            {
                return false;   // 가만히 둬도 바닥을 넘겨 통과한다
            }

            //  치면 틈 위로 넘겨 버리는가. 넘길 것 같으면 <b>치지 않는다</b> — 둘 다 나빠 보여도
            //  덜 나쁜 쪽이 안 치는 쪽이다. 안 치면 조금 낮아질 뿐이고 다음 틱에 다시 판단하면
            //  되지만, 치면 3.5m를 솟아 틈 위 기둥에 박는다. 박으면 0.8초 얼고, 떨어지면서 같은
            //  판단을 반복해 그 자리에서 영영 못 지나간다.
            //  (예전엔 "안 쳐도 어차피 바닥 밑으로 샌다"면 치게 두는 예외가 있었다. 그 예외가 바로
            //  이 고리를 만들었다 — 그 규칙으로는 코스 완주가 10번 중 0번, 빼니 10번 중 10번이다.)
            float flapped = PredictHeight(y, flapImpulse, ticks, deltaTime, gravity, maxFallSpeed);
            if (flapped > high - margin)
            {
                return false;
            }
            return true;
        }
    }
}
