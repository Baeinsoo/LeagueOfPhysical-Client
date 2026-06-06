using GameFramework;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using VContainer;

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

            // 이 스코프가 속한 씬(로드 시 active가 됨)을 즉시 주입.
            InjectScene(SceneManager.GetActiveScene());

            // 이후 additive 등으로 로드되는 씬도 같은 스캔으로 커버한다.
            // (예: LOPGame이 런타임에 additive 로드하는 맵 씬의 [DIMonoBehaviour]/[DIGameObject])
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            base.OnDestroy();

            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 자기 씬은 Awake에서 이미 주입했으므로 중복 주입 방지.
            if (scene == gameObject.scene)
            {
                return;
            }

            InjectScene(scene);
        }

        // 해당 씬의 [DIGameObject]/[DIMonoBehaviour] 마킹 객체를 스캔해 주입한다.
        // 주입(Inject)만 수행하고 컨테이너 등록은 하지 않는다 — 대상의 [Inject] 멤버(필드/프로퍼티/메서드)만 채운다.
        private void InjectScene(Scene scene)
        {
            if (Container == null)
            {
                return;
            }

            foreach (var DIGameObject in scene.FindGameObjectsWithAttribute<DIGameObjectAttribute>().OrEmpty())
            {
                Container.InjectGameObject(DIGameObject);
            }

            foreach (var DIMonoBehaviour in scene.FindComponentsWithAttribute<DIMonoBehaviourAttribute>().OrEmpty())
            {
                Container.Inject(DIMonoBehaviour);
            }
        }
    }
}
