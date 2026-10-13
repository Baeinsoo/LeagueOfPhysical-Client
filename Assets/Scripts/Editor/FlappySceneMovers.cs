using System.Collections.Generic;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 검사기용 — 열린 맵 씬의 진자·승강기·낙석을 모아, 게임이 매 틱 하는 것(FlappyWorld.Mutation 맨 앞)과 똑같이 그 틱 자세로 세운다.
    /// 게임은 맵 로드 때 마커의 [Inject]가 필드를 채우지만 검사기는 DI 없이 도니 씬에서 직접 긁는다.
    /// <b>끝나면 <see cref="Restore"/></b> — 맵 씬은 구운 자세로 커밋되므로 검사가 자세를 남기면 diff로 샌다.
    /// </summary>
    internal sealed class FlappySceneMovers
    {
        //  구간 근처 장애물을 찾을 때 구간 양끝에서 더 보는 거리 — 진자 광차가 축에서 앞뒤 ±2.5 m 흔들린다.
        private const float NearMargin = 8f;

        private readonly LOP.FlappyPendulumField pendulums = new LOP.FlappyPendulumField();
        private readonly LOP.FlappyLiftField lifts = new LOP.FlappyLiftField();
        private readonly LOP.FlappyRockfallField rockfalls = new LOP.FlappyRockfallField();
        private readonly List<(Transform t, Vector3 pos, Quaternion rot)> rest = new List<(Transform, Vector3, Quaternion)>();
        private readonly List<(float x, float period)> periods = new List<(float, float)>();
        private long posedTick = long.MinValue;

        public int Count => pendulums.Count + lifts.Count + rockfalls.Count;
        public int Lifts => lifts.Count;
        public int Pendulums => pendulums.Count;
        public int Rockfalls => rockfalls.Count;

        public static FlappySceneMovers Collect()
        {
            var m = new FlappySceneMovers();
            foreach (var p in Object.FindObjectsByType<LOP.FlappyPendulum>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                m.pendulums.Add(p);
                m.Remember(p.transform, p.transform.position.x, p.Period);
                //  필드는 매단 수레(Upright)도 반대로 돌린다 — 이것도 안 돌려놓으면 구운 자세가 샌다.
                if (p.Upright != null) { m.rest.Add((p.Upright, p.Upright.localPosition, p.Upright.localRotation)); }
            }
            foreach (var l in Object.FindObjectsByType<LOP.FlappyLift>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.Cage == null) { continue; }
                m.lifts.Add(l);
                m.Remember(l.Cage, l.transform.position.x, l.Period);
            }
            foreach (var r in Object.FindObjectsByType<LOP.FlappyRockfall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r.Rock == null) { continue; }
                m.rockfalls.Add(r);
                m.Remember(r.Rock, r.transform.position.x, r.Period);
            }
            return m;
        }

        private void Remember(Transform t, float x, float period)
        {
            rest.Add((t, t.localPosition, t.localRotation));
            periods.Add((x, period));
        }

        /// <summary>이 틱 자세로 세운다. 같은 틱을 다시 세우면 바로 돌아온다(필드가 SyncTransforms까지 부르므로 공짜가 아니다).</summary>
        public void PoseForTick(long tick, float tickSeconds)
        {
            if (posedTick == tick) { return; }
            pendulums.PoseForTick(tick, tickSeconds);
            lifts.PoseForTick(tick, tickSeconds);
            rockfalls.PoseForTick(tick, tickSeconds);
            posedTick = tick;
        }

        /// <summary>
        /// [x0, x1] 근처 장애물이 다 같이 처음 자세로 돌아오는 틱 수 — 주기마다 틱으로 반올림한 값의 최소공배수.
        /// 출발 박자를 이만큼 훑어야 장애물끼리의 위상 조합을 다 본다(가장 긴 주기만 보면 계단 3초 + 낙석 2초처럼
        /// 섞인 구간은 6초 중 절반만 본다). 근처에 없으면 0 — 그 구간은 출발 박자가 상관없다.
        /// </summary>
        public int CycleTicksNear(float x0, float x1, float tickSeconds)
        {
            long cycle = 0;
            foreach (var (x, period) in periods)
            {
                if (x < x0 - NearMargin || x > x1 + NearMargin) { continue; }
                long ticks = TicksOf(period, tickSeconds);
                cycle = cycle == 0 ? ticks : cycle / Gcd(cycle, ticks) * ticks;
            }
            return (int)System.Math.Min(int.MaxValue, cycle);
        }

        /// <summary>맵 전체에서 훑어야 할 위상 수(틱) — 가장 긴 주기. 섞인 주기의 최소공배수가 아니라 가장 긴 것(풍차 위상 공간과 같은 규칙,
        /// 비용 때문 — 맵 전체 최소공배수는 수십 초다). 구간 검사는 구간 근처만 보므로 <see cref="CycleTicksNear"/>로 최소공배수를 쓴다.</summary>
        public int PhaseSpaceTicks(float tickSeconds)
        {
            int longest = 1;
            foreach (var (_, period) in periods) { longest = Mathf.Max(longest, TicksOf(period, tickSeconds)); }
            return longest;
        }

        //  주기(초) → 틱. 틱 정수배가 아닌 주기는 가장 가까운 틱으로 — 위상 훑기의 단위가 틱이다.
        private static int TicksOf(float period, float tickSeconds) => Mathf.Max(1, Mathf.RoundToInt(period / tickSeconds));

        private static long Gcd(long a, long b)
        {
            while (b != 0) { (a, b) = (b, a % b); }
            return a;
        }

        /// <summary>구운 자세에서 벗어난 Transform 수 — <see cref="Restore"/> 뒤 0이어야 한다.
        /// 스크립트로 바꾼 자세는 씬을 더티로 만들지 않아 isDirty로는 새는 걸 못 본다.</summary>
        public int PoseMismatches()
        {
            int n = 0;
            foreach (var (t, pos, rot) in rest)
            {
                if (t == null) { continue; }
                if ((t.localPosition - pos).sqrMagnitude > 1e-10f || Quaternion.Angle(t.localRotation, rot) > 1e-3f) { n++; }
            }
            return n;
        }

        public void Restore()
        {
            foreach (var (t, pos, rot) in rest)
            {
                if (t == null) { continue; }
                t.localPosition = pos;
                t.localRotation = rot;
            }
            Physics.SyncTransforms();
            posedTick = long.MinValue;
        }
    }
}
