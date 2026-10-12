using System;
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>한 구간 검사 결과. <see cref="Passed"/>가 false면 <see cref="BlockedX"/>가 마지막으로 살아남은 x다.</summary>
    public readonly struct SectionReachResult
    {
        public readonly bool Passed;
        public readonly float BlockedX;
        public readonly int MaxStates;

        public SectionReachResult(bool passed, float blockedX, int maxStates)
        {
            Passed = passed; BlockedX = blockedX; MaxStates = maxStates;
        }
    }

    /// <summary>
    /// "이 구간을 한 번도 안 부딪히고 지나가는 날갯짓 순서가 있나"를 앞으로 퍼뜨려 찾는다(10-12 광산 맵 검사).
    ///
    /// <para>상태 = (발밑 높이, 마지막 날갯짓 뒤 틱 수 k). 날갯짓은 k ≥ <c>minFlapTicks</c>일 때만 —
    /// 사람이 칠 수 있는 빠르기의 상한이다(12틱 = 초당 4번, 15틱 = 초당 3.3번). 세로 속도는 k만으로 정해진다
    /// (날갯짓이 속도를 덮어쓰고 그 뒤는 중력·종단속도뿐 — <see cref="LOP.FlappyVerticalKernel"/>, 기류 없는 맵).</para>
    ///
    /// <para>한 틱 이동과 "닿았나"는 부르는 쪽이 <paramref name="step"/>으로 준다 — 에디터는 게임의 진짜 이동 커널
    /// (<see cref="LOP.KinematicMover"/>)을 맵 콜라이더에 대고 부른다. 그래서 이 검사는 판정 모양을 따로 적지 않는다
    /// (프로토타입은 바닥·천장을 몸 가운데 한 점에서만 봐서, 누운 몸의 앞코가 가파른 천장에 먼저 닿는 걸 놓쳤다).</para>
    /// </summary>
    public static class SectionReach
    {
        /// <param name="startHeights">구간 시작 x에서 해 볼 발밑 높이들.</param>
        /// <param name="step">(x, 발밑 y, 세로 속도) → (안 닿았나, 한 틱 뒤 발밑 y).</param>
        /// <param name="keep">(한 틱 뒤 x, 발밑 y) → 이 상태를 계속 볼까(예: 고수 굴 안에서는 굴 칸만). null이면 다 본다.</param>
        /// <param name="startFlapAge">시작 상태의 k — 15면 날갯짓 뒤 꼭대기 근처(세로 속도 ≈ 0)로 시작한다.</param>
        public static SectionReachResult Run(float x0, float x1, IEnumerable<float> startHeights,
                                             float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed, float dt,
                                             int minFlapTicks, Func<float, float, float, (bool clean, float y)> step,
                                             Func<float, float, bool> keep = null, int startFlapAge = 15,
                                             float heightGrid = 0.02f)
        {
            const int MaxAge = 40;   // 종단속도에 닿고도 남는 틱 수 — 이 위는 같은 세로 속도라 하나로 묶는다.
            var cur = new Dictionary<long, (float y, int k)>();
            int k0 = Math.Min(MaxAge - 1, startFlapAge);
            foreach (float y in startHeights)
            {
                cur[Key(y, k0, heightGrid)] = (y, k0);
            }
            float x = x0, lastAlive = x0;
            int maxStates = cur.Count;
            float dx = forwardSpeed * dt;
            while (x < x1 && cur.Count > 0)
            {
                var next = new Dictionary<long, (float y, int k)>();
                foreach ((float y, int k) in cur.Values)
                {
                    for (int a = 0; a < 2; a++)
                    {
                        bool flap = a == 1;
                        if (flap && k < minFlapTicks) { continue; }
                        int k2 = flap ? 0 : Math.Min(MaxAge - 1, k + 1);
                        float vy = k2 == 0 ? flapImpulse : Math.Max(-maxFallSpeed, flapImpulse - gravity * dt * k2);
                        (bool clean, float ny) = step(x, y, vy);
                        if (clean == false) { continue; }
                        if (keep != null && keep(x + dx, ny) == false) { continue; }
                        long key = Key(ny, k2, heightGrid);
                        if (next.ContainsKey(key) == false) { next[key] = (ny, k2); }
                    }
                }
                cur = next;
                x += dx;
                if (cur.Count > 0) { lastAlive = x; }
                maxStates = Math.Max(maxStates, cur.Count);
            }
            return new SectionReachResult(cur.Count > 0, lastAlive, maxStates);
        }

        private static long Key(float y, int k, float grid) => (long)Math.Round(y / grid) * 64 + k;
    }
}
