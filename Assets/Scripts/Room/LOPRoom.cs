using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPRoom : MonoBehaviour, IRoom
    {
        [Inject] private IGameFactory gameFactory;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IUserDataStore userDataStore;
        [Inject] private NetworkMessageDispatcher dispatcher;
        [Inject] private AppStateMachine appStateMachine;

        public IRunner runner { get; private set; }

        public bool initialized { get; private set; }

        private async void Awake()
        {
            try
            {
                await InitializeAsync();
                await ConnectRoomServerAsync();

                // 접속하자마자 시계 안정 대기를 시작해 gameInfo 왕복과 겹쳐 돌린다. 순차로 두면
                // 겹치지 않아 오히려 늦다 — 시작만 앞당기고 합류는 시드 직전에 한다.
                var clockSettle = WaitForClockSettleAsync();
                await JoinRoomServerAsync();
                await clockSettle;

                await StartGameAsync();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                appStateMachine.Fire(AppEvent.MatchEnded);
            }
        }

        private async void OnDestroy()
        {
            await DisconnectRoomServerAsync();
            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            var getMatch = await WebAPI.GetMatch(roomDataStore.room.matchId);
            if (getMatch.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"GetMatch Error. code: {getMatch.code}");
            }

            // 초기 엔티티 생성(GameInfoToC)이 JoinRoomServer에서 처리되기 전에 게임이 준비돼야 하므로 여기서 생성한다.
            runner = await gameFactory.CreateAsync();
            runner.onGameStateChanged += OnGameStateChanged;
            await runner.InitializeAsync();

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await runner.DeinitializeAsync();
            runner.onGameStateChanged -= OnGameStateChanged;

            await gameFactory.DestroyAsync();
            runner = null;

            roomDataStore.Clear();
            gameDataStore.Clear();

            initialized = false;
        }

        private async Task ConnectRoomServerAsync()
        {
            NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
            {
                dispatcher.Dispatch(message.payload);
            });

            if (EnvironmentSettings.active.UseLocalRoomInstance)
            {
                networkManager.networkAddress = EnvironmentSettings.active.LocalRoomHost;
                networkManager.port = EnvironmentSettings.active.LocalRoomPort;
            }
            else
            {
                networkManager.networkAddress = roomDataStore.room.ip;
                networkManager.port = roomDataStore.room.port;
            }

            //networkManager.onStartClient += () =>
            //{
            //};
            //networkManager.onStopClient += () =>
            //{
            //    SceneManager.LoadScene("Lobby");
            //};

            //  토큰 갱신을 여기서 끝낸다 — 접속 인증 콜백은 동기라 그 안에서는 기다릴 수 없다.
            await ((LOPNetworkAuthenticator)networkManager.authenticator).PrepareCredentialAsync(destroyCancellationToken);

            networkManager.StartClient();

            await UniTask.WaitUntil(() => NetworkClient.ready);
        }

        private async Task JoinRoomServerAsync()
        {
            CustomMirrorMessage message = new CustomMirrorMessage
            {
                payload = new GameInfoToS(),
            };
            NetworkClient.Send(message);

            await UniTask.WaitUntil(() => gameDataStore.gameInfo != null);
        }

        private async Task DisconnectRoomServerAsync()
        {
            networkManager.StopClient();

            await UniTask.WaitUntil(() => NetworkClient.ready == false);
        }

        // 서버 시간 추정이 자리를 잡을 때까지 기다린다.
        //
        // 왜 필요한가: Mirror는 접속 즉시 첫 핑을 보내는데 그 순간이 인증·씬·스폰으로 가장 바쁘다.
        // 실측상 첫 표본만 왕복 1052ms(이후 180~200ms)로 부풀고, Mirror의 평균은 첫 표본을 그대로
        // 채택한 뒤 한 번에 9.5%씩만 교정한다. 그 상태에서 출발선을 그으면 0.7초(35틱) 어긋난 채
        // 시작하고 시계는 초당 5%씩만 따라잡아 10초 넘게 어긋나 있다 — 입장 직후 러버밴딩의 원인.
        //
        // 판정: drift(예측시간 − 실시간)가 창 안에서 거의 안 변하면 추정이 멈춘 것 = 안정.
        // 진폭 5ms는 Mirror의 평균 계수(PredictionErrorWindowSize=20 → 표본당 9.5%)에서 유도했다 —
        // 0.5초(퐁 5개) 동안의 변화량이 남은 오차의 약 39%이므로, 진폭 5ms면 남은 오차는 13ms
        // 미만(1틱 미만)이다. Mirror가 그 상수를 바꾸면 임계값을 다시 유도해야 한다.
        private async Task WaitForClockSettleAsync()
        {
            const double WindowSeconds = 0.5;         // 창 0.5초 = 퐁 약 5개분 변화량(그래서 5ms 임계값이 1틱 미만 오차에 대응)
            const int MinSamples = 5;                 // 프레임이 정체돼 창 안 표본이 너무 적으면 진폭이 우연히 작게 나올 수 있어 막는다
            const double AmplitudeThreshold = 0.005;  // 5ms
            const double TimeoutSeconds = 7;

            var times = new Queue<double>();
            var drifts = new Queue<double>();
            double start = Time.unscaledTimeAsDouble;
            double amplitude = double.NaN;   // 창을 한 번도 못 채우면(극단적 프레임 정체) 측정된 적 없음을 값 자체로 표시
            double drift = 0;
            bool settled = false;
            bool disconnected = false;

            try
            {
                while (true)
                {
                    // 연결이 끊기면 7초를 헛되이 기다리지 않는다. 끊김 자체의 처리는 기존 흐름 소관.
                    if (NetworkClient.ready == false)
                    {
                        disconnected = true;
                        break;
                    }

                    double now = Time.unscaledTimeAsDouble;
                    double elapsed = now - start;

                    // 실시간 기준은 반드시 unscaledTime — Mirror의 localTime과 같아야 실시간 항이
                    // 상쇄돼 drift가 곧 "서버와의 시차"가 된다.
                    drift = runner.networkTime.PredictedTime - now;
                    times.Enqueue(now);
                    drifts.Enqueue(drift);

                    // 창 밖으로 나간 표본을 버린다. 방금 넣은 표본은 나이가 0이라 큐가 비지 않는다.
                    while (now - times.Peek() > WindowSeconds)
                    {
                        times.Dequeue();
                        drifts.Dequeue();
                    }

                    // RTT가 0이면 아직 퐁을 한 번도 못 받은 상태다. 그때는 predictedTime이 곧
                    // localTime이라 drift가 정확히 0으로 완벽하게 안 변해, 진폭만 보면 "안정"으로
                    // 오판한다 — 실제로는 아무것도 측정되지 않은 것이다.
                    if (elapsed >= WindowSeconds && drifts.Count >= MinSamples && runner.networkTime.Rtt > 0)
                    {
                        double min = double.MaxValue;
                        double max = double.MinValue;
                        foreach (double d in drifts)
                        {
                            if (d < min) min = d;
                            if (d > max) max = d;
                        }
                        amplitude = max - min;

                        if (amplitude < AmplitudeThreshold)
                        {
                            settled = true;
                            break;
                        }
                    }

                    if (elapsed >= TimeoutSeconds)
                    {
                        break;
                    }

                    await UniTask.Yield(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;   // 오브젝트가 사라지는 중 — 조용히 끝낸다
            }

            double total = Time.unscaledTimeAsDouble - start;
            string amplitudeText = double.IsNaN(amplitude) ? "미측정" : $"{amplitude:F4}";
            if (disconnected)
            {
                Debug.Log($"[ClockSettle] 연결 끊김 elapsed={total:F2}s");
            }
            else if (settled)
            {
                Debug.Log(
                    $"[ClockSettle] settled elapsed={total:F2}s amplitude={amplitudeText}" +
                    $" drift={drift:F3} window={drifts.Count}");
            }
            else
            {
                Debug.LogWarning(
                    $"[ClockSettle] TIMEOUT elapsed={total:F2}s amplitude={amplitudeText}" +
                    $" drift={drift:F3} window={drifts.Count} — 최선값으로 시작");
            }
        }

        public async Task StartGameAsync()
        {
            var gameInfo = gameDataStore.gameInfo;

            // 출발선을 제 위치(서버보다 앞)에 놓는다. gameInfo.Tick/ElapsedTime은 보낸 순간의 값이라
            // 받았을 땐 이미 과거다 — 지금 시계에서 유도한다.
            double target = ((LOPTickUpdater)runner.tickUpdater).TargetTime;
            runner.Run((long)(target / gameInfo.Interval), gameInfo.Interval, target);
        }

        private void OnGameStateChanged(RunnerState gameState)
        {
            switch (gameState)
            {
                case RunnerState.GameOver:
                    Debug.Log("Game Over");
                    appStateMachine.Fire(AppEvent.MatchEnded);
                    break;
            }
        }
    }
}
