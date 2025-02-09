using GameFramework;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace LOP
{
    public class SceneLifetimeScope : LifetimeScope
    {
        protected override void Awake()
        {
            base.Awake();

            var activeScene = SceneManager.GetActiveScene();

            var DIGameObjects = activeScene.FindGameObjectsWithAttribute<DIGameObjectAttribute>();
            foreach (var DIGameObject in DIGameObjects.OrEmpty())
            {
                Container.InjectGameObject(DIGameObject);
            }

            var DIMonoBehaviours = activeScene.FindComponentsWithAttribute<DIMonoBehaviourAttribute>();
            foreach (var DIMonoBehaviour in DIMonoBehaviours.OrEmpty())
            {
                Container.Inject(DIMonoBehaviour);
            }
        }
    }
}
