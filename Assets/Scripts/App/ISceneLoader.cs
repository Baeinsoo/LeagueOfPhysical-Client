namespace LOP
{
    /// <summary>씬 로드 포트. FSM이 UnityEngine.SceneManagement static에 직접 묶이지 않게 한다.</summary>
    public interface ISceneLoader
    {
        void Load(string sceneName);
    }
}
