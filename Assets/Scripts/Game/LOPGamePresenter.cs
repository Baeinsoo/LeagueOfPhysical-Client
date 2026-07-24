using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.UI;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPGamePresenter : MonoGamePresenter<LOPRunner>
    {
        [Inject]
        private CameraController cameraController;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IWindowManager windowManager;

        private UIView gameLoadingView;
        private System.IDisposable gameInfoSubscription;

        private void Awake()
        {
            // LOPRunner은 이 프레젠터 GameObject의 자식("LOPGameEngine")에 있다.
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
            switch (gameState)
            {
                case RunnerState.Initialized:
                    gameLoadingView = windowManager.Open<GameLoadingView>();
                    break;

                case RunnerState.Playing:
                    if (gameLoadingView != null)
                    {
                        windowManager.Close(gameLoadingView);
                        gameLoadingView = null;
                    }
                    break;
            }
        }

        private async void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            await UniTask.WaitUntil(() => playerContext.actor != null && playerContext.actor.visualGameObject != null);

            cameraController.SetTarget(playerContext.actor.visualGameObject.transform);
        }
    }
}
