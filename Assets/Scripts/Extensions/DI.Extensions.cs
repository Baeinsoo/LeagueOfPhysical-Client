using UnityEngine;
using VContainer;

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
    }
}
