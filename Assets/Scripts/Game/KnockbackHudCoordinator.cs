using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;
using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 내 몸이 생기면 Knockback 조작 패드와 디버그 HUD를 연다. 내가 떨어져 탈락하면 조작판을 닫고 "탈락"을 띄운 뒤
    /// 카메라를 살아 있는 사람에게 넘긴다(말 활쏘기 MountedArcheryHudCoordinator와 같은 흐름).
    /// </summary>
    public class KnockbackHudCoordinator : MessageHandlerBase, ITickable
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;

        private readonly KnockbackClientState state;
        private readonly KnockbackSpectate spectate;
        private readonly ActorRegistry actorRegistry;
        private readonly CameraController cameraController;
        private readonly AppStateMachine appStateMachine;
        private readonly MatchLeaveRequester leaveRequester;

        //  서버가 로비 위치를 비울 시간. 넘기면 그냥 나간다.
        private static readonly TimeSpan LeaveReplyTimeout = TimeSpan.FromSeconds(3);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _opened;
        private bool _spectating;
        private bool _leaving;
        private KnockbackPadView _pad;
        private string _cameraTargetId;

        public KnockbackHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber, KnockbackClientState state,
            KnockbackSpectate spectate, ActorRegistry actorRegistry, CameraController cameraController,
            AppStateMachine appStateMachine, MatchLeaveRequester leaveRequester)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.state = state;
            this.spectate = spectate;
            this.actorRegistry = actorRegistry;
            this.cameraController = cameraController;
            this.appStateMachine = appStateMachine;
            this.leaveRequester = leaveRequester;
        }

        protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));

        public override void Dispose()
        {
            _lifetime.Cancel();
            base.Dispose();
        }

        /// <summary>몸이 한 번도 안 생겼는데 관전부터 열어야 하는가(탈락 뒤 재접속). 말 활쏘기와 같은 판단이다.</summary>
        public static bool ShouldSpectateWithoutBody(bool opened, bool spectating, bool gameInfoReceived,
                                                     bool stateReceived, string userEntityId, bool meEliminated)
            => MountedArcheryHudCoordinator.ShouldSpectateWithoutBody(
                opened, spectating, gameInfoReceived, stateReceived, userEntityId, meEliminated);

        //  탈락은 이벤트가 없어 상태를 매 틱 살핀다. 보는 대상이 바뀐 틱에만 카메라를 옮긴다(SetTarget은 매번 거리·각도를 다시 잡는다).
        public void Tick()
        {
            string mine = gameDataStore.userEntityId;
            bool meEliminated = state.IsEliminated(mine);

            if (_spectating == false
                && ((_opened && meEliminated)
                    || ShouldSpectateWithoutBody(_opened, _spectating, gameDataStore.gameInfo != null,
                                                 state.Version > 0, mine, meEliminated)))
            {
                EnterSpectate();
            }

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

        private void EnterSpectate()
        {
            _spectating = true;
            if (_pad != null)
            {
                windowManager.Close(_pad);   // 지켜보는 동안엔 조작이 없다
                _pad = null;
            }
            if (_opened == false)
            {
                windowManager.Open<KnockbackHudView>();   // 몸 없이 재접속한 관전자도 남은 사람 수·원 안내를 본다
            }
            windowManager.Open<RaceEliminatedView>();
            windowManager.Open<RaceSpectateView>().SetLeaveCallback(() => AskAndLeaveAsync().Forget());
        }

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

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 조작면을 먼저 열어 Window 밴드 맨 아래에 깐다(전체화면이라 위 위젯 입력을 막지 않게).
            _pad = windowManager.Open<KnockbackPadView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<KnockbackHudView>();
            windowManager.Open<RaceStartView>();   // 출발 전엔 못 움직이고 못 쏜다 — 대기·카운트다운을 보여 준다
            _opened = true;
        }
    }
}
