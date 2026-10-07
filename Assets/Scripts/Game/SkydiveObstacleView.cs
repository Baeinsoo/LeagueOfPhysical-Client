using System.Collections.Generic;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 도는 원판·날개·조리개를 <b>프레임마다</b> 세운다 — <see cref="SkydiveDoorView"/>와 같은 이유·같은 시각(내 캐릭터를 그리는 한 틱 뒤).
    /// 판정은 안 건드린다: 다음 틱 첫 줄에서 <see cref="SkydiveWorld"/>가 정수 틱 자세를 다시 세우고 SyncTransforms 한다.
    /// </summary>
    public class SkydiveObstacleView : ILateTickable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ObstacleField field;

        public SkydiveObstacleView(GameFramework.Runner.IRunner runner, ObstacleField field)
        {
            this.runner = runner;
            this.field = field;
        }

        public void LateTick()
        {
            IReadOnlyList<IPosedObstacle> all = field.All;
            if (all.Count == 0 || runner?.tickUpdater == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            for (int i = 0; i < all.Count; i++)
            {
                all[i].Pose(renderTick);
            }
        }
    }
}
