namespace LOP
{
    /// <summary>구 LOPRunner.ProcessEvent 이동. 확정된 이벤트 버퍼를 드레인해 연출용으로 송출하고 비운다.</summary>
    public class WorldEventDrainSystem : GameFramework.ITickSystem
    {
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.IEventSink eventSink;

        public WorldEventDrainSystem(GameFramework.World.WorldEventBuffer worldEventBuffer, GameFramework.World.IEventSink eventSink)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.eventSink = eventSink;
        }

        public void Tick(long tick, float deltaTime)
        {
            // --- World Core — 슬라이스 3: 이벤트 버퍼 드레인 ---
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;

            eventSink.Emit(snapshot);
            worldEventBuffer.Clear();
            // --- end World Core slice 3 ---
        }
    }
}
