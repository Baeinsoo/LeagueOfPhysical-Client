using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// Flappy 인게임 화면을 여닫는다. 내 새가 생기면 입력면을 열고, 추격자에게 잡히면 입력면을 닫고
    /// "탈락"을 띄운 뒤 카메라를 남은 사람에게 넘긴다.
    ///
    /// <para>화면 교체는 "큰 흐름"이라 코디네이터 책임이다(아키텍처 가이드라인 "흐름의 경계").
    /// 카메라 타깃도 같은 흐름이라 여기서 함께 다룬다 — 입력면 인스턴스를 이 클래스가 들고 있어야
    /// 닫을 수 있는 것도 이유다.</para>
    /// </summary>
    public class FlappyHudCoordinator : MessageHandlerBase, ITickable
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ActorRegistry actorRegistry;
        private readonly CameraController cameraController;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        private readonly ISubscriber<MatchEndedToC> matchEndedSubscriber;

        private bool _opened;
        private bool _matchEnded;
        private FlapPadView _flapPad;
        private string _cameraTargetId;

        public FlappyHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            GameFramework.World.EntityRegistry entityRegistry,
            ActorRegistry actorRegistry,
            CameraController cameraController,
            ISubscriber<EntityCreated> entityCreatedSubscriber,
            ISubscriber<EntityDestroyed> entityDestroyedSubscriber,
            ISubscriber<MatchEndedToC> matchEndedSubscriber)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.cameraController = cameraController;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.entityDestroyedSubscriber = entityDestroyedSubscriber;
            this.matchEndedSubscriber = matchEndedSubscriber;
        }

        protected override void Subscribe()
        {
            Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
            Track(entityDestroyedSubscriber.Subscribe(OnEntityDestroyed));
            Track(matchEndedSubscriber.Subscribe(_ => _matchEnded = true));
        }

        //  완주는 알림이 오지 않는다 — 매 틱 바뀌는 상태(FinishState)라 여기서 확인한다.
        public void Tick()
        {
            UpdateFinish();
        }

        //  내 새가 결승선을 넘었는지는 시뮬이 안다. 등수는 서버가 정해 스냅샷으로 오는데
        //  통과보다 0.2초쯤 늦으므로, 화면을 먼저 띄우고 숫자는 뷰가 오는 대로 채운다.
        private void UpdateFinish()
        {
            if (_matchEnded || _flapPad == null)
            {
                return;
            }

            var mine = entityRegistry.Get(gameDataStore.userEntityId);
            if (mine?.Get<FinishState>()?.Finished != true)
            {
                return;
            }

            windowManager.Close(_flapPad);   // 대시 버튼도 함께 사라진다
            _flapPad = null;
            windowManager.Open<RaceFinishView>();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 입력면을 먼저 열어 Window 밴드 최하단에 깐다(전체화면이라 위 위젯 입력을 막지 않도록).
            _flapPad = windowManager.Open<FlapPadView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<RaceStartView>();
            _opened = true;
            _cameraTargetId = gameDataStore.userEntityId;
        }

        private void OnEntityDestroyed(EntityDestroyed entityDestroyed)
        {
            //  판이 끝나면 방을 정리하면서 엔티티도 사라진다. 그것까지 탈락으로 읽으면
            //  결과 화면 위에 "탈락"이 겹친다.
            if (_opened == false || _matchEnded)
            {
                return;
            }

            if (entityDestroyed.entityId == gameDataStore.userEntityId)
            {
                if (_flapPad != null)
                {
                    windowManager.Close(_flapPad);   // 대시 버튼도 함께 사라진다
                    _flapPad = null;
                }
                windowManager.Open<RaceEliminatedView>();
            }

            FollowNextRunner();
        }

        //  보고 있던 새가 사라졌으면 다음 사람에게 넘긴다. 규칙은 벽을 그리는 쪽과 같은 것을 쓴다
        //  — 둘이 다른 새를 고르면 벽이 화면 속 새와 다른 시각으로 그려진다.
        private void FollowNextRunner()
        {
            string next = FlappyWatchTarget.Resolve(entityRegistry, gameDataStore.userEntityId);
            if (next == null || next == _cameraTargetId)
            {
                return;
            }

            var visual = actorRegistry.Get(next)?.visualGameObject;
            if (visual == null)
            {
                return;   // 아직 몸이 안 붙었다 — 다음 소멸 때 다시 본다
            }

            _cameraTargetId = next;
            cameraController.SetTarget(visual.transform);
        }
    }
}
