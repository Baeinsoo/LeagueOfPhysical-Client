using Cysharp.Threading.Tasks;
using GameFramework;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 게임 씬을 Room 스코프의 자식으로 additive 로드해 game을 생성한다.
    /// </summary>
    public class LOPGameFactory : IGameFactory
    {
        private const string GameSceneName = "LOPGame";

        public async Task<IGame> CreateAsync()
        {
            var roomScope = LifetimeScope.Find<RoomLifetimeScope>();

            using (LifetimeScope.EnqueueParent(roomScope))
            {
                await SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Additive).ToUniTask();
            }

            var gameScope = LifetimeScope.Find<GameLifetimeScope>();
            return gameScope.Container.Resolve<IGame>();
        }

        public async Task DestroyAsync()
        {
            var scene = SceneManager.GetSceneByName(GameSceneName);
            if (scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();
            }
        }
    }
}
