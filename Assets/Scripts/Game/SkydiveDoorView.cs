using System.Collections.Generic;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 문 패널을 <b>프레임마다</b> 그 순간 자세로 옮긴다. 시뮬은 초당 50번인데 화면은 그보다
    /// 자주 그려져, 시뮬이 잡아 준 정수 틱 자세만 쓰면 몇 프레임에 한 번은 제자리라 계단처럼 떤다.
    ///
    /// <para>판정이 쓰는 <b>같은 식</b>(<see cref="DoorGeometry.Openness"/>)에 틱 사이를 담은
    /// 소수 틱을 넣을 뿐이라, 보이는 자세와 맞는 자세가 같은 곡선 위에 있다.</para>
    ///
    /// <para><b>판정은 안 건드린다</b>: 패널에 콜라이더가 있어 여기서 옮긴 트랜스폼이 곧 sweep
    /// 대상이지만, 다음 틱의 첫 줄에서 <see cref="SkydiveWorld"/>가 정수 틱 자세를 다시 세우고
    /// <c>SyncTransforms</c>까지 한 뒤에야 질의가 시작된다. 되감기 재생도 같은 경로를 지난다.</para>
    ///
    /// <para>씬에 미리 놓을 대상이 아니고 프레임마다 트랜스폼만 옮기므로 MonoBehaviour가 아니라
    /// 진입점이다(<see cref="SkydiveLaserView"/>와 같은 이유). 맵 씬에 클라 전용 컴포넌트를 붙이면
    /// 같은 씬을 읽는 서버에서 missing script가 되어 씬 주입이 끊긴다.</para>
    /// </summary>
    public class SkydiveDoorView : ILateTickable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly DoorField doorField;

        public SkydiveDoorView(GameFramework.Runner.IRunner runner, DoorField doorField)
        {
            this.runner = runner;
            this.doorField = doorField;
        }

        public void LateTick()
        {
            IReadOnlyList<DoorVolume> doors = doorField.All;
            if (doors.Count == 0 || runner?.tickUpdater == null)
            {
                return;   // 맵이 아직 안 올라왔거나 러너가 아직 안 물렸다
            }

            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;   // 아직 Run 전이라 틱 간격이 없다 — 나누면 자세가 NaN이 된다
            }

            double renderTick = runner.tickUpdater.elapsedTime / interval;
            for (int i = 0; i < doors.Count; i++)
            {
                doors[i].Pose(renderTick);
            }
        }
    }
}
