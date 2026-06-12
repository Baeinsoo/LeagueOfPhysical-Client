using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.UI;
using UnityEngine;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPGamePresenter : MonoGamePresenter<LOPGame>
    {
        [Inject]
        private CameraController cameraController;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IWindowManager windowManager;

        private UIView gameLoadingView;

        private void Awake()
        {
            game = GetComponent<LOPGame>();
            game.onGameStateChanged += OnGameStateChanged;
            
            EventBus.Default.Subscribe<GameInfoToC>(nameof(IMessage), OnGameInfoToC);
        }

        private void OnDestroy()
        {
            game.onGameStateChanged -= OnGameStateChanged;
            game = null;

            EventBus.Default.Unsubscribe<GameInfoToC>(nameof(IMessage), OnGameInfoToC);
        }

        private void OnGameStateChanged(IGameState gameState)
        {
            switch (gameState)
            {
                case Initialized:
                    gameLoadingView = windowManager.Open<GameLoadingView>();
                    break;

                case Playing:
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
            await UniTask.WaitUntil(() => playerContext.entityView != null && playerContext.entityView.visualGameObject != null);

            cameraController.SetTarget(playerContext.entityView.visualGameObject.transform);
        }
    }
}
