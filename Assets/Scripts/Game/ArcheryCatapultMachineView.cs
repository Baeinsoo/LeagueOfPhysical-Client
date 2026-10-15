using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 투석기 둘을 세우고 무리마다 팔을 휘두른다. "준비" 0.7초 동안 그 무리 표적의 그림(판정 없음)이 바구니에서
    /// 출발점으로 날아 들어와, 발사 틱에 진짜 표적(<see cref="ArcheryTargetView"/>)과 같은 자리에서 바뀐다.
    /// </summary>
    public sealed class ArcheryCatapultMachineView : ILateTickable, System.IDisposable
    {
        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly ArcheryConfig config;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryFestivalKit kit;
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly List<ArcheryTarget> volley = new List<ArcheryTarget>();
        private readonly List<GameObject> lobs = new List<GameObject>();
        //  틀과 팔은 형제로 둔다 — 회색 틀을 지울 때 그 아래 키트 팔의 에셋 재질까지 지우지 않게.
        private readonly List<GameObject> parts = new List<GameObject>();
        private Transform[] arms;
        private Vector3[] buckets;
        private GameObject root;
        private int lobVolley = -1;

        public ArcheryCatapultMachineView(ArcheryCourse course, ArcheryWorld world, ArcheryConfig config,
                                          GameFramework.Runner.IRunner runner, ArcheryFestivalKit kit,
                                          LOP.MasterData.LOPMasterData md)
        {
            this.course = course; this.world = world; this.config = config; this.runner = runner; this.kit = kit; this.md = md;
        }

        public void LateTick()
        {
            if (course.IsCatapult == false || runner?.tickUpdater == null || runner.tickUpdater.interval <= 0d)
            {
                return;
            }
            if (root == null && TryBuild() == false)
            {
                return;   // 맵 씬이 아직 안 떴다
            }
            double interval = runner.tickUpdater.interval;
            long tick = (long)System.Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            long start = world.GameplayStartTick;
            var s = config.Catapult;
            int v = ArcheryCatapultSchedule.VolleyIndexAt(s, tick, start);
            if (v < 0 || v >= s.VolleyCount)
            {
                ClearLobs();
                return;
            }
            long cue = ArcheryCatapultSchedule.CueTick(s, v, start);
            long launch = ArcheryCatapultSchedule.LaunchTick(s, v, start);
            float angle = ArcheryCatapultMachines.ArmAngle(tick, cue, launch);
            //  팔은 뒤(−z)로 누운 자세가 0도 — 로컬 x축 양의 회전이 바구니를 위로, 던지는 쪽(+z)으로 넘긴다.
            foreach (var arm in arms) { arm.localRotation = Quaternion.Euler(angle, 0f, 0f); }

            if (tick >= launch) { ClearLobs(); return; }
            if (lobVolley != v) { BuildLobs(v, start); }
            var lane = course.SharedLane.Value;
            Vector3 right = ArcheryTargetMotion.ShooterRightAxis(-lane.Forward);
            float k = ArcheryCatapultMachines.LobProgress(tick, cue, launch);
            for (int i = 0; i < lobs.Count; i++)
            {
                lobs[i].SetActive(k >= 0f);   // 놓기 전엔 바구니 안 — 안 보인다
                if (k < 0f) { continue; }
                float lateral = Vector3.Dot(volley[i].Origin - lane.ShooterPosition, right);
                int side = ArcheryCatapultMachines.SideOf(lateral);
                lobs[i].transform.position = ArcheryCatapultMachines.Lob(buckets[side < 0 ? 0 : 1], volley[i].Origin, k);
            }
        }

        private bool TryBuild()
        {
            var lane = course.SharedLane;
            if (lane.HasValue == false) { return false; }
            root = new GameObject("ArcheryCatapults");
            Vector3 right = ArcheryTargetMotion.ShooterRightAxis(-lane.Value.Forward);
            arms = new Transform[2];
            buckets = new Vector3[2];
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : +1;
                Vector3 at = ArcheryCatapultMachines.Position(side, lane.Value.ShooterPosition, lane.Value.Forward, right);
                //  던지는 쪽(+z)이 사대 한가운데 위쪽을 본다.
                var face = Quaternion.LookRotation(-right * side);
                var frame = kit.catapultFrame != null
                    ? kit.Make("catapult-frame", kit.catapultFrame, kit.catapultFrameMaterials)
                    : ArcheryCatapultLooks.BuildPlaceholder("CatapultFrame", 1.5f);
                frame.transform.SetParent(root.transform, false);
                frame.transform.SetPositionAndRotation(at, face);
                parts.Add(frame);

                var pivot = new GameObject("arm-pivot").transform;
                pivot.SetParent(root.transform, false);
                pivot.SetPositionAndRotation(frame.transform.TransformPoint(new Vector3(0f, 1.2f, -0.2f)), face);
                var holder = new GameObject("arm-swing").transform;   // 이 축이 돈다(바깥 축은 투석기 방향)
                holder.SetParent(pivot, false);
                var arm = kit.catapultArm != null
                    ? kit.Make("catapult-arm", kit.catapultArm, kit.catapultArmMaterials)
                    : ArcheryCatapultLooks.BuildPlaceholder("CatapultArm", 1.3f);
                arm.transform.SetParent(holder, false);
                parts.Add(arm);
                arms[i] = holder;
                //  놓는 순간(다 휘두른 자세)의 바구니에서 출발한다.
                buckets[i] = pivot.TransformPoint(Quaternion.Euler(ArcheryCatapultMachines.ArmFiredDeg, 0f, 0f) * ArcheryCatapultMachines.BucketLocal);
            }
            return true;
        }

        private void BuildLobs(int v, long start)
        {
            ClearLobs();
            course.Fill(volley, v, start);
            foreach (var t in volley)
            {
                string code = md.Tables.TbArcheryTarget.GetOrDefault(t.KindId)?.Code;
                GameObject go = kit.TryBuild(code, out var modeled) ? modeled : ArcheryCatapultLooks.BuildPlaceholder(code, t.Radius);
                go.transform.rotation = Quaternion.LookRotation(t.Facing == Vector3.zero ? Vector3.back : t.Facing);
                lobs.Add(go);
            }
            lobVolley = v;
        }

        private void ClearLobs()
        {
            foreach (var go in lobs) { ArcheryCatapultLooks.DestroyVisual(go); }
            lobs.Clear();
            lobVolley = -1;
        }

        public void Dispose()
        {
            ClearLobs();
            foreach (var go in parts) { ArcheryCatapultLooks.DestroyVisual(go); }
            parts.Clear();
            if (root != null) { Object.Destroy(root); root = null; }
        }
    }
}
