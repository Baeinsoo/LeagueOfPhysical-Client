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
    /// 내 몸이 생기면 활 조작판과 디버그 HUD를 연다. 내가 탈락하면 조작판을 닫고 "탈락"을 띄운 뒤
    /// 카메라를 살아 있는 사람에게 넘긴다(플래피 FlappyHudCoordinator와 같은 흐름).
    /// </summary>
    public class MountedArcheryHudCoordinator : MessageHandlerBase, ITickable
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private readonly MountedArcheryClientState state;
        private readonly MountedSpectate spectate;
        private readonly ActorRegistry actorRegistry;
        private readonly CameraController cameraController;

        private readonly AppStateMachine appStateMachine;
        private readonly MatchLeaveRequester leaveRequester;

        //  서버가 로비 위치를 비울 시간. 넘기면 그냥 나간다(FlappyHudCoordinator와 같다).
        private static readonly TimeSpan LeaveReplyTimeout = TimeSpan.FromSeconds(3);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _leaving;
        private bool _opened;
        private bool _spectating;
        private ArcheryPadView _pad;
        private string _cameraTargetId;

        public MountedArcheryHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber, MountedArcheryClientState state,
            MountedSpectate spectate, ActorRegistry actorRegistry, CameraController cameraController,
            AppStateMachine appStateMachine, MatchLeaveRequester leaveRequester)
        {
            this.appStateMachine = appStateMachine;
            this.leaveRequester = leaveRequester;
            this.state = state;
            this.spectate = spectate;
            this.actorRegistry = actorRegistry;
            this.cameraController = cameraController;
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
        }

        protected override void Subscribe()
        {
            Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
            state.Eliminated += OnEliminated;
        }

        public override void Dispose()
        {
            _lifetime.Cancel();
            state.Eliminated -= OnEliminated;
            base.Dispose();
        }

        private void OnEliminated(MountedArcheryClientState.Elimination e)
        {
            if (_opened == false || _spectating || e.VictimId != gameDataStore.userEntityId)
            {
                return;
            }

            _spectating = true;
            if (_pad != null)
            {
                windowManager.Close(_pad);   // 지켜보는 동안엔 조작이 없다
                _pad = null;
            }
            windowManager.Open<RaceEliminatedView>();
            windowManager.Open<RaceSpectateView>().SetLeaveCallback(OnLeaveRequested);
        }

        private void OnLeaveRequested() => AskAndLeaveAsync().Forget();

        private async UniTaskVoid AskAndLeaveAsync()
        {
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

            //  서버가 로비 위치를 비운 뒤에 로비로 간다 — 먼저 가면 로비가 같은 방으로 다시 들여보낸다.
            bool released = await leaveRequester.RequestAsync(LeaveReplyTimeout, _lifetime.Token);
            if (_lifetime.IsCancellationRequested)
            {
                return;
            }
            if (released == false)
            {
                Debug.LogWarning("[LeaveMatch] 서버가 로비 위치를 비우지 못했다(거절·시간 초과) — 그래도 나간다.");
            }
            appStateMachine.Fire(AppEvent.MatchLeft);
        }

        //  보는 대상이 바뀐 틱에만 카메라를 옮긴다. SetTarget은 매번 거리·각도를 다시 잡는다.
        public void Tick()
        {
            if (_spectating == false)
            {
                return;
            }

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

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 조작면을 먼저 열어 Window 밴드 맨 아래에 깐다(전체화면이라 위 위젯 입력을 막지 않게).
            _pad = windowManager.Open<ArcheryPadView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<MountedCombatHudView>();
            windowManager.Open<RaceStartView>();   // 출발 전엔 못 몰고 못 쏜다 — 대기·카운트다운을 보여 준다
            _opened = true;
        }
    }
}
