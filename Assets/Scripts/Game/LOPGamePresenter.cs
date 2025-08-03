using Cysharp.Threading.Tasks;
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPGamePresenter : MonoGamePresenter<LOPGame>
    {
        [SerializeField] private CharacterUI characterUI;

        [Inject]
        private CameraController cameraController;

        [Inject]
        private IPlayerContext playerContext;

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
                    break;

                case Playing:
                    break;
            }
        }

        private async void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            await UniTask.WaitUntil(() => playerContext.entityView != null && playerContext.entityView.visualGameObject != null);

            cameraController.SetTarget(playerContext.entityView.visualGameObject.transform);
            characterUI.SetEntity(playerContext.entity);
        }
    }
}
