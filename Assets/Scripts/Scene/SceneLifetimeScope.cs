using VContainer;
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

        public static T Resolve<T>()
        {
            return instance.Container.Resolve<T>();
        }

        protected override void Awake()
        {
            base.Awake();

            instance = this;

            Container.InjectSceneObjects(gameObject.scene);
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
