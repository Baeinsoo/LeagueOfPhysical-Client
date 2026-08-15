using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 이번 판의 게임 씬을 Room 스코프의 자식으로 additive 로드해 game을 생성한다.
    /// 어떤 씬인지는 매치가 정한 게임 모드에서 온다 — 게임마다 다른 씬이 통째로 올라온다.
    /// </summary>
    public class LOPGameFactory : IGameFactory
    {
        private readonly IRoomDataStore roomDataStore;
        private readonly LOP.MasterData.LOPMasterData masterData;

        private string loadedScenePath;

        public LOPGameFactory(IRoomDataStore roomDataStore, LOP.MasterData.LOPMasterData masterData)
        {
            this.roomDataStore = roomDataStore;
            this.masterData = masterData;
        }

        public async Task<IRunner> CreateAsync()
        {
            loadedScenePath = ResolveGameScenePath();

            var roomScope = LifetimeScope.Find<RoomLifetimeScope>();

            using (LifetimeScope.EnqueueParent(roomScope))
            {
                await SceneManager.LoadSceneAsync(loadedScenePath, LoadSceneMode.Additive).ToUniTask();
            }

            var gameScope = LifetimeScope.Find<GameLifetimeScope>();
            return gameScope.Container.Resolve<IRunner>();
        }

        public async Task DestroyAsync()
        {
            if (string.IsNullOrEmpty(loadedScenePath))
            {
                return;
            }

            var scene = SceneManager.GetSceneByPath(loadedScenePath);
            if (scene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();
            }

            loadedScenePath = null;
        }

        private string ResolveGameScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var round = rounds[MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0)];
            var gameMode = masterData.Tables.TbGameMode.GetOrDefault(round.gameModeId);

            return MatchSceneResolver.RequireScenePath("TbGameMode", round.gameModeId, gameMode?.ScenePath);
        }
    }
}
