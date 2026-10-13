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
        //  장애물마다 콜라이더 묶음 — 맵 검사기의 정적 훑기가 이것을 빼고 재고(풍차를 빼는 것과 같은 규칙),
        //  관문이 움직이는 장애물에 걸치는지를 x 범위로 가른다.
        private readonly List<Collider[]> groups = new List<Collider[]>();
        private readonly HashSet<Collider> colliders = new HashSet<Collider>();
        private readonly List<(float min, float max)> xExtents = new List<(float, float)>();

        /// <summary>장애물 콜라이더 전부(진자 팔·매단 수레, 승강기 우리, 낙석 바위).</summary>
        public IReadOnlyCollection<Collider> Colliders => colliders;

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
                m.AddGroup(p.transform, p.Upright);
            }
            foreach (var l in Object.FindObjectsByType<LOP.FlappyLift>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.Cage == null) { continue; }
                m.lifts.Add(l);
                m.Remember(l.Cage, l.transform.position.x, l.Period);
                m.AddGroup(l.Cage, null);
            }
            foreach (var r in Object.FindObjectsByType<LOP.FlappyRockfall>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (r.Rock == null) { continue; }
                m.rockfalls.Add(r);
                m.Remember(r.Rock, r.transform.position.x, r.Period);
                m.AddGroup(r.Rock, null);
            }
            return m;
        }

        private void AddGroup(Transform a, Transform b)
        {
            var set = new HashSet<Collider>(a.GetComponentsInChildren<Collider>(includeInactive: true));
            //  매단 수레가 팔 밑에 없을 수도 있으니 따로 긁는다(겹치면 집합이 거른다).
            if (b != null) { set.UnionWith(b.GetComponentsInChildren<Collider>(includeInactive: true)); }
            var arr = new Collider[set.Count];
            set.CopyTo(arr);
            groups.Add(arr);
            colliders.UnionWith(arr);
        }

        /// <summary>
        /// 장애물마다 한 주기 동안 콜라이더가 쓸고 지나가는 x 범위를 잰다(<see cref="ExtentPoseStride"/>틱마다 세워 bounds를 합친다).
        /// 구운 자세 하나의 bounds만 보면 ±2.5 m 흔들리는 진자 광차가 걸친 관문을 놓친다. 끝나면 구운 자세로 되돌린다 —
        /// 부르는 쪽이 자세 캐시(마지막으로 세운 틱)를 들고 있으면 이 뒤에 비워야 한다.
        /// </summary>
        public void MeasureXExtents(float tickSeconds)
        {
            xExtents.Clear();
            var mins = new float[groups.Count];
            var maxs = new float[groups.Count];
            for (int g = 0; g < groups.Count; g++) { mins[g] = float.MaxValue; maxs[g] = float.MinValue; }
            int space = PhaseSpaceTicks(tickSeconds);
            for (int tick = 0; tick <= space; tick += ExtentPoseStride)
            {
                PoseForTick(tick, tickSeconds);
                for (int g = 0; g < groups.Count; g++)
                {
                    foreach (var c in groups[g])
                    {
                        if (c == null) { continue; }
                        Bounds bounds = c.bounds;
                        mins[g] = Mathf.Min(mins[g], bounds.min.x);
                        maxs[g] = Mathf.Max(maxs[g], bounds.max.x);
                    }
                }
            }
            for (int g = 0; g < groups.Count; g++)
            {
                if (mins[g] > maxs[g]) { continue; }
                //  틱 사이에 지나친 끝자락 몫 — 0.1초 사이 광차가 가는 거리보다 넉넉하다.
                xExtents.Add((mins[g] - ExtentPad, maxs[g] + ExtentPad));
            }
            Restore();
        }

        private const int ExtentPoseStride = 5;
        private const float ExtentPad = 0.5f;

        /// <summary>[startX, endX]에 움직이는 장애물이 한 주기 중 한 번이라도 걸치나(<see cref="MeasureXExtents"/> 뒤에만 의미가 있다).</summary>
        public bool OverlapsX(float startX, float endX)
        {
            foreach (var (min, max) in xExtents)
            {
                if (max >= startX && min <= endX) { return true; }
            }
            return false;
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
