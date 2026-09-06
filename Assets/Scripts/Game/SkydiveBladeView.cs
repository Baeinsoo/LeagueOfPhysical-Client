using System;
using System.Collections.Generic;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 도는 날개를 <b>프레임마다</b> 그 순간 자세로 돌린다.
    ///
    /// <para><b>왜 필요한가</b>: 시뮬은 초당 50번 도는데 화면은 60번 이상 그려진다. 시뮬이 잡아 준
    /// 틱 자세만 쓰면 여섯 프레임 중 하나가 제자리라 계단처럼 떤다(추격자 벽이 같은 이유로
    /// 0.2m씩 계단으로 왔던 것과 같다).</para>
    ///
    /// <para><b>판정은 안 건드린다</b>: 다음 틱이 시작되면 시뮬이 <c>PoseBlades</c>로 정수 틱 자세를
    /// 다시 잡고 그 자세에서 질의한다. 그래서 여기서 프레임 자세로 덮어써도 맞는 자세는 늘
    /// "그 틱의 자세"다 — 되감기 재생도 같은 값을 본다.</para>
    ///
    /// <para>⚠️ <b>실험용(스파이크)</b> — 결과에 따라 통째로 버릴 수 있다.</para>
    /// </summary>
    public class SkydiveBladeView : ILateTickable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly BladeField bladeField;

        public SkydiveBladeView(GameFramework.Runner.IRunner runner, BladeField bladeField)
        {
            this.runner = runner;
            this.bladeField = bladeField;
        }

        public void LateTick()
        {
            IReadOnlyList<SpinningBlade> blades = bladeField.All;
            if (blades.Count == 0 || runner?.tickUpdater == null)
            {
                return;
            }

            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            //  틱 사이 어디쯤인지까지 담은 소수 틱. 판정이 쓰는 식에 이 값을 그대로 넣으므로
            //  보이는 자세와 맞는 자세가 같은 곡선 위에 있다.
            double renderTick = runner.tickUpdater.elapsedTime / interval;

            for (int i = 0; i < blades.Count; i++)
            {
                blades[i].Pose(renderTick);
            }
        }
    }
}
