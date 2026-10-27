using System;
using System.Threading.Tasks;

namespace LOP.Tests
{
    /// <summary>플래피 경기 진입점 시험용 가짜 — 내 id, 틱 시계, 러너.</summary>
    public sealed class RigGameDataStore : IGameDataStore
    {
        public GameInfo gameInfo { get; set; }
        public string userEntityId { get; set; }
        public void Clear() { }
    }

    public sealed class RigTickUpdater : GameFramework.Runner.ITickUpdater
    {
        public event Action<long> onTick { add { } remove { } }
        public long tick { get; set; }
        public double interval { get; set; }
        public double elapsedTime { get; set; }
        public long processibleTick => 0;
        public double deltaTime => interval;
        public int catchUpCappedCount => 0;
        public long maxTicksBehind => 0;
        public bool isFaulted => false;
        public long faultedTick => 0;
        public void Run(long tick, double interval, double elapsedTime) { }
        public void Stop() { }
    }

    public sealed class RigRunner : GameFramework.Runner.IRunner
    {
        public event Action<GameFramework.Runner.RunnerState> onGameStateChanged { add { } remove { } }
        public GameFramework.Runner.RunnerState gameState => GameFramework.Runner.RunnerState.Playing;
        public GameFramework.Runner.ITickUpdater tickUpdater { get; set; }
        public GameFramework.Netcode.INetworkTime networkTime => null;
        public bool initialized => true;
        public Task InitializeAsync() => Task.CompletedTask;
        public Task DeinitializeAsync() => Task.CompletedTask;
        public void Run(long tick, double interval, double elapsedTime) { }
        public void Stop() { }
        public void RegisterSystem<TPhase>(GameFramework.Runner.ITickSystem system) { }
        public void UnregisterSystem(GameFramework.Runner.ITickSystem system) { }
    }
}
