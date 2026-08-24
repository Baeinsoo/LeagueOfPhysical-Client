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
            // 판치기처럼 아바타가 없는 모드는 visualGameObject가 끝내 생기지 않는다 —
            // 취소 토큰 없이 기다리면 씬이 사라진 뒤에도 매 프레임 계속 도는 델리게이트가 남는다.
            try
            {
                await UniTask.WaitUntil(() => playerContext.actor != null && playerContext.actor.visualGameObject != null, cancellationToken: destroyCancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                return;   // 오브젝트가 사라지는 중 — 조용히 끝낸다
            }

            cameraController.SetTarget(playerContext.actor.visualGameObject.transform);
        }
    }
}
