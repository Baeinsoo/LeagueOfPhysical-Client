using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 날아가는 화살을 그린다. <b>시뮬과 같은 식에 같은 시각을 넣으므로</b> 그림과 계산이 어긋나지 않는다.
    /// 화살은 엔티티가 아니라서 뷰가 직접 월드의 발사 목록을 읽는다.
    /// </summary>
    public class ArcheryArrowView : ILateTickable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryWorld world;

        // 목록의 자리(index)로 화살을 알아보면 안 된다 — 수명이 다한 화살이 빠지면 뒤 화살들의
        // 자리가 앞으로 당겨져서, 남아 있는 화살이 남의 궤적으로 순간이동한다.
        // 쏜 사람과 쏜 틱은 절대 안 바뀌므로 그 둘을 이름으로 쓴다.
        private readonly Dictionary<(string shooterId, long fireTick), GameObject> drawn =
            new Dictionary<(string, long), GameObject>();
        private readonly List<(string, long)> stale = new List<(string, long)>();

        public ArcheryArrowView(GameFramework.Runner.IRunner runner, ArcheryWorld world)
        {
            this.runner = runner;
            this.world = world;
        }

        public void LateTick()
        {
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            var shots = world.Shots;
            var alive = new HashSet<(string, long)>();

            for (int i = 0; i < shots.Count; i++)
            {
                var key = (shots[i].ShooterId, shots[i].FireTick);
                alive.Add(key);

                if (drawn.TryGetValue(key, out var arrow) == false || arrow == null)
                {
                    arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    arrow.transform.localScale = new Vector3(0.05f, 0.05f, 0.6f);
                    Object.Destroy(arrow.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 시뮬이 한다
                    drawn[key] = arrow;
                }

                float seconds = (float)((renderTick - shots[i].FireTick) * interval);
                if (seconds < 0f)
                {
                    seconds = 0f;   // 아직 떠나기 전 프레임 — 출발점에 둔다
                }
                arrow.transform.position = ArcheryTrajectory.PositionAt(shots[i], seconds);

                var velocity = ArcheryTrajectory.VelocityAt(shots[i], seconds);
                if (velocity.sqrMagnitude > 1e-6f)
                {
                    arrow.transform.rotation = Quaternion.LookRotation(velocity);
                }
            }

            stale.Clear();
            foreach (var pair in drawn)
            {
                if (alive.Contains(pair.Key) == false)
                {
                    stale.Add(pair.Key);
                }
            }
            foreach (var key in stale)
            {
                Object.Destroy(drawn[key]);
                drawn.Remove(key);
            }
        }
    }
}
