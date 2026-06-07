using GameFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    public static partial class Extensions
    {
        public static T GetOrAddComponentWithInject<T>(this GameObject self) where T : Component
        {
            return GetOrAddComponentWithInject<T>(self, SceneLifetimeScope.instance.Container);
        }

        public static T GetOrAddComponentWithInject<T>(this GameObject self, IObjectResolver objectResolver) where T : Component
        {
            T component = self.GetComponent<T>();
            if (component == null)
            {
                component = self.AddComponent<T>();
                objectResolver.Inject(component);
            }
            return component;
        }

        /// <summary>
        /// 한 씬의 [DIGameObject]/[DIMonoBehaviour] 마킹 객체를 주어진 컨테이너로 주입한다.
        /// </summary>
        public static void InjectSceneObjects(this IObjectResolver resolver, Scene scene)
        {
            foreach (var DIGameObject in scene.FindGameObjectsWithAttribute<DIGameObjectAttribute>().OrEmpty())
            {
                resolver.InjectGameObject(DIGameObject);
            }

            foreach (var DIMonoBehaviour in scene.FindComponentsWithAttribute<DIMonoBehaviourAttribute>().OrEmpty())
            {
                resolver.Inject(DIMonoBehaviour);
            }
        }
    }
}
