using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using LOP.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPGameSceneCoordinator : MonoBehaviour
    {
        [Inject]
        private CameraController cameraController;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private MatchLoadingViewModel matchLoadingViewModel;

        private LOPRunner runner;
        private System.IDisposable gameInfoSubscription;

        private void Awake()
        {
            // LOPRunner은 이 코디네이터 GameObject의 자식("LOPGameEngine")에 있다.
            runner = GetComponentInChildren<LOPRunner>();
            runner.onGameStateChanged += OnGameStateChanged;

            // MonoBehaviour가 Awake에서 구독 — 주입 타이밍 의존을 피해 GlobalMessagePipe로 구독(구 정적 버스와 동형).
            gameInfoSubscription = GlobalMessagePipe.GetSubscriber<GameInfoToC>().Subscribe(OnGameInfoToC);
        }

        private void OnDestroy()
        {
            runner.onGameStateChanged -= OnGameStateChanged;
            runner = null;

            gameInfoSubscription?.Dispose();
        }

        private void OnGameStateChanged(RunnerState gameState)
        {
            // 로딩 화면은 룸 연결 시점(위치=GameRoom)부터 이미 떠 있다.
            // 게임이 실제로 시작되면 그 사실만 보고하고, 창을 내리는 판단은 VM/코디네이터가 한다.
            if (gameState == RunnerState.Playing)
            {
                matchLoadingViewModel.NotifyGameLive();
            }
        }

        private async void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            await UniTask.WaitUntil(() => playerContext.actor != null && playerContext.actor.visualGameObject != null);

            cameraController.SetTarget(playerContext.actor.visualGameObject.transform);
        }
    }
}
