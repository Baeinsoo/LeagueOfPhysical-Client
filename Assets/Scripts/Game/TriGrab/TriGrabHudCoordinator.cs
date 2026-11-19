using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.Event.Entity;
using LOP.UI;
using MessagePipe;
using System;
using System.Threading;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 내 몸이 생기면 삼색 그랩 조작판·HUD·카운트다운·디버그 HUD를 연다. 조작판의 [나가기]는 확인을 받고 서버가
    /// 로비 위치를 비운 뒤 로비로 간다(다른 모드 코디네이터와 같은 흐름).
    /// </summary>
    public class TriGrabHudCoordinator : MessageHandlerBase
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly AppStateMachine appStateMachine;
        private readonly MatchLeaveRequester leaveRequester;

        //  서버가 로비 위치를 비울 시간. 넘기면 그냥 나간다.
        private static readonly TimeSpan LeaveReplyTimeout = TimeSpan.FromSeconds(3);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _opened;
        private bool _leaving;

        public TriGrabHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            ISubscriber<EntityCreated> entityCreatedSubscriber, AppStateMachine appStateMachine,
            MatchLeaveRequester leaveRequester)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.appStateMachine = appStateMachine;
            this.leaveRequester = leaveRequester;
        }

        protected override void Subscribe() => Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));

        public override void Dispose()
        {
            _lifetime.Cancel();
            base.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // 조작면을 먼저 열어 Window 밴드 맨 아래에 깐다(전체화면이라 위 HUD 입력을 막지 않게 — HUD는 입력을 안 받는다).
            var pad = windowManager.Open<TriGrabPadView>();
            pad?.SetLeaveCallback(() => AskAndLeaveAsync().Forget());
            windowManager.Open<TriGrabHudView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<RaceStartView>();   // 출발 전엔 쏠 수 없다 — 대기·카운트다운을 보여 준다
            _opened = true;
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
    }
}
