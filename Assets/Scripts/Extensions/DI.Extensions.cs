using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    public static partial class Extensions
    {
        public static T AddComponentWithDI<T>(this GameObject self) where T : Component
        {
            return AddComponentWithDI<T>(self, SceneLifetimeScope.instance);
        }

        public static T AddComponentWithDI<T>(this GameObject self, LifetimeScope scope) where T : Component
        {
            var component = self.AddComponent<T>();
            scope.Container.Inject(component);
            return component;
        }
    }
}
