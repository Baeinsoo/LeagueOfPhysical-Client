using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class SceneResolver : MonoSingleton<SceneResolver>
    {
        private SceneResolver() { }

        private IObjectResolver _resolver;
        private IObjectResolver resolver => _resolver ?? (_resolver = GameObject.FindObjectOfType<SceneLifetimeScope>().Container);

        public T Resolve<T>()
        {
            return resolver.Resolve<T>();
        }
    }
}
