using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 투석기 표적이 나오는 순간의 "뿅"(3차 spec §1). 발사 0.25초 전에 그 무리 표적의 그림(판정 없음)이 출발점에서
    /// 0부터 커지고, 그 자리에서 색종이가 터진다. 발사 틱에 진짜 표적(<see cref="ArcheryTargetView"/>)이 같은 자리·같은 각으로
    /// 이어받는다. <b>클라 화면만의 일이다</b> — 판정·목록은 건드리지 않는다(서버 판정은 발사 틱부터 그대로).
    /// 그림은 종류마다 모아 두고 다시 쓴다(무리마다 키트 메시를 새로 만들지 않는다).
    /// </summary>
    public sealed class ArcheryCatapultSpawnPopView : ILateTickable, System.IDisposable
    {
        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly ArcheryConfig config;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryFestivalKit kit;
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly ArcheryCatapultHitFxView hitFx;

        private readonly List<ArcheryTarget> volley = new List<ArcheryTarget>();
        private readonly List<GameObject> shown = new List<GameObject>();
        private readonly List<string> shownCodes = new List<string>();
        private readonly Dictionary<string, Stack<GameObject>> pool = new Dictionary<string, Stack<GameObject>>();
        private GameObject root;
        private int poppedVolley = -1;

        public ArcheryCatapultSpawnPopView(ArcheryCourse course, ArcheryWorld world, ArcheryConfig config,
                                           GameFramework.Runner.IRunner runner, ArcheryFestivalKit kit,
                                           LOP.MasterData.LOPMasterData md, ArcheryCatapultHitFxView hitFx)
        {
            this.course = course; this.world = world; this.config = config; this.runner = runner;
            this.kit = kit; this.md = md; this.hitFx = hitFx;
        }

        public void LateTick()
        {
            if (course.IsCatapult == false || runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            long start = world.GameplayStartTick;
            var s = config.Catapult;
            //  "준비" 기준 무리 — 판정·화면의 무리(LiveVolleyAt)는 발사 틱에야 넘어가서 발사 전 0.25초엔 아직 앞 무리다.
            int v = ArcheryCatapultSchedule.VolleyIndexAt(s, (long)System.Math.Floor(renderTick), start);
            if (v < 0 || v >= s.VolleyCount)
            {
                Release();
                return;
            }
            long launch = ArcheryCatapultSchedule.LaunchTick(s, v, start);
            if (ArcheryCatapultSpawnPop.IsRevealing(renderTick, launch, (float)interval) == false)
            {
                Release();
                return;
            }
            if (poppedVolley != v)
            {
                Begin(v, start, launch, (float)interval);
            }
            float grow = ArcheryCatapultSpawnPop.GrowAt(renderTick, launch, (float)interval);
            for (int i = 0; i < shown.Count; i++)
            {
                shown[i].transform.localScale = Vector3.one * grow;
            }
        }

        //  무리마다 한 번 — 그림을 출발점에 세우고 색종이를 터뜨린다. 자리·각은 발사 전 내내 같으므로 여기서 한 번만 정한다.
        private void Begin(int v, long start, long launch, float interval)
        {
            Release();
            if (root == null)
            {
                root = new GameObject("ArcheryCatapultSpawnPop");
            }
            course.Fill(volley, v, start);
            for (int i = 0; i < volley.Count; i++)
            {
                var t = volley[i];
                string code = md.Tables.TbArcheryTarget.GetOrDefault(t.KindId)?.Code;
                string key = code ?? string.Empty;
                var go = Take(key, code, t.Radius);
                //  발사 틱의 각으로 커진다 — 진짜 표적으로 바뀌는 순간 접시·종이 안 튄다.
                go.transform.SetPositionAndRotation(t.Origin, ArcheryCatapultLooks.RotationAt(t, code, launch, interval));
                go.transform.localScale = Vector3.zero;
                go.SetActive(true);
                shown.Add(go);
                shownCodes.Add(key);
                hitFx.Pop(code, t.Origin, t.Radius);
            }
            poppedVolley = v;
        }

        private GameObject Take(string key, string code, float radius)
        {
            if (pool.TryGetValue(key, out var stack) && stack.Count > 0)
            {
                return stack.Pop();
            }
            var go = kit.TryBuild(code, out var modeled) ? modeled : ArcheryCatapultLooks.BuildPlaceholder(code, radius);
            go.transform.SetParent(root.transform, true);
            return go;
        }

        //  발사 틱이 오면(또는 무리가 없으면) 그림을 숨겨 모아 둔다 — 지우지 않고 다음 무리에 다시 쓴다.
        private void Release()
        {
            for (int i = 0; i < shown.Count; i++)
            {
                shown[i].SetActive(false);
                if (pool.TryGetValue(shownCodes[i], out var stack) == false)
                {
                    stack = new Stack<GameObject>();
                    pool[shownCodes[i]] = stack;
                }
                stack.Push(shown[i]);
            }
            shown.Clear();
            shownCodes.Clear();
        }

        public void Dispose()
        {
            Release();
            foreach (var stack in pool.Values)
            {
                foreach (var go in stack) { ArcheryCatapultLooks.DestroyVisual(go); }
            }
            pool.Clear();
            if (root != null) { Object.Destroy(root); root = null; }
        }
    }
}
