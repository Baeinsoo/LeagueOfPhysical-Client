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

        /// <summary>[x0, x1] 근처 장애물 중 가장 긴 주기(초). 없으면 0 — 그 구간은 출발 박자가 상관없다.</summary>
        public float LongestPeriodNear(float x0, float x1)
        {
            float longest = 0f;
            foreach (var (x, period) in periods)
            {
                if (x >= x0 - NearMargin && x <= x1 + NearMargin) { longest = Mathf.Max(longest, period); }
            }
            return longest;
        }

        /// <summary>훑어야 할 위상 수(틱) — 가장 긴 주기. 섞인 주기의 최소공배수가 아니라 가장 긴 것(풍차 위상 공간과 같은 규칙, 비용 때문).</summary>
        public int PhaseSpaceTicks(float tickSeconds)
        {
            float longest = 0f;
            foreach (var (_, period) in periods) { longest = Mathf.Max(longest, period); }
            return Mathf.Max(1, Mathf.CeilToInt(longest / tickSeconds - 1e-4f));
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
