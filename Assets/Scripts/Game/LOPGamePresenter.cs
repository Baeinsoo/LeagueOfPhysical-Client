using Cysharp.Threading.Tasks;
using GameFramework;
using Mirror;
using System.Collections;
using System.Collections.Generic;
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
        private IGameDataContext gameDataContext;

        [Inject]
        private IMessageDispatcher messageDispatcher;

        [Inject]
        private IPlayerContext playerContext;

        private void Awake()
        {
            game = GetComponent<LOPGame>();
            game.onGameStateChanged += OnGameStateChanged;

            messageDispatcher.RegisterHandler<GameInfoToC>(OnGameInfoToC, LOPRoomMessageInterceptor.Default);
        }

        private void OnDestroy()
        {
            game.onGameStateChanged -= OnGameStateChanged;
            game = null;

            messageDispatcher.UnregisterHandler<GameInfoToC>(OnGameInfoToC);
        }

        private void OnGameStateChanged(IGameState gameState)
        {
            switch (gameState)
            {
                case Initialized:
                    break;

                case Playing:
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
