using UnityEngine.SceneManagement;

namespace LOP
{
    public class SceneLoader : ISceneLoader
    {
        public void Load(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
