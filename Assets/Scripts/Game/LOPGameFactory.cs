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
        private Scene loadedScene;

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

            //  경기 씬을 다시 띄울 때 옛 정리가 새로 올라온 같은 경로의 씬을 내리지 않게, 내가 올린 씬 핸들을 쥔다.
            loadedScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
            if (loadedScene.path != loadedScenePath)
            {
                UnityEngine.Debug.LogError($"[LOPGameFactory] 마지막 씬이 방금 올린 게임 씬이 아니다: {loadedScene.path} (기대 {loadedScenePath})");
                loadedScene = SceneManager.GetSceneByPath(loadedScenePath);
            }

            var gameScope = LifetimeScope.Find<GameLifetimeScope>();
            return gameScope.Container.Resolve<IRunner>();
        }

        public async Task DestroyAsync()
        {
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                await SceneManager.UnloadSceneAsync(loadedScene).ToUniTask();
            }

            loadedScene = default;
            loadedScenePath = null;
        }

        private string ResolveGameScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var roundIndex = MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0);
            var round = rounds[roundIndex];
            var gameMode = MatchSceneResolver.RequireRow(
                "TbGameMode", round.gameModeId, masterData.Tables.TbGameMode.GetOrDefault(round.gameModeId));

            return MatchSceneResolver.RequireScenePath("TbGameMode", round.gameModeId, gameMode.ScenePath);
        }
    }
}
