using Cysharp.Threading.Tasks;
using LOP.UI;
using System;
using System.Threading;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 경기 중 내 방 연결이 끊긴 것을 알아채고 재접속을 맡긴다.
    /// 일부러 끊는 쪽은 모두 클라다 — 결과를 받았거나 앱 흐름이 이미 이 경기를 떠났으면 무시한다.
    /// </summary>
    public sealed class MatchConnectionWatcher : IStartable, IDisposable
    {
        public const string MatchEndedNotice = "경기가 끝났습니다";
        public const double ResumeReconnectSeconds = 10;   //  서버도 이만큼이면 나를 끊었다(kcp 10초)

        public sealed class Deps
        {
            public Func<bool> stillThisMatch;
            public Func<bool> resultArrived;
            public Func<CancellationToken, UniTask<ReconnectOutcome>> runReconnect;
            public Action showOverlay;
            public Action hideOverlay;
            public Action<AppEvent> fire;
            public Action<string> postLobbyNotice;
            public Action stopClient;
            public Func<double> now;
            public Action beforeRejoin;
            public Action resetSession;
            public Action<Action> subscribeStopClient;
            public Action<Action> unsubscribeStopClient;
        }

        private readonly Deps deps;
        private readonly CancellationTokenSource cts = new();
        private bool running;
        private bool overlayShown;
        private double pausedAt = -1;

        public MatchConnectionWatcher(Deps deps)
        {
            this.deps = deps;
        }

        [Inject]
        public MatchConnectionWatcher(
            LOPNetworkManager networkManager,
            AppStateMachine appStateMachine,
            IMatchResultDataStore results,
            IWindowManager windowManager,
            IUserDataStore users,
            IRoomDataStore rooms,
            LobbyNotice lobbyNotice,
            MatchLoadingViewModel matchLoading,
            ReconnectSession session)
            : this(Build(networkManager, appStateMachine, results, windowManager, users, rooms, lobbyNotice, matchLoading, session))
        {
        }

        private static Deps Build(
            LOPNetworkManager networkManager, AppStateMachine appStateMachine, IMatchResultDataStore results,
            IWindowManager windowManager, IUserDataStore users, IRoomDataStore rooms, LobbyNotice lobbyNotice,
            MatchLoadingViewModel matchLoading, ReconnectSession session)
        {
            //  이 경기 씬을 연 상태 — 앱 흐름이 다른 상태로 넘어갔으면 씬이 내려가는 중이다.
            var myState = appStateMachine.currentState;
            string roomId = rooms.room?.id;
            ReconnectingOverlayView overlay = null;
            var random = new System.Random();

            return new Deps
            {
                stillThisMatch = () => ReferenceEquals(appStateMachine.currentState, myState),
                resultArrived = () => results.result != null,
                runReconnect = ct => new MatchReconnector(
                    c => RejoinProbe.AskLobbyAsync(users.user?.id, roomId, c),
                    (s, c) => UniTask.Delay(TimeSpan.FromSeconds(s), DelayType.Realtime, cancellationToken: c),
                    (s, c) => UniTask.Delay(TimeSpan.FromSeconds(s), DelayType.Realtime, cancellationToken: c),
                    () => Time.realtimeSinceStartupAsDouble,
                    () => random.NextDouble(),
                    c => windowManager.OpenModalAsync<ReconnectFailedView, bool>().AttachExternalCancellation(c),
                    () => results.result != null,
                    session).RunAsync(ct),
                showOverlay = () => overlay ??= windowManager.Open<ReconnectingOverlayView>(),
                hideOverlay = () =>
                {
                    if (overlay == null) return;
                    windowManager.Close(overlay);
                    overlay = null;
                },
                fire = appStateMachine.Fire,
                postLobbyNotice = lobbyNotice.Post,
                stopClient = networkManager.StopClient,
                now = () => Time.realtimeSinceStartupAsDouble,
                beforeRejoin = matchLoading.NotifyGameNotLive,
                resetSession = session.Reset,
                subscribeStopClient = h => networkManager.onStopClient += h,
                unsubscribeStopClient = h => networkManager.onStopClient -= h,
            };
        }

        public void Start()
        {
            deps.subscribeStopClient?.Invoke(Begin);
        }

        public void Begin()
        {
            if (running || deps.resultArrived() || deps.stillThisMatch() == false) return;
            RunAsync().Forget();
        }

        public void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                pausedAt = deps.now();
                return;
            }
            if (pausedAt < 0) return;
            double away = deps.now() - pausedAt;
            pausedAt = -1;
            if (away < ResumeReconnectSeconds) return;

            //  폰은 내려가 있는 동안 연결을 지켜 주지 않는다 — 10초 감지를 기다리지 않고 바로 다시 붙는다.
            deps.stopClient();
            Begin();
        }

        private async UniTaskVoid RunAsync()
        {
            running = true;
            deps.showOverlay();
            overlayShown = true;
            try
            {
                var outcome = await deps.runReconnect(cts.Token);
                switch (outcome)
                {
                    case ReconnectOutcome.Rejoin:
                        deps.beforeRejoin();
                        deps.fire(AppEvent.MatchRejoin);
                        break;
                    case ReconnectOutcome.MatchOver:
                        deps.resetSession();
                        deps.postLobbyNotice(MatchEndedNotice);
                        deps.fire(AppEvent.MatchLeft);
                        break;
                    case ReconnectOutcome.ResultArrived:
                        //  끊김이 오판이었다 — 기존 결과 흐름이 결과 화면으로 데려간다.
                        deps.resetSession();
                        HideOverlay();
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                //  경기 씬이 내려가는 중 — Dispose가 덮개를 닫는다.
            }
            catch (Exception e)
            {
                //  멈춘 화면보다 로비가 낫다 — 판이 진행 중이면 로비가 다시 넣어 준다.
                Debug.LogError($"[Reconnect] 재접속 중 예외: {e}");
                deps.resetSession();
                deps.postLobbyNotice(MatchEndedNotice);
                deps.fire(AppEvent.MatchLeft);
            }
            finally
            {
                running = false;
            }
        }

        private void HideOverlay()
        {
            if (overlayShown == false) return;
            overlayShown = false;
            deps.hideOverlay();
        }

        public void Dispose()
        {
            deps.unsubscribeStopClient?.Invoke(Begin);
            cts.Cancel();
            cts.Dispose();
            HideOverlay();
        }
    }
}
