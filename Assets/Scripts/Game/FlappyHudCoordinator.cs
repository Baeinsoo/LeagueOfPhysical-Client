using Cysharp.Threading.Tasks;
using GameFramework;
using System;
using System.Threading;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;
using UnityEngine;
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
        private readonly FlappySpectate spectate;
        private readonly AppStateMachine appStateMachine;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        private readonly ISubscriber<MatchEndedToC> matchEndedSubscriber;
        private readonly MatchLeaveRequester leaveRequester;

        //  서버가 로비 위치를 비울 시간. 넘기면 그냥 나간다(같은 방으로 다시 들어갈 수 있다 — 나가기 전과 같은 결과).
        private static readonly TimeSpan LeaveReplyTimeout = TimeSpan.FromSeconds(3);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _opened;
        private bool _matchEnded;
        private bool _leaving;
        private FlapPadView _flapPad;
        private RaceSpectateView _spectateView;
        private string _cameraTargetId;

        public FlappyHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            GameFramework.World.EntityRegistry entityRegistry,
            ActorRegistry actorRegistry,
            CameraController cameraController,
            FlappySpectate spectate,
            AppStateMachine appStateMachine,
            ISubscriber<EntityCreated> entityCreatedSubscriber,
            ISubscriber<EntityDestroyed> entityDestroyedSubscriber,
            ISubscriber<MatchEndedToC> matchEndedSubscriber,
            MatchLeaveRequester leaveRequester)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityRegistry = entityRegistry;
            this.actorRegistry = actorRegistry;
            this.cameraController = cameraController;
            this.spectate = spectate;
            this.appStateMachine = appStateMachine;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.entityDestroyedSubscriber = entityDestroyedSubscriber;
            this.matchEndedSubscriber = matchEndedSubscriber;
            this.leaveRequester = leaveRequester;
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
            UpdateCamera();
        }

        //  보는 대상이 바뀌었을 때만 카메라를 옮긴다. SetTarget은 현재 카메라 위치로부터
        //  거리·각도를 다시 잡으므로 매 틱 부르면 조작감이 망가진다.
        private void UpdateCamera()
        {
            spectate.Refresh();

            if (spectate.Current == null || spectate.Current == _cameraTargetId)
            {
                return;
            }

            var visual = actorRegistry.Get(spectate.Current)?.visualGameObject;
            if (visual == null)
            {
                return;   // 아직 몸이 안 붙었다 — 다음 틱에 다시 본다
            }

            _cameraTargetId = spectate.Current;
            cameraController.SetTarget(visual.transform);
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
            OpenSpectate();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 입력면을 먼저 열어 Window 밴드 최하단에 깐다(전체화면이라 위 위젯 입력을 막지 않도록).
            _flapPad = windowManager.Open<FlapPadView>();
            //  정보 HUD는 입력면 바로 위 — 입력을 안 잡으므로 엄지를 막지 않는다. 완주·탈락 뒤에도 닫지 않는다
            //  (관전 중에도 막대가 남은 새들을 보여 준다. 내 새가 없으면 순위 판만 숨는다).
            windowManager.Open<RaceHudView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<RaceStartView>();
            _opened = true;
            //  카메라는 UpdateCamera가 매 틱 스스로 잡는다. 여기서 기록만 해 두면, 그 전에
            //  카메라가 남에게 가 있었을 때 "이미 나를 본다"고 거짓 기록이 남아 영영 안 돌아온다.
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
                OpenSpectate();
            }
        }

        //  완주했든 탈락했든 같은 조작면을 연다. 두 번 열리지 않게 자기 인스턴스를 본다.
        private void OpenSpectate()
        {
            if (_spectateView != null)
            {
                return;
            }

            _spectateView = windowManager.Open<RaceSpectateView>();
            _spectateView.SetLeaveCallback(OnLeaveRequested);
        }

        //  나가기는 화면 교체(큰 흐름)라 View가 아니라 여기서 처리한다.
        private void OnLeaveRequested() => AskAndLeaveAsync().Forget();

        private async UniTaskVoid AskAndLeaveAsync()
        {
            //  확인 창이 떠 있거나 서버 답을 기다리는 동안 [나가기]를 또 누르면 무시한다.
            if (_leaving)
            {
                return;
            }
            _leaving = true;

            bool leave = await windowManager.OpenModalAsync<LeaveMatchConfirmView, bool>();
            if (leave == false)
            {
                _leaving = false;
                return;
            }

            //  서버가 로비 위치를 비운 뒤에 로비로 간다 — 먼저 가면 로비가 아직 "게임 방"인 위치를 보고
            //  같은 방으로 다시 들여보낸다. 등수는 서버가 그대로 들고 있다(완주·탈락 기록).
            bool released = await leaveRequester.RequestAsync(LeaveReplyTimeout, _lifetime.Token);
            if (_lifetime.IsCancellationRequested)
            {
                return;   // 기다리는 사이 판이 끝나 씬이 내려갔다 — 화면 흐름은 이미 다른 쪽이 맡았다.
            }
            if (released == false)
            {
                Debug.LogWarning("[LeaveMatch] 서버가 로비 위치를 비우지 못했다(거절·시간 초과) — 그래도 나간다.");
            }
            appStateMachine.Fire(AppEvent.MatchLeft);
        }

        public override void Dispose()
        {
            _lifetime.Cancel();
            base.Dispose();
        }
    }
}
