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
    /// 카드 러시 인게임 화면을 여닫는다(<see cref="FlappyHudCoordinator"/>와 같은 흐름). 내 몸이 생기면 조작판·정보 HUD·
    /// 카운트다운을 열고, 내가 완주하면 조작판을 닫고 "완주"와 관전 조작면을 띄운다.
    /// 매 틱 관전 대상을 새로 고른다 — 카메라(<see cref="CardRushCamera"/>)가 그 답을 읽어 따라간다(SetTarget을 안 쓴다).
    /// </summary>
    public class CardRushHudCoordinator : MessageHandlerBase, ITickable
    {
        private readonly IGameDataStore gameDataStore;
        private readonly IWindowManager windowManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly ISubscriber<EntityCreated> entityCreatedSubscriber;
        private readonly ISubscriber<MatchEndedToC> matchEndedSubscriber;
        private readonly ISpectateTargets spectate;
        private readonly AppStateMachine appStateMachine;
        private readonly MatchLeaveRequester leaveRequester;

        //  서버가 로비 위치를 비울 시간. 넘기면 그냥 나간다(Flappy와 같은 값).
        private static readonly TimeSpan LeaveReplyTimeout = TimeSpan.FromSeconds(3);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _opened;
        private bool _matchEnded;
        private bool _leaving;
        private CardRushPadView _pad;
        private RaceSpectateView _spectateView;

        public CardRushHudCoordinator(IGameDataStore gameDataStore, IWindowManager windowManager,
            GameFramework.World.EntityRegistry entityRegistry,
            ISubscriber<EntityCreated> entityCreatedSubscriber,
            ISubscriber<MatchEndedToC> matchEndedSubscriber,
            ISpectateTargets spectate,
            AppStateMachine appStateMachine,
            MatchLeaveRequester leaveRequester)
        {
            this.gameDataStore = gameDataStore;
            this.windowManager = windowManager;
            this.entityRegistry = entityRegistry;
            this.entityCreatedSubscriber = entityCreatedSubscriber;
            this.matchEndedSubscriber = matchEndedSubscriber;
            this.spectate = spectate;
            this.appStateMachine = appStateMachine;
            this.leaveRequester = leaveRequester;
        }

        protected override void Subscribe()
        {
            Track(entityCreatedSubscriber.Subscribe(OnEntityCreated));
            Track(matchEndedSubscriber.Subscribe(_ => _matchEnded = true));
        }

        /// <summary>
        /// 완주 화면으로 넘어갈 때인가. 판이 이미 끝났으면(결과 화면이 뜬다) 겹쳐 띄우지 않고,
        /// 조작판이 없으면(아직 안 열렸거나 이미 넘어갔다) 다시 하지 않는다.
        /// </summary>
        public static bool ShouldEnterFinish(bool matchEnded, bool padOpen, bool mineFinished)
            => matchEnded == false && padOpen && mineFinished;

        //  완주는 알림이 오지 않는다 — 매 틱 바뀌는 상태(FinishState)라 여기서 확인한다.
        public void Tick()
        {
            //  관전 대상 고르기는 한 곳에서만 돌린다 — 두 곳에서 부르면 결승 뒤 머무는 시간이 두 배 빨리 흐른다.
            spectate.Refresh();
            UpdateFinish();
        }

        //  내 몸의 완주는 시뮬이 안다(내 몸만 굴린다). 등수는 스냅으로 조금 늦게 오므로 화면을 먼저 띄우고
        //  숫자는 뷰가 오는 대로 채운다(Flappy와 같다).
        private void UpdateFinish()
        {
            var mine = entityRegistry.Get(gameDataStore.userEntityId);
            bool finished = mine?.Get<FinishState>()?.Finished == true;
            if (ShouldEnterFinish(_matchEnded, _pad != null, finished) == false)
            {
                return;
            }

            windowManager.Close(_pad);
            _pad = null;
            windowManager.Open<RaceFinishView>();
            OpenSpectate();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }
            //  조작면을 먼저 열어 Window 밴드 맨 아래에 깐다(전체화면 탭 면이라 위 위젯 입력을 막지 않게).
            _pad = windowManager.Open<CardRushPadView>();
            windowManager.Open<RaceHudView>();
            windowManager.Open<DebugHudView>();
            windowManager.Open<RaceStartView>();   // 출발 전엔 카드가 안 돈다 — 대기·카운트다운을 보여 준다
            _opened = true;
        }

        private void OpenSpectate()
        {
            if (_spectateView != null)
            {
                return;
            }
            _spectateView = windowManager.Open<RaceSpectateView>();
            _spectateView.SetLeaveCallback(() => AskAndLeaveAsync().Forget());
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

        public override void Dispose()
        {
            _lifetime.Cancel();
            base.Dispose();
        }
    }
}
