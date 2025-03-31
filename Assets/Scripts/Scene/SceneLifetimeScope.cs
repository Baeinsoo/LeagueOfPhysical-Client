using GameFramework;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace LOP
{
    public class SceneLifetimeScope : LifetimeScope
    {
        public static SceneLifetimeScope instance { get; private set; }

        public static void Inject(object obj)
        {
            instance.Container.Inject(obj);
        }

        protected override void Awake()
        {
            base.Awake();

            instance = this;

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

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
