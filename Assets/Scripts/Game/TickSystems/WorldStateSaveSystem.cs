namespace LOP
{
    /// <summary>
    /// 매 틱 끝에 월드에게 이번 틱 상태를 보관시킨다. 되돌리기(하드 복원+재생)는 Reconciler.Reconcile이
    /// 다음 틱 앞에서 수행한다. End 디스패치(=PredictedEntityInterpolator의 지연 렌더링용 틱 기록) 전에 불러,
    /// 뷰 보간이 얹히기 전 원본 예측 상태를 담는다.
    /// </summary>
    public class WorldStateSaveSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.IWorld world;

        public WorldStateSaveSystem(GameFramework.World.IWorld world)
        {
            this.world = world;
        }

        public void Tick(long tick, float deltaTime)
        {
            // 이번 틱 시뮬 결과를 보관한다. 무엇을 담을지는 월드가 안다.
            world.SaveState(tick);
        }
    }
}
