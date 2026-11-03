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
    /// 카운트다운을 열고, 서버가 내 완주를 확정하면(등수가 오면) 조작판을 닫고 "완주"와 관전 조작면을 띄운다.
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

        //  예측한 완주로는 닫지 않는다(최종 리뷰 I2) — 마지막 탭이 서버에 늦게 닿아 버려지면 보정이 완주를 되돌리는데,
        //  그때 조작판이 없으면 그 판에서 다시 누를 길이 없다. 서버 등수(FinishPlacement &gt; 0)가 와야 닫는다.
        //  카드 러시는 마지막 입력이 정확히 탭 하나라 Flappy보다 이 경로가 잦다. 대가는 완주 화면이 스냅 지연만큼 늦는 것.
        private void UpdateFinish()
        {
            //  GameInfo(내 몸 id)가 오기 전에도 틱은 돈다 — null 키로 찾으면 레지스트리가 예외를 던진다.
            if (string.IsNullOrEmpty(gameDataStore.userEntityId))
            {
                return;
            }
            var mine = entityRegistry.Get(gameDataStore.userEntityId);
            bool finished = mine?.Get<FinishState>()?.Finished == true
                && (mine.Get<FinishPlacement>()?.Value ?? 0) > 0;
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
