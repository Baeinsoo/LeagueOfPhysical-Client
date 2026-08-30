using GameFramework;

namespace LOP
{
    /// <summary>
    /// 남의 새가 이번 틱에 쓸 입력을 확정한다. 서버가 되뿌린 것을
    /// <see cref="RemoteInputMessageHandler"/>가 버퍼에 넣어 두면, 여기서 틱을 맞춰 꺼낸다.
    ///
    /// <para>서버의 <c>ServerInputSystem</c>과 같은 자리지만 <b>꺼내되 버퍼에서 빼지 않는다</b>
    /// (<see cref="InputBufferSystem.Apply"/>) — 클라는 되감기 재생으로 같은 틱을 여러 번 굴리기
    /// 때문이다.</para>
    ///
    /// <para>내 새는 <see cref="PlayerInputManager"/>가 따로 확정하므로 건드리지 않는다.</para>
    /// </summary>
    public class RemoteInputSystem : GameFramework.Runner.ITickSystem
    {
        //  버퍼에 남겨 둘 과거 틱 수. 되감기 재생이 거슬러 올라가는 만큼은 있어야 한다
        //  (Reconciler.MaxReplayTicks와 같은 눈금) — 그보다 짧으면 재생이 입력 없는 구간을 만난다.
        private const int HistoryTicks = 64;

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly InputBufferSystem inputBufferSystem;

        public RemoteInputSystem(IPlayerContext playerContext,
                                 GameFramework.World.EntityRegistry entityRegistry,
                                 InputBufferSystem inputBufferSystem)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
        }

        public void Tick(long tick, float deltaTime)
        {
            ApplyAll(tick);

            foreach (var worldEntity in entityRegistry.All)
            {
                if (worldEntity.Id == playerContext.entityId)
                {
                    continue;
                }
                var buffer = worldEntity.Get<InputBuffer>();
                if (buffer != null)
                {
                    inputBufferSystem.TrimToWindow(buffer, HistoryTicks);
                }
            }
        }

        /// <summary>
        /// 남의 새 전부의 <c>Current</c>를 그 틱 값으로 맞춘다. 라이브(위 Tick)와 되감기 재생
        /// (<see cref="Reconciler"/>)이 <b>같은 함수</b>를 불러야 두 경로가 같은 답을 낸다.
        /// </summary>
        public void ApplyAll(long tick)
        {
            foreach (var worldEntity in entityRegistry.All)
            {
                if (worldEntity.Id == playerContext.entityId)
                {
                    continue;   // 내 것은 PlayerInputManager가 정한다
                }
                var buffer = worldEntity.Get<InputBuffer>();
                if (buffer == null)
                {
                    continue;
                }
                inputBufferSystem.Apply(buffer, tick);
            }
        }
    }
}
