namespace LOP
{
    /// <summary>
    /// 날갯짓 소리(spec §6, 사용자 결정 10-25). 화면을 누른 순간 낸다 — 시뮬 결과를 기다리면 한 틱 늦고, 예측이라 어차피 바로 뜬다.
    /// 대신 시뮬이 날갯짓을 무시하는 때(몸 없음·출발 전·기절 중·결승 뒤)엔 소리도 안 낸다. 시제품 <c>flap()</c>과 같은 조건.
    /// <para>먹힌 날갯짓마다 <see cref="LiveFlap"/>을 알린다 — 연속 날갯짓(<see cref="FlappyFlapStreak"/>)이 같은 조건으로 센다.
    /// 조건을 두 곳에 두면 "소리는 안 나는데 +N이 오른다" 같은 갈림이 생긴다.</para>
    /// </summary>
    public sealed class FlappyFlapSound
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;
        private readonly MatchStartState startState;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly IFlappyAudio audio;

        /// <summary>먹힌 날갯짓(소리가 난 날갯짓)마다 한 번.</summary>
        public event System.Action LiveFlap;

        public FlappyFlapSound(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore,
                               MatchStartState startState, GameFramework.Runner.IRunner runner, IFlappyAudio audio)
        {
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
            this.startState = startState;
            this.runner = runner;
            this.audio = audio;
        }

        public void OnFlap()
        {
            if (CanFlapNow())
            {
                audio.Play(FlappySfx.Flap);
                LiveFlap?.Invoke();
            }
        }

        internal bool CanFlapNow()
        {
            //  판 시작 직후엔 내 id가 비어 있다 — 사전에 null 키를 넣으면 터진다.
            string id = gameDataStore.userEntityId;
            var mine = string.IsNullOrEmpty(id) ? null : entityRegistry.Get(id);
            if (mine == null)
            {
                return false;
            }
            if ((mine.Get<FlappyStun>()?.StunRemaining ?? 0f) > 0f)
            {
                return false;
            }
            if (mine.Get<FinishState>()?.Finished == true)
            {
                return false;
            }
            //  출발 전(카운트다운) 탭은 월드가 무시한다.
            long startTick = startState.StartTick.CurrentValue;
            var ticker = runner.tickUpdater;
            return startTick != long.MaxValue && ticker != null && ticker.tick >= startTick;
        }
    }
}
